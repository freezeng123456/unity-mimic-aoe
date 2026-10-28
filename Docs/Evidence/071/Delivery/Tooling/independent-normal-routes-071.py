#!/usr/bin/env python3
"""Read-only, independent audit of the eight ordinary 071 AI route observations.

The observer's result/seed choice is a claim, not proof. Actual route evidence is
recomputed from opening data, payment transactions, ordered events and terminal
world/scheduler state. Only a new exclusive receipt is written. No Unity is run.
Checksums/source fingerprints bind local bytes; none is a signed anti-cheat proof.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import re
import stat
import struct
import sys
import tempfile
import unittest

CIVS = ("coast", "river", "france", "ottoman")
EXPECTED = {f"{c}-{r}": (c, r, s) for c in CIVS
            for r, s in (("revolution", 42), ("empire", 43))}
SOURCE_ROOTS = ("Assets", "Packages", "ProjectSettings", "Tools")
HEX = re.compile(r"^[0-9a-f]{64}$")
MAX_BYTES = 512 * 1024 * 1024
STEP = struct.unpack("f", struct.pack("f", .1))[0]
EPS = .002
PAY_EPS = .05
DURATION_EPS = .15


class AuditError(ValueError):
    pass


def need(condition, message):
    if not condition:
        raise AuditError(message)


def unique_object(pairs):
    result = {}
    for k, v in pairs:
        need(k not in result, "duplicate JSON property: " + k)
        result[k] = v
    return result


def parse(raw):
    return json.loads(raw, object_pairs_hook=unique_object,
                      parse_constant=lambda s: (_ for _ in ()).throw(AuditError("nonfinite JSON: " + s)))


def stable_raw(path):
    before = path.lstat()
    need(stat.S_ISREG(before.st_mode), "not a regular input: " + str(path))
    need(before.st_size <= MAX_BYTES, "input exceeds audit bound: " + str(path))
    raw = path.read_bytes()
    after = path.lstat()
    need((before.st_ino, before.st_size, before.st_mtime_ns, before.st_ctime_ns) ==
         (after.st_ino, after.st_size, after.st_mtime_ns, after.st_ctime_ns), "input changed while read")
    return raw


def read_json(path, hashes):
    raw = stable_raw(path)
    hashes[str(path)] = hashlib.sha256(raw).hexdigest()
    return parse(raw.decode("utf-8", "strict"))


def read_lines(path, hashes):
    raw = stable_raw(path)
    hashes[str(path)] = hashlib.sha256(raw).hexdigest()
    lines = raw.decode("utf-8", "strict").splitlines()
    need(len(lines) <= 1000000, "too many event/summary rows")
    need(all(line for line in lines), "blank JSONL record")
    return [parse(line) for line in lines]


def number(v):
    return type(v) in (int, float) and math.isfinite(v)


def near(a, b, epsilon=EPS):
    return number(a) and number(b) and abs(a - b) <= epsilon


def cost(v):
    need(isinstance(v, dict), "missing resource vector")
    result = {k: v.get(k, 0) for k in ("food", "wood", "coin")}
    need(all(number(x) for x in result.values()), "invalid resource vector")
    return result


def costs_equal(a, b, epsilon=EPS):
    a, b = cost(a), cost(b)
    return all(near(a[k], b[k], epsilon) for k in a)


def float_clock(steps):
    need(type(steps) is int and 0 <= steps <= 24010, "invalid ordinary observation step count")
    t = 0.0
    for _ in range(steps):
        t = struct.unpack("f", struct.pack("f", t + STEP))[0]
    return t


def source_identity(repo):
    files = {}
    for root in SOURCE_ROOTS:
        for p in sorted((repo / root).rglob("*")):
            relative = p.relative_to(repo)
            if p.is_file() and not any(x.startswith(".") or x == "__pycache__" for x in relative.parts):
                files[relative.as_posix()] = hashlib.sha256(stable_raw(p)).hexdigest()
    need(files, "no source inputs")
    fp = hashlib.sha256(json.dumps(files, sort_keys=True, separators=(",", ":")).encode()).hexdigest()
    return fp, files


def catalogue(repo, hashes):
    rules = read_json(repo / "Assets/Resources/Rules.json", hashes)
    model_path = repo / "Assets/Frontier/Simulation/Model.cs"
    model = stable_raw(model_path)
    hashes[str(model_path)] = hashlib.sha256(model).hexdigest()
    civ_source = model.decode().split("public class CivDef", 1)[1].split("public class AgeChoiceDef", 1)[0]
    workers = re.search(r"startingWorkers\s*=\s*(\d+)", civ_source)
    worker = re.search(r'workerUnit\s*=\s*"([^"]+)"', civ_source)
    need(workers and worker, "cannot independently resolve CivDef opening defaults")
    rules["_opening_defaults"] = {"startingWorkers": int(workers.group(1)), "workerUnit": worker.group(1)}
    rules["_entities"] = {d["id"]: d for d in rules["entities"]}
    rules["_civs"] = {d["id"]: d for d in rules["civs"]}
    rules["_revolutions"] = {d["civilization"]: d for d in rules["revolutions"]}
    rules["_ages"] = {d["age"]: d for d in rules["ages"]}
    rules["_rebuild"] = next(d for d in rules["techs"] if d["id"] == "civilian_rebuilding")
    need(all(rules["_revolutions"][c]["time"] == 90 for c in CIVS) and rules["_rebuild"]["time"] == 60,
         "audit targets the 071 90s commitment / 60s civilian rebuilding contract")
    return rules


def land_worker(definition):
    return definition is not None and definition.get("kind", 0) == 0 and not definition.get("waterborne", False) and \
        definition.get("tags", 0) & 128 != 0


def live(state):
    return not state.get("dead", False) and state.get("hp", 0) > 0


def valid_payment(event, expected, prefix):
    if event.get("kind") != "Payment" or event.get("hasEconomy") is not True or not event.get("message", "").startswith(prefix + ": "):
        return False
    economy = event.get("economy")
    if not isinstance(economy, dict):
        return False
    expected = cost(expected)
    try:
        requested, actual = cost(economy["requestedDelta"]), cost(economy["actualDelta"])
        before, after = cost(economy["balanceBefore"]), cost(economy["balanceAfter"])
    except (AuditError, KeyError):
        return False
    return all(near(requested[k], -expected[k], PAY_EPS) and near(actual[k], -expected[k], PAY_EPS)
               and before[k] + EPS >= expected[k] and near(after[k] - before[k], actual[k], PAY_EPS)
               for k in expected)


def payment_for(events, completion_index, expected, prefix, duration, item, require_queue=False):
    complete = events[completion_index]
    for i in range(completion_index - 1, -1, -1):
        event = events[i]
        if event.get("player") != 0 or event.get("entity") != complete.get("entity") or not valid_payment(event, expected, prefix):
            continue
        if complete["time"] - event["time"] < duration - DURATION_EPS:
            continue
        intervening = events[i + 1:completion_index]
        if any(e.get("player") == 0 and e.get("entity") == event["entity"] and
               (e.get("kind") == "Death" or e.get("kind") == "Refund" and
                e.get("message", "").startswith("Cancelled " + str(item) + ": ")) for e in intervening):
            continue
        queued = [i + 1 + j for j, e in enumerate(intervening) if e.get("player") == 0 and
                  e.get("entity") == event["entity"] and e.get("kind") == "RevolutionQueued" and
                  e.get("message") == item and near(e.get("time"), event.get("time"))]
        if require_queue and not queued:
            continue
        return {"paymentEvent": i, "completionEvent": completion_index, "town": complete["entity"],
                "paidAt": event["time"], "completedAt": complete["time"],
                "elapsedWorldSeconds": complete["time"] - event["time"], "requiredQueueSeconds": duration,
                "requestedCost": expected, "queuedEvent": queued[0] if queued else None}
    return None


def recompute_route(rules, case, world, events):
    """Recompute only player 0's requested route; never infer it from seed policy."""
    state = world["players"][0]["state"]
    civ, requested = case["civilization"], case["requestedRoute"]
    entities = {e["state"]["id"]: e for e in world["entities"]}
    definitions = rules["_entities"]
    proofs = {"ageAdvancements": [], "revolution": None, "rebuilding": None,
              "convertedIds": [], "postRebuiltWorkerBirths": [], "naturalGatherEvents": [],
              "naturalIncomeAfterRebuilding": {"food": 0.0, "wood": 0.0, "coin": 0.0}}
    def own_town(proof):
        entity = entities.get(proof["town"]) if proof else None
        return entity is not None and entity.get("definition") == "town" and entity["state"].get("owner") == 0
    ordered_age = 1
    for i, event in enumerate(events):
        if event.get("player") != 0 or event.get("kind") != "AgeAdvanced":
            continue
        match = re.fullmatch(r"Reached age ([2-5])", event.get("message", ""))
        if not match:
            continue
        age = int(match.group(1))
        if age != ordered_age + 1:
            continue
        rule = rules["_ages"][age]
        proof = payment_for(events, i, rule["cost"], "Advance to age " + str(age), rule["time"], str(age))
        if proof and own_town(proof):
            ordered_age = age
            proofs["ageAdvancements"].append(dict(proof, age=age))
    if requested == "empire":
        reached = state.get("age") == 5 and state.get("politicalPhase") == 0 and ordered_age == 5 and not any(
            e.get("player") == 0 and e.get("kind") == "RevolutionCompleted" for e in events)
        return reached, proofs
    rule, rebuild = rules["_revolutions"][civ], rules["_rebuild"]
    for i, event in enumerate(events):
        if event.get("player") == 0 and event.get("kind") == "RevolutionCompleted" and event.get("message") == rule["id"]:
            proof = payment_for(events, i, rule["cost"], rule["name"], rule["time"], rule["id"], True)
            age_four = next((p for p in proofs["ageAdvancements"] if p["age"] == 4), None)
            if proof and own_town(proof) and age_four is not None and age_four["completedAt"] <= proof["paidAt"] + EPS:
                proofs["revolution"] = proof
                break
    revolution = proofs["revolution"]
    if revolution is None:
        return False, proofs
    rev_index = revolution["completionEvent"]
    proofs["convertedIds"] = [e["entity"] for e in events[:rev_index] if e.get("player") == 0 and
                             e.get("kind") == "WorkerConverted" and near(e.get("time"), revolution["completedAt"]) and
                             e.get("message") == rule["unit"]]
    if len(set(proofs["convertedIds"])) != len(proofs["convertedIds"]) or not near(
            events[rev_index].get("amount"), len(proofs["convertedIds"])):
        return False, proofs
    if any(entities.get(i, {}).get("state", {}).get("owner") != 0 or not definitions.get(
            entities.get(i, {}).get("definition"), {}).get("revolutionary", False) for i in proofs["convertedIds"]):
        return False, proofs
    for i in range(rev_index + 1, len(events)):
        event = events[i]
        if event.get("player") == 0 and event.get("kind") == "CivilianRebuilt":
            proof = payment_for(events, i, rebuild["cost"], rebuild["name"], rebuild["time"], rebuild["id"])
            research_completed = any(e.get("player") == 0 and e.get("entity") == event.get("entity") and
                                     e.get("kind") == "ResearchComplete" and e.get("message") == rebuild["name"] and
                                     near(e.get("time"), event.get("time")) for e in events[i + 1:])
            if proof and own_town(proof) and research_completed and proof["paidAt"] >= revolution["completedAt"] - EPS:
                proofs["rebuilding"] = proof
                break
    rebuilding = proofs["rebuilding"]
    if rebuilding is None:
        return False, proofs
    born = {}
    for i, event in enumerate(events):
        if event.get("player") != 0 or event.get("kind") != "Spawn" or i <= rebuilding["completionEvent"] or event.get("time", -1) < rebuilding["completedAt"] - EPS:
            continue
        entity = entities.get(event.get("entity"))
        d = definitions.get(entity.get("definition")) if entity else None
        if entity and entity["state"]["owner"] == 0 and land_worker(d) and event.get("message") == d["name"]:
            born[event["entity"]] = (i, event["time"])
            proofs["postRebuiltWorkerBirths"].append({"event": i, "id": event["entity"], "time": event["time"],
                                                   "definition": entity["definition"], "aliveAtEnd": live(entity["state"])})
    for i, event in enumerate(events):
        if event.get("player") != 0 or event.get("kind") != "Gather" or not number(event.get("amount")) or event["amount"] <= 0:
            continue
        worker_birth = born.get(event.get("entity"))
        source = entities.get(event.get("target"))
        if not worker_birth or i <= worker_birth[0] or event.get("time", -1) <= worker_birth[1] or source is None:
            continue
        definition = definitions.get(source["definition"])
        if definition is None or source["definition"].startswith("crate_") or definition.get("resource", 0) not in (1, 2, 3):
            continue
        if any(e.get("kind") == "Death" and e.get("entity") == event["entity"] for e in events[worker_birth[0] + 1:i]):
            continue
        resource = {1: "food", 2: "wood", 3: "coin"}[definition["resource"]]
        if event.get("message") != resource.capitalize():
            continue
        proofs["naturalGatherEvents"].append({"event": i, "worker": event["entity"], "source": event["target"],
                                             "sourceDefinition": source["definition"], "time": event["time"], "amount": event["amount"]})
        proofs["naturalIncomeAfterRebuilding"][resource] += event["amount"]
    reached = state.get("politicalPhase") == 2 and state.get("age") == 4 and state.get("revolutionId") == rule["id"] and \
        near(state.get("revolutionAt"), revolution["completedAt"]) and near(state.get("civilianRebuiltAt"), rebuilding["completedAt"]) and \
        rebuild["id"] in world["players"][0].get("technologies", []) and bool(born) and bool(proofs["naturalGatherEvents"])
    return reached, proofs


