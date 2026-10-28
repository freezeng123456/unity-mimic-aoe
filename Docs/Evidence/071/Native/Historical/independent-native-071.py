#!/usr/bin/env python3
"""Read-only local native evidence audit. No GUI, simulation, extraction or repair."""
import argparse
import base64
import copy
import hashlib
import json
import re
import struct
import tempfile
import unittest
import zlib
from datetime import datetime, timezone
from pathlib import Path

MAX_STORED = 64 * 1024 * 1024
MAX_PAYLOAD = 256 * 1024 * 1024
COMMANDS = {"Train": 2, "Research": 5, "Revolution": 25, "Disband": 26}
PHASES = {"Empire": 0, "Revolutionary": 1, "CivilianRebuilt": 2}
REVO_COST = {"food": 800, "wood": 800, "coin": 800}
REBUILD_COST = {"food": 400, "wood": 0, "coin": 400}
REVO_NAMES = {"rev_coast":"Volunteer Assembly", "rev_river":"Merchant Republic", "rev_france":"Citizens' Guard", "rev_ottoman":"Provincial Assembly"}
REQUIRED = ["process", "capture", "slot", "exit_events", "accepted_Revolution", "accepted_Research", "accepted_Train", "accepted_Disband", "revolution_completed", "civilian_rebuilt", "worker_trained", "unit_disbanded", "cold_restore", "native_forward_replay", "native_worker_ui"]

class Pending(Exception):
    pass

def require(ok, message):
    if not ok:
        raise ValueError(message)

def sha(raw):
    return hashlib.sha256(raw).hexdigest()

def pairs(rows):
    out = {}
    for key, value in rows:
        require(key not in out, "duplicate JSON key: " + key)
        out[key] = value
    return out

def parse(raw):
    def nonfinite(value):
        raise ValueError("nonfinite JSON: " + value)
    return json.loads(raw.decode("utf-8", errors="strict") if isinstance(raw, bytes) else raw, object_pairs_hook=pairs, parse_constant=nonfinite)

def digest(value):
    require(isinstance(value, str) and re.fullmatch(r"[0-9a-f]{64}", value), "missing/invalid SHA256")
    return value

def inside(root, relative):
    require(isinstance(relative, str) and relative and not Path(relative).is_absolute(), "input must be a Native-relative path")
    path = root / relative
    require(".." not in Path(relative).parts, "parent traversal")
    require(path.resolve().is_relative_to(root.resolve()), "input escapes Native root")
    for p in [path, *path.parents]:
        if p == root.parent:
            break
        require(not p.is_symlink(), "symlink input: " + str(p))
    return path

def regular(path, limit=MAX_STORED):
    require(path.is_file() and not path.is_symlink(), "missing regular input: " + str(path))
    require(0 < path.stat().st_size <= limit, "input size outside limit")
    return path.read_bytes()

def envelope(raw):
    e = parse(raw)
    require(isinstance(e, dict), "envelope object required")
    digest(e.get("sha256"))
    if e.get("schema") == 1:
        require(e.get("payload") and not e.get("encoding") and not e.get("payloadBase64"), "bad plain envelope")
        payload = e["payload"].encode("utf-8", errors="strict")
    else:
        require(e.get("schema") == 2 and e.get("encoding") == "gzip-base64" and not e.get("payload"), "bad gzip envelope")
        text = e.get("payloadBase64")
        require(isinstance(text, str), "base64 string missing")
        packed = base64.b64decode(text, validate=True)
        require(base64.b64encode(packed).decode("ascii") == text, "noncanonical base64")
        require(18 <= len(packed) <= MAX_STORED and packed[:4] == b"\x1f\x8b\x08\x00", "unsupported gzip header")
        decoder = zlib.decompressobj(-15)
        payload = decoder.decompress(packed[10:-8], MAX_PAYLOAD + 1)
        require(len(payload) <= MAX_PAYLOAD and decoder.eof and not decoder.unused_data and not decoder.unconsumed_tail, "truncated/concatenated/trailing deflate or oversized payload")
        crc, length = struct.unpack("<II", packed[-8:])
        require(crc == zlib.crc32(payload) and length == len(payload), "gzip footer CRC/size mismatch")
    require(len(payload) <= MAX_PAYLOAD and sha(payload) == e["sha256"], "payload checksum mismatch")
    return parse(payload), {"storedSha256": sha(raw), "payloadSha256": e["sha256"], "storedBytes": len(raw), "payloadBytes": len(payload), "encodingSchema": e["schema"]}

