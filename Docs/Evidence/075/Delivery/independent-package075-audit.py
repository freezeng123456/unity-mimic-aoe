#!/usr/bin/env python3
"""Independent, read-only 075 package audit. Does not build, extract or execute.

Run only after the packager has closed. JSON/Markdown outputs must be fresh and
outside the repository and Delivery directory. This script does not import the
packager or accept its PASS flags as proof. Reads every ZIP member through EOF
(therefore checking CRC), recomputes SHA, and independently inventories inputs.
"""
import argparse
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
import subprocess
import sys
import unicodedata
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parent
REPO = Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
SNAPSHOT = '67b13e77d47ccdf648a5e6113a43b2a5eb50bb0a'
RUNTIME = '51bd61ff1abc92f224dd449784671234a20a5555'
FP = 'a3e1e3518a23d000f6ce1e80bef628f72bd55c631fcb9ddcd6e9f2be80066594'
GUID = '67594b51e2dd40f593ec8e956f6f4960'
VERSION = '0.18.0'
CHUNK = 1024 * 1024
DOCS = ('README.md', 'Docs/Content-075.md', 'Docs/Content-075-Contract.md',
        'Docs/Player-Guide-075.md', 'Docs/Verification-075.md', 'Docs/Native-075.md',
        'Docs/Delivery-075.md', 'Docs/Completion-Status.md', 'Docs/Known-Issues.md',
        'Docs/AoE3-Gap-Roadmap.md')
DECLARATIONS = ('predeclaration075.md', 'amendment075-guardians-and-interface.md',
                'normal-observation075-contract.md', 'run075.py',
                'launch-native075.py', 'prepare-packages075.py')


def require(ok, message):
    if not ok:
        raise ValueError(message)


def strict_json(raw):
    def pairs(values):
        out = {}
        for key, value in values:
            require(key not in out, 'Repeated JSON key: ' + key)
            out[key] = value
        return out
    return json.loads(raw, object_pairs_hook=pairs,
                      parse_constant=lambda x: require(False, 'Nonfinite JSON: ' + x))


def load(path):
    return strict_json(Path(path).read_bytes())


def fs_identity(s):
    return (s.st_dev, s.st_ino, s.st_mode, s.st_size, s.st_mtime_ns, s.st_ctime_ns)


def file_record(path):
    path = Path(path)
    first = path.lstat()
    require(stat.S_ISREG(first.st_mode), 'Expected regular file: ' + str(path))
    h, size = hashlib.sha256(), 0
    with os.fdopen(os.open(path, os.O_RDONLY | os.O_NOFOLLOW), 'rb') as stream:
        require(fs_identity(os.fstat(stream.fileno())) == fs_identity(first), 'Input replaced: ' + str(path))
        for block in iter(lambda: stream.read(CHUNK), b''):
            h.update(block)
            size += len(block)
        require(fs_identity(os.fstat(stream.fileno())) == fs_identity(first), 'Input changed while hashing: ' + str(path))
    require(fs_identity(path.lstat()) == fs_identity(first) and size == first.st_size, 'Input changed: ' + str(path))
    return dict(type='file', bytes=size, sha256=h.hexdigest(), unixMode=first.st_mode & 0xffff,
                originalPath=str(path.absolute()))


def ref(path):
    row = file_record(path)
    return {'path': row['originalPath'], 'bytes': row['bytes'], 'sha256': row['sha256']}


def safe_name(name):
    require(isinstance(name, str) and name and not name.startswith('/') and '\\' not in name,
            'Unsafe/absolute archive name: ' + repr(name))
    parts = name.rstrip('/').split('/')
    require(all(p not in ('', '.', '..') for p in parts) and not re.match(r'^[A-Za-z]:', name)
            and not any(ord(c) < 32 or ord(c) == 127 for c in name), 'Unsafe archive components: ' + repr(name))
    return name


def relative_to(path, root):
    require(path.resolve().is_relative_to(root.resolve()), 'Path escapes approved input root: ' + str(path))


