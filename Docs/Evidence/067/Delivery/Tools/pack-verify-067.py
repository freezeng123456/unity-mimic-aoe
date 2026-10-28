#!/usr/bin/env python3
"""Package frozen 0.10 / 067 inputs; never build, launch, upload or edit source.

Default is read-only preflight. --execute exclusively creates three ZIPs and an
OUTSIDE-ZIP manifest. --verify-existing checks those ZIPs against current frozen
inputs. --self-test exercises byte/mode/link/path checks in a temporary fixture.
Run --help for paths. Native must already be archived and frozen by its owner.
Tools/package-source.py --evidence-version 067 supplies the source allowlist;
its temporary ZIP is checked before the portable ZIP preserves Unix link bytes.
Replay checks here certify container/header identity, not C# replay execution.
"""
import argparse
import base64
import codecs
from datetime import datetime, timezone
import hashlib
import gzip
import importlib.util
import json
import os
from pathlib import Path, PurePosixPath
import plistlib
import re
import shutil
import stat
import struct
import subprocess
import sys
import tempfile
from xml.etree import ElementTree as ET
import zipfile
import zlib

sys.dont_write_bytecode = True
VERSION = '0.10.0'
RELEASE = '067'
PREFIX = 'Frontier-Reckoning-' + VERSION
NAMES = {'source': PREFIX + '-source.zip', 'client': PREFIX + '-macOS-arm64.zip',
         'gallery': PREFIX + '-gallery.zip'}
LIMIT_STORED = 64 * 1024 * 1024
LIMIT_RAW = 256 * 1024 * 1024
DEFAULT_PROJECT = Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
DEFAULT_OUTPUT = Path(__file__).resolve().parent.parent


def need(condition, reason):
    if not condition:
        raise ValueError(reason)


def pairs_unique(pairs):
    obj = {}
    for key, value in pairs:
        need(key not in obj, 'Duplicate JSON property: ' + key)
        obj[key] = value
    return obj


def parse_json(data):
    if isinstance(data, bytes):
        data = data.decode('utf-8', 'strict')
    return json.loads(data, object_pairs_hook=pairs_unique,
                      parse_constant=lambda value: (_ for _ in ()).throw(ValueError('Nonfinite JSON')))


def json_bytes(value):
    return (json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + '\n').encode('utf-8')


def hash_stream(stream):
    digest = hashlib.sha256()
    length = 0
    for block in iter(lambda: stream.read(1024 * 1024), b''):
        digest.update(block)
        length += len(block)
    return digest.hexdigest(), length


def file_hash(path):
    with path.open('rb') as stream:
        return hash_stream(stream)[0]


def record(path):
    mode = path.lstat().st_mode
    if stat.S_ISLNK(mode):
        data = os.fsencode(os.readlink(path))
        kind = 'symlink'
        digest, length = hashlib.sha256(data).hexdigest(), len(data)
    elif stat.S_ISDIR(mode):
        kind, digest, length = 'directory', hashlib.sha256(b'').hexdigest(), 0
    else:
        need(stat.S_ISREG(mode), 'Unsupported filesystem object: ' + str(path))
        with path.open('rb') as stream:
            digest, length = hash_stream(stream)
        kind = 'file'
    return {'kind': kind, 'mode': mode & 0xffff, 'bytes': length, 'sha256': digest}


def safe_member(name):
    need(name and '\x00' not in name and '\\' not in name and not name.startswith('/'), 'Unsafe ZIP name')
    path = PurePosixPath(name)
    need(not re.match(r'^[A-Za-z]:', name) and '..' not in path.parts and '.' not in name.split('/'), 'Unsafe ZIP path')
    need('// ' not in name and '//' not in name, 'Ambiguous ZIP path')
    return name


def path_entry(name, path, boundary=None):
    safe_member(name)
    original = record(path)
    if original['kind'] == 'symlink':
        target = os.readlink(path)
        need(not os.path.isabs(target) and '\x00' not in target, 'Absolute or invalid symlink')
        need(boundary is not None and path.resolve().is_relative_to(boundary.resolve()), 'Symlink escapes payload')
    if original['kind'] == 'directory' and not name.endswith('/'):
        name += '/'
    return {'name': name, 'path': path, 'record': original}


def memory_entry(name, data, mode=stat.S_IFREG | 0o644):
    safe_member(name)
    return {'name': name, 'data': data,
            'record': {'kind': 'file', 'mode': mode, 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()}}


def tree_entries(base, prefix):
    need(base.is_dir() and not base.is_symlink(), 'Missing regular directory: ' + str(base))
    entries = []
    def visit(path, name):
        entry = path_entry(name, path, base)
        entries.append(entry)
        if entry['record']['kind'] == 'directory':
            for child in sorted(path.iterdir()):
                visit(child, name.rstrip('/') + '/' + child.name)
    visit(base, prefix)
    return entries


def source_paths(project):
    # Exact files-only allowlist and evidence filter of Tools/package-source.py.
    allowed = ('Assets', 'Packages', 'ProjectSettings', 'Docs', 'SourceAssets', 'Tools', 'README.md', '.gitignore')
    files = []
    for name in allowed:
        path = project / name
        if path.is_file():
            files.append(path)
        elif path.is_dir():
            files.extend(p for p in path.rglob('*') if p.is_file() and not p.name.startswith('.') and '__pycache__' not in p.parts)
    evidence = project / 'Docs/Evidence'
    return sorted(p for p in files if not p.is_relative_to(evidence) or p.relative_to(evidence).parts[0] == RELEASE)