def png(raw):
    require(raw[:8] == b"\x89PNG\r\n\x1a\n", "PNG signature")
    offset, dimensions, ended = 8, None, False
    while offset < len(raw):
        require(offset + 12 <= len(raw), "truncated PNG chunk")
        size = struct.unpack(">I", raw[offset:offset+4])[0]
        tag = raw[offset+4:offset+8]
        end = offset + size + 12
        require(end <= len(raw), "truncated PNG payload")
        payload = raw[offset+8:offset+8+size]
        require(zlib.crc32(tag + payload) == struct.unpack(">I", raw[end-4:end])[0], "PNG chunk CRC")
        if tag == b"IHDR":
            require(dimensions is None and offset == 8 and size == 13, "PNG IHDR")
            dimensions = struct.unpack(">II", payload[:8])
            require(min(dimensions) > 0, "PNG zero dimensions")
        if tag == b"IEND":
            require(size == 0 and end == len(raw), "PNG IEND/trailing bytes")
            ended = True
        offset = end
    require(dimensions and ended, "PNG incomplete")
    return dimensions

def stamp(value):
    require(isinstance(value, str) and value, "missing UTC timestamp")
    return datetime.fromisoformat(value.replace("Z", "+00:00"))

def near(a, b, tolerance=.05):
    return isinstance(a, (int, float)) and isinstance(b, (int, float)) and abs(a-b) <= tolerance

def paid(cost, expected):
    return isinstance(cost, dict) and all(near(cost.get(k), v) for k, v in expected.items())

def payment(event, expected):
    e = event.get("economy", {})
    return event.get("kind") == "Payment" and event.get("hasEconomy") is True and all(
        near(e.get("requestedDelta", {}).get(k), -v) and near(e.get("actualDelta", {}).get(k), -v)
        and near(e.get("balanceAfter", {}).get(k, 0)-e.get("balanceBefore", {}).get(k, 0), -v)
        and e.get("balanceBefore", {}).get(k, -1) >= v-.05 for k, v in expected.items())

def projected_world(world):
    w = copy.deepcopy(world)
    w["events"] = []
    w["paused"] = True  # LoadMatch pauses the authoritative restored world before recording.
    return w

def projected_sim(simulation):
    s = copy.deepcopy(simulation)
    s["accumulator"] = 0  # ReplayDigest normalizes rendered-frame debt in anchors.
    return s

