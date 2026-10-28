#!/usr/bin/env python3
"""Derive declared UI fixtures before bootstrap; never launch Unity or mutate a World."""
import argparse
import copy
import importlib.util
import json
import math
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

sys.dont_write_bytecode = True
HELPER_PATH = Path(__file__).with_name("prepare-controlled-native-scenarios-071.py")
spec = importlib.util.spec_from_file_location("native_prep071", HELPER_PATH)
helper = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helper)
require, digest = helper.require, helper.digest

# Exact serialized public DTO shapes, including inactive/default fields. Do not
# pass unknown nested keys to Unity's permissive JsonUtility parser.
POINT = {"x": "number", "y": "number"}
COST = {"food": "number", "wood": "number", "coin": "number"}
START = {"player": "int", "team": "int", "age": "int", "civilization": "string",
         "initialResources": COST, "town": POINT, "explorer": POINT, "workers": [POINT]}
PLACEMENT = {"id": "string", "definition": "string", "position": POINT,
             "owner": "int", "remaining": "number", "complete": "bool"}
OBJECTIVE = {key: "string" for key in ("id", "title", "description", "kind", "objectId", "targetId", "regionId")}
OBJECTIVE.update(player="int", required="bool", resource="int", amount="number",
                 holdSeconds="number", deadline="number", prerequisites=["string"])
CONDITION = {key: "string" for key in ("kind", "objectId", "targetId", "regionId", "objectiveId", "triggerId")}
CONDITION.update(player="int", resource="int", amount="number", time="number")
ACTION = {key: "string" for key in ("kind", "objectiveId", "regionId", "objectId", "triggerId", "definition", "targetId", "message")}
ACTION.update(player="int", count="int", point=POINT, resource="int", amount="number")
TRIGGER = {"id": "string", "enabled": "bool", "matchAny": "bool", "maxFires": "int",
           "repeatSeconds": "number", "conditions": [CONDITION], "actions": [ACTION]}
DOCUMENT = {
    "schema": "int", "id": "string", "name": "string", "description": "string", "rulesHash": "string",
    "width": "int", "height": "int", "seed": "int", "waterHeightCm": "int", "victoryPlayer": "int",
    "enableElimination": "bool", "heightsCm": ["int"], "surfaces": ["int"],
    "rivers": [{"id": "string", "points": [POINT], "halfWidths": ["number"], "smooth": "bool"}],
    "bridges": [{"id": "string", "center": POINT, "direction": POINT, "halfLength": "number",
                 "halfWidth": "number", "rampLength": "number", "height": "number"}],
    "starts": [START], "resources": [PLACEMENT], "objects": [PLACEMENT],
    "groups": [{"id": "string", "objectIds": ["string"]}], "tradeRoute": [POINT],
    "tradeSockets": [{"id": "string", "position": POINT, "waypoint": "int"}],
    "treasures": [{"id": "string", "definition": "int", "position": POINT, "guardPositions": [POINT]}],
    "neutralBindings": [{"definition": "string", "socketId": "string"}],
    "regions": [{"id": "string", "center": POINT, "size": POINT}],
    "objectives": [OBJECTIVE], "triggers": [TRIGGER],
}
require(set(DOCUMENT) == helper.DOC_KEYS, "Portable DTO contract differs from the original preparation helper")
NORMAL_RESOURCES = {"food": 300, "wood": 300, "coin": 100}
ID_ARRAYS = ("rivers", "bridges", "resources", "objects", "groups", "tradeSockets", "treasures", "regions", "objectives", "triggers")
CRATE_ID = "controlled_supply071"


def shape(value, model, location="document"):
    if isinstance(model, dict):
        require(isinstance(value, dict) and set(value) == set(model), "Missing or unknown DTO fields at " + location)
        for key, child in model.items():
            shape(value[key], child, location + "." + key)
    elif isinstance(model, list):
        require(isinstance(value, list), "Expected array at " + location)
        for index, child in enumerate(value):
            shape(child, model[0], location + "[" + str(index) + "]")
    elif model == "int":
        require(type(value) is int and -(2 ** 31) <= value < 2 ** 31, "Expected Int32 at " + location)
    elif model == "number":
        require(type(value) in (int, float) and math.isfinite(value) and abs(value) <= 3.402823466e38, "Expected finite float at " + location)
    elif model == "bool":
        require(type(value) is bool, "Expected bool at " + location)
    else:
        require(isinstance(value, str) and "\0" not in value, "Expected string at " + location)


def stable_ids(document):
    seen = set()
    for start in document["starts"]:
        prefix = "start-" + str(start["player"])
        seen.update((prefix + "-town", prefix + "-explorer"))
        seen.update(prefix + "-worker-" + str(i) for i in range(len(start["workers"])))
    for field in ID_ARRAYS:
        for item in document[field]:
            item_id = item["id"]
            require(re.fullmatch(r"[a-z0-9_-]{1,64}", item_id) and item_id not in seen, "Invalid or duplicate stable ID: " + item_id)
            seen.add(item_id)
            if field == "treasures":
                for index in range(len(item["guardPositions"])):
                    alias = item_id + "-guard-" + str(index)
                    require(alias not in seen, "Duplicate treasure guard ID: " + alias)
                    seen.add(alias)
    return seen