def source_entries(project):
    return [path_entry('FrontierReckoning/' + p.relative_to(project).as_posix(), p, project) for p in source_paths(project)]


def verify_tests(project):
    evidence_dir = project / 'Docs/Evidence/067/Tests'
    spec = importlib.util.spec_from_file_location('verification_evidence_067', project / 'Tools/verification_evidence.py')
    need(spec is not None and spec.loader is not None, 'Missing source evidence verifier')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    evidence = parse_json((evidence_dir / 'source-evidence.json').read_bytes())
    need(module.verify(project, evidence_dir) == evidence['sourceFingerprint'], 'Frozen source fingerprint changed')
    need(bool(evidence['files']), 'Frozen source inputs are missing')
    counts = {}
    for mode in ('EditMode', 'PlayMode'):
        expected = evidence['reports'][mode]['passed']
        root = ET.parse(evidence_dir / (mode + '.xml')).getroot()
        need(type(expected) is int and expected > 0 and root.tag == 'test-run' and root.get('result') == 'Passed'
             and int(root.get('total', '0')) == expected and int(root.get('passed', '0')) == expected
             and int(root.get('failed', '0')) == 0, 'Incomplete or changed ' + mode + ' XML')
        counts[mode] = expected
    counts.update(total=sum(counts.values()), failed=0)
    return evidence, counts


def app_identity(app, receipt, evidence):
    need(app.name == 'Frontier Reckoning.app' and app.is_dir() and not app.is_symlink(), 'Require the final app bundle')
    plist = plistlib.loads((app / 'Contents/Info.plist').read_bytes())
    need(plist.get('CFBundleShortVersionString') == VERSION, 'App version mismatch')
    executable_name = plist.get('CFBundleExecutable', '')
    need(executable_name and Path(executable_name).name == executable_name, 'Unsafe executable name')
    binary = app / 'Contents/MacOS' / executable_name
    need(binary.is_file() and not binary.is_symlink() and binary.stat().st_mode & 0o111, 'Missing executable or Unix executable permission')
    with binary.open('rb') as stream:
        header = stream.read(12)
    need(header[:4] == b'\xcf\xfa\xed\xfe' and struct.unpack('<I', header[4:8])[0] == 0x0100000c,
         'Executable is not a thin macOS arm64 Mach-O')
    boot = app / 'Contents/Resources/Data/boot.config'
    match = re.findall(r'^build-guid=([0-9a-f]{32})$', boot.read_text(encoding='utf-8'), re.M)
    guid = receipt.get('buildGuid')
    need(isinstance(guid, str) and re.fullmatch(r'[0-9a-f]{32}', guid) and match == [guid], 'Final build GUID mismatch')
    need(receipt.get('schema') == 1 and receipt.get('appVersion') == VERSION,
         'Build receipt identity mismatch')
    need(receipt.get('sourceFingerprint') == evidence['sourceFingerprint'] and receipt.get('sourceFileCount') == len(evidence['files'])
         and receipt.get('sourceFingerprintVerifiedAfterBuild') is True, 'Build receipt source binding mismatch')
    need(receipt.get('architecture') == 'arm64' and receipt.get('executableSha256') == file_hash(binary), 'Build receipt executable mismatch')
    need(isinstance(receipt.get('unityVersion'), str) and bool(receipt['unityVersion'])
         and re.fullmatch(r'[0-9a-f]{40}', receipt.get('runtimeCommit', '')),
         'Build receipt Unity/runtime commit mismatch')
    need(receipt.get('sceneSha256') == evidence['files']['Assets/Frontier/Scenes/FrontierSkirmish.unity'], 'Build scene differs from tested scene')
    return {'appVersion': VERSION, 'buildGuid': guid, 'architecture': 'arm64',
            'executableSha256': file_hash(binary), 'bootConfigSha256': file_hash(boot),
            'runtimeCommit': receipt['runtimeCommit'], 'unityVersion': receipt['unityVersion'],
            'sourceFingerprint': evidence['sourceFingerprint'], 'sourceFileCount': len(evidence['files']),
            'development': receipt.get('development'), 'scriptingBackend': receipt.get('scriptingBackend')}


def replay_header(prefix):
    # Generated DTO fields precede anchors. Do not materialize a 235MB world tree.
    decoder = json.JSONDecoder(object_pairs_hook=pairs_unique)
    # Full raw UTF8 is validated before this call; the retained byte prefix may
    # end halfway through a later Unicode character after the ASCII header.
    text = codecs.getincrementaldecoder('utf-8')('strict').decode(prefix, final=False)
    cursor = 0
    def ws(pos):
        while pos < len(text) and text[pos].isspace(): pos += 1
        return pos
    cursor = ws(cursor)
    need(text[cursor:cursor + 1] == '{', 'Replay document must be an object')
    cursor += 1
    result = {}
    while True:
        key, cursor = decoder.raw_decode(text, ws(cursor))
        need(isinstance(key, str) and key not in result, 'Invalid replay header key')
        cursor = ws(cursor)
        need(text[cursor:cursor + 1] == ':', 'Invalid replay header separator')
        cursor = ws(cursor + 1)
        if key == 'anchors':
            need(text[cursor:cursor + 1] == '[', 'Missing replay anchors')
            return result
        value, cursor = decoder.raw_decode(text, cursor)
        result[key] = value
        cursor = ws(cursor)
        need(text[cursor:cursor + 1] == ',', 'Replay header does not precede anchors')
        cursor += 1


