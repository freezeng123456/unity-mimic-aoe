#!/usr/bin/env python3
"""Validate source packaging and actual Unity NUnit results; never claims gameplay acceptance."""
import argparse,json,re,sys
from pathlib import Path
from datetime import datetime,timezone
from xml.etree import ElementTree as ET
from verification_evidence import MANIFEST,verify

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--reports-dir',type=Path);parser.add_argument('--require-current-source',action='store_true');args=parser.parse_args()
    root=Path(__file__).resolve().parents[1];reports=args.reports_dir or root/'Builds/Tests';failed=[];missing=[]
    def result(name,state,detail=''):
        print(f'{state:8} {name}'+(f' — {detail}' if detail else ''))
        if state=='FAIL':failed.append(name)
        if state=='NOT RUN':missing.append(name)
    if args.reports_dir is None and (reports/'latest-run.txt').exists():
        run=(reports/'latest-run.txt').read_text().strip()
        if re.fullmatch(r'run-[A-Za-z0-9]+',run):reports=reports/run
        else:result('Latest test run','FAIL','Invalid latest-run.txt')
    version=root/'ProjectSettings/ProjectVersion.txt'
    version_text=version.read_text() if version.exists() else ''
    editor=re.search(r'^m_EditorVersion: (\d+\.\d+\.\d+[abfp]\d+)$',version_text,re.M)
    consistent=editor is not None and re.search(r'^m_EditorVersionWithRevision: '+re.escape(editor.group(1))+r' \([0-9a-f]+\)$',version_text,re.M) is not None
    result('Editor version','PASS' if consistent else 'FAIL',version_text.strip())
    try:
        manifest=json.loads((root/'Packages/manifest.json').read_text())['dependencies'];lock=json.loads((root/'Packages/packages-lock.json').read_text())['dependencies']
        conflicts=[f'{k}: requested {v}, resolved {lock.get(k,{}).get("version")}' for k,v in manifest.items() if lock.get(k,{}).get('version')!=v]
        result('Locked direct dependencies','PASS' if not conflicts else 'FAIL','; '.join(conflicts))
    except (OSError,ValueError,KeyError) as exc:result('Packages','FAIL',str(exc))
    assets=root/'Assets';missing_meta=[str(p.relative_to(root)) for p in assets.rglob('*') if not p.name.startswith('.') and p.suffix!='.meta' and not p.with_name(p.name+'.meta').exists()]
    result('Assets metadata','PASS' if not missing_meta else 'FAIL',', '.join(missing_meta))
    invalid_order=[str(p.relative_to(root)) for p in assets.rglob('*.cs.meta') if re.search(r'^\s+executionOrder: (?!-?\d+\s*$).+$',p.read_text(errors='replace'),re.M)]
    result('Script importer execution order','PASS' if not invalid_order else 'FAIL',', '.join(invalid_order))
    guids=set()
    for meta in assets.rglob('*.meta'):
        guids.update(re.findall(r'^guid: ([0-9a-f]{32})$',meta.read_text(errors='replace'),re.M))
    scene=assets/'Frontier/Scenes/FrontierSkirmish.unity';settings=root/'ProjectSettings/EditorBuildSettings.asset'
    enabled=scene.exists() and settings.exists() and 'path: Assets/Frontier/Scenes/FrontierSkirmish.unity' in settings.read_text() and 'enabled: 1' in settings.read_text()
    result('Enabled native scene','PASS' if enabled else 'FAIL')
    # Check user script links only: Unity/URP built-in asset GUIDs may live in package caches.
    scriptrefs=re.findall(r'm_Script: \{fileID: 11500000, guid: ([0-9a-f]{32})',scene.read_text()) if scene.exists() else []
    controller_meta=assets/'Frontier/Presentation/GameController.cs.meta'
    controller_guid=re.search(r'guid: ([0-9a-f]{32})',controller_meta.read_text()).group(1) if controller_meta.exists() else ''
    result('Scene GameController reference','PASS' if controller_guid and controller_guid in scriptrefs and controller_guid in guids else 'FAIL')
    private=[]
    for base in [assets,root/'ProjectSettings',root/'Tools']:
        for p in base.rglob('*'):
            if p.is_file() and p.suffix in {'.cs','.asset','.json','.unity','.sh','.py'}:
                if re.search(r'/Users/[^/\s]+/(Documents|Desktop|Library|Downloads|Projects?)/',p.read_text(errors='replace')):private.append(str(p.relative_to(root)))
    result('Private absolute runtime dependencies','PASS' if not private else 'FAIL',', '.join(private))
    if (reports/MANIFEST).exists():
        try:result('Tests match current source','PASS',verify(root,reports))
        except (OSError,ValueError,KeyError,TypeError,ET.ParseError) as exc:result('Tests match current source','FAIL',str(exc))
    elif args.require_current_source:result('Tests match current source','NOT RUN','No source-evidence.json; run Tools/test.sh')
    else:print('UNBOUND  Test reports without source evidence are historical, not current-source verification.')
    for mode in ['EditMode','PlayMode']:
        files=[p for p in reports.glob('*.xml') if mode.lower() in p.name.lower()]
        if not files:result(mode,'NOT RUN',str(reports));continue
        candidates=[]
        for path in files:
            try:
                node=ET.parse(path).getroot()
                stamp=datetime.fromisoformat(node.attrib['end-time'].replace('Z','+00:00'))
                if stamp.tzinfo is None:stamp=stamp.replace(tzinfo=timezone.utc)
                candidates.append((stamp,path.name,path,node))
            except (ET.ParseError,OSError,ValueError,KeyError) as exc:result(mode,'FAIL',f'{path.name}: invalid report: {exc}')
        if not candidates:continue
        _,_,path,node=max(candidates,key=lambda item:(item[0],item[1]))
        try:
            total=int(node.attrib.get('total','0'));passed=int(node.attrib.get('passed','0'));bad=int(node.attrib.get('failed','0'))
            valid=node.tag=='test-run' and node.attrib.get('result')=='Passed' and total>0 and passed==total and bad==0
            result(mode,'PASS' if valid else 'FAIL',f'{passed}/{total}; failed={bad}; UTC {node.attrib.get("end-time","unknown")}; {path.name}')
        except (ET.ParseError,OSError,ValueError) as exc:result(mode,'FAIL',str(exc))
    print('Native input, sound, full-match experience and performance require separate evidence.')
    return 1 if failed else 2 if missing else 0
if __name__=='__main__':sys.exit(main())
