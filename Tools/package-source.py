#!/usr/bin/env python3
"""Package portable source with an allowlist. Never move or delete Unity caches."""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile

parser=argparse.ArgumentParser()
parser.add_argument('--output',required=True,type=Path)
parser.add_argument('--evidence-version',help='Include only this numeric Docs/Evidence release; omit to include all history')
args=parser.parse_args()
if args.evidence_version and (not args.evidence_version.isdigit() or len(args.evidence_version)!=3):
    parser.error('--evidence-version must be a three-digit release identifier')
root=Path(__file__).resolve().parents[1]
allowed=('Assets','Packages','ProjectSettings','Docs','SourceAssets','Tools','README.md','.gitignore')
files=[]
for name in allowed:
    entry=root/name
    if entry.is_file(): files.append(entry)
    elif entry.is_dir(): files.extend(p for p in entry.rglob('*') if p.is_file() and not p.name.startswith('.') and '__pycache__' not in p.parts)
if args.evidence_version:
    evidence_root=root/'Docs'/'Evidence'
    files=[p for p in files if not p.is_relative_to(evidence_root) or p.relative_to(evidence_root).parts[0]==args.evidence_version]
files=sorted(files)
args.output.parent.mkdir(parents=True,exist_ok=True)
with zipfile.ZipFile(args.output,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
    for path in files: archive.write(path,Path('FrontierReckoning')/path.relative_to(root))
with zipfile.ZipFile(args.output) as archive:
    failure=archive.testzip()
    if failure: raise RuntimeError('Corrupt archive member: '+failure)
manifest={str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
receipt={'archive':args.output.name,'sha256':hashlib.sha256(args.output.read_bytes()).hexdigest(),'files':len(files),'bytes':args.output.stat().st_size,'excludes':['Library','Logs','Temp','obj','Builds','UserSettings','work'],'sourceHashes':manifest}
if args.evidence_version:
    receipt['includedEvidenceVersion']=args.evidence_version
    receipt['excludes'].append('Docs/Evidence releases other than '+args.evidence_version)
args.output.with_suffix('.manifest.json').write_text(json.dumps(receipt,indent=2)+'\n')
print(json.dumps({k:v for k,v in receipt.items() if k!='sourceHashes'},indent=2))
