#!/usr/bin/env python3
"""Read-only 0.8.0 NativeCapture schema-3 JSON/PNG identity verification.

Only canonical Frontier-<timestamp>-f<frame>-<uuid>.json files are captures.
Other JSON files (receipts, previews, reports) are ignored. An optional --output
creates a NEW report; otherwise the report is printed to stdout. No source,
capture, image, save or input receipt is modified. Exit 0 means identity checks
passed for at least one pair; it never means native gameplay passed.
"""

import argparse
from datetime import datetime, timezone
import hashlib
import json
import math
import os
from pathlib import Path
import re
import struct
import sys
import zlib


VERSION = "0.8.0"
CAPTURE = re.compile(r"Frontier-\d{8}-\d{6}-\d{3}-f(\d+)-[0-9a-f]{32}\.json\Z")
HEX32 = re.compile(r"[0-9a-fA-F]{32}\Z")
HEX64 = re.compile(r"[0-9a-fA-F]{64}\Z")
HUD = re.compile(
    r"FOOD\s+(-?\d+)\s+WOOD\s+(-?\d+)\s+COIN\s+(-?\d+)\s+"
    r"POP\s+(\d+)\+(\d+)/(\d+)\s+AGE\s+(\d+)\s+"
    r"XP\s+(-?\d+)/(-?\d+)\s+SHIP\s+(\d+)\Z"
)
SCOPE = (
    "JSON/PNG file identity and metadata consistency only. PNG SHA-256 binds the "
    "exact same-stem image bytes; frame, camera and HUD values are recorded "
    "metadata, not independently observed pixels. No OCR or visual review, "
    "native-input/gameplay acceptance, continuous focus, save restoration, "
    "performance, test execution or build provenance verification is performed."
)


def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def finite(value):
    return type(value) in (int, float) and math.isfinite(value)


def integer(value, minimum=0):
    return type(value) is int and value >= minimum


def strict_json(data):
    def pairs(items):
        result = {}
        for key, value in items:
            require(key not in result, "Duplicate JSON key: " + key)
            result[key] = value
        return result

    def invalid_constant(value):
        raise ValueError("Non-finite JSON constant: " + value)

    return json.loads(data, object_pairs_hook=pairs, parse_constant=invalid_constant)


def read_regular(path):
    require(path.is_file() and not path.is_symlink(), "Expected a regular, non-symlink file: " + str(path))
    return path.read_bytes()


def png_dimensions(data):
    """Validate the PNG container, all chunk CRCs, IHDR, IDAT and final IEND.

    This deliberately does not decode pixels or assess the rendered content.
    """
    require(data[:8] == b"\x89PNG\r\n\x1a\n", "Invalid PNG signature")
    offset, dimensions, has_idat = 8, None, False
    while offset < len(data):
        require(offset + 12 <= len(data), "Truncated PNG chunk")
        length = struct.unpack_from(">I", data, offset)[0]
        kind = data[offset + 4:offset + 8]
        end = offset + 12 + length
        require(end <= len(data), "Truncated PNG chunk payload")
        payload = data[offset + 8:end - 4]
        expected_crc = struct.unpack_from(">I", data, end - 4)[0]
        require(zlib.crc32(kind + payload) & 0xFFFFFFFF == expected_crc, "PNG chunk CRC mismatch")
        if offset == 8:
            require(kind == b"IHDR" and length == 13, "PNG must begin with a 13-byte IHDR")
            dimensions = struct.unpack_from(">II", payload)
            require(all(x > 0 for x in dimensions), "PNG has zero dimensions")
        else:
            require(kind != b"IHDR", "Duplicate PNG IHDR")
        if kind == b"IDAT":
            has_idat = True
        if kind == b"IEND":
            require(length == 0 and end == len(data) and has_idat, "Invalid PNG end or missing image data")
            return dimensions
        offset = end
    raise ValueError("PNG has no complete IEND")


def vector(value, label):
    require(isinstance(value, dict) and all(finite(value.get(k)) for k in ("x", "y")), label + " must have finite x/y")


