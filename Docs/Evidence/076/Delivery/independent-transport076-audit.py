#!/usr/bin/env python3
"""Independent read-only audit of twelve-file 076 lossless transport.

Does not import the preparer. Rehashes fixed original files and closed size-only
failure audit; compares all exact copies; hashes ordered parts and presents them
as a seekable virtual ZIP. Reads every member of all four ZIPs through EOF for
actual CRC/SHA/Unix mode checks. Never reconstructs/extracts to disk, recompresses,
executes a client, writes Diagnostics, modifies inputs, uploads or publishes.
Fresh JSON/Markdown outputs must be outside original/transport/repo/Diagnostics.
"""
import argparse
from collections import Counter
from datetime import datetime, timezone
import hashlib
import io
import json
import os
from pathlib import Path
import re
import stat
import struct
import sys
import unicodedata
import zipfile

ROOT = Path(__file__).resolve().parent
REPO = Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
SNAPSHOT = 'f0f829bc2ae5550fb13f3c1ebab403652c921065'
RUNTIME = 'b1a9b6a977a60b63cd3e9f9632179c1787f3d00d'
FP = 'dabf34d26490cece872959764b8b9e515749584b180f3d7899f38d79b69a6f8f'
GUID = 'a86c515f3ab040649ea505e507193ff3'
VERSION = '0.19.0'
CHUNK = 1024 * 1024
PART_BYTES = 64 * 1024 * 1024
LIMIT = 100 * 1024 * 1024
SIZE_ERROR = 'Actual artifact exceeds declared 100MiB delivery limit'
EXPECTED = {
    'README.md': (1796, '2e9e0cfcc17f78fcd8cdbdde7e2a5e35d6daa491f49fdef6ae75f1f51373174b'),
    'actual-app-manifest076.json': (204218, 'f05d5fbccfec46f54a1696ff7450ff01f541252c275669319af1b5dc48a90c4d'),
    'checksums.sha256': (657, 'bae17e26dc8bc050c63d594392be0268ba5bb9240ef8c2ce4762e3e8ab6c254c'),
    'evidence.zip': (122090550, '101a05650de14ccb5588bd577ae130318d5ee544537767c3880b4deb2428bb7a'),
    'macOS.zip': (76004155, 'ef6be719db8ef73a96ebaf45f27d040f5316f4c192133724aaef18a81098c690'),
    'normal.zip': (36922998, '23d5f9f35ddb633d08b6a0cbd36e7c8c3b639e6c93da57a657a827be162ebdb1'),
    'package-manifest076.json': (1369366, '2bc94f869674fd1c6cfb9c7710026966d90bb2ef4d1e510b22643f90d109678b'),
    'source.manifest.json': (151713, '4f427b044fe221a837436ae3956cd2604121d567341e86b8d4e2205ebd4389a0'),
    'source.zip': (58231890, '2720158793eeaa807dd47255ea108507e58276f217c541c4c1173ebc1e2eb6d7')}
AUDITS = {
    'json': (15263, '2b0d71d45662c16a99dea41193ba0ea992bc4d19ec0b88311d2d4dee72a40f70'),
    'markdown': (761, '72a7e27cca2874af3b750617e6c78a5cfec7775a195388dba672fe7295ada3d9')}
COUNTS = {'source': 1201, 'macOS': 319, 'evidence': 337, 'normal': 33}
PART_NAMES = ('evidence.zip.part01', 'evidence.zip.part02')
TRANSPORT_NAMES = (set(EXPECTED) - {'evidence.zip'}) | set(PART_NAMES) | {'README-076.md', 'transport-manifest076.json'}


def require(ok, reason):
    if not ok:
        raise ValueError(reason)


def identity(s):
    return (s.st_dev, s.st_ino, s.st_mode, s.st_size, s.st_mtime_ns, s.st_ctime_ns)


