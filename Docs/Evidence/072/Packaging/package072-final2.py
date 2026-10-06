#!/usr/bin/env python3
"""Prepare/validate sealed 072 local ZIPs. No Unity, network or account actions.

This is tooling, not a release receipt. --template is deliberately pending.
--self-test uses only a newly created temporary directory beside this script.
The final --package action is unavailable until every explicit seal gate passes.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import plistlib
import re
import stat
import struct
import subprocess
import sys
import tempfile
import unicodedata
import zlib
from xml.etree import ElementTree as ET
import zipfile

HERE = Path(__file__).absolute().parent
CHUNK = 1024 * 1024
EMPTY = hashlib.sha256(b'').hexdigest()
ROOTS = ('Assets', 'Packages', 'ProjectSettings', 'Tools')
PROFILE = 'current-source-workset-with-evidence-070-071-072'
VERSION = '0.15.0'
MATRIX_CHECKER_SHA = '8579ec7a2bfdda5987c830a29bebfd94c1c0c17fc58b04af93f710c63661858c'
MATRIX = [(m, c, s) for m, seeds in (('traders_bend070', (42, 43)), ('winding', (44, 45)))
          for c in ('coast', 'river', 'france', 'ottoman') for s in seeds]
ARCHIVES = {'source': 'source-current-072.zip', 'app': 'client072.zip',
            'evidence': 'evidence072.zip', 'gallery': 'gallery072.zip',
            'normal': 'normal-routes-originals072.zip'}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def utc():
    return datetime.now(timezone.utc).isoformat()


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def pairs(items):
    result = {}
    for key, value in items:
        require(key not in result, 'Duplicate JSON property: ' + key)
        result[key] = value
    return result


def decode(raw):
    return json.loads(raw.decode('utf-8-sig'), object_pairs_hook=pairs,
                      parse_constant=lambda _: require(False, 'Nonfinite JSON'))


def encoded(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2, allow_nan=False) + '\n').encode()


def fingerprint(files):
    return sha(json.dumps(files, sort_keys=True, separators=(',', ':')).encode())


def identity(s):
    return (s.st_dev, s.st_ino, s.st_mode, s.st_size, s.st_mtime_ns, s.st_ctime_ns)


def real_path(path, directory=False):
    path = Path(path).absolute()
    require(path.parent.resolve(strict=True) == path.parent, 'Symlink input ancestry: ' + str(path))
    node = path.lstat()
    require(stat.S_ISDIR(node.st_mode) if directory else stat.S_ISREG(node.st_mode),
            'Expected real ' + ('directory: ' if directory else 'file: ') + str(path))
    return path


class Inputs:
    def __init__(self):
        self.files = {}

    def read(self, path, collect=False, sink=None, dir_fd=None, entry_name=None):
        path = real_path(path)
        before = path.lstat() if dir_fd is None else os.stat(entry_name, dir_fd=dir_fd, follow_symlinks=False)
        require(stat.S_ISREG(before.st_mode), 'Expected regular pinned file')
        digest, count, raw = hashlib.sha256(), 0, bytearray() if collect else None
        fd = os.open(path if dir_fd is None else entry_name, os.O_RDONLY | os.O_NOFOLLOW, dir_fd=dir_fd)
        with os.fdopen(fd, 'rb') as stream:
            require(identity(os.fstat(stream.fileno())) == identity(before), 'File raced on open: ' + str(path))
            for block in iter(lambda: stream.read(CHUNK), b''):
                digest.update(block); count += len(block)
                if raw is not None:
                    raw.extend(block)
                if sink is not None:
                    sink.write(block)
            require(identity(os.fstat(stream.fileno())) == identity(before)
                    and identity(path.lstat()) == identity(before) and count == before.st_size,
                    'File changed while reading: ' + str(path))
        item = {'path': str(path), 'bytes': count, 'sha256': digest.hexdigest(), 'identity': list(identity(before))}
        require(str(path) not in self.files or self.files[str(path)] == item, 'Input differs between reads: ' + str(path))
        self.files[str(path)] = item
        return bytes(raw) if raw is not None else item

    def ref(self, ref, document=True):
        require(isinstance(ref, dict) and set(ref) == {'path', 'bytes', 'sha256'}, 'A pinned path/bytes/SHA reference is required')
        require(re.fullmatch(r'[0-9a-f]{64}', ref['sha256']) and type(ref['bytes']) is int and ref['bytes'] >= 0, 'Invalid pinned reference')
        raw = self.read(ref['path'], collect=document)
        item = self.files[str(Path(ref['path']).absolute())]
        require(item['sha256'] == ref['sha256'] and item['bytes'] == ref['bytes'], 'Pinned reference differs: ' + ref['path'])
        return decode(raw) if document else item

    def unchanged(self):
        for item in list(self.files.values()):
            self.read(item['path'])
        return True


def safe(name):
    parts = name.rstrip('/').split('/')
    require(name and not name.startswith('/') and '\\' not in name
            and all(part not in ('', '.', '..') and ':' not in part and not part.endswith((' ', '.')) for part in parts)
            and not any(ord(c) < 32 or ord(c) == 127 for c in name), 'Unsafe member path: ' + repr(name))
    require(not any(part.lower() in ('.env', '.ssh', '.aws', 'credentials.json', 'secrets.json', 'id_rsa', 'id_ed25519')
                    or part.lower().endswith(('.p12', '.pfx', '.key', '.pem')) for part in parts),
            'Secret-bearing filename requires an explicit new review: ' + name)
    return name


def record(name, kind, mode, size=0, digest=EMPTY, target=None):
    item = {'path': safe(name), 'type': kind, 'mode': mode, 'size': size, 'sha256': digest}
    if target is not None:
        item['symlinkTarget'] = target
    return item


def namespace(records):
    aliases, nodes = {}, {}
    for name, item in records.items():
        safe(name); require(item['path'] == name, 'Inventory key differs')
        key = unicodedata.normalize('NFC', name.rstrip('/')).casefold()
        require(key not in aliases, 'Case/Unicode/path-type alias: ' + name)
        aliases[key] = name; nodes[name.rstrip('/')] = item
    for name in nodes:
        for parent in PurePosixPath(name).parents:
            if str(parent) == '.':
                continue
            require(str(parent) in nodes and nodes[str(parent)]['type'] == 'directory', 'Missing/non-directory member ancestor: ' + name)
    roots = {name.split('/')[0] for name in nodes}
    require(len(roots) == 1, 'Exactly one archive root is required')
    root = next(iter(roots))

    def resolve(parts, visiting):
        resolved = []
        for index, part in enumerate(parts):
            if part in ('', '.'):
                continue
            if part == '..':
                require(len(resolved) > 1, 'Symlink escapes archive root')
                resolved.pop(); continue
            resolved.append(part)
            current = '/'.join(resolved)
            require(current in nodes, 'Dangling symlink target: ' + current)
            node = nodes[current]
            if node['type'] == 'symlink':
                require(current not in visiting and len(visiting) < 40, 'Symlink cycle/depth')
                target = node['symlinkTarget']
                require(target and not target.startswith('/') and '\\' not in target and ':' not in target
                        and not any(ord(c) < 32 or ord(c) == 127 for c in target), 'Unsafe symlink text')
                return resolve(resolved[:-1] + target.split('/') + parts[index+1:], visiting | {current})
            if index < len(parts)-1:
                require(node['type'] == 'directory' or all(p in ('', '.') for p in parts[index+1:]), 'Symlink traverses a regular file')
        require(resolved and resolved[0] == root, 'Symlink root differs')
        return '/'.join(resolved)

    for name, item in nodes.items():
        if item['type'] == 'symlink':
            resolve(name.split('/'), set())


def inventory(directory, prefix, inputs):
    directory = real_path(directory, directory=True); safe(prefix)
    require('/' not in prefix, 'Archive root must be one component')
    records, signatures = {}, {}

    def walk(fd, folder, member):
        before = os.fstat(fd)
        require(stat.S_ISDIR(before.st_mode) and identity(folder.lstat()) == identity(before), 'Directory replaced before inventory')
        signatures[str(folder)] = list(identity(before))
        records[member + '/'] = record(member + '/', 'directory', before.st_mode & 0xffff)
        with os.scandir(fd) as scan:
            names = sorted(entry.name for entry in scan)
        for child_name in names:
            child = folder / child_name
            name = safe(member + '/' + child_name); node = os.stat(child_name, dir_fd=fd, follow_symlinks=False); mode = node.st_mode & 0xffff
            if stat.S_ISDIR(node.st_mode):
                require(child_name not in ('Library', 'Temp', '__pycache__'), 'Unity/Python cache directory must not be packaged: ' + name)
                child_fd = os.open(child_name, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=fd)
                try:
                    require(identity(os.fstat(child_fd)) == identity(node), 'Directory raced on descriptor open')
                    walk(child_fd, child, name)
                finally:
                    os.close(child_fd)
            elif stat.S_ISLNK(node.st_mode):
                target = os.readlink(child_name, dir_fd=fd); raw = os.fsencode(target)
                require(identity(os.stat(child_name, dir_fd=fd, follow_symlinks=False)) == identity(node)
                        and identity(child.lstat()) == identity(node), 'Symlink changed during inventory')
                signatures[str(child)] = list(identity(node))
                records[name] = record(name, 'symlink', mode, len(raw), sha(raw), target)
            elif stat.S_ISREG(node.st_mode):
                item = inputs.read(child, dir_fd=fd, entry_name=child_name)
                require(item['identity'] == list(identity(node)), 'File changed during inventory')
                records[name] = record(name, 'file', mode, item['bytes'], item['sha256'])
            else:
                raise ValueError('Unsupported filesystem type: ' + name)
        require(identity(os.fstat(fd)) == identity(before) and identity(folder.lstat()) == identity(before), 'Directory changed during inventory')

    fd = os.open(directory, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    try:
        walk(fd, directory, prefix)
    finally:
        os.close(fd)
    namespace(records)
    return records, signatures


def git(repo, *args):
    env = dict(os.environ); env['GIT_OPTIONAL_LOCKS'] = '0'
    return subprocess.run(['git', '--no-replace-objects', '-C', str(repo), *args], env=env, check=True,
                          stdout=subprocess.PIPE, stderr=subprocess.PIPE).stdout


def exact_commit(repo, value):
    require(isinstance(value, str) and re.fullmatch(r'[0-9a-f]{40}|[0-9a-f]{64}', value), 'An exact full commit hash is required')
    require(git(repo, 'rev-parse', '--verify', value + '^{commit}').decode().strip() == value, 'Commit identity differs')


def excluded_batch(path):
    match = re.match(r'^Docs/Evidence/([0-9]{3})/', path)
    return match.group(1) if match and match.group(1) not in ('070', '071', '072') else None


def runtime_path(path):
    parts = path.split('/')
    return parts[0] in ROOTS and not any(part.startswith('.') or part == '__pycache__' for part in parts)


def git_records(repo, commit, require_evidence=True):
    exact_commit(repo, commit)
    algorithm = git(repo, 'rev-parse', '--show-object-format').decode().strip()
    records = {'source/': record('source/', 'directory', 0o040755)}
    selected, excluded, runtime = {}, {}, {}
    tree = git(repo, 'ls-tree', '-r', '-z', '--full-tree', commit)
    for row in tree.split(b'\0'):
        if not row:
            continue
        header, raw_name = row.split(b'\t', 1); mode_raw, kind, oid = header.split()
        name = safe(raw_name.decode('utf-8')); mode = int(mode_raw, 8)
        require(kind == b'blob' and mode in (0o100644, 0o100755, 0o120000), 'Non-blob/unsupported Git mode: ' + name)
        require(name not in selected and name not in excluded, 'Duplicate Git path')
        require(name.split('/')[0] not in ('Library', 'Temp', 'Logs', 'obj', 'Builds', 'UserSettings', '.git')
                and not any(p in ('Library', 'Temp', '__pycache__') for p in name.split('/')), 'Tracked Unity cache must not be packaged: ' + name)
        metadata = {'path': name, 'gitTreeMode': mode_raw.decode(), 'gitObjectId': oid.decode()}
        if excluded_batch(name):
            excluded[name] = dict(metadata, excludedByDirectory='Docs/Evidence/' + excluded_batch(name) + '/')
            continue
        raw = git(repo, 'cat-file', 'blob', oid.decode())
        address = hashlib.new(algorithm, b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest()
        require(address == oid.decode(), 'Git object content/address mismatch')
        metadata.update(size=len(raw), sha256=sha(raw)); selected[name] = metadata
        if runtime_path(name):
            require(mode != 0o120000, 'Runtime input symlinks require a new explicit review')
            runtime[name] = metadata['sha256']
        member = 'source/' + name
        for parent in PurePosixPath(member).parents:
            if str(parent) != '.':
                key = str(parent) + '/'; records.setdefault(key, record(key, 'directory', 0o040755))
        records[member] = dict(record(member, 'symlink' if mode == 0o120000 else 'file',
                                     0o120777 if mode == 0o120000 else mode, len(raw), sha(raw),
                                     os.fsdecode(raw) if mode == 0o120000 else None), **metadata)
        records[member]['path'] = member
    namespace(records)
    require(not require_evidence or all(any(p.startswith('Docs/Evidence/' + batch + '/') for p in selected) for batch in ('070', '071', '072')),
            'Source snapshot must actually preserve Evidence 070, 071 and 072')
    return records, selected, excluded, runtime, sha(tree)


def source_map(document, expected, fp):
    files = document.get('files')
    require(files == expected and fingerprint(files) == fp
            and (document.get('fingerprint') or document.get('sourceFingerprint')) == fp, 'Exact complete runtime/source map differs')


def runner(document, operation, fp):
    require(document.get('operation') == operation and document.get('sourceBefore') == document.get('sourceAfter') == fp
            and document.get('sourceStable') is True and document.get('allProcessesSucceeded') is True
            and document.get('finishedAtUtc') and document.get('runs'), 'Incomplete/failed/drifting runner: ' + operation)
    for run in document['runs']:
        require(run.get('exitCode') == 0 and run.get('finishedAtUtc') and run.get('sourceAfter') == fp, 'Incomplete/failed/drifting process')


def test_xml(raw, expected):
    require(b'<!DOCTYPE' not in raw and b'<!ENTITY' not in raw, 'Unexpected XML DTD/entity')
    root = ET.fromstring(raw); attrs = root.attrib
    require(root.tag == 'test-run' and attrs.get('result') == 'Passed' and attrs.get('end-time'), 'No complete passing test run')
    require(expected > 0 and int(attrs.get('total', '0')) == int(attrs.get('passed', '0')) == expected
            and all(int(attrs.get(k, '0')) == 0 for k in ('failed', 'skipped', 'inconclusive')), 'Test totals differ or tests were skipped')
    cases = list(root.iter('test-case'))
    require(len(cases) == expected and all(c.get('result') == 'Passed' for c in cases), 'Actual test-case results/count differ')
    require(all(s.get('result') == 'Passed' for s in root.iter('test-suite')), 'Nested suite not passed')


def same_identity(document, seal):
    require(document.get('sourceFingerprint') == seal['sourceFingerprint'] and document.get('runtimeCommit') == seal['runtimeCommit']
            and document.get('sourceInputs') == seal['sourceInputs'] and document.get('appVersion') == VERSION
            and document.get('buildGuid') == seal['buildGuid'], 'Runtime/build/source identity differs')


def native_identity(document, seal):
    identity_doc, provenance = document.get('identity', {}), document.get('buildProvenance', {})
    wanted = {'sourceFingerprint': seal['sourceFingerprint'], 'sourceInputs': seal['sourceInputs'],
              'runtimeCommit': seal['runtimeCommit'], 'appVersion': VERSION, 'buildGuid': seal['buildGuid']}
    for key, value in wanted.items():
        supplied = [where[key] for where in (document, identity_doc, provenance) if key in where]
        require(supplied and all(item == value for item in supplied), 'Native runtime/build/source identity differs: ' + key)


def runner_files(document, receipt_ref, inputs):
    root = Path(receipt_ref['path']).absolute().parent
    require(document.get('files'), 'Runner must pin its actual original logs/source maps')
    for relative, item in document['files'].items():
        safe(relative)
        inputs.ref({'path': str(root / relative), 'bytes': item['bytes'], 'sha256': item['sha256']}, document=False)


NATIVE_SCOPE_IDS072 = (
    'identity_and_closed_originals', 'normal_opening_and_read_only',
    'normal_muster_departure', 'normal_responsiveness',
    'human_declared_target_attack', 'revolution_payment_clock_conversion',
    'government_title_clock_cancel_requeue', 'save_and_cold_restore_pending_queue',
    'short_actual_forward_replay',
)
NATIVE_PROCESS_IDS072 = ('ProcessD', 'ProcessE', 'ProcessF', 'ProcessG', 'ProcessH')
NATIVE_PLAN_SHA072 = 'd1a7a1a71a0180a7a92f3096c80f3b93b81de3dd00b2c35a0d2fd370a64e8623'
NATIVE_AUDIT_SPECS072 = {
    '072-final-processd-performance-audit.json': ('frontier-independent-final-processd-performance072-1', None),
    '072-final-processd-army-final-audit.json': ('frontier-independent-final-processd-army072-1', None),
    '072-final-processe-audit.json': ('frontier-closed-processe-audit072-1', 'passed_selected_closed_E'),
    '072-final-processe-save-replay-audit.json': (1, 'verified_selected_raw_save_replay_with_scope_limits'),
    '072-final-processf-cold-save-replay-audit.json': (1, 'verified_cold_full_initial_state_and_native_continuation_with_scope_limits'),
    '072-final-processf-continuation-pixels-audit.json': ('frontier-closed-processf-continuation-pixels-audit072-1', 'passed_selected_closed_F'),
    '072-final-processg-camera-readonly-audit.json': ('frontier-normal-observer-camera-audit072-1', 'passed_closed_normal_G_observer_scope'),
    '072-final-processh-forward-audit.json': (1, 'actual_short_forward_observed_with_document_boundary_checks_and_limits'),
}
NATIVE_SCOPE_AUDITS072 = (
    tuple(NATIVE_AUDIT_SPECS072),
    ('072-final-processd-army-final-audit.json', '072-final-processg-camera-readonly-audit.json'),
    ('072-final-processd-army-final-audit.json',),
    ('072-final-processd-performance-audit.json',),
    ('072-final-processe-audit.json', '072-final-processe-save-replay-audit.json'),
    ('072-final-processe-audit.json', '072-final-processe-save-replay-audit.json'),
    ('072-final-processe-audit.json', '072-final-processf-cold-save-replay-audit.json', '072-final-processf-continuation-pixels-audit.json'),
    ('072-final-processf-cold-save-replay-audit.json', '072-final-processf-continuation-pixels-audit.json'),
    ('072-final-processh-forward-audit.json',),
)


def native_gate072_final1(native, seal, inputs):
    """Accept the actual nine bounded native scopes; never assert whole release."""
    native_identity(native, seal)
    require(native.get('schema') == 'frontier-final-native-journal072-1'
            and native.get('status') == 'native_scopes_complete'
            and native.get('releaseAcceptance') == 'accepted_declared_native_scopes_only'
            and native.get('nativeAllPassed') is True and native.get('finalReleaseAccepted') is False
            and native.get('pending') == [] and isinstance(native.get('scope'), str) and native['scope'].strip(),
            'Actual final bounded native schema/status is incomplete or overclaims release')
    for flag in ('originalInputsStableBeforeAndAfter', 'allRequiredProcessesClosed',
                 'rawNativeOriginalTreeFrozen', 'scopePlanOriginalRemainsImmutable', 'selfReferenceExcluded'):
        require(native.get(flag) is True, 'Native evidence flag incomplete: ' + flag)
    require(isinstance(native.get('limits'), list) and native['limits']
            and all(isinstance(limit, str) and limit.strip() for limit in native['limits']),
            'Bounded native limits must remain explicit')
    require(native.get('matrixGate', {}).get('claimedByThisNativeJournal') is False,
            'Native journal must not claim independent matrix acceptance')
    require('manualObservationStatus' not in native,
            'This schema has individual pixel observations, not global manual acceptance')

    original_root = real_path(native['nativeOriginalDirectory'], True)
    require(str(original_root) == seal.get('nativeOriginalDirectory'),
            'Native original directory differs from seal')
    plan_ref = native.get('scopePlan')
    require(plan_ref == {'path': str(HERE / '072-final-native-scope-plan.json'), 'bytes': 20937,
                         'sha256': NATIVE_PLAN_SHA072}, 'Immutable actual scope plan binding differs')
    plan = inputs.ref(plan_ref)
    require(plan.get('schema') == 'frontier-final-native-scope-plan-072-1'
            and plan.get('status') == 'planned_not_observed'
            and plan.get('proposedNativeOriginalDirectory') == str(original_root),
            'Wrong original native plan; do not rewrite its planned status')
    for key, value in plan.get('finalIdentity', {}).items():
        require(value is None or value == native['identity'].get(key),
                'Nonempty original planned identity contradicts actual native identity: ' + key)
    boundary = plan.get('acceptanceBoundary', {})
    for flag in ('requiredScopesMustActuallySucceed', 'failedRequiredScopeCannotBeRenamedLimitToClearPending',
                 'nativeAcceptanceDoesNotCloseFullTestsBuildMatrixOrZIPGates',
                 'noAllPixelsManualReviewOrHostNetworkAttestation'):
        require(boundary.get(flag) is True, 'Actual plan acceptance boundary differs: ' + flag)
    requirements = plan.get('requirements')
    scopes = native.get('scopes')
    require(isinstance(requirements, list) and isinstance(scopes, list)
            and tuple(item.get('id') for item in requirements) == NATIVE_SCOPE_IDS072
            and tuple(item.get('id') for item in scopes) == NATIVE_SCOPE_IDS072,
            'Exactly the nine declared native IDs in plan order are required')

    # The real journal adds relativePath to each original reference. Validate it
    # first, then project the exact triple required by the unchanged hash reader.
    originals = native.get('originalFiles')
    require(isinstance(originals, list) and originals, 'No actual native originals')
    original_refs = {}
    for item in originals:
        require(isinstance(item, dict) and set(item) == {'path', 'bytes', 'sha256', 'relativePath'},
                'Actual native original reference schema differs')
        relative = safe(item['relativePath'])
        require(str(original_root / relative) == item['path'] and item['path'] not in original_refs,
                'Duplicate or outside/misbound native original relative path')
        ref = {key: item[key] for key in ('path', 'bytes', 'sha256')}
        inputs.ref(ref, document=False)
        original_refs[item['path']] = ref
    require(native.get('originalFileCount') == len(originals)
            and native.get('originalBytes') == sum(item['bytes'] for item in originals),
            'Actual native original totals differ')
    excluded = native.get('journalFilesExcludedFromOriginalInventory')
    require(excluded == ['native-journal072-final-running.json', 'native-journal072-final.json'],
            'Only the two actual self-referential journals may be excluded')
    actual_paths = set()
    for path in original_root.rglob('*'):
        mode = path.lstat().st_mode
        require(stat.S_ISREG(mode) or stat.S_ISDIR(mode), 'Unexpected native symlink/special original')
        if stat.S_ISREG(mode) and path.relative_to(original_root).as_posix() not in excluded:
            actual_paths.add(str(path))
    require(actual_paths == set(original_refs), 'Native journal omits/adds an actual raw original')

    outside = native.get('outsideAudits')
    require(isinstance(outside, list) and len(outside) == 2 * len(NATIVE_AUDIT_SPECS072),
            'All eight actual JSON audits and eight MD originals are required')
    outside_refs, audit_docs = {}, {}
    wanted_names = set(NATIVE_AUDIT_SPECS072) | {name[:-5] + '.md' for name in NATIVE_AUDIT_SPECS072}
    for ref in outside:
        require(isinstance(ref, dict) and set(ref) == {'path', 'bytes', 'sha256'},
                'Exact outside audit reference required')
        name = Path(ref['path']).name
        require(name in wanted_names and name not in outside_refs and ref['path'] == str(HERE / name),
                'Unknown/duplicate/outside audit original')
        doc = inputs.ref(ref, document=name.endswith('.json'))
        outside_refs[name] = ref
        if name.endswith('.json'):
            audit_docs[name] = doc
    require(set(outside_refs) == wanted_names, 'Missing actual outside audit original')
    check_count = 0
    for name, (schema, status) in NATIVE_AUDIT_SPECS072.items():
        audit = audit_docs[name]
        require(type(audit.get('schema')) is type(schema) and audit.get('schema') == schema,
                'Actual source audit schema differs: ' + name)
        if status is None:
            require(audit.get('passed') is True and audit.get('failures') == [],
                    'Actual source audit failed: ' + name)
        else:
            require(audit.get('status') == status and audit.get('failedChecks') in ([], 0)
                    and type(audit.get('failedChecks')) is not bool, 'Actual source audit failed: ' + name)
        checks = audit.get('checks')
        require(isinstance(checks, list) and checks and all(check.get('passed') is True for check in checks),
                'Source audit contains failed/empty actual checks: ' + name)
        require('checkCount' not in audit or audit['checkCount'] == len(checks),
                'Source audit actual check count differs: ' + name)
        check_count += len(checks)
    require(native.get('sourceAuditCheckCountSum') == check_count
            and native.get('sourceAuditFailureCount') == 0
            and type(native.get('sourceAuditFailureCount')) is int, 'Native source audit aggregate differs')
    for index, (requirement, scope) in enumerate(zip(requirements, scopes)):
        require(scope.get('status') == 'passed_declared_bounded_native_scope'
                and scope.get('pendingAssertions') == []
                and scope.get('allSourceAuditChecksPassed') is True and scope.get('scopeLimitsExplicit') is True
                and scope.get('scopePlanAssertions') == requirement.get('assertions')
                and scope.get('description') == requirement.get('description')
                and isinstance(scope.get('actualProgress'), str) and scope['actualProgress'].strip(),
                'Required native scope incomplete/misbound: ' + scope['id'])
        expected = [outside_refs[name] for name in NATIVE_SCOPE_AUDITS072[index]]
        require(scope.get('evidenceReferences') == expected,
                'Required native scope actual audit references differ: ' + scope['id'])

    processes = native.get('processes')
    require(isinstance(processes, list)
            and tuple(process.get('id') for process in processes) == NATIVE_PROCESS_IDS072,
            'Exactly the five actual exited native processes are required')
    pids, actual_processes = set(), {}
    for process in processes:
        pid = process.get('pid')
        require(type(pid) is int and pid > 0 and pid not in pids
                and process.get('status') == 'exited' and process.get('exitCode') == 0
                and process.get('buildGuid') == seal['buildGuid'] and process.get('endedAtUtc'),
                'Native process journal is partial/duplicate or identity differs')
        ref = process.get('processMetadata')
        require(ref and ref == original_refs.get(ref.get('path'))
                and ref['path'] == str(original_root / process['id'] / 'process.json'),
                'Native process metadata is not its exact actual raw original')
        actual = inputs.ref(ref)
        require(actual.get('status') == 'exited' and actual.get('exitCode') == 0
                and actual.get('pid', actual.get('processId')) == pid
                and actual.get('endedAtUtc') == process['endedAtUtc']
                and actual.get('startedAtUtc') == process.get('startedAtUtc')
                and actual.get('buildGuid') == seal['buildGuid'] and actual.get('appVersion') == VERSION,
                'Actual process metadata incomplete/PID/clock/identity differs')
        require(actual.get('command') == process.get('command') and actual.get('profile') == process.get('profile'),
                'Actual native command/profile differs')
        pids.add(pid); actual_processes[process['id']] = actual

    captures = native.get('captures')
    require(isinstance(captures, list) and captures, 'No actual native capture references')
    capture_docs = {}
    for capture in captures:
        cid = capture.get('id')
        require(isinstance(cid, str) and cid.startswith('Frontier-') and cid not in capture_docs
                and capture.get('process') in actual_processes and capture.get('metadataAndPngHashChecked') is True,
                'Capture ID/process/hash fact unknown or duplicate')
        for key, suffix in (('json', '.json'), ('png', '.png')):
            ref = capture.get(key)
            require(ref and ref == original_refs.get(ref.get('path')) and Path(ref['path']).name == cid + suffix,
                    'Capture metadata/PNG is not an exact native original')
        meta = inputs.ref(capture['json'])
        require(meta.get('buildGuid') == seal['buildGuid'] and meta.get('appVersion') == VERSION
                and meta.get('pngFile') == cid + '.png' and meta.get('pngSha256') == capture['png']['sha256']
                and meta.get('captureFrame') == capture.get('frame')
                and meta.get('worldTime') == capture.get('worldTime')
                and meta.get('width') == capture.get('width') and meta.get('height') == capture.get('height'),
                'Actual capture identity/pair/frame/projection differs: ' + cid)
        actual = actual_processes[capture['process']]
        require(actual['startedAtUtc'] <= capture['utc'].replace('Z', '+00:00') <= actual['endedAtUtc'],
                'Capture lies outside its actual process clock interval')
        capture_docs[cid] = meta
    observations = native.get('manualObservations')
    require(isinstance(observations, list) and observations, 'No actual selected pixel observations')
    observation_ids = set()
    for observation in observations:
        oid = observation.get('id')
        require(isinstance(oid, str) and oid and oid not in observation_ids
                and observation.get('status') in ('partial_observation_recorded_not_acceptance', 'observed_selected_pixels')
                and observation.get('actuallyObserved') is True
                and isinstance(observation.get('description'), str) and observation['description'].strip()
                and isinstance(observation.get('captureIds'), list) and observation['captureIds']
                and len(set(observation['captureIds'])) == len(observation['captureIds']),
                'Pixel record is unknown/partial/mislabelled or lacks actual description/captures')
        require(all(cid in capture_docs and capture_docs[cid].get('inMenu') is False for cid in observation['captureIds']),
                'Pixel observation refers to missing/menu-only actual captures')
        observation_ids.add(oid)
    require(inputs.unchanged(), 'Native raw originals/audits changed during schema validation')
    return {'status': 'accepted_actual_nine_bounded_native_scopes_only',
            'finalReleaseAccepted': False, 'scopeCount': len(scopes), 'processCount': len(processes),
            'actualPids': sorted(pids), 'rawOriginalFiles': len(originals), 'rawOriginalBytes': native['originalBytes'],
            'outsideOriginals': len(outside), 'sourceAuditCount': len(audit_docs),
            'sourceAuditChecks': check_count, 'captureCount': len(captures),
            'selectedPixelRecords': len(observations), 'limits': native['limits']}



def matrix_audited_refs_gate072_final2(audit, seal, runtime, repo, matrix, inputs):
    """Validate the fixed auditor's complete real 844-row unchanged-input list."""
    rows = audit.get('filesUnchangedDuringAudit')
    require(type(rows) is list and len(rows) == 844, 'Matrix audit requires exactly 844 actual audited-reference records')
    require(len(runtime) == 718 and [(c['map'], c['civilization'], c['seed']) for c in matrix['cases']] == MATRIX,
            'Audited-reference coverage requires the frozen 718-input / 16-case matrix')
    repo = real_path(repo, True); root = real_path(seal['trees']['normal']['directory'], True)
    expected = {str(repo / rel) for rel in runtime}
    expected.update(str(root / name) for name in ('source-before.json', 'source-after.json', 'runner-receipt.json', 'matrix/manifest.json'))
    expected.update((seal['matrixChecker']['path'], seal['inputs']['matrixDeclaration']['path']))
    require(seal['matrixChecker']['path'] == str(HERE / 'independent-normal072.py') and seal['matrixChecker']['sha256'] == MATRIX_CHECKER_SHA,
            'Audited-reference checker must retain the pinned fixed implementation')
    for case in matrix['cases']:
        require(case['id'] == '{}-{}-{}'.format(case['map'], case['civilization'], case['seed']), 'Audited-reference case identity differs')
        names = ['result.json', 'opening-world.json', 'final-world.json', 'final-simulation.json', 'events.jsonl', 'samples.jsonl']
        names.append('comparison-2400-world.json' if case['comparisonReached'] else 'early-terminal-world.json')
        if case['map'] == 'traders_bend070':
            names.append('opening-scenario.json')
        expected.update(str(root / 'matrix' / case['id'] / name) for name in names)
    require(len(expected) == 844, 'Matrix audited-reference namespaces overlap or expected coverage differs')
    by_path = {}
    for row in rows:
        require(type(row) is dict and set(row) == {'path', 'bytes', 'sha256', 'identity'}, 'Malformed matrix audited-reference record')
        path = row['path']; token = row['identity']
        require(type(path) is str and Path(path).is_absolute() and str(Path(path).absolute()) == path and path in expected,
                'Unexpected/noncanonical matrix audited-reference path')
        require(path not in by_path, 'Duplicate matrix audited-reference path')
        require(type(row['bytes']) is int and row['bytes'] >= 0 and type(row['sha256']) is str and re.fullmatch(r'[0-9a-f]{64}', row['sha256']),
                'Malformed matrix audited-reference size/hash')
        require(type(token) is list and len(token) == 6 and all(type(x) is int and x >= 0 for x in token)
                and stat.S_ISREG(token[2]) and token[3] == row['bytes'], 'Malformed matrix audited-reference filesystem identity')
        by_path[path] = row
    require(set(by_path) == expected, 'Missing or extra matrix audited-reference coverage')
    for key in ('matrixDeclaration', 'matrixManifest', 'matrixRunner', 'matrixSourceBefore', 'matrixSourceAfter'):
        pinned = seal['inputs'][key]
        require(pinned['path'] in by_path and {k: by_path[pinned['path']][k] for k in ('path', 'bytes', 'sha256')} == pinned,
                'Matrix audited-reference differs from a sealed matrix input: ' + key)
    checker = seal['matrixChecker']
    require({k: by_path[checker['path']][k] for k in ('path', 'bytes', 'sha256')} == checker, 'Matrix audited-reference differs from pinned checker')
    for rel, expected_hash in runtime.items():
        require(by_path[str(repo / rel)]['sha256'] == expected_hash, 'Matrix audited runtime reference differs from frozen source map')
    for row in rows:
        actual = inputs.read(row['path'])
        require(actual == row, 'Matrix audited-reference bytes/hash/identity no longer match: ' + row['path'])
    require(inputs.unchanged() is True, 'Matrix audited-reference input stability failed')
    return True


