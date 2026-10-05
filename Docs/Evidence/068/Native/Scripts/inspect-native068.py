#!/usr/bin/env python3
"""Inspect candidate native evidence; never operate a game or inspect personal data.

JSON metadata completes a Capture PNG pair. Metadata verification is deliberately
separate from manually observed UI and from validation on two physical Macs.
Only Python's standard library is required. All CLI paths must resolve inside
Candidate1, Candidate2 or Candidate3 of this release, including symlink targets. Inputs
must belong to one candidate; report output must be under its Native directory.
"""
import argparse
import datetime as dt
import hashlib
import json
import math
import os
from pathlib import Path
import re
import struct
import tempfile

CANDIDATE_ROOT = Path(__file__).resolve().parent.parent
RELEASE_ROOT = CANDIDATE_ROOT.parent
ALLOWED_ROOTS = tuple(RELEASE_ROOT / name for name in ("Candidate1", "Candidate2", "Candidate3"))
MAX_JSON = 4 * 1024 * 1024
MAX_PNG = 64 * 1024 * 1024
MAX_LOG = 64 * 1024 * 1024
MAX_CAPTURES = 2048
MAX_LOG_RECORDS = 20000
GUID = re.compile(r"^[0-9a-fA-F]{32}$")
HASH = re.compile(r"^[0-9a-fA-F]{64}$")
CAPTURE_NAME = re.compile(r"^Frontier-[0-9]{8}-[0-9]{6}-[0-9]{3}-f[0-9]+-[0-9a-f]{32}\.json$")
LOG_MARKER = re.compile(r"FRONTIER_LAN_(APPLY|RESULT)\s+")
LOG_FIELDS = re.compile(r"(?:^|\s)(peer|sequence|hostSequence|accepted|protocolRejected|step|ordinal|revision)=([^\s]+)")


def bounded_path(value):
    path = Path(value).expanduser().resolve()
    for root in ALLOWED_ROOTS:
        try:
            path.relative_to(root)
            return path
        except ValueError:
            pass
    raise ValueError("Every input/output path must resolve inside Candidate1, Candidate2 or Candidate3")


def relative(path):
    return str(bounded_path(path).relative_to(RELEASE_ROOT))


def candidate_for(path):
    return RELEASE_ROOT / bounded_path(path).relative_to(RELEASE_ROOT).parts[0]