def file_record(path):
    path = Path(path).absolute(); before = path.lstat()
    require(stat.S_ISREG(before.st_mode), 'Expected regular file: ' + str(path))
    h, size = hashlib.sha256(), 0
    with os.fdopen(os.open(path, os.O_RDONLY | os.O_NOFOLLOW), 'rb') as stream:
        require(identity(os.fstat(stream.fileno())) == identity(before), 'File replaced before hash: ' + str(path))
        for block in iter(lambda: stream.read(CHUNK), b''):
            h.update(block); size += len(block)
        require(identity(os.fstat(stream.fileno())) == identity(before), 'File changed while hash: ' + str(path))
    require(identity(path.lstat()) == identity(before) and size == before.st_size, 'File changed after hash: ' + str(path))
    return {'path': str(path), 'bytes': size, 'sha256': h.hexdigest(), 'unixMode': before.st_mode & 0xffff,
            'inputIdentity': list(identity(before))}


def load(path):
    def unique(pairs):
        out = {}
        for key, value in pairs:
            require(key not in out, 'Duplicate JSON key: ' + key); out[key] = value
        return out
    return json.loads(Path(path).read_bytes(), object_pairs_hook=unique,
                      parse_constant=lambda x: require(False, 'Nonfinite JSON: ' + x))


def same_claim(actual, claim, path=True):
    keys = ('bytes', 'sha256', 'unixMode') + (('path',) if path else ())
    require(all(actual[k] == claim[k] for k in keys), 'File claim differs from actual independent input: ' + actual['path'])


def original_identity(original):
    require({p.name for p in original.iterdir()} == set(EXPECTED), 'Original Delivery1 membership differs')
    actual = {name: file_record(original / name) for name in EXPECTED}
    for name, (size, sha) in EXPECTED.items():
        require((actual[name]['bytes'], actual[name]['sha256']) == (size, sha), 'Fixed original bytes differ: ' + name)
    m = load(original / 'package-manifest076.json')
    require(m['schema'] == '076-delivery-packages-v1' and m['sourceSnapshot'] == SNAPSHOT and m['runtimeCommit'] == RUNTIME
            and m['sourceFingerprint'] == FP and m['runtimeInputCount'] == 774 and m['appVersion'] == VERSION
            and m['buildGuid'] == GUID and m['status'] == 'COMPLETE_BUT_SIZE_LIMIT_EXCEEDED', 'Original manifest identity differs')
    require(len(m['sizeLimitExceeded']) == 1 and Path(m['sizeLimitExceeded'][0]).resolve() == original / 'evidence.zip',
            'Original size-failure scope differs')
    require(set(m['packages']) == set(COUNTS), 'Original four-package set differs')
    for name, count in COUNTS.items():
        claim = m['packages'][name]
        require(claim['memberCount'] == count and len(claim['members']) == count
                and (claim['bytes'], claim['sha256']) == EXPECTED[name + '.zip'], 'Original package record differs: ' + name)
    return actual, m


def closed_original_audit(original, originals, json_path, md_path):
    records = {'json': file_record(json_path), 'markdown': file_record(md_path)}
    for key, expected in AUDITS.items():
        require((records[key]['bytes'], records[key]['sha256']) == expected, 'Fixed original audit bytes differ: ' + key)
    a = load(json_path)
    require(a['schema'] == 'independent-package076-audit-v1' and a['status'] == 'FAIL' and a['error'] == SIZE_ERROR
            and a['finishedUtc'] and a['sourceSnapshot'] == SNAPSHOT and a['runtimeCommit'] == RUNTIME
            and a['sourceFingerprint'] == FP and a['expectedBuildGuid'] == GUID, 'Original audit is not the fixed size-only failure')
    require(a['checks']['tests']['totalPassed'] == 1795 and a['checks']['sourceSnapshot']['runtimeInputCount'] == 774,
            'Original test/source identity differs')
    require(set(a['checks']['archives']) == set(COUNTS), 'Original audit did not complete all archives')
    for name, count in COUNTS.items():
        r = a['checks']['archives'][name]
        require(r['members'] == count and all(r[k] is True for k in
                ('crcAllMembersReadToEof', 'centralDirectoryAndEof', 'safeUniqueNames', 'allMemberBytesShaModeCompared')),
                'Original CRC/EOF/SHA/mode result incomplete: ' + name)
        require(Path(r['file']['path']).resolve() == original / (name + '.zip')
                and all(r['file'][k] == originals[name + '.zip'][k] for k in ('bytes', 'sha256')),
                'Original audit archive binding differs: ' + name)
    return records, a


