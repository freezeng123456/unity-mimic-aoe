#!/usr/bin/env python3
"""Root-operated 077 native launch; import/main are not part of static review."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import plistlib
import re
import stat
import subprocess
import sys
from xml.etree import ElementTree as ET

ROOT = Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
BASE = Path(__file__).resolve().parent
EXPECTED_FP = '8c858fe82932c1c4909af27aea8dab71a183812902c937ee4931003f33840335'
EXPECTED_VERSION = '0.20.0'
EXPECTED_COUNT = 784
BUNDLE_ID = 'com.frontierworkshop.reckoning'
OLD_GUIDS = {'a86c515f3ab040649ea505e507193ff3', 'ed29410c2b27484abeac486c8f57b81b'}
OUTPUT = BASE / 'Native1' / 'winding-view'


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_json(path):
    return json.loads(path.read_text())


def now():
    return datetime.now(timezone.utc).isoformat()


def fingerprint(files):
    return hashlib.sha256(json.dumps(files, sort_keys=True, separators=(',', ':')).encode()).hexdigest()


def source_files():
    # Exact verification_evidence.py semantics; closed Unity leaves no temporary
    # InitTestScene files. A stale fixture changes this fingerprint and stops launch.
    files = {}
    for directory in ('Assets', 'Packages', 'ProjectSettings', 'Tools'):
        for path in sorted((ROOT / directory).rglob('*')):
            if path.is_file() and not any(p.startswith('.') or p == '__pycache__' for p in path.relative_to(ROOT).parts):
                files[path.relative_to(ROOT).as_posix()] = digest(path)
    return files


def guard_processes():
    active = subprocess.check_output(['/bin/ps', '-ww', '-axo', 'uid=,pid=,comm='], text=True)
    blocked = []
    for line in active.splitlines():
        parts = line.strip().split(None, 2)
        if len(parts) != 3 or int(parts[0]) != os.getuid():
            continue
        command = parts[2]
        if command.endswith('/Unity.app/Contents/MacOS/Unity') or command == 'Unity' or command.endswith('/Frontier Reckoning') or command == 'Frontier Reckoning':
            blocked.append({'pid': int(parts[1]), 'command': command})
    require(not blocked, 'Owned Unity Editor/native process still active; preserve it and close through its owner: ' + json.dumps(blocked))
    return {'checkedUtc': now(), 'uid': os.getuid(), 'matchingOwnedProcesses': blocked}


def closed_receipt(name, kind, sources):
    path = BASE / name / 'runner-receipt077.json'
    receipt = read_json(path)
    require(receipt.get('schema') == 1 and receipt.get('kind') == kind, name + ' receipt schema/kind mismatch')
    require(receipt.get('status') == 'closed' and receipt.get('exitCode') == 0, name + ' is not actually closed successfully')
    require(receipt.get('sourceStable') is True and receipt.get('changedSources') == [], name + ' source changed')
    require(receipt.get('sourceFingerprintBefore') == receipt.get('sourceFingerprintAfter') == EXPECTED_FP, name + ' fingerprint mismatch')
    require(receipt.get('sourcesBefore') == sources and len(receipt['sourcesBefore']) == EXPECTED_COUNT, name + ' exact source inventory mismatch')
    require(datetime.fromisoformat(receipt['finishedUtc']) >= datetime.fromisoformat(receipt['startedUtc']), name + ' timestamps incomplete')
    commands = receipt.get('commands', [])
    require(len(commands) == 1 and commands[0].get('status') == 'closed' and commands[0].get('exitCode') == 0 and commands[0].get('finishedUtc'), name + ' command lifecycle not closed')
    expected_command = ['bash', str(ROOT / 'Tools/test.sh')] if kind == 'full' else None
    if expected_command:
        require(commands[0]['command'] == expected_command, 'Full2 was not the actual full-test runner')
    else:
        command = commands[0]['command']
        require('-executeMethod' in command and command[command.index('-executeMethod') + 1] == 'Frontier.Editor.BuildTools.BuildMac', 'Build1 command is not the actual Mac build')
    return path, receipt


def passing_xml(path):
    node = ET.parse(path).getroot()
    attrs = node.attrib
    total = int(attrs.get('total', 0))
    require(node.tag == 'test-run' and attrs.get('result') == 'Passed' and total > 0, 'Incomplete test report: ' + str(path))
    require(int(attrs.get('passed', 0)) == total and int(attrs.get('failed', 0)) == 0 and int(attrs.get('skipped', 0)) == 0 and int(attrs.get('inconclusive', 0)) == 0, 'Nonpassing/omitted test instances: ' + str(path))
    return attrs, total


def validate_full(receipt, sources):
    reports = Path(receipt['reportsDirectory'])
    require(receipt.get('freshReportsDirectories') == [str(reports)], 'Full2 does not bind exactly one fresh report directory')
    require(reports.parent.resolve() == (ROOT / 'Builds/Tests').resolve() and reports.name.startswith('run-'), 'Unexpected Full2 report location')
    evidence_path = reports / 'source-evidence.json'
    evidence = read_json(evidence_path)
    require(evidence.get('schema') == 1 and evidence.get('files') == sources and evidence.get('sourceFingerprint') == EXPECTED_FP, 'Full2 source-evidence binding mismatch')
    result = {}
    for mode in ('EditMode', 'PlayMode'):
        path = reports / (mode + '.xml')
        attrs, total = passing_xml(path)
        h = digest(path)
        require(receipt['tests'][mode] == attrs and receipt['testsXmlSha256'][mode] == h, mode + ' XML/runner mismatch')
        require(evidence['reports'][mode] == {'file': mode + '.xml', 'sha256': h, 'passed': total}, mode + ' XML/source-evidence mismatch')
        result[mode] = {'path': str(path), 'sha256': h, 'passed': total, 'failed': 0}
    return {'reportsDirectory': str(reports), 'sourceEvidenceSha256': digest(evidence_path), 'tests': result}


def validate_app(args, build_path, build):
    manifest_path = BASE / 'Build1/actual-app-manifest077.json'
    require(digest(manifest_path) == args.manifest_sha256, 'Frozen whole app-manifest SHA mismatch')
    manifest = read_json(manifest_path)
    app = BASE / 'Build1/Frontier Reckoning.app'
    require(manifest.get('schema') == 1 and manifest.get('appPath') == str(app), 'App manifest schema/path mismatch')
    require(manifest.get('version') == EXPECTED_VERSION and manifest.get('bundleId') == BUNDLE_ID, 'App manifest version/bundle mismatch')
    require(manifest.get('sourceFingerprint') == EXPECTED_FP and manifest.get('sourceInputCount') == EXPECTED_COUNT, 'App source identity mismatch')
    require(manifest.get('sourceHead') == build['gitHeadBefore'] and manifest.get('runnerReceiptSha256') == digest(build_path), 'App/Build1 receipt binding mismatch')
    require(manifest.get('runnerScriptSha256') == digest(BASE / 'run077.py'), 'Build runner script changed after manifest')
    require(manifest.get('codesignVerification', {}).get('exitCode') == 0, 'Manifest lacks successful codesign verification')
    require(app.is_dir() and not app.is_symlink(), 'Actual app bundle missing or redirected')
    plist = plistlib.loads((app / 'Contents/Info.plist').read_bytes())
    require(plist.get('CFBundleShortVersionString') == EXPECTED_VERSION and plist.get('CFBundleIdentifier') == BUNDLE_ID, 'Actual app plist mismatch')
    executable_name = plist['CFBundleExecutable']
    require(Path(executable_name).name == executable_name, 'Unexpected bundle executable path')
    executable = app / 'Contents/MacOS' / executable_name
    boot = (app / 'Contents/Resources/Data/boot.config').read_text()
    matches = re.findall(r'^build-guid=([0-9a-f]{32})$', boot, re.M)
    require(matches == [args.new_build_guid] and manifest.get('buildGuid') == args.new_build_guid, 'Actual new build GUID mismatch')
    require(args.new_build_guid != '0' * 32 and args.new_build_guid not in OLD_GUIDS, 'Fresh 077 native GUID required, not a prior 076 or zero editor GUID')
    entries = []
    symlinks = []
    for path in sorted(app.rglob('*')):
        if path.is_symlink():
            symlinks.append({'path': path.relative_to(app).as_posix(), 'target': str(path.readlink())})
        elif path.is_file():
            entries.append({'path': path.relative_to(app).as_posix(), 'bytes': path.stat().st_size, 'sha256': digest(path), 'mode': oct(stat.S_IMODE(path.stat().st_mode))})
    require(entries == manifest.get('files') and symlinks == manifest.get('symlinks'), 'Whole actual app file set/SHA/bytes/mode or symlink mismatch')
    require(len(entries) == manifest.get('fileCount') and sum(x['bytes'] for x in entries) == manifest.get('totalFileBytes'), 'App aggregate mismatch')
    require(executable.is_file() and executable.stat().st_mode & stat.S_IXUSR and digest(executable) == manifest.get('executableSha256'), 'Actual native executable/hash/mode mismatch')
    architecture = subprocess.check_output(['/usr/bin/file', str(executable)], text=True).strip()
    require('arm64' in architecture and architecture == manifest.get('architecture'), 'App architecture mismatch')
    signature = subprocess.run(['/usr/bin/codesign', '--verify', '--deep', '--strict', str(app)], capture_output=True, text=True)
    require(signature.returncode == 0, 'Actual codesign verification failed: ' + signature.stderr)
    log = BASE / 'Build1/build.log'
    require('FRONTIER_BUILD Succeeded' in log.read_text(errors='replace'), 'Actual successful Mac build marker missing')
    return executable, {'path': str(app), 'manifestPath': str(manifest_path), 'manifestSha256': args.manifest_sha256, 'version': EXPECTED_VERSION, 'bundleId': BUNDLE_ID, 'buildGuid': args.new_build_guid, 'executableSha256': digest(executable), 'executableMode': oct(stat.S_IMODE(executable.stat().st_mode)), 'fileCount': len(entries), 'totalFileBytes': sum(x['bytes'] for x in entries), 'allFileBytesShaModeAndSymlinksMatch': True, 'architecture': architecture, 'codesignExitCode': signature.returncode, 'buildLogSha256': digest(log)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument('--check-only', action='store_true', help='Read-only prerequisite verification; does not create Native1 or launch')
    mode.add_argument('--launch', action='store_true', help='Root only: detached actual native executable, no GUI actions')
    parser.add_argument('--manifest-sha256', required=True)
    parser.add_argument('--new-build-guid', required=True)
    args = parser.parse_args()
    require(re.fullmatch('[0-9a-f]{64}', args.manifest_sha256) is not None and re.fullmatch('[0-9a-f]{32}', args.new_build_guid) is not None, 'Explicit actual manifest SHA and new GUID required')
    guard = guard_processes()
    require(not OUTPUT.exists() and not OUTPUT.is_symlink(), 'Preserve prior native output; fresh Native1/winding-view required')
    require(not (BASE / 'Native1').is_symlink(), 'Native1 parent must not redirect output')
    sources = source_files()
    require(len(sources) == EXPECTED_COUNT and fingerprint(sources) == EXPECTED_FP, 'Current exact 784-source identity differs')
    full_path, full = closed_receipt('Full2', 'full', sources)
    full_validation = validate_full(full, sources)
    build_path, build = closed_receipt('Build1', 'build', sources)
    executable, app = validate_app(args, build_path, build)
    data = OUTPUT / 'Data'
    logs = OUTPUT / 'logs'
    command = [str(executable), '-frontierDataDirectory', str(data), '-frontierPlayerCiv', 'coast', '-frontierOpponentCiv', 'ottoman', '-frontierMap', 'winding', '-frontierSeed', '42', '-frontierFormat', 'duel', '-frontierInputTrace', '-screen-width', '1920', '-screen-height', '1080', '-screen-fullscreen', '0', '-logFile', str(logs / 'player.log')]
    receipt = {'schema': 1, 'status': 'prerequisites_verified_not_launched', 'phase': 'preflight', 'case': 'winding-view', 'checkedUtc': now(), 'scope': 'Fresh default AgeI coast/ottoman Winding42 duel; root UI Move/Hold/camera/F9 then menu_exit and Quit only. No paid/medical/FPS/win acceptance.', 'helperSha256': digest(Path(__file__)), 'sourceFingerprint': EXPECTED_FP, 'sourceInputCount': EXPECTED_COUNT, 'currentHead': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(), 'fullReceiptPath': str(full_path), 'fullReceiptSha256': digest(full_path), 'fullValidation': full_validation, 'buildReceiptPath': str(build_path), 'buildReceiptSha256': digest(build_path), 'app': app, 'processGuard': guard, 'command': command, 'cwd': str(executable.parent), 'dataDirectory': str(data), 'logsDirectory': str(logs), 'normalMatchRequiresUiBeginSkirmish': True, 'defaultAgeI': 'Normal StartMatch default; no max-age/scenario/autoplay/benchmark/replay/fixture overrides', 'guiAcceptance': 'not_performed', 'pid': None, 'nativeExitCode': None}
    if args.check_only:
        print(json.dumps(receipt, indent=2))
        return 0
    # Recheck immediately before directory reservation and Popen. No process is
    # killed and no completed or failed native folder is reused or removed.
    receipt['processGuardImmediatelyBeforeLaunch'] = guard_processes()
    require(source_files() == sources and digest(full_path) == receipt['fullReceiptSha256'] and digest(build_path) == receipt['buildReceiptSha256'] and digest(BASE / 'Build1/actual-app-manifest077.json') == args.manifest_sha256, 'Source or prerequisite receipt changed during preflight')
    OUTPUT.mkdir(parents=True, exist_ok=False)
    data.mkdir()
    logs.mkdir()
    receipt_path = OUTPUT / 'receipt077.json'
    receipt.update(status='launch_prepared_not_accepted', phase='launch', preparedUtc=now())
    receipt_path.write_text(json.dumps(receipt, indent=2) + '\n')
    try:
        with (logs / 'stdout.log').open('xb') as stream:
            process = subprocess.Popen(command, cwd=executable.parent, stdin=subprocess.DEVNULL, stdout=stream, stderr=subprocess.STDOUT, start_new_session=True, close_fds=True)
        receipt.update(pid=process.pid, startedUtc=now(), nativeExitCode=process.poll(), status='launched_not_accepted')
        if receipt['nativeExitCode'] is not None:
            receipt['status'] = 'exited_before_gui_acceptance'
    except Exception as error:
        receipt.update(status='launch_error_preserved', errorType=type(error).__name__, error=str(error), failedUtc=now())
        receipt_path.write_text(json.dumps(receipt, indent=2) + '\n')
        raise
    receipt_path.write_text(json.dumps(receipt, indent=2) + '\n')
    print(json.dumps({'receiptPath': str(receipt_path), 'status': receipt['status'], 'pid': receipt['pid'], 'nativeExitCode': receipt['nativeExitCode'], 'guiAcceptance': 'not_performed'}, indent=2))
    return 0 if receipt['nativeExitCode'] is None else 1


if __name__ == '__main__':
    try:
        raise SystemExit(main())
    except (OSError, ValueError, KeyError, TypeError, ET.ParseError, subprocess.SubprocessError) as error:
        print('Native077 preflight/launch stopped: ' + str(error), file=sys.stderr)
        raise SystemExit(1)
