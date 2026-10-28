#!/usr/bin/env python3
"""Offline, descriptive economics analysis of a CLOSED NormalRoutes072 matrix.

No Unity, subprocess, network, observer imports, simulation or resource grants.
Reads events/samples one JSONL record at a time; never opens final-world's ledger.
Writes only new .json/.md files directly beside this script in 072 Diagnostics.
See analyze-normal072-usage.md for the evidence contract and limitations.
"""
import argparse
from collections import Counter, defaultdict
from datetime import datetime, timezone
import hashlib
import json
import math
import os
from pathlib import Path
import stat
import sys

HERE = Path(__file__).absolute().parent
RESOURCES = ("food", "wood", "coin")
FACILITIES = ("town", "trade_post", "bank", "estate", "factory")
EXPECTED = [(m, c, s) for m, seeds in (("traders_bend070", (42, 43)), ("winding", (44, 45)))
            for c in ("coast", "river", "france", "ottoman") for s in seeds]
EPS = .002


def require(ok, message):
    if not ok:
        raise ValueError(message)


def unique_pairs(items):
    result = {}
    for key, value in items:
        require(key not in result, "Duplicate JSON property: " + key)
        result[key] = value
    return result


def decode(raw):
    return json.loads(raw.decode("utf-8-sig"), object_pairs_hook=unique_pairs,
                      parse_constant=lambda value: (_ for _ in ()).throw(ValueError("Nonfinite JSON: " + value)))


def numeric(value, label):
    require(type(value) in (int, float) and math.isfinite(value), "Not a finite number: " + label)
    return value


def cost(value):
    require(isinstance(value, dict), "Missing Cost object")
    return {r: numeric(value.get(r, 0), "Cost." + r) for r in RESOURCES}


def add(a, b):
    return {r: a[r] + b[r] for r in RESOURCES}


def zero():
    return dict.fromkeys(RESOURCES, 0.0)


def identity(value):
    return (value.st_dev, value.st_ino, value.st_mode, value.st_size, value.st_mtime_ns, value.st_ctime_ns)


class Inputs:
    """SHA while reading, plus stat identity checks; no unsigned-proof claims."""
    def __init__(self):
        self.records = {}

    def stream(self, path):
        path = Path(path).absolute()
        require(path.parent.resolve(strict=True) == path.parent, "Symlink input ancestry: " + str(path))
        before = path.lstat()
        require(stat.S_ISREG(before.st_mode), "Input is not an ordinary file: " + str(path))
        handle = os.fdopen(os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)), "rb")
        require(identity(os.fstat(handle.fileno())) == identity(before), "Input changed before open")
        return path, handle, before

    def finish(self, path, handle, before, size, sha):
        require(identity(os.fstat(handle.fileno())) == identity(before)
                and identity(path.lstat()) == identity(before), "Input changed while read: " + str(path))
        row = {"path": str(path), "bytes": size, "sha256": sha, "statIdentity": list(identity(before))}
        old = self.records.get(str(path))
        require(old is None or old == row, "Input differs between reads: " + str(path))
        self.records[str(path)] = row

    def raw(self, path):
        path, handle, before = self.stream(path)
        with handle:
            raw = handle.read()
            self.finish(path, handle, before, len(raw), hashlib.sha256(raw).hexdigest())
        return raw

    def document(self, path):
        return decode(self.raw(path))

    def lines(self, path):
        path, handle, before = self.stream(path)
        digest, size = hashlib.sha256(), 0
        with handle:
            for line, raw in enumerate(handle, 1):
                require(raw.strip(), f"Blank JSONL record: {path}:{line}")
                digest.update(raw); size += len(raw)
                yield line, decode(raw)
            self.finish(path, handle, before, size, digest.hexdigest())

    def unchanged(self):
        for row in self.records.values():
            require(identity(Path(row["path"]).lstat()) == tuple(row["statIdentity"]),
                    "Input changed after reading: " + row["path"])


class Catalog:
    def __init__(self, raw):
        self.entities = {e["id"]: e for e in raw["entities"]}
        self.names = defaultdict(list)
        for entity in self.entities.values():
            self.names[entity["name"]].append(entity["id"])
        self.techs = {t["id"]: t for t in raw["techs"]}
        self.cards = defaultdict(list)
        for card in raw["cards"]:
            self.cards[card["name"]].append(card)
        self.revolutions = {r["name"]: r for r in raw.get("revolutions", [])}

    def definition_from_name(self, name):
        ids = self.names.get(name, [])
        require(len(ids) == 1, "Unrecognized/ambiguous entity event name: " + name)
        return ids[0]

    def card_from_name(self, name, civilization):
        matches = [card for card in self.cards.get(name, []) if card.get("civilization", "") in ("", civilization)]
        require(len(matches) == 1, "Unrecognized/ambiguous civilization shipment name: " + name)
        return matches[0]

    def category(self, definition):
        d = self.entities[definition]
        tags = d.get("tags", 0)
        if tags & 128:
            return "workers"  # Matches observer Worker, including any waterborne Villager.
        military = d.get("kind") == 0 and not d.get("civilian", False)
        if military and not d.get("waterborne", False) and not tags & 512:
            return "landSoldiers"
        if military and d.get("waterborne", False):
            return "navalMilitary"
        if military and tags & 512:
            return "explorers"
        return "other"


def alive(row):
    return not row["state"].get("dead", False) and row["state"].get("hp", 0) > 0


