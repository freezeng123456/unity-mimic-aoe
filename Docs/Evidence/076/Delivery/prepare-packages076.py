#!/usr/bin/env python3
"""Prepare four immutable 076 delivery ZIPs only after an explicit snapshot SHA.

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
import tarfile
import xml.etree.ElementTree as ET
import unicodedata
import zipfile

CANDIDATE = Path(__file__).resolve().parent
REPO = Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
RUNTIME = 'b1a9b6a977a60b63cd3e9f9632179c1787f3d00d'
FINGERPRINT = 'dabf34d26490cece872959764b8b9e515749584b180f3d7899f38d79b69a6f8f'
GUID = 'a86c515f3ab040649ea505e507193ff3'
VERSION = '0.19.0'
SOURCE_COUNT = 774
LIMIT = 100 * 1024 * 1024
CHUNK = 1024 * 1024
RUNTIME_ROOTS = ('Assets', 'Packages', 'ProjectSettings', 'Tools')
SOURCE_ALLOWED = ('Assets', 'Packages', 'ProjectSettings', 'Docs',
                  'SourceAssets', 'Tools', 'README.md', '.gitignore')
DOCS = ('README.md', 'Docs/Content-076.md', 'Docs/Content-076-Contract.md',
        'Docs/Player-Guide-076.md', 'Docs/Verification-076.md',
        'Docs/Acceptance-Matrix-076.md', 'Docs/Ordinary-076-Results.md',
        'Docs/Native-076.md', 'Docs/Completion-Status.md',
        'Docs/Known-Issues.md', 'Docs/AoE3-Gap-Roadmap.md')
OPTIONAL_DOCS = ('Docs/Delivery-076.md',)
DECLARATIONS = ('normal-observation076-contract.md', 'run076.py',
                'launch-native076.py', 'prepare-packages076.py',
                'independent-package076-audit.py')
EXPECTED_RUNS = {
    'Focused1': ('3d0849cf53886a21e8d915e9106a1d85afeae51f72472958346a0f1639f47192', [2]),
    'Focused2': ('8b33e0973f1ef67273dc7e6acdb28bfeced58c534bfcf55978ab9e9cd4230fd8', [0, 2]),
    'Focused3': ('51103973c58821571bc79da2b4e931801e0dce9c71bd262be513ebd236bba49a', [0, 0]),
    'Focused4': (FINGERPRINT, [0, 0]), 'Full1': (FINGERPRINT, [0]),
    'Build1': (FINGERPRINT, [0]), 'NormalObserved1': (FINGERPRINT, [0])}



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
            '076 source count/fingerprint differs from the frozen 774-input candidate')
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
                   or p.relative_to(evidence).parts[0] == '076')
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
    r = load(path / 'runner-receipt076.json')
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
    for name in ('Focused1', 'Focused2', 'Focused3', 'Focused4', 'Full1'):
        folder = CANDIDATE / name
        r = receipt(folder, final=name in ('Focused4', 'Full1'))
        merge(members, tree(folder, 'Evidence076/RawRuns/' + name))
        runs[name] = {'runner': read_file(folder / 'runner-receipt076.json'),
                      'exitCode': r['exitCode'], 'sourceFingerprint': r['sourceFingerprintAfter']}
        if name == 'Full1':
            actual = report_directory(folder, r, repo)
            require(all((actual / n).is_file() for n in
                        ('EditMode.xml', 'EditMode.log', 'PlayMode.xml', 'PlayMode.log', 'source-evidence.json')),
                    'Actual full report files missing')
            merge(members, tree(actual, 'Evidence076/ActualFullReports/' + name))
            runs[name]['actualReportsDirectory'] = str(actual)
    receipt(CANDIDATE / 'Build1', final=True)
    for file in sorted((CANDIDATE / 'Build1').iterdir()):
        if file.is_file():
            one_file(members, file, 'Evidence076/Build1/' + file.name)
    merge(members, tree(CANDIDATE / 'Native1', 'Evidence076/Native1'))
    # All ordinary raw JSONL, samples, snapshots, reports and analysis are kept
    # both here and in the dedicated normal ZIP; no failure/history trimming.
    merge(members, tree(CANDIDATE / 'NormalObserved1', 'Evidence076/NormalObserved1'))
    for file in sorted((CANDIDATE / 'Diagnostics').rglob('*')):
        if file.is_file() and file.suffix in ('.json', '.md', '.py') and '__pycache__' not in file.parts:
            one_file(members, file, 'Evidence076/Diagnostics/' + file.relative_to(CANDIDATE / 'Diagnostics').as_posix())
    for name in DECLARATIONS:
        one_file(members, CANDIDATE / name, 'Evidence076/Declarations/' + name)
    for name in DOCS + tuple(n for n in OPTIONAL_DOCS if (repo / n).is_file()):
        one_file(members, repo / name, 'Evidence076/ProjectDocs/' + name)
    merge(members, tree(repo / 'Docs/Evidence/076', 'Evidence076/ProjectDocs/Docs/Evidence/076'))
    return members, runs


def verify_run_inputs(repo, runtime_map):
    out = {}
    for name, (fp, exits) in EXPECTED_RUNS.items():
        r = receipt(CANDIDATE / name, final=name in ('Focused4', 'Full1', 'Build1', 'NormalObserved1'))
        require(r['sourceFingerprintBefore'] == r['sourceFingerprintAfter'] == fp
                and [c['exitCode'] for c in r['commands']] == exits
                and r['exitCode'] == exits[-1] and r.get('changedSources') == [],
                'Declared historical/final runner identity differs: ' + name)
        if fp == FINGERPRINT:
            require(r['sourcesBefore'] == runtime_map and r['gitHeadBefore'] == RUNTIME,
                    'Final runner not bound to runtime commit/input map: ' + name)
        out[name] = {'receipt': read_file(CANDIDATE / name / 'runner-receipt076.json'),
                     'actualExitCodes': exits, 'sourceFingerprint': fp}
    full = receipt(CANDIDATE / 'Full1', final=True)
    directory = report_directory(CANDIDATE / 'Full1', full, repo)
    se = load(directory / 'source-evidence.json')
    require(se['sourceFingerprint'] == FINGERPRINT and se['files'] == runtime_map,
            'Actual full source-evidence differs')
    total = 0
    for mode, count in (('EditMode', 1347), ('PlayMode', 448)):
        xml = ET.parse(directory / (mode + '.xml')).getroot(); cases = xml.findall('.//test-case')
        require(xml.attrib['result'] == 'Passed' and int(xml.attrib['total']) == int(xml.attrib['passed']) == len(cases) == count
                and all(int(xml.attrib[n]) == 0 for n in ('failed', 'skipped', 'inconclusive'))
                and all(c.attrib['result'] == 'Passed' for c in cases), 'Formal full XML differs: ' + mode)
        require(full['tests'][mode] == xml.attrib and se['reports'][mode]['sha256'] == read_file(directory / (mode + '.xml'))['sha256'],
                'Formal full XML receipt/SHA differs: ' + mode)
        total += count
    require(total == 1795, 'Full total differs')
    observed = load(CANDIDATE / 'NormalObserved1/Observed/manifest.json')
    require(observed['status'] == 'closed_two_declared_ordinary_cases' and observed['sourceFingerprint'] == FINGERPRINT
            and observed['version'] == VERSION
            and [(c['id'], c['seed'], c['status']) for c in observed['cases']] == [
                ('frontier-ottoman-mirror42', 42, 'natural_terminal'),
                ('winding-ottoman-british44', 44, 'observation_cap')], 'Ordinary manifest identity differs')
    return out


def native_closed(path):
    path = Path(path).resolve(strict=True)
    require(path.is_relative_to(CANDIDATE / 'Diagnostics') or path.is_relative_to(CANDIDATE / 'Native1'),
            'Native lifecycle closure must be an original Candidate1 Diagnostics/Native1 JSON')
    r = load(path)
    require(r['status'] == 'closed' and r['runtimeCommit'] == RUNTIME
            and r['sourceFingerprint'] == FINGERPRINT and r['appVersion'] == VERSION and r['buildGuid'] == GUID,
            'Native lifecycle closure identity differs')
    pids = r['processPids']
    require(pids and len(pids) == len(set(pids)) and all(type(p) is int and p > 0 for p in pids),
            'Native closure requires distinct positive actual PIDs')
    for pid in pids:
        try:
            os.kill(pid, 0)
        except ProcessLookupError:
            continue
        raise ValueError('Declared native PID still exists: ' + str(pid))
    # Check every live command line for this exact build path. Do not kill it.
    live = subprocess.check_output(['/bin/ps', '-axo', 'pid=,command=']).decode(errors='replace')
    exact_exe = str(CANDIDATE / 'Build1/Frontier Reckoning.app/Contents/MacOS/Frontier Reckoning')
    require(not any(exact_exe in line for line in live.splitlines()), 'Exact owned app is still running')
    require(r.get('auditPaths'), 'Closure must name actual final bounded audits')
    audits = []
    for value in r['auditPaths']:
        audit = Path(value).resolve(strict=True)
        require(audit.is_relative_to(CANDIDATE / 'Diagnostics') and audit.suffix in ('.json', '.md'),
                'Native audit path must be existing Candidate1 Diagnostics JSON/MD')
        audits.append(read_file(audit))
    return {'closure': read_file(path), 'processPidsVerifiedAbsent': pids, 'audits': audits,
            'scope': 'Lifecycle/source/app closure only; gameplay conclusions remain those of named bounded native audits.'}


def bind_actual_build(application):
    m = load(CANDIDATE / 'Build1/actual-app-manifest076.json')
    require(m['version'] == VERSION and m['buildGuid'] == GUID and m['sourceFingerprint'] == FINGERPRINT
            and m['sourceHead'] == RUNTIME
            and m['runnerSha256'] == read_file(CANDIDATE / 'Build1/runner-receipt076.json')['sha256'],
            'Original Build1 app manifest identity differs')
    actual = {n[len('Frontier Reckoning.app/'):]: {'bytes': r['bytes'], 'sha256': r['sha256']}
              for n, r in application.items() if r['type'] == 'file'}
    declared = {r['path']: {'bytes': r['bytes'], 'sha256': r['sha256']} for r in m['files']}
    require(len(declared) == len(m['files']) == m['fileCount'] == 298
            and m['totalFileBytes'] == 191062651 == sum(r['bytes'] for r in actual.values())
            and actual == declared, 'Current actual app bytes differ from the original Build1 manifest')
    return read_file(CANDIDATE / 'Build1/actual-app-manifest076.json')


def source_git_archive(repo, snapshot, output, records):
    """Actual Git tar stream -> portable ZIP, with no extraction or source reads."""
    relatives = [n[len('FrontierReckoning/'):] for n in sorted(records)]
    command = ['git', '--no-replace-objects', '-C', str(repo), '-c', 'tar.umask=0022',
               'archive', '--format=tar', '--prefix=FrontierReckoning/', snapshot, '--'] + relatives
    p = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                         env=dict(os.environ, GIT_OPTIONAL_LOCKS='0'))
    seen = set()
    try:
        with tarfile.open(fileobj=p.stdout, mode='r|') as tar, zipfile.ZipFile(output, 'x', zipfile.ZIP_DEFLATED,
                    compresslevel=6, allowZip64=False) as archive:
            for member in tar:
                name = safe(member.name.rstrip('/') + ('/' if member.isdir() else ''))
                if member.isdir():
                    continue  # Directory entries are not part of the portable-source file allowlist.
                require(member.isfile() and name in records and name not in seen,
                        'Git archive emitted unexpected/duplicate/nonregular member: ' + name)
                seen.add(name); item = records[name]
                require(member.size == item['bytes'] and (stat.S_IFREG | member.mode) == item['unixMode'],
                        'Git archive size/Unix mode differs: ' + name)
                info = zipfile.ZipInfo(name, (2026, 10, 10, 0, 0, 0)); info.create_system = 3
                info.external_attr = item['unixMode'] << 16; info.compress_type = zipfile.ZIP_DEFLATED
                h, size = hashlib.sha256(), 0
                with tar.extractfile(member) as src, archive.open(info, 'w') as sink:
                    for block in iter(lambda: src.read(CHUNK), b''):
                        h.update(block); size += len(block); sink.write(block)
                require(size == item['bytes'] and h.hexdigest() == item['sha256'], 'Git archive bytes differ: ' + name)
        require(seen == set(records), 'Git archive omitted allowlisted source files')
        # Consume tar padding so the read-only Git producer reaches its real exit.
        for _ in iter(lambda: p.stdout.read(CHUNK), b''):
            pass
    finally:
        p.stdout.close()
        if sys.exc_info()[0] is not None:
            p.terminate()
        code = p.wait(); error = p.stderr.read().decode(errors='replace'); p.stderr.close()
    require(code == 0, 'Git archive failed: ' + error[:1000])



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
    parser.add_argument('--output', type=Path, default=CANDIDATE / 'Delivery1')
    parser.add_argument('--native-closure', required=True, type=Path, help='Closed native lifecycle JSON, with fixed identity, exited processPids and existing bounded auditPaths')
    args = parser.parse_args(); repo = args.repo.resolve(strict=True); output = args.output.resolve(strict=False)
    require(not output.exists(), 'Refusing existing output directory: ' + str(output))
    require(not output.is_relative_to(repo) and not CANDIDATE.is_relative_to(output), 'Output must be outside repository and must not contain candidate inputs')
    require(not output.is_relative_to(CANDIDATE) or output.parent == CANDIDATE,
            'Output inside candidate must be a fresh direct child, never inside a closed evidence input tree')
    clean_snapshot(repo, args.snapshot); runtime_map = source_fingerprint(repo)
    source = source_selection(repo); bind_source_git(repo, args.snapshot, source)
    runners = verify_run_inputs(repo, runtime_map)
    native = native_closed(args.native_closure)
    evidence, runs = evidence_selection(repo)
    receipt(CANDIDATE / 'NormalObserved1', final=True)
    normal = tree(CANDIDATE / 'NormalObserved1', 'NormalObserved1')
    app = CANDIDATE / 'Build1/Frontier Reckoning.app'; application = tree(app, app.name)
    original_build_manifest = bind_actual_build(application)
    require(('build-guid=' + GUID) in (app / 'Contents/Resources/Data/boot.config').read_text(), 'Actual app GUID differs')
    plist = plistlib.loads((app / 'Contents/Info.plist').read_bytes())
    require(plist['CFBundleShortVersionString'] == VERSION, 'Actual app version differs')
    executable = app / 'Contents/MacOS' / plist['CFBundleExecutable']
    require(executable.lstat().st_mode & 0o111, 'Actual executable lacks Unix execute permission')
    for name, records in (('source', source), ('evidence', evidence), ('normal', normal), ('app', application)):
        print(json.dumps({'preflightInventory': name, 'members': len(records),
                          'fileBytes': sum(i['bytes'] for i in records.values())}), flush=True)
    output.mkdir(parents=True, exist_ok=False)
    source_git_archive(repo, args.snapshot, output / 'source.zip', source)
    json_write(output / 'source.manifest.json', {
        'archive': 'source.zip', 'sha256': read_file(output / 'source.zip')['sha256'],
        'files': len(source), 'bytes': (output / 'source.zip').stat().st_size,
        'sourceHashes': {str(Path(i['originalPath']).relative_to(repo)): i['sha256'] for i in source.values()},
        'includedEvidenceVersion': '076', 'sourceSnapshot': args.snapshot,
        'method': 'Actual git archive tar stream, verified against Git blobs and clean working tree, converted to file-only ZIP without extraction',
        'excludes': ['Library', 'Logs', 'Temp', 'obj', 'Builds', 'UserSettings', 'work',
                     'Docs/Evidence releases other than 076']})
    require(not (output / 'macOS.zip').exists(), 'Refusing existing macOS.zip')
    subprocess.run(['/usr/bin/ditto', '-c', '-k', '--keepParent', '--norsrc', '--noextattr',
                    str(app), str(output / 'macOS.zip')], check=True)
    write_zip(output / 'evidence.zip', evidence); write_zip(output / 'normal.zip', normal)
    packages = {name: verify_zip(output / (name + '.zip'), records) for name, records in
                (('source', source), ('macOS', application), ('evidence', evidence), ('normal', normal))}
    sm = load(output / 'source.manifest.json')
    require(sm['includedEvidenceVersion'] == '076' and sm['sha256'] == packages['source']['sha256']
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
    require(native_closed(args.native_closure) == native, 'Native closure changed during packaging')
    common = {'schema': '076-delivery-packages-v1', 'sourceSnapshot': args.snapshot,
              'runtimeCommit': RUNTIME, 'sourceFingerprint': FINGERPRINT,
              'runtimeInputCount': SOURCE_COUNT, 'appVersion': VERSION, 'buildGuid': GUID}
    app_manifest = output / 'actual-app-manifest076.json'
    json_write(app_manifest, {**common, 'actualAppOriginalPath': str(app), 'members': application,
                             'sourceUnchangedAcrossPackaging': True})
    readme = output / 'README.md'
    with readme.open('x', encoding='utf-8') as stream:
        stream.write('# Frontier Reckoning 0.19.0 — local candidate delivery\n\n')
        stream.write('Source snapshot: `' + args.snapshot + '`. Runtime commit: `' + RUNTIME + '`.\n\n')
        stream.write('Runtime inputs: 774; fingerprint `' + FINGERPRINT + '`; app GUID `' + GUID + '`.\n\n')
        for name, p in packages.items():
            stream.write('- `' + name + '.zip`: ' + str(p['bytes']) + ' bytes; SHA-256 `' + p['sha256'] + '`.\n')
        stream.write('\nFull1: 1347 EditMode + 448 PlayMode = 1795 passing tests. Focused1/2 original failures and Focused3/4 reruns are preserved, not relabeled.\n')
        stream.write('\nThe ordinary archive retains both declared observations, including the natural terminal frontier match and the capped nonwinning winding match. Native originals retain failed first match, paid second match, cold/replay records, and bounded audit conclusions. Otto native plan remains unexecuted unless actual receipts show otherwise.\n')
        stream.write('\nThis package proves local byte, membership, CRC, Unix mode and snapshot identity only. It makes no whole-goal completion, native human victory, performance/FPS acceptance, medical river/transport lifecycle acceptance, two-physical-Mac or 075-save migration claim. No upload, sharing change or Git publication is performed.\n')
        stream.write('\nOriginal filenames and full raw observations, captures, logs, saves and replay records are preserved. Source is an actual git archive stream converted to a portable ZIP, not a rebuilt client. macOS.zip preserves the actual Build1 app.\n')
    sidecars = {p.name: read_file(p) for p in (output / 'source.manifest.json', app_manifest, readme)}
    over = [p['originalPath'] for p in packages.values() if p['bytes'] >= LIMIT]
    over += [p['originalPath'] for p in sidecars.values() if p['bytes'] >= LIMIT]
    manifest_path = output / 'package-manifest076.json'
    summary = {**common, 'createdUtc': datetime.now(timezone.utc).isoformat(),
               'status': 'PASS' if not over else 'COMPLETE_BUT_SIZE_LIMIT_EXCEEDED',
               'scope': 'Four local archives; membership, original bytes/SHA/Unix modes, CRC and EOF only. No upload/runtime acceptance claim.',
               'packages': packages, 'sidecars': sidecars, 'runs': runs,
               'runnerIdentity': runners, 'nativeLifecycleClosure': native,
               'originalBuildManifest': original_build_manifest,
               'sourceMapSidecar': 'source.manifest.json', 'sourceProfile': 'Actual git archive tar stream for exact portable allowlist; per-file Git blob SHA/size/mode binding; only Docs/Evidence/076',
               'evidenceProfile': 'Focused1-4/Full1 complete raw folders and actual Full1 reports; Build1 root files; Native1 complete including unsuccessful first case; NormalObserved1 full raw+analysis; Diagnostics JSON/MD/Python; named declarations/docs; ONLY Docs/Evidence/076 complete.',
               'normalProfile': 'Complete NormalObserved1 lstat tree; no trimming, replay/event/snapshot/sample omission.',
               'noOriginalChanges': True, 'cleanSnapshotBeforeAndAfter': True,
               'manifestSelfHashIncluded': False, 'maxBytesExclusive': LIMIT,
               'sizeLimitExceeded': over}
    # Size is known before the single exclusive write. No self hash is included.
    predicted = len((json.dumps(summary, ensure_ascii=False, indent=2, allow_nan=False) + '\n').encode())
    if predicted >= LIMIT:
        over.append(str(manifest_path)); summary['status'] = 'COMPLETE_BUT_SIZE_LIMIT_EXCEEDED'
        print('package-manifest076.json exceeds 100MiB; no archive/member was trimmed', file=sys.stderr)
    json_write(manifest_path, summary)
    with (output / 'checksums.sha256').open('x', encoding='ascii') as stream:
        for name in ('README.md', 'source.zip', 'macOS.zip', 'evidence.zip', 'normal.zip',
                     'source.manifest.json', 'actual-app-manifest076.json', 'package-manifest076.json'):
            stream.write(read_file(output / name)['sha256'] + '  ' + name + '\n')
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
    except (ValueError, OSError, KeyError, TypeError, ET.ParseError, tarfile.TarError, subprocess.CalledProcessError, zipfile.BadZipFile) as error:
        print('076 packaging refused/failed: ' + str(error), file=sys.stderr)
        sys.exit(1)
