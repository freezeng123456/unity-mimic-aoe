#!/usr/bin/env python3
# Generates authored UI fixtures only; never reads current Rules or edits a World.
import argparse
import copy
import hashlib
import json
import re
from datetime import datetime, timezone
from pathlib import Path


MAX_BYTES = 2 * 1024 * 1024
ENCODING = "frontier-scenario-json-v1"
DOC_KEYS = set("schema id name description rulesHash width height seed waterHeightCm victoryPlayer enableElimination heightsCm surfaces rivers bridges starts resources objects groups tradeRoute tradeSockets treasures neutralBindings regions objectives triggers".split())
START_KEYS = set("player team age civilization initialResources town explorer workers".split())
BUDGETS = {
    "coast": ("British", {"food": 1300, "wood": 800, "coin": 1200}),
    "france": ("French", {"food": 1320, "wood": 800, "coin": 1200}),
    "river": ("Dutch", {"food": 1200, "wood": 800, "coin": 1300}),
    "ottoman": ("Ottoman", {"food": 1200, "wood": 800, "coin": 1200}),
}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def compact(value):
    return json.dumps(value, ensure_ascii=False, separators=(",", ":"), allow_nan=False)


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, "Duplicate JSON key: " + key)
        result[key] = value
    return result


def invalid_constant(value):
    raise ValueError("Nonfinite JSON number: " + value)


def decode_json(data):
    return json.loads(data, object_pairs_hook=unique_object, parse_constant=invalid_constant)


def read_regular(path):
    require(path.is_file() and not path.is_symlink(), "Expected a regular input file: " + str(path))
    raw = path.read_bytes()
    require(len(raw) <= MAX_BYTES, "Input exceeds the portable scenario size limit")
    return raw


def check_document(document):
    require(isinstance(document, dict) and set(document) == DOC_KEYS, "Require the complete current ScenarioDocument DTO; no legacy migration or missing fields")
    require(type(document["schema"]) is int and document["schema"] == 1, "Unsupported document schema")
    require(document["width"] == 128 and document["height"] == 128, "Require the current 128x128 Traders' Bend")
    require(document["id"] == "traders_bend070", "Source must be the normal Traders' Bend opening")
    require(isinstance(document["rulesHash"], str) and re.fullmatch(r"[0-9a-f]{64}", document["rulesHash"]), "Missing source rulesHash")
    require(len(document["heightsCm"]) == 129 * 129 and len(document["surfaces"]) == 128 * 128, "Terrain arrays are incomplete")
    require(document["enableElimination"] is True, "Source must retain normal skirmish elimination")
    require(document["objects"] == [] and document["groups"] == [] and document["objectives"] == [] and document["triggers"] == [], "Source must be a plain skirmish without armies, groups or scripted outcomes")
    require(isinstance(document["starts"], list) and len(document["starts"]) == 2, "Require two real normal opening slots")
    for player, start in enumerate(document["starts"]):
        require(isinstance(start, dict) and set(start) == START_KEYS, "Starting slot DTO is incomplete")
        require(start["player"] == player and start["team"] == player and start["age"] == 1, "Source is not the normal age-I mirror opening")
        require(len(start["workers"]) == 6, "Require six actual original workers in each source slot")
        require(set(start["initialResources"]) == {"food", "wood", "coin"}, "Incomplete cost DTO")
        for point in [start["town"], start["explorer"], *start["workers"]]:
            require(isinstance(point, dict) and set(point) == {"x", "y"}, "Incomplete start position DTO")


def decode_envelope(raw):
    envelope = decode_json(raw.decode("utf-8", errors="strict"))
    require(isinstance(envelope, dict) and set(envelope) == {"schema", "encoding", "payload", "sha256"}, "Incomplete or unknown envelope fields")
    require(type(envelope["schema"]) is int and envelope["schema"] == 1 and envelope["encoding"] == ENCODING, "Unsupported portable scenario envelope")
    require(isinstance(envelope["payload"], str) and envelope["payload"], "Payload must be a nonempty JSON string")
    payload_bytes = envelope["payload"].encode("utf-8", errors="strict")
    require(len(payload_bytes) <= MAX_BYTES, "Payload exceeds the scenario limit")
    require(envelope["sha256"] == digest(payload_bytes), "Original envelope payload checksum differs")
    document = decode_json(envelope["payload"])
    check_document(document)
    return envelope, document


