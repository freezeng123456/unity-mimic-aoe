#!/usr/bin/env python3
"""Audit NormalAiMatrix exports without running or modifying the game.

Headless evidence is never counted as native player or statistical balance
acceptance. False coverage flags and simulation timeouts remain visible.
"""
import argparse
import collections
import datetime
import gzip
import hashlib
import json
import math
import pathlib
import re
import sys

# Reuse the repository's exact source fingerprint format; do not invent a
# narrower tracked-files fingerprint for AI evidence.
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[3] / "Tools"))
from verification_evidence import fingerprint

RESOURCES = ("food", "wood", "coin")


def read_json(path):
    return json.loads(path.read_text())


def open_lines(path):
    return gzip.open(path, "rt", encoding="utf-8") if path.suffix == ".gz" else path.open()


def source(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return {"path": str(path.resolve()), "sha256": digest.hexdigest()}


def reference(line, event):
    return {"line": line, **{key: event[key] for key in
            ("time", "kind", "player", "entity", "target", "message", "amount")}}


def paid(event, player=None, producer=None):
    return bool(event and event["kind"] == "Payment" and
                (player is None or event["player"] == player) and
                (producer is None or event["entity"] == producer) and
                event.get("hasEconomy") and
                any(event["economy"]["actualDelta"].get(key, 0) < 0 for key in RESOURCES))


def scan_events(path, case, rules):
    """Reconstruct births, deaths and paid completions from event identities.

    No gameplay decisions, resource costs, timers or AI logic are reimplemented.
    Train XP ties a preceding unit Spawn to its paid producer queue. Shipments,
    age rewards, Manor gifts and initial units are kept separate from training.
    """
    definitions = {item["name"]: item for item in rules["entities"]}
    entities, alive, foundations = {}, {}, {}
    queues = collections.defaultdict(collections.Counter)
    age_queues = collections.Counter()
    ships_pending = [collections.Counter(), collections.Counter()]
    counts = collections.Counter()
    issues, ends, starts, openings = [], [], [], [[], []]
    previous, previous_ref = None, None
    players = [{"player": p, "civilization": case[f"civilization{p}"],
                "deckPreset": case[f"deckPreset{p}"], "deck": case[f"deck{p}"],
                "eventCounts": collections.Counter(), "spawned": collections.Counter(),
                "initialSpawned": collections.Counter(), "paidTraining": collections.Counter(),
                "paidMilitaryTraining": collections.Counter(), "paidCompletedBuildings": collections.Counter(),
                "gatheredFromEvents": collections.Counter(), "bankFromEvents": collections.Counter(),
                "tradeFromEvents": collections.Counter(), "paidActual": collections.Counter(),
                "refundActual": collections.Counter(), "ageHistory": [], "paidTrainingEvidence": [],
                "cannonBuildingDamage": [], "shipmentSent": [], "shipmentArrived": [],
                "treasureClaims": [], "tradeDeliveries": [], "tradeModes": []}
               for p in (0, 1)]
    last_time = -1
    event_count = 0
    with open_lines(path) as stream:
        for line, text in enumerate(stream, 1):
            if not text.strip():
                continue
            event = json.loads(text)
            event_count += 1
            kind, owner, entity = event["kind"], event["player"], event["entity"]
            ref = reference(line, event)
            counts[kind] += 1
            own = owner in (0, 1)
            if own:
                p = players[owner]
                p["eventCounts"][kind] += 1
            if event["time"] < last_time - .001:
                issues.append({"issue": "Event time moves backwards", "event": ref})
            last_time = max(last_time, event["time"])
            if kind == "StartingResources" and own:
                openings[owner].append(ref)
            if kind == "Spawn":
                definition = definitions.get(event["message"])
                if definition is None or entity in entities:
                    issues.append({"issue": "Unknown definition or reused entity ID", "event": ref})
                birth = {"owner": owner, "definition": definition, "event": ref}
                entities[entity] = birth
                alive[entity] = birth
                if own and definition:
                    p["spawned"][definition["id"]] += 1
                    if event["time"] == 0:
                        p["initialSpawned"][definition["id"]] += 1
                    if definition["kind"] == 1 and paid(previous, owner) and previous["message"].startswith("Foundation " + definition["name"] + ":"):
                        foundations[entity] = {"payment": previous_ref, "spawn": ref}
            elif kind == "Death":
                if entity not in alive:
                    issues.append({"issue": "Death has no living Spawn identity", "event": ref})
                alive.pop(entity, None)
                queues.pop(entity, None)
                for key in [key for key in age_queues if key[0] == entity]:
                    del age_queues[key]
            elif kind == "ProductionQueued":
                job = re.fullmatch(r"([a-z_]+) ×(\d+)", event["message"])
                if not job or not paid(previous, owner, entity):
                    issues.append({"issue": "Training queue lacks preceding structured payment", "event": ref})
                else:
                    queues[entity][job[1]] += int(job[2])
            elif kind == "Payment" and own:
                if not event.get("hasEconomy"):
                    issues.append({"issue": "Payment lacks structured economy", "event": ref})
                else:
                    transaction = event["economy"]
                    for key in RESOURCES:
                        delta = transaction["actualDelta"].get(key, 0)
                        p["paidActual"][key] -= delta
                        before = transaction["balanceBefore"].get(key, 0)
                        after = transaction["balanceAfter"].get(key, 0)
                        if not math.isfinite(after) or after < -.001 or delta > .001 or not math.isclose(after - before, delta, rel_tol=1e-6, abs_tol=.001):
                            issues.append({"issue": "Invalid structured payment balance", "resource": key, "event": ref})
                    age = re.match(r"Advance to age (\d+):", event["message"])
                    if age:
                        age_queues[(entity, int(age[1]))] += 1
            elif kind == "Refund" and own and event.get("hasEconomy"):
                for key in RESOURCES:
                    p["refundActual"][key] += event["economy"]["actualDelta"].get(key, 0)
                cancelled = re.match(r"Cancelled ([a-z_]+|\d+):", event["message"])
                if cancelled and cancelled[1].isdigit():
                    age_queues[(entity, int(cancelled[1]))] -= 1
                # Refunds omit training batch count. Do not guess an unlogged count.
            elif kind == "Experience" and event["message"] == "Unit trained":
                birth = entities.get(previous["entity"]) if previous and previous["kind"] == "Spawn" else None
                valid = birth and own and birth["owner"] == owner and birth["definition"] and birth["definition"]["kind"] == 0 and math.isclose(birth["event"]["time"], event["time"], abs_tol=.0001)
                definition = birth["definition"] if valid else None
                if not valid or queues[entity][definition["id"]] <= 0:
                    issues.append({"issue": "Training completion cannot be linked to paid producer queue", "event": ref, "preceding": previous_ref})
                else:
                    queues[entity][definition["id"]] -= 1
                    p["paidTraining"][definition["id"]] += 1
                    if definition["id"] not in ("villager", "explorer"):
                        p["paidMilitaryTraining"][definition["id"]] += 1
                    p["paidTrainingEvidence"].append({"definition": definition["id"], "spawn": birth["event"], "completion": ref})
            elif kind == "BuildingComplete" and own:
                birth = entities.get(entity)
                if entity not in foundations or not birth or not birth["definition"]:
                    issues.append({"issue": "Completed building lacks paid foundation", "event": ref})
                else:
                    p["paidCompletedBuildings"][birth["definition"]["id"]] += 1
            elif kind == "AgeAdvanced" and own:
                age = re.fullmatch(r"Reached age (\d+)", event["message"])
                if not age or age_queues[(entity, int(age[1]))] <= 0:
                    issues.append({"issue": "Age completion lacks paid advance queue", "event": ref})
                else:
                    age_queues[(entity, int(age[1]))] -= 1
                    p["ageHistory"].append({"age": int(age[1]), "event": ref})
            elif kind == "Gather" and own:
                worker, target = entities.get(entity), entities.get(event["target"])
                # Farms/estates are buildings with a resource kind; resource
                # identity is not equivalent to EntityKind.Resource.
                if not worker or not target or worker["owner"] != owner or not worker["definition"] or worker["definition"]["id"] != "villager" or not target["definition"] or not target["definition"].get("resource", 0) or event["amount"] <= 0:
                    issues.append({"issue": "Gather lacks real worker/resource identity or positive amount", "event": ref})
                p["gatheredFromEvents"][event["message"].lower()] += event["amount"]
            elif kind == "BankIncome" and own:
                p["bankFromEvents"][event["message"].lower()] += event["amount"]
            elif kind == "TradeDelivery" and own:
                p["tradeDeliveries"].append(ref)
                if event["message"] in ("Food", "Wood", "Coin"):
                    p["tradeFromEvents"][event["message"].lower()] += event["amount"]
            elif kind == "TradeMode" and own:
                p["tradeModes"].append(ref)
            elif kind == "TreasureClaimed" and own:
                p["treasureClaims"].append(ref)
            elif kind == "ShipmentSent" and own:
                allowed = any(card["id"] in p["deck"] and card["name"] == event["message"] for card in rules["cards"])
                if not allowed:
                    issues.append({"issue": "Shipment not in recorded legal preset", "event": ref})
                ships_pending[owner][event["message"]] += 1
                p["shipmentSent"].append(ref)
            elif kind == "ShipmentArrived" and own:
                name = event["message"].removesuffix(" arrived")
                if ships_pending[owner][name] <= 0:
                    issues.append({"issue": "Shipment arrival has no sent card", "event": ref})
                else:
                    ships_pending[owner][name] -= 1
                p["shipmentArrived"].append(ref)
            elif kind == "Damage" and own:
                shooter, target = entities.get(entity), entities.get(event["target"])
                if shooter and target and shooter["definition"] and target["definition"] and shooter["definition"]["id"] == "cannon" and target["definition"]["kind"] == 1 and target["owner"] in (0, 1) and target["owner"] != owner and event["amount"] > 0:
                    p["cannonBuildingDamage"].append({**ref, "targetDefinition": target["definition"]["id"]})
            elif kind == "MatchEnded":
                ends.append(ref)
            elif kind == "MatchStarted":
                starts.append(ref)
            previous, previous_ref = event, ref
    for p in players:
        p["age"] = max([1] + [item["age"] for item in p["ageHistory"]])
        p["livingUnitsAndBuildings"] = sum(bool(item["owner"] == p["player"] and item["definition"] and item["definition"]["kind"] != 2) for item in alive.values())
        p["pendingShipments"] = dict(ships_pending[p["player"]])
        expected = case[f"startingResources{p['player']}"]
        opening = openings[p["player"]]
        message = "Configured opening: {} F / {} W / {} C".format(*(round(expected.get(key, 0)) for key in RESOURCES))
        p["openingEvidence"] = opening
        p["normalOpening"] = len(opening) == 1 and opening[0]["time"] == 0 and opening[0]["message"] == message and p["initialSpawned"] == {"town": 1, "villager": 6, "explorer": 1, "crate_food": 1, "crate_wood": 1}
        p["coverageActual"] = {"gatheredRealResources": sum(p["gatheredFromEvents"].values()) > 0,
            "completedPaidWorkers": p["paidTraining"]["villager"] > 0,
            "completedPaidMilitary": sum(p["paidMilitaryTraining"].values()) > 0,
            "completedPaidBuildings": sum(p["paidCompletedBuildings"].values()) > 0,
            "ageII": p["age"] >= 2, "ageIII": p["age"] >= 3, "ageIV": p["age"] >= 4,
            "ageV": p["age"] >= 5, "shipmentArrived": bool(p["shipmentArrived"]),
            "completedPaidCannon": p["paidTraining"]["cannon"] > 0,
            "cannonDamagedEnemyBuilding": bool(p["cannonBuildingDamage"]),
            "treasureClaimed": bool(p["treasureClaims"]), "tradeDelivery": bool(p["tradeDeliveries"]),
            "resourceTradeDelivery": sum(p["tradeFromEvents"].values()) > 0}
        p["coverageGaps"] = [key for key, value in p["coverageActual"].items() if not value]
    return {"eventCount": event_count, "eventCounts": dict(sorted(counts.items())),
            "lastEventTime": last_time, "startEvents": starts, "endEvents": ends, "players": players, "eventIssues": issues}


def audit_case(root, case, rules, manifest):
    directory = root / case["id"]
    evidence = {"id": case["id"], "config": case, "scope": "Editor headless; not a native client match"}
    paths = {name: directory / name for name in ("case.json", "result.json", "events.jsonl", "snapshots.jsonl")}
    for name in ("events.jsonl", "snapshots.jsonl"):
        if not paths[name].is_file() and paths[name].with_suffix(".jsonl.gz").is_file():
            paths[name] = paths[name].with_suffix(".jsonl.gz")
    missing = [name for name, path in paths.items() if not path.is_file()]
    evidence["sources"] = {name: source(path) for name, path in paths.items() if path.is_file()}
    if missing:
        return {**evidence, "outcome": "incomplete", "missingFiles": missing,
                "checks": {"allExportsPresent": False}, "coverageActual": {}, "coverageGaps": ["No complete event export; retain run status/error in config"]}
    result = read_json(paths["result.json"])
    scan = scan_events(paths["events.jsonl"], case, rules)
    with open_lines(paths["snapshots.jsonl"]) as stream:
        snapshots = [json.loads(line) for line in stream if line.strip()]
    final = {p: next((snapshot for snapshot in reversed(snapshots) if snapshot["player"] == p), None) for p in (0, 1)}
    winner, ends = result["winner"], scan["endEvents"]
    civs = {civ["id"]: civ for civ in rules["civs"]}
    cards = {card["id"]: card for card in rules["cards"]}
    natural = len(ends) == 1 and winner in (0, 1) and scan["eventCounts"].get("Resign", 0) == 0
    eliminated = winner in (0, 1) and scan["players"][1-winner]["livingUnitsAndBuildings"] == 0
    checks = {"allExportsPresent": True, "wrapperExported": case["status"] == "exported",
        "caseExportMatchesManifest": read_json(paths["case.json"]) == case,
        "resultUsesSpecifiedCivilizations": all(result[f"civilization{p}"] == case[f"civilization{p}"] for p in (0, 1)),
        "resultUsesSpecifiedDifficulty": result["aiDifficulty"] == case["difficulty"],
        "resultUsesSpecifiedSeed": result["seed"] == manifest["seed"],
        "resultDeclaresHeadlessScope": "Headless" in result.get("scope", ""),
        "eventCountMatchesResult": scan["eventCount"] == result["events"],
        "oneNormalMatchStarted": scan["eventCounts"].get("MatchStarted") == 1,
        "matchStartedUsesSpecifiedSeedAgeAndCivilizations": len(scan["startEvents"]) == 1 and scan["startEvents"][0]["message"].startswith(f"Seed {manifest['seed']}; age cap {manifest['maxAge']}; {case['civilization0']} vs {case['civilization1']};"),
        "caseUsesCatalogStartingResources": all(case[f"startingResources{p}"] == civs[case[f"civilization{p}"]].get("startingResources", {"food": 300, "wood": 300, "coin": 100}) for p in (0, 1)),
        "recordedDecksLegal": all(0 < len(case[f"deck{p}"]) <= rules["deckCapacity"] and len(set(case[f"deck{p}"])) == len(case[f"deck{p}"]) and all(item in cards and cards[item].get("civilization", "") in ("", case[f"civilization{p}"]) for item in case[f"deck{p}"]) for p in (0, 1)),
        "normalOpeningBothPlayers": all(p["normalOpening"] for p in scan["players"]),
        "noEventAttributionOrEconomyErrors": not scan["eventIssues"],
        "agesMatchResult": all(result[f"age{p}"] == scan["players"][p]["age"] for p in (0, 1)),
        "finalSnapshotsPresent": all(final.values()),
        "boundedSimulation": 0 <= result["seconds"] <= manifest["simulationLimitSeconds"] + .15,
        "naturalEndWinnerMatches": not ends or len(ends) == 1 and ends[0]["player"] == winner,
        "naturalEndTimeMatches": not ends or len(ends) == 1 and math.isclose(ends[0]["time"], result["seconds"], abs_tol=.15),
        "noResign": scan["eventCounts"].get("Resign", 0) == 0,
        "naturalEndHasEliminatedLoser": not natural or eliminated}
    for p in scan["players"]:
        snap = final[p["player"]]
        p["finalSnapshot"] = snap
        if snap:
            expected = case[f"startingResources{p['player']}"]
            p["ledgerResidualDiagnostic"] = {key: snap["resources"].get(key, 0) -
                (expected.get(key, 0) + sum(p[income].get(key, 0) for income in ("gatheredFromEvents", "bankFromEvents", "tradeFromEvents", "refundActual")) - p["paidActual"].get(key, 0)) for key in RESOURCES}
            p["ledgerResidualScope"] = "Diagnostic only: independent event sums vs accumulated float snapshot, no arbitrary pass tolerance"
    timed_out = winner == -1 and not ends and result["seconds"] >= manifest["simulationLimitSeconds"]
    outcome = "natural_end" if natural and eliminated else "timeout" if timed_out else "early_stop"
    if not all(checks.values()):
        outcome = "invalid_evidence"
    coverage = {"naturalFullElimination": natural and eliminated,
        "bothGathered": all(p["coverageActual"]["gatheredRealResources"] for p in scan["players"]),
        "bothPaidWorkersAndMilitary": all(p["coverageActual"]["completedPaidWorkers"] and p["coverageActual"]["completedPaidMilitary"] for p in scan["players"]),
        "bothAgeIII": all(p["age"] >= 3 for p in scan["players"]),
        "bothAgeV": all(p["age"] >= 5 for p in scan["players"]),
        "bothReceivedShipments": all(p["coverageActual"]["shipmentArrived"] for p in scan["players"]),
        "atLeastOnePaidCannon": any(p["coverageActual"]["completedPaidCannon"] for p in scan["players"]),
        "atLeastOneCannonDamagedEnemyBuilding": any(p["coverageActual"]["cannonDamagedEnemyBuilding"] for p in scan["players"])}
    return {**evidence, "outcome": outcome, "result": result, "checks": checks, **scan,
            "coverageActual": coverage, "coverageGaps": [key for key, value in coverage.items() if not value]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("matrix_root", type=pathlib.Path)
    parser.add_argument("--output", type=pathlib.Path)
    args = parser.parse_args()
    root = args.matrix_root.resolve()
    manifest_path = root / "matrix.json"
    manifest = read_json(manifest_path)
    catalog_path = root / "catalog-loaded.json"
    rules = read_json(catalog_path)
    matches = []
    for case in manifest["cases"]:
        try:
            matches.append(audit_case(root, case, rules, manifest))
        except (OSError, ValueError, KeyError, TypeError, StopIteration) as error:
            matches.append({"id": case["id"], "config": case, "outcome": "audit_error", "error": str(error), "checks": {"auditCompleted": False}, "coverageActual": {}, "coverageGaps": ["Audit error; no acceptance conclusion"]})
    expected = {f"{difficulty}-{pairing}" for difficulty in ("easy", "standard", "hard") for pairing in ("cross-forward", "cross-reverse", "coast-mirror", "river-mirror")}
    pairing_plan = {"cross-forward": ("coast", "economy", "river", "military"),
                    "cross-reverse": ("river", "military", "coast", "economy"),
                    "coast-mirror": ("coast", "economy", "coast", "economy"),
                    "river-mirror": ("river", "military", "river", "military")}
    by_id = {case["id"]: case for case in manifest["cases"]}
    swap_checks = {}
    for difficulty in ("easy", "standard", "hard"):
        a, b = by_id.get(difficulty + "-cross-forward"), by_id.get(difficulty + "-cross-reverse")
        swap_checks[difficulty + "CivilizationAndDeckTravelTogether"] = bool(a and b and all(a[f"{key}0"] == b[f"{key}1"] and a[f"{key}1"] == b[f"{key}0"] for key in ("civilization", "deckPreset", "deck", "startingResources")))
        for pairing in ("coast-mirror", "river-mirror"):
            c = by_id.get(difficulty + "-" + pairing)
            swap_checks[difficulty + "-" + pairing + "IdenticalCards"] = bool(c and c["civilization0"] == c["civilization1"] and c["deck0"] == c["deck1"])
    coverage = {}
    for civ in ("coast", "river"):
        for difficulty in ("easy", "standard", "hard"):
            observed = [p for match in matches if match.get("outcome") not in ("audit_error", "invalid_evidence", "incomplete") and match["config"]["difficulty"] == difficulty for p in match.get("players", []) if p["civilization"] == civ]
            coverage[f"{civ}/{difficulty}"] = {key: any(p["coverageActual"][key] for p in observed) for key in
                ("gatheredRealResources", "completedPaidWorkers", "completedPaidMilitary", "completedPaidBuildings", "ageIII", "ageIV", "ageV", "shipmentArrived", "completedPaidCannon", "cannonDamagedEnemyBuilding", "treasureClaimed", "tradeDelivery", "resourceTradeDelivery")}
    receipt = pathlib.Path(manifest["testEvidence"]["path"])
    if not receipt.is_file():
        receipt = root.parent / "Tests" / "source-evidence.json"
    checks = {"fullTwelveCasesDeclared": set(by_id) == expected and len(manifest["cases"]) == 12,
        "pairingsUsePlannedCivilizationsAndPresets": all(tuple(case[key] for key in ("civilization0", "deckPreset0", "civilization1", "deckPreset1")) == pairing_plan[case["pairing"]] for case in manifest["cases"]),
        "normalAgeVFixedMapBound": manifest["seed"] == 314159 and manifest["maxAge"] == 5 and manifest["simulationLimitSeconds"] == 2400,
        "loadedCatalogHashMatches": source(catalog_path)["sha256"] == manifest["loadedCatalog"]["sha256"],
        "sourcesStableDuringRecordedRun": manifest.get("sourcesStable", False),
        "sourceFingerprintsUseFullCoverage": all(isinstance(manifest.get(f"sources{label}"), list) and fingerprint({item["relativePath"]: item["sha256"] for item in manifest[f"sources{label}"]}) == manifest.get(f"sourceFingerprint{label}") for label in ("Before", "After")),
        "assembliesCurrentAtStart": all(manifest[key].get("currentByPipelineAndSourceTime", False) and manifest[key].get("loadedModuleVersionId") and len(manifest[key]["file"].get("sha256", "")) == 64 for key in ("runtimeAssembly", "editorAssembly")),
        "testReceiptIdentityMatches": receipt.is_file() and source(receipt)["sha256"] == manifest["testEvidence"]["sha256"] and read_json(receipt)["sourceFingerprint"] == manifest["sourceFingerprintBefore"],
        "allSwapAndMirrorChecks": all(swap_checks.values()),
        "allEventAuditsValid": all(all(match["checks"].values()) for match in matches),
        "allTwelveNaturalEnds": len(matches) == 12 and all(match["outcome"] == "natural_end" for match in matches)}
    outcome_counts = collections.Counter(match["outcome"] for match in matches)
    missing_coverage = {key: [item for item, actual in values.items() if not actual] for key, values in coverage.items()}
    summary = {"generatedAtUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "scope": "Bounded normal-rule headless AI regression; not native player acceptance, varied maps, or statistical balance evidence",
        "pilot": manifest.get("pilot", False),
        "interpretation": "Early natural wins may leave age/artillery coverage false without demonstrating a gameplay defect. Timeouts remain incomplete; slot wins do not establish balance.",
        "matrixSource": source(manifest_path), "catalogSource": source(catalog_path),
        "auditorSource": source(pathlib.Path(__file__)),
        "testEvidenceSource": manifest["testEvidence"],
        "checks": checks, "swapAndMirrorChecks": swap_checks, "outcomes": dict(outcome_counts),
        "wallSecondsRecorded": sum(case.get("wallSeconds", 0) for case in manifest["cases"]),
        "civilizationDifficultyCoverageActual": coverage, "coverageGaps": missing_coverage,
        "status": "complete_with_coverage_gaps" if all(checks.values()) and any(missing_coverage.values()) else "complete" if all(checks.values()) else "incomplete_or_failed",
        "matches": matches}
    output = args.output or root / "summary.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps({"output": str(output.resolve()), "status": summary["status"], "outcomes": summary["outcomes"], "checks": checks}, ensure_ascii=False))
    return 0 if all(checks.values()) else 1


if __name__ == "__main__":
    raise SystemExit(main())
