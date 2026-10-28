#!/usr/bin/env python3
"""Audit an explicitly paired native player report; no game or GUI mutation.

Deck selection is an external UI declaration paired with a menu PNG. The game
does not log DeckConfigured; event evidence cannot certify gestures or pixels.
"""
import argparse
import collections
import datetime
import json
import math
from pathlib import Path
import re
import shutil
import struct

from audit_ai_matrix import scan_events, reference, source
from verify_performance import strict_json

RESOURCES = ("food", "wood", "coin")
BASE_REQUIREMENTS = ("ageII", "ageIII", "paidWorkers", "paidMilitary", "paidCannon",
                     "cannonEnemyBuildingDamage", "gathered", "shipmentArrived")
RIVER_REQUIREMENTS = ("ageIV", "ageV", "paidBank", "bankIncome", "specialUnit",
                      "paidTechnology", "treasure", "paidTradePost", "tradeXP", "resourceMode", "resourceTradeDelivery")


def identity(value, name):
    if not isinstance(value, str) or not re.fullmatch(r"[0-9a-fA-F]{32}", value) or int(value, 16) == 0:
        raise ValueError(name + " must be an explicit nonzero 32-digit hexadecimal ID")
    return value.lower()


def finite(value):
    return type(value) in (int, float) and math.isfinite(value)


def preset(rules, civilization, choice):
    # Exact ordering used by DeckLibrary.Preset; this is not a logged deck.
    cards = [c for c in rules["cards"] if c.get("civilization", "") in ("", civilization)]
    def key(card):
        military = bool(card.get("unit")) and card["unit"] != "villager"
        preferred = military if choice == "military" else not military
        return (0 if preferred else 1, card["age"], card["id"])
    return [c["id"] for c in sorted(cards, key=key)[:rules["deckCapacity"]]]


def entity_cost(definition, civilization, civs):
    civ = civs[civilization]
    if definition["id"] == "villager" and civ.get("coinVillager"):
        return {"coin": 100}
    return civ["houseCost"] if definition["id"] == "house" else definition.get("cost", {})


def menu_pair(path, guid, version):
    if path is None:
        return {"passed": False, "scope": "No menu capture paired; deck remains an unverified UI declaration"}
    data = strict_json(path.read_text())
    png_name = data.get("pngFile")
    if not isinstance(png_name, str) or Path(png_name).name != png_name or png_name != path.stem + ".png":
        raise ValueError("Menu PNG must use its receipt basename")
    png = path.parent / png_name
    if png.is_symlink() or not png.is_file():
        raise ValueError("Paired menu PNG is missing or a symlink")
    header = png.read_bytes()[:24]
    dimensions = struct.unpack(">II", header[16:24]) if len(header) == 24 and header[:16] == b"\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR" else None
    checks = {"nativeBuildVersionMatches": str(data.get("buildGuid", "")).lower() == guid and data.get("appVersion") == version,
              "menuHasNoMatch": data.get("inMenu") is True and data.get("hasPlayer") is False and data.get("matchId") == "" and data.get("requestMatchId") == "",
              "pngHashMatches": source(png)["sha256"] == data.get("pngSha256"),
              "pngDimensionsMatch": all(type(data.get(k)) is int for k in ("width", "height")) and dimensions == (data.get("width"), data.get("height")) and dimensions is not None and min(dimensions) > 0}
    return {"passed": all(checks.values()), "checks": checks, "sources": {"receipt": source(path), "png": source(png)},
            "scope": "Paired menu PNG identity and bytes only; its civilization, preset, difficulty and gesture content must be read in the separate GUI record. No deck fields exist in the capture metadata."}