def check_dto(document):
    shape(document, DOCUMENT)
    require(document["schema"] == 1 and document["width"] == 128 and document["height"] == 128, "Require current schema and fixed 128x128 geography")
    require(re.fullmatch(r"[a-z0-9_-]{1,64}", document["id"]), "Invalid scenario ID")
    require(re.fullmatch(r"[0-9a-f]{64}", document["rulesHash"]), "Missing source rulesHash")
    require(len(document["heightsCm"]) == 129 * 129 and len(document["surfaces"]) == 128 * 128, "Incomplete terrain arrays")
    require(len(document["starts"]) == 2 and document["victoryPlayer"] in (0, 1), "Require two ordered controlled player slots")
    for index, start in enumerate(document["starts"]):
        require(start["player"] == index and start["team"] == index, "Player/team ordering differs")
        require(1 <= start["age"] <= 5 and all(0 <= x <= 1000000 for x in start["initialResources"].values()), "Invalid starting era or budget")
        for point in (start["town"], start["explorer"], *start["workers"]):
            require(all(0 <= x <= 128 for x in point.values()), "Starting point is outside the map")
    stable_ids(document)


def decode_source(raw):
    envelope = helper.decode_json(raw.decode("utf-8", errors="strict"))
    require(isinstance(envelope, dict) and set(envelope) == {"schema", "encoding", "payload", "sha256"}, "Missing or unknown portable envelope fields")
    require(type(envelope["schema"]) is int and envelope["schema"] == 1 and envelope["encoding"] == helper.ENCODING, "Unsupported portable envelope")
    require(isinstance(envelope["payload"], str) and envelope["payload"], "Missing payload string")
    payload = envelope["payload"].encode("utf-8", errors="strict")
    require(len(payload) <= helper.MAX_BYTES and envelope["sha256"] == digest(payload), "Source payload size or SHA256 differs")
    document = helper.decode_json(envelope["payload"])
    check_dto(document)
    require(document["id"] == "controlled_iv071_coast", "Source must be the previously prepared controlled British IV document")
    require(document["enableElimination"] is False and document["victoryPlayer"] == 0, "Source must retain controlled player0/no-elimination scope")
    require(all(document[field] == [] for field in ("objects", "groups", "objectives", "triggers")), "Source already contains armies, groups or scripted outcomes")
    p0, p1 = document["starts"]
    require(p0["civilization"] == "coast" and p0["age"] == 4 and p0["initialResources"] == helper.BUDGETS["coast"][1] and len(p0["workers"]) == 6, "Source's real six British civilians or approved IV budget differ")
    require(p1["civilization"] == "france" and p1["age"] == 1 and p1["initialResources"] == {"food": 0, "wood": 0, "coin": 0} and p1["workers"] == [], "Source opponent bootstrap differs")
    require(CRATE_ID not in stable_ids(document), "Reserved UI crate ID already exists")
    return envelope, document


def objective(deadline):
    result = {key: "" for key in ("objectId", "targetId", "regionId")}
    result.update(id="controlled_goal071", title="CONTROLLED HUD GOAL", description="Authored 0.5s terminal HUD fixture; not ordinary match victory evidence.",
                  kind="manual", player=0, required=True, resource=0, amount=1, holdSeconds=30, deadline=deadline, prerequisites=[])
    return result


def completion_trigger():
    condition = {key: "" for key in ("objectId", "targetId", "regionId", "objectiveId", "triggerId")}
    condition.update(kind="time", player=0, resource=0, amount=1, time=0.5)
    action = {key: "" for key in ("regionId", "objectId", "triggerId", "definition", "targetId", "message")}
    action.update(kind="complete_objective", objectiveId="controlled_goal071", player=0, count=1, point={"x": 0, "y": 0}, resource=0, amount=1)
    return {"id": "controlled_finish071", "enabled": True, "matchAny": False, "maxFires": 1,
            "repeatSeconds": 1, "conditions": [condition], "actions": [action]}


