#!/usr/bin/env python3
"""Independent, read-only verification of frozen 069 source/client/gallery ZIPs.

Usage:
  python3 independent-zip-verify-069.py --self-test
  python3 independent-zip-verify-069.py --manifest release-manifest.json \
      --package-dir /absolute/Candidate2 --repo /absolute/project \
      --payload-approval /absolute/Packaging/payload-approval-069.json \
      --output /absolute/Packaging/independent-zip-verification-069.json

Only the exclusive receipt is written in formal mode. No packaging helper is
imported or executed. Git objects bind the complete source allowlist. A separate
frozen approval binds the complete lstat application tree, approved client extras
and exact selected gallery files. Every member is streamed for CRC/SHA/size,
Unix modes, types, paths and confined relative symlinks. No LAN schema is assumed.
Self-tests use memory ZIP fixtures and automatically removed local filesystem
fixtures; they create no ZIP files or commits and inspect no release artifact.
"""

import argparse
import hashlib
import io
import json
import os
import plistlib
from pathlib import Path
import re
import stat
import struct
import subprocess
import sys
import tempfile
import time
import unicodedata
import zipfile
import zlib


VERSION = "0.12.0"
RELEASE = "069"
EXPECTED_RUNTIME_COMMIT = "28cd1b3191fafd49417d544e60dc898986568aa8"
EXPECTED_INPUTS = 664
EXPECTED_FINGERPRINT = "4cbc889a5ba3d706768a2b568af42dc4ec8b4cf0d60e7b45a4cc706d9ddab3ad"
CHUNK = 1024 * 1024
MAX_MEMBER_BYTES = 512 * 1024 * 1024
MAX_PACKAGE_RAW_BYTES = 2 * 1024 * 1024 * 1024
MAX_MEMBERS = 100000
ALLOW_ROOTS = {"Assets", "Packages", "ProjectSettings", "Docs", "SourceAssets", "Tools"}
ALLOW_FILES = {"README.md", ".gitignore"}
INPUT_ROOTS = {"Assets", "Packages", "ProjectSettings", "Tools"}
FORBIDDEN_COMPONENTS = {"Library", "Builds", "outputs", "Temp", "obj",
                        "UserSettings", ".git", "__pycache__", "Saves", "Account", "Preferences"}
HEX256 = re.compile(r"^[0-9a-f]{64}$")
GIT_OBJECT = re.compile(r"^[0-9a-f]{40}(?:[0-9a-f]{24})?$")
GUID = re.compile(r"^[0-9a-f]{32}$")


class VerificationError(ValueError):
    pass


def need(condition, message):
    if not condition:
        raise VerificationError(message)


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        need(key not in result, "duplicate JSON property")
        result[key] = value
    return result


def stat_identity(item):
    return (item.st_dev, item.st_ino, item.st_mode, item.st_size, item.st_mtime_ns, item.st_ctime_ns)


def regular_stream(path):
    original = path.lstat()
    need(stat.S_ISREG(original.st_mode), "input is not a regular file: " + str(path))
    descriptor = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
    stream = os.fdopen(descriptor, "rb")
    try:
        need(stat_identity(os.fstat(stream.fileno())) == stat_identity(original), "input identity raced before read")
    except Exception:
        stream.close()
        raise
    return stream, original


def finish_read(path, stream, original):
    need(stat_identity(os.fstat(stream.fileno())) == stat_identity(original), "input changed during read")
    need(stat_identity(path.lstat()) == stat_identity(original), "input path changed during read")


def read_json(path):
    stream, original = regular_stream(path)
    with stream:
        raw = stream.read(64 * 1024 * 1024 + 1)
        need(len(raw) <= 64 * 1024 * 1024, "JSON exceeds 64 MiB")
        finish_read(path, stream, original)
    return json.loads(raw.decode("utf-8", "strict"), object_pairs_hook=unique_object,
                      parse_constant=lambda _: (_ for _ in ()).throw(VerificationError("nonfinite JSON number"))), raw


def hash_bytes(raw):
    return hashlib.sha256(raw).hexdigest()


def file_hash(path):
    digest = hashlib.sha256()
    count = 0
    stream, original = regular_stream(path)
    with stream:
        while True:
            data = stream.read(CHUNK)
            if not data:
                break
            count += len(data)
            digest.update(data)
        finish_read(path, stream, original)
    return count, digest.hexdigest()


def integer(value, label, lower=0, upper=None):
    need(type(value) is int and value >= lower and (upper is None or value <= upper), label + " is invalid")
    return value


def sha(value, label):
    need(isinstance(value, str) and bool(HEX256.fullmatch(value)), label + " is invalid")
    return value


def safe_name(name, directory=False):
    need(isinstance(name, str) and name and len(name.encode("utf-8", "strict")) <= 4096,
         "invalid member path length/type")
    need(not name.startswith("/") and "\\" not in name and not re.match(r"^[A-Za-z]:", name),
         "absolute or platform-ambiguous member path")
    need(all(ord(c) >= 32 and ord(c) != 127 for c in name), "control character in member path")
    need(name.endswith("/") == directory, "member directory suffix disagrees with kind")
    parts = name[:-1].split("/") if directory else name.split("/")
    need(all(p and p not in (".", "..") for p in parts), "noncanonical or traversing member path")
    return parts


def safe_link(name, raw):
    need(0 < len(raw) <= 4096, "symlink target length is invalid")
    target = raw.decode("utf-8", "strict")
    need(not target.startswith("/") and "\\" not in target and not re.match(r"^[A-Za-z]:", target),
         "absolute or platform-ambiguous symlink target")
    need(all(ord(c) >= 32 and ord(c) != 127 for c in target), "control character in symlink target")
    base = safe_name(name)[:-1]
    # A link can traverse within its own package root, but never leave that root.
    for part in target.split("/"):
        if part in ("", "."):
            continue
        if part == "..":
            need(len(base) > 1, "symlink escapes package root")
            base.pop()
        else:
            base.append(part)
    need(len(base) >= 2, "symlink points outside package payload")
    return target


def kind_for_mode(mode):
    return {stat.S_IFREG: "file", stat.S_IFDIR: "directory", stat.S_IFLNK: "symlink"}.get(stat.S_IFMT(mode))


def eligible_source(path):
    parts = path.split("/")
    if parts[0] not in ALLOW_ROOTS and path not in ALLOW_FILES:
        return False
    if any(p in FORBIDDEN_COMPONENTS for p in parts):
        return False
    if parts[-1].startswith(".") and path not in ALLOW_FILES:
        return False
    if len(parts) >= 2 and parts[:2] == ["Docs", "Evidence"]:
        return len(parts) >= 4 and parts[2] == RELEASE
    return True


def is_input(path):
    parts = path.split("/")
    return parts[0] in INPUT_ROOTS and not any(p.startswith(".") or p == "__pycache__" for p in parts)


