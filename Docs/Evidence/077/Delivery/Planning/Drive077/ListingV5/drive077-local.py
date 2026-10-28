#!/usr/bin/env python3
"""Future-only local Drive077 planning/full-byte verification. No Drive write API.
The readback command consumes a fresh authenticated connector reference on stdin;
references/credentials are never stored in receipts or placed on curl's argv.
"""
import argparse, hashlib, json, os, re, shutil, stat, struct, subprocess, sys
from pathlib import Path
from urllib.parse import urlparse
BASE=Path(__file__).resolve().parents[3]
PARENT='1Dgm7Nx9Qwdaz-vhClbRekJ134tEOW4GH'; C1='1CgcNrTzLaVKXoDwtBTaYJ1I2mD7Wegnv'
FOLDER='0.20.0 · 077 曲河内外岸 · Candidate1'; LIMIT=100*1024*1024; PART_LIMIT=64*1024*1024; CHUNK=1024*1024

def need(ok,message):
    if not ok: raise ValueError(message)
def load(p): return json.loads(Path(p).read_text())
def stdin_json():
    line=sys.stdin.readline(8*1024*1024); need(line.endswith('\n'),'Expected one bounded JSON line on stdin'); return json.loads(line)
def name(s):
    need(s and Path(s).name==s and not any(c in s for c in '/\\\x00\r\n:'),'Unsafe artifact filename'); return s
def file(p):
    p=Path(p); before=p.lstat(); need(stat.S_ISREG(before.st_mode),'Not regular input: '+str(p)); h=hashlib.sha256(); size=0
    with os.fdopen(os.open(p,os.O_RDONLY|os.O_NOFOLLOW),'rb') as f:
        opened=os.fstat(f.fileno()); need((before.st_dev,before.st_ino)==(opened.st_dev,opened.st_ino),'Input changed before open')
        for block in iter(lambda:f.read(CHUNK),b''): h.update(block); size+=len(block)
    after=p.lstat(); need((before.st_dev,before.st_ino,before.st_mode,before.st_size,before.st_mtime_ns,before.st_ctime_ns)==(after.st_dev,after.st_ino,after.st_mode,after.st_size,after.st_mtime_ns,after.st_ctime_ns),'Input changed')
    return dict(bytes=size,sha256=h.hexdigest())
def owner(m):
    p=m.get('permissions'); need(m.get('shared') is False and m.get('source_visibility_status')=='permission_metadata_available' and p and all(x.get('type')=='user' and x.get('role')=='owner' for x in p),'Fresh owner-only permissions required')
    identities=[x.get('emailAddress') or x.get('id') for x in p]; need(all(identities) and len(identities)==len(set(identities)),'Owner identity missing/duplicated'); return sorted(identities)
def declared_part_facts(plan):
    packages=Path(plan['packages']); tp=packages/'transport-manifest077.json'
    need(file(tp)['sha256']==plan['transportManifestSha256'],'Transport manifest changed'); t=load(tp); result=[]
    for order,part in enumerate(t['orderedParts'],1):
        expected=t['logicalArchive']['archive']+f'.part-{order:03d}'
        need(t['logicalArchive']['archive']=='frontier-reckoning-0.20.0-evidence077.zip' and part['file']==expected,'Declared evidence transport order differs')
        matches=[x for x in plan['files'] if x['name']==expected]; need(len(matches)==1,'Declared evidence part missing/duplicated'); item=matches[0]
        need(item['mime']=='application/octet-stream' and Path(item['path'])==packages/expected and all(item[k]==part[k] for k in ('bytes','sha256')),'Declared evidence part differs')
        result.append(dict(file=expected,bytes=part['bytes'],sha256=part['sha256'],transportOrder=order,requestedMime=item['mime']))
    return result
def artifact_name_matches(plan,item,observed_mime,artifact_name):
    if artifact_name==item['name']: return True
    return observed_mime=='application/octet-stream' and item['mime']=='application/octet-stream' and artifact_name==item['name']+'.bin' and any(x['file']==item['name'] and all(x[k]==item[k] for k in ('bytes','sha256')) for x in declared_part_facts(plan))
