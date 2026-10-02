#!/usr/bin/env python3
"""Audit the one approved .31 -> .32 menu/version delta; never edit source."""
import argparse
import hashlib
import json
import math
import plistlib
import re
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path, PurePosixPath
from xml.etree import ElementTree as ET

BASELINE_COMMIT = "50d303b76519a3a9b448c27cd2cdeb22779a1e5d"
BASELINE_FINGERPRINT = "1b45f97f3d90c9b76388419492a83c5622ccb2cf3a365030044403ccd309c1c7"
BASELINE_SOURCE_EVIDENCE_SHA = "bf8ca06fcfc8e609e87b7b319bdf98a6ba6d70a4d130576b63cc50117742048b"
BASELINE_RECEIPT_SNAPSHOT_SHA = "9218690cdc8b77154299f0638334ac86d33d9cff45c10812fcb7ddbe9f115aca"
OLD_VERSION, NEW_VERSION = "0.2.31", "0.2.32"
OLD_GUID = "128a731d440e4b7a94abfcc74b88fa97"
SOURCE_DIRS = ("Assets", "Packages", "ProjectSettings", "Tools")
SOURCE_COUNT = 357
HUD = "Assets/Frontier/Presentation/FrontierHud.cs"
SETTINGS = "ProjectSettings/ProjectSettings.asset"
REPLACEMENTS = {
    HUD: (
        ('"FIVE-ERA LAND GAME · DEVELOPMENT"', '"FIVE-ERA LAND GAME"'),
        (
            r'"Two civilizations · Custom decks · Original project rules\nTrade, treasures and final audiovisual quality are still in development.\nIndependent project. Not affiliated with Age of Empires or its publishers."',
            r'"Two civilizations · Custom decks · Original project rules\nBanks · Trade routes · Guarded treasures · Original visuals and audio\nIndependent project. Not affiliated with Age of Empires or its publishers."',
        ),
    ),
    SETTINGS: (("  bundleVersion: 0.2.31\n", "  bundleVersion: 0.2.32\n"),),
}
RUNTIME_FILES = (
    "Contents/Info.plist",
    "Contents/MacOS/Frontier Reckoning",
    "Contents/Resources/Data/Managed/Frontier.Runtime.dll",
)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, "Duplicate JSON key: " + key)
        result[key] = value
    return result


def reject_constant(value):
    raise ValueError("Non-finite JSON constant: " + value)


def finite_float(value):
    result = float(value)
    require(math.isfinite(result), "Non-finite JSON number: " + value)
    return result


def parse_json(data):
    return json.loads(data, object_pairs_hook=unique_object, parse_constant=reject_constant,
                      parse_float=finite_float)


def strict_json(path):
    result = parse_json(path.read_bytes())
    require(isinstance(result, dict), str(path) + ": expected JSON object")
    return result


def fingerprint(files):
    # Exactly the canonical serialization used by Tools/verification_evidence.py.
    return sha(json.dumps(files, sort_keys=True, separators=(",", ":")).encode())


def source_path(name):
    path = PurePosixPath(name)
    return (bool(path.parts) and path.parts[0] in SOURCE_DIRS
            and not path.is_absolute() and ".." not in path.parts
            and not any(part.startswith(".") or part == "__pycache__" for part in path.parts))


def read_current(root):
    files = {}
    for directory in SOURCE_DIRS:
        require((root / directory).is_dir(), "Missing source directory: " + directory)
        for path in sorted((root / directory).rglob("*")):
            if path.is_file() and source_path(path.relative_to(root).as_posix()):
                require(not path.is_symlink(), "Source symlink is not an ordinary baseline file: " + str(path))
                files[path.relative_to(root).as_posix()] = path.read_bytes()
    return files


def git(root, *arguments):
    process = subprocess.run(["git", "-C", str(root), *arguments], capture_output=True, check=False)
    require(process.returncode == 0, "Git baseline read failed: " + process.stderr.decode(errors="replace").strip())
    return process.stdout


def read_baseline(root):
    names = git(root, "ls-tree", "-rz", "--name-only", BASELINE_COMMIT).decode().split("\0")
    names = sorted(name for name in names if name and source_path(name))
    require(len(names) == SOURCE_COUNT, "Baseline Git source inventory is not exactly 357 files")
    return {name: git(root, "show", BASELINE_COMMIT + ":" + name) for name in names}