class OrderedParts(io.RawIOBase):
    """Read-only seekable concatenation; no reconstructed ZIP is written."""
    def __init__(self, paths):
        super().__init__(); self.files = []; self.paths = tuple(Path(p) for p in paths)
        self.stats = [p.lstat() for p in self.paths]
        require(all(stat.S_ISREG(s.st_mode) for s in self.stats), 'Virtual ZIP input is not regular')
        self.lengths = [s.st_size for s in self.stats]; self.offsets = [0]
        for length in self.lengths:
            self.offsets.append(self.offsets[-1] + length)
        self.files = []
        try:
            for path, before in zip(self.paths, self.stats):
                stream = os.fdopen(os.open(path, os.O_RDONLY | os.O_NOFOLLOW), 'rb')
                require(identity(os.fstat(stream.fileno())) == identity(before), 'Part replaced while opening virtual ZIP')
                self.files.append(stream)
        except BaseException:
            for f in self.files:
                f.close()
            raise
        self.position = 0

    def readable(self):
        return True

    def seekable(self):
        return True

    def tell(self):
        return self.position

    def seek(self, offset, whence=os.SEEK_SET):
        if whence == os.SEEK_SET:
            new = offset
        elif whence == os.SEEK_CUR:
            new = self.position + offset
        elif whence == os.SEEK_END:
            new = self.offsets[-1] + offset
        else:
            raise ValueError('Invalid virtual ZIP seek origin')
        require(isinstance(new, int) and new >= 0, 'Negative/noninteger virtual ZIP seek')
        self.position = new
        return new

    def read(self, size=-1):
        require(not self.closed, 'Read on closed virtual ZIP')
        available = max(0, self.offsets[-1] - self.position)
        remaining = available if size is None or size < 0 else min(size, available)
        if remaining == 0:
            return b''
        blocks = []
        for index, stream in enumerate(self.files):
            start, end = self.offsets[index:index + 2]
            if self.position >= end:
                continue
            require(self.position >= start, 'Virtual ZIP position mapping failure')
            count = min(remaining, end - self.position)
            stream.seek(self.position - start); block = stream.read(count)
            require(len(block) == count, 'Transport part ended early during virtual ZIP read')
            blocks.append(block); self.position += count; remaining -= count
            if remaining == 0:
                break
        require(remaining == 0, 'Virtual ZIP read did not cover declared range')
        return b''.join(blocks)

    def close(self):
        if not self.closed:
            for path, stream, before in zip(self.paths, self.files, self.stats):
                require(identity(os.fstat(stream.fileno())) == identity(before)
                        and identity(path.lstat()) == identity(before), 'Part changed during virtual ZIP audit')
                stream.close()
        super().close()


def eocd(stream, length):
    stream.seek(max(0, length - 65557)); tail = stream.read()
    at = tail.rfind(b'PK\x05\x06'); require(at >= 0 and len(tail) - at >= 22, 'Missing ZIP EOCD')
    _, disk, cddisk, diskcount, count, cdsize, offset, comment = struct.unpack_from('<4s4H2IH', tail, at)
    require(disk == cddisk == 0 and diskcount == count and count != 65535
            and cdsize != 0xffffffff and offset != 0xffffffff, 'Unexpected split-volume/ZIP64 original format')
    require(at + 22 + comment == len(tail) and offset + cdsize == length - len(tail) + at,
            'ZIP central directory/EOF/trailing bytes differ')
    return count


