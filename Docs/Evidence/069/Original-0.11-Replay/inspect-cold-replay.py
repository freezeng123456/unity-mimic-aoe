#!/usr/bin/env python3
"""Bounded post-delivery observation checks; no GUI, simulation or acceptance claim."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import struct
import sys

ROOT = Path(__file__).resolve().parent
BASE = ROOT.parent.parent
CANDIDATE = BASE / 'Candidate3'
ORIGINAL = CANDIDATE / 'Native/host/Replays/replay-c6bb09c7691f46028ffb31a9c94bb54e.replay.json'
ARCHIVED = Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project/Docs/Evidence/068/Native/host/Replays/replay-c6bb09c7691f46028ffb31a9c94bb54e.replay.json')
DESCRIPTOR = CANDIDATE / 'Build/client-build.json'
EXECUTABLE = CANDIDATE / 'Build/Frontier Reckoning.app/Contents/MacOS/Frontier Reckoning'
RUNTIME_DLL = CANDIDATE / 'Build/Frontier Reckoning.app/Contents/Resources/Data/Managed/Frontier.Runtime.dll'
GUID = 'fa26339934914cd2813df9763de6bba7'
COMMIT = 'dca073708b671c60a1fe9ff243a67e4ace3fe157'
FP = '62240c2373c773b5aaf807c72815acab2e76577504f3a0569218702864730ebe'
SEGMENT = 'c6bb09c7691f46028ffb31a9c94bb54e'
MATCH = '94d5ee6efc4945afad8153a3aba978f1'
REPLAY_SHA = 'd9a4534ace1a10737311663ab32fa2556d8e400a414dd6b3732dfbf2be6673c4'
ROSTER = [('coast', 0), ('river', 1), ('france', 0), ('ottoman', 1)]
LIMIT_FILE = 256 * 1024 * 1024
LIMIT_TOTAL = 2 * 1024 * 1024 * 1024

def ensure(condition, reason):
    if not condition:
        raise ValueError(reason)

def utc():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()

def parse_utc(value):
    # Unity's .NET round-trip UTC format can contain seven fraction digits;
    # Python versions with strict ISO parsing accept six microsecond digits.
    value = re.sub(r'(\.\d{6})\d+(?=Z|[+-]\d{2}:\d{2}$)', r'\1', value)
    return datetime.datetime.fromisoformat(value.replace('Z', '+00:00'))

def inside(path, root):
    path, root = Path(path).resolve(), Path(root).resolve()
    ensure(path == root or root in path.parents, 'Path outside requested observation directory')
    return path

def file_record(path, limit=LIMIT_FILE):
    path = Path(path)
    ensure(not path.is_symlink() and path.is_file(), 'Expected regular file')
    initial_stat = path.stat()
    size = initial_stat.st_size
    ensure(size <= limit, 'File exceeds bounded read limit')
    digest = hashlib.sha256()
    with path.open('rb') as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b''):
            digest.update(chunk)
    final_stat = path.stat()
    ensure((final_stat.st_ino, final_stat.st_size, final_stat.st_mtime_ns) ==
           (initial_stat.st_ino, initial_stat.st_size, initial_stat.st_mtime_ns), 'File changed during hashing')
    return {'bytes': size, 'sha256': digest.hexdigest()}

def strict_json(path, limit=16 * 1024 * 1024):
    ensure(Path(path).stat().st_size <= limit, 'JSON exceeds bounded read limit')
    def pairs(values):
        result = {}
        for key, value in values:
            ensure(key not in result, 'Duplicate JSON key')
            result[key] = value
        return result
    return json.loads(Path(path).read_text('utf-8'), object_pairs_hook=pairs,
                      parse_constant=lambda _: (_ for _ in ()).throw(ValueError('Non-finite JSON')))

def write_new(path, value):
    path = inside(path, ROOT)
    with path.open('x', encoding='utf-8') as handle:
        json.dump(value, handle, indent=2, ensure_ascii=False)
        handle.write('\n')

def identity_files(replay_path):
    descriptor = strict_json(DESCRIPTOR)
    ensure(descriptor['appVersion'] == '0.11.0' and descriptor['buildGuid'] == GUID,
           'Build descriptor application identity mismatch')
    ensure(descriptor['runtimeCommit'] == COMMIT and descriptor['sourceFingerprint'] == FP
           and descriptor['sourceInputs'] == 630, 'Build descriptor source identity mismatch')
    ensure(replay_path.resolve() in (ORIGINAL.resolve(), ARCHIVED.resolve()), 'Only frozen Candidate3 or 068 archived LAN replay paths are supported')
    replay = file_record(replay_path)
    ensure(replay == {'bytes': 354649, 'sha256': REPLAY_SHA}, 'Original sealed replay differs')
    executable = file_record(EXECUTABLE)
    ensure(executable == {key: descriptor['executable'][key] for key in ('bytes', 'sha256')},
           'Application executable differs from build descriptor')
    runtime_dll = file_record(RUNTIME_DLL)
    ensure(runtime_dll == {'bytes': 925184, 'sha256': '96c0da0b8729918729222a23a9629e933e98b34e0cc45545f300f4b7594d0995'},
           'Compiled Frontier runtime differs from delivered package member')
    return {'replay': {'path': str(replay_path), **replay},
            'descriptor': {'path': str(DESCRIPTOR), **file_record(DESCRIPTOR)},
            'executable': {'path': str(EXECUTABLE), **executable},
            'compiledRuntime': {'path': str(RUNTIME_DLL), **runtime_dll}}

def protected_record(path):
    path = Path(path).absolute()
    ensure(len(path.parts) >= 5 and any('Frontier' in part for part in path.parts),
           'Protected path must be a specifically selected Frontier game path')
    ensure(not path.is_symlink(), 'Protected root cannot be a symlink')
    if not path.exists():
        return {'path': str(path), 'exists': False, 'entries': []}
    if path.is_file():
        return {'path': str(path), 'exists': True, 'kind': 'file', 'entries': [file_record(path)]}
    ensure(path.is_dir(), 'Protected root is not a file or directory')
    records, total = [], 0
    for folder, dirs, files in os.walk(path, followlinks=False):
        dirs.sort()
        files.sort()
        for name in dirs + files:
            member = Path(folder) / name
            ensure(not member.is_symlink(), 'Protected paths cannot contain symlinks')
            if member.is_dir():
                records.append({'name': member.relative_to(path).as_posix(), 'kind': 'directory'})
            else:
                item = file_record(member)
                total += item['bytes']
                ensure(total <= LIMIT_TOTAL, 'Protected tree exceeds bounded total read limit')
                records.append({'name': member.relative_to(path).as_posix(), 'kind': 'file', **item})
            ensure(len(records) <= 5000, 'Protected tree exceeds member limit')
    return {'path': str(path), 'exists': True, 'kind': 'directory', 'entries': records}

def capture_record(path, replay_path):
    value = strict_json(path, 4 * 1024 * 1024)
    ensure(value.get('schemaVersion') == 5 and value.get('appVersion') == '0.11.0'
           and value.get('buildGuid') == GUID and value.get('unityVersion') == '6000.6.3f1',
           'Capture build identity mismatch')
    ensure(value.get('matchId') == MATCH and not value.get('inMenu'), 'Capture match mismatch')
    replay = value.get('replay') or {}
    ensure(replay.get('watching') and not replay.get('recording'), 'Capture is not detached replay viewing')
    ensure(replay.get('segment') == SEGMENT and replay.get('startStep') == 0
           and replay.get('endStep') == 2210 and not replay.get('incomplete'), 'Capture segment mismatch')
    ensure(replay.get('error') == '' and replay.get('fileError') == ''
           and replay.get('libraryError') == '', 'Capture has replay error')
    identity = replay.get('identity') or {}
    ensure(identity.get('appVersion') == '0.11.0' and identity.get('buildIdentity') == GUID
           and identity.get('unityVersion') == '6000.6.3f1'
           and identity.get('rulesHash') == '5324ece4d54715613e941af737d8da34dcf2d5f517388373516b5e0f17b85d1f',
           'Replay identity mismatch')
    viewed_path = Path(replay.get('file', '')).resolve()
    ensure(viewed_path in (ORIGINAL.resolve(), ARCHIVED.resolve()), 'Different replay source path')
    viewed_hash = file_record(viewed_path)
    ensure(viewed_hash == {'bytes': 354649, 'sha256': REPLAY_SHA}, 'Viewed frozen counterpart differs')
    players = value.get('players', [])
    ensure(len(players) == 4 and [(p['civilization'], p['team']) for p in players] == ROSTER,
           'Capture roster mismatch')
    observer = value.get('observerPlayer')
    ensure(observer in range(4) and observer == replay.get('observerPlayer'), 'Observer mismatch')
    name = value.get('pngFile', '')
    ensure(Path(name).name == name and name.endswith('.png'), 'Invalid PNG basename')
    png = inside(path.parent / name, ROOT)
    png_record = file_record(png, 32 * 1024 * 1024)
    ensure(png_record['sha256'] == value.get('pngSha256'), 'Capture PNG hash mismatch')
    with png.open('rb') as handle:
        header = handle.read(24)
    ensure(header[:8] == b'\x89PNG\r\n\x1a\n' and header[12:16] == b'IHDR', 'Invalid PNG header')
    dimensions = struct.unpack('>II', header[16:24])
    ensure(dimensions == (value.get('width'), value.get('height')), 'Capture PNG dimensions mismatch')
    return {'json': path.relative_to(ROOT).as_posix(), 'jsonHash': file_record(path),
            'png': png.relative_to(ROOT).as_posix(), 'pngHash': png_record,
            'captureFrame': value['captureFrame'], 'worldTime': value['worldTime'],
            'captureUtc': value['utc'], 'replayInputPath': str(viewed_path), 'replayInputHash': viewed_hash,
            'observerPlayer': observer, 'step': replay['currentStep'], 'verified': replay['verified'],
            'seeking': replay['seek'], 'paused': replay['paused'], 'speed': replay['speed'],
            'applicationFocused': value['camera']['applicationFocused'], 'dimensions': list(dimensions)}

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--phase', required=True, choices=('before', 'inspect'))
    parser.add_argument('--replay', help='Frozen Candidate3 original or exact 068 archived replay path; inspect defaults to before receipt')
    parser.add_argument('--protected-path', action='append', default=[])
    parser.add_argument('--before', default=str(ROOT / 'before-hashes.json'))
    parser.add_argument('--captures', default=str(ROOT / 'Captures'))
    parser.add_argument('--player-log', default=str(ROOT / 'player.log'))
    parser.add_argument('--lifecycle', default=str(ROOT / 'process-lifecycle.json'))
    parser.add_argument('--out')
    args = parser.parse_args()
    if args.phase == 'before':
        replay_path = Path(args.replay or ORIGINAL).resolve()
        identities = identity_files(replay_path)
        value = {'schema': 1, 'createdUtc': utc(), 'phase': 'before', 'identityFiles': identities,
                 'selectedProtectedPaths': [protected_record(p) for p in args.protected_path],
                 'selectionScope': 'Only explicitly selected Frontier game paths; hashes only, no content copied.'}
        output = args.out or args.before
    else:
        before_path = inside(args.before, ROOT)
        before = strict_json(before_path)
        ensure(before.get('phase') == 'before', 'Expected before-phase receipt')
        replay_path = Path(args.replay or before['identityFiles']['replay']['path']).resolve()
        # Compare the exact file selected for the invocation-time receipt. The
        # watched archived counterpart is independently bound by every capture
        # and the root's real startup receipt; equal bytes do not erase paths.
        identities = identity_files(Path(before['identityFiles']['replay']['path']).resolve())
        requested_identity = identity_files(replay_path)
        protected = [protected_record(p['path']) for p in before['selectedProtectedPaths']]
        identity_unchanged = before['identityFiles'] == identities
        protected_unchanged = before['selectedProtectedPaths'] == protected
        captures = inside(args.captures, ROOT)
        ensure(captures.is_dir(), 'Capture directory is absent')
        paths = sorted(captures.glob('*.json'))
        ensure(0 < len(paths) <= 200, 'Expected 1..200 capture sidecars')
        records = sorted([capture_record(p, replay_path) for p in paths], key=lambda p: p['captureFrame'])
        initial = [p for p in records if p['step'] == 0 and not p['verified'] and not p['seeking']]
        final = [p for p in records if p['step'] == 2210 and p['verified'] and not p['seeking']]
        chronological = bool(initial and final and initial[0]['captureFrame'] < final[-1]['captureFrame'])
        log_path = inside(args.player_log, ROOT)
        ensure(log_path.is_file() and log_path.stat().st_size <= 32 * 1024 * 1024, 'Owned player log missing or exceeds bound')
        log = log_path.read_text('utf-8', errors='replace')
        opens = re.findall(r'FRONTIER_REPLAY_OPEN file=(.*?) segment=([a-f0-9]+) original=([a-f0-9]+) start=(\d+) end=(\d+)', log)
        viewed_paths = {Path(p['replayInputPath']) for p in records}
        open_matches = [p for p in opens if Path(p[0]).resolve() in viewed_paths
                        and p[1:] == (SEGMENT, MATCH, '0', '2210')]
        errors = sorted(set(re.findall(r'FRONTIER_(?:REPLAY_OPEN_FAILED|REPLAY_SAVE_FAILED|REPORT_FAILED|CAPTURE_FAILED)', log)))
        lifecycle_path = inside(args.lifecycle, ROOT)
        lifecycle = None
        if lifecycle_path.is_file():
            item = strict_json(lifecycle_path, 1024 * 1024)
            lifecycle = {'file': lifecycle_path.relative_to(ROOT).as_posix(), 'hash': file_record(lifecycle_path),
                         'reportedExitCode': item.get('exitCode'), 'reportedPid': item.get('pid'),
                         'reportedExecSession': item.get('execSession'),
                         'reportedNormalQuitObserved': item.get('normalQuitObserved')}
        checks = {'unchangedReplayDescriptorAndExecutable': identity_unchanged,
                  'selectedProtectedPathsUnchanged': protected_unchanged,
                  'initialAndFinalCapturesInFrameOrder': chronological,
                  'exactSegmentOpenLogPresent': bool(open_matches), 'noAllowlistedReplayCaptureErrors': not errors,
                  'fourObserverSlotsCaptured': sorted({p['observerPlayer'] for p in records}) == list(range(4))}
        ensure(all(checks[k] for k in checks if k != 'fourObserverSlotsCaptured'), 'One or more required static checks failed')
        value = {'schema': 1, 'createdUtc': utc(), 'phase': 'inspect', 'buildGuid': GUID,
                 'runtimeCommit': COMMIT, 'sourceFingerprint': FP, 'sourceInputs': 630,
                 'matchId': MATCH, 'segmentId': SEGMENT, 'startStep': 0, 'endStep': 2210,
                 'metadataChecksPassed': True, 'checks': checks, 'beforeReceipt': file_record(before_path),
                 'helperBeforeWindow': 'Invocation-time snapshot; does not certify a startup-before window. Root startup input and pre-PLAY viewer storage receipts are separate.',
                 'helperBeforeWasAfterInitialCapture': parse_utc(before['createdUtc']) > parse_utc(records[0]['captureUtc']),
                 'identityFilesBefore': before['identityFiles'], 'identityFilesAfter': identities,
                 'requestedReplayAtInspection': requested_identity['replay'],
                 'selectedProtectedPathsBefore': before['selectedProtectedPaths'], 'selectedProtectedPathsAfter': protected,
                 'captures': records, 'playerLog': file_record(log_path),
                 'matchingSegmentOpenCount': len(open_matches), 'allowlistedErrors': errors, 'reportedLifecycle': lifecycle,
                 'actualGuiWholeForwardVerification': 'requires separate root UI observation; snapshots cannot exclude an intervening seek',
                 'nativeProcessExitVerification': 'requires root exec handle result; log or reported lifecycle alone does not prove exit',
                 'physicalMacAcceptance': 'pending', 'naturalFullMatchAcceptance': 'pending',
                 'scope': 'Static bounded hashes, capture metadata and allowlisted owned-log markers; no Unity simulation or GUI operations.'}
        output = args.out or str(ROOT / 'inspection.json')
    write_new(output, value)
    print(json.dumps({'output': str(Path(output).resolve()), 'phase': args.phase,
                      'protectedPathCount': len(value.get('selectedProtectedPaths', value.get('selectedProtectedPathsBefore', []))),
                      'captureCount': len(value.get('captures', [])), 'claimsGuiAcceptance': False}))

if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError, KeyError, TypeError, json.JSONDecodeError) as error:
        print(json.dumps({'errorClass': type(error).__name__, 'phaseFailed': True}), file=sys.stderr)
        sys.exit(1)
