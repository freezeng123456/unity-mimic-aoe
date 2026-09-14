#!/usr/bin/env python3
"""Recompute iteration 035 evidence; no runtime mutations or inferred hidden state."""
from pathlib import Path
from collections import Counter
from datetime import datetime
import hashlib, json, math, re, struct

root = Path(__file__).resolve().parent
stem = '20260914-102439-356-3ecbfd2c61704425b4dceda69b895bd2-match_finished'
report_file, events_file = root/(stem+'.json'), root/(stem+'-events.jsonl')
report = json.loads(report_file.read_text())
events = [json.loads(line) for line in events_file.read_text().splitlines()]
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
assert len(events) == report['eventCount']
assert report['eventSchemaVersion'] == 2
assert all(e['schemaVersion'] == 2 for e in events)
assert report['autoplay'] is False and report['simulationSpeed'] == 1
assert report['buildGuid'] == 'da1595b3e2d148f084d7fb03bb783c93'
axes = ('food','wood','coin')
selected_keys = ('time','kind','player','entity','target','message','population','hasPopulation')
brief = lambda e: {k:e[k] for k in selected_keys}
transactions = [e for e in events if e['hasEconomy']]
assert all(e['kind'] in ('Payment','Refund') for e in transactions)
assert all(e['hasEconomy'] for e in events if e['kind'] in ('Payment','Refund'))
errors = []
for event in transactions:
    economy = event['economy']
    for axis in axes:
        expected = economy['balanceAfter'][axis]-economy['balanceBefore'][axis]
        actual = economy['actualDelta'][axis]
        if not math.isclose(expected, actual, rel_tol=1e-7, abs_tol=1e-5):
            errors.append([event['time'],axis,expected,actual])
        assert actual <= 0 if event['kind']=='Payment' else actual >= 0
    if economy['hasRepair']:
        assert economy['hpAfter'] > economy['hpBefore']
        assert economy['healthTarget'] == event['target']
assert not errors, errors[:5]
players = {}
for p in (0,1):
    own = [e for e in events if e['player']==p]
    repair = [e for e in own if e['hasEconomy'] and e['economy']['hasRepair']]
    by_message = {}
    for event in own:
        if not event['hasEconomy']: continue
        label = event['kind']+': '+('Repair' if event['economy']['hasRepair'] else event['message'])
        group = by_message.setdefault(label, {'count':0,'requestedDelta':dict.fromkeys(axes,0.),'actualDelta':dict.fromkeys(axes,0.)})
        group['count'] += 1
        for source in ('requestedDelta','actualDelta'):
            for axis in axes: group[source][axis] += event['economy'][source][axis]
    players[str(p)] = {
        'transactions': by_message,
        'repair': {'count':len(repair), 'targetIds':sorted({e['target'] for e in repair}),
                   'workerIds':sorted({e['entity'] for e in repair}),
                   'hpRestoredSum':sum(e['economy']['hpAfter']-e['economy']['hpBefore'] for e in repair),
                   'first':repair[0] if repair else None, 'last':repair[-1] if repair else None},
        'milestones':[brief(e) for e in own if e['kind'] in ('ProductionQueued','BuildingComplete','AgeAdvanced','ShipmentSent','ShipmentArrived','TreasureClaimed')],
        'refunds':[e for e in own if e['kind']=='Refund'],
        'counts':dict(Counter(e['kind'] for e in own))}
