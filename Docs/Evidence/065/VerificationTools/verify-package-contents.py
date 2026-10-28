#!/usr/bin/env python3
"""Read-only verification of the separately created Frontier 0.8.0 packages.

Writes only a new JSON report and a temporary extracted client under the OS
temporary directory. Does not build, package, edit a project, or launch Unity.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import plistlib
import re
import shutil
import stat
import struct
import zlib
import subprocess
import tempfile
from xml.etree import ElementTree as ET
import zipfile

VERSION = '0.8.0'
ALLOWED = ('Assets', 'Packages', 'ProjectSettings', 'Docs', 'SourceAssets',
           'Tools', 'README.md', '.gitignore')
RUNTIME_ROOTS = ('Assets', 'Packages', 'ProjectSettings', 'Tools')
SOURCE_PREFIX = 'FrontierReckoning/'
EVIDENCE_VERSION = '065'
MAP_IDS = {'frontier', 'river', 'winding', 'tropical', 'tributaries', 'estuary',
           'generated_river', 'generated_delta', 'generated_strait'}


def digest(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def hash_bytes(value):
    return hashlib.sha256(value).hexdigest()


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError('Duplicate JSON key: ' + key)
        result[key] = value
    return result


def reject_constant(value):
    raise ValueError('Non-finite JSON value: ' + value)


def parse_json(value):
    return json.loads(value, object_pairs_hook=unique_object,
                      parse_constant=reject_constant)


def check(section, label, condition, detail=None):
    entry = {'passed': bool(condition)}
    if detail is not None:
        entry['detail'] = detail
    section.setdefault('checks', {})[label] = entry
    if not condition:
        raise ValueError(label + (': ' + str(detail) if detail is not None else ''))


def ignored(name):
    parts = PurePosixPath(name).parts
    return '__MACOSX' in parts or '.DS_Store' in parts


def safe_member(name):
    p = PurePosixPath(name)
    return bool(name and p.parts and not p.is_absolute() and '..' not in p.parts
                and '\\' not in name and '\x00' not in name
                and not re.match(r'^[A-Za-z]:', name)
                and str(p) == name.rstrip('/'))


def record(path):
    mode = path.lstat().st_mode
    value = {'mode': stat.S_IMODE(mode)}
    if stat.S_ISLNK(mode):
        target = os.readlink(os.fsencode(path))
        value.update(kind='symlink', bytes=len(target), sha256=hash_bytes(target),
                     target=os.fsdecode(target))
    elif stat.S_ISREG(mode):
        value.update(kind='file', bytes=path.stat().st_size, sha256=digest(path))
    elif stat.S_ISDIR(mode):
        value.update(kind='directory')
    else:
        raise ValueError('Unsupported filesystem entry: ' + str(path))
    return value


def tree_inventory(base):
    values = {'': record(base)}
    for current, dirs, files in os.walk(base, followlinks=False):
        dirs[:] = sorted(d for d in dirs if not ignored(d))
        for name in dirs + sorted(files):
            p = Path(current) / name
            rel = p.relative_to(base).as_posix()
            if not ignored(rel):
                values[rel] = record(p)
    return values


def archive_inventory(path, section):
    check(section, 'archiveExists', path.is_file(), str(path))
    section['archive'] = {'file': path.name, 'bytes': path.stat().st_size,
                          'sha256': digest(path)}
    values = {}
    with zipfile.ZipFile(path) as archive:
        entries = archive.infolist()
        check(section, 'uniqueMembers', len(entries) == len({x.filename for x in entries}))
        check(section, 'safeMemberPaths', all(safe_member(x.filename) for x in entries))
        bad = archive.testzip()
        check(section, 'crcPassed', bad is None, bad)
        for item in entries:
            mode = item.external_attr >> 16
            kind = ('directory' if item.is_dir() else
                    'symlink' if stat.S_ISLNK(mode) else 'file')
            value = {'kind': kind, 'mode': stat.S_IMODE(mode)}
            if kind != 'directory':
                data = archive.read(item)
                value.update(bytes=len(data), sha256=hash_bytes(data))
                if kind == 'symlink':
                    value['target'] = os.fsdecode(data)
            values[item.filename] = value
    section['members'] = values
    section['memberCount'] = len(values)
    section['ignoredMembers'] = [n for n in values if ignored(n)]
    section['archiveIdentity'] = dict(section['archive'])
    return values


def packaged_files(project):
    files = {}
    for name in ALLOWED:
        entry = project / name
        candidates = [entry] if entry.is_file() else entry.rglob('*') if entry.is_dir() else []
        for p in candidates:
            if p.is_symlink():
                raise ValueError('Source allowlist contains a symlink: ' + str(p))
            if not p.is_file() or (p != entry and (p.name.startswith('.') or '__pycache__' in p.parts)):
                continue
            rel = p.relative_to(project)
            if rel.parts[:2] == ('Docs', 'Evidence') and rel.parts[2] != '065':
                continue
            files[rel.as_posix()] = record(p)
    return files


def runtime_files(project):
    result = {}
    for root in RUNTIME_ROOTS:
        for p in (project / root).rglob('*'):
            rel = p.relative_to(project)
            if p.is_file() and not any(v.startswith('.') or v == '__pycache__' for v in rel.parts):
                if p.is_symlink():
                    raise ValueError('Runtime source contains a symlink: ' + str(p))
                result[rel.as_posix()] = digest(p)
    return result


def verify_tests(args, section):
    receipt_path = args.testdir / 'source-evidence.json'
    evidence = parse_json(receipt_path.read_bytes())
    check(section, 'schemaOne', isinstance(evidence, dict) and evidence.get('schema') == 1)
    files = evidence['files']
    check(section, 'validRuntimeManifest', isinstance(files, dict) and bool(files)
          and all(isinstance(n, str) and safe_member(n) and isinstance(h, str)
                  and bool(re.fullmatch(r'[0-9a-f]{64}', h)) for n, h in files.items()))
    expected = hash_bytes(json.dumps(files, sort_keys=True, separators=(',', ':')).encode())
    check(section, 'fingerprintRecomputed', expected == evidence['sourceFingerprint'])
    check(section, 'runtimeSourceMatches', files == runtime_files(args.project))
    section.update(sourceFingerprint=expected, receiptSha256=digest(receipt_path), reports={})
    for mode in ('EditMode', 'PlayMode'):
        info = evidence['reports'][mode]
        check(section, mode + 'Filename', info['file'] == mode + '.xml')
        report = args.testdir / info['file']
        xml = ET.parse(report).getroot()
        total, passed, failed = (int(xml.get(k, '0')) for k in ('total', 'passed', 'failed'))
        skipped = int(xml.get('skipped', '0'))
        inconclusive = int(xml.get('inconclusive', '0'))
        section['reports'][mode] = {'sha256': digest(report), 'total': total,
                                   'passed': passed, 'failed': failed, 'skipped': skipped, 'inconclusive': inconclusive,
                                   'endTime': xml.get('end-time')}
        check(section, mode + 'CompletePass', xml.tag == 'test-run' and xml.get('result') == 'Passed'
              and total > 0 and passed == total and failed == 0 and skipped == 0 and inconclusive == 0)
        check(section, mode + 'ReceiptMatches', digest(report) == info['sha256'] and passed == info['passed'])
    return evidence


def verify_source(args, section, evidence):
    archive_path = args.output / ('Frontier-Reckoning-' + VERSION + '-source.zip')
    members = archive_inventory(archive_path, section)
    actual = {n: v for n, v in members.items() if not ignored(n)}
    expected = {SOURCE_PREFIX + n: v for n, v in packaged_files(args.project).items()}
    files = {n: v for n, v in actual.items() if v['kind'] != 'directory'}
    check(section, 'exactAllowlistedFiles', set(files) == set(expected),
          {'missing': sorted(set(expected) - set(files)), 'extra': sorted(set(files) - set(expected))})
    mismatch = [n for n, v in expected.items() if files[n] != v]
    check(section, 'sourceBytesShaModesAndKindsMatchProject', not mismatch, mismatch)
    bad_dirs = []
    for name, value in actual.items():
        if value['kind'] != 'directory':
            continue
        clean = name.rstrip('/')
        if not name.startswith(SOURCE_PREFIX) or not any(n.startswith(clean + '/') for n in expected):
            bad_dirs.append(name)
            continue
        rel = clean[len(SOURCE_PREFIX.rstrip('/')):].lstrip('/')
        source = args.project / rel
        if not source.is_dir() or source.is_symlink() or value != record(source):
            bad_dirs.append(name)
    check(section, 'suppliedDirectoryModesAndPathsMatchProject', not bad_dirs, bad_dirs)
    check(section, 'testEvidenceAvailable', evidence is not None)
    wrong = [n for n, h in evidence['files'].items()
             if members.get(SOURCE_PREFIX + n, {}).get('sha256') != h]
    check(section, 'runtimeFilesMatchTestEvidence', not wrong, wrong)
    for name in ('source-evidence.json', 'EditMode.xml', 'PlayMode.xml'):
        archived = members.get(SOURCE_PREFIX + 'Docs/Evidence/' + EVIDENCE_VERSION + '/Tests/' + name)
        check(section, 'archivedTestReceipt:' + name, archived is not None
              and archived.get('sha256') == digest(args.testdir / name))
    archived_receipt = members.get(SOURCE_PREFIX + 'Docs/Evidence/' + EVIDENCE_VERSION + '/Native/client-build.json')
    check(section, 'archivedClientReceiptMatchesOriginal', archived_receipt is not None
          and archived_receipt.get('sha256') == digest(args.client_receipt))
    section.update(sourceFingerprint=evidence['sourceFingerprint'], runtimeFileCount=len(evidence['files']),
                   packagedFileCount=len(expected), includedEvidenceVersion=EVIDENCE_VERSION)


def app_identity(app, section):
    check(section, 'originalAppExists', app.is_dir() and not app.is_symlink(), str(app))
    info = plistlib.loads((app / 'Contents/Info.plist').read_bytes())
    version = info.get('CFBundleShortVersionString')
    check(section, 'appVersionMatches', version == VERSION, version)
    executable_name = info.get('CFBundleExecutable', 'Frontier Reckoning')
    check(section, 'executableNameSafe', isinstance(executable_name, str) and safe_member(executable_name)
          and '/' not in executable_name and executable_name not in ('.', '..'))
    exe = app / 'Contents/MacOS' / executable_name
    check(section, 'executableExistsAndExecutable', exe.is_file() and not exe.is_symlink()
          and bool(exe.stat().st_mode & stat.S_IXUSR))
    probe = subprocess.run(['/usr/bin/lipo', '-archs', str(exe)], capture_output=True, text=True, check=False)
    check(section, 'exactArm64Architecture', probe.returncode == 0 and probe.stdout.strip().split() == ['arm64'],
          {'exitCode': probe.returncode, 'architectures': probe.stdout.strip(), 'error': probe.stderr.strip()})
    boot = app / 'Contents/Resources/Data/boot.config'
    guids = re.findall(r'^build-guid=([^\r\n]+)\r?$', boot.read_text(), re.MULTILINE)
    check(section, 'singleValidBuildGuid', len(guids) == 1 and bool(re.fullmatch(r'[0-9a-fA-F]{32}', guids[0]))
          and int(guids[0], 16) != 0)
    identity = {'appVersion': version, 'buildGuid': guids[0].lower(),
                'architecture': probe.stdout.strip(), 'executableSha256': digest(exe),
                'bootConfigSha256': digest(boot), 'infoPlistSha256': digest(app / 'Contents/Info.plist'),
                'bundleIdentifier': info.get('CFBundleIdentifier')}
    section['appIdentity'] = identity
    return identity


def extract_app(archive_path, prefix, destination, members):
    selected = {n[len(prefix):].rstrip('/'): v for n, v in members.items()
                if n.startswith(prefix) and not ignored(n)}
    destination.mkdir()
    with zipfile.ZipFile(archive_path) as archive:
        for rel, value in selected.items():
            if value['kind'] == 'directory':
                (destination / rel).mkdir(parents=True, exist_ok=True)
        for name, value in members.items():
            if not name.startswith(prefix) or ignored(name) or value['kind'] != 'file':
                continue
            target = destination / name[len(prefix):]
            target.parent.mkdir(parents=True, exist_ok=True)
            with archive.open(name) as source, target.open('xb') as sink:
                shutil.copyfileobj(source, sink)
            target.chmod(value['mode'])
        # Links are created after regular files, preventing traversal through a
        # link during extraction. Every link must stay inside the extracted app.
        for rel, value in selected.items():
            if value['kind'] != 'symlink':
                continue
            target = destination / rel
            link = value['target']
            if os.path.isabs(link) or not (target.parent / link).resolve().is_relative_to(destination.resolve()):
                raise ValueError('App symlink escapes its bundle: ' + rel)
            if any(parent.is_symlink() for parent in target.parents if parent.is_relative_to(destination)):
                raise ValueError('Nested link traversal during extraction: ' + rel)
            target.parent.mkdir(parents=True, exist_ok=True)
            target.symlink_to(link)
        for rel, value in sorted(selected.items(), key=lambda x: len(PurePosixPath(x[0]).parts), reverse=True):
            if value['kind'] == 'directory':
                (destination / rel).chmod(value['mode'])


def verify_client(args, section, evidence):
    path = args.output / ('Frontier-Reckoning-' + VERSION + '-macOS-arm64.zip')
    members = archive_inventory(path, section)
    identity = app_identity(args.buildapp, section)
    original = tree_inventory(args.buildapp)
    prefixes = set()
    for n in members:
        if ignored(n):
            continue
        parts = PurePosixPath(n).parts
        if args.buildapp.name in parts:
            i = parts.index(args.buildapp.name)
            prefixes.add('/'.join(parts[:i + 1]) + '/')
    check(section, 'singleAppRoot', len(prefixes) == 1, sorted(prefixes))
    prefix = next(iter(prefixes))
    payload = prefix[:-len(args.buildapp.name) - 1]
    logical = {n: v for n, v in members.items() if not ignored(n)}
    approved = lambda n, v: (n.startswith(prefix) or n.startswith(payload + 'Licenses/')
        or n in (payload + 'README-zh.md', payload + 'README.md', payload + 'client-build.json', payload + 'release-manifest.json')
        or (v['kind'] == 'directory' and n.rstrip('/') == payload.rstrip('/')))
    unexpected = [n for n, v in logical.items() if not approved(n, v)]
    check(section, 'clientPayloadAllowlist', not unexpected, unexpected)
    archived = {n[len(prefix):].rstrip('/'): v for n, v in logical.items() if n.startswith(prefix)}
    check(section, 'completeAppFileAndDirectorySet', set(archived) == set(original),
          {'missing': sorted(set(original) - set(archived)), 'extra': sorted(set(archived) - set(original))})
    mismatch = [n for n in original if archived[n] != original[n]]
    check(section, 'originalAppShaSizeModesAndLinksMatchZip', not mismatch, mismatch)
    licenses = {n: v for n, v in tree_inventory(args.project / 'Docs/Licenses').items()
                if v['kind'] != 'directory'}
    archived_licenses = {n[len(payload + 'Licenses/'):]: v for n, v in logical.items()
                         if n.startswith(payload + 'Licenses/') and v['kind'] != 'directory'}
    check(section, 'licensePayloadMatches', licenses == archived_licenses)
    guides = [n for n in (payload + 'README-zh.md', payload + 'README.md') if n in logical]
    check(section, 'oneClientGuidePresent', len(guides) == 1)
    check(section, 'clientGuideMatchesOriginalBytesAndMode', logical[guides[0]] == record(args.client_guide))
    for packaged, source in (('client-build.json', args.client_receipt), ('release-manifest.json', args.client_manifest)):
        key = payload + packaged
        check(section, 'originalPayload:' + packaged, key in logical and logical[key] == record(source))
    with zipfile.ZipFile(path) as archive:
        receipt = parse_json(archive.read(payload + 'client-build.json'))
        manifest = parse_json(archive.read(payload + 'release-manifest.json'))
    check(section, 'clientReceiptAndManifestAreObjects', isinstance(receipt, dict) and isinstance(manifest, dict))
    check(section, 'testEvidenceAvailable', evidence is not None)
    check(section, 'clientReceiptVersionGuidAndSourceMatch', receipt.get('schema') == 1
          and receipt.get('appVersion') == VERSION
          and str(receipt.get('buildGuid', '')).lower() == identity['buildGuid']
          and receipt.get('sourceFingerprint') == evidence['sourceFingerprint'])
    check(section, 'clientReceiptFileCountAndArchitectureMatch', receipt.get('sourceFileCount') == len(evidence['files'])
          and receipt.get('architecture') == identity['architecture'] == 'arm64')
    check(section, 'sourceVerifiedAfterBuild', receipt.get('sourceFingerprintVerifiedAfterBuild') is True)
    editor_version = re.search(r'^m_EditorVersion: (.+)$', (args.project / 'ProjectSettings/ProjectVersion.txt').read_text(), re.MULTILINE)
    check(section, 'clientReceiptUnityVersionMatchesFrozenProject', editor_version is not None
          and receipt.get('unityVersion') == editor_version.group(1).strip())
    executable_name = plistlib.loads((args.buildapp / 'Contents/Info.plist').read_bytes())['CFBundleExecutable']
    check(section, 'clientReceiptBinaryFileAndBytesMatch', receipt.get('binaryFile') == 'Contents/MacOS/' + executable_name
          and receipt.get('binaryBytes') == (args.buildapp / receipt['binaryFile']).stat().st_size)
    for field, observed in (('binarySha256', identity['executableSha256']), ('bootConfigSha256', identity['bootConfigSha256'])):
        check(section, 'clientReceipt:' + field, receipt.get(field) == observed)
    for mode in ('EditMode', 'PlayMode'):
        check(section, 'clientReceiptTestBinding:' + mode, receipt.get('tests', {}).get(mode) == evidence['reports'][mode])
    commit = receipt.get('runtimeCommit', '')
    check(section, 'runtimeCommitPresent', isinstance(commit, str) and bool(re.fullmatch(r'[0-9a-f]{40}', commit)))
    check(section, 'embeddedManifestIdentityMatches', manifest.get('appVersion') == VERSION
          and str(manifest.get('buildGuid', '')).lower() == identity['buildGuid']
          and manifest.get('sourceFingerprint') == evidence['sourceFingerprint']
          and manifest.get('runtimeCommit') == commit)
    check(section, 'embeddedManifestHasNoCircularPackageHashes', not manifest.get('packages'))
    if 'sourceFiles' in manifest:
        check(section, 'embeddedManifestSourceFileCount', manifest['sourceFiles'] == len(evidence['files']))
    if 'sourceFileCount' in manifest:
        check(section, 'embeddedManifestSourceFileCount', manifest['sourceFileCount'] == len(evidence['files']))
    if 'tests' in manifest:
        expected_counts = {mode: evidence['reports'][mode]['passed'] for mode in ('EditMode', 'PlayMode')}
        values = manifest['tests']
        for mode, count in expected_counts.items():
            observed = values.get(mode)
            if isinstance(observed, dict):
                check(section, 'embeddedManifestTestBinding:' + mode, observed == evidence['reports'][mode])
            else:
                check(section, 'embeddedManifestTestCount:' + mode, observed == count)
        if 'total' in values:
            check(section, 'embeddedManifestTotalTests', values['total'] == sum(expected_counts.values()))
        if 'failed' in values:
            check(section, 'embeddedManifestNoFailedTests', values['failed'] == 0)
    with tempfile.TemporaryDirectory(prefix='frontier-package-verify-') as temp:
        extracted = Path(temp) / args.buildapp.name
        extract_app(path, prefix, extracted, members)
        check(section, 'extractedAppShaSizeModesAndLinksMatchOriginal', tree_inventory(extracted) == original)
        extracted_identity = app_identity(extracted, {'checks': {}})
        check(section, 'extractedArm64VersionAndGuidMatchOriginal', extracted_identity == identity)
    check(section, 'originalAppUnchangedDuringVerification', tree_inventory(args.buildapp) == original)
    section.update(appPrefix=prefix, appFileCount=sum(v['kind'] == 'file' for v in original.values()),
                   appSymlinkCount=sum(v['kind'] == 'symlink' for v in original.values()),
                   clientBuildJsonSha256=logical[payload + 'client-build.json']['sha256'],
                   embeddedManifestSha256=logical[payload + 'release-manifest.json']['sha256'],
                   runtimeCommit=commit, sourceFingerprint=evidence['sourceFingerprint'])
    return identity


def png_dimensions(data):
    if data[:8] != b'\x89PNG\r\n\x1a\n':
        raise ValueError('Invalid PNG signature')
    offset, dimensions, pixels = 8, None, False
    while offset + 12 <= len(data):
        length = struct.unpack('>I', data[offset:offset + 4])[0]
        end = offset + 12 + length
        if end > len(data):
            raise ValueError('Truncated PNG chunk')
        kind = data[offset + 4:offset + 8]
        value = data[offset + 8:offset + 8 + length]
        crc = struct.unpack('>I', data[end - 4:end])[0]
        if zlib.crc32(kind + value) & 0xffffffff != crc:
            raise ValueError('Invalid PNG chunk CRC')
        if offset == 8:
            if kind != b'IHDR' or length != 13:
                raise ValueError('PNG must begin with a valid IHDR')
            dimensions = struct.unpack('>II', value[:8])
            if min(dimensions) <= 0:
                raise ValueError('Invalid PNG dimensions')
        elif kind == b'IHDR':
            raise ValueError('Duplicate PNG IHDR')
        if kind == b'IDAT':
            pixels = True
        if kind == b'IEND':
            if length != 0 or end != len(data) or not pixels:
                raise ValueError('Invalid PNG termination or missing pixel data')
            return dimensions
        offset = end
    raise ValueError('PNG has no IEND')


def gallery_prefix(members, original, directory_name, section):
    actual_files = {n for n, v in members.items() if not ignored(n) and v['kind'] != 'directory'}
    expected_files = {n for n, v in original.items() if v['kind'] != 'directory'}
    candidates = [p for p in ('', directory_name + '/')
                  if actual_files == {p + n for n in expected_files}]
    check(section, 'singleRecognizedGalleryRoot', len(candidates) == 1,
          {'expectedPrefixes': ['', directory_name + '/'], 'matches': candidates})
    return candidates[0]


def verify_gallery(args, section, evidence, identity):
    path = args.output / ('Frontier-Reckoning-' + VERSION + '-gallery.zip')
    members = archive_inventory(path, section)
    original = tree_inventory(args.gallery_dir)
    unsupported = [n for n, v in members.items() if not ignored(n)
                   and (v['kind'] not in ('file', 'directory') or
                        (v['kind'] == 'file' and PurePosixPath(n).suffix.lower() not in ('.png', '.json', '.md')))]
    check(section, 'galleryPayloadFormatsAndNoSymlinks', not unsupported, unsupported)
    check(section, 'galleryOriginalHasNoSymlinks', all(v['kind'] in ('file', 'directory') for v in original.values()))
    prefix = gallery_prefix(members, original, args.gallery_dir.name, section)
    logical = {n[len(prefix):].rstrip('/'): v for n, v in members.items() if not ignored(n) and n.startswith(prefix)}
    actual_files = {n: v for n, v in logical.items() if v['kind'] != 'directory'}
    expected_files = {n: v for n, v in original.items() if v['kind'] != 'directory'}
    check(section, 'exactGalleryFileSetBytesShaAndModes', actual_files == expected_files)
    bad_dirs = [n for n, v in logical.items() if v['kind'] == 'directory' and original.get(n) != v]
    check(section, 'suppliedGalleryDirectoryModesMatchOriginal', not bad_dirs, bad_dirs)
    check(section, 'galleryGuidePresent', any(n.lower().endswith('.md') for n in actual_files))
    pngs = [n for n, v in members.items() if not ignored(n) and v['kind'] == 'file' and n.lower().endswith('.png')]
    check(section, 'nativePngPairsPresent', bool(pngs))
    check(section, 'appIdentityAndTestEvidenceAvailable', evidence is not None and identity is not None)
    section['images'] = []
    metadata = {}
    with zipfile.ZipFile(path) as archive:
        for name in pngs:
            metadata_name = name[:-4] + '.json'
            check(section, name + ':pairedMetadata', metadata_name in members and members[metadata_name]['kind'] == 'file')
            image = archive.read(name)
            data = parse_json(archive.read(metadata_name))
            check(section, name + ':metadataIsObject', isinstance(data, dict))
            metadata[metadata_name] = data
            dimensions = png_dimensions(image)
            check(section, name + ':pngDimensionsMatchMetadata', list(dimensions) == [data.get('width'), data.get('height')], list(dimensions))
            check(section, name + ':pngShaMatchesMetadata', data.get('pngSha256') == members[name]['sha256'])
            check(section, name + ':nativeVersionAndGuidMatch', data.get('appVersion') == VERSION
                  and str(data.get('buildGuid', '')).lower() == identity['buildGuid'])
            check(section, name + ':captureSchemaThree', data.get('schemaVersion') == 3)
            check(section, name + ':nativeCaptureFieldTypes', type(data.get('inMenu')) is bool and type(data.get('paused')) is bool
                  and type(data.get('width')) is int and type(data.get('height')) is int
                  and isinstance(data.get('matchId'), str))
            check(section, name + ':sameRequestedMatch', data.get('requestMatchId') == data.get('matchId'))
            check(section, name + ':captureFrameOrder', type(data.get('requestFrame')) is int
                  and type(data.get('captureFrame')) is int and 0 <= data['requestFrame'] <= data['captureFrame'])
            check(section, name + ':matchIdentityPresent', bool(data.get('matchId')) or data.get('inMenu') is True)
            if not data.get('inMenu'):
                check(section, name + ':mapAndSeedPresent', data.get('mapId') in MAP_IDS
                      and type(data.get('seed')) is int and -2147483648 <= data['seed'] <= 2147483647)
                generated = str(data.get('mapId', '')).startswith('generated_')
                check(section, name + ':generatedTerrainIdentity', data.get('generatedTerrain') is generated
                      and data.get('mapGeneratorVersion') == (1 if generated else 0)
                      and data.get('requiresTransport') is (data.get('mapId') == 'generated_strait'))
            section['images'].append({'png': name, 'metadata': metadata_name,
                'pngSha256': members[name]['sha256'], 'metadataSha256': members[metadata_name]['sha256'],
                'buildGuid': data['buildGuid'], 'appVersion': data['appVersion'], 'matchId': data.get('matchId'),
                'mapId': data.get('mapId'), 'seed': data.get('seed'), 'inMenu': data.get('inMenu'), 'paused': data.get('paused'),
                'generatedTerrain': data.get('generatedTerrain'), 'mapGeneratorVersion': data.get('mapGeneratorVersion'),
                'requiresTransport': data.get('requiresTransport'), 'objective': data.get('objective'),
                'fleet': [u for u in data.get('units', []) if u.get('definition') in ('fishing_boat', 'gunboat', 'transport_ship', 'frigate', 'monitor')]})
        for name, value in members.items():
            if ignored(name) or value['kind'] != 'file' or not name.lower().endswith('.json'):
                continue
            data = parse_json(archive.read(name))
            if isinstance(data, dict) and 'sourceFingerprint' in data:
                check(section, name + ':sourceFingerprintMatches', data['sourceFingerprint'] == evidence['sourceFingerprint'])
        section['captureIndexes'] = []
        for name, value in members.items():
            if ignored(name) or value['kind'] != 'file' or PurePosixPath(name).name not in ('capture-index.json', 'gallery-index.json', 'index.json'):
                continue
            data = parse_json(archive.read(name))
            check(section, name + ':indexIsObject', isinstance(data, dict))
            guid = data.get('nativeBuildGuid', data.get('buildGuid', ''))
            check(section, name + ':nativeBuildGuidMatches', str(guid).lower() == identity['buildGuid'])
            references = [(k, v, data.get('matchId')) for k, v in data.items() if k.endswith('Capture') and isinstance(v, str)]
            for index, entry in enumerate(data.get('captures', data.get('images', []))):
                if isinstance(entry, str):
                    references.append(('capture-' + str(index), entry, data.get('matchId')))
                elif isinstance(entry, dict):
                    for field in ('metadata', 'json', 'stateJson', 'capture', 'png'):
                        if isinstance(entry.get(field), str):
                            references.append(('capture-' + str(index) + '-' + field, entry[field], entry.get('matchId', data.get('matchId'))))
            check(section, name + ':f9ReferencesPresent', bool(references))
            index_result = {'file': name, 'sha256': value['sha256'], 'nativeBuildGuid': guid, 'references': {}}
            for field, filename, match_id in references:
                check(section, name + ':' + field + ':safeReference', safe_member(filename) and filename.lower().endswith(('.json', '.png')))
                resolved = str(PurePosixPath(name).parent / filename)
                if resolved.lower().endswith('.png'):
                    check(section, name + ':' + field + ':nativePngIncluded', resolved in pngs)
                    resolved = resolved[:-4] + '.json'
                check(section, name + ':' + field + ':pairedF9Included', resolved in metadata)
                if match_id:
                    check(section, name + ':' + field + ':matchIdMatches', metadata[resolved].get('matchId') == match_id)
                index_result['references'][field] = resolved
            section['captureIndexes'].append(index_result)
    check(section, 'originalGalleryUnchangedDuringVerification', tree_inventory(args.gallery_dir) == original)
    section.update(imageCount=len(pngs), galleryPrefix=prefix, galleryOriginal=str(args.gallery_dir),
                   sourceFingerprint=evidence['sourceFingerprint'], buildGuid=identity['buildGuid'])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('project', 'output', 'testdir', 'buildapp', 'gallery_dir', 'client_manifest', 'client_guide'):
        parser.add_argument('--' + name.replace('_', '-'), type=Path, required=True)
    parser.add_argument('--client-receipt', type=Path, help='Original build receipt; defaults to PROJECT/Docs/Evidence/065/Native/client-build.json')
    parser.add_argument('--report', type=Path, help='New report file; default OUTPUT/package-contents-verification.json')
    args = parser.parse_args()
    for name in ('project', 'output', 'testdir', 'buildapp', 'gallery_dir', 'client_manifest', 'client_guide'):
        setattr(args, name, getattr(args, name).resolve())
    args.client_receipt = (args.client_receipt or args.project / 'Docs/Evidence/065/Native/client-build.json').resolve()
    args.report = (args.report or args.output / 'package-contents-verification.json').resolve()
    if args.report.exists():
        parser.error('Report already exists; choose a new --report path')
    if not args.report.is_relative_to(args.output) or args.report.suffix != '.json':
        parser.error('Report must be a new JSON under --output')
    result = {'schema': 'frontier-package-contents-verification-080',
        'checkedAtUtc': datetime.now(timezone.utc).isoformat(), 'release': VERSION,
        'scope': 'Actual package/source/test/app identity and extracted file verification only. CRC includes all ZIP members; __MACOSX and .DS_Store are ignored for logical payload equivalence. No gameplay, native FPS, signing, notarization, cloud checksum or clean-Git claim. The embedded manifest is an identity snapshot; final outer release-manifest package hashes are outside this run. ZIP byte reproducibility is not claimed.',
        'verifierSha256': digest(Path(__file__)),
        'inputs': {n: str(getattr(args, n)) for n in ('project', 'output', 'testdir', 'buildapp', 'gallery_dir', 'client_manifest', 'client_guide', 'client_receipt')},
        'sections': {}}
    def run(label, action):
        section = result['sections'][label] = {'status': 'running', 'checks': {}}
        try:
            value = action(section)
            section['status'] = 'passed'
            return value
        except (OSError, ValueError, KeyError, TypeError, ET.ParseError,
                zipfile.BadZipFile, plistlib.InvalidFileException, RuntimeError, AttributeError, struct.error) as error:
            section.update(status='failed', error=str(error))
            return None
    evidence = run('testEvidence', lambda s: verify_tests(args, s))
    run('source', lambda s: verify_source(args, s, evidence))
    identity = run('client', lambda s: verify_client(args, s, evidence))
    run('gallery', lambda s: verify_gallery(args, s, evidence, identity))
    if evidence is not None:
        run('sourceStillFrozen', lambda s: check(s, 'runtimeSourceStillMatches', runtime_files(args.project) == evidence['files']))
    for label in ('source', 'client', 'gallery'):
        section = result['sections'][label]
        snapshot = section.get('archiveIdentity')
        if snapshot is not None:
            def unchanged(s, name=snapshot['file'], saved=snapshot):
                path = args.output / name
                check(s, 'archiveUnchangedDuringVerification', path.stat().st_size == saved['bytes'] and digest(path) == saved['sha256'])
            run(label + 'ArchiveStillFrozen', unchanged)
    result['passed'] = all(v['status'] == 'passed' for v in result['sections'].values())
    result['finishedAtUtc'] = datetime.now(timezone.utc).isoformat()
    args.report.parent.mkdir(parents=True, exist_ok=True)
    with args.report.open('x', encoding='utf-8') as stream:
        stream.write(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps({'report': str(args.report), 'passed': result['passed'],
                      'sections': {n: s['status'] for n, s in result['sections'].items()}}, ensure_ascii=False))
    return 0 if result['passed'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