def ref(event, line):
    return {"time": event["time"], "eventLine": line, "kind": event["kind"],
            "entity": event.get("entity", 0), "target": event.get("target", 0),
            "message": event.get("message", "")}


class Gap:
    """Maximum interval between positive LOGGED income events, with censoring."""
    def __init__(self, end):
        self.end, self.last, self.count = end, 0.0, 0
        self.total, self.first, self.best = 0.0, None, None

    def consider(self, start, end, left, right):
        value = {"start": start, "end": end, "seconds": end - start,
                 "leftBoundary": left, "rightBoundary": right}
        if self.best is None or value["seconds"] > self.best["seconds"]:
            self.best = value

    def observe(self, time, amount):
        if time > self.end + .000001:
            return
        self.consider(self.last, time, "opening" if self.count == 0 else "positive_logged_income",
                      "positive_logged_income")
        self.last, self.count, self.total = time, self.count + 1, self.total + amount
        if self.first is None:
            self.first = time

    def finish(self):
        self.consider(self.last, self.end, "opening" if self.count == 0 else "positive_logged_income",
                      "observation_end")
        return {"positiveLoggedEvents": self.count, "positiveLoggedAmount": self.total,
                "firstPositiveLoggedAt": self.first, "lastPositiveLoggedAt": self.last if self.count else None,
                "longestNoPositiveLoggedEventInterval": self.best,
                "timeMeaning": "Recorded ledger timestamps; Gather/Bank/Factory income is buffered, normally flushed per source about once per second and at source switches/removal/end. This is not an exact no-production duration."}


def snapshot_summary(world, catalog, label):
    """Vision-only diagnostic totals at actual eventless snapshot boundaries."""
    require(isinstance(world.get("visibility"), list), "Snapshot lacks visibility")
    width = world["landNavigation"]["width"]; height = world["landNavigation"]["height"]
    players = [p["state"] for p in world["players"]]
    require(len(world["visibility"]) == len(players) * width * height, "Unexpected visibility shape")
    summaries = []
    for player in players:
        pid = player["id"]
        counts = Counter(catalog.category(e["definition"]) for e in world["entities"] if alive(e) and e["state"]["owner"] == pid)
        wood, ids = 0.0, []
        for entity in world["entities"]:
            d, state = catalog.entities[entity["definition"]], entity["state"]
            if not alive(entity) or state.get("garrisonedIn", 0) or d.get("resource", 0) != 2:
                continue
            if d.get("kind") == 1 or d.get("waterborne", False) or d["id"].startswith("crate_") or state.get("remaining", 0) <= 0:
                continue
            point = state["position"]; x, y = point["x"], point["y"]
            visible = state["owner"] == pid
            if 0 <= x < width and 0 <= y < height:
                # Mathf.RoundToInt uses ties-to-even, as Python round does.
                index = pid * width * height + min(width - 1, round(x)) * height + min(height - 1, round(y))
                visible = visible or world["visibility"][index] == 2
            if visible:
                wood += state["remaining"]; ids.append(state["id"])
        summaries.append({"player": pid, "time": world["time"], "boundary": label,
                          "age": player["age"], "phase": player.get("politicalPhase", 0),
                          "workers": counts["workers"], "soldiers": counts["landSoldiers"],
                          "resources": cost(player["resources"]), "gathered": cost(player["gathered"]),
                          "passive": cost(player["passiveIncome"]), "trade": cost(player["tradeIncome"]),
                          "rawFiniteWoodInCurrentVision": {"amount": wood, "entityIds": ids,
                              "meaning": "Offline snapshot diagnostic only. Current visibility/ownership is filtered; safety, accessible worker, CanGather and path eligibility are NOT proved. This is NOT planner VisibleWoodRemaining, remembered intelligence, global exhaustion or future AI knowledge."}})
    return summaries


def sample_income_intervals(rows, trade_events, end):
    """Cumulative deltas, independent of spending. Endpoint accuracy is explicit."""
    rows = sorted(rows, key=lambda row: row["time"])
    trade_total, cursor, output = zero(), 0, {}
    valued = []
    for row in rows:
        if row["time"] > end + .000001:
            continue
        while cursor < len(trade_events) and trade_events[cursor][0] <= row["time"] + .000001:
            trade_total = add(trade_total, trade_events[cursor][1]); cursor += 1
        # Snapshot rows carry the exact cumulative trade ledger; samples do not.
        income = add(add(row["gathered"], row["passive"]), row.get("trade", trade_total))
        if valued and abs(row["time"] - valued[-1][0]) <= .000001:
            require(all(abs(income[r] - valued[-1][1][r]) <= .1 for r in RESOURCES), "Duplicate endpoint income differs")
            continue
        valued.append((row["time"], income, row.get("boundary", "30_second_sample")))
    require(valued and abs(valued[0][0]) <= EPS and abs(valued[-1][0] - end) <= EPS,
            "Income interval endpoints are missing")
    for resource in RESOURCES:
        best, start, intervals, positive_bins = None, None, [], 0
        for index in range(1, len(valued)):
            ta, a, ba = valued[index - 1]; tb, b, bb = valued[index]
            delta = b[resource] - a[resource]
            require(delta >= -.1, "Cumulative earned income decreased")
            if delta > .0001:
                positive_bins += 1
                start = None; intervals = []
            else:
                if start is None:
                    start = ta; intervals = []
                intervals.append({"start": ta, "end": tb, "startBoundary": ba, "endBoundary": bb})
                candidate = {"start": start, "end": tb, "observedFlatSeconds": tb - start,
                             "intervals": list(intervals)}
                if best is None or candidate["observedFlatSeconds"] > best["observedFlatSeconds"]:
                    best = candidate
        output[resource] = {"positiveIncomeBins": positive_bins, "longestObservedFlatCumulativeInterval": best,
                            "totalEarnedAtEnd": valued[-1][1][resource],
                            "resolution": "Normally approximately 30 seconds, plus actual opening/comparison/terminal endpoints. Flat bins describe recorded cumulative gathered+passive+trade income. Onset/return inside adjacent positive bins is unknown; a bin containing positive income is not continuously active."}
    output["largestActualEndpointSpacingSeconds"] = max((valued[i][0] - valued[i - 1][0] for i in range(1, len(valued))), default=0)
    return output


