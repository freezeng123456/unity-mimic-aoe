#!/usr/bin/env python3
"""Verify explicitly identified native WAV/receipt pairs, without a listening verdict."""
from __future__ import annotations

import argparse
import array
import datetime as dt
import hashlib
import json
import math
from pathlib import Path
import re
import struct
import sys


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"Duplicate JSON key: {key}")
        result[key] = value
    return result


def no_nonfinite(value):
    raise ValueError(f"Nonfinite JSON number: {value}")


def load_receipt(path):
    if path.stat().st_size > 128 * 1024:
        raise ValueError("Receipt exceeds the bounded metadata size")
    result = json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=unique_object, parse_constant=no_nonfinite)
    if not isinstance(result, dict):
        raise ValueError("Receipt is not an object")
    return result


def integer(value, name, minimum=0):
    if type(value) is not int or value < minimum:
        raise ValueError(f"{name} is not an integer >= {minimum}")
    return value


def number(value, name, minimum=0):
    if type(value) not in (int, float) or not math.isfinite(value) or value < minimum:
        raise ValueError(f"{name} is not a finite number >= {minimum}")
    return float(value)


def utc(value):
    if not isinstance(value, str):
        raise ValueError("UTC timestamp is not a string")
    # Unity DateTime.ToString("O") emits seven fractional digits. Python 3.9's
    # fromisoformat accepts up to six; discard only its sub-microsecond digit.
    # Validate the wire format before normalizing, preserving explicit UTC.
    match = re.fullmatch(r"(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})(?:\.(\d{1,7}))?(Z|\+00:00)", value)
    if match is None:
        raise ValueError("UTC timestamp is not ISO 8601 with an explicit zero offset")
    fraction = match[2]
    value = match[1] + ("." + fraction[:6] if fraction else "") + match[3]
    result = dt.datetime.fromisoformat(value.replace("Z", "+00:00"))
    if result.tzinfo is None or result.utcoffset() != dt.timedelta(0):
        raise ValueError("UTC timestamp lacks an explicit zero offset")
    return result


def digest(path):
    result = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            result.update(block)
    return result.hexdigest()


def read_pcm(path):
    size = path.stat().st_size
    if size < 44 or size > 44 + 192000 * 20 * 8 * 2:
        raise ValueError("WAV size is outside the capture buffer bounds")
    with path.open("rb") as stream:
        header = stream.read(44)
        magic, riff_size, wave_fmt, fmt_size, encoding, channels, rate, byte_rate, alignment, bits, data_tag, data_size = struct.unpack("<4sI8sIHHIIHH4sI", header)
        if (magic, wave_fmt, fmt_size, encoding, bits, data_tag) != (b"RIFF", b"WAVEfmt ", 16, 1, 16, b"data"):
            raise ValueError("WAV is not the recorder's little-endian PCM16 layout")
        if not 1 <= channels <= 8 or not 8000 <= rate <= 192000:
            raise ValueError("WAV sample rate or channel count is invalid")
        if alignment != channels * 2 or byte_rate != rate * alignment or data_size % alignment:
            raise ValueError("WAV interleaved frame alignment is inconsistent")
        if riff_size != size - 8 or data_size != size - 44:
            raise ValueError("WAV is truncated or has unaccounted trailing data")
        frames = data_size // alignment
        if frames == 0 or frames > rate * 20:
            raise ValueError("WAV has no PCM frames or exceeds 20 seconds")
        pcm = array.array("h"); pcm.frombytes(stream.read())
        if sys.byteorder != "little":
            pcm.byteswap()
    peak = max(abs(value) for value in pcm) / 32767
    rms = math.sqrt(sum(value * value for value in pcm) / len(pcm)) / 32767
    return {"sampleRate": rate, "channels": channels, "frames": frames, "samples": len(pcm),
            "audioSeconds": frames / rate, "pcm16Peak": peak, "pcm16Rms": rms,
            "nonzeroSamples": sum(value != 0 for value in pcm), "bytes": size, "sha256": digest(path)}