def check_replay(path, identity):
    need(path.stat().st_size <= LIMIT_STORED, 'Replay stored size exceeds 64 MiB')
    envelope = parse_json(path.read_bytes())
    need(set(envelope) == {'schema', 'payload', 'sha256', 'encoding', 'payloadBase64'}
         and type(envelope['schema']) is int and envelope['schema'] == 2 and envelope['payload'] == ''
         and envelope['encoding'] == 'gzip-base64' and re.fullmatch(r'[0-9a-f]{64}', envelope['sha256']),
         'Unsupported or invalid replay envelope')
    encoded = envelope['payloadBase64']
    need(isinstance(encoded, str), 'Replay Base64 must be a string')
    compressed = base64.b64decode(encoded, validate=True)
    need(base64.b64encode(compressed).decode('ascii') == encoded and len(compressed) >= 18
         and compressed[:4] == b'\x1f\x8b\x08\x00', 'Noncanonical gzip/Base64 header')
    inflater = zlib.decompressobj(-15)
    utf = codecs.getincrementaldecoder('utf-8')('strict')
    digest, crc, length = hashlib.sha256(), 0, 0
    prefix = bytearray()
    for offset in range(10, len(compressed) - 8, 32768):
        pending = compressed[offset:min(offset + 32768, len(compressed) - 8)]
        while pending:
            raw = inflater.decompress(pending, min(1024 * 1024, LIMIT_RAW - length + 1))
            pending = inflater.unconsumed_tail
            need(not inflater.unused_data, 'Concatenated member or trailing gzip body')
            length += len(raw)
            need(length <= LIMIT_RAW, 'Replay raw data exceeds 256 MiB')
            digest.update(raw); crc = zlib.crc32(raw, crc); utf.decode(raw)
            if len(prefix) < 65536: prefix.extend(raw[:65536 - len(prefix)])
    need(inflater.eof and not inflater.unused_data and not inflater.unconsumed_tail, 'Truncated gzip deflate body')
    utf.decode(b'', final=True)
    need(struct.unpack('<II', compressed[-8:]) == (crc, length), 'Gzip footer CRC/ISIZE mismatch')
    need(digest.hexdigest() == envelope['sha256'], 'Raw replay SHA mismatch')
    header = replay_header(bytes(prefix))
    need(type(header.get('schema')) is int and header['schema'] == 1 and header.get('identity') == identity, 'Replay schema/build/rules identity mismatch')
    need(isinstance(header.get('incomplete'), bool) and bool(header.get('incompleteReason')) == header['incomplete'], 'Invalid recording interruption marker')
    need(header.get('recordLimit') == 200000 and header.get('eventLimit') == 1000000 and header.get('anchorInterval') == 300,
         'Unexpected replay limits')
    segment = str(header.get('segmentId'))
    need(path.name in ('replay-' + segment + '.replay.json', 'manual-export-' + segment + '.replay.json'),
         'Replay filename/segment mismatch')
    return {'file': path.name, 'storedBytes': path.stat().st_size, 'storedSha256': file_hash(path),
            'rawBytes': length, 'rawSha256': digest.hexdigest(), 'rawLimitRemainingBytes': LIMIT_RAW - length,
            'segmentId': header['segmentId'], 'matchId': header['matchId'], 'startKind': header['startKind'],
            'finishReason': header['finishReason'], 'incomplete': header['incomplete'], 'identity': header['identity'],
            'checkScope': 'UTF8/gzip/CRC/ISIZE/raw SHA/header identity; simulation verification belongs to Native evidence'}


def verify_archive_receipt(native, identity):
    receipt_path = native / 'archive-receipt.json'
    if not receipt_path.exists():
        return {'present': False, 'scope': 'No owner archive receipt available; raw file/ZIP checks still apply'}
    receipt = parse_json(receipt_path.read_bytes())
    for key in ('buildGuid', 'appVersion', 'runtimeCommit', 'sourceFingerprint', 'sourceFileCount'):
        need(receipt.get(key) == identity[key], 'Native archive receipt identity mismatch: ' + key)
    files = receipt.get('files')
    need(isinstance(files, list) and bool(files), 'Native archive receipt has no files')
    encoded = (json.dumps(files, ensure_ascii=False, sort_keys=True, indent=2, allow_nan=False) + '\n').encode('utf-8')
    need(hashlib.sha256(encoded).hexdigest() == receipt.get('inventorySha256'), 'Native inventory digest mismatch')
    seen = set()
    for item in files:
        name = item['file']; safe_member(name)
        need(name not in seen and not name.endswith('/'), 'Duplicate/directory Native receipt member')
        seen.add(name); path = native / name
        need(path.resolve().is_relative_to(native.resolve()) and path.is_file() and not path.is_symlink(),
             'Native receipt member is missing or outside archive')
        need(path.stat().st_size == item['bytes'] and file_hash(path) == item['sha256'], 'Native receipt byte/SHA mismatch: ' + name)
        mode = item.get('mode', '')
        need(isinstance(mode, str) and re.fullmatch(r'0?[0-7]{3}', mode)
             and stat.S_IMODE(path.stat().st_mode) == int(mode, 8), 'Native receipt mode mismatch: ' + name)
        if item.get('transform') == 'copy':
            need(item['originalBytes'] == item['bytes'] and item['originalSha256'] == item['sha256'], 'Native original/copy mismatch')
        elif item.get('transform') == 'gzip':
            need(path.suffix == '.gz', 'Gzip receipt member needs .gz')
            with path.open('rb') as stream: head = stream.read(10)
            need(head[:4] == b'\x1f\x8b\x08\x00' and head[4:8] == b'\0\0\0\0', 'Native gzip header is not deterministic mtime0')
            digest, size, lines = hashlib.sha256(), 0, 0
            with gzip.open(path, 'rb') as stream:
                for block in iter(lambda: stream.read(1024 * 1024), b''):
                    digest.update(block); size += len(block); lines += block.count(b'\n')
            need(size == item['originalBytes'] and digest.hexdigest() == item['originalSha256'], 'Native gzip differs from raw original')
            need(item.get('decompressedBytes') == size and item.get('decompressedSha256') == digest.hexdigest()
                 and item.get('decompressionVerified') is True and item.get('eventLines') == lines,
                 'Native gzip lineage/line count mismatch')
        else:
            need(item.get('transform') == 'redact', 'Unknown Native archive transform')
    # Root-owned README/acceptance and receipt itself are intentionally outside
    # the archiver's member list; they are independently inventoried by the ZIP.
    return {'present': True, 'file': receipt_path.name, 'sha256': file_hash(receipt_path),
            'members': len(files), 'inventorySha256': receipt['inventorySha256'],
            'scope': 'All listed bytes/modes and gzip raw lineage; root acceptance/README are separate payload members'}


