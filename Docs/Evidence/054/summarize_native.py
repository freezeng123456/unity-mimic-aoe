#!/usr/bin/env python3
"""Read native reports for one nonzero build GUID; write evidence, never game data."""
import argparse
import collections
import datetime
import hashlib
import json
import math
import pathlib
import re


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def source(path, archive_root):
    result = {"path": str(path.resolve()), "sha256": digest(path)}
    archived = archive_root / path.name
    if archived.is_file() and digest(archived) == result["sha256"]:
        result["archivedPath"] = str(archived.resolve())
    return result


def summarize(report_path, report, rules, archive_root):
    event_path = report_path.with_name(report_path.stem + "-events.jsonl")
    if not event_path.is_file():
        return {"matchId": report["matchId"], "reportSource": source(report_path, archive_root),
                "status": "incomplete", "error": "Corresponding events file is missing"}
    events = [json.loads(line) for line in event_path.read_text().splitlines() if line.strip()]
    definitions = {entity["name"]: entity for entity in rules["entities"]}
    civs = {civ["id"]: civ for civ in rules["civs"]}
    kinds = collections.Counter(event["kind"] for event in events)
    spawned, alive, unknown = {}, {}, []
    player_counts = [collections.Counter(), collections.Counter()]
    queues = collections.defaultdict(collections.Counter)
    unpaid_queue_events = []
    unmatched_training = []
    cannon_damage = [[], []]
    cannon_fires = [0, 0]
    spawn_counts = [collections.Counter(), collections.Counter()]
    trained_counts = [collections.Counter(), collections.Counter()]
    first_cannon = [None, None]
    previous = None
    for event in events:
        kind, player, entity = event["kind"], event["player"], event["entity"]
        own = player in (0, 1)
        if own:
            player_counts[player][kind] += 1
        if kind == "Spawn":
            definition = definitions.get(event["message"])
            if definition is None:
                unknown.append({"entity": entity, "message": event["message"]})
            spawned[entity] = {"player": player, "definition": definition, "time": event["time"]}
            alive[entity] = spawned[entity]
            if own and definition:
                spawn_counts[player][definition["id"]] += 1
                if definition["id"] == "cannon" and first_cannon[player] is None:
                    first_cannon[player] = event["time"]
        elif kind == "Death":
            alive.pop(entity, None)
        elif kind == "ProductionQueued":
            match = re.fullmatch(r"([a-z_]+) ×(\d+)", event["message"])
            paid = previous and previous["kind"] == "Payment" and previous["entity"] == entity and previous["player"] == player and previous.get("hasEconomy") and any(previous["economy"]["actualDelta"].get(resource, 0) < 0 for resource in ("food", "wood", "coin"))
            if match and paid:
                queues[entity][match.group(1)] += int(match.group(2))
            else:
                unpaid_queue_events.append({"time": event["time"], "producer": entity,
                                           "message": event["message"]})
        elif kind == "Experience" and event["message"] == "Unit trained":
            birth = spawned.get(previous["entity"]) if previous and previous["kind"] == "Spawn" else None
            valid = birth and birth["player"] == player and birth["definition"] and birth["definition"]["kind"] == 0 and math.isclose(birth["time"], event["time"], abs_tol=1e-4)
            definition_id = birth["definition"]["id"] if valid else None
            if valid and queues[entity][definition_id] > 0:
                queues[entity][definition_id] -= 1
                trained_counts[player][definition_id] += 1
            else:
                unmatched_training.append({"time": event["time"], "producer": entity,
                                           "precedingEvent": previous})
        if kind in ("Fire", "Damage") and own:
            shooter = spawned.get(entity)
            target = spawned.get(event["target"])
            if shooter and shooter["definition"] and shooter["definition"]["id"] == "cannon" and shooter["player"] == player and target and target["definition"] and target["definition"]["kind"] == 1 and target["player"] in (0, 1) and target["player"] != player:
                if kind == "Fire":
                    cannon_fires[player] += 1
                else:
                    cannon_damage[player].append({"time": event["time"], "source": entity,
                        "target": event["target"], "targetDefinition": target["definition"]["id"],
                        "amount": event["amount"]})
        previous = event
    ends = [event for event in events if event["kind"] == "MatchEnded"]
    winner = report["winner"]
    remaining = [sum(item["player"] == player and item["definition"] is not None and item["definition"]["kind"] != 2 for item in alive.values()) for player in (0, 1)]
    checks = {
        "nativePlayerPlatform": report.get("platform") == "OSXPlayer",
        "oneMatchStarted": kinds["MatchStarted"] == 1,
        "eventCountMatchesReport": len(events) == report["eventCount"],
        "allSpawnsRecognized": not unknown,
        "allTrainingQueuesHaveStructuredPayment": not unpaid_queue_events,
        "completeTrainingAttribution": not unmatched_training,
        "oneNaturalEnd": len(ends) == 1 and kinds["Resign"] == 0 and winner in (0, 1),
        "winnerMatchesEndEvent": len(ends) == 1 and ends[0]["player"] == winner,
        "endTimeMatchesReport": len(ends) == 1 and math.isclose(ends[0]["time"], report["time"], abs_tol=.15),
        "loserHasNoLivingUnitsOrBuildings": winner in (0, 1) and remaining[1-winner] == 0,
        "bothPlayersGathered": all(player_counts[player]["Gather"] > 0 for player in (0, 1)),
        "bothPlayersPaidAndCompletedTraining": all(player_counts[player]["Payment"] > 0 and sum(trained_counts[player].values()) > 0 for player in (0, 1)),
        "bothPlayersReceivedShipments": all(player_counts[player]["ShipmentArrived"] > 0 for player in (0, 1)),
        "atLeastOneCannonDamagedEnemyBuilding": any(cannon_damage),
    }
    players = []
    for player, state in enumerate(report["players"]):
        initial = civs[state["civilization"]].get("startingResources", {"food": 300, "wood": 300, "coin": 100})
        residual = {}
        for resource in ("food", "wood", "coin"):
            predicted = initial.get(resource, 0) + sum(state.get(income, {}).get(resource, 0) for income in ("gathered", "passiveIncome", "tradeIncome", "refunded")) - state["spent"].get(resource, 0)
            residual[resource] = state["resources"].get(resource, 0) - predicted
        players.append({"player": player, "civilization": state["civilization"],
            "age": state["age"], "ageHistory": state["ageHistory"],
            "spawnByDefinition": dict(sorted(spawn_counts[player].items())),
            "completedPaidTrainingByDefinition": dict(sorted(trained_counts[player].items())),
            "productionJobsQueued": player_counts[player]["ProductionQueued"],
            "payments": player_counts[player]["Payment"],
            "gatherEvents": player_counts[player]["Gather"],
            "shipmentsSent": player_counts[player]["ShipmentSent"],
            "shipmentsArrived": player_counts[player]["ShipmentArrived"],
            "firstCannonSpawnAt": first_cannon[player],
            "cannonFiresAtEnemyBuildings": cannon_fires[player],
            "cannonDamageEventsAgainstEnemyBuildings": len(cannon_damage[player]),
            "cannonDamageTargets": sorted({entry["target"] for entry in cannon_damage[player]}),
            "firstCannonBuildingDamage": cannon_damage[player][0] if cannon_damage[player] else None,
            "livingUnitsAndBuildingsAtEnd": remaining[player],
            "gathered": state["gathered"], "spent": state["spent"],
            "passiveIncome": state["passiveIncome"], "tradeIncome": state["tradeIncome"],
            "resources": state["resources"], "economyLedgerResidual": residual,
            "ledgerResidualScope": "Aggregate float accumulation diagnostic; no arbitrary pass tolerance imposed"})
    return {"matchId": report["matchId"], "buildGuid": report["buildGuid"],
        "appVersion": report["appVersion"], "unityVersion": report["version"],
        "platform": report["platform"], "reportReason": report["reason"],
        "autoplay": report["autoplay"], "simulationSpeed": report["simulationSpeed"],
        "difficulty": report["aiDifficulty"], "seed": report["seed"],
        "maxAge": report["maxAge"], "seconds": report["time"], "winner": winner,
        "eventCount": len(events), "eventCounts": dict(sorted(kinds.items())),
        "reportSource": source(report_path, archive_root),
        "eventsSource": source(event_path, archive_root), "checks": checks,
        "status": "passed" if all(checks.values()) else "failed",
        "naturalCompleteElimination": checks["oneNaturalEnd"] and checks["winnerMatchesEndEvent"] and checks["loserHasNoLivingUnitsOrBuildings"],
        "bothPlayersReachedAgeIII": all(state["age"] >= 3 for state in report["players"]),
        "players": players, "unknownSpawns": unknown,
        "unpaidTrainingQueueEvents": unpaid_queue_events,
        "unmatchedTrainingEvents": unmatched_training,
        "performanceScope": "Autoplay at the recorded speed; frame metrics are not normal-player performance acceptance",
        "frameMeanMs": report["meanFrameMs"], "frameP95Ms": report["p95FrameMs"]}