def source_manifest(evidence):
    require(type(evidence.get("schema")) is int and evidence["schema"] == 1, "Unsupported source-evidence schema")
    files = evidence.get("files")
    require(isinstance(files, dict) and len(files) == SOURCE_COUNT, "Source evidence must list exactly 357 files")
    require(all(isinstance(name, str) and source_path(name) and isinstance(digest, str)
                and re.fullmatch(r"[0-9a-f]{64}", digest) for name, digest in files.items()),
            "Invalid source evidence path or SHA256")
    require(fingerprint(files) == evidence.get("sourceFingerprint"), "Source evidence fingerprint is inconsistent")
    return files


def expected_release(old_files):
    require(len(old_files) == SOURCE_COUNT, "Expected precisely 357 baseline source files")
    result = dict(old_files)
    for name, replacements in REPLACEMENTS.items():
        require(name in result, "Approved replacement file absent: " + name)
        data = result[name]
        for before, after in replacements:
            before_bytes, after_bytes = before.encode("utf-8"), after.encode("utf-8")
            require(data.count(before_bytes) == 1, "Approved old literal must occur exactly once: " + name + " / " + before)
            require(after_bytes != before_bytes, "Replacement must change bytes")
            data = data.replace(before_bytes, after_bytes, 1)
        result[name] = data
    return result


def verify_source_delta(old_files, current):
    expected = expected_release(old_files)
    require(set(current) == set(old_files), "Source file set changed; added=" + repr(sorted(set(current) - set(old_files)))
            + "; removed=" + repr(sorted(set(old_files) - set(current))))
    wrong = sorted(name for name in old_files if current[name] != expected[name])
    require(not wrong, "Bytes differ from the exact approved replacement: " + ", ".join(wrong))
    changed = sorted(name for name in old_files if current[name] != old_files[name])
    require(changed == sorted(REPLACEMENTS), "Expected exactly the two approved changed files")
    return {
        "fileCount": SOURCE_COUNT, "changedFileCount": len(changed), "unchangedFileCount": SOURCE_COUNT - len(changed),
        "changedFiles": [{"path": name, "oldSha256": sha(old_files[name]), "newSha256": sha(current[name]),
                          "exactApprovedBytes": True} for name in changed],
        "buildToolsBytesUnchanged": current["Assets/Frontier/Editor/BuildTools.cs"] == old_files["Assets/Frontier/Editor/BuildTools.cs"],
    }


def instant(text):
    require(isinstance(text, str), "Missing test/evidence timestamp")
    # Unity's test-run timestamps end in Z; source-evidence uses an ISO timezone.
    result = datetime.fromisoformat(text.replace("Z", "+00:00"))
    require(result.tzinfo is not None, "Timestamp must identify a timezone")
    return result


def verify_xml(path, mode, expected_names=None):
    node = ET.parse(path).getroot()
    expected_count = {"EditMode": 214, "PlayMode": 210}[mode]
    require(node.tag == "test-run" and node.get("result") == "Passed", mode + " XML is not Passed test-run")
    require(all(node.get(name) == str(expected_count) for name in ("testcasecount", "total", "passed")),
            mode + " must retain its exact baseline full test count")
    require(all(node.get(name) == "0" for name in ("failed", "skipped", "inconclusive")), mode + " contains non-passing tests")
    cases = node.findall(".//test-case")
    names = [case.get("fullname") for case in cases]
    require(len(cases) == expected_count and all(isinstance(name, str) and name for name in names)
            and len(set(names)) == expected_count and all(case.get("result") == "Passed" for case in cases),
            mode + " test-case nodes must be complete, unique and individually Passed")
    require(expected_names is None or set(names) == set(expected_names), mode + " test-case inventory changed")
    start, end = instant(node.get("start-time")), instant(node.get("end-time"))
    require(start < end, mode + " test-run timestamps are invalid")
    return {"passed": expected_count, "sha256": sha(path.read_bytes()), "names": set(names),
            "startedAt": start, "finishedAt": end,
            "testCaseInventorySha256": sha(json.dumps(sorted(names), separators=(",", ":")).encode())}


