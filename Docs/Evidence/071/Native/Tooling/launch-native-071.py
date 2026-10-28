"""Launch one owned native client, then record its actual exit without killing it."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import plistlib
import subprocess

def utc(): return datetime.now(timezone.utc).isoformat()
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app',required=True,type=Path)
    parser.add_argument('--build-identity',required=True,type=Path)
    parser.add_argument('--process-directory',required=True,type=Path)
    parser.add_argument('--profile',required=True,type=Path)
    parser.add_argument('--scenario',type=Path)
    parser.add_argument('--replay',type=Path)
    args=parser.parse_args()
    identity=json.loads(args.build_identity.read_text())
    if identity.get('appVersion')!='0.14.0' or identity.get('buildResult')!='Succeeded': parser.error('Require the verified 071 build identity.')
    info=plistlib.loads((args.app/'Contents/Info.plist').read_bytes())
    executable=args.app/'Contents/MacOS'/info['CFBundleExecutable']
    if digest(executable)!=identity['executable']['sha256']: parser.error('Executable bytes do not match the build identity.')
    if args.process_directory.exists(): parser.error('Use a new process evidence directory.')
    args.process_directory.mkdir(parents=True)
    args.profile.mkdir(parents=True,exist_ok=True)
    log=args.process_directory/'Player.log'
    command=[str(executable),'-screen-width','1600','-screen-height','1000','-frontierMaxAge','5','-frontierDataDirectory',str(args.profile),'-frontierReportDirectory',str(args.profile/'Reports'),'-frontierReplayDirectory',str(args.profile/'Replays'),'-frontierInputTrace','-logFile',str(log)]
    if args.scenario: command+=['-frontierScenarioPath',str(args.scenario)]
    if args.replay: command+=['-frontierReplay',str(args.replay)]
    receipt={'schema':1,'appVersion':identity['appVersion'],'buildGuid':identity['buildGuid'],'bundleId':info['CFBundleIdentifier'],'command':command,'profile':str(args.profile),'startedAtUtc':utc(),'status':'running','buildIdentitySha256':digest(args.build_identity),'executableSha256':digest(executable)}
    target=args.process_directory/'process.json'
    with (args.process_directory/'stdout.log').open('wb') as output:
        process=subprocess.Popen(command,stdout=output,stderr=subprocess.STDOUT)
        receipt['processId']=process.pid
        receipt['pid']=process.pid
        target.write_text(json.dumps(receipt,indent=2)+'\n')
        print(json.dumps({'processId':process.pid,'receipt':str(target),'app':str(args.app),'bundleId':info['CFBundleIdentifier']}),flush=True)
        code=process.wait()
    receipt.update(status='exited',endedAtUtc=utc(),exitCode=code)
    receipt['log']={'file':str(log),'bytes':log.stat().st_size,'sha256':digest(log)} if log.exists() else {'file':str(log),'missing':True}
    target.write_text(json.dumps(receipt,indent=2)+'\n')
    print(json.dumps({'processId':process.pid,'exitCode':code,'receipt':str(target)}),flush=True)
    return 0 if code==0 else 1

if __name__=='__main__': raise SystemExit(main())