def verify_capture(path, build_guid, app_version, match_id, *, signal="any", minimum_seconds=1.0, allow_diagnostic=False):
    receipt = load_receipt(path)
    wav_name = receipt.get("wavFile")
    if not isinstance(wav_name, str) or Path(wav_name).name != wav_name or wav_name != path.stem + ".wav":
        raise ValueError("WAV filename must be the paired receipt basename")
    wav = path.parent / wav_name
    if wav.is_symlink() or not wav.is_file():
        raise ValueError("Paired WAV is missing or a symlink")
    pcm = read_pcm(wav)
    checks = {}
    checks["schemaVersion"] = type(receipt.get("schemaVersion")) is int and receipt["schemaVersion"] == 1
    checks["exactNativeBuild"] = (str(receipt.get("buildGuid", "")).lower() == build_guid.lower()
                                    and receipt.get("appVersion") == app_version and receipt.get("platform") == "OSXPlayer")
    checks["explicitSingleSession"] = (receipt.get("singleSession") is True and receipt.get("matchId") == match_id
                                        and receipt.get("currentMatchId") == match_id)
    checks["nativeDspDataReceived"] = receipt.get("hasDspData") is True and integer(receipt.get("dspBlocks"), "dspBlocks", 1) > 0
    checks["listenerAttachment"] = (receipt.get("listenerEnabledAtStart") is True and receipt.get("listenerHasAudioSource") is False
        and isinstance(receipt.get("listenerObjectEntityId"), str) and bool(receipt["listenerObjectEntityId"])
        and receipt["listenerObjectEntityId"] == receipt.get("captureObjectEntityId"))
    checks["boundedSourcePools"] = integer(receipt.get("eventSources"), "eventSources") == 12 and integer(receipt.get("ambientSources"), "ambientSources") == 2
    checks["wavHash"] = receipt.get("wavSha256") == pcm["sha256"]
    checks["pcmDimensions"] = all(integer(receipt.get(key), key) == pcm[key] for key in ("sampleRate", "channels", "frames", "samples"))
    seconds = number(receipt.get("audioSeconds"), "audioSeconds")
    requested = number(receipt.get("requestedSeconds"), "requestedSeconds")
    checks["captureDuration"] = (requested == 20 and minimum_seconds <= pcm["audioSeconds"] <= 20
                                   and math.isclose(seconds, pcm["audioSeconds"], abs_tol=1e-7))
    peak = number(receipt.get("peak"), "peak"); rms = number(receipt.get("rms"), "rms")
    # PCM16 rounded values differ from the original float mix by <= half an LSB.
    quantization = 0.5 / 32767 + 1e-6
    checks["floatAndPcmMetricsMatch"] = abs(peak - pcm["pcm16Peak"]) <= quantization and abs(rms - pcm["pcm16Rms"]) <= quantization
    checks["finiteUnclippedSignal"] = (integer(receipt.get("nonfiniteSamples"), "nonfiniteSamples") == 0
        and integer(receipt.get("clippedSamples"), "clippedSamples") == 0 and peak < 1 and receipt.get("validSignal") is True)
    checks["noDroppedOrChangedFormat"] = (integer(receipt.get("formatErrors"), "formatErrors") == 0
        and integer(receipt.get("discardedSamples"), "discardedSamples") == 0 and receipt.get("validFormat") is True)
    excluded = integer(receipt.get("samplesExcludedAtDurationLimit"), "samplesExcludedAtDurationLimit")
    checks["durationTruncationIsFrameAligned"] = excluded % pcm["channels"] == 0 and (excluded == 0 or pcm["frames"] == pcm["sampleRate"] * 20)
    start = number(receipt.get("realtimeStart"), "realtimeStart"); end = number(receipt.get("realtimeEnd"), "realtimeEnd")
    world_start = number(receipt.get("worldTimeStart"), "worldTimeStart"); world_end = number(receipt.get("worldTimeEnd"), "worldTimeEnd")
    wall_seconds = end - start
    checks["monotonicWindow"] = end >= start and world_end >= world_start and abs(wall_seconds - seconds) <= .5
    checks["utcWindow"] = abs((utc(receipt.get("utcEnd")) - utc(receipt.get("utcStart"))).total_seconds() - wall_seconds) <= .5
    checks["renderFrameWindow"] = integer(receipt.get("endFrame"), "endFrame") >= integer(receipt.get("startFrame"), "startFrame")
    checks["acceptedStopReason"] = receipt.get("stopReason") in ("f10", "audio_window_complete", "wall_clock_limit", "quit", "requested")
    checks["scopeMetadataTypes"] = (type(receipt.get("autoplay")) is bool and type(receipt.get("benchmarkMode")) is bool
        and type(receipt.get("simulationSpeed")) in (int, float) and math.isfinite(receipt["simulationSpeed"]) and receipt["simulationSpeed"] > 0)
    checks["normalPlayerScopeOrExplicitDiagnostic"] = allow_diagnostic or (receipt.get("autoplay") is False and receipt.get("benchmarkMode") is False
        and math.isclose(number(receipt.get("simulationSpeed"), "simulationSpeed"), 1, abs_tol=1e-6))
    states = receipt.get("stateChanges")
    state_valid = isinstance(states, list) and 1 <= len(states) <= 128 and integer(receipt.get("omittedStateChanges"), "omittedStateChanges") == 0
    previous_realtime, previous_world = start, world_start
    if state_valid:
        for state in states:
            if not isinstance(state, dict): state_valid = False; break
            instant = number(state.get("realtime"), "state realtime"); world_time = number(state.get("worldTime"), "state worldTime")
            state_valid = state_valid and (previous_realtime <= instant <= end and previous_world <= world_time <= world_end)
            state_valid = state_valid and all(type(state.get(key)) is bool for key in ("paused", "muted", "focused", "inMenu", "musicEnabled", "environmentSoundEnabled"))
            previous_realtime, previous_world = instant, world_time
        state_valid = state_valid and states[0]["inMenu"] == (match_id == "")
    checks["boundedStateTimeline"] = state_valid
    checks["requestedSignalContent"] = signal == "any" or (signal == "nonzero" and pcm["nonzeroSamples"] > 0) or (signal == "silence" and pcm["nonzeroSamples"] == 0)
    return {"receiptFile": path.name, "receiptSha256": digest(path), "wavFile": wav.name,
        "identity": {key: receipt.get(key) for key in ("appVersion", "buildGuid", "unityVersion", "platform", "matchId", "autoplay", "benchmarkMode", "simulationSpeed")},
        "checks": checks, "passed": all(checks.values()), "failedChecks": [key for key, value in checks.items() if not value],
        "wav": pcm, "floatMix": {"peak": peak, "rms": rms}, "wallSeconds": wall_seconds,
        "worldTimeRange": [world_start, world_end], "eventAudioPlayedRange": [receipt.get("eventAudioPlayedStart"), receipt.get("eventAudioPlayedEnd")],
        "stateChanges": states, "scope": "WAV and native receipt consistency only. Nonzero PCM proves signal data, not audible quality, speaker output, cue recognition, fog behavior or a listening verdict."}


