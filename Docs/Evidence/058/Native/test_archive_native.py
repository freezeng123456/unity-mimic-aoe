#!/usr/bin/env python3
"""Temporary synthetic fixtures only; these tests are not native evidence."""
import copy
import gzip
import json
from pathlib import Path
import struct
import tempfile
from types import SimpleNamespace
import unittest
from unittest import mock

import archive_native as native

KINDS = {'Town Center': 1, 'Wood Crate': 2}


def write_rules(path):
    native.dump(path, {'entities': [{'name': name, 'kind': kind} for name, kind in KINDS.items()]})


def write_capture(root, report, menu=False):
    png = b'\x89PNG\r\n\x1a\n' + b'\0\0\0\rIHDR' + struct.pack('>II', 16, 10)
    mid = '' if menu else report['matchId']
    c = {'buildGuid': report['buildGuid'], 'appVersion': 'synthetic', 'pngFile': 'test.png',
         'pngSha256': native.sha(png), 'width': 16, 'height': 10, 'matchId': mid,
         'requestMatchId': mid, 'utc': 'synthetic', 'worldTime': 0 if menu else 10,
         'requestWorldTime': 0 if menu else 10, 'inMenu': menu, 'paused': False}
    native.dump(root / 'Captures/test.json', c); (root / 'Captures/test.png').write_bytes(png)
    return png


def event(kind, entity=0, player=-1, time=0, message=''):
    return {'schemaVersion': 2, 'time': time, 'kind': kind, 'message': message,
            'player': player, 'entity': entity, 'target': 0, 'amount': 0.0}


def fixture():
    report = {'buildGuid': '1' * 32, 'appVersion': 'synthetic', 'platform': 'OSXPlayer',
              'matchId': '2' * 32, 'eventSchemaVersion': 2, 'eventCount': 5,
              'players': [{'civilization': 'france', 'age': 3}, {'civilization': 'ottoman', 'age': 2}],
              'autoplay': True, 'simulationSpeed': 16, 'reason': 'match_finished',
              'winner': 0, 'time': 10}
    events = [event('Spawn', 1, 0, message='Town Center'),
              event('Spawn', 2, 1, message='Town Center'),
              event('MatchStarted', message='Seed 1; age cap 5; france vs ottoman; synthetic'),
              event('Death', 2, 1, 10, 'Town Center'),
              event('MatchEnded', player=0, time=10, message='All opposing units and buildings eliminated')]
    raw = b''.join((json.dumps(e) + '\n').encode() for e in events)
    return report, events, raw


