#!/usr/bin/env python3
"""Independent, read-only input/archive audit. Does not import package helpers."""
from pathlib import Path, PurePosixPath
import datetime, hashlib, json, os, plistlib, re, stat, struct, subprocess, sys, unicodedata, zipfile, zlib
from xml.etree import ElementTree as ET

B = Path('/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.17.0/Candidate1')
REPO = Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
D = B / 'DeliveryFinal1'; DIAG = B / 'Diagnostics'
RUNTIME = 'ba0b57336c46d72738b71b59af923a149aff467d'
SNAPSHOT = 'e133412599455d1213ff0c20fc6552af6d1bc47a'
FP = '61d00af23e00d960dd040ef55bdfb0aa269b15745be1d1a2f0faec46377d6698'
GUID = '1ada3d99ddf84c0f93e1b526e6360390'
ROOTS = ('Assets', 'Packages', 'ProjectSettings', 'Tools')
LIMIT = 100 * 1024 * 1024; CHUNK = 1024 * 1024
checks = 0; failures = []; pins = {}; inventories = {}; details = {}
EMPTY = hashlib.sha256(b'').hexdigest()

def check(label, value, detail=None):
    global checks
    checks += 1
    if not value: failures.append({'check': label, 'details': detail})
    return bool(value)

def need(label, value):
    if not check(label, value): raise ValueError(label)

def identity(s):
    return [s.st_dev, s.st_ino, s.st_mode, s.st_size, s.st_mtime_ns, s.st_ctime_ns]

def file(path, collect=False):
    path = Path(path); before = path.lstat()
    need('regular-file:' + str(path), stat.S_ISREG(before.st_mode))
    digest = hashlib.sha256(); size = 0; raw = bytearray() if collect else None
    with os.fdopen(os.open(path, os.O_RDONLY | os.O_NOFOLLOW), 'rb') as stream:
        need('stable-open:' + str(path), identity(os.fstat(stream.fileno())) == identity(before))
        while True:
            block = stream.read(CHUNK)
            if not block: break
            size += len(block); digest.update(block)
            if raw is not None: raw.extend(block)
        need('stable-read:' + str(path), identity(before) == identity(os.fstat(stream.fileno())) == identity(path.lstat()) and size == before.st_size)
    result = {'bytes': size, 'sha256': digest.hexdigest(), 'mode': before.st_mode & 0xffff, 'identity': identity(before)}
    if str(path) in pins: need('stable-reread:' + str(path), pins[str(path)] == result)
    else: pins[str(path)] = result
    return (result, bytes(raw)) if collect else result

def pairs(items):
    result = {}
    for k, v in items:
        if k in result: raise ValueError('Duplicate JSON key: ' + k)
        result[k] = v
    return result

def load(path):
    meta, raw = file(path, True)
    return json.loads(raw.decode('utf-8-sig'), object_pairs_hook=pairs)

def fingerprint(files):
    return hashlib.sha256(json.dumps(files, sort_keys=True, separators=(',', ':')).encode()).hexdigest()

def git(*args):
    env = dict(os.environ); env['GIT_OPTIONAL_LOCKS'] = '0'
    return subprocess.check_output(['git', '--no-replace-objects', '-C', str(REPO), *args], env=env)

def safe(name):
    need('safe-member-name:' + repr(name), isinstance(name, str) and bool(name) and not name.startswith('/') and '\\' not in name and not any(ord(c)<32 or ord(c)==127 for c in name))
    parts = name.rstrip('/').split('/')
    need('safe-member-components:' + name, all(p not in ('', '.', '..') and ':' not in p and not p.endswith((' ', '.')) for p in parts))
    forbidden = {'.git','.hg','.svn','.ssh','.aws','.gnupg','.env','__pycache__','library','temp','credentials.json','secrets.json','id_rsa','id_ed25519'}
    need('no-cache-key-path:' + name, not any(p.casefold() in forbidden or p.casefold().startswith('.env.') or p.casefold().endswith(('.p12','.pfx','.key','.pem')) for p in parts))
    return name

