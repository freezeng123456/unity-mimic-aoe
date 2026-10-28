#!/usr/bin/env python3
"""Prepare 3 immutable 078 ZIPs after parent acceptance; never run Unity/Git writes/upload.
Only exact current selected final Full run/Build1 sources bind release identity. Other included evidence
retains its original success/failure/source scope, not an old ordinary-match claim.
Fresh output only; failed partial output is retained. ZIPs omit xattrs/ACLs/notarization.
"""
import argparse, hashlib, json, os, plistlib, re, stat, subprocess, unicodedata, zipfile
from pathlib import Path
from xml.etree import ElementTree as ET
BASE=Path(__file__).resolve().parent
REPO=Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
VERSION='0.21.0'; LIMIT=100*1024*1024; PART_LIMIT=64*1024*1024; CHUNK=1024*1024
RUNTIME=('Assets','Packages','ProjectSettings','Tools')
ALLOWED=RUNTIME+('SourceAssets','Docs','README.md','.gitignore')
def need(ok, why):
    if not ok: raise ValueError(why)
def digest(raw): return hashlib.sha256(raw).hexdigest()
def git(*args):
    return subprocess.check_output(['git','--no-replace-objects','-C',str(REPO),*args],env=dict(os.environ,GIT_OPTIONAL_LOCKS='0'))
def safe(name):
    parts=name.rstrip('/').split('/')
    need(name and not name.startswith('/') and '\\' not in name and ':' not in name and all(p not in ('','.','..') for p in parts) and all(ord(c)>=32 and ord(c)!=127 for c in name),'Unsafe ZIP path: '+repr(name))
    return name
def namespace(items):
    aliases=[unicodedata.normalize('NFC',safe(n).rstrip('/')).casefold() for n in items]
    need(len(aliases)==len(set(aliases)),'Duplicate/case/Unicode ZIP alias')
def identity(s): return (s.st_dev,s.st_ino,s.st_mode,s.st_size,s.st_mtime_ns,s.st_ctime_ns)
def file_record(p,sink=None):
    s=p.lstat(); need(stat.S_ISREG(s.st_mode),'Nonregular file: '+str(p)); h=hashlib.sha256(); blob=hashlib.sha1(b'blob '+str(s.st_size).encode()+b'\0'); size=0
    with os.fdopen(os.open(p,os.O_RDONLY|os.O_NOFOLLOW),'rb') as f:
        need(identity(os.fstat(f.fileno()))==identity(s),'Input replaced on open')
        for block in iter(lambda:f.read(CHUNK),b''):
            h.update(block); blob.update(block); size+=len(block)
            if sink is not None: sink.write(block)
        need(identity(os.fstat(f.fileno()))==identity(s)==identity(p.lstat()) and size==s.st_size,'Input changed: '+str(p))
    return dict(source=str(p),kind='file',bytes=size,sha256=h.hexdigest(),unixMode=s.st_mode&65535,gitBlob=blob.hexdigest())
def load(p):
    expected=file_record(p); raw=p.read_bytes(); need(digest(raw)==expected['sha256'],'JSON changed: '+str(p))
    def pairs(rows):
        d=dict(rows); need(len(d)==len(rows),'Duplicate JSON key'); return d
    return json.loads(raw,object_pairs_hook=pairs,parse_constant=lambda x:need(False,'Nonfinite JSON'))
def inventory(root,prefix,skip=lambda p:False,links=False):
    result={}
    def visit(p,name):
        rel=p.relative_to(root)
        if rel.parts and skip(rel): return
        s=p.lstat(); mode=s.st_mode&65535
        if stat.S_ISREG(s.st_mode): item=file_record(p)
        elif stat.S_ISDIR(s.st_mode):
            name+='/'
            item=dict(source=str(p),kind='directory',bytes=0,sha256=digest(b''),unixMode=mode)
            result[safe(name)]=item
            for child in sorted(p.iterdir()): visit(child,name+child.name)
            need(identity(s)==identity(p.lstat()),'Directory changed: '+str(p)); return
        elif stat.S_ISLNK(s.st_mode):
            target=os.readlink(p); need(links and target and not os.path.isabs(target) and p.resolve(strict=True).is_relative_to(root),'Unsupported/escaping symlink: '+str(p))
            item=dict(source=str(p),kind='symlink',bytes=len(os.fsencode(target)),sha256=digest(os.fsencode(target)),unixMode=mode,target=target)
        else: raise ValueError('Unsupported node: '+str(p))
        result[safe(name)]=item
    need(stat.S_ISDIR(root.lstat().st_mode),'Input is not real directory: '+str(root)); visit(root,prefix); namespace(result); return result

