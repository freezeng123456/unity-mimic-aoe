#!/usr/bin/env python3
"""Generate NEW 070 payload approval, ZIP manifest, or final freeze from real inputs.

This preparation helper never packages, builds, runs Unity, uploads, edits the
project, or invokes the independent checker. The checker remains a separate
implementation. Output JSON is created exclusively; existing evidence is kept.
Use --self-test for automatically removed synthetic fixtures only.
"""

import argparse
import hashlib
import json
import os
from pathlib import Path
import plistlib
import re
import stat
import subprocess
import sys
import tempfile
import time
import unicodedata
from xml.etree import ElementTree as ET
import zipfile

VERSION = "0.13.0"
RELEASE = "070"
CHUNK = 1024 * 1024
MAX_FILE = 512 * 1024 * 1024
HEX = re.compile(r"^[0-9a-f]{64}$")
COMMIT = re.compile(r"^[0-9a-f]{40}(?:[0-9a-f]{24})?$")
BUILD_GUID = re.compile(r"^[0-9a-f]{32}$")
INPUT_ROOTS = {"Assets", "Packages", "ProjectSettings", "Tools"}
SOURCE_ROOTS = INPUT_ROOTS | {"Docs", "SourceAssets"}
SOURCE_FILES = {"README.md", ".gitignore"}
FORBIDDEN = {"Library", "Builds", "outputs", "Temp", "obj", "UserSettings", ".git", "__pycache__", "Saves", "Account", "Preferences"}
PREFIX = "Frontier-Reckoning-" + VERSION
APP_PREFIX = PREFIX + "/Frontier Reckoning.app"


def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def identity(path):
    s = path.lstat()
    return (s.st_dev, s.st_ino, s.st_mode, s.st_size, s.st_mtime_ns, s.st_ctime_ns)


def absolute(path):
    require(isinstance(path, str) and path and Path(path).is_absolute(), "input path must be absolute")
    result = Path(path)
    require(result.parent.resolve(strict=True) == result.parent, "input has symlink ancestry: " + str(result))
    return result


def read(path, limit=64 * 1024 * 1024):
    before = identity(path)
    require(stat.S_ISREG(before[2]) and before[3] <= limit, "input must be a bounded regular file")
    fd = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
    with os.fdopen(fd, "rb") as f:
        require((os.fstat(f.fileno()).st_dev, os.fstat(f.fileno()).st_ino) == before[:2], "input raced before read")
        raw = f.read(limit + 1)
        require(len(raw) <= limit and identity(path) == before, "input changed during read")
    return raw


def pairs(items):
    d = {}
    for k, v in items:
        require(k not in d, "duplicate JSON field")
        d[k] = v
    return d


def document(path):
    raw = read(path)
    return json.loads(raw.decode("utf-8", "strict"), object_pairs_hook=pairs,
                      parse_constant=lambda _: (_ for _ in ()).throw(ValueError("nonfinite JSON"))), raw


def file_digest(path):
    before = identity(path)
    require(stat.S_ISREG(before[2]) and before[3] <= 2 * 1024 * 1024 * 1024, "hash input must be bounded regular file")
    digest = hashlib.sha256()
    amount = 0
    fd = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
    with os.fdopen(fd, "rb") as f:
        require((os.fstat(f.fileno()).st_dev, os.fstat(f.fileno()).st_ino) == before[:2], "hash input raced")
        while True:
            block = f.read(CHUNK)
            if not block:
                break
            amount += len(block)
            digest.update(block)
    require(identity(path) == before and amount == before[3], "file changed while hashing")
    return amount, digest.hexdigest()


def safe_path(name, directory=False):
    require(isinstance(name, str) and name and len(name.encode("utf-8")) <= 4096, "invalid package member name")
    require(not name.startswith("/") and "\\" not in name and not re.match(r"^[A-Za-z]:", name), "ambiguous member path")
    require(all(32 <= ord(c) != 127 for c in name) and name.endswith("/") == directory, "member controls/type suffix")
    parts = name.rstrip("/").split("/")
    require(all(part and part not in (".", "..") for part in parts), "noncanonical member path")
    return parts


