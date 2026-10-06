"""Run bounded Unity checks without changing the project; preserve each receipt."""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
from datetime import datetime, timezone

PROJECT=Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
EDITOR=Path('/Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents/MacOS/Unity')

def now(): return datetime.now(timezone.utc).isoformat()
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--operation',choices=('focused','routes','build'),required=True)
    p.add_argument('--directory',type=Path,required=True)
    p.add_argument('--fingerprint',required=True)
    p.add_argument('--edit-filter',default='Frontier.Tests.AiResourceContinuity072Tests;Frontier.Tests.AiArmyPlan072Tests')
    p.add_argument('--play-filter',default='Frontier.Tests.ArmyControl072Tests')
    p.add_argument('--only-play',action='store_true')
    p.add_argument('--passed-reports',type=Path)
    a=p.parse_args()
    a.directory=a.directory.resolve()
    if a.passed_reports: a.passed_reports=a.passed_reports.resolve()
    if a.directory.exists(): p.error('Use a new directory; existing evidence is preserved.')
    spec=importlib.util.spec_from_file_location('frontier_evidence',PROJECT/'Tools/verification_evidence.py')
    evidence=importlib.util.module_from_spec(spec);spec.loader.exec_module(evidence)
    before=evidence.source_files(PROJECT);fp=evidence.fingerprint(before)
    if fp!=a.fingerprint: p.error('Source does not match the declared fingerprint.')
    if a.operation in ('routes','build'):
        if not a.passed_reports: p.error('Routes/build require the complete passing reports for these exact inputs.')
        if evidence.verify(PROJECT,a.passed_reports)!=fp: p.error('Complete test evidence belongs to different inputs.')
    a.directory.mkdir(parents=True)
    (a.directory/'source-before.json').write_text(json.dumps({'fingerprint':fp,'files':before,'capturedAtUtc':now()},indent=2)+'\n')
    receipt={'schema':1,'operation':a.operation,'startedAtUtc':now(),'sourceBefore':fp,'sourceInputs':len(before),'runs':[]}
    if a.passed_reports: receipt['testRun']=str(a.passed_reports)
    target=a.directory/'runner-receipt.json'
    def save(): target.write_text(json.dumps(receipt,indent=2)+'\n')
    save()
    common=[str(EDITOR),'-batchmode','-projectPath',str(PROJECT)]
    commands=[]
    env=os.environ.copy()
    if a.operation=='focused':
        for mode,flt in [('EditMode',a.edit_filter),('PlayMode',a.play_filter)]:
            if a.only_play and mode=='EditMode': continue
            commands.append((mode,common+(['-nographics'] if mode=='EditMode' else [])+['-automated','-projectRepositoryPath',str(PROJECT),'-runTests','-testPlatform',mode,'-testFilter',flt,'-testResults',str(a.directory/(mode+'.xml')),'-logFile',str(a.directory/(mode+'.log'))]))
    elif a.operation=='routes':
        env['FRONTIER_REPORT_PATH']=str(a.directory/'matrix')
        env['FRONTIER_SOURCE_FINGERPRINT']=fp
        commands.append(('routes',common+['-nographics','-quit','-executeMethod','Frontier.Editor.NormalRoutes072.Run','-logFile',str(a.directory/'routes.log')]))
    else:
        env['FRONTIER_BUILD_PATH']=str(a.directory/'Frontier Reckoning.app')
        commands.append(('build',common+['-quit','-executeMethod','Frontier.Editor.BuildTools.BuildMac','-logFile',str(a.directory/'build.log')]))
    for name,command in commands:
        entry={'name':name,'startedAtUtc':now(),'command':command};receipt['runs'].append(entry);save()
        with (a.directory/(name+'-stdout.log')).open('wb') as output:
            proc=subprocess.run(command,cwd=PROJECT,env=env,stdout=output,stderr=subprocess.STDOUT)
        entry.update(exitCode=proc.returncode,finishedAtUtc=now())
        current=evidence.fingerprint(evidence.source_files(PROJECT))
        entry['sourceAfter']=current;save()
        print(json.dumps({'name':name,'exitCode':proc.returncode,'sourceStable':current==fp}),flush=True)
        if proc.returncode or current!=fp: break
    after=evidence.source_files(PROJECT)
    receipt.update(finishedAtUtc=now(),sourceAfter=evidence.fingerprint(after),sourceStable=before==after)
    (a.directory/'source-after.json').write_text(json.dumps({'fingerprint':evidence.fingerprint(after),'files':after,'sourceStable':before==after,'unityExitCode':receipt['runs'][-1]['exitCode'],'capturedAtUtc':now()},indent=2)+'\n')
    receipt['files']={x.relative_to(a.directory).as_posix():{'bytes':x.stat().st_size,'sha256':sha(x)} for x in a.directory.iterdir() if x.is_file() and x!=target}
    receipt['allProcessesSucceeded']=len(receipt['runs'])==len(commands) and all(x['exitCode']==0 for x in receipt['runs'])
    save()
    return 0 if receipt['sourceStable'] and receipt['allProcessesSucceeded'] else 1

if __name__=='__main__': raise SystemExit(main())
