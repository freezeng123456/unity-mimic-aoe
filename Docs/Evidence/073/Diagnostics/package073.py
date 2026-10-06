#!/usr/bin/env python3
"""Create or independently verify local 073 ZIPs from explicit immutable inputs.

Preparation of this helper is not a packaging or acceptance receipt. No Unity,
network, commit, upload, extraction, or executable launch is performed here.
"""
import argparse
from datetime import datetime, timezone
from functools import lru_cache
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import plistlib
import re
import stat
import subprocess
import sys
import unicodedata
from xml.etree import ElementTree as ET
import zipfile

RUNTIME_COMMIT = '519d7d34a0be85f75ee19e53458c636d1ba0b3e4'
FINGERPRINT = '9b92812a922adb69a5ae501925417d8eba8e24b836f496274ebc8ec8679ba03d'
BUILD_GUID = '5f347ad9ec2f494984b0b49c4f896c31'
VERSION = '0.16.0'
RUNTIME_ROOTS = ('Assets', 'Packages', 'ProjectSettings', 'Tools')
PROFILE = 'all-tracked-except-Docs-Evidence-073-v1'
LIMIT = 100 * 1024 * 1024
CHUNK = 1024 * 1024
EMPTY = hashlib.sha256(b'').hexdigest()
HERE = Path(__file__).resolve().parent
CANDIDATE = HERE.parent
REPO = Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
TESTS = REPO / 'Builds/Tests/run-9q8yTJKS'


def require(ok, message):
    if not ok:
        raise ValueError(message)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def encoded(value):
    return (json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True,
                       allow_nan=False) + '\n').encode('utf-8')


def fingerprint(files):
    return sha(json.dumps(files, sort_keys=True, separators=(',', ':')).encode())


def pairs(items):
    result = {}
    for key, value in items:
        require(key not in result, 'Duplicate JSON key: ' + key)
        result[key] = value
    return result


def decode(raw):
    return json.loads(raw.decode('utf-8-sig'), object_pairs_hook=pairs,
                      parse_constant=lambda _: require(False, 'Nonfinite JSON'))


def stable_identity(node):
    return (node.st_dev, node.st_ino, node.st_mode, node.st_size,
            node.st_mtime_ns, node.st_ctime_ns)


def stable_read(path, sink=None, collect=False):
    """Read a regular file without following a final symlink; detect replacement."""
    path = Path(path)
    before = path.lstat()
    require(stat.S_ISREG(before.st_mode), 'Expected regular input: ' + str(path))
    digest, count = hashlib.sha256(), 0
    raw = bytearray() if collect else None
    with os.fdopen(os.open(path, os.O_RDONLY | os.O_NOFOLLOW), 'rb') as stream:
        require(stable_identity(os.fstat(stream.fileno())) == stable_identity(before),
                'Input changed while opening: ' + str(path))
        for block in iter(lambda: stream.read(CHUNK), b''):
            digest.update(block)
            count += len(block)
            if sink is not None:
                sink.write(block)
            if raw is not None:
                raw.extend(block)
        require(stable_identity(os.fstat(stream.fileno())) == stable_identity(before)
                and stable_identity(path.lstat()) == stable_identity(before)
                and count == before.st_size, 'Input changed while reading: ' + str(path))
    item = {'path': str(path), 'bytes': count, 'sha256': digest.hexdigest(),
            'identity': list(stable_identity(before))}
    return (bytes(raw), item) if collect else item


def real_directory(path):
    path = Path(path).absolute()
    require(stat.S_ISDIR(path.lstat().st_mode), 'Expected real directory: ' + str(path))
    return path.resolve(strict=True)


def real_file(path):
    path = Path(path).absolute()
    require(stat.S_ISREG(path.lstat().st_mode), 'Expected real file: ' + str(path))
    return path.resolve(strict=True)


def safe(name):
    require(isinstance(name, str) and name, 'Empty/non-string member name')
    parts = name.rstrip('/').split('/')
    require(not name.startswith('/') and '\\' not in name
            and not any(ord(c) < 32 or ord(c) == 127 for c in name)
            and all(p not in ('', '.', '..') and ':' not in p
                    and not p.endswith((' ', '.')) for p in parts),
            'Unsafe archive path: ' + repr(name))
    forbidden = {'.git', '.hg', '.svn', '.ssh', '.aws', '.gnupg', '.env',
                 '__pycache__', 'library', 'temp', 'credentials.json',
                 'secrets.json', 'id_rsa', 'id_ed25519'}
    require(not any(p.casefold() in forbidden or p.casefold().startswith('.env.')
                    or p.casefold().endswith(('.p12', '.pfx', '.key', '.pem'))
                    for p in parts), 'Forbidden cache/Git/credential path: ' + name)
    return name