def check_inventory(rows):
    require(isinstance(rows, list) and 0 < len(rows) < 65535, "inventory must have 1..65534 members")
    seen = {}
    spellings = {}
    result = {}
    total = 0
    for row in rows:
        require(isinstance(row, dict), "invalid member record")
        mode = row.get("mode")
        require(type(mode) is int and 0 <= mode <= 0xffff and not mode & 0o6000, "invalid member POSIX mode")
        kind = {stat.S_IFREG: "file", stat.S_IFDIR: "directory", stat.S_IFLNK: "symlink"}.get(stat.S_IFMT(mode))
        require(kind is not None and row.get("kind") == kind, "member kind/mode differs")
        parts = safe_path(row.get("name"), kind == "directory")
        size = row.get("bytes")
        require(type(size) is int and 0 <= size <= MAX_FILE and isinstance(row.get("sha256"), str) and HEX.fullmatch(row["sha256"]), "invalid member size/hash")
        require(kind != "directory" or size == 0 and row["sha256"] == sha(b""), "directory has payload")
        normalized = unicodedata.normalize("NFC", row["name"].rstrip("/")).casefold()
        require(normalized not in seen, "duplicate/case/Unicode alias")
        seen[normalized] = row["name"]
        result[row["name"]] = row
        total += size
        for i in range(1, len(parts) + 1):
            text = "/".join(parts[:i])
            key = unicodedata.normalize("NFC", text).casefold()
            require(key not in spellings or spellings[key] == text, "member ancestry case/Unicode alias")
            spellings[key] = text
    require(total <= 2 * 1024 * 1024 * 1024, "inventory raw bytes exceed bound")
    for name in result:
        parts = name.rstrip("/").split("/")
        require(all("/".join(parts[:i]) not in result for i in range(1, len(parts))), "regular file/symlink ancestor")
    return result


def core(row):
    return {k: row[k] for k in ("name", "kind", "mode", "bytes", "sha256")}


def lstat_row(path, name, app_root=None):
    before = identity(path)
    mode = before[2] & 0xffff
    kind = {stat.S_IFREG: "file", stat.S_IFDIR: "directory", stat.S_IFLNK: "symlink"}.get(stat.S_IFMT(mode))
    require(kind is not None and not mode & 0o6000, "unsupported/setuid filesystem object")
    safe_path(name, kind == "directory")
    if kind == "file":
        count, digest = file_digest(path)
    elif kind == "directory":
        count, digest = 0, sha(b"")
    else:
        require(app_root is not None, "selected extras/gallery cannot be symlinks")
        target = os.readlink(path)
        require(target and not target.startswith("/") and "\\" not in target and not re.match(r"^[A-Za-z]:", target)
                and all(32 <= ord(c) != 127 for c in target), "unsafe app symlink")
        require(path.resolve(strict=True).is_relative_to(app_root), "app symlink escapes original tree")
        body = target.encode("utf-8", "strict")
        count, digest = len(body), sha(body)
    require(count <= MAX_FILE and identity(path) == before, "filesystem object changed or exceeds bound")
    return {"name": name, "kind": kind, "mode": mode, "bytes": count, "sha256": digest}


def walk_app(root):
    require(root.is_dir() and not root.is_symlink() and root.resolve(strict=True) == root, "app must be a real original directory")
    rows = []
    def visit(path, name):
        before = identity(path)
        directory = stat.S_ISDIR(before[2])
        rows.append(lstat_row(path, name + "/" if directory else name, root))
        if directory:
            for child in sorted(path.iterdir(), key=lambda p: p.name):
                visit(child, name + "/" + child.name)
        require(identity(path) == before, "app directory changed while recording")
    visit(root, APP_PREFIX)
    check_inventory(rows)
    return rows


def selected_rows(selections, gallery=False):
    require(isinstance(selections, list) and selections, "explicit selected files are required")
    rows = []
    prefix = PREFIX + "-gallery" if gallery else PREFIX
    def visit(path, name, recursive):
        directory = stat.S_ISDIR(path.lstat().st_mode)
        row = lstat_row(path, name.rstrip("/") + "/" if directory else name)
        parts = safe_path(row["name"], directory)
        require(parts[0] == prefix and not any(p in FORBIDDEN or p.endswith(".app") for p in parts[1:]), "selection outside current package scope")
        require(not any(p in {"Diagnostics", "Candidate1", "Candidate2", "FrozenCandidate1", "FrozenCandidate2"} for p in parts[1:]), "selection contains historical diagnostics/candidate")
        if gallery and "Build" in parts:
            i = parts.index("Build")
            require(i > 0 and parts[i - 1] == "Native" and
                    ((directory and len(parts) == i + 1) or
                     (not directory and len(parts) == i + 2 and parts[-1] in {"boot.config", "build-identity.json", "client-build.json"})), "gallery Build selection is not identity-only")
        if gallery and "Evidence" in parts:
            i = parts.index("Evidence")
            require(i + 1 < len(parts) and parts[i + 1] == RELEASE, "selection contains older evidence")
        row["path"] = str(path)
        rows.append(row)
        if directory and recursive:
            for child in sorted(path.iterdir(), key=lambda p: p.name):
                visit(child, name.rstrip("/") + "/" + child.name, True)
    for item in selections:
        require(isinstance(item, dict) and isinstance(item.get("recursive", False), bool), "invalid file selection")
        visit(absolute(item.get("path")), item.get("name"), item.get("recursive", False))
    check_inventory(rows)
    if gallery:
        images = [r for r in rows if r["kind"] == "file" and r["name"].lower().endswith(".png")]
        require(images, "native gallery must select actual PNG files")
        for image in images:
            header = read(Path(image["path"]), MAX_FILE)[:33]
            require(len(header) == 33 and header[:8] == b"\x89PNG\r\n\x1a\n" and header[12:16] == b"IHDR", "selected native PNG has invalid header")
    return rows