def record(path, kind, mode, size=0, digest=EMPTY, target=None):
    value = {'path':path,'type':kind,'mode':mode,'bytes':size,'sha256':digest}
    if target is not None: value['symlinkTarget'] = target
    return value

def tree(directory, prefix):
    nodes = {}; dirs = {}
    def walk(path, member):
        before = path.lstat(); need('real-directory:' + str(path), stat.S_ISDIR(before.st_mode))
        names = sorted(os.listdir(path)); dirs[str(path)] = {'identity':identity(before),'names':names}
        nodes[member+'/'] = record(member+'/', 'directory', before.st_mode & 0xffff)
        for leaf in names:
            p = path / leaf; n = safe(member+'/'+leaf); s = p.lstat()
            if stat.S_ISDIR(s.st_mode): walk(p,n)
            elif stat.S_ISREG(s.st_mode):
                v = file(p); nodes[n] = record(n,'file',v['mode'],v['bytes'],v['sha256'])
            elif stat.S_ISLNK(s.st_mode):
                target = os.readlink(p); raw = os.fsencode(target)
                need('stable-link:' + str(p), identity(p.lstat()) == identity(s))
                nodes[n] = record(n,'symlink',s.st_mode & 0xffff,len(raw),hashlib.sha256(raw).hexdigest(),target)
            else: raise ValueError('Unsupported node: '+str(p))
        need('stable-directory:' + str(path), identity(path.lstat()) == identity(before))
    walk(Path(directory),prefix)
    value = {'members':nodes,'directories':dirs}
    inventories[(str(directory),prefix)] = value
    return value

def archive(path, expected, manifest_record, object_format=None):
    meta = file(path); need('zip-cap:' + path.name, meta['bytes'] < LIMIT)
    check('archive-sha:' + path.name, (meta['bytes'],meta['sha256']) == (manifest_record['bytes'],manifest_record['sha256']))
    with path.open('rb') as stream:
        stream.seek(max(0,meta['bytes']-65557)); tail=stream.read()
    off=tail.rfind(b'PK\x05\x06'); need('zip-eof-record:' + path.name, off>=0 and off+22<=len(tail))
    eof=struct.unpack('<4s4H2IH',tail[off:off+22])
    need('zip-real-eof:' + path.name,eof[1]==eof[2]==0 and off+22+eof[-1]==len(tail))
    aliases=set(); total=0; actual={}
    with zipfile.ZipFile(path) as z:
        infos=z.infolist(); names=[i.filename for i in infos]
        need('archive-complete-members:' + path.name,len(names)==len(set(names)) and set(names)==set(expected) and eof[3]==eof[4]==len(names))
        for info in infos:
            name=safe(info.filename); alias=unicodedata.normalize('NFC',name.rstrip('/')).casefold()
            need('archive-unique-alias:'+name,alias not in aliases);aliases.add(alias)
            e=expected[name]; mode=info.external_attr>>16
            check('archive-unix-mode:'+name,info.create_system==3 and mode==e['mode'] and stat.S_IFMT(mode)=={'file':stat.S_IFREG,'directory':stat.S_IFDIR,'symlink':stat.S_IFLNK}[e['type']])
            check('archive-flags-size:'+name,info.orig_filename==name and not info.flag_bits&1 and info.file_size==e['bytes'] and info.is_dir()==(e['type']=='directory'))
            h=hashlib.sha256(); crc=0; count=0; target=bytearray() if e['type']=='symlink' else None
            oid=hashlib.new(object_format) if e.get('gitObjectId') else None
            if oid is not None:oid.update(b'blob '+str(e['bytes']).encode()+b'\0')
            with z.open(info) as src:
                while True:
                    block=src.read(CHUNK)
                    if not block:break
                    h.update(block);crc=zlib.crc32(block,crc);count+=len(block)
                    if target is not None:target.extend(block)
                    if oid is not None:oid.update(block)
            check('archive-crc:'+name,crc&0xffffffff==info.CRC)
            check('archive-payload:'+name,(count,h.hexdigest())==(e['bytes'],e['sha256']))
            if target is not None:check('archive-link-text:'+name,os.fsdecode(bytes(target))==e['symlinkTarget'])
            if oid is not None:check('archive-git-address:'+name,oid.hexdigest()==e['gitObjectId'])
            total+=count;actual[name]=dict(e)
    check('archive-member-count:' + path.name,len(actual)==manifest_record['memberCount'])
    return {'path':str(path),'bytes':meta['bytes'],'sha256':meta['sha256'],'members':len(actual),'fileMembers':sum(x['type']=='file' for x in actual.values()),'payloadBytes':total,'exhaustiveCRCByteSHAModeAndEOF':True}

