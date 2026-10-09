import argparse
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import time
from datetime import datetime, timezone
from xml.etree import ElementTree

ROOT = Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
BASE = Path(__file__).resolve().parent
UNITY = '/Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents/MacOS/Unity'
spec = importlib.util.spec_from_file_location('evidence076', ROOT/'Tools/verification_evidence.py')
ev = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ev)

def now():
    return datetime.now(timezone.utc).isoformat()

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('kind', choices=['river', 'focused', 'full', 'build', 'normal'])
    parser.add_argument('name')
    args = parser.parse_args()
    active = subprocess.check_output(['/bin/ps','-axo','pid=,comm='], text=True)
    if any(line.strip().endswith('/Unity.app/Contents/MacOS/Unity') for line in active.splitlines()):
        raise SystemExit('Another Unity Editor is active; preserve its process and wait')
    output = BASE/args.name
    output.mkdir(exist_ok=False)
    before = ev.source_files(ROOT)
    fp = ev.fingerprint(before)
    receipt = dict(schema=1, kind=args.kind, status='running', startedUtc=now(),
        sourceFingerprintBefore=fp, sourcesBefore=before,
        gitHeadBefore=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(), commands=[])
    def save():
        (output/'runner-receipt076.json').write_text(json.dumps(receipt,indent=2)+'\n')
    save()
    base = [UNITY,'-batchmode','-projectPath',str(ROOT)]
    environment = os.environ.copy()
    commands = []
    if args.kind == 'river':
        commands.append(base+['-nographics','-runTests','-testPlatform','EditMode',
            '-testFilter','Frontier.Tests.Healing076RiverLifecycleTests',
            '-testResults',str(output/'EditMode.xml'),'-logFile',str(output/'EditMode.log')])
    elif args.kind == 'focused':
        for mode, filters in [('EditMode','Frontier.Tests.Healing076RiverLifecycleTests;Frontier.Tests.Healing076CoreTests;Frontier.Tests.Healing076AiTests;Frontier.Tests.Healing076ReplicationTests;Frontier.Tests.ClassicCivilizationAiTests;Frontier.Tests.ScoutingProduction075Tests;Frontier.Tests.NavalTransport065Tests;Frontier.Tests.CurvedManeuver063Tests;Frontier.Tests.WaterNavigation061Tests;Frontier.Tests.MapLayout063Tests;Frontier.Tests.NavalDeliveryTests;Frontier.Tests.GarrisonExitTests;Frontier.Tests.NavigationSweep063Tests'),
            ('PlayMode','Frontier.Tests.Healing076InputPlayModeTests;Frontier.Tests.Healing076PresentationPlayModeTests;Frontier.Tests.ScoutingPresentation075Tests;Frontier.Tests.ScoutingInput075Tests')]:
            commands.append(base+(['-nographics'] if mode=='EditMode' else [])+['-runTests','-testPlatform',mode,'-testFilter',filters,
                '-testResults',str(output/(mode+'.xml')),'-logFile',str(output/(mode+'.log'))])
    elif args.kind == 'full':
        commands.append(['bash',str(ROOT/'Tools/test.sh')])
    elif args.kind == 'build':
        environment['FRONTIER_BUILD_PATH'] = str(output/'Frontier Reckoning.app')
        commands.append(base+['-nographics','-quit','-executeMethod','Frontier.Editor.BuildTools.BuildMac','-logFile',str(output/'build.log')])
    else:
        environment['FRONTIER_REPORT_PATH'] = str(output/'Observed')
        environment['FRONTIER_SOURCE_FINGERPRINT'] = fp
        commands.append(base+['-nographics','-quit','-executeMethod','Frontier.Editor.HealingObservation076.RunNormal','-logFile',str(output/'normal.log')])
    prior_reports = set((ROOT/"Builds/Tests").glob("run-*"))
    started = time.monotonic()
    for i, command in enumerate(commands):
        step = dict(command=command, startedUtc=now(), status='running')
        receipt['commands'].append(step)
        save()
        with (output/('stdout-'+str(i)+'.log')).open('wb') as stream:
            process = subprocess.Popen(command,cwd=ROOT,env=environment,stdout=stream,stderr=subprocess.STDOUT)
            step['pid'] = process.pid
            save()
            code = process.wait()
        step.update(exitCode=code, finishedUtc=now(), status='closed')
        save()
        print(json.dumps(dict(kind=args.kind, commandIndex=i, exitCode=code, output=str(output))), flush=True)
        if code:
            break
    after = ev.source_files(ROOT)
    receipt.update(finishedUtc=now(), wallSeconds=time.monotonic()-started, status='closed', exitCode=code,
        sourceFingerprintAfter=ev.fingerprint(after), sourceStable=(before==after),
        changedSources=sorted(k for k in before.keys()|after.keys() if before.get(k)!=after.get(k)), tests={})
    reports = output
    if args.kind=='full':
        fresh_reports = sorted(set((ROOT/'Builds/Tests').glob('run-*')) - prior_reports)
        receipt['freshReportsDirectories'] = [str(p) for p in fresh_reports]
        if len(fresh_reports)==1:
            reports = fresh_reports[0]
            receipt['reportsDirectory'] = str(reports)
    for mode in ['EditMode','PlayMode']:
        f = reports/(mode+'.xml')
        if f.exists():
            receipt['tests'][mode] = ElementTree.parse(f).getroot().attrib
            receipt.setdefault('testsXmlSha256', {})[mode] = ev.file_hash(f)
    if args.kind in ['focused','full'] and code==0 and receipt['sourceStable']:
        ev.record(ROOT,reports,fp)
    save()
    print(json.dumps({k:receipt[k] for k in ['status','exitCode','sourceStable','sourceFingerprintAfter','tests']}),flush=True)
    raise SystemExit(code if code else 0 if receipt['sourceStable'] else 3)

if __name__=='__main__':
    main()