def safe_name(name):
    require(isinstance(name, str) and name and not name.startswith('/') and '\\' not in name
            and not re.match(r'^[A-Za-z]:', name)
            and all(p not in ('', '.', '..') for p in name.rstrip('/').split('/'))
            and not any(ord(c) < 32 or ord(c) == 127 for c in name), 'Unsafe archive member')
    return name


def audit_zip(stream, length, declared, old_check):
    count = eocd(stream, length); expected = declared['members']; aliases = set(); results = {}; types = Counter()
    require(count == len(expected) == declared['memberCount'] == old_check['members'], 'ZIP central/manifest/original audit count differs')
    with zipfile.ZipFile(stream, 'r') as archive:
        infos = archive.infolist(); names = [i.filename for i in infos]
        require(len(names) == len(set(names)) == count and set(names) == set(expected), 'ZIP member membership/uniqueness differs')
        for info in infos:
            name = safe_name(info.filename); alias = unicodedata.normalize('NFC', name.rstrip('/')).casefold()
            require(alias not in aliases, 'Case/Unicode ZIP alias'); aliases.add(alias)
            require(info.create_system == 3 and not info.flag_bits & 1, 'Encrypted/non-Unix original ZIP member')
            claim = expected[name]; mode = (info.external_attr >> 16) & 0xffff
            require(mode == claim['unixMode'] and oct(mode) == claim['unixModeOctal'], 'ZIP Unix mode differs: ' + name)
            node = {'file': stat.S_IFREG, 'directory': stat.S_IFDIR, 'symlink': stat.S_IFLNK}[claim['type']]
            require(stat.S_IFMT(mode) == node and info.is_dir() == (claim['type'] == 'directory'), 'ZIP node type differs: ' + name)
            h, size = hashlib.sha256(), 0
            with archive.open(info, 'r') as member:
                for block in iter(lambda: member.read(CHUNK), b''):
                    h.update(block); size += len(block)
            # ZipExtFile performs actual CRC verification on EOF of every member.
            require(size == info.file_size == claim['bytes'] and h.hexdigest() == claim['sha256'], 'ZIP member bytes/SHA differ: ' + name)
            require(claim['type'] != 'directory' or size == 0, 'Nonempty ZIP directory member')
            results[name] = {'type': claim['type'], 'bytes': size, 'sha256': h.hexdigest(), 'unixMode': mode}
            types[claim['type']] += 1
    inventory_sha = hashlib.sha256(json.dumps(results, sort_keys=True, separators=(',', ':')).encode()).hexdigest()
    require(inventory_sha == old_check['memberInventorySha256'] and dict(types) == old_check['types']
            and sum(v['bytes'] for v in results.values()) == old_check['uncompressedBytes'],
            'Re-audited archive differs from fixed original independent inventory')
    return {'members': count, 'types': dict(types), 'uncompressedBytes': sum(v['bytes'] for v in results.values()),
            'memberInventorySha256': inventory_sha, 'crcAllMembersReadToEof': True, 'centralDirectoryAndEof': True,
            'safeUniqueNames': True, 'allMemberBytesShaModeCompared': True}


