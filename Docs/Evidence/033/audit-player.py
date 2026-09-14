import json
import re
from pathlib import Path

root = Path(__file__).resolve().parent
report = json.loads((root / 'native-player-menu_exit.json').read_text())
events = [json.loads(line) for line in (root / 'native-player-menu_exit-events.jsonl').read_text().splitlines()]
fresh = json.loads((root / 'native-player-quit.json').read_text())
fresh_events = [json.loads(line) for line in (root / 'native-player-quit-events.jsonl').read_text().splitlines()]
assert report['appVersion'] == fresh['appVersion'] == '0.2.12'
assert report['buildGuid'] == fresh['buildGuid'] == '2d5e2f2410014652bd677920c4f2f465'
assert not report['autoplay'] and report['simulationSpeed'] == 1
assert len(events) == report['eventCount'] and len(fresh_events) == fresh['eventCount']
paid = [e for e in events if e['kind'] == 'Payment' and e['player'] == 0]
assert len(paid) == 1 and paid[0]['message'] == 'Settler ×1: 100 F / 0 W / 0 C'
born = [e for e in events if e['kind'] == 'Spawn' and e['player'] == 0 and e['time'] > 0]
assert len(born) == 1 and born[0]['message'] == 'Settler'
worker = born[0]['entity']
rally = [e for e in events if e['kind'] == 'RallyAssigned' and e['entity'] == worker]
gathers = [e for e in events if e['kind'] == 'Gather' and e['entity'] == worker]
assert len(rally) == 1 and gathers and all(e['target'] == rally[0]['target'] for e in gathers)
pauses = [e for e in events if e['kind'] == 'PauseChanged']
assert pauses[0]['time'] == pauses[1]['time'] and pauses[0]['message'] == 'manual_pause' and pauses[1]['message'] == 'manual_resume'
traces = re.findall(r'FRONTIER_PAUSE paused=(True|False) reason=(\w+) world=([\d.]+) realtime=([\d.]+)', (root / 'NativePlayer.log').read_text())
assert traces[0][2] == traces[1][2]
assert fresh['players'][0]['population'] == 6 and fresh['players'][0]['spent'] == dict(food=0.0, wood=0.0, coin=0.0)
assert fresh['players'][0]['gathered'] == dict(food=0.0, wood=0.0, coin=0.0)
assert not any(e['player'] == 0 and e['kind'] in ('Command', 'RallySet', 'Payment') for e in fresh_events)
result = dict(version=report['appVersion'], buildGuid=report['buildGuid'], autoplay=False, simulationSpeed=1,
    firstMatchSeconds=report['time'], eventCount=len(events), payment=paid[0], born=born[0],
    rally=rally[0], workerGatherEvents=len(gathers), workerGatherFood=sum(e['amount'] for e in gathers),
    workerFirstGatherTime=gathers[0]['time'], pauseWorldTime=pauses[0]['time'],
    pauseWallSeconds=float(traces[1][3])-float(traces[0][3]),
    newMatchSeconds=fresh['time'], newMatchEventCount=len(fresh_events),
    newMatchPlayer=fresh['players'][0], acceptance='Native local input smoke only; no player shipment, age advance, siege or full match in this run.')
(root / 'native-player-audit.json').write_text(json.dumps(result, indent=2, ensure_ascii=False)+'\n')
print(json.dumps({k:v for k,v in result.items() if k not in ('payment','born','rally','newMatchPlayer')}, indent=2))