def verify_tests(directory, evidence, old_results=None):
    reports = evidence.get("reports")
    require(isinstance(reports, dict) and set(reports) == {"EditMode", "PlayMode"}, "Missing or additional Unity test modes")
    results = {}
    for mode in ("EditMode", "PlayMode"):
        record = reports[mode]
        require(isinstance(record, dict) and record.get("file") == mode + ".xml", "Unexpected test report filename")
        result = verify_xml(directory / record["file"], mode, old_results[mode]["names"] if old_results else None)
        require(type(record.get("passed")) is int and record["passed"] == result["passed"]
                and record.get("sha256") == result["sha256"], mode + " XML does not match its source evidence")
        if old_results:
            old = old_results[mode]
            require(result["sha256"] != old["sha256"], mode + " reused exact old XML bytes instead of a fresh run")
            require(result["startedAt"] > old["finishedAt"], mode + " is not newer than the old full run")
        results[mode] = result
    require(instant(evidence.get("recordedAtUtc")) >= max(result["finishedAt"] for result in results.values()),
            "Source evidence was recorded before its XML runs finished")
    return results


def receipt_identity(receipt, version, source_fp):
    require(receipt.get("appVersion") == version, "Build receipt appVersion mismatch")
    require(receipt.get("sourceFingerprint") == source_fp, "Build receipt sourceFingerprint mismatch")
    guid = receipt.get("buildGuid")
    require(isinstance(guid, str) and re.fullmatch(r"[0-9a-f]{32}", guid) and guid != "0" * 32,
            "Build receipt needs a nonzero lowercase 32-hex native GUID")
    return guid


def verify_app(app, old_receipt, new_receipt, current_fp):
    guid = receipt_identity(new_receipt, NEW_VERSION, current_fp)
    require(guid != OLD_GUID, "New client reused the .31 GUID")
    plist_path = app / "Contents/Info.plist"
    plist = plistlib.loads(plist_path.read_bytes())
    require(plist.get("CFBundleShortVersionString") == NEW_VERSION
            and plist.get("CFBundleVersion") == NEW_VERSION, "Actual client plist version is not .32")
    boot_path = app / "Contents/Resources/Data/boot.config"
    boot = boot_path.read_bytes()
    guid_lines = [line for line in boot.splitlines() if line.startswith(b"build-guid=")]
    require(len(guid_lines) == 1 and guid_lines[0] == ("build-guid=" + guid).encode(),
            "Actual boot.config GUID is missing, duplicate or unlike new receipt")
    runtime = new_receipt.get("compiledRuntimeSha256")
    require(isinstance(runtime, dict) and set(RUNTIME_FILES).issubset(runtime), "New receipt omits required compiled file hashes")
    files = {}
    for name, digest in runtime.items():
        path = PurePosixPath(name)
        require(isinstance(name, str) and not path.is_absolute() and ".." not in path.parts
                and path.parts and path.parts[0] == "Contents" and isinstance(digest, str)
                and re.fullmatch(r"[0-9a-f]{64}", digest), "Invalid compiled runtime path/hash")
        data = (app / name).read_bytes()
        require(sha(data) == digest, "Actual client runtime bytes differ from receipt: " + name)
        files[name] = {"bytes": len(data), "sha256": digest}
    dll = "Contents/Resources/Data/Managed/Frontier.Runtime.dll"
    require(runtime[dll] != old_receipt.get("compiledRuntimeSha256", {}).get(dll),
            "New client kept the old runtime DLL despite the approved HUD literal change")
    return {"appPath": str(app), "appVersionFromPlist": NEW_VERSION, "buildGuidFromBootConfig": guid,
            "bootConfigSha256": sha(boot), "receiptRuntimeFiles": files}


def old_native_inventory(directory, new_guid=None):
    # .30 attempts already live in this archive too: preserve rather than relabel them.
    records = []
    for path in sorted(directory.rglob("*.json")):
        data = path.read_bytes()
        record = parse_json(data)
        if not isinstance(record, dict) or "buildGuid" not in record:
            continue
        guid, version = record["buildGuid"], record.get("appVersion")
        require(version != NEW_VERSION, "Old 055 evidence was relabeled .32: " + str(path))
        require(new_guid is None or guid != new_guid, "New .32 GUID was written into old 055 evidence: " + str(path))
        if guid == OLD_GUID:
            require(version in (None, OLD_VERSION), "Old .31 GUID/version pairing was changed: " + str(path))
        if version == OLD_VERSION:
            require(guid == OLD_GUID, "Old .31 evidence has a different GUID: " + str(path))
        records.append({"path": str(path), "bytes": len(data), "sha256": sha(data),
                        "appVersion": version, "buildGuid": guid})
    return records


