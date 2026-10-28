#!/usr/bin/env python3
"""Prepare lossless transport fragments of the immutable 076 Delivery1.

Does not build, recompress, trim, extract, execute, modify the original delivery,
write Diagnostics, publish Git, upload, or change permissions in a cloud service.
A fresh output directory is mandatory. A partial failed output is preserved.
Merely creating this helper is not an execution or acceptance receipt.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import stat
import sys

ROOT = Path(__file__).resolve().parent
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


def require(ok, reason):
    if not ok:
        raise ValueError(reason)


def identity(s):
    return (s.st_dev, s.st_ino, s.st_mode, s.st_size, s.st_mtime_ns, s.st_ctime_ns)


def record(path, sink=None):
    path = Path(path).absolute(); before = path.lstat()
    require(stat.S_ISREG(before.st_mode), 'Expected original regular file: ' + str(path))
    h, total = hashlib.sha256(), 0
    with os.fdopen(os.open(path, os.O_RDONLY | os.O_NOFOLLOW), 'rb') as stream:
        require(identity(os.fstat(stream.fileno())) == identity(before), 'Input replaced before read: ' + str(path))
        for block in iter(lambda: stream.read(CHUNK), b''):
            h.update(block); total += len(block)
            if sink is not None:
                sink.write(block)
        require(identity(os.fstat(stream.fileno())) == identity(before), 'Input changed while read: ' + str(path))
    require(identity(path.lstat()) == identity(before) and total == before.st_size, 'Input changed after read: ' + str(path))
    return {'path': str(path), 'bytes': total, 'sha256': h.hexdigest(), 'unixMode': before.st_mode & 0xffff,
            'inputIdentity': list(identity(before))}


def load(path):
    def unique(pairs):
        d = {}
        for key, value in pairs:
            require(key not in d, 'Duplicate JSON key: ' + key); d[key] = value
        return d
    return json.loads(Path(path).read_bytes(), object_pairs_hook=unique,
                      parse_constant=lambda x: require(False, 'Nonfinite JSON: ' + x))


def write_json(path, value):
    with path.open('x', encoding='utf-8') as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2, allow_nan=False); stream.write('\n')


def fixed_originals(original):
    require({p.name for p in original.iterdir()} == set(EXPECTED), 'Original Delivery1 member set differs')
    files = {}
    for name, (size, sha) in EXPECTED.items():
        row = record(original / name)
        require((row['bytes'], row['sha256']) == (size, sha), 'Immutable Delivery1 identity differs: ' + name)
        files[name] = row
    m = load(original / 'package-manifest076.json')
    require(m['schema'] == '076-delivery-packages-v1' and m['sourceSnapshot'] == SNAPSHOT
            and m['runtimeCommit'] == RUNTIME and m['sourceFingerprint'] == FP and m['runtimeInputCount'] == 774
            and m['appVersion'] == VERSION and m['buildGuid'] == GUID
            and m['status'] == 'COMPLETE_BUT_SIZE_LIMIT_EXCEEDED', 'Original package manifest identity/status differs')
    require(len(m['sizeLimitExceeded']) == 1 and Path(m['sizeLimitExceeded'][0]).resolve() == original / 'evidence.zip',
            'Original exceeded-size set differs; do not reinterpret another failure')
    for name in COUNTS:
        declared = m['packages'][name]; actual = files[name + '.zip']
        require((declared['bytes'], declared['sha256']) == (actual['bytes'], actual['sha256'])
                and declared['memberCount'] == COUNTS[name], 'Original package record differs: ' + name)
    return files, m


def original_audit(json_path, markdown_path, original, originals):
    refs = {'json': record(json_path), 'markdown': record(markdown_path)}
    for key, expected in AUDITS.items():
        require((refs[key]['bytes'], refs[key]['sha256']) == expected, 'Original closed audit changed: ' + key)
    a = load(json_path)
    require(a['schema'] == 'independent-package076-audit-v1' and a['status'] == 'FAIL'
            and a['error'] == SIZE_ERROR and a['finishedUtc'] and a['sourceSnapshot'] == SNAPSHOT
            and a['runtimeCommit'] == RUNTIME and a['sourceFingerprint'] == FP and a['expectedBuildGuid'] == GUID,
            'Original audit is not the fixed closed size-only failure')
    require(a['checks']['tests']['totalPassed'] == 1795 and a['checks']['sourceSnapshot']['runtimeInputCount'] == 774,
            'Original formal-test/source identity differs')
    require(set(a['checks']['archives']) == set(COUNTS), 'Original audit did not reach every archive')
    checked = {}
    for name, count in COUNTS.items():
        row = a['checks']['archives'][name]; original_row = originals[name + '.zip']
        require(row['members'] == count and all(row[k] is True for k in
                ('crcAllMembersReadToEof', 'centralDirectoryAndEof', 'safeUniqueNames', 'allMemberBytesShaModeCompared')),
                'Original audit member/CRC/EOF/mode proof incomplete: ' + name)
        require(Path(row['file']['path']).resolve() == original / (name + '.zip')
                and all(row['file'][k] == original_row[k] for k in ('bytes', 'sha256')),
                'Original audit ZIP byte binding differs: ' + name)
        checked[name] = {'members': count, 'originalWholeSha256': original_row['sha256'],
                         'originalFullCrcEofShaModesPassed': True}
    return {'records': refs, 'status': 'FAIL', 'soleError': SIZE_ERROR, 'allFourArchiveChecks': checked,
            'failurePreservedNotReclassified': True}


def original_checksums(path, files):
    checks = {}
    for line in path.read_text('ascii').splitlines():
        require(len(line) > 66 and line[64:66] == '  ', 'Malformed original checksum row')
        sha, name = line[:64], line[66:]
        require(len(sha) == 64 and all(c in '0123456789abcdef' for c in sha) and name in EXPECTED
                and name not in checks, 'Unsafe/duplicate original checksum row')
        checks[name] = sha
    require(set(checks) == set(EXPECTED) - {'checksums.sha256'}, 'Original checksum membership differs')
    require(all(files[n]['sha256'] == sha for n, sha in checks.items()), 'Original checksum value differs')
    return checks


def fresh_file(path, mode):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, mode & 0o7777)
    os.fchmod(fd, mode & 0o7777)
    return os.fdopen(fd, 'wb')


def copy_exact(source, destination, expected):
    with fresh_file(destination, expected['unixMode']) as sink:
        actual_source = record(source, sink=sink)
    require(actual_source == expected, 'Original changed during exact copy: ' + str(source))
    copied = record(destination)
    require(all(copied[k] == expected[k] for k in ('bytes', 'sha256', 'unixMode')), 'Copied original differs: ' + destination.name)
    return copied


def split_exact(source, output, expected):
    before = source.lstat(); total = 0; full = hashlib.sha256(); parts = []
    with os.fdopen(os.open(source, os.O_RDONLY | os.O_NOFOLLOW), 'rb') as stream:
        require(identity(os.fstat(stream.fileno())) == identity(before), 'Evidence replaced before split')
        for order in (1, 2):
            name = 'evidence.zip.part' + str(order).zfill(2); remaining = min(PART_BYTES, expected['bytes'] - total)
            require(remaining > 0, 'Empty transport part')
            offset = total; part_hash = hashlib.sha256()
            with fresh_file(output / name, expected['unixMode']) as sink:
                while remaining:
                    block = stream.read(min(CHUNK, remaining)); require(block, 'Original evidence ended early')
                    sink.write(block); full.update(block); part_hash.update(block)
                    total += len(block); remaining -= len(block)
            row = record(output / name)
            require(row['sha256'] == part_hash.hexdigest(), 'Transport part readback SHA differs')
            parts.append({'order': order, 'offset': offset, 'name': name, **row})
        require(not stream.read(1) and total == expected['bytes'] and full.hexdigest() == expected['sha256'],
                'Split does not equal the complete original evidence ZIP')
        require(identity(os.fstat(stream.fileno())) == identity(before), 'Evidence changed during split')
    require(identity(source.lstat()) == identity(before), 'Evidence changed after split')
    require([p['bytes'] for p in parts] == [PART_BYTES, 54981686], 'Declared two-part sizes differ')
    return parts


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original', type=Path, default=ROOT / 'Delivery1')
    parser.add_argument('--output', type=Path, default=ROOT / 'DeliveryTransport1')
    parser.add_argument('--original-audit', type=Path, default=ROOT / 'PackageAudit1/audit.json')
    parser.add_argument('--original-audit-markdown', type=Path, default=ROOT / 'PackageAudit1/audit.md')
    args = parser.parse_args(); original = args.original.resolve(strict=True); output = args.output.resolve(strict=False)
    require(original == ROOT / 'Delivery1', 'Transport is fixed to immutable Candidate1/Delivery1')
    require(not output.exists() and output.parent == ROOT and output.name != 'Diagnostics',
            'Transport output must be a fresh direct Candidate1 child outside Diagnostics')
    audit_json = args.original_audit.resolve(strict=True); audit_md = args.original_audit_markdown.resolve(strict=True)
    require(audit_json == ROOT / 'PackageAudit1/audit.json' and audit_md == ROOT / 'PackageAudit1/audit.md',
            'Original audit must be the declared closed PackageAudit1 files')
    originals, package = fixed_originals(original)
    audit = original_audit(audit_json, audit_md, original, originals)
    checksum_values = original_checksums(original / 'checksums.sha256', originals)
    helpers = {name: record(ROOT / name) for name in ('prepare-transport076.py', 'independent-transport076-audit.py')}
    output.mkdir(exist_ok=False)
    copied = {}
    for name in sorted(set(EXPECTED) - {'evidence.zip'}):
        copied[name] = copy_exact(original / name, output / name, originals[name])
    parts = split_exact(original / 'evidence.zip', output, originals['evidence.zip'])
    readme = output / 'README-076.md'
    with readme.open('x', encoding='utf-8') as stream:
        stream.write('# Frontier Reckoning 0.19.0 — 下载与资料说明\n\n')
        stream.write('这是原始本地交付的无损传输层，不是新构建。source.zip、macOS.zip、normal.zip 可直接解压使用；其余原清单和 README.md 保持原始字节。\n\n')
        stream.write('## 重建完整证据 ZIP\n\n必须下载 evidence.zip.part01 与 evidence.zip.part02，并放在同一目录。两个文件是顺序二进制片段，不是独立 ZIP；不要分别解压，也不要交换顺序。\n\n```sh\n')
        stream.write('cat evidence.zip.part01 evidence.zip.part02 > evidence.zip\nshasum -a 256 evidence.zip\n```\n\n')
        stream.write('预期完整文件大小：122090550 字节。预期 SHA-256：\n\n`' + EXPECTED['evidence.zip'][1] + '`\n\n')
        stream.write('核对后可解压 evidence.zip；在十二项全部下载且证据重建完成后，运行 `shasum -a 256 -c checksums.sha256` 核对原来的八项校验。checksums.sha256 是原件，不含新分片、此说明或 transport manifest；新传输文件的 SHA 见 transport-manifest076.json。重建前请先确认同目录没有需要保留的 evidence.zip，避免 shell 重定向覆盖同名文件。\n\n')
        stream.write('## 如实保留的交付状态\n\n原 Delivery1 打包 exit2：evidence.zip 大于严格的100MiB单件门槛。原独立审计 status FAIL，唯一错误是 `' + SIZE_ERROR + '`。四个 ZIP 的全成员 CRC、EOF、SHA、Unix模式和原始输入对照均已完成；该体积失败没有被删除、重命名为通过或放宽阈值。原完整 evidence.zip 留在本地，不重压、不裁剪。\n\n')
        stream.write('当前传输仅复制八个原件并按64MiB顺序拆分 evidence.zip，使每个传输文件均小于100MiB。独立 transport 审计须另行执行；本说明和 prepare receipt 本身不代表独立审计已通过，也不代表云端回读完成。\n\n')
        stream.write('## 资料边界\n\n源代码快照 `' + SNAPSHOT + '`；运行时代码 `' + RUNTIME + '`；774项输入；版本0.19.0；构建GUID `' + GUID + '`。完整回归为1795项；原失败尝试、普通观察的封顶未胜局、原生首局失败和未执行计划均在原始证据中保留。\n\n')
        stream.write('此传输层证明可无损重建原始字节，不增加整体目标完成、原生人类胜局、FPS性能验收、医护跨河/上下船生命周期、双物理Mac或075存档迁移结论。有关实际玩法、冷恢复和回放的结论，以原始各有界审计为准。没有合并主分支、公开发布、上传或变更云端共享权限。\n')
    inventory = {name: record(output / name) for name in sorted(copied)}
    inventory.update({p['name']: record(output / p['name']) for p in parts})
    inventory[readme.name] = record(readme)
    require(len(inventory) == 11 and all(row['bytes'] < LIMIT for row in inventory.values()), 'Transport object count/size limit differs')
    # Re-read every original, audit and helper. Only new output files were written.
    require(fixed_originals(original)[0] == originals, 'Original delivery changed across transport preparation')
    require(original_audit(audit_json, audit_md, original, originals) == audit, 'Original audit changed across preparation')
    require({n: record(ROOT / n) for n in helpers} == helpers, 'Transport helpers changed across preparation')
    manifest = {
        'schema': '076-lossless-transport-v1', 'status': 'PREPARED_LOCAL_TRANSPORT_BYTES_NOT_INDEPENDENT_AUDIT',
        'createdUtc': datetime.now(timezone.utc).isoformat(), 'sourceSnapshot': SNAPSHOT, 'runtimeCommit': RUNTIME,
        'sourceFingerprint': FP, 'runtimeInputCount': 774, 'appVersion': VERSION, 'buildGuid': GUID,
        'originalDeliveryDirectory': str(original), 'originalDeliveryStatus': package['status'],
        'originalPackagerExitCode': 2, 'originalAudit': audit, 'originalFiles': originals,
        'helpers': helpers, 'copiedOriginalFiles': copied,
        'fullEvidenceZip': originals['evidence.zip'], 'parts': parts, 'splitPartBytes': PART_BYTES,
        'partCount': 2, 'partsAreStandaloneZipFiles': False, 'recompressionOrTrimming': False,
        'reconstructionCommand': 'cat evidence.zip.part01 evidence.zip.part02 > evidence.zip',
        'reconstructionShaCommand': 'shasum -a 256 evidence.zip',
        'originalChecksumCommandAfterReconstruction': 'shasum -a 256 -c checksums.sha256',
        'originalChecksumValues': checksum_values, 'outputFileCountIncludingManifest': 12,
        'outputInventoryExcludingManifest': inventory, 'manifestSelfHashIncluded': False,
        'maxTransportBytesExclusive': LIMIT, 'originalDeliveryAndAuditsUnchanged': True,
        'scope': 'Eight exact original copies, two ordered binary fragments, README-076 and this manifest. No archive/member bytes are altered; no cloud or new runtime acceptance is inferred.',
        'boundaries': ['Original size failure remains a failure of Delivery1; this is a separate transport layer.',
                       'Independent transport audit must run separately and read the virtual concatenated ZIP through all members/CRC/EOF.',
                       'No whole-goal, human victory, FPS, medical river/transport lifecycle, two-physical-Mac or 075-save migration acceptance claim.',
                       'No source, Diagnostics, original delivery, closed audit, Git remote, application runtime or cloud sharing state was modified.']}
    predicted = len((json.dumps(manifest, ensure_ascii=False, indent=2, allow_nan=False) + '\n').encode())
    require(predicted < LIMIT, 'Transport manifest exceeds strict single-object limit')
    write_json(output / 'transport-manifest076.json', manifest)
    expected_names = set(inventory) | {'transport-manifest076.json'}
    require({p.name for p in output.iterdir()} == expected_names and len(expected_names) == 12,
            'Actual twelve-file transport output set differs')
    for name in sorted(expected_names):
        row = record(output / name); require(row['bytes'] < LIMIT, 'Actual transport object exceeds strict limit: ' + name)
        print(json.dumps({'output': row['path'], 'bytes': row['bytes'], 'sha256': row['sha256']}), flush=True)
    return 0


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (ValueError, OSError, KeyError, TypeError) as error:
        print('076 lossless transport preparation failed: ' + str(error), file=sys.stderr)
        sys.exit(1)