def first_part_policy(plan):
    packages=Path(plan['packages']); tp=packages/'transport-manifest077.json'
    need(file(tp)['sha256']==plan['transportManifestSha256'],'Transport manifest changed')
    t=load(tp)
    if not t['orderedParts']: return None
    first=t['orderedParts'][0]; expected='frontier-reckoning-0.20.0-evidence077.zip.part-001'
    need(first['file']==expected==t['logicalArchive']['archive']+'.part-001','First evidence transport position differs')
    matches=[x for x in plan['files'] if x['name']==expected]; need(len(matches)==1,'First evidence part missing/duplicated'); item=matches[0]
    need(item['mime']=='application/octet-stream' and Path(item['path'])==packages/expected and 0<item['bytes']<PART_LIMIT and all(item[k]==first[k] for k in ('bytes','sha256')),'First evidence part declaration differs')
    need(file(item['path'])=={k:item[k] for k in ('bytes','sha256')},'First evidence part bytes changed')
    with os.fdopen(os.open(item['path'],os.O_RDONLY|os.O_NOFOLLOW),'rb') as f: magic=f.read(4)
    need(magic==b'PK\x03\x04','First evidence part lacks ZIP local-header magic')
    return dict(file=expected,bytes=item['bytes'],sha256=item['sha256'],requestedMime='application/octet-stream',observedMime='application/x-zip',transportOrder=1,localMagicHex=magic.hex())
def mime_matches(plan,item,observed):
    if observed==item['mime']: return True
    if item['mime']!='application/octet-stream' or observed!='application/x-zip': return False
    p=first_part_policy(plan)
    return bool(p and p['file']==item['name'] and all(p[k]==item[k] for k in ('bytes','sha256')))
def remote(m,item,folder,expected_id,plan):
    owner(m); need(m['id']==expected_id and expected_id and m['title']==item['name'] and m['parent_ids']==[folder] and int(m['size'])==item['bytes'] and mime_matches(plan,item,m['mime_type']),'Remote file metadata differs')
def output(p):
    p=Path(p).absolute(); need(p.parent.resolve().is_relative_to(BASE) and not p.exists() and not p.is_symlink(),'Fresh external Candidate1 output required'); return p

