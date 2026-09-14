import json,hashlib
from pathlib import Path
r=Path(__file__).parent
def dg(p): return hashlib.sha256(p.read_bytes()).hexdigest()
out={}
for d in ('easy','standard','hard'):
 p=r/'matrix'/d;ev=[json.loads(x) for x in (p/'events.jsonl').read_text().splitlines()];rep=json.loads((p/'result.json').read_text());bad=[];structured=0;pop_bad=[]
 for e in ev:
  eco=e.get('economy',{});b=eco.get('balanceBefore',{});a=eco.get('actualDelta',{});z=eco.get('balanceAfter',{})
  if e.get('hasPopulation'):
   pop=e['population']
   if any(pop[k]<0 for k in ('used','reserved','capacity')):pop_bad.append([e['time'],e['kind'],pop])
  if e.get('hasEconomy'):
   structured+=1
   for k in ('food','wood','coin'):
    if abs(b.get(k,0)+a.get(k,0)-z.get(k,0))>.02 or z.get(k,0)<-.02:bad.append([e.get('time'),e.get('kind'),k])
   if e.get('kind')=='Payment' and any(a.get(k,0)>.02 for k in ('food','wood','coin')):bad.append([e.get('time'),'Payment','positiveDelta'])
 out[d]={'result':rep,'payment':sum(e.get('kind')=='Payment' for e in ev),'shipSent':sum(e.get('kind')=='ShipmentSent' for e in ev),'shipArrived':sum(e.get('kind')=='ShipmentArrived' for e in ev),'structuredEconomyEvents':structured,'ledgerAnomalies':bad,'populationRecords':sum(e.get('hasPopulation',False) for e in ev),'negativePopulationRecords':pop_bad,'players':[{'player':q,'payment':sum(e['kind']=='Payment' and e['player']==q for e in ev),'shipSent':sum(e['kind']=='ShipmentSent' and e['player']==q for e in ev),'shipArrived':sum(e['kind']=='ShipmentArrived' and e['player']==q for e in ev)} for q in (0,1)]}
b=r/'baseline-standard';out['baselineStandardByteComparison']={f:dg(b/f)==dg(r/'matrix'/'standard'/f) for f in ('events.jsonl','result.json','snapshots.jsonl')};a=json.loads((b/'result.json').read_text());s=json.loads((r/'matrix'/'standard/result.json').read_text());out['baselineStandardResultDifferences']={k:[a.get(k),s.get(k)] for k in set(a)|set(s) if a.get(k)!=s.get(k)}
(r/'matrix-audit.json').write_text(json.dumps(out,indent=2,ensure_ascii=False)+'\n')
