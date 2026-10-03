#!/usr/bin/env python3
"""Prove the exact tested-source delta after the civilization-summary layout fix.

This does not assert that the old matrix ran on the new global fingerprint.
All source-inventory entries are compared; only two exact paths may differ.
"""
import argparse
import difflib
import hashlib
import json
from pathlib import Path
import subprocess
from xml.etree import ElementTree as ET

SOURCE_DIRS=('Assets','Packages','ProjectSettings','Tools')
ALLOWED_CHANGED=(
    'Assets/Frontier/Presentation/FrontierHud.cs',
    'Assets/Frontier/Tests/PlayMode/ClassicCivilizationUiTests.cs',
)
ADDED_TEST='Frontier.Tests.ClassicCivilizationUiTests.EveryCivilizationSummaryFitsFullyAboveDeckButtons'


def sha(data):return hashlib.sha256(data).hexdigest()
def file_hash(path):return sha(path.read_bytes())
def fingerprint(files):return sha(json.dumps(files,sort_keys=True,separators=(',',':')).encode())


def inventory(root):
    # Identical declared source scope as Tools/verification_evidence.py. This is
    # not a change-exemption filter: every file in the tested scope is compared.
    return {p.relative_to(root).as_posix():file_hash(p)
        for directory in SOURCE_DIRS for p in sorted((root/directory).rglob('*'))
        if p.is_file() and not any(part.startswith('.') or part=='__pycache__' for part in p.relative_to(root).parts)}


def tests(directory):
    receipt_path=directory/'source-evidence.json';receipt=json.loads(receipt_path.read_text())
    if receipt.get('schema')!=1 or fingerprint(receipt['files'])!=receipt['sourceFingerprint']:
        raise ValueError('Invalid test source receipt: '+str(directory))
    cases={};reports={}
    for mode in ('EditMode','PlayMode'):
        expected=receipt['reports'][mode];path=directory/(mode+'.xml')
        if expected['file']!=path.name or file_hash(path)!=expected['sha256']:raise ValueError('Test XML hash mismatch: '+str(path))
        root=ET.parse(path).getroot();total=int(root.get('total','0'))
        if root.tag!='test-run' or root.get('result')!='Passed' or int(root.get('failed','0'))!=0 or int(root.get('passed','0'))!=total or total<=0 or total!=expected['passed']:
            raise ValueError('Test XML is not a complete passing run: '+str(path))
        names=[node.get('fullname') for node in root.iter('test-case')]
        if len(names)!=total or len(set(names))!=len(names) or not all(names):raise ValueError('Test-case identity list is incomplete: '+str(path))
        if any(node.get('result')!='Passed' for node in root.iter('test-case')):raise ValueError('Nonpassing test case: '+str(path))
        cases[mode]=set(names);reports[mode]={'path':str(path),'sha256':expected['sha256'],'passed':total}
    return receipt,cases,{'path':str(receipt_path),'sha256':file_hash(receipt_path),'sourceFingerprint':receipt['sourceFingerprint'],'fileCount':len(receipt['files']),'reports':reports}


def matrix_binding(path,initial,initial_receipt_sha):
    matrix=json.loads(path.read_text())
    before={e['path']:e['sha256'] for e in matrix['sourcesBefore']};after={e['path']:e['sha256'] for e in matrix['sourcesAfter']}
    if len(before)!=len(matrix['sourcesBefore']) or len(after)!=len(matrix['sourcesAfter']):raise ValueError('Duplicate matrix source path')
    if not matrix['sourcesStable'] or not matrix.get('finishedUtc') or before!=initial['files'] or after!=initial['files']:
        raise ValueError('Matrix source inventories do not equal the initial tested source: '+str(path))
    if matrix['sourceFingerprintBefore']!=initial['sourceFingerprint'] or matrix['sourceFingerprintAfter']!=initial['sourceFingerprint']:
        raise ValueError('Matrix fingerprint does not equal the initial tested source')
    if matrix['testReceipt']['sha256']!=initial_receipt_sha:raise ValueError('Matrix does not bind the initial passing test receipt')
    statuses={}
    for case in matrix['cases']:statuses[case['status']]=statuses.get(case['status'],0)+1
    return {'path':str(path),'sha256':file_hash(path),'sourceFingerprint':matrix['sourceFingerprintBefore'],'status':matrix['status'],'caseStatuses':statuses,
        'naturalEnds':sum(c['observations']['naturalEnds'] for c in matrix['cases']),'cases':len(matrix['cases']),
        'unfinishedCaseIds':[c['id'] for c in matrix['cases'] if c['status']!='natural_end'],
        'claim':'Executed on initialSourceFingerprint only; not rerun on finalSourceFingerprint.'}


