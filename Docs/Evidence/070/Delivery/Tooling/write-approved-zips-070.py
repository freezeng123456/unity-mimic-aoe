#!/usr/bin/env python3
"""Write three NEW 070 ZIPs from a ready request and approved originals.

Source bytes/modes come from sourceSnapshotCommit Git blobs, never a source
working-tree walk. The preparation helper supplies the audited identity and
original-inventory checks; formal writing never imports the independent checker.
Self-tests use automatically removed fixtures, not real delivery packages.
"""

import argparse
import hashlib
import io
import json
import os
from pathlib import Path
import re
import runpy
import stat
import struct
import subprocess
import sys
import tempfile
import time
import zipfile
import zlib
from xml.etree import ElementTree as ET

BASE = Path(__file__).absolute().parent
HELPER = BASE / "prepare-delivery-documents-070.py"
CHECKER = BASE / "independent-zip-verify-070.py"
CHUNK = 1024 * 1024
MAX_ZIP = 2 * 1024 * 1024 * 1024 - 1
EMPTY_SHA = hashlib.sha256(b"").hexdigest()
PREFIX = "Frontier-Reckoning-0.13.0"


def need(condition, message):
    if not condition:
        raise ValueError(message)


def state(path):
    s = path.lstat()
    return (s.st_dev, s.st_ino, s.st_mode, s.st_size, s.st_mtime_ns, s.st_ctime_ns)


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def load_helper():
    need(HELPER.parent.resolve(strict=True) == HELPER.parent and not HELPER.is_symlink(), "helper must be an original regular file")
    before = state(HELPER)
    raw = HELPER.read_bytes()
    h = runpy.run_path(str(HELPER))
    need(state(HELPER) == before and digest(HELPER.read_bytes()) == digest(raw), "helper changed while loading")
    return h, before, digest(raw)


class GitBlobs:
    def __init__(self, repo):
        self.p = subprocess.Popen(["git", "-C", str(repo), "cat-file", "--batch"],
                                  stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)

    def chunks(self, row):
        oid = row["oid"]
        need(re.fullmatch(r"[0-9a-f]{40}(?:[0-9a-f]{24})?", oid), "invalid Git blob ID")
        self.p.stdin.write((oid + "\n").encode("ascii"))
        self.p.stdin.flush()
        header = self.p.stdout.readline().decode("ascii", "strict").strip().split()
        need(len(header) == 3 and header[:2] == [oid, "blob"] and int(header[2]) == row["bytes"], "Git blob reply differs from selected snapshot")
        object_hash = hashlib.sha1() if len(oid) == 40 else hashlib.sha256()
        object_hash.update(("blob " + str(row["bytes"]) + "\0").encode("ascii"))
        left = row["bytes"]
        while left:
            block = self.p.stdout.read(min(CHUNK, left))
            need(block, "truncated Git blob")
            object_hash.update(block)
            left -= len(block)
            yield block
        need(self.p.stdout.read(1) == b"\n" and object_hash.hexdigest() == oid, "Git blob framing/object digest differs")

    def close(self, successful):
        if successful:
            self.p.stdin.close()
            need(self.p.wait(timeout=10) == 0, "Git blob process failed")
        elif self.p.poll() is None:
            self.p.kill()
            self.p.wait(timeout=10)
        if not self.p.stdin.closed:
            self.p.stdin.close()
        self.p.stdout.close()


def link_target(row, raw):
    need(0 < len(raw) <= 4096 and digest(raw) == row["sha256"], "symlink bytes/hash differ")
    target = raw.decode("utf-8", "strict")
    need(not target.startswith("/") and "\\" not in target and not re.match(r"^[A-Za-z]:", target)
         and all(32 <= ord(c) != 127 for c in target), "unsafe symlink target")
    base = row["name"].split("/")[:-1]
    for part in target.split("/"):
        if part in ("", "."):
            continue
        if part == "..":
            need(len(base) > 1, "symlink escapes package root")
            base.pop()
        else:
            base.append(part)
    need(len(base) >= 2, "symlink points outside payload")
    return target