def identity(value, name):
    if not re.fullmatch(r"[0-9a-fA-F]{32}", value) or int(value, 16) == 0:
        raise ValueError(f"{name} must be an explicit nonzero 32-digit hexadecimal ID")
    return value.lower()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("receipts", nargs="+", type=Path, help="Explicit JSON receipt paths; no newest-file inference")
    parser.add_argument("--build-guid", required=True)
    parser.add_argument("--app-version", required=True)
    parser.add_argument("--match-id", required=True, help="Known match ID, or the literal menu for a deliberately sampled menu")
    parser.add_argument("--signal", choices=("any", "nonzero", "silence"), default="any")
    parser.add_argument("--minimum-seconds", type=float, default=1.0)
    parser.add_argument("--allow-diagnostic", action="store_true", help="Explicitly permit autoplay or a benchmark, preserving those labels")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    try:
        build_guid = identity(args.build_guid, "build GUID")
        match_id = "" if args.match_id == "menu" else identity(args.match_id, "match ID")
        if not math.isfinite(args.minimum_seconds) or not 0 <= args.minimum_seconds <= 20:
            raise ValueError("Minimum duration must be a finite value from 0 to 20")
    except ValueError as error:
        parser.error(str(error))
    results = []
    for path in args.receipts:
        try:
            results.append(verify_capture(path, build_guid, args.app_version, match_id, signal=args.signal,
                minimum_seconds=args.minimum_seconds, allow_diagnostic=args.allow_diagnostic))
        except (OSError, ValueError, KeyError, TypeError, struct.error) as error:
            results.append({"receiptFile": path.name, "passed": False, "error": f"{type(error).__name__}: {error}"})
    result = {"generatedAtUTC": dt.datetime.now(dt.timezone.utc).isoformat(), "passed": all(item["passed"] for item in results),
        "expectedIdentity": {"buildGuid": build_guid, "appVersion": args.app_version, "matchId": match_id},
        "expectedSignal": args.signal, "diagnosticAllowed": args.allow_diagnostic, "captures": results,
        "listeningVerdict": "not performed by this verifier"}
    text = json.dumps(result, ensure_ascii=False, indent=2, allow_nan=False) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        temporary = args.output.with_suffix(args.output.suffix + ".partial"); temporary.write_text(text); temporary.replace(args.output)
        print(json.dumps({"output": str(args.output), "passed": result["passed"], "captureCount": len(results)}))
    else:
        print(text, end="")
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
