#!/usr/bin/env python3
"""Prepare four immutable 075 delivery ZIPs only after an explicit snapshot SHA.

This external helper never launches Unity/the client, changes Git/source/evidence,
or uploads. Merely creating this helper is not a packaging/acceptance receipt.
Run only after the parent has explicitly selected and committed the snapshot.
An existing output directory is always refused, including failed partial runs.
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
import unicodedata
import zipfile

CANDIDATE = Path(__file__).resolve().parent
REPO = Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
RUNTIME = '51bd61ff1abc92f224dd449784671234a20a5555'
FINGERPRINT = 'a3e1e3518a23d000f6ce1e80bef628f72bd55c631fcb9ddcd6e9f2be80066594'
GUID = '67594b51e2dd40f593ec8e956f6f4960'
VERSION = '0.18.0'
SOURCE_COUNT = 758
LIMIT = 100 * 1024 * 1024
CHUNK = 1024 * 1024
RUNTIME_ROOTS = ('Assets', 'Packages', 'ProjectSettings', 'Tools')
SOURCE_ALLOWED = ('Assets', 'Packages', 'ProjectSettings', 'Docs',
                  'SourceAssets', 'Tools', 'README.md', '.gitignore')
DOCS = ('README.md', 'Docs/Content-075.md', 'Docs/Content-075-Contract.md',
        'Docs/Player-Guide-075.md', 'Docs/Verification-075.md',
        'Docs/Native-075.md', 'Docs/Delivery-075.md',
        'Docs/Completion-Status.md', 'Docs/Known-Issues.md',
        'Docs/AoE3-Gap-Roadmap.md')
DECLARATIONS = ('predeclaration075.md', 'amendment075-guardians-and-interface.md',
                'normal-observation075-contract.md', 'run075.py',
                'launch-native075.py', 'prepare-packages075.py')


def require(ok, reason):
    if not ok:
        raise ValueError(reason)


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def identity(s):
    return (s.st_dev, s.st_ino, s.st_mode, s.st_size, s.st_mtime_ns, s.st_ctime_ns)


def read_file(path, sink=None, collect=False):
    """Hash exact regular-file bytes and detect mutation/replacement while read."""
    path = Path(path).absolute()
    before = path.lstat()
    require(stat.S_ISREG(before.st_mode), 'Not a regular input: ' + str(path))
    h, size, raw = hashlib.sha256(), 0, bytearray() if collect else None
    with os.fdopen(os.open(path, os.O_RDONLY | os.O_NOFOLLOW), 'rb') as stream:
        require(identity(os.fstat(stream.fileno())) == identity(before), 'Input changed while opening: ' + str(path))
        for block in iter(lambda: stream.read(CHUNK), b''):
            h.update(block); size += len(block)
            if sink is not None:
                sink.write(block)
            if raw is not None:
                raw.extend(block)
        require(identity(os.fstat(stream.fileno())) == identity(before)
                and identity(path.lstat()) == identity(before) and size == before.st_size,
                'Input changed while reading: ' + str(path))
    item = {'originalPath': str(path), 'type': 'file', 'bytes': size,
            'sha256': h.hexdigest(), 'unixMode': before.st_mode & 0xffff,
            'unixModeOctal': oct(before.st_mode & 0xffff),
            'inputIdentity': list(identity(before))}
    return (bytes(raw), item) if collect else item


def load(path):
    raw, _ = read_file(path, collect=True)
    def pairs(values):
        d = {}
        for k, v in values:
            require(k not in d, 'Duplicate JSON property: ' + k); d[k] = v
        return d
    return json.loads(raw, object_pairs_hook=pairs,
                      parse_constant=lambda x: require(False, 'Nonfinite JSON: ' + x))


def git(repo, *args):
    env = dict(os.environ, GIT_OPTIONAL_LOCKS='0')
    return subprocess.check_output(['git', '--no-replace-objects', '-C', str(repo), *args], env=env)


def source_fingerprint(repo):
    files = {}
    for top in RUNTIME_ROOTS:
        for folder, dirs, names in os.walk(repo / top):
            dirs[:] = sorted(d for d in dirs if not d.startswith('.') and d != '__pycache__')
            for name in sorted(names):
                if name.startswith('.'):
                    continue
                path = Path(folder) / name
                files[path.relative_to(repo).as_posix()] = read_file(path)['sha256']
    fp = digest(json.dumps(files, sort_keys=True, separators=(',', ':')).encode())
    require(len(files) == SOURCE_COUNT and fp == FINGERPRINT,
            '075 source count/fingerprint differs from the frozen 758-input candidate')
    return files


def clean_snapshot(repo, snapshot):
    require(re.fullmatch(r'[0-9a-f]{40}', snapshot or ''), '--snapshot requires a full lowercase 40-character Git SHA')
    require(git(repo, 'rev-parse', '--verify', snapshot + '^{commit}').decode().strip() == snapshot,
            'Snapshot is not the exact requested commit')
    require(git(repo, 'rev-parse', 'HEAD').decode().strip() == snapshot, 'HEAD is not --snapshot')
    require(not git(repo, 'status', '--porcelain=v1', '-z', '--untracked-files=all'),
            'Tracked changes or untracked files remain; commit the reviewed snapshot first')
    subprocess.run(['git', '--no-replace-objects', '-C', str(repo), 'merge-base', '--is-ancestor', RUNTIME, snapshot], check=True)


def safe(name):
    parts = name.rstrip('/').split('/')
    require(name and not name.startswith('/') and '\\' not in name
            and all(p not in ('', '.', '..') for p in parts)
            and not any(ord(c) < 32 or ord(c) == 127 for c in name), 'Unsafe ZIP member: ' + repr(name))
    return name


def tree(directory, prefix):
    """Complete lstat tree: regular files, real directories, and symlink text."""
    directory = Path(directory).absolute()
    require(stat.S_ISDIR(directory.lstat().st_mode), 'Not a real input directory: ' + str(directory))
    records = {}
    def walk(path, name):
        before = path.lstat(); mode = before.st_mode & 0xffff
        if stat.S_ISREG(mode):
            item = read_file(path)
        elif stat.S_ISLNK(mode):
            target = os.readlink(path); raw = os.fsencode(target)
            require(target and not os.path.isabs(target), 'Absolute/empty symlink: ' + str(path))
            resolved = path.resolve(strict=True)
            require(resolved.is_relative_to(directory), 'Symlink escapes its original tree: ' + str(path))
            item = {'originalPath': str(path), 'type': 'symlink', 'bytes': len(raw),
                    'sha256': digest(raw), 'symlinkTarget': target, 'unixMode': mode,
                    'unixModeOctal': oct(mode), 'inputIdentity': list(identity(before))}
        elif stat.S_ISDIR(mode):
            name += '/'
            item = {'originalPath': str(path), 'type': 'directory', 'bytes': 0,
                    'sha256': digest(b''), 'unixMode': mode, 'unixModeOctal': oct(mode),
                    'inputIdentity': list(identity(before))}
            records[safe(name)] = item
            with os.scandir(path) as scan:
                children = sorted(e.name for e in scan)
            for child in children:
                walk(path / child, name + child)
            require(identity(path.lstat()) == identity(before), 'Directory changed during inventory: ' + str(path))
            return
        else:
            raise ValueError('Unsupported input node: ' + str(path))
        require(identity(path.lstat()) == identity(before), 'Input changed during inventory: ' + str(path))
        records[safe(name)] = item
    walk(directory, prefix)
    namespace(records)
    return records


def namespace(records):
    aliases = set()
    for name in records:
        safe(name)
        alias = unicodedata.normalize('NFC', name.rstrip('/')).casefold()
        require(alias not in aliases, 'Duplicate/case/Unicode ZIP alias: ' + name)
        aliases.add(alias)


def merge(target, additions):
    require(not set(target) & set(additions), 'Duplicate planned archive member')
    target.update(additions); namespace(target)


def one_file(records, path, name):
    merge(records, {safe(name): read_file(path)})


def source_selection(repo):
    paths = []
    for top in SOURCE_ALLOWED:
        entry = repo / top
        if entry.is_file():
            paths.append(entry)
        elif entry.is_dir():
            paths.extend(p for p in entry.rglob('*') if p.is_file()
                         and not p.name.startswith('.') and '__pycache__' not in p.parts)
    evidence = repo / 'Docs/Evidence'
    paths = sorted(p for p in paths if not p.is_relative_to(evidence)
                   or p.relative_to(evidence).parts[0] == '075')
    require(len(paths) == len(set(paths)), 'Duplicate source selection')
    records = {'FrontierReckoning/' + p.relative_to(repo).as_posix(): read_file(p) for p in paths}
    namespace(records)
    return records


def bind_source_git(repo, snapshot, records):
    """Reject ignored extras and verify every packaged source byte against Git."""
    entries = {}
    for row in git(repo, 'ls-tree', '-r', '-z', '--full-tree', snapshot).split(b'\0'):
        if not row:
            continue
        metadata, path = row.split(b'\t', 1)
        mode, kind, oid = metadata.decode().split()
        entries[path.decode()] = (mode, kind, oid)
    env = dict(os.environ, GIT_OPTIONAL_LOCKS='0')
    proc = subprocess.Popen(['git', '--no-replace-objects', '-C', str(repo), 'cat-file', '--batch'],
                            stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, env=env)
    try:
        for member, item in records.items():
            relative = str(Path(item['originalPath']).relative_to(repo))
            require(relative in entries, 'Source allowlist contains a file absent from snapshot: ' + relative)
            mode, kind, oid = entries[relative]
            require(kind == 'blob' and mode in ('100644', '100755'), 'Source packager cannot preserve this Git node: ' + relative)
            require(bool(item['unixMode'] & 0o111) == (mode == '100755'), 'Source executable mode differs from Git: ' + relative)
            proc.stdin.write((oid + '\n').encode()); proc.stdin.flush()
            header = proc.stdout.readline().split()
            require(len(header) == 3 and header[0].decode() == oid and header[1] == b'blob', 'Unexpected Git blob framing')
            remaining = int(header[2]); size = remaining; h = hashlib.sha256()
            address = hashlib.sha1(); address.update(b'blob ' + str(size).encode() + b'\0')
            while remaining:
                block = proc.stdout.read(min(CHUNK, remaining)); require(block, 'Git blob EOF')
                remaining -= len(block); h.update(block); address.update(block)
            require(proc.stdout.read(1) == b'\n' and address.hexdigest() == oid, 'Git blob address mismatch')
            require(size == item['bytes'] and h.hexdigest() == item['sha256'], 'Source bytes differ from snapshot: ' + relative)
            item.update(gitBlob=oid, gitTreeMode=mode)
    finally:
        proc.stdin.close()
        if sys.exc_info()[0] is not None:
            proc.terminate()
        code = proc.wait(); error = proc.stderr.read().decode(errors='replace')
        proc.stdout.close(); proc.stderr.close()
    require(code == 0, 'Git blob reader failed: ' + error[:1000])


def receipt(path, final=False):
    r = load(path / 'runner-receipt075.json')
    require(r.get('status') == 'closed' and r.get('sourceStable') is True,
            'Runner not actually closed with stable sources: ' + str(path))
    require(all(c.get('status') == 'closed' and c.get('finishedUtc')
                and isinstance(c.get('exitCode'), int) for c in r['commands']), 'Child process not closed: ' + str(path))
    if final:
        require(r['exitCode'] == 0 and r['sourceFingerprintBefore'] == r['sourceFingerprintAfter'] == FINGERPRINT,
                'Final runner belongs to a failed/different source: ' + str(path))
    return r


def report_directory(path, r, repo):
    value = r.get('reportsDirectory')
    if not value:
        text, _ = read_file(path / 'stdout-0.log', collect=True)
        found = re.findall(r'^This run writes fresh results to: (.+)$', text.decode(), re.M)
        require(len(found) == 1, 'Failed full run lacks unique actual reportsDirectory stdout declaration')
        value = found[0].strip()
    result = Path(value).absolute()
    require(result.resolve(strict=True).is_relative_to((repo / 'Builds/Tests').resolve()), 'Full report directory outside actual Tests root')
    return result


def evidence_selection(repo):
    members, runs = {}, {}
    for name in tuple('Focused' + str(i) for i in range(1, 7)) + ('Full1', 'Full2'):
        folder = CANDIDATE / name; r = receipt(folder, final=name in ('Focused6', 'Full2'))
        merge(members, tree(folder, 'Evidence075/RawRuns/' + name))
        runs[name] = {'runner': read_file(folder / 'runner-receipt075.json'), 'exitCode': r['exitCode'],
                      'sourceFingerprint': r['sourceFingerprintAfter']}
        if name.startswith('Full'):
            actual = report_directory(folder, r, repo)
            required = ('EditMode.xml', 'EditMode.log') if name == 'Full1' else (
                'EditMode.xml', 'EditMode.log', 'PlayMode.xml', 'PlayMode.log', 'source-evidence.json')
            require(all((actual / n).is_file() for n in required), 'Actual full report files missing: ' + name)
            merge(members, tree(actual, 'Evidence075/ActualFullReports/' + name))
            runs[name]['actualReportsDirectory'] = str(actual)
    receipt(CANDIDATE / 'Build1', final=True)
    for file in sorted((CANDIDATE / 'Build1').iterdir()):
        if file.is_file():
            one_file(members, file, 'Evidence075/Build1/' + file.name)
    merge(members, tree(CANDIDATE / 'Native1', 'Evidence075/Native1'))
    for file in sorted((CANDIDATE / 'Diagnostics').rglob('*')):
        if file.is_file() and file.suffix in ('.json', '.md', '.py') and '__pycache__' not in file.parts:
            one_file(members, file, 'Evidence075/Diagnostics/' + file.relative_to(CANDIDATE / 'Diagnostics').as_posix())
    for name in DECLARATIONS:
        one_file(members, CANDIDATE / name, 'Evidence075/Declarations/' + name)
    for name in DOCS:
        one_file(members, repo / name, 'Evidence075/ProjectDocs/' + name)
    merge(members, tree(repo / 'Docs/Evidence/075', 'Evidence075/ProjectDocs/Docs/Evidence/075'))
    return members, runs


def write_zip(path, members):
    with zipfile.ZipFile(path, 'x', zipfile.ZIP_DEFLATED, compresslevel=6, allowZip64=False) as archive:
        for name, item in sorted(members.items()):
            info = zipfile.ZipInfo(name, (2026, 10, 9, 0, 0, 0)); info.create_system = 3
            info.external_attr = item['unixMode'] << 16
            info.compress_type = zipfile.ZIP_DEFLATED
            if item['type'] == 'directory':
                info.external_attr |= 0x10; archive.writestr(info, b'')
            elif item['type'] == 'symlink':
                archive.writestr(info, os.fsencode(item['symlinkTarget']))
            else:
                with archive.open(info, 'w') as sink:
                    actual = read_file(item['originalPath'], sink=sink)
                require(actual == {k: item[k] for k in actual}, 'Input changed while writing: ' + item['originalPath'])


def zip_eof(path):
    size = path.stat().st_size
    with path.open('rb') as stream:
        stream.seek(max(0, size - 65557)); tail = stream.read()
    at = tail.rfind(b'PK\x05\x06'); require(at >= 0 and at + 22 <= len(tail), 'ZIP EOCD missing/truncated')
    fields = struct.unpack_from('<4s4H2IH', tail, at)
    _, disk, cd_disk, on_disk, total, cd_size, cd_offset, comment = fields
    require(disk == cd_disk == 0 and on_disk == total and total != 65535
            and cd_size != 0xffffffff and cd_offset != 0xffffffff, 'Unsupported split/ZIP64 archive')
    require(at + 22 + comment == len(tail) and cd_offset + cd_size == size - len(tail) + at,
            'ZIP central directory/EOF mismatch or trailing bytes')
    return total


def verify_zip(path, expected):
    count = zip_eof(path)
    with zipfile.ZipFile(path) as archive:
        infos = archive.infolist(); names = [i.filename for i in infos]
        require(count == len(infos) == len(set(names)) and set(names) == set(expected), 'ZIP duplicate/missing/extra member')
        namespace({n: None for n in names})
        for info in infos:
            item = expected[info.filename]
            require(info.create_system == 3 and ((info.external_attr >> 16) & 0xffff) == item['unixMode'],
                    'ZIP Unix type/permissions differ: ' + info.filename)
            require(not info.flag_bits & 1, 'Encrypted ZIP member')
            h, length = hashlib.sha256(), 0
            with archive.open(info) as stream:
                for block in iter(lambda: stream.read(CHUNK), b''):
                    h.update(block); length += len(block)
            require(length == info.file_size == item['bytes'] and h.hexdigest() == item['sha256'],
                    'ZIP member bytes/SHA differ: ' + info.filename)
        require(archive.testzip() is None, 'ZIP CRC failure')
    return {**read_file(path), 'memberCount': len(expected), 'crcAllMembersPassed': True,
            'eofPassed': True, 'duplicateMembers': False, 'bytesShaUnixModeAllMembersPassed': True,
            'members': expected, 'below100MiB': path.stat().st_size < LIMIT}


def unchanged(records):
    for item in records.values():
        path = Path(item['originalPath'])
        require(identity(path.lstat()) == tuple(item['inputIdentity']), 'Original input changed: ' + str(path))
        if item['type'] == 'file':
            actual = read_file(path)
            require(all(actual[k] == item[k] for k in actual), 'Original file bytes changed: ' + str(path))
        elif item['type'] == 'symlink':
            require(os.readlink(path) == item['symlinkTarget'], 'Original symlink changed: ' + str(path))


def json_write(path, value):
    with path.open('x', encoding='utf-8') as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2, allow_nan=False); stream.write('\n')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--snapshot', required=True, help='Explicitly approved full source snapshot commit SHA; must equal clean HEAD')
    parser.add_argument('--repo', type=Path, default=REPO)
    parser.add_argument('--output', type=Path, default=CANDIDATE / 'DeliveryFinal1')
    args = parser.parse_args(); repo = args.repo.resolve(strict=True); output = args.output.resolve(strict=False)
    require(not output.exists(), 'Refusing existing output directory: ' + str(output))
    require(not output.is_relative_to(repo) and not CANDIDATE.is_relative_to(output), 'Output must be outside repository and must not contain candidate inputs')
    require(not output.is_relative_to(CANDIDATE) or output.parent == CANDIDATE,
            'Output inside candidate must be a fresh direct child, never inside a closed evidence input tree')
    clean_snapshot(repo, args.snapshot); runtime_map = source_fingerprint(repo)
    source = source_selection(repo); bind_source_git(repo, args.snapshot, source)
    evidence, runs = evidence_selection(repo)
    receipt(CANDIDATE / 'NormalObserved1', final=True)
    normal = tree(CANDIDATE / 'NormalObserved1', 'NormalObserved1')
    app = CANDIDATE / 'Build1/Frontier Reckoning.app'; application = tree(app, app.name)
    require(('build-guid=' + GUID) in (app / 'Contents/Resources/Data/boot.config').read_text(), 'Actual app GUID differs')
    plist = plistlib.loads((app / 'Contents/Info.plist').read_bytes())
    require(plist['CFBundleShortVersionString'] == VERSION, 'Actual app version differs')
    executable = app / 'Contents/MacOS' / plist['CFBundleExecutable']
    require(executable.lstat().st_mode & 0o111, 'Actual executable lacks Unix execute permission')
    for name, records in (('source', source), ('evidence', evidence), ('normal', normal), ('app', application)):
        print(json.dumps({'preflightInventory': name, 'members': len(records),
                          'fileBytes': sum(i['bytes'] for i in records.values())}), flush=True)
    output.mkdir(parents=True, exist_ok=False)
    subprocess.run([sys.executable, str(repo / 'Tools/package-source.py'), '--output', str(output / 'source.zip'),
                    '--evidence-version', '075'], check=True)
    require(not (output / 'macOS.zip').exists(), 'Refusing existing macOS.zip')
    subprocess.run(['/usr/bin/ditto', '-c', '-k', '--keepParent', '--norsrc', '--noextattr',
                    str(app), str(output / 'macOS.zip')], check=True)
    write_zip(output / 'evidence.zip', evidence); write_zip(output / 'normal.zip', normal)
    packages = {name: verify_zip(output / (name + '.zip'), records) for name, records in
                (('source', source), ('macOS', application), ('evidence', evidence), ('normal', normal))}
    sm = load(output / 'source.manifest.json')
    require(sm['includedEvidenceVersion'] == '075' and sm['sha256'] == packages['source']['sha256']
            and sm['sourceHashes'] == {str(Path(i['originalPath']).relative_to(repo)): i['sha256'] for i in source.values()},
            'Actual existing source.manifest.json differs from selected source bytes')
    for records in (source, evidence, normal, application):
        unchanged(records)
    require(source_fingerprint(repo) == runtime_map, 'Runtime sources changed during packaging')
    require(source_selection(repo) == {n: {k: v for k, v in i.items() if k not in ('gitBlob', 'gitTreeMode')}
                                      for n, i in source.items()}, 'Source selection changed during packaging')
    require(tree(app, app.name) == application and tree(CANDIDATE / 'NormalObserved1', 'NormalObserved1') == normal,
            'App/normal tree changed during packaging')
    evidence_after, _ = evidence_selection(repo)
    require(evidence_after == evidence, 'Evidence tree changed during packaging')
    clean_snapshot(repo, args.snapshot)
    common = {'schema': '075-delivery-packages-v1', 'sourceSnapshot': args.snapshot,
              'runtimeCommit': RUNTIME, 'sourceFingerprint': FINGERPRINT,
              'runtimeInputCount': SOURCE_COUNT, 'appVersion': VERSION, 'buildGuid': GUID}
    app_manifest = output / 'actual-app-manifest075.json'
    json_write(app_manifest, {**common, 'actualAppOriginalPath': str(app), 'members': application,
                             'sourceUnchangedAcrossPackaging': True})
    sidecars = {p.name: read_file(p) for p in (output / 'source.manifest.json', app_manifest)}
    over = [p['originalPath'] for p in packages.values() if p['bytes'] >= LIMIT]
    over += [p['originalPath'] for p in sidecars.values() if p['bytes'] >= LIMIT]
    manifest_path = output / 'package-manifest075.json'
    summary = {**common, 'createdUtc': datetime.now(timezone.utc).isoformat(),
               'status': 'PASS' if not over else 'COMPLETE_BUT_SIZE_LIMIT_EXCEEDED',
               'scope': 'Four local archives; membership, original bytes/SHA/Unix modes, CRC and EOF only. No upload/runtime acceptance claim.',
               'packages': packages, 'sidecars': sidecars, 'runs': runs,
               'sourceMapSidecar': 'source.manifest.json', 'sourceProfile': 'Unmodified Tools/package-source.py --evidence-version 075 allowlist',
               'evidenceProfile': 'Focused1-6/Full1-2 complete raw folders and actual full reports; Build1 root files; Native1 complete; Diagnostics JSON/MD/Python; named declarations; named current docs; ONLY Docs/Evidence/075 complete.',
               'normalProfile': 'Complete NormalObserved1 lstat tree; no trimming, replay/event/snapshot/sample omission.',
               'noOriginalChanges': True, 'cleanSnapshotBeforeAndAfter': True,
               'manifestSelfHashIncluded': False, 'maxBytesExclusive': LIMIT,
               'sizeLimitExceeded': over}
    # Size is known before the single exclusive write. No self hash is included.
    predicted = len((json.dumps(summary, ensure_ascii=False, indent=2, allow_nan=False) + '\n').encode())
    if predicted >= LIMIT:
        over.append(str(manifest_path)); summary['status'] = 'COMPLETE_BUT_SIZE_LIMIT_EXCEEDED'
        print('package-manifest075.json exceeds 100MiB; no archive/member was trimmed', file=sys.stderr)
    json_write(manifest_path, summary)
    for path in sorted(output.iterdir()):
        if path.is_file():
            print(json.dumps({'output': str(path), 'bytes': path.stat().st_size,
                              'sha256': read_file(path)['sha256']}), flush=True)
    if over:
        print(json.dumps({'sizeLimitExceeded': over, 'policy': 'All complete outputs preserved; no silent deletion or trimming.'}), file=sys.stderr)
        return 2
    return 0


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (ValueError, OSError, subprocess.CalledProcessError, zipfile.BadZipFile) as error:
        print('075 packaging refused/failed: ' + str(error), file=sys.stderr)
        sys.exit(1)