def link_graph(rows, links):
    paths = {r["name"].rstrip("/"): r for r in rows}
    parents = set()
    for path in paths:
        parts = path.split("/")
        parents.update("/".join(parts[:i]) for i in range(1, len(parts)))
    for name, target in links.items():
        pending = name.split("/")[:-1] + target.split("/")
        resolved = []
        substitutions = 0
        while pending:
            part = pending.pop(0)
            if part in ("", "."):
                continue
            if part == "..":
                need(len(resolved) > 1, "chained link escapes package")
                resolved.pop()
                continue
            resolved.append(part)
            path = "/".join(resolved)
            if path in links:
                substitutions += 1
                need(substitutions <= 64, "cyclic/excessive symlink chain")
                resolved.pop()
                pending = links[path].split("/") + pending
            elif pending:
                need(path in parents or paths.get(path, {}).get("kind") == "directory", "symlink traverses missing/regular-file ancestor")
        need(len(resolved) >= 2 and ("/".join(resolved) in paths or "/".join(resolved) in parents), "dangling/out-of-payload symlink")


def scope(kind, rows, h):
    prefix = "FrontierReckoning" if kind == "source" else PREFIX + ("-gallery" if kind == "gallery" else "")
    for row in rows:
        parts = h["safe_path"](row["name"], row["kind"] == "directory")
        need(parts[0] == prefix and not any(p in h["FORBIDDEN"] for p in parts[1:]), "archive member outside approved package scope")
        tail = parts[1:]
        diagnostic = kind == "source" and tail[:5] == ["Docs", "Evidence", "070", "Diagnostics", "Candidate1"]
        need(not any(p in {"Candidate1", "Candidate2", "FrozenCandidate1", "FrozenCandidate2"}
                     for p in (tail[5:] if diagnostic else tail)), "candidate path outside exact source diagnostic exception")
        if kind == "source":
            need(row["kind"] != "directory" and h["source_path"]("/".join(tail))
                 and not any(p.endswith(".app") for p in tail), "source member outside Git allowlist")
        if kind == "gallery":
            need(not any(p.endswith(".app") for p in tail) and "Diagnostics" not in tail, "application/diagnostics in gallery")


def path_rows(approval):
    root = Path(approval["app"]["root"])
    prefix = approval["app"]["prefix"]
    client = []
    for original in approval["app"]["members"]:
        row = dict(original)
        name = row["name"].rstrip("/")
        need(name == prefix or name.startswith(prefix + "/"), "app member outside original prefix")
        relative = name[len(prefix):].lstrip("/")
        row["path"] = str(root / relative)
        client.append(row)
    client.extend(dict(r) for r in approval["clientExtras"])
    return client, [dict(r) for r in approval["gallery"]]


def source_rows(repo, commit, h):
    git = h["git_inventory"](repo, commit, h["source_path"])
    rows = []
    for path, r in sorted(git.items()):
        kind = {stat.S_IFREG: "file", stat.S_IFLNK: "symlink"}.get(stat.S_IFMT(r["mode"]))
        need(kind is not None, "unsupported source Git mode")
        rows.append({"name": "FrontierReckoning/" + path, "kind": kind, **r})
    h["check_inventory"](rows)
    return rows


def path_chunks(row):
    path = Path(row["path"])
    need(path.is_absolute() and path.parent.resolve(strict=True) == path.parent, "approved path has symlink ancestry")
    before = state(path)
    need(before[2] & 0xffff == row["mode"], "approved filesystem type/mode changed")
    if row["kind"] == "directory":
        need(row["bytes"] == 0 and row["sha256"] == EMPTY_SHA, "directory carries payload")
    elif row["kind"] == "symlink":
        yield os.readlink(path).encode("utf-8", "strict")
    else:
        need(stat.S_ISREG(before[2]) and before[3] == row["bytes"], "approved regular-file size/type changed")
        fd = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
        with os.fdopen(fd, "rb") as stream:
            s = os.fstat(stream.fileno())
            need((s.st_dev, s.st_ino) == before[:2], "approved file raced before reading")
            while True:
                block = stream.read(CHUNK)
                if not block:
                    break
                yield block
    need(state(path) == before, "approved filesystem object changed while reading")


def preflight_links(rows, repo=None):
    links = {}
    reader = GitBlobs(repo) if repo is not None else None
    successful = False
    try:
        for row in rows:
            if row["kind"] == "symlink":
                raw = b"".join(reader.chunks(row) if reader else path_chunks(row))
                need(len(raw) == row["bytes"], "symlink size differs")
                links[row["name"]] = link_target(row, raw)
        link_graph(rows, links)
        successful = True
    finally:
        if reader:
            reader.close(successful)