class CaseAnalysis:
    def __init__(self, item, opening, projection, catalog):
        self.item, self.catalog = item, catalog
        self.ends = {"wholeObservation": item["seconds"], "comparisonBoundary": item["comparisonAt"]}
        self.entities = {}
        self.opening_ids = set()
        self.teams = {p["state"]["id"]: p["state"]["team"] for p in opening["players"]}
        require(set(self.teams) == {0, 1}, "Expected exactly two ordinary players")
        self.players = {}
        self.pending = []
        self.last_time, self.event_count = -1.0, 0
        self.known_spawn_events = set()
        self.opening = snapshot_summary(opening, catalog, "opening_world")
        self.comparison = snapshot_summary(projection, catalog, "actual_2400_projection" if item["comparisonReached"] else "actual_early_terminal_projection")
        for pid in self.teams:
            self.players[pid] = {"player": pid, "civilization": opening["players"][pid]["state"]["civilization"], "facilities": {}, "ledger": {"researchPayments": [], "researchCompletions": [], "activations": [], "shipmentSent": [], "shipmentArrived": []},
                "factoryEntitlements": {"shipmentSent": [], "shipmentArrived": [], "wagonConsumed": []},
                "life": {"openingAlive": Counter(), "spawnedAfterOpening": Counter(), "produced": defaultdict(Counter),
                         "deaths": Counter(), "enemyDeaths": Counter(), "conversions": [], "deathRecords": []},
                "townEnemyLosses": [], "townEnemyDamage": [], "townRecoveryPayments": [],
                "positiveIncomeFirsts": {}, "incomeTotals": defaultdict(zero), "tradeEvents": [],
                "sampleRows": [self.opening[pid]], "political": {"payments": [], "completed": [], "terminalQueues": []},
                "gaps": {w: {r: Gap(end) for r in RESOURCES} for w, end in self.ends.items()},
                "channelGaps": {w: {r: Gap(end) for r in ("wood", "coin")} for w, end in self.ends.items()},
                "naturalWorkerFirsts": {}, "lastSample": None, "sampleCount": 0}
        for row in opening["entities"]:
            state, definition = row["state"], row["definition"]
            require(definition in catalog.entities, "Unknown opening definition")
            self.entities[state["id"]] = {"owner": state["owner"], "definition": definition, "alive": alive(row)}
            self.opening_ids.add(state["id"])
            if state["owner"] in self.players and alive(row):
                p = self.players[state["owner"]]
                p["life"]["openingAlive"][catalog.category(definition)] += 1
                if definition in FACILITIES:
                    self.facility(state["owner"], state["id"], definition, 0, "opening", state.get("construction", 0) >= 1)
        for pid, row in enumerate(opening["players"]):
            if "cargo_ledgers" in row.get("technologies", []):
                self.players[pid]["ledger"]["activations"].append({"time": 0, "kind": "opening_technology", "eventLine": None})

    def hostile(self, a, b):
        return a in self.teams and b in self.teams and a != b and self.teams[a] != self.teams[b]

    def facility(self, pid, eid, definition, time, origin, complete=False):
        rows = self.players[pid]["facilities"]
        if eid not in rows:
            rows[eid] = {"entity": eid, "definition": definition, "spawnedAt": time, "origin": origin,
                         "payment": None, "completedAt": time if complete else None, "completionEvent": None,
                         "death": None, "firstPositiveWood": None, "firstPositiveCoin": None}
        return rows[eid]

    def observe(self, event, line):
        time = numeric(event.get("time"), "event time")
        require(time + .000001 >= self.last_time and -.000001 <= time <= self.item["seconds"] + EPS, "Event time order/range is invalid")
        self.last_time, self.event_count = time, self.event_count + 1
        kind = event["kind"]; pid, eid, target = event.get("player", -1), event.get("entity", 0), event.get("target", 0)
        p = self.players.get(pid); evidence = ref(event, line)
        if kind == "Spawn":
            require(eid not in self.known_spawn_events, "Duplicate Spawn event identity")
            self.known_spawn_events.add(eid)
            definition = self.catalog.definition_from_name(event["message"])
            if eid in self.entities:
                require(eid in self.opening_ids and time <= EPS and self.entities[eid]["definition"] == definition
                        and self.entities[eid]["owner"] == pid, "Spawn duplicates non-opening entity")
            else:
                self.entities[eid] = {"owner": pid, "definition": definition, "alive": True}
                if p:
                    p["life"]["spawnedAfterOpening"][self.catalog.category(definition)] += 1
                    if definition in FACILITIES:
                        self.facility(pid, eid, definition, time, "spawn_event")
            matching = [entry for entry in self.pending if entry["player"] == pid and entry["definition"] == definition and abs(entry["time"] - time) <= .000001]
            require(len(matching) <= 1, "Ambiguous foundation payment/Spawn binding")
            if matching:
                payment = matching[0]; self.pending.remove(payment)
                row = self.facility(pid, eid, definition, time, "paid_normal_foundation") if definition in FACILITIES else None
                if row is not None:
                    row["payment"] = payment
                if definition == "town" and payment["townMissingBeforePayment"]:
                    payment["buildingEntity"] = eid
                    p["townRecoveryPayments"].append(payment)
                    payment["associatedEnemyLossEventLine"] = next((loss["eventLine"] for loss in reversed(p["townEnemyLosses"]) if loss["lastAliveTownLost"]), None)
            return
        entity = self.entities.get(eid)
        if kind == "Payment" and p:
            require(event.get("hasEconomy") is True, "Payment lacks explicit economy transaction")
            transaction = event["economy"]
            request, actual = cost(transaction["requestedDelta"]), cost(transaction["actualDelta"])
            require(all(request[r] <= .0001 and actual[r] <= .002 for r in RESOURCES), "Payment is not a debit")
            payment = dict(evidence, requestedCost={r: -request[r] for r in RESOURCES},
                           actualDebit={r: -actual[r] for r in RESOURCES}, balanceBefore=cost(transaction["balanceBefore"]), balanceAfter=cost(transaction["balanceAfter"]))
            reason = event["message"].split(": ", 1)[0]
            if reason.startswith("Foundation "):
                definition = self.catalog.definition_from_name(reason[len("Foundation "):])
                payment.update(player=pid, definition=definition, builderEntity=eid,
                               townMissingBeforePayment=not any(e["alive"] and e["owner"] == pid and e["definition"] == "town" for e in self.entities.values()))
                self.pending.append(payment)
            if reason == self.catalog.techs["cargo_ledgers"]["name"]:
                p["ledger"]["researchPayments"].append(payment)
            if reason == "Advance to age 5" or reason in self.catalog.revolutions or reason == self.catalog.techs["civilian_rebuilding"]["name"]:
                p["political"]["payments"].append(payment)
        elif kind == "BuildingComplete" and p and entity and entity["definition"] in FACILITIES:
            row = self.facility(pid, eid, entity["definition"], time, "completion_event")
            require(row["completedAt"] is None, "Duplicate completion of tracked facility")
            row.update(completedAt=time, completionEvent=evidence)
        elif kind == "ResearchComplete" and p and event["message"] == self.catalog.techs["cargo_ledgers"]["name"]:
            p["ledger"]["researchCompletions"].append(evidence)
        elif kind == "Technology" and p and event["message"].startswith("cargo_ledgers applied once"):
            p["ledger"]["activations"].append(evidence)
        elif kind in ("ShipmentSent", "ShipmentArrived") and p:
            name = event["message"] if kind == "ShipmentSent" else event["message"].removesuffix(" arrived")
            card = self.catalog.card_from_name(name, p["civilization"])
            key = "shipmentSent" if kind == "ShipmentSent" else "shipmentArrived"
            if card.get("tech") == "cargo_ledgers":
                p["ledger"][key].append(dict(evidence, card=card["id"]))
            if card.get("unit") == "factory_wagon":
                p["factoryEntitlements"][key].append(dict(evidence, card=card["id"], count=card.get("count", 0)))
        elif kind == "FactoryFoundationStarted" and p:
            require(entity and entity["definition"] == "factory" and target in self.entities
                    and self.entities[target]["definition"] == "factory_wagon", "Factory consumed-wagon link is invalid")
            p["factoryEntitlements"]["wagonConsumed"].append(evidence)
            self.facility(pid, eid, "factory", time, "consumed_shipment_wagon")["wagonConsumedEvent"] = evidence
        elif kind == "Damage" and target in self.entities:
            victim = self.entities[target]
            if victim["definition"] == "town" and self.hostile(pid, victim["owner"]) and event.get("amount", 0) > 0:
                self.players[victim["owner"]]["townEnemyDamage"].append(dict(evidence, attackerPlayer=pid,
                                                                            townEntity=target, appliedAmount=event["amount"]))
        elif kind == "Death" and entity:
            require(entity["alive"], "Duplicate Death or initially dead entity")
            category = self.catalog.category(entity["definition"])
            entity["alive"] = False
            owner = entity["owner"]
            if owner in self.players:
                q = self.players[owner]
                enemy = self.hostile(target, owner)
                q["life"]["deaths"][category] += 1
                if enemy:
                    q["life"]["enemyDeaths"][category] += 1
                death = dict(evidence, definition=entity["definition"], category=category,
                             creditedKillerPlayer=target, hostileCreditedKill=enemy,
                             reason="hostile_credited_death" if enemy else "uncredited_or_noncombat_removal")
                q["life"]["deathRecords"].append(death)
                if entity["definition"] in FACILITIES:
                    self.facility(owner, eid, entity["definition"], time, "death_event")["death"] = death
                if entity["definition"] == "town" and enemy:
                    lethal = next((damage for damage in reversed(q["townEnemyDamage"]) if damage["townEntity"] == eid
                                   and damage["attackerPlayer"] == target and abs(damage["time"] - time) <= .000001), None)
                    require(lethal is not None, "Credited enemy Town death has no actual lethal Damage event")
                    q["townEnemyLosses"].append(dict(death, lethalDamageEventLine=lethal["eventLine"],
                        lastAliveTownLost=not any(e["alive"] and e["owner"] == owner and e["definition"] == "town" for e in self.entities.values())))
        elif kind == "UnitDisbanded" and p:
            previous = next((death for death in reversed(p["life"]["deathRecords"]) if death["entity"] == eid and abs(death["time"] - time) <= .000001), None)
            require(previous is not None and not previous["hostileCreditedKill"], "Disband lacks noncombat removal")
            previous["reason"] = "disband_without_refund"; previous["disbandEventLine"] = line
        elif kind == "WorkerConverted" and p:
            require(entity and entity["alive"] and self.catalog.category(entity["definition"]) == "workers"
                    and event["message"] in self.catalog.entities, "Invalid worker conversion identity")
            p["life"]["conversions"].append(dict(evidence, fromDefinition=entity["definition"], toDefinition=event["message"]))
            entity["definition"] = event["message"]
        elif kind in ("UnitTrained", "AutomaticWorkerProduced", "FactoryArtilleryProduced") and p:
            produced = self.entities.get(target)
            require(produced and produced["owner"] == pid and produced["definition"] == event["message"] and produced["alive"], "Production event is not linked to its actual spawned unit")
            p["life"]["produced"][kind][self.catalog.category(produced["definition"])] += 1
        if kind in ("AgeAdvanced", "RevolutionCompleted", "CivilianRebuilt") and p:
            p["political"]["completed"].append(evidence)
        if p:
            self.income(event, line, p)

    def income(self, event, line, p):
        kind = event["kind"]; amount = numeric(event.get("amount", 0), "income amount")
        if amount <= 0:
            return
        resource = event.get("message", "").lower()
        if kind == "CaravanDelivered":
            resource = "coin"
        if kind not in ("Gather", "BankIncome", "FactoryIncome", "TradeDelivery", "CaravanDelivered") or resource not in RESOURCES:
            return  # XP, refunds, grants and wallet exchange are not earned F/W/C.
        time, delta = event["time"], zero(); delta[resource] = amount
        p["incomeTotals"][kind][resource] += amount
        p["positiveIncomeFirsts"].setdefault(resource, ref(event, line))
        for window in self.ends:
            p["gaps"][window][resource].observe(time, amount)
        if kind in ("TradeDelivery", "CaravanDelivered"):
            p["tradeEvents"].append((time, delta))
        source_id = event.get("target", 0) if kind == "Gather" else event.get("entity", 0)
        source = self.entities.get(source_id)
        source_def = source["definition"] if source else None
        if kind == "Gather":
            worker = self.entities.get(event.get("entity", 0))
            if worker and self.catalog.category(worker["definition"]) == "workers" and not self.catalog.entities[worker["definition"]].get("waterborne", False) and source_def and not source_def.startswith("crate_"):
                p["naturalWorkerFirsts"].setdefault(resource, dict(ref(event, line), sourceDefinition=source_def))
        if source_def in ("trade_post", "bank", "estate", "factory") and resource in ("wood", "coin"):
            evidence = dict(ref(event, line), amount=amount, sourceDefinition=source_def)
            row = self.facility(p["player"], source_id, source_def, time, "income_event")
            key = "firstPositiveWood" if resource == "wood" else "firstPositiveCoin"
            if row[key] is None:
                row[key] = evidence
            for window in self.ends:
                p["channelGaps"][window][resource].observe(time, amount)

    def samples(self, inputs, path):
        last = {pid: -1.0 for pid in self.players}
        for line, sample in inputs.lines(path):
            pid = sample.get("player"); require(pid in self.players, "Unknown sample player")
            time = numeric(sample.get("time"), "sample time")
            require(last[pid] <= time <= self.item["seconds"] + EPS, "Sample time order/range differs")
            last[pid] = time; p = self.players[pid]
            row = {key: sample[key] for key in ("time", "player", "workers", "soldiers", "age", "phase")}
            for key in ("resources", "spent", "gathered", "passive"):
                row[key] = cost(sample[key])
            row["sampleLine"] = line
            p["sampleRows"].append(row); p["lastSample"] = row; p["sampleCount"] += 1
            queues = []
            for building in sample["buildings"]:
                for job in building.get("queue", []):
                    queues.append({"entity": building["id"], "definition": building["definition"],
                                   "complete": building["complete"], **job})
            p["political"]["terminalQueues"] = queues
        for pid, p in self.players.items():
            require(p["lastSample"] and abs(p["lastSample"]["time"] - self.item["seconds"]) <= EPS,
                    "Missing actual terminal sample for player " + str(pid))
            p["sampleRows"].append(self.comparison[pid])

    def finish(self):
        require(not self.pending and self.event_count > 0, "Missing events or unbound actual foundation payments")
        result = []
        for pid, p in self.players.items():
            counts = Counter(self.catalog.category(e["definition"]) for e in self.entities.values() if e["alive"] and e["owner"] == pid)
            require(counts["workers"] == p["lastSample"]["workers"] and counts["landSoldiers"] == p["lastSample"]["soldiers"],
                    "Event-time workers/soldiers differ from actual terminal sample")
            p["life"]["terminalAlive"] = counts
            initial = p["life"]["openingAlive"]; spawned = p["life"]["spawnedAfterOpening"]; dead = p["life"]["deaths"]
            converted_out, converted_in = Counter(), Counter()
            for conversion in p["life"]["conversions"]:
                converted_out[self.catalog.category(conversion["fromDefinition"])] += 1
                converted_in[self.catalog.category(conversion["toDefinition"])] += 1
            for category in set(initial) | set(spawned) | set(dead) | set(counts) | set(converted_in) | set(converted_out):
                require(initial[category] + spawned[category] + converted_in[category] - converted_out[category] - dead[category] == counts[category], "Life accounting differs: " + category)
            facilities = sorted(p["facilities"].values(), key=lambda row: row["entity"])
            firsts = {}
            for definition in FACILITIES:
                rows = [row for row in facilities if row["definition"] == definition]
                firsts[definition] = {"firstNormalResourcePaymentAt": min((row["payment"]["time"] for row in rows if row["payment"]), default=None),
                                     "firstCompletedAt": min((row["completedAt"] for row in rows if row["completedAt"] is not None), default=None),
                                     "firstPositiveWoodAt": min((row["firstPositiveWood"]["time"] for row in rows if row["firstPositiveWood"]), default=None),
                                     "firstPositiveCoinAt": min((row["firstPositiveCoin"]["time"] for row in rows if row["firstPositiveCoin"]), default=None)}
            firsts["factory"]["resourcePaymentMeaning"] = "Factory deployment consumes a legally shipped wagon; no normal resource Foundation payment is fabricated. See entitlement events."
            recovery = []
            for payment in p["townRecoveryPayments"]:
                row = p["facilities"][payment["buildingEntity"]]
                recovery.append({"payment": payment, "completedAt": row["completedAt"], "death": row["death"],
                                 "status": "completed" if row["completedAt"] is not None else "paid_but_not_completed",
                                 "enemyLossLink": payment["associatedEnemyLossEventLine"]})
            windows = {}
            for window, end in self.ends.items():
                rows = p["sampleRows"] if window == "wholeObservation" else [row for row in p["sampleRows"] if row["time"] <= end + .000001]
                windows[window] = {"actualEndSeconds": end, "loggedEarnedIncome": {r: gap.finish() for r, gap in p["gaps"][window].items()},
                    "loggedAlternativeChannelIncome": {r: gap.finish() for r, gap in p["channelGaps"][window].items()},
                    "sampledCumulativeEarnedIncome": sample_income_intervals(rows, p["tradeEvents"], end)}
            result.append({"player": pid, "opening": self.opening[pid], "comparison": self.comparison[pid],
                "terminalSample": p["lastSample"], "sampleCount": p["sampleCount"],
                "facilityFirsts": firsts, "facilities": facilities, "cargoLedgers": p["ledger"],
                "factoryEntitlements": p["factoryEntitlements"], "townEnemyDamage": p["townEnemyDamage"],
                "townEnemyLosses": p["townEnemyLosses"], "normalTownRecoveries": recovery, "lifeAccounting": p["life"],
                "firstPositiveResourceIncome": p["positiveIncomeFirsts"], "firstPositiveOrdinaryLandWorkerIncome": p["naturalWorkerFirsts"],
                "loggedIncomeTotalsByChannel": p["incomeTotals"], "incomeWindows": windows, "political": p["political"]})
        return {"id": self.item["id"], "map": self.item["map"], "civilization": self.item["civilization"], "seed": self.item["seed"],
                "requestedPlayer0Route": self.item["requestedRoute"], "observerPlayer0RouteReached": self.item["routeReached"],
                "observerStatus": self.item["status"], "naturalTerminal": self.item["naturalTerminal"], "winner": self.item["winner"],
                "seconds": self.item["seconds"], "comparisonReached": self.item["comparisonReached"],
                "comparisonStatus": self.item["comparisonStatus"], "actualComparisonAt": self.item["comparisonAt"],
                "eventRecords": self.event_count, "players": result}