def tree(path, prefix):
    """Complete independent lstat inventory, including empty dirs and links."""
    path = Path(path).absolute()
    require(stat.S_ISDIR(path.lstat().st_mode), 'Expected real directory: ' + str(path))
    out = {}
    def walk(p, name):
        s = p.lstat()
        if stat.S_ISDIR(s.st_mode):
            row = dict(type='directory', bytes=0, sha256=hashlib.sha256(b'').hexdigest(),
                       unixMode=s.st_mode & 0xffff, originalPath=str(p))
            out[safe_name(name + '/')] = row
            for child in sorted(p.iterdir()):
                walk(child, name + '/' + child.name)
        elif stat.S_ISLNK(s.st_mode):
            value = os.readlink(p)
            require(value and not os.path.isabs(value), 'Unsafe absolute/empty symlink: ' + str(p))
            resolved = p.resolve(strict=True)
            require(resolved.is_relative_to(path), 'Escaping symlink: ' + str(p))
            raw = os.fsencode(value)
            out[safe_name(name)] = dict(type='symlink', bytes=len(raw), sha256=hashlib.sha256(raw).hexdigest(),
                                        unixMode=s.st_mode & 0xffff, originalPath=str(p), symlinkTarget=value)
        elif stat.S_ISREG(s.st_mode):
            out[safe_name(name)] = file_record(p)
        else:
            raise ValueError('Unsupported node: ' + str(p))
        require(fs_identity(p.lstat()) == fs_identity(s), 'Tree changed during audit: ' + str(p))
    walk(path, prefix)
    return out


def merge(target, extra):
    require(not (target.keys() & extra.keys()), 'Overlapping independently selected archive inputs')
    target.update(extra)


def git(repo, *args):
    return subprocess.check_output(['git', '--no-replace-objects', '-C', str(repo), *args],
                                   env=dict(os.environ, GIT_OPTIONAL_LOCKS='0'))


def selected_source(path):
    p = PurePosixPath(path)
    roots = {'Assets', 'Packages', 'ProjectSettings', 'Docs', 'SourceAssets', 'Tools'}
    if path in ('README.md', '.gitignore'):
        return True
    if p.parts[0] not in roots or p.name.startswith('.') or '__pycache__' in p.parts:
        return False
    return not (p.parts[:2] == ('Docs', 'Evidence') and (len(p.parts) < 3 or p.parts[2] != '075'))


class GitBlobs:
    def __init__(self, repo):
        self.p = subprocess.Popen(['git', '--no-replace-objects', '-C', str(repo), 'cat-file', '--batch'],
                                  stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                  env=dict(os.environ, GIT_OPTIONAL_LOCKS='0'))
        self.cache = {}

    def read(self, oid):
        if oid in self.cache:
            return self.cache[oid]
        self.p.stdin.write((oid + '\n').encode())
        self.p.stdin.flush()
        head = self.p.stdout.readline().split()
        require(len(head) == 3 and head[0].decode() == oid and head[1] == b'blob', 'Git blob framing differs')
        size = int(head[2]); remaining = size; h = hashlib.sha256()
        address = hashlib.sha1(b'blob ' + str(size).encode() + b'\0')
        while remaining:
            block = self.p.stdout.read(min(CHUNK, remaining))
            require(block, 'Truncated Git blob')
            remaining -= len(block); h.update(block); address.update(block)
        require(self.p.stdout.read(1) == b'\n' and address.hexdigest() == oid, 'Git blob address mismatch')
        self.cache[oid] = (size, h.hexdigest())
        return self.cache[oid]

    def close(self, failed=False):
        self.p.stdin.close()
        if failed:
            self.p.terminate()
        code = self.p.wait()
        error = self.p.stderr.read().decode(errors='replace')
        self.p.stdout.close(); self.p.stderr.close()
        if not failed:
            require(code == 0, 'Read-only Git blob process failed: ' + error[:200])