def input_path(path):
    parts = path.split("/")
    return parts[0] in INPUT_ROOTS and not any(p.startswith(".") or p == "__pycache__" for p in parts)


def source_path(path):
    parts = path.split("/")
    return (parts[0] in SOURCE_ROOTS or path in SOURCE_FILES) and not any(p in FORBIDDEN for p in parts) and (not parts[-1].startswith(".") or path in SOURCE_FILES) and (parts[:2] != ["Docs", "Evidence"] or len(parts) >= 4 and parts[2] == RELEASE)


def git_inventory(repo, commit, predicate):
    require(isinstance(commit, str) and COMMIT.fullmatch(commit), "complete Git commit ID required")
    raw = subprocess.run(["git", "-C", str(repo), "ls-tree", "-rz", "--full-tree", commit], check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE).stdout
    selected = {}
    for item in raw.split(b"\0"):
        if not item:
            continue
        head, path = item.split(b"\t", 1)
        mode, kind, oid = head.decode("ascii").split()
        name = path.decode("utf-8", "strict")
        safe_path(name)
        if predicate(name):
            require(kind == "blob" and name not in selected, "unsupported/duplicate selected Git object")
            selected[name] = {"mode": int(mode, 8), "oid": oid}
    p = subprocess.Popen(["git", "-C", str(repo), "cat-file", "--batch"], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
    try:
        for name, row in sorted(selected.items()):
            oid = row["oid"]
            p.stdin.write((oid + "\n").encode())
            p.stdin.flush()
            header = p.stdout.readline().decode().strip().split()
            require(len(header) == 3 and header[:2] == [oid, "blob"], "invalid Git blob reply")
            count = int(header[2])
            require(0 <= count <= MAX_FILE, "Git blob exceeds size bound")
            digest = hashlib.sha256()
            object_digest = hashlib.sha1() if len(oid) == 40 else hashlib.sha256()
            object_digest.update(("blob " + str(count) + "\0").encode())
            left = count
            while left:
                block = p.stdout.read(min(CHUNK, left))
                require(block, "truncated Git blob")
                digest.update(block)
                object_digest.update(block)
                left -= len(block)
            require(p.stdout.read(1) == b"\n" and object_digest.hexdigest() == oid, "Git blob framing/object digest differs")
            row.update({"bytes": count, "sha256": digest.hexdigest()})
        p.stdin.close()
        require(p.wait(timeout=10) == 0, "Git reader failed")
    finally:
        if p.poll() is None:
            p.kill()
            p.wait(timeout=10)
        if not p.stdin.closed:
            p.stdin.close()
        p.stdout.close()
    return selected


def current_input_map(repo):
    result = {}
    def visit(path):
        rel = path.relative_to(repo).as_posix()
        if any(p.startswith(".") or p == "__pycache__" for p in rel.split("/")):
            return
        mode = path.lstat().st_mode
        if stat.S_ISDIR(mode):
            for child in sorted(path.iterdir(), key=lambda p: p.name):
                visit(child)
        else:
            row = lstat_row(path, "Current/" + rel)
            require(row["kind"] == "file", "runtime inputs cannot contain symlinks/special files")
            result[rel] = row["sha256"]
    for root in sorted(INPUT_ROOTS):
        base = repo / root
        require(base.is_dir() and not base.is_symlink(), "missing real input root")
        visit(base)
    return result


def fingerprint(files):
    return sha(json.dumps(files, sort_keys=True, separators=(",", ":")).encode())


def test_report(path):
    raw = read(path, 32 * 1024 * 1024)
    require(b"<!DOCTYPE" not in raw and b"<!ENTITY" not in raw, "test XML declarations are unsupported")
    node = ET.fromstring(raw)
    total = int(node.get("total", "0"))
    require(node.tag == "test-run" and node.get("result") == "Passed" and total > 0 and int(node.get("passed", "0")) == total
            and all(int(node.get(k, "0")) == 0 for k in ("failed", "skipped", "inconclusive")), "full passing test report required")
    cases = list(node.iter("test-case"))
    require(len(cases) == total and all(c.get("result") == "Passed" for c in cases), "test-case results/counts disagree with passing report")
    return total, sha(raw)


def load_request(path):
    d, raw = document(path)
    require(isinstance(d, dict) and type(d.get("schema")) is int and d["schema"] == 1 and d.get("ready") is True,
            "delivery request is pending; fill actual frozen inputs before generation")
    require(d.get("version") == VERSION and d.get("release") == RELEASE, "wrong release request")
    expected = d.get("expectedIdentity")
    require(isinstance(expected, dict), "missing separately supplied frozen identity")
    for key in ("runtimeCommit", "sourceSnapshotCommit"):
        require(isinstance(expected.get(key), str) and COMMIT.fullmatch(expected[key]), "missing/invalid frozen " + key)
    require(type(expected.get("inputCount")) is int and 680 <= expected["inputCount"] < 65535, "invalid exact frozen inputCount")
    require(isinstance(expected.get("fingerprint"), str) and HEX.fullmatch(expected["fingerprint"]), "invalid frozen fingerprint")
    require(isinstance(expected.get("buildGuid"), str) and BUILD_GUID.fullmatch(expected["buildGuid"]), "missing/invalid frozen buildGuid")
    for key in ("repo", "appRoot", "buildIdentity", "sourceFreeze", "testDirectory", "packageDirectory"):
        absolute(d.get(key))
    require(Path(d["repo"]).resolve(strict=True) == Path(d["repo"]), "repo must resolve to itself")
    require(re.fullmatch(r"run-[A-Za-z0-9]+", Path(d["testDirectory"]).name), "invalid actual test-run directory")
    require(isinstance(d.get("nativeScope"), str) and d["nativeScope"].strip(), "explicit honest native observation scope required")
    return d, raw


def facts(request):
    e = request["expectedIdentity"]
    repo = Path(request["repo"])
    current = current_input_map(repo)
    require(len(current) == e["inputCount"] and fingerprint(current) == e["fingerprint"], "current inputs differ from explicit runtime freeze")
    runtime = git_inventory(repo, e["runtimeCommit"], input_path)
    snapshot = git_inventory(repo, e["sourceSnapshotCommit"], input_path)
    require({p: (r["bytes"], r["sha256"], r["mode"]) for p, r in runtime.items()} ==
            {p: (r["bytes"], r["sha256"], r["mode"]) for p, r in snapshot.items()}, "runtime/source snapshot input Git blobs differ")
    require({p: r["sha256"] for p, r in runtime.items()} == current, "Git/current runtime input map differs")
    require(all((repo / p).lstat().st_mode & 0xffff == row["mode"] for p, row in runtime.items()),
            "current runtime input modes differ from frozen Git")
    freeze, freeze_raw = document(Path(request["sourceFreeze"]))
    require(type(freeze.get("schema")) is int and freeze["schema"] == 1 and freeze.get("version") == VERSION
            and freeze.get("release") == RELEASE and freeze.get("runtimeCommit") == e["runtimeCommit"] and freeze.get("sourceInputs") == e["inputCount"]
            and freeze.get("sourceFingerprint") == e["fingerprint"] and freeze.get("files") == current, "actual source-freeze receipt differs")
    test_dir = Path(request["testDirectory"])
    evidence, evidence_raw = document(test_dir / "source-evidence.json")
    require(type(evidence.get("schema")) is int and evidence["schema"] == 1 and evidence.get("files") == current
            and evidence.get("sourceFingerprint") == e["fingerprint"], "full tests belong to different source")
    tests = {"run": test_dir.name, "result": "Passed"}
    for mode in ("EditMode", "PlayMode"):
        count, report_sha = test_report(test_dir / (mode + ".xml"))
        record = evidence.get("reports", {}).get(mode)
        require(isinstance(record, dict) and record.get("file") == mode + ".xml" and record.get("sha256") == report_sha
                and type(record.get("passed")) is int and record["passed"] == count, "test report differs from recorded evidence")
        tests[mode] = count
    tests["total"] = tests["EditMode"] + tests["PlayMode"]
    if request.get("expectedTests") is not None:
        require(request["expectedTests"] == tests, "real tests differ from separately expected run/counts")
    build, build_raw = document(Path(request["buildIdentity"]))
    require(build.get("appVersion") == VERSION and build.get("buildResult") == "Succeeded" and build.get("buildGuid") == e["buildGuid"]
            and build.get("runtimeCommit") == e["runtimeCommit"] and build.get("sourceInputs") == e["inputCount"]
            and build.get("sourceFingerprint") == e["fingerprint"] and build.get("testRun") == str(test_dir), "actual successful build identity differs from freeze/full tests")
    app = Path(request["appRoot"])
    plist = plistlib.loads(read(app / "Contents/Info.plist", 4 * 1024 * 1024))
    require(plist.get("CFBundleShortVersionString") == VERSION, "app version differs")
    executable_name = plist.get("CFBundleExecutable")
    require(len(safe_path(executable_name)) == 1, "unsafe bundle executable")
    executable = app / "Contents/MacOS" / executable_name
    require(stat.S_ISREG(executable.lstat().st_mode) and executable.lstat().st_mode & 0o111, "mac executable is not executable regular file")
    boot = read(app / "Contents/Resources/Data/boot.config", CHUNK).decode("utf-8")
    require(re.findall(r"^build-guid=([0-9a-f]{32})$", boot, re.MULTILINE) == [e["buildGuid"]], "actual app GUID differs")
    executable_record = build.get("executable")
    require(isinstance(executable_record, dict) and file_digest(executable) == (executable_record.get("bytes"), executable_record.get("sha256")), "actual app executable differs from build receipt")
    if "compiledRuntime" in build:
        record = build["compiledRuntime"]
        runtime_dll = app / "Contents/Resources/Data/Managed/Frontier.Runtime.dll"
        require(isinstance(record, dict) and file_digest(runtime_dll) == (record.get("bytes"), record.get("sha256")), "compiled runtime differs from build receipt")
    metadata = {"sourceFreezeSha256": sha(freeze_raw), "sourceEvidenceSha256": sha(evidence_raw), "buildIdentitySha256": sha(build_raw)}
    return tests, build, metadata


def make_approval(request):
    tests, build, evidence = facts(request)
    e = request["expectedIdentity"]
    app = walk_app(Path(request["appRoot"]))
    extra = selected_rows(request.get("clientExtras"))
    gallery = selected_rows(request.get("gallery"), gallery=True)
    check_inventory(app + extra)
    receipts = [r for r in extra if r["name"].endswith("/client-build.json")]
    require(len(receipts) == 1, "exactly one original client-build.json selection required")
    client_build, _ = document(Path(receipts[0]["path"]))
    require(client_build == build, "selected client-build.json must preserve original build identity")
    return {"schema": 1, "version": VERSION, "release": RELEASE, "runtimeCommit": e["runtimeCommit"],
            "sourceFingerprint": e["fingerprint"], "buildGuid": e["buildGuid"], "createdUtc": utc(),
            "app": {"root": request["appRoot"], "prefix": APP_PREFIX, "members": app},
            "clientExtras": extra, "gallery": gallery,
            "preparationEvidence": {**evidence, "tests": tests, "claim": "Original payload inventory selection; independent ZIP verification remains pending."}}


def compare_approval(request, approved):
    current = make_approval(request)
    for key in ("schema", "version", "release", "runtimeCommit", "sourceFingerprint", "buildGuid", "app", "clientExtras", "gallery", "preparationEvidence"):
        require(approved.get(key) == current[key], "original payload or identity changed since approval: " + key)
    return current


def zip_inventory(path, source=False):
    before = identity(path)
    size, digest = file_digest(path)
    rows = []
    with zipfile.ZipFile(path, "r") as z:
        require(not z.comment, "ZIP archive comment unsupported")
        for entry in z.infolist():
            require(entry.create_system == 3 and not entry.flag_bits & 1 and entry.compress_type in (zipfile.ZIP_STORED, zipfile.ZIP_DEFLATED), "ZIP mode/compression/encryption unsupported")
            mode = entry.external_attr >> 16
            kind = {stat.S_IFREG: "file", stat.S_IFDIR: "directory", stat.S_IFLNK: "symlink"}.get(stat.S_IFMT(mode))
            require(kind is not None and entry.is_dir() == (kind == "directory") and entry.orig_filename == entry.filename, "ZIP type/name differs")
            safe_path(entry.filename, kind == "directory")
            require(entry.file_size <= MAX_FILE, "ZIP member exceeds bound")
            member_sha = hashlib.sha256()
            count = 0
            with z.open(entry) as f:
                while True:
                    block = f.read(CHUNK)
                    if not block:
                        break
                    count += len(block)
                    require(count <= entry.file_size, "ZIP member exceeds declared size")
                    member_sha.update(block)
            require(count == entry.file_size, "ZIP member size differs")
            row = {"name": entry.filename, "kind": kind, "mode": mode, "bytes": count, "sha256": member_sha.hexdigest()}
            if source and kind != "directory":
                require(entry.filename.startswith("FrontierReckoning/"), "source ZIP prefix differs")
                row["gitPath"] = entry.filename[len("FrontierReckoning/"):]
            rows.append(row)
    check_inventory(rows)
    require(identity(path) == before and file_digest(path) == (size, digest), "ZIP changed during manifest generation")
    return {"file": path.name, "bytes": size, "sha256": digest, "members": rows}


def make_manifest(request, approved):
    current = compare_approval(request, approved)
    tests = current["preparationEvidence"]["tests"]
    e = request["expectedIdentity"]
    directory = Path(request["packageDirectory"])
    require(directory.is_dir() and directory.resolve(strict=True) == directory, "package directory must be real")
    names = request.get("packages")
    require(isinstance(names, dict) and set(names) == {"source", "client", "gallery"}, "exact three package basenames required")
    require(len(set(names.values())) == 3, "package filenames must differ")
    packages = {}
    for kind, name in names.items():
        require(isinstance(name, str) and safe_path(name) == [name] and name.endswith(".zip"), "invalid ZIP basename")
        packages[kind] = zip_inventory(directory / name, source=kind == "source")
    expected_client = {r["name"]: core(r) for r in current["app"]["members"] + current["clientExtras"]}
    expected_gallery = {r["name"]: core(r) for r in current["gallery"]}
    for kind, expected in (("client", expected_client), ("gallery", expected_gallery)):
        require({r["name"]: core(r) for r in packages[kind]["members"]} == expected, kind + " ZIP differs from complete original approval")
    selected_git = git_inventory(Path(request["repo"]), e["sourceSnapshotCommit"], source_path)
    actual_source = {r["gitPath"]: r for r in packages["source"]["members"] if r["kind"] != "directory"}
    require(set(actual_source) == set(selected_git), "source ZIP is not exact current Git allowlist")
    for path, row in actual_source.items():
        expected = selected_git[path]
        require(all(row[key] == expected[key] for key in ("mode", "bytes", "sha256")), "source ZIP differs from Git: " + path)
    for row in packages["source"]["members"]:
        parts = safe_path(row["name"], row["kind"] == "directory")
        require(parts[0] == "FrontierReckoning" and not any(p in FORBIDDEN for p in parts[1:]), "source directory/package prefix is outside allowlist")
        if len(parts) > 1 and row["kind"] == "directory":
            require(parts[1] in SOURCE_ROOTS, "unapproved source directory root")
            if parts[1:3] == ["Docs", "Evidence"] and len(parts) > 3:
                require(parts[3] == RELEASE, "older evidence directory in source package")
    compare_approval(request, approved)
    return {"schema": 1, "version": VERSION, "release": RELEASE, "createdUtc": utc(),
            "sourceSnapshotCommit": e["sourceSnapshotCommit"], "runtimeCommit": e["runtimeCommit"],
            "sourceFingerprint": e["fingerprint"], "sourceInputs": e["inputCount"], "buildGuid": e["buildGuid"],
            "tests": tests, "nativeScope": request["nativeScope"], "physicalMacAcceptance": "pending",
            "naturalWholeMatchPerformance": "pending", "remoteContentHashVerified": False, "packages": packages,
            "preparationEvidence": current["preparationEvidence"],
            "independentZipVerification": "pending; generated manifest is an inventory, not independent acceptance"}


def make_freeze(request, manifest, manifest_raw, approval, approval_raw):
    actual = make_manifest(request, approval)
    require(set(actual) == set(manifest) and all(actual[k] == manifest[k] for k in actual if k != "createdUtc"),
            "manifest/original ZIPs/source/build/test evidence changed before final freeze")
    e = request["expectedIdentity"]
    return {"schema": 1, "ready": True, "version": VERSION, "release": RELEASE,
            "runtimeCommit": e["runtimeCommit"], "sourceSnapshotCommit": e["sourceSnapshotCommit"],
            "inputCount": e["inputCount"], "fingerprint": e["fingerprint"], "buildGuid": e["buildGuid"],
            "tests": manifest["tests"], "manifestSha256": sha(manifest_raw), "payloadApprovalSha256": sha(approval_raw),
            "createdUtc": utc(), "scope": "Real local inputs frozen for subsequent independent verification; no independent acceptance or upload claim."}


def utc():
    return time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())


