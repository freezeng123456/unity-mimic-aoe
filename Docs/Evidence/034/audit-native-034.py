#!/usr/bin/env python3
"""Recompute this iteration's evidence from adjacent original JSON/JSONL/log files."""
from pathlib import Path
from collections import Counter, defaultdict
import hashlib, json, re

root = Path(__file__).resolve().parent
def read_events(stem):
    report = json.loads((root / (stem + '.json')).read_text())
    events = [json.loads(line) for line in (root / (stem + '-events.jsonl')).read_text().splitlines()]
    assert len(events) == report['eventCount']
    assert report['buildGuid'] == 'ae1dace836c84d79b94d291667478592'
    assert report['autoplay'] is False and report['simulationSpeed'] == 1
    return report, events

report, events = read_events('native-normal-finished')
p0 = [e for e in events if e['player'] == 0]
def kind(name): return [e for e in p0 if e['kind'] == name]
def compact(e): return {k:e[k] for k in ('time','kind','entity','target','amount','message')}
spawns = {e['entity']:e for e in events if e['kind'] == 'Spawn'}
repair = [e for e in kind('Payment') if e['message'].startswith('Repair ')]
payments = [e for e in kind('Payment') if not e['message'].startswith('Repair ')]
coins = [e for e in kind('Spawn') if e['message'] == 'Coin Crate']
assert len(coins) == 1
coin_id = coins[0]['entity']
coin_gather = [e for e in kind('Gather') if e['target'] == coin_id]
gather = defaultdict(float)
for e in kind('Gather'): gather[e['message']] += e['amount']
tc_death = next(e for e in kind('Death') if e['message'] == 'Town Center lost')
ended = next(e for e in events if e['kind'] == 'MatchEnded')
assert not any(e['kind'] == 'Resign' for e in events)
assert ended['message'] == 'All opposing units and buildings eliminated'
settlers = [e for e in kind('Spawn') if e['message'] == 'Settler']
military = [e for e in kind('Spawn') if e['message'] in ('Musketeer','Pikeman','Longbow Guard','Field Cannon')]
assert len(settlers) == 18 and not military
assert len(payments) == 14
pauses = []
for m in re.finditer(r'FRONTIER_PAUSE paused=(True|False) reason=(\w+) world=([\d.]+) realtime=([\d.]+) frame=(\d+)', (root/'NativeNormal.log').read_text()):
    pauses.append(dict(paused=m[1]=='True',reason=m[2],world=float(m[3]),realtime=float(m[4]),frame=int(m[5])))
assert len(pauses) == 6
pause_pairs = []
for before,after in zip(pauses[::2],pauses[1::2]):
    assert before['paused'] and not after['paused'] and before['world'] == after['world']
    pause_pairs.append(dict(world=before['world'],wallSeconds=after['realtime']-before['realtime'],pause=before,resume=after))
restart, rev = read_events('native-normal-restart')
restart_p0 = [e for e in rev if e['player'] == 0]
assert restart['players'][0]['age'] == 1 and restart['players'][0]['shipments'] == 0
assert not any(e['kind'] in ('Payment','ShipmentSent','AgeAdvanced') for e in restart_p0)
spawn_restart = [e for e in restart_p0 if e['kind']=='Spawn']
assert sum(e['message']=='Settler' for e in spawn_restart) == 6
audit = {
 'generatedBy':Path(__file__).name,
 'inputHashes':{name:hashlib.sha256((root/name).read_bytes()).hexdigest() for name in ('native-normal-finished.json','native-normal-finished-events.jsonl','native-normal-restart.json','native-normal-restart-events.jsonl','NativeNormal.log')},
 'identity':{k:report[k] for k in ('appVersion','buildGuid','version','seed','maxAge','simulationSpeed','autoplay','eventCount')},
 'player0KindCounts':dict(Counter(e['kind'] for e in p0)),
 'playersFromReport':report['players'],
 'nonRepairPayments':[compact(e) for e in payments],
 'settlerSpawns':[compact(e) for e in settlers],
 'militarySpawns':[compact(e) for e in military],
 'buildings':[compact(e) for e in p0 if e['message'] in ('Manor','Barracks','Manor ready','Barracks ready','Manor lost','Barracks lost')],
 'agesAndShips':[compact(e) for e in p0 if e['kind'] in ('AgeAdvanced','AgeRewardArrived','ShipmentSent','ShipmentArrived')],
 'coinCrate':{'spawn':compact(coins[0]),'gatherCount':len(coin_gather),'gatherAmount':sum(e['amount'] for e in coin_gather),'gatherEvents':[compact(e) for e in coin_gather],'deathEvents':[compact(e) for e in kind('Death') if e['entity']==coin_id]},
 'gatherEventTotals':dict(gather),
 'repair':{'count':len(repair),'firstTime':repair[0]['time'],'lastTime':repair[-1]['time'],'actors':sorted(set(e['entity'] for e in repair)),'messages':dict(Counter(e['message'] for e in repair)),'woodSpentMinusFourFoundations':report['players'][0]['spent']['wood']-670,'limit':'Payment message rounds per-tick costs to integers; not an exact per-event ledger or an HP snapshot.'},
 'treasures':[compact(e) for e in kind('TreasureClaimed')],
 'garrison':[compact(e) for e in p0 if e['kind'] in ('Garrison','Ungarrison')],
 'deathCounts':dict(Counter(e['message'] for e in kind('Death'))),
 'combatCounts':{k:len(kind(k)) for k in ('AttackPrepared','Fire','Impact','Damage')},
 'end':{'townCenterDeath':compact(tc_death),'matchEnded':compact(ended),'delaySeconds':ended['time']-tc_death['time']},
 'focusPausePairs':pause_pairs,
 'restart':{'report':restart,'player0SpawnCount':len(spawn_restart),'player0CommandEvents':[compact(e) for e in restart_p0 if e['kind']=='Command'],'player0GatherCount':sum(e['kind']=='Gather' for e in restart_p0)},
 'limits':['No claim of successful player cannon siege or stage B/C/D acceptance.','No population reservation snapshots exist in the event stream; visual discrepancy needs a dedicated reproduction.','Frame metrics describe this single match, not 50/100/200/400 validation.','Diagnostics based on the older probe binary are excluded.']
}
(root/'native-normal-audit-generated.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'events':len(events),'combat':audit['combatCounts'],'coinCollected':audit['coinCrate']['gatherAmount'],'pausePairs':[{k:p[k] for k in ('world','wallSeconds')} for p in pause_pairs],'restartTime':restart['time'],'restartEvents':len(rev),'tcToEnd':audit['end']['delaySeconds']},indent=2))