def source_inventory(repo, snapshot):
    require(git(repo, 'rev-parse', snapshot + '^{commit}').decode().strip() == snapshot, 'Wrong snapshot commit')
    require(git(repo, 'rev-parse', RUNTIME + '^{commit}').decode().strip() == RUNTIME, 'Runtime commit missing')
    subprocess.run(['git', '--no-replace-objects', '-C', str(repo), 'merge-base', '--is-ancestor', RUNTIME, snapshot],
                   check=True, env=dict(os.environ, GIT_OPTIONAL_LOCKS='0'))
    out, runtime = {}, {}
    blobs = GitBlobs(repo)
    try:
        for line in git(repo, 'ls-tree', '-r', '-z', '--full-tree', snapshot).split(b'\0'):
            if not line:
                continue
            info, encoded = line.split(b'\t', 1)
            path = encoded.decode('utf-8'); mode, kind, oid = info.decode().split()
            if not selected_source(path):
                continue
            require(kind == 'blob' and mode in ('100644', '100755'), 'Unsupported portable-source Git node: ' + path)
            size, sha = blobs.read(oid)
            out['FrontierReckoning/' + safe_name(path)] = dict(type='file', bytes=size, sha256=sha,
                gitBlob=oid, gitTreeMode=mode, executable=mode == '100755', originalPath=str(repo / path))
            parts = PurePosixPath(path).parts
            if parts[0] in ('Assets', 'Packages', 'ProjectSettings', 'Tools') and not any(x.startswith('.') for x in parts):
                runtime[path] = sha
        blobs.close()
    except BaseException:
        blobs.close(failed=True)
        raise
    actual_fp = hashlib.sha256(json.dumps(runtime, sort_keys=True, separators=(',', ':')).encode()).hexdigest()
    require(len(runtime) == 758 and actual_fp == FP, 'Snapshot does not contain the frozen 758 runtime inputs')
    # Read the current 758 inputs once, independently of package/runner claims.
    current = {}
    for top in ('Assets', 'Packages', 'ProjectSettings', 'Tools'):
        for folder, dirs, names in os.walk(repo / top):
            dirs[:] = [x for x in dirs if not x.startswith('.') and x != '__pycache__']
            for name in names:
                if not name.startswith('.'):
                    p = Path(folder) / name
                    current[p.relative_to(repo).as_posix()] = file_record(p)['sha256']
    require(current == runtime, 'Current runtime files differ from the snapshot/tested inputs')
    return out, runtime


def eocd(path):
    size = path.stat().st_size
    with path.open('rb') as f:
        f.seek(max(0, size - 65557)); data = f.read()
    pos = data.rfind(b'PK\x05\x06')
    require(pos >= 0 and len(data) - pos >= 22, 'Missing ZIP EOCD: ' + str(path))
    _, disk, cddisk, diskcount, total, cdsize, offset, comment = struct.unpack_from('<4s4H2IH', data, pos)
    require(disk == cddisk == 0 and diskcount == total and total != 65535 and cdsize != 0xffffffff and offset != 0xffffffff,
            'Unexpected split/ZIP64 package; this declared four-package profile uses non-ZIP64 ZIPs')
    require(pos + 22 + comment == len(data), 'Trailing bytes after ZIP EOCD/comment')
    absolute = size - len(data) + pos
    require(offset + cdsize == absolute, 'ZIP central directory offset/length does not reach EOCD')
    return total