def record(name, kind, mode, size=0, digest=EMPTY, target=None):
    item = {'path': safe(name), 'type': kind, 'mode': mode,
            'bytes': size, 'sha256': digest}
    if target is not None:
        item['symlinkTarget'] = target
    return item


def validate_namespace(records):
    nodes, aliases = {}, {}
    for name, item in records.items():
        safe(name)
        require(item['path'] == name, 'Member/inventory path mismatch')
        key = unicodedata.normalize('NFC', name.rstrip('/')).casefold()
        require(key not in aliases, 'Case/Unicode/type alias: ' + name)
        aliases[key] = name
        nodes[name.rstrip('/')] = item
    require(len({n.split('/')[0] for n in nodes}) == 1, 'One archive root required')
    root = next(iter(nodes)).split('/')[0]
    for name in nodes:
        for parent in PurePosixPath(name).parents:
            if str(parent) != '.':
                require(str(parent) in nodes and nodes[str(parent)]['type'] == 'directory',
                        'Missing or non-directory ancestor: ' + name)

    def resolve(parts, visiting):
        resolved = []
        for index, part in enumerate(parts):
            if part in ('', '.'):
                continue
            if part == '..':
                require(len(resolved) > 1, 'Symlink escapes archive root')
                resolved.pop()
                continue
            resolved.append(part)
            current = '/'.join(resolved)
            require(current in nodes, 'Dangling symlink: ' + current)
            node = nodes[current]
            if node['type'] == 'symlink':
                require(current not in visiting and len(visiting) < 40, 'Symlink cycle')
                target = node['symlinkTarget']
                require(target and not target.startswith('/') and '\\' not in target
                        and ':' not in target and not any(ord(c) < 32 for c in target),
                        'Unsafe symlink text: ' + current)
                return resolve(resolved[:-1] + target.split('/') + parts[index + 1:],
                               visiting | {current})
            if index < len(parts) - 1:
                require(node['type'] == 'directory'
                        or all(p in ('', '.') for p in parts[index + 1:]),
                        'Symlink traverses a regular file')
        require(resolved and resolved[0] == root, 'Symlink archive root mismatch')
        return '/'.join(resolved)

    for name, item in nodes.items():
        if item['type'] == 'symlink':
            resolve(name.split('/'), set())


def inventory(directory, prefix):
    """Hash the exact complete lstat tree, including empty dirs and link text."""
    directory = real_directory(directory)
    require('/' not in safe(prefix), 'Archive root must be one component')
    records, identities = {}, {}

    def walk(folder, member):
        before = folder.lstat()
        require(stat.S_ISDIR(before.st_mode), 'Input directory replaced: ' + str(folder))
        identities[str(folder)] = list(stable_identity(before))
        records[member + '/'] = record(member + '/', 'directory', before.st_mode & 0xffff)
        with os.scandir(folder) as scan:
            names = sorted(entry.name for entry in scan)
        for leaf in names:
            path, name = folder / leaf, safe(member + '/' + leaf)
            node = path.lstat()
            mode = node.st_mode & 0xffff
            if stat.S_ISDIR(node.st_mode):
                walk(path, name)
            elif stat.S_ISLNK(node.st_mode):
                target = os.readlink(path)
                raw = os.fsencode(target)
                require(stable_identity(path.lstat()) == stable_identity(node),
                        'Symlink changed: ' + str(path))
                identities[str(path)] = list(stable_identity(node))
                records[name] = record(name, 'symlink', mode, len(raw), sha(raw), target)
            elif stat.S_ISREG(node.st_mode):
                item = stable_read(path)
                require(item['identity'] == list(stable_identity(node)),
                        'File changed after inventory stat: ' + str(path))
                identities[str(path)] = item['identity']
                records[name] = record(name, 'file', mode, item['bytes'], item['sha256'])
            else:
                raise ValueError('Unsupported filesystem node: ' + str(path))
        require(stable_identity(folder.lstat()) == stable_identity(before),
                'Directory changed during inventory: ' + str(folder))

    walk(directory, prefix)
    validate_namespace(records)
    return {'directory': str(directory), 'archiveRoot': prefix,
            'members': records, 'identities': identities,
            'fileBytes': sum(v['bytes'] for v in records.values()),
            'memberCount': len(records)}


