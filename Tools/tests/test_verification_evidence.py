"""Regression coverage for rejecting stale or substituted verification evidence."""
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from verification_evidence import MANIFEST, fingerprint, record, source_files, verify


class VerificationEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.source = self.root / 'Assets' / 'Rules.json'
        self.source.parent.mkdir()
        self.source.write_text('{"cost":100}')
        self.reports = self.root / 'Builds' / 'Tests' / 'run-test'
        self.reports.mkdir(parents=True)
        self.xml = '<test-run total="3" passed="3" failed="0" result="Passed" end-time="2026-10-01 10:00:00Z" />'
        for mode in ('EditMode', 'PlayMode'):
            (self.reports / (mode + '.xml')).write_text(self.xml)
        self.original = fingerprint(source_files(self.root))

    def record(self):
        return record(self.root, self.reports, self.original)

    def test_current_source_and_both_reports_are_bound(self):
        self.record()
        self.assertEqual(self.original, verify(self.root, self.reports))

    def test_changed_source_rejects_previously_passing_reports(self):
        self.record()
        self.source.write_text('{"cost":200}')
        with self.assertRaisesRegex(ValueError, 'different source'):
            verify(self.root, self.reports)

    def test_source_edit_during_tests_cannot_be_recorded(self):
        self.source.write_text('{"cost":200}')
        with self.assertRaisesRegex(ValueError, 'Source changed'):
            self.record()
        self.assertFalse((self.reports / MANIFEST).exists())

    def test_replaced_passing_report_is_rejected(self):
        self.record()
        (self.reports / 'PlayMode.xml').write_text(self.xml.replace('3', '4'))
        with self.assertRaisesRegex(ValueError, 'report changed'):
            verify(self.root, self.reports)

    def test_failed_or_incomplete_run_cannot_be_recorded(self):
        for xml in (self.xml.replace('Passed', 'Failed'), self.xml.replace('passed="3"', 'passed="2"'), self.xml.replace('total="3"', 'total="0"')):
            with self.subTest(xml=xml):
                (self.reports / 'EditMode.xml').write_text(xml)
                with self.assertRaisesRegex(ValueError, 'not a complete passing'):
                    self.record()
                self.assertFalse((self.reports / MANIFEST).exists())

    def test_added_or_removed_asset_invalidates_evidence(self):
        self.record()
        added = self.source.with_suffix('.meta')
        added.write_text('guid: abc')
        with self.assertRaisesRegex(ValueError, 'different source'):
            verify(self.root, self.reports)
        added.unlink()
        self.source.unlink()
        with self.assertRaisesRegex(ValueError, 'different source'):
            verify(self.root, self.reports)

    def test_evidence_internal_hash_must_match(self):
        self.record()
        path = self.reports / MANIFEST
        evidence = json.loads(path.read_text())
        evidence['sourceFingerprint'] = 'wrong'
        path.write_text(json.dumps(evidence))
        with self.assertRaisesRegex(ValueError, 'fingerprint is inconsistent'):
            verify(self.root, self.reports)

    def test_generated_reports_and_python_cache_do_not_change_source(self):
        self.record()
        cache = self.root / 'Tools' / '__pycache__'
        cache.mkdir(parents=True)
        (cache / 'module.pyc').write_bytes(b'cached')
        (self.reports / 'log.txt').write_text('new log')
        self.assertEqual(self.original, verify(self.root, self.reports))


if __name__ == '__main__':
    unittest.main()