def source_skip(rel):
    return any(p.startswith('.') or p=='__pycache__' for p in rel.parts) or (rel.parts[:2]==('Docs','Evidence') and len(rel.parts)>2 and rel.parts[2]!='078')
def sources():
    rows={}
    for top in ALLOWED:
        p=REPO/top
        need(p.exists(),'Missing portable root: '+top)
        if p.is_dir(): rows.update(inventory(p,'FrontierReckoning/'+top,lambda r:source_skip(Path(top)/r)))
        else: rows['FrontierReckoning/'+top]=file_record(p)
    namespace(rows); return rows

def runtime_map():
    files={}
    for top in RUNTIME:
        for name,item in inventory(REPO/top,top,lambda r:any(p.startswith('.') or p=='__pycache__' for p in r.parts)).items():
            if item['kind']=='file': files[name]=item['sha256']
    return files

def closed(name,fp,current):
    path=BASE/name/'runner-receipt078.json'; r=load(path)
    need(r['status']=='closed' and r['exitCode']==0 and r['sourceStable'] is True and r['changedSources']==[],'Incomplete/failed runner: '+name)
    need(r['sourceFingerprintBefore']==r['sourceFingerprintAfter']==fp and r['sourcesBefore']==current,'Runner source map differs: '+name)
    need(r['commands'] and all(c['status']=='closed' and c['exitCode']==0 and c.get('finishedUtc') for c in r['commands']),'Runner child incomplete: '+name)
    return r

def app_bind(fp,guid,build):
    app=BASE/'Build1/Frontier Reckoning.app'; mp=BASE/'Build1/actual-app-manifest078.json'; m=load(mp)
    need(m['version']==VERSION and m['buildGuid']==guid and m['sourceFingerprint']==fp and m['sourceHead']==build['gitHeadBefore'] and m['sourceInputCount']==len(build['sourcesBefore']) and m['bundleId']=='com.frontierworkshop.reckoning' and Path(m['appPath'])==app,'Actual app identity differs')
    need(m['runnerReceiptSha256']==file_record(BASE/'Build1/runner-receipt078.json')['sha256'] and m['runnerScriptSha256']==file_record(BASE/'run078.py')['sha256'],'Actual app runner binding differs')
    rows=inventory(app,app.name,links=True); files={n[len(app.name)+1:]:i for n,i in rows.items() if i['kind']=='file'}
    expected=m['files']; need(len({x['path'] for x in expected})==len(expected),'Duplicate app-manifest path')
    need(set(files)=={x['path'] for x in expected} and len(files)==m['fileCount'] and sum(x['bytes'] for x in files.values())==m['totalFileBytes'],'Incomplete app contents')
    for x in expected:
        i=files[x['path']]; need((i['bytes'],i['sha256'],stat.S_IMODE(i['unixMode']))==(x['bytes'],x['sha256'],int(x['mode'],8)),'App bytes/mode changed: '+x['path'])
    actual_links={n[len(app.name)+1:]:i for n,i in rows.items() if i['kind']=='symlink'}
    need(len({x['path'] for x in m['symlinks']})==len(m['symlinks']) and set(actual_links)=={x['path'] for x in m['symlinks']},'App symlink inventory differs')
    for x in m['symlinks']:
        i=actual_links[x['path']]; need(i['target']==x['target'],'App symlink target differs')
        if 'mode' in x: need(stat.S_IMODE(i['unixMode'])==int(x['mode'],8),'Declared app symlink mode differs')
    # Target-only legacy records are accepted; actual lstat link modes bind ZIP/readback/final reinventory.
    plist=plistlib.loads((app/'Contents/Info.plist').read_bytes()); exe=files['Contents/MacOS/'+plist['CFBundleExecutable']]
    need(plist['CFBundleShortVersionString']==VERSION and plist['CFBundleIdentifier']=='com.frontierworkshop.reckoning' and exe['unixMode']&stat.S_IXUSR and exe['sha256']==m['executableSha256'],'App plist/executable differs')
    need(re.search(r'^build-guid='+guid+r'$',(app/'Contents/Resources/Data/boot.config').read_text(),re.M) and m['codesignVerification']['exitCode']==0,'App build GUID/signature receipt differs')
    need('FRONTIER_BUILD Succeeded' in (BASE/'Build1/build.log').read_text(errors='replace'),'Missing actual successful build log')
    return rows

