#!/usr/bin/env python3
"""Summarize one explicitly identified native player match without grading gaps green."""
import argparse
import collections
import datetime
import json
import math
import pathlib
import re
import sys

try:
    import summarize_native as native
except ModuleNotFoundError:
    # A sealed Player/ copy shares the native helper from its parent evidence directory.
    sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent))
    import summarize_native as native


def witness(event, line, **extra):
    return {"eventLine": line, "kind": event["kind"], "seconds": event["time"],
            "player": event["player"], "entity": event["entity"],
            "target": event["target"], "message": event["message"],
            "amount": event["amount"], **extra}


def item(evidence, count=None):
    return {"actual": bool(evidence), "count": len(evidence) if count is None else count,
            "firstEvidence": evidence[:3]}


def summarize(path, report, rules, archive_root, exports):
    event_path = path.with_name(path.stem + "-events.jsonl")
    events = [json.loads(line) for line in event_path.read_text().splitlines() if line.strip()]
    core = native.summarize(path, report, rules, archive_root)
    names = {definition["name"]: definition for definition in rules["entities"]}
    spawned = {}
    for line, event in enumerate(events, 1):
        if event["kind"] == "Spawn":
            spawned[event["entity"]] = {"event": event, "line": line,
                                         "definition": names.get(event["message"])}
    starts = [witness(event, line) for line, event in enumerate(events, 1) if event["kind"] == "MatchStarted"]
    resources = [witness(event, line) for line, event in enumerate(events, 1) if event["kind"] == "StartingResources"]
    opening = {}
    opening_deaths = []
    for player in (0, 1):
        initial = collections.Counter(birth["definition"]["id"] for birth in spawned.values()
            if birth["definition"] and birth["event"]["player"] == player and birth["event"]["time"] == 0 and birth["definition"]["kind"] != 2)
        opening[str(player)] = dict(initial)
    for line, event in enumerate(events, 1):
        if event["kind"] == "Death" and event["time"] == 0:
            birth = spawned.get(event["entity"])
            if birth and birth["definition"] and birth["event"]["player"] in (0, 1) and birth["definition"]["kind"] != 2:
                opening_deaths.append(witness(event, line))
    configured_resources = True
    for player, state in enumerate(report["players"]):
        civilization = next(civ for civ in rules["civs"] if civ["id"] == state["civilization"])
        cost = civilization.get("startingResources", {"food": 300, "wood": 300, "coin": 100})
        text = "Configured opening: " + " / ".join(f"{cost.get(resource, 0):.0f} {suffix}" for resource, suffix in (("food", "F"), ("wood", "W"), ("coin", "C")))
        configured_resources = configured_resources and any(entry["player"] == player and entry["seconds"] == 0 and entry["message"] == text for entry in resources)
    normal_opening = len(starts) == 1 and len(resources) == 2 and configured_resources and not opening_deaths and all(opening[str(player)] == {"town": 1, "villager": 6, "explorer": 1} for player in (0, 1))
    samples = collections.defaultdict(list)
    paid_foundations = {}
    finished_buildings = []
    training = []
    for line, event in enumerate(events, 1):
        if event["player"] != 0:
            continue
        kind = event["kind"]
        source = spawned.get(event["entity"])
        target = spawned.get(event["target"])
        source_def = source["definition"] if source else None
        target_def = target["definition"] if target else None
        if kind == "Gather" and event["amount"] > 0 and source_def and source_def.get("tags", 0) & 128:
            samples["gathering"].append(witness(event, line, resource=event["message"]))
        if kind == "Spawn" and source_def and source_def["kind"] == 1 and line > 1:
            previous = events[line - 2]
            if previous["kind"] == "Payment" and previous["player"] == 0 and previous.get("hasEconomy") and previous["message"].startswith("Foundation "):
                proof = witness(event, line, definition=source_def["id"], paymentEventLine=line-1,
                                paymentActualDelta=previous["economy"]["actualDelta"])
                paid_foundations[event["entity"]] = proof
                samples["paidConstructionStarted"].append(proof)
        if kind == "BuildingComplete" and event["entity"] in paid_foundations:
            proof = witness(event, line, definition=source_def["id"],
                            foundationSpawnEventLine=paid_foundations[event["entity"]]["eventLine"],
                            paymentEventLine=paid_foundations[event["entity"]]["paymentEventLine"])
            finished_buildings.append(proof)
            samples["paidConstructionCompleted"].append(proof)
            if source_def["id"] == "house":
                samples["housingCompleted"].append(proof)
            if source_def["id"] == "tower":
                samples["watchtowerCompleted"].append(proof)
        if kind == "Payment" and event.get("hasEconomy") and event.get("economy", {}).get("hasRepair"):
            transaction = event["economy"]
            if transaction["hpAfter"] > transaction["hpBefore"] and any(transaction["actualDelta"].get(resource, 0) < 0 for resource in ("food", "wood", "coin")):
                samples["paidRepairHealedBuilding"].append(witness(event, line,
                    healthTarget=transaction["healthTarget"], hpBefore=transaction["hpBefore"],
                    hpAfter=transaction["hpAfter"], actualDelta=transaction["actualDelta"],
                    requestedDelta=transaction["requestedDelta"]))
        if kind == "Garrison" and source_def and source_def["kind"] == 0 and target_def and target_def["kind"] == 1 and target["event"]["player"] == 0:
            samples["garrisonEntered"].append(witness(event, line, targetDefinition=target_def["id"]))
        if kind == "Ungarrison":
            samples["garrisonReleased"].append(witness(event, line))
        if source_def and source_def["id"] == "town" and kind in ("AttackPrepared", "Fire", "Damage", "Impact"):
            samples["town" + kind].append(witness(event, line,
                targetDefinition=target_def["id"] if target_def else None))
        if kind == "Experience" and event["message"] == "Unit trained" and line > 1:
            birth_event = events[line-2]
            birth = spawned.get(birth_event["entity"]) if birth_event["kind"] == "Spawn" else None
            if birth and birth["definition"] and birth_event["player"] == 0 and math.isclose(birth_event["time"], event["time"], abs_tol=1e-4):
                proof = witness(event, line, unit=birth["definition"]["id"],
                                bornEntity=birth_event["entity"], spawnEventLine=line-1,
                                producerDefinition=source_def["id"] if source_def else None)
                training.append(proof)
                samples["paidTrainingCompleted"].append(proof)
                if birth["definition"].get("tags", 0) & 128:
                    samples["settlerTrainingCompleted"].append(proof)
                else:
                    samples["militaryTrainingCompleted"].append(proof)
        if kind == "AgeAdvanced":
            if event["message"] == "Reached age 2":
                samples["ageIICompleted"].append(witness(event, line))
            if event["message"] == "Reached age 3":
                samples["ageIIICompleted"].append(witness(event, line))
        if kind == "ShipmentSent":
            samples["shipmentSent"].append(witness(event, line))
        if kind == "ShipmentArrived":
            samples["shipmentArrived"].append(witness(event, line))
        if kind == "Spawn" and source_def and source_def["kind"] == 0 and not source_def.get("tags", 0) & (128 | 512 | 1024) and event["time"] > 0:
            proof = witness(event, line, definition=source_def["id"])
            samples["militaryBorn"].append(proof)
            if source_def["id"] == "cannon":
                samples["cannonBorn"].append(proof)
        enemy_building = target_def and target_def["kind"] == 1 and target["event"]["player"] == 1
        military = source_def and source_def["kind"] == 0 and not source_def.get("tags", 0) & (128 | 512 | 1024)
        if kind == "Damage" and event["amount"] > 0 and enemy_building and military:
            proof = witness(event, line, sourceDefinition=source_def["id"], targetDefinition=target_def["id"])
            samples["militaryDamagedEnemyBuilding"].append(proof)
            if source_def["id"] == "cannon":
                samples["cannonDamagedEnemyBuilding"].append(proof)
        if kind == "Command" and enemy_building and military and event["message"].startswith("Attack ×"):
            samples["explicitMilitaryAttackOnEnemyBuilding"].append(witness(event, line,
                sourceDefinition=source_def["id"], targetDefinition=target_def["id"]))
        if kind == "PauseChanged":
            samples["pauseTransitions"].append(witness(event, line))
    acceptance_names = ["gathering", "paidTrainingCompleted", "settlerTrainingCompleted",
        "paidConstructionStarted", "paidConstructionCompleted", "housingCompleted",
        "watchtowerCompleted", "paidRepairHealedBuilding", "garrisonEntered", "garrisonReleased",
        "townAttackPrepared", "townFire", "townDamage", "townImpact",
        "ageIICompleted", "ageIIICompleted", "shipmentSent", "shipmentArrived",
        "militaryBorn", "militaryTrainingCompleted", "cannonBorn",
        "explicitMilitaryAttackOnEnemyBuilding", "militaryDamagedEnemyBuilding",
        "cannonDamagedEnemyBuilding"]
    acceptance = {name: item(samples[name]) for name in acceptance_names}
    ends = [witness(event, line) for line, event in enumerate(events, 1) if event["kind"] == "MatchEnded"]
    resigns = [witness(event, line) for line, event in enumerate(events, 1) if event["kind"] == "Resign"]
    natural = core.get("naturalCompleteElimination", False)
    acceptance["naturalCompleteElimination"] = {"actual": natural, "count": len(ends), "evidence": ends, "resignEvents": resigns}
    acceptance["playerVictory"] = {"actual": natural and report["winner"] == 0, "winner": report["winner"]}
    pauses = samples["pauseTransitions"]
    resumes = [entry for entry in pauses if entry["amount"] == 0]
    pause_seen = [entry for entry in pauses if entry["amount"] == 1]
    acceptance["pauseAndResumeObserved"] = {"actual": any(resume["eventLine"] > pause["eventLine"] for pause in pause_seen for resume in resumes), "transitions": pauses}
    menu_exports = [entry for entry in exports if entry["reason"] == "menu_exit"]
    acceptance["returnToMenuReportExport"] = {"actual": bool(menu_exports), "exports": menu_exports,
        "scope": "Report export only; rendered menu and restart need independent UI/new-match evidence"}
    return {"generatedAtUTC": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "scope": "Single explicitly identified native normal-speed player match. Each acceptance item reports actual evidence separately; no all-green game-completion verdict. Events cannot alone certify physical UI gestures, listening, restart or every possible off-ledger intervention.",
        "identity": {key: report.get(key) for key in ("matchId", "buildGuid", "appVersion", "version", "platform", "autoplay", "simulationSpeed", "aiDifficulty", "maxAge", "seed", "reason", "time", "winner")},
        "expectedScenario": {"civilizations": ["coast", "coast"], "maxAge": 3, "difficulty": "easy"},
        "configurationMatchesRequestedScenario": report.get("maxAge") == 3 and report.get("aiDifficulty") == "easy" and [state["civilization"] for state in report["players"]] == ["coast", "coast"],
        "reportSource": native.source(path, archive_root), "eventsSource": native.source(event_path, archive_root),
        "reportExportsForSameMatch": exports,
        "normalOpeningObserved": normal_opening,
        "benchmarkOrNonstandardOpeningSuspected": not normal_opening,
        "openingEvidence": {"matchStarted": starts, "startingResources": resources,
            "configuredResourcesMatchRules": configured_resources, "ownedUnitsAndBuildingsAtZeroTime": opening,
            "zeroTimeOwnedDeaths": opening_deaths},
        "eventCount": len(events),
        "evidenceIntegrity": {key: value for key, value in core.get("checks", {}).items() if key in ("nativePlayerPlatform", "oneMatchStarted", "eventCountMatchesReport", "allSpawnsRecognized", "allTrainingQueuesHaveStructuredPayment", "completeTrainingAttribution")},
        "acceptance": acceptance,
        "paidTrainingCompletions": training, "paidBuildingsCompleted": finished_buildings,
        "repairStatistics": {"paidHealingTransactions": len(samples["paidRepairHealedBuilding"]),
            "healedHpSum": sum(entry["hpAfter"]-entry["hpBefore"] for entry in samples["paidRepairHealedBuilding"]),
            "actualResourcesSpent": {resource: -sum(entry["actualDelta"].get(resource, 0) for entry in samples["paidRepairHealedBuilding"]) for resource in ("food", "wood", "coin")},
            "scope": "Sum of individual structured healing/payment transactions; not net building health change during incoming damage"},
        "playerStatistics": core.get("players", []),
        "trainingAttributionScope": "Spawn followed by same-step Unit trained identifies actual completion. The shared native audit separately checks paid production association; individual queue job IDs are not emitted.",
        "missingEvidenceItems": [name for name, value in acceptance.items() if not value["actual"]]}


