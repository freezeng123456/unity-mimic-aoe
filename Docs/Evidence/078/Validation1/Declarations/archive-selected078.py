"""Copy exact selected 078 originals; preserve both native sessions and failed probe identity."""
import hashlib,json,shutil
from pathlib import Path
BASE=Path(__file__).resolve().parent
REPO=Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
DST=REPO/'Docs/Evidence/078/Validation1'
assert not DST.exists()
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def load(p):return json.loads(p.read_text())
def closed(n):
 d=load(BASE/n/'runner-receipt078.json');assert d['status']=='closed' and d['exitCode']==0 and d['sourceStable'];return d
full=closed('Full1');build=closed('Build1')
assert full['sourceFingerprintAfter']==build['sourceFingerprintAfter']=='05dc944960c2724e08f2d6eeb02df282710d099ae36cf35bb7923d8918166217'
audit=load(BASE/'Native2/winding-view/native-audit078.json')
assert audit['status']=='accepted_bounded_camera_capture_menu_exit_quit' and audit['originalsUnchangedDuringAudit']
assert audit['lifecycle']['allOwnedEditorAndNativeAbsent'] and audit['identity']['sourceFingerprint']==full['sourceFingerprintAfter']
assert load(BASE/'Native1/winding-view/native-partial-audit078.json')['status']!='accepted_bounded_camera_capture_menu_exit_quit'
selections={}
for stage in ['ProbeCoarse1','ProbeCoarse2','ProbeFine1','Shore1','Render1','Full1','Build1','Native1','Native2']:
 root=BASE/stage
 for p in sorted(root.rglob('*')):
  if not p.is_file() or any(x.endswith('.app') or x.startswith('Frontier Reckoning_BackUp') or x=='__pycache__' for x in p.relative_to(root).parts):continue
  if p.suffix.lower()=='.png' and stage not in ['Native1','Native2']:
   if not(stage=='Render1' and p.name in [f'{v}-{s}.png' for v in ('01-east-bend-default26','02-east-bend-close16') for s in ('current-fine-half-metre','control-reconstructed-coarse-one-metre')]):continue
  selections['Candidate1/'+p.relative_to(BASE).as_posix()]=p
for p in sorted(Path(full['reportsDirectory']).iterdir()):
 if p.is_file():selections['ActualFull1Reports/'+p.name]=p
for n in ['run078.py','create-build-manifest078.py','launch-native078.py','launch-native078-second.py','prepare-packages078.py','prepare-packages078-native2.py','archive-selected078.py','github078.py']:
 p=BASE/n;assert p.is_file();selections['Declarations/'+n]=p
for n in ['full-source-independent-audit078.json','unchanged-authority-baseline078.json','coarse-probe-independent-audit078.json','fine-probe-independent-audit078.json','native078-second-session-static-review.json']:
 p=BASE/'Planning'/n;assert p.is_file();selections['Planning/'+n]=p
DST.mkdir(parents=True)
rows=[]
for rel,src in sorted(selections.items()):
 d=DST/rel;d.parent.mkdir(parents=True,exist_ok=True);h=sha(src);size=src.stat().st_size
 shutil.copy2(src,d);assert (sha(d),d.stat().st_size)==(h,size);rows.append(dict(path=rel,original=str(src),bytes=size,sha256=h))
index=dict(schema=1,scope='Exact selected078 originals. Original compile failure and Native1 partial remain distinct; accepted Native2 is bounded camera/capture/menu_exit/Quit only. Unselected URP PNGs stay in Candidate1 and complete evidence package. No historical version relabel.',sourceFingerprint=full['sourceFingerprintAfter'],files=rows,fileCount=len(rows),totalBytes=sum(r['bytes'] for r in rows))
(DST/'copy-index078.json').write_text(json.dumps(index,indent=2)+'\n')
print(json.dumps({k:index[k] for k in ('fileCount','totalBytes','sourceFingerprint')}))