def inspect_case(rules, case, manifest, directory, hashes):
    result = read_json(directory / "result.json", hashes)
    need(result == case, "case manifest and result differ")
    if case.get("status") == "exception":
        return {"id": case["id"], "verdict": "execution_exception", "independentRouteReached": False,
                "observedStatus": case["status"], "error": case.get("error", ""), "issues": []}
    envelope = read_json(directory / "opening-scenario.json", hashes)
    need(envelope.get("schema") == 1 and envelope.get("encoding") == "frontier-scenario-json-v1" and
         isinstance(envelope.get("payload"), str), "wrong opening envelope")
    need(hashlib.sha256(envelope["payload"].encode()).hexdigest() == envelope.get("sha256"), "opening checksum mismatch")
    doc = parse(envelope["payload"])
    world = read_json(directory / "final-world.json", hashes)
    simulation = read_json(directory / "final-simulation.json", hashes)
    events = read_lines(directory / "events.jsonl", hashes)
    summaries = read_lines(directory / "snapshots.jsonl", hashes)
    need(doc.get("id") == "traders_bend070" and doc.get("seed") == case["seed"] and doc.get("enableElimination") is True,
         "wrong ordinary scenario identity")
    need(not doc.get("objectives") and not doc.get("triggers"), "ordinary observation has scripted goals/rewards")
    need(len(doc.get("starts", [])) == 2 and len(world.get("players", [])) == 2, "mirror roster mismatch")
    need(world.get("scenarioDocument") == doc and world.get("scenarioHash") == envelope["sha256"] and
         world.get("hasScenarioDocument") is True and world.get("mapId") == "custom_scenario", "terminal opening binding mismatch")
    need(doc.get("rulesHash") == manifest.get("rulesHash") == world.get("rulesHash") and HEX.fullmatch(doc.get("rulesHash", "")),
         "rules identity mismatch")
    need(world.get("seed") == case["seed"] and world.get("maxAge") == 5 and world.get("paused") is False, "terminal simulation identity mismatch")
    need(simulation.get("difficultyId") == "standard" and [x.get("player") for x in simulation.get("controllers", [])] == [1, 0],
         "actual AI roster/difficulty mismatch")
    need(near(world.get("time"), float_clock(simulation.get("stepCount"))) and near(case.get("seconds"), world["time"]),
         "actual fixed scheduler/world clock mismatch")
    need(events == world.get("events"), "event stream differs from final world history")
    need(events and all(isinstance(e, dict) and number(e.get("time")) and 0 <= e["time"] <= world["time"] + EPS for e in events),
         "invalid event clock")
    need(all(events[i]["time"] <= events[i + 1]["time"] for i in range(len(events) - 1)), "event order regressed")
    entities = {e["state"]["id"]: e for e in world["entities"]}
    need(len(entities) == len(world["entities"]), "duplicate final entity identity")
    bindings = {b["objectId"]: b["entityIds"] for b in world["scenarioState"]["bindings"]}
    civ = rules["_civs"][case["civilization"]]
    expected_workers = civ.get("startingWorkers", rules["_opening_defaults"]["startingWorkers"])
    worker_id = civ.get("workerUnit", rules["_opening_defaults"]["workerUnit"]) or rules["_opening_defaults"]["workerUnit"]
    worker_name = rules["_entities"][worker_id]["name"]
    opening = []
    for p, start in enumerate(doc["starts"]):
        need(start.get("player") == p and start.get("team") == p and start.get("civilization") == civ["id"] and start.get("age") == 1,
             "opening is not a normal age-I mirror faction")
        need(costs_equal(start["initialResources"], civ["startingResources"]) and len(start["workers"]) == expected_workers,
             "opening differs from actual Catalog resources/workforce")
        for placement in doc.get("objects", []) + doc.get("resources", []):
            d = rules["_entities"][placement["definition"]]
            need(not (placement.get("owner") == p and d.get("kind", 0) == 0), "opening grants an extra owned unit")
        actual_ids = []
        for n, point in enumerate(start["workers"]):
            ids = bindings.get(f"start-{p}-worker-{n}")
            need(isinstance(ids, list) and len(ids) == 1 and ids[0] in entities, "missing actual opening worker binding")
            matches = [e for e in events if e.get("kind") == "Spawn" and e.get("player") == p and e.get("entity") == ids[0] and near(e.get("time"), 0)]
            need(len(matches) == 1 and matches[0].get("message") == worker_name and matches[0].get("position") == point,
                 "opening worker not physically spawned at declared position")
            final_definition = rules["_entities"].get(entities[ids[0]]["definition"])
            need(final_definition is not None and (land_worker(final_definition) or final_definition.get("revolutionary", False)),
                 "opening worker changed to an unrelated definition")
            if final_definition.get("revolutionary", False):
                converted = [e for e in events if e.get("kind") == "WorkerConverted" and e.get("player") == p and
                             e.get("entity") == ids[0] and e.get("message") == entities[ids[0]]["definition"] and
                             near(e.get("time"), world["players"][p]["state"].get("revolutionAt"))]
                need(len(converted) == 1, "converted opening worker is missing its real conversion event")
            actual_ids.append(ids[0])
        starts = [e for e in events if e.get("kind") == "StartingResources" and e.get("player") == p and near(e.get("time"), 0)]
        expected_message = "Authored scenario opening: " + " / ".join(f"{cost(civ['startingResources'])[k]:.0f} {symbol}" for k, symbol in (("food", "F"), ("wood", "W"), ("coin", "C")))
        need(len(starts) == 1 and starts[0].get("message") == expected_message, "actual opening resource event mismatch")
        state = world["players"][p]["state"]
        need(state.get("id") == p and state.get("civilization") == civ["id"] and state.get("team") == p, "final roster changed")
        opening.append({"player": p, "resources": cost(start["initialResources"]), "workerDefinition": worker_id, "workerIds": actual_ids})
    need(case.get("startingWorkers") == expected_workers and costs_equal(case["startingResources"], civ["startingResources"]), "opening metadata contradicts original evidence")
    need(len(summaries) >= 2 and [s.get("player") for s in summaries[:2]] == [0, 1] and
         all(s.get("age") == 1 and s.get("phase") == 0 for s in summaries[:2]), "missing actual age-I summaries")
    terminal = world.get("winner") != -1
    need(type(case.get("naturalTerminal")) is bool and case["naturalTerminal"] == terminal and case.get("winner") == world.get("winner"), "terminal claim differs from world")
    reached, proof = recompute_route(rules, case, world, events)
    state = world["players"][0]["state"]
    issues = []
    if case.get("routeReached") is not reached:
        issues.append("observer routeReached contradicts independent payment/clock/birth/gather proof")
    if case.get("age") != state.get("age") or case.get("phase") != state.get("politicalPhase"):
        issues.append("observer age/phase differs from final world")
    if reached:
        verdict = "route_reached"
    elif case["requestedRoute"] == "revolution" and state.get("politicalPhase") == 2:
        verdict = "civilian_economy_recovery_incomplete"
    elif terminal:
        verdict = "natural_terminal_before_route"
    elif world["time"] >= 2400 - EPS:
        verdict = "route_not_reached_at_time_limit"
    else:
        verdict = "observation_stopped_before_terminal_or_limit"
        issues.append("no natural terminal and clock is below the observation cap")
    return {"id": case["id"], "requestedRoute": case["requestedRoute"], "seed": case["seed"],
            "verdict": verdict, "independentRouteReached": reached, "observerRouteReached": case.get("routeReached"),
            "naturalTerminal": terminal, "winner": world["winner"], "seconds": world["time"], "opening": opening,
            "proof": proof, "issues": issues}