def write_new(path, data):
    require(path.is_absolute() and path.parent.is_dir() and path.parent.resolve(strict=True) == path.parent, "output parent must be real existing absolute directory")
    raw = (json.dumps(data, ensure_ascii=False, indent=2, allow_nan=False) + "\n").encode()
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0), 0o644)
    with os.fdopen(fd, "wb") as f:
        f.write(raw)
        f.flush()
        os.fsync(f.fileno())
    return {"file": str(path), "bytes": len(raw), "sha256": sha(raw)}


def self_test():
    passed = []
    def rejected(label, action):
        try:
            action()
        except (OSError, ValueError, TypeError, KeyError, ET.ParseError, zipfile.BadZipFile, RuntimeError):
            passed.append(label)
            return
        raise AssertionError("accepted invalid fixture: " + label)
    def row(name="root/file", content=b"fixture", mode=stat.S_IFREG | 0o644):
        return {"name": name, "kind": {stat.S_IFREG: "file", stat.S_IFDIR: "directory", stat.S_IFLNK: "symlink"}[stat.S_IFMT(mode)],
                "mode": mode, "bytes": len(content), "sha256": sha(content)}
    require(fingerprint({"b": "2", "a": "1"}) == fingerprint({"a": "1", "b": "2"}), "fingerprint is order-dependent")
    passed.append("input-map canonical fingerprint")
    require(source_path("Docs/Evidence/070/README.md") and not source_path("Docs/Evidence/069/README.md"), "evidence filtering differs")
    passed.append("070 source evidence allowlist")
    check_inventory([row(), row("root/second")])
    passed.append("member inventory normal roundtrip")
    rejected("duplicate paths", lambda: check_inventory([row(), row()]))
    rejected("case alias", lambda: check_inventory([row("root/File"), row("root/file")]))
    rejected("Unicode decomposition alias", lambda: check_inventory([row("root/café"), row("root/cafe\u0301")]))
    rejected("parent case alias", lambda: check_inventory([row("root/Dir/one"), row("root/dir/two")]))
    rejected("file ancestor", lambda: check_inventory([row("root/a"), row("root/a/b")]))
    rejected("boolean byte count", lambda: check_inventory([{**row(), "bytes": True}]))
    rejected("setuid mode", lambda: check_inventory([row(mode=stat.S_IFREG | 0o4644)]))
    rejected("directory payload", lambda: check_inventory([row("root/dir/", mode=stat.S_IFDIR | 0o755)]))
    rejected("parent path", lambda: safe_path("root/../escape"))
    rejected("absolute path", lambda: safe_path("/outside"))
    rejected("Windows path", lambda: safe_path("root\\outside"))
    rejected("drive path", lambda: safe_path("C:/outside"))
    rejected("NUL path", lambda: safe_path("root/a\0suffix"))
    with tempfile.TemporaryDirectory(prefix="preparation-helper-self-test-070-", dir=Path(__file__).absolute().parent) as temp:
        root = Path(temp).resolve()
        pending = root / "request.json"
        pending.write_text(json.dumps({"schema": 1, "ready": False}))
        rejected("pending request refuses generation", lambda: load_request(pending))
        incomplete = {"schema": 1, "ready": True, "version": VERSION, "release": RELEASE,
                      "expectedIdentity": {"runtimeCommit": "a" * 40, "sourceSnapshotCommit": "b" * 40, "inputCount": 682,
                                           "fingerprint": "c" * 64, "buildGuid": None}}
        pending.write_text(json.dumps(incomplete))
        rejected("missing real build GUID refuses generation", lambda: load_request(pending))
        pending.write_text(json.dumps({**incomplete, "expectedIdentity": {**incomplete["expectedIdentity"], "inputCount": 664}}))
        rejected("old source count refuses generation", lambda: load_request(pending))
        xml = root / "EditMode.xml"
        xml.write_text('<test-run result="Passed" total="2" passed="2" failed="0"><test-case result="Passed"/><test-case result="Passed"/></test-run>')
        count, digest = test_report(xml)
        require(count == 2 and digest == sha(xml.read_bytes()), "actual test XML/hash differ")
        passed.append("actual full passing XML count and SHA")
        xml.write_text('<test-run result="Passed" total="2" passed="2" failed="0"><test-case result="Passed"/></test-run>')
        rejected("test XML aggregate cannot replace complete cases", lambda: test_report(xml))
        xml.write_text('<test-run result="Passed" total="1" passed="1" skipped="1"><test-case result="Passed"/></test-run>')
        rejected("skipped test run refuses full-pass claim", lambda: test_report(xml))
        xml.write_text('<!DOCTYPE test-run><test-run result="Passed" total="1" passed="1"><test-case result="Passed"/></test-run>')
        rejected("XML declarations refused", lambda: test_report(xml))
        app = root / "Frontier Reckoning.app"
        (app / "Contents/MacOS").mkdir(parents=True)
        exe = app / "Contents/MacOS/game"
        exe.write_bytes(b"synthetic executable")
        exe.chmod(0o755)
        (app / "Contents/link").symlink_to("MacOS/game")
        inventory = walk_app(app)
        require(any(r["kind"] == "symlink" for r in inventory) and any(r["mode"] == stat.S_IFREG | 0o755 for r in inventory), "app lstat modes/link missing")
        passed.append("complete app lstat preserves executable modes and symlink body")
        (app / "Contents/escape").symlink_to("../../outside")
        rejected("app dangling/escaping symlink refused", lambda: walk_app(app))
        (app / "Contents/escape").unlink()
        selected = root / "selected.txt"
        selected.write_bytes(b"selected original")
        selections = [{"path": str(selected), "name": PREFIX + "/README.txt"}]
        require(selected_rows(selections)[0]["path"] == str(selected), "selected original path not retained")
        passed.append("explicit original extra inventory path retained")
        alias = root / "selected-link"
        alias.symlink_to(selected)
        rejected("extra symlink refused", lambda: selected_rows([{"path": str(alias), "name": PREFIX + "/alias"}]))
        rejected("private durable store refused", lambda: selected_rows([{"path": str(selected), "name": PREFIX + "/Saves/private.json"}]))
        rejected("gallery historical diagnostics refused", lambda: selected_rows([{"path": str(selected), "name": PREFIX + "-gallery/Diagnostics/old.txt"}], gallery=True))
        rejected("gallery lacking actual PNG refused", lambda: selected_rows([{"path": str(selected), "name": PREFIX + "-gallery/README.txt"}], gallery=True))
        bad_png = root / "bad.png"
        bad_png.write_bytes(b"invalid PNG bytes")
        rejected("gallery fake PNG header refused", lambda: selected_rows([{"path": str(bad_png), "name": PREFIX + "-gallery/bad.png"}], gallery=True))
        zip_path = root / "synthetic.zip"
        with zipfile.ZipFile(zip_path, "w") as archive:
            info = zipfile.ZipInfo("FrontierReckoning/README.md")
            info.create_system = 3
            info.external_attr = (stat.S_IFREG | 0o644) << 16
            archive.writestr(info, b"synthetic source")
        observed = zip_inventory(zip_path, source=True)
        require(observed["members"][0]["gitPath"] == "README.md" and observed["members"][0]["sha256"] == sha(b"synthetic source"), "source ZIP record differs")
        passed.append("source ZIP CRC/SHA/mode inventory and gitPath schema")
        raw = bytearray(zip_path.read_bytes())
        raw[raw.index(b"synthetic source")] ^= 1
        zip_path.write_bytes(raw)
        rejected("CRC detects corrupted ZIP fixture", lambda: zip_inventory(zip_path, source=True))
        output = root / "exclusive.json"
        first = write_new(output, {"fixture": True})
        raw = output.read_bytes()
        require(first["sha256"] == sha(raw), "new output digest differs")
        rejected("existing evidence cannot be overwritten", lambda: write_new(output, {"fixture": False}))
        require(output.read_bytes() == raw, "existing evidence bytes changed")
        passed.append("exclusive new document creation preserves existing bytes")
    return {"result": "Passed", "count": len(passed), "checks": passed,
            "scope": "Automatically removed synthetic local fixtures only; no real release inputs, Unity, project edits, commits or independent acceptance."}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", nargs="?", choices=("payload", "manifest", "freeze"))
    parser.add_argument("--request", type=Path)
    parser.add_argument("--approval", type=Path)
    parser.add_argument("--manifest", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        require(not any((args.command, args.request, args.approval, args.manifest, args.output)), "self-test cannot access real inputs")
        print(json.dumps(self_test(), indent=2))
        return 0
    if not all((args.command, args.request, args.output)):
        parser.error("command, --request and --output are required")
    if args.command in ("manifest", "freeze") and not args.approval:
        parser.error("--approval is required")
    if args.command == "freeze" and not args.manifest:
        parser.error("--manifest is required")
    output = args.output.absolute()
    require(not output.exists() and not output.is_symlink(), "existing output cannot be overwritten")
    request_path = args.request.absolute()
    request, raw = load_request(request_path)
    request_state = identity(request_path)
    if args.command == "payload":
        result = make_approval(request)
    else:
        approved, approval_raw = document(args.approval.absolute())
        result = make_manifest(request, approved) if args.command == "manifest" else make_freeze(
            request, *document(args.manifest.absolute()), approved, approval_raw)
        require(read(args.approval.absolute()) == approval_raw, "approval changed during generation")
    require(identity(request_path) == request_state and read(request_path) == raw, "request changed during generation")
    facts(request)
    print(json.dumps({"generated": args.command, "independentVerification": "pending", **write_new(output, result)}))
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError, TypeError, KeyError, ET.ParseError, subprocess.SubprocessError, zipfile.BadZipFile, RuntimeError) as error:
        print(json.dumps({"result": "Stopped", "errorType": type(error).__name__, "error": str(error),
                          "scope": "Preparation only; no Unity/build/upload/source writes; existing evidence preserved."}), file=sys.stderr)
        sys.exit(2)