def prepare(a):
    packages=Path(a.packages).resolve(strict=True); release=load(packages/'release-manifest077.json'); transport=load(packages/'transport-manifest077.json')
    need(packages.is_relative_to(BASE) and release['version']=='0.20.0' and release['logicalZipCount']==3,'Completed 0.20.0 three-package release required')
    need(re.fullmatch('[0-9a-f]{40}',release['snapshot']) and re.fullmatch('[0-9a-f]{64}',release['sourceFingerprint']) and re.fullmatch('[0-9a-f]{32}',release['buildGuid']),'Release identity invalid')
    need(re.fullmatch(r'Full[1-9]\d*',release['finalFullRun']),'Explicit final full-run release binding required')
    need({x['archive'] for x in release['archives']}=={'frontier-reckoning-0.20.0-'+s+'.zip' for s in ('source','macos','evidence077')} and len(release['archives'])==3,'Logical ZIP identities differ')
    need(release['uploadArtifacts']==[x for archive in release['archives'] for x in archive['uploadArtifacts']],'Release upload order differs')
    need(file(packages/'transport-manifest077.json')=={k:release['transportManifest'][k] for k in ('bytes','sha256')} and release['transportManifest']['file']=='transport-manifest077.json','Transport manifest binding differs')
    evidence=next(x for x in release['archives'] if x['archive'].endswith('evidence077.zip'))
    need(transport['logicalArchive']=={k:evidence[k] for k in ('archive','bytes','sha256')} and transport['uploadArtifacts']==evidence['uploadArtifacts'],'Evidence transport differs')
    need((transport['orderedParts']==evidence['uploadArtifacts'] and evidence['bytes']>=LIMIT and transport['fullArchiveUploadAllowed'] is False) if transport['orderedParts'] else (evidence['bytes']<LIMIT and evidence['uploadArtifacts']==[dict(file=evidence['archive'],bytes=evidence['bytes'],sha256=evidence['sha256'])]),'Evidence transport choice differs')
    out=output(a.out); out.mkdir(); rows=[]
    def add(p,mime,expected=None):
        p=Path(p).absolute(); n=name(p.name); observed=file(p); need(expected is None or observed==expected,'Local artifact differs: '+n); need(observed['bytes']<LIMIT,'Upload artifact exceeds 100 MiB')
        need(n not in {x['name'] for x in rows},'Duplicate artifact name'); rows.append(dict(name=n,path=str(p),mime=mime,**observed))
    for logical in release['archives']:
        need(file(packages/name(logical['archive']))=={k:logical[k] for k in ('bytes','sha256')},'Local logical ZIP differs')
        manifest=packages/(logical['archive'][:-4]+'.manifest.json'); lm=load(manifest)
        need(all(lm[k]==release[k] for k in ('version','snapshot','sourceFingerprint','buildGuid','finalFullRun')) and all(lm[k]==logical[k] for k in ('archive','bytes','sha256')),'Logical ZIP manifest identity differs'); add(manifest,'application/json')
    for x in release['uploadArtifacts']:
        add(packages/name(x['file']),'application/octet-stream' if '.part-' in x['file'] else 'application/zip',{k:x[k] for k in ('bytes','sha256')})
        if '.part-' in x['file']: need(x['bytes']<PART_LIMIT,'Evidence part must be strictly below64 MiB')
    for n in ('release-manifest077.json','transport-manifest077.json'): add(packages/n,'application/json')
    app=BASE/'Build1/actual-app-manifest077.json'; am=load(app); need(am['version']=='0.20.0' and am['buildGuid']==release['buildGuid'] and am['sourceFingerprint']==release['sourceFingerprint'],'App manifest differs'); add(app,'application/json')
    need(a.native_png,'Select at least one actual completed native PNG explicitly')
    for value in a.native_png:
        p=Path(value).resolve(strict=True); need(p.is_relative_to(BASE/'Native1') and p.suffix=='.png','Choose actual Candidate1/Native1 PNG')
        metadata=load(p.with_suffix('.json'))
        with p.open('rb') as f: raw_header=f.read(24)
        need(raw_header[:8]==b'\x89PNG\r\n\x1a\n','Native image is not PNG')
        need(metadata['appVersion']=='0.20.0' and metadata['buildGuid']==release['buildGuid'] and metadata['mapId']=='winding' and metadata['pngFile']==p.name and metadata['pngSha256']==file(p)['sha256'] and metadata['requestMatchId']==metadata['matchId'] and metadata['requestFrame']<=metadata['captureFrame'],'Native completion/identity differs')
        need(metadata['inMenu'] is False and metadata['matchId'] and metadata['hudMatchId']==metadata['matchId'] and metadata['width']>0 and metadata['height']>0,'Native gameplay/HUD identity differs')
        need(struct.unpack('>II',raw_header[16:24])==(metadata['width'],metadata['height']),'Native dimensions differ'); add(p,'image/png'); add(p.with_suffix('.json'),'application/json')
    readme=out/'README-077.md'; readme.write_text('# Frontier Reckoning 0.20.0 / 077\n\nSnapshot: '+release['snapshot']+'\nSource SHA256: '+release['sourceFingerprint']+'\nBuild GUID: '+release['buildGuid']+'\n\nThree logical packages: portable source, actual macOS app, complete 077 evidence. Large evidence is transported as ordered byte parts, not independently extractable ZIPs. Follow transport-manifest077.json orderedParts; concatenate in that order and verify the full logical ZIP SHA256/size before extraction. Original PNG bytes are preserved.\n\nSelected native images are actual Winding client PNG/JSON pairs for this app GUID. Scope: normal start/manual Explorer camera, Move, Hold and F9 only where supported by original receipts; not paid harbor, medical, balance, FPS, victory or complete gameplay acceptance. Controlled render historical-ground comparisons are reconstructed inside the new build, not original old-build screenshots. Historical failures retain their own source identities. C1 and older releases remain unchanged.\n')
    add(readme,'text/markdown'); checksums=out/'checksums.sha256'; checksums.write_text(''.join(x['sha256']+'  '+x['name']+'\n' for x in rows)); add(checksums,'text/plain')
    plan=dict(schema=1,state='LOCAL_PLAN_NOT_UPLOADED',parentId=PARENT,protectedC1Id=C1,folderName=FOLDER,packages=str(packages),releaseIdentity={k:release[k] for k in ('version','snapshot','sourceFingerprint','buildGuid','finalFullRun')},releaseManifestSha256=file(packages/'release-manifest077.json')['sha256'],transportManifestSha256=file(packages/'transport-manifest077.json')['sha256'],logicalZipCount=3,files=rows)
    target=out/'upload-plan077.json'; target.write_text(json.dumps(plan,indent=2)+'\n'); print(json.dumps(dict(plan=str(target),fileCount=len(rows),state=plan['state'])))