class Audit:
    def __init__(self, root, build, journal):
        self.root, self.build, self.journal = root, build, journal
        self.results, self.observed, self.missing, self.errors = [], set(), [], []
        self.processes, self.captures, self.slots, self.reports, self.replays = {}, {}, {}, {}, {}
        self.inputs = []

    def ref(self, row, field="file", hashfield="sha256", limit=MAX_STORED):
        if not row.get(field) or not row.get(hashfield):
            raise Pending("pending " + field + "/" + hashfield)
        path = inside(self.root, row[field]); raw = regular(path, limit)
        require(sha(raw) == digest(row[hashfield]), "original file SHA mismatch: " + row[field])
        self.inputs.append({"file": row[field], "sha256": sha(raw), "bytes": len(raw)})
        return path, raw

    def identity(self, value, replay=False):
        require(value.get("appVersion") == self.build["appVersion"], "application version mismatch")
        require(value.get("buildIdentity" if replay else "buildGuid") == self.build["buildGuid"], "build GUID mismatch")
        if value.get("unityVersion") is not None:
            require(value["unityVersion"] == self.build["unityVersion"], "Unity version mismatch")
        if replay:
            require(value.get("rulesHash") == digest(self.journal.get("rulesHash")), "rules identity mismatch")

    def process(self, row):
        path, raw = self.ref(row); value = parse(raw); self.identity(value)
        command = value.get("command", [])
        require(value.get("pid", 0) > 0 and isinstance(command, list) and command, "process PID/command missing")
        require(command[0] == self.journal.get("executablePath"), "process is not the bound built executable")
        require("-batchmode" not in command and "-nographics" not in command, "headless process cannot prove native UI")
        require(value.get("exitCode") == 0 and stamp(value.get("endedAtUtc")) >= stamp(value.get("startedAtUtc")), "normal process exit missing")
        for flag in ["-frontierDataDirectory", "-frontierReportDirectory", "-frontierReplayDirectory", "-logFile"]:
            require(command.count(flag) == 1 and command.index(flag)+1 < len(command), "missing isolated argument " + flag)
            require(Path(command[command.index(flag)+1]).resolve().is_relative_to(self.root.resolve()), "process argument outside isolated Native root")
        _, lograw = self.ref(row, "logFile", "logSha256", MAX_PAYLOAD)
        log = lograw.decode("utf-8", errors="strict")
        require(str((self.root / row["logFile"]).resolve()) == command[command.index("-logFile")+1], "process log path mismatch")
        warnings = [line for line in log.splitlines() if re.search(r"Exception|FRONTIER_\w*FAILED|memory leak|allocation.*leak|Mono.*warning", line, re.I)]
        self.processes[row["id"]] = (value, log)
        self.observed.add("process")
        return {"pid": value["pid"], "normalExitCode": 0, "warnings": warnings, "receiptScope": "local launcher receipt; not an OS-signed attestation"}

    def capture(self, row):
        path, raw = self.ref(row); value = parse(raw); self.identity(value)
        require(value.get("schemaVersion") == 6 and value.get("eventSchemaVersion") == 2, "capture schema mismatch")
        name = value.get("pngFile", ""); require(Path(name).name == name and name.endswith(".png"), "capture PNG basename")
        imagepath = path.parent / name; image = regular(imagepath)
        require(sha(image) == digest(value.get("pngSha256")), "F9 PNG SHA mismatch")
        dimensions = png(image); require(list(dimensions) == [value.get("width"), value.get("height")], "F9 PNG dimensions mismatch")
        require(value.get("requestMatchId") == value.get("matchId") and value.get("captureFrame", -1) >= value.get("requestFrame", 0), "F9 request/frame identity")
        process, log = self.processes[row["processId"]]
        require(stamp(process["startedAtUtc"]) <= stamp(value.get("utc")) <= stamp(process["endedAtUtc"]), "F9 time outside process")
        require("FRONTIER_CAPTURE_OK " + str(path) in log, "F9 completion marker absent from original log")
        self.inputs.append({"file": str(imagepath.relative_to(self.root)), "sha256": sha(image), "bytes": len(image)})
        self.captures[row["id"]] = (row, value)
        self.observed.add("capture")
        return {"dimensions": list(dimensions), "matchId": value.get("matchId"), "worldTime": value.get("worldTime"), "focused": value.get("camera", {}).get("applicationFocused"), "visualContentReviewed": False}

    def slot(self, row):
        _, raw = self.ref(row); value, receipt = envelope(raw)
        require(value.get("appVersion") == self.build["appVersion"], "slot application version")
        require(row.get("processId") in self.processes and "FRONTIER_SAVE slot="+str(row["slotNumber"])+" match="+value["matchId"]+" " in self.processes[row["processId"]][1], "slot native SAVE completion/process missing")
        world, sim = value.get("world", {}), value.get("simulation", {})
        require(world.get("rulesHash") == digest(self.journal.get("rulesHash")) and value.get("matchId") and sim, "slot world/scheduler/rules identity")
        expectation = row.get("expect", {})
        if expectation.get("phase") is None or expectation.get("player") is None:
            raise Pending("slot expected political boundary not selected")
        player = expectation["player"]; p = world["players"][player]["state"]
        require(p["id"] == player and p.get("politicalPhase") == PHASES[expectation["phase"]], "slot political phase mismatch")
        if p["politicalPhase"] != 0:
            require(world.get("maxAge") == 5 and p.get("age") == 4 and p.get("revolutionId") == "rev_" + p.get("civilization", "") and 0 <= p.get("revolutionAt", -1) <= world["time"], "slot completed revolution identity")
        if p["politicalPhase"] == 2:
            require("civilian_rebuilding" in world["players"][player]["technologies"] and p["revolutionAt"] <= p.get("civilianRebuiltAt", -1) <= world["time"], "slot civilian rebuilding receipt")
        jobs = [(e["state"]["id"], j) for e in world["entities"] if e["state"]["owner"] == player for j in e["queue"]]
        kind = expectation.get("queuedKind")
        if kind:
            relevant = [(entity,j) for entity,j in jobs if j["kind"] == kind and (kind != "Research" or j["item"] == "civilian_rebuilding")]
            require(len(relevant) == 1, "slot must contain exactly one declared political job")
            _, job = relevant[0]
            require(paid(job.get("paid"), REVO_COST if kind == "Revolution" else REBUILD_COST) and near(job.get("duration"), 90 if kind == "Revolution" else 60) and 0 <= job["elapsed"] <= job["duration"], "slot political queued paid/duration/elapsed mismatch")
            require(kind == "Revolution" and p["politicalPhase"] == 0 and job["item"] == "rev_" + p["civilization"] or kind == "Research" and p["politicalPhase"] == 1 and job["item"] == "civilian_rebuilding", "slot queued phase/item mismatch")
            receipt["queuedJob"] = job
        rows = {e["state"]["id"]:e for e in world["entities"]}
        for entity in expectation.get("requiredIds", []):
            require(entity in rows and rows[entity]["state"]["owner"] == player, "slot required stable actor missing/wrong owner")
        self.slots[row["id"]] = (row, value); self.observed.add("slot")
        receipt.update(matchId=value["matchId"], phase=expectation["phase"], requiredIds=expectation.get("requiredIds", []), checksumScope="corruption/identity checksum; not payment provenance authentication")
        return receipt

    def report(self, row):
        reportpath, raw = self.ref(row); report = parse(raw); self.identity(report)
        require(row.get("processId") in self.processes and "FRONTIER_REPORT "+str(reportpath) in self.processes[row["processId"]][1], "native report completion/process missing")
        require(report.get("platform") == "OSXPlayer" and report.get("version") == self.build["unityVersion"], "report is not this native macOS build")
        require(report.get("reason") in ["quit", "menu_exit", "load_exit", "replay_exit", "restart_exit", "match_finished", "guided_finished"], "report is not an actual ending/transition")
        _, eventraw = self.ref(row, "eventsFile", "eventsSha256", MAX_PAYLOAD)
        events = [parse(line) for line in eventraw.splitlines() if line.strip()]
        require(report.get("eventCount") == len(events), "exit report event count mismatch")
        last = -1
        for event in events:
            require(event.get("schemaVersion") == 2 and event.get("time", -1) >= last and event["time"] <= report["time"]+.05, "exit event schema/clock")
            last = event["time"]
        player = self.journal.get("player", 0)
        for index, event in enumerate(events):
            if event.get("player") != player or event["kind"] not in ["RevolutionCompleted", "CivilianRebuilt"]:
                continue
            revolution = event["kind"] == "RevolutionCompleted"
            name = REVO_NAMES.get(event["message"]) if revolution else "Rebuild Civilian Government"
            expected, duration = (REVO_COST,90) if revolution else (REBUILD_COST,60)
            require(name is not None, "unknown native revolution identity")
            candidates = [e for e in events[:index] if e.get("player") == player and e.get("entity") == event["entity"] and e.get("message", "").startswith(name+": ") and payment(e,expected)]
            require(candidates and event["time"] >= candidates[-1]["time"]+duration-.15, "native political completion lacks actual payment/time")
            if revolution:
                require(any(e["kind"] == "RevolutionQueued" and e.get("player") == player and e.get("entity") == event["entity"] and e.get("message") == event["message"] and near(e["time"],candidates[-1]["time"]) for e in events[:index]), "native completed revolution queue identity")
        for kind, scope in [("RevolutionCompleted", "revolution_completed"), ("CivilianRebuilt", "civilian_rebuilt"), ("UnitTrained", "worker_trained"), ("UnitDisbanded", "unit_disbanded")]:
            found = [e for e in events if e["kind"] == kind and e["player"] == player]
            if kind == "UnitTrained":
                found = [e for e in found if e["message"] in ["villager", "coureur"] and any(b["kind"] == "CivilianRebuilt" and b["player"] == player and b["time"] <= e["time"] for b in events)]
            if found:
                self.observed.add(scope)
        self.reports[row["id"]] = (row, report, events); self.observed.add("exit_events")
        return {"matchId": report["matchId"], "reason": report["reason"], "eventCount": len(events), "politicalPhaseInReport": False}

    def replay(self, row):
        path, raw = self.ref(row); document, receipt = envelope(raw); self.identity(document.get("identity", {}), True)
        require(row.get("processId") in self.processes and "FRONTIER_REPLAY_SAVED file="+str(path) in self.processes[row["processId"]][1], "native original recording save/process marker missing")
        anchors = document.get("anchors", []); require(document.get("schema") == 1 and len(anchors) >= 1, "replay anchors missing")
        start, end = anchors[0]["completedStep"], anchors[-1]["completedStep"]
        require(start <= end and anchors[-1]["eventCount"] == len(document["events"]), "replay segment boundary")
        require(not document.get("incomplete") and not document.get("incompleteReason"), "interrupted replay cannot pass complete native scope")
        player = self.journal.get("player", 0)
        commands = []
        for record in document["records"]:
            if record.get("kind") != 0 or not record.get("hasCommand") or not record.get("accepted"):
                continue
            c = record["command"]
            if c["player"] != player:
                continue
            for name, number in COMMANDS.items():
                if c.get("kind") != number or name == "Research" and c.get("item") != "civilian_rebuilding":
                    continue
                eventprefix = document["events"][:record["eventCount"]]
                actor = c.get("actors", [0])[0]
                if name in ["Revolution", "Research"]:
                    expected = REVO_COST if name == "Revolution" else REBUILD_COST
                    require(any(e.get("player") == player and e.get("entity") == actor and payment(e, expected) for e in eventprefix[-8:]), "accepted political command lacks its actual payment event")
                if name == "Disband":
                    require(any(e.get("kind") == "UnitDisbanded" and e.get("player") == player and e.get("entity") in c["actors"] for e in eventprefix[-2*len(c["actors"])-4:]), "accepted disband lacks exact actor completion")
                if name == "Train":
                    civ = anchors[-1]["world"]["players"][player]["state"]["civilization"]
                    worker_cost = {"food":0 if civ == "river" else 120 if civ == "france" else 100,"wood":0,"coin":100 if civ == "river" else 0}
                    require(any(e.get("player") == player and e.get("entity") == actor and payment(e,worker_cost) for e in eventprefix[-8:]), "accepted civilian Train lacks actual payment")
                    require(c.get("item") in ["villager", "coureur"] and any(e.get("kind") == "ProductionQueued" and e.get("entity") == actor and e.get("message", "").startswith(c["item"] + " ×") for e in eventprefix[-8:]), "accepted Train is not the civilian worker queue")
                self.observed.add("accepted_" + name); commands.append({"kind": name, "completedStep": record["completedStep"], "actors": c["actors"], "accepted": True})
        if row.get("reportId"):
            report = self.reports[row["reportId"]]
            require(document["matchId"] == report[1]["matchId"] and document["events"] == report[2][:len(document["events"])], "replay differs from linked actual exit event prefix")
        self.replays[row["id"]] = (row, document)
        observations = row.get("playbackCaptureIds", [])
        if len(observations) < 2:
            raise Pending("native playback start/end F9 captures pending; replay file existence is not playback")
        samples = [self.captures[item] for item in observations]
        processid = samples[0][0]["processId"]
        require(all(item[0]["processId"] == processid for item in samples), "playback captures span processes")
        last, frame = start, -1
        for _, sample in samples:
            p = sample.get("replay", {}); self.identity(p.get("identity", {}), True)
            require(sample.get("matchId") == document["matchId"] and p.get("file") == str(path) and sample.get("captureFrame", -1) > frame, "native playback original file/match/frame identity")
            frame = sample["captureFrame"]
            require(p.get("watching") is True and p.get("segment") == document["segmentId"] and p.get("startStep") == start and p.get("endStep") == end and p.get("seek") is False and not p.get("error") and not p.get("fileError"), "native playback identity/error/seek")
            require(last <= p.get("currentStep", -1) <= end, "native playback observed clock must move forward")
            last = p["currentStep"]
        first, final = samples[0][1], samples[-1][1]
        require(first["replay"]["currentStep"] == start and final["replay"]["currentStep"] == end and final["replay"].get("verified") is True, "native playback has no original start/final verified boundary")
        log = self.processes[processid][1]
        marker = "FRONTIER_REPLAY_OPEN file="
        openings = [line for line in log.splitlines() if marker in line and "segment="+document["segmentId"] in line]
        require(len(openings) == 1 and "start="+str(start)+" end="+str(end) in openings[0], "native replay open marker missing/ambiguous")
        first_marker = "FRONTIER_CAPTURE_OK " + str(self.root / samples[0][0]["file"])
        require(first_marker in log, "native replay start capture marker")
        first_capture_offset = log.index(first_marker)
        seeks = [(match.start(),match.group(1)) for match in re.finditer(r"FRONTIER_REPLAY_SEEK segment="+re.escape(document["segmentId"])+r" target=(\d+)",log)]
        require(all(offset < first_capture_offset and int(target) == start for offset,target in seeks), "native playback used a seek beyond the original start, or after the observed start")
        self.observed.add("native_forward_replay")
        receipt.update(segment=document["segmentId"], startKind=document["startKind"], startStep=start, endStep=end, acceptedCommands=commands, nativeForwardPlaybackObserved=True, nativeForwardSeekCalls=0, initialFromStartResets=len(seeks))
        return receipt

    def cold(self, row):
        slotrow, save = self.slots[row["slotId"]]; replayrow, document = self.replays[row["replayId"]]
        require(row["processId"] != slotrow["processId"] and replayrow["processId"] == row["processId"], "cold restore requires a distinct recording process")
        before = self.processes[slotrow["processId"]][0]; after, log = self.processes[row["processId"]]
        require(stamp(after["startedAtUtc"]) >= stamp(before["endedAtUtc"]), "cold restore did not start after original process exit")
        require(document["startKind"] == "loaded_checkpoint" and document["matchId"] == save["matchId"], "cold replay is not this loaded slot")
        anchor = document["anchors"][0]
        require(projected_world(anchor["world"]) == projected_world(save["world"]) and projected_sim(anchor["simulation"]) == projected_sim(save["simulation"]), "cold native anchor differs from original slot beyond documented pause/debt normalization")
        require(document["events"][:anchor["eventCount"]] == save["world"]["events"], "cold loaded history differs from original slot")
        require("FRONTIER_LOAD slot="+str(row["slotNumber"])+" match="+save["matchId"]+" " in log, "cold native LOAD completion missing")
        self.observed.add("cold_restore")
        return {"slotId": row["slotId"], "recordingProcess": row["processId"], "matchId": save["matchId"], "stableState": True, "normalizations": ["world.paused=true from LoadMatch", "simulation.accumulator=0 from ReplayDigest"], "controlsUiRestored": False}

    def manual(self, row):
        if row.get("status") != "observed":
            raise Pending("manual native UI observation: " + row.get("id", "unknown"))
        require(row.get("description") and row.get("captureIds") and row.get("reviewer"), "UI observation must include actual captures, reviewer and description")
        for item in row["captureIds"]:
            require(item in self.captures, "UI observation references unverified capture")
        if row.get("id") == "native_worker_ui":
            self.observed.add("native_worker_ui")
        return {"scope": "operator observation of the actual native visible frame linked to original F9 images; checker does not interpret pixels", "description": row["description"], "captureIds": row["captureIds"]}

    def run(self):
        for plural, callback in [("processes",self.process),("captures",self.capture),("slots",self.slot),("reports",self.report),("replays",self.replay),("coldRestores",self.cold),("manualObservations",self.manual)]:
            seen = set()
            for row in self.journal.get(plural, []):
                try:
                    require(row.get("id") and row["id"] not in seen, "missing/duplicate row id")
                    seen.add(row["id"])
                    receipt = callback(row); self.results.append({"kind":plural,"id":row["id"],"status":"observed_verified","receipt":receipt})
                except Pending as error:
                    self.missing.append({"kind":plural,"id":row.get("id"),"reason":str(error)})
                except (ValueError, KeyError, IndexError, TypeError, OSError, zlib.error) as error:
                    self.errors.append({"kind":plural,"id":row.get("id"),"reason":str(error)})
        for scope in REQUIRED:
            if scope not in self.observed:
                self.missing.append({"scope":scope,"reason":"actual supporting native inputs have not been verified"})
        if self.journal.get("status") != "observed":
            self.missing.append({"scope":"journal","reason":"journal remains planned; no completed delivery claim"})
        status = "invalid_evidence" if self.errors else "pending_native_observations" if self.missing else "passed_observed_native_scope"
        return {"schema":1,"generatedAtUtc":datetime.now(timezone.utc).isoformat(),"status":status,"actualNativeInputsVerified":bool(self.inputs),"buildIdentity":self.build,
                "observedScope":sorted(self.observed),"missingPending":self.missing,"errors":self.errors,"checks":self.results,"inputFiles":self.inputs,
                "limitations":["Local original-byte/hash consistency and actual captured native scope only; receipts/checksums are unsigned.","No GUI control, no Unity/test execution, no native reconstruction is performed by this Python script.","Full native ReplaySession execution is observed through its real start/final F9 frames and a process log without forward seeks, not reimplemented here.","A controlled authored age-IV UI fixture does not certify normal age-I economy, balance, all civilizations or two physical machine LAN.","Cold worker selection/group/UI appearance requires separate operator observations of actual native visible frames in original captures; automated tests never fill missing native scope."]}

