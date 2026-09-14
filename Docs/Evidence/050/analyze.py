from pathlib import Path
import json,hashlib,collections
w=Path(__file__).parent;p=w.parents[2]
rules=json.loads((p/'Assets/Resources/Rules.json').read_text());defs={d['name']:d for d in rules['entities']};summary={}
for name in ['baseline-coast','baseline-river-coast','normal-coast','normal-river-coast']:
 path=w.parent/'049'/name.replace('baseline-','normal-') if name.startswith('baseline-') else w/name;events=[json.loads(l) for l in (path/'events.jsonl').read_text().splitlines()];result=json.loads((path/'result.json').read_text());snapshots=[json.loads(l) for l in (path/'snapshots.jsonl').read_text().splitlines()]
 assert result['events']==len(events)
 spawned={e['entity']:e for e in events if e['kind']=='Spawn'};buildings={i for i,e in spawned.items() if defs.get(e['message'],{}).get('kind')==1};sustained={i for i,e in spawned.items() if defs.get(e['message'],{}).get('id') in ['farm','estate']}
 completed={e['entity']:e['time'] for e in events if e['kind']=='BuildingComplete'};deaths={e['entity']:e['time'] for e in events if e['kind']=='Death'}
 premature=[e for e in events if e['kind']=='Command' and e['message'].startswith('Gather') and e['target'] in sustained and e['time']<completed.get(e['target'],float('inf'))]
 resumed=[{'time':e['time'],'player':e['player'],'worker':e['entity'],'foundation':e['target'],'completedAt':completed.get(e['target']),'destroyedAt':deaths.get(e['target'])} for e in events if e['kind']=='AIConstructionResumed']
 cannonDamage=[e for e in events if e['kind']=='Damage' and e['target'] in buildings and defs.get(spawned.get(e['entity'],{}).get('message'),{}).get('id')=='cannon']
 active=[set(),set()];firstTownDeaths={};ledgerFailures=[];populationFailures=[];structuredKinds=collections.Counter()
 for e in events:
  owner=e['player'];i=e['entity']
  if e['kind']=='Spawn' and owner in [0,1] and defs.get(e['message'],{}).get('kind')!=2:active[owner].add(i)
  if e['kind']=='Death':
   for ids in active:ids.discard(i)
   if spawned.get(i,{}).get('message')=='Town Center' and owner in [0,1] and owner not in firstTownDeaths:firstTownDeaths[owner]={'time':e['time'],'remainingUnitsAndBuildings':len(active[owner])}
  if e.get('hasEconomy'):
   structuredKinds[e['kind']]+=1;t=e['economy']
   for k in ['food','wood','coin']:
    if abs(t['balanceBefore'][k]+t['actualDelta'][k]-t['balanceAfter'][k])>.02 or t['balanceAfter'][k]<-.02:ledgerFailures.append([e['time'],e['kind'],k])
  if e.get('hasPopulation') and any(e['population'][k]<0 for k in ['used','reserved','capacity']):populationFailures.append(e['time'])
 ended=[e for e in events if e['kind']=='MatchEnded'];winner=result['winner']
 assert winner in [0,1] and len(ended)==1 and ended[0]['player']==winner and not active[1-winner],(name,winner,len(ended),len(active[1-winner]))
 assert not ledgerFailures and not populationFailures
 if name.startswith('normal-'):assert not premature,(name,len(premature))
 techs=[(e['player'],e['message'].split()[0]) for e in events if e['kind']=='Technology'];duplicates=[str(k) for k,n in collections.Counter(techs).items() if n>1];assert not duplicates
 summary[name]={'result':result,'searchPoints':sum(e['kind']=='AISearchPoint' for e in events),'deferredSearchPoints':sum(e['kind']=='AISearchDeferred' for e in events),'prematureGatherCount':len(premature),'prematureGatherExamples':[{'time':e['time'],'player':e['player'],'worker':e['entity'],'target':e['target'],'completedAt':completed.get(e['target'])} for e in premature[:6]],'constructionResumes':resumed,'cannonBuildingDamageEvents':{str(i):sum(e['player']==i for e in cannonDamage) for i in [0,1]},'firstTownDeaths':firstTownDeaths,'survivingUnitsAndBuildings':list(map(len,active)),'structuredEconomyKinds':dict(structuredKinds),'ledgerAnomalies':ledgerFailures,'negativePopulationRecords':populationFailures,'duplicateTechnologyEvents':duplicates,'players':[{'player':i,'ages':[{'time':e['time'],'message':e['message']} for e in events if e['kind']=='AgeAdvanced' and e['player']==i],'shipmentsSent':sum(e['kind']=='ShipmentSent' and e['player']==i for e in events),'shipmentsArrived':sum(e['kind']=='ShipmentArrived' and e['player']==i for e in events),'finalSnapshot':snapshots[-2+i]} for i in [0,1]],'files':{f.name:{'bytes':f.stat().st_size,'sha256':hashlib.sha256(f.read_bytes()).hexdigest()} for f in path.iterdir() if f.is_file()}}
(w/'analysis.json').write_text(json.dumps(summary,indent=2)+'\n')
for name,s in summary.items():print(name,{k:s[k] for k in ['result','prematureGatherCount','constructionResumes','cannonBuildingDamageEvents','firstTownDeaths','structuredEconomyKinds']})
