"""Independently read fresh NUnit XML and bind it to unchanged source inputs."""
import argparse,hashlib,json,xml.etree.ElementTree as E
from pathlib import Path
from datetime import datetime,timezone

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def fp(files):return hashlib.sha256(json.dumps(files,sort_keys=True,separators=(",",":")).encode()).hexdigest()
def read(directory):
    rows={}
    for mode in ("EditMode","PlayMode"):
        path=directory/(mode+".xml");root=E.parse(path).getroot()
        cases=list(root.iter("test-case")); names=[c.get("fullname") for c in cases]
        assert root.tag=="test-run" and root.get("result")=="Passed"
        assert len(cases)==int(root.get("total"))==int(root.get("passed"))>0
        assert all(c.get("result")=="Passed" for c in cases)
        assert all(int(root.get(k,"0"))==0 for k in ("failed","skipped","inconclusive"))
        assert len(set(names))==len(names)
        rows[mode]={"count":len(cases),"names":names,"xmlSha256":sha(path),"duration":root.get("duration"),"start":root.get("start-time"),"end":root.get("end-time")}
    return rows
p=argparse.ArgumentParser();p.add_argument("--reports",type=Path,required=True);p.add_argument("--baseline",type=Path,required=True);p.add_argument("--project",type=Path,required=True);p.add_argument("--output",type=Path,required=True);a=p.parse_args()
old=read(a.baseline);new=read(a.reports)
e=json.loads((a.reports/"source-evidence.json").read_text());assert e["schema"]==1 and fp(e["files"])==e["sourceFingerprint"]
current={str(x.relative_to(a.project)):sha(x) for folder in ("Assets","Packages","ProjectSettings","Tools") for x in sorted((a.project/folder).rglob("*")) if x.is_file() and not any(v.startswith(".") or v=="__pycache__" for v in x.relative_to(a.project).parts)}
assert current==e["files"]
for mode in new:
    assert e["reports"][mode]["sha256"]==new[mode]["xmlSha256"] and e["reports"][mode]["passed"]==new[mode]["count"]
    missing=sorted(set(old[mode]["names"])-set(new[mode]["names"]));assert not missing,missing
    new[mode]["addedNames"]=sorted(set(new[mode]["names"])-set(old[mode]["names"]))
    new[mode]["baselinePreserved"]=old[mode]["count"];new[mode].pop("names")
version=[v.strip() for v in (a.project/"ProjectSettings/ProjectSettings.asset").read_text().splitlines() if "bundleVersion:" in v];assert version==["bundleVersion: 0.15.0"]
result={"schema":1,"status":"passed_frozen_full_regression","createdUtc":datetime.now(timezone.utc).isoformat(),"sourceFingerprint":e["sourceFingerprint"],"sourceInputs":len(current),"baselineReports":str(a.baseline),"reports":str(a.reports),"results":new,"totalPassed":sum(v["count"] for v in new.values()),"limitations":["Unit/InputSystem tests are not native OS UI evidence, natural balance or long-run performance.","Retained names establish test presence. Source fixes to assertions are separately reviewable in Git."]}
assert not a.output.exists();a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(result,indent=2)+"\n");print(json.dumps({k:v for k,v in result.items() if k!="results"},indent=2))