def verify_archive(path, expected, declared):
    before = path.lstat(); count = eocd(path)
    whole = file_record(path)
    require(whole['bytes'] == declared['bytes'] and whole['sha256'] == declared['sha256'], 'Whole ZIP SHA/bytes differ: ' + path.name)
    require(set(declared['members']) == set(expected), 'Manifest member set differs from independent inputs: ' + path.name)
    results, aliases, types = {}, set(), Counter()
    with zipfile.ZipFile(path, 'r') as z:
        entries = z.infolist()
        names = [i.filename for i in entries]
        require(count == len(names) == len(set(names)) and set(names) == set(expected), 'ZIP omitted/extra/duplicate members: ' + path.name)
        for i in entries:
            name = safe_name(i.filename)
            alias = unicodedata.normalize('NFC', name.rstrip('/')).casefold()
            require(alias not in aliases, 'Case/Unicode duplicate archive path: ' + name)
            aliases.add(alias)
            require(not i.flag_bits & 1 and i.create_system == 3, 'Encrypted/non-Unix ZIP member: ' + name)
            row = expected[name]; claim = declared['members'][name]; mode = (i.external_attr >> 16) & 0xffff
            require(mode == claim['unixMode'] and oct(mode) == claim['unixModeOctal'], 'Manifest Unix mode differs: ' + name)
            required_type = {'file': stat.S_IFREG, 'directory': stat.S_IFDIR, 'symlink': stat.S_IFLNK}[row['type']]
            require(stat.S_IFMT(mode) == required_type and i.is_dir() == (row['type'] == 'directory'), 'ZIP node type differs: ' + name)
            if 'unixMode' in row:
                require(mode == row['unixMode'], 'Actual input Unix type/permissions differ: ' + name)
            else:
                require(bool(mode & 0o111) == row['executable'] and mode & 0o777 in (0o644, 0o755), 'Source Git executable/mode differs: ' + name)
            require(claim['type'] == row['type'] and claim['bytes'] == row['bytes'] and claim['sha256'] == row['sha256'], 'Manifest differs from actual independent input: ' + name)
            require(Path(claim['originalPath']).absolute() == Path(row['originalPath']).absolute(), 'Manifest original path differs: ' + name)
            if row['type'] == 'symlink':
                require(claim.get('symlinkTarget') == row['symlinkTarget'], 'Manifest symlink target differs: ' + name)
            if 'gitBlob' in row:
                require(claim.get('gitBlob') == row['gitBlob'] and claim.get('gitTreeMode') == row['gitTreeMode'], 'Manifest Git binding differs: ' + name)
            h, total = hashlib.sha256(), 0
            with z.open(i, 'r') as stream:
                for block in iter(lambda: stream.read(CHUNK), b''):
                    total += len(block); h.update(block)
            # ZipExtFile verifies actual CRC on reaching EOF; every member is read.
            require(total == i.file_size == row['bytes'] and h.hexdigest() == row['sha256'], 'Actual ZIP member differs: ' + name)
            if row['type'] == 'directory':
                require(total == 0, 'Nonempty directory member')
            types[row['type']] += 1
            results[name] = dict(type=row['type'], bytes=total, sha256=h.hexdigest(), unixMode=mode)
    require(fs_identity(path.lstat()) == fs_identity(before), 'ZIP changed during audit')
    require(declared['memberCount'] == len(results), 'Manifest member count differs')
    return {'file': ref(path), 'members': len(results), 'types': dict(types),
            'uncompressedBytes': sum(x['bytes'] for x in results.values()),
            'memberInventorySha256': hashlib.sha256(json.dumps(results, sort_keys=True, separators=(',', ':')).encode()).hexdigest(),
            'crcAllMembersReadToEof': True, 'centralDirectoryAndEof': True,
            'safeUniqueNames': True, 'allMemberBytesShaModeCompared': True}


def full_reports(candidate, repo, name):
    runner = load(candidate / name / 'runner-receipt075.json')
    value = runner.get('reportsDirectory')
    if not value:
        matches = re.findall(r'^This run writes fresh results to: (.+)$', (candidate/name/'stdout-0.log').read_text(), re.M)
        require(len(matches) == 1, 'Missing unique full-run report directory')
        value = matches[0].strip()
    path = Path(value)
    relative_to(path, repo/'Builds/Tests')
    return path


def evidence_inventory(candidate, repo, declared):
    out = {}
    for name in [f'Focused{i}' for i in range(1, 7)] + ['Full1', 'Full2']:
        merge(out, tree(candidate/name, 'Evidence075/RawRuns/' + name))
        if name.startswith('Full'):
            merge(out, tree(full_reports(candidate, repo, name), 'Evidence075/ActualFullReports/' + name))
    for p in sorted((candidate/'Build1').iterdir()):
        if p.is_file():
            out['Evidence075/Build1/' + p.name] = file_record(p)
    merge(out, tree(candidate/'Native1', 'Evidence075/Native1'))
    # Diagnostic additions made after packaging (including this audit's outputs)
    # are not retroactively required inside an immutable evidence ZIP. Every
    # advertised diagnostic member is still independently checked against disk.
    for name in declared['members']:
        if name.startswith('Evidence075/Diagnostics/'):
            safe_name(name)
            rel = name[len('Evidence075/Diagnostics/'):]
            p = candidate/'Diagnostics'/rel
            relative_to(p, candidate/'Diagnostics')
            require(p.suffix in ('.json', '.md', '.py') and '__pycache__' not in p.parts, 'Unexpected diagnostic profile member')
            out[name] = file_record(p)
    for name in DECLARATIONS:
        out['Evidence075/Declarations/' + name] = file_record(candidate/name)
    for name in DOCS:
        out['Evidence075/ProjectDocs/' + name] = file_record(repo/name)
    merge(out, tree(repo/'Docs/Evidence/075', 'Evidence075/ProjectDocs/Docs/Evidence/075'))
    return out