def verify_plan(a):
    plan=load(a.plan)
    for item in plan['files']: need(file(item['path'])=={k:item[k] for k in ('bytes','sha256')},'Planned bytes changed: '+item['name'])
    need(file(Path(plan['packages'])/'release-manifest077.json')['sha256']==plan['releaseManifestSha256'] and file(Path(plan['packages'])/'transport-manifest077.json')['sha256']==plan['transportManifestSha256'],'Release/transport manifest changed'); print(json.dumps(dict(plan,providerMimeException=first_part_policy(plan),declaredTransportParts=declared_part_facts(plan))))
def journal(a):
    directory=Path(a.directory).absolute(); need(directory.resolve().is_relative_to(BASE),'Journal outside Candidate1'); directory.mkdir(parents=True,exist_ok=True)
    target=directory/name(a.name); need(not target.exists(),'Journal entry exists')
    value=stdin_json()
    with target.open('x') as f: json.dump(value,f,indent=2); f.write('\n')
    print(json.dumps(dict(path=str(target))))
def readback(a):
    plan=load(a.plan); item=next(x for x in plan['files'] if x['name']==a.name); request=stdin_json(); metadata=request['metadata']; remote(metadata,item,request['folderId'],request['fileId'],plan)
    need(request['folderId'] not in (PARENT,C1),'Protected folder is not a release candidate')
    directory=Path(a.directory).absolute(); need(directory.resolve().is_relative_to(BASE),'Readback outside Candidate1'); directory.mkdir(parents=True,exist_ok=True)
    final=directory/('readback-'+name(item['name'])); partial=output(str(final)+'.partial'); need(not final.exists(),'Readback already exists')
    reference=request['reference']; local=reference.get('workspace_path'); uri=reference.get('file_uri'); artifact_name=request['artifactName']
    need(artifact_name_matches(plan,item,metadata['mime_type'],artifact_name),'Connector artifact filename differs')
    if isinstance(uri,dict):
        # file_id names an opaque connector artifact, not the Drive file ID.
        if 'file_id' in uri: need(isinstance(uri['file_id'],str) and uri['file_id'].strip(),'Authenticated reference artifact identity missing')
        for key,expected in (('file_name',artifact_name),('mime_type',metadata['mime_type'])):
            if key in uri: need(uri[key]==expected,'Authenticated reference '+key+' binding differs')
        local=local or uri.get('workspace_path'); uri=uri.get('download_url') or uri.get('url') or uri.get('uri')
    if local:
        need(Path(local).is_absolute(),'Materialized reference must be absolute'); before=file(local)
        with Path(local).open('rb') as src,partial.open('xb') as dst: shutil.copyfileobj(src,dst,CHUNK)
        need(file(local)==before,'Materialized reference changed'); route='authenticated_connector_materialized_workspace_path'
    else:
        parsed=urlparse(uri or ''); need(parsed.scheme=='https' and parsed.hostname and not parsed.username and not parsed.password and not any(ord(c)<32 for c in uri),'Expected exact authenticated HTTPS connector file_uri')
        command=['/usr/bin/curl','--ipv4','--noproxy','*','--fail','--location','--proto','=https','--proto-redir','=https','--silent','--show-error','--connect-timeout','20','--max-time','900','--max-filesize',str(item['bytes']),'--output',str(partial),'--write-out','%{http_code}','--config','-']
        result=subprocess.run(command,input='url = '+json.dumps(uri)+'\n',text=True,capture_output=True); need(result.returncode==0 and result.stdout.strip()=='200','Authenticated full-byte download failed; partial retained, fresh fetch required')
        route='authenticated_connector_file_uri_direct_ipv4_https'
    observed=file(partial); need(observed=={k:item[k] for k in ('bytes','sha256')},'Whole-file bytes/SHA differ; partial retained'); partial.rename(final); final.chmod(0o444)
    target=directory/('verified-'+name(item['name'])+'.json')
    receipt=dict(name=item['name'],id=metadata['id'],metadata=metadata,localReadback=str(final),receiptPath=str(target),wholeFileRead=True,byteShaMatches=True,transport=route,requestedMime=item['mime'],observedMime=metadata['mime_type'],providerMimeNormalized=metadata['mime_type']!=item['mime'],connectorArtifactName=artifact_name,**observed)
    with target.open('x') as f: json.dump(receipt,f,indent=2); f.write('\n')
    print(json.dumps(receipt))