def audit(args):
    hashes = {}
    manifest = read_json(args.evidence_dir / "manifest.json", hashes)
    before = read_json(args.source_before, hashes)
    after = read_json(args.source_after, hashes)
    need(HEX.fullmatch(args.expected_source_fingerprint), "expected source fingerprint missing or invalid")
    fp, files = source_identity(args.repo)
    need(before.get("fingerprint") == after.get("fingerprint") == manifest.get("declaredSourceFingerprint") ==
         args.expected_source_fingerprint == fp and after.get("sourceStable") is True and after.get("unityExitCode") == 0,
         "source/observer runner identity is not independently stable")
    need(manifest.get("finishedUtc") and manifest.get("status") != "running", "matrix is still unfinished")
    cases = manifest.get("cases")
    need(isinstance(cases, list) and len(cases) == 8 and {c.get("id") for c in cases} == set(EXPECTED), "expected eight unique cases, retain all samples")
    rules = catalogue(args.repo, hashes)
    observed = []
    for case in cases:
        expected = EXPECTED[case["id"]]
        try:
            need((case.get("civilization"), case.get("requestedRoute"), case.get("seed")) == expected, "case identity changed")
            observed.append(inspect_case(rules, case, manifest, args.evidence_dir / case["id"], hashes))
        except (OSError, ValueError, KeyError, TypeError) as error:
            observed.append({"id": case["id"], "independentRouteReached": False,
                             "verdict": "evidence_invalid_or_incomplete", "issues": [str(error)], "observedCase": case})
    current, _ = source_identity(args.repo)
    need(current == fp, "source changed during independent audit")
    incomplete = sum(not c["independentRouteReached"] for c in observed)
    invalid = sum(bool(c.get("issues")) for c in observed)
    return {"schema": 1, "scope": "Local headless ordinary age-I observation only; no native input, physical-Mac LAN or balance certification",
            "trustLimit": "JSON checksums and source fingerprints are unsigned local integrity evidence, not proof of all historical payments or anti-cheat authentication",
            "status": "evidence_invalid" if invalid else "observed_routes_incomplete" if incomplete else "all_routes_independently_reached",
            "sourceFingerprint": fp, "sourceInputCount": len(files), "cases": observed,
            "caseCount": len(observed), "independentlyReached": 8 - incomplete, "invalidEvidenceCases": invalid,
            "evidenceHashes": hashes, "auditorSha256": hashlib.sha256(stable_raw(Path(__file__))).hexdigest()}