def verify_output_path(args):
    target = args.output.resolve()
    root, old_dir, new_dir, app = (path.resolve() for path in
                                   (args.root, args.old_evidence, args.new_evidence, args.app))
    require(target.parent == new_dir and target.suffix == ".json"
            and target.name.startswith("release-equivalence"),
            "Output must be a release-equivalence*.json result directly in the new evidence directory")
    require(not target.is_relative_to(old_dir) and not target.is_relative_to(app)
            and not any(target.is_relative_to(root / directory) for directory in SOURCE_DIRS),
            "Output cannot overwrite old evidence, source or the client")
    return target


def public_tests(results):
    return {mode: {name: (value.isoformat() if isinstance(value, datetime) else value)
                   for name, value in record.items() if name != "names"} for mode, record in results.items()}


def audit(args):
    root = args.root.resolve()
    old_dir, new_dir = args.old_evidence.resolve(), args.new_evidence.resolve()
    require(old_dir != new_dir, "Old and new evidence directories must be independent")
    head = git(root, "rev-parse", "HEAD").decode().strip()
    old_source_path, new_source_path = old_dir / "Tests/source-evidence.json", new_dir / "Tests/source-evidence.json"
    old_receipt_path, new_receipt_path = old_dir / "build-receipt.json", new_dir / "build-receipt.json"
    old_bundle_path = old_dir / "client-bundle-manifest.json"
    input_bytes = {path: path.read_bytes() for path in
                   (old_source_path, new_source_path, old_receipt_path, new_receipt_path, old_bundle_path)}
    for directory in (old_dir, new_dir):
        for mode in ("EditMode", "PlayMode"):
            path = directory / "Tests" / (mode + ".xml")
            input_bytes[path] = path.read_bytes()
    require(sha(input_bytes[old_source_path]) == BASELINE_SOURCE_EVIDENCE_SHA,
            "Frozen old source-evidence bytes changed, including the old test report identities")
    old_source, new_source = strict_json(old_source_path), strict_json(new_source_path)
    old_files = read_baseline(root)
    old_hashes = {name: sha(data) for name, data in old_files.items()}
    require(source_manifest(old_source) == old_hashes, "055 source evidence does not match all baseline commit files")
    require(old_source["sourceFingerprint"] == BASELINE_FINGERPRINT, "055 is not the frozen .31 source fingerprint")
    current = read_current(root)
    delta = verify_source_delta(old_files, current)
    current_hashes = {name: sha(data) for name, data in current.items()}
    current_fp = fingerprint(current_hashes)
    require(source_manifest(new_source) == current_hashes, "056 full tests are not bound to every current source file")
    require(new_source["sourceFingerprint"] == current_fp and current_fp != BASELINE_FINGERPRINT,
            "056 source fingerprint is not the independently changed .32 source")
    old_tests = verify_tests(old_dir / "Tests", old_source)
    new_tests = verify_tests(new_dir / "Tests", new_source, old_tests)
    old_receipt, new_receipt = strict_json(old_receipt_path), strict_json(new_receipt_path)
    require(receipt_identity(old_receipt, OLD_VERSION, BASELINE_FINGERPRINT) == OLD_GUID, "055 build identity changed")
    old_bundle = strict_json(old_bundle_path)
    require(receipt_identity(old_bundle, OLD_VERSION, BASELINE_FINGERPRINT) == OLD_GUID, "055 archived client manifest identity changed")
    require(old_bundle.get("sources", {}).get("sourceEvidence", {}).get("sha256") == BASELINE_SOURCE_EVIDENCE_SHA,
            "Old bundle manifest does not bind the frozen source evidence bytes")
    snapshot = old_bundle.get("buildReceiptSnapshotUtf8")
    require(isinstance(snapshot, str) and sha(snapshot.encode()) == BASELINE_RECEIPT_SNAPSHOT_SHA
            and old_bundle.get("sources", {}).get("buildReceipt", {}).get("sha256") == BASELINE_RECEIPT_SNAPSHOT_SHA,
            "Old bundle receipt snapshot SHA is inconsistent")
    snapshot_receipt = parse_json(snapshot)
    require(isinstance(snapshot_receipt, dict) and all(snapshot_receipt.get(field) == old_receipt.get(field)
            for field in ("appVersion", "buildGuid", "sourceFingerprint", "compiledRuntimeSha256")),
            "Old receipt identity/runtime differs from its archived snapshot")
    require(all(old_bundle.get("bundleFiles", {}).get(name, {}).get("sha256") == digest
                for name, digest in old_receipt["compiledRuntimeSha256"].items()),
            "Old receipt compiled hashes differ from the archived bundle file manifest")
    client = verify_app(args.app.resolve(), old_receipt, new_receipt, current_fp)
    native = old_native_inventory(old_dir, client["buildGuidFromBootConfig"])
    # An archive can be produced later by the distribution driver; this audit reads the actual .app.
    require(read_current(root) == current, "Source changed during release equivalence audit")
    require(git(root, "rev-parse", "HEAD").decode().strip() == head, "HEAD changed during release equivalence audit")
    require(all(path.read_bytes() == data for path, data in input_bytes.items()), "Evidence input changed during audit")
    require(verify_app(args.app.resolve(), old_receipt, new_receipt, current_fp) == client,
            "Native client changed during release equivalence audit")
    require(old_native_inventory(old_dir, client["buildGuidFromBootConfig"]) == native,
            "Old native identity inventory changed during release equivalence audit")
    return {
        "schema": 1, "recordedAtUtc": datetime.now(timezone.utc).isoformat(), "status": "passed",
        "scope": "Exact approved two-file source delta, fresh source-bound full Unity tests and independent native client identity. No new .32 gameplay, audio, performance or rendered-menu acceptance is inferred.",
        "baselineCommit": BASELINE_COMMIT, "currentHeadObserved": head,
        "source": delta, "approvedReplacements": REPLACEMENTS,
        "oldRelease": {"appVersion": OLD_VERSION, "buildGuid": OLD_GUID, "sourceFingerprint": BASELINE_FINGERPRINT,
                       "tests": public_tests(old_tests), "nativeIdentityRecords": native},
        "newRelease": {"appVersion": NEW_VERSION, "buildGuid": new_receipt["buildGuid"], "sourceFingerprint": current_fp,
                       "tests": public_tests(new_tests), "client": client},
        "evidenceFiles": [{"path": str(path), "bytes": len(data), "sha256": sha(data)}
                          for path, data in input_bytes.items()],
        "continuity": "055 native observations retain their recorded versions and GUIDs. Exact source continuity supports use as prior gameplay/feature evidence; it does not turn them into observations of the .32 client. Root must separately archive the new menu/start/exit smoke and distribution manifest.",
    }


