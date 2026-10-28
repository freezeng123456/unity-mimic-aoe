#!/usr/bin/env python3
"""Archive completed native scale outputs; never launch or control the game.

This thin wrapper reuses the existing F8 auditor and event count reconstruction.
A pressure fixture is diagnostic evidence, never a normal player match.
"""
import argparse
import datetime
import hashlib
import importlib.util
import json
from pathlib import Path
import plistlib
import re
import subprocess
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[4]
AUDITOR = ROOT / 'Docs/Evidence/055/verify_performance.py'


def module(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


def digest(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--units', type=int, choices=(50, 100, 200, 400), required=True)
    parser.add_argument('--benchmark', type=Path, required=True)
    parser.add_argument('--performance', type=Path, required=True)
    parser.add_argument('--match-report', type=Path, required=True)
    parser.add_argument('--events', type=Path)
    parser.add_argument('--capture-metadata', type=Path, required=True,
                        help='Completed benchmark automatic F9 JSON; its PNG is also archived')
    parser.add_argument('--client-log', type=Path, help='Optional completed per-launch log; never read a running client log')
    parser.add_argument('--archive-dir', type=Path, required=True)
    parser.add_argument('--duration-seconds', type=int,
                        help='Explicit launch duration: defaults to 25 short /360 long')
    parser.add_argument('--app', type=Path, default=ROOT / 'Builds/macOS/Frontier Reckoning.app')
    args = parser.parse_args()
    audit = module(AUDITOR, 'scale_existing_performance_auditor')
    sources = module(ROOT / 'Tools/verification_evidence.py', 'scale_source_coverage')
    destination = args.archive_dir.resolve()
    allowed_root = (ROOT / 'Docs/Evidence/055/Performance').resolve()
    if not destination.is_relative_to(allowed_root) or destination == allowed_root:
        parser.error('Archive must be a new case directory under Docs/Evidence/055/Performance')
    if destination.exists() and any(destination.iterdir()):
        parser.error('Use an empty case directory; do not overwrite an earlier attempt')
    duration = args.duration_seconds if args.duration_seconds is not None else 360 if args.units == 400 else 25
    if not 25 <= duration <= 600 or args.units == 400 and duration <= 310:
        parser.error('Duration must be 25..600;400 long fixture must leave room for warmup and >300 eligible seconds')
    try:
        bench = audit.strict_json(args.benchmark.read_text())
        perf = audit.strict_json(args.performance.read_text())
        match = audit.strict_json(args.match_report.read_text())
        cap = audit.strict_json(args.capture_metadata.read_text())
        receipt_path = ROOT / 'Docs/Evidence/055/build-receipt.json'
        evidence_path = ROOT / 'Docs/Evidence/055/Tests/source-evidence.json'
        receipt = audit.strict_json(receipt_path.read_text())
        evidence = audit.strict_json(evidence_path.read_text())
        frozen = sources.source_files(ROOT)
        original_inputs = {str(p.resolve()): digest(p) for p in
                           (args.benchmark, args.performance, args.match_report,
                            args.capture_metadata, receipt_path, evidence_path, AUDITOR, Path(__file__))}
        if args.client_log:
            original_inputs[str(args.client_log.resolve())] = digest(args.client_log)
        events = args.events or args.match_report.with_name(args.match_report.stem + '-events.jsonl')
        if not events.is_file() and args.events is None:
            events = events.with_suffix('.jsonl.gz')
        original_inputs[str(events.resolve())] = digest(events)
        guid = receipt['buildGuid'].lower()
        png_name = cap['pngFile']
        if not isinstance(png_name, str) or Path(png_name).name != png_name:
            raise ValueError('Screenshot must use a simple completed PNG filename')
        png = args.capture_metadata.with_name(png_name)
        original_inputs[str(png.resolve())] = digest(png)
        plist = plistlib.loads((args.app / 'Contents/Info.plist').read_bytes())
        boot = (args.app / 'Contents/Resources/Data/boot.config').read_text()
        boot_guids = re.findall(r'^build-guid=([0-9a-fA-F]{32})$', boot, re.MULTILINE)
        runtime = {p: digest(args.app / p) for p in receipt['compiledRuntimeSha256']}
        changes, issues, _, _ = audit.entity_timeline(events, audit.strict_json((ROOT / 'Assets/Resources/Rules.json').read_text()))
        # After initialization the fixture keeps exactly N units throughout the
        # capture; inspect each actual event count prefix, not population slots.
        before = [state for time, _, state in changes if time <= perf['worldTimeStart'] + .0001]
        active = ([before[-1]] if before else []) + [state for time, _, state in changes
                 if perf['worldTimeStart'] + .0001 < time <= perf['worldTimeEnd'] + .0001]
        finite_metrics = ('meanFrameMs', 'p95Ms', 'p99Ms', 'simulationPerRenderedFrameMs',
                          'navigationPerTickSampleMs', 'combatPerTickSampleMs', 'economyPerTickSampleMs')
        checks = {
            'frozen357SourceMatches055Tests': len(frozen) == 357 and frozen == evidence['files'] and sources.fingerprint(frozen) == evidence['sourceFingerprint'] == receipt['sourceFingerprint'],
            'nonzeroExplicitNativeGuid': bool(re.fullmatch(r'[0-9a-f]{32}', guid)) and int(guid, 16) != 0,
            'bundleRuntimeMatchesReceipt': runtime == receipt['compiledRuntimeSha256'] and plist.get('CFBundleShortVersionString') == '0.2.31' and boot_guids == [guid],
            'sameVersionGuidAndMatch': all(v.get('appVersion') == '0.2.31' and str(v.get('buildGuid')).lower() == guid and v.get('matchId') == bench.get('matchId') for v in (bench, perf, match, cap)) and bool(bench.get('matchId')),
            'sameUnityAndResolution': bench['unity'] == perf['unityVersion'] == match['version'] == cap['unityVersion'] and bench['resolution'] == perf['resolution'] == match['resolution'] == str(cap['width']) + 'x' + str(cap['height']),
            'completedExitReport': match['reason'] in ('quit', 'menu_exit'),
            'explicitDiagnosticMode': perf['benchmark'] is True and perf['autoplay'] is False and match['autoplay'] is False and perf['simulationSpeed'] == match['simulationSpeed'] == 1,
            'requestedDurationAndWarmup': bench['durationSeconds'] == duration and bench['warmupSeconds'] == (10 if duration > 25 else 5),
            'initialAndEndingActiveUnitsMatchRequested': type(bench['initialActiveUnits']) is int and type(bench['endingActiveUnits']) is int and bench['initialActiveUnits'] == bench['endingActiveUnits'] == args.units,
            'fixtureStableActualEventCounts': bool(active) and not issues and all(state['livingUnits'] == args.units and state['playerUnits0'] == state['playerUnits1'] == args.units // 2 and state['neutralUnits'] == state['livingBuildings'] == 0 for state in active),
            'allMemorySnapshotsShowRequestedActualUnits': bool(perf['memory']) and all(m['livingUnits'] == args.units and m['playerUnits0'] == m['playerUnits1'] == args.units // 2 and m['neutralUnits'] == m['livingBuildings'] == 0 for m in perf['memory']),
            'benchmarkMetricsFiniteAndSamplesPositive': all(audit.finite_number(bench[k]) and bench[k] >= 0 for k in finite_metrics) and audit.integer(bench['samples']) and bench['samples'] > 0 and all(audit.integer(bench[k]) for k in ('gc0', 'gc1', 'gc2', 'managedBytes', 'unityAllocatedBytes')) and type(bench['managedGrowthBytes']) is int,
            'automaticSavedScreenshotCompleted': cap['inMenu'] is False and cap['paused'] is True and cap['pngSha256'] == digest(png) and cap['worldTime'] <= match['time'] + .15,
        }
        command = [sys.executable, str(AUDITOR), str(args.performance), '--match-report', str(args.match_report),
                   '--events', str(events), '--build-guid', guid, '--match-id', bench['matchId'], '--app-version', '0.2.31',
                   '--allow-diagnostic', '--archive-dir', str(destination), '--output', str(destination / 'f8-audit.json')]
        if args.units == 400:
            command += ['--min-unpaused-seconds', '300']
        result = subprocess.run(command, text=True, capture_output=True)
        destination.mkdir(parents=True, exist_ok=True)
        (destination / 'f8-audit-cli.txt').write_text(result.stdout + result.stderr)
        summary_path = destination / 'f8-audit.json'
        f8 = audit.strict_json(summary_path.read_text()) if summary_path.is_file() else {}
        checks['existingF8AuditorPassedAsDiagnostic'] = result.returncode == 0 and f8.get('status') == 'passed' and f8.get('diagnostic') is True and f8.get('normalPlayerCapture') is False
        archived = {name: audit.archive(path, destination) for name, path in (
            ('benchmark', args.benchmark), ('screenshotMetadata', args.capture_metadata), ('screenshotPng', png),
            ('buildReceipt', receipt_path), ('sourceEvidence', evidence_path), ('wrapper', Path(__file__)))}
        if args.client_log:
            archived['clientLog'] = audit.archive(args.client_log, destination)
        checks['sourceAndRuntimeUnchangedDuringArchive'] = sources.source_files(ROOT) == frozen and {p: digest(args.app / p) for p in runtime} == runtime
        checks['rawInputsUnchangedDuringArchive'] = all(digest(Path(p)) == sha for p, sha in original_inputs.items())
        checks['archivesMatchRawInputBytes'] = all(v['archiveHashMatches'] for v in archived.values())
        output = {'schemaVersion': 1, 'generatedAtUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  'status': 'passed' if all(checks.values()) else 'failed', 'scope': 'Completed native scale pressure diagnostic; not normal-player, natural-match or whole-viewport evidence',
                  'normalPlayerCapture': False, 'unitsRequested': args.units, 'version': '0.2.31', 'buildGuid': guid,
                  'matchId': bench['matchId'], 'sourceFingerprint': sources.fingerprint(frozen), 'checks': checks,
                  'benchmark': bench, 'f8AuditFile': 'f8-audit.json', 'f8Computed': f8.get('computed'), 'sources': archived,
                  'visibilityScope': 'Actual alive unit counts include both owners. viewportVisibleUnitCenters is only 0..alive range checked by F8 auditor; neither proves every unit unobstructed, outside HUD, on screen, or all400 rendered at once.',
                  'warmupScope': 'Benchmark reports its built-in excluded warmup. F8 is separately started/stopped by the operator; no benchmark raw per-frame file exists to independently recompute benchmark mean or warmup timing.',
                  'failedChecks': [k for k, v in checks.items() if not v]}
        (destination / 'scale-summary.json').write_text(json.dumps(output, ensure_ascii=False, indent=2) + '\n')
        print(json.dumps({'output': str(destination / 'scale-summary.json'), 'status': output['status'], 'failedChecks': output['failedChecks']}, ensure_ascii=False))
        return 0 if output['status'] == 'passed' else 1
    except (OSError, ValueError, KeyError, TypeError) as exc:
        parser.exit(2, 'Cannot archive completed native scale evidence: ' + str(exc) + '\n')


if __name__ == '__main__':
    raise SystemExit(main())
