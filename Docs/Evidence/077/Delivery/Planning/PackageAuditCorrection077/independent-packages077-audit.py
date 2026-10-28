#!/usr/bin/env python3
"""Read-only independent 077 package audit; never import the preparation helper or run app/Unity/cloud/Git writes.
Run only after the parent declares Packages077 closed. Writes fresh external JSON/Markdown audit receipts only.
"""
import argparse, hashlib, json, os, plistlib, re, stat, struct, subprocess, traceback, unicodedata, zipfile
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path, PurePosixPath
from xml.etree import ElementTree as ET
BASE=Path(__file__).resolve().parents[2]
REPO=Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
FP='8c858fe82932c1c4909af27aea8dab71a183812902c937ee4931003f33840335'
GUID='6d3fd495bd8f48be852c1e6c62bcb02f'; RUNTIME_HEAD='a74fc436dc08c50f9d23dc506df92f7ff2942d76'
PREPARE_SHA='509feb5fb166e3d43f755176c639622e37ba97ae8fc19da4bd2c506b92e147a8'
LIMIT=100*1024*1024; PART_LIMIT=64*1024*1024; CHUNK=1024*1024
ROOTS=('Assets','Packages','ProjectSettings','Tools'); PORTABLE=ROOTS+('SourceAssets','Docs','README.md','.gitignore')
NAMES=['frontier-reckoning-0.20.0-'+x for x in ('source','macos','evidence077')]
class Audit:
 def __init__(self): self.errors=[]; self.inputs={}; self.archives=[]
 def check(self,ok,message):
  if not ok: self.errors.append(message)
 def require(self,ok,message):
  if not ok: raise ValueError(message)
 def record(self,p):
  p=Path(p); s=p.lstat();self.require(stat.S_ISREG(s.st_mode),'Not regular original: '+str(p));sha=hashlib.sha256();size=0
  ident=lambda t:(t.st_dev,t.st_ino,t.st_mode,t.st_size,t.st_mtime_ns,t.st_ctime_ns)
  with os.fdopen(os.open(p,os.O_RDONLY|os.O_NOFOLLOW),'rb') as f:
   self.require(ident(os.fstat(f.fileno()))==ident(s),'Original replaced on open: '+str(p))
   for b in iter(lambda:f.read(CHUNK),b''):sha.update(b);size+=len(b)
   self.require(ident(os.fstat(f.fileno()))==ident(s)==ident(p.lstat()) and size==s.st_size,'Original changed while reading: '+str(p))
  r=dict(path=str(p),bytes=size,sha256=sha.hexdigest(),unixMode=s.st_mode&65535)
  self.check(str(p) not in self.inputs or self.inputs[str(p)]==r,'Original changed between reads: '+str(p));self.inputs[str(p)]=r;return r
 def load(self,p):
  before=self.record(p);raw=Path(p).read_bytes();self.require(hashlib.sha256(raw).hexdigest()==before['sha256'],'JSON changed while reading: '+str(p))
  def pairs(rows):
   d=dict(rows);self.require(len(d)==len(rows),'Duplicate JSON key: '+str(p));return d
  return json.loads(raw,object_pairs_hook=pairs,parse_constant=lambda x:self.require(False,'Nonfinite JSON'))
 def git(self,*args):return subprocess.check_output(['git','--no-replace-objects','-C',str(REPO),*args],env=dict(os.environ,GIT_OPTIONAL_LOCKS='0'))
 def safe(self,name):
  self.require(isinstance(name,str) and name and not name.startswith('/') and '\\' not in name and ':' not in name and all(ord(c)>=32 and ord(c)!=127 for c in name),'Unsafe ZIP path: '+repr(name));parts=name.rstrip('/').split('/');self.require(all(x not in ('','.','..') for x in parts),'Unsafe ZIP path components: '+repr(name));return name
 def namespace(self,names):
  keys=[unicodedata.normalize('NFC',self.safe(n).rstrip('/')).casefold() for n in names];self.require(len(keys)==len(set(keys)),'ZIP case/Unicode/path aliases');return names
 def eof(self,p):
  size=p.stat().st_size
  with p.open('rb') as f:f.seek(max(0,size-65557));tail=f.read()
  off=tail.rfind(b'PK\x05\x06');self.require(off>=0 and off+22<=len(tail),'Missing/truncated ZIP EOCD: '+p.name)
  disk,cdDisk,onDisk,total,cdSize,cdOffset,comment=struct.unpack_from('<HHHHIIH',tail,off+4)
  self.require(off+22+comment==len(tail) and disk==cdDisk==0 and onDisk==total,'ZIP EOCD trailing bytes/multidisk: '+p.name)
  absolute=max(0,size-65557)+off;self.require(total<65535 and cdOffset+cdSize==absolute,'Unsupported ZIP64 central-directory or EOCD span differs: '+p.name)
  return dict(actualFileBytes=size,eocdOffset=absolute,centralDirectoryOffset=cdOffset,centralDirectoryBytes=cdSize,memberCount=total,commentBytes=comment,exactEof=True)
 def link(self,name,target):
  self.require(target and not target.startswith('/') and '\\' not in target and ':' not in target and all(ord(c)>=32 for c in target),'Unsafe symlink target: '+name)
  parts=list(PurePosixPath(name).parent.parts)
  for part in target.split('/'):
   if part in ('','.'):continue
   if part=='..':self.require(len(parts)>1,'Symlink escapes archive root: '+name);parts.pop()
   else:parts.append(part)
  self.require(parts and parts[0]==PurePosixPath(name).parts[0],'Symlink root changed: '+name)
 def archive(self,folder,base,identity):
  p=folder/(base+'.zip');mp=folder/(base+'.manifest.json');m=self.load(mp);r=self.record(p)
  self.check(stat.S_IMODE(p.stat().st_mode)==0o444 and stat.S_IMODE(mp.stat().st_mode)==0o444,'Logical ZIP/manifest is not readonly: '+base)
  self.check(all(m.get(k)==v for k,v in identity.items()),'Archive manifest release identity differs: '+base)
  self.check(m['archive']==p.name and (m['bytes'],m['sha256'])==(r['bytes'],r['sha256']),'Logical ZIP whole bytes/SHA differs: '+base)
  self.check(m['directUploadLimitBytes']==LIMIT and m['requiresTransportParts']==(r['bytes']>=LIMIT),'Archive transport declaration differs: '+base)
  if not base.endswith('evidence077'):self.check(r['bytes']<LIMIT,'Source/app ZIP exceeds strict100MiB: '+base)
  eof=self.eof(p);expected=m['members'];self.namespace(expected);computed={};localOriginals=0
  with zipfile.ZipFile(p) as z:
   infos=z.infolist();self.namespace([i.filename for i in infos]);self.require(len(infos)==len(expected)==eof['memberCount'] and {i.filename for i in infos}==set(expected),'Exact archive membership differs: '+base)
   for i in infos:
    item=expected[i.filename];mode=i.external_attr>>16;kind=item['kind'];self.check(i.create_system==3 and mode==item['unixMode'],'ZIP UNIX mode differs: '+i.filename)
    self.check(not i.flag_bits&1 and i.compress_type==zipfile.ZIP_DEFLATED,'Encrypted/unexpected ZIP compression: '+i.filename)
    typ={'file':stat.S_IFREG,'directory':stat.S_IFDIR,'symlink':stat.S_IFLNK}.get(kind);self.check(typ is not None and stat.S_IFMT(mode)==typ,'ZIP node type differs: '+i.filename)
    self.check(i.filename.endswith('/')==(kind=='directory'),'ZIP directory suffix differs: '+i.filename)
    h=hashlib.sha256();blob=hashlib.sha1(b'blob '+str(i.file_size).encode()+b'\0');size=0;content=bytearray() if kind=='symlink' else None
    with z.open(i) as f:
     for b in iter(lambda:f.read(CHUNK),b''):h.update(b);blob.update(b);size+=len(b);content.extend(b) if content is not None else None
    self.check((size,h.hexdigest())==(item['bytes'],item['sha256']) and i.file_size==size,'ZIP member bytes/SHA differs: '+i.filename)
    if kind=='file':self.check(blob.hexdigest()==item['gitBlob'],'ZIP member Git blob digest differs: '+i.filename)
    if kind=='directory':self.check(size==0,'Nonempty directory member: '+i.filename)
    if kind=='symlink':self.check(bytes(content)==os.fsencode(item['target']),'ZIP symlink target bytes differ: '+i.filename);self.link(i.filename,item['target'])
    source=Path(item['source']);s=source.lstat();self.check(s.st_mode&65535==mode,'Original mode differs: '+str(source))
    if kind=='file':o=self.record(source);self.check((o['bytes'],o['sha256'])==(size,h.hexdigest()),'Original bytes differ from ZIP: '+str(source));localOriginals+=1
    elif kind=='directory':self.check(stat.S_ISDIR(s.st_mode),'Original directory type differs: '+str(source))
    else:self.check(stat.S_ISLNK(s.st_mode) and os.readlink(source)==item['target'],'Original symlink differs: '+str(source))
    computed[i.filename]=dict(kind=kind,bytes=size,sha256=h.hexdigest(),unixMode=mode,**({'gitBlob':blob.hexdigest()} if kind=='file' else {}))
  digest=lambda d:hashlib.sha256(json.dumps(d,sort_keys=True,separators=(',',':')).encode()).hexdigest()
  self.archives.append(dict(archive=p.name,bytes=r['bytes'],sha256=r['sha256'],manifestSha256=self.inputs[str(mp)]['sha256'],exactMembership=True,fullMemberCrcEofRead=True,memberBytesShaModesChecked=len(computed),originalFilesCompared=localOriginals,nodeCounts=dict(Counter(x['kind'] for x in computed.values())),uncompressedRegularFileBytes=sum(x['bytes'] for x in computed.values() if x['kind']=='file'),verifiedMembersDigest=digest(computed),eof=eof))
  return m,computed