def historical_reports(name):
    # A failed full run has no passing source-evidence.json: retain its original XML/receipt identity.
    r=load(BASE/name/'runner-receipt078.json'); p=Path(r['reportsDirectory']).resolve(strict=True)
    need(r['kind']=='full' and r['status']=='closed' and r['sourceStable'] is True and r['changedSources']==[] and r['sourceFingerprintBefore']==r['sourceFingerprintAfter']==digest(json.dumps(r['sourcesBefore'],sort_keys=True,separators=(',',':')).encode()),'Historical full source identity differs: '+name)
    need(p.is_relative_to((REPO/'Builds/Tests').resolve()) and r['freshReportsDirectories']==[str(p)],'Historical actual reports identity differs: '+name)
    for mode in ('EditMode','PlayMode'):
        node=ET.parse(p/(mode+'.xml')).getroot(); need(node.tag=='test-run' and node.attrib==r['tests'][mode] and len(node.findall('.//test-case'))==int(node.attrib['total']) and file_record(p/(mode+'.xml'))['sha256']==r['testsXmlSha256'][mode],'Historical actual XML differs: '+name+'/'+mode)
    return p

# Exact directory allowlist; never discover historical077 or future delivery trees.
EVIDENCE_DIRS=('ProbeCoarse1','ProbeCoarse2','ProbeFine1','Shore1','Render1','Full1','Build1','Native1')
PLANNING_FILES=('audit-coarse-probe078.py','audit-fine-probe078.py',
    'coarse-probe-independent-audit078.json','fine-probe-independent-audit078.json',
    'render078-prepared.json','shore078-measured-verification-draft.md',
    'shore078-pre-candidate-control-contract.json','shore078-readonly-probe-comparison.json',
    'unchanged-authority-baseline078.json','launch-native078-static-review.json')
DECLARATIONS=('run078.py','create-build-manifest078.py','launch-native078.py','prepare-packages078.py')
OPTIONAL_DECLARATIONS=('archive-selected078.py','integration078.json')

def original_runner(name,expected_exit):
    # Original compile failure and coarse controls retain THEIR source identity.
    r=load(BASE/name/'runner-receipt078.json')
    need(r['schema']==1 and r['status']=='closed' and r['exitCode']==expected_exit and r['sourceStable'] is True and r['changedSources']==[],'Original runner outcome changed: '+name)
    own=digest(json.dumps(r['sourcesBefore'],sort_keys=True,separators=(',',':')).encode())
    need(r['sourceFingerprintBefore']==r['sourceFingerprintAfter']==own,'Original runner source identity differs: '+name)
    for mode,sha in (r.get('testsXmlSha256') or {}).items():
        need(mode in ('EditMode','PlayMode') and file_record(BASE/name/(mode+'.xml'))['sha256']==sha,'Original actual XML differs: '+name+'/'+mode)
    return r