def changed(path, before, after):
    return {"path": path, "before": before, "after": after}


def transform(source, civilization):
    document = copy.deepcopy(source)
    label, budget = BUDGETS[civilization]
    document["id"] = "controlled_iv071_" + civilization
    document["name"] = "Controlled IV UI: " + label
    document["description"] = (
        "Controlled age-IV native UI fixture, not a normal opening or AI route result. "
        "Player 0 has six real initial civilian workers, the original town/explorer, "
        "and authored funds for revolution, civilian rebuilding and one legal new worker. "
        "Player 1 is France at age I with no initial workers or funds; its distant town/explorer remain real. "
        "Terrain and source rulesHash come from the completed normal-opening matrix. "
        "No XP or live-world state is gifted. Elimination and scripted objectives/triggers are disabled."
    )
    document["enableElimination"] = False
    document["objectives"] = []
    document["triggers"] = []
    player = document["starts"][0]
    player.update(civilization=civilization, team=0, age=4, initialResources=copy.deepcopy(budget))
    opponent = document["starts"][1]
    opponent.update(civilization="france", team=1, age=1,
                    initialResources={"food": 0, "wood": 0, "coin": 0}, workers=[])
    mutable = {"id", "name", "description", "enableElimination", "objectives", "triggers", "starts"}
    for key in DOC_KEYS - mutable:
        require(document[key] == source[key], "Unapproved geography/content change: " + key)
    for index in (0, 1):
        for key in ("player", "town", "explorer"):
            require(document["starts"][index][key] == source["starts"][index][key], "Starting town/explorer/player changed")
    require(document["starts"][0]["workers"] == source["starts"][0]["workers"], "Player0's six real workers must stay in place")
    require(set(document) == set(source), "DTO fields were dropped")
    changes = []
    for key in ("id", "name", "description", "enableElimination", "objectives", "triggers"):
        if source[key] != document[key]:
            changes.append(changed(key, source[key], document[key]))
    for index in (0, 1):
        for key in START_KEYS:
            before, after = source["starts"][index][key], document["starts"][index][key]
            if before != after:
                changes.append(changed("starts[" + str(index) + "]." + key, before, after))
    return document, changes


def encode(document):
    payload = compact(document)
    envelope = {"schema": 1, "encoding": ENCODING, "payload": payload,
                "sha256": digest(payload.encode("utf-8"))}
    raw = compact(envelope).encode("utf-8")
    require(len(raw) <= MAX_BYTES, "Generated file exceeds the portable scenario size limit")
    return envelope, raw


