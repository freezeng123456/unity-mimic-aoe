#!/usr/bin/env python3
"""Audit a saved native match report and event file. Does not simulate or control play."""
import json,sys,collections
from pathlib import Path
p=Path(sys.argv[1]);r=json.loads(p.read_text());events=p.with_name(p.stem+'-events.jsonl');es=[json.loads(s) for s in events.read_text().splitlines()]
assert len(es)==r['eventCount'], 'Report/event count mismatch'
spawns={e['entity']:e for e in es if e['kind']=='Spawn'}
compact=lambda e:{k:e[k] for k in ['time','kind','player','entity','target','message','amount']}
loser=1-r['winner'] if r['winner'] in [0,1] else -1
losses=[e for e in es if e['kind']=='Death' and e['player']==loser]
towns=[e for e in losses if spawns.get(e['entity'],{}).get('message')=='Town Center']
players={}
for n in [0,1]:
 pe=[e for e in es if e['player']==n];cannon={i for i,e in spawns.items() if e['player']==n and e['message']=='Field Cannon'}
 records={k:[e for e in pe if e['kind']==k and e['entity'] in cannon] for k in ['Fire','Impact','Damage']}
 targets={k:dict(collections.Counter(spawns.get(e['target'],{}).get('message','Unknown') for e in items)) for k,items in records.items()}
 players[str(n)]={'report':r['players'][n],'shipmentsSent':len([e for e in pe if e['kind']=='ShipmentSent']),'shipmentsArrived':len([e for e in pe if e['kind']=='ShipmentArrived']),'cannonSpawns':len(cannon),'cannonEvents':{k:len(v) for k,v in records.items()},'cannonTargetsByKind':targets,'automaticRallyEvents':sum(e['kind']=='RallyAssigned' for e in pe),'evacuationWaitingEvents':sum(e['kind']=='EvacuationWaiting' for e in pe)}
out={'source':{'report':p.name,'events':events.name,'eventLines':len(es),'reportEventCount':r['eventCount']},'build':{k:r[k] for k in ['appVersion','buildGuid','version','platform','resolution','autoplay','simulationSpeed','seed','maxAge']},'result':{k:r[k] for k in ['reason','time','winner']},'resignEvents':sum(e['kind']=='Resign' for e in es),'matchEnded':[compact(e) for e in es if e['kind']=='MatchEnded'],'losingTownDeaths':[compact(e) for e in towns],'lastLosingDeath':compact(losses[-1]) if losses else None,'secondsBetweenLastTownDeathAndLastDeath':losses[-1]['time']-towns[-1]['time'] if losses and towns else None,'players':players,'samplePerformance':{k:r[k] for k in ['meanFrameMs','p95FrameMs','managedBytes']},'limits':['One native automatic match, not a complete player-controlled game or a broad AI balance result.','Fire, Impact and Damage counted separately with source entity IDs joined to original spawns; Damage amounts are not net HP audit.','Performance is an 8x automatic sample, not the 50/100/200/400 entity matrix or a 4K60 claim.','No cross-machine install, audio audition, or general deterministic replay claim.']}
print(json.dumps(out,indent=2,ensure_ascii=False))