def write_archive(stream, rows, repo=None):
    need(stream.seekable(), "ZIP output must be seekable to avoid data descriptors")
    reader = GitBlobs(repo) if repo is not None else None
    successful = False
    try:
        with zipfile.ZipFile(stream, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6, allowZip64=False) as archive:
            for row in sorted(rows, key=lambda r: r["name"]):
                info = zipfile.ZipInfo(row["name"], (1980, 1, 1, 0, 0, 0))
                info.create_system = 3
                info.create_version = info.extract_version = 20
                info.external_attr = row["mode"] << 16 | (0x10 if row["kind"] == "directory" else 0)
                info.extra = info.comment = b""
                info.compress_type = zipfile.ZIP_DEFLATED if row["kind"] == "file" else zipfile.ZIP_STORED
                info.file_size = row["bytes"]
                amount = 0
                sha = hashlib.sha256()
                chunks = reader.chunks(row) if reader else path_chunks(row)
                with archive.open(info, "w", force_zip64=False) as member:
                    for block in chunks:
                        amount += len(block)
                        need(amount <= row["bytes"], "member grew beyond approved size")
                        sha.update(block)
                        member.write(block)
                need(amount == row["bytes"] and sha.hexdigest() == row["sha256"], "member bytes/hash differ from approved Git/original: " + row["name"])
            need(not archive.comment, "archive comment must remain empty")
        need(stream.tell() <= MAX_ZIP and stream.tell() < zipfile.ZIP64_LIMIT, "controlled ZIP exceeds classic bound")
        successful = True
    finally:
        if reader:
            reader.close(successful)


def output_paths(request, receipt, h):
    directory = Path(request["packageDirectory"])
    need(directory.is_dir() and directory.resolve(strict=True) == directory, "package directory must be real/existing")
    names = request["packages"]
    need(isinstance(names, dict) and set(names) == {"source", "client", "gallery"} and len(set(names.values())) == 3, "three distinct package basenames required")
    paths = {}
    for kind, name in names.items():
        need(isinstance(name, str) and h["safe_path"](name) == [name] and name.endswith(".zip"), "invalid package basename")
        paths[kind] = directory / name
    need(receipt.is_absolute() and receipt.parent.is_dir() and receipt.parent.resolve(strict=True) == receipt.parent, "receipt parent must be real/existing")
    need(receipt not in paths.values(), "receipt collides with ZIP")
    for path in list(paths.values()) + [receipt]:
        need(not path.exists() and not path.is_symlink(), "existing output cannot be overwritten: " + str(path))
    return paths