def main():
    root = Path(__file__).resolve().parents[3]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=root)
    parser.add_argument("--old-evidence", type=Path, default=root / "Docs/Evidence/055")
    parser.add_argument("--new-evidence", type=Path, default=root / "Docs/Evidence/056")
    parser.add_argument("--app", type=Path, default=root / "Builds/macOS/Frontier Reckoning.app")
    parser.add_argument("--output", type=Path, default=root / "Docs/Evidence/056/release-equivalence.json")
    args = parser.parse_args()
    try:
        verify_output_path(args)
    except ValueError as error:
        print(json.dumps({"status": "failed", "outputWritten": False, "error": str(error)}, ensure_ascii=False))
        return 1
    try:
        result = audit(args)
        code = 0
    except (OSError, ValueError, KeyError, TypeError, ET.ParseError, plistlib.InvalidFileException) as error:
        result = {"schema": 1, "recordedAtUtc": datetime.now(timezone.utc).isoformat(), "status": "failed",
                  "scope": "Release equivalence check; failure is retained and does not establish native acceptance.",
                  "error": str(error), "baselineCommit": BASELINE_COMMIT}
        code = 1
    target = verify_output_path(args)
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({"status": result["status"], "output": str(args.output), "error": result.get("error")}, ensure_ascii=False))
    return code


if __name__ == "__main__":
    sys.exit(main())