def self_test():
    class ContractTests(unittest.TestCase):
        def test_duplicate_json_rejected(self):
            with self.assertRaises(ValueError): parse(b'{"a":1,"a":2}')
        def test_gzip_original_bytes_crc_and_digest(self):
            import gzip
            payload=b'{"matchId":"synthetic-only"}'; packed=gzip.compress(payload)
            raw=json.dumps({"schema":2,"encoding":"gzip-base64","payloadBase64":base64.b64encode(packed).decode(),"sha256":sha(payload)}).encode()
            self.assertEqual(envelope(raw)[0]["matchId"], "synthetic-only")
            e=parse(raw); e["sha256"]="0"*64
            with self.assertRaises(ValueError): envelope(json.dumps(e).encode())
            e=parse(raw); broken=bytearray(packed); broken[-8]^=1; e["payloadBase64"]=base64.b64encode(broken).decode()
            with self.assertRaises(ValueError): envelope(json.dumps(e).encode())
        def test_concatenated_gzip_rejected(self):
            import gzip
            payload=b'{}'; packed=gzip.compress(payload)+gzip.compress(b'')
            raw=json.dumps({"schema":2,"encoding":"gzip-base64","payloadBase64":base64.b64encode(packed).decode(),"sha256":sha(payload)}).encode()
            with self.assertRaises(ValueError): envelope(raw)
        def test_png_chunk_integrity(self):
            def chunk(tag,p): return struct.pack('>I',len(p))+tag+p+struct.pack('>I',zlib.crc32(tag+p))
            raw=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',1280,800,8,6,0,0,0))+chunk(b'IEND',b'')
            self.assertEqual(png(raw),(1280,800))
            with self.assertRaises(ValueError): png(raw+b'x')
            broken=bytearray(raw); broken[24]^=1
            with self.assertRaises(ValueError): png(broken)
        def test_zero_native_inputs_never_pass(self):
            result=Audit(Path('/nonexistent'),{}, {"status":"planned"}).run()
            self.assertEqual(result['status'],'pending_native_observations'); self.assertFalse(result['actualNativeInputsVerified'])
            self.assertEqual(result['observedScope'],[])
        def test_payment_actual_delta_not_summary(self):
            e={"kind":"Payment","hasEconomy":True,"economy":{"requestedDelta":{k:-v for k,v in REVO_COST.items()},"actualDelta":{k:-v for k,v in REVO_COST.items()},"balanceBefore":{k:1000 for k in REVO_COST},"balanceAfter":{k:200 for k in REVO_COST}}}
            self.assertTrue(payment(e,REVO_COST)); e['economy']['actualDelta']['food']=0; self.assertFalse(payment(e,REVO_COST))
        def test_cold_normalizes_only_pause_debt_not_roles_jobs(self):
            a={"events":[1],"paused":False,"entities":[{"definition":"villager","queue":[{"paid":REVO_COST}]}]}
            b=copy.deepcopy(a);b['paused']=True;b['events']=[]
            self.assertEqual(projected_world(a),projected_world(b));b['entities'][0]['definition']='revolutionary'
            self.assertNotEqual(projected_world(a),projected_world(b))
        def test_native_forward_segment_is_not_file_existence_or_final_seek(self):
            with tempfile.TemporaryDirectory() as directory:
                root=Path(directory); build={"appVersion":"0.14.0","buildGuid":"1"*32,"unityVersion":"test-engine"}
                ident={"appVersion":"0.14.0","buildIdentity":"1"*32,"unityVersion":"test-engine","rulesHash":"a"*64}
                document={"schema":1,"segmentId":"synthetic","matchId":"synthetic-match","identity":ident,"incomplete":False,"incompleteReason":"","startKind":"authored_scenario","records":[],"events":[],"anchors":[{"completedStep":0,"eventCount":0},{"completedStep":1000,"eventCount":0}]}
                payload=json.dumps(document);raw=json.dumps({"schema":1,"payload":payload,"sha256":sha(payload.encode())}).encode();(root/'segment.replay.json').write_bytes(raw)
                row={"id":"segment","file":"segment.replay.json","sha256":sha(raw),"processId":"p","playbackCaptureIds":["start","final"]}
                audit=Audit(root,build,{"rulesHash":"a"*64})
                for name, step, frame in [("start",0,100),("final",1000,900)]:
                    meta={"matchId":"synthetic-match","captureFrame":frame,"replay":{"identity":ident,"file":str(root/'segment.replay.json'),"watching":True,"segment":"synthetic","startStep":0,"endStep":1000,"currentStep":step,"seek":False,"verified":name=='final',"error":"","fileError":""}}
                    audit.captures[name]=({"processId":"p","file":name+'.json'},meta)
                log='FRONTIER_REPLAY_SAVED file='+str(root/'segment.replay.json')+'\nFRONTIER_REPLAY_OPEN file='+str(root/'segment.replay.json')+' segment=synthetic original=synthetic-match start=0 end=1000\nFRONTIER_REPLAY_SEEK segment=synthetic target=0\nFRONTIER_CAPTURE_OK '+str(root/'start.json')+'\nFRONTIER_CAPTURE_OK '+str(root/'final.json')
                audit.processes['p']=({},log);self.assertTrue(audit.replay(row)['nativeForwardPlaybackObserved'])
                row['playbackCaptureIds']=['final']
                with self.assertRaises(Pending):audit.replay(row)
                row['playbackCaptureIds']=['start','final'];audit.processes['p']=({},log+'\nFRONTIER_REPLAY_SEEK segment=synthetic target=1000')
                with self.assertRaises(ValueError):audit.replay(row)
        def test_report_summary_without_actual_ninety_second_payment_rejected(self):
            with tempfile.TemporaryDirectory() as directory:
                root=Path(directory);build={"appVersion":"0.14.0","buildGuid":"1"*32,"unityVersion":"test-engine"}
                event={"schemaVersion":2,"kind":"Payment","time":0,"entity":1,"player":0,"message":"Volunteer Assembly: 800 F / 800 W / 800 C","hasEconomy":True,"economy":{"requestedDelta":{k:-v for k,v in REVO_COST.items()},"actualDelta":{k:-v for k,v in REVO_COST.items()},"balanceBefore":{k:1000 for k in REVO_COST},"balanceAfter":{k:200 for k in REVO_COST}}}
                events=[event,{"schemaVersion":2,"kind":"RevolutionQueued","time":0,"entity":1,"player":0,"message":"rev_coast"},{"schemaVersion":2,"kind":"RevolutionCompleted","time":90,"entity":1,"player":0,"message":"rev_coast"}]
                report={**build,"platform":"OSXPlayer","version":"test-engine","reason":"quit","time":90,"eventCount":3,"matchId":"synthetic-match"}
                def write():
                    raw=json.dumps(report).encode();(root/'exit.json').write_bytes(raw);evraw='\n'.join(json.dumps(e) for e in events).encode();(root/'events.jsonl').write_bytes(evraw)
                    return {"id":"r","file":"exit.json","sha256":sha(raw),"eventsFile":"events.jsonl","eventsSha256":sha(evraw)}
                def auditor():
                    a=Audit(root,build,{});a.processes['p']=({},'FRONTIER_REPORT '+str(root/'exit.json'));return a
                valid=write();valid['processId']='p';self.assertEqual(auditor().report(valid)['eventCount'],3)
                events[-1]['time']=20;invalid=write();invalid['processId']='p'
                with self.assertRaises(ValueError):auditor().report(invalid)
        def test_path_traversal_and_symlink_rejected(self):
            with tempfile.TemporaryDirectory() as directory:
                root=Path(directory);(root/'target').write_text('x');(root/'link').symlink_to(root/'target')
                with self.assertRaises(ValueError): inside(root,'../x')
                with self.assertRaises(ValueError): inside(root,'link')
    result=unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(ContractTests))
    print(json.dumps({"status":"synthetic_contract_tests_only","cases":result.testsRun,"passed":result.wasSuccessful(),"actualNativeInputsVerified":False}))
    return 0 if result.wasSuccessful() else 2

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--native-root',type=Path);parser.add_argument('--build-identity',type=Path);parser.add_argument('--journal',type=Path);parser.add_argument('--output',type=Path);parser.add_argument('--self-test',action='store_true')
    args=parser.parse_args()
    if args.self_test: return self_test()
    for key in ['native_root','build_identity','journal','output']:
        if getattr(args,key) is None: parser.error('--'+key.replace('_','-')+' required')
    require(not args.output.exists(), 'output already exists; retain all previous evidence')
    buildraw=regular(args.build_identity);build=parse(buildraw);journalraw=regular(args.journal);journal=parse(journalraw)
    require(build.get('schema')==1 and build.get('buildResult')=='Succeeded' and build.get('appVersion')=='0.14.0' and re.fullmatch(r'[0-9a-f]{32}',build.get('buildGuid','')), '071 successful frozen build identity required')
    digest(build.get('sourceFingerprint'));require(type(build.get('sourceInputs')) is int and build['sourceInputs']>0,'current frozen input identity required')
    require(journal.get('schema')==1 and journal.get('status') in ['planned','observed'], 'journal schema/status')
    if journal.get('status') == 'observed':
        digest(journal.get('buildIdentitySha256'));digest(journal.get('sourceFingerprint'));digest(journal.get('rulesHash'))
        require(journal.get('sourceInputs') == build['sourceInputs'], 'exact frozen source input count differs')
    if journal.get('executablePath'):
        executable = Path(journal['executablePath'])
        expected = args.build_identity.resolve().parent.parent / build['executable']['file']
        require(executable.resolve() == expected.resolve(), 'native executable path differs from built artifact identity')
        raw = regular(executable)
        require(len(raw) == build['executable']['bytes'] and sha(raw) == build['executable']['sha256'] and executable.stat().st_mode & 0o111, 'built executable original SHA/bytes/mode differs')
    elif journal.get('status') == 'observed':
        raise ValueError('actual bound native executable path missing')
    if journal.get('buildIdentitySha256'):
        require(journal['buildIdentitySha256']==sha(buildraw),'journal build identity original SHA mismatch')
    if journal.get('sourceFingerprint'):
        require(journal['sourceFingerprint']==build['sourceFingerprint'],'journal source identity mismatch')
    report=Audit(args.native_root.resolve(),build,journal).run()
    report.update(buildIdentityFileSha256=sha(buildraw),journalFileSha256=sha(journalraw),checkerSha256=sha(Path(__file__).read_bytes()))
    args.output.parent.mkdir(parents=True,exist_ok=True)
    with args.output.open('x',encoding='utf-8') as file:json.dump(report,file,indent=2,ensure_ascii=False);file.write('\n')
    print(json.dumps({"status":report['status'],"observedScope":report['observedScope'],"missing":len(report['missingPending']),"errors":len(report['errors']),"output":str(args.output)}))
    return 2 if report['errors'] else 3 if report['missingPending'] else 0

if __name__=='__main__':
    try:raise SystemExit(main())
    except (ValueError, OSError, KeyError, TypeError) as error:
        print(json.dumps({"status":"invalid_inputs","error":str(error)}));raise SystemExit(2)