def write_formal(args, created):
    h, helper_state, helper_sha = load_helper()
    writer = Path(__file__).absolute()
    writer_before, writer_raw = state(writer), h["read"](writer)
    request_path, approval_path = args.request.absolute(), args.approval.absolute()
    h["absolute"](str(request_path))
    h["absolute"](str(approval_path))
    request, request_raw = h["load_request"](request_path)
    approved, approval_raw = h["document"](approval_path)
    before = {p: state(p) for p in (request_path, approval_path)}
    current = h["compare_approval"](request, approved)
    repo = Path(request["repo"])
    client, gallery = path_rows(current)
    plans = {"source": source_rows(repo, request["expectedIdentity"]["sourceSnapshotCommit"], h), "client": client, "gallery": gallery}
    for kind, rows in plans.items():
        h["check_inventory"](rows)
        scope(kind, rows, h)
        preflight_links(rows, repo if kind == "source" else None)
    paths = output_paths(request, args.output.absolute(), h)
    handles = {}
    try:
        for kind in ("source", "client", "gallery"):
            fd = os.open(paths[kind], os.O_RDWR | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0), 0o644)
            created.append(str(paths[kind]))
            handles[kind] = os.fdopen(fd, "w+b")
        for kind in ("source", "client", "gallery"):
            stream = handles[kind]
            write_archive(stream, plans[kind], repo if kind == "source" else None)
            stream.flush()
            os.fsync(stream.fileno())
            stream.close()
        packages = {}
        for kind, path in paths.items():
            inventory = h["zip_inventory"](path, source=kind == "source")
            expected = {r["name"]: h["core"](r) for r in plans[kind]}
            need({r["name"]: h["core"](r) for r in inventory["members"]} == expected, "written ZIP inventory differs from selected originals/Git")
            packages[kind] = {k: inventory[k] for k in ("file", "bytes", "sha256")}
            packages[kind]["members"] = len(inventory["members"])
        h["compare_approval"](request, approved)
        need(all(state(p) == s for p, s in before.items())
             and h["read"](request_path) == request_raw and h["read"](approval_path) == approval_raw, "request/approval changed during writing")
        need(state(HELPER) == helper_state and digest(HELPER.read_bytes()) == helper_sha, "preparation helper changed during writing")
        need(state(writer) == writer_before and h["read"](writer) == writer_raw, "writer changed during writing")
        result = {"schema": 1, "result": "Written", "version": "0.13.0", "release": "070", "createdUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
                  "identity": request["expectedIdentity"], "tests": current["preparationEvidence"]["tests"],
                  "requestSha256": digest(request_raw), "payloadApprovalSha256": digest(approval_raw),
                  "preparationHelper": {"file": str(HELPER), "sha256": helper_sha}, "packages": packages,
                  "writer": {"file": str(writer), "bytes": len(writer_raw), "sha256": digest(writer_raw)},
                  "scope": "Exact Git-snapshot source and approved-original ZIP writing; independent ZIP acceptance, native completeness and upload verification remain pending."}
        return {**result, "receipt": h["write_new"](args.output.absolute(), result)}
    finally:
        for stream in handles.values():
            if not stream.closed:
                stream.close()


