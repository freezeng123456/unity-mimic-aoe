"""Controlled byte/XML/client fixtures: verifier tests, never native acceptance."""
import copy
import json
import plistlib
import shutil
import tempfile
import unittest
from argparse import Namespace
from pathlib import Path
from xml.etree import ElementTree as ET
from unittest import mock

import verify_release_equivalence as verifier


class ReleaseEquivalenceToolTests(unittest.TestCase):
    def baseline(self):
        files = {
            verifier.HUD: (verifier.REPLACEMENTS[verifier.HUD][0][0] + "\n"
                           + verifier.REPLACEMENTS[verifier.HUD][1][0] + "\nvoid Logic() {}\n").encode(),
            verifier.SETTINGS: verifier.REPLACEMENTS[verifier.SETTINGS][0][0].encode(),
            "Assets/Frontier/Editor/BuildTools.cs": b"void Prepare() { Metadata(); }\n",
            "Assets/Resources/Rules/units.json": b'{"cost":100}\n',
        }
        files.update({"Assets/fixture-%03d" % number: b"unchanged bytes\n" for number in range(353)})
        self.assertEqual(len(files), 357)
        return files

    def test_exact_two_file_replacement(self):
        old = self.baseline()
        summary = verifier.verify_source_delta(old, verifier.expected_release(old))
        self.assertEqual((summary["changedFileCount"], summary["unchangedFileCount"]), (2, 355))
        self.assertTrue(summary["buildToolsBytesUnchanged"])

    def test_hud_extra_logic_is_rejected(self):
        old = self.baseline()
        current = verifier.expected_release(old)
        current[verifier.HUD] += b"void Cheat() {}\n"
        with self.assertRaisesRegex(ValueError, "exact approved replacement"):
            verifier.verify_source_delta(old, current)

    def test_rules_change_is_rejected(self):
        old = self.baseline()
        current = verifier.expected_release(old)
        current["Assets/Resources/Rules/units.json"] = b'{"cost":0}\n'
        with self.assertRaisesRegex(ValueError, "exact approved replacement"):
            verifier.verify_source_delta(old, current)

    def test_buildtools_version_assignment_is_rejected(self):
        old = self.baseline()
        current = verifier.expected_release(old)
        current["Assets/Frontier/Editor/BuildTools.cs"] += b'PlayerSettings.bundleVersion="0.2.32";\n'
        with self.assertRaisesRegex(ValueError, "exact approved replacement"):
            verifier.verify_source_delta(old, current)

    def test_added_and_removed_source_files_are_rejected(self):
        old = self.baseline()
        for change in ("add", "remove"):
            with self.subTest(change=change):
                current = verifier.expected_release(old)
                if change == "add":
                    current["Tools/extra.py"] = b"pass\n"
                else:
                    del current["Assets/fixture-000"]
                with self.assertRaisesRegex(ValueError, "Source file set changed"):
                    verifier.verify_source_delta(old, current)

    def test_wrong_version_or_unapproved_copy_is_rejected(self):
        old = self.baseline()
        for name in verifier.REPLACEMENTS:
            with self.subTest(path=name):
                current = verifier.expected_release(old)
                current[name] = old[name]
                with self.assertRaisesRegex(ValueError, "exact approved replacement"):
                    verifier.verify_source_delta(old, current)

    def test_replacement_requires_unique_old_literal(self):
        old = self.baseline()
        old[verifier.HUD] += verifier.REPLACEMENTS[verifier.HUD][0][0].encode()
        with self.assertRaisesRegex(ValueError, "exactly once"):
            verifier.expected_release(old)

    def xml(self, path, mode="EditMode", hour=12):
        count = {"EditMode": 214, "PlayMode": 210}[mode]
        root = ET.Element("test-run", {
            "result": "Passed", "testcasecount": str(count), "total": str(count), "passed": str(count),
            "failed": "0", "skipped": "0", "inconclusive": "0",
            "start-time": "2026-10-02 %02d:00:00Z" % hour, "end-time": "2026-10-02 %02d:01:00Z" % hour,
        })
        for index in range(count):
            ET.SubElement(root, "test-case", fullname="Fixture.Case%d" % index, result="Passed")
        ET.ElementTree(root).write(path)
        return root

    def make_tests(self, directory, hour):
        directory.mkdir(parents=True)
        evidence = {"recordedAtUtc": "2026-10-02T%02d:01:00+00:00" % hour, "reports": {}}
        for mode in ("EditMode", "PlayMode"):
            path = directory / (mode + ".xml")
            self.xml(path, mode, hour)
            evidence["reports"][mode] = {"file": path.name, "sha256": verifier.sha(path.read_bytes()),
                                         "passed": {"EditMode": 214, "PlayMode": 210}[mode]}
        return evidence

    def test_new_full_tests_accept_fresh_and_reject_old_xml_reuse(self):
        with tempfile.TemporaryDirectory() as directory:
            old_dir, new_dir = Path(directory) / "old", Path(directory) / "new"
            old_evidence = self.make_tests(old_dir, 12)
            old_results = verifier.verify_tests(old_dir, old_evidence)
            new_evidence = self.make_tests(new_dir, 13)
            self.assertEqual(sum(item["passed"] for item in verifier.verify_tests(new_dir, new_evidence, old_results).values()), 424)
            for mode in ("EditMode", "PlayMode"):
                (new_dir / (mode + ".xml")).write_bytes((old_dir / (mode + ".xml")).read_bytes())
            with self.assertRaisesRegex(ValueError, "reused exact old XML"):
                verifier.verify_tests(new_dir, old_evidence, old_results)
            (new_dir / "EditMode.xml").write_bytes((new_dir / "EditMode.xml").read_bytes() + b"\n")
            copied = copy.deepcopy(old_evidence)
            copied["reports"]["EditMode"]["sha256"] = verifier.sha((new_dir / "EditMode.xml").read_bytes())
            with self.assertRaisesRegex(ValueError, "not newer"):
                verifier.verify_tests(new_dir, copied, old_results)

    def test_tests_recorded_before_run_end_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "tests"
            evidence = self.make_tests(path, 12)
            evidence["recordedAtUtc"] = "2026-10-02T12:00:00+00:00"
            with self.assertRaisesRegex(ValueError, "before its XML"):
                verifier.verify_tests(path, evidence)

    def test_xml_complete_cases_and_duplicate_or_skipped_case(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "EditMode.xml"
            root = self.xml(path)
            names = verifier.verify_xml(path, "EditMode")["names"]
            for bad in ("duplicate", "skipped", "missing", "renamed"):
                with self.subTest(bad=bad):
                    edited = copy.deepcopy(root)
                    cases = list(edited)
                    if bad == "duplicate":
                        cases[-1].set("fullname", cases[0].get("fullname"))
                    elif bad == "skipped":
                        cases[-1].set("result", "Skipped")
                    elif bad == "missing":
                        edited.remove(cases[-1])
                    else:
                        cases[-1].set("fullname", "Fixture.Extra")
                    ET.ElementTree(edited).write(path)
                    with self.assertRaises(ValueError):
                        verifier.verify_xml(path, "EditMode", names)

    def app(self, path):
        old = {"compiledRuntimeSha256": {verifier.RUNTIME_FILES[-1]: verifier.sha(b"old dll")}}
        guid, fp = "1" * 32, "a" * 64
        new = {"appVersion": verifier.NEW_VERSION, "buildGuid": guid, "sourceFingerprint": fp}
        for name in verifier.RUNTIME_FILES:
            item = path / name
            item.parent.mkdir(parents=True, exist_ok=True)
            if name.endswith("Info.plist"):
                item.write_bytes(plistlib.dumps({"CFBundleVersion": verifier.NEW_VERSION,
                                                "CFBundleShortVersionString": verifier.NEW_VERSION}))
            else:
                item.write_bytes(b"new controlled client bytes")
        boot = path / "Contents/Resources/Data/boot.config"
        boot.write_text("build-guid=" + guid + "\n")
        new["compiledRuntimeSha256"] = {name: verifier.sha((path / name).read_bytes()) for name in verifier.RUNTIME_FILES}
        return old, new, fp

    def test_client_identity_and_zero_old_duplicate_guid_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            app = Path(directory)
            old, new, fp = self.app(app)
            self.assertEqual(verifier.verify_app(app, old, new, fp)["buildGuidFromBootConfig"], new["buildGuid"])
            for guid in ("0" * 32, verifier.OLD_GUID, "2" * 32):
                with self.subTest(guid=guid):
                    modified = copy.deepcopy(new)
                    modified["buildGuid"] = guid
                    with self.assertRaises(ValueError):
                        verifier.verify_app(app, old, modified, fp)
            (app / "Contents/Resources/Data/boot.config").write_text("build-guid=" + new["buildGuid"] + "\n" * 2 + "build-guid=" + new["buildGuid"] + "\n")
            with self.assertRaisesRegex(ValueError, "duplicate"):
                verifier.verify_app(app, old, new, fp)

    def test_client_old_dll_and_changed_runtime_bytes_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            app = Path(directory)
            old, new, fp = self.app(app)
            old["compiledRuntimeSha256"][verifier.RUNTIME_FILES[-1]] = new["compiledRuntimeSha256"][verifier.RUNTIME_FILES[-1]]
            with self.assertRaisesRegex(ValueError, "old runtime DLL"):
                verifier.verify_app(app, old, new, fp)
            old["compiledRuntimeSha256"][verifier.RUNTIME_FILES[-1]] = verifier.sha(b"old dll")
            (app / verifier.RUNTIME_FILES[-1]).write_bytes(b"tampered")
            with self.assertRaisesRegex(ValueError, "runtime bytes differ"):
                verifier.verify_app(app, old, new, fp)

    def test_old_native_versions_and_guids_remain_old(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            path = root / "capture.json"
            path.write_text(json.dumps({"appVersion": verifier.OLD_VERSION, "buildGuid": verifier.OLD_GUID}))
            self.assertEqual(len(verifier.old_native_inventory(root)), 1)
            path.write_text(json.dumps({"appVersion": verifier.NEW_VERSION, "buildGuid": verifier.OLD_GUID}))
            with self.assertRaisesRegex(ValueError, "relabeled"):
                verifier.old_native_inventory(root)

    def test_duplicate_json_and_nonfinite_constants_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "data.json"
            for text in ('{"schema":1,"schema":1}', '{"x":NaN}', '{"x":Infinity}', '{"x":1e400}'):
                path.write_text(text)
                with self.assertRaises(ValueError):
                    verifier.strict_json(path)

    def test_source_schema_boolean_is_rejected(self):
        files = {name: verifier.sha(data) for name, data in self.baseline().items()}
        evidence = {"schema": True, "files": files, "sourceFingerprint": verifier.fingerprint(files)}
        with self.assertRaisesRegex(ValueError, "schema"):
            verifier.source_manifest(evidence)

    def test_output_guard_preserves_source_receipts_and_old_evidence(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            new = root / "Docs/Evidence/056"
            args = Namespace(root=root, old_evidence=root / "Docs/Evidence/055", new_evidence=new,
                             app=root / "Builds/client.app", output=new / "release-equivalence.json")
            self.assertEqual(verifier.verify_output_path(args), args.output.resolve())
            for path in (root / "Assets/Rules.json", root / "Tools/release-equivalence.json",
                         args.old_evidence / "release-equivalence.json", new / "build-receipt.json",
                         new / "Tests/source-evidence.json", args.app / "release-equivalence.json"):
                with self.subTest(output=path):
                    args.output = path
                    with self.assertRaises(ValueError):
                        verifier.verify_output_path(args)
            args.new_evidence = root / "Assets"
            args.output = args.new_evidence / "release-equivalence.json"
            with self.assertRaisesRegex(ValueError, "overwrite"):
                verifier.verify_output_path(args)

    def test_end_to_end_temporary_fixture_using_real_old_357_bytes(self):
        # Only fixture files are written. A mocked HEAD/baseline reader is disclosed;
        # this tests CLI audit composition, never a real .32 build/test/native run.
        actual_root = Path(verifier.__file__).resolve().parents[3]
        old_files = verifier.read_baseline(actual_root)
        expected = verifier.expected_release(old_files)
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for name, data in expected.items():
                path = root / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(data)
            old_dir, new_dir = root / "Docs/Evidence/055", root / "Docs/Evidence/056"
            (old_dir / "Tests").mkdir(parents=True)
            (new_dir / "Tests").mkdir(parents=True)
            for relative in ("Tests/source-evidence.json", "Tests/EditMode.xml", "Tests/PlayMode.xml",
                             "build-receipt.json", "client-bundle-manifest.json"):
                shutil.copyfile(actual_root / "Docs/Evidence/055" / relative, old_dir / relative)
            files = {name: verifier.sha(data) for name, data in expected.items()}
            source = {"schema": 1, "recordedAtUtc": "2030-01-01T00:02:00+00:00", "files": files,
                      "sourceFingerprint": verifier.fingerprint(files), "reports": {}}
            for mode in ("EditMode", "PlayMode"):
                xml = ET.parse(old_dir / "Tests" / (mode + ".xml"))
                xml.getroot().set("start-time", "2030-01-01 00:00:00Z")
                xml.getroot().set("end-time", "2030-01-01 00:01:00Z")
                path = new_dir / "Tests" / (mode + ".xml")
                xml.write(path)
                source["reports"][mode] = {"file": path.name, "sha256": verifier.sha(path.read_bytes()),
                                           "passed": {"EditMode": 214, "PlayMode": 210}[mode]}
            (new_dir / "Tests/source-evidence.json").write_text(json.dumps(source))
            app = root / "Builds/client.app"
            _, receipt, _ = self.app(app)
            receipt["sourceFingerprint"] = source["sourceFingerprint"]
            (new_dir / "build-receipt.json").write_text(json.dumps(receipt))
            args = Namespace(root=root, old_evidence=old_dir, new_evidence=new_dir, app=app,
                             output=new_dir / "release-equivalence.json")
            with mock.patch.object(verifier, "read_baseline", return_value=old_files), \
                    mock.patch.object(verifier, "git", return_value=b"f" * 40):
                result = verifier.audit(args)
            self.assertEqual(result["status"], "passed")
            self.assertEqual(result["source"]["unchangedFileCount"], 355)
            self.assertNotEqual(result["newRelease"]["buildGuid"], result["oldRelease"]["buildGuid"])


if __name__ == "__main__":
    unittest.main()