class Tests(unittest.TestCase):
    def test_strict_json_rejects_duplicate_and_nonfinite(self):
        for raw in ('{"a":1,"a":2}', '{"a":NaN}', '{"a":1e999}'):
            with self.assertRaises(ValueError): native.load(raw)

    def test_lifecycle_and_end_identity_rejected(self):
        report, events, raw = fixture()
        self.assertTrue(native.event_analysis(raw, report, KINDS)['naturallyEnded'])
        depleted = copy.deepcopy(events); depleted[3]['target'] = -1
        self.assertTrue(native.event_analysis(b''.join((json.dumps(e) + '\n').encode() for e in depleted), report, KINDS)['naturallyEnded'])
        for change in ('death', 'winner', 'time', 'civilization'):
            r, rows = copy.deepcopy(report), copy.deepcopy(events)
            if change == 'death': rows[3]['entity'] = 99
            if change == 'winner': r['winner'] = 1
            if change == 'time': rows[4]['time'] = 9
            if change == 'civilization': r['players'][0]['civilization'] = 'dutch'
            b = b''.join((json.dumps(e) + '\n').encode() for e in rows)
            with self.assertRaises(ValueError): native.event_analysis(b, r, KINDS)

    def test_gzip_round_trip_and_damage_rejected(self):
        _, _, raw = fixture(); compressed = gzip.compress(raw, mtime=0)
        self.assertEqual(gzip.decompress(compressed), raw)
        broken = bytearray(compressed); broken[-8] ^= 1
        with self.assertRaises(OSError): gzip.decompress(bytes(broken))

    def test_repeated_report_is_one_match_and_png_identity(self):
        report, _, raw = fixture()
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); (root / 'Reports').mkdir(); (root / 'Captures').mkdir()
            write_rules(root / 'Rules.json')
            for reason in ('match_finished', 'menu_exit'):
                r = {**report, 'reason': reason}
                native.dump(root / 'Reports' / (reason + '.json'), r)
                (root / 'Reports' / (reason + '-events.jsonl.gz')).write_bytes(gzip.compress(raw, mtime=0))
            png = write_capture(root, report)
            summary = native.build_summary(root, report['buildGuid'], 'synthetic')
            self.assertEqual(summary['counts']['reports'], 2)
            self.assertEqual(summary['counts']['naturalAutoplayMatches'], 1)
            self.assertEqual(summary['counts']['distinctMatches'], 1)
            (root / 'Captures/test.png').write_bytes(png + b'damaged')
            with self.assertRaises(ValueError): native.build_summary(root, report['buildGuid'], 'synthetic')

    def test_unfinished_manual_is_not_natural_match(self):
        report, events, _ = fixture()
        report.update(autoplay=False, simulationSpeed=1, reason='quit', winner=-1, eventCount=3, time=1)
        raw = b''.join((json.dumps(e) + '\n').encode() for e in events[:3])
        self.assertFalse(native.event_analysis(raw, report, KINDS)['naturallyEnded'])
        report['reason'] = 'match_finished'
        with self.assertRaises(ValueError): native.event_analysis(raw, report, KINDS)

    def test_archive_exact_identity_and_original_bytes_then_tamper(self):
        report, _, raw = fixture()
        with tempfile.TemporaryDirectory() as directory:
            base = Path(directory); root = base / 'archive'; source = base / 'source'; logs = base / 'logs'
            root.mkdir(); logs.mkdir(); (source / 'Reports').mkdir(parents=True)
            (source / 'Captures').mkdir(); (logs / 'Player.log').write_bytes(b'Synthetic log\n')
            write_capture(source, report); write_rules(source / 'Rules.json')
            native.dump(source / 'Reports/match.json', report)
            (source / 'Reports/match-events.jsonl').write_bytes(raw)
            native.dump(source / 'Reports/old.json', {**report, 'buildGuid': '3' * 32})
            args = SimpleNamespace(source=source, log_source=logs, build_guid=report['buildGuid'], app_version='synthetic', rules=source / 'Rules.json')
            with mock.patch.object(native, '__file__', str(root / 'archive_native.py')):
                result = native.archive(args)
            self.assertTrue(result['verified'])
            self.assertTrue(result['originalNativeBytesCompared'])
            self.assertEqual(result['reports'], 1)
            self.assertFalse((root / 'Reports/old.json').exists())
            self.assertEqual(gzip.decompress((root / 'Reports/match-events.jsonl.gz').read_bytes()), raw)
            (root / 'Reports/match-events.jsonl.gz').write_bytes(b'damaged')
            with self.assertRaises(ValueError): native.verify(root, source, logs)

    def test_loser_resource_is_not_unit_or_building(self):
        report, events, _ = fixture()
        events.insert(3, event('Spawn', 3, 1, 5, 'Wood Crate')); report['eventCount'] += 1
        raw = b''.join((json.dumps(e) + '\n').encode() for e in events)
        a = native.event_analysis(raw, report, KINDS)
        self.assertEqual(a['livingOwnedEntitiesAtExport'], [1, 1])
        self.assertEqual(a['livingOwnedUnitsAndBuildingsAtExport'], [1, 0])
        with self.assertRaises(ValueError): native.event_analysis(raw, report, {'Town Center': 1, 'Wood Crate': 0})

    def test_menu_only_has_zero_matches_and_rejects_live_capture(self):
        report, _, _ = fixture()
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); (root / 'Captures').mkdir()
            write_capture(root, report, menu=True)
            summary = native.build_summary(root, report['buildGuid'], 'synthetic', menu_only=True)
            self.assertEqual(summary['counts']['distinctMatches'], 0)
            self.assertEqual(summary['counts']['reports'], 0)
            self.assertTrue(summary['menuOnly'])
            write_capture(root, report, menu=False)
            with self.assertRaises(ValueError): native.build_summary(root, report['buildGuid'], 'synthetic', menu_only=True)

    def test_menu_only_archive_then_resume_mismatch_rejected(self):
        report, _, _ = fixture()
        with tempfile.TemporaryDirectory() as directory:
            base = Path(directory); root = base / 'archive'; source = base / 'source'; logs = base / 'logs'
            root.mkdir(); logs.mkdir(); (source / 'Captures').mkdir(parents=True)
            write_capture(source, report, menu=True); (logs / 'Player.log').write_bytes(b'Synthetic log\n')
            args = SimpleNamespace(source=source, log_source=logs, build_guid=report['buildGuid'], app_version='synthetic', menu_only=True, destination=root)
            result = native.archive(args)
            self.assertTrue(result['verified']); self.assertEqual(result['distinctMatches'], 0)
            with self.assertRaises(ValueError): native.archive(args)
            (root / 'manifest.json').unlink(); (root / 'manifest.sha256').unlink()
            (root / 'Captures/test.png').write_bytes(b'different'); args.resume = True
            with self.assertRaises(ValueError): native.archive(args)


if __name__ == '__main__':
    unittest.main()