def verify_pair(path, root, build):
    result = {"json": path.relative_to(root).as_posix(), "status": "failed", "errors": []}
    try:
        raw = read_regular(path)
        result.update(jsonBytes=len(raw), jsonSha256=sha256(raw))
        data = strict_json(raw)
        require(isinstance(data, dict), "Capture JSON must be an object")
        require(data.get("schemaVersion") == 3, "Expected NativeCapture schemaVersion 3")
        require(integer(data.get("eventSchemaVersion"), 1), "Invalid eventSchemaVersion")
        for key in ("appVersion", "buildGuid", "unityVersion"):
            require(data.get(key) == build[key], key + " differs from build receipt")
        expected_png = path.with_suffix(".png")
        require(data.get("pngFile") == expected_png.name, "pngFile must be the actual same-stem PNG basename")
        require(expected_png.parent == path.parent, "PNG must share its JSON directory")
        png = read_regular(expected_png)
        dimensions = png_dimensions(png)
        declared_hash = data.get("pngSha256")
        require(isinstance(declared_hash, str) and HEX64.fullmatch(declared_hash), "Invalid PNG SHA-256 field")
        require(sha256(png) == declared_hash.lower(), "PNG SHA-256 differs from metadata")
        require(integer(data.get("width"), 1) and integer(data.get("height"), 1), "Invalid metadata dimensions")
        require(dimensions == (data["width"], data["height"]), "PNG IHDR dimensions differ from metadata")
        result.update(png=expected_png.relative_to(root).as_posix(), pngBytes=len(png),
                      pngSha256=sha256(png), width=dimensions[0], height=dimensions[1])
        for key in ("requestFrame", "captureFrame"):
            require(integer(data.get(key)), "Invalid " + key)
        require(int(CAPTURE.fullmatch(path.name).group(1)) == data["captureFrame"], "Filename frame differs from captureFrame")
        require(data["requestFrame"] <= data["captureFrame"], "Capture precedes its request frame")
        require(isinstance(data.get("matchId"), str) and data.get("requestMatchId") == data["matchId"], "Request/capture match identity differs")
        for key in ("worldTime", "requestWorldTime"):
            require(finite(data.get(key)) and data[key] >= 0, "Invalid " + key)
        require(data["requestWorldTime"] <= data["worldTime"] + 0.0001, "Capture world time precedes its request")
        require(isinstance(data.get("utc"), str), "Missing UTC timestamp")
        # Unity DateTime.ToString("O") emits seven fractional digits; Python
        # 3.9 accepts at most six. Preserve the source field in the report.
        timestamp = re.sub(r"(\.\d{6})\d(?=Z|[+-]\d\d:\d\d$)", r"\1", data["utc"])
        stamp = datetime.fromisoformat(timestamp.replace("Z", "+00:00"))
        require(stamp.utcoffset() is not None and stamp.utcoffset().total_seconds() == 0, "Capture timestamp must be UTC")
        for key in ("inMenu", "paused", "hasPlayer"):
            require(type(data.get(key)) is bool, "Invalid " + key)
        require(type(data.get("seed")) is int, "Invalid seed")
        camera = data.get("camera")
        require(isinstance(camera, dict), "Missing camera metadata")
        vector(camera.get("focus"), "camera.focus")
        vector(camera.get("pointer"), "camera.pointer")
        require(finite(camera.get("orthographicSize")) and camera["orthographicSize"] > 0, "Invalid camera orthographicSize")
        require(type(camera.get("applicationFocused")) is bool, "Invalid camera focus flag")
        result.update(utc=data["utc"], appVersion=data["appVersion"], buildGuid=data["buildGuid"],
                      matchId=data["matchId"], inMenu=data["inMenu"], paused=data["paused"],
                      seed=data["seed"], mapId=data.get("mapId"), worldTime=data["worldTime"],
                      requestWorldTime=data["requestWorldTime"], requestFrame=data["requestFrame"],
                      captureFrame=data["captureFrame"], requestCaptureSameFrame=data["requestFrame"] == data["captureFrame"],
                      camera=camera, hudResources=data.get("hudResources"), hudUpdatedFrame=data.get("hudUpdatedFrame"),
                      hudWorldTime=data.get("hudWorldTime"), hudMatchId=data.get("hudMatchId"))
        if data["inMenu"]:
            require(not data["hasPlayer"], "Menu capture incorrectly declares an active player")
            result["hudConsistency"] = "not_applicable_menu_resource_HUD_can_be_stale"
        else:
            require(data["hasPlayer"] and bool(data["matchId"]), "Active capture needs a player and matchId")
            require(data.get("hudMatchId") == data["matchId"], "HUD belongs to another match")
            require(type(data.get("hudUpdatedFrame")) is int and data["hudUpdatedFrame"] == data["captureFrame"], "HUD was not updated in the captured frame")
            require(finite(data.get("hudWorldTime")) and abs(data["hudWorldTime"] - data["worldTime"]) <= 0.0001, "HUD world time differs from captured world")
            text = data.get("hudResources")
            match = HUD.fullmatch(text) if isinstance(text, str) else None
            require(match is not None, "HUD resource metadata has an unexpected format")
            values = list(map(int, match.groups()))
            for index, key in enumerate(("food", "wood", "coin")):
                amount = data.get("resources", {}).get(key)
                require(finite(amount) and abs(amount - values[index]) <= 0.5001, "HUD rounded " + key + " differs from resource metadata")
            for index, key in enumerate(("used", "reserved", "capacity"), 3):
                count = data.get("population", {}).get(key)
                require(integer(count) and count == values[index], "HUD population " + key + " differs from metadata")
            require(1 <= values[6] <= 5, "Invalid age in HUD text")
            for key in ("production", "units", "neutralServices"):
                require(isinstance(data.get(key), list), "Missing schema-3 " + key + " array")
            require(isinstance(data.get("objective"), dict), "Missing schema-3 objective metadata")
            result.update(hudConsistency="same_match_frame_world_time_resources_population", hudAgeObserved=values[6],
                          objectiveMode=data["objective"].get("mode"), objectiveStatus=data["objective"].get("status"))
        result["status"] = "identity_verified"
    except (OSError, ValueError, KeyError, TypeError, AttributeError, struct.error) as error:
        result["errors"].append(str(error))
    return result