def digest_map(d):return hashlib.sha256(json.dumps(d,sort_keys=True,separators=(',',':')).encode()).hexdigest()
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--snapshot',required=True);p.add_argument('--packages',type=Path,default=BASE/'Packages077');p.add_argument('--output-prefix',type=Path,default=BASE/'Diagnostics/packages077-independent-audit2');args=p.parse_args();a=Audit();out=Path(str(args.output_prefix)+'.json');md=Path(str(args.output_prefix)+'.md');a.require(out.parent.resolve().is_relative_to(BASE) and not out.exists() and not md.exists(),'Fresh external audit output required');report={}
 try:
  a.require(re.fullmatch('[0-9a-f]{40}',args.snapshot),'Explicit full snapshot required');folder=args.packages.resolve(strict=True);a.require(folder.parent==BASE and re.fullmatch(r'Packages077(?:-[A-Za-z0-9._-]+)?',folder.name),'Unexpected package directory');release=a.load(folder/'release-manifest077.json');transport=a.load(folder/'transport-manifest077.json');identity=dict(schema=1,version='0.20.0',snapshot=args.snapshot,sourceFingerprint=FP,buildGuid=GUID,finalFullRun='Full2')
  a.check(all(release.get(k)==v for k,v in identity.items()),'Release identity differs from explicit snapshot/runtime');a.check(release['logicalZipCount']==3 and len(release['archives'])==3 and [r['archive'] for r in release['archives']]==[n+'.zip' for n in NAMES],'Exactly three logical ZIPs/order required');a.check(release['helperSha256']==PREPARE_SHA==a.record(BASE/'prepare-packages077.py')['sha256'],'Accepted preparation helper SHA differs')
  a.check(a.git('rev-parse','HEAD').decode().strip()==args.snapshot==a.git('rev-parse','--verify',args.snapshot+'^{commit}').decode().strip(),'Current HEAD differs from exact snapshot');a.check(not a.git('status','--porcelain=v1','-z','--untracked-files=all'),'Snapshot checkout is not clean');a.check(subprocess.run(['git','--no-replace-objects','-C',str(REPO),'merge-base','--is-ancestor',RUNTIME_HEAD,args.snapshot],env=dict(os.environ,GIT_OPTIONAL_LOCKS='0'),capture_output=True).returncode==0,'Runtime commit is not ancestor of source snapshot')
  current={}
  for top in ROOTS:
   for path in sorted((REPO/top).rglob('*')):
    rel=path.relative_to(REPO).as_posix()
    if path.is_file() and not any(s.startswith('.') or s=='__pycache__' for s in Path(rel).parts):current[rel]=a.record(path)['sha256']
  a.check(len(current)==784 and digest_map(current)==FP,'Current complete runtime inventory differs from784/FP')
  maps=[];manifests=[]
  for n,r in zip(NAMES,release['archives']):
   m,c=a.archive(folder,n,identity);maps.append(c);manifests.append(m);a.check((r['archive'],r['bytes'],r['sha256'])==(m['archive'],m['bytes'],m['sha256']),'Release/archive sidecar differs: '+n)
  source,app,evidence=maps;sm,am,em=manifests;tree={}
  for row in a.git('ls-tree','-r','-z','--full-tree',args.snapshot).split(b'\0'):
   if row:
    meta,name=row.split(b'\t',1);mode,kind,oid=meta.decode().split();tree[name.decode()]=(mode,kind,oid)
  def portable(name):
   parts=Path(name).parts
   return parts[0] in PORTABLE and (name=='.gitignore' or not any(x.startswith('.') or x=='__pycache__' for x in parts)) and not (parts[:2]==('Docs','Evidence') and len(parts)>2 and parts[2]!='077')
  gitNames={n for n in tree if portable(n)};archiveNames={n.removeprefix('FrontierReckoning/') for n,x in source.items() if x['kind']=='file'};a.check(archiveNames==gitNames,'Portable source file set differs from declared Git snapshot selection')
  for n,x in source.items():
   a.check(n.startswith('FrontierReckoning/'),'Portable source root differs')
   if x['kind']=='file':
    rel=n.removeprefix('FrontierReckoning/');mode,kind,oid=tree[rel];a.check(kind=='blob' and oid==x['gitBlob'] and bool(x['unixMode']&0o111)==(mode=='100755'),'ZIP source bytes/mode differ from snapshot Git blob: '+rel)
  a.check({n.removeprefix('FrontierReckoning/'):x['sha256'] for n,x in source.items() if x['kind']=='file' and Path(n.removeprefix('FrontierReckoning/')).parts[0] in ROOTS}==current,'Portable runtime source set differs from tested current map')
  appm=a.load(BASE/'Build1/actual-app-manifest077.json');br=a.load(BASE/'Build1/runner-receipt077.json');fr=a.load(BASE/'Full2/runner-receipt077.json');a.check(appm['version']=='0.20.0' and appm['buildGuid']==GUID and appm['sourceFingerprint']==FP and appm['sourceInputCount']==784 and appm['sourceHead']==RUNTIME_HEAD,'Actual app source/GUID identity differs');a.check(br['status']=='closed' and br['exitCode']==0 and br['sourceStable'] is True and br['changedSources']==[] and br['sourcesBefore']==current and br['sourceFingerprintBefore']==br['sourceFingerprintAfter']==FP and br['gitHeadBefore']==RUNTIME_HEAD,'Actual Build1 runner differs');a.check(appm['runnerReceiptSha256']==a.inputs[str(BASE/'Build1/runner-receipt077.json')]['sha256'] and appm['runnerScriptSha256']==a.record(BASE/'run077.py')['sha256'],'App runner SHA bindings differ')
  appPhysical=BASE/'Build1/Frontier Reckoning.app';physicalNames={appPhysical.name+'/'}
  for path in appPhysical.rglob('*'):
   rel=path.relative_to(appPhysical).as_posix();physicalNames.add(appPhysical.name+'/'+rel+('/' if path.is_dir() and not path.is_symlink() else ''))
  a.namespace(physicalNames);a.check(physicalNames==set(app),'Actual physical app node inventory differs from ZIP')
  a.check(a.inputs[str(BASE/'Build1/actual-app-manifest077.json')]['sha256']=='3ba141de6d4faa290b1009b116c2ec65e256efe92c144c79cc1cb6c56dfde0df','Frozen actual app manifest SHA differs')
  appfiles={n.removeprefix('Frontier Reckoning.app/'):x for n,x in app.items() if x['kind']=='file'};expected={x['path']:x for x in appm['files']};a.check(len(expected)==len(appm['files'])==appm['fileCount']==len(appfiles)==298 and set(expected)==set(appfiles) and sum(x['bytes'] for x in appfiles.values())==appm['totalFileBytes']==191066644,'App file set/count/total differs')
  for n,x in expected.items():a.check((appfiles[n]['bytes'],appfiles[n]['sha256'],stat.S_IMODE(appfiles[n]['unixMode']))==(x['bytes'],x['sha256'],int(x['mode'],8)),'App ZIP differs from actual app manifest: '+n)
  syms={n.removeprefix('Frontier Reckoning.app/'):x for n,x in app.items() if x['kind']=='symlink'};a.check(set(syms)=={x['path'] for x in appm['symlinks']} and len(appm['symlinks'])==len(syms),'App symlink set differs')
  for x in appm['symlinks']:
   actual=am['members']['Frontier Reckoning.app/'+x['path']];a.check(actual['target']==x['target'] and ('mode' not in x or stat.S_IMODE(actual['unixMode'])==int(x['mode'],8)),'Actual app symlink target/mode differs from manifest')
  with zipfile.ZipFile(folder/(NAMES[1]+'.zip')) as z:
   plist=plistlib.loads(z.read('Frontier Reckoning.app/Contents/Info.plist'));boot=z.read('Frontier Reckoning.app/Contents/Resources/Data/boot.config').decode();exe='Contents/MacOS/'+plist['CFBundleExecutable'];a.check(plist['CFBundleShortVersionString']=='0.20.0' and plist['CFBundleIdentifier']=='com.frontierworkshop.reckoning' and appfiles[exe]['sha256']==appm['executableSha256'] and appfiles[exe]['unixMode']&stat.S_IXUSR and re.search(r'^build-guid='+GUID+r'$',boot,re.M) is not None and appm['codesignVerification']['exitCode']==0,'App ZIP plist/executable/GUID/signature receipt differs')
  # Independently enumerate the physical evidence allowlist, pruning before descent; never recurse packages/cloud/app.
  postAudit={BASE/'Diagnostics/packages077-independent-audit.json':(539430,'5a78ae2044b1b8819fe6d736745d093cae9b61ec6b32fae72441833b975e400e'),BASE/'Diagnostics/packages077-independent-audit.md':(1583,'23a1ac53464f701c418c944442758e6fcbbcb6c156bee8946a4b8acb8a5db1b9')}
  for path,(size,sha) in postAudit.items():
   item=a.record(path);a.require((item['bytes'],item['sha256'])==(size,sha),'Original first FAIL audit changed: '+str(path))
  desired=set()
  def omit(parts):return any(x.lower().endswith(('.app','.zip','.tar','.tgz','.dmg')) or x.startswith('Frontier Reckoning_BackUp') or x=='__pycache__' or x.lower().startswith(('delivery','cloud','package','upload')) for x in parts)
  def add(root,prefix):
   def walk(path,name):
    if path in postAudit:return # Explicit hash-pinned receipts created after immutable packaging.
    rel=path.relative_to(root)
    if rel.parts and omit(rel.parts):return
    s=path.lstat();a.require(not stat.S_ISLNK(s.st_mode),'Unexpected evidence symlink: '+str(path))
    if stat.S_ISDIR(s.st_mode):desired.add(name+'/');[walk(c,name+'/'+c.name) for c in sorted(path.iterdir())]
    else:a.require(stat.S_ISREG(s.st_mode),'Nonregular evidence node');desired.add(name)
   walk(root,prefix)
  for path in sorted(BASE.iterdir()):
   if path.is_dir() and re.fullmatch(r'(Bank|Compatibility|Mutations|Render|Full|Build|Native)\d+|Diagnostics',path.name):add(path,'Evidence077/Candidate1/'+path.name)
  add(Path(fr['reportsDirectory']),'Evidence077/ActualFull2Reports');hr=a.load(BASE/'Full1/runner-receipt077.json');add(Path(hr['reportsDirectory']),'Evidence077/ActualFull1Reports')
  for path in sorted((REPO/'Docs').iterdir()):
   if path.is_file() and '077' in path.stem:desired.add('Evidence077/Docs/'+path.name)
  if (REPO/'Docs/Evidence/077').exists():add(REPO/'Docs/Evidence/077','Evidence077/Docs/Evidence/077')
  for path in sorted(BASE.iterdir()):
   if path.is_file() and (path.suffix=='.py' or path.name=='integration077.json'):desired.add('Evidence077/Declarations/'+path.name)
  a.check(set(evidence)==desired,'Evidence ZIP differs from independent current allowlist inventory');a.check(evidence['Evidence077/Docs/Delivery-077.md']['kind']=='file','Explicit immediate Delivery077 documentation is not a regular file');a.check(not any(omit(PurePosixPath(n).parts[1:]) for n in evidence if not n.startswith('Evidence077/Declarations/') and n!='Evidence077/Docs/Delivery-077.md'),'Evidence contains excluded package/cloud/app/archive outputs')
  with zipfile.ZipFile(folder/(NAMES[2]+'.zip')) as z:
   j=lambda n:json.loads(z.read(n));full=j('Evidence077/Candidate1/Full2/runner-receipt077.json');failed=j('Evidence077/Candidate1/Full1/runner-receipt077.json');se=j('Evidence077/ActualFull2Reports/source-evidence.json');a.check(full==fr and failed==hr,'Packaged original full receipts differ');a.check(full['status']=='closed' and full['exitCode']==0 and full['sourceStable'] is True and full['changedSources']==[] and full['kind']=='full' and full['gitHeadBefore']==RUNTIME_HEAD and full['sourcesBefore']==current and full['sourceFingerprintBefore']==full['sourceFingerprintAfter']==FP and se['files']==current and se['sourceFingerprint']==FP,'Packaged actual Full2 source binding differs')
   total=0;bad=[];oldRetained=0;newCount=0;historicalFailures=[];testCounts={}
   for mode in ('EditMode','PlayMode'):
    data=z.read('Evidence077/ActualFull2Reports/'+mode+'.xml');node=ET.fromstring(data);cases=node.findall('.//test-case');a.check(node.attrib==full['tests'][mode] and hashlib.sha256(data).hexdigest()==full['testsXmlSha256'][mode]==se['reports'][mode]['sha256'] and node.get('result')=='Passed' and int(node.get('total'))==int(node.get('passed'))==len(cases) and all(int(node.get(k))==0 for k in ('failed','skipped','inconclusive')) and all(c.get('result')=='Passed' for c in cases),'Packaged final XML not wholly passing/bound '+mode);total+=len(cases);testCounts[mode]=len(cases);bad.extend(c.get('fullname') for c in cases if c.get('result')!='Passed')
    oldPath=REPO/'Builds/Tests/run-JENFOStU'/(mode+'.xml');oldRecord=a.record(oldPath);a.check(oldRecord['sha256']=={'EditMode':'85b25538f34eced2090124fa31e595f2d94b7340e823c23cf91c53ded65b6ad4','PlayMode':'602b205f8d22cc4fbc4ae6fe105d48c229778e34393d4e1bad35d3603460823a'}[mode],'Original C2 XML SHA differs');old=ET.fromstring(oldPath.read_bytes());a.check(all(c.get('result')=='Passed' for c in old.findall('.//test-case')),'Original C2 XML has nonpassing cases');oldNames={c.get('fullname') for c in old.findall('.//test-case')};newNames={c.get('fullname') for c in cases};a.check(len(newNames)==len(cases) and oldNames<=newNames,'Packaged final old-case identities missing/duplicate '+mode);oldRetained+=len(oldNames);newCount+=len(newNames-oldNames)
    data=z.read('Evidence077/ActualFull1Reports/'+mode+'.xml');prior=ET.fromstring(data);a.check(prior.attrib==failed['tests'][mode] and hashlib.sha256(data).hexdigest()==failed['testsXmlSha256'][mode],'Historical failed Full1 XML was changed/rebound '+mode)
    historicalFailures.extend(dict(fullname=c.get('fullname'),message=c.findtext('failure/message'),stackTrace=c.findtext('failure/stack-trace')) for c in prior.findall('.//test-case') if c.get('result')!='Passed')
   a.check(total==1824 and oldRetained==1803 and newCount==21 and not bad,'Packaged final actual old/new test counts differ');a.check(failed['status']=='closed' and failed['exitCode']==2 and failed['sourceStable'] is True and failed['changedSources']==[] and digest_map(failed['sourcesBefore'])==failed['sourceFingerprintBefore'] and failed['sourceFingerprintBefore']==failed['sourceFingerprintAfter']=='b7783d5d2af85f67a26ba96f32591e9f20664e56cf3f11693d29095ab5ab1484' and len(historicalFailures)==1 and 'RiverBankMesh077Tests.cs:28' in historicalFailures[0]['stackTrace'] and 'Evidence077/ActualFull1Reports/source-evidence.json' not in evidence,'Historical failed Full1 original boundary differs');a.check(j('Evidence077/Candidate1/Diagnostics/full1-independent-audit077.json')['status']=='FAIL' and j('Evidence077/Candidate1/Diagnostics/full2-independent-audit077.json')['status']=='PASS','Original failed/final independent audit statuses not retained')
  with zipfile.ZipFile(folder/(NAMES[2]+'.zip')) as z:
   for n,sha in [('full1-independent-audit077.json','1872c714726da8d2256cd756c76839bc1e8ed6537ffe2ca2509f2d3b10159063'),('full2-independent-audit077.json','7fed2ea9afa6ea352fd9e7684d5d4ae967d4e010a2744c0108820452b9ffcbfe')]:a.check(hashlib.sha256(z.read('Evidence077/Candidate1/Diagnostics/'+n)).hexdigest()==sha,'Frozen full-run independent receipt SHA differs: '+n)
  for logical in release['archives'][:2]:a.check(logical['uploadArtifacts']==[dict(file=logical['archive'],bytes=logical['bytes'],sha256=logical['sha256'])],'Source/app direct upload declaration differs')
  a.check(all(stat.S_IMODE((folder/n).stat().st_mode)==0o444 for n in ('release-manifest077.json','transport-manifest077.json')),'Release/transport manifests are not readonly')
  evidenceLogical={k:release['archives'][2][k] for k in ('archive','bytes','sha256')};a.check(transport['logicalArchive']==evidenceLogical and transport['fullArchiveKeptLocal'] is True and transport['partLimitBytesExclusive']==PART_LIMIT,'Transport logical identity differs');parts=transport['orderedParts'];uploads=transport['uploadArtifacts'];partRows=[];joined=hashlib.sha256();joinedSize=0;concatExact=True
  if evidenceLogical['bytes']>=LIMIT:
   a.check(parts==uploads==release['archives'][2]['uploadArtifacts'] and transport['fullArchiveUploadAllowed'] is False and transport['transport']=='ordered-lossless-byte-parts' and transport['concatenationByteForByteVerified'] is True,'Oversized evidence transport declaration differs')
   with (folder/evidenceLogical['archive']).open('rb') as full:
    for idx,item in enumerate(parts,1):
     expected=evidenceLogical['archive']+f'.part-{idx:03d}';a.require(item['file']==expected and 0<item['bytes']<PART_LIMIT,'Part name/order/strict64MiB differs');part=folder/item['file'];r=a.record(part);a.check((r['bytes'],r['sha256'])==(item['bytes'],item['sha256']) and stat.S_IMODE(part.stat().st_mode)==0o444,'Part original SHA/mode differs');a.check(item['bytes']==min(PART_LIMIT-1,evidenceLogical['bytes']-joinedSize),'Part exact split size differs');
     with part.open('rb') as f:
      for b in iter(lambda:f.read(CHUNK),b''):
       equal=full.read(len(b))==b;concatExact=concatExact and equal;a.check(equal,'Parts concatenation differs byte-for-byte');joined.update(b);joinedSize+=len(b)
     partRows.append(dict(**item,order=idx))
    a.check(full.read(1)==b'' and (joinedSize,joined.hexdigest())==(evidenceLogical['bytes'],evidenceLogical['sha256']),'Parts reassembled whole ZIP size/SHA differs')
  else:a.check(not parts and transport['fullArchiveUploadAllowed'] is True and uploads==[dict(file=evidenceLogical['archive'],bytes=evidenceLogical['bytes'],sha256=evidenceLogical['sha256'])],'Direct evidence upload declaration differs')
  a.check(release['uploadArtifacts']==[x for r in release['archives'] for x in r['uploadArtifacts']],'Release upload artifact order differs');a.check(all(x['bytes']<LIMIT for x in release['uploadArtifacts']),'Upload artifact exceeds strict100MiB');a.check(release['transportManifest']['file']=='transport-manifest077.json' and (release['transportManifest']['bytes'],release['transportManifest']['sha256'])==(a.inputs[str(folder/'transport-manifest077.json')]['bytes'],a.inputs[str(folder/'transport-manifest077.json')]['sha256']),'Release transport-manifest bytes/SHA differs')
  expectedFiles={n+'.zip' for n in NAMES}|{n+'.manifest.json' for n in NAMES}|{'release-manifest077.json','transport-manifest077.json'}|{x['file'] for x in parts};a.check({p.name for p in folder.iterdir()}==expectedFiles and all(p.is_file() and not p.is_symlink() for p in folder.iterdir()),'Exact package output file set differs')
  a.check(a.git('rev-parse','HEAD').decode().strip()==args.snapshot and not a.git('status','--porcelain=v1','-z','--untracked-files=all'),'Snapshot changed during independent audit')
  for original in list(a.inputs.values()):a.record(Path(original['path']))
  report['auditorCorrection']=dict(originalAuditorSha256='a0f69ac7ea2dbf2c7ee31f354710fd4fc7c3c4fd6073d28366ad9e402399b84f',originalAuditStatus='FAIL',originalSoleError='Evidence contains excluded package/cloud/app/archive outputs',cause='Extra exclusion predicate misclassified explicitly included immediate077 documentation as a delivery artifact.',exactDocumentationExemption='Evidence077/Docs/Delivery-077.md',hashPinnedPostPackageReceipts=[dict(path=str(path),bytes=size,sha256=sha) for path,(size,sha) in postAudit.items()],originalPackagedAuditorAndFailureReceiptsUnchanged=True,nestedDeliveryCloudExclusionRetained=True)
  report.update(identity=identity,logicalZipCount=3,actualArchives=a.archives,exactPackageOutputFiles=sorted(expectedFiles),currentRuntimeInputs=len(current),currentRuntimeFingerprint=digest_map(current),sourceSnapshotFileCount=len(gitNames),appManifestSha256=a.inputs[str(BASE/'Build1/actual-app-manifest077.json')]['sha256'],appFiles=len(appfiles),appFileBytes=appm['totalFileBytes'],finalFull2=dict(testCounts=testCounts,total=total,oldC2Retained=oldRetained,newCases=newCount),historicalFailedFull1=dict(exitCode=failed['exitCode'],sourceFingerprint=failed['sourceFingerprintBefore'],exactFailures=historicalFailures,passingSourceEvidenceExists=False),transport=dict(logicalArchive=evidenceLogical,orderedParts=partRows,remoteUploadPerformed=False,wholeConcatBytes=joinedSize if parts else None,wholeConcatSha256=joined.hexdigest() if parts else None,byteForByteMatchesFull=bool(parts) and concatExact and (joinedSize,joined.hexdigest())==(evidenceLogical['bytes'],evidenceLogical['sha256'])),physicalEvidenceExpectedMembers=len(desired))
 except Exception as e:a.errors.append(type(e).__name__+': '+str(e));report['exceptionTrace']=traceback.format_exc()
 report.update(schema=1,kind='independent-actual-packages077-audit',recordedAtUtc=datetime.now(timezone.utc).isoformat(),status='PASS' if not a.errors else 'FAIL',errors=a.errors,scope='Local three logical ZIPs/actual original member integrity/source snapshot/app identity/transport bytes only. No cloud upload, native gameplay, paid lifecycle, FPS, human win, historical screenshot equivalence or whole-goal completion.',auditorSha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),inputs=list(a.inputs.values()))
 # New audit receipts are intentionally post-package and not added to immutable ZIPs.
 out.parent.mkdir(parents=True,exist_ok=True);jb=(json.dumps(report,indent=2,ensure_ascii=False)+'\n').encode();lines=['# Independent Packages077 audit — '+report['status'],'','Scope: local technical package integrity only; no upload or new gameplay acceptance.','', 'Explicit snapshot: `'+args.snapshot+'`','Runtime FP: `'+FP+'`; GUID: `'+GUID+'`; final full: `Full2`.','','| Logical ZIP | Bytes | SHA256 | Fully read members | Exact EOF |','|---|---:|---|---:|---|']
 for r in a.archives:lines.append(f"| {r['archive']} | {r['bytes']} | `{r['sha256']}` | {r['memberBytesShaModesChecked']} | {r['eof']['exactEof']} |")
 if 'finalFull2' in report:lines+=['',f"Actual packaged Full2: {report['finalFull2']['total']}/1824 cases; exact old C2 {report['finalFull2']['oldC2Retained']} retained, {report['finalFull2']['newCases']} new. Original Full1 remains exit2 with its exact initial palette failure and no passing source evidence.",'',f"App: {report['appFiles']} files / {report['appFileBytes']} bytes; current runtime {report['currentRuntimeInputs']} inputs; source Git snapshot {report['sourceSnapshotFileCount']} files.",'', 'Evidence whole ZIP retained locally. Source/app strict<100MiB; declared transport parts strict<64MiB, streamed order/byte-for-byte concatenation/whole SHA verified. No entries, PNGs, modes or original files were modified.']
 lines+=['','Auditor-only correction: exactly immediate `Evidence077/Docs/Delivery-077.md` is allowed as documentation; nested delivery/cloud outputs remain excluded. The original packaged auditor and its first FAIL JSON/Markdown remain unchanged; those two hash-pinned post-package audit receipts alone are excluded from physical evidence reinventory.','']
 lines+=['','Errors: '+('none.' if not a.errors else ''),*['- '+e for e in a.errors],'','The JSON contains original input bytes/SHA/modes and archive/member verification digests. These new audit receipts are post-package, outside immutable ZIPs and not claimed uploaded.'];mb=('\n'.join(lines)+'\n').encode()
 with out.open('xb') as f:f.write(jb)
 with md.open('xb') as f:f.write(mb)
 for path in (out,md):print(json.dumps(dict(path=str(path),bytes=path.stat().st_size,sha256=hashlib.sha256(path.read_bytes()).hexdigest())))
 print(json.dumps(dict(status=report['status'],errors=a.errors)));return 0 if not a.errors else 2
if __name__=='__main__':raise SystemExit(main())