def main():
    manifest=load(D/'package-manifest074.json')
    check('exact-final-identity',manifest['runtimeCommit']==RUNTIME and manifest['sourceSnapshotCommit']==SNAPSHOT and manifest['sourceFingerprint']==FP and manifest['sourceInputs']==740 and manifest['appVersion']=='0.17.0' and manifest['buildGuid']==GUID)
    check('manifest-closure-scope',manifest['schema']=='local-package-manifest074-v1' and manifest['status']=='local_archives_verified' and manifest['allInputsUnchangedBeforeAndAfter'] is True and manifest['nativeAcceptance']=='not_assessed_by_this_packager')
    for c in (RUNTIME,SNAPSHOT):check('exact-git-commit:'+c,git('rev-parse','--verify',c+'^{commit}').decode().strip()==c)
    check('runtime-ancestor-of-snapshot',subprocess.run(['git','--no-replace-objects','-C',str(REPO),'merge-base','--is-ancestor',RUNTIME,SNAPSHOT],stdout=subprocess.PIPE,stderr=subprocess.PIPE).returncode==0)
    algo=git('rev-parse','--show-object-format').decode().strip();need('supported-git-object-format',algo in ('sha1','sha256'))
    raw_trees={c:git('ls-tree','-r','-l','-z','--full-tree',c) for c in (RUNTIME,SNAPSHOT)}
    rows={};objects={}
    env=dict(os.environ);env['GIT_OPTIONAL_LOCKS']='0'
    proc=subprocess.Popen(['git','--no-replace-objects','-C',str(REPO),'cat-file','--batch'],env=env,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
    try:
        for c,raw_tree in raw_trees.items():
            selected={};excluded={};runtime_files={};runtime_modes={}
            for raw in raw_tree.split(b'\0'):
                if not raw:continue
                header,raw_name=raw.split(b'\t',1);gm,kind,oid,sz=header.split();name=safe(raw_name.decode());oid=oid.decode();size=int(sz);mode=gm.decode()
                need('tracked-blob-mode:'+name,kind==b'blob' and mode in ('100644','100755','120000'))
                if oid not in objects:
                    proc.stdin.write((oid+'\n').encode());proc.stdin.flush();parts=proc.stdout.readline().split()
                    need('actual-git-blob-header:'+oid,parts==[oid.encode(),b'blob',str(size).encode()])
                    digest=hashlib.sha256();address=hashlib.new(algo);address.update(b'blob '+str(size).encode()+b'\0');left=size;link=bytearray() if mode=='120000' else None
                    while left:
                        block=proc.stdout.read(min(CHUNK,left));need('actual-git-blob-not-truncated:'+oid,bool(block));left-=len(block);digest.update(block);address.update(block)
                        if link is not None:link.extend(block)
                    need('actual-git-blob-address:'+oid,proc.stdout.read(1)==b'\n' and address.hexdigest()==oid)
                    objects[oid]=(size,digest.hexdigest(),bytes(link) if link is not None else None)
                size2,digest,link=objects[oid];need('git-blob-consistent-size:'+name,size2==size)
                item={'path':name,'gitTreeMode':mode,'gitObjectId':oid,'bytes':size,'sha256':digest}
                if mode=='120000':
                    need('git-link-cached-text:'+name,link is not None);item['symlinkTarget']=os.fsdecode(link)
                excluded_path=name=='Docs/Evidence' or name.startswith('Docs/Evidence/')
                if excluded_path:excluded[name]=dict(item,excludedByDirectory='Docs/Evidence/')
                else:selected[name]=item
                parts=name.split('/')
                if parts[0] in ROOTS and not any(p.startswith('.') or p=='__pycache__' for p in parts):
                    need('regular-runtime-blob:'+name,mode in ('100644','100755'));runtime_files[name]=digest;runtime_modes[name]=int(mode,8)
            rows[c]={'selected':selected,'excluded':excluded,'runtime':runtime_files,'runtimeModes':runtime_modes}
    finally:
        proc.stdin.close();proc.wait();need('git-blob-reader-exit',proc.returncode==0);proc.stdout.close();proc.stderr.close()
    for c,key in [(SNAPSHOT,'source'),(RUNTIME,'runtimeSource')]:
        section=manifest[key];actual=rows[c]
        check('git-selected-partition:'+c,section['selectedTrackedPaths']==actual['selected'])
        check('git-excluded-partition:'+c,section['excludedTrackedPaths']==actual['excluded'])
        check('git-partition-metadata:'+c,section['profile']=='all-tracked-except-Docs-Evidence-074-v1' and section['sourceSnapshotCommit']==c and section['gitObjectFormat']==algo and section['fullTreeMetadataSha256']==hashlib.sha256(raw_trees[c]).hexdigest() and section['fullTreePathCount']==len(actual['selected'])+len(actual['excluded']))
        check('git-partition-bytes:'+c,section['selectedBytes']==sum(x['bytes'] for x in actual['selected'].values()) and section['excludedBytes']==sum(x['bytes'] for x in actual['excluded'].values()))
        check('git-runtime-map:'+c,section['runtimeFiles']==actual['runtime'] and len(actual['runtime'])==740 and fingerprint(actual['runtime'])==FP)
    check('all-740-runtime-blobs-and-modes-equal',rows[SNAPSHOT]['runtime']==rows[RUNTIME]['runtime'] and rows[SNAPSHOT]['runtimeModes']==rows[RUNTIME]['runtimeModes'])
    source_members={'source/':record('source/','directory',0o040755)}
    for name,item in rows[SNAPSHOT]['selected'].items():
        member='source/'+name
        for p in PurePosixPath(member).parents:
            if str(p)!='.':source_members[str(p)+'/']=record(str(p)+'/','directory',0o040755)
        link=item.get('symlinkTarget');mode=0o120777 if link is not None else int(item['gitTreeMode'],8)
        source_members[member]=record(member,'symlink' if link is not None else 'file',mode,item['bytes'],item['sha256'],link)
        source_members[member]['gitObjectId']=item['gitObjectId']
    check('source-members-derived-from-actual-git',source_members==manifest['source']['members'])
    actual_trees={}
    for key in ('macOS','evidence','normal'):
        t=manifest['trees'][key];actual=tree(t['directory'],t['archiveRoot']);actual_trees[key]=actual
        check('complete-actual-input-tree:'+key,actual['members']==t['members'])
        check('actual-input-tree-count-bytes:'+key,len(actual['members'])==t['memberCount'] and sum(v['bytes'] for v in actual['members'].values())==t['fileBytes'])
    details['archives']={}
    for key in ('source','macOS','evidence','normal'):
        expected=source_members if key=='source' else actual_trees[key]['members']
        details['archives'][key]=archive(D/(key+'.zip'),expected,manifest['artifacts'][key],algo if key=='source' else None)
    check('exactly-four-manifest-archives',set(manifest['artifacts'])=={'source','macOS','evidence','normal'})
    source_map=load(D/'source-map074.json');app_map=load(D/'actual-app-manifest074.json')
    for name in ('source-map074.json','actual-app-manifest074.json'):
        v=file(D/name);check('sidecar-recorded-sha:'+name,{k:v[k] for k in ('bytes','sha256')}==manifest['sidecars'][name])
    check('standalone-source-map',source_map=={'schema':'source-map074-v1','runtimeCommit':RUNTIME,'sourceSnapshotCommit':SNAPSHOT,'sourceFingerprint':FP,'sourceInputs':740,'files':rows[RUNTIME]['runtime']})
    check('standalone-actual-app-manifest',app_map=={'schema':'actual-app-manifest074-v1','identity':manifest['appIdentity'],'inputTree':manifest['trees']['macOS'],'archive':manifest['artifacts']['macOS']})
    current={}
    for root in ROOTS:
        def walk(folder):
            for name in sorted(os.listdir(folder)):
                if name.startswith('.') or name=='__pycache__':continue
                p=folder/name;s=p.lstat()
                if stat.S_ISDIR(s.st_mode):walk(p)
                else:
                    need('current-runtime-regular:'+str(p),stat.S_ISREG(s.st_mode));v=file(p);rel=p.relative_to(REPO).as_posix();current[rel]=v['sha256']
                    check('current-runtime-mode:'+rel,v['mode']==rows[RUNTIME]['runtimeModes'].get(rel))
        walk(REPO/root)
    check('full-current-runtime-740',current==rows[RUNTIME]['runtime'] and len(current)==740 and fingerprint(current)==FP and manifest['currentRuntime']['files']==current)
    full=B/'Full2/runner-receipt074.json';runner=load(full);tests=Path(runner['reportsDirectory']);evidence=load(tests/'source-evidence.json')
    check('full-test-complete-source-map',evidence['schema']==1 and bool(evidence['recordedAtUtc']) and evidence['files']==current and evidence['sourceFingerprint']==FP)
    totals={}
    for mode,n in [('EditMode',1227),('PlayMode',419)]:
        p=tests/(mode+'.xml');v,raw=file(p,True);root=ET.fromstring(raw);cases=list(root.iter('test-case'))
        check('full-XML-actual:'+mode,root.tag=='test-run' and root.get('result')=='Passed' and bool(root.get('end-time')) and int(root.get('total'))==int(root.get('passed'))==len(cases)==n and all(int(root.get(x,'0'))==0 for x in ('failed','skipped','inconclusive')) and all(x.get('result')=='Passed' for x in cases))
        check('full-XML-evidence-hash:'+mode,evidence['reports'][mode]=={'file':p.name,'passed':n,'sha256':v['sha256']});totals[mode]=n
    check('actual-full-total-1646',sum(totals.values())==manifest['fullTests']['totalPassed']==1646)
    for kind,rel in [('full','Full2/runner-receipt074.json'),('build','Build1/runner-receipt074.json'),('normal','NormalObserved1/runner-receipt074.json')]:
        r=load(B/rel)
        check('actual-closed-runner:'+kind,r['schema']=='frontier074-unity-runner-v1' and r['kind']==kind and r['status']=='completed_source_stable' and r['exitCode']==0 and r['sourceStable'] is True and bool(r['finishedUtc']) and r['gitHeadBefore']==RUNTIME and r['sourceFilesBefore']==current and r['sourceFingerprintBefore']==r['sourceFingerprintAfter']==FP and r['changedSources']==[])
        check('actual-no-preparation-or-intervention:'+kind,r['preparationRun'] is False and r['caseInterventions'] is False)
    index=load(DIAG/'evidence-copy-index074.json');copy_records=[];rels=set()
    check('52-original-copy-index',index['schema']=='frontier074-selected-original-copy-index-v1' and len(index['copies'])==52)
    for item in index['copies']:
        rel=safe(item['relative']);need('unique-evidence-copy:'+rel,rel not in rels);rels.add(rel)
        paths=[Path(item['original']),B/'EvidenceFinal1'/rel,Path(index['repoEvidenceDirectory'])/rel]
        observed=[file(p) for p in paths];expected=(item['bytes'],item['sha256'])
        check('original-copy-Git-current-identity:'+rel,all((x['bytes'],x['sha256'])==expected for x in observed))
        check('original-copy-permissions:'+rel,len({x['mode'] for x in observed})==1)
        gp=paths[2].relative_to(REPO).as_posix();g=rows[SNAPSHOT]['excluded'].get(gp)
        check('original-copy-frozen-Git-blob:'+rel,g is not None and (g['bytes'],g['sha256'])==expected and int(g['gitTreeMode'],8)==observed[2]['mode'])
        check('original-copy-delivered-member:'+rel,actual_trees['evidence']['members']['Evidence074/'+rel]['sha256']==item['sha256'])
        copy_records.append({'relative':rel,'bytes':item['bytes'],'sha256':item['sha256'],'mode':observed[0]['mode'],'original':item['original']})
    evidence_files={n[len('Evidence074/'):]:v for n,v in actual_trees['evidence']['members'].items() if v['type']=='file'}
    check('complete-selected-evidence-52-plus-README',set(evidence_files)==rels|{'README.md'} and len(evidence_files)==53)
    frozen074={p[len('Docs/Evidence/074/'):]:v for p,v in rows[SNAPSHOT]['excluded'].items() if p.startswith('Docs/Evidence/074/')}
    check('complete-selected-evidence-Git-snapshot-coverage',set(frozen074)==set(evidence_files))
    for rel,v in evidence_files.items():
        g=frozen074.get(rel)
        check('all-53-selected-evidence-Git-identities:'+rel,g is not None and (g['bytes'],g['sha256'],int(g['gitTreeMode'],8))==(v['bytes'],v['sha256'],v['mode']))
    check('whole-normal-29-files',sum(v['type']=='file' for v in actual_trees['normal']['members'].values())==29)
    normal=load(B/'NormalObserved1/matrix/manifest.json')
    check('closed-normal-three-declared-cases',normal['schema']=='normal-gather074-v1' and normal['status']=='observed_three_declared_cases' and bool(normal['finishedUtc']) and normal['sourceFingerprint']==FP and normal['version']=='0.17.0' and len(normal['cases'])==3 and all(c['status']=='observation_cap' and bool(c['finishedUtc']) and c['winner']==-1 and c['steps']>0 and 2400<=c['time']<2400.2 for c in normal['cases']))
    app=B/'Build1/Frontier Reckoning.app';_,raw=file(app/'Contents/Info.plist',True);info=plistlib.loads(raw);br=load(B/'Build1/runner-receipt074.json')
    exe=file(app/'Contents/MacOS'/info['CFBundleExecutable']);_,boot=file(app/'Contents/Resources/Data/boot.config',True)
    check('actual-app-version-permissions-boot',info['CFBundleShortVersionString']==br['buildVersion']=='0.17.0' and bool(exe['mode']&0o111) and oct(exe['mode']&0o777)==br['mainExecutableMode'] and boot.decode()==br['bootConfig'] and re.findall(r'^build-guid=([0-9a-f]{32})$',boot.decode(),re.M)==[GUID])
    with (app/'Contents/MacOS'/info['CFBundleExecutable']).open('rb') as src:header=src.read(12)
    check('actual-app-arm64-mach-o',header[:4]==b'\xcf\xfa\xed\xfe' and struct.unpack('<I',header[4:8])[0]==0x0100000c)
    for path,p in manifest['pinnedOriginals'].items():
        v=file(path);check('package-pinned-original-current:'+path,(v['bytes'],v['sha256'])==(p['bytes'],p['sha256']))
    supplemental_path=DIAG/'independent074-mac-normal-audit.json'
    if supplemental_path.exists():
        supplemental=load(supplemental_path);v=file(supplemental_path)
        check('second-independent-mac-normal-review-zero-failures',supplemental.get('status')=='independent_two_archives_exact_actual_tree_integrity_verified' and supplemental.get('failures')==[])
        details['secondIndependentMacNormalReview']={'path':str(supplemental_path),'bytes':v['bytes'],'sha256':v['sha256'],'statusAsRecorded':supplemental.get('status'),'checks':supplemental.get('checkCount'),'failureCount':len(supplemental.get('failures',[]))}
    for c in (RUNTIME,SNAPSHOT):check('fresh-Git-tree-unchanged:'+c,git('ls-tree','-r','-l','-z','--full-tree',c)==raw_trees[c])
    for (directory,prefix),old in list(inventories.items()):
        check('complete-input-tree-unchanged:'+prefix,tree(directory,prefix)==old)
    for path,old in list(pins.items()):check('all-original-and-archive-reads-stable:'+path,file(path)==old)
    details.update(runtimeCommit=RUNTIME,sourceSnapshotCommit=SNAPSHOT,sourceFingerprint=FP,sourceInputs=740,buildGuid=GUID,
                   snapshotSelectedPaths=len(rows[SNAPSHOT]['selected']),snapshotExcludedPaths=len(rows[SNAPSHOT]['excluded']),
                   snapshotExcludedBytes=sum(x['bytes'] for x in rows[SNAPSHOT]['excluded'].values()),uniqueGitBlobsActuallyHashed=len(objects),
                   evidenceOriginalCount=52,evidenceFileCount=53,normalFileCount=29,fullActualPassed=totals,originalCopies=copy_records,
                   caseStatuses=[{'id':c['id'],'status':c['status'],'time':c['time'],'winner':c['winner']} for c in normal['cases']],
                   originalAfterMapFact='Runner does not record complete sourceFilesAfter; no historical field was fabricated.',
                   separateNativePerformanceBalanceRemoteProof='Not assessed by this archive/source audit.')

if __name__=='__main__':
    targets=[DIAG/'independent074-package-audit-2.json',DIAG/'independent074-package-audit-2.md']
    if any(p.exists() for p in targets):raise SystemExit('Fresh audit outputs required')
    started=datetime.datetime.now(datetime.timezone.utc).isoformat()
    try:main()
    except Exception as error:failures.append({'check':'unexpected-audit-stop','details':type(error).__name__+': '+str(error)})
    result={'schema':'independent074-package-audit-v1','startedUtc':started,'finishedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
            'status':'independently_verified_exact_local_packages_and_originals' if not failures else 'failed_independent_local_package_audit',
            'checks':checks,'failureCount':len(failures),'failures':failures,'details':details,
            'method':'Fresh independent implementation, direct Git blobs/current files/source map/XML/copy index/actual app and exhaustive member decompression; no package helper import or execution.',
            'earlierAttempt':{'path':str(DIAG/'independent074-package-audit.json'),'byteExactPreservationCopy':str(DIAG/'independent074-package-audit-attempt1.json'),'status':'Preserved failed audit integration attempt; secondary reader expected failureCount instead of actual failures array. No package/source mismatch was reported. Only the report schema adapter was corrected before this fresh rerun.'},
            'scriptSHA256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
            'limits':['All three declared cases ended at observation caps, not demonstrated match victories.','No native launch, install, signing/notarization, FPS/balance/continuous safety or remote proof.','Unique immutable Git objects are streamed once per audit and shared between the two exact commit inventories; both Git trees are freshly re-read.']}
    with targets[0].open('x') as out:json.dump(result,out,ensure_ascii=False,indent=2,sort_keys=True);out.write('\n')
    lines=['# Independent 074 actual package audit','',result['status'],f"Checks: {checks}; failures: {len(failures)}.",'',result['method'],'']
    if not failures:
        lines.extend([f'Runtime {RUNTIME}; source snapshot {SNAPSHOT}; 740 inputs / {FP}.',f'Actual app 0.17.0, arm64, GUID {GUID}; executable mode and all delivered app member bytes checked.','52 original-copy entries match originals, selected evidence, committed snapshot and delivered archive; 53 evidence files include generated README. Entire NormalObserved1 contains 29 files.','Actual full regression: 1227 EditMode + 419 PlayMode = 1646 passed. Three ordinary cases reached observation caps.',''])
        for k,v in details['archives'].items():lines.append(f"- {k}: {v['bytes']} bytes, {v['members']} members, SHA256 {v['sha256']}")
    else:lines.extend(json.dumps(f,ensure_ascii=False) for f in failures)
    lines.extend(['','No package, project or original was modified. Only these fresh audit outputs were written. No Unity, extraction, install or external upload was invoked.'])
    with targets[1].open('x') as out:out.write('\n'.join(lines)+'\n')
    print(json.dumps({k:result[k] for k in ('status','checks','failureCount','failures')},ensure_ascii=False))
    sys.exit(0 if not failures else 2)