def main():
    script_path = pathlib.Path(__file__).resolve()
    repo = next((parent for parent in script_path.parents if (parent / "Assets/Resources/Rules.json").is_file()), script_path.parent / "project")
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--match-id", required=True, help="Explicit known player match; never infer from a 25-second benchmark report")
    parser.add_argument("--build-guid", required=True)
    parser.add_argument("--app-version", default="0.2.30")
    parser.add_argument("--reports-root", type=pathlib.Path, default=pathlib.Path.home() / "Library/Application Support/Frontier Workshop/Frontier Reckoning/Reports")
    parser.add_argument("--rules", type=pathlib.Path, default=repo / "Assets/Resources/Rules.json")
    parser.add_argument("--output", type=pathlib.Path, help="Omit to print the JSON without creating evidence files")
    args = parser.parse_args()
    for label, value in (("build-guid", args.build_guid), ("match-id", args.match_id)):
        if not re.fullmatch(r"[0-9a-fA-F]{32}", value) or int(value, 16) == 0:
            parser.error(f"--{label} must be an explicit nonzero 32-digit hexadecimal ID")
    copies = []
    for path in sorted(args.reports_root.glob("*.json")):
        try:
            report = json.loads(path.read_text())
        except (OSError, ValueError):
            continue
        if str(report.get("matchId", "")).lower() != args.match_id.lower():
            continue
        if str(report.get("buildGuid", "")).lower() != args.build_guid.lower() or report.get("appVersion") != args.app_version or report.get("platform") != "OSXPlayer" or report.get("autoplay") is not False or not math.isclose(report.get("simulationSpeed", -1), 1, abs_tol=1e-6):
            continue
        copies.append((path, report))
    if not copies:
        parser.error("No report matches the explicit native GUID, version, match ID, autoplay=false and speed=1 filters; no conclusion generated")
    path, report = max(copies, key=lambda entry: (entry[1].get("reason") == "match_finished", entry[1].get("time", -1), entry[0].name))
    event_path = path.with_name(path.stem + "-events.jsonl")
    if not event_path.is_file():
        parser.error("The selected report has no corresponding events file; no conclusion generated")
    archive_root = args.output.parent if args.output else repo / "Builds/Native054"
    exports = [{"reason": entry.get("reason"), "seconds": entry.get("time"), "source": native.source(file, archive_root)} for file, entry in copies]
    result = summarize(path, report, json.loads(args.rules.read_text()), archive_root, exports)
    if not result["normalOpeningObserved"]:
        parser.error("The explicit match has a benchmark or nonstandard opening (configured starts, six settlers, explorer, town, or zero-time destruction differ); excluded from normal player evidence")
    result["rulesSource"] = {"path": str(args.rules.resolve()), "sha256": native.digest(args.rules)}
    text = json.dumps(result, ensure_ascii=False, indent=2) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        temporary = args.output.with_suffix(args.output.suffix + ".partial")
        temporary.write_text(text)
        temporary.replace(args.output)
        print(json.dumps({"output": str(args.output.resolve()), "matchId": result["identity"]["matchId"],
            "normalOpeningObserved": result["normalOpeningObserved"], "missingEvidenceItems": result["missingEvidenceItems"]}))
    else:
        print(text, end="")


if __name__ == "__main__":
    main()
