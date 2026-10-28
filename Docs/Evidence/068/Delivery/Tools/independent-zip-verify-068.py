#!/usr/bin/env python3
"""Read-only, standard-library verification of frozen 068 delivery ZIPs.

Usage:
  python3 independent-zip-verify-068.py --self-test
  python3 independent-zip-verify-068.py --manifest release-manifest.json \
      --package-dir /absolute/Candidate3 --repo /absolute/project \
      --output independent-zip-verification.json

The manifest and three ZIPs are read-only. The receipt is created exclusively;
an existing receipt is never replaced. No packaging helper is imported or run.
Git objects bind every source file/symlink to the specified source snapshot;
directory modes are checked against the inventory because Git does not store
directories. Client/gallery bytes are independently checked against the frozen
inventory, not a claim of native gameplay, remote hashes, or cloud delivery.
"""

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import sys
import tempfile
import time
import zipfile
import zlib


VERSION = "0.11.0"
RELEASE = "068"
CHUNK = 1024 * 1024
MAX_MEMBER_BYTES = 512 * 1024 * 1024
MAX_PACKAGE_RAW_BYTES = 2 * 1024 * 1024 * 1024
MAX_MEMBERS = 100000
ALLOW_ROOTS = {"Assets", "Packages", "ProjectSettings", "Docs", "SourceAssets", "Tools"}
ALLOW_FILES = {"README.md", ".gitignore"}
INPUT_ROOTS = {"Assets", "Packages", "ProjectSettings", "Tools"}
FORBIDDEN_COMPONENTS = {"Library", "Builds", "Build", "outputs", "Temp", "obj",
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


def read_json(path):
    raw = path.read_bytes()
    need(len(raw) <= 64 * 1024 * 1024, "manifest exceeds 64 MiB")
    return json.loads(raw.decode("utf-8", "strict"), object_pairs_hook=unique_object,
                      parse_constant=lambda _: (_ for _ in ()).throw(VerificationError("nonfinite JSON number"))), raw


def hash_bytes(raw):
    return hashlib.sha256(raw).hexdigest()


def file_hash(path):
    digest = hashlib.sha256()
    count = 0
    with path.open("rb") as stream:
        while True:
            data = stream.read(CHUNK)
            if not data:
                break
            count += len(data)
            digest.update(data)
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
    need(isinstance(manifest, dict) and manifest.get("schema") == 1, "unsupported manifest schema")
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
        elif package_kind == "source" and tail:
            need(tail[0] in ALLOW_ROOTS, "source directory is outside allowlist")
            if tail[:2] == ["Docs", "Evidence"] and len(tail) >= 3:
                need(tail[2] == RELEASE, "other evidence release directory in source")
        if package_kind == "gallery":
            need(not any(p.endswith(".app") for p in tail), "application payload in gallery")
            need("Diagnostics" not in tail, "historical diagnostic payload in gallery")
            if "Evidence" in tail:
                i = tail.index("Evidence")
                need(i + 1 < len(tail) and tail[i + 1] == RELEASE, "other evidence release in gallery")
    return root


def verify_zip(path, package_kind, package):
    members = inventory(package)
    root = validate_scope(package_kind, members)
    size, digest = file_hash(path)
    need(size == package["bytes"] and digest == package["sha256"], package_kind + " whole ZIP identity mismatch")
    total = 0
    kind_counts = {"file": 0, "directory": 0, "symlink": 0}
    with zipfile.ZipFile(path, "r") as archive:
        entries = archive.infolist()
        names = [entry.filename for entry in entries]
        need(len(entries) == len(members) and len(set(names)) == len(names) and set(names) == set(members),
             package_kind + " ZIP member set mismatch")
        need(not archive.comment, "unexpected archive comment")
        for entry in entries:
            member = members[entry.filename]
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
            # zipfile verifies the CRC while the entire stream is read.
            need(count == member["bytes"] and digest_member.hexdigest() == member["sha256"], "ZIP member byte/SHA mismatch")
            if link_body is not None:
                safe_link(entry.filename, bytes(link_body))
            total += count
            kind_counts[member["kind"]] += 1
    need(file_hash(path) == (size, digest), "ZIP changed during independent verification")
    return {"file": path.name, "bytes": size, "sha256": digest, "members": len(members),
            "memberKinds": kind_counts, "memberRawBytes": total, "prefix": root,
            "memberBytesShaModesTypesCrcAndPaths": "Passed"}, members


def verify(manifest_path, repo, package_dir, require_current=True):
    package_dir = package_dir.resolve(strict=True)
    need(package_dir.is_dir(), "package directory is not a directory")
    manifest, raw_manifest = read_json(manifest_path)
    validate_header(manifest)
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
    need(len(input_map) == manifest["sourceInputs"] and fingerprint == manifest["sourceFingerprint"],
         "Git input count/fingerprint disagrees with manifest")
    if require_current:
        current_map = {}
        for root_name in INPUT_ROOTS:
            for path in (repo / root_name).rglob("*"):
                if path.is_file():
                    relative = path.relative_to(repo).as_posix()
                    if is_input(relative):
                        current_map[relative] = file_hash(path)[1]
        need(current_map == input_map, "current runtime inputs drifted from frozen Git inputs")
    summaries = {}
    inventories = {}
    zip_names = set()
    for kind in ("source", "client", "gallery"):
        package = manifest["packages"][kind]
        inventory(package)
        need(package["file"] not in zip_names, "three packages must have distinct filenames")
        zip_names.add(package["file"])
        package_path = package_dir / package["file"]
        need(not package_path.is_symlink() and package_path.resolve(strict=True).parent == package_dir,
             "ZIP basename resolves outside the controlled package directory")
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
    need(manifest_path.read_bytes() == raw_manifest, "manifest changed during independent verification")
    return {"schema": 1, "result": "Passed", "createdUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
            "manifest": {"file": str(manifest_path.resolve()), "bytes": len(raw_manifest), "sha256": hash_bytes(raw_manifest)},
            "packageDirectory": str(package_dir),
            "identity": {key: manifest[key] for key in ("version", "release", "sourceSnapshotCommit", "runtimeCommit",
                                                         "sourceFingerprint", "sourceInputs", "buildGuid", "tests")},
            "packages": summaries,
            "checks": {"manifestSchemaAndRelease": "Passed", "wholeZipBytesAndSha256": "Passed",
                       "allMembersBytesSha256UnixModesTypesCrcPaths": "Passed", "safeRelativeSymlinks": "Passed",
                       "sourceGitAllowlistCompleteness": "Passed", "sourceGitBlobBytesSha256Modes": "Passed",
                       "runtimeAndSnapshotInputGitBlobs": "Passed", "sourceInputFingerprint": "Passed",
                       "currentSourceInputBytes": "Passed" if require_current else "NotRequested",
                       "cachePersonalStorageOldCandidateExclusions": "Passed", "manifestAndZipsUnchangedDuringReview": "Passed"},
            "scope": ["Independent standard-library ZIP/Git object verification; no packaging helper imports.",
                      "Every ZIP member is fully streamed and CRC/SHA/size/mode/type/path checked against frozen inventory.",
                      "Source regular files and symlinks bind to complete selected sourceSnapshotCommit Git blobs and modes.",
                      "Git does not store directories: directory bytes/modes are checked against the manifest only.",
                      "Client/gallery inventories are verified as supplied; native gameplay semantics are outside this tool.",
                      "No remote checksum, cloud download equality, new test execution, native performance or release-completion claim."]}


def write_new_json(path, document):
    raw = (json.dumps(document, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")
    with path.open("xb") as stream:
        stream.write(raw)
    os.chmod(path, 0o644)
    return len(raw), hash_bytes(raw)


def self_test():
    with tempfile.TemporaryDirectory(prefix="frontier-independent-068-") as temporary:
        root = Path(temporary)
        repo = root / "repo"
        repo.mkdir()
        def git(*args):
            return subprocess.run(["git", "-C", str(repo), *args], stdout=subprocess.PIPE,
                                  stderr=subprocess.PIPE, check=True).stdout.decode().strip()
        git("init", "-q")
        git("config", "user.name", "Temporary Fixture")
        git("config", "user.email", "fixture@example.invalid")
        for name, body in {"Assets/sample.txt": b"runtime\n", "Packages/manifest.json": b"{}\n",
                           "ProjectSettings/settings.asset": b"fixture\n", "Tools/fixture.py": b"#!/bin/sh\n",
                           "README.md": b"fixture guide\n", ".gitignore": b"Library/\n"}.items():
            path = repo / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(body)
        os.chmod(repo / "Tools/fixture.py", 0o755)
        git("add", ".")
        git("commit", "-qm", "runtime fixture")
        runtime = git("rev-parse", "HEAD")
        for name, body in {"Docs/Evidence/068/README.md": b"current evidence\n",
                           "Docs/Evidence/067/README.md": b"historical evidence\n"}.items():
            path = repo / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(body)
        (repo / "SourceAssets").mkdir()
        (repo / "SourceAssets/link").symlink_to("../README.md")
        git("add", ".")
        git("commit", "-qm", "source documentation fixture")
        snapshot = git("rev-parse", "HEAD")
        tree = git_tree(repo, snapshot)
        source_paths = {p for p in tree if eligible_source(p)}
        hashes = git_hashes(repo, tree, source_paths)
        source_payload = []
        for path in sorted(source_paths):
            entry = hashes[path]
            body = os.readlink(repo / path).encode() if stat.S_ISLNK(entry["mode"]) else (repo / path).read_bytes()
            source_payload.append(("FrontierReckoning/" + path, entry["mode"], body, path))
        source_payload.insert(0, ("FrontierReckoning/", stat.S_IFDIR | 0o755, b"", None))
        payloads = {"source": source_payload,
                    "client": [("Frontier-Reckoning-0.11.0/", stat.S_IFDIR | 0o755, b"", None),
                               ("Frontier-Reckoning-0.11.0/Frontier.app/Contents/MacOS/game", stat.S_IFREG | 0o755, b"executable fixture", None),
                               ("Frontier-Reckoning-0.11.0/Frontier.app/Contents/link", stat.S_IFLNK | 0o777, b"MacOS/game", None)],
                    "gallery": [("Gallery-0.11.0/README.md", stat.S_IFREG | 0o644, b"fixture scope", None),
                                ("Gallery-0.11.0/Docs/Evidence/068/Native/Replays/manual-export-fixture.replay.json",
                                 stat.S_IFREG | 0o644, b"container fixture", None)]}
        manifest = {"schema": 1, "version": VERSION, "release": RELEASE, "runtimeCommit": runtime,
                    "sourceSnapshotCommit": snapshot, "sourceInputs": 4, "buildGuid": "f" * 32,
                    "sourceFingerprint": hash_bytes(json.dumps({p: hashes[p]["sha256"] for p in hashes if is_input(p)},
                                                               sort_keys=True, separators=(",", ":")).encode()),
                    "tests": {"run": "run-fixture", "EditMode": 1, "PlayMode": 1, "total": 2, "result": "Passed"},
                    "packages": {}}
        def make_package(kind, payload):
            path = root / (kind + ".zip")
            members = []
            with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
                for name, mode, body, git_path in payload:
                    info = zipfile.ZipInfo(name)
                    info.create_system = 3
                    info.external_attr = mode << 16
                    info.compress_type = zipfile.ZIP_DEFLATED
                    archive.writestr(info, body)
                    member = {"name": name, "kind": kind_for_mode(mode), "mode": mode,
                              "bytes": len(body), "sha256": hash_bytes(body)}
                    if git_path:
                        member["gitPath"] = git_path
                    members.append(member)
            size, digest = file_hash(path)
            manifest["packages"][kind] = {"file": path.name, "bytes": size, "sha256": digest, "members": members}
        for kind, payload in payloads.items():
            make_package(kind, payload)
        (root / "Packaging").mkdir()
        manifest_path = root / "Packaging" / "release-manifest.json"
        def save():
            manifest_path.write_text(json.dumps(manifest))
        save()
        initial_result = verify(manifest_path, repo, root)
        need(initial_result["result"] == "Passed" and initial_result["packageDirectory"] == str(root.resolve()),
             "separate manifest/package directory fixture did not pass")
        passed = ["valid three-package Git/symlink/executable/directory roundtrip"]
        def rejected(label, action):
            try:
                action()
            except (VerificationError, zipfile.BadZipFile, UnicodeError, zlib.error):
                passed.append(label)
                return
            raise AssertionError("accepted malicious fixture: " + label)
        rejected("parent path traversal", lambda: safe_name("root/../escape"))
        rejected("absolute path", lambda: safe_name("/absolute"))
        rejected("Windows path ambiguity", lambda: safe_name("root\\escape"))
        rejected("escaping relative symlink", lambda: safe_link("root/link", b"../../escape"))
        rejected("absolute symlink", lambda: safe_link("root/link", b"/outside"))
        rejected("duplicate JSON properties", lambda: unique_object([("schema", 1), ("schema", 1)]))
        original = manifest["packages"]["source"]["members"][1]["mode"]
        manifest["packages"]["source"]["members"][1]["mode"] = stat.S_IFREG | 0o755
        save()
        rejected("wrong member POSIX mode", lambda: verify(manifest_path, repo, root))
        manifest["packages"]["source"]["members"][1]["mode"] = original
        original = manifest["packages"]["client"]["members"][1]["sha256"]
        manifest["packages"]["client"]["members"][1]["sha256"] = "0" * 64
        save()
        rejected("wrong member SHA", lambda: verify(manifest_path, repo, root))
        manifest["packages"]["client"]["members"][1]["sha256"] = original
        original = manifest["sourceFingerprint"]
        manifest["sourceFingerprint"] = "0" * 64
        save()
        rejected("wrong runtime input fingerprint", lambda: verify(manifest_path, repo, root))
        manifest["sourceFingerprint"] = original
        original = manifest["packages"]["gallery"]["members"][0]["name"]
        manifest["packages"]["gallery"]["members"][0]["name"] = "Gallery-0.11.0/Saves/private.json"
        save()
        rejected("personal durable store in gallery", lambda: verify(manifest_path, repo, root))
        manifest["packages"]["gallery"]["members"][0]["name"] = original
        original = manifest["packages"]["source"]["members"]
        manifest["packages"]["source"]["members"] = original + [dict(original[1])]
        save()
        rejected("duplicate member inventory", lambda: verify(manifest_path, repo, root))
        manifest["packages"]["source"]["members"] = original
        save()
        (repo / "Assets/sample.txt").write_bytes(b"drift")
        rejected("current runtime source drift", lambda: verify(manifest_path, repo, root))
        (repo / "Assets/sample.txt").write_bytes(b"runtime\n")
        make_package("source", [row for row in payloads["source"] if not row[0].endswith("README.md")])
        save()
        rejected("self-consistent inventory omits tracked source", lambda: verify(manifest_path, repo, root))
        make_package("source", payloads["source"])
        save()
        make_package("source", [(name, mode, b"tampered Git payload" if name.endswith("Assets/sample.txt") else body, git_path)
                                for name, mode, body, git_path in payloads["source"]])
        save()
        rejected("self-consistent ZIP/inventory changes frozen Git bytes", lambda: verify(manifest_path, repo, root))
        make_package("source", payloads["source"])
        save()
        truncated = (root / "gallery.zip").read_bytes()[:-22]
        (root / "gallery.zip").write_bytes(truncated)
        manifest["packages"]["gallery"]["bytes"] = len(truncated)
        manifest["packages"]["gallery"]["sha256"] = hash_bytes(truncated)
        save()
        rejected("truncated ZIP with matching container SHA", lambda: verify(manifest_path, repo, root))
        make_package("gallery", payloads["gallery"])
        save()
        make_package("client", [(name, mode, b"altered" if name.endswith("/game") else body, git_path)
                                for name, mode, body, git_path in payloads["client"]])
        # Restore old inventory/hash while keeping the modified ZIP.
        manifest["packages"]["client"]["sha256"] = "0" * 64
        save()
        rejected("whole ZIP digest mismatch", lambda: verify(manifest_path, repo, root))
        make_package("client", payloads["client"])
        (root / "client-alias.zip").symlink_to(root / "client.zip")
        manifest["packages"]["client"]["file"] = "client-alias.zip"
        save()
        rejected("ZIP container symlink cannot bypass controlled directory", lambda: verify(manifest_path, repo, root))
        result = {"result": "Passed", "count": len(passed), "checks": passed,
                  "scope": "Temporary Git/ZIP fixtures only; no formal artifact verification or package creation."}
        return result


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--manifest", type=Path)
    parser.add_argument("--package-dir", type=Path, help="Controlled directory containing all three ZIP basenames")
    parser.add_argument("--repo", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--self-test", action="store_true")
    parser.add_argument("--skip-current-inputs", action="store_true", help="Use frozen Git inputs without current working-tree comparison")
    args = parser.parse_args()
    if args.self_test:
        need(not any((args.manifest, args.package_dir, args.repo, args.output)), "self-test cannot inspect formal inputs")
        print(json.dumps(self_test(), ensure_ascii=False, indent=2))
        return 0
    if not all((args.manifest, args.package_dir, args.repo, args.output)):
        parser.error("--manifest, --package-dir, --repo, --output are required")
    need(not args.output.exists(), "output receipt already exists; refusing overwrite")
    try:
        result = verify(args.manifest.resolve(strict=True), args.repo.resolve(strict=True), args.package_dir,
                        not args.skip_current_inputs)
        code = 0
    except (VerificationError, OSError, ValueError, UnicodeError, zipfile.BadZipFile, zlib.error, subprocess.SubprocessError) as error:
        result = {"schema": 1, "result": "Failed", "errorType": type(error).__name__, "error": str(error),
                  "scope": "Independent ZIP/Git validation; failure preserves every input artifact."}
        code = 2
    size, digest = write_new_json(args.output, result)
    print(json.dumps({"result": result["result"], "receipt": str(args.output.resolve()), "bytes": size, "sha256": digest}))
    return code


if __name__ == "__main__":
    sys.exit(main())