def derive(source):
    documents = []
    actions = copy.deepcopy(source)
    actions.update(id="controlled_iv_actions071", name="CONTROLLED IV UI ACTIONS: British",
                   description="Controlled IV native UI actions, not normal-opening progress. Original six civilian workers, town/explorer, 1300F/800W/1200C and terrain retained. One authored own Food Crate of 400 at (38,18) is added before World construction for actual right-click collection. No wallet credit, XP, queue or live World mutation is provided.")
    actions["resources"].append({"id": CRATE_ID, "definition": "crate_food", "position": {"x": 38, "y": 18}, "owner": 0, "remaining": 400, "complete": True})
    documents.append(("iv_actions", actions, {"id", "name", "description", "resources"}))
    for failed in (False, True):
        terminal = copy.deepcopy(source)
        terminal.update(id="controlled_hud_fail071" if failed else "controlled_hud_complete071",
                        name="CONTROLLED HUD: " + ("deadline failure at 0.5s" if failed else "goal completed at 0.5s"),
                        description="Controlled terminal HUD fixture. Both eras and starting funds are authored age I / current British-French ordinary 300F/300W/100C before bootstrap. Original positions, six player0 workers, zero player1 workers and terrain retained. Elimination disabled. Required manual goal " + ("expires after 0.5s." if failed else "is completed by a time condition at 0.5s.") + " This is a scripted HUD fixture, not normal-opening or earned victory evidence. No clock, World or account is edited after construction.",
                        objectives=[objective(0.5 if failed else 0)], triggers=[] if failed else [completion_trigger()])
        for start in terminal["starts"]:
            start["age"] = 1
            start["initialResources"] = copy.deepcopy(NORMAL_RESOURCES)
        documents.append(("terminal_failure" if failed else "terminal_completion", terminal,
                          {"id", "name", "description", "starts", "objectives", "triggers"}))
    prepared = []
    for kind, document, allowed in documents:
        check_dto(document)
        for key in set(DOCUMENT) - allowed:
            require(document[key] == source[key], "Unapproved content/geography change: " + key)
        for index in (0, 1):
            for key in ("player", "team", "civilization", "town", "explorer", "workers"):
                require(document["starts"][index][key] == source["starts"][index][key], "Unapproved actor change: " + key)
        if kind == "iv_actions":
            require(document["starts"] == source["starts"] and document["resources"][:-1] == source["resources"], "IV actions changed actors/budget or original resources")
        changes = [helper.changed(key, source[key], document[key]) for key in sorted(allowed) if source[key] != document[key]]
        envelope, raw = helper.encode(document)
        require(helper.decode_json(envelope["payload"]) == document and digest(envelope["payload"].encode("utf-8")) == envelope["sha256"], "Generated portable roundtrip differs")
        prepared.append((kind, document, changes, envelope, raw))
    return prepared


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True, help="Actual previously prepared controlled_iv071_coast.scenario.json")
    parser.add_argument("--output-dir", type=Path, required=True, help="New directory; parent must exist, existing output is never replaced")
    args = parser.parse_args()
    source_path = args.source.absolute()
    source_raw, helper_raw = helper.read_regular(source_path), helper.read_regular(HELPER_PATH)
    envelope, source = decode_source(source_raw)
    prepared = derive(source)
    require(helper.read_regular(source_path) == source_raw and helper.read_regular(HELPER_PATH) == helper_raw, "Inputs changed during preparation")
    output = args.output_dir.absolute()
    output.mkdir(parents=False, exist_ok=False)
    receipt = {
        "schema": 1, "preparedUtc": datetime.now(timezone.utc).isoformat(),
        "scope": "Controlled IV native UI actions and two authored terminal HUD fixtures; not ordinary opening, earned victory, AI route or acceptance results",
        "beforeBootstrapScope": "Only document inputs are changed before game construction. The added 400F is one own physical resource crate, not wallet credit. Terminal goals are deliberately authored outcomes. No live clock, World or account mutation is performed.",
        "nativeAcceptance": "Pending actual ScenarioCodec/ScenarioCompiler/rules validation and native UI execution; Python structure/checksum checks do not certify geography or gameplay",
        "source": {"path": str(source_path.resolve()), "fileSha256": digest(source_raw), "payloadSha256": envelope["sha256"], "scenarioId": source["id"], "rulesHash": source["rulesHash"], "starts": source["starts"]},
        "preparationHelper": {"path": str(HELPER_PATH.resolve()), "fileSha256": digest(helper_raw)},
        "hashMeaning": "payloadSha256 hashes exact portable UTF8 payload bytes, not Unity's reserialized World.ScenarioHash; fileSha256 hashes exact emitted envelope bytes",
        "terminalNormalResourceContract": {"coast": NORMAL_RESOURCES, "france": NORMAL_RESOURCES},
        "documents": [],
    }
    for kind, document, changes, result_envelope, result_raw in prepared:
        path = output / (document["id"] + ".scenario.json")
        with path.open("xb") as handle:
            handle.write(result_raw)
        require(path.read_bytes() == result_raw, "Output readback differs")
        receipt["documents"].append({"kind": kind, "path": str(path), "scenarioId": document["id"], "name": document["name"],
                                     "bytes": len(result_raw), "fileSha256": digest(result_raw), "payloadSha256": result_envelope["sha256"], "rulesHash": document["rulesHash"], "changes": changes,
                                     "suggestedFreshDataDirectory": str(output / ("native-profile-" + kind))})
    require(helper.read_regular(source_path) == source_raw and helper.read_regular(HELPER_PATH) == helper_raw, "Inputs changed before receipt completion")
    manifest = output / "prepared-ui-actions-071.json"
    with manifest.open("x", encoding="utf-8") as handle:
        json.dump(receipt, handle, ensure_ascii=False, indent=2, allow_nan=False)
        handle.write("\n")
    print(str(manifest))
    for item in receipt["documents"]:
        print(item["path"] + " " + item["fileSha256"])


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError, KeyError, TypeError, UnicodeError, OverflowError, RecursionError) as error:
        raise SystemExit("Preparation refused: " + str(error))
