#!/usr/bin/env python3
"""Create or independently verify local 074 ZIPs from explicit immutable inputs.

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
import struct
import subprocess
import sys
import unicodedata
from xml.etree import ElementTree as ET
import zipfile

RUNTIME_COMMIT = '4344edacffad01bfc8d49ff90cb8361534f7702d'
FINGERPRINT = 'b72d96ee8ef5018ae7c2543c552117bb3e9370f525fcc2d97470c1ce9aeacdb9'
BUILD_GUID = None
SOURCE_COUNT = 740
VERSION = '0.17.0'
RUNTIME_ROOTS = ('Assets', 'Packages', 'ProjectSettings', 'Tools')
PROFILE = 'all-tracked-except-Docs-Evidence-074-v1'
LIMIT = 100 * 1024 * 1024
CHUNK = 1024 * 1024
EMPTY = hashlib.sha256(b'').hexdigest()
HERE = Path(__file__).resolve().parent
CANDIDATE = HERE.parent
REPO = Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')


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


class GitBlobs:
    """One bounded streaming cat-file reader; validate every object's address."""
    def __init__(self, repo):
        self.repo = repo
        self.algorithm = git_algorithm(repo)
        require(self.algorithm in ('sha1', 'sha256'), 'Unsupported Git object format')
        env = dict(os.environ); env['GIT_OPTIONAL_LOCKS'] = '0'
        self.process = subprocess.Popen(
            ['git', '--no-replace-objects', '-C', str(repo), 'cat-file', '--batch'],
            env=env, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    def __enter__(self):
        return self
    def __exit__(self, typ, value, traceback):
        if self.process.stdin is not None:
            self.process.stdin.close()
        if typ is not None:
            self.process.terminate()
        code = self.process.wait()
        error = self.process.stderr.read().decode('utf-8', 'replace')
        self.process.stdout.close(); self.process.stderr.close()
        if typ is None:
            require(code == 0, 'Git blob reader failed: ' + error[:1000])
    def read(self, oid, size, sink=None, collect=False):
        require(re.fullmatch(r'[0-9a-f]{40}|[0-9a-f]{64}', oid or ''), 'Invalid blob identity')
        self.process.stdin.write((oid + '\n').encode()); self.process.stdin.flush()
        header = self.process.stdout.readline().split()
        require(len(header) == 3 and header[0].decode() == oid and header[1] == b'blob'
                and int(header[2]) == size, 'Git blob header/address/size differs: ' + oid)
        digest = hashlib.sha256(); address = hashlib.new(self.algorithm)
        address.update(b'blob ' + str(size).encode() + b'\0')
        remaining = size; collected = bytearray() if collect else None
        while remaining:
            block = self.process.stdout.read(min(CHUNK, remaining))
            require(block, 'Unexpected Git blob EOF: ' + oid)
            remaining -= len(block); digest.update(block); address.update(block)
            if sink is not None:
                sink.write(block)
            if collected is not None:
                collected.extend(block)
        require(self.process.stdout.read(1) == b'\n' and address.hexdigest() == oid,
                'Git blob framing/address mismatch: ' + oid)
        return digest.hexdigest(), bytes(collected) if collected is not None else None


def source_members(selected):
    records = {'source/': record('source/', 'directory', 0o040755)}
    for name, item in selected.items():
        require(item['path'] == safe(name) and item['bytes'] >= 0
                and re.fullmatch(r'[0-9a-f]{64}', item['sha256']), 'Invalid source record')
        mode = int(item['gitTreeMode'], 8)
        require(mode in (0o100644, 0o100755, 0o120000), 'Unsupported source mode')
        member = 'source/' + name
        for parent in PurePosixPath(member).parents:
            if str(parent) != '.':
                key = str(parent) + '/'
                records.setdefault(key, record(key, 'directory', 0o040755))
        target = item.get('symlinkTarget')
        require((mode == 0o120000) == (target is not None), 'Source link type/target mismatch')
        records[member] = record(member, 'symlink' if mode == 0o120000 else 'file',
                                 0o120777 if mode == 0o120000 else mode,
                                 item['bytes'], item['sha256'], target)
        records[member]['gitObjectId'] = item['gitObjectId']
    validate_namespace(records)
    return records


def source_inventory(repo, commit):
    """Read/hash ALL committed blobs; partition selected and excluded paths."""
    exact_commit(repo, commit)
    tree = git(repo, 'ls-tree', '-r', '-l', '-z', '--full-tree', commit)
    selected, excluded, runtime = {}, {}, {}
    rows = [r for r in tree.split(b'\0') if r]
    with GitBlobs(repo) as blobs:
        for row in rows:
            header, name_raw = row.split(b'\t', 1)
            mode_raw, kind, oid_raw, size_raw = header.split()
            name, oid, mode = safe(name_raw.decode('utf-8')), oid_raw.decode(), int(mode_raw, 8)
            require(kind == b'blob' and mode in (0o100644, 0o100755, 0o120000),
                    'Unsupported Git object/mode: ' + name)
            require(name.split('/')[0] not in ('Builds', 'Logs', 'obj', 'UserSettings'),
                    'Tracked generated input forbidden: ' + name)
            require(name not in selected and name not in excluded, 'Duplicate tracked path')
            size = int(size_raw)
            digest, link = blobs.read(oid, size, collect=mode == 0o120000)
            item = {'path': name, 'gitTreeMode': mode_raw.decode(),
                    'gitObjectId': oid, 'bytes': size, 'sha256': digest}
            if link is not None:
                item['symlinkTarget'] = os.fsdecode(link)
            if name == 'Docs/Evidence' or name.startswith('Docs/Evidence/'):
                excluded[name] = dict(item, excludedByDirectory='Docs/Evidence/')
                continue
            selected[name] = item
            if runtime_path(name):
                require(mode != 0o120000, 'Runtime symlink cannot match the regular-file test map')
                runtime[name] = digest
    require(len(selected) + len(excluded) == len(rows), 'Incomplete tracked path partition')
    return {'profile': PROFILE, 'notCompleteGitTreeArchive': True,
            'sourceSnapshotCommit': commit, 'gitObjectFormat': git_algorithm(repo),
            'fullTreeMetadataSha256': sha(tree), 'fullTreePathCount': len(rows),
            'allGitBlobsAddressAndSha256Verified': True,
            'selectedTrackedPaths': selected, 'excludedTrackedPaths': excluded,
            'runtimeFiles': runtime, 'sourceFingerprint': fingerprint(runtime),
            'sourceInputs': len(runtime), 'members': source_members(selected),
            'selectedBytes': sum(v['bytes'] for v in selected.values()),
            'excludedBytes': sum(v['bytes'] for v in excluded.values())}


def current_runtime(repo):
    files, identities, directories = {}, {}, {}
    def walk(folder):
        before = folder.lstat()
        require(stat.S_ISDIR(before.st_mode), 'Runtime directory is not a real directory')
        with os.scandir(folder) as entries:
            names = sorted(e.name for e in entries if not e.name.startswith('.') and e.name != '__pycache__')
        directories[str(folder)] = {'identity': list(stable_identity(before)), 'names': names}
        for name in names:
            path = folder / name; node = path.lstat()
            if stat.S_ISDIR(node.st_mode):
                walk(path)
            elif stat.S_ISREG(node.st_mode):
                pin = stable_read(path); relative = path.relative_to(repo).as_posix()
                files[relative] = pin['sha256']; identities[str(path)] = pin
            else:
                raise ValueError('Unsupported runtime node: ' + str(path))
        require(stable_identity(folder.lstat()) == stable_identity(before), 'Runtime directory changed')
    for name in RUNTIME_ROOTS:
        walk(repo / name)
    return {'files': files, 'sourceInputs': len(files), 'sourceFingerprint': fingerprint(files),
            'pinnedFiles': identities, 'directories': directories}


def pinned_json(path, pins):
    path = real_file(path)
    raw, item = stable_read(path, collect=True)
    pins[str(path)] = item
    return decode(raw)


def exact_source_map(doc, files):
    require(doc.get('files') == files and doc.get('sourceFingerprint') == FINGERPRINT
            and fingerprint(doc['files']) == FINGERPRINT, 'Exact source map/fingerprint differs')


def validate_tests(tests, files, pins, expected_edit=None, expected_play=None):
    tests = real_directory(tests)
    doc = pinned_json(tests / 'source-evidence.json', pins)
    require(doc.get('schema') == 1 and doc.get('recordedAtUtc'), 'Unclosed/unknown full source evidence')
    exact_source_map(doc, files)
    results = {}
    for mode, expected in (('EditMode', expected_edit), ('PlayMode', expected_play)):
        path = tests / (mode + '.xml'); raw, item = stable_read(path, collect=True); pins[str(path)] = item
        report = doc['reports'][mode]
        require(report['file'] == path.name and report['sha256'] == item['sha256'], 'Test report/source evidence mismatch: ' + mode)
        require(b'<!DOCTYPE' not in raw and b'<!ENTITY' not in raw, 'Unexpected XML declarations')
        root = ET.fromstring(raw); a = root.attrib; cases = list(root.iter('test-case'))
        count = int(a.get('total', '0'))
        require(count > 0 and root.tag == 'test-run' and a.get('result') == 'Passed' and a.get('end-time')
                and int(a.get('passed', '0')) == count and report['passed'] == count
                and all(int(a.get(k, '0')) == 0 for k in ('failed', 'skipped', 'inconclusive'))
                and len(cases) == count and all(c.get('result') == 'Passed' for c in cases)
                and all(s.get('result') == 'Passed' for s in root.iter('test-suite')),
                'Full regression XML is incomplete/nonpassing: ' + mode)
        require(expected is None or count == expected, 'Actual test count differs from root expectation: ' + mode)
        names = [c.get('fullname') for c in cases]
        require(all(names) and len(set(names)) == count, 'Missing/duplicate test fullname: ' + mode)
        results[mode] = {'passed': count, 'xmlSha256': item['sha256'], 'endTimeAsRecorded': a['end-time']}
        pins[str(tests / (mode + '.log'))] = stable_read(tests / (mode + '.log'))
    return {'directory': str(tests), 'totalPassed': sum(v['passed'] for v in results.values()), 'reports': results}


def closed_receipt(path, pins):
    doc = pinned_json(path, pins)
    finished = doc.get('finishedUtc') or doc.get('finishedAtUtc')
    require(isinstance(finished, str) and finished.strip(), 'Actual process finish timestamp required')
    status = doc.get('status')
    require(isinstance(status, str) and status.strip()
            and not re.search(r'running|pending|prepared|planned|in_progress|failed|error|cancel|unknown|unverified|unclosed|unassessed|not_assessed|not_tested', status, re.I),
            'Receipt is unclosed/failed/unknown: ' + str(path))
    values = [doc[k] for k in ('sourceFingerprint', 'sourceFingerprintBefore', 'sourceFingerprintAfter') if k in doc]
    require(values and all(v == FINGERPRINT for v in values), 'Receipt source binding differs')
    require(doc.get('runtimeCommit') == RUNTIME_COMMIT and doc.get('exitCode') == 0
            and doc.get('sourceStable') is True, 'Receipt lacks actual successful stable runtime closure')
    return {'path': str(real_file(path)), 'statusAsRecorded': status,
            'finishedAsRecorded': finished, 'document': doc}


def validate_build(app, receipt_path, pins):
    app = real_directory(app); closure = closed_receipt(receipt_path, pins); doc = closure['document']
    require(real_directory(doc['app']) == app, 'Build receipt actual app path differs')
    plist_raw, plist_pin = stable_read(app / 'Contents/Info.plist', collect=True); pins[plist_pin['path']] = plist_pin
    info = plistlib.loads(plist_raw)
    require(info.get('CFBundleShortVersionString') == VERSION, 'Actual app version differs')
    executable_name = info.get('CFBundleExecutable')
    require(isinstance(executable_name, str) and '/' not in safe(executable_name), 'Invalid app executable name')
    executable = app / 'Contents/MacOS' / executable_name; node = executable.lstat()
    require(stat.S_ISREG(node.st_mode) and node.st_mode & 0o111, 'Actual executable lacks Unix execution permission')
    exe_pin = stable_read(executable); pins[str(executable)] = exe_pin
    boot_raw, boot_pin = stable_read(app / 'Contents/Resources/Data/boot.config', collect=True); pins[boot_pin['path']] = boot_pin
    boot = boot_raw.decode('utf-8'); guids = re.findall(r'^build-guid=([0-9a-f]{32})\r?$', boot, re.M)
    require(len(guids) == 1 and doc.get('bootConfig') == boot, 'Actual app/receipt boot configuration differs')
    require(BUILD_GUID is None or guids[0] == BUILD_GUID, 'Actual app GUID differs from root expected GUID')
    return {'app': str(app), 'appVersion': VERSION, 'buildGuid': guids[0],
            'executable': exe_pin, 'receipt': str(real_file(receipt_path)), 'closure': closure}


def zip_info(item):
    info = zipfile.ZipInfo(item['path'], (1980, 1, 1, 0, 0, 0))
    info.create_system = 3
    info.external_attr = item['mode'] << 16
    if item['type'] == 'directory':
        info.external_attr |= 0x10
    info.compress_type = zipfile.ZIP_DEFLATED
    info.file_size = item['bytes']
    return info


def write_zip(path, records, payload):
    validate_namespace(records)
    with zipfile.ZipFile(path, 'x', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for name in sorted(records):
            item = records[name]
            with archive.open(zip_info(item), 'w', force_zip64=item['bytes'] >= zipfile.ZIP64_LIMIT) as out:
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


def source_payload(blobs, source):
    def read(name, item, sink):
        if item['type'] == 'directory':
            return 0, EMPTY
        selected = source['selectedTrackedPaths'][name[len('source/'):]]
        digest, _ = blobs.read(selected['gitObjectId'], item['bytes'], sink=sink)
        return item['bytes'], digest
    return read


def verify_zip_eof(path):
    size = path.stat().st_size
    with path.open('rb') as stream:
        tail_size = min(size, 65557); stream.seek(size - tail_size); tail = stream.read()
    offset = tail.rfind(b'PK\x05\x06')
    require(offset >= 0 and len(tail) - offset >= 22, 'Missing/truncated ZIP end record')
    record = struct.unpack('<4s4H2IH', tail[offset:offset + 22])
    require(record[1] == record[2] == 0 and offset + 22 + record[-1] == len(tail),
            'Multidisk ZIP or trailing bytes after actual ZIP EOF')


def verify_zip(path, expected, object_format=None):
    """Fresh exhaustive readback: EOF, CRC, bytes/SHA, modes and Git addresses."""
    path = real_file(path); before = stable_identity(path.lstat())
    require(before[3] < LIMIT, 'ZIP exceeds strict delivery cap')
    validate_namespace(expected); verify_zip_eof(path)
    with zipfile.ZipFile(path, 'r') as archive:
        infos = archive.infolist(); names = [i.filename for i in infos]
        require(len(names) == len(set(names)) and set(names) == set(expected), 'ZIP member set differs')
        require(archive.testzip() is None, 'ZIP CRC mismatch')
        for info in infos:
            item = expected[info.filename]
            require(info.orig_filename == info.filename and info.create_system == 3
                    and (info.external_attr >> 16) == item['mode']
                    and info.is_dir() == (item['type'] == 'directory') and not info.flag_bits & 1
                    and info.file_size == item['bytes'], 'ZIP mode/type/flags/size mismatch: ' + info.filename)
            digest, count, link = hashlib.sha256(), 0, bytearray()
            address = None
            if 'gitObjectId' in item:
                require(object_format in ('sha1', 'sha256'), 'Source archive needs Git object format')
                address = hashlib.new(object_format); address.update(b'blob ' + str(item['bytes']).encode() + b'\0')
            with archive.open(info) as stream:
                for block in iter(lambda: stream.read(CHUNK), b''):
                    digest.update(block); count += len(block)
                    if address is not None:
                        address.update(block)
                    if item['type'] == 'symlink':
                        link.extend(block)
            require(count == item['bytes'] and digest.hexdigest() == item['sha256'], 'ZIP payload mismatch: ' + info.filename)
            if address is not None:
                require(address.hexdigest() == item['gitObjectId'], 'Source ZIP Git blob address differs')
            if item['type'] == 'symlink':
                require(os.fsdecode(bytes(link)) == item['symlinkTarget'], 'ZIP link target differs')
    require(stable_identity(path.lstat()) == before, 'ZIP changed during readback')
    receipt = stable_read(path)
    require(receipt['identity'] == list(before), 'ZIP changed after readback'); receipt.pop('identity')
    receipt.update(memberCount=len(expected), verifiedEOF=True, verifiedCRC=True, verifiedExactMembers=True,
                   verifiedBytesAndSha256=True, verifiedUnixTypeAndModes=True, verifiedSymlinkTargets=True,
                   verifiedSelectedGitBlobAddresses=object_format is not None)
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
    require(args.source_snapshot_commit != RUNTIME_COMMIT,
            'Source snapshot and approved runtime must be separate explicit commits')
    source = source_inventory(repo, args.source_snapshot_commit)
    runtime = source_inventory(repo, RUNTIME_COMMIT)
    require(source['runtimeFiles'] == runtime['runtimeFiles'] and source['sourceInputs'] == SOURCE_COUNT
            and source['sourceFingerprint'] == FINGERPRINT, 'Snapshot differs from frozen runtime')
    current = current_runtime(repo)
    require(current['files'] == source['runtimeFiles'], 'Current runtime differs from tested Git blobs')
    pins = {}; tests = validate_tests(args.tests_directory, source['runtimeFiles'], pins,
                                      args.expected_edit_count, args.expected_play_count)
    if args.source_freeze is not None:
        exact_source_map(pinned_json(args.source_freeze, pins), source['runtimeFiles'])
    if args.test_receipt is not None:
        tests['runnerClosure'] = closed_receipt(args.test_receipt, pins)
    build_directory = real_directory(args.build_directory)
    app = build_directory / args.app_name
    build_receipt = args.build_receipt or build_directory / 'runner-receipt074.json'
    build = validate_build(app, build_receipt, pins)
    normal_directory = real_directory(args.normal_directory)
    normal_receipt = args.normal_receipt or normal_directory / 'runner-receipt074.json'
    normal = closed_receipt(normal_receipt, pins)
    require(real_file(normal_receipt).is_relative_to(normal_directory), 'Normal receipt must be inside complete normal input')
    require(normal['document'].get('preparationRun') is False and normal['document'].get('caseInterventions') is False,
            'Ordinary observer must explicitly record no preparation/interventions')
    matrix = pinned_json(normal_directory / 'matrix/manifest.json', pins)
    cases = matrix.get('cases')
    require(matrix.get('finishedUtc') and matrix.get('sourceFingerprint') == FINGERPRINT
            and matrix.get('version') == VERSION and isinstance(matrix.get('status'), str)
            and not re.search(r'running|pending|prepared|planned|in_progress|failed|error|cancel|unknown|unverified|unclosed|unassessed|not_assessed|not_tested', matrix['status'], re.I)
            and isinstance(cases, list) and cases, 'Normal matrix is not actually closed and source bound')
    require(all(c.get('finishedUtc') and c.get('status') in ('natural_terminal', 'observation_cap')
                and c.get('steps', 0) > 0 for c in cases), 'Normal case has no actual closed observation')
    require(len({c.get('id') for c in cases}) == len(cases) and all(c.get('id') for c in cases), 'Duplicate/missing normal case ID')
    normal['matrixStatusAsRecorded'] = matrix['status']; normal['matrixCaseCountAsRecorded'] = len(cases)
    normal['matrixCasesAsRecorded'] = cases
    trees = {'macOS': inventory(app, app.name), 'evidence': inventory(args.evidence_directory, 'Evidence074'),
             'normal': inventory(normal_directory, 'Normal074')}
    evidence_pins = {p: v for p, v in pins.items() if not Path(p).is_relative_to(app)}
    pin_tree_duplicates(trees['evidence'], evidence_pins)
    output = Path(args.output_directory).absolute()
    require(not output.exists() and not output.is_symlink(), 'Output directory must be new')
    output = real_directory(output.parent) / output.name
    for root in [repo] + [Path(t['directory']) for t in trees.values()]:
        require(not output.is_relative_to(root) and not root.is_relative_to(output), 'Output overlaps an input tree/repo')
    unchanged(pins, trees); require(current_runtime(repo) == current, 'Current runtime changed before packaging')
    started = datetime.now(timezone.utc).isoformat(); output.mkdir(mode=0o755)
    # Failed output is retained for diagnosis. It is never reused or relabelled complete.
    artifacts = {}
    with GitBlobs(repo) as blobs:
        write_zip(output / 'source.zip', source['members'], source_payload(blobs, source))
    artifacts['source'] = verify_zip(output / 'source.zip', source['members'], source['gitObjectFormat'])
    for key, tree in trees.items():
        write_zip(output / (key + '.zip'), tree['members'], filesystem_payload(tree))
        artifacts[key] = verify_zip(output / (key + '.zip'), tree['members'])
    unchanged(pins, trees); require(current_runtime(repo) == current, 'Current runtime changed during packaging')
    require(source_inventory(repo, args.source_snapshot_commit) == source
            and source_inventory(repo, RUNTIME_COMMIT) == runtime, 'Committed source inventory changed')
    for key in artifacts:
        expected = source['members'] if key == 'source' else inventory(trees[key]['directory'], trees[key]['archiveRoot'])['members']
        artifacts[key] = verify_zip(output / (key + '.zip'), expected,
                                    source['gitObjectFormat'] if key == 'source' else None)
    app_manifest = {'schema': 'actual-app-manifest074-v1', 'identity': build,
                    'inputTree': trees['macOS'], 'archive': artifacts['macOS']}
    source_map = {'schema': 'source-map074-v1', 'runtimeCommit': RUNTIME_COMMIT,
                  'sourceSnapshotCommit': args.source_snapshot_commit, 'sourceFingerprint': FINGERPRINT,
                  'sourceInputs': SOURCE_COUNT, 'files': source['runtimeFiles']}
    sidecars = {'actual-app-manifest074.json': app_manifest, 'source-map074.json': source_map}
    for name, document in sidecars.items():
        with (output / name).open('xb') as stream:
            stream.write(encoded(document))
    receipt = {'schema': 'local-package-manifest074-v1', 'status': 'local_archives_verified',
               'scope': 'local_only_no_commit_no_upload_no_native_acceptance', 'startedUtc': started,
               'finishedUtc': datetime.now(timezone.utc).isoformat(), 'repo': str(repo),
               'runtimeCommit': RUNTIME_COMMIT, 'sourceSnapshotCommit': args.source_snapshot_commit,
               'sourceFingerprint': FINGERPRINT, 'sourceInputs': SOURCE_COUNT, 'appVersion': VERSION,
               'buildGuid': build['buildGuid'], 'source': source, 'runtimeSource': runtime,
               'appIdentity': build, 'fullTests': tests, 'normalStatus': normal,
               'nativeAcceptance': 'not_assessed_by_this_packager', 'trees': trees,
               'pinnedOriginals': pins, 'currentRuntime': current, 'artifacts': artifacts,
               'allInputsUnchangedBeforeAndAfter': True,
               'limits': ['Exactly four ZIPs, each strictly below 100 MiB.',
                          'Source profile excludes the entire Docs/Evidence directory; full selected/excluded Git blob partition is recorded.',
                          'Package closure does not infer gameplay, balance, native, performance or whole-release acceptance.',
                          'Unix type, permissions and symlink text preserved; timestamps set to 1980-01-01.',
                          'Hard links become ordinary file payloads; ACLs, xattrs, resource forks, signing and notarization are not represented.',
                          'Runtime commit and source snapshot are separate explicit identities; this manifest does not reference its own hash.'],
               'sidecars': {name: {k: v for k, v in stable_read(output / name).items() if k in ('bytes', 'sha256')}
                            for name in sidecars}}
    with (output / 'package-manifest074.json').open('xb') as stream:
        stream.write(encoded(receipt))
    result = verify_manifest(output / 'package-manifest074.json')
    return {'status': receipt['status'], 'outputDirectory': str(output),
            'manifest': str(output / 'package-manifest074.json'), 'archives': artifacts,
            'finalOfflineReadback': result['status']}


def validate_source_document(source):
    require(source.get('profile') == PROFILE and source.get('allGitBlobsAddressAndSha256Verified') is True,
            'Unexpected/unverified source partition profile')
    selected, excluded = source['selectedTrackedPaths'], source['excludedTrackedPaths']
    require(isinstance(selected, dict) and isinstance(excluded, dict) and not set(selected) & set(excluded)
            and len(selected) + len(excluded) == source['fullTreePathCount'], 'Invalid source partition')
    for group, excluded_group in ((selected, False), (excluded, True)):
        for name, item in group.items():
            require(item['path'] == safe(name) and item['bytes'] >= 0
                    and re.fullmatch(r'[0-9a-f]{64}', item['sha256'])
                    and re.fullmatch(r'[0-9a-f]{40}|[0-9a-f]{64}', item['gitObjectId'])
                    and item['gitTreeMode'] in ('100644', '100755', '120000')
                    and ((name == 'Docs/Evidence' or name.startswith('Docs/Evidence/')) == excluded_group),
                    'Source selection/exclusion record differs')
    runtime = {name: item['sha256'] for name, item in selected.items() if runtime_path(name)}
    require(runtime == source['runtimeFiles'] and fingerprint(runtime) == source['sourceFingerprint']
            and len(runtime) == source['sourceInputs'] and source_members(selected) == source['members'],
            'Source member/runtime reconstruction differs')
    require(sum(v['bytes'] for v in selected.values()) == source['selectedBytes']
            and sum(v['bytes'] for v in excluded.values()) == source['excludedBytes'], 'Source partition byte totals differ')


def verify_manifest(path, expected_manifest_sha256=None):
    """Portable offline verification: only existing manifest, sidecars and ZIPs."""
    path = real_file(path); raw, pin = stable_read(path, collect=True); doc = decode(raw)
    require(expected_manifest_sha256 is None or pin['sha256'] == expected_manifest_sha256, 'Reviewed manifest SHA differs')
    require(doc.get('schema') == 'local-package-manifest074-v1' and doc.get('status') == 'local_archives_verified'
            and doc.get('runtimeCommit') == RUNTIME_COMMIT and doc.get('sourceFingerprint') == FINGERPRINT
            and doc.get('sourceInputs') == SOURCE_COUNT and doc.get('appVersion') == VERSION
            and re.fullmatch(r'[0-9a-f]{32}', doc.get('buildGuid', ''))
            and (BUILD_GUID is None or doc['buildGuid'] == BUILD_GUID)
            and doc.get('allInputsUnchangedBeforeAndAfter') is True, 'Manifest identity/status differs')
    validate_source_document(doc['source']); validate_source_document(doc['runtimeSource'])
    require(doc['sourceSnapshotCommit'] == doc['source']['sourceSnapshotCommit']
            and doc['runtimeCommit'] == doc['runtimeSource']['sourceSnapshotCommit']
            and doc['sourceSnapshotCommit'] != doc['runtimeCommit']
            and doc['sourceFingerprint'] == doc['source']['sourceFingerprint'] == doc['runtimeSource']['sourceFingerprint'] == doc['currentRuntime']['sourceFingerprint']
            and doc['sourceInputs'] == doc['source']['sourceInputs'] == doc['runtimeSource']['sourceInputs'] == doc['currentRuntime']['sourceInputs']
            and doc['source']['runtimeFiles'] == doc['runtimeSource']['runtimeFiles']
            and doc['source']['runtimeFiles'] == doc['currentRuntime']['files'], 'Runtime/snapshot/current identity mismatch')
    require(set(doc['trees']) == {'macOS', 'evidence', 'normal'}
            and set(doc['artifacts']) == {'source', 'macOS', 'evidence', 'normal'}, 'Exactly four archive mappings required')
    receipts = {}
    for key, old in doc['artifacts'].items():
        expected = doc['source']['members'] if key == 'source' else doc['trees'][key]['members']
        actual = verify_zip(path.parent / (key + '.zip'), expected,
                            doc['source']['gitObjectFormat'] if key == 'source' else None)
        require(actual['bytes'] == old['bytes'] and actual['sha256'] == old['sha256'], 'Whole ZIP byte/SHA mismatch: ' + key)
        receipts[key] = actual
    expected_sidecars = {'source-map074.json', 'actual-app-manifest074.json'}
    require(set(doc['sidecars']) == expected_sidecars, 'Unexpected/missing sidecar mapping')
    sidecars = {}
    for name, old in doc['sidecars'].items():
        data, actual = stable_read(path.parent / name, collect=True)
        require(actual['bytes'] == old['bytes'] and actual['sha256'] == old['sha256'], 'Sidecar changed: ' + name)
        sidecars[name] = decode(data)
    require(sidecars['source-map074.json'] == {'schema': 'source-map074-v1', 'runtimeCommit': doc['runtimeCommit'],
            'sourceSnapshotCommit': doc['sourceSnapshotCommit'], 'sourceFingerprint': doc['sourceFingerprint'],
            'sourceInputs': doc['sourceInputs'], 'files': doc['source']['runtimeFiles']}, 'Standalone source map differs')
    require(sidecars['actual-app-manifest074.json'] == {'schema': 'actual-app-manifest074-v1',
            'identity': doc['appIdentity'], 'inputTree': doc['trees']['macOS'], 'archive': doc['artifacts']['macOS']},
            'Standalone app manifest differs')
    require(stable_read(path) == pin, 'Manifest changed during offline verification')
    return {'status': 'independently_verified_existing_archives', 'scope': 'offline_archive_integrity_only',
            'manifestSha256': pin['sha256'], 'artifacts': receipts}


def main():
    global RUNTIME_COMMIT, FINGERPRINT, SOURCE_COUNT, VERSION, BUILD_GUID
    parser = argparse.ArgumentParser(description=__doc__)
    modes = parser.add_mutually_exclusive_group(required=True)
    modes.add_argument('--package', action='store_true', help='Create only after root closes all actual inputs')
    modes.add_argument('--verify', action='store_true', help='Read existing manifest, sidecars and ZIPs only')
    parser.add_argument('--repo', type=Path, default=REPO)
    parser.add_argument('--runtime-commit', default=RUNTIME_COMMIT)
    parser.add_argument('--source-fingerprint', default=FINGERPRINT)
    parser.add_argument('--source-input-count', type=int, default=SOURCE_COUNT)
    parser.add_argument('--app-version', default=VERSION)
    parser.add_argument('--build-guid', help='Optional root-reviewed actual 32-hex app GUID')
    parser.add_argument('--source-snapshot-commit', '--source-snapshot', dest='source_snapshot_commit')
    parser.add_argument('--source-freeze', type=Path)
    parser.add_argument('--tests-directory', '--test-directory', dest='tests_directory', type=Path)
    parser.add_argument('--test-receipt', type=Path)
    parser.add_argument('--expected-edit-count', type=int)
    parser.add_argument('--expected-play-count', type=int)
    parser.add_argument('--build-directory', type=Path)
    parser.add_argument('--app-name', default='Frontier Reckoning.app')
    parser.add_argument('--build-receipt', type=Path)
    parser.add_argument('--evidence-directory', type=Path)
    parser.add_argument('--normal-directory', type=Path)
    parser.add_argument('--normal-receipt', type=Path)
    parser.add_argument('--output-directory', type=Path)
    parser.add_argument('--manifest', type=Path)
    parser.add_argument('--manifest-sha256', help='External reviewed manifest hash for offline verification')
    args = parser.parse_args()
    RUNTIME_COMMIT, FINGERPRINT, SOURCE_COUNT, VERSION, BUILD_GUID = (
        args.runtime_commit, args.source_fingerprint, args.source_input_count, args.app_version, args.build_guid)
    try:
        require(re.fullmatch(r'[0-9a-f]{40}|[0-9a-f]{64}', RUNTIME_COMMIT or '')
                and re.fullmatch(r'[0-9a-f]{64}', FINGERPRINT or '') and SOURCE_COUNT > 0
                and isinstance(VERSION, str) and VERSION.strip()
                and (BUILD_GUID is None or re.fullmatch(r'[0-9a-f]{32}', BUILD_GUID)), 'Invalid expected identity arguments')
        if args.verify:
            require(args.manifest is not None, '--verify requires --manifest')
            result = verify_manifest(args.manifest, args.manifest_sha256)
        else:
            for name in ('source_snapshot_commit', 'tests_directory', 'build_directory',
                         'evidence_directory', 'normal_directory', 'output_directory'):
                require(getattr(args, name) is not None, '--package requires --' + name.replace('_', '-'))
            require('/' not in safe(args.app_name) and args.app_name.endswith('.app'), 'App name must be one .app component')
            result = package(args)
        print(json.dumps(result, ensure_ascii=False, indent=2, sort_keys=True))
    except (OSError, ValueError, KeyError, TypeError, AttributeError, subprocess.CalledProcessError,
            ET.ParseError, zipfile.BadZipFile, zipfile.LargeZipFile, struct.error) as error:
        parser.exit(2, '074 packaging stopped: ' + str(error) + '\n')


if __name__ == '__main__':
    main()