def payment_and_completions(rows, rules, civilizations):
    """Validate recorded payments and minimum paid production times, not gameplay.

    Every qualified training birth retains payment/queue/spawn/completion lines.
    No rules are executed and no resources are added. Cancellation identity is
    not logged: equal-cost duplicate cancelled jobs are conservatively removed
    from confident attribution and explicitly reported.
    """
    by_name = {d["name"]: d for d in rules["entities"]}
    by_id = {d["id"]: d for d in rules["entities"]}
    techs = {t["name"]: t for t in rules["techs"]}
    ages = {a["age"]: a for a in rules["ages"]}
    civs = {c["id"]: c for c in rules["civs"]}
    births, living, good_payments = {}, set(), {}
    training = collections.defaultdict(list); advancing = collections.defaultdict(list); researching = collections.defaultdict(list)
    completed_buildings, applied = {}, [set(), set()]
    evidence = [{"training": [], "ages": [], "technologies": [], "buildings": [], "bankIncome": [],
                 "treasure": [], "tradeXP": [], "resourceModes": [], "resourceDeliveries": []} for _ in (0, 1)]
    issues, ambiguities = [], []
    previous = None; previous_line = None; trade_mode = {}; xp_deliveries = {}
    by_line = dict(rows)
    cards = {c["name"]: c for c in rules["cards"]}; shipping = collections.defaultdict(list)
    for line, event in rows:
        kind, owner, entity = event["kind"], event["player"], event["entity"]
        ref = reference(line, event)
        own = owner in (0, 1)
        if kind == "Payment":
            expected, label = None, event["message"].split(":", 1)[0]
            age = re.fullmatch(r"Advance to age (\d+)", label)
            unit = re.fullmatch(r"(.+) ×(\d+)", label)
            foundation = label.removeprefix("Foundation ") if label.startswith("Foundation ") else None
            definition = by_name.get(foundation or (unit[1] if unit else ""))
            if definition and own:
                base = entity_cost(definition, civilizations[owner], civs); count = int(unit[2]) if unit else 1
                expected = {r: -base.get(r, 0) * count for r in RESOURCES}
            elif age and int(age[1]) in ages:
                expected = {r: -ages[int(age[1])]["cost"].get(r, 0) for r in RESOURCES}
            elif label in techs:
                expected = {r: -techs[label]["cost"].get(r, 0) for r in RESOURCES}
            transaction = event.get("economy", {})
            structured = event.get("hasEconomy") is True and own and entity in living and births[entity][0] == owner
            producer = births.get(entity)
            if age: structured = structured and producer is not None and producer[1]["id"] == "town"
            elif label in techs: structured = structured and producer is not None and producer[1]["id"] == techs[label]["building"]
            elif foundation: structured = structured and producer is not None and producer[1]["id"] == "villager"
            for r in RESOURCES:
                numbers = [transaction.get(k, {}).get(r) for k in ("requestedDelta", "actualDelta", "balanceBefore", "balanceAfter")]
                if not all(finite(n) for n in numbers): structured = False; continue
                requested, actual, before, after = numbers
                # Actual float subtraction is allowed a few ulps of its balance.
                tolerance = max(.002, abs(before) * 3e-7)
                structured = structured and requested <= 0 and actual <= .001 and before >= -.001 and after >= -.001
                structured = structured and math.isclose(after - before, actual, abs_tol=.002, rel_tol=1e-6) and abs(actual-requested) <= tolerance
                if expected is not None: structured = structured and math.isclose(requested, expected[r], abs_tol=.001, rel_tol=1e-6)
            structured = structured and any(transaction.get("requestedDelta", {}).get(r, 0) < 0 for r in RESOURCES)
            if not structured:
                issues.append({"issue": "Payment is not an actual finite debit of its catalog cost from an owned living producer/worker", "event": ref})
            else:
                good_payments[line] = ref
                if age: advancing[(owner, entity, int(age[1]))].append(ref)
                elif label in techs: researching[(owner, entity, label)].append(ref)
        elif kind == "Spawn":
            definition = by_name.get(event["message"])
            births[entity] = (owner, definition, ref); living.add(entity)
            if own and definition and definition["kind"] == 1 and previous_line in good_payments and previous["player"] == owner and previous["message"].startswith("Foundation " + definition["name"] + ":"):
                completed_buildings[entity] = {"definition": definition["id"], "payment": good_payments[previous_line], "spawn": ref, "completion": None}
        elif kind == "ProductionQueued":
            job = re.fullmatch(r"([a-z_]+) ×(\d+)", event["message"])
            if own and job and previous_line in good_payments and previous["entity"] == entity and previous["player"] == owner:
                definition = by_id.get(job[1]); producer = births.get(entity)
                if definition and producer and producer[1] and producer[1]["id"] == definition["producer"] and previous["message"].startswith(definition["name"] + " ×" + job[2] + ":"):
                    training[(owner, entity, job[1])].append({"payment": good_payments[previous_line], "queue": ref, "remaining": int(job[2]), "duration": definition["time"]})
                else: issues.append({"issue": "Training producer or paid item does not match catalog", "event": ref})
        elif kind == "Experience" and event["message"] == "Unit trained" and own:
            birth = births.get(previous["entity"]) if previous and previous["kind"] == "Spawn" else None
            definition = birth[1] if birth else None
            key = (owner, entity, definition["id"] if definition else "")
            jobs = training[key]
            if birth and definition and birth[0] == owner and jobs and jobs[0]["remaining"] > 0 and abs(birth[2]["time"] - event["time"]) < .0001 and event["time"] - jobs[0]["queue"]["time"] >= jobs[0]["duration"] - .15:
                job = jobs[0]; evidence[owner]["training"].append({"definition": definition["id"], "payment": job["payment"], "queue": job["queue"], "spawn": birth[2], "completion": ref})
                job["remaining"] -= 1
                if job["remaining"] == 0: jobs.pop(0)
            else: issues.append({"issue": "Training completion lacks a paid uncancelled queue and sufficient elapsed production time", "event": ref})
        elif kind == "AgeAdvanced" and own:
            age = re.fullmatch(r"Reached age (\d+)", event["message"])
            jobs = advancing[(owner, entity, int(age[1]))] if age else []
            if jobs and event["time"]-jobs[0]["time"] >= ages[int(age[1])]["time"]-.15:
                evidence[owner]["ages"].append({"age": int(age[1]), "payment": jobs.pop(0), "completion": ref})
            else: issues.append({"issue": "Age completion lacks paid elapsed advance", "event": ref})
        elif kind == "Technology" and own:
            applied[owner].add(event["message"].split(" ", 1)[0])
        elif kind == "ResearchComplete" and own:
            tech = techs.get(event["message"]); jobs = researching[(owner, entity, event["message"])]
            if tech and jobs and event["time"]-jobs[0]["time"] >= tech["time"]-.15 and previous and previous["kind"] == "Technology" and previous["player"] == owner and previous["message"].startswith(tech["id"] + " applied once"):
                evidence[owner]["technologies"].append({"definition": tech["id"], "payment": jobs.pop(0), "completion": ref, "applied": reference(previous_line, previous)})
            else: issues.append({"issue": "Research completion lacks paid elapsed research and matching application", "event": ref})
        elif kind == "BuildingComplete" and own:
            job = completed_buildings.get(entity)
            if job and job["payment"]["player"] == owner and entity in living and job["completion"] is None:
                job["completion"] = ref; evidence[owner]["buildings"].append(dict(job))
            else: issues.append({"issue": "Building completion lacks an owned living paid foundation or is repeated", "event": ref})
        elif kind == "BankIncome" and own:
            job = completed_buildings.get(entity)
            if job and job["payment"]["player"] == owner and job["definition"] == "bank" and job["completion"] and entity in living and event["amount"] > 0 and event["message"] == "Coin":
                evidence[owner]["bankIncome"].append({"income": ref, "paidBank": job})
            else: issues.append({"issue": "Bank income lacks an owned paid completed bank", "event": ref})
        elif kind == "TreasureClaimed" and own:
            treasure, explorer = births.get(entity), births.get(event["target"])
            if treasure and treasure[1] and treasure[1]["id"].startswith("treasure_") and entity in living and explorer and explorer[0] == owner and explorer[1]["id"] == "explorer" and event["target"] in living:
                evidence[owner]["treasure"].append({"claim": ref, "treasureSpawn": treasure[2], "explorerSpawn": explorer[2]})
            else: issues.append({"issue": "Treasure claim lacks treasure/explorer identities", "event": ref})
        elif kind == "Experience" and event["message"] == "Trade route delivery" and own:
            xp_deliveries[(owner, entity)] = ref
        elif kind == "TradeMode" and own:
            trade_mode.pop((owner, entity), None)
            if event["message"] in ("Food", "Wood", "Coin") and "cargo_ledgers" in applied[owner]:
                trade_mode[(owner, entity)] = ref; evidence[owner]["resourceModes"].append(ref)
        elif kind == "TradeDelivery" and own:
            post = completed_buildings.get(entity); socket = births.get(event["target"])
            real_post = post and post["payment"]["player"] == owner and post["definition"] == "trade_post" and post["completion"] and entity in living and socket and socket[1] and socket[1]["id"] == "trade_socket" and event["amount"] > 0
            if event["message"] == "XP":
                xp = xp_deliveries.get((owner, entity))
                if real_post and xp and abs(xp["time"]-event["time"]) < .0001 and math.isclose(xp["amount"], event["amount"], abs_tol=.0001):
                    evidence[owner]["tradeXP"].append({"delivery": ref, "experience": xp, "paidPost": post})
                else: issues.append({"issue": "XP trade lacks paid completed post, socket and matching XP event", "event": ref})
            else:
                mode = trade_mode.get((owner, entity))
                if real_post and mode and mode["message"] == event["message"]:
                    evidence[owner]["resourceDeliveries"].append({"delivery": ref, "mode": mode, "paidPost": post})
                else: issues.append({"issue": "Resource trade lacks paid completed post and recorded resource mode", "event": ref})
        elif kind == "Refund" and own:
            cancelled = re.match(r"Cancelled ([a-z_]+|\d+):", event["message"])
            if cancelled:
                item = cancelled[1]
                if item.isdigit(): advancing.pop((owner, entity, int(item)), None)
                elif item in by_id:
                    jobs = training[(owner, entity, item)]
                    # Queue cancellation omits batch count/index. Equal-cost
                    # candidates cannot receive a made-up exact job identity.
                    refund = event.get("economy", {}).get("requestedDelta", {})
                    candidates = [job for job in jobs if all(math.isclose(-by_line[job["payment"]["line"]]["economy"]["requestedDelta"][r], refund.get(r, 0), abs_tol=.002) for r in RESOURCES)]
                    if len(candidates) > 1: ambiguities.append({"event": ref, "excludedPaymentLines": [j["payment"]["line"] for j in candidates]})
                    for job in candidates: jobs.remove(job)
                else:
                    for name, tech in techs.items():
                        if tech["id"] == item: researching.pop((owner, entity, name), None)
        elif kind == "ShipmentSent" and own:
            shipping[(owner, event["message"])].append(ref)
        elif kind == "ShipmentArrived" and own:
            name = event["message"].removesuffix(" arrived"); jobs = shipping[(owner, name)]
            card = cards.get(name)
            if not card or not jobs or event["time"]-jobs[0]["time"] < card.get("travel", 35)-.15:
                issues.append({"issue": "Shipment arrival lacks a sent card and elapsed travel", "event": ref})
            elif jobs: jobs.pop(0)
        elif kind == "Damage":
            shooter, target = births.get(entity), births.get(event["target"])
            if not shooter or shooter[0] != owner or not target or event["amount"] <= 0:
                issues.append({"issue": "Damage lacks real attacker ownership, target identity or positive amount", "event": ref})
        elif kind == "Death":
            living.discard(entity)
            for mapping in (training, advancing, researching):
                for key in [k for k in mapping if k[1] == entity]: del mapping[key]
        previous, previous_line = event, line
    return {"players": evidence, "issues": issues, "cancellationAmbiguities": ambiguities,
            "elapsedToleranceSeconds": .15, "scope": "Payment, queue, spawn and completion attribution with minimum catalog production duration; not a replay of navigation, worker labor, queue scheduling or input."}


