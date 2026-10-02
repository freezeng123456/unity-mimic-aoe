#!/usr/bin/env python3
"""Archive and independently audit a native F8 performance capture.

Recompute interval eligibility from current/previous raw state and transition
flags. Rebuild entity counts from associated match events, admitting only event
prefixes at an identical simulation time (several render frames share a tick).
Optional minimum duration/sample requirements are explicit, never assumed.
"""
import argparse
import bisect
import collections
import datetime
import gzip
import hashlib
import json
import math
import pathlib
import re
import shutil
import struct
import zlib


# Delta time is measured at Unity's frame boundary; realtime is sampled in
# LateUpdate. Admit a fixed endpoint phase difference, never a tolerance that
# grows with a reported (and potentially forged) frame duration.
WALL_ENDPOINT_TOLERANCE_SECONDS = .25


def finite_number(value):
    return type(value) in (int, float) and math.isfinite(value)


def integer(value):
    return type(value) is int and value >= 0


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate JSON field: " + key)
        result[key] = value
    return result


def finite_float(text):
    value = float(text)
    if not math.isfinite(value):
        raise ValueError("Nonfinite JSON number: " + text)
    return value


def invalid_constant(text):
    raise ValueError("Nonstandard JSON constant: " + text)


def strict_json(text):
    return json.loads(text, object_pairs_hook=unique_object,
                      parse_float=finite_float, parse_constant=invalid_constant)