def verify_native(native, identity, project):
    need((native / 'native-acceptance.json').is_file(), 'Native owner must freeze native-acceptance.json first')
    acceptance = parse_json((native / 'native-acceptance.json').read_bytes())
    expected_guid = (acceptance.get('expectedBuildGuid') or acceptance.get('buildGuid')
                     or acceptance.get('identity', {}).get('buildGuid')
                     or acceptance.get('identity', {}).get('buildIdentity'))
    need(expected_guid == identity['buildGuid'], 'Native acceptance must declare the final receipt build GUID')
    guid = identity['buildGuid']
    archive_receipt = verify_archive_receipt(native, identity)
    rules = parse_json((project / 'Assets/Resources/Rules.json').read_bytes())
    civs = {c['id']: c['name'] for c in rules['civs']}
    required = ['coast', 'river', 'france', 'ottoman']
    need([civs[x] for x in required] == ['British', 'Dutch', 'French', 'Ottomans'], 'Current four-classic civilization names changed')
    captures = []
    replay_identity = None
    roster_seen = False
    required_replay = {'watching', 'recording', 'segment', 'file', 'startStep', 'endStep', 'currentStep',
                       'seek', 'paused', 'speed', 'verified', 'error', 'incomplete', 'observerPlayer', 'identity'}
    for path in sorted((native / 'Captures').glob('Frontier-*.json')):
        capture = parse_json(path.read_bytes())
        need(capture.get('schemaVersion') == 4 and capture.get('eventSchemaVersion') == 2
             and capture.get('appVersion') == VERSION and capture.get('buildGuid') == guid
             and capture.get('unityVersion') == identity['unityVersion'], 'Native capture identity/schema mismatch: ' + path.name)
        png_name = capture.get('pngFile')
        need(isinstance(png_name, str) and PurePosixPath(png_name).name == png_name
             and png_name == path.with_suffix('.png').name, 'Capture PNG name mismatch')
        png = path.with_suffix('.png')
        need(png.is_file() and file_hash(png) == capture.get('pngSha256'), 'Capture PNG missing or SHA mismatch')
        with png.open('rb') as stream: png_head = stream.read(24)
        need(png_head[:8] == b'\x89PNG\r\n\x1a\n' and png_head[12:16] == b'IHDR'
             and struct.unpack('>II', png_head[16:24]) == (capture.get('width'), capture.get('height')),
             'Capture PNG dimensions mismatch')
        need(type(capture.get('requestFrame')) is int and type(capture.get('captureFrame')) is int
             and capture['captureFrame'] >= capture['requestFrame'], 'Capture request/frame ordering mismatch')
        replay = capture.get('replay', {})
        need(required_replay.issubset(replay), 'Capture lacks schema4 replay fields')
        current_identity = replay['identity']
        need(current_identity.get('appVersion') == VERSION and current_identity.get('buildIdentity') == guid
             and current_identity.get('unityVersion') == identity['unityVersion'], 'Capture replay build identity mismatch')
        if replay_identity is None: replay_identity = current_identity
        need(replay_identity == current_identity, 'Native replay identities changed across captures')
        players = capture.get('players', [])
        roster = [p['civilization'] for p in players]
        need(all(c in civs for c in roster), 'Capture has unknown civilization')
        roster_seen |= roster == required
        if players:
            need(0 <= capture['observerPlayer'] < len(players) and capture['observerPlayer'] == replay['observerPlayer'], 'Observer slot mismatch')
        captures.append({'file': path.name, 'jsonSha256': file_hash(path), 'png': png.name,
                         'pngSha256': capture['pngSha256'], 'matchId': capture.get('matchId'),
                         'worldTime': capture.get('worldTime'), 'roster': roster,
                         'watching': replay['watching'], 'observerPlayer': replay['observerPlayer'],
                         'currentStep': replay['currentStep'], 'endStep': replay['endStep'],
                         'verified': replay['verified'], 'seek': replay['seek'], 'error': replay['error']})
    need(captures and roster_seen, 'Require real schema4 PNG pairs with the actual four-classic roster')
    replays = [check_replay(p, replay_identity) for p in sorted((native / 'Replays').glob('*.replay.json'))]
    need(replays, 'Native replay sample is missing')
    need(all((native / 'Captures' / p.name.replace('.png', '.json')).exists()
             for p in (native / 'Captures').glob('Frontier-*.png')), 'Orphan F9 PNG')
    return {'acceptanceFile': 'native-acceptance.json', 'acceptanceSha256': file_hash(native / 'native-acceptance.json'),
            'acceptanceScope': 'Read the owner-written acceptance; this helper does not upgrade its gameplay claims',
            'capturePairs': captures, 'replays': replays, 'archiveReceipt': archive_receipt,
            'civilizations': [{'id': x, 'name': civs[x]} for x in required],
            'performanceBoundary': 'No frame/memory/export-latency acceptance is inferred from a successful ZIP/container check'}


