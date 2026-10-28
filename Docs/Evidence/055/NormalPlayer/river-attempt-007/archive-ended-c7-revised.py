#!/usr/bin/env python3
"""Preserve C7 only after root confirms natural completion. Never operate the game."""
import argparse
import datetime
import hashlib
import json
from pathlib import Path
import re
import struct
import subprocess
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
GUID = "128a731d440e4b7a94abfcc74b88fa97"
VERSION = "0.2.31"
# All runtime inputs must be supplied after natural completion; no live defaults.


def digest(data):
    return {"bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()}


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--natural-end-confirmed', action='store_true', required=True)
    p.add_argument('--match-id', required=True)
    p.add_argument('--menu-capture', type=Path, required=True)
    p.add_argument('--player-log', type=Path, required=True, help='Frozen Player-menu.log')
    ended = p.add_mutually_exclusive_group(required=True)
    ended.add_argument('--ended-log', type=Path, help='Actual frozen Player-ended.log, if it exists')
    ended.add_argument('--ended-log-unavailable', action='store_true',
                       help='Declare missing ended snapshot: natural-end-to-menu-Escape; never fabricate one')
    p.add_argument('--menu-exit-capture', type=Path, required=True)
    p.add_argument('--final-report', type=Path, required=True)
    p.add_argument('--menu-exit-report', type=Path, required=True)
    p.add_argument('--capture-directory', type=Path, required=True)
    p.add_argument('--frozen-app', type=Path, required=True, help='Actual frozen .31 app, checked before .32 changes')
    args = p.parse_args()
    if not re.fullmatch(r'[0-9a-f]{32}', args.match_id):
        p.error('Supply the actual C7 match ID after completion')
    logs_to_check = [(args.player_log, 'Player-menu.log')]
    if args.ended_log is not None:
        logs_to_check.append((args.ended_log, 'Player-ended.log'))
    for log, expected in logs_to_check:
        if log.name != expected or not log.resolve().is_relative_to((ROOT / 'Builds').resolve()):
            p.error('Use explicitly frozen ended/menu snapshots under ignored Builds, never a live Player.log')
    now = datetime.datetime.now(datetime.timezone.utc)
    log_bytes = args.player_log.read_bytes()
    ended_bytes = args.ended_log.read_bytes() if args.ended_log is not None else None
    if ended_bytes is not None and not log_bytes.startswith(ended_bytes):
        raise ValueError('Frozen ended log is not a byte prefix of frozen menu log')
    ended_snapshot = {
        'status': 'unavailable' if args.ended_log_unavailable else 'preserved',
        'sourcePath': str(args.ended_log.resolve()) if args.ended_log is not None else None,
        'reason': 'natural-end-to-menu-Escape' if args.ended_log_unavailable else None,
        'bytesAndSha256': digest(ended_bytes) if ended_bytes is not None else None,
        'prefixCheck': 'not_applicable_no_snapshot' if ended_bytes is None else 'passed',
        'derivedSnapshotCreated': False,
    }
    archive = ROOT / 'Docs/Evidence/055/NormalPlayer/river-attempt-007'
    archive.mkdir(parents=True, exist_ok=True)
    copies = {}

    def write(name, value):
        (archive / name).write_text(json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + '\n')

    def copy(source, name=None):
        name = name or source.name
        data = source.read_bytes()
        destination = archive / name
        if destination.exists():
            if destination.read_bytes() != data:
                raise ValueError('Conflicting archive bytes: ' + str(destination))
        else:
            with destination.open('xb') as stream:
                stream.write(data)
        if source.read_bytes() != data or destination.read_bytes() != data:
            raise ValueError('Source/archive changed during copy: ' + str(source))
        copies[name] = {'sourcePath': str(source.resolve()), **digest(data),
                        'sourceUnchangedDuringCopy': True, 'archivedBytesMatchSource': True}
        return destination

    archived_log = copy(args.player_log, 'native-input-full-Player-menu.log')
    if args.ended_log is not None:
        copy(args.ended_log, 'native-input-full-Player-ended.log')
    write('end-capture-deviation.json', {
        'generatedAtUtc': now.isoformat(),
        'endedLogSnapshot': ended_snapshot,
        'declaredTerminalF9Unavailable': bool(args.ended_log_unavailable),
        'operatorReportedSequence': ('Natural victory appeared before a planned Escape; Escape returned directly to menu before a distinct ended-log snapshot or terminal F9 was saved.'
                                     if args.ended_log_unavailable else None),
        'preservedEvidence': 'Original complete frozen menu log, match_finished/menu_exit reports and events, bounded actual F9 pairs; any separately saved Sky screenshot is documented with its own provenance during finalization.',
        'scope': 'A declared preservation deviation, not replacement evidence. No derived ended snapshot, fabricated F9 or new gameplay acceptance gate. Strict and technology visual requirements remain unchanged.'
    })
    report_sources = [args.final_report, args.final_report.with_name(args.final_report.stem + '-events.jsonl'),
                      args.menu_exit_report, args.menu_exit_report.with_name(args.menu_exit_report.stem + '-events.jsonl')]
    if any(args.match_id not in p.name for p in report_sources):
        raise ValueError('All four explicit report/event filenames must contain the ended match ID')
    for report_source in report_sources:
        copy(report_source)
    finals = [x for x in report_sources if x.name.endswith('-match_finished.json')]
    if len(finals) != 1:
        raise ValueError('Require exactly one actual match_finished report; full log already preserved')
    report = json.loads((archive / finals[0].name).read_text())
    if report.get('matchId') != args.match_id or report.get('winner') not in (0, 1) or report.get('reason') != 'match_finished':
        raise ValueError('Report does not identify a naturally ended C7 match')
    lines = log_bytes.decode('utf-8', errors='replace').splitlines(keepends=True)
    markers = [(i + 1, Path(line.strip().split('FRONTIER_CAPTURE_OK ', 1)[1]).name)
               for i, line in enumerate(lines) if line.startswith('FRONTIER_CAPTURE_OK ')]
    menu_name = args.menu_capture.name
    starts = [i for i, name in markers if name == menu_name]
    if len(starts) != 1:
        raise ValueError('Explicit C7 menu F9 must occur exactly once in the preserved shared log')
    start_line = starts[0]
    stops = [i for i, name in markers if name == args.menu_exit_capture.name]
    if len(stops) != 1 or stops[0] <= start_line:
        raise ValueError('Require exactly one explicit after-menu F9 marker later than the opening menu')
    stop_line = stops[0]
    capture_entries = []
    for line, name in markers:
        if line < start_line or line > stop_line:
            continue
        source = args.capture_directory / name
        receipt = json.loads(source.read_text())
        png_source = source.with_suffix('.png')
        copy(source)
        png = copy(png_source)
        png_bytes = png.read_bytes()
        size = struct.unpack('>II', png_bytes[16:24]) if len(png_bytes) >= 24 and png_bytes[:8] == b'\x89PNG\r\n\x1a\n' else None
        menu = receipt.get('inMenu') is True
        expected = '' if menu else args.match_id
        checks = {'pngFilenameMatches': receipt.get('pngFile') == png.name,
                  'pngShaMatches': receipt.get('pngSha256') == digest(png_bytes)['sha256'],
                  'pngDimensionsMatch': size == (receipt.get('width'), receipt.get('height')),
                  'versionGuidMatches': receipt.get('appVersion') == VERSION and receipt.get('buildGuid') == GUID,
                  'matchIdentityExpected': receipt.get('matchId') == receipt.get('requestMatchId') == expected,
                  'playerPresenceExpected': receipt.get('hasPlayer') == (not menu)}
        if not menu:
            checks.update(hudIdentityMatches=receipt.get('hudMatchId') == args.match_id,
                          worldTimeWithinMatch=isinstance(receipt.get('worldTime'), (int, float)) and 0 <= receipt['worldTime'] <= report['time'] + .15,
                          requestWorldMatches=receipt.get('requestWorldTime') == receipt.get('worldTime'),
                          hudWorldMatches=receipt.get('hudWorldTime') == receipt.get('worldTime'))
        capture_entries.append({'json': name, 'png': png.name, 'sourceLogLine': line,
                                'worldTime': receipt.get('worldTime'), 'inMenu': menu,
                                'paused': receipt.get('paused'), 'checks': checks})
    for source, name in [(ROOT / 'Assets/Resources/Rules.json', 'Rules.json'),
                         (ROOT / 'Assets/Frontier/Simulation/DeckLibrary.cs', 'DeckLibrary.cs'),
                         (ROOT / 'Docs/Evidence/055/Tests/source-evidence.json', '055-source-evidence.json'),
                         (ROOT / 'Docs/Evidence/055/build-receipt.json', '055-build-receipt.json')]:
        copy(source, name)
    for name in ('verify_player.py', 'audit_ai_matrix.py', 'verify_performance.py'):
        copy(ROOT / 'Docs/Evidence/055' / name)
    command = [sys.executable, '-B', str(ROOT / 'Docs/Evidence/055/verify_player.py'),
               str(archive / finals[0].name), '--build-guid', GUID, '--app-version', VERSION,
               '--match-id', args.match_id, '--scenario', 'river-v', '--deck-preset', 'military',
               '--menu-capture', str(archive / menu_name), '--rules', str(archive / 'Rules.json'),
               '--archive-dir', str(archive), '--output', str(archive / 'summary.json')]
    completed = subprocess.run(command, cwd=ROOT, capture_output=True, text=True)
    (archive / 'verify-player-cli.txt').write_text(completed.stdout + completed.stderr)
    summary = json.loads((archive / 'summary.json').read_text())
    write('audit-invocation.json', {'generatedAtUtc': now.isoformat(), 'command': command,
                                  'exitCode': completed.returncode, 'status': summary.get('status'),
                                  'failedChecks': summary.get('failedChecks'), 'coverageGaps': summary.get('coverageGaps')})
    write('capture-audit.json', {'generatedAtUtc': now.isoformat(),
          'status': 'passed' if capture_entries and all(all(e['checks'].values()) for e in capture_entries) else 'failed',
          'scope': 'Every completed F9 marker from the explicitly identified C7 menu through the explicit after-menu capture; bytes and metadata only. Visual interpretation is separate.',
          'captures': capture_entries, 'visualRead': {}})
    write('input-trace-audit.json', {'generatedAtUtc': now.isoformat(), 'preservedFullSource': str(args.player_log),
          'preservedFullSourceHash': digest(log_bytes), 'archivedFullLog': archived_log.name,
          'scope': 'Full frozen menu log retained without rewriting or trimming. Capture window is bounded by explicit C7 opening and after-menu markers. Missing ended snapshot, if declared, is not reconstructed.',
          'endedLogSnapshot': ended_snapshot,
          'sourceLineCount': len(lines), 'captureWindowStartLine': start_line, 'captureWindowEndLine': stop_line,
          'captureCompletionSourceLines': [e['sourceLogLine'] for e in capture_entries]})
    write('source-copy-audit.json', {'generatedAtUtc': now.isoformat(), 'status': 'passed', 'sources': copies})
    frozen = json.loads((archive / '055-source-evidence.json').read_text())
    receipt = json.loads((archive / '055-build-receipt.json').read_text())
    actual_sources = {}
    for directory in ('Assets', 'Packages', 'ProjectSettings', 'Tools'):
        for path in sorted((ROOT / directory).rglob('*')):
            if path.is_file() and not any(part.startswith('.') or part == '__pycache__' for part in path.relative_to(ROOT).parts):
                actual_sources[path.relative_to(ROOT).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
    source_fp = hashlib.sha256(json.dumps(actual_sources, sort_keys=True, separators=(',', ':')).encode()).hexdigest()
    source_differences = sorted(p for p in set(frozen['files']) | set(actual_sources)
                                if frozen['files'].get(p) != actual_sources.get(p))
    runtime = {p: {'expectedSha256': sha, **digest((args.frozen_app / p).read_bytes())}
               for p, sha in receipt['compiledRuntimeSha256'].items()}
    frozen_checks = {'sourceFileCount357': len(actual_sources) == len(frozen['files']) == 357,
                     'allSourceHashesMatch': not source_differences,
                     'fingerprintMatches': source_fp == frozen['sourceFingerprint'] == receipt['sourceFingerprint'] == '1b45f97f3d90c9b76388419492a83c5622ccb2cf3a365030044403ccd309c1c7',
                     'threeRuntimeHashesMatch': len(runtime) == 3 and all(v['sha256'] == v['expectedSha256'] for v in runtime.values()),
                     'reportVersionGuidMatch': report['appVersion'] == receipt['appVersion'] == VERSION and report['buildGuid'] == receipt['buildGuid'] == GUID}
    frozen_status = 'passed' if all(frozen_checks.values()) else 'failed'
    write('frozen-build-audit.json', {'generatedAtUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
          'status': frozen_status, 'checks': frozen_checks, 'sourceFingerprint': source_fp,
          'sourceFileCount': len(actual_sources), 'sourceDifferences': source_differences,
          'currentSourceHashesObservedBefore32': actual_sources, 'runtime': runtime,
          'appPath': str(args.frozen_app.resolve()),
          'scope': 'Completed current .31 source/runtime identity check before any .32 edit/build. Later archive finalization must use this frozen result, not re-read a changed current tree/app.'})
    copy(Path(__file__), 'archive-ended-c7-revised.py')
    write('source-copy-audit.json', {'generatedAtUtc': now.isoformat(), 'status': 'passed', 'sources': copies})
    write('preliminary-archive-receipt.json', {'generatedAtUtc': now.isoformat(), 'status': summary.get('status'),
          'identity': {'appVersion': VERSION, 'buildGuid': GUID, 'matchId': args.match_id},
          'scope': 'Raw preservation and existing strict verifier invocation. Report-pair comparison, key-event interpretation, visual readings and final README/manifest remain separate follow-up.',
          'strictFailedChecks': summary.get('failedChecks'), 'strictCoverageGaps': summary.get('coverageGaps'),
          'files': {x.name: digest(x.read_bytes()) for x in sorted(archive.iterdir()) if x.is_file() and x.name != 'preliminary-archive-receipt.json'}})
    print(json.dumps({'archive': str(archive), 'logPreserved': str(args.player_log), 'capturePairs': len(capture_entries),
                      'strictStatus': summary.get('status'), 'failedChecks': summary.get('failedChecks'),
                      'coverageGaps': summary.get('coverageGaps'), 'verifierExitCode': completed.returncode, 'frozenBuildAuditStatus': frozen_status,
                      'endedLogSnapshot': ended_snapshot, 'declaredTerminalF9Unavailable': bool(args.ended_log_unavailable)}))
    return 0 if frozen_status == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