def runner_identity(candidate, runtime_map):
    summary = {}
    for name, exits in [('Focused6', [0, 0]), ('Full2', [0]), ('Build1', [0]), ('NormalObserved1', [0])]:
        p = candidate/name/'runner-receipt075.json';r = load(p)
        require(r['status'] == 'closed' and r['exitCode'] == 0 and r['sourceStable'] is True and r['changedSources'] == [], 'Unclosed/failed/unstable final runner: ' + name)
        require(r['sourceFingerprintBefore'] == r['sourceFingerprintAfter'] == FP and r['sourcesBefore'] == runtime_map,
                'Final runner does not bind the frozen runtime inputs: ' + name)
        require([x['exitCode'] for x in r['commands']] == exits and all(x['status'] == 'closed' and x['pid'] > 0 and x['finishedUtc'] for x in r['commands']), 'Child closure mismatch: ' + name)
        summary[name] = {'receipt': ref(p), 'actualExitCodes': exits, 'sourceStable': True}
    return summary


def tests_identity(candidate, repo, runtime_map):
    folder = full_reports(candidate, repo, 'Full2');e = load(folder/'source-evidence.json')
    r = load(candidate/'Full2/runner-receipt075.json')
    require(e['sourceFingerprint'] == FP and e['files'] == runtime_map, 'Full2 source-evidence differs from commit')
    result = {'sourceEvidence': ref(folder/'source-evidence.json'), 'modes': {}}
    for mode, number in [('EditMode', 1291), ('PlayMode', 432)]:
        p=folder/(mode+'.xml');x=ET.parse(p).getroot();cases=x.findall('.//test-case')
        require(x.attrib['result'] == 'Passed' and int(x.attrib['total']) == int(x.attrib['passed']) == len(cases) == number,
                'Wrong formal full-test counts: ' + mode)
        require(int(x.attrib['failed']) == int(x.attrib['skipped']) == int(x.attrib['inconclusive']) == 0 and all(c.attrib['result'] == 'Passed' for c in cases), 'Nonpassing full-test leaf')
        actual=ref(p)
        require(e['reports'][mode]['sha256'] == actual['sha256'] and r['tests'][mode] == x.attrib, 'Full-test XML receipt mismatch')
        result['modes'][mode] = {**actual, 'passed': number}
    result['totalPassed'] = 1723
    return result


def app_identity(app):
    boot=app/'Contents/Resources/Data/boot.config';info=app/'Contents/Info.plist'
    require(re.findall(r'^build-guid=([0-9a-f]{32})$',boot.read_text(),re.M)==[GUID], 'Actual app GUID differs')
    p=plistlib.loads(info.read_bytes());require(p['CFBundleShortVersionString']==VERSION, 'Actual app version differs')
    exe=app/'Contents/MacOS'/p['CFBundleExecutable']; mode=exe.lstat().st_mode
    with exe.open('rb') as stream:head=stream.read(8)
    require(stat.S_ISREG(mode) and mode & 0o111 and head[:4]==b'\xcf\xfa\xed\xfe' and struct.unpack('<I',head[4:])[0]==0x100000c, 'Client is not an executable thin arm64 Mach-O')
    return {'version':VERSION,'buildGuid':GUID,'bootConfig':ref(boot),'infoPlist':ref(info),'executable':ref(exe),'executed':False}