def self_test():
    h, _, _ = load_helper()
    checker = runpy.run_path(str(CHECKER))
    checks = []
    def rejected(name, callback):
        try:
            callback()
        except (ValueError, OSError, RuntimeError):
            checks.append(name)
        else:
            raise ValueError("self-test should reject: " + name)
    def git(repo, *args):
        return subprocess.run(["git", "-C", str(repo), *args], check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE).stdout.decode().strip()
    def png():
        def chunk(kind, body):
            return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body) & 0xffffffff)
        return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", 1, 1, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(b"\0\0\0\0\xff")) + chunk(b"IEND", b"")
    with tempfile.TemporaryDirectory(prefix="writer-self-test-070-", dir=BASE) as temp:
        root = Path(temp).resolve()
        repo = root / "repo"
        repo.mkdir()
        git(repo, "init", "-q")
        git(repo, "config", "user.name", "Synthetic Writer Test")
        git(repo, "config", "user.email", "synthetic@example.invalid")
        git(repo, "config", "core.filemode", "true")
        for path, body in {"README.md": b"frozen committed README\n", "Tools/tool.py": b"#!/usr/bin/env python3\n", "Assets/core.txt": b"frozen runtime\n", "SourceAssets/asset.txt": b"asset\n", "Docs/Evidence/070/UI/曲河.png": png(), "Docs/Evidence/070/Diagnostics/Candidate1/truth.txt": b"honest diagnostic", "Docs/Evidence/069/old.txt": b"old", "Library/cache": b"excluded"}.items():
            file = repo / path
            file.parent.mkdir(parents=True, exist_ok=True)
            file.write_bytes(body)
        (repo / "Tools/tool.py").chmod(0o755)
        (repo / "SourceAssets/alias").symlink_to("asset.txt")
        git(repo, "add", ".")
        git(repo, "commit", "-qm", "synthetic frozen snapshot")
        commit = git(repo, "rev-parse", "HEAD")
        (repo / "README.md").write_bytes(b"uncommitted replacement must not ship")
        (repo / "Tools/tool.py").chmod(0o644)
        (repo / "Docs/untracked.txt").write_bytes(b"must not ship")
        source = source_rows(repo, commit, h)
        names = {r["name"] for r in source}
        need("FrontierReckoning/Docs/untracked.txt" not in names and not any("/069/" in n or "/Library/" in n for n in names), "source allowlist failed")
        checks.append("Git committed allowlist excludes untracked, cache and historical release files")
        need(next(r for r in source if r["name"].endswith("Tools/tool.py"))["mode"] == stat.S_IFREG | 0o755, "Git executable mode lost")
        checks.append("source mode comes from Git despite working-tree chmod")
        app = root / "Frontier Reckoning.app"
        exe = app / "Contents/MacOS/Frontier Reckoning"
        exe.parent.mkdir(parents=True)
        exe.write_bytes(b"synthetic executable")
        exe.chmod(0o755)
        resources = app / "Contents/Resources"
        resources.mkdir()
        (resources / "data.txt").write_bytes(b"data")
        (resources / "alias").symlink_to("data.txt")
        build = root / "build-identity.json"
        build.write_text('{"fixture":true}\n')
        image = root / "actual.png"
        image.write_bytes(png())
        metadata = root / "actual.json"
        metadata.write_text('{"fixture":true}\n')
        approval = {"app": {"root": str(app), "prefix": h["APP_PREFIX"], "members": h["walk_app"](app)},
                    "clientExtras": h["selected_rows"]([{"path": str(build), "name": PREFIX + "/client-build.json"}]),
                    "gallery": h["selected_rows"]([{"path": str(image), "name": PREFIX + "-gallery/Native/UI/曲河.png"}, {"path": str(metadata), "name": PREFIX + "-gallery/Native/UI/曲河.json"}], gallery=True)}
        client, gallery = path_rows(approval)
        plans = {"source": source, "client": client, "gallery": gallery}
        for kind, rows in plans.items():
            h["check_inventory"](rows)
            scope(kind, rows, h)
            preflight_links(rows, repo if kind == "source" else None)
            path = root / (kind + ".zip")
            with path.open("x+b") as stream:
                write_archive(stream, rows, repo if kind == "source" else None)
            inventory = h["zip_inventory"](path, source=kind == "source")
            actual = {r["name"]: h["core"](r) for r in inventory["members"]}
            need(actual == {r["name"]: h["core"](r) for r in rows}, "synthetic ZIP inventory differs")
            checker["verify_zip"](path, kind, inventory)
            checks.append(kind + " actual generated ZIP passes independent raw central/local/header/CRC/SHA/mode/path checks")
        with zipfile.ZipFile(root / "source.zip") as z:
            need(z.read("FrontierReckoning/README.md") == b"frozen committed README\n", "writer read working-tree bytes")
            need(z.read("FrontierReckoning/SourceAssets/alias") == b"asset.txt", "Git symlink body lost")
        checks.append("source reads committed bytes and literal Git symlink body")
        with zipfile.ZipFile(root / "client.zip") as z:
            need(z.getinfo(h["APP_PREFIX"] + "/Contents/MacOS/Frontier Reckoning").external_attr >> 16 == stat.S_IFREG | 0o755
                 and z.read(h["APP_PREFIX"] + "/Contents/Resources/alias") == b"data.txt"
                 and z.getinfo(h["APP_PREFIX"] + "/").is_dir(), "app modes/directories/link lost")
        checks.append("full app lstat directories, executable mode and symlink body preserved")
        with zipfile.ZipFile(root / "gallery.zip") as z:
            entry = z.getinfo(PREFIX + "-gallery/Native/UI/曲河.png")
            need(entry.flag_bits == 0x0800 and not entry.extra and not entry.comment and checker["png_dimensions"](z, entry)["width"] == 1, "UTF8/PNG serialization failed")
        checks.append("Unicode names use canonical UTF8 flag with no extras/comment and valid PNG dimensions")
        one = dict(gallery[0])
        first = io.BytesIO()
        second = io.BytesIO()
        write_archive(first, [one])
        write_archive(second, [one])
        need(first.getvalue() == second.getvalue(), "ZIP determinism failed")
        checks.append("same original inventory generates deterministic ZIP bytes")
        rejected("mutated approved file hash refuses completion", lambda: write_archive(io.BytesIO(), [dict(one, sha256="0" * 64)]))
        rejected("mutated approved file size refuses completion", lambda: write_archive(io.BytesIO(), [dict(one, bytes=one["bytes"] + 1)]))
        rejected("mutated approved mode refuses completion", lambda: write_archive(io.BytesIO(), [dict(one, mode=stat.S_IFREG | 0o755)]))
        partial = root / "preserved-partial.zip"
        def failed_new_zip():
            with partial.open("x+b") as stream:
                write_archive(stream, [dict(one, sha256="0" * 64)])
        rejected("failed newly created ZIP is reported without cleanup", failed_new_zip)
        need(partial.is_file() and partial.stat().st_size > 0, "failed ZIP evidence was deleted")
        partial_bytes = partial.read_bytes()
        rejected("failed ZIP cannot be reused or overwritten", failed_new_zip)
        need(partial.read_bytes() == partial_bytes, "failed ZIP evidence was overwritten")
        checks.append("partial output bytes survive failure and exclusive retry unchanged")
        rejected("nonseekable writer refuses descriptors", lambda: write_archive(type("Nonseekable", (), {"seekable": lambda _: False})(), [one]))
        rejected("duplicate case alias inventory rejected", lambda: h["check_inventory"]([one, dict(one, name=one["name"].replace("Native", "native"))]))
        rejected("escaping Git symlink rejected", lambda: link_target({"name": "FrontierReckoning/SourceAssets/link", "sha256": digest(b"../../outside")}, b"../../outside"))
        rejected("dangling Git symlink rejected", lambda: link_graph(source, {"FrontierReckoning/SourceAssets/alias": "missing"}))
        rejected("cyclic Git symlink rejected", lambda: link_graph(source, {"FrontierReckoning/SourceAssets/alias": "alias"}))
        rejected("gallery candidate archive path rejected", lambda: scope("gallery", [dict(one, name=PREFIX + "-gallery/Candidate2/image.png")], h))
        rejected("source misplaced historical candidate rejected", lambda: scope("source", [dict(source[0], name="FrontierReckoning/Docs/Candidate1/old.txt")], h))
        fake = {"packageDirectory": str(root), "packages": {"source": "fresh-source.zip", "client": "fresh-client.zip", "gallery": "fresh-gallery.zip"}}
        existing = root / "fresh-client.zip"
        existing.write_bytes(b"must survive")
        rejected("any existing package refuses entire batch before reservation", lambda: output_paths(fake, root / "receipt.json", h))
        need(existing.read_bytes() == b"must survive" and not (root / "fresh-source.zip").exists() and not (root / "fresh-gallery.zip").exists(), "existing output overwritten/partial batch created")
        checks.append("existing output unchanged and absent outputs not reserved after preflight refusal")
        existing.unlink()
        (root / "receipt.json").symlink_to(existing)
        rejected("dangling receipt symlink refused", lambda: output_paths(fake, root / "receipt.json", h))
        rejected("receipt ZIP collision refused", lambda: output_paths(fake, root / "fresh-source.zip", h))
        pending = root / "pending.json"
        pending.write_text('{"schema":1,"ready":false}\n')
        rejected("formal pending request refused before production output access", lambda: h["load_request"](pending))
        checks.append("fixtures automatically removed; no real request/approval/packages/Unity/project accessed")
    return {"schema": 1, "result": "Passed", "count": len(checks), "checks": checks,
            "scope": "Isolated temporary synthetic Git/app/gallery ZIPs only; no actual release packaging or acceptance."}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--request", type=Path)
    parser.add_argument("--approval", type=Path)
    parser.add_argument("--output", type=Path, help="NEW writer receipt, never an existing evidence path")
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    created = []
    try:
        if args.self_test:
            need(not any((args.request, args.approval, args.output)), "self-test cannot access formal inputs")
            print(json.dumps(self_test(), ensure_ascii=False, indent=2))
            return 0
        if not all((args.request, args.approval, args.output)):
            parser.error("--request, --approval and --output are required")
        result = write_formal(args, created)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0
    except (OSError, ValueError, TypeError, KeyError, RuntimeError, ET.ParseError, zipfile.BadZipFile, zipfile.LargeZipFile, subprocess.SubprocessError) as error:
        result = {"schema": 1, "result": "Stopped", "errorType": type(error).__name__, "error": str(error),
                  "createdPaths": created, "scope": "No independent acceptance. Any partial newly created packages are preserved; never reuse or overwrite them."}
        if created and args.output:
            try:
                h, _, _ = load_helper()
                result["failureReceipt"] = h["write_new"](args.output.absolute(), result)
            except (OSError, ValueError):
                pass
        print(json.dumps(result, ensure_ascii=False), file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