def baseline_summary(inputs, path):
    data = inputs.document(path)
    require(data.get("status") == "observed_routes_incomplete" and len(data.get("cases", [])) == 8, "Not the closed071 eight-case independent summary")
    return {"source": str(Path(path).absolute()), "sourceFingerprint": data.get("sourceFingerprint"),
            "scope": "Descriptive historical summary only: eight071 Traders Bend normal age-I cases, declared2400s or earlier natural terminal. Different072 runtime, second map and3600s ceiling prevent a causal improvement claim.",
            "routeReachedCount": sum(case.get("independentRouteReached") is True for case in data["cases"]),
            "cases": [{key: case.get(key) for key in ("id", "requestedRoute", "seed", "verdict", "independentRouteReached", "naturalTerminal", "winner", "seconds")} for case in data["cases"]]}


def markdown(report):
    lines = ["# 072 真实正常局经济描述统计", "", "仅分析已退出且来源一致的原始观察。以下是本地账本与稀疏样本指标，不是胜率、文明平衡或对071的因果改善证明。", "",
             f"观察 {len(report['cases'])} 局 / {report['summary']['players']} 位玩家；到达2400秒观察边界 {report['summary']['comparisonReachedCases']} 局，提前自然终局 {report['summary']['earlyTerminalCases']} 局。", "",
             "付款、完工、正收入分开。Factory消耗真实shipment wagon；资源Foundation付款保持为空。Town重建只统计无存活Town时的实际正常Foundation付款。所有null表示本次未观察到。", "",
             "| 局 / 玩家 | 终点秒 | Post付款 / 完工 | Ledger付款 / 激活 | Bank完工 | Estate完工 | Factory完工 | 首次替代Wood / Coin | Town敌损 / 已付款重建 / 完成 | 工人存活 / 陆军存活 |",
             "|---|---:|---|---|---|---|---|---|---|---|"]
    def fmt(value):
        return "未观察" if value is None else f"{value:.3f}"
    for case in report["cases"]:
        for p in case["players"]:
            f = p["facilityFirsts"]; ledger = p["cargoLedgers"]
            paid = min((row["time"] for row in ledger["researchPayments"]), default=None)
            active = min((row["time"] for row in ledger["activations"]), default=None)
            income = p["incomeWindows"]["wholeObservation"]["loggedAlternativeChannelIncome"]
            lines.append("| " + " | ".join([case["id"] + " / P" + str(p["player"]), fmt(case["seconds"]),
                fmt(f["trade_post"]["firstNormalResourcePaymentAt"]) + " / " + fmt(f["trade_post"]["firstCompletedAt"]),
                fmt(paid) + " / " + fmt(active), fmt(f["bank"]["firstCompletedAt"]), fmt(f["estate"]["firstCompletedAt"]),
                fmt(f["factory"]["firstCompletedAt"]), fmt(income["wood"]["firstPositiveLoggedAt"]) + " / " + fmt(income["coin"]["firstPositiveLoggedAt"]),
                str(len(p["townEnemyLosses"])) + " / " + str(len(p["normalTownRecoveries"])) + " / " + str(sum(row["status"] == "completed" for row in p["normalTownRecoveries"])),
                str(p["terminalSample"]["workers"]) + " / " + str(p["terminalSample"]["soldiers"])]) + " |")
    lines += ["", "收入gap详情见同名JSON的incomeWindows：logged间隔是缓冲账本时间戳间隔；sampled平坦区间由真实累计收入端点确定，正常分辨率约30秒。它们都不能证明区间中每一秒的采集动作。TradeDelivery与CaravanDelivered已加入，XP与退款不计资源收入。", "",
              "rawFiniteWoodInCurrentVision只在opening和实际comparison/early-terminal世界快照计算，排除隐藏、crate、可持续建筑。该数字未证明安全、工人资格和可达路径，不能称AI实际拥有的全图枯竭情报，也不能代替planner的VisibleWoodRemaining。", "",
              "player0的routeReached字段仅是observer原值；本脚本没有重新执行模拟或完整路线独立验收。P1经济指标分别从P1账本统计，没有复用P0路线结论。"]
    if "baseline071" in report:
        b = report["baseline071"]
        lines += ["", f"071旧独立汇总：{b['routeReachedCount']} / 8 局到达完整路线；仅作历史描述。072只对真实到达2400边界的局给出对应窗口；提前终局不补成2400，也不声称同一2400窗口全部达成。"]
    return "\n".join(lines) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--matrix", required=True, type=Path)
    parser.add_argument("--runner", type=Path, help="Closed runner directory; default: matrix parent")
    parser.add_argument("--rules", required=True, type=Path, help="Raw Rules.json matching closed source-before SHA")
    parser.add_argument("--output", required=True, type=Path, help="New output basename directly in072 Diagnostics, without extension")
    parser.add_argument("--baseline071", type=Path, help="Optional closed eight-case independent071 receipt, never its large matrix")
    args = parser.parse_args()
    inputs = Inputs()
    matrix = args.matrix.absolute(); runner = (args.runner or matrix.parent).absolute()
    require(matrix.resolve(strict=True) == matrix and matrix.is_dir(), "Matrix directory is not real")
    output = args.output.absolute()
    require(output.parent == HERE and output.parent.resolve(strict=True) == HERE and output.suffix == "", "Use an extensionless new basename directly in this072 Diagnostics directory")
    targets = [output.with_suffix(".json"), output.with_suffix(".md")]
    require(not any(path.exists() for path in targets), "Existing analysis output must be preserved; use a new basename")
    inputs.raw(Path(__file__).absolute())
    before = inputs.document(runner / "source-before.json"); after = inputs.document(runner / "source-after.json")
    receipt = inputs.document(runner / "runner-receipt.json"); manifest = inputs.document(matrix / "manifest.json")
    files = before.get("files", {})
    fingerprint = hashlib.sha256(json.dumps(files, sort_keys=True, separators=(",", ":")).encode()).hexdigest()
    require(files and files == after.get("files") and before.get("fingerprint") == after.get("fingerprint") == fingerprint,
            "Closed source file maps/fingerprints differ")
    require(receipt.get("operation") == "routes" and receipt.get("finishedAtUtc") and receipt.get("sourceStable") is True
            and receipt.get("allProcessesSucceeded") is True and receipt.get("sourceBefore") == receipt.get("sourceAfter") == fingerprint,
            "Runner is live, aborted, failed or changed source")
    require(len(receipt.get("runs", [])) == 1 and receipt["runs"][0].get("exitCode") == 0 and receipt["runs"][0].get("finishedAtUtc"),
            "No actual closed successful route process")
    require(manifest.get("finishedUtc") and manifest.get("status") in ("observed_routes_incomplete", "all_observed_routes_reached")
            and manifest.get("declaredSourceFingerprint") == fingerprint, "Matrix is unfinished, erroneous or unbound")
    require(manifest.get("appVersion") == "0.15.0" and manifest.get("declaredLimitSeconds") == 3600
            and manifest.get("comparisonSeconds") == 2400 and manifest.get("sampleIntervalSeconds") == 30,
            "Unexpected072 observation version/horizons")
    require([(c["map"], c["civilization"], c["seed"]) for c in manifest["cases"]] == EXPECTED, "Not the declared sixteen-case matrix")
    rules_raw = inputs.raw(args.rules)
    require(hashlib.sha256(rules_raw).hexdigest() == files.get("Assets/Resources/Rules.json"), "Rules differ from the actual frozen runner input")
    catalog = Catalog(decode(rules_raw))
    report = {"schema": 1, "kind": "descriptive_normal072_economics", "generatedUtc": datetime.now(timezone.utc).isoformat(),
              "status": "analyzed_closed_actual_data", "sourceFingerprint": fingerprint, "sourceInputs": len(files),
              "unityRulesHash": manifest["rulesHash"], "cases": [],
              "limits": ["Economics descriptions only; no independently recreated simulation, balance, native interaction or victory certification.",
                         "Closed runner source SHA maps and supplied frozen raw rules are checked. Other runtime blobs are not rehashed here; use independent-normal072.py for full source/artifact audit.",
                         "Raw JSONL SHA values are computed during streaming; unchanged file identities are checked after parsing. These are unsigned local integrity records, not historical-payment authentication.",
                         "No whole-map finite-wood total is computed or attributed to AI. Current-vision raw finite wood appears only at actual opening/comparison snapshot boundaries, without safety/reachability claims.",
                         "Income timestamps are buffered logs, and cumulative sample gaps have approximately30s resolution. Spending, refunds, trade exchange and XP are not earned resource income.",
                         "Samples.gathered+passive and observer result.income omit tradeIncome; this analysis includes real resource TradeDelivery/CaravanDelivered separately.",
                         "Revolution conversion changes role with the same entityID; it is neither a new spawn nor a worker death. Death without hostile credit may be demolition, cancellation, disband, wagon consumption or other removal.",
                         "Requested P0 routes and observer verdicts are reported as source fields, not independently certified by this economics analyzer. Earlier natural terminal is never filled out to2400."]}
    for declared in manifest["cases"]:
        case_id = declared["id"]
        require(case_id == f"{declared['map']}-{declared['civilization']}-{declared['seed']}" and "/" not in case_id and ".." not in case_id,
                "Unsafe or changed case identity")
        item = inputs.document(matrix / case_id / "result.json")
        require(item == declared and item.get("finishedUtc") and item.get("status") not in ("planned", "running", "exception")
                and not item.get("error"), "Case result differs from closed manifest or failed")
        require(item["requestedRoute"] == ("revolution" if item["seed"] % 2 == 0 else "empire"), "Declared route changed")
        opening = inputs.document(matrix / case_id / "opening-world.json")
        projection_file = "comparison-2400-world.json" if item["comparisonReached"] else "early-terminal-world.json"
        projection = inputs.document(matrix / case_id / projection_file)
        require(opening["rulesHash"] == projection["rulesHash"] == manifest["rulesHash"] and opening["seed"] == projection["seed"] == item["seed"], "World identity differs")
        require(abs(opening["time"]) <= EPS and not projection["events"] and abs(projection["time"] - item["comparisonAt"]) <= EPS,
                "Invalid actual opening/comparison boundary")
        require(item["comparisonReached"] == (projection["time"] >= 2400) and (item["comparisonReached"] or item["naturalTerminal"]),
                "Early terminal is falsely presented as a2400 observation")
        analysis = CaseAnalysis(item, opening, projection, catalog)
        for line, event in inputs.lines(matrix / case_id / "events.jsonl"):
            analysis.observe(event, line)
        analysis.samples(inputs, matrix / case_id / "samples.jsonl")
        report["cases"].append(analysis.finish())
        print(json.dumps({"caseAnalyzed": case_id, "players": 2, "actualSeconds": item["seconds"]}), file=sys.stderr, flush=True)
    report["summary"] = {"cases": 16, "players": 32,
        "comparisonReachedCases": sum(c["comparisonReached"] for c in report["cases"]),
        "earlyTerminalCases": sum(not c["comparisonReached"] for c in report["cases"]),
        "observerPlayer0RoutesReached": sum(c["observerPlayer0RouteReached"] for c in report["cases"]),
        "routeMeaning": "Observer-reported P0 flag only; not an independent route/balance verdict."}
    if args.baseline071:
        report["baseline071"] = baseline_summary(inputs, args.baseline071)
    inputs.unchanged()
    report["inputsRead"] = sorted(inputs.records.values(), key=lambda row: row["path"])
    rendered = [(json.dumps(report, ensure_ascii=False, indent=2, allow_nan=False) + "\n").encode("utf-8"), markdown(report).encode("utf-8")]
    created = []
    try:
        for target, raw in zip(targets, rendered):
            with target.open("xb") as handle:
                created.append(target); handle.write(raw)
    except Exception:
        for target in created:
            target.unlink()  # Only this invocation's own newly-created partial files.
        raise
    print(json.dumps({"status": report["status"], "outputs": [{"path": str(target), "bytes": len(raw),
                      "sha256": hashlib.sha256(raw).hexdigest()} for target, raw in zip(targets, rendered)]}))


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, KeyError, TypeError) as error:
        print(json.dumps({"status": "not_analyzed", "errorType": type(error).__name__, "error": str(error)}), file=sys.stderr)
        raise SystemExit(2)