def native_bind(fp,guid,current,final_run):
    root=BASE/'Native1/winding-view'; path=root/'native-audit078.json'; audit=load(path)
    need(audit.get('schema')==1 and audit.get('auditKind','').startswith('078 independent') and audit.get('status')=='accepted_bounded_camera_capture_menu_exit_quit' and audit.get('originalsUnchangedDuringAudit') is True,'Independent actual078 native audit not accepted/unchanged')
    need(Path(audit['originalEvidenceRoot'])==root,'Native audit root differs')
    ident=audit['identity']; need(ident['appVersion']==VERSION and ident['buildGuid']==guid and ident['sourceFingerprint']==fp and ident['sourceInputCount']==len(current),'Native accepted source/app identity differs')
    need(ident.get('currentExactInventoryMatches'+final_run+'AndBuild1') is True,'Native audit exact current/full/build source inventory not corroborated')
    life=audit['lifecycle']; need(life.get('launchPidAbsent') is True and life.get('allOwnedEditorAndNativeAbsent') is True and life.get('launchPidRows')==[] and life.get('matchingOwnedEditorAndNativeRows')==[],'Native lifecycle is not closed by actual PID evidence')
    prerequisites=audit['prerequisites']
    for name in (final_run,'Build1'):
        record=prerequisites[name]; actual=BASE/name/'runner-receipt078.json'; need(Path(record['path'])==actual and record['sha256']==file_record(actual)['sha256'],'Native audit prerequisite changed: '+name)
    for record,actual in ((prerequisites['app']['manifest'],BASE/'Build1/actual-app-manifest078.json'),(prerequisites['launchReceipt'],root/'receipt078.json')):
        need(Path(record['path'])==actual and record['sha256']==file_record(actual)['sha256'],'Native audit app/launch binding changed')
    launch=load(root/'receipt078.json'); need(launch['schema']==1 and launch['sourceFingerprint']==fp and launch['sourceInputCount']==len(current) and launch['app']['version']==VERSION and launch['app']['buildGuid']==guid and launch['pid']==life['launchPid'],'Original launch source/GUID/PID differs')
    need(launch['helperSha256']==file_record(BASE/'launch-native078.py')['sha256'],'Native launch helper changed after launch')
    rows=inventory(root,'NativeOriginals',lambda rel:rel.as_posix() in ('native-audit078.json','native-audit078.md'))
    actual={name.removeprefix('NativeOriginals/'):item for name,item in rows.items() if item['kind']=='file'}
    originals=audit['originalEvidenceIndex']; need(len({x['path'] for x in originals})==len(originals) and {x['path'] for x in originals}==set(actual),'Native original index incomplete/duplicated')
    need(len(originals)==audit['originalFileCount'] and sum(x['bytes'] for x in originals)==audit['originalFileBytes'],'Native original aggregate differs')
    for record in originals:
        need(safe(record['path'])==record['path'] and all(actual[record['path']][k]==record[k] for k in ('bytes','sha256')),'Native original bytes changed: '+record['path'])
    pngs=[n for n in actual if n.startswith('Data/Captures/') and n.endswith('.png')]; need(len(pngs)>=5,'At least five actual completed default26/close16/bridge/empty-harbor native capture pairs required')
    for png in pngs:
        side=png[:-4]+'.json'; need(side in actual,'Native PNG sidecar missing'); metadata=load(root/side)
        need(metadata['appVersion']==VERSION and metadata['buildGuid']==guid and metadata['mapId']=='winding' and metadata['pngFile']==Path(png).name and metadata['pngSha256']==actual[png]['sha256'] and metadata['requestMatchId']==metadata['matchId'],'Actual native capture identity differs')
    return dict(path=str(path),sha256=file_record(path)['sha256'],status=audit['status'],scope=audit['scope'])

def evidence(reports,final_run,extra_planning=()):
    # Copy complete original stage directories, not screenshots selected for beauty.
    # No generic BASE directory traversal and no recursive Planning/Drive inclusion.
    def omit(rel):
        return any(x.endswith('.app') or x.startswith('Frontier Reckoning_BackUp') or x=='__pycache__' or x.lower().startswith(('delivery','cloud','package','upload')) or x.lower().endswith(('.zip','.tar','.tgz','.dmg')) for x in rel.parts)
    rows={}
    def add(folder,prefix):
        more=inventory(folder,prefix,omit); need(not set(rows)&set(more),'Duplicate evidence member'); rows.update(more)
    selected=EVIDENCE_DIRS+((final_run,) if final_run not in EVIDENCE_DIRS else ())
    for name in selected:
        need((BASE/name).is_dir() and not (BASE/name).is_symlink(),'Missing original required stage: '+name)
        add(BASE/name,'Evidence078/Candidate1/'+name)
    add(reports,'Evidence078/Actual'+final_run+'Reports')
    if final_run!='Full1':add(historical_reports('Full1'),'Evidence078/ActualFull1Reports')
    for name in ('Content-078.md','Verification-078.md'):
        p=REPO/'Docs'/name; need(p.is_file(),'Missing actual078 delivery document: '+name);rows['Evidence078/Docs/'+name]=file_record(p)
    selected_docs=REPO/'Docs/Evidence/078';need(selected_docs.is_dir(),'Missing selected078 repository evidence');add(selected_docs,'Evidence078/Docs/Evidence/078')
    for name in DECLARATIONS:
        p=BASE/name;need(p.is_file(),'Missing exact078 declaration: '+name);rows['Evidence078/Declarations/'+name]=file_record(p)
    for name in OPTIONAL_DECLARATIONS:
        p=BASE/name
        if p.is_file():rows['Evidence078/Declarations/'+name]=file_record(p)
    for name in PLANNING_FILES:
        p=BASE/'Planning'/name;need(p.is_file(),'Missing original078 Planning evidence: '+name);rows['Evidence078/Planning/'+name]=file_record(p)
    # Root may explicitly name a later independent078 audit FILE after it exists.
    # This is not a directory/glob/recursive discovery mechanism; future project
    # plans and Drive/packaging journals do not become release evidence by accident.
    for value in extra_planning:
        rel=Path(value);need(len(rel.parts)==2 and rel.parts[0]=='Planning' and rel.name not in PLANNING_FILES and rel.suffix in ('.json','.md','.py') and re.fullmatch(r'(?:full|shore|render|native|package)[A-Za-z0-9._-]*078[A-Za-z0-9._-]*\.(?:json|md|py)',rel.name,re.I),'Additional Planning evidence must be one explicitly named078 readonly audit/declaration file')
        need(not re.search(r'(?:next|079|drive|journal|upload|delivery|package-plan)',rel.name,re.I),'Future delivery/planning material excluded from078 evidence')
        p=BASE/rel;need(p.is_file() and not p.is_symlink(),'Extra Planning audit missing/redirected');member='Evidence078/'+rel.as_posix();need(member not in rows,'Duplicate extra audit');rows[member]=file_record(p)
    namespace(rows); return rows