def git(repo, *args):
    env = dict(os.environ)
    env['GIT_OPTIONAL_LOCKS'] = '0'
    return subprocess.run(['git', '--no-replace-objects', '-C', str(repo), *args],
                          env=env, check=True, stdout=subprocess.PIPE,
                          stderr=subprocess.PIPE).stdout


def exact_commit(repo, value):
    require(re.fullmatch(r'[0-9a-f]{40}|[0-9a-f]{64}', value or ''),
            'Full lowercase source snapshot commit hash required')
    require(git(repo, 'rev-parse', '--verify', value + '^{commit}').decode().strip() == value,
            'Commit does not resolve to its exact identity')


def runtime_path(path):
    parts = path.split('/')
    return parts[0] in RUNTIME_ROOTS and not any(p.startswith('.') or p == '__pycache__'
                                               for p in parts)


def blob(repo, oid):
    raw = git(repo, 'cat-file', 'blob', oid)
    algorithm = git_algorithm(repo)
    require(hashlib.new(algorithm, b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == oid,
            'Git blob content/address mismatch: ' + oid)
    return raw


@lru_cache(maxsize=4)
def git_algorithm(repo):
    return git(repo, 'rev-parse', '--show-object-format').decode().strip()


def source_inventory(repo, commit):
    """Read the committed blobs, never working-tree copies or archive attributes."""
    exact_commit(repo, commit)
    tree = git(repo, 'ls-tree', '-r', '-l', '-z', '--full-tree', commit)
    selected, excluded, runtime = {}, {}, {}
    records = {'source/': record('source/', 'directory', 0o040755)}
    for row in tree.split(b'\0'):
        if not row:
            continue
        header, name_raw = row.split(b'\t', 1)
        mode_raw, kind, oid_raw, size_raw = header.split()
        name, oid, mode = safe(name_raw.decode('utf-8')), oid_raw.decode(), int(mode_raw, 8)
        require(kind == b'blob' and mode in (0o100644, 0o100755, 0o120000),
                'Unsupported Git object/mode: ' + name)
        require(name.split('/')[0] not in ('Builds', 'Logs', 'obj', 'UserSettings'),
                'Tracked generated input forbidden: ' + name)
        require(name not in selected and name not in excluded, 'Duplicate tracked path')
        item = {'path': name, 'gitTreeMode': mode_raw.decode(),
                'gitObjectId': oid, 'bytes': int(size_raw)}
        if name.startswith('Docs/Evidence/'):
            excluded[name] = dict(item, excludedByDirectory='Docs/Evidence/')
            continue
        raw = blob(repo, oid)
        require(len(raw) == item['bytes'], 'Git tree/blob bytes differ: ' + name)
        item['sha256'] = sha(raw)
        selected[name] = item
        if runtime_path(name):
            require(mode != 0o120000, 'Runtime input symlink requires a new review')
            runtime[name] = item['sha256']
        member = 'source/' + name
        for parent in PurePosixPath(member).parents:
            if str(parent) != '.':
                key = str(parent) + '/'
                records.setdefault(key, record(key, 'directory', 0o040755))
        records[member] = record(member, 'symlink' if mode == 0o120000 else 'file',
                                 0o120777 if mode == 0o120000 else mode,
                                 len(raw), item['sha256'], os.fsdecode(raw) if mode == 0o120000 else None)
    validate_namespace(records)
    require(len(selected) + len(excluded) == len([r for r in tree.split(b'\0') if r]),
            'Source selection/exclusion partition is incomplete')
    return {'profile': PROFILE, 'notCompleteGitTreeArchive': True,
            'sourceSnapshotCommit': commit, 'fullTreeMetadataSha256': sha(tree),
            'selectedTrackedPaths': selected, 'excludedTrackedPaths': excluded,
            'runtimeFiles': runtime, 'sourceFingerprint': fingerprint(runtime),
            'sourceInputs': len(runtime), 'members': records,
            'selectedBytes': sum(v['bytes'] for v in selected.values()),
            'excludedBytes': sum(v['bytes'] for v in excluded.values())}


def pinned_json(path, pins):
    path = real_file(path)
    raw, item = stable_read(path, collect=True)
    pins[str(path)] = item
    return decode(raw)


def exact_source_map(doc, files):
    require(doc.get('files') == files and doc.get('sourceFingerprint') == FINGERPRINT
            and fingerprint(doc['files']) == FINGERPRINT, 'Exact source map/fingerprint differs')


def validate_tests(tests, files, pins):
    tests = real_directory(tests)
    doc = pinned_json(tests / 'source-evidence.json', pins)
    exact_source_map(doc, files)
    results = {}
    for mode, count in (('EditMode', 1192), ('PlayMode', 419)):
        path = tests / (mode + '.xml')
        raw, item = stable_read(path, collect=True)
        pins[str(path)] = item
        report = doc['reports'][mode]
        require(report['file'] == path.name and report['sha256'] == item['sha256']
                and report['passed'] == count, 'Test source-evidence/report differs: ' + mode)
        require(b'<!DOCTYPE' not in raw and b'<!ENTITY' not in raw, 'Unexpected XML declarations')
        root = ET.fromstring(raw)
        a, cases = root.attrib, list(root.iter('test-case'))
        require(root.tag == 'test-run' and a.get('result') == 'Passed' and a.get('end-time')
                and int(a.get('total', '0')) == int(a.get('passed', '0')) == count
                and all(int(a.get(k, '0')) == 0 for k in ('failed', 'skipped', 'inconclusive'))
                and len(cases) == count and all(c.get('result') == 'Passed' for c in cases)
                and all(s.get('result') == 'Passed' for s in root.iter('test-suite')),
                'Full regression XML is incomplete or does not pass: ' + mode)
        names = [c.get('fullname') for c in cases]
        require(all(names) and len(set(names)) == count, 'Missing/duplicate test fullname: ' + mode)
        results[mode] = {'passed': count, 'xmlSha256': item['sha256']}
        pins[str(tests / (mode + '.log'))] = stable_read(tests / (mode + '.log'))
    return {'directory': str(tests), 'totalPassed': 1611, 'reports': results}


def closed_receipt(path, pins, source_bound=True):
    """Preserve an actual status without translating it to gameplay acceptance."""
    doc = pinned_json(path, pins)
    require(doc.get('finishedUtc') or doc.get('finishedAtUtc'),
            'An actual finish timestamp is required: ' + str(path))
    status = doc.get('status')
    require(isinstance(status, str) and status.strip()
            and not re.search(r'running|pending|prepared|planned|in_progress', status, re.I),
            'Receipt is not closed: ' + str(path))
    if source_bound:
        values = [doc[k] for k in ('sourceFingerprint', 'sourceFingerprintBefore',
                                  'sourceFingerprintAfter') if k in doc]
        require(values and all(v == FINGERPRINT for v in values),
                'Closed receipt lacks actual matching source binding: ' + str(path))
        if 'runtimeCommit' in doc:
            require(doc['runtimeCommit'] == RUNTIME_COMMIT, 'Receipt runtime commit differs')
    if 'sourceStable' in doc:
        require(doc['sourceStable'] is True, 'Receipt reports unstable source')
    if 'pid' in doc:
        require('exitCode' in doc, 'Process receipt lacks an actual exit code')
    return {'path': str(real_file(path)), 'statusAsRecorded': status,
            'finishedAsRecorded': doc.get('finishedUtc') or doc.get('finishedAtUtc'),
            'document': doc}


def validate_build(app, receipt_path, pins):
    app = real_directory(app)
    doc = pinned_json(receipt_path, pins)
    require(doc.get('status') == 'built_same_frozen_source' and doc.get('exitCode') == 0
            and doc.get('finishedUtc') and doc.get('sourceStable') is True
            and doc.get('runtimeCommit') == RUNTIME_COMMIT
            and doc.get('sourceFingerprintBefore') == doc.get('sourceFingerprintAfter') == FINGERPRINT
            and real_directory(doc['app']) == app, 'Actual build receipt identity/closure differs')
    plist_raw, plist_pin = stable_read(app / 'Contents/Info.plist', collect=True)
    pins[plist_pin['path']] = plist_pin
    info = plistlib.loads(plist_raw)
    require(info.get('CFBundleShortVersionString') == VERSION, 'Actual app version differs')
    executable_name = info.get('CFBundleExecutable')
    require(isinstance(executable_name, str) and '/' not in safe(executable_name),
            'Invalid Info.plist executable')
    executable = app / 'Contents/MacOS' / executable_name
    node = executable.lstat()
    require(stat.S_ISREG(node.st_mode) and node.st_mode & 0o111,
            'Actual app executable lacks Unix execution permission')
    exe_pin = stable_read(executable)
    pins[str(executable)] = exe_pin
    boot_raw, boot_pin = stable_read(app / 'Contents/Resources/Data/boot.config', collect=True)
    pins[boot_pin['path']] = boot_pin
    boot = boot_raw.decode('utf-8')
    require(re.findall(r'^build-guid=([0-9a-f]{32})\r?$', boot, re.M) == [BUILD_GUID]
            and doc.get('bootConfig') == boot, 'Actual app/receipt build GUID differs')
    return {'app': str(app), 'appVersion': VERSION, 'buildGuid': BUILD_GUID,
            'executable': exe_pin, 'receipt': str(real_file(receipt_path))}


def zip_info(item):
    info = zipfile.ZipInfo(item['path'], (1980, 1, 1, 0, 0, 0))
    info.create_system = 3
    info.external_attr = item['mode'] << 16
    if item['type'] == 'directory':
        info.external_attr |= 0x10
    info.compress_type = zipfile.ZIP_DEFLATED
    return info


def write_zip(path, records, payload):
    validate_namespace(records)
    with zipfile.ZipFile(path, 'x', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for name in sorted(records):
            item = records[name]
            with archive.open(zip_info(item), 'w') as out:
                actual = payload(name, item, out)
            require(actual == (item['bytes'], item['sha256']), 'Input changed while ZIP writing: ' + name)
    require(path.stat().st_size < LIMIT, 'ZIP is not strictly smaller than 100 MiB: ' + str(path))


def filesystem_payload(tree):
    def read(name, item, sink):
        rel = PurePosixPath(name).parts[1:]
        path = Path(tree['directory']).joinpath(*rel)
        require(list(stable_identity(path.lstat())) == tree['identities'][str(path)],
                'Tree node changed before ZIP write: ' + str(path))
        if item['type'] == 'directory':
            return 0, EMPTY
        if item['type'] == 'symlink':
            raw = os.fsencode(os.readlink(path))
            sink.write(raw)
            require(list(stable_identity(path.lstat())) == tree['identities'][str(path)],
                    'Symlink changed during ZIP write: ' + str(path))
            return len(raw), sha(raw)
        actual = stable_read(path, sink=sink)
        return actual['bytes'], actual['sha256']
    return read


def source_payload(repo, source):
    def read(name, item, sink):
        if item['type'] == 'directory':
            return 0, EMPTY
        selected = source['selectedTrackedPaths'][name[len('source/'):]]
        raw = blob(repo, selected['gitObjectId'])
        sink.write(raw)
        return len(raw), sha(raw)
    return read


def verify_zip(path, expected):
    """Independent readback: exact members, CRC, bytes/SHA, Unix type/mode/link."""
    path = real_file(path)
    require(path.stat().st_size < LIMIT, 'ZIP exceeds delivery size cap')
    validate_namespace(expected)
    with zipfile.ZipFile(path, 'r') as archive:
        infos = archive.infolist()
        names = [i.filename for i in infos]
        require(len(names) == len(set(names)) and set(names) == set(expected),
                'ZIP member set differs: ' + str(path))
        require(archive.testzip() is None, 'ZIP CRC mismatch: ' + str(path))
        for info in infos:
            item = expected[info.filename]
            require(info.orig_filename == info.filename
                    and info.create_system == 3 and (info.external_attr >> 16) == item['mode']
                    and info.is_dir() == (item['type'] == 'directory')
                    and not info.flag_bits & 1 and info.file_size == item['bytes'],
                    'ZIP mode/type/flags/bytes differ: ' + info.filename)
            digest, count, link = hashlib.sha256(), 0, bytearray()
            with archive.open(info) as stream:
                for block in iter(lambda: stream.read(CHUNK), b''):
                    digest.update(block)
                    count += len(block)
                    if item['type'] == 'symlink':
                        link.extend(block)
            require(count == item['bytes'] and digest.hexdigest() == item['sha256'],
                    'ZIP payload differs: ' + info.filename)
            if item['type'] == 'symlink':
                require(os.fsdecode(bytes(link)) == item['symlinkTarget'], 'ZIP link target differs')
    receipt = stable_read(path)
    receipt.pop('identity')
    receipt.update(memberCount=len(expected), verifiedCRC=True, verifiedExactMembers=True,
                   verifiedBytesAndSha256=True, verifiedUnixTypeAndModes=True,
                   verifiedSymlinkTargets=True)
    return receipt


def pin_tree_duplicates(tree, pins):
    """Evidence must contain the exact identity/test/build originals supplied."""
    hashes = {(v['bytes'], v['sha256']) for v in tree['members'].values() if v['type'] == 'file'}
    for item in pins.values():
        require((item['bytes'], item['sha256']) in hashes,
                'Selected evidence tree omits an exact supplied original: ' + item['path'])


def unchanged(pins, trees):
    for path, expected in pins.items():
        require(stable_read(path) == expected, 'Pinned original changed: ' + path)
    for key, expected in trees.items():
        require(inventory(expected['directory'], expected['archiveRoot']) == expected,
                'Full input tree changed: ' + key)


def package(args):
    repo = real_directory(args.repo)
    source = source_inventory(repo, args.source_snapshot_commit)
    runtime = source_inventory(repo, RUNTIME_COMMIT)
    require(source['runtimeFiles'] == runtime['runtimeFiles'] and source['sourceInputs'] == 732
            and source['sourceFingerprint'] == FINGERPRINT, 'Snapshot differs from frozen 732-input runtime')
    pins = {}
    exact_source_map(pinned_json(args.source_freeze, pins), source['runtimeFiles'])
    tests = validate_tests(args.tests_directory, source['runtimeFiles'], pins)
    build = validate_build(args.app_directory, args.build_receipt, pins)
    normal = closed_receipt(args.normal_receipt, pins)
    require(args.native_gallery_directory is None or args.native_receipt is not None,
            'Optional native gallery requires its actual closed source-bound receipt')
    native = closed_receipt(args.native_receipt, pins) if args.native_receipt else None
    # Only evidence references are required in evidence.zip; app bytes belong in macOS.zip.
    evidence_pins = {p: v for p, v in pins.items() if not Path(p).is_relative_to(Path(build['app']))}
    trees = {
        'macOS': inventory(args.app_directory, Path(build['app']).name),
        'evidence': inventory(args.evidence_directory, 'Evidence073'),
        'normal': inventory(args.normal_directory, 'Normal073'),
    }
    require(str(real_file(args.normal_receipt)).startswith(trees['normal']['directory'] + '/'),
            'Normal receipt must be preserved inside the complete normal input tree')
    require((Path(trees['normal']['directory']) / 'matrix/manifest.json').is_file(),
            'Normal tree must preserve actual matrix/manifest.json')
    matrix = pinned_json(Path(trees['normal']['directory']) / 'matrix/manifest.json', pins)
    require(matrix.get('finishedUtc') and matrix.get('sourceFingerprint') == FINGERPRINT
            and isinstance(matrix.get('status'), str)
            and not re.search(r'running|pending|prepared|planned|in_progress', matrix['status'], re.I),
            'Actual normal matrix manifest is not closed/source-bound')
    normal['matrixStatusAsRecorded'] = matrix['status']
    normal['matrixCaseCountAsRecorded'] = len(matrix.get('cases', []))
    if args.native_gallery_directory:
        trees['nativegallery'] = inventory(args.native_gallery_directory, 'NativeGallery073')
    pin_tree_duplicates(trees['evidence'], evidence_pins)
    output = Path(args.output_directory).absolute()
    require(not output.exists() and not output.is_symlink(), 'Output directory must be new')
    parent = real_directory(output.parent)
    output = parent / output.name
    for root in [repo] + [Path(t['directory']) for t in trees.values()]:
        require(not output.is_relative_to(root) and not root.is_relative_to(output),
                'Output must be outside and distinct from all input trees/repo')
    unchanged(pins, trees)
    started = datetime.now(timezone.utc).isoformat()
    output.mkdir(mode=0o755)
    # On error, keep any partial output for diagnosis; never overwrite or clean inputs.
    artifacts = {}
    write_zip(output / 'source.zip', source['members'], source_payload(repo, source))
    artifacts['source'] = verify_zip(output / 'source.zip', source['members'])
    for key, tree in trees.items():
        path = output / (key + '.zip')
        write_zip(path, tree['members'], filesystem_payload(tree))
        artifacts[key] = verify_zip(path, tree['members'])
    unchanged(pins, trees)
    require(source_inventory(repo, args.source_snapshot_commit) == source,
            'Source snapshot object inventory changed')
    # Readback was checked against fresh Git payloads and fresh full filesystem inventories.
    for key in artifacts:
        expected = source['members'] if key == 'source' else inventory(
            trees[key]['directory'], trees[key]['archiveRoot'])['members']
        artifacts[key] = verify_zip(output / (key + '.zip'), expected)
    app_manifest = {'schema': 'actual-app-manifest073-v1', 'identity': build,
                    'inputTree': trees['macOS'], 'archive': artifacts['macOS']}
    source_map = {'schema': 'source-map073-v1', 'runtimeCommit': RUNTIME_COMMIT,
                  'sourceSnapshotCommit': args.source_snapshot_commit,
                  'sourceFingerprint': FINGERPRINT, 'sourceInputs': 732,
                  'files': source['runtimeFiles']}
    for name, document in (('actual-app-manifest073.json', app_manifest),
                           ('source-map073.json', source_map)):
        with (output / name).open('xb') as stream:
            stream.write(encoded(document))
    receipt = {'schema': 'local-package-manifest073-v1', 'status': 'local_archives_verified',
               'scope': 'local_only_no_commit_no_upload', 'startedUtc': started,
               'finishedUtc': datetime.now(timezone.utc).isoformat(), 'repo': str(repo),
               'runtimeCommit': RUNTIME_COMMIT, 'sourceSnapshotCommit': args.source_snapshot_commit,
               'sourceFingerprint': FINGERPRINT, 'sourceInputs': 732,
               'appVersion': VERSION, 'buildGuid': BUILD_GUID, 'source': source,
               'appIdentity': build, 'fullTests': tests,
               'normalStatus': normal, 'nativeStatus': native,
               'trees': trees, 'pinnedOriginals': pins, 'artifacts': artifacts,
               'allInputsUnchangedBeforeAndAfter': True,
               'limits': ['Each ZIP is strictly below 100 MiB.',
                          'Source is an explicit selected profile, excluding all Docs/Evidence/.',
                          'No gameplay, normal balance, native pixel, performance or whole-release acceptance is inferred.',
                          'Unix type/mode/symlink text are preserved; timestamps use 1980-01-01.',
                          'ACLs, xattrs, resource forks and notarization are not represented.'],
               'sidecars': {name: {k: v for k, v in stable_read(output / name).items()
                                    if k in ('bytes', 'sha256')} for name in
                            ('actual-app-manifest073.json', 'source-map073.json')}}
    with (output / 'package-manifest073.json').open('xb') as stream:
        stream.write(encoded(receipt))
    return {'status': receipt['status'], 'outputDirectory': str(output),
            'manifest': str(output / 'package-manifest073.json'), 'artifacts': artifacts}


def verify(args):
    path = real_file(args.manifest)
    raw, pin = stable_read(path, collect=True)
    if args.manifest_sha256:
        require(pin['sha256'] == args.manifest_sha256, 'Reviewed manifest SHA differs')
    doc = decode(raw)
    require(doc.get('schema') == 'local-package-manifest073-v1'
            and doc.get('status') == 'local_archives_verified'
            and doc.get('runtimeCommit') == RUNTIME_COMMIT
            and doc.get('sourceFingerprint') == FINGERPRINT
            and doc.get('sourceInputs') == 732
            and doc.get('appVersion') == VERSION and doc.get('buildGuid') == BUILD_GUID,
            'Manifest identity/status differs')
    repo = real_directory(args.repo)
    source = source_inventory(repo, doc['sourceSnapshotCommit'])
    require(source == doc['source'] and source['runtimeFiles'] == source_inventory(
        repo, RUNTIME_COMMIT)['runtimeFiles'], 'Manifest/Git source partition/map differs')
    unchanged(doc['pinnedOriginals'], doc['trees'])
    receipts = {}
    require({'macOS', 'evidence', 'normal'} <= set(doc['trees'])
            and set(doc['trees']) <= {'macOS', 'evidence', 'normal', 'nativegallery'},
            'Manifest must preserve the three required actual input trees')
    require(set(doc['artifacts']) == {'source'} | set(doc['trees']), 'Archive/tree mapping differs')
    for key, old in doc['artifacts'].items():
        expected = source['members'] if key == 'source' else inventory(
            doc['trees'][key]['directory'], doc['trees'][key]['archiveRoot'])['members']
        actual = verify_zip(path.parent / (key + '.zip'), expected)
        require(actual['bytes'] == old['bytes'] and actual['sha256'] == old['sha256'],
                'Whole ZIP bytes/SHA differs: ' + key)
        receipts[key] = actual
    for name, old in doc['sidecars'].items():
        actual = stable_read(path.parent / name)
        require(actual['bytes'] == old['bytes'] and actual['sha256'] == old['sha256'],
                'Sidecar changed: ' + name)
    require(decode((path.parent / 'source-map073.json').read_bytes())['files'] == source['runtimeFiles'],
            'Standalone source map differs')
    require(decode((path.parent / 'actual-app-manifest073.json').read_bytes())['inputTree'] == doc['trees']['macOS'],
            'Standalone actual app manifest differs')
    return {'status': 'independently_verified', 'manifestSha256': pin['sha256'], 'artifacts': receipts}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    modes = parser.add_mutually_exclusive_group(required=True)
    modes.add_argument('--plan', action='store_true', help='Read-only source profile summary; no ZIP/output directory')
    modes.add_argument('--package', action='store_true', help='Package only after root has closed the actual inputs')
    modes.add_argument('--verify', action='store_true', help='Independently re-read ZIPs, Git blobs and original trees')
    parser.add_argument('--repo', type=Path, default=REPO)
    parser.add_argument('--source-snapshot-commit', help='Exact root-selected final documentation/source commit')
    parser.add_argument('--source-freeze', type=Path, default=HERE / 'source-freeze073-before-full.json')
    parser.add_argument('--tests-directory', type=Path, default=TESTS)
    parser.add_argument('--app-directory', type=Path, default=CANDIDATE / 'Build1/Frontier Reckoning.app')
    parser.add_argument('--build-receipt', type=Path, default=CANDIDATE / 'Build1/runner-receipt073.json')
    parser.add_argument('--evidence-directory', type=Path, help='Complete root-selected/staged 073 evidence tree')
    parser.add_argument('--normal-directory', type=Path, help='Complete actual normal runner tree with matrix/manifest.json')
    parser.add_argument('--normal-receipt', type=Path, help='Actual source-bound closed receipt within normal tree')
    parser.add_argument('--native-gallery-directory', type=Path, help='Optional exact selected native gallery tree')
    parser.add_argument('--native-receipt', type=Path, help='Actual closed source-bound native receipt; status retained verbatim')
    parser.add_argument('--output-directory', type=Path, help='New delivery directory; parent must already exist')
    parser.add_argument('--manifest', type=Path, help='Existing package-manifest073.json for --verify')
    parser.add_argument('--manifest-sha256', help='Optional externally reviewed manifest SHA-256 for --verify')
    args = parser.parse_args()
    try:
        if args.verify:
            require(args.manifest is not None, '--verify requires --manifest')
            result = verify(args)
        else:
            require(args.source_snapshot_commit, 'Root-selected --source-snapshot-commit is required')
            if args.plan:
                source = source_inventory(real_directory(args.repo), args.source_snapshot_commit)
                require(source['runtimeFiles'] == source_inventory(real_directory(args.repo), RUNTIME_COMMIT)['runtimeFiles']
                        and source['sourceInputs'] == 732 and source['sourceFingerprint'] == FINGERPRINT,
                        'Planned source snapshot runtime identity differs')
                result = {k: v for k, v in source.items() if k not in
                          ('members', 'runtimeFiles', 'selectedTrackedPaths', 'excludedTrackedPaths')}
                result.update(status='read_only_profile_plan',
                              selectedPaths=len(source['selectedTrackedPaths']),
                              excludedPaths=len(source['excludedTrackedPaths']),
                              rootPartitionComplete=True, zipNotCreated=True)
            else:
                for name in ('evidence_directory', 'normal_directory', 'normal_receipt', 'output_directory'):
                    require(getattr(args, name) is not None, '--package requires --' + name.replace('_', '-'))
                result = package(args)
        print(json.dumps(result, ensure_ascii=False, indent=2, sort_keys=True))
    except (OSError, ValueError, KeyError, TypeError, subprocess.CalledProcessError,
            ET.ParseError, zipfile.BadZipFile) as error:
        parser.exit(2, '073 packaging stopped: ' + str(error) + '\n')


if __name__ == '__main__':
    main()
