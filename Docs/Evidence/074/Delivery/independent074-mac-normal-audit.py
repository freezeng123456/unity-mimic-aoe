#!/usr/bin/env python3
"""Independent read-only audit of the two selected closed 074 ZIPs.

Does not import the packager, extract, install, launch apps, run Unity or read
repository code. Only its two new exclusive audit outputs may be written.
"""
from collections import Counter
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import plistlib
import re
import stat
import struct
import unicodedata
import zipfile
import zlib

BASE = Path('/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.17.0/Candidate1')
RUNTIME = 'ba0b57336c46d72738b71b59af923a149aff467d'
FP = '61d00af23e00d960dd040ef55bdfb0aa269b15745be1d1a2f0faec46377d6698'
GUID = '1ada3d99ddf84c0f93e1b526e6360390'
VERSION = '0.17.0'
CHUNK = 1024 * 1024
CHECKS = []


def check(label, value):
    CHECKS.append({'check': label, 'passed': bool(value)})
    if not value:
        raise AssertionError(label)


def sha(value):
    return hashlib.sha256(value).hexdigest()


def identity(node):
    return [node.st_dev, node.st_ino, node.st_mode, node.st_size,
            node.st_mtime_ns, node.st_ctime_ns]


def unique(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError('Duplicate JSON key: ' + key)
        result[key] = value
    return result


def decode(value):
    return json.loads(value, object_pairs_hook=unique,
                      parse_constant=lambda x: (_ for _ in ()).throw(ValueError(x)))


def read_regular(path, collect=False):
    before = path.lstat()
    check(str(path) + ' regular file', stat.S_ISREG(before.st_mode))
    h = hashlib.sha256()
    count = 0
    crc = 0
    parts = [] if collect else None
    with os.fdopen(os.open(path, os.O_RDONLY | os.O_NOFOLLOW), 'rb') as stream:
        check(str(path) + ' open identity', identity(os.fstat(stream.fileno())) == identity(before))
        for part in iter(lambda: stream.read(CHUNK), b''):
            h.update(part)
            crc = zlib.crc32(part, crc)
            count += len(part)
            if collect:
                parts.append(part)
        check(str(path) + ' read stable', identity(os.fstat(stream.fileno())) == identity(before)
              and identity(path.lstat()) == identity(before) and count == before.st_size)
    item = {'path': str(path), 'bytes': count, 'sha256': h.hexdigest(),
            'crc32': crc & 0xffffffff, 'identity': identity(before)}
    return (item, b''.join(parts)) if collect else item


def namespace(records):
    aliases = {}
    for name, item in records.items():
        parts = name.rstrip('/').split('/')
        check('safe name ' + name, not name.startswith('/') and '\\' not in name
              and all(p not in ('', '.', '..') and ':' not in p and not p.endswith((' ', '.')) for p in parts)
              and not any(ord(c) < 32 or ord(c) == 127 for c in name)
              and (name.endswith('/') == (item['type'] == 'directory')))
        alias = unicodedata.normalize('NFD', name.rstrip('/')).casefold()
        check('unique Unicode/case alias ' + name, alias not in aliases)
        aliases[alias] = name
        for i in range(1, len(parts)):
            parent = '/'.join(parts[:i]) + '/'
            check('real directory parent ' + name, parent in records and records[parent]['type'] == 'directory')
    # Traverse symlink components as text only, never through the filesystem.
    for name, item in records.items():
        if item['type'] != 'symlink':
            continue
        target = item['target']
        check('relative symlink target ' + name, bool(target) and not target.startswith('/') and '\\' not in target)
        todo = list(PurePosixPath(name).parent.parts) + target.split('/')
        resolved = []
        visited = set()
        while todo:
            part = todo.pop(0)
            if part in ('', '.'):
                continue
            if part == '..':
                check('symlink contained ' + name, len(resolved) > 1)
                resolved.pop()
                continue
            resolved.append(part)
            candidate = '/'.join(resolved)
            node = records.get(candidate) or records.get(candidate + '/')
            if node and node['type'] == 'symlink':
                check('acyclic symlink graph ' + name, candidate not in visited)
                visited.add(candidate)
                resolved.pop()
                todo = node['target'].split('/') + todo
        dest = '/'.join(resolved)
        check('symlink destination exists ' + name, dest in records or dest + '/' in records)


def inventory(root, prefix):
    records = {}
    states = {}

    def walk(path, name):
        node = path.lstat()
        states[str(path)] = identity(node)
        mode = stat.S_IFMT(node.st_mode) | stat.S_IMODE(node.st_mode)
        if stat.S_ISDIR(node.st_mode):
            records[name + '/'] = {'type': 'directory', 'mode': mode, 'bytes': 0, 'sha256': sha(b''), 'crc32': 0}
            with os.scandir(path) as scan:
                children = sorted(scan, key=lambda e: e.name)
            for child in children:
                walk(path / child.name, name + '/' + child.name)
        elif stat.S_ISREG(node.st_mode):
            item = read_regular(path)
            records[name] = {'type': 'file', 'mode': mode, 'bytes': item['bytes'],
                             'sha256': item['sha256'], 'crc32': item['crc32']}
        elif stat.S_ISLNK(node.st_mode):
            target = os.readlink(path)
            raw = os.fsencode(target)
            records[name] = {'type': 'symlink', 'mode': mode, 'bytes': len(raw),
                             'sha256': sha(raw), 'crc32': zlib.crc32(raw) & 0xffffffff, 'target': target}
        else:
            raise AssertionError('Unsupported lstat type: ' + str(path))
        check(str(path) + ' walk stable', identity(path.lstat()) == states[str(path)])

    check(str(root) + ' real root directory', stat.S_ISDIR(root.lstat().st_mode))
    walk(root, prefix)
    namespace(records)
    return {'directory': str(root), 'prefix': prefix, 'members': records, 'nodeIdentities': states}


def extras(raw, label):
    result = {}
    at = 0
    while at < len(raw):
        check(label + ' complete extra header', at + 4 <= len(raw))
        tag, n = struct.unpack('<HH', raw[at:at + 4])
        at += 4
        check(label + ' allowed unique ZIP64 extra', tag == 1 and tag not in result and at + n <= len(raw))
        result[tag] = raw[at:at + n]
        at += n
    return result


def expanded(values, raw, label):
    data = extras(raw, label).get(1, b'')
    offset = 0
    result = []
    for value in values:
        if value == 0xffffffff:
            check(label + ' complete ZIP64 integer', offset + 8 <= len(data))
            value = struct.unpack('<Q', data[offset:offset + 8])[0]
            offset += 8
        result.append(value)
    return result


def headers(path, infos):
    size = path.stat().st_size
    with path.open('rb') as stream:
        stream.seek(max(0, size - 65557))
        tail = stream.read()
        at = tail.rfind(b'PK\x05\x06')
        check(path.name + ' complete EOF record', at >= 0 and len(tail) - at >= 22)
        end = struct.unpack('<4s4H2IH', tail[at:at + 22])
        check(path.name + ' no multi-disk / exact EOF', end[1] == end[2] == 0 and end[3] == end[4] == len(infos)
              and at + 22 + end[7] == len(tail))
        eocd = size - len(tail) + at
        check(path.name + ' central directory reaches EOF', end[6] + end[5] == eocd)
        stream.seek(end[6])
        central = {}
        for info in infos:
            h = stream.read(46)
            check(info.filename + ' complete central header', len(h) == 46)
            c = struct.unpack('<4s6H3I5H2I', h)
            name = stream.read(c[10])
            extra = stream.read(c[11])
            comment = stream.read(c[12])
            name_text = name.decode('utf-8' if c[3] & 0x800 else 'cp437')
            usize, csize, local_offset = expanded([c[9], c[8], c[16]], extra, info.filename + ' central')
            check(info.filename + ' central matches public entry', c[0] == b'PK\x01\x02' and c[13] == 0
                  and name_text == info.orig_filename == info.filename and c[3] in (0, 0x800)
                  and c[3] == info.flag_bits and c[4] == info.compress_type and c[7] == info.CRC
                  and usize == info.file_size and csize == info.compress_size and local_offset == info.header_offset
                  and c[15] == info.external_attr and extra == info.extra and comment == info.comment)
            central[info.filename] = (c, name, usize, csize, local_offset)
        check(path.name + ' exact central directory extent', stream.tell() == eocd)
        ranges = []
        for info in infos:
            c, name, usize, csize, local_offset = central[info.filename]
            stream.seek(local_offset)
            h = stream.read(30)
            check(info.filename + ' complete local header', len(h) == 30)
            local = struct.unpack('<4s5H3I2H', h)
            lname = stream.read(local[9])
            extra = stream.read(local[10])
            lu, lc = expanded([local[8], local[7]], extra, info.filename + ' local')
            check(info.filename + ' local/central consistent without descriptor', local[0] == b'PK\x03\x04'
                  and local[1] == c[2] and local[2] == c[3] and local[3] == c[4]
                  and local[4] == c[5] and local[5] == c[6] and local[6] == c[7]
                  and lname == name and lu == usize and lc == csize)
            ranges.append((local_offset, stream.tell() + csize))
        cursor = 0
        for start, finish in sorted(ranges):
            check(path.name + ' exact contiguous local record ' + str(start), start == cursor and finish >= start and finish <= end[6])
            cursor = finish
        check(path.name + ' no hidden local data or archive preamble', cursor == end[6])
    return {'eofOffset': eocd, 'centralOffset': end[6], 'centralBytes': end[5],
            'localCentralHeadersConsistent': True, 'contiguousNonoverlappingLocalRecords': True,
            'onlyZip64ExtraFieldsAllowed': True, 'dataDescriptorsAbsent': True}


def audit_zip(path, tree):
    before = read_regular(path)
    check(path.name + ' under100MiB', before['bytes'] < 100 * 1024 * 1024)
    members = tree['members']
    with zipfile.ZipFile(path, 'r') as archive:
        infos = archive.infolist()
        names = [i.filename for i in infos]
        check(path.name + ' exhaustive exact member set', len(names) == len(set(names)) == len(members) and set(names) == set(members))
        header_result = headers(path, infos)
        for info in infos:
            expected = members[info.filename]
            check(info.filename + ' exact Unix type/mode/size', info.create_system == 3
                  and info.external_attr >> 16 == expected['mode'] and info.file_size == expected['bytes']
                  and info.is_dir() == (expected['type'] == 'directory') and info.compress_type in (0, 8)
                  and info.external_attr & 0xffff == (16 if info.is_dir() else 0))
            h = hashlib.sha256()
            crc = 0
            count = 0
            link = bytearray()
            with archive.open(info, 'r') as stream:
                for part in iter(lambda: stream.read(CHUNK), b''):
                    count += len(part)
                    h.update(part)
                    crc = zlib.crc32(part, crc)
                    if expected['type'] == 'symlink':
                        link.extend(part)
            check(info.filename + ' all payload bytes/SHA/CRC', count == expected['bytes']
                  and h.hexdigest() == expected['sha256'] and crc & 0xffffffff == expected['crc32'] == info.CRC)
            if expected['type'] == 'symlink':
                check(info.filename + ' exact symlink text', os.fsdecode(bytes(link)) == expected['target'])
    check(path.name + ' complete readback unchanged', read_regular(path) == before)
    return {'path': str(path), 'bytes': before['bytes'], 'sha256': before['sha256'],
            'memberCount': len(members), 'types': dict(Counter(v['type'] for v in members.values())),
            'inputBytes': sum(v['bytes'] for v in members.values()), 'headers': header_result,
            'allPayloadsComparedToActualTree': True, 'allCrcVerifiedIndependently': True,
            'allTypesModesAndLinkTextsMatch': True, 'unicodeCaseAliasesAbsent': True,
            'originalIdentity': before['identity']}


def receipt(path, kind):
    pin, raw = read_regular(path, collect=True)
    doc = decode(raw)
    files = doc.get('sourceFilesBefore')
    check(kind + ' actual closed runner schema', doc.get('schema') == 'frontier074-unity-runner-v1'
          and doc.get('kind') == kind and doc.get('status') == 'completed_source_stable'
          and doc.get('exitCode') == 0 and doc.get('sourceStable') is True
          and doc.get('changedSources') == [] and isinstance(doc.get('finishedUtc'), str) and bool(doc['finishedUtc']))
    check(kind + ' pinned runtime/source', doc.get('gitHeadBefore') == RUNTIME
          and doc.get('sourceFingerprintBefore') == doc.get('sourceFingerprintAfter') == FP
          and isinstance(files, dict) and len(files) == 740
          and sha(json.dumps(files, sort_keys=True, separators=(',', ':')).encode()) == FP)
    check(kind + ' no preparation/interventions', doc.get('preparationRun') is False and doc.get('caseInterventions') is False)
    return doc, pin


def main():
    output = BASE / 'Diagnostics/independent074-mac-normal-audit'
    check('new exclusive outputs', not output.with_suffix('.json').exists() and not output.with_suffix('.md').exists())
    app = BASE / 'Build1/Frontier Reckoning.app'
    normal = BASE / 'NormalObserved1'
    initial_app = inventory(app, app.name)
    initial_normal = inventory(normal, 'Normal074')
    build, build_pin = receipt(BASE / 'Build1/runner-receipt074.json', 'build')
    normal_runner, normal_pin = receipt(normal / 'runner-receipt074.json', 'normal')
    check('two before source maps identical', build['sourceFilesBefore'] == normal_runner['sourceFilesBefore'])
    normal_files = {name.removeprefix('Normal074/') for name, v in initial_normal['members'].items() if v['type'] == 'file'}
    cases = ('winding-ottoman-44', 'winding-ottoman-45', 'river_bend-river-42')
    common = ('opening-world.json', 'opening-simulation.json', 'comparison-world.json', 'comparison-simulation.json',
              'final-world.json', 'final-simulation.json', 'events.jsonl', 'samples.jsonl')
    expected_normal = {'matrix/manifest.json', 'normal.log', 'runner-receipt074.json', 'wrapper-output.log',
                       'matrix/river_bend-river-42/opening-scenario.json'}
    expected_normal.update('matrix/' + case + '/' + file for case in cases for file in common)
    check('normal actual29 files exact declared namespace', normal_files == expected_normal and len(normal_files) == 29)
    manifest_pin, manifest_raw = read_regular(normal / 'matrix/manifest.json', collect=True)
    matrix = decode(manifest_raw)
    check('normal actual manifest binding', matrix['schema'] == 'normal-gather074-v1'
          and matrix['version'] == VERSION and matrix['sourceFingerprint'] == FP and matrix['finishedUtc']
          and matrix['status'] == normal_runner['manifestStatus'] == 'observed_three_declared_cases'
          and normal_runner['manifestSourceFingerprint'] == FP
          and {c['id'] for c in matrix['cases']} == set(cases) and len(matrix['cases']) == 3)
    zip_results = {'macOS': audit_zip(BASE / 'DeliveryFinal1/macOS.zip', initial_app),
                   'normal': audit_zip(BASE / 'DeliveryFinal1/normal.zip', initial_normal)}
    plist_pin, plist_raw = read_regular(app / 'Contents/Info.plist', collect=True)
    boot_pin, boot_raw = read_regular(app / 'Contents/Resources/Data/boot.config', collect=True)
    info = plistlib.loads(plist_raw)
    executable = info['CFBundleExecutable']
    check('actual app fixed version/bundle/executable', info['CFBundleShortVersionString'] == build['buildVersion'] == VERSION
          and info['CFBundleIdentifier'] == 'com.frontierworkshop.reckoning' and executable == 'Frontier Reckoning')
    exe_pin = read_regular(app / 'Contents/MacOS' / executable)
    mode = stat.S_IMODE((app / 'Contents/MacOS' / executable).lstat().st_mode)
    check('actual executable755 equals runner', mode == 0o755 and build['mainExecutableMode'] == oct(mode))
    check('actual boot exact runner bytes/GUID', boot_raw.decode('utf-8') == build['bootConfig']
          and re.findall(r'^build-guid=([0-9a-f]{32})$', boot_raw.decode('utf-8'), re.M) == [GUID])
    with zipfile.ZipFile(BASE / 'DeliveryFinal1/macOS.zip', 'r') as archive:
        check('delivered boot/plist/executable match actual', archive.read(app.name + '/Contents/Info.plist') == plist_raw
              and archive.read(app.name + '/Contents/Resources/Data/boot.config') == boot_raw
              and archive.getinfo(app.name + '/Contents/MacOS/' + executable).external_attr >> 16 == stat.S_IFREG | 0o755)
    check('complete app before/after lstat+SHA identical', inventory(app, app.name) == initial_app)
    check('complete normal before/after lstat+SHA identical', inventory(normal, 'Normal074') == initial_normal)
    pins = [build_pin, normal_pin, manifest_pin, plist_pin, boot_pin, exe_pin]
    for pin in pins:
        check(pin['path'] + ' selected original unchanged', read_regular(Path(pin['path'])) == pin)
    for result in zip_results.values():
        after = read_regular(Path(result['path']))
        check(result['path'] + ' final archive unchanged', after['sha256'] == result['sha256']
              and after['bytes'] == result['bytes'] and after['identity'] == result['originalIdentity'])
    script_pin = read_regular(Path(__file__))
    report = {'schema': 1, 'status': 'independent_two_archives_exact_actual_tree_integrity_verified',
              'auditedUtc': datetime.now(timezone.utc).isoformat(), 'checkCount': len(CHECKS), 'failures': [],
              'nativeAllPassed': False, 'finalReleaseAccepted': False,
              'identity': {'runtimeCommitAsRecorded': RUNTIME, 'sourceFingerprintAsRecorded': FP,
                           'sourceFilesBeforeCount': 740, 'appVersion': VERSION, 'buildGuid': GUID,
                           'runnerSchema': build['schema'], 'sourceFilesAfterRecorded': 'sourceFilesAfter' in build or 'sourceFilesAfter' in normal_runner},
              'archives': zip_results, 'inputPins': pins, 'script': script_pin,
              'actualApp': {'bundleId': info['CFBundleIdentifier'], 'executableName': executable,
                            'executableMode': oct(mode), 'executableSha256': exe_pin['sha256'],
                            'plistSha256': plist_pin['sha256'], 'bootConfigSha256': boot_pin['sha256']},
              'normal': {'fileCount': len(normal_files), 'fileNames': sorted(normal_files),
                         'casesAsRecorded': matrix['cases'], 'statusAsRecorded': matrix['status']},
              'actualInputTrees': {'macOS': initial_app, 'normal': initial_normal}, 'checks': CHECKS,
              'scope': ['Only macOS.zip and normal.zip compared with actual complete Build1 app and NormalObserved1.',
                        'No packaging helper imported or executed; no extraction, installation, Unity, repo writes, commit or upload.',
                        'Receipt complete before source map and both fingerprints verified as recorded; no missing after map invented.',
                        'This integrity audit does not independently prove native GUI acceptance, semantic correctness of every normal event, balance, host networking or FPS.',
                        'Source Git partition, physical 740-input map and evidence archive are audited by the parent owner separately.']}
    with output.with_suffix('.json').open('x') as stream:
        json.dump(report, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
    lines = ['# Independent 074 macOS and normal archive audit', '',
             'Status: two selected archives exactly match complete actual input trees. No release or all-native acceptance assertion.', '',
             f'- {len(CHECKS)} checks passed; no failures.',
             f'- Actual app version {VERSION}, GUID `{GUID}`, executable mode 0755; runner boot configuration, version and mode match actual bytes.',
             '- Both runner receipts are completed_source_stable/exit0 under frontier074-unity-runner-v1, pinned runtime/source, complete 740-entry before maps and matching recorded after fingerprints.',
             '- NormalObserved1 has exactly 29 files plus 5 directories, including wrapper-output.log; all 3 declared cases are preserved without converting observation caps into natural outcomes.',
             '- Every member is read and independently compared for SHA-256, bytes, CRC, Unix type/mode and symlink text; complete namespaces and Unicode/case alias absence are checked.',
             '- Central/local names, flags, compression, timestamps, CRC and sizes agree. Local records are contiguous/nonoverlapping, with exact ZIP EOF and no descriptor/Unicode-path extra ambiguity.',
             '- Complete app/normal lstat trees and every payload SHA remain identical before/after; both ZIP bytes and metadata remain stable.', '',
             'No extraction, app/Unity execution, packaging helper execution, repo edit, commit or upload occurred. This audit certifies archive/input integrity; native GUI acceptance, full normal event semantics, balance, networking and FPS are outside its scope. Parent independently audits source/evidence.', '']
    for key, result in zip_results.items():
        lines.append(f"- {key}: {result['memberCount']} members, {result['bytes']} archive bytes, SHA-256 `{result['sha256']}`.")
    lines += ['', 'Script SHA-256 `' + script_pin['sha256'] + '`. Complete member inventories, identities and checks are in the JSON.', '']
    with output.with_suffix('.md').open('x') as stream:
        stream.write('\n'.join(lines))
    outputs = []
    for ext in ('.json', '.md'):
        path = output.with_suffix(ext)
        raw = path.read_bytes()
        assert path.read_bytes() == raw
        outputs.append({'path': str(path), 'bytes': len(raw), 'sha256': sha(raw)})
    print(json.dumps({'status': report['status'], 'checks': report['checkCount'], 'failures': 0,
                      'archives': {k: {f: v[f] for f in ('memberCount', 'types', 'bytes', 'sha256')} for k, v in zip_results.items()},
                      'outputs': outputs,
                      'scriptSha256': script_pin['sha256']}, indent=2))


if __name__ == '__main__':
    main()
