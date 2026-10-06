#!/usr/bin/env python3
"""Verify the explicit current-source subset plus original client/gallery packages."""
import argparse
import hashlib
import importlib.util
import json
import os
import re
import subprocess
import sys
import zipfile
from datetime import datetime,timezone
from pathlib import Path
D=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('frozen_full_package071_checker',D/'independent-package071-review.py')
L=importlib.util.module_from_spec(spec);spec.loader.exec_module(L)


def batch_directory(path):
    match=re.match(r'^Docs/Evidence/(\d{3})/',path)
    return 'Docs/Evidence/'+match.group(1) if match and match.group(1)not in ('070','071') else None


def git_subset(repo,commit):
    L.require(L.git(repo,'rev-parse','--verify',commit+'^{commit}').decode().strip()==commit,'Wrong exact source commit')
    algorithm=L.git(repo,'rev-parse','--show-object-format').decode().strip()
    tree=L.git(repo,'ls-tree','-r','-z','--full-tree',commit);allrows={}
    for raw in tree.split(b'\0'):
        if not raw:continue
        header,rawpath=raw.split(b'\t',1);mode,kind,oid=header.split()
        L.require(kind==b'blob','Non-blob tracked paths cannot be omitted silently')
        path=rawpath.decode('utf-8');L.safe(path);L.require(path not in allrows,'Duplicate Git path')
        allrows[path]={'path':path,'gitTreeMode':mode.decode(),'gitObjectId':oid.decode()}
    selected={p:r for p,r in allrows.items()if not batch_directory(p)}
    excluded={p:dict(r,excludedByDirectory=batch_directory(p))for p,r in allrows.items()if batch_directory(p)}
    result={'source/':L.record('source/','directory',0)};cache={}
    linkoids={r['gitObjectId']for r in selected.values()if r['gitTreeMode']=='120000'}
    env=dict(os.environ);env['GIT_OPTIONAL_LOCKS']='0'
    process=subprocess.Popen(['git','--no-replace-objects','-C',str(repo),'cat-file','--batch'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,env=env)
    try:
        for path,row in sorted(selected.items()):
            name='source/'+path;mode=int(row['gitTreeMode'],8);oid=row['gitObjectId']
            L.require(mode in (0o100644,0o100755,0o120000),'Unsupported Git mode')
            parts=name.split('/')
            for i in range(1,len(parts)):
                directory='/'.join(parts[:i])+'/'
                result.setdefault(directory,L.record(directory,'directory',0))
            if oid not in cache:
                process.stdin.write(oid.encode()+b'\n');process.stdin.flush();head=process.stdout.readline().split()
                L.require(len(head)==3 and head[0].decode()==oid and head[1]==b'blob','Git batch object header differs')
                size=int(head[2]);L.require(size>=0,'Negative Git blob size')
                digest=hashlib.sha256();object_digest=hashlib.new(algorithm);object_digest.update(b'blob '+str(size).encode()+b'\0')
                remaining=size;linkraw=bytearray()if oid in linkoids else None
                while remaining:
                    data=process.stdout.read(min(L.CHUNK,remaining));L.require(data,'Truncated Git blob')
                    digest.update(data);object_digest.update(data);remaining-=len(data)
                    if linkraw is not None:L.require(len(linkraw)+len(data)<=65536,'Oversized symlink blob');linkraw.extend(data)
                L.require(process.stdout.read(1)==b'\n'and object_digest.hexdigest()==oid,'Git blob address/boundary differs')
                cache[oid]=(size,digest.hexdigest(),bytes(linkraw)if linkraw is not None else None)
            size,digest,raw=cache[oid]
            if mode==0o120000:
                item=L.record(name,'symlink',0o120777,size,digest,symlinkTarget=L.link_text_check(name,raw))
            else:item=L.record(name,'file',0 if mode==0o100644 else mode,size,digest)
            item.update(gitTreeMode=row['gitTreeMode'],gitObjectId=oid);result[name]=item
            row.update(size=size,sha256=digest)
        process.stdin.close();process.stdout.close();L.require(process.wait()==0,'Git batch failed')
    finally:
        if process.poll()is None:process.terminate();process.wait()
        process.stderr.close()
    hierarchy=L.git(repo,'ls-tree','-r','-t','-z','--full-tree',commit)
    directories={}
    for raw in hierarchy.split(b'\0'):
        if not raw:continue
        header,path=raw.split(b'\t',1);mode,kind,oid=header.split()
        if kind==b'tree':directories['source/'+path.decode('utf-8')+'/']=(mode.decode(),oid.decode())
    for name,item in result.items():
        if item['type']=='directory'and name!='source/':
            L.require(name in directories and directories[name][0]=='040000','Selected directory absent from actual Git tree')
            item.update(gitTreeMode=directories[name][0],gitObjectId=directories[name][1])
    return result,selected,excluded,{'commit':commit,'fullTreePathCount':len(allrows),'selectedTrackedPaths':len(selected),'excludedTrackedPaths':len(excluded),'excludedBatchDirectories':sorted(set(r['excludedByDirectory']for r in excluded.values())),'fullGitTreeArchive':False,'fullTreeMetadataSha256':L.sha(tree),'selectedUniqueBlobObjectsRehashed':len(cache)}


def table(rows,name):
    out={}
    for row in rows:
        path=L.safe(row['path']);L.require(path not in out,'Duplicate '+name+' path');out[path]=row
    return out


def main():
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--profile-manifest',type=Path);ap.add_argument('--full-manifest',type=Path)
    ap.add_argument('--repo',type=Path);ap.add_argument('--source-freeze',type=Path)
    ap.add_argument('--full-review',type=Path,default=D/'final-full-package-review071.json')
    ap.add_argument('--expected-source-commit',default=L.DEFAULT_COMMIT)
    ap.add_argument('--output-json',type=Path);ap.add_argument('--output-md',type=Path);ap.add_argument('--self-test',action='store_true')
    args=ap.parse_args()
    if args.self_test:
        L.require(batch_directory('Docs/Evidence/027/raw.png')=='Docs/Evidence/027','Old batch classifier')
        for path in ('Docs/Evidence/070/a','Docs/Evidence/071/a','Docs/Evidence/Benchmarks/a','Docs/Evidence/Captures/a','Docs/Evidence/README.md','Assets/Frontier/Model.cs'):
            L.require(batch_directory(path)is None,'Classifier must retain non-old-batch path')
        return L.self_test()
    L.require(all((args.profile_manifest,args.full_manifest,args.repo,args.source_freeze,args.output_json,args.output_md)),'All original manifests/repo/source-freeze and new outputs required')
    L.require(not args.output_json.exists()and not args.output_md.exists()and not args.output_json.is_symlink()and not args.output_md.is_symlink(),'Use new output paths; retain existing reviews')
    full_review,full_review_ref=L.checked_json(args.full_review)
    library_ref=L.hash_regular(Path(L.__file__));L.require(library_ref==full_review['checker'],'Previously executed full-checker original bytes must remain frozen')
    full,full_ref=L.checked_json(args.full_manifest);profile,profile_ref=L.checked_json(args.profile_manifest);freeze,freeze_ref=L.checked_json(args.source_freeze)
    L.require(profile['schema']=='frontier-source-current-workset-071-1' and profile['passed'] is True,'Completed declared current-source profile required')
    L.require(full['passed']is True and full['sourceSnapshotCommit']==profile['sourceSnapshotCommit']==args.expected_source_commit,'Explicit source snapshot differs')
    L.require(profile['notCompleteGitTreeArchive']is True,'Subset must explicitly disclaim complete Git tree')
    L.require(profile['zipComment']==args.expected_source_commit,'Profile Git comment declaration differs')
    expected,selected,excluded,source=git_subset(args.repo,args.expected_source_commit)
    declared=table(profile['selectedTrackedPaths'],'selected');omitted=table(profile['excludedTrackedPaths'],'excluded')
    L.require(set(declared)==set(selected)and set(omitted)==set(excluded),'Selected/excluded sets differ from independent approved policy')
    L.require(not(set(declared)&set(omitted)),'Selected/excluded overlap')
    for path,row in selected.items():
        for field in ('gitTreeMode','gitObjectId','size','sha256'):L.require(declared[path][field]==row[field],'Selected Git blob differs: '+path)
        L.require(declared[path]['actualGitBlobVerified']is True,'Producer did not declare actual Git blob verification')
        L.require(declared[path]['runtimeInput']==(path in freeze['files']),'Runtime flag differs: '+path)
    for path,row in excluded.items():
        for field in ('gitTreeMode','gitObjectId'):L.require(omitted[path][field]==row[field],'Excluded Git identity differs')
        L.require(omitted[path]['excludedByDirectory'].rstrip('/')==row['excludedByDirectory'],'Excluded-directory reason differs')
    runtime_files=freeze['files'];L.require(len(runtime_files)==704,'Actual frozen704-input set required')
    recomputed_fingerprint=L.sha(json.dumps({path:selected[path]['sha256'] for path in runtime_files},sort_keys=True,separators=(',',':')).encode())
    L.require(profile['sourceFreeze']['sourceFingerprint']==profile['sourceFreeze']['recomputedFingerprint']==freeze['fingerprint']==recomputed_fingerprint,'Frozen runtime fingerprint differs')
    runtime_rows=table(profile['sourceFreeze']['files'],'sourceFreeze')
    L.require(set(runtime_rows)==set(runtime_files),'Profile frozen runtime set differs')
    for path,digest in runtime_files.items():
        L.require(path in selected and selected[path]['sha256']==digest,'Selected source differs from actual frozen runtime: '+path)
        L.require(runtime_rows[path]['sha256']==digest,'Declared runtime SHA differs: '+path)
    L.require(Path(profile['zipFile']).name==profile['zipFile'],'Profile ZIP must be declared basename')
    zip_path=args.profile_manifest.parent/profile['zipFile'];L.require(zip_path.parent.resolve()==args.profile_manifest.parent.resolve(),'Profile archive outside approved Delivery directory')
    before_zip=L.hash_regular(zip_path);summary=profile['summary']
    L.require(before_zip['bytes']==summary['archiveBytes']and before_zip['sha256']==summary['sha256'],'Profile archive container differs')
    normalized_members=[dict(row,mode=row['zipRawMode'],modeOctal=format(row['zipRawMode'],'06o'))for row in profile['selectedMembers']]
    members=L.manifest_inventory(normalized_members);L.compare(expected,members,'Profile manifest vs selected Git blobs',manifest=True)
    for name,row in members.items():
        if name!='source/':L.require(row['gitObjectId']==expected[name]['gitObjectId'],'Selected ZIP member Git object differs: '+name)
    found,details=L.zip_inventory(zip_path,expected,args.expected_source_commit)
    L.compare(found,members,'Profile actual ZIP vs manifest',manifest=True)
    metadata={row['path']:row for row in profile['selectedMembers']}
    with zipfile.ZipFile(zip_path,'r')as archive:
        for info in archive.infolist():
            L.require(info.orig_filename==info.filename,'Hidden filename NUL/alias rejected')
            row=metadata[info.filename]
            actual_metadata={'zipRawMode':(info.external_attr>>16)&0xffff,'zipRawModeOctal':oct((info.external_attr>>16)&0xffff),'externalAttributes':info.external_attr,'internalAttributes':info.internal_attr,'createSystem':info.create_system,'createVersion':info.create_version,'extractVersion':info.extract_version,'dateTime':list(info.date_time),'memberCommentHex':info.comment.hex(),'extraHex':info.extra.hex()}
            L.require(actual_metadata==row['newZipMetadata'],'Profile ZIP metadata differs: '+info.filename)
            L.require(format(info.CRC,'08x')==row['crc32'],'Profile declared CRC differs: '+info.filename)
    original_source=next(row for row in full['artifacts']if row['fileName']=='source.zip')
    original_source_ref=L.hash_regular(Path(original_source['path']))
    L.require(original_source_ref['sha256']==original_source['sha256']and original_source_ref['bytes']==original_source['bytes'],'Original full-source ZIP changed')
    full_member_names={row['path']for row in original_source['members']}
    excluded_members=table(profile['excludedMembers'],'excluded ZIP member')
    L.require(set(excluded_members)==full_member_names-set(found),'Profile omitted ZIP member set differs from original archive partition')
    L.require(summary['archiveMembers']==len(found)and summary['trackedFiles']==len(selected)and summary['directoryEntries']==sum(r['type']=='directory'for r in found.values())and summary['runtimeInputsVerified']==704,'Profile summary counts differ')
    result=[{'artifact':'source-current-071.zip','zip':before_zip,'memberCount':len(found),'payloadBytes':sum(r['size']for r in found.values()),'allMembersCrcShaModeBytesVerified':True,'sourceZipGitCommentVerified':True,'members':details}]
    roots={'client.zip':Path(full['appDirectory']),'gallery.zip':Path(full['galleryDirectory'])};trees={name:L.inventory(root)for name,root in roots.items()};zip_refs={}
    for artifact in full['artifacts']:
        name=artifact['fileName']
        if name not in roots:continue
        path=Path(artifact['path']);zip_refs[name]=L.hash_regular(path)
        L.require(zip_refs[name]['sha256']==artifact['sha256']and zip_refs[name]['bytes']==artifact['bytes'],'Original app/gallery package container differs')
        declared_members=L.manifest_inventory(artifact['members']);L.compare(trees[name],declared_members,'Original app/gallery vs manifest',manifest=True)
        actual,detail=L.zip_inventory(path,trees[name]);L.compare(actual,declared_members,'Actual original app/gallery ZIP vs manifest',manifest=True)
        result.append({'artifact':name,'zip':zip_refs[name],'memberCount':len(actual),'payloadBytes':sum(r['size']for r in actual.values()),'allMembersCrcShaModeBytesVerified':True,'members':detail})
    L.require(len(result)==3,'Required current-source/client/gallery scope incomplete')
    for name,root in roots.items():L.compare(L.inventory(root),trees[name],'Original app/gallery after review')
    for path,reference in ((args.profile_manifest,profile_ref),(args.full_manifest,full_ref),(args.source_freeze,freeze_ref),(args.full_review,full_review_ref),(zip_path,before_zip),(Path(L.__file__),library_ref)):
        L.require(L.hash_regular(path)==reference,'Original metadata/archive/checker changed during review')
    for artifact in full['artifacts']:
        if artifact['fileName']in zip_refs:L.require(L.hash_regular(Path(artifact['path']))==zip_refs[artifact['fileName']],'Original ZIP changed')
    L.require(source['fullTreeMetadataSha256']==L.sha(L.git(args.repo,'ls-tree','-r','-z','--full-tree',args.expected_source_commit)),'Git tree metadata changed')
    L.require(L.hash_regular(Path(original_source['path']))==original_source_ref,'Original full-source container changed during subset review')
    report={'schema':1,'reviewedUtc':datetime.now(timezone.utc).isoformat(),'status':'passed_explicit_current_source_profile_and_original_client_gallery','source':source,'profile':profile['profile'],'notCompleteGitTreeArchive':True,'runtimeInputsVerified':704,'runtimeFingerprint':freeze['fingerprint'],'profileManifest':profile_ref,'fullPackageManifest':full_ref,'sourceFreeze':freeze_ref,'priorFullSourceReview':full_review_ref,'checker':L.hash_regular(Path(__file__)),'frozenHelper':library_ref,'artifacts':result,'selectedTrackedPaths':list(selected.values()),'excludedTrackedPaths':list(excluded.values()),'originalInputsStableBeforeAfter':True,'symlinkTargetsFollowed':False,'archivesExtracted':False,'limitations':['Explicit selected current-source subset: only old three-digit Docs/Evidence batches other than070/071 excluded. This is not a complete Git tree archive.','All704 frozen runtime files independently match selected Git blob bytes. Full-source1GB local archive was independently verified separately and retained unchanged.','Local packaging integrity only; no new Unity/build/native quality, remote Drive hash verification, ACL/xattrs, signing or notarization proof.','Links are checked as payload text and lexically within the archive root; targets are never opened or extracted.']}
    with args.output_json.open('x',encoding='utf-8')as f:json.dump(report,f,ensure_ascii=True,indent=2);f.write('\n')
    text='Independent explicit current-source/package review\n\nPassed exact selected-source/client/gallery local scope. Source commit '+args.expected_source_commit+'. Current source is a declared subset, not a complete Git tree.\n\n'+str(len(selected))+' tracked files selected; '+str(len(excluded))+' tracked files in '+str(len(source['excludedBatchDirectories']))+' old numeric Evidence batches excluded.070/071, Benchmarks, Captures and all other tracked paths retained. All704 frozen runtime inputs match actual selected Git blob bytes and source fingerprint.\n\n'
    for artifact in result:text+=artifact['artifact']+': '+str(artifact['zip']['bytes'])+' bytes, '+str(artifact['memberCount'])+' members, SHA '+artifact['zip']['sha256']+'.\n'
    text+='\nMember paths/set/type/mode/size/CRC/SHA and source ZIP Git commit comment independently verified. Original app/gallery match complete lstat inventories. The original full-source archive has a separate passing full-tree review and remains local. No extraction or symlink-target reads, Unity/GUI/Git writes, network or original mutation. Local integrity only; remote delivery and signing are separate.\n'
    with args.output_md.open('x',encoding='utf-8')as f:f.write(text)
    print(json.dumps({'status':report['status'],'sourceCommit':args.expected_source_commit,'selectedTrackedPaths':len(selected),'excludedTrackedPaths':len(excluded),'runtimeInputsVerified':704,'artifacts':[{k:r[k]for k in ('artifact','zip','memberCount')}for r in result]}))
    return 0

if __name__=='__main__':
    try:sys.exit(main())
    except Exception as error:print(json.dumps({'status':'not_passed','error':str(error),'noReleasePassGenerated':True}));sys.exit(2)