def reject_duplicates(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate JSON field")
        result[key] = value
    return result


def read_json(path):
    path = bounded_path(path)
    if path.stat().st_size > MAX_JSON:
        raise ValueError("JSON exceeds inspection size limit")
    with path.open("r", encoding="utf-8-sig") as source:
        return json.load(source, object_pairs_hook=reject_duplicates, parse_float=finite_float,
                         parse_constant=lambda _: (_ for _ in ()).throw(ValueError("Nonfinite JSON number")))


def finite_float(value):
    number = float(value)
    if not math.isfinite(number):
        raise ValueError("Nonfinite JSON number")
    return number


def sha256(path, limit):
    path = bounded_path(path)
    if path.stat().st_size > limit:
        raise ValueError("File exceeds inspection size limit")
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def check(name, valid, expected=None, actual=None):
    row = {"check": name, "ok": bool(valid)}
    if expected is not None:
        row["expected"] = expected
    if actual is not None:
        row["actual"] = actual
    return row


def finite_number(value):
    if type(value) not in (int, float):
        return False
    try:
        return math.isfinite(value)
    except OverflowError:
        return False


def safe_scalar(value):
    # No free-form connection text, credentials, raw JSON or raw log line is
    # copied to the report. GUID/hash fields and numeric/boolean fields suffice.
    if isinstance(value, str):
        return value if GUID.fullmatch(value) or HASH.fullmatch(value) else "[invalid format]"
    if type(value) is float and not math.isfinite(value):
        return "[invalid number]"
    return value if value is None or type(value) in (int, float, bool) else "[invalid type]"


def capture(path, expected_role, local_slot):
    row = {"metadata": relative(path), "checks": [], "status": "failed"}
    try:
        metadata = read_json(path)
        if not isinstance(metadata, dict):
            raise ValueError("Capture metadata must be an object")
        checks = row["checks"]
        checks.append(check("schema5", type(metadata.get("schemaVersion")) is int and metadata["schemaVersion"] == 5))
        name = metadata.get("pngFile")
        if not isinstance(name, str) or name != path.with_suffix(".png").name:
            raise ValueError("PNG must be the same-stem file beside its completion JSON")
        png = bounded_path(path.parent / name)
        if not png.is_file():
            raise ValueError("Completion JSON has no PNG")
        png_hash = sha256(png, MAX_PNG)
        with png.open("rb") as source:
            header = source.read(24)
        if len(header) != 24 or header[:8] != b"\x89PNG\r\n\x1a\n" or header[12:16] != b"IHDR":
            raise ValueError("PNG signature/IHDR is invalid")
        width, height = struct.unpack(">II", header[16:24])
        advertised = metadata.get("pngSha256")
        row["png"] = relative(png)
        row["pngSha256"] = png_hash
        row["pngDimensions"] = [width, height]
        checks.append(check("png_sha256", isinstance(advertised, str) and HASH.fullmatch(advertised) and png_hash == advertised.lower()))
        checks.append(check("png_dimensions", type(metadata.get("width")) is int and type(metadata.get("height")) is int and width == metadata["width"] and height == metadata["height"] and width > 0 and height > 0))
        lan = metadata.get("lan")
        if not isinstance(lan, dict):
            raise ValueError("Capture has no LAN metadata object")
        role = lan.get("role")
        if role in ("Single", "Replay"):
            row["phase"] = "not_lan"
            row["status"] = "ignored_non_lan" if all(c["ok"] for c in checks) else "failed"
            return row
        phase = "lobby" if metadata.get("inMenu") is True else "match"
        row["phase"] = phase
        checks.append(check("role", role == expected_role, expected_role,
                            role if role in ("LanHost", "LanClient") else "[invalid role]"))
        checks.append(check("local_player", type(lan.get("localPlayer")) is int and lan["localPlayer"] == local_slot, local_slot, safe_scalar(lan.get("localPlayer"))))
        checks.append(check("observer_is_local_player", type(metadata.get("observerPlayer")) is int and metadata["observerPlayer"] == local_slot, local_slot, safe_scalar(metadata.get("observerPlayer"))))
        for key in ("connected", "localMenuPaused", "sharedPaused", "hasSimulation", "hasRecorder"):
            checks.append(check("typed_" + key, type(lan.get(key)) is bool))
        for key in ("revision", "eventCursor", "completedStep", "pending"):
            checks.append(check("typed_" + key, type(lan.get(key)) is int and lan[key] >= 0))
        checks.append(check("finite_world_time", finite_number(metadata.get("worldTime")) and metadata["worldTime"] >= 0))
        checks.append(check("pause_scope", type(metadata.get("paused")) is bool and metadata["paused"] == (lan.get("localMenuPaused") is True or lan.get("sharedPaused") is True)))
        simulation = phase == "match" and expected_role == "LanHost"
        checks.append(check("host_only_simulation", lan.get("hasSimulation") is simulation, simulation))
        checks.append(check("host_only_recorder", lan.get("hasRecorder") is simulation, simulation))
        replay = metadata.get("replay")
        checks.append(check("replay_metadata_object", isinstance(replay, dict)))
        if not isinstance(replay, dict):
            replay = {}
        checks.append(check("not_watching_replay", replay.get("watching") is False))
        checks.append(check("client_not_recording", expected_role != "LanClient" or replay.get("recording") is False))
        checks.append(check("shared_pause_world_flag", replay.get("worldPaused") is lan.get("sharedPaused")))
        build = metadata.get("buildGuid")
        checks.append(check("native_build_guid", isinstance(build, str) and GUID.fullmatch(build)))
        checks.append(check("capture_frame_boundary", type(metadata.get("requestFrame")) is int and type(metadata.get("captureFrame")) is int and 0 <= metadata["requestFrame"] <= metadata["captureFrame"]))
        facts = {key: safe_scalar(lan.get(key)) for key in ("localPlayer", "pending", "revision", "eventCursor", "completedStep", "connected", "localMenuPaused", "sharedPaused", "hasSimulation", "hasRecorder")}
        facts.update({key: safe_scalar(metadata.get(key)) for key in ("buildGuid", "matchId", "observerPlayer", "worldTime", "paused", "seed")})
        facts["role"] = expected_role if role == expected_role else "[unexpected role]"
        facts["projectedDigest"] = safe_scalar(lan.get("projectedDigest"))
        version = metadata.get("appVersion")
        valid_version = isinstance(version, str) and re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", version)
        checks.append(check("app_version", valid_version))
        checks.append(check("typed_seed", type(metadata.get("seed")) is int and -(2**31) <= metadata["seed"] < 2**31))
        facts["appVersion"] = version if valid_version else "[invalid format]"
        row["facts"] = facts
        if phase == "match":
            match = metadata.get("matchId")
            checks.append(check("match_identity", isinstance(match, str) and GUID.fullmatch(match) and metadata.get("requestMatchId") == match))
            checks.append(check("has_observer_player", metadata.get("hasPlayer") is True and metadata.get("inMenu") is False))
            digest = lan.get("projectedDigest")
            checks.append(check("projection_digest", isinstance(digest, str) and HASH.fullmatch(digest)))
            hud_match = metadata.get("hudMatchId") == match
            hud_frame = type(metadata.get("hudUpdatedFrame")) is int and metadata["hudUpdatedFrame"] == metadata.get("captureFrame")
            hud_time = finite_number(metadata.get("hudWorldTime")) and finite_number(metadata.get("worldTime")) and abs(metadata["hudWorldTime"] - metadata["worldTime"]) <= 0.0001
            checks.extend([check("hud_same_match", hud_match), check("hud_same_frame", hud_frame), check("hud_same_world_time", hud_time)])
            row["hudFacts"] = {
                "sameMatch": hud_match, "sameFrame": hud_frame, "sameWorldTime": hud_time,
                "note": "HUD metadata does not certify that the rendered text/geometry was manually inspected."
            }
        row["status"] = "verified" if all(c["ok"] for c in checks) else "failed"
    except (OSError, ValueError, TypeError, KeyError, struct.error) as error:
        # Exception text is not emitted: malformed input could place a secret
        # in a path, JSON error excerpt or free-form field.
        row["error"] = type(error).__name__
    return row


def inspect_captures(data_directory, role, slot):
    directory = bounded_path(data_directory / "Captures")
    result = {"directory": relative(directory), "status": "pending", "captures": [], "orphanPngs": []}
    if not directory.is_dir():
        result["reason"] = "No Captures directory yet"
        return result
    paths = sorted(p for p in directory.glob("Frontier-*.json") if CAPTURE_NAME.fullmatch(p.name))
    if len(paths) > MAX_CAPTURES:
        result.update(status="failed", reason="Capture count exceeds inspection bound")
        return result
    result["captures"] = [capture(path, role, slot) for path in paths]
    result["orphanPngs"] = [relative(p) for p in sorted(directory.glob("Frontier-*.png")) if not p.with_suffix(".json").exists()][:MAX_CAPTURES]
    active = [r for r in result["captures"] if r.get("phase") == "match"]
    if any(r["status"] == "failed" for r in result["captures"]):
        result["status"] = "failed"
    elif active:
        result["status"] = "verified"
    else:
        result["reason"] = "No completed active LAN match capture yet"
    return result


def inspect_log(path):
    result = {"file": relative(path), "status": "pending", "records": [], "malformedRecords": 0}
    if not path.is_file():
        result["reason"] = "No isolated native log yet"
        return result
    try:
        result["sha256"] = sha256(path, MAX_LOG)
        with path.open("r", encoding="utf-8", errors="replace") as source:
            for line_number, line in enumerate(source, 1):
                marker = LOG_MARKER.search(line)
                if marker is None:
                    continue
                fields = dict(LOG_FIELDS.findall(line[marker.end():]))
                required = ("peer", "sequence", "accepted", "step", "ordinal", "revision")
                try:
                    if not all(key in fields for key in required):
                        raise ValueError("Missing structured field")
                    if fields["peer"] != "server" and not GUID.fullmatch(fields["peer"]):
                        raise ValueError("Invalid peer identifier")
                    record = {"kind": marker.group(1), "line": line_number, "peer": fields["peer"]}
                    for key in ("sequence", "hostSequence", "step", "ordinal", "revision"):
                        if key in fields:
                            record[key] = int(fields[key])
                    for key in ("accepted", "protocolRejected"):
                        if key in fields:
                            if fields[key].lower() not in ("true", "false"):
                                raise ValueError("Invalid boolean")
                            record[key] = fields[key].lower() == "true"
                    result["records"].append(record)
                    if len(result["records"]) > MAX_LOG_RECORDS:
                        raise OverflowError("Log record limit")
                except (ValueError, KeyError):
                    result["malformedRecords"] += 1
        result["status"] = "verified" if result["records"] and not result["malformedRecords"] else "failed" if result["malformedRecords"] else "pending"
        result["counts"] = {kind: sum(r["kind"] == kind for r in result["records"]) for kind in ("APPLY", "RESULT")}
    except (OSError, ValueError, OverflowError) as error:
        result.update(status="failed", error=type(error).__name__)
    return result


def check_candidate_build(candidate, host, client):
    boot = candidate / "Build" / "Frontier Reckoning.app" / "Contents" / "Resources" / "Data" / "boot.config"
    result = {"status": "pending", "reason": "No candidate native boot.config build identity yet"}
    if not boot.is_file():
        return result
    try:
        boot = bounded_path(boot)
        if boot.stat().st_size > 65536:
            raise ValueError("Native boot configuration exceeds bound")
        content = boot.read_text(encoding="utf-8")
        identities = re.findall(r"^build-guid=([0-9a-fA-F]{32})\s*$", content, re.MULTILINE)
        if len(identities) != 1:
            raise ValueError("Native boot configuration has no unique build identity")
        expected = identities[0].lower()
        result = {"status": "verified", "buildGuid": expected, "bootConfig": relative(boot), "sha256": sha256(boot, 65536)}
        for group in (host, client):
            for row in group["captures"]:
                if row.get("phase") != "match" or "facts" not in row:
                    continue
                actual = row["facts"]["buildGuid"]
                row["checks"].append(check("candidate_native_build_guid", isinstance(actual, str) and actual.lower() == expected, expected))
                if not all(c["ok"] for c in row["checks"]):
                    row["status"] = "failed"
                    group["status"] = "failed"
    except (OSError, ValueError) as error:
        result = {"status": "failed", "error": type(error).__name__}
    return result


def compare(host, client):
    hosts = [r for r in host["captures"] if r["status"] == "verified" and r.get("phase") == "match"]
    clients = [r for r in client["captures"] if r["status"] == "verified" and r.get("phase") == "match"]
    identity = {"status": "pending", "pairs": []}
    shared = {"status": "pending", "pairs": [], "stoppedIntervals": []}
    for h in hosts:
        for c in clients:
            hf, cf = h["facts"], c["facts"]
            same = all(hf[key] == cf[key] for key in ("buildGuid", "matchId", "appVersion", "seed"))
            if not same:
                continue
            identity["pairs"].append({"host": h["metadata"], "client": c["metadata"], "sameBuildAndMatch": True})
            if not (hf["sharedPaused"] and cf["sharedPaused"] and hf["connected"] and cf["connected"] and hf["pending"] == cf["pending"] == 0):
                continue
            checks = [check(key, hf[key] == cf[key]) for key in ("completedStep", "eventCursor", "projectedDigest")]
            checks.append(check("worldTime", abs(hf["worldTime"] - cf["worldTime"]) <= 0.0001))
            shared["pairs"].append({"host": h["metadata"], "client": c["metadata"], "checks": checks,
                                    "status": "verified" if all(r["ok"] for r in checks) else "different_snapshot",
                                    "hostLocalMenuPaused": hf["localMenuPaused"], "clientLocalMenuPaused": cf["localMenuPaused"]})
    if hosts and clients:
        identity["status"] = "verified" if identity["pairs"] else "failed"
    for role, rows in (("host", hosts), ("client", clients)):
        paused = [r for r in rows if r["facts"]["sharedPaused"] and r["facts"]["pending"] == 0]
        for before, after in zip(paused, paused[1:]):
            a, b = before["facts"], after["facts"]
            if all(a[k] == b[k] for k in ("buildGuid", "matchId", "completedStep", "eventCursor", "projectedDigest")) and abs(a["worldTime"] - b["worldTime"]) <= 0.0001:
                shared["stoppedIntervals"].append({"role": role, "before": before["metadata"], "after": after["metadata"], "snapshotUnchanged": True})
    if shared["pairs"]:
        shared["status"] = "verified" if any(r["status"] == "verified" for r in shared["pairs"]) else "pending"
    shared["notes"] = ["Sequential running captures are not a synchronized comparison; equal last-published revision alone is insufficient.",
                        "Revision may increase while shared pause holds; local menu flags may differ without changing World.",
                        "Different paused snapshots can belong to separate pauses or intervening report flushes. A matching immutable snapshot is required."]
    return identity, shared


def correlate(host_log, client_log):
    applies = {}
    for record in host_log["records"]:
        if record["kind"] == "APPLY" and record["sequence"] > 0:
            applies.setdefault((record["peer"], record["sequence"]), []).append(record)
    rows = []
    for result in client_log["records"]:
        if result["kind"] != "RESULT" or result["sequence"] <= 0:
            continue
        matches = applies.get((result["peer"], result["sequence"]), [])
        row = {"peer": result["peer"], "sequence": result["sequence"], "resultLine": result["line"], "applyLines": [r["line"] for r in matches]}
        if len(matches) == 1:
            row["checks"] = [check(key, result[key] == matches[0][key]) for key in ("accepted", "step", "ordinal", "revision")]
            row["status"] = "verified" if all(c["ok"] for c in row["checks"]) else "failed"
        else:
            row["status"] = "unmatched_result" if not matches else "duplicate_apply"
        rows.append(row)
    return {"status": "pending" if not rows else "failed" if any(r["status"] in ("failed", "duplicate_apply") for r in rows) else "verified" if any(r["status"] == "verified" for r in rows) else "pending",
            "records": rows, "note": "Pre-execution protocol rejection can legitimately have RESULT without APPLY. These logs do not identify command kind or prove payment; accepted training needs replay/payment evidence."}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("host-data-dir", "client-data-dir", "host-log", "client-log", "out"):
        parser.add_argument("--" + name, required=True)
    args = parser.parse_args()
    try:
        paths = {name: bounded_path(value) for name, value in vars(args).items()}
        candidate = candidate_for(paths["host_data_dir"])
        if any(candidate_for(path) != candidate for path in paths.values()):
            raise ValueError("Host/client logs, data and output must belong to one candidate")
        try:
            paths["out"].relative_to(candidate / "Native")
        except ValueError:
            raise ValueError("Report output must be under the selected candidate Native directory")
        if paths["host_data_dir"] == paths["client_data_dir"] or paths["host_log"] == paths["client_log"]:
            raise ValueError("Host/client data directories and logs must be isolated")
        if paths["out"] in (paths["host_log"], paths["client_log"]) or any(paths["out"].parent == root / "Captures" for root in (paths["host_data_dir"], paths["client_data_dir"])):
            raise ValueError("Output must not replace an input or become capture evidence")
    except ValueError as error:
        parser.error(str(error))
    host = inspect_captures(paths["host_data_dir"], "LanHost", 0)
    client = inspect_captures(paths["client_data_dir"], "LanClient", 2)
    candidate_build = check_candidate_build(candidate, host, client)
    host_log, client_log = inspect_log(paths["host_log"]), inspect_log(paths["client_log"])
    identity, shared = compare(host, client)
    states = (host["status"], client["status"], identity["status"], candidate_build["status"])
    metadata_status = "failed" if "failed" in states else "verified" if all(state == "verified" for state in states) else "pending"
    report = {"schema": 1, "generatedUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
              "scope": candidate.name + " metadata for two local native processes; this is not two-physical-Mac acceptance",
              "inputs": {key: relative(path) for key, path in paths.items() if key != "out"},
              "metadataFacts": {"status": metadata_status, "host": host, "client": client, "identity": identity, "sharedPause": shared},
              "structuredLogs": {"host": host_log, "client": client_log, "correlation": correlate(host_log, client_log)},
              "actualUiObservations": {"status": "pending", "observations": [], "reason": "This script did not observe UI, clicks, visual correctness, native input or responsiveness. Root must record actual Computer Use observations separately."},
              "physicalMacAcceptance": {"status": "pending"},
              "candidateNativeBuild": candidate_build,
              "nativeAcceptance": {"status": "pending", "reason": "Metadata alone cannot certify the required native UI phases or two physical Macs."}}
    freeze = candidate / "Build" / "runtime-freeze.json"
    if freeze.is_file():
        source = read_json(freeze)
        formats = {"runtimeCommit": r"[0-9a-f]{40}", "appVersion": r"[0-9]+\.[0-9]+\.[0-9]+",
                   "sourceFingerprint": r"[0-9a-f]{64}", "testRun": r"run-[A-Za-z0-9]{1,32}"}
        report["candidateProvenance"] = {}
        for key, pattern in formats.items():
            value = source.get(key) if isinstance(source, dict) else None
            report["candidateProvenance"][key] = value if isinstance(value, str) and re.fullmatch(pattern, value) else "[invalid type or format]"
        count = source.get("inputs") if isinstance(source, dict) else None
        report["candidateProvenance"]["inputs"] = count if type(count) is int and 0 <= count <= 1000000 else "[invalid type or format]"
        report["candidateProvenance"]["note"] = "Manifest provenance; this script does not prove the native binary was built from that fingerprint."
    paths["out"].parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", dir=paths["out"].parent, prefix=paths["out"].name + ".", delete=False) as temporary:
        json.dump(report, temporary, indent=2, ensure_ascii=False, allow_nan=False)
        temporary.write("\n")
        temporary_path = temporary.name
    os.replace(temporary_path, paths["out"])
    print(json.dumps({"report": relative(paths["out"]), "metadataStatus": metadata_status,
                      "nativeAcceptance": "pending", "hostCaptures": len(host["captures"]), "clientCaptures": len(client["captures"])}, ensure_ascii=False))


if __name__ == "__main__":
    main()
