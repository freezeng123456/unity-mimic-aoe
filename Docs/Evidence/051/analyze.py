from pathlib import Path
import collections
import hashlib
import json

WORK = Path(__file__).parent
PROJECT = WORK.parents[2]
BASELINE = WORK.parent / "050"
RULES = json.loads((PROJECT / "Assets" / "Resources" / "Rules.json").read_text())
DEFS = {d["name"]: d for d in RULES["entities"]}


def load_match(directory):
    events = [json.loads(line) for line in (directory / "events.jsonl").read_text().splitlines()]
    result = json.loads((directory / "result.json").read_text())
    snapshots = [json.loads(line) for line in (directory / "snapshots.jsonl").read_text().splitlines()]
    assert result["events"] == len(events), (directory.name, result["events"], len(events))
    return events, result, snapshots


def analyze(name, directory):
    events, result, snapshots = load_match(directory)
    spawned = {e["entity"]: e for e in events if e["kind"] == "Spawn"}
    buildings = {i for i, e in spawned.items() if DEFS.get(e.get("message"), {}).get("kind") == 1}
    sustained = {i for i, e in spawned.items() if DEFS.get(e.get("message"), {}).get("id") in {"farm", "estate"}}
    completed = {e["entity"]: e["time"] for e in events if e["kind"] == "BuildingComplete"}
    deaths = {e["entity"]: e["time"] for e in events if e["kind"] == "Death"}
    premature = [e for e in events if e["kind"] == "Command" and e.get("message", "").startswith("Gather")
                 and e.get("target") in sustained and e["time"] < completed.get(e["target"], float("inf"))]
    resumed = [{"time": e["time"], "player": e.get("player"), "worker": e.get("entity"),
                "foundation": e.get("target"), "completedAt": completed.get(e.get("target")),
                "destroyedAt": deaths.get(e.get("target"))}
               for e in events if e["kind"] == "AIConstructionResumed"]
    adjusted = []
    for e in events:
        if e["kind"] != "DestinationAdjusted":
            continue
        raw = e.get("distance", e.get("value", e.get("amount", 0)))
        try:
            distance = float(raw)
        except (TypeError, ValueError):
            distance = None
        adjusted.append({"time": e.get("time"), "player": e.get("player"), "unit": e.get("entity"),
                         "distance": distance, "message": e.get("message")})
    assert all(e["distance"] is not None and 0 <= e["distance"] <= 8.0001 for e in adjusted)
    active = [set(), set()]
    first_town_deaths = {}
    ledger_failures, population_failures = [], []
    structured = collections.Counter()
    for e in events:
        owner, entity = e.get("player"), e.get("entity")
        if e["kind"] == "Spawn" and owner in (0, 1) and DEFS.get(e.get("message"), {}).get("kind") != 2:
            active[owner].add(entity)
        if e["kind"] == "Death":
            for ids in active:
                ids.discard(entity)
            if (spawned.get(entity, {}).get("message") == "Town Center" and owner in (0, 1)
                    and owner not in first_town_deaths):
                first_town_deaths[owner] = {"time": e["time"], "remainingUnitsAndBuildings": len(active[owner])}
        if e.get("hasEconomy"):
            structured[e["kind"]] += 1
            t = e["economy"]
            for key in ("food", "wood", "coin"):
                if abs(t["balanceBefore"][key] + t["actualDelta"][key] - t["balanceAfter"][key]) > .02 \
                        or t["balanceAfter"][key] < -.02:
                    ledger_failures.append([e["time"], e["kind"], key])
        if e.get("hasPopulation") and any(e["population"][key] < 0 for key in ("used", "reserved", "capacity")):
            population_failures.append(e["time"])
    ended = [e for e in events if e["kind"] == "MatchEnded"]
    winner = result["winner"]
    assert winner in (0, 1) and len(ended) == 1 and ended[0]["player"] == winner and not active[1 - winner]
    assert not ledger_failures and not population_failures
    if name.startswith("normal-"):
        assert not premature, (name, len(premature))
    technologies = [(e["player"], e["message"].split()[0]) for e in events if e["kind"] == "Technology"]
    duplicates = [str(k) for k, n in collections.Counter(technologies).items() if n > 1]
    assert not duplicates
    return {
        "result": result,
        "destinationAdjusted": {"count": len(adjusted), "byPlayer": dict(collections.Counter(e["player"] for e in adjusted)),
                                "byUnit": dict(collections.Counter(e["unit"] for e in adjusted)),
                                "distancesOver8": [e for e in adjusted if e["distance"] is not None and e["distance"] > 8],
                                "events": adjusted},
        "prematureGatherCount": len(premature),
        "prematureGatherExamples": premature[:6], "constructionResumes": resumed,
        "cannonBuildingDamageEvents": {str(i): sum(e.get("player") == i and e.get("target") in buildings
                                                    and DEFS.get(spawned.get(e.get("entity"),{}).get("message"),{}).get("id")=="cannon"
                                                    for e in events if e["kind"] == "Damage") for i in (0, 1)},
        "firstTownDeaths": first_town_deaths, "survivingUnitsAndBuildings": list(map(len, active)),
        "structuredEconomyKinds": dict(structured), "ledgerAnomalies": ledger_failures,
        "negativePopulationRecords": population_failures, "duplicateTechnologyEvents": duplicates,
        "players": [{"player": i, "ages": [e for e in events if e["kind"] == "AgeAdvanced" and e.get("player") == i],
                     "shipmentsSent": sum(e["kind"] == "ShipmentSent" and e.get("player") == i for e in events),
                     "shipmentsArrived": sum(e["kind"] == "ShipmentArrived" and e.get("player") == i for e in events),
                     "finalSnapshot": snapshots[-2 + i]} for i in (0, 1)],
        "files": {f.name: {"bytes": f.stat().st_size, "sha256": hashlib.sha256(f.read_bytes()).hexdigest()}
                  for f in directory.iterdir() if f.is_file()},
    }


directories = {
    "baseline-coast": BASELINE / "normal-coast",
    "baseline-river-coast": BASELINE / "normal-river-coast",
    "normal-coast": WORK / "normal-coast",
    "normal-river-coast": WORK / "normal-river-coast",
}
summary = {name: analyze(name, path) for name, path in directories.items()}
(WORK / "analysis.json").write_text(json.dumps(summary, indent=2) + "\n")
comparison={}
for pair in ("coast","river-coast"):
    old,new=summary["baseline-"+pair],summary["normal-"+pair]
    comparison[pair]={"oldResult":old["result"],"newResult":new["result"],"filesIdentical":{n:old["files"][n]["sha256"]==new["files"][n]["sha256"] for n in ["events.jsonl","result.json","snapshots.jsonl"]}}
(WORK/"normal-comparison.json").write_text(json.dumps(comparison,indent=2)+"\n")
for name, report in summary.items():
    print(name, {key: report[key] for key in ("result", "prematureGatherCount", "constructionResumes", "cannonBuildingDamageEvents", "firstTownDeaths")}, "destinationAdjusted", report["destinationAdjusted"]["count"])