def check_gates(seal, inputs):
    require(seal.get('schema') == 'frontier-local-package-seal-072-1' and seal.get('status') == 'sealed'
            and seal.get('sourceSealed') is True and seal.get('scope') == 'local_only_no_upload', 'Pending/unsealed approval; no final package allowed')
    require(seal.get('profile') == PROFILE and seal.get('appVersion') == VERSION, 'Wrong release/profile')
    require(re.fullmatch(r'[0-9a-f]{64}', seal.get('sourceFingerprint', '')) and seal.get('sourceInputs', 0) > 0
            and re.fullmatch(r'[0-9a-f]{32}', seal.get('buildGuid', '')), 'Exact final fingerprint/count/build GUID required')
    repo = real_path(seal['repo'], directory=True)
    exact_commit(repo, seal['sourceSnapshotCommit']); exact_commit(repo, seal['runtimeCommit'])
    source, selected, excluded, runtime, tree_hash = git_records(repo, seal['sourceSnapshotCommit'])
    _, _, _, runtime_at_build, _ = git_records(repo, seal['runtimeCommit'], require_evidence=False)
    require(runtime == runtime_at_build and len(runtime) == seal['sourceInputs'] and fingerprint(runtime) == seal['sourceFingerprint'], 'Snapshot/runtime commit source differs')
    refs = seal['inputs']; fp = seal['sourceFingerprint']
    docs = {key: inputs.ref(ref) for key, ref in refs.items()}
    for key in ('sourceFreeze', 'testsEvidence', 'buildSourceBefore', 'buildSourceAfter', 'matrixSourceBefore', 'matrixSourceAfter'):
        source_map(docs[key], runtime, fp)
    runner(docs['buildRunner'], 'build', fp)
    runner(docs['matrixRunner'], 'routes', fp)
    runner_files(docs['buildRunner'], refs['buildRunner'], inputs)
    runner_files(docs['matrixRunner'], refs['matrixRunner'], inputs)
    full_tests = docs['fullTestsAudit']
    require(full_tests.get('status') == 'passed_frozen_full_regression' and full_tests.get('sourceFingerprint') == fp
            and full_tests.get('sourceInputs') == len(runtime), 'Actual independent full-regression audit required; focused runner is insufficient')
    for mode in ('EditMode', 'PlayMode'):
        expected = seal['fullTestTotals'][mode]
        report = docs['testsEvidence']['reports'][mode]
        require(report['file'] == mode + '.xml' and report['passed'] == expected, 'Test source-evidence totals differ')
        xml = inputs.read(Path(refs['testsEvidence']['path']).parent / report['file'], collect=True)
        require(sha(xml) == report['sha256'], 'Test XML differs from successful source evidence')
        test_xml(xml, expected)
        test_names = [case.get('fullname') for case in ET.fromstring(xml).iter('test-case')]
        require(all(test_names) and len(test_names) == len(set(test_names)), 'Missing/duplicate actual full test names')
        audited = full_tests['results'][mode]
        require(audited.get('count') == expected and audited.get('xmlSha256') == report['sha256'], 'Independent full test audit differs from actual XML')
        baseline_ref = seal['testBaseline'][mode]
        inputs.ref(baseline_ref, document=False); baseline = inputs.read(baseline_ref['path'], collect=True)
        baseline_name = 'Docs/Evidence/071/Tests/' + mode + '.xml'
        require(selected[baseline_name]['sha256'] == sha(baseline), 'Prior complete baseline must match retained immutable 071 Git bytes')
        baseline_root = ET.fromstring(baseline); baseline_total = int(baseline_root.attrib['total']); test_xml(baseline, baseline_total)
        baseline_names = [case.get('fullname') for case in baseline_root.iter('test-case')]
        require(all(baseline_names) and len(baseline_names) == len(set(baseline_names)) and set(baseline_names).issubset(test_names)
                and audited.get('baselinePreserved') == baseline_total, 'A complete baseline test is absent from this regression')
    require(full_tests['totalPassed'] == sum(seal['fullTestTotals'].values()), 'Full test total differs')
    build = docs['buildIdentity']; same_identity(build, seal)
    require(build.get('buildResult') == 'Succeeded' and build.get('sourceStable') is True and build.get('completedAtUtc'), 'Build is not a successful sealed result')
    build_log = inputs.read(Path(refs['buildRunner']['path']).parent / 'build.log', collect=True)
    require(sha(build_log) == build['buildLogSha256'] and b'FRONTIER_BUILD Succeeded' in build_log, 'Actual successful build log differs')
    native = docs['nativeJournal']; native_gate072_final1(native, seal, inputs)
    matrix, audit, declaration = docs['matrixManifest'], docs['matrixAudit'], docs['matrixDeclaration']
    require(declaration.get('status') == 'declared_before_execution' and declaration.get('applicationVersion') == VERSION
            and declaration.get('simulatedSecondsLimit') == 3600 and declaration.get('comparisonHorizonSeconds') == 2400
            and [(c['map'], c['civilization'], c['seed']) for c in declaration['cases']] == MATRIX
            and declaration.get('observerSha256AtDeclaration') == runtime['Assets/Frontier/Editor/NormalRoutes072.cs'], 'Matrix predeclaration changed or incomplete')
    require(matrix.get('appVersion') == VERSION and matrix.get('declaredSourceFingerprint') == fp and matrix.get('finishedUtc')
            and matrix.get('declaredLimitSeconds') == 3600 and matrix.get('comparisonSeconds') == 2400
            and [(c['map'], c['civilization'], c['seed']) for c in matrix['cases']] == MATRIX, 'Closed predeclared 16-case matrix required')
    require(audit.get('result') == 'Consistent' and audit.get('matrixExecuted') is True and matrix_audited_refs_gate072_final2(audit, seal, runtime, repo, matrix, inputs)
            and audit.get('observedRoutesTotal') == 16 and len(audit.get('cases', [])) == 16
            and audit.get('sourceBinding', {}).get('fingerprint') == fp and audit['sourceBinding'].get('inputs') == len(runtime), 'Independent 16-case matrix audit incomplete/inconsistent')
    require(audit.get('observationStatus') == matrix.get('status') in ('observed_routes_incomplete', 'all_observed_routes_reached'), 'Route completion summary was changed')
    require(seal['matrixChecker']['path'] == str(HERE / 'independent-normal072.py')
            and seal['matrixChecker']['sha256'] == MATRIX_CHECKER_SHA, 'Only the unchanged independent 072 matrix auditor is permitted')
    inputs.ref(seal['matrixChecker'], document=False)
    return {'source': source, 'selected': selected, 'excluded': excluded, 'runtime': runtime,
            'treeSha256': tree_hash, 'repo': repo, 'documents': docs}