def git_blob(root,commit,path):
    return subprocess.run(['git','show',commit+':'+path],cwd=root,check=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE).stdout


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo',type=Path,default=Path('.'))
    parser.add_argument('--initial-tests',type=Path,default=Path('Docs/Evidence/058/Tests-Initial'))
    parser.add_argument('--final-tests',type=Path,default=Path('Docs/Evidence/058/Tests'))
    parser.add_argument('--initial-commit',default='79d88e290cad42ecd88082cd5e55e8b613f56ae2')
    parser.add_argument('--final-commit',required=True,help='Exact final source commit whose blobs match the final passing test inventory')
    parser.add_argument('--matrix',type=Path,action='append',default=[])
    parser.add_argument('--output',type=Path,default=Path('Docs/Evidence/058/menu-equivalence.json'))
    args=parser.parse_args();repo=args.repo.resolve()
    if args.output.exists():raise FileExistsError('Preserve existing proof: '+str(args.output))
    initial,old_cases,old_receipt=tests(args.initial_tests)
    final,new_cases,new_receipt=tests(args.final_tests)
    initial_files=initial['files'];final_files=final['files']
    added=sorted(set(final_files)-set(initial_files));removed=sorted(set(initial_files)-set(final_files))
    changed=sorted(p for p in set(initial_files)&set(final_files) if initial_files[p]!=final_files[p])
    if added or removed or changed!=sorted(ALLOWED_CHANGED):
        raise ValueError('Unexpected exact source delta: '+json.dumps({'added':added,'removed':removed,'changed':changed}))
    if inventory(repo)!=final_files:raise ValueError('Current complete source inventory differs from final passing test inventory')
    test_delta={}
    for mode in ('EditMode','PlayMode'):
        added_tests=sorted(new_cases[mode]-old_cases[mode]);removed_tests=sorted(old_cases[mode]-new_cases[mode])
        expected=[ADDED_TEST] if mode=='PlayMode' else []
        if removed_tests or added_tests!=expected:raise ValueError('Unexpected test identities changed in '+mode)
        test_delta[mode]={'added':added_tests,'removed':removed_tests,'retained':len(old_cases[mode]&new_cases[mode])}
    changes=[]
    for path in changed:
        old=git_blob(repo,args.initial_commit,path);new=git_blob(repo,args.final_commit,path)
        if sha(old)!=initial_files[path] or sha(new)!=final_files[path] or new!=(repo/path).read_bytes():
            raise ValueError('Commit blobs do not prove the recorded tested-source delta: '+path)
        changes.append({'path':path,'initialSha256':sha(old),'finalSha256':sha(new),
            'unifiedDiff':''.join(difflib.unified_diff(old.decode().splitlines(keepends=True),new.decode().splitlines(keepends=True),fromfile=args.initial_commit+'/'+path,tofile=args.final_commit+'/'+path))})
    unchanged=[{'path':p,'sha256':final_files[p]} for p in sorted(final_files) if p not in ALLOWED_CHANGED]
    simulation=[row for row in unchanged if row['path'].startswith('Assets/Frontier/Simulation/')]
    if not simulation:raise ValueError('Simulation inventory unexpectedly empty')
    rules=[row for row in unchanged if row['path']=='Assets/Resources/Rules.json']
    if len(rules)!=1:raise ValueError('Rules.json not proven identical')
    matrices=args.matrix or [Path('Docs/Evidence/058/Matrix/matrix.json')]
    bindings=[matrix_binding(p,initial,old_receipt['sha256']) for p in matrices]
    proof={'schemaVersion':1,'result':'verified_exact_menu_only_delta','initialCommit':args.initial_commit,'finalCommit':args.final_commit,
        'initialSourceFingerprint':initial['sourceFingerprint'],'finalSourceFingerprint':final['sourceFingerprint'],
        'initialTestReceipt':old_receipt,'finalTestReceipt':new_receipt,
        'comparison':{'initialFileCount':len(initial_files),'finalFileCount':len(final_files),'changedFileCount':len(changed),'unchangedFileCount':len(unchanged),'addedPaths':added,'removedPaths':removed,'exactAllowedChangedPaths':list(ALLOWED_CHANGED),'changedFiles':changes,'unchangedFiles':unchanged},
        'testCaseChanges':test_delta,'rulesIdentical':rules,'simulationIdentical':simulation,'matrixBindings':bindings,
        'interpretation':{'verified':'The complete tested source inventory differs at exactly the two listed paths. Every other source entry, including every simulation/AI file and Rules.json, is byte-identical. The recorded commit blobs reproduce the full two-file diff. All existing tests remain, and the only added test is the civilization-summary geometry regression.',
            'uiDiffReview':'The complete recorded unifiedDiff is the reviewable evidence for the two allowed files. Runtime changes concern civilization-summary menu layout; the new regression checks full rendered descriptions and clearance above deck buttons. Detailed values and geometry are preserved in the diff rather than inferred from filenames.',
            'limits':'This proves unchanged simulation/AI/rules inputs, not a new full-matrix execution on the final global fingerprint. The original matrix retains its initial fingerprint and 35/36 outcome. Final tests certify the final source; native screenshots and rebuilt-client receipt are separate evidence.'}}
    if inventory(repo)!=final_files:raise ValueError('Source changed while equivalence proof was generated')
    args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({'result':proof['result'],'initialFingerprint':proof['initialSourceFingerprint'],'finalFingerprint':proof['finalSourceFingerprint'],
        'initialFiles':len(initial_files),'finalFiles':len(final_files),'changed':changed,'unchanged':len(unchanged),'simulationIdenticalFiles':len(simulation),
        'testCaseChanges':test_delta,'matrixBindings':bindings,'output':str(args.output)},ensure_ascii=False,indent=2))


if __name__=='__main__':main()