def native_copies(candidate, repo, evidence_members, source_members):
    p=repo/'Docs/Evidence/075/copy-index075.json';index=load(p);native=[]
    for row in index['copies']:
        if not row['copy'].startswith('Native/'):
            continue
        safe_name(row['copy']);safe_name(row['original'])
        original=candidate/row['original'];copy=repo/'Docs/Evidence/075'/row['copy']
        relative_to(original,candidate/'Native1');relative_to(copy,repo/'Docs/Evidence/075/Native')
        actual=file_record(original);archived=file_record(copy)
        require(actual['bytes']==archived['bytes']==row['bytes'] and actual['sha256']==archived['sha256']==row['sha256'], 'Native copy index differs: '+row['copy'])
        source_name='FrontierReckoning/Docs/Evidence/075/'+row['copy']
        evidence_name='Evidence075/ProjectDocs/Docs/Evidence/075/'+row['copy']
        original_name='Evidence075/'+row['original']
        for collection,name in [(source_members,source_name),(evidence_members,evidence_name),(evidence_members,original_name)]:
            require(name in collection and collection[name]['sha256']==row['sha256'], 'Native raw/copy missing from declared archive input: '+name)
        native.append(row['copy'])
    actual_files={p.relative_to(repo/'Docs/Evidence/075').as_posix() for p in (repo/'Docs/Evidence/075/Native').rglob('*') if p.is_file()}
    require(set(native)==actual_files and len(native)==len(set(native)), 'Native copy-index coverage differs')
    return {'copyIndex':ref(p),'files':len(native),'allNativeCopyBytesAndOriginalsMatched':True}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo',type=Path,default=REPO)
    parser.add_argument('--candidate',type=Path,default=ROOT)
    parser.add_argument('--delivery',type=Path,default=ROOT/'DeliveryFinal1')
    parser.add_argument('--snapshot',default=SNAPSHOT)
    parser.add_argument('--json',required=True,type=Path)
    parser.add_argument('--markdown',required=True,type=Path)
    args=parser.parse_args()
    require(args.snapshot==SNAPSHOT,'This independent audit is bound to the approved 075 source snapshot')
    require(args.json.resolve()!=args.markdown.resolve(),'Audit outputs must differ')
    for path in (args.json,args.markdown):
        require(not path.exists(),'Refusing existing audit output: '+str(path))
        require(not path.resolve().is_relative_to(args.repo.resolve()) and not path.resolve().is_relative_to(args.delivery.resolve()), 'Audit output must be outside repository/immutable delivery')
        require(path.parent.is_dir(),'Audit output parent must already exist')
    report={'schema':'independent-package075-audit-v1','startedUtc':datetime.now(timezone.utc).isoformat(),
            'auditor':ref(Path(__file__)),'sourceSnapshot':SNAPSHOT,'runtimeCommit':RUNTIME,'sourceFingerprint':FP,
            'expectedBuildGuid':GUID,'status':'FAIL','checks':{},'boundaries':[
                'Independent reviewer script; does not import the packager, modify archives, extract or execute application members, launch Unity, or upload.',
                'All ZIP bytes are read through EOF for actual CRC plus SHA; manifest flags alone are not accepted.',
                'Portable source is compared with independently read Git blobs/modes; app, Native1 and NormalObserved1 are compared with complete original lstat trees.',
                'Diagnostics membership is the immutable declared set, with every included diagnostic independently byte-checked; later audit receipts need not be inside earlier packages.',
                'No remote byte/permission, gameplay, playback, cold-restoration or native process-exit-code claim is inferred from package consistency.']}
    try:
        candidate=args.candidate.resolve(strict=True);repo=args.repo.resolve(strict=True);delivery=args.delivery.resolve(strict=True)
        manifest_path=delivery/'package-manifest075.json';m=load(manifest_path)
        require(m['schema']=='075-delivery-packages-v1' and m['sourceSnapshot']==SNAPSHOT and m['runtimeCommit']==RUNTIME and m['sourceFingerprint']==FP and m['runtimeInputCount']==758 and m['appVersion']==VERSION and m['buildGuid']==GUID, 'Package identity differs from independently fixed candidate')
        require(set(m['packages'])=={'source','macOS','evidence','normal'},'Wrong package set')
        source,runtime=source_inventory(repo,SNAPSHOT)
        report['checks']['sourceSnapshot']={'portableFiles':len(source),'runtimeInputCount':len(runtime),'gitBlobBytesAndModesRead':True,'currentRuntimeMatchesSnapshot':True}
        report['checks']['runners']=runner_identity(candidate,runtime)
        report['checks']['tests']=tests_identity(candidate,repo,runtime)
        app=candidate/'Build1/Frontier Reckoning.app'
        report['checks']['application']=app_identity(app)
        build_log=candidate/'Build1/build.log'
        succeeded=re.findall(r'^FRONTIER_BUILD Succeeded bytes=(\d+) path=(.+)$',build_log.read_text(),re.M)
        require(len(succeeded)==1 and int(succeeded[0][0])>0,'Build log lacks one actual Succeeded record')
        report['checks']['application']['buildLog']=ref(build_log)
        report['checks']['application']['reportedBuildBytes']=int(succeeded[0][0])
        inventories={'source':source,'macOS':tree(app,app.name),'evidence':evidence_inventory(candidate,repo,m['packages']['evidence']),
                     'normal':tree(candidate/'NormalObserved1','NormalObserved1')}
        report['checks']['nativeCopies']=native_copies(candidate,repo,inventories['evidence'],source)
        report['checks']['archives']={}
        for name in ('source','macOS','evidence','normal'):
            report['checks']['archives'][name]=verify_archive(delivery/(name+'.zip'),inventories[name],m['packages'][name])
            print(json.dumps({'checkedArchive':name,'members':report['checks']['archives'][name]['members']}),flush=True)
        sm=load(delivery/'source.manifest.json');source_hashes={n[len('FrontierReckoning/'):]:v['sha256'] for n,v in source.items()}
        require(sm['archive']=='source.zip' and sm['files']==len(source) and sm['sourceHashes']==source_hashes and sm['includedEvidenceVersion']=='075' and sm['sha256']==report['checks']['archives']['source']['file']['sha256'] and sm['bytes']==report['checks']['archives']['source']['file']['bytes'], 'Source sidecar does not match independently checked source ZIP')
        am=load(delivery/'actual-app-manifest075.json')
        require(all(am[k]==m[k] for k in ('schema','sourceSnapshot','runtimeCommit','sourceFingerprint','runtimeInputCount','appVersion','buildGuid')), 'App sidecar identity differs')
        require(set(am['members'])==set(inventories['macOS']), 'App sidecar membership differs')
        for name,row in inventories['macOS'].items():
            require(all(am['members'][name][k]==row[k] for k in ('type','bytes','sha256','unixMode','originalPath')), 'App sidecar differs from original tree: '+name)
            if row['type']=='symlink':require(am['members'][name]['symlinkTarget']==row['symlinkTarget'],'App sidecar link target differs')
        require(set(m['sidecars'])=={'source.manifest.json','actual-app-manifest075.json'},'Unexpected sidecars')
        for name,declared in m['sidecars'].items():
            actual=file_record(delivery/name)
            require(all(actual[k]==declared[k] for k in ('bytes','sha256','unixMode')),'Sidecar hash/mode differs: '+name)
        report['checks']['sidecars']={n:ref(delivery/n) for n in sorted(m['sidecars'])}
        require(all((delivery/n).stat().st_size<100*1024*1024 for n in ['source.zip','macOS.zip','evidence.zip','normal.zip','source.manifest.json','actual-app-manifest075.json','package-manifest075.json']), 'Actual artifact exceeds declared 100MiB delivery limit')
        report['checks']['packageManifest']=ref(manifest_path)
        report['status']='PASS_LOCAL_PACKAGE_CONSISTENCY'
    except (ValueError,OSError,KeyError,TypeError,ET.ParseError,zipfile.BadZipFile,subprocess.CalledProcessError) as error:
        report['error']=str(error)
    report['finishedUtc']=datetime.now(timezone.utc).isoformat()
    with args.json.open('x',encoding='utf-8') as stream:json.dump(report,stream,ensure_ascii=False,indent=2,allow_nan=False);stream.write('\n')
    with args.markdown.open('x',encoding='utf-8') as stream:
        stream.write('# Independent 075 package audit\n\nStatus: **'+report['status']+'**.\n\n')
        if 'error' in report:stream.write('Discrepancy: '+report['error']+'\n\n')
        for name,row in report['checks'].get('archives',{}).items():stream.write('- '+name+': '+str(row['members'])+' members; full CRC/EOF, SHA, Unix modes and independently selected inputs matched.\n')
        stream.write('\nSource snapshot '+SNAPSHOT+'; runtime '+RUNTIME+'; '+str(758)+' runtime inputs; GUID '+GUID+'.\n\nNo runtime or cloud acceptance is inferred. No original input or archive was changed.\n')
    print(json.dumps({'status':report['status'],'json':ref(args.json),'markdown':ref(args.markdown)}))
    return 0 if report['status']=='PASS_LOCAL_PACKAGE_CONSISTENCY' else 1


if __name__=='__main__':
    sys.exit(main())