def source(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return {"path": str(path.resolve()), "sha256": digest.hexdigest()}


def archive(path, directory):
    identity = source(path)
    target = directory / path.name
    if target.exists():
        if source(target)["sha256"] != identity["sha256"]:
            raise ValueError("Archive already contains different bytes: " + str(target))
    else:
        shutil.copyfile(path, target)
    identity["archivedPath"] = str(target.resolve())
    identity["archiveHashMatches"] = source(target)["sha256"] == identity["sha256"]
    return identity


def f32(value):
    return struct.unpack("<f", struct.pack("<f", value))[0]


def near(a, b):
    return math.isfinite(a) and math.isfinite(b) and math.isclose(a, b, rel_tol=1e-6, abs_tol=.0001)


def capture_modes(report, match, allow_diagnostic):
    one_x = finite_number(report["simulationSpeed"]) and report["simulationSpeed"] == 1 and finite_number(match["simulationSpeed"]) and match["simulationSpeed"] == 1
    no_autoplay = report["autoplay"] is False and match["autoplay"] is False
    return (no_autoplay and one_x and report["benchmark"] is False,
            bool(allow_diagnostic and report["benchmark"] is True and no_autoplay and one_x))


def frame_audit(report):
    raw = report["frames"]
    previous_focus, previous_pause = report["focusedAtStart"], report["pausedAtStart"]
    last_frame, last_realtime, last_world = -1, report["realtimeStart"], report["worldTimeStart"]
    included, issues = [], []
    raw_intervals = []
    excluded = collections.Counter()
    all_focus = bool(raw) and report["focusedAtStart"]
    if not all(type(report[key]) is bool for key in ("focusedAtStart", "pausedAtStart", "allFramesFocused")):
        issues.append({"issue": "Initial/focus summary fields must be explicit booleans"})
    capture_bounds_valid = all(finite_number(report[key]) for key in ("realtimeStart", "realtimeEnd", "worldTimeStart", "worldTimeEnd")) and report["realtimeEnd"] >= report["realtimeStart"] and report["worldTimeEnd"] >= report["worldTimeStart"]
    for index, frame in enumerate(raw):
        required_bools = ("focused", "paused", "eligibleInterval", "focusLostInInterval", "pauseChangedInInterval")
        if not all(isinstance(frame[key], bool) for key in required_bools):
            issues.append({"frameIndex": index, "issue": "State/transition fields must be explicit booleans"})
        eligible = frame["focused"] and previous_focus and not frame["paused"] and not previous_pause and not frame["focusLostInInterval"] and not frame["pauseChangedInInterval"]
        if frame["eligibleInterval"] != eligible:
            issues.append({"frameIndex": index, "frame": frame["frame"], "issue": "Recorded eligibility differs from raw states/transitions", "computed": eligible})
        physical_interval = finite_number(frame["frameMs"]) and frame["frameMs"] >= 0
        if not physical_interval:
            issues.append({"frameIndex": index, "issue": "Frame duration must be a finite nonnegative number"})
        else:
            raw_intervals.append(f32(frame["frameMs"]) / 1000)
        valid = physical_interval and frame["frameMs"] > 0
        if eligible and valid:
            included.append(f32(frame["frameMs"]))
        else:
            excluded["invalidInterval" if not valid else "ineligibleStateOrTransition"] += 1
        if not frame["focused"] or not previous_focus:
            excluded["focusCurrentOrPrevious"] += 1
        if frame["paused"] or previous_pause:
            excluded["pauseCurrentOrPrevious"] += 1
        if frame["focusLostInInterval"]:
            excluded["focusLostCallback"] += 1
        if frame["pauseChangedInInterval"]:
            excluded["pauseChangedEvent"] += 1
        if not integer(frame["frame"]):
            issues.append({"frameIndex": index, "issue": "Render frame index must be a nonnegative integer"})
        if last_frame >= 0 and frame["frame"] != last_frame + 1:
            issues.append({"frameIndex": index, "issue": "Render frame sequence is not consecutive"})
        if not finite_number(frame["realtime"]) or frame["realtime"] < last_realtime or frame["realtime"] > report["realtimeEnd"] + .001:
            issues.append({"frameIndex": index, "issue": "Realtime is outside monotonically increasing capture bounds"})
        if not finite_number(frame["worldTime"]) or frame["worldTime"] < last_world - .0001 or frame["worldTime"] > report["worldTimeEnd"] + .0001:
            issues.append({"frameIndex": index, "issue": "World time is outside same-session monotonic capture bounds"})
        if not finite_number(frame["simulationMs"]) or frame["simulationMs"] < 0 or frame["paused"] and frame["simulationMs"] != 0:
            issues.append({"frameIndex": index, "issue": "Invalid simulation duration or nonzero paused duration"})
        all_focus = all_focus and frame["focused"] and not frame["focusLostInInterval"]
        previous_focus, previous_pause = frame["focused"], frame["paused"]
        last_frame, last_realtime, last_world = frame["frame"], frame["realtime"], frame["worldTime"]
    included.sort()
    mean = f32(sum(included) / len(included)) if included else 0
    p95 = included[math.floor((len(included) - 1) * .95)] if included else 0
    p99 = included[math.floor((len(included) - 1) * .99)] if included else 0
    wall_seconds = report["realtimeEnd"] - report["realtimeStart"]
    raw_seconds = sum(raw_intervals)
    eligible_seconds = sum(included) / 1000
    # The last sampled frame can precede Finish (F8, disable or a limit). The
    # lower bound therefore ends at that sample, not at the capture stop time.
    sampled_wall = last_realtime - report["realtimeStart"] if raw else 0
    tolerance = WALL_ENDPOINT_TOLERANCE_SECONDS
    checks = {"captureBoundsFiniteAndOrdered": capture_bounds_valid,
        "allFrameDurationsFiniteAndNonnegative": len(raw_intervals) == len(raw),
        "rawDurationFitsWallEnvelope": capture_bounds_valid and len(raw_intervals) == len(raw) and max(0, sampled_wall - tolerance) <= raw_seconds <= wall_seconds + tolerance,
        "eligibleDurationFitsWallEnvelope": capture_bounds_valid and eligible_seconds <= wall_seconds + tolerance,
        "frameCountMatches": integer(report["totalFrames"]) and report["totalFrames"] == len(raw),
        "eligibleFlagsMatchRecomputation": not any("eligibility" in issue["issue"] for issue in issues),
        "rawFrameStateAndOrderValid": not issues,
        "includedFramesMatch": integer(report["includedFrames"]) and report["includedFrames"] == len(included),
        "excludedFramesMatch": integer(report["excludedFrames"]) and report["excludedFrames"] == len(raw) - len(included),
        "allFramesFocusedMatches": report["allFramesFocused"] == all_focus,
        "meanMatches": near(report["meanFrameMs"], mean), "floorP95Matches": near(report["p95FrameMs"], p95),
        "floorP99Matches": near(report["p99FrameMs"], p99)}
    return {"checks": checks, "issues": issues, "computed": {"totalFrames": len(raw), "includedFrames": len(included),
        "excludedFrames": len(raw) - len(included), "eligibleUnpausedSeconds": eligible_seconds,
        "rawFrameIntervalSeconds": raw_seconds, "sampledWallSeconds": sampled_wall,
        "wallEndpointToleranceSeconds": tolerance,
        "worldAdvancedSeconds": report["worldTimeEnd"] - report["worldTimeStart"],
        "wallSeconds": wall_seconds, "meanFrameMs": mean,
        "p95FrameMs": p95, "p99FrameMs": p99, "allFramesFocused": all_focus},
        "excludedReasonCounts": dict(excluded), "reasonCountScope": "Reasons overlap; their counts are not a sum of excludedFrames",
        "wallEnvelopeScope": "Summed intervals are bounded by capture realtime with a fixed 0.25-second endpoint phase allowance. The lower bound uses the last sampled realtime, admitting an unsampled stop interval. No individual delta is required to equal a LateUpdate realtime difference; the allowance cannot grow with a frame duration."}


COUNT_KEYS = ("livingUnits", "playerUnits0", "playerUnits1", "neutralUnits", "livingBuildings", "totalEntities")


def entity_timeline(event_path, rules):
    definitions = {entity["name"]: entity for entity in rules["entities"]}
    state = {key: 0 for key in COUNT_KEYS}
    entities, alive, changes, issues = {}, set(), [], []
    count, last_time = 0, -1
    with (gzip.open(event_path, "rt", encoding="utf-8") if event_path.suffix == ".gz" else event_path.open()) as stream:
        for line, text in enumerate(stream, 1):
            if not text.strip():
                continue
            event = strict_json(text); count += 1
            if event["time"] < last_time - .0001:
                issues.append({"line": line, "issue": "Match event time moves backwards"})
            last_time = max(last_time, event["time"])
            if event["kind"] == "Spawn":
                definition = definitions.get(event["message"])
                if definition is None or event["entity"] in entities:
                    issues.append({"line": line, "issue": "Unknown Spawn definition or reused identity"})
                    continue
                entities[event["entity"]] = (definition["kind"], event["player"])
                alive.add(event["entity"]); state["totalEntities"] += 1
                increment = 1
            elif event["kind"] == "Death":
                if event["entity"] not in alive:
                    issues.append({"line": line, "issue": "Death without living Spawn identity"})
                    continue
                alive.remove(event["entity"]); increment = -1
            else:
                continue
            kind, owner = entities[event["entity"]]
            if kind == 0:
                state["livingUnits"] += increment
                state["playerUnits0" if owner == 0 else "playerUnits1" if owner == 1 else "neutralUnits"] += increment
            elif kind == 1:
                state["livingBuildings"] += increment
            changes.append((event["time"], line, dict(state)))
    return changes, issues, count, last_time


def memory_audit(report, changes):
    times = [item[0] for item in changes]
    rows, issues = [], []
    last_real, last_world = report["realtimeStart"], report["worldTimeStart"]
    for index, memory in enumerate(report["memory"]):
        actual = {key: memory[key] for key in COUNT_KEYS}
        before = bisect.bisect_left(times, memory["worldTime"] - .0001)
        after = bisect.bisect_right(times, memory["worldTime"] + .0001)
        candidates = changes[max(0, before - 1):after]
        if before == 0:
            candidates = [(-1, 0, {key: 0 for key in COUNT_KEYS})] + candidates
        matches = [row for row in candidates if row[2] == actual]
        checks = {"nonnegativeCountsAndMemory": all(integer(value) for value in actual.values()) and integer(memory["managedBytes"]) and integer(memory["unityAllocatedBytes"]),
            "unitOwnerPartitionMatches": memory["livingUnits"] == memory["playerUnits0"] + memory["playerUnits1"] + memory["neutralUnits"],
            "livingCountFitsHistoricalEntityCount": memory["livingUnits"] + memory["livingBuildings"] <= memory["totalEntities"],
            "viewportMetricPresentAndInAliveRange": integer(memory.get("viewportVisibleUnitCenters")) and memory["viewportVisibleUnitCenters"] <= memory["livingUnits"],
            "countsMatchActualSpawnDeathPrefix": bool(matches),
            "realtimeWithinCaptureAndMonotonic": last_real <= memory["realtime"] <= report["realtimeEnd"] + .001,
            "worldTimeWithinCaptureAndMonotonic": last_world - .0001 <= memory["worldTime"] <= report["worldTimeEnd"] + .0001}
        row = {"memoryIndex": index, "sample": memory, "checks": checks,
            "matchingEventPrefixLines": [item[1] for item in matches],
            "sameTickAmbiguousPrefixes": after - before > 0 and len(candidates) > 1}
        if not matches:
            row["candidateCounts"] = [{"throughEventLine": item[1], "counts": item[2]} for item in candidates]
        rows.append(row)
        if not all(checks.values()):
            issues.append({"memoryIndex": index, "failedChecks": [key for key, value in checks.items() if not value]})
        last_real, last_world = memory["realtime"], memory["worldTime"]
    memories = report["memory"]
    checks = {"hasStartAndEndMemorySamples": len(memories) >= 2,
        "memoryCountsAndBoundsValid": not issues,
        "firstMemoryMatchesCaptureStartWorld": bool(memories) and near(memories[0]["worldTime"], report["worldTimeStart"]),
        "lastMemoryMatchesCaptureEndWorld": bool(memories) and near(memories[-1]["worldTime"], report["worldTimeEnd"])}
    return {"checks": checks, "issues": issues, "rows": rows,
        "countScope": "Living units include neutral guardians; totalEntities includes historical dead entities. Exact event prefix is allowed within a shared simulation tick; it does not provide render-frame timestamps for events.",
        "viewportMetricScope": "Only presence and 0..livingUnits are audited for viewportVisibleUnitCenters. Camera/fog/viewport-center inclusion cannot be reconstructed independently from event time alone, and does not prove unobstructed or HUD-free visibility of all units."}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("report", type=pathlib.Path)
    parser.add_argument("--match-report", type=pathlib.Path, required=True)
    parser.add_argument("--events", type=pathlib.Path)
    parser.add_argument("--build-guid", required=True)
    parser.add_argument("--match-id")
    parser.add_argument("--app-version")
    parser.add_argument("--allow-diagnostic", action="store_true", help="Permit explicitly labeled benchmark captures at 1x; autoplay remains rejected")
    parser.add_argument("--rules", type=pathlib.Path, default=pathlib.Path(__file__).resolve().parents[3] / "Assets/Resources/Rules.json")
    parser.add_argument("--archive-dir", type=pathlib.Path, required=True)
    parser.add_argument("--output", type=pathlib.Path)
    parser.add_argument("--min-unpaused-seconds", type=float, help="Minimum summed eligible unpaused frame intervals; transition intervals excluded")
    parser.add_argument("--min-eligible-frames", type=int)
    args = parser.parse_args()
    guid = args.build_guid.lower()
    if not re.fullmatch(r"[0-9a-f]{32}", guid) or int(guid, 16) == 0:
        parser.error("--build-guid must be an explicit nonzero 32-digit hexadecimal GUID")
    if args.min_unpaused_seconds is not None and (not math.isfinite(args.min_unpaused_seconds) or args.min_unpaused_seconds < 0) or args.min_eligible_frames is not None and args.min_eligible_frames < 0:
        parser.error("Minimum duration and sample count must be finite and nonnegative")
    events_path = args.events or args.match_report.with_name(args.match_report.stem + "-events.jsonl")
    compressed_events = events_path.with_suffix(".jsonl.gz")
    if args.events is None and not events_path.is_file() and compressed_events.is_file():
        events_path = compressed_events
    try:
        report = strict_json(args.report.read_text()); match = strict_json(args.match_report.read_text()); rules = strict_json(args.rules.read_text())
        frames = frame_audit(report)
        changes, event_issues, event_count, final_event_time = entity_timeline(events_path, rules)
        memories = memory_audit(report, changes)
        computed = frames["computed"]
        normal_player, diagnostic = capture_modes(report, match, args.allow_diagnostic)
        checks = {"captureNativePlatform": report["platform"] == "OSXPlayer", "matchNativePlatform": match["platform"] == "OSXPlayer",
            "explicitBuildGuidMatchesBoth": str(report["buildGuid"]).lower() == guid and str(match["buildGuid"]).lower() == guid,
            "matchIdentityMatches": bool(report["matchId"]) and report["matchId"] == match["matchId"] and (args.match_id is None or report["matchId"] == args.match_id),
            "versionMatchesAssociatedMatch": report["appVersion"] == match["appVersion"] and (args.app_version is None or report["appVersion"] == args.app_version),
            "unityVersionMatchesAssociatedMatch": report["unityVersion"] == match["version"],
            "normalPlayerCapture": normal_player,
            "captureModeAllowed": normal_player or diagnostic,
            "singleSession": report["singleSession"] is True and report["currentMatchId"] == report["matchId"],
            "notTruncated": report["truncated"] is False, "schemaRecognized": type(report["schemaVersion"]) is int and report["schemaVersion"] == 1,
            "gcCountsNonnegative": all(integer(report[key]) for key in ("gc0", "gc1", "gc2")),
            "hasEligibleSamples": computed["includedFrames"] > 0,
            "positiveWorldAdvance": computed["worldAdvancedSeconds"] > 0,
            "captureTimesValid": computed["wallSeconds"] >= 0 and report["worldTimeStart"] >= 0 and report["worldTimeEnd"] <= match["time"] + .15,
            "matchEventCountMatches": event_count == match["eventCount"], "matchEventIdentitiesValid": not event_issues,
            "associatedEventFilenameMatches": events_path.name in (args.match_report.stem + "-events.jsonl", args.match_report.stem + "-events.jsonl.gz"),
            "captureWithinEventCoverage": final_event_time >= report["worldTimeEnd"] - .0001,
            **frames["checks"], **memories["checks"]}
        requirements = {}
        if args.min_unpaused_seconds is not None:
            requirements["minimumEligibleUnpausedSeconds"] = {"required": args.min_unpaused_seconds, "actual": computed["eligibleUnpausedSeconds"], "passed": computed["eligibleUnpausedSeconds"] >= args.min_unpaused_seconds}
        if args.min_eligible_frames is not None:
            requirements["minimumEligibleFrames"] = {"required": args.min_eligible_frames, "actual": computed["includedFrames"], "passed": computed["includedFrames"] >= args.min_eligible_frames}
        args.archive_dir.mkdir(parents=True, exist_ok=True)
        identities = {label: archive(path, args.archive_dir) for label, path in
            (("capture", args.report), ("matchReport", args.match_report), ("events", events_path), ("rules", args.rules), ("auditor", pathlib.Path(__file__)))}
        # Keep actual normalPlayerCapture=false for a fixture. Only this explicit
        # mode permits that one false field; every other check remains required.
        informational = {"normalPlayerCapture"} if diagnostic else set()
        passed = all(value for key, value in checks.items() if key not in informational) and all(item["passed"] for item in requirements.values()) and all(item["archiveHashMatches"] for item in identities.values())
        summary = {"generatedAtUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
            "scope": "Native 1x benchmark F8 diagnostic with capture overhead; synthetic elevated-HP pressure fixture, not normal-player or whole-match evidence" if diagnostic else "Native normal-player F8 interval audit with capture overhead; not whole-match completion, unrestricted long-run stability, other hardware or a statistical balance claim",
            "diagnostic": bool(diagnostic), "normalPlayerCapture": bool(normal_player),
            "informationalChecksExcludedFromDiagnosticAcceptance": sorted(informational),
            "transitionScope": "Eligibility independently recomputed from raw previous/current state and recorded transition flags. Focus callbacks are not independently proven by match events; no render timestamp is inferred from an event simulation time.",
            "requiredBuildGuid": guid, "matchId": report["matchId"], "appVersion": report["appVersion"],
            "checks": checks, "requirements": requirements, "status": "passed" if passed else "failed",
            "minimumRequirementScope": "No duration/sample acceptance threshold imposed unless explicitly supplied",
            "computed": computed, "frameAudit": frames, "memoryAudit": memories, "eventIssues": event_issues,
            "sources": identities, "captureIdentity": {key: report[key] for key in
                ("buildGuid", "unityVersion", "platform", "cpu", "gpu", "resolution", "utcStart", "utcEnd", "stopReason", "autoplay", "benchmark", "singleSession", "truncated", "gc0", "gc1", "gc2")}}
        output = args.output or args.archive_dir / (args.report.stem + "-audit.json")
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n")
        print(json.dumps({"output": str(output.resolve()), "status": summary["status"], "computed": computed,
            "failedChecks": [key for key, value in checks.items() if not value], "requirements": requirements}, ensure_ascii=False))
        return 0 if passed else 1
    except (OSError, EOFError, zlib.error, ValueError, KeyError, TypeError, OverflowError, struct.error) as error:
        parser.exit(2, "Cannot audit performance evidence: " + str(error) + "\n")


if __name__ == "__main__":
    raise SystemExit(main())