def finish(a):
    plan=load(a.plan); live=stdin_json(); folder=live['folder']; need(folder['title']==plan['folderName'] and folder['parent_ids']==[PARENT] and folder['mime_type']=='application/vnd.google-apps.folder','Final folder identity differs')
    need(live['parent']['id']==PARENT and folder['id'] not in (PARENT,C1) and owner(live['parent'])==owner(folder),'Parent/folder owner or ID differs'); verified=[]; ids=[]
    for item in plan['files']:
        rp=Path(live['readbacks'][item['name']]); need(rp.resolve().is_relative_to(BASE),'Receipt outside Candidate1'); r=load(rp); m=live['filesMetadata'][r['id']]; remote(m,item,folder['id'],r['id'],plan); need(owner(m)==owner(folder),'File owner differs')
        need(r['name']==item['name'] and r['receiptPath']==str(rp) and Path(r['localReadback']).resolve().is_relative_to(BASE),'Readback receipt identity differs')
        need(r['wholeFileRead'] is True and r['byteShaMatches'] is True and file(r['localReadback'])=={k:item[k] for k in ('bytes','sha256')},'Actual raw readback differs'); verified.append(r); ids.append(r['id'])
    need(len(ids)==len(set(ids)) and set(ids)=={x['id'] for x in live['folderChildren']} and len(ids)==len(live['folderChildren']),'Exact folder child ID set differs')
    need(all(x['parent_ids']==[folder['id']] for x in live['folderChildren']),'Final child parent differs')
    need({x['id'] for x in live['c1Before']}=={x['id'] for x in live['c1After']},'C1 direct child set changed')
    need({x['id'] for x in live['projectAfter']}=={x['id'] for x in live['projectBefore']}|{folder['id']},'Project child set changed unexpectedly')
    transport=load(Path(plan['packages'])/'transport-manifest077.json'); joined=None
    if transport['orderedParts']:
        h=hashlib.sha256(); size=0; mapping={x['name']:x for x in verified}; original=Path(plan['packages'])/transport['logicalArchive']['archive']
        with original.open('rb') as full:
            for part in transport['orderedParts']:
                with Path(mapping[part['file']]['localReadback']).open('rb') as f:
                    for block in iter(lambda:f.read(CHUNK),b''): need(full.read(len(block))==block,'Remote part concatenation differs'); h.update(block); size+=len(block)
            need(full.read(1)==b'' and (size,h.hexdigest())==(transport['logicalArchive']['bytes'],transport['logicalArchive']['sha256']),'Remote evidence whole SHA/size differs')
        joined=dict(bytes=size,sha256=h.hexdigest(),byteForByteMatchesOriginal=True,partsAreIndependentZipFiles=False)
    target=output(Path(a.directory)/'drive077-final-verified.json'); target.parent.mkdir(parents=True,exist_ok=True)
    receipt=dict(schema=1,status='all_planned_files_whole_bytes_sha_owner_parent_exact_childset_verified',releaseIdentity=plan['releaseIdentity'],folder=folder,projectParent=live['parent'],fileCount=len(verified),files=verified,remoteEvidenceConcatenation=joined,c1ChildIdsUnchanged=True,scope='Private technical delivery verification only; final receipt remains outside immutable ZIPs/cloud file set and is not claimed uploaded')
    with target.open('x') as f: json.dump(receipt,f,indent=2); f.write('\n')
    print(json.dumps(dict(receipt=str(target),status=receipt['status'],fileCount=len(verified))))
def main():
    parser=argparse.ArgumentParser(description=__doc__); sub=parser.add_subparsers(dest='action',required=True)
    p=sub.add_parser('prepare'); p.add_argument('--packages',required=True); p.add_argument('--native-png',action='append',required=True); p.add_argument('--out',required=True)
    p=sub.add_parser('verify-plan'); p.add_argument('--plan',required=True)
    p=sub.add_parser('journal'); p.add_argument('--directory',required=True); p.add_argument('--name',required=True)
    p=sub.add_parser('readback'); p.add_argument('--plan',required=True); p.add_argument('--name',required=True); p.add_argument('--directory',required=True)
    p=sub.add_parser('finish'); p.add_argument('--plan',required=True); p.add_argument('--directory',required=True)
    args=parser.parse_args(); {'prepare':prepare,'verify-plan':verify_plan,'journal':journal,'readback':readback,'finish':finish}[args.action](args)
if __name__=='__main__': main()