def snapshot_binding(project, entries, commit):
    need(re.fullmatch(r'[0-9a-f]{40}', commit or ''), '--source-commit must be the frozen source snapshot commit')
    result = subprocess.run(['git', 'ls-tree', '-r', '-z', '--full-tree', commit], cwd=project, capture_output=True, check=True)
    tree = {}
    for item in result.stdout.split(b'\0'):
        if item:
            meta, name = item.split(b'\t', 1)
            mode, kind, blob = meta.split()
            tree[os.fsdecode(name)] = (mode, kind, blob.decode('ascii'))
    for entry in entries:
        path = entry['path']; name = path.relative_to(project).as_posix()
        need(name in tree and tree[name][1] == b'blob', 'Source ZIP file is absent from snapshot commit: ' + name)
        item = entry['record']; length = item['bytes']
        digest = hashlib.sha1(('blob ' + str(length) + '\0').encode('ascii'))
        if item['kind'] == 'symlink': digest.update(os.fsencode(os.readlink(path)))
        else:
            with path.open('rb') as stream:
                for block in iter(lambda: stream.read(1024 * 1024), b''): digest.update(block)
        need(digest.hexdigest() == tree[name][2], 'Source differs from snapshot commit: ' + name)
        need((tree[name][0] == b'120000') == (item['kind'] == 'symlink'), 'Git link type mismatch: ' + name)
    return commit


def current_entry(entry):
    return record(entry['path']) if 'path' in entry else entry['record']


def verify_zip(path, entries):
    expected = {entry['name']: entry for entry in entries}
    need(len(expected) == len(entries), 'Duplicate planned archive member')
    with zipfile.ZipFile(path) as archive:
        infos = archive.infolist()
        need(len({i.filename for i in infos}) == len(infos), 'Duplicate ZIP member')
        need({i.filename for i in infos} == set(expected), 'ZIP member set differs from frozen payload')
        for info in infos:
            safe_member(info.filename)
            need(not info.flag_bits & 1 and info.create_system == 3, 'Encrypted/non-Unix ZIP member')
            entry = expected[info.filename]; original = entry['record']
            need(current_entry(entry) == original, 'Input changed since inventory: ' + info.filename)
            need((info.external_attr >> 16) & 0xffff == original['mode'], 'ZIP Unix mode/type differs: ' + info.filename)
            need(info.file_size == original['bytes'], 'ZIP size differs: ' + info.filename)
            with archive.open(info) as stream: digest, length = hash_stream(stream)
            need(digest == original['sha256'] and length == original['bytes'], 'ZIP bytes/link target differ: ' + info.filename)
            if original['kind'] == 'symlink':
                # Link validation used its source boundary; these bytes must be identical.
                with archive.open(info) as stream: target = os.fsdecode(stream.read())
                need(not os.path.isabs(target), 'ZIP has absolute link target')
    return {'file': path.name, 'bytes': path.stat().st_size, 'sha256': file_hash(path), 'members': len(entries),
            'memberInventory': {name: entry['record'] for name, entry in sorted(expected.items())}}


def create_zip(path, entries):
    need(not path.exists() and not path.is_symlink(), 'Refusing to overwrite ZIP: ' + str(path))
    temporary = None
    try:
        fd, temporary_name = tempfile.mkstemp(prefix=path.name + '.', suffix='.partial', dir=path.parent)
        os.close(fd); temporary = Path(temporary_name)
        with zipfile.ZipFile(temporary, 'w', zipfile.ZIP_DEFLATED, compresslevel=6, allowZip64=True) as archive:
            for entry in entries:
                need(current_entry(entry) == entry['record'], 'Input changed before ZIP write')
                original = entry['record']; info = zipfile.ZipInfo(entry['name'], (2026, 10, 5, 0, 0, 0))
                info.create_system = 3; info.external_attr = original['mode'] << 16
                info.compress_type = zipfile.ZIP_DEFLATED
                if original['kind'] == 'directory': info.external_attr |= 0x10
                if 'data' in entry: archive.writestr(info, entry['data'])
                elif original['kind'] == 'symlink': archive.writestr(info, os.fsencode(os.readlink(entry['path'])))
                elif original['kind'] == 'directory': archive.writestr(info, b'')
                else:
                    with entry['path'].open('rb') as source, archive.open(info, 'w', force_zip64=True) as target:
                        shutil.copyfileobj(source, target, 1024 * 1024)
        verify_zip(temporary, entries)
        # Exclusive install, same filesystem; no overwrite even after a racing writer.
        os.link(temporary, path)
    finally:
        if temporary is not None and temporary.exists(): temporary.unlink()
    return verify_zip(path, entries)