def verify(captures, build_path):
    report = {"schema": 1, "recordedAtUtc": datetime.now(timezone.utc).isoformat(),
              "status": "failed", "scope": SCOPE, "nativeFunctionality": "not_evaluated",
              "controlledFixtureClassification": "not_evaluated_F9_has_no_maxAge_or_grant_provenance",
              "capturesDirectory": str(captures), "errors": [], "captures": [], "ignoredJsonFiles": []}
    try:
        require(captures.is_dir() and not captures.is_symlink(), "Captures must be an existing non-symlink directory")
        raw = read_regular(build_path)
        build = strict_json(raw)
        require(isinstance(build, dict), "Build receipt must be an object")
        require(build.get("appVersion") == VERSION, "Build receipt must identify 0.8.0")
        require(isinstance(build.get("buildGuid"), str) and HEX32.fullmatch(build["buildGuid"]) and int(build["buildGuid"], 16) != 0, "Invalid build receipt GUID")
        require(isinstance(build.get("unityVersion"), str) and bool(build["unityVersion"]), "Build receipt needs unityVersion")
        report["buildReceipt"] = {"path": str(build_path), "bytes": len(raw), "sha256": sha256(raw),
                                  "appVersion": build["appVersion"], "buildGuid": build["buildGuid"], "unityVersion": build["unityVersion"]}
        report["buildProvenanceReportedButNotIndependentlyVerified"] = {
            key: build.get(key) for key in ("runtimeCommit", "sourceFingerprint", "architecture", "buildType")}
        for directory, subdirs, names in os.walk(captures, followlinks=False):
            subdirs.sort()
            for name in list(subdirs):
                path = Path(directory) / name
                if path.is_symlink():
                    subdirs.remove(name)
                    report["errors"].append("Refused symlink subdirectory: " + str(path.relative_to(captures)))
            for name in sorted(names):
                path = Path(directory) / name
                if not name.endswith(".json"):
                    continue
                if not CAPTURE.fullmatch(name):
                    report["ignoredJsonFiles"].append(path.relative_to(captures).as_posix())
                    continue
                report["captures"].append(verify_pair(path, captures, build))
        require(report["captures"], "No canonical F9 capture JSON files found; empty input cannot pass")
    except (OSError, ValueError, KeyError, TypeError) as error:
        report["errors"].append(str(error))
    report["captureCount"] = len(report["captures"])
    report["verifiedPairCount"] = sum(item["status"] == "identity_verified" for item in report["captures"])
    report["failedPairCount"] = report["captureCount"] - report["verifiedPairCount"]
    if report["captureCount"] and not report["errors"] and not report["failedPairCount"]:
        report["status"] = "identity_verified"
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--captures", required=True, type=Path, help="Recursively inspect canonical F9 JSON/PNG pairs here")
    parser.add_argument("--build-receipt", required=True, type=Path, help="Actual 0.8.0 client-build.json")
    parser.add_argument("--output", type=Path, help="Create a new JSON report (refuses overwrite); default: stdout")
    args = parser.parse_args()
    captures = args.captures.expanduser().absolute()
    receipt = args.build_receipt.expanduser().absolute()
    report = verify(captures, receipt)
    output = json.dumps(report, ensure_ascii=False, indent=2, allow_nan=False) + "\n"
    if args.output:
        try:
            require(not CAPTURE.fullmatch(args.output.name), "Report must not use a capture filename")
            with args.output.expanduser().open("x", encoding="utf-8") as stream:
                stream.write(output)
        except (OSError, ValueError) as error:
            print("Cannot create report: " + str(error), file=sys.stderr)
            return 2
    else:
        sys.stdout.write(output)
    return 0 if report["status"] == "identity_verified" else 1


if __name__ == "__main__":
    sys.exit(main())