class SyntheticChecks(unittest.TestCase):
    """Synthetic contract tests are not observations of any actual game."""
    def payment(self, when=10):
        return {"kind": "Payment", "player": 0, "entity": 1, "time": when, "message": "Assembly: cost",
                "hasEconomy": True, "economy": {"requestedDelta": {"food": -800, "wood": -800, "coin": -800},
                "actualDelta": {"food": -800, "wood": -800, "coin": -800},
                "balanceBefore": {"food": 1000, "wood": 1000, "coin": 1000}, "balanceAfter": {"food": 200, "wood": 200, "coin": 200}}}

    def sequence(self, end=100):
        return [self.payment(), {"kind": "RevolutionQueued", "player": 0, "entity": 1, "time": 10, "message": "rev"},
                {"kind": "RevolutionCompleted", "player": 0, "entity": 1, "time": end, "message": "rev"}]

    def test_actual_payment_and_own_ninety_seconds(self):
        self.assertIsNotNone(payment_for(self.sequence(), 2, {"food": 800, "wood": 800, "coin": 800}, "Assembly", 90, "rev", True))

    def test_design_seed_is_never_payment_evidence(self):
        events = self.sequence()[1:]
        self.assertIsNone(payment_for(events, 1, {"food": 800, "wood": 800, "coin": 800}, "Assembly", 90, "rev", True))

    def test_underpaid_requested_receipt_rejected(self):
        event = self.payment();event["economy"]["requestedDelta"]["food"] = -799
        self.assertFalse(valid_payment(event, {"food": 800, "wood": 800, "coin": 800}, "Assembly"))

    def test_actual_money_must_leave_balance(self):
        event = self.payment();event["economy"]["actualDelta"]["food"] = 0
        self.assertFalse(valid_payment(event, {"food": 800, "wood": 800, "coin": 800}, "Assembly"))

    def test_balance_vector_must_agree(self):
        event = self.payment();event["economy"]["balanceAfter"]["coin"] = 1000
        self.assertFalse(valid_payment(event, {"food": 800, "wood": 800, "coin": 800}, "Assembly"))

    def test_eighty_seconds_is_not_ninety(self):
        self.assertIsNone(payment_for(self.sequence(90), 2, {"food": 800, "wood": 800, "coin": 800}, "Assembly", 90, "rev", True))

    def test_cancellation_cannot_be_reused_as_paid_completion(self):
        events = self.sequence();events.insert(2, {"kind": "Refund", "player": 0, "entity": 1, "time": 20, "message": "Cancelled rev: cost"})
        self.assertIsNone(payment_for(events, 3, {"food": 800, "wood": 800, "coin": 800}, "Assembly", 90, "rev", True))

    def test_destroyed_producer_cannot_complete_old_payment(self):
        events = self.sequence();events.insert(2, {"kind": "Death", "player": 0, "entity": 1, "time": 20})
        self.assertIsNone(payment_for(events, 3, {"food": 800, "wood": 800, "coin": 800}, "Assembly", 90, "rev", True))

    def test_real_queue_is_required_for_revolution(self):
        events = self.sequence();events.pop(1)
        self.assertIsNone(payment_for(events, 1, {"food": 800, "wood": 800, "coin": 800}, "Assembly", 90, "rev", True))

    def test_water_worker_cannot_prove_civilian_recovery(self):
        self.assertTrue(land_worker({"kind": 0, "tags": 128}))
        self.assertFalse(land_worker({"kind": 0, "tags": 128, "waterborne": True}))
        self.assertFalse(land_worker({"kind": 0, "tags": 3, "revolutionary": True}))

    def test_duplicate_json_property_rejected(self):
        with self.assertRaises(AuditError):parse('{"routeReached":false,"routeReached":true}')

    def test_nonfinite_json_rejected(self):
        with self.assertRaises(AuditError):parse('{"time":NaN}')

    def route_data(self):
        zero = {"food": 0, "wood": 0, "coin": 0}
        def charge(prefix, t, payment):
            return {"kind": "Payment", "player": 0, "entity": 10, "time": t, "message": prefix + ": cost", "hasEconomy": True,
                    "economy": {"requestedDelta": {k: -v for k, v in payment.items()}, "actualDelta": {k: -v for k, v in payment.items()},
                    "balanceBefore": {k: 2000 for k in payment}, "balanceAfter": {k: 2000 - v for k, v in payment.items()}}}
        ages = {a: {"time": 10, "cost": dict(zero, food=100)} for a in range(2, 6)}
        rev = {"id": "rev", "name": "Assembly", "unit": "soldier", "time": 90, "cost": {"food": 800, "wood": 800, "coin": 800}}
        rebuild = {"id": "civilian_rebuilding", "name": "Rebuild", "time": 60, "cost": dict(zero, food=400, coin=400)}
        rules = {"_ages": ages, "_revolutions": {"coast": rev}, "_rebuild": rebuild,
                 "_entities": {"villager": {"kind": 0, "tags": 128, "name": "Settler"}, "soldier": {"kind": 0, "tags": 3, "name": "Soldier", "revolutionary": True},
                               "tree": {"kind": 2, "resource": 2}, "crate_wood": {"kind": 2, "resource": 2}}}
        events = []
        for a in range(2, 5):
            events += [charge("Advance to age " + str(a), (a - 2) * 10, ages[a]["cost"]),
                       {"kind": "AgeAdvanced", "player": 0, "entity": 10, "time": (a - 1) * 10, "message": "Reached age " + str(a)}]
        events += [charge("Assembly", 30, rev["cost"]),
                   {"kind": "RevolutionQueued", "player": 0, "entity": 10, "time": 30, "message": "rev"},
                   {"kind": "WorkerConverted", "player": 0, "entity": 1, "time": 120, "message": "soldier"},
                   {"kind": "RevolutionCompleted", "player": 0, "entity": 10, "time": 120, "message": "rev", "amount": 1},
                   charge("Rebuild", 120, rebuild["cost"]),
                   {"kind": "CivilianRebuilt", "player": 0, "entity": 10, "time": 180},
                   {"kind": "ResearchComplete", "player": 0, "entity": 10, "time": 180, "message": "Rebuild"},
                   {"kind": "Spawn", "player": 0, "entity": 2, "time": 180, "message": "Settler"},
                   {"kind": "Gather", "player": 0, "entity": 2, "target": 3, "time": 182, "amount": 5, "message": "Wood"}]
        world = {"players": [{"state": {"age": 4, "politicalPhase": 2, "revolutionId": "rev", "revolutionAt": 120, "civilianRebuiltAt": 180},
                              "technologies": ["civilian_rebuilding"]}],
                 "entities": [{"definition": "town", "state": {"id": 10, "owner": 0, "hp": 100}},
                              {"definition": "soldier", "state": {"id": 1, "owner": 0, "hp": 100}},
                              {"definition": "villager", "state": {"id": 2, "owner": 0, "hp": 100}},
                              {"definition": "tree", "state": {"id": 3, "owner": -1, "hp": 100}}]}
        return rules, {"civilization": "coast", "requestedRoute": "revolution", "seed": 42, "routeReached": True}, world, events

    def test_route_requires_full_paid_lifecycle_birth_and_natural_gather(self):
        rules, case, world, events = self.route_data()
        reached, proof = recompute_route(rules, case, world, events)
        self.assertTrue(reached);self.assertEqual(5, proof["naturalIncomeAfterRebuilding"]["wood"])

    def test_same_boundary_birth_after_phase_completion_is_legitimate(self):
        rules, case, world, events = self.route_data()
        self.assertEqual(events[-2]["time"], events[-3]["time"])
        self.assertTrue(recompute_route(rules, case, world, events)[0])

    def test_result_flag_and_seed_cannot_replace_recovery(self):
        rules, case, world, events = self.route_data()
        events.pop()
        self.assertFalse(recompute_route(rules, case, world, events)[0])

    def test_crate_gather_is_not_restored_natural_economy(self):
        rules, case, world, events = self.route_data()
        world["entities"][3]["definition"] = "crate_wood"
        self.assertFalse(recompute_route(rules, case, world, events)[0])

    def test_no_actual_civilian_birth_rejects_gather_metadata(self):
        rules, case, world, events = self.route_data()
        events = [e for e in events if e["kind"] != "Spawn"]
        self.assertFalse(recompute_route(rules, case, world, events)[0])

    def test_zero_gather_does_not_recover_economy(self):
        rules, case, world, events = self.route_data()
        events[-1]["amount"] = 0
        self.assertFalse(recompute_route(rules, case, world, events)[0])

    def test_worker_death_before_gather_cannot_prove_recovery(self):
        rules, case, world, events = self.route_data()
        events.insert(-1, {"kind": "Death", "player": 0, "entity": 2, "time": 181})
        self.assertFalse(recompute_route(rules, case, world, events)[0])

    def test_worker_can_die_after_honest_natural_income(self):
        rules, case, world, events = self.route_data()
        events.append({"kind": "Death", "player": 0, "entity": 2, "time": 183})
        world["entities"][2]["state"].update(dead=True, hp=0)
        self.assertTrue(recompute_route(rules, case, world, events)[0])

    def test_unpaid_rebuilding_cannot_use_newborn_and_gather(self):
        rules, case, world, events = self.route_data()
        events = [e for e in events if not e.get("message", "").startswith("Rebuild:")]
        self.assertFalse(recompute_route(rules, case, world, events)[0])

    def test_empire_needs_paid_age_five_and_previous_ages(self):
        rules, case, world, events = self.route_data()
        events = events[:6]
        payment = self.payment(30);payment["entity"] = 10;payment["message"] = "Advance to age 5: cost"
        payment["economy"] = {"requestedDelta": {"food": -100, "wood": 0, "coin": 0}, "actualDelta": {"food": -100, "wood": 0, "coin": 0},
                              "balanceBefore": {"food": 1000, "wood": 0, "coin": 0}, "balanceAfter": {"food": 900, "wood": 0, "coin": 0}}
        events += [payment, {"kind": "AgeAdvanced", "player": 0, "entity": 10, "time": 40, "message": "Reached age 5"}]
        case["requestedRoute"] = "empire";world["players"][0]["state"].update(age=5, politicalPhase=0)
        self.assertTrue(recompute_route(rules, case, world, events)[0])
        events.pop(-2)
        self.assertFalse(recompute_route(rules, case, world, events)[0])

    def ordinary_fixture(self):
        rules, case, world, events = self.route_data()
        rules.update(_civs={"coast": {"id": "coast", "startingResources": {"food": 300, "wood": 300, "coin": 100}}},
                     _opening_defaults={"startingWorkers": 6, "workerUnit": "villager"})
        points = [{"x": 12 + n, "y": 18} for n in range(6)]
        starts = [{"player": p, "team": p, "civilization": "coast", "age": 1, "initialResources": {"food": 300, "wood": 300, "coin": 100},
                   "workers": points} for p in range(2)]
        doc = {"id": "traders_bend070", "seed": 42, "enableElimination": True, "rulesHash": "a" * 64, "starts": starts,
               "objects": [], "resources": [], "objectives": [], "triggers": []}
        original_ids = [1, 4, 5, 6, 7, 8]
        initial = []
        bindings = []
        for p in range(2):
            initial.append({"kind": "StartingResources", "player": p, "entity": 0, "time": 0,
                            "message": "Authored scenario opening: 300 F / 300 W / 100 C"})
            for n, point in enumerate(points):
                unit = original_ids[n] if p == 0 else 12 + n
                initial.append({"kind": "Spawn", "player": p, "entity": unit, "time": 0, "message": "Settler", "position": point})
                bindings.append({"objectId": f"start-{p}-worker-{n}", "entityIds": [unit]})
                if unit not in {e["state"]["id"] for e in world["entities"]}:
                    world["entities"].append({"definition": "soldier" if p == 0 else "villager", "state": {"id": unit, "owner": p, "hp": 100}})
        events = initial + events
        index = next(i for i, e in enumerate(events) if e["kind"] == "RevolutionCompleted")
        events[index]["amount"] = len(original_ids)
        events[index:index] = [{"kind": "WorkerConverted", "player": 0, "entity": unit, "time": 120, "message": "soldier"} for unit in original_ids[1:]]
        simulation = {"difficultyId": "standard", "controllers": [{"player": 1}, {"player": 0}], "stepCount": 2000}
        world.update(time=float_clock(2000), winner=-1, seed=42, maxAge=5, paused=False, hasScenarioDocument=True, mapId="custom_scenario",
                     scenarioDocument=doc, rulesHash="a" * 64, scenarioState={"bindings": bindings}, events=events)
        world["players"][0]["state"].update(id=0, team=0, civilization="coast")
        world["players"].append({"state": {"id": 1, "team": 1, "civilization": "coast"}})
        case.update(id="coast-revolution", status="route_reached", seconds=world["time"], winner=-1, naturalTerminal=False,
                    startingWorkers=6, startingResources={"food": 300, "wood": 300, "coin": 100}, age=4, phase=2)
        return rules, case, world, events, simulation, doc

    def inspect_ordinary_fixture(self, data):
        rules, case, world, events, simulation, doc = data
        payload = json.dumps(doc, separators=(",", ":"))
        digest = hashlib.sha256(payload.encode()).hexdigest()
        world["scenarioHash"] = digest
        with tempfile.TemporaryDirectory(prefix="synthetic-route-audit071-", dir=Path(__file__).parent) as directory:
            directory = Path(directory)
            for filename, value in (("result.json", case), ("opening-scenario.json", {"schema": 1, "encoding": "frontier-scenario-json-v1", "payload": payload, "sha256": digest}),
                                    ("final-world.json", world), ("final-simulation.json", simulation)):
                (directory / filename).write_text(json.dumps(value))
            (directory / "events.jsonl").write_text("\n".join(json.dumps(e) for e in events) + "\n")
            (directory / "snapshots.jsonl").write_text("\n".join(json.dumps({"time": .1, "player": p, "age": 1, "phase": 0}) for p in range(2)) + "\n")
            return inspect_case(rules, case, {"rulesHash": "a" * 64}, directory, {})

    def test_actual_opening_envelope_event_births_and_scheduler_are_checked(self):
        result = self.inspect_ordinary_fixture(self.ordinary_fixture())
        self.assertEqual("route_reached", result["verdict"]);self.assertFalse(result["issues"])

    def test_declared_extra_money_is_not_an_ordinary_opening(self):
        data = self.ordinary_fixture();data[-1]["starts"][0]["initialResources"]["food"] = 10000
        with self.assertRaises(AuditError):self.inspect_ordinary_fixture(data)

    def test_declared_extra_workers_are_not_an_ordinary_opening(self):
        data = self.ordinary_fixture();data[-1]["starts"][0]["workers"].append({"x": 20, "y": 20})
        with self.assertRaises(AuditError):self.inspect_ordinary_fixture(data)

    def test_wrong_actual_scheduler_clock_is_rejected(self):
        data = self.ordinary_fixture();data[4]["stepCount"] += 10
        with self.assertRaises(AuditError):self.inspect_ordinary_fixture(data)

    def test_actual_opening_spawn_position_must_match(self):
        data = self.ordinary_fixture();event = next(e for e in data[3] if e["kind"] == "Spawn")
        event["position"] = {"x": 80, "y": 80}
        with self.assertRaises(AuditError):self.inspect_ordinary_fixture(data)

    def test_early_terminal_failure_is_retained(self):
        data = self.ordinary_fixture();data[3].pop();data[2]["winner"] = 1
        data[1].update(winner=1, naturalTerminal=True, routeReached=False, status="government_rebuilt_economy_not_recovered")
        result = self.inspect_ordinary_fixture(data)
        self.assertEqual("civilian_economy_recovery_incomplete", result["verdict"])
        self.assertTrue(result["naturalTerminal"]);self.assertFalse(result["independentRouteReached"])

    def test_timeout_failure_is_retained(self):
        data = list(self.ordinary_fixture());data[3] = [e for e in data[3] if e["time"] <= 120]
        data[2]["events"] = data[3]
        data[2]["players"][0]["state"].update(politicalPhase=1, civilianRebuiltAt=0)
        data[4]["stepCount"] = 24001;data[2]["time"] = float_clock(24001)
        data[1].update(seconds=data[2]["time"], phase=1, routeReached=False, status="route_not_reached_at_limit")
        result = self.inspect_ordinary_fixture(data)
        self.assertEqual("route_not_reached_at_time_limit", result["verdict"])
        self.assertFalse(result["independentRouteReached"])

    def test_route_flag_cannot_hide_missing_gather(self):
        data = self.ordinary_fixture();data[3].pop()
        result = self.inspect_ordinary_fixture(data)
        self.assertFalse(result["independentRouteReached"]);self.assertTrue(result["issues"])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--self-test", action="store_true")
    parser.add_argument("--evidence-dir", type=Path)
    parser.add_argument("--repo", type=Path)
    parser.add_argument("--source-before", type=Path)
    parser.add_argument("--source-after", type=Path)
    parser.add_argument("--expected-source-fingerprint")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    if args.self_test:
        return 0 if unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(SyntheticChecks)).wasSuccessful() else 1
    need(all(getattr(args, k) is not None for k in ("evidence_dir", "repo", "source_before", "source_after", "expected_source_fingerprint", "output")), "all formal inputs are required")
    need(not args.output.exists(), "preserve existing independent receipt; select a new output path")
    report = audit(args)
    with args.output.open("x", encoding="utf-8") as stream:
        json.dump(report, stream, ensure_ascii=False, indent=2, allow_nan=False);stream.write("\n")
    print(json.dumps({k: report[k] for k in ("status", "caseCount", "independentlyReached", "invalidEvidenceCases", "sourceFingerprint")}))
    return 2 if report["status"] == "evidence_invalid" else 3 if report["status"] == "observed_routes_incomplete" else 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError, KeyError, TypeError) as error:
        print("Cannot independently audit ordinary routes: " + str(error), file=sys.stderr)
        sys.exit(2)