def verify_source_tool(project, entries, output):
    with tempfile.TemporaryDirectory(prefix='source-allowlist-067-', dir=output) as tmp:
        archive_path = Path(tmp) / 'allowlisted.zip'
        subprocess.run([sys.executable, str(project / 'Tools/package-source.py'), '--evidence-version', RELEASE,
                        '--output', str(archive_path)], cwd=project, check=True, capture_output=True)
        receipt = parse_json(archive_path.with_suffix('.manifest.json').read_bytes())
        need(receipt.get('includedEvidenceVersion') == RELEASE, 'Source tool omitted evidence filter')
        with zipfile.ZipFile(archive_path) as archive:
            need({i.filename for i in archive.infolist()} == {e['name'] for e in entries}, 'Source helper allowlist differs')
            for entry in entries:
                with archive.open(entry['name']) as stream: digest, length = hash_stream(stream)
                # The upstream helper follows file links. Its allowlist is kept;
                # our final ZIP preserves the actual link's bytes and mode instead.
                if entry['record']['kind'] != 'symlink':
                    need((digest, length) == (entry['record']['sha256'], entry['record']['bytes']), 'Source helper member changed')
    return 'Tools/package-source.py --evidence-version 067 allowlist and regular bytes verified; final ZIP preserves links'


def plans(args):
    evidence, tests = verify_tests(args.project)
    receipt_path = args.native / 'client-build.json'
    receipt = parse_json(receipt_path.read_bytes())
    identity = app_identity(args.app, receipt, evidence)
    identity['tests'] = tests
    native = verify_native(args.native, identity, args.project)
    source = source_entries(args.project)
    if args.source_commit: identity['sourceSnapshotCommit'] = snapshot_binding(args.project, source, args.source_commit)
    guide = args.client_guide
    need(guide.is_file() and not guide.is_symlink(), 'Prepare an actual player guide')
    replay_guide = args.project / 'Docs/Replay-067.md'
    identity_data = json_bytes(dict(identity, scope='Frozen payload identity; ZIP/delivery hashes are outside all ZIPs'))
    readme = ('# Frontier Reckoning ' + VERSION + '\n\n'
              'macOS arm64 客户端。打开 Frontier Reckoning.app；玩家操作见 Docs/Player-Guide-067.md。\n\n'
              '本次原生范围见图集 Native/native-acceptance.json。录像需其原始相同 build GUID 与规则；'
              '导入独立 Replays 目录后从 RECORDING LIBRARY 打开，或使用 -frontierReplay 完整路径。\n\n'
              '长录像有较大解压和导出成本。本包不声称帧率、内存峰值、导出延迟或全局性能验收通过。'
              'Docs 内源码链接需配合 source.zip 阅读。开发构建身份见 payload-identity.json。\n').encode('utf-8')
    client_prefix = PREFIX
    client = tree_entries(args.app, client_prefix + '/' + args.app.name)
    client += tree_entries(args.project / 'Docs/Licenses', client_prefix + '/Docs/Licenses')
    client += [path_entry(client_prefix + '/Docs/Player-Guide-067.md', guide, args.project),
               path_entry(client_prefix + '/Docs/Replay-067.md', replay_guide, args.project),
               path_entry(client_prefix + '/client-build.json', receipt_path, args.native),
               memory_entry(client_prefix + '/payload-identity.json', identity_data),
               memory_entry(client_prefix + '/README-zh.md', readme)]
    gallery_prefix = PREFIX + '-gallery'
    gallery = tree_entries(args.native, gallery_prefix + '/Native')
    for entry in gallery:
        relative = PurePosixPath(entry['name']).parts[2:]
        need(not any(p.endswith('.app') or 'BackUpThisFolder' in p or p in {'Diagnostics', 'Saves', 'Preferences', 'Account'} for p in relative),
             'Native gallery contains a build/diagnostic/personal-storage directory')
        need(entry['record']['kind'] != 'symlink', 'Gallery evidence must be regular files/directories')
        if entry['record']['kind'] == 'file':
            path = entry['path']; need(path.suffix.lower() in {'.json', '.png', '.jpg', '.jpeg', '.md', '.log', '.py', '.gz', '.jsonl'}, 'Unexpected Native gallery payload')
    gallery += [path_entry(gallery_prefix + '/Docs/Player-Guide-067.md', guide, args.project),
                path_entry(gallery_prefix + '/Docs/Replay-067.md', replay_guide, args.project),
                memory_entry(gallery_prefix + '/payload-identity.json', identity_data),
                memory_entry(gallery_prefix + '/README-zh.md', readme)]
    return {'source': source, 'client': client, 'gallery': gallery}, {'identity': identity, 'native': native}