def audit_player(report_path, rules_path, guid, version, match_id, scenario, deck_preset, menu_capture=None):
    guid, match_id = identity(guid, "Build GUID"), identity(match_id, "Match ID")
    if scenario not in ("coast-iii", "river-v") or deck_preset not in ("economy", "military"):
        raise ValueError("Explicit supported scenario and deck preset are required")
    report = strict_json(report_path.read_text()); rules = strict_json(rules_path.read_text())
    event_path = report_path.with_name(report_path.stem + "-events.jsonl")
    sources = {"report": source(report_path), "events": source(event_path), "rules": source(rules_path),
               "auditor": source(Path(__file__)), "scanEvents": source(Path(__file__).with_name("audit_ai_matrix.py")),
               "strictParser": source(Path(__file__).with_name("verify_performance.py")),
               "deckPresetSource": source(Path(__file__).resolve().parents[3] / "Assets/Frontier/Simulation/DeckLibrary.cs")}
    rows = []
    for line, text in enumerate(event_path.read_text().splitlines(), 1):
        if not text.strip(): continue
        event = strict_json(text)
        # Death.target is killerOwner and may be -1, not an entity ID.
        if type(event.get("schemaVersion")) is not int or event["schemaVersion"] != 2 or not all(type(event.get(k)) is int for k in ("player", "entity", "target")) or event["player"] not in (-1,0,1) or event["entity"] < 0 or event["target"] < -1 or not finite(event.get("time")) or event["time"] < 0 or not finite(event.get("amount")):
            raise ValueError("Malformed event identity, schema or numeric fields at line " + str(line))
        rows.append((line, event))
    civilizations = [p["civilization"] for p in report["players"]]
    civs = {c["id"]: c for c in rules["civs"]}
    if len(civilizations) != 2 or not all(c in civs for c in civilizations): raise ValueError("Invalid report civilizations")
    case = {f"{key}{p}": value for p in (0, 1) for key, value in (("civilization", civilizations[p]), ("deckPreset", deck_preset if p == 0 else "military"),
            ("deck", preset(rules, civilizations[p], deck_preset if p == 0 else "military")), ("startingResources", civs[civilizations[p]]["startingResources"]))}
    scan = scan_events(event_path, case, rules)
    lineage = payment_and_completions(rows, rules, civilizations)
    paired = menu_pair(menu_capture, guid, version)
    p = scan["players"][0]; proof = lineage["players"][0]
    trained = collections.Counter(item["definition"] for item in proof["training"])
    buildings = collections.Counter(item["definition"] for item in proof["buildings"])
    attained = {item["age"] for item in proof["ages"]}
    special = "carabineer" if scenario == "river-v" else "longbow"
    coverage = {"ageII": 2 in attained, "ageIII": 3 in attained, "ageIV": 4 in attained, "ageV": 5 in attained,
        "paidWorkers": trained["villager"] > 0, "paidMilitary": sum(v for k,v in trained.items() if k not in ("villager", "explorer")) > 0,
        "paidCannon": trained["cannon"] > 0, "cannonEnemyBuildingDamage": bool(p["cannonBuildingDamage"]),
        "gathered": sum(p["gatheredFromEvents"].values()) > 0, "shipmentArrived": bool(p["shipmentArrived"]),
        "paidBank": buildings["bank"] > 0, "bankIncome": bool(proof["bankIncome"]), "specialUnit": trained[special] > 0,
        "paidTechnology": bool(proof["technologies"]), "treasure": bool(proof["treasure"]),
        "paidTradePost": buildings["trade_post"] > 0, "tradeXP": bool(proof["tradeXP"]),
        "resourceMode": bool(proof["resourceModes"]), "resourceTradeDelivery": bool(proof["resourceDeliveries"])}
    ends = scan["endEvents"]; winner = report["winner"]
    required = list(BASE_REQUIREMENTS) + (list(RIVER_REQUIREMENTS) if scenario == "river-v" else [])
    checks = {"exactNativeBuildVersionMatch": str(report.get("buildGuid", "")).lower() == guid and report.get("appVersion") == version and report.get("platform") == "OSXPlayer",
        "explicitMatchIdentity": report.get("matchId") == match_id and match_id in report_path.stem,
        "normalPlayerOneX": report.get("autoplay") is False and finite(report.get("simulationSpeed")) and report["simulationSpeed"] == 1,
        "schemaRecognized": type(report.get("eventSchemaVersion")) is int and report["eventSchemaVersion"] == 2,
        "scenarioCivilizationsAndAgeCap": civilizations == ["coast", "coast"] and report.get("maxAge") == 3 if scenario == "coast-iii" else civilizations[0] == "river" and report.get("maxAge") == 5,
        "reportEventCountMatches": type(report.get("eventCount")) is int and report["eventCount"] == scan["eventCount"],
        "oneNormalMatchStarted": len(scan["startEvents"]) == 1 and scan["startEvents"][0]["time"] == 0 and scan["startEvents"][0]["message"].startswith(f"Seed {report['seed']}; age cap {report['maxAge']}; {civilizations[0]} vs {civilizations[1]};"),
        "normalOpeningBothPlayers": all(q["normalOpening"] for q in scan["players"]),
        "noOpeningDeaths": not any(e["kind"] == "Death" and e["time"] <= .0001 for _,e in rows),
        "associatedRawUnchangedDuringAudit": all(source(Path(info["path"]))["sha256"] == info["sha256"] for info in sources.values()),
        "eventIdentitiesAndPaymentsValid": not scan["eventIssues"] and not lineage["issues"],
        "reportedAgesMatchPaidCompletion": all(type(report["players"][i]["age"]) is int and report["players"][i]["age"] == max([1] + [item["age"] for item in lineage["players"][i]["ages"]]) for i in (0,1)),
        "oneNaturalEndWithoutResign": type(winner) is int and winner in (0,1) and len(ends) == 1 and scan["eventCounts"].get("Resign", 0) == 0,
        "naturalWinnerAndElapsedMatch": len(ends) == 1 and ends[0]["player"] == winner and finite(report.get("time")) and report["time"] > 0 and math.isclose(ends[0]["time"], report["time"], abs_tol=.15) and scan["lastEventTime"] <= report["time"] + .15,
        "naturalLoserHasZeroLivingUnitsOrBuildings": type(winner) is int and winner in (0,1) and scan["players"][1-winner]["livingUnitsAndBuildings"] == 0,
        "menuCapturePaired": paired["passed"], "requiredPlayerCoverage": all(coverage[k] for k in required)}
    return {"generatedAtUTC": datetime.datetime.now(datetime.timezone.utc).isoformat(), "status": "passed" if all(checks.values()) else "failed",
        "scope": "Native normal-rule 1x player event audit plus paired menu image bytes. Cannot certify gestures, frame appearance, human decisions, audible quality, performance, statistical balance, every technology or every resource trade mode.",
        "scenario": scenario, "expectedIdentity": {"appVersion":version,"buildGuid":guid,"matchId":match_id},
        "checks": checks, "failedChecks": [k for k,v in checks.items() if not v], "coverage": coverage,
        "requiredCoverage": required, "coverageGaps": [k for k in required if not coverage[k]],
        "observedOptionalGaps": [k for k,v in coverage.items() if not v and k not in required],
        "menuEvidence": paired, "deck": {"declaredPlayerPreset": deck_preset, "playerCards": case["deck0"], "opponentPreset": "military", "opponentCards": case["deck1"],
        "scope": "Preset is an externally declared UI pairing, calculated from recorded Rules and DeckLibrary source. Report/events/menu metadata have no deck configuration fields. Shipment legality is conditional on that UI declaration, not proof the declaration was logged."},
        "match": {k:report.get(k) for k in ("matchId","appVersion","buildGuid","platform","version","seed","maxAge","time","winner","reason","autoplay","simulationSpeed")},
        "sources": sources, "eventCounts": scan["eventCounts"], "eventIssues": scan["eventIssues"],
        "playerPaidCounts": dict(trained), "playerPaidBuildingCounts": dict(buildings), "lineage": lineage,
        "cannonEnemyBuildingDamageEvidence": p["cannonBuildingDamage"], "shipmentSentEvidence": p["shipmentSent"], "shipmentArrivedEvidence": p["shipmentArrived"],
        "openingEvidence": [q["openingEvidence"] for q in scan["players"]], "naturalEndEvidence": ends,
        "livingUnitsAndBuildingsAtEnd": [q["livingUnitsAndBuildings"] for q in scan["players"]]}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("report", type=Path); parser.add_argument("--build-guid", required=True); parser.add_argument("--app-version", required=True)
    parser.add_argument("--match-id", required=True); parser.add_argument("--scenario", choices=("coast-iii","river-v"), required=True)
    parser.add_argument("--deck-preset", choices=("economy","military"), required=True); parser.add_argument("--menu-capture", type=Path)
    parser.add_argument("--rules", type=Path, default=Path(__file__).resolve().parents[3]/"Assets/Resources/Rules.json")
    parser.add_argument("--archive-dir", type=Path); parser.add_argument("--output", type=Path, required=True)
    args=parser.parse_args()
    try:
        result=audit_player(args.report,args.rules,args.build_guid,args.app_version,args.match_id,args.scenario,args.deck_preset,args.menu_capture)
        if args.archive_dir:
            args.archive_dir.mkdir(parents=True,exist_ok=True)
            entries=list(result["sources"].values())+list(result["menuEvidence"].get("sources",{}).values())
            for info in entries:
                src=Path(info["path"]); dst=args.archive_dir/src.name
                if dst.exists() and source(dst)["sha256"] != info["sha256"]: raise ValueError("Archive has conflicting bytes: "+str(dst))
                if not dst.exists(): shutil.copyfile(src,dst)
                info["archivedPath"]=str(dst.resolve())
    except (OSError,ValueError,KeyError,TypeError,IndexError) as error:
        result={"status":"failed","error":type(error).__name__+": "+str(error),"scope":"Parsing/audit failed; no native gameplay acceptance inferred"}
    args.output.parent.mkdir(parents=True,exist_ok=True)
    partial=args.output.with_suffix(args.output.suffix+".partial"); partial.write_text(json.dumps(result,indent=2,ensure_ascii=False,allow_nan=False)+"\n"); partial.replace(args.output)
    print(json.dumps({"status":result["status"],"output":str(args.output.resolve()),"failedChecks":result.get("failedChecks",[]),"coverageGaps":result.get("coverageGaps",[]),"error":result.get("error")}))
    return 0 if result["status"]=="passed" else 1


if __name__=="__main__":
    raise SystemExit(main())