def zip_info(item):
    info = zipfile.ZipInfo(item['path'], (1980, 1, 1, 0, 0, 0))
    info.create_system = 3; info.external_attr = item['mode'] << 16 | (0x10 if item['type'] == 'directory' else 0)
    info.compress_type = zipfile.ZIP_DEFLATED; info.file_size = item['size']
    return info


def extra_fields(raw):
    fields, position = {}, 0
    while position < len(raw):
        require(position+4 <= len(raw), 'Truncated ZIP extra')
        key, size = struct.unpack_from('<HH', raw, position); position += 4
        require(key == 1 and key not in fields and position+size <= len(raw), 'Unsupported/duplicate ZIP extra')
        fields[key] = raw[position:position+size]; position += size
    return fields


def verify_zip(path, expected):
    namespace(expected); actual = {}
    with Path(path).open('rb') as raw, zipfile.ZipFile(raw, 'r') as archive:
        require(not archive.comment, 'Unexpected archive comment')
        infos = archive.infolist(); require(len(infos) == len(expected), 'ZIP member count differs')
        boundary = 0
        for info in infos:
            name = safe(info.filename); require(name not in actual and name in expected, 'Duplicate/extra ZIP member')
            item = expected[name]; mode = info.external_attr >> 16
            require(info.create_system == 3 and info.external_attr == ((item['mode'] << 16) | (0x10 if item['type'] == 'directory' else 0)), 'ZIP Unix type/mode differs')
            require(info.compress_type == zipfile.ZIP_DEFLATED and info.flag_bits in (0, 0x800)
                    and not info.comment and info.volume == 0, 'Unsupported ZIP encoding/flags/compression/comment/disk')
            extra_fields(info.extra)
            require(info.header_offset == boundary, 'Hidden ZIP prefix, gap or overlapping member')
            raw.seek(info.header_offset); header = raw.read(30)
            require(len(header) == 30, 'Truncated local ZIP header')
            signature, version, flags, method, time, date, crc, compressed, size, name_size, extra_size = struct.unpack('<I5H3I2H', header)
            local_name, local_extra = raw.read(name_size), raw.read(extra_size)
            require(signature == 0x04034b50 and version == info.extract_version and flags == info.flag_bits
                    and method == info.compress_type and time == 0 and date == 33 and crc == info.CRC, 'Local/central ZIP header differs')
            require(local_name.decode('utf-8' if flags & 0x800 else 'cp437') == name, 'Local/central ZIP filename differs')
            fields = extra_fields(local_extra)
            values = iter(struct.unpack('<' + 'Q'*(len(fields.get(1, b''))//8), fields.get(1, b'')))
            if size == 0xffffffff:
                size = next(values, None)
            if compressed == 0xffffffff:
                compressed = next(values, None)
            require(size == info.file_size and compressed == info.compress_size, 'Local/central ZIP sizes differ')
            boundary = info.header_offset + 30 + name_size + extra_size + info.compress_size
            require(boundary <= archive.start_dir, 'Member overlaps central directory')
            digest, count = hashlib.sha256(), 0
            with archive.open(info, 'r') as stream:
                for block in iter(lambda: stream.read(CHUNK), b''):
                    digest.update(block); count += len(block)
            require(count == item['size'] == info.file_size and digest.hexdigest() == item['sha256'], 'ZIP payload bytes/SHA/CRC differ: ' + name)
            require((item['type'] == 'directory') == info.is_dir() and (item['type'] != 'directory' or count == 0), 'ZIP directory type/payload differs')
            actual[name] = dict(item, crc32=format(info.CRC, '08x'), compressedBytes=info.compress_size)
        require(boundary == archive.start_dir and set(actual) == set(expected), 'ZIP final namespace/data boundary differs')
        raw.seek(-22, os.SEEK_END); end = raw.read(22)
        require(len(end) == 22 and end[:4] == b'PK\x05\x06' and end[-2:] == b'\0\0', 'Unexpected ZIP suffix/end comment')
    return actual


def write_zip(path, expected, supplier):
    with zipfile.ZipFile(path, 'x', zipfile.ZIP_DEFLATED, compresslevel=6, allowZip64=True) as archive:
        for name, item in sorted(expected.items()):
            info = zip_info(item)
            if item['type'] == 'directory':
                archive.writestr(info, b'')
            else:
                with archive.open(info, 'w', force_zip64=True) as sink:
                    supplier(name, item, sink)
    actual = verify_zip(path, expected)
    return {'file': path.name, 'bytes': path.stat().st_size, 'sha256': Inputs().read(path)['sha256'],
            'memberCount': len(actual), 'payloadBytes': sum(x['size'] for x in actual.values()),
            'members': list(actual.values()), 'localCrcShaBytesTypeModeHeaderGraphVerified': True}


def pinned_parent(directory, relative, signatures):
    current = Path(directory); fd = os.open(current, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    try:
        require(list(identity(os.fstat(fd))) == signatures[str(current)], 'Root directory differs before archive read')
        for part in relative.parts[:-1]:
            child = os.open(part, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=fd)
            os.close(fd); fd = child; current = current / part
            require(list(identity(os.fstat(fd))) == signatures[str(current)], 'Parent directory differs before archive read')
        return fd
    except BaseException:
        os.close(fd); raise


def filesystem_supplier(directory, prefix, signatures, inputs):
    def supply(name, item, sink):
        relative = PurePosixPath(name).relative_to(prefix); path = Path(directory) / relative
        fd = pinned_parent(directory, relative, signatures)
        try:
            if item['type'] == 'symlink':
                node = os.stat(relative.name, dir_fd=fd, follow_symlinks=False)
                require(list(identity(node)) == signatures[str(path)], 'Symlink differs before archive read')
                raw = os.fsencode(os.readlink(relative.name, dir_fd=fd))
                require(list(identity(os.stat(relative.name, dir_fd=fd, follow_symlinks=False))) == signatures[str(path)], 'Symlink changed during archive read')
                sink.write(raw)
            else:
                inputs.read(path, sink=sink, dir_fd=fd, entry_name=relative.name)
        finally:
            os.close(fd)
    return supply


def frozen_trees(seal, inputs):
    require(set(seal['trees']) == {'app', 'evidence', 'gallery', 'normal'}, 'Exactly four frozen filesystem inputs required')
    trees = {}
    for key, spec in seal['trees'].items():
        approval = inputs.ref(spec['approval'])
        require(approval.get('schema') == 'lstat-package072-approval-1' and approval.get('directory') == str(real_path(spec['directory'], True))
                and approval.get('archiveRoot') == spec['archiveRoot'] and approval.get('status') == 'frozen_inventory', 'Wrong independent tree approval')
        records, signatures = inventory(spec['directory'], spec['archiveRoot'], inputs)
        require(approval.get('members') == list(records.values()), 'Complete lstat tree differs from independently pinned approval: ' + key)
        trees[key] = (records, signatures)
    normal = Path(seal['trees']['normal']['directory']).absolute()
    matrix_manifest = Path(seal['inputs']['matrixManifest']['path']).absolute()
    require(matrix_manifest == normal / 'matrix/manifest.json', 'Normal archive must contain the actual matrix runner root')
    return trees


def binary_gate(seal, docs, inputs):
    app = Path(seal['trees']['app']['directory']).absolute(); build = docs['buildIdentity']
    for key, suffix in (('executable', 'Contents/MacOS/Frontier Reckoning'), ('compiledRuntime', 'Contents/Resources/Data/Managed/Frontier.Runtime.dll')):
        wanted = build[key]; actual = inputs.read(app / suffix)
        require(actual['bytes'] == wanted['bytes'] and actual['sha256'] == wanted['sha256'], 'Actual app binary differs from build identity: ' + key)
    plist = plistlib.loads(inputs.read(app / 'Contents/Info.plist', collect=True))
    require(plist.get('CFBundleShortVersionString') == VERSION and plist.get('CFBundleExecutable') == 'Frontier Reckoning', 'Actual app plist release differs')
    boot = inputs.read(app / 'Contents/Resources/Data/boot.config', collect=True).decode('utf-8')
    require(re.findall(r'^build-guid=([0-9a-f]{32})\r?$', boot, re.M) == [seal['buildGuid']], 'Actual app boot.config build GUID differs')
    return True


def verify_audit_coverage(seal, prepared, trees, inputs):
    root = Path(seal['trees']['normal']['directory']).absolute()
    # The unchanged independent auditor does not export individual input hashes.
    # Rerun only that pinned standard-library reader against the frozen originals,
    # comparing its semantic report; never substitute an unchecked new binding.
    print('Rechecking closed 16-case originals with the pinned independent Python auditor', file=sys.stderr, flush=True)
    process = subprocess.run([sys.executable, '-B', seal['matrixChecker']['path'], '--matrix', str(root / 'matrix'),
                              '--runner', str(root), '--project', str(prepared['repo']),
                              '--declaration', seal['inputs']['matrixDeclaration']['path']],
                             stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    require(process.returncode == 0, 'Closed matrix independent Python reread failed: ' + process.stderr.decode(errors='replace')[-1600:])
    actual = decode(process.stdout); previous = prepared['documents']['matrixAudit']
    for key in ('result', 'matrixExecuted', 'sourceBinding', 'cases', 'observedRoutesReached', 'observedRoutesTotal',
                'observationStatus', 'balanceVerdict', 'filesUnchangedDuringAudit'):
        require(actual.get(key) == previous.get(key), 'Matrix audit no longer matches frozen raw originals: ' + key)
    require(actual['result'] == 'Consistent' and matrix_audited_refs_gate072_final2(actual, seal, prepared['runtime'], prepared['repo'], prepared['documents']['matrixManifest'], inputs), 'Independent matrix reread is not consistent/stable')
    inputs.read(seal['matrixChecker']['path'])
    # Native source originals must be present in the separately frozen evidence tree.
    evidence_root = Path(seal['trees']['evidence']['directory']).absolute()
    originals = real_path(seal['nativeOriginalDirectory'], True)
    prefix = safe(seal['nativeEvidencePrefix']).rstrip('/')
    for item in prepared['documents']['nativeJournal']['originalFiles']:
        original = Path(item['path']).absolute()
        require(original.is_relative_to(originals), 'Native original path lies outside the declared closed native directory')
        member = seal['trees']['evidence']['archiveRoot'] + '/' + prefix + '/' + original.relative_to(originals).as_posix()
        copied = trees['evidence'][0].get(member, {})
        require(copied.get('type') == 'file' and copied.get('size') == item['bytes'] and copied.get('sha256') == item['sha256'],
                'Evidence archive omits/misnames/differs from the native original: ' + member)
    require(evidence_root != root, 'Evidence/normal inputs must be distinct trees')


def exclusive(path, raw):
    path = Path(path).absolute()
    require(path.parent.resolve(strict=True) == path.parent, 'Output ancestor must be real')
    with os.fdopen(os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o644), 'wb') as stream:
        stream.write(raw); stream.flush(); os.fsync(stream.fileno())


def template():
    names = ('sourceFreeze testsEvidence fullTestsAudit buildIdentity buildRunner buildSourceBefore '
             'buildSourceAfter nativeJournal matrixManifest matrixAudit matrixRunner matrixSourceBefore matrixSourceAfter matrixDeclaration').split()
    return {'schema': 'frontier-local-package-seal-072-1', 'status': 'pending_actual_acceptance', 'sourceSealed': False,
            'scope': 'local_only_no_upload', 'profile': PROFILE, 'appVersion': VERSION,
            'repo': '/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project',
            'sourceSnapshotCommit': None, 'runtimeCommit': None, 'sourceFingerprint': None, 'sourceInputs': None,
            'buildGuid': None, 'fullTestTotals': {'EditMode': None, 'PlayMode': None},
            'nativeOriginalDirectory': None, 'nativeEvidencePrefix': 'Native/Originals',
            'testBaseline': {mode: {'path': None, 'bytes': None, 'sha256': None} for mode in ('EditMode', 'PlayMode')},
            'inputs': {name: {'path': None, 'bytes': None, 'sha256': None} for name in names},
            'matrixChecker': {'path': str(HERE / 'independent-normal072.py'), 'bytes': None, 'sha256': None},
            'trees': {key: {'directory': None, 'archiveRoot': prefix, 'approval': {'path': None, 'bytes': None, 'sha256': None}}
                      for key, prefix in (('app', 'Frontier Reckoning.app'), ('evidence', 'Evidence072'), ('gallery', 'Gallery072'), ('normal', 'NormalRoutes072'))}}


def self_test():
    checks = []

    def bad(label, call):
        try:
            call()
        except (ValueError, OSError, zipfile.BadZipFile, ET.ParseError):
            checks.append(label); return
        raise AssertionError('Expected rejection: ' + label)

    with tempfile.TemporaryDirectory(prefix='package072-fixture-', dir=HERE) as tmp:
        root = Path(tmp); tree = root / 'App.app'; (tree / 'Contents/Empty').mkdir(parents=True)
        payload = tree / 'Contents/run'; payload.write_bytes(b'fixture executable\0data'); payload.chmod(0o755)
        (tree / 'Contents/alias').symlink_to('run')
        inputs = Inputs(); records, signatures = inventory(tree, tree.name, inputs)
        supply = filesystem_supplier(tree, tree.name, signatures, inputs)
        packed = write_zip(root / 'fixture.zip', records, supply)
        require(packed['memberCount'] == 5 and packed['localCrcShaBytesTypeModeHeaderGraphVerified'], 'Fixture ZIP failed')
        require(inventory(tree, tree.name, inputs) == (records, signatures) and inputs.unchanged(), 'Fixture input stability failed'); checks.append('regular/executable/empty-directory/internal-link ZIP CRC SHA type mode exact headers stable inputs')
        bad('exclusive ZIP output', lambda: write_zip(root / 'fixture.zip', records, supply))
        bad('pending final seal', lambda: check_gates(template(), Inputs()))
        for name in ('../escape', '/absolute', 'root/../escape', 'root/back\\slash', 'root/.env', 'root/id_rsa', 'root/key.pem'):
            bad('unsafe/secret filename ' + name, lambda name=name: safe(name))
        alias = dict(records); alias['App.app/Contents/RUN'] = record('App.app/Contents/RUN', 'file', 0o100755)
        bad('case alias', lambda: namespace(alias))
        escaping = dict(records); escaping['App.app/Contents/alias'] = record('App.app/Contents/alias', 'symlink', 0o120777, 1, EMPTY, '../../../outside')
        bad('symlink escape', lambda: namespace(escaping))
        cycling = dict(records); cycling['App.app/Contents/alias'] = record('App.app/Contents/alias', 'symlink', 0o120777, 1, EMPTY, 'alias')
        bad('symlink cycle', lambda: namespace(cycling))
        raw = bytearray((root / 'fixture.zip').read_bytes()); raw[30] ^= 1; (root / 'bad-local.zip').write_bytes(raw)
        bad('local central filename mismatch', lambda: verify_zip(root / 'bad-local.zip', records))
        bad('Unicode path extra forbidden', lambda: extra_fields(struct.pack('<HH', 0x7075, 0)))
        bad('ZIP extra duplicate', lambda: extra_fields(struct.pack('<HHHH', 1, 0, 1, 0)))
        xml = b'<test-run result="Passed" total="2" passed="2" failed="0" skipped="0" inconclusive="0" end-time="fixture"><test-suite result="Passed"><test-case result="Passed"/><test-case result="Passed"/></test-suite></test-run>'
        test_xml(xml, 2); checks.append('complete passing XML actual cases')
        bad('fake XML summary', lambda: test_xml(xml.replace(b'<test-case result="Passed"/>', b'', 1), 2))
        bad('skipped XML', lambda: test_xml(xml.replace(b'skipped="0"', b'skipped="1"'), 2))
        bad('duplicate JSON property', lambda: decode(b'{"status":1,"status":2}'))
        bad('nonfinite JSON', lambda: decode(b'{"value":NaN}'))
        require(excluded_batch('Docs/Evidence/069/a') == '069' and all(excluded_batch('Docs/Evidence/'+x+'/a') is None for x in ('070', '071', '072', 'Benchmarks', 'Captures')), '071 profile continuation failed'); checks.append('only old numeric Evidence batches omitted')
        require(runtime_path('Assets/Frontier/a.cs') and not runtime_path('Assets/.hidden/a') and not runtime_path('Docs/a'), 'Runtime scope failed'); checks.append('runtime input scope')
        # Construct tiny Git objects directly in this temporary fixture only.
        # No init/commit/index/ref operation touches the user's repository.
        repo = root / 'synthetic-repo'; objects = repo / '.git/objects'; objects.mkdir(parents=True)
        (repo / '.git/refs').mkdir(); (repo / '.git/HEAD').write_text('ref: refs/heads/fixture\n')
        (repo / '.git/config').write_text('[core]\nrepositoryformatversion = 0\nbare = false\n')
        def object_bytes(kind, content):
            raw = kind.encode() + b' ' + str(len(content)).encode() + b'\0' + content
            oid = hashlib.sha1(raw).hexdigest(); directory = objects / oid[:2]; directory.mkdir(exist_ok=True)
            (directory / oid[2:]).write_bytes(zlib.compress(raw)); return oid
        files = {'.gitattributes': (0o100644, b'/Assets export-ignore\n'),
                 'Assets/a.cs': (0o100644, b'fixture source\n'), 'SourceAssets/link': (0o120000, b'../Assets/a.cs'),
                 'Tools/run.sh': (0o100755, b'fixture executable\n'),
                 'Docs/Evidence/069/old.json': (0o100644, b'old excluded\n'),
                 'Docs/Evidence/070/raw.json': (0o100644, b'070 preserved\n'),
                 'Docs/Evidence/071/raw.json': (0o100644, b'071 preserved\n'),
                 'Docs/Evidence/072/raw.json': (0o100644, b'072 preserved\n')}
        nested = {}
        for name, item in files.items():
            node = nested; components = name.split('/')
            for component in components[:-1]:
                node = node.setdefault(component, {})
            node[components[-1]] = item
        def tree_object(node):
            content = bytearray()
            for name, value in sorted(node.items(), key=lambda pair: pair[0] + ('/' if isinstance(pair[1], dict) else '')):
                if isinstance(value, dict):
                    mode, oid = '40000', tree_object(value)
                else:
                    mode, oid = format(value[0], 'o'), object_bytes('blob', value[1])
                content.extend(mode.encode() + b' ' + name.encode() + b'\0' + bytes.fromhex(oid))
            return object_bytes('tree', bytes(content))
        tree_oid = tree_object(nested)
        commit = object_bytes('commit', ('tree ' + tree_oid + '\nauthor Fixture <fixture@invalid> 0 +0000\ncommitter Fixture <fixture@invalid> 0 +0000\n\nsynthetic fixture\n').encode())
        tracked, selected, omitted, runtime, _ = git_records(repo, commit)
        require(set(omitted) == {'Docs/Evidence/069/old.json'} and set(selected) == set(files)-set(omitted), 'Synthetic Git selection failed')
        require(selected['Assets/a.cs']['sha256'] == sha(files['Assets/a.cs'][1]) and selected['Tools/run.sh']['gitTreeMode'] == '100755', 'Git blob/mode failed')
        require(runtime == {'Assets/a.cs': sha(files['Assets/a.cs'][1]), 'Tools/run.sh': sha(files['Tools/run.sh'][1])}, 'Synthetic runtime map failed')
        def git_supply(name, item, sink):
            sink.write(git(repo, 'cat-file', 'blob', item['gitObjectId']))
        source_packed = write_zip(root / 'source-fixture.zip', tracked, git_supply)
        require(source_packed['memberCount'] == len(tracked), 'Synthetic Git ZIP failed')
        checks.append('immutable Git tree partition blobs executable modes symlink and export-ignore-resistant source ZIP')
        bad('symbolic commit refused', lambda: exact_commit(repo, 'HEAD'))
        bad('runtime symlink refused', lambda: git_records(repo, object_bytes('commit', ('tree ' + tree_object({'Assets': {'runtime-link': (0o120000, b'other')}}) + '\nauthor Fixture <fixture@invalid> 0 +0000\ncommitter Fixture <fixture@invalid> 0 +0000\n\nfixture\n').encode()), False))
        payload.write_bytes(b'changed'); bad('original input mutation', inputs.unchanged)
    return {'status': 'fixture_self_tests_passed', 'checks': checks, 'count': len(checks), 'actualReleasePackaged': False,
            'scope': 'Synthetic local fixture boundaries only; no final source/build/tests/native/matrix acceptance.'}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    action = parser.add_mutually_exclusive_group(required=True)
    action.add_argument('--template', action='store_true'); action.add_argument('--self-test', action='store_true')
    action.add_argument('--inventory', type=Path); action.add_argument('--check', action='store_true'); action.add_argument('--package', action='store_true')
    parser.add_argument('--archive-root'); parser.add_argument('--inventory-output', type=Path)
    parser.add_argument('--seal', type=Path); parser.add_argument('--seal-sha256'); parser.add_argument('--output-directory', type=Path)
    args = parser.parse_args()
    try:
        if args.template:
            print(encoded(template()).decode(), end=''); return 0
        if args.self_test:
            print(encoded(self_test()).decode(), end=''); return 0
        if args.inventory:
            require(args.archive_root and args.inventory_output and args.inventory_output.absolute().parent == HERE, 'Inventory approval must be a new Diagnostics file with explicit archive root')
            inputs = Inputs(); records, signatures = inventory(args.inventory, args.archive_root, inputs)
            require(inventory(args.inventory, args.archive_root, inputs) == (records, signatures) and inputs.unchanged(), 'Inventory not stable')
            receipt = {'schema': 'lstat-package072-approval-1', 'status': 'frozen_inventory', 'createdUtc': utc(),
                       'directory': str(args.inventory.absolute()), 'archiveRoot': args.archive_root, 'members': list(records.values()),
                       'scope': 'Complete lstat bytes/types/modes/link text only; must be reviewed and separately pinned before packaging.'}
            raw = encoded(receipt); exclusive(args.inventory_output, raw)
            print(json.dumps({'inventory': str(args.inventory_output), 'bytes': len(raw), 'sha256': sha(raw)})); return 0
        require(args.seal and re.fullmatch(r'[0-9a-f]{64}', args.seal_sha256 or ''), 'Explicit final seal and its reviewed SHA-256 required')
        inputs = Inputs(); raw = inputs.read(args.seal, collect=True)
        require(sha(raw) == args.seal_sha256, 'Seal bytes differ from reviewed approval')
        seal = decode(raw); prepared = check_gates(seal, inputs); trees = frozen_trees(seal, inputs)
        binary_gate(seal, prepared['documents'], inputs); verify_audit_coverage(seal, prepared, trees, inputs)
        require(inputs.unchanged(), 'Acceptance originals changed')
        if args.check:
            print(json.dumps({'status': 'sealed_local_inputs_consistent', 'sourceFingerprint': seal['sourceFingerprint'], 'packageWritten': False,
                              'matrixObservation': prepared['documents']['matrixAudit']['observationStatus']})); return 0
        require(args.output_directory and not args.output_directory.absolute().exists(), 'Final output must be a new exclusive directory')
        out = args.output_directory.absolute(); require(out.parent.resolve(strict=True) == out.parent, 'Output parent must already be real')
        for folder in [prepared['repo']] + [real_path(spec['directory'], True) for spec in seal['trees'].values()]:
            require(not out.is_relative_to(folder) and not folder.is_relative_to(out), 'Output overlaps an input tree')
        out.mkdir(mode=0o755); started = utc(); artifacts = {}
        def source_supplier(name, item, sink):
            raw = git(prepared['repo'], 'cat-file', 'blob', item['gitObjectId'])
            require(len(raw) == item['size'] and sha(raw) == item['sha256'], 'Git blob changed on write'); sink.write(raw)
        artifacts['source'] = write_zip(out / ARCHIVES['source'], prepared['source'], source_supplier)
        for key, (records, signatures) in trees.items():
            spec = seal['trees'][key]; directory = Path(spec['directory']).absolute()
            supplier = filesystem_supplier(directory, spec['archiveRoot'], signatures, inputs)
            artifacts[key] = write_zip(out / ARCHIVES[key], records, supplier)
        for key, (records, signatures) in trees.items():
            spec = seal['trees'][key]
            require(inventory(spec['directory'], spec['archiveRoot'], inputs) == (records, signatures), 'Original tree changed during packaging: ' + key)
        require(inputs.unchanged(), 'Original input SHA/signature changed after all ZIPs')
        fresh, selected, excluded, runtime, tree_hash = git_records(prepared['repo'], seal['sourceSnapshotCommit'])
        require((fresh, selected, excluded, runtime, tree_hash) == (prepared['source'], prepared['selected'], prepared['excluded'], prepared['runtime'], prepared['treeSha256']), 'Git immutable snapshot changed')
        for item in artifacts.values():
            current = Inputs().read(out / item['file'])
            require(current['bytes'] == item['bytes'] and current['sha256'] == item['sha256'], 'A completed output ZIP changed before its receipt')
        receipt = {'schema': 'frontier-local-package-result-072-1', 'status': 'passed_local_archive_integrity', 'startedUtc': started, 'finishedUtc': utc(),
                   'profile': PROFILE, 'notCompleteGitTreeArchive': True, 'sourceSnapshotCommit': seal['sourceSnapshotCommit'],
                   'runtimeCommit': seal['runtimeCommit'], 'sourceFingerprint': seal['sourceFingerprint'], 'sourceInputs': seal['sourceInputs'],
                   'buildGuid': seal['buildGuid'], 'appVersion': VERSION, 'seal': inputs.files[str(args.seal.absolute())],
                   'artifacts': artifacts, 'selectedTrackedPaths': list(selected.values()), 'excludedTrackedPaths': list(excluded.values()),
                   'allOriginalInputsUnchangedBeforeAfter': True, 'matrixObservationStatus': prepared['documents']['matrixAudit']['observationStatus'],
                   'observedRoutesReached': prepared['documents']['matrixAudit']['observedRoutesReached'],
                   'sourceFullTreeMetadataSha256': tree_hash,
                   'limits': ['Local packaging bytes/CRC/SHA/type/mode/path/graph identity only; no new gameplay or balance verdict.',
                              'Existing accepted native scope is preserved; this tool does not inspect pixels or certify two hosts/network.',
                              'No Unity/build/test rerun, upload, remote-content hash, ACL/xattr/resource fork, signing or notarization proof.',
                              'Secret-bearing filenames are rejected; human approval remains responsible for the selected evidence content.']}
        exclusive(out / 'package-manifest072.json', encoded(receipt))
        print(json.dumps({'status': receipt['status'], 'archives': [{k:v for k,v in item.items() if k != 'members'} for item in artifacts.values()]})); return 0
    except (OSError, ValueError, KeyError, TypeError, struct.error, subprocess.CalledProcessError, zipfile.BadZipFile, ET.ParseError, StopIteration) as error:
        print(json.dumps({'status': 'blocked_no_acceptance_claim', 'errorType': type(error).__name__, 'error': str(error),
                          'partialOutputsIfAnyRetained': True, 'uploadPerformed': False}), file=sys.stderr)
        return 2


if __name__ == '__main__':
    sys.exit(main())