def main():
    parser = argparse.ArgumentParser(description="Prepare controlled age-IV native UI scenarios from a completed, source-bound normal matrix opening; does not launch Unity or a game")
    parser.add_argument("--opening", required=True, type=Path)
    parser.add_argument("--matrix-manifest", required=True, type=Path)
    parser.add_argument("--expected-source-fingerprint", required=True)
    parser.add_argument("--output-dir", required=True, type=Path, help="New directory; existing output is never replaced")
    parser.add_argument("--civ", action="append", choices=list(BUDGETS), help="Repeat to select factions; default is all four")
    args = parser.parse_args()
    require(re.fullmatch(r"[0-9a-f]{64}", args.expected_source_fingerprint), "Expected external source fingerprint must be64 lowercase hex characters")
    opening_raw, matrix_raw = read_regular(args.opening), read_regular(args.matrix_manifest)
    opening_path, matrix_path = args.opening.resolve(), args.matrix_manifest.resolve()
    envelope, source = decode_envelope(opening_raw)
    matrix = decode_json(matrix_raw.decode("utf-8", errors="strict"))
    require(matrix.get("status") in {"all_routes_reached", "observed_routes_incomplete"} and matrix.get("finishedUtc"), "The normal matrix has not finished")
    require(matrix.get("declaredSourceFingerprint") == args.expected_source_fingerprint, "Matrix belongs to a different externally frozen source")
    require(matrix.get("rulesHash") == source["rulesHash"], "Opening's rulesHash differs from the completed matrix")
    require(opening_path.name == "opening-scenario.json" and opening_path.parent.parent == matrix_path.parent, "Opening is not a case file in the supplied matrix")
    cases = matrix.get("cases")
    require(isinstance(cases, list) and len(cases) == 8, "Require the complete eight-case normal matrix")
    require(len({case.get("id") for case in cases}) == 8, "Duplicate matrix case IDs")
    selected = [case for case in cases if case.get("id") == opening_path.parent.name]
    require(len(selected) == 1, "Opening's case is not uniquely listed in matrix")
    case = selected[0]
    require(case.get("status") in {"route_reached", "ended_before_route", "route_not_reached_at_limit", "government_rebuilt_economy_not_recovered"}, "Selected opening did not successfully start a real normal game")
    require(case.get("seed") == source["seed"] and case.get("startingWorkers") == 6, "Source bootstrap differs from its observed matrix case")
    for start in source["starts"]:
        require(start["civilization"] == case.get("civilization") and start["initialResources"] == case.get("startingResources"), "Source opening funds/civilization differ from the actual observed bootstrap")
    civilizations = args.civ or list(BUDGETS)
    require(len(set(civilizations)) == len(civilizations), "Do not repeat a civilization")
    prepared = []
    for civilization in civilizations:
        document, changes = transform(source, civilization)
        result_envelope, result_raw = encode(document)
        prepared.append((civilization, document, changes, result_envelope, result_raw))
    require(read_regular(args.opening) == opening_raw and read_regular(args.matrix_manifest) == matrix_raw, "Inputs changed during preparation")
    output = args.output_dir.absolute()
    output.mkdir(parents=False, exist_ok=False)
    receipt = {
        "schema": 1, "preparedUtc": datetime.now(timezone.utc).isoformat(),
        "scope": "Authored controlled age-IV native UI fixtures; not normal-opening completion evidence",
        "beforeBootstrapScope": "All civilian counts, era and funds are document inputs before World construction. No XP, live World mutation, injected queue, free technology or victory is provided.",
        "nativeAcceptance": "Pending actual game ScenarioCodec/ScenarioCompiler/rules validation and native UI actions; Python checks do not certify playability",
        "sourceOpening": {"path": str(opening_path), "fileSha256": digest(opening_raw), "payloadSha256": envelope["sha256"], "scenarioId": source["id"], "rulesHash": source["rulesHash"], "starts": source["starts"]},
        "matrix": {"path": str(matrix_path), "fileSha256": digest(matrix_raw), "declaredSourceFingerprint": args.expected_source_fingerprint, "status": matrix["status"], "caseId": case["id"], "caseStatus": case["status"]},
        "preservedFields": sorted(DOC_KEYS - {"id", "name", "description", "enableElimination", "objectives", "triggers", "starts"}),
        "hashMeaning": "payloadSha256 is the portable envelope's exact UTF8 checksum, not Unity's reserialized World.ScenarioHash; fileSha256 hashes exact emitted file bytes",
        "documents": []
    }
    for civilization, document, changes, result_envelope, result_raw in prepared:
        path = output / (document["id"] + ".scenario.json")
        with path.open("xb") as handle:
            handle.write(result_raw)
        receipt["documents"].append({
            "civilization": civilization, "scenarioId": document["id"], "name": document["name"],
            "path": str(path), "bytes": len(result_raw), "fileSha256": digest(result_raw), "payloadSha256": result_envelope["sha256"],
            "rulesHash": document["rulesHash"], "startingResources": document["starts"][0]["initialResources"],
            "startingWorkers": len(document["starts"][0]["workers"]), "changes": changes,
            "suggestedFreshDataDirectory": str(output / ("native-profile-" + civilization))
        })
    require(read_regular(args.opening) == opening_raw and read_regular(args.matrix_manifest) == matrix_raw, "Inputs changed before receipt completion")
    manifest = output / "prepared-native-scenarios-071.json"
    with manifest.open("x", encoding="utf-8") as handle:
        json.dump(receipt, handle, ensure_ascii=False, indent=2, allow_nan=False)
        handle.write("\n")
    print(str(manifest))
    for item in receipt["documents"]:
        print(item["path"] + " " + item["fileSha256"])


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError, KeyError, TypeError, UnicodeError) as error:
        raise SystemExit("Preparation refused: " + str(error))
