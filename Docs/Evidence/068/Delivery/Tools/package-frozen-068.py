#!/usr/bin/env python3
"""Package only frozen 0.11 source, native app and scoped native evidence.
Default preflight is read-only. --execute uses exclusive ZIP installation.
No build, launch, network, cloud write or Git mutation.
"""
import argparse,hashlib,importlib.util,json,os,plistlib,re,shutil,stat,subprocess,sys,tempfile,zipfile
from datetime import datetime,timezone
from pathlib import Path,PurePosixPath
from xml.etree import ElementTree as ET
sys.dont_write_bytecode=True
RELEASE='068'
VERSION='0.11.0'
PROJECT=Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
BASE=Path(__file__).resolve().parent.parent
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

def run():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--source-commit',required=True);parser.add_argument('--execute',action='store_true');args=parser.parse_args()
    commit=args.source_commit
    need(subprocess.check_output(['git','rev-parse','HEAD'],cwd=PROJECT,text=True).strip()==commit,'HEAD is not source snapshot')
    need(subprocess.check_output(['git','status','--porcelain'],cwd=PROJECT,text=True).strip()=='','Source snapshot working tree is not clean')
    freeze=parse_json((BASE/'source-freeze.json').read_bytes());build=parse_json((BASE/'Build/client-build.json').read_bytes())
    need(freeze['runtimeCommit']=='dca073708b671c60a1fe9ff243a67e4ace3fe157' and freeze['inputs']==630,'Unexpected source freeze')
    need(freeze['sourceFingerprint']=='62240c2373c773b5aaf807c72815acab2e76577504f3a0569218702864730ebe','Unexpected source fingerprint')
    spec=importlib.util.spec_from_file_location('ve068',PROJECT/'Tools/verification_evidence.py');ve=importlib.util.module_from_spec(spec);spec.loader.exec_module(ve)
    testdir=PROJECT/'Docs/Evidence/068/Tests';need(ve.verify(PROJECT,testdir)==freeze['sourceFingerprint'],'Tests do not bind current source')
    tests={'run':'run-Z47ikuJY','result':'Passed'}
    for mode,count in [('EditMode',817),('PlayMode',368)]:
        root=ET.parse(testdir/(mode+'.xml')).getroot();need(root.get('result')=='Passed' and int(root.get('total'))==count and int(root.get('passed'))==count and all(int(root.get(k,'0'))==0 for k in ('failed','skipped','inconclusive')),'Test result mismatch');tests[mode]=count
    tests['total']=1185
    app=BASE/'Build/Frontier Reckoning.app';boot=(app/'Contents/Resources/Data/boot.config').read_text();guid=re.search(r'build-guid=(.+)',boot).group(1)
    need(guid=='fa26339934914cd2813df9763de6bba7' and build['buildGuid']==guid and build['sourceFingerprint']==freeze['sourceFingerprint'],'Build identity mismatch')
    need(file_hash(app/'Contents/MacOS/Frontier Reckoning')==build['executable']['sha256'],'Executable changed')
    need('arm64' in subprocess.check_output(['file',str(app/'Contents/MacOS/Frontier Reckoning')],text=True),'Executable is not arm64')
    with (app/'Contents/Info.plist').open('rb') as f: plist=plistlib.load(f)
    need(plist['CFBundleShortVersionString']==VERSION,'Client version mismatch')
    native=PROJECT/'Docs/Evidence/068/Native';need((native/'native-acceptance.json').is_file() and (native/'archive-receipt.json').is_file(),'Final native evidence not archived')
    inspection=parse_json((native/'inspection-final.json').read_bytes());need(inspection['metadataFacts']['status']=='verified' and inspection['candidateNativeBuild']['buildGuid']==guid,'Native inspection mismatch')
    observations=parse_json((native/'actual-ui-observations.json').read_bytes());need(observations['buildGuid']==guid and len(observations['observations'])==22,'Native observation mismatch')
    for item in observations['observations']:need(file_hash(native/item['evidence'])==item['sha256'],'UI screenshot changed')
    source=source_entries(PROJECT);snapshot_binding(PROJECT,source,commit)
    readme=PROJECT/'Docs/Evidence/068/Delivery/README-zh.md';guide=PROJECT/'Docs/Player-Guide-068.md'
    client_prefix='Frontier-Reckoning-0.11.0/'
    client=tree_entries(app,client_prefix+'Frontier Reckoning.app')+[path_entry(client_prefix+'README-zh.md',readme),path_entry(client_prefix+'Player-Guide-068.md',guide),path_entry(client_prefix+'client-build.json',BASE/'Build/client-build.json')]
    gallery_prefix='Frontier-Reckoning-0.11.0-gallery/'
    gallery=tree_entries(native,gallery_prefix+'Native')+[path_entry(gallery_prefix+'README-zh.md',readme),path_entry(gallery_prefix+'Player-Guide-068.md',guide)]
    plans={'source':source,'client':client,'gallery':gallery};names={'source':'Frontier-Reckoning-0.11.0-source.zip','client':'Frontier-Reckoning-0.11.0-macOS-arm64.zip','gallery':'Frontier-Reckoning-0.11.0-gallery.zip'}
    manifest={'schema':1,'version':VERSION,'release':RELEASE,'createdUtc':datetime.now(timezone.utc).isoformat(),'sourceSnapshotCommit':commit,'runtimeCommit':freeze['runtimeCommit'],'sourceFingerprint':freeze['sourceFingerprint'],'sourceInputs':630,'buildGuid':guid,'tests':tests,'nativeScope':'Two native processes over loopback TCP on one physical Mac','physicalMacAcceptance':'pending','remoteContentHashVerified':False,'packages':{}}
    for kind,entries in plans.items():
        need(len({e['name'] for e in entries})==len(entries),'Duplicate planned member')
        print(kind,len(entries),'members')
        if args.execute:
            info=create_zip(BASE/names[kind],entries)
            members=[]
            for entry in entries:
                item={'name':entry['name'],**entry['record']}
                if kind=='source':item['gitPath']=entry['path'].relative_to(PROJECT).as_posix()
                members.append(item)
            manifest['packages'][kind]={'file':names[kind],'bytes':info['bytes'],'sha256':info['sha256'],'members':members}
    if args.execute:
        path=BASE/'Packaging/release-manifest.json';need(not path.exists(),'Refuse overwrite manifest');path.write_bytes(json_bytes(manifest));print(json.dumps({k:{p:v for p,v in row.items() if p!='members'} for k,row in manifest['packages'].items()}))
    else:print('Read-only preflight Passed; no ZIP or manifest written')
if __name__=='__main__':run()
