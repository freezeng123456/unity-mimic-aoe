"""Controlled verifier regression inputs; never native gameplay/performance evidence."""
import copy
import contextlib
import gzip
import hashlib
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock

import verify_performance as verifier


class PerformanceVerifierTests(unittest.TestCase):
    def frames(self):
        report = {"focusedAtStart": True, "pausedAtStart": True, "realtimeStart": 10, "realtimeEnd": 11.10,
                  "worldTimeStart": 20, "worldTimeEnd": 20.1, "totalFrames": 5, "includedFrames": 2,
                  "excludedFrames": 3, "meanFrameMs": 25, "p95FrameMs": 20, "p99FrameMs": 20, "allFramesFocused": False}
        rows = [(10.02, 20, True, False, False, False, True, 20),
                (10.04, 20.02, True, False, True, False, False, 20),
                (11.04, 20.02, False, True, False, True, True, 1000),
                (11.06, 20.02, True, False, False, False, True, 20),
                (11.09, 20.05, True, False, True, False, False, 30)]
        report["frames"] = [{"frame": 100 + i, "realtime": real, "worldTime": world, "focused": focus,
            "paused": pause, "eligibleInterval": eligible, "focusLostInInterval": focus_lost,
            "pauseChangedInInterval": pause_changed, "frameMs": ms, "simulationMs": 0 if pause else 1}
            for i, (real, world, focus, pause, eligible, focus_lost, pause_changed, ms) in enumerate(rows)]
        return report

    def test_pause_resume_and_focus_neighbors_are_excluded_without_discarding_raw(self):
        report = self.frames(); raw = copy.deepcopy(report["frames"])
        result = verifier.frame_audit(report)
        self.assertTrue(all(result["checks"].values())); self.assertEqual(.05, result["computed"]["eligibleUnpausedSeconds"])
        self.assertEqual(raw, report["frames"]); self.assertEqual(20, result["computed"]["p95FrameMs"])

    def test_forged_resume_eligibility_and_summary_statistics_are_rejected(self):
        report = self.frames(); report["frames"][0]["eligibleInterval"] = True
        self.assertFalse(verifier.frame_audit(report)["checks"]["eligibleFlagsMatchRecomputation"])
        report = self.frames(); report["p95FrameMs"] = 30
        self.assertFalse(verifier.frame_audit(report)["checks"]["floorP95Matches"])

    def test_cross_session_world_time_and_nonconsecutive_frame_are_rejected(self):
        report = self.frames(); report["frames"][4]["worldTime"] = 2
        self.assertFalse(verifier.frame_audit(report)["checks"]["rawFrameStateAndOrderValid"])
        report = self.frames(); report["frames"][4]["frame"] += 1
        self.assertFalse(verifier.frame_audit(report)["checks"]["rawFrameStateAndOrderValid"])

    def test_wall_bound_rejects_synchronized_forged_durations_and_aggregates(self):
        report = self.frames()
        report["frames"][1]["frameMs"] = report["frames"][4]["frameMs"] = 1000000.0
        report.update(meanFrameMs=1000000.0, p95FrameMs=1000000.0, p99FrameMs=1000000.0)
        result = verifier.frame_audit(report)
        self.assertTrue(result["checks"]["meanMatches"])
        self.assertFalse(result["checks"]["eligibleDurationFitsWallEnvelope"])
        self.assertFalse(result["checks"]["rawDurationFitsWallEnvelope"])
        self.assertEqual(2000, result["computed"]["eligibleUnpausedSeconds"])

    def test_wall_bound_allows_fixed_endpoint_phase_and_unsampled_stop_interval(self):
        report = self.frames()
        # Unity delta spans frame boundaries, while timestamps are LateUpdate.
        # This excluded first interval has a legal 100ms endpoint phase skew.
        report["frames"][0]["frameMs"] = 120
        result = verifier.frame_audit(report)
        self.assertTrue(all(result["checks"].values()))
        self.assertAlmostEqual(1.19, result["computed"]["rawFrameIntervalSeconds"])
        report["realtimeEnd"] = 15  # Finish can follow the final sampled frame.
        self.assertTrue(all(verifier.frame_audit(report)["checks"].values()))

    def test_wall_bound_rejects_impossibly_small_interval_total_and_nonfinite_exclusions(self):
        report = self.frames()
        for frame in report["frames"]:
            frame["frameMs"] /= 10
        report.update(meanFrameMs=2.5, p95FrameMs=2, p99FrameMs=2)
        self.assertFalse(verifier.frame_audit(report)["checks"]["rawDurationFitsWallEnvelope"])
        report = self.frames(); report["frames"][2]["frameMs"] = float("nan")
        self.assertFalse(verifier.frame_audit(report)["checks"]["allFrameDurationsFiniteAndNonnegative"])

    def test_strict_json_rejects_duplicate_nonstandard_and_overflow_numbers(self):
        for text in ('{"schemaVersion":1,"schemaVersion":1}', '{"x":NaN}',
                     '{"x":Infinity}', '{"x":1e400}'):
            with self.subTest(text=text), self.assertRaises(ValueError):
                verifier.strict_json(text)
        self.assertEqual({"x": .1}, verifier.strict_json('{"x":0.1}'))
        self.assertFalse(verifier.integer(True))

    def test_diagnostic_requires_explicit_benchmark_one_x_and_never_admits_autoplay(self):
        report = {"autoplay": False, "benchmark": True, "simulationSpeed": 1}
        match = {"autoplay": False, "simulationSpeed": 1}
        self.assertEqual((False, False), verifier.capture_modes(report, match, False))
        self.assertEqual((False, True), verifier.capture_modes(report, match, True))
        report["autoplay"] = True
        self.assertEqual((False, False), verifier.capture_modes(report, match, True))
        report["autoplay"] = False; report["simulationSpeed"] = 16
        self.assertEqual((False, False), verifier.capture_modes(report, match, True))
        report["simulationSpeed"] = 1; report["benchmark"] = False
        self.assertEqual((True, False), verifier.capture_modes(report, match, True))
        report["simulationSpeed"] = True
        self.assertEqual((False, False), verifier.capture_modes(report, match, True))

    def timeline(self):
        temporary = tempfile.TemporaryDirectory(); self.addCleanup(temporary.cleanup)
        path = Path(temporary.name) / "synthetic-events.jsonl"
        events = [{"time": 0, "kind": "Spawn", "message": "Worker", "player": 0, "entity": 1},
                  {"time": 0, "kind": "Spawn", "message": "Resource", "player": -1, "entity": 2},
                  {"time": 0, "kind": "Spawn", "message": "Post", "player": 0, "entity": 3},
                  {"time": .5, "kind": "Spawn", "message": "Guard", "player": -1, "entity": 4},
                  {"time": 1, "kind": "Death", "message": "Worker lost", "player": 0, "entity": 1},
                  {"time": 1, "kind": "Spawn", "message": "Worker", "player": 0, "entity": 5}]
        path.write_text("".join(json.dumps(event) + "\n" for event in events))
        rules = {"entities": [{"name": "Worker", "kind": 0}, {"name": "Resource", "kind": 2},
                              {"name": "Post", "kind": 1}, {"name": "Guard", "kind": 0}]}
        changes, issues, count, _ = verifier.entity_timeline(path, rules)
        self.assertEqual([], issues); self.assertEqual(6, count)
        sample = {"realtime": 10, "worldTime": 1, "managedBytes": 100, "unityAllocatedBytes": 200,
                  "livingUnits": 2, "playerUnits0": 1, "playerUnits1": 0, "neutralUnits": 1,
                  "livingBuildings": 1, "totalEntities": 4, "viewportVisibleUnitCenters": 1}
        report = {"realtimeStart": 10, "realtimeEnd": 11, "worldTimeStart": 1, "worldTimeEnd": 1,
                  "memory": [sample, {**sample, "realtime": 11, "totalEntities": 5}]}
        return changes, report

    def test_shared_tick_accepts_only_an_actual_event_prefix_not_a_guessed_end_state(self):
        changes, report = self.timeline(); result = verifier.memory_audit(report, changes)
        self.assertTrue(all(result["checks"].values()))
        self.assertEqual([4], result["rows"][0]["matchingEventPrefixLines"])
        self.assertEqual([6], result["rows"][1]["matchingEventPrefixLines"])
        report["memory"][0]["totalEntities"] = 99
        self.assertFalse(verifier.memory_audit(report, changes)["checks"]["memoryCountsAndBoundsValid"])

    def test_viewport_count_only_has_a_range_check_and_does_not_claim_event_reconstruction(self):
        changes, report = self.timeline(); report["memory"][0]["viewportVisibleUnitCenters"] = 3
        result = verifier.memory_audit(report, changes)
        self.assertFalse(result["rows"][0]["checks"]["viewportMetricPresentAndInAliveRange"])
        self.assertIn("cannot be reconstructed", result["viewportMetricScope"])

    def test_boolean_count_cannot_pass_as_one_entity_or_memory_byte(self):
        changes, report = self.timeline(); report["memory"][0]["playerUnits0"] = True
        self.assertFalse(verifier.memory_audit(report, changes)["checks"]["memoryCountsAndBoundsValid"])

    def gzip_cli_fixture(self):
        temporary=tempfile.TemporaryDirectory();self.addCleanup(temporary.cleanup)
        root=Path(temporary.name);guid='1'*32;match_id='2'*32
        match_path=root/(match_id+'-match_finished.json');events=match_path.with_name(match_path.stem+'-events.jsonl')
        rules=root/'Rules.json';rules.write_text(json.dumps({'entities':[{'name':'Worker','kind':0},{'name':'Post','kind':1}]}))
        rows=[{'time':0,'kind':'Spawn','message':'Worker','player':0,'entity':1},
              {'time':0,'kind':'Spawn','message':'Post','player':0,'entity':2},
              {'time':20.1,'kind':'Gather','message':'Food','player':0,'entity':1}]
        events.write_text(''.join(json.dumps(e)+'\n' for e in rows))
        match_path.write_text(json.dumps(dict(buildGuid=guid,matchId=match_id,appVersion='0.2.31',platform='OSXPlayer',version='6000.6.3f1',autoplay=False,simulationSpeed=1,time=21,eventCount=len(rows))))
        capture=self.frames();capture.update(buildGuid=guid,matchId=match_id,currentMatchId=match_id,appVersion='0.2.31',platform='OSXPlayer',unityVersion='6000.6.3f1',autoplay=False,simulationSpeed=1,benchmark=False,singleSession=True,truncated=False,schemaVersion=1,gc0=0,gc1=0,gc2=0,cpu='synthetic',gpu='synthetic',resolution='1x1',utcStart='fixture',utcEnd='fixture',stopReason='fixture')
        sample=dict(realtime=10,worldTime=20,managedBytes=100,unityAllocatedBytes=200,livingUnits=1,playerUnits0=1,playerUnits1=0,neutralUnits=0,livingBuildings=1,totalEntities=2,viewportVisibleUnitCenters=1)
        capture['memory']=[sample,{**sample,'realtime':11.1,'worldTime':20.1}]
        capture_path=root/'performance.json';capture_path.write_text(json.dumps(capture))
        base=['verify_performance.py',str(capture_path),'--match-report',str(match_path),'--rules',str(rules),'--build-guid',guid,'--match-id',match_id,'--app-version','0.2.31']
        return root,events,base

    def run_cli_fixture(self,base,archive,extra=()):
        argv=base+['--archive-dir',str(archive),'--output',str(archive/'summary.json')]+list(extra)
        with mock.patch.object(sys,'argv',argv),contextlib.redirect_stdout(io.StringIO()),contextlib.redirect_stderr(io.StringIO()):
            result=verifier.main()
        return result,json.loads((archive/'summary.json').read_text())

    def test_exact_gzip_fallback_preserves_complete_performance_cli_audit(self):
        root,events,base=self.gzip_cli_fixture()
        status,baseline=self.run_cli_fixture(base,root/'raw');self.assertEqual(0,status)
        raw=events.read_bytes();compressed=events.with_suffix('.jsonl.gz');compressed.write_bytes(gzip.compress(raw,mtime=0));events.unlink()
        status,result=self.run_cli_fixture(base,root/'compressed');self.assertEqual(0,status)
        for key in baseline:
            if key not in ('generatedAtUtc','sources'):
                self.assertEqual(baseline[key],result[key],key)
        self.assertEqual(hashlib.sha256(compressed.read_bytes()).hexdigest(),result['sources']['events']['sha256'])
        self.assertEqual(compressed.name,Path(result['sources']['events']['archivedPath']).name)
        self.assertEqual(raw,gzip.decompress(compressed.read_bytes()))

    def test_gzip_damage_and_duplicate_json_are_rejected_by_performance_reader(self):
        root,events,base=self.gzip_cli_fixture();raw=events.read_bytes();encoded=gzip.compress(raw,mtime=0);compressed=events.with_suffix('.jsonl.gz');events.unlink()
        corrupt_crc=bytearray(encoded);corrupt_crc[-8]^=1
        for damaged in (encoded[:-8],bytes(corrupt_crc),b'not gzip'):
            with self.subTest(damage=len(damaged)):
                compressed.write_bytes(damaged)
                with self.assertRaises(SystemExit) as error:self.run_cli_fixture(base,root/'bad')
                self.assertEqual(2,error.exception.code)
        text=raw.decode().splitlines();text[0]=text[0][:-1]+',"entity":1}'
        compressed.write_bytes(gzip.compress(('\n'.join(text)+'\n').encode(),mtime=0))
        with self.assertRaises(ValueError):verifier.entity_timeline(compressed,{'entities':[{'name':'Worker','kind':0}]})

    def test_explicit_gzip_journal_cannot_bypass_associated_basename(self):
        root,events,base=self.gzip_cli_fixture();other=root/'other-events.jsonl.gz';other.write_bytes(gzip.compress(events.read_bytes(),mtime=0))
        status,result=self.run_cli_fixture(base,root/'wrong',['--events',str(other)])
        self.assertEqual(1,status);self.assertFalse(result['checks']['associatedEventFilenameMatches'])


if __name__ == "__main__":
    unittest.main()