bar_queue = next(e for e in events if e['kind']=='ProductionQueued' and e['entity']==172)
bar_death = next(e for e in events if e['kind']=='Death' and e['entity']==172)
tc_death = next(e for e in events if e['kind']=='Death' and e['entity']==1)
end = next(e for e in events if e['kind']=='MatchEnded')
assert bar_queue['message']=='pike ×5' and bar_queue['population']['reserved']==5
assert bar_death['hasPopulation'] and bar_death['population']['reserved']==0
assert not [e for e in events if e['kind']=='Refund' and e['entity']==172]
assert not [e for e in events if e['kind']=='Spawn' and e['player']==0 and 'Pikeman' in e['message']]
assert not [e for e in events if e['kind']=='Resign' or (e['kind']=='Command' and e['message'].startswith('Resign'))]
capture_rows = []
index = [json.loads(x) for x in (root/'capture-index.jsonl').read_text().splitlines()]
for row in index:
    label = row['label']
    meta = json.loads((root/(label+'.json')).read_text())
    png = root/(label+'.png')
    dimensions = struct.unpack('>II',png.read_bytes()[16:24])
    assert dimensions == (meta['width'],meta['height']) == (2560,1440)
    assert sha(png) == meta['pngSha256'] == row['sha256']
    assert meta['pngFile'] == row['sourcePng']
    assert meta['matchId'] == meta['requestMatchId']
    assert meta['buildGuid'] == report['buildGuid']
    if meta['hasPlayer']:
        pop = meta['population']
        text_pop = re.search(r'POP\s+(\d+)\+(\d+)/(\d+)',meta['hudResources'])
        assert text_pop and tuple(map(int,text_pop.groups())) == tuple(pop[k] for k in ('used','reserved','capacity'))
        assert meta['matchId'] == meta['hudMatchId']
    else:
        assert meta['inMenu'] and not meta['matchId'] and not meta['hudResources']
    capture_rows.append({'label':label,'utc':meta['utc'],'matchId':meta['matchId'],'worldTime':meta['worldTime'],
                         'population':meta['population'],'paused':meta['paused'],'inMenu':meta['inMenu'],
                         'captureFrame':meta['captureFrame'],'hudUpdatedFrame':meta['hudUpdatedFrame'],
                         'hudWorldTime':meta['hudWorldTime'],'pngSha256':meta['pngSha256']})
meta_by_label = lambda label: json.loads((root/(label+'.json')).read_text())
a,b = [meta_by_label('native-restart-paused-'+suffix) for suffix in ('a','b')]
assert a['worldTime']==b['worldTime']==.5 and a['paused'] and b['paused']
assert a['resources']==b['resources'] and a['production']==b['production']
paused_seconds = (datetime.fromisoformat(b['utc'].replace('Z','+00:00'))-datetime.fromisoformat(a['utc'].replace('Z','+00:00'))).total_seconds()
queued,reset = [meta_by_label(label) for label in ('native-queued-paused-a','native-restart-cleared')]
assert queued['population']['reserved']==1 and reset['population']['reserved']==0
assert queued['matchId']!=reset['matchId'] and reset['production'][0]['queue']==[]
result = {'inputs':{report_file.name:sha(report_file),events_file.name:sha(events_file)},
          'report':report,'events':len(events),'transactionCount':len(transactions),
          'transactionBalanceTolerance':{'absolute':1e-5,'relative':1e-7},'transactionBalanceMismatches':errors,
          'players':players,'bar172Queued':brief(bar_queue),'bar172Death':brief(bar_death),
          'tcDeath':brief(tc_death),'end':brief(end),'tcDeathToEndSeconds':end['time']-tc_death['time'],
          'captures':capture_rows,'captureCount':len(capture_rows),'pauseWallSeconds':paused_seconds,
          'caveats':['Financial checks apply to Payment/Refund only; Gather/BankIncome remain grouped float records.',
                     'HUD string vs population checks are not automated pixel OCR.',
                     'Capture filename labels are annotations, not proof of the named action.',
                     'No uninterrupted successful player siege or five-age game was completed.',
                     'MatchReport frame samples exclude long stalls and are not a 50/100/200/400 performance acceptance.']}
(root/'native-audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'events':len(events),'transactions':len(transactions),'captures':len(capture_rows),
                  'repairCount':players['0']['repair']['count'],'repairWoodActual':players['0']['transactions']['Payment: Repair']['actualDelta']['wood'],
                  'repairHp':players['0']['repair']['hpRestoredSum'],'tcDeathToEndSeconds':result['tcDeathToEndSeconds'],
                  'pauseWallSeconds':paused_seconds},indent=2))
