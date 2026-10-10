from pathlib import Path
import subprocess,json,hashlib,datetime,xml.etree.ElementTree as ET
R=Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
B=Path(__file__).resolve().parent
U='/Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents/MacOS/Unity'
D=B/'Mutations1';D.mkdir(exist_ok=False)
sha=lambda b:hashlib.sha256(b).hexdigest()
fp=lambda:subprocess.check_output(['python3',str(R/'Tools/verification_evidence.py')],text=True).strip()
receipt=dict(schema=1,purpose='Deliberate failure sensitivity, not a passing production-source run',startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),sourceFingerprintBefore=fp(),mutations=[])
def save(): (D/'mutation-receipt077.json').write_text(json.dumps(receipt,indent=2)+'\n')
save()
old=Path('/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate2/Planning/Bank077Staging/V2Final/Baseline/Assets/Frontier/Presentation/RiverLandscapePresentation.cs').read_text()
start=old.index('                float distance=layout.SignedBankDistance(p);');end=old.index('\n            }',start)
historical=old[start:end]
specs=[('reversed-side','Assets/Frontier/Simulation/RiverBankProfile.cs','EditMode','Frontier.Tests.RiverBankAsymmetry077Tests.LiteralEasternMeanderHasItsInnerBankWestAndOuterBankEastInTheStoredQuery',lambda s:s.replace('Vector2.Lerp(a.tangent,b.tangent,fraction),point-center);','Vector2.Lerp(a.tangent,b.tangent,fraction),center-point);')),
('disconnected-mesh-colors','Assets/Frontier/Presentation/RiverLandscapePresentation.cs','PlayMode','Frontier.Tests.RiverBankMesh077Tests.ActualGroundMeshColorsAndHeightsEqualTheSharedMapQueries',lambda s:s.replace('                layers[i]=layout.BankLayers(p);',historical))]
for name,path,mode,filter,change in specs:
 active=subprocess.check_output(['/bin/ps','-axo','pid=,comm='],text=True)
 assert not any(x.strip().endswith('/Unity.app/Contents/MacOS/Unity') for x in active.splitlines())
 d=D/name;d.mkdir();p=R/path;original=p.read_bytes();(d/'original.txt').write_bytes(original)
 altered=change(original.decode()).encode();assert altered!=original
 step=dict(name=name,path=path,mode=mode,filter=filter,originalSha256=sha(original),mutatedSha256=sha(altered));receipt['mutations'].append(step)
 try:
  p.write_bytes(altered);step['mutatedFingerprint']=fp();save()
  cmd=[U,'-batchmode','-projectPath',str(R)]+(['-nographics'] if mode=='EditMode' else [])+['-runTests','-testPlatform',mode,'-testFilter',filter,'-testResults',str(d/(mode+'.xml')),'-logFile',str(d/(mode+'.log'))]
  step['command']=cmd
  with (d/'stdout.log').open('wb') as f:
   proc=subprocess.Popen(cmd,cwd=R,stdout=f,stderr=subprocess.STDOUT);step['pid']=proc.pid;save();step['exitCode']=proc.wait()
  xml=d/(mode+'.xml');root=ET.parse(xml).getroot();step['result']=root.attrib;step['xmlSha256']=sha(xml.read_bytes());step['failureMessages']=[t.findtext('failure/message') for t in root.iter('test-case') if t.get('result')=='Failed']
  step['expectedFailureObserved']=step['exitCode']==2 and int(root.get('total','0'))==1 and int(root.get('failed','0'))==1
 finally:
  p.write_bytes(original);step['restoredSha256']=sha(p.read_bytes());step['sourceFingerprintAfterRestore']=fp();save()
 assert step['expectedFailureObserved'],step
 assert step['sourceFingerprintAfterRestore']==receipt['sourceFingerprintBefore']
 print(json.dumps(step),flush=True)
receipt.update(finishedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),sourceFingerprintAfter=fp(),status='closed',sensitivityConfirmed=all(s['expectedFailureObserved'] for s in receipt['mutations']))
save()
