#!/usr/bin/env python3
"""Extract ended-match evidence from explicit archive copies; not an acceptance verifier."""
import argparse
from collections import Counter
import datetime
import gzip
import hashlib
import json
from pathlib import Path


def digest(path):
    data = path.read_bytes()
    return {"file": path.name, "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--natural-end-confirmed", action="store_true", required=True)
    parser.add_argument("--archive-dir", type=Path, required=True)
    parser.add_argument("--report-name", required=True, help="Explicit match_finished JSON basename")
    parser.add_argument("--output", type=Path, required=True, help="New output; never overwrites")
    args = parser.parse_args()
    archive = args.archive_dir.resolve(strict=True)

    def member(name):
        if Path(name).name != name:
            raise ValueError("Supply an archive member basename: " + name)
        path = (archive / name).resolve(strict=True)
        if path.parent != archive:
            raise ValueError("Input must resolve inside the explicit archive: " + name)
        return path

    report_path = member(args.report_name)
    if not report_path.name.endswith("-match_finished.json"):
        raise ValueError("Require an explicit natural-end report basename")
    events_name = report_path.stem + "-events.jsonl"
    # Exact report-derived pairing only; no runtime-directory scan or latest-file lookup.
    if not (archive / events_name).exists():
        events_name += ".gz"
    events_path = member(events_name)
    summary_path, rules_path = member("summary.json"), member("Rules.json")
    report = json.loads(report_path.read_text())
    summary = json.loads(summary_path.read_text())
    rules = json.loads(rules_path.read_text())
    if report.get("reason") != "match_finished" or report.get("winner") not in (0, 1):
        raise ValueError("Input report is not naturally ended")
    identity_keys = ("appVersion", "buildGuid", "matchId")
    if any(report.get(k) != summary["expectedIdentity"].get(k)
           or report.get(k) != summary["match"].get(k) for k in identity_keys):
        raise ValueError("Report/strict summary identity mismatch")
    opener = gzip.open if events_path.suffix == ".gz" else open
    with opener(events_path, "rt", encoding="utf-8") as stream:
        events = [json.loads(line) for line in stream]
    definitions = {entry["name"]: entry for entry in rules["entities"]}
    player = 0
    lineage = summary["lineage"]["players"][player]
    issues, references = [], set()

    def raw_reference(line):
        if not isinstance(line, int) or not 1 <= line <= len(events):
            raise ValueError("Invalid original JSONL line reference: " + str(line))
        references.add(line)
        return {"line": line, "event": events[line - 1]}

    def check_references(value):
        if isinstance(value, dict):
            if "line" in value and "kind" in value:
                raw = raw_reference(value["line"])["event"]
                differences = {k: {"summary": v, "raw": raw.get(k)} for k, v in value.items()
                               if k != "line" and raw.get(k) != v}
                if differences:
                    issues.append({"line": value["line"], "summaryReferenceDifferences": differences})
            for nested in value.values():
                check_references(nested)
        elif isinstance(value, list):
            for nested in value:
                check_references(nested)

    for category in ("training", "buildings", "technologies", "ages"):
        check_references(lineage[category])
    births, deaths, living = {}, {}, set()
    for line, event in enumerate(events, 1):
        entity = event["entity"]
        if event["kind"] == "Spawn":
            if entity in births:
                issues.append({"line": line, "duplicateSpawnEntity": entity})
            births[entity] = {"line": line, "event": event,
                              "definition": definitions.get(event["message"])}
            living.add(entity)
        elif event["kind"] == "Death":
            deaths[entity] = {"line": line, "event": event}
            living.discard(entity)
    paid_by_id = {row["spawn"]["entity"]: row for row in lineage["training"]}
    cannon_ids = {entity for entity, birth in births.items()
                  if birth["event"]["player"] == player
                  and (birth["definition"] or {}).get("id") == "cannon"}
    building_hits = []
    cannon_records = []
    for entity in sorted(cannon_ids):
        paid = paid_by_id.get(entity)
        fires, commands, damage = [], [], []
        for line, event in enumerate(events, 1):
            if event["entity"] != entity or event["player"] != player:
                continue
            if event["kind"] == "Fire":
                fires.append(raw_reference(line))
            elif event["kind"] == "Command":
                commands.append(raw_reference(line))
            elif event["kind"] == "Damage" and event["amount"] > 0:
                target = births.get(event["target"])
                target_definition = (target or {}).get("definition") or {}
                enemy_building = bool(target and target["event"]["player"] == 1 - player
                                      and target_definition.get("kind") == 1)
                row = {**raw_reference(line), "targetSpawn": target,
                       "enemyBuilding": enemy_building,
                       "paidSource": bool(paid and paid["definition"] == "cannon")}
                damage.append(row)
                if enemy_building:
                    building_hits.append(row)
                    raw_reference(target["line"])
        raw_reference(births[entity]["line"])
        if entity in deaths:
            raw_reference(deaths[entity]["line"])
        cannon_records.append({"entity": entity, "spawn": births[entity],
                               "paidLineage": paid, "death": deaths.get(entity),
                               "commands": commands, "fires": fires, "positiveDamage": damage})
    paid_hits = sorted((row for row in building_hits if row["paidSource"]), key=lambda row: row["line"])
    canonical_lines = [row["line"] for row in summary["cannonEnemyBuildingDamageEvidence"]]
    extracted_lines = [row["line"] for row in paid_hits]
    if extracted_lines != canonical_lines:
        issues.append({"cannonBuildingLinesDiffer": {"extracted": extracted_lines,
                                                       "canonical": canonical_lines}})
    natural, resign, player_economy_progress = [], [], []
    for line, event in enumerate(events, 1):
        if event["kind"] == "MatchEnded":
            natural.append(raw_reference(line))
        elif event["kind"] == "Resign":
            resign.append(raw_reference(line))
        if event["player"] == player and event["kind"] in (
                "Payment", "AgeAdvanced", "ResearchComplete", "Technology", "TechnologyApplied"):
            player_economy_progress.append(raw_reference(line))
    living_counts = [sum(births[entity]["event"]["player"] == side
                         and (births[entity]["definition"] or {}).get("kind") in (0, 1)
                         for entity in living) for side in (0, 1)]
    if living_counts != summary["livingUnitsAndBuildingsAtEnd"]:
        issues.append({"livingCountsDiffer": {"extracted": living_counts,
                                               "canonical": summary["livingUnitsAndBuildingsAtEnd"]}})
    lifetimes = []
    for row in lineage["training"]:
        if row["definition"] not in ("musketeer", "cannon", "carabineer"):
            continue
        entity = row["spawn"]["entity"]
        death = deaths.get(entity)
        if death:
            raw_reference(death["line"])
        lifetimes.append({"entity": entity, "definition": row["definition"],
                          "paidLineage": row, "death": death,
                          "technologyCompletionsBeforeSpawn": [technology for technology in lineage["technologies"]
                           if technology["completion"]["time"] < row["spawn"]["time"]]})
    output = {
        "generatedAtUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "scope": "Ended archive copies only. Extraction aid, not a new acceptance verifier. Canonical status remains authoritative; screenshots/context association require separate inspection.",
        "identity": {k: report[k] for k in identity_keys},
        "sources": [digest(path) for path in (report_path, events_path, summary_path, rules_path)],
        "canonicalResult": {k: summary[k] for k in ("status", "checks", "failedChecks", "coverage", "coverageGaps")},
        "canonicalLineage": {k: lineage[k] for k in ("training", "buildings", "technologies", "ages")},
        "paidTrainingCounts": dict(Counter(row["definition"] for row in lineage["training"])),
        "paidBuildingCounts": dict(Counter(row["definition"] for row in lineage["buildings"])),
        "cannons": cannon_records,
        "paidCannonEnemyBuildingDamage": paid_hits,
        "paidCannonBuildingHitCount": len(paid_hits),
        "paidCannonBuildingDamageTotal": sum(row["event"]["amount"] for row in paid_hits),
        "unpaidCannonEnemyBuildingDamage": [row for row in building_hits if not row["paidSource"]],
        "unitLifetimesForVisualAssociation": lifetimes,
        "playerPaymentsAndProgression": player_economy_progress,
        "naturalEnd": {"matchEnded": natural, "resignEvents": resign,
                       "noResignObserved": not resign, "reportWinner": report["winner"],
                       "reportTime": report["time"], "rawEventCount": len(events),
                       "reportEventCount": report["eventCount"],
                       "livingUnitsAndBuildingsByPlayer": living_counts},
        "referenceIssues": issues,
        "selectedRawEvents": [raw_reference(line) for line in sorted(references)],
        "lineNumbering": "One-based original JSONL lines; gzip retains original decompressed line order.",
        "visualLimit": "No screenshots or input-context IDs are read or asserted by this helper. F9 metadata has no selectedEntity field. Do not infer same-old identity from a group label."
    }
    with args.output.open("x", encoding="utf-8") as stream:
        stream.write(json.dumps(output, ensure_ascii=False, indent=2, allow_nan=False) + "\n")
    print(json.dumps({"output": str(args.output), "canonicalStatus": summary["status"],
                      "coverageGaps": summary["coverageGaps"], "referenceIssueCount": len(issues),
                      "paidCannonBuildingHits": len(paid_hits),
                      "paidCannonBuildingDamage": output["paidCannonBuildingDamageTotal"],
                      "scope": "Extraction completed; this exit code is not gameplay acceptance."}))


if __name__ == "__main__":
    main()