def archive(output,name,rows,identity_data):
    namespace(rows); path=output/(name+'.zip'); members={}
    with zipfile.ZipFile(path,'x',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        for member,item in sorted(rows.items()):
            info=zipfile.ZipInfo(member,(1980,1,1,0,0,0)); info.create_system=3; info.external_attr=item['unixMode']<<16; info.compress_type=zipfile.ZIP_DEFLATED
            if item['kind']=='directory': info.external_attr|=0x10
            if item['kind']=='file':
                with z.open(info,'w',force_zip64=True) as sink: now=file_record(Path(item['source']),sink)
                need(now==item,'Archive input changed: '+member)
            else: z.writestr(info,os.fsencode(item['target']) if item['kind']=='symlink' else b'')
            members[member]=item
    if not name.endswith('evidence078'): need(path.stat().st_size<LIMIT,'Source/app ZIP exceeds conservative 100 MiB upload ceiling: '+name)
    with zipfile.ZipFile(path) as z:
        infos=z.infolist(); namespace([i.filename for i in infos]); need(len(infos)==len(rows) and {i.filename for i in infos}==set(rows),'ZIP membership differs')
        for info in infos:
            h=hashlib.sha256(); size=0
            with z.open(info) as f:
                for block in iter(lambda:f.read(CHUNK),b''): h.update(block); size+=len(block)
            item=rows[info.filename]; need((size,h.hexdigest(),info.external_attr>>16)==(item['bytes'],item['sha256'],item['unixMode']),'ZIP bytes/SHA/mode mismatch: '+info.filename)
    receipt=dict(**identity_data,archive=path.name,bytes=path.stat().st_size,sha256=file_record(path)['sha256'],members=members,directUploadLimitBytes=LIMIT,requiresTransportParts=path.stat().st_size>=LIMIT)
    target=output/(name+'.manifest.json'); target.write_text(json.dumps(receipt,indent=2)+'\n'); path.chmod(0o444); target.chmod(0o444); return {k:receipt[k] for k in ('archive','bytes','sha256')}


def transport(output,full):
    """Never rewrite ZIP entries. Split bytes only, then compare concatenation byte-for-byte."""
    path=output/full['archive']; parts=[]; upload=[]; multipart=full['bytes']>=LIMIT
    if multipart:
        remaining=full['bytes']
        with path.open('rb') as original:
            while remaining:
                part=output/(path.name+f'.part-{len(parts)+1:03d}'); count=min(PART_LIMIT-1,remaining)
                with part.open('xb') as out:
                    left=count
                    while left:
                        block=original.read(min(CHUNK,left)); need(block,'Original ZIP ended early'); out.write(block); left-=len(block)
                item=file_record(part); need(item['bytes']==count<PART_LIMIT,'Transport part exceeds ceiling')
                parts.append(dict(file=part.name,bytes=count,sha256=item['sha256'])); remaining-=count
            need(original.read(1)==b'','Original ZIP grew during split')
        h=hashlib.sha256(); size=0
        with path.open('rb') as original:
            for item in parts:
                with (output/item['file']).open('rb') as part:
                    ph=hashlib.sha256(); amount=0
                    for block in iter(lambda:part.read(CHUNK),b''):
                        need(original.read(len(block))==block,'Transport concatenation differs byte-for-byte'); h.update(block); ph.update(block); size+=len(block); amount+=len(block)
                    need((amount,ph.hexdigest())==(item['bytes'],item['sha256']),'Transport part changed')
                (output/item['file']).chmod(0o444)
            need(original.read(1)==b'' and (size,h.hexdigest())==(full['bytes'],full['sha256']),'Reassembled ZIP size/SHA differs')
        upload=parts
    else: upload=[dict(file=path.name,bytes=full['bytes'],sha256=full['sha256'])]
    need((file_record(path)['bytes'],file_record(path)['sha256'])==(full['bytes'],full['sha256']),'Full ZIP changed')
    receipt=dict(schema=1,logicalArchive=full,fullArchiveKeptLocal=True,fullArchiveUploadAllowed=not multipart,transport='ordered-lossless-byte-parts' if multipart else 'direct-zip',partLimitBytesExclusive=PART_LIMIT,orderedParts=parts,uploadArtifacts=upload,concatenationByteForByteVerified=True if multipart else None)
    target=output/'transport-manifest078.json'; target.write_text(json.dumps(receipt,indent=2)+'\n'); target.chmod(0o444)
    return upload,dict(file=target.name,bytes=target.stat().st_size,sha256=file_record(target)['sha256'])

def main():
    p=argparse.ArgumentParser(description=__doc__); p.add_argument('--snapshot',required=True); p.add_argument('--fingerprint',required=True); p.add_argument('--guid',required=True); p.add_argument('--full-run',default='Full1'); p.add_argument('--planning-evidence',action='append',default=[],help='Explicit later readonly078 audit file directly under Planning; no directory/glob'); p.add_argument('--output',type=Path,default=BASE/'Packages078'); a=p.parse_args()
    need(re.fullmatch('[0-9a-f]{40}',a.snapshot) and re.fullmatch('[0-9a-f]{64}',a.fingerprint) and re.fullmatch('[0-9a-f]{32}',a.guid),'Full lowercase snapshot/fingerprint/GUID required')
    need(re.fullmatch(r'Full[1-9]\d*',a.full_run),'Explicit final full-run identity invalid')
    need(a.guid!='0'*32 and a.guid not in ('a86c515f3ab040649ea505e507193ff3','ed29410c2b27484abeac486c8f57b81b','6d3fd495bd8f48be852c1e6c62bcb02f'),'Fresh078 build GUID required, not zero/076/077')
    def snapshot():
        need(git('rev-parse','HEAD').decode().strip()==a.snapshot==git('rev-parse','--verify',a.snapshot+'^{commit}').decode().strip() and not git('status','--porcelain=v1','-z','--untracked-files=all'),'Explicit exact clean HEAD required')
    snapshot(); need(re.search(r'^  bundleVersion: 0\.21\.0$',(REPO/'ProjectSettings/ProjectSettings.asset').read_text(),re.M),'Current source version is not 0.21.0')
    current=runtime_map(); need(digest(json.dumps(current,sort_keys=True,separators=(',',':')).encode())==a.fingerprint,'Current complete source fingerprint differs')
    full=closed(a.full_run,a.fingerprint,current); build=closed('Build1',a.fingerprint,current); need(full['kind']=='full' and build['kind']=='build','Wrong runner kind')
    need(len(full['commands'])==1 and full['commands'][0]['command']==['bash',str(REPO/'Tools/test.sh')],'Final full run was not actual Tools/test.sh')
    need(len(build['commands'])==1 and '-executeMethod' in build['commands'][0]['command'] and build['commands'][0]['command'][build['commands'][0]['command'].index('-executeMethod')+1]=='Frontier.Editor.BuildTools.BuildMac','Actual Build1 method differs')
    for r in (full,build): subprocess.run(['git','-C',str(REPO),'merge-base','--is-ancestor',r['gitHeadBefore'],a.snapshot],check=True)
    reports=Path(full['reportsDirectory']).resolve(strict=True); need(reports.is_relative_to((REPO/'Builds/Tests').resolve()) and full['freshReportsDirectories']==[str(reports)],'Not the actual unique fresh full reports')
    se=load(reports/'source-evidence.json'); need(se['files']==current and se['sourceFingerprint']==a.fingerprint,'Actual full source evidence differs')
    for mode in ('EditMode','PlayMode'):
        node=ET.parse(reports/(mode+'.xml')).getroot(); count=len(node.findall('.//test-case')); sha=file_record(reports/(mode+'.xml'))['sha256']
        need(node.tag=='test-run' and node.attrib==full['tests'][mode] and node.attrib['result']=='Passed' and int(node.attrib['total'])==int(node.attrib['passed'])==count>0 and all(int(node.attrib[k])==0 for k in ('failed','skipped','inconclusive')) and all(c.attrib['result']=='Passed' for c in node.findall('.//test-case')),'Actual full XML is not wholly passing: '+mode)
        need(sha==full['testsXmlSha256'][mode]==se['reports'][mode]['sha256'] and se['reports'][mode]['file']==mode+'.xml' and se['reports'][mode]['passed']==count,'Actual full XML binding differs')
    for name,code in (('ProbeCoarse1',1),('ProbeCoarse2',0)): original_runner(name,code)
    for name,kind in (('ProbeFine1','probe'),('Shore1','shore'),('Render1','render')):
        need(closed(name,a.fingerprint,current)['kind']==kind,'Wrong078 targeted runner kind: '+name)
    native=native_bind(a.fingerprint,a.guid,current,a.full_run)
    source=sources(); app=app_bind(a.fingerprint,a.guid,build); proof=evidence(reports,a.full_run,a.planning_evidence)
    tree={}
    for row in git('ls-tree','-r','-z','--full-tree',a.snapshot).split(b'\0'):
        if row: meta,path=row.split(b'\t',1); mode,kind,oid=meta.decode().split(); tree[path.decode()]=(mode,kind,oid)
    for name,item in source.items():
        if item['kind']=='file':
            rel=name.removeprefix('FrontierReckoning/'); mode,kind,oid=tree[rel]; need(kind=='blob' and oid==item['gitBlob'] and bool(item['unixMode']&0o111)==(mode=='100755'),'Portable source differs from committed snapshot: '+rel)
    need(a.output.absolute().parent.resolve()==BASE and re.fullmatch(r'Packages078(?:-[A-Za-z0-9._-]+)?',a.output.name) and not a.output.exists() and not a.output.is_symlink(),'Fresh direct Candidate1 Packages078[-suffix] directory required')
    exact_exe=str(BASE/'Build1/Frontier Reckoning.app/Contents/MacOS/Frontier Reckoning'); need(exact_exe not in subprocess.check_output(['/bin/ps','-axo','command='],text=True),'Close actual native app before packaging')
    a.output.mkdir(); identity_data=dict(schema=1,version=VERSION,snapshot=a.snapshot,sourceFingerprint=a.fingerprint,buildGuid=a.guid,finalFullRun=a.full_run,nativeAudit=native,scope='Three technical078 packages only; included original compile failure/coarse/fine/targeted/render/full/build/native evidence retains its original source boundaries; no paidharbor/army/FPS/fullgame acceptance or cloud-upload claim')
    results=[archive(a.output,n,rows,identity_data) for n,rows in [('frontier-reckoning-0.21.0-source',source),('frontier-reckoning-0.21.0-macos',app),('frontier-reckoning-0.21.0-evidence078',proof)]]
    uploads,transport_manifest=transport(a.output,results[2])
    for result in results: result['uploadArtifacts']=uploads if result is results[2] else [dict(file=result['archive'],bytes=result['bytes'],sha256=result['sha256'])]
    snapshot(); need(runtime_map()==current and sources()==source and app_bind(a.fingerprint,a.guid,build)==app and evidence(reports,a.full_run,a.planning_evidence)==proof and native_bind(a.fingerprint,a.guid,current,a.full_run)==native,'Inputs changed during packaging')
    target=a.output/'release-manifest078.json'; target.write_text(json.dumps(dict(**identity_data,logicalZipCount=3,archives=results,uploadArtifacts=[x for r in results for x in r['uploadArtifacts']],transportManifest=transport_manifest,helperSha256=file_record(Path(__file__))['sha256']),indent=2)+'\n'); target.chmod(0o444); print(json.dumps(results,indent=2))
if __name__=='__main__': main()
