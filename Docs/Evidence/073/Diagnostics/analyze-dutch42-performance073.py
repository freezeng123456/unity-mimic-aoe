#!/usr/bin/env python3
"""Read closed 072 originals; emit a bounded 073 analysis, without running Unity."""
import collections
import datetime
import hashlib
import json
import pathlib
import subprocess

REPO = pathlib.Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
ROOT = pathlib.Path('/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.15.0/Candidate1/NormalRoutesFinal1/matrix')
OUT = pathlib.Path(__file__).resolve().parent
SOURCE = 'bc127e02638caaeefd2310a1a3eff858f4f4cb2f'
CASES = ('traders_bend070-river-42', 'traders_bend070-river-43', 'traders_bend070-france-43', 'winding-ottoman-44')
refs = {}

def hash_file(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for data in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(data)
    return digest.hexdigest()

def reference(path):
    path = path.resolve()
    if str(path) not in refs:
        stat = path.stat()
        refs[str(path)] = {'path': str(path), 'bytes': stat.st_size,
                           'sha256': hash_file(path), 'mtimeNs': stat.st_mtime_ns,
                           'inode': stat.st_ino, 'device': stat.st_dev}
    return path

def load(path):
    return json.loads(reference(path).read_text())

def world_entities(path):
    """Extract the exact pretty-JSON entities array before the large event array."""
    reference(path)
    data = []; active = False
    with path.open() as stream:
        for line in stream:
            if not active:
                if line == '    "entities": [\n':
                    active = True; data.append('[')
                continue
            if line == '    ],\n':
                data.append(']')
                result = json.loads(''.join(data))
                assert isinstance(result, list) and result
                return result
            data.append(line)
    raise ValueError('Exact top-level entity array terminator missing')

report = {'schema': 'frontier-readonly-longrun-performance073-1',
          'createdUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
          'status': 'closed_originals_analyzed_no_profile_executed',
          'runtimeSourceCommit': SOURCE, 'cases': [],
          'scope': 'No original edits, no Unity launch, no runtime instrumentation, no timing reconstruction from mtime. Existing per-case wall includes setup, simulation, observer I/O and final serialization. No native FPS claim.',
          'sourceReferences': []}
manifest = load(ROOT / 'manifest.json')
for case in CASES:
    directory = ROOT / case
    result = load(directory / 'result.json')
    manifest_result = next(x for x in manifest['cases'] if x['id'] == case)
    assert result == manifest_result
    events_path = reference(directory / 'events.jsonl')
    bins = collections.defaultdict(collections.Counter)
    last_time = -1.; event_count = 0
    with events_path.open() as stream:
        for line in stream:
            event = json.loads(line)
            assert event['time'] >= last_time
            last_time = event['time']; event_count += 1
            bins[int(event['time'] // 300)][event['kind']] += 1
    samples_path = reference(directory / 'samples.jsonl')
    samples = []
    with samples_path.open() as stream:
        for line in stream:
            sample = json.loads(line)
            if sample['player'] != 0:
                continue
            samples.append(sample)
    final_entities = world_entities(directory / 'final-world.json')
    alive = [e for e in final_entities if e['state']['hp'] > 0]
    final_by_player = []
    for player in (0, 1):
        owned = [e for e in alive if e['state']['owner'] == player]
        final_by_player.append({'player': player, 'alive': len(owned),
            'statuses': dict(collections.Counter(e['state'].get('status', '') for e in owned)),
            'orders': dict(collections.Counter(o['kind'] for e in owned for o in e['orders'])),
            'actorsWithPaths': sum(bool(e['path']) for e in owned),
            'incompleteEntities': [{'id': e['state']['id'], 'definition': e['definition'],
                'construction': e['state']['construction']} for e in owned if 0 < e['state']['construction'] < 1]})
    summarized = []
    for wanted in (1800, 2100, 2400, 2700, 3000, 3300, result['seconds']):
        sample = min(samples, key=lambda x: abs(x['time'] - wanted))
        if summarized and summarized[-1]['time'] == sample['time']:
            continue
        summarized.append({'time': sample['time'], 'workersP0': sample['workers'],
            'soldiersP0': sample['soldiers'], 'buildingsP0': len(sample['buildings']),
            'armyPlans': [{'player': ai['player'], 'plan': ai['armyPlan']}
                          for ai in sample['simulation']['controllers']]})
    report['cases'].append({'id': case, 'wallSecondsEntireObserverCase': result['wallSeconds'],
        'simulationSeconds': result['seconds'], 'winner': result['winner'],
        'eventCount': event_count, 'eventsBytes': events_path.stat().st_size,
        'samplesBytes': samples_path.stat().st_size,
        'finalWorldBytes': (directory / 'final-world.json').stat().st_size,
        'finalEntityCount': len(final_entities), 'finalPlayers': final_by_player,
        'eventBins300s': [{'begin': 300 * b, 'endExclusive': 300 * (b + 1),
                          'events': sum(kinds.values()), 'kinds': dict(kinds)}
                         for b, kinds in sorted(bins.items())],
        'selectedSampleStates': summarized})

for path in ('Assets/Frontier/Editor/NormalRoutes072.cs',
             'Assets/Frontier/Simulation/AiResourceContinuity072.cs',
             'Assets/Frontier/Simulation/AiArmyPlan072.cs',
             'Assets/Frontier/Simulation/SkirmishAI.cs',
             'Assets/Frontier/Simulation/MovementRecovery.cs',
             'Assets/Frontier/Simulation/GridNavigation.cs'):
    data = subprocess.check_output(['git', 'show', SOURCE + ':' + path], cwd=str(REPO))
    report['sourceReferences'].append({'gitCommit': SOURCE, 'path': path,
                                     'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()})

dutch = report['cases'][0]; france = report['cases'][2]
late = next(b for b in dutch['eventBins300s'] if b['begin'] == 3300)
assert all(late['kinds'].get(k, 0) == 0 for k in ('AttackPrepared', 'Fire', 'Damage'))
late_samples = [x for x in dutch['selectedSampleStates'] if x['time'] >= 2400]
assert all(ai['plan']['hasStage'] is False and ai['plan']['wave'] == []
           and ai['plan']['phase'] in (0, 2) for x in late_samples for ai in x['armyPlans'])
report['confirmed'] = {
    'dutchVsFranceWallRatio': dutch['wallSecondsEntireObserverCase'] / france['wallSecondsEntireObserverCase'],
    'dutchVsFranceEventsBytesRatio': dutch['eventsBytes'] / france['eventsBytes'],
    'dutchVsFranceFinalWorldBytesRatio': dutch['finalWorldBytes'] / france['finalWorldBytes'],
    'late3300to3600CombatEventCounts': {k: late['kinds'].get(k, 0) for k in ('AttackPrepared', 'Fire', 'Damage')},
    'finalIdleEntitiesTotal': sum(p['statuses'].get('Idle', 0) for p in dutch['finalPlayers']),
    'finalAliveOwnedEntitiesTotal': sum(p['alive'] for p in dutch['finalPlayers']),
    'lateSelectedSamplesBothAiHaveNoStageOrWave': True,
    'initialThreeBinsRiver42And43KindCountsEqual': all(a['kinds'] == b['kinds'] for a, b in zip(dutch['eventBins300s'][:3], report['cases'][1]['eventBins300s'][:3]))}
report['hypotheses'] = [
    {'priority': 1, 'topic': 'Repeated construction/source filtered navigation queries',
     'basis': 'Construction worker search includes prospective ordinary navigation plus a separate filtered route and repeated current-threat predicates; up to 6 workers and 8 approaches. Source safety has a query-local connectivity optimization, construction does not.',
     'verifiedCpuCause': False},
    {'priority': 2, 'topic': 'Dense ally movement recovery',
     'basis': 'Dutch final has 12 Route blocked; retrying, 219 Idle, 302 owned alive; MovementRecovery can use a 4x grid (512x512) with per-query ally filters. One reserved fine slot bounds scheduling, not individual search time.',
     'verifiedCpuCause': False},
    {'priority': 3, 'topic': 'Home selection and no-stage army reviews',
     'basis': 'Home scans each eligible worker/facility against all world entities even when an owned Town exists. Late SelectStage repeatedly fails; all 25 stage candidates require visible/safe 7x7 area before cohort dispatch.',
     'verifiedCpuCause': False},
    {'priority': 4, 'topic': 'Per-step repair reserve source survey',
     'basis': 'StopRecoveryRepairs runs before thinkAt decision gate each fixed Step; with any active repair it evaluates fresh source safety. A current branch with repairs is needed to test this hypothesis. Final Dutch states show no Repair order, so final-state profile cannot prove earlier repair overhead.',
     'verifiedCpuCause': False},
    {'priority': 5, 'topic': 'Observer serialization and event accumulation',
     'basis': 'Wall stopwatch includes JSON serialization and writes. Recent trade forecasting scans only the last 60 simulation seconds per eligible post, not all history each Tick. Dutch event/archive size is about 1.3x French, wall about 5.9x; size alone cannot justify attribution.',
     'verifiedCpuCause': False}]

for path, saved in refs.items():
    current = pathlib.Path(path); stat = current.stat()
    assert (stat.st_size, stat.st_mtime_ns, stat.st_ino, stat.st_dev) == (saved['bytes'], saved['mtimeNs'], saved['inode'], saved['device'])
    assert hash_file(current) == saved['sha256']
report['originalReferences'] = list(refs.values())
report['originalsUnchangedDuringAnalysis'] = True
output = OUT / '073-dutch42-readonly-performance-audit.json'
with output.open('x') as stream:
    json.dump(report, stream, ensure_ascii=False, indent=2); stream.write('\n')
print(json.dumps({'status': report['status'], 'output': str(output), 'bytes': output.stat().st_size,
                  'sha256': hash_file(output), 'confirmed': report['confirmed'],
                  'originalReferences': len(refs)}, ensure_ascii=False))
