#!/usr/bin/env python3
"""Read one actual player report and its sibling event JSONL, without simulating a match."""
import json,sys,collections
from pathlib import Path
report=Path(sys.argv[1]);events=report.with_name(report.stem+'-events.jsonl')
r=json.loads(report.read_text());es=[json.loads(line) for line in events.read_text().splitlines()]
assert len(es)==r['eventCount']
spawns={e['entity']:e for e in es if e['kind']=='Spawn'}
selected=lambda kind,p=0:[e for e in es if e['kind']==kind and e['player']==p]
minimal=lambda e:{k:e[k] for k in ['time','kind','player','entity','target','message','amount']}
town_ids={i for i,e in spawns.items() if e['player']==0 and e['message']=='Town Center'}
town_deaths=[e for e in selected('Death') if e['entity'] in town_ids]
ends=[e for e in es if e['kind']=='MatchEnded']
repairs=[e for e in selected('Payment') if e['message'].startswith('Repair ')]
result={
 'build':{k:r[k] for k in ['appVersion','buildGuid','version','platform','resolution']},
 'match':{k:r[k] for k in ['reason','autoplay','simulationSpeed','seed','maxAge','time','winner','eventCount']},
 'resignEvents':len([e for e in es if e['kind']=='Resign']),
 'townDeaths':[minimal(e) for e in town_deaths],
 'matchEnded':[minimal(e) for e in ends],
 'secondsAfterLastTownDeath':ends[0]['time']-town_deaths[-1]['time'] if town_deaths and ends else None,
 'player0Report':r['players'][0],
 'shipsSent':[minimal(e) for e in selected('ShipmentSent')],
 'shipsArrived':[minimal(e) for e in selected('ShipmentArrived')],
 'buildingsCompleted':[minimal(e) for e in selected('BuildingComplete')],
 'player0Deaths':dict(collections.Counter(e['message'] for e in selected('Death'))),
 'player0FireImpactDamageCounts':{k:len(selected(k)) for k in ['Fire','Impact','Damage']},
 'player0FireByEntity':dict(collections.Counter(spawns.get(e['entity'],{}).get('message','Unknown') for e in selected('Fire'))),
 'repairPaymentEvents':{'count':len(repairs),'first':minimal(repairs[0]) if repairs else None,'last':minimal(repairs[-1]) if repairs else None},
 'limits':['Fire and Impact are distinct; Damage event amount is not a net HP-loss audit.',
           'Payment message rounds resource values; it cannot prove a zero-cost repair.',
           'Restart, menu return, audio and playability require separate evidence.',
           'An ordinary player defeat with age II only does not accept the full core loop.']}
print(json.dumps(result,indent=2,ensure_ascii=False))