def checksum_audit(path, files, evidence_sha):
    rows = {}
    for line in path.read_text('ascii').splitlines():
        require(re.fullmatch(r'[0-9a-f]{64}  [A-Za-z0-9.-]+', line), 'Malformed original checksum row')
        sha, name = line.split('  ', 1); require(name not in rows, 'Duplicate original checksum'); rows[name] = sha
    require(set(rows) == set(EXPECTED) - {'checksums.sha256'}, 'Original eight-checksum membership differs')
    for name, sha in rows.items():
        actual = evidence_sha if name == 'evidence.zip' else files[name]['sha256']
        require(sha == actual == EXPECTED[name][1], 'Original checksum differs after virtual reconstruction: ' + name)
    return rows


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original', type=Path, default=ROOT / 'Delivery1')
    parser.add_argument('--transport', type=Path, default=ROOT / 'DeliveryTransport1')
    parser.add_argument('--original-audit', type=Path, default=ROOT / 'PackageAudit1/audit.json')
    parser.add_argument('--original-audit-markdown', type=Path, default=ROOT / 'PackageAudit1/audit.md')
    parser.add_argument('--json', required=True, type=Path)
    parser.add_argument('--markdown', required=True, type=Path)
    args = parser.parse_args(); original = args.original.resolve(strict=True); transport = args.transport.resolve(strict=True)
    require(original == ROOT / 'Delivery1' and transport.parent == ROOT and transport != ROOT / 'Diagnostics', 'Unexpected original/transport roots')
    require(args.json.resolve() != args.markdown.resolve(), 'Audit output paths must differ')
    for output in (args.json, args.markdown):
        require(not output.exists() and output.parent.is_dir(), 'Audit outputs must be fresh with existing parent')
        require(not any(output.resolve().is_relative_to(p.resolve()) for p in (original, transport, REPO, ROOT / 'Diagnostics', ROOT / 'PackageAudit1')),
                'Audit output must be outside immutable inputs, repository and Diagnostics')
    report = {'schema': 'independent-transport076-audit-v1', 'status': 'FAIL',
              'startedUtc': datetime.now(timezone.utc).isoformat(), 'auditor': file_record(Path(__file__)),
              'sourceSnapshot': SNAPSHOT, 'runtimeCommit': RUNTIME, 'sourceFingerprint': FP, 'buildGuid': GUID,
              'checks': {}, 'boundaries': [
                  'Independent code; does not import or execute the preparer, reconstruct/extract archives to disk, execute the application, alter originals, upload or publish.',
                  'The original Delivery1 size failure is preserved; transport success is not a reversal or relaxation of that failure.',
                  'Every original checksum and every actual member of all four ZIPs is checked, including evidence through a seekable read-only concatenation of ordered parts.',
                  'No whole-goal, native human victory, FPS, medical river/transport lifecycle, two-physical-Mac, 075-save migration or cloud acceptance is inferred.']}
    try:
        originals, package = original_identity(original)
        audit_json = args.original_audit.resolve(strict=True); audit_md = args.original_audit_markdown.resolve(strict=True)
        require(audit_json == ROOT / 'PackageAudit1/audit.json' and audit_md == ROOT / 'PackageAudit1/audit.md', 'Wrong original closed audit paths')
        audit_records, original_audit = closed_original_audit(original, originals, audit_json, audit_md)
        report['checks']['originalFailure'] = {'status': 'FAIL', 'soleError': SIZE_ERROR,
                                              'packagerExitCode': 2, 'auditFiles': audit_records, 'failurePreserved': True}
        require({p.name for p in transport.iterdir()} == TRANSPORT_NAMES and len(TRANSPORT_NAMES) == 12, 'Transport twelve-file set differs')
        outputs = {name: file_record(transport / name) for name in TRANSPORT_NAMES}
        require(all(v['bytes'] < LIMIT for v in outputs.values()), 'Actual transport object exceeds strict 100MiB limit')
        m = load(transport / 'transport-manifest076.json')
        require(m['schema'] == '076-lossless-transport-v1' and m['status'] == 'PREPARED_LOCAL_TRANSPORT_BYTES_NOT_INDEPENDENT_AUDIT'
                and m['sourceSnapshot'] == SNAPSHOT and m['runtimeCommit'] == RUNTIME and m['sourceFingerprint'] == FP
                and m['runtimeInputCount'] == 774 and m['appVersion'] == VERSION and m['buildGuid'] == GUID
                and m['originalDeliveryStatus'] == 'COMPLETE_BUT_SIZE_LIMIT_EXCEEDED' and m['originalPackagerExitCode'] == 2,
                'Transport manifest identity differs')
        require(m['outputFileCountIncludingManifest'] == 12 and m['partCount'] == 2 and m['splitPartBytes'] == PART_BYTES
                and m['maxTransportBytesExclusive'] == LIMIT and m['manifestSelfHashIncluded'] is False
                and m['partsAreStandaloneZipFiles'] is False and m['recompressionOrTrimming'] is False,
                'Transport profile differs')
        require(Path(m['originalDeliveryDirectory']).resolve() == original and set(m['originalFiles']) == set(EXPECTED), 'Transport original inventory differs')
        for name, actual in originals.items():
            same_claim(actual, m['originalFiles'][name])
        require(set(m['outputInventoryExcludingManifest']) == TRANSPORT_NAMES - {'transport-manifest076.json'}, 'Transport declared output inventory differs')
        for name, claim in m['outputInventoryExcludingManifest'].items():
            same_claim(outputs[name], claim)
        require(set(m['copiedOriginalFiles']) == set(EXPECTED) - {'evidence.zip'}, 'Transport copied-original set differs')
        for name in m['copiedOriginalFiles']:
            same_claim(outputs[name], m['copiedOriginalFiles'][name])
            require(all(outputs[name][k] == originals[name][k] for k in ('bytes', 'sha256', 'unixMode')), 'Copied original differs: ' + name)
        require(set(m['helpers']) == {'prepare-transport076.py', 'independent-transport076-audit.py'}, 'Transport helper references differ')
        helper_records = {name: file_record(ROOT / name) for name in m['helpers']}
        for name, actual in helper_records.items():
            same_claim(actual, m['helpers'][name])
        for key, actual in audit_records.items():
            same_claim(actual, m['originalAudit']['records'][key])
        require(m['originalAudit']['status'] == 'FAIL' and m['originalAudit']['soleError'] == SIZE_ERROR
                and m['originalAudit']['failurePreservedNotReclassified'] is True, 'Original failure was reclassified')
        require(set(m['originalAudit']['allFourArchiveChecks']) == set(COUNTS), 'Original archive-proof reference set differs')
        for name, count in COUNTS.items():
            r = m['originalAudit']['allFourArchiveChecks'][name]
            require(r['members'] == count and r['originalWholeSha256'] == EXPECTED[name + '.zip'][1]
                    and r['originalFullCrcEofShaModesPassed'] is True, 'Transport original audit reference differs: ' + name)
        same_claim(originals['evidence.zip'], m['fullEvidenceZip'])
        require([p['name'] for p in m['parts']] == list(PART_NAMES)
                and [p['order'] for p in m['parts']] == [1, 2]
                and [p['offset'] for p in m['parts']] == [0, PART_BYTES]
                and [p['bytes'] for p in m['parts']] == [PART_BYTES, 54981686], 'Ordered split geometry differs')
        full_hash, total = hashlib.sha256(), 0
        for p in m['parts']:
            same_claim(outputs[p['name']], p)
            with (transport / p['name']).open('rb') as stream:
                for block in iter(lambda: stream.read(CHUNK), b''):
                    full_hash.update(block); total += len(block)
        full_sha = full_hash.hexdigest()
        require((total, full_sha) == EXPECTED['evidence.zip'], 'Ordered concatenation differs from full original evidence ZIP')
        report['checks']['exactCopies'] = {'files': 8, 'allOriginalBytesShaModesMatched': True}
        report['checks']['orderedParts'] = {'files': 2, 'partBytes': [PART_BYTES, 54981686], 'fullBytes': total,
                                           'fullSha256': full_sha, 'exactOriginalZipReconstructedVirtually': True, 'reconstructedFileWritten': False}
        report['checks']['archives'] = {}
        for name in ('source', 'macOS', 'evidence', 'normal'):
            if name == 'evidence':
                with OrderedParts([transport / p for p in PART_NAMES]) as stream:
                    result = audit_zip(stream, total, package['packages'][name], original_audit['checks']['archives'][name])
                result['input'] = 'Read-only virtual ordered-part concatenation'
            else:
                with (transport / (name + '.zip')).open('rb') as stream:
                    result = audit_zip(stream, outputs[name + '.zip']['bytes'], package['packages'][name], original_audit['checks']['archives'][name])
                result['input'] = str(transport / (name + '.zip'))
            report['checks']['archives'][name] = result
            print(json.dumps({'checkedTransportArchive': name, 'members': result['members']}), flush=True)
        checksums = checksum_audit(transport / 'checksums.sha256', outputs, full_sha)
        require(checksums == m['originalChecksumValues'], 'Transport original checksum declaration differs')
        report['checks']['originalChecksums'] = {'rows': 8, 'allOriginalValuesMatched': True,
                                                'evidenceUsesActualOrderedConcatenationSha': True, 'values': checksums}
        readme = (transport / 'README-076.md').read_text('utf-8')
        require('cat evidence.zip.part01 evidence.zip.part02 > evidence.zip' in readme and EXPECTED['evidence.zip'][1] in readme
                and '不是独立 ZIP' in readme and 'exit2' in readme and SIZE_ERROR in readme,
                'Transport user instructions omit reconstruction/hash/size-failure boundaries')
        report['checks']['transportInventory'] = {'files': 12, 'strictMaxBytesExclusive': LIMIT,
                                                'everyFileBelowLimit': True, 'filesAndActualHashes': outputs}
        require(original_identity(original)[0] == originals, 'Original delivery changed during audit')
        require(closed_original_audit(original, originals, audit_json, audit_md)[0] == audit_records, 'Original audit changed during transport audit')
        require({name: file_record(transport / name) for name in TRANSPORT_NAMES} == outputs, 'Transport files changed during audit')
        require({name: file_record(ROOT / name) for name in helper_records} == helper_records, 'Transport helper code changed during audit')
        report['checks']['unchangedInputs'] = {'originalDelivery': True, 'originalAudit': True, 'transport': True, 'helpers': True}
        report['status'] = 'PASS_LOCAL_TRANSPORT_CONSISTENCY'
    except (ValueError, OSError, KeyError, TypeError, zipfile.BadZipFile, NotImplementedError, struct.error, RuntimeError) as error:
        report['error'] = str(error)
    report['finishedUtc'] = datetime.now(timezone.utc).isoformat()
    with args.json.open('x', encoding='utf-8') as stream:
        json.dump(report, stream, ensure_ascii=False, indent=2, allow_nan=False); stream.write('\n')
    with args.markdown.open('x', encoding='utf-8') as stream:
        stream.write('# Independent 076 lossless transport audit\n\nStatus: **' + report['status'] + '**.\n\n')
        if 'error' in report:
            stream.write('Discrepancy: ' + report['error'] + '\n\n')
        stream.write('Original Delivery1 remains exit2 / audit FAIL, solely `' + SIZE_ERROR + '`. Original evidence.zip is preserved locally. This separate transport layer does not reverse that result or relax its limit.\n\n')
        for name, r in report['checks'].get('archives', {}).items():
            stream.write('- ' + name + ': ' + str(r['members']) + ' members; all member CRC/EOF/SHA/Unix modes independently checked.\n')
        stream.write('\nTransport scope: eight unchanged originals, two ordered binary fragments, README-076 and manifest; twelve files, each strictly below100MiB. Evidence is read as a virtual concatenation; no reconstructed file or extracted/executed app is created. Original eight checksum values are checked using the actual full concatenation SHA.\n\nNo cloud, whole-goal, human victory, FPS or new gameplay acceptance is inferred.\n')
    print(json.dumps({'status': report['status'], 'json': file_record(args.json), 'markdown': file_record(args.markdown)}), flush=True)
    return 0 if report['status'] == 'PASS_LOCAL_TRANSPORT_CONSISTENCY' else 1


if __name__ == '__main__':
    sys.exit(main())