def git_tree(repo, commit):
    need(isinstance(commit, str) and bool(GIT_OBJECT.fullmatch(commit)), "Git commit ID is invalid")
    process = subprocess.run(["git", "-C", str(repo), "ls-tree", "-rz", "--full-tree", commit],
                             stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False)
    need(process.returncode == 0, "cannot read specified Git tree")
    result = {}
    for item in process.stdout.split(b"\0"):
        if not item:
            continue
        meta, raw_path = item.split(b"\t", 1)
        mode, kind, oid = meta.decode("ascii").split(" ")
        path = raw_path.decode("utf-8", "strict")
        safe_name(path)
        need(path not in result, "duplicate Git tree path")
        result[path] = {"mode": int(mode, 8), "kind": kind, "oid": oid}
    return result


def git_hashes(repo, tree, paths):
    # One persistent cat-file process, independently hash both raw bytes and Git
    # object header+bytes. Never render object content or process diagnostics.
    result = {}
    cache = {}
    process = subprocess.Popen(["git", "-C", str(repo), "cat-file", "--batch"],
                               stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
    try:
        for path in sorted(paths):
            entry = tree[path]
            need(entry["kind"] == "blob", "source includes unsupported Git object: " + path)
            oid = entry["oid"]
            if oid not in cache:
                process.stdin.write((oid + "\n").encode("ascii"))
                process.stdin.flush()
                header = process.stdout.readline().decode("ascii", "strict").strip().split(" ")
                need(len(header) == 3 and header[0] == oid and header[1] == "blob", "invalid Git blob response")
                size = int(header[2])
                need(0 <= size <= MAX_MEMBER_BYTES, "Git blob exceeds member bound")
                raw_hash = hashlib.sha256()
                object_hash = hashlib.sha1() if len(oid) == 40 else hashlib.sha256()
                object_hash.update(("blob " + str(size) + "\0").encode("ascii"))
                remaining = size
                while remaining:
                    chunk = process.stdout.read(min(CHUNK, remaining))
                    need(bool(chunk), "truncated Git blob response")
                    remaining -= len(chunk)
                    raw_hash.update(chunk)
                    object_hash.update(chunk)
                need(process.stdout.read(1) == b"\n", "invalid Git blob delimiter")
                need(object_hash.hexdigest() == oid, "Git blob object digest mismatch")
                cache[oid] = {"bytes": size, "sha256": raw_hash.hexdigest()}
            result[path] = dict(cache[oid], mode=entry["mode"], oid=oid)
        process.stdin.close()
        need(process.wait(timeout=10) == 0, "Git blob reader did not complete")
    finally:
        if process.poll() is None:
            process.kill()
            process.wait(timeout=10)
        if process.stdin and not process.stdin.closed:
            process.stdin.close()
        process.stdout.close()
    return result


def validate_header(manifest):
    need(isinstance(manifest, dict) and type(manifest.get("schema")) is int and manifest["schema"] == 1, "unsupported manifest schema")
    need(manifest.get("version") == VERSION, "unexpected release version")
    need(manifest.get("release") == RELEASE, "unexpected evidence release")
    for key in ("runtimeCommit", "sourceSnapshotCommit"):
        need(isinstance(manifest.get(key), str) and bool(GIT_OBJECT.fullmatch(manifest[key])), "invalid " + key)
    sha(manifest.get("sourceFingerprint"), "source fingerprint")
    integer(manifest.get("sourceInputs"), "source input count", 1, MAX_MEMBERS)
    need(isinstance(manifest.get("buildGuid"), str) and bool(GUID.fullmatch(manifest["buildGuid"])), "invalid build GUID")
    tests = manifest.get("tests")
    need(isinstance(tests, dict) and tests.get("result") == "Passed", "tests are not marked Passed")
    need(isinstance(tests.get("run"), str) and re.fullmatch(r"run-[A-Za-z0-9]+", tests["run"]), "invalid test run")
    counts = [integer(tests.get(key), "test " + key, 1) for key in ("EditMode", "PlayMode", "total")]
    need(counts[0] + counts[1] == counts[2], "test totals disagree")
    packages = manifest.get("packages")
    need(isinstance(packages, dict) and set(packages) == {"source", "client", "gallery"}, "expected exactly three packages")
    need(all(isinstance(value, dict) for value in packages.values()), "invalid package record")


def inventory(package):
    name = package.get("file")
    need(isinstance(name, str) and safe_name(name) == [name] and name.endswith(".zip"), "invalid ZIP basename")
    integer(package.get("bytes"), "ZIP bytes", 1)
    sha(package.get("sha256"), "ZIP SHA")
    members = package.get("members")
    need(isinstance(members, list) and 0 < len(members) <= MAX_MEMBERS, "invalid member inventory")
    result = {}
    for member in members:
        need(isinstance(member, dict), "invalid member record")
        kind = member.get("kind")
        need(kind in ("file", "directory", "symlink"), "unsupported member kind")
        member_name = member.get("name")
        safe_name(member_name, kind == "directory")
        need(member_name not in result, "duplicate manifest member path")
        mode = integer(member.get("mode"), "member mode", 0, 0o177777)
        need(kind_for_mode(mode) == kind, "member type disagrees with POSIX mode")
        need(not (mode & (stat.S_ISUID | stat.S_ISGID)), "setuid/setgid package member")
        size = integer(member.get("bytes"), "member bytes", 0, MAX_MEMBER_BYTES)
        sha(member.get("sha256"), "member SHA")
        if kind == "directory":
            need(size == 0 and member["sha256"] == hash_bytes(b""), "directory contains payload")
        result[member_name] = member
    need(sum(m["bytes"] for m in members) <= MAX_PACKAGE_RAW_BYTES, "package exceeds raw byte bound")
    # Reject prefix collisions: a file/symlink cannot be an ancestor directory.
    for member_name in result:
        parts = member_name.rstrip("/").split("/")
        for i in range(1, len(parts)):
            ancestor = "/".join(parts[:i])
            need(ancestor not in result, "file/symlink used as ancestor directory")
    return result


def validate_scope(package_kind, members):
    roots = {safe_name(n, m["kind"] == "directory")[0] for n, m in members.items()}
    need(len(roots) == 1, "package has multiple top-level roots")
    root = next(iter(roots))
    if package_kind == "source":
        need(root == "FrontierReckoning", "unexpected source package prefix")
    elif package_kind == "client":
        need(root == "Frontier-Reckoning-" + VERSION, "unexpected client package prefix")
    for name, member in members.items():
        parts = safe_name(name, member["kind"] == "directory")
        tail = parts[1:]
        need(not any(p in FORBIDDEN_COMPONENTS for p in tail), "forbidden cache/personal-storage member")
        need(not any(p in ("FrozenCandidate1", "FrozenCandidate2", "Candidate1", "Candidate2") for p in tail),
             "historical candidate payload in current package")
        if package_kind == "source" and member["kind"] != "directory":
            relative = "/".join(tail)
            need(eligible_source(relative), "source member is outside release allowlist")
            need(member.get("gitPath", relative) == relative, "source gitPath disagrees with member path")
            need(not any(p.endswith(".app") for p in tail), "application payload in source package")
        elif package_kind == "source" and tail:
            need(tail[0] in ALLOW_ROOTS, "source directory is outside allowlist")
            if tail[:2] == ["Docs", "Evidence"] and len(tail) >= 3:
                need(tail[2] == RELEASE, "other evidence release directory in source")
        if package_kind == "gallery":
            need(not any(p.endswith(".app") for p in tail), "application payload in gallery")
            need("Diagnostics" not in tail, "historical diagnostic payload in gallery")
            if "Build" in tail:
                i = tail.index("Build")
                # Native/Build holds identity receipts, not a complete application.
                need(i > 0 and tail[i - 1] == "Native", "unrecognized build payload in gallery")
                need((member["kind"] == "directory" and len(tail) == i + 1)
                     or (member["kind"] == "file" and len(tail) == i + 2
                         and tail[-1] in ("boot.config", "build-identity.json", "client-build.json")),
                     "non-identity build payload in gallery")
            if "Evidence" in tail:
                i = tail.index("Evidence")
                need(i + 1 < len(tail) and tail[i + 1] == RELEASE, "other evidence release in gallery")
    return root


def zip_extras(raw, label):
    fields = {}
    offset = 0
    while offset < len(raw):
        need(len(raw) - offset >= 4, label + " has truncated extra-field header")
        key, length = struct.unpack_from("<HH", raw, offset)
        offset += 4
        need(length <= len(raw) - offset, label + " has truncated extra-field body")
        need(key == 0x0001 and key not in fields, label + " has unsupported or duplicate extra field")
        fields[key] = raw[offset:offset + length]
        offset += length
    return fields


def validate_zip_headers(archive, entry):
    # Alternative Unicode-path fields can override a benign central filename in
    # native unzip implementations. The controlled packager needs only ZIP64.
    zip_extras(entry.extra, "ZIP central header")
    need(entry.flag_bits & ~0x0800 == 0, "unsupported ZIP flags or data descriptor")
    need(not entry.comment, "unexpected ZIP member comment")
    position = archive.fp.tell()
    try:
        need(0 <= entry.header_offset < archive.start_dir, "ZIP local header lies outside data area")
        archive.fp.seek(entry.header_offset)
        raw = archive.fp.read(30)
        need(len(raw) == 30, "truncated ZIP local header")
        signature, version, flags, method, mod_time, mod_date, crc, compressed, size, namesize, extrasize = struct.unpack("<4s5H3I2H", raw)
        need(signature == b"PK\x03\x04", "invalid ZIP local header signature")
        need(version == entry.extract_version | (entry.reserved << 8), "ZIP local/central required version differs")
        need(flags == entry.flag_bits and method == entry.compress_type, "ZIP local/central flags or compression differs")
        date = entry.date_time
        expected_time = date[3] << 11 | date[4] << 5 | date[5] // 2
        expected_date = (date[0] - 1980) << 9 | date[1] << 5 | date[2]
        need(mod_time == expected_time and mod_date == expected_date, "ZIP local/central timestamp differs")
        need(crc == entry.CRC, "ZIP local/central CRC differs")
        local_name = archive.fp.read(namesize)
        raw_extra = archive.fp.read(extrasize)
        need(len(local_name) == namesize and len(raw_extra) == extrasize, "truncated ZIP local name or extra")
        need(local_name.decode("utf-8" if flags & 0x0800 else "cp437", "strict") == entry.filename,
             "ZIP local/central filename differs")
        extras = zip_extras(raw_extra, "ZIP local header")
        if size == 0xffffffff or compressed == 0xffffffff:
            large = extras.get(0x0001)
            required = 8 * (int(size == 0xffffffff) + int(compressed == 0xffffffff))
            need(large is not None and len(large) == required, "ZIP local ZIP64 size payload is invalid")
            at = 0
            if size == 0xffffffff:
                size = struct.unpack_from("<Q", large, at)[0]
                at += 8
            if compressed == 0xffffffff:
                compressed = struct.unpack_from("<Q", large, at)[0]
        elif extras:
            # Some macOS Python versions retain a redundant ZIP64 local extra
            # after backfilling the small 32-bit sizes. Its values must agree.
            large = extras.get(0x0001)
            need(large is not None and len(large) == 16 and struct.unpack("<QQ", large) == (size, compressed),
                 "redundant local ZIP64 sizes differ from header")
        need(size == entry.file_size and compressed == entry.compress_size, "ZIP local/central byte sizes differ")
        need(archive.fp.tell() + compressed <= archive.start_dir, "ZIP payload overlaps central directory")
    finally:
        archive.fp.seek(position)


def verify_archive(archive, package_kind, members):
    entries = archive.infolist()
    names = [entry.filename for entry in entries]
    need(len(entries) == len(members) and len(set(names)) == len(names) and set(names) == set(members),
         package_kind + " ZIP member set mismatch")
    need(not archive.comment, "unexpected archive comment")
    total = 0
    links = {}
    kind_counts = {"file": 0, "directory": 0, "symlink": 0}
    for entry in entries:
        member = members[entry.filename]
        validate_zip_headers(archive, entry)
        need(entry.create_system == 3, "ZIP member lacks Unix mode semantics")
        need(entry.external_attr >> 16 == member["mode"], "ZIP POSIX mode mismatch")
        need(entry.is_dir() == (member["kind"] == "directory"), "ZIP member type/suffix mismatch")
        need(not entry.flag_bits & 1, "encrypted ZIP member")
        need(entry.compress_type in (zipfile.ZIP_STORED, zipfile.ZIP_DEFLATED), "unsupported ZIP compression")
        need(entry.file_size == member["bytes"], "ZIP member declared size mismatch")
        need(entry.orig_filename == entry.filename, "ZIP member contains truncated path")
        digest_member = hashlib.sha256()
        count = 0
        link_body = bytearray() if member["kind"] == "symlink" else None
        with archive.open(entry, "r") as stream:
            while True:
                chunk = stream.read(CHUNK)
                if not chunk:
                    break
                count += len(chunk)
                need(count <= member["bytes"], "ZIP member exceeds expected bytes")
                digest_member.update(chunk)
                if link_body is not None:
                    need(count <= 4096, "symlink target exceeds bound")
                    link_body.extend(chunk)
        # zipfile verifies the CRC when the entire member stream has been read.
        need(count == member["bytes"] and digest_member.hexdigest() == member["sha256"], "ZIP member byte/SHA mismatch")
        if link_body is not None:
            links[entry.filename] = safe_link(entry.filename, bytes(link_body))
        total += count
        kind_counts[member["kind"]] += 1
    validate_link_graph(members, links)
    return total, kind_counts


def verify_zip(path, package_kind, package):
    members = inventory(package)
    root = validate_scope(package_kind, members)
    size, digest = file_hash(path)
    need(size == package["bytes"] and digest == package["sha256"], package_kind + " whole ZIP identity mismatch")
    stream, original = regular_stream(path)
    with stream:
        with zipfile.ZipFile(stream, "r") as archive:
            total, kind_counts = verify_archive(archive, package_kind, members)
        finish_read(path, stream, original)
    need(file_hash(path) == (size, digest), "ZIP changed during independent verification")
    return {"file": path.name, "bytes": size, "sha256": digest, "members": len(members),
            "memberKinds": kind_counts, "memberRawBytes": total, "prefix": root,
            "memberBytesShaModesTypesCrcAndPaths": "Passed"}, members


def canonical_key(name):
    return unicodedata.normalize("NFC", name.rstrip("/")).casefold()


def validate_aliases(members):
    aliases = {}
    for name in members:
        key = canonical_key(name)
        need(key not in aliases, "case/Unicode or file/directory alias in inventory")
        aliases[key] = name
    # Parent aliases can also escape a simple full-path duplicate check.
    spellings = {}
    for name in members:
        parts = name.rstrip("/").split("/")
        for depth in range(1, len(parts) + 1):
            spelling = "/".join(parts[:depth])
            key = canonical_key(spelling)
            need(key not in spellings or spellings[key] == spelling,
                 "case/Unicode alias in member ancestry")
            spellings[key] = spelling


def validate_link_graph(members, links):
    paths = {name.rstrip("/"): member for name, member in members.items()}
    implicit_dirs = set()
    for path in paths:
        parts = path.split("/")
        implicit_dirs.update("/".join(parts[:i]) for i in range(1, len(parts)))
    for name, target in links.items():
        pending = name.split("/")[:-1] + target.split("/")
        resolved = []
        substitutions = 0
        while pending:
            part = pending.pop(0)
            if part in ("", "."):
                continue
            if part == "..":
                need(len(resolved) > 1, "chained symlink escapes package root")
                resolved.pop()
                continue
            resolved.append(part)
            path = "/".join(resolved)
            if path in links:
                substitutions += 1
                need(substitutions <= 64, "cyclic or excessive chained symlink")
                resolved.pop()
                pending = links[path].split("/") + pending
            elif pending:
                need(path in implicit_dirs or paths.get(path, {}).get("kind") == "directory",
                     "symlink traverses nonexistent or regular-file ancestor")
        final = "/".join(resolved)
        need(len(resolved) >= 2 and (final in paths or final in implicit_dirs),
             "dangling or out-of-payload symlink")


def member_record(path, name):
    original = path.lstat()
    kind = kind_for_mode(original.st_mode)
    need(kind is not None, "unsupported lstat object: " + str(path))
    mode = original.st_mode & 0xffff
    need(not mode & 0o6000, "setuid/setgid filesystem object")
    safe_name(name, kind == "directory")
    if kind == "file":
        size, digest = file_hash(path)
    elif kind == "symlink":
        body = os.readlink(path).encode("utf-8", "strict")
        safe_link(name, body)
        size, digest = len(body), hash_bytes(body)
    else:
        size, digest = 0, hash_bytes(b"")
    need(size <= MAX_MEMBER_BYTES, "filesystem object exceeds byte limit")
    need(stat_identity(path.lstat()) == stat_identity(original), "lstat object changed during record")
    return {"name": name, "kind": kind, "mode": mode, "bytes": size, "sha256": digest}


def record_fields(member):
    return {key: member[key] for key in ("name", "kind", "mode", "bytes", "sha256")}


def inventory_rows(rows, label):
    # Reuse the same path/type/size/mode and ancestor checks for non-ZIP inventory.
    need(isinstance(rows, list), label + " is not a list")
    result = inventory({"file": "inventory.zip", "bytes": 1, "sha256": "0" * 64, "members": rows})
    validate_aliases(result)
    return result


def lstat_tree(root, prefix):
    need(root.is_absolute() and not root.is_symlink() and root.is_dir(), "application root must be a real absolute directory")
    need(root.resolve(strict=True) == root, "application root has symlink ancestry")
    need(prefix.endswith(".app") and len(safe_name(prefix)) == 2, "invalid application archive prefix")
    result = []
    links = {}
    total = 0
    def visit(path, name):
        nonlocal total
        before = path.lstat()
        directory = stat.S_ISDIR(before.st_mode)
        archive_name = name + "/" if directory else name
        row = member_record(path, archive_name)
        result.append(row)
        need(len(result) <= MAX_MEMBERS, "application inventory exceeds member limit")
        total += row["bytes"]
        need(total <= MAX_PACKAGE_RAW_BYTES, "application inventory exceeds byte limit")
        if row["kind"] == "symlink":
            links[archive_name] = os.readlink(path)
            try:
                resolved = path.resolve(strict=True)
            except (OSError, RuntimeError) as error:
                raise VerificationError("application symlink is dangling or cyclic") from error
            need(resolved.is_relative_to(root), "application symlink escapes immutable application tree")
        elif directory:
            for child in sorted(path.iterdir(), key=lambda item: item.name):
                visit(child, name + "/" + child.name)
        need(stat_identity(path.lstat()) == stat_identity(before), "application tree changed while inventoried")
    visit(root, prefix)
    members = inventory_rows(result, "application lstat tree")
    validate_scope("client", members)
    validate_link_graph(members, links)
    return members


def approved_paths(rows, label):
    members = inventory_rows(rows, label)
    actual = {}
    original_states = {}
    for name, row in members.items():
        raw_path = row.get("path")
        need(isinstance(raw_path, str) and raw_path and Path(raw_path).is_absolute(), label + " path must be absolute")
        path = Path(raw_path)
        need(path.parent.resolve(strict=True) == path.parent, label + " path has symlink ancestry")
        need(path not in original_states, label + " repeats an approved source path")
        original_states[path] = stat_identity(path.lstat())
        observed = member_record(path, name)
        need(record_fields(row) == observed, label + " differs from frozen approval: " + name)
        # Gallery and client extras are explicitly selected regular files/real directories.
        need(row["kind"] != "symlink", label + " cannot contain extra symlinks")
        actual[name] = observed
    return actual, original_states


def validate_approval(approval, manifest):
    need(isinstance(approval, dict) and type(approval.get("schema")) is int and approval["schema"] == 1,
         "unsupported payload approval schema")
    for key in ("version", "release", "runtimeCommit", "sourceFingerprint", "buildGuid"):
        need(approval.get(key) == manifest[key], "payload approval identity differs: " + key)
    need(isinstance(approval.get("app"), dict), "missing independent application approval")
    app = approval["app"]
    need(isinstance(app.get("root"), str) and Path(app["root"]).is_absolute(), "application approval root must be absolute")
    need(app.get("prefix") == "Frontier-Reckoning-" + VERSION + "/Frontier Reckoning.app",
         "application prefix differs from release")
    expected_app = inventory_rows(app.get("members"), "immutable application approval")
    for name in expected_app:
        need(name.rstrip("/") == app["prefix"] or name.startswith(app["prefix"] + "/"),
             "application approval member lies outside application prefix")
    need(app["prefix"] + "/" in expected_app and expected_app[app["prefix"] + "/"]["kind"] == "directory",
         "application approval omits root directory")
    validate_scope("client", expected_app)
    client_extra, extra_states = approved_paths(approval.get("clientExtras"), "client extras")
    gallery, gallery_states = approved_paths(approval.get("gallery"), "approved gallery")
    validate_scope("client", client_extra)
    validate_scope("gallery", gallery)
    need(all(not name.startswith(app["prefix"] + "/") and name.rstrip("/") != app["prefix"]
             for name in client_extra), "client extra replaces an application member")
    need(set(client_extra).isdisjoint(expected_app), "client application/extras approval aliases")
    expected_client = {**expected_app, **client_extra}
    validate_aliases(expected_client)
    inventory_rows(list(expected_client.values()), "full approved client")
    return expected_app, expected_client, gallery, {**extra_states, **gallery_states}


def verify_app_identity(root, manifest, approval):
    boot = root / "Contents/Resources/Data/boot.config"
    stream, original = regular_stream(boot)
    with stream:
        raw = stream.read(1024 * 1024 + 1)
        need(len(raw) <= 1024 * 1024, "boot config exceeds bound")
        finish_read(boot, stream, original)
    matches = re.findall(r"^build-guid=([0-9a-f]{32})$", raw.decode("utf-8", "strict"), re.MULTILINE)
    need(matches == [manifest["buildGuid"]], "immutable application build GUID differs from manifest")
    info = root / "Contents/Info.plist"
    stream, original = regular_stream(info)
    with stream:
        data = stream.read(4 * 1024 * 1024 + 1)
        need(len(data) <= 4 * 1024 * 1024, "Info.plist exceeds bound")
        finish_read(info, stream, original)
    plist = plistlib.loads(data)
    need(isinstance(plist, dict), "Info.plist is not a dictionary")
    need(plist.get("CFBundleShortVersionString") == VERSION, "immutable application version differs from release")
    executable = plist.get("CFBundleExecutable")
    need(isinstance(executable, str) and len(safe_name(executable)) == 1, "invalid bundle executable")
    executable_path = root / "Contents/MacOS" / executable
    need(stat.S_ISREG(executable_path.lstat().st_mode) and executable_path.lstat().st_mode & 0o111,
         "bundle executable is not an executable regular file")
    receipts = [row for row in approval["clientExtras"] if row["name"].endswith("/client-build.json")]
    need(len(receipts) == 1, "client extras must contain one frozen client-build.json")
    build, _ = read_json(Path(receipts[0]["path"]))
    need(isinstance(build, dict), "client-build receipt is not an object")
    for key in ("runtimeCommit", "sourceFingerprint", "sourceInputs", "buildGuid"):
        need(build.get(key) == manifest[key], "immutable client-build receipt differs: " + key)
    need(build.get("appVersion") == VERSION, "client-build version differs")
    executable_record = build.get("executable")
    need(isinstance(executable_record, dict), "missing build executable identity")
    need(file_hash(executable_path) == (executable_record.get("bytes"), executable_record.get("sha256")),
         "build receipt executable bytes differ from immutable tree")


def current_inputs(repo, tree, paths):
    observed = {}
    for root_name in sorted(INPUT_ROOTS):
        base = repo / root_name
        need(base.is_dir() and not base.is_symlink(), "missing real input root")
        def visit(path):
            mode = path.lstat().st_mode
            relative = path.relative_to(repo).as_posix()
            parts = relative.split("/")
            if any(part.startswith(".") or part == "__pycache__" for part in parts):
                return
            if stat.S_ISDIR(mode):
                for child in sorted(path.iterdir(), key=lambda item: item.name):
                    visit(child)
            else:
                need(is_input(relative), "unexpected input file scope")
                row = member_record(path, "Current/" + relative)
                observed[relative] = row
        visit(base)
    need(set(observed) == paths, "current runtime input path set differs from frozen Git")
    for path, row in observed.items():
        blob = tree[path]
        need((row["bytes"], row["sha256"], row["mode"]) == (blob["bytes"], blob["sha256"], blob["mode"]),
             "current runtime input Git bytes/modes differ: " + path)
    return {path: row["sha256"] for path, row in observed.items()}


def verify(manifest_path, repo, package_dir, approval_path):
    package_dir = package_dir.absolute()
    need(package_dir.resolve(strict=True) == package_dir and package_dir.is_dir(), "package directory is not a real directory")
    manifest, raw_manifest = read_json(manifest_path)
    approval, raw_approval = read_json(approval_path)
    manifest_state = stat_identity(manifest_path.lstat())
    approval_state = stat_identity(approval_path.lstat())
    validate_header(manifest)
    need(manifest["runtimeCommit"] == EXPECTED_RUNTIME_COMMIT and manifest["sourceInputs"] == EXPECTED_INPUTS
         and manifest["sourceFingerprint"] == EXPECTED_FINGERPRINT, "release identity differs from authorized 069 source freeze")
    expected_app, expected_client, expected_gallery, approved_states = validate_approval(approval, manifest)
    app_root = Path(approval["app"]["root"])
    app_tree = lstat_tree(app_root, approval["app"]["prefix"])
    need({name: record_fields(row) for name, row in app_tree.items()} ==
         {name: record_fields(row) for name, row in expected_app.items()},
         "complete application lstat tree differs from immutable approval")
    verify_app_identity(app_root, manifest, approval)
    source_tree = git_tree(repo, manifest["sourceSnapshotCommit"])
    runtime_tree = git_tree(repo, manifest["runtimeCommit"])
    source_paths = {p for p in source_tree if eligible_source(p)}
    input_paths = {p for p in source_tree if is_input(p)}
    runtime_inputs = {p for p in runtime_tree if is_input(p)}
    need(input_paths == runtime_inputs, "runtime/source-snapshot input path sets differ")
    source_hashes = git_hashes(repo, source_tree, source_paths | input_paths)
    runtime_hashes = git_hashes(repo, runtime_tree, runtime_inputs)
    need({p: source_hashes[p] for p in input_paths} == runtime_hashes,
         "runtime/source-snapshot input Git blobs/modes differ")
    input_map = {p: source_hashes[p]["sha256"] for p in input_paths}
    fingerprint = hash_bytes(json.dumps(input_map, sort_keys=True, separators=(",", ":")).encode("utf-8"))
    need(len(input_map) == EXPECTED_INPUTS and fingerprint == EXPECTED_FINGERPRINT,
         "Git input count/fingerprint disagrees with authorized freeze")
    current_inputs(repo, source_hashes, input_paths)
    summaries = {}
    inventories = {}
    zip_names = set()
    zip_before = {}
    for kind in ("source", "client", "gallery"):
        package = manifest["packages"][kind]
        members = inventory(package)
        validate_aliases(members)
        need(package["file"] not in zip_names, "three packages must have distinct filenames")
        zip_names.add(package["file"])
        package_path = package_dir / package["file"]
        need(not package_path.is_symlink() and package_path.resolve(strict=True).parent == package_dir,
             "ZIP basename resolves outside the controlled package directory")
        zip_before[kind] = stat_identity(package_path.lstat())
        summary, members = verify_zip(package_path, kind, package)
        summaries[kind] = summary
        inventories[kind] = members
    represented = {}
    for name, member in inventories["source"].items():
        if member["kind"] == "directory":
            continue
        relative = name.removeprefix("FrontierReckoning/")
        need(relative not in represented, "duplicate source Git path")
        represented[relative] = member
    need(set(represented) == source_paths, "source ZIP is not the complete selected Git allowlist")
    for path, member in represented.items():
        blob = source_hashes[path]
        need(member["bytes"] == blob["bytes"] and member["sha256"] == blob["sha256"] and member["mode"] == blob["mode"],
             "source ZIP differs from frozen Git blob/mode: " + path)
    for kind, expected in (("client", expected_client), ("gallery", expected_gallery)):
        actual = {name: record_fields(row) for name, row in inventories[kind].items()}
        need(actual == {name: record_fields(row) for name, row in expected.items()},
             kind + " ZIP is not the exact independent approved inventory")
    # Re-read complete trees and whole containers after all independent work.
    need(lstat_tree(app_root, approval["app"]["prefix"]) == app_tree, "application tree changed during independent review")
    for rows, label in ((approval["clientExtras"], "client extras"), (approval["gallery"], "approved gallery")):
        _, ending_states = approved_paths(rows, label)
        for path, state in ending_states.items():
            need(state == approved_states[path], label + " file identity changed during independent review")
    current_inputs(repo, source_hashes, input_paths)
    for kind, package in manifest["packages"].items():
        path = package_dir / package["file"]
        need(stat_identity(path.lstat()) == zip_before[kind] and file_hash(path) == (package["bytes"], package["sha256"]),
             "ZIP changed before completion of independent review")
    need(stat_identity(manifest_path.lstat()) == manifest_state and manifest_path.read_bytes() == raw_manifest,
         "manifest changed during independent verification")
    need(stat_identity(approval_path.lstat()) == approval_state and approval_path.read_bytes() == raw_approval,
         "payload approval changed during independent verification")
    return {"schema": 1, "result": "Passed", "createdUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
            "manifest": {"file": str(manifest_path), "bytes": len(raw_manifest), "sha256": hash_bytes(raw_manifest)},
            "payloadApproval": {"file": str(approval_path), "bytes": len(raw_approval), "sha256": hash_bytes(raw_approval)},
            "packageDirectory": str(package_dir),
            "verifier": {"file": str(Path(__file__).resolve()), "bytes": file_hash(Path(__file__))[0],
                         "sha256": file_hash(Path(__file__))[1],
                         "implementation": "Independent standard-library ZIP streaming, Git-object hashing and complete lstat tree"},
            "identity": {key: manifest[key] for key in ("version", "release", "sourceSnapshotCommit", "runtimeCommit",
                                                         "sourceFingerprint", "sourceInputs", "buildGuid", "tests")},
            "approvedPayload": {"applicationRoot": str(app_root), "applicationMembers": len(app_tree),
                                "clientExtraMembers": len(approval["clientExtras"]), "galleryMembers": len(expected_gallery)},
            "packages": summaries,
            "checks": {key: "Passed" for key in (
                "manifestSchemaAndAuthorizedReleaseFreeze", "wholeZipBytesAndSha256", "allMembersBytesSha256UnixModesTypesCrcPaths",
                "confinedRelativeSymlinkGraph", "sourceGitAllowlistCompleteness", "sourceGitBlobBytesSha256Modes",
                "runtimeAndSnapshotInputGitBlobs", "source664InputFingerprint", "currentSourceInputBytesAndModes",
                "completeApplicationLstatInventory", "immutableApplicationTreeAndBuildIdentity",
                "clientApprovedInventoryCompleteness", "galleryExactApprovedSelection", "cachePersonalStorageOldCandidateExclusions",
                "manifestApprovalAllZipsAndPayloadsUnchangedDuringReview")},
            "scope": ["Independent standard-library ZIP/Git/lstat verification; no packaging helper imports.",
                      "Every ZIP member is fully streamed and CRC/SHA/size/mode/type/path checked.",
                      "Every selected source file/symlink binds to sourceSnapshotCommit Git blob and mode; Git omits directory modes.",
                      "Client app must equal the complete separately frozen lstat tree, including directories and symlinks.",
                      "Client extras and gallery equal the exact separately approved source-file inventory.",
                      "No native gameplay, physical two-Mac, new test execution, performance, cloud download or remote hash claim."]}


def write_new_json(path, document):
    raw = (json.dumps(document, ensure_ascii=False, sort_keys=True, indent=2, allow_nan=False) + "\n").encode("utf-8")
    need(path.parent.is_dir() and path.parent.resolve(strict=True) == path.parent, "receipt parent must be a real existing directory")
    descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0), 0o644)
    with os.fdopen(descriptor, "wb") as stream:
        stream.write(raw)
        stream.flush()
        os.fsync(stream.fileno())
    return len(raw), hash_bytes(raw)


