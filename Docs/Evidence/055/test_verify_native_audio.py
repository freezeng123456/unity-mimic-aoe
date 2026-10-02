"""Temporary synthetic pairs test the verifier, never native or listening evidence."""
import copy
import hashlib
import json
from pathlib import Path
import struct
import tempfile
import unittest
import wave

import verify_native_audio as verifier


class NativeAudioVerifierTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(); self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name); self.receipt = self.root / "fixture.json"; self.wav = self.root / "fixture.wav"
        self.pcm = struct.pack("<hh", 4096, -4096) * 8000
        with wave.open(str(self.wav), "wb") as output:
            output.setnchannels(2); output.setsampwidth(2); output.setframerate(8000); output.writeframes(self.pcm)
        self.metadata = {"schemaVersion": 1, "scope": "Synthetic regression fixture, not native evidence", "wavFile": "fixture.wav",
            "wavSha256": hashlib.sha256(self.wav.read_bytes()).hexdigest(), "buildGuid": "1" * 32, "appVersion": "0.2.31",
            "unityVersion": "6000.6.3f1", "platform": "OSXPlayer", "matchId": "2" * 32, "currentMatchId": "2" * 32,
            "singleSession": True, "hasDspData": True, "validFormat": True, "validSignal": True,
            "listenerEnabledAtStart": True, "listenerHasAudioSource": False, "listenerObjectEntityId": "fixture-listener",
            "captureObjectEntityId": "fixture-listener", "eventSources": 12, "ambientSources": 2,
            "sampleRate": 8000, "channels": 2, "frames": 8000, "samples": 16000, "dspBlocks": 16, "requestedSeconds": 20,
            "audioSeconds": 1, "peak": 4096 / 32767, "rms": 4096 / 32767, "nonfiniteSamples": 0, "clippedSamples": 0,
            "formatErrors": 0, "discardedSamples": 0, "samplesExcludedAtDurationLimit": 0, "omittedStateChanges": 0,
            "realtimeStart": 100, "realtimeEnd": 101, "worldTimeStart": 20, "worldTimeEnd": 21, "startFrame": 100, "endFrame": 160,
            "utcStart": "2026-10-02T11:00:00Z", "utcEnd": "2026-10-02T11:00:01Z", "stopReason": "f10",
            "autoplay": False, "benchmarkMode": False, "simulationSpeed": 1, "eventAudioPlayedStart": 0, "eventAudioPlayedEnd": 1,
            "stateChanges": [{"realtime": 100, "worldTime": 20, "paused": False, "muted": False, "focused": True,
                "inMenu": False, "musicEnabled": True, "environmentSoundEnabled": True}]}
        self.save()

    def save(self):
        self.receipt.write_text(json.dumps(self.metadata))

    def verify(self, **kwargs):
        return verifier.verify_capture(self.receipt, "1" * 32, "0.2.31", "2" * 32, **kwargs)

    def test_explicit_pair_dimensions_hash_and_raw_metrics_are_consistent(self):
        result = self.verify(signal="nonzero"); self.assertTrue(result["passed"]); self.assertEqual([], result["failedChecks"])
        self.assertIn("not audible quality", result["scope"])

    def test_replaced_wav_is_rejected_even_when_its_dimensions_match(self):
        changed = bytearray(self.wav.read_bytes()); changed[-1] ^= 1; self.wav.write_bytes(changed)
        self.assertFalse(self.verify()["checks"]["wavHash"])

    def test_truncated_wav_is_rejected_instead_of_parsing_partial_frames(self):
        self.wav.write_bytes(self.wav.read_bytes()[:-4])
        with self.assertRaisesRegex(ValueError, "truncated"):
            self.verify()

    def test_renamed_receipt_cannot_reference_an_unrelated_wave_or_parent_path(self):
        for name in ("other.wav", "../fixture.wav"):
            self.metadata["wavFile"] = name; self.save()
            with self.assertRaisesRegex(ValueError, "basename"):
                self.verify()

    def test_missing_frames_empty_claim_and_forged_source_topology_fail(self):
        for key, value, check in (("samples", 15999, "pcmDimensions"), ("hasDspData", False, "nativeDspDataReceived"),
            ("listenerHasAudioSource", True, "listenerAttachment"), ("captureObjectEntityId", "isolated-source", "listenerAttachment"),
            ("ambientSources", 30, "boundedSourcePools")):
            with self.subTest(key=key):
                original = self.metadata[key]; self.metadata[key] = value; self.save()
                self.assertFalse(self.verify()["checks"][check]); self.metadata[key] = original

    def test_other_guid_version_editor_and_cross_match_never_pass(self):
        for key, value, check in (("buildGuid", "3" * 32, "exactNativeBuild"), ("appVersion", "0.2.30", "exactNativeBuild"),
            ("platform", "OSXEditor", "exactNativeBuild"), ("currentMatchId", "3" * 32, "explicitSingleSession")):
            with self.subTest(key=key):
                original = self.metadata[key]; self.metadata[key] = value; self.save()
                self.assertFalse(self.verify()["checks"][check]); self.metadata[key] = original

    def test_clipping_nonfinite_drops_and_format_flags_override_well_formed_pcm(self):
        for key, value, check in (("clippedSamples", 1, "finiteUnclippedSignal"), ("nonfiniteSamples", 1, "finiteUnclippedSignal"),
            ("validSignal", False, "finiteUnclippedSignal"), ("formatErrors", 1, "noDroppedOrChangedFormat"),
            ("discardedSamples", 1, "noDroppedOrChangedFormat"), ("validFormat", False, "noDroppedOrChangedFormat")):
            with self.subTest(key=key):
                original = self.metadata[key]; self.metadata[key] = value; self.save()
                self.assertFalse(self.verify()["checks"][check]); self.metadata[key] = original

    def test_forged_float_statistics_are_detected_after_pcm_recomputation(self):
        self.metadata["rms"] = .001; self.save()
        self.assertFalse(self.verify()["checks"]["floatAndPcmMetricsMatch"])

    def test_zero_signal_is_a_silence_sample_and_cannot_prove_nonzero_mix(self):
        with wave.open(str(self.wav), "wb") as output:
            output.setnchannels(2); output.setsampwidth(2); output.setframerate(8000); output.writeframes(bytes(len(self.pcm)))
        self.metadata.update(wavSha256=hashlib.sha256(self.wav.read_bytes()).hexdigest(), peak=0, rms=0); self.save()
        self.assertTrue(self.verify(signal="silence")["passed"]); self.assertFalse(self.verify(signal="nonzero")["passed"])

    def test_autoplay_benchmark_and_wrong_speed_require_explicit_diagnostic_scope(self):
        self.metadata.update(autoplay=True, benchmarkMode=True, simulationSpeed=16); self.save()
        self.assertFalse(self.verify()["passed"]); self.assertTrue(self.verify(allow_diagnostic=True)["passed"])

    def test_incomplete_timeline_and_clock_discontinuity_are_rejected(self):
        self.metadata["omittedStateChanges"] = 1; self.save(); self.assertFalse(self.verify()["checks"]["boundedStateTimeline"])
        self.metadata["omittedStateChanges"] = 0; self.metadata["realtimeEnd"] = 105; self.save()
        self.assertFalse(self.verify()["checks"]["monotonicWindow"])

    def test_native_dotnet_seven_digit_utc_is_accepted_without_allowing_non_utc(self):
        self.metadata.update(utcStart="2026-10-02T11:00:00.1234567Z", utcEnd="2026-10-02T11:00:01.1234567Z")
        self.save(); self.assertTrue(self.verify()["passed"])
        self.assertEqual(123456, verifier.utc(self.metadata["utcStart"]).microsecond)
        for value in ("2026-10-02T11:00:00.1234567+08:00", "2026-10-02T11:00:00.1234567",
                      "2026-10-02T11:00:00.12345678Z"):
            with self.assertRaises(ValueError):
                verifier.utc(value)

    def test_duplicate_keys_bool_counts_and_json_nonfinite_values_are_not_accepted(self):
        self.receipt.write_text('{"schemaVersion":1,"schemaVersion":1}')
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            self.verify()
        self.save(); self.metadata["frames"] = True; self.save()
        with self.assertRaisesRegex(ValueError, "integer"):
            self.verify()
        self.metadata["frames"] = 8000; self.metadata["peak"] = float("nan"); self.save()
        with self.assertRaisesRegex(ValueError, "Nonfinite JSON"):
            self.verify()


if __name__ == "__main__":
    unittest.main()