def main():
    repo = pathlib.Path(__file__).resolve().parents[3]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build-guid", required=True)
    parser.add_argument("--reports-root", type=pathlib.Path, default=pathlib.Path(__file__).resolve().parent)
    parser.add_argument("--rules", type=pathlib.Path, default=repo / "Assets/Resources/Rules.json")
    parser.add_argument("--output", type=pathlib.Path, default=repo / "Builds/Native054/recomputed-summary.json")
    args = parser.parse_args()
    guid = args.build_guid.lower()
    if not re.fullmatch(r"[0-9a-f]{32}", guid) or int(guid, 16) == 0:
        parser.error("--build-guid must be an explicit nonzero 32-digit hexadecimal GUID")
    rules = json.loads(args.rules.read_text())
    groups = collections.defaultdict(list)
    ignored = collections.Counter()
    parse_errors = []
    for path in sorted(args.reports_root.glob("*.json")):
        try:
            report = json.loads(path.read_text())
        except (OSError, ValueError) as error:
            parse_errors.append({"path": str(path.resolve()), "error": str(error)})
            continue
        if str(report.get("buildGuid", "")).lower() != guid:
            ignored["otherOrZeroGuid"] += 1
            continue
        if not report.get("matchId") or report.get("platform") != "OSXPlayer":
            ignored["missingMatchIdentityOrNotNativePlayer"] += 1
            continue
        if not report.get("autoplay"):
            ignored["notNormalAiAutoplay"] += 1
            continue
        groups[report["matchId"]].append((path, report))
    matches = []
    for match_id, copies in sorted(groups.items()):
        # Quit/menu exports of the same ended match are evidence copies, not new games.
        path, report = max(copies, key=lambda entry: (entry[1].get("reason") == "match_finished", entry[1].get("time", -1), entry[0].name))
        match = summarize(path, report, rules, args.output.parent)
        match["reportExportsForSameMatch"] = [{"reason": item.get("reason"),
            "seconds": item.get("time"), "source": source(file, args.output.parent)} for file, item in copies]
        matches.append(match)
    result = {"generatedAtUTC": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "scope": "Native client normal-rule AI autoplay; excludes zero-GUID editor/test fixtures. Does not certify human input, full five-era coverage, audio or normal-player performance.",
        "requiredBuildGuid": guid, "reportsRoot": str(args.reports_root.resolve()),
        "rulesSource": {"path": str(args.rules.resolve()), "sha256": digest(args.rules)},
        "uniqueNativeMatches": len(matches), "naturalEliminationMatches": sum(match.get("naturalCompleteElimination", False) for match in matches),
        "passedEventAudits": sum(match.get("status") == "passed" for match in matches),
        "ignoredReports": dict(ignored), "reportParseErrors": parse_errors,
        "matches": matches}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    temporary = args.output.with_suffix(".json.partial")
    temporary.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
    temporary.replace(args.output)
    print(json.dumps({"output": str(args.output.resolve()), "matches": result["uniqueNativeMatches"],
                      "naturalEliminations": result["naturalEliminationMatches"],
                      "passedEventAudits": result["passedEventAudits"]}))


if __name__ == "__main__":
    main()