def self_test():
    passed = []
    def rejected(label, action):
        try:
            action()
        except (VerificationError, zipfile.BadZipFile, UnicodeError, zlib.error, OSError, RuntimeError, ValueError):
            passed.append(label)
            return
        raise AssertionError("accepted malicious fixture: " + label)
    def row(name, kind="file", data=b"sample", mode=None):
        if mode is None:
            mode = {"file": stat.S_IFREG | 0o644, "directory": stat.S_IFDIR | 0o755, "symlink": stat.S_IFLNK | 0o777}[kind]
        return {"name": name, "kind": kind, "mode": mode, "bytes": len(data), "sha256": hash_bytes(data)}
    def memory_zip(payload):
        memory = io.BytesIO()
        with zipfile.ZipFile(memory, "w", compression=zipfile.ZIP_STORED) as archive:
            for member, data in payload:
                info = zipfile.ZipInfo(member["name"])
                info.create_system = 3
                info.external_attr = member["mode"] << 16
                info.compress_type = zipfile.ZIP_STORED
                archive.writestr(info, data)
        return memory.getvalue()
    def inspect_memory(data, members):
        with zipfile.ZipFile(io.BytesIO(data), "r") as archive:
            return verify_archive(archive, "fixture", inventory_rows(members, "fixture"))
    source_link = row("FrontierReckoning/SourceAssets/link", "symlink", b"../README.md")
    readme = row("FrontierReckoning/README.md", data=b"guide")
    source_rows = [source_link, readme]
    raw = memory_zip([(source_link, b"../README.md"), (readme, b"guide")])
    need(inspect_memory(raw, source_rows)[0] == len(b"../README.mdguide"), "valid CRC/SHA/link memory fixture failed")
    passed.append("fully streamed in-memory ZIP with confined Git-style relative symlink")
    rejected("parent path traversal", lambda: safe_name("root/../escape"))
    rejected("absolute path", lambda: safe_name("/absolute"))
    rejected("Windows path ambiguity", lambda: safe_name("root\\escape"))
    rejected("drive prefix ambiguity", lambda: safe_name("C:/escape"))
    rejected("member control character", lambda: safe_name("root/evil\x00suffix"))
    rejected("escaping relative symlink", lambda: safe_link("root/link", b"../../escape"))
    rejected("absolute symlink", lambda: safe_link("root/link", b"/outside"))
    rejected("source non-allowlisted cache", lambda: validate_scope("source", {"FrontierReckoning/Build/cache.bin": {"kind": "file"}}))
    rejected("gallery personal durable store", lambda: validate_scope("gallery", {"Frontier-Reckoning-" + VERSION + "-gallery/Saves/private.json": {"kind": "file"}}))
    rejected("gallery executable application", lambda: validate_scope("gallery", {"Frontier-Reckoning-" + VERSION + "-gallery/Frontier.app/Contents/game": {"kind": "file"}}))
    rejected("gallery historical diagnostic", lambda: validate_scope("gallery", {"Frontier-Reckoning-" + VERSION + "-gallery/Diagnostics/fail.json": {"kind": "file"}}))
    rejected("non-identity Native/Build binary", lambda: validate_scope("gallery", {"Frontier-Reckoning-" + VERSION + "-gallery/Native/Build/game": {"kind": "file"}}))
    need(eligible_source("Docs/Evidence/" + RELEASE + "/Diagnostics/Candidate1-failed.json"), "pre-fix source diagnostics incorrectly excluded")
    need(not eligible_source("Docs/Evidence/068/README.md"), "historical source evidence included")
    passed.append("current evidence can preserve honest pre-fix diagnostics while other releases are excluded")
    rejected("duplicate JSON properties", lambda: unique_object([("schema", 1), ("schema", 1)]))
    rejected("boolean bytes count", lambda: inventory_rows([dict(readme, bytes=True)], "fixture"))
    rejected("duplicate member inventory", lambda: inventory_rows([readme, readme], "fixture"))
    rejected("case alias", lambda: inventory_rows([readme, dict(readme, name="FrontierReckoning/readme.md")], "fixture"))
    rejected("Unicode decomposition alias", lambda: inventory_rows([row("root/caf\u00e9"), row("root/cafe\u0301")], "fixture"))
    rejected("case alias in parent paths", lambda: inventory_rows([row("root/Dir/a"), row("root/dir/b")], "fixture"))
    rejected("regular file as member ancestor", lambda: inventory_rows([row("root/a"), row("root/a/b")], "fixture"))
    rejected("symlink as member ancestor", lambda: inventory_rows([row("root/a", "symlink", b"target"), row("root/a/b")], "fixture"))
    rejected("setuid member", lambda: inventory_rows([dict(readme, mode=stat.S_IFREG | 0o4644)], "fixture"))
    rejected("mismatched directory type", lambda: inventory_rows([dict(readme, kind="directory")], "fixture"))
    rejected("member byte bound", lambda: inventory_rows([dict(readme, bytes=MAX_MEMBER_BYTES + 1)], "fixture"))
    wrong = [dict(source_link), dict(readme, mode=stat.S_IFREG | 0o755)]
    rejected("wrong POSIX mode against actual ZIP", lambda: inspect_memory(raw, wrong))
    wrong = [dict(source_link), dict(readme, sha256="0" * 64)]
    rejected("wrong member SHA against actual ZIP", lambda: inspect_memory(raw, wrong))
    rejected("complete member set omitted", lambda: inspect_memory(raw, [readme]))
    rejected("truncated ZIP with otherwise matching inventory", lambda: inspect_memory(raw[:-22], source_rows))
    damaged = bytearray(raw)
    location = damaged.index(b"guide")
    damaged[location] ^= 1
    # SHA is changed to the corrupted body so CRC is the rejecting invariant.
    corrupted_readme = dict(readme, sha256=hash_bytes(bytes(damaged[location:location + 5])))
    rejected("CRC catches corrupted payload even with updated member SHA", lambda: inspect_memory(bytes(damaged), [source_link, corrupted_readme]))
    unicode_name = b"FrontierReckoning/unapproved.md"
    alternate = b"\x01" + struct.pack("<I", zlib.crc32(readme["name"].encode())) + unicode_name
    extra = struct.pack("<HH", 0x7075, len(alternate)) + alternate
    memory = io.BytesIO()
    with zipfile.ZipFile(memory, "w") as archive:
        info = zipfile.ZipInfo(readme["name"])
        info.create_system = 3
        info.external_attr = readme["mode"] << 16
        info.extra = extra
        archive.writestr(info, b"guide")
    rejected("Unicode extra cannot redirect native extractor to unapproved path", lambda: inspect_memory(memory.getvalue(), [readme]))
    encrypted_local = bytearray(memory_zip([(readme, b"guide")]))
    encrypted_local[6] |= 1
    rejected("local encrypted flag differs from benign central header", lambda: inspect_memory(bytes(encrypted_local), [readme]))
    time_drift = bytearray(memory_zip([(readme, b"guide")]))
    time_drift[10] ^= 1
    rejected("local timestamp differs from central header", lambda: inspect_memory(bytes(time_drift), [readme]))
    memory = io.BytesIO()
    with zipfile.ZipFile(memory, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        info = zipfile.ZipInfo(readme["name"])
        info.create_system = 3
        info.external_attr = readme["mode"] << 16
        info.compress_type = zipfile.ZIP_DEFLATED
        with archive.open(info, "w", force_zip64=True) as stream:
            stream.write(b"guide")
    need(inspect_memory(memory.getvalue(), [readme])[0] == 5, "controlled force_zip64 streaming style rejected")
    passed.append("actual packager-style ZIP64 local header accepted with matching streamed CRC and sizes")
    cyclic_rows = [row("root/a", "symlink", b"b"), row("root/b", "symlink", b"a")]
    rejected("symlink cycle", lambda: validate_link_graph(inventory_rows(cyclic_rows, "fixture"), {"root/a": "b", "root/b": "a"}))
    rejected("dangling relative symlink", lambda: validate_link_graph(inventory_rows([source_link], "fixture"), {source_link["name"]: "../README.md"}))
    rejected("symlink through regular-file ancestor", lambda: validate_link_graph(inventory_rows([row("root/a"), row("root/link", "symlink", b"a/b")], "fixture"), {"root/link": "a/b"}))
    # Filesystem tests live only under this Packaging directory and are removed.
    with tempfile.TemporaryDirectory(prefix="self-test-069-", dir=Path(__file__).absolute().parent) as temporary:
        root = Path(temporary).resolve()
        app = root / "Frontier Reckoning.app"
        (app / "Contents/Resources/Data").mkdir(parents=True)
        (app / "Contents/MacOS").mkdir()
        prefix = "Frontier-Reckoning-" + VERSION + "/Frontier Reckoning.app"
        build_guid = "f" * 32
        (app / "Contents/Resources/Data/boot.config").write_text("build-guid=" + build_guid + "\n")
        (app / "Contents/Info.plist").write_bytes(plistlib.dumps({"CFBundleShortVersionString": VERSION, "CFBundleExecutable": "game"}))
        executable = app / "Contents/MacOS/game"
        executable.write_bytes(b"temporary executable fixture")
        executable.chmod(0o755)
        (app / "Contents/link").symlink_to("MacOS/game")
        manifest = {"schema": 1, "version": VERSION, "release": RELEASE,
                    "runtimeCommit": EXPECTED_RUNTIME_COMMIT, "sourceSnapshotCommit": "a" * 40,
                    "sourceFingerprint": EXPECTED_FINGERPRINT, "sourceInputs": EXPECTED_INPUTS,
                    "buildGuid": build_guid,
                    "tests": {"run": "run-fixture", "result": "Passed", "EditMode": 1, "PlayMode": 1, "total": 2},
                    "packages": {kind: {} for kind in ("source", "client", "gallery")}}
        validate_header(manifest)
        passed.append("schema1 manifest compatibility without LAN schema assumptions")
        rejected("boolean manifest schema", lambda: validate_header(dict(manifest, schema=True)))
        initial = lstat_tree(app, prefix)
        need(initial[prefix + "/Contents/link"]["kind"] == "symlink", "lstat followed an app symlink")
        passed.append("complete app lstat includes all directories, regular files, executable modes and symlink bodies")
        receipt = root / "client-build.json"
        receipt.write_text(json.dumps({key: manifest[key] for key in ("runtimeCommit", "sourceFingerprint", "sourceInputs", "buildGuid")} |
                                      {"appVersion": VERSION, "executable": {"bytes": file_hash(executable)[0], "sha256": file_hash(executable)[1]}}))
        extra = member_record(receipt, "Frontier-Reckoning-" + VERSION + "/client-build.json")
        extra["path"] = str(receipt)
        screenshot = root / "approved.png"
        screenshot.write_bytes(b"temporary approved screenshot")
        gallery_row = member_record(screenshot, "Frontier-Reckoning-" + VERSION + "-gallery/Native/UI/approved.png")
        gallery_row["path"] = str(screenshot)
        approval = {key: manifest[key] for key in ("version", "release", "runtimeCommit", "sourceFingerprint", "buildGuid")}
        approval.update({"schema": 1, "app": {"root": str(app), "prefix": prefix, "members": list(initial.values())},
                         "clientExtras": [extra], "gallery": [gallery_row]})
        expected_app, expected_client, expected_gallery, _ = validate_approval(approval, manifest)
        need(expected_app == initial and len(expected_client) == len(initial) + 1 and len(expected_gallery) == 1, "approved inventory fixture failed")
        verify_app_identity(app, manifest, approval)
        passed.append("separate immutable app/client extras/gallery approval and build identity roundtrip")
        changed = {**approval, "sourceFingerprint": "0" * 64}
        rejected("approval mismatched runtime fingerprint", lambda: validate_approval(changed, manifest))
        changed_row = {**gallery_row, "sha256": "0" * 64}
        rejected("approved gallery source differs", lambda: approved_paths([changed_row], "gallery"))
        changed_row = {**extra, "name": prefix + "/Contents/Info.plist"}
        rejected("client extras replace app tree", lambda: validate_approval({**approval, "clientExtras": [changed_row]}, manifest))
        (app / "Contents/unlisted.txt").write_bytes(b"must be detected")
        need(lstat_tree(app, prefix) != expected_app, "app completeness missed unlisted extra file")
        passed.append("unlisted app file changes full inventory despite self-consistent ZIP manifest")
        (app / "Contents/unlisted.txt").unlink()
        executable.chmod(0o644)
        need(lstat_tree(app, prefix) != expected_app, "app executable-mode drift missed")
        passed.append("immutable executable mode drift changes app inventory")
        executable.chmod(0o755)
        (app / "Contents/escaping").symlink_to("../../approved.png")
        rejected("actual app symlink leaves app while remaining in package parent", lambda: lstat_tree(app, prefix))
        (app / "Contents/escaping").unlink()
        fifo = app / "Contents/fifo"
        os.mkfifo(fifo)
        rejected("unsupported app filesystem object", lambda: lstat_tree(app, prefix))
        fifo.unlink()
        alias = root / "receipt-alias.json"
        alias.symlink_to(receipt)
        rejected("formal JSON cannot be a symlink", lambda: read_json(alias))
        output = root / "exclusive-receipt.json"
        write_new_json(output, {"result": "fixture"})
        original_receipt = output.read_bytes()
        rejected("exclusive receipt cannot overwrite prior output", lambda: write_new_json(output, {"result": "replacement"}))
        need(output.read_bytes() == original_receipt, "exclusive receipt overwritten")
        passed.append("exclusive receipt leaves existing bytes unchanged")
        nonfinite = root / "nonfinite.json"
        nonfinite.write_text('{"value": NaN}')
        rejected("nonfinite JSON", lambda: read_json(nonfinite))
        passed.append("temporary fixture cleanup; no disk ZIP or Git mutation")
    return {"result": "Passed", "count": len(passed), "checks": passed,
            "scope": "Memory ZIP and automatically removed Packaging-local filesystem fixtures only; no formal package verification."}


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--manifest", type=Path)
    parser.add_argument("--package-dir", type=Path)
    parser.add_argument("--repo", type=Path)
    parser.add_argument("--payload-approval", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        need(not any((args.manifest, args.package_dir, args.repo, args.payload_approval, args.output)), "self-test cannot inspect formal inputs")
        print(json.dumps(self_test(), ensure_ascii=False, indent=2))
        return 0
    if not all((args.manifest, args.package_dir, args.repo, args.payload_approval, args.output)):
        parser.error("--manifest, --package-dir, --repo, --payload-approval and --output are required")
    output = args.output.absolute()
    need(not output.exists() and not output.is_symlink(), "output receipt exists; refusing overwrite")
    need(output.parent.is_dir() and output.parent.resolve(strict=True) == output.parent, "receipt parent must be a real existing directory")
    inputs = [args.manifest.absolute(), args.payload_approval.absolute()]
    need(output not in inputs, "receipt cannot replace input")
    verifier_state = stat_identity(Path(__file__).absolute().lstat())
    verifier_before = file_hash(Path(__file__).absolute())
    try:
        result = verify(args.manifest.absolute(), args.repo.resolve(strict=True), args.package_dir.absolute(), args.payload_approval.absolute())
        need(stat_identity(Path(__file__).absolute().lstat()) == verifier_state and file_hash(Path(__file__).absolute()) == verifier_before,
             "verifier source changed while executing")
        code = 0
    except (VerificationError, OSError, ValueError, UnicodeError, zipfile.BadZipFile, zlib.error, subprocess.SubprocessError, plistlib.InvalidFileException) as error:
        result = {"schema": 1, "result": "Failed", "createdUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
                  "errorType": type(error).__name__, "error": str(error),
                  "verifier": {"file": str(Path(__file__).absolute()), "bytes": verifier_before[0], "sha256": verifier_before[1]},
                  "scope": "Independent ZIP/Git/lstat validation; failed validation preserves all input artifacts."}
        code = 2
    size, digest = write_new_json(output, result)
    print(json.dumps({"result": result["result"], "receipt": str(output), "bytes": size, "sha256": digest}))
    return code


if __name__ == "__main__":
    sys.exit(main())