def self_test():
    with tempfile.TemporaryDirectory(prefix='pack-verify-067-fixture-') as tmp:
        root = Path(tmp); payload = root / 'payload'; payload.mkdir(mode=0o750)
        executable = payload / 'executable'; executable.write_bytes(b'fixture\0\xff\n'); executable.chmod(0o751)
        link = payload / 'relative-link'; link.symlink_to('executable')
        (payload / 'sub').mkdir(mode=0o700); (payload / 'sub/plain').write_bytes('中文'.encode())
        entries = tree_entries(payload, 'Fixture')
        archive = root / 'fixture.zip'; create_zip(archive, entries)
        cases = ['regular bytes', 'Unix executable and directory modes', 'relative symlink bytes', 'exact inventory']
        def rejects(action, name):
            try: action()
            except (ValueError, OSError, zipfile.BadZipFile): cases.append(name); return
            raise AssertionError('Fixture unexpectedly accepted: ' + name)
        rejects(lambda: create_zip(archive, entries), 'exclusive output')
        for name in ('../escape', '/absolute', 'C:/drive', 'Fixture/../escape', 'Fixture\\escape', 'Fixture//ambiguous'):
            rejects(lambda name=name: safe_member(name), 'unsafe name ' + name)
        bad_link = payload / 'escape'; bad_link.symlink_to('../outside')
        rejects(lambda: tree_entries(payload, 'Fixture'), 'escaping symlink'); bad_link.unlink()
        broken = root / 'changed.zip'
        with zipfile.ZipFile(archive) as source, zipfile.ZipFile(broken, 'w') as target:
            for info in source.infolist():
                data = source.read(info)
                if info.filename.endswith('/executable'): data += b'changed'
                target.writestr(info, data)
        rejects(lambda: verify_zip(broken, entries), 'changed member bytes')
        broken_mode = root / 'mode.zip'
        with zipfile.ZipFile(archive) as source, zipfile.ZipFile(broken_mode, 'w') as target:
            for info in source.infolist():
                if info.filename.endswith('/executable'): info.external_attr ^= 1 << 16
                target.writestr(info, source.read(info))
        rejects(lambda: verify_zip(broken_mode, entries), 'changed member mode')
        rejects(lambda: parse_json(b'{"a":1,"a":2}'), 'duplicate JSON key')
        rejects(lambda: parse_json(b'"\xff"'), 'invalid UTF8')
        rejects(lambda: parse_json(b'{"a":NaN}'), 'nonfinite JSON')
        replay_identity = {'appVersion': VERSION, 'buildIdentity': 'fixture-build', 'unityVersion': 'fixture-unity',
                           'rulesHash': '0' * 64, 'mapGenerationVersion': 1, 'guidedCatalogRevision': 1}
        document = {'schema': 1, 'segmentId': 'fixture', 'matchId': 'fixture-match', 'startKind': 'normal',
                    'finishReason': 'segment_end', 'identity': replay_identity, 'incomplete': False,
                    'incompleteReason': '', 'recordLimit': 200000, 'eventLimit': 1000000, 'anchorInterval': 300,
                    'anchors': [], 'records': [], 'events': []}
        raw = json_bytes(document)
        compressor = zlib.compressobj(level=6, wbits=31)
        compressed = compressor.compress(raw) + compressor.flush()
        envelope = {'schema': 2, 'payload': '', 'sha256': hashlib.sha256(raw).hexdigest(),
                    'encoding': 'gzip-base64', 'payloadBase64': base64.b64encode(compressed).decode('ascii')}
        replay = root / 'replay-fixture.replay.json'; replay.write_bytes(json_bytes(envelope))
        need(check_replay(replay, replay_identity)['rawBytes'] == len(raw), 'Fixture replay container failed')
        cases.append('replay gzip/UTF8/CRC/ISIZE/raw SHA/header')
        wrong_identity = dict(replay_identity, buildIdentity='other-build')
        rejects(lambda: check_replay(replay, wrong_identity), 'wrong replay build identity')
        for bad, name in [(compressed[:-1], 'truncated replay gzip'),
                          (compressed[:-8] + bytes([compressed[-8] ^ 1]) + compressed[-7:], 'wrong replay CRC'),
                          (compressed + compressed, 'concatenated replay gzip')]:
            changed = dict(envelope, payloadBase64=base64.b64encode(bad).decode('ascii'))
            replay.write_bytes(json_bytes(changed))
            rejects(lambda: check_replay(replay, replay_identity), name)
        replay.write_bytes(json_bytes(envelope))
        alias = root / 'manual-export-fixture.replay.json'; alias.write_bytes(replay.read_bytes())
        need(check_replay(alias, replay_identity)['rawSha256'] == envelope['sha256'], 'Manual export alias fixture failed')
        cases.append('exact manual-export segment alias')
        arbitrary = root / 'arbitrary.replay.json'; arbitrary.write_bytes(replay.read_bytes())
        rejects(lambda: check_replay(arbitrary, replay_identity), 'arbitrary replay filename')
        wrong_segment = root / 'manual-export-other-segment.replay.json'; wrong_segment.write_bytes(replay.read_bytes())
        rejects(lambda: check_replay(wrong_segment, replay_identity), 'manual export wrong segment filename')
        native = root / 'Native'; native.mkdir()
        sample = native / 'sample.json'; sample.write_bytes(b'{"fixture":true}\n'); sample.chmod(0o644)
        sample_hash = file_hash(sample)
        item = {'file': sample.name, 'bytes': sample.stat().st_size, 'sha256': sample_hash, 'mode': '0644',
                'originalBytes': sample.stat().st_size, 'originalSha256': sample_hash, 'transform': 'copy'}
        identity = {'buildGuid': 'fixture', 'appVersion': VERSION, 'runtimeCommit': '0' * 40,
                    'sourceFingerprint': '0' * 64, 'sourceFileCount': 1}
        raw_events = b'{"event":1}\n{"event":2}\n'
        events = native / 'fixture-events.jsonl.gz'
        with events.open('wb') as target:
            with gzip.GzipFile(filename='', mode='wb', fileobj=target, compresslevel=9, mtime=0) as compressed:
                compressed.write(raw_events)
        events.chmod(0o644)
        raw_hash = hashlib.sha256(raw_events).hexdigest()
        gzip_item = {'file': events.name, 'bytes': events.stat().st_size, 'sha256': file_hash(events), 'mode': '0644',
                     'originalBytes': len(raw_events), 'originalSha256': raw_hash, 'transform': 'gzip',
                     'decompressedBytes': len(raw_events), 'decompressedSha256': raw_hash,
                     'decompressionVerified': True, 'eventLines': 2}
        inventory = [item, gzip_item]
        inventory_hash = hashlib.sha256((json.dumps(inventory, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode()).hexdigest()
        receipt = {**identity, 'files': inventory, 'inventorySha256': inventory_hash}
        (native / 'archive-receipt.json').write_bytes(json_bytes(receipt))
        need(verify_archive_receipt(native, identity)['members'] == 2, 'Fixture archive receipt failed')
        cases.append('Native archive receipt inventory/bytes/mode')
        cases.append('Native gzip deterministic header/raw SHA/bytes/lines')
        gzip_item['originalSha256'] = 'f' * 64
        receipt['inventorySha256'] = hashlib.sha256((json.dumps(inventory, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode()).hexdigest()
        (native / 'archive-receipt.json').write_bytes(json_bytes(receipt))
        rejects(lambda: verify_archive_receipt(native, identity), 'wrong Native gzip raw lineage')
        gzip_item['originalSha256'] = raw_hash
        receipt['inventorySha256'] = 'f' * 64
        (native / 'archive-receipt.json').write_bytes(json_bytes(receipt))
        rejects(lambda: verify_archive_receipt(native, identity), 'wrong Native inventory digest')
        return {'status': 'PASS', 'scope': 'temporary delivery-helper fixtures; no Unity/native/runtime claims', 'checks': cases}


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--project', type=Path, default=DEFAULT_PROJECT)
    parser.add_argument('--output', type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument('--native', type=Path, help='Frozen archive; default PROJECT/Docs/Evidence/067/Native')
    parser.add_argument('--app', type=Path, help='Final bundle; default OUTPUT/Client/Frontier Reckoning.app')
    parser.add_argument('--client-guide', type=Path, help='Actual player guide; default PROJECT/Docs/Player-Guide-067.md')
    parser.add_argument('--source-commit', help='Commit containing every source allowlist file; checked via Git blob SHA, required for execute/verify')
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument('--execute', action='store_true', help='Create exclusive source/client/gallery ZIPs and outside manifest')
    mode.add_argument('--verify-existing', action='store_true', help='Verify existing final ZIPs; no writes')
    mode.add_argument('--self-test', action='store_true', help='Temporary ZIP fixtures only; no candidate packaging')
    args = parser.parse_args()
    if args.self_test:
        print(json.dumps(self_test(), ensure_ascii=False, indent=2)); return 0
    args.project = args.project.resolve(); args.output = args.output.resolve()
    args.native = (args.native or args.project / 'Docs/Evidence/067/Native').resolve()
    args.app = (args.app or args.output / 'Client/Frontier Reckoning.app').resolve()
    args.client_guide = (args.client_guide or args.project / 'Docs/Player-Guide-067.md').resolve()
    need(not args.output.is_relative_to(args.project), 'ZIP output must be outside source allowlist')
    plan, proof = plans(args)
    if args.execute or args.verify_existing:
        need(args.source_commit, 'Final packaging/verification requires --source-commit')
    result = {'schema': 1, 'version': VERSION, 'evidenceRelease': RELEASE, **proof,
              'helperSha256': file_hash(Path(__file__)), 'createdAtUtc': datetime.now(timezone.utc).isoformat(),
              'deliveryBoundary': 'No upload/remote SHA/download equality, signing, notarization or general performance claim', 'packages': {}}
    manifest_path = args.output / 'release-manifest.json'
    if args.execute:
        need(not manifest_path.exists(), 'Refusing to overwrite outer release manifest')
        for kind in plan: need(not (args.output / NAMES[kind]).exists(), 'Final ZIP already exists')
        args.output.mkdir(parents=True, exist_ok=True)
        result['sourceAllowlistCheck'] = verify_source_tool(args.project, plan['source'], args.output)
        for kind, entries in plan.items(): result['packages'][kind] = create_zip(args.output / NAMES[kind], entries)
        # Each final archive was verified before this outer hash inventory exists.
        with manifest_path.open('xb') as stream: stream.write(json_bytes(result))
        print(json.dumps({'status': 'PASS', 'manifest': str(manifest_path), 'packages':
                          {k: {a: b for a, b in v.items() if a != 'memberInventory'} for k, v in result['packages'].items()}}, indent=2))
    elif args.verify_existing:
        existing = parse_json(manifest_path.read_bytes())
        need(existing.get('identity') == result['identity'], 'Outer manifest identity differs')
        for kind, entries in plan.items():
            current = verify_zip(args.output / NAMES[kind], entries)
            need(existing.get('packages', {}).get(kind) == current, 'Outer package inventory differs: ' + kind)
        need(existing.get('native') == result['native'], 'Native observations changed since packaging')
        print(json.dumps({'status': 'PASS', 'checks': ['source/tests/build identity', 'Native capture pairs/replay container',
                                                     'every ZIP member bytes/modes/links', 'outside manifest hashes']}, indent=2))
    else:
        print(json.dumps({'status': 'PREFLIGHT PASS; no files created', **proof,
                          'plans': {k: {'file': NAMES[k], 'members': len(v), 'payloadBytes': sum(e['record']['bytes'] for e in v)} for k, v in plan.items()}}, ensure_ascii=False, indent=2))
    return 0


if __name__ == '__main__':
    try: sys.exit(main())
    except (OSError, ValueError, KeyError, TypeError, ET.ParseError, zipfile.BadZipFile, zlib.error, subprocess.CalledProcessError) as error:
        sys.exit('Packaging/verification failed: ' + str(error))
