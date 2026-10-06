#!/usr/bin/env python3
"""Independent standard-library audit of NormalRoutes072 artifacts.

Prepare (does not claim that a matrix ran):
  python3 -B independent-normal072.py --prepare
Audit an exited, frozen runner directory (never run against a live matrix):
  python3 -B independent-normal072.py --matrix /absolute/Routes/matrix \
      --runner /absolute/Routes --output /absolute/Diagnostics/normal072-audit.json

No observer/helper is imported; no Unity, subprocess, network or account access.
Only an explicitly requested, exclusive Diagnostics receipt is written.
"""
import argparse
from collections import Counter
from datetime import datetime, timezone
import hashlib
import json
import math
import os
from pathlib import Path
import stat
import struct
import sys

HERE = Path(__file__).absolute().parent
PROJECT = Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
OBSERVER = 'Assets/Frontier/Editor/NormalRoutes072.cs'
RULES = 'Assets/Resources/Rules.json'
ROOTS = ('Assets', 'Packages', 'ProjectSettings', 'Tools')
CIVS = ('coast', 'river', 'france', 'ottoman')
MATRIX = [(m, c, s) for m, seeds in (('traders_bend070', (42, 43)), ('winding', (44, 45)))
          for c in CIVS for s in seeds]
KEYS = ('food', 'wood', 'coin')
LIMIT, HORIZON, INTERVAL = 3600, 2400, 30


class AuditError(ValueError):
    pass


def require(ok, message):
    if not ok:
        raise AuditError(message)


def pairs(items):
    result = {}
    for key, value in items:
        require(key not in result, 'Duplicate JSON property: ' + key)
        result[key] = value
    return result


def decode(raw):
    return json.loads(raw.decode('utf-8-sig'), object_pairs_hook=pairs,
                      parse_constant=lambda _: (_ for _ in ()).throw(AuditError('Nonfinite JSON')))


def identity(s):
    return (s.st_dev, s.st_ino, s.st_mode, s.st_size, s.st_mtime_ns, s.st_ctime_ns)


class Inputs:
    def __init__(self):
        self.files = {}

    def stream(self, path):
        path = Path(path).absolute()
        require(path.parent.resolve(strict=True) == path.parent, 'Symlink input ancestry: ' + str(path))
        before = path.lstat()
        require(stat.S_ISREG(before.st_mode), 'Not a regular input: ' + str(path))
        handle = os.fdopen(os.open(path, os.O_RDONLY | getattr(os, 'O_NOFOLLOW', 0)), 'rb')
        require(identity(os.fstat(handle.fileno())) == identity(before), 'Input raced before open')
        return path, handle, before

    def finish(self, path, handle, before, size, digest):
        require(identity(handle_stat := os.fstat(handle.fileno())) == identity(before)
                and identity(path.lstat()) == identity(before), 'Input changed while read: ' + str(path))
        record = {'path': str(path), 'bytes': size, 'sha256': digest, 'identity': list(identity(handle_stat))}
        require(str(path) not in self.files or self.files[str(path)] == record, 'Input differs between reads: ' + str(path))
        self.files[str(path)] = record

    def raw(self, path):
        path, handle, before = self.stream(path)
        with handle:
            raw = handle.read()
            self.finish(path, handle, before, len(raw), hashlib.sha256(raw).hexdigest())
        return raw

    def document(self, path):
        return decode(self.raw(path))

    def hashed(self, path):
        path, handle, before = self.stream(path)
        digest, size = hashlib.sha256(), 0
        with handle:
            for block in iter(lambda: handle.read(1024 * 1024), b''):
                digest.update(block); size += len(block)
            self.finish(path, handle, before, size, digest.hexdigest())
        return digest.hexdigest()

    def lines(self, path):
        path, handle, before = self.stream(path)
        digest, size = hashlib.sha256(), 0
        with handle:
            for number, raw in enumerate(handle, 1):
                digest.update(raw); size += len(raw)
                require(raw.strip(), f'Blank JSONL record: {path}:{number}')
                yield decode(raw)
            self.finish(path, handle, before, size, digest.hexdigest())

    def unchanged(self):
        original = list(self.files.values())
        for row in original:
            self.hashed(Path(row['path']))
        return original


def number(value, label):
    require(type(value) in (int, float) and math.isfinite(value), label + ': nonfinite/non-number')
    return value


def near(actual, expected, label, tolerance=.002):
    require(abs(number(actual, label) - number(expected, label)) <= tolerance,
            f'{label}: {actual!r} != {expected!r}')


def f32(value):
    return struct.unpack('<f', struct.pack('<f', value))[0]


def cost(value):
    require(isinstance(value, dict), 'Cost is not an object')
    return {key: number(value.get(key, 0), 'Cost.' + key) for key in KEYS}


def same_cost(a, b, label, tolerance=.002):
    for key in KEYS:
        near(cost(a)[key], cost(b)[key], label + '.' + key, tolerance)


def vector(value, label):
    require(isinstance(value, dict) and 'x' in value and 'y' in value, label + ': missing Vector2')
    return (number(value['x'], label), number(value['y'], label))


def same_point(a, b, label):
    for x, y in zip(vector(a, label), b):
        near(x, y, label)


def fingerprint(files):
    return hashlib.sha256(json.dumps(files, sort_keys=True, separators=(',', ':')).encode()).hexdigest()


def source_paths(project):
    result = []
    for root in ROOTS:
        for path in sorted((project / root).rglob('*')):
            relative = path.relative_to(project)
            if path.is_file() and not any(part.startswith('.') or part == '__pycache__' for part in relative.parts):
                result.append(relative.as_posix())
    return sorted(result)


def declaration_check(declaration):
    require(declaration.get('status') == 'declared_before_execution', 'Missing prior declaration')
    require(declaration.get('applicationVersion') == '0.15.0', 'Wrong declared version')
    require(declaration.get('simulatedSecondsLimit') == LIMIT and declaration.get('comparisonHorizonSeconds') == HORIZON
            and declaration.get('summaryIntervalSeconds') == INTERVAL and declaration.get('stopAtNaturalTerminal') is True,
            'Changed predeclared horizons/terminal policy')
    rows = declaration.get('cases', [])
    require(len(rows) == 16 and [(x['map'], x['civilization'], x['seed']) for x in rows] == MATRIX,
            'Declaration is not the ordered 16-case matrix')
    require(all(x['player0Route'] == ('revolution' if x['seed'] % 2 == 0 else 'empire') for x in rows), 'Declared route changed')


def source_binding(inputs, runner, project, manifest, declaration):
    before = inputs.document(runner / 'source-before.json')
    after = inputs.document(runner / 'source-after.json')
    receipt = inputs.document(runner / 'runner-receipt.json')
    files = before.get('files', {})
    require(files and files == after.get('files') and before.get('fingerprint') == after.get('fingerprint') == fingerprint(files),
            'Runner source maps/fingerprints differ')
    require(manifest.get('declaredSourceFingerprint') == before['fingerprint'], 'Matrix source fingerprint differs')
    require(receipt.get('operation') == 'routes' and receipt.get('sourceStable') is True
            and receipt.get('allProcessesSucceeded') is True and receipt.get('sourceBefore') == before['fingerprint']
            and receipt.get('sourceAfter') == before['fingerprint'], 'Runner was incomplete/failed or source changed')
    require(len(receipt.get('runs', [])) == 1 and receipt['runs'][0].get('exitCode') == 0, 'Missing successful exited route process')
    current = {relative: inputs.hashed(project / relative) for relative in source_paths(project)}
    require(current == files, 'Current complete runtime input map differs from runner freeze')
    require(declaration.get('observerSha256AtDeclaration') == files.get(OBSERVER),
            'Observer changed since predeclaration: require a repaired/rebound declaration before execution')
    require(files.get(RULES) == inputs.hashed(project / RULES), 'Rules do not belong to frozen source')
    return ({'fingerprint': before['fingerprint'], 'inputs': len(files), 'runnerExit': 0,
             'scope': 'Independent source SHA map reconciliation; no Unity test rerun or binary provenance claim'}, sorted(current))


def alive(entity):
    return not entity['state']['dead'] and entity['state']['hp'] > 0


def worker(definition):
    return bool(definition.get('tags', 0) & 128) and not definition.get('waterborne', False)


def soldier(definition):
    return (definition['kind'] == 0 and not definition.get('tags', 0) & (128 | 512)
            and not definition.get('civilian', False) and not definition.get('waterborne', False))


def scenario_document(envelope):
    require(envelope.get('schema') == 1 and envelope.get('encoding') == 'frontier-scenario-json-v1'
            and isinstance(envelope.get('payload'), str), 'Invalid opening-scenario envelope')
    raw = envelope['payload'].encode('utf-8')
    require(hashlib.sha256(raw).hexdigest() == envelope.get('sha256'), 'Opening scenario original payload SHA differs')
    return decode(raw)


def bootstrap(opening, item, definitions, civilizations, scenario):
    require(opening.get('schema') == 4 and opening.get('seed') == item['seed'] and opening.get('maxAge') == 5,
            'Opening schema/seed/max age mismatch')
    near(opening['time'], 0, 'Opening clock')
    require(opening['winner'] == -1 and opening['paused'] is False and opening['teams'] == [0, 1], 'Non-normal opening state')
    require(len(opening['players']) == 2, 'Opening needs two mirror players')
    require(opening['hasScenarioDocument'] == (item['map'] == 'traders_bend070'), 'Wrong opening map family')
    require(opening['mapId'] == ('custom_scenario' if scenario else 'winding'), 'Wrong world map identity')
    if scenario:
        require(opening['scenarioDocument'] == scenario and scenario['id'] == 'traders_bend070'
                and scenario['seed'] == item['seed'] and scenario['enableElimination'] is True
                and not scenario.get('objectives') and not scenario.get('triggers'), 'Opening scenario is not normal skirmish')
    report = []
    for player in (0, 1):
        state = opening['players'][player]['state']
        civ = civilizations[item['civilization']]
        require(state['id'] == player and state['civilization'] == item['civilization'] and state['age'] == 1
                and state['politicalPhase'] == 0 and not state['eliminated'] and not state['resigned'], 'Non-normal opening player')
        same_cost(state['resources'], civ['startingResources'], 'Faction starting resources')
        for counter in ('spent', 'gathered', 'passiveIncome', 'refunded'):
            same_cost(state[counter], {}, 'Opening counter ' + counter)
        own = [e for e in opening['entities'] if alive(e) and e['state']['owner'] == player]
        workers = [e for e in own if worker(definitions[e['definition']])]
        towns = [e for e in own if e['definition'] == 'town']
        scouts = [e for e in own if e['definition'] == 'explorer']
        require(len(workers) == civ.get('startingWorkers', 6) == 6 and len(towns) == len(scouts) == 1, 'Wrong normal bootstrap actor counts')
        require(all(e['definition'] == civ.get('workerUnit', 'villager') for e in workers), 'Wrong faction worker definition')
        require(all(not e['orders'] and not e['queue'] and e['state']['construction'] == 1 for e in own), 'Preloaded orders/paid queues/unfinished actors')
        require(not [e for e in own if soldier(definitions[e['definition']])], 'Gifted opening army')
        if scenario:
            town, scout = (22, 24), (35, 24)
            positions = [(12, 18), (14, 16), (17, 14), (26, 14), (30, 16), (32, 18)]
        else:
            town, scout = (24, 28), (31, 32)
            positions = [(24 + 5 + i % 3 * 1.3, 28 - 3 + i // 3 * 1.4) for i in range(6)]
        reflect = lambda p: p if player == 0 else (128-p[0], 128-p[1])
        same_point(towns[0]['state']['position'], reflect(town), 'Actual map town bootstrap')
        same_point(scouts[0]['state']['position'], reflect(scout), 'Actual map explorer bootstrap')
        for entity, expected in zip(sorted(workers, key=lambda e: e['state']['id']), positions):
            same_point(entity['state']['position'], reflect(expected), 'Actual map worker bootstrap')
        if scenario:
            start = scenario['starts'][player]
            require(start['age'] == 1 and start['civilization'] == item['civilization'] and len(start['workers']) == 6, 'Scenario faction opening differs')
            same_cost(start['initialResources'], state['resources'], 'Scenario initial resources')
        report.append({'player': player, 'workers': 6, 'resources': cost(state['resources']),
                       'town': towns[0]['state']['position'], 'workerPositions': [e['state']['position'] for e in workers]})
    near(item['startingWorkers'], 6, 'Result starting workers')
    same_cost(item['startingResources'], civilizations[item['civilization']]['startingResources'], 'Result starting resources')
    require(not [e for e in opening['events'] if e['kind'] in ('Payment', 'RevolutionCompleted', 'CivilianRebuilt')], 'Intervened bootstrap ledger')
    return report


def stage_evidence(events, item, rules, entities, definitions):
    own = [e for e in events if e['player'] == 0]
    payments = [e for e in own if e['kind'] == 'Payment']
    revolution = next(r for r in rules['revolutions'] if r['civilization'] == item['civilization'])
    government = next(t for t in rules['techs'] if t['id'] == 'civilian_rebuilding')
    age = next(a for a in rules['ages'] if a['age'] == 5)
    stages = {}
    for field, prefix, completion, target_cost, duration in (
            ('empirePaidAt', 'Advance to age 5:', 'AgeAdvanced', age['cost'], age['time']),
            ('revolutionPaidAt', revolution['name'] + ':', 'RevolutionCompleted', revolution['cost'], revolution['time']),
            ('governmentPaidAt', government['name'] + ':', 'CivilianRebuilt', government['cost'], government['time'])):
        paid = [e for e in payments if e['message'].startswith(prefix)]
        complete = [e for e in own if e['kind'] == completion and (completion != 'AgeAdvanced' or e['message'] == 'Reached age 5')]
        for event in paid:
            require(event.get('hasEconomy') is True, 'Political payment lacks actual economy transaction')
            require(event['entity'] in entities and entities[event['entity']]['definition'] == 'town', 'Political payment has no actual town producer')
            same_cost(event['economy']['requestedDelta'], {k: -v for k, v in cost(target_cost).items()}, 'Political cost')
        if item[field] >= 0:
            require(any(-.002 <= item[field] - event['time'] <= .112 for event in paid), field + ' is not a paid queue boundary')
        if complete:
            require(len(complete) == 1 and item[field] >= 0, completion + ' has no observed unfinished queue')
            require(any(event['entity'] == complete[0]['entity'] and complete[0]['time']-event['time'] >= duration-.3
                        for event in paid), completion + ' has no preceding real producer payment/duration')
        stages[field] = {'payments': len(paid), 'firstPaymentAt': paid[0]['time'] if paid else None,
                         'observedQueueAt': item[field], 'completedAt': complete[0]['time'] if complete else None}
    advanced = stages['empirePaidAt']['completedAt']
    if item['empireCompletedAt'] >= 0:
        require(advanced is not None, 'Empire completion has no actual event')
        near(item['empireCompletedAt'], advanced, 'Empire completion clock')
    else:
        require(advanced is None, 'Unreported actual empire completion')
    for field, stage in (('revolutionAt', 'revolutionPaidAt'), ('rebuiltAt', 'governmentPaidAt')):
        complete = stages[stage]['completedAt']
        near(item[field], complete if complete is not None else 0, field)
    rebuilt = stages['governmentPaidAt']['completedAt']
    revolution_completed = stages['revolutionPaidAt']['completedAt']
    expected_phase = 2 if rebuilt is not None else 1 if revolution_completed is not None else 0
    require(item['phase'] == expected_phase, 'Final political phase conflicts with completion events')
    if expected_phase:
        require(item['age'] == 4 and advanced is None, 'Revolution incorrectly reached empire age V')
    if rebuilt is not None:
        require(revolution_completed is not None and rebuilt >= revolution_completed, 'Civilian government completed without revolution')
    first_worker = None
    if rebuilt is not None:
        births = [e for e in own if e['kind'] == 'Spawn' and e['time'] >= rebuilt
                  and e['entity'] in entities and worker(definitions[entities[e['entity']]['definition']])]
        first_worker = min((e['time'] for e in births), default=None)
    near(item['firstCivilianRecoveredAt'], first_worker if first_worker is not None else -1, 'First restored civilian')
    natural = dict.fromkeys(KEYS, 0.0)
    if rebuilt is not None:
        for event in own:
            actor, target = entities.get(event['entity']), entities.get(event['target'])
            if (event['kind'] != 'Gather' or event['time'] <= rebuilt or event['amount'] <= 0 or not actor or not target
                    or not worker(definitions[actor['definition']]) or target['definition'].startswith('crate_')):
                continue
            resource = definitions[target['definition']].get('resource', 0)
            require(resource in (1, 2, 3), 'Natural income has no recognized resource')
            key = KEYS[resource - 1]; natural[key] = f32(natural[key] + event['amount'])
    same_cost(item['naturalIncomeAfterRebuilding'], natural, 'Independent natural-income sum', .02)
    recovered = rebuilt is not None and first_worker is not None and sum(natural.values()) > 0
    require(item['civilianSourcesRestored'] == (rebuilt is not None) and item['civilianEconomyRecovered'] == recovered,
            'Government restoration was confused with civilian/natural-income recovery')
    return stages, recovered, natural


def case_audit(inputs, matrix, item, manifest, rules):
    directory = matrix / item['id']
    result = inputs.document(directory / 'result.json')
    require(result == item, 'Manifest case and result.json disagree: ' + item['id'])
    require(result['status'] not in ('planned', 'running', 'exception') and result['error'] == '', 'Case observation incomplete/errored')
    opening = inputs.document(directory / 'opening-world.json')
    final = inputs.document(directory / 'final-world.json')
    simulation = inputs.document(directory / 'final-simulation.json')
    definitions = {e['id']: e for e in rules['entities']}
    civilizations = {c['id']: c for c in rules['civs']}
    scenario = None
    if item['map'] == 'traders_bend070':
        envelope = inputs.document(directory / 'opening-scenario.json')
        scenario = scenario_document(envelope)
        require(opening['scenarioHash'] == envelope['sha256'], 'Opening world scenarioHash differs from original envelope bytes')
    opening_report = bootstrap(opening, item, definitions, civilizations, scenario)
    require(final['rulesHash'] == opening['rulesHash'] == manifest['rulesHash'] and final['seed'] == opening['seed']
            and final['mapId'] == opening['mapId'] and final['maxAge'] == 5 and final['teams'] == [0, 1], 'World identity drift')
    require(len(final['players']) == 2 and all(final['players'][player]['state']['id'] == player
            and final['players'][player]['state']['civilization'] == item['civilization']
            and final['players'][player]['state']['team'] == player for player in (0, 1)), 'Final faction/slot/team drift')
    require(final['scenarioHash'] == opening['scenarioHash'] and final['scenarioDocument'] == opening['scenarioDocument'], 'Scenario identity drift')
    near(final['time'], item['seconds'], 'Final time')
    require(final['winner'] == item['winner'] and item['naturalTerminal'] == (final['winner'] != -1), 'False natural terminal')
    require(final['paused'] is False and (item['naturalTerminal'] or LIMIT <= final['time'] < LIMIT+.112), 'Stopped early without natural terminal')
    require(0 <= final['time'] < LIMIT+.112, 'Extended predeclared horizon')
    require(simulation['difficultyId'] == 'standard' and [c['player'] for c in simulation['controllers']] == [1, 0], 'Wrong two-AI scheduler')
    require(type(simulation['stepCount']) is int and 1 <= simulation['stepCount'] <= 40000, 'Invalid step count')
    clock, next_sample, scheduled_times = 0.0, 0.0, []
    for _ in range(simulation['stepCount']):
        clock = f32(clock + f32(.1))
        if clock >= next_sample:
            scheduled_times.append(clock); next_sample = f32(clock+INTERVAL)
    near(final['time'], clock, 'Fixed-step clock')
    scheduled_times.append(final['time'])
    events = final['events']
    previous, streamed = -1, 0
    for streamed, event in enumerate(inputs.lines(directory / 'events.jsonl'), 1):
        require(streamed <= len(events) and event == events[streamed-1], 'Raw event stream differs from complete world ledger')
        require(event['schemaVersion'] == 2 and previous <= event['time'] <= final['time'], 'Event schema/order/clock invalid')
        previous = event['time']
        if event['kind'] in ('Payment', 'Refund'):
            require(event.get('hasEconomy') is True, 'Payment/refund lacks ledger')
            tx = event['economy']
            for key in KEYS:
                near(cost(tx['actualDelta'])[key], f32(cost(tx['balanceAfter'])[key]-cost(tx['balanceBefore'])[key]), 'Actual transaction delta', .01)
                expected = f32(cost(tx['balanceBefore'])[key]+cost(tx['requestedDelta'])[key])
                near(cost(tx['balanceAfter'])[key], expected, 'Actual paid/refunded balance', .02)
    require(streamed == len(events) and events[:len(opening['events'])] == opening['events'], 'Omitted opening/final raw events')
    require(len([e for e in events if e['kind'] == 'MatchEnded']) == int(item['naturalTerminal']), 'Natural terminal has no unique event')
    if item['naturalTerminal']:
        require(final['winner'] in (-2, 0, 1) and any(e['kind'] == 'MatchEnded' and e['player'] == final['winner'] for e in events), 'Terminal winner differs from actual event')
        for player in (0, 1):
            require(final['players'][player]['state']['eliminated'] == (final['winner'] == -2 or player != final['winner']), 'Natural elimination flags differ')
    entities = {e['state']['id']: e for e in final['entities']}
    require(len(entities) == len(final['entities']), 'Duplicate final entity id')
    p = final['players'][0]['state']
    require(item['age'] == p['age'] and item['phase'] == p['politicalPhase'], 'Result age/phase differs')
    near(item['revolutionAt'], p['revolutionAt'], 'Final revolution timestamp')
    near(item['rebuiltAt'], p['civilianRebuiltAt'], 'Final rebuilt timestamp')
    same_cost(item['spent'], p['spent'], 'Result actual spending')
    same_cost(item['income'], {k: f32(cost(p['gathered'])[k]+cost(p['passiveIncome'])[k]) for k in KEYS}, 'Result cumulative income', .02)
    stages, recovered, natural = stage_evidence(events, item, rules, entities, definitions)
    revolution = next(r for r in rules['revolutions'] if r['civilization'] == item['civilization'])
    require(p['revolutionId'] == (revolution['id'] if p['politicalPhase'] else ''), 'Final political identity differs')
    require(('civilian_rebuilding' in final['players'][0]['technologies']) == (p['politicalPhase'] == 2), 'Final government research receipt differs')
    reached = p['age'] == 5 if item['requestedRoute'] == 'empire' else recovered
    require(item['routeReached'] == reached, 'False route attainment')
    status = ('route_reached' if reached else 'government_rebuilt_economy_not_recovered' if item['civilianSourcesRestored']
              else 'ended_before_route' if item['naturalTerminal'] else 'route_not_reached_at_declared_limit')
    require(item['status'] == status, 'Result status hides route/terminal failure')
    comparison_reached = final['time'] >= HORIZON
    require(item['comparisonReached'] == comparison_reached, 'False 2400 reached flag')
    name = 'comparison-2400-world.json' if comparison_reached else 'early-terminal-world.json'
    require(not (directory / ('early-terminal-world.json' if comparison_reached else 'comparison-2400-world.json')).exists(), 'Mixed matched/early-terminal projections')
    projection = inputs.document(directory / name)
    near(item['comparisonAt'], projection['time'], 'Comparison time')
    require(not projection['events'] and projection['rulesHash'] == final['rulesHash'] and projection['seed'] == item['seed'], 'Comparison projection identity/ledger differs')
    expected_comparison = 'matched_horizon_reached' if comparison_reached else 'natural_terminal_before_matched_horizon'
    require(item['comparisonStatus'] == expected_comparison and (HORIZON <= projection['time'] < HORIZON+.112 if comparison_reached
            else item['naturalTerminal'] and projection['time'] == final['time']), 'Early terminal misrepresented as matched horizon')
    require(item['ageAtComparison'] == projection['players'][0]['state']['age']
            and item['phaseAtComparison'] == projection['players'][0]['state']['politicalPhase'], 'Comparison age/phase differs')
    sample_count, ordered, moving, last_pair = 0, 0, 0, None
    pending, previous_army, phase_rows, pair_index = [], {}, Counter(), 0
    actual_payments = [event for event in events if event['kind'] == 'Payment']
    birth_times = {e['state']['id']: 0.0 for e in opening['entities']}
    death_times, converted_times, born_definitions = {}, {}, {}
    names = {definition['name']: definition['id'] for definition in definitions.values()}
    for event in events:
        if event['kind'] == 'Spawn':
            birth_times.setdefault(event['entity'], event['time'])
            if event['message'] in names:
                born_definitions[event['entity']] = names[event['message']]
        elif event['kind'] == 'Death':
            death_times.setdefault(event['entity'], event['time'])
        elif event['kind'] == 'WorkerConverted':
            converted_times[event['entity']] = event['time']
    def existed_at(entity, time):
        require(entity in birth_times and birth_times[entity] <= time+.002, 'Sampled actor did not yet exist')
        # AI can disband at the previous completed world clock before the next
        # tick. Equal-clock rows alone cannot identify that substep boundary.
        require(entity not in death_times or death_times[entity] >= time-.002, 'Sampled actor had already died')
    for sample in inputs.lines(directory / 'samples.jsonl'):
        sample_count += 1; pending.append(sample)
        require(len(pending) <= 2 and sample['player'] == len(pending)-1, 'Samples must be paired players0/1')
        require(0 <= sample['time'] <= final['time'] and sample['soldiers'] == len(sample['army']), 'Sample clock/army count differs')
        require(sample['simulation']['difficultyId'] == 'standard' and [c['player'] for c in sample['simulation']['controllers']] == [1, 0], 'Sample changed scheduler')
        phase_rows[(sample['player'], sample['phase'])] += len(sample['army'])
        require(len({a['id'] for a in sample['army']}) == len(sample['army']), 'Duplicate sampled soldier')
        for actor in sample['army']:
            require(actor['id'] in entities and actor['definition'] in definitions and soldier(definitions[actor['definition']]), 'Unrecognized actual army actor')
            existed_at(actor['id'], sample['time'])
            if definitions[actor['definition']].get('revolutionary', False) and born_definitions.get(actor['id']) != actor['definition']:
                require(actor['id'] in converted_times and converted_times[actor['id']] <= sample['time']+.002, 'Sampled revolutionary predates actual conversion')
            point = vector(actor['position'], 'Army position'); require(actor['hp'] > 0, 'Dead sampled army actor')
            require(isinstance(actor['orders'], list) and isinstance(actor['path'], list), 'Army orders/path omitted')
            ordered += bool(actor['orders'])
            for order in actor['orders']:
                require(type(order['kind']) is int and 0 <= order['kind'] <= 15 and type(order['target']) is int, 'Invalid army order')
                vector(order['point'], 'Army requested point')
            for point_path in actor['path']:
                vector(point_path, 'Army path point')
            if actor['id'] in previous_army:
                moving += math.dist(previous_army[actor['id']], point) > .002
            previous_army[actor['id']] = point
        for building in sample['buildings']:
            require(building['id'] in entities and isinstance(building['queue'], list), 'Sample building/paid queue missing')
            existed_at(building['id'], sample['time'])
            vector(building['position'], 'Building position'); vector(building['rally'], 'Building rally')
            for job in building['queue']:
                require(job['kind'] in ('Age', 'Revolution', 'Research', 'Train', 'Gift', 'Manufacture'), 'Unknown paid queue kind')
                require(0 <= job['elapsed'] <= job['duration']+.002, 'Queue is not unfinished production')
                paid_cost = cost(job['paid'])
                require(all(amount >= 0 for amount in paid_cost.values()), 'Negative paid queue cost')
                if job['kind'] != 'Gift' and any(paid_cost.values()):
                    require(any(event['player'] == sample['player'] and event['entity'] == building['id']
                                and event['time'] <= sample['time']+.002
                                and all(abs(cost(event['economy']['requestedDelta'])[key]+paid_cost[key]) <= .002 for key in KEYS)
                                for event in actual_payments), 'Sampled paid queue has no actual producer payment')
        if len(pending) == 2:
            require(pending[0]['time'] == pending[1]['time'] and pending[0]['simulation'] == pending[1]['simulation'], 'Unpaired sample boundary')
            require(pair_index < len(scheduled_times), 'Unexpected extra sample pair')
            near(pending[0]['time'], scheduled_times[pair_index], 'Declared 30s sample schedule')
            pair_index += 1
            if last_pair:
                require(pending[0]['time'] >= last_pair[0]['time'], 'Sample time moved backwards')
            last_pair = pending; pending = []
    require(not pending and last_pair and sample_count >= 4 and pair_index == len(scheduled_times), 'Missing complete declared sample pairs')
    for player, sample in enumerate(last_pair):
        state = final['players'][player]['state']
        near(sample['time'], final['time'], 'Final sample time')
        require(sample['age'] == state['age'] and sample['phase'] == state['politicalPhase'] and sample['simulation'] == simulation, 'Final sample phase/scheduler differs')
        for field, source in (('resources', 'resources'), ('spent', 'spent'), ('gathered', 'gathered'), ('passive', 'passiveIncome')):
            same_cost(sample[field], state[source], 'Final sample ' + field)
        expected_army = {e['state']['id']: e for e in final['entities'] if alive(e) and e['state']['owner'] == player and soldier(definitions[e['definition']])}
        require({a['id'] for a in sample['army']} == set(expected_army), 'Final army sample omits/adds actors')
        for actor in sample['army']:
            entity = expected_army[actor['id']]
            require(actor['definition'] == entity['definition'] and actor['orders'] == entity['orders'] and actor['path'] == entity['path'], 'Final actual army orders differ')
            same_point(actor['position'], vector(entity['state']['position'], 'Final actor position'), 'Final army position')
        expected_buildings = {e['state']['id']: e for e in final['entities'] if alive(e) and e['state']['owner'] == player and definitions[e['definition']]['kind'] == 1}
        require({b['id'] for b in sample['buildings']} == set(expected_buildings), 'Final sampled building set differs')
        for building in sample['buildings']:
            require(building['queue'] == expected_buildings[building['id']]['queue'], 'Final actual paid queues differ')
    return {'id': item['id'], 'status': status, 'routeReached': reached, 'seconds': item['seconds'], 'naturalTerminal': item['naturalTerminal'],
            'opening': opening_report, 'comparison': {'reached': comparison_reached, 'at': projection['time'], 'status': expected_comparison},
            'paymentQueueCompletionStages': stages, 'naturalIncomeAfterRebuilding': natural, 'events': len(events), 'sampleRows': sample_count,
            'army': {'actorRowsByPlayerAndPhase': {str(k): v for k, v in phase_rows.items()}, 'orderedActorRows': ordered,
                     'positionChangedIntervals': moving, 'scope': 'Stored positions/orders/path/queues and final DTO consistency; no tactical or balance verdict'}}


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--prepare', action='store_true')
    parser.add_argument('--matrix', type=Path)
    parser.add_argument('--runner', type=Path)
    parser.add_argument('--project', type=Path, default=PROJECT)
    parser.add_argument('--declaration', type=Path, default=HERE / 'predeclared-observation072.json')
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    require(args.prepare != bool(args.matrix), 'Choose --prepare or --matrix')
    inputs, report = Inputs(), {'schema': 1, 'generatedUtc': datetime.now(timezone.utc).isoformat(), 'result': 'Incomplete'}
    code = 2
    try:
        inputs.hashed(Path(__file__).absolute())
        declaration = inputs.document(args.declaration); declaration_check(declaration)
        rules = inputs.document(args.project / RULES)
        if args.prepare:
            observer_hash = inputs.hashed(args.project / OBSERVER)
            report.update(result='Prepared', matrixExecuted=False, declaredCases=16,
                          observerMatchesPredeclaration=observer_hash == declaration.get('observerSha256AtDeclaration'),
                          observerSha256Current=observer_hash, observerSha256AtDeclaration=declaration.get('observerSha256AtDeclaration'))
        else:
            matrix = args.matrix.absolute(); require(matrix.resolve(strict=True) == matrix and matrix.is_dir(), 'Matrix directory not real')
            before_set = sorted(p.relative_to(matrix).as_posix() for p in matrix.rglob('*') if p.is_file())
            manifest = inputs.document(matrix / 'manifest.json')
            require(manifest['appVersion'] == '0.15.0' and manifest['declaredLimitSeconds'] == LIMIT and manifest['comparisonSeconds'] == HORIZON
                    and manifest['sampleIntervalSeconds'] == INTERVAL, 'Changed matrix version/horizons')
            cases = manifest['cases']
            require(len(cases) == 16 and [(c['map'], c['civilization'], c['seed']) for c in cases] == MATRIX, 'Changed/omitted/duplicated matrix cases')
            for item in cases:
                require(item['id'] == f"{item['map']}-{item['civilization']}-{item['seed']}" and item['requestedRoute'] == ('revolution' if item['seed'] % 2 == 0 else 'empire'), 'Case identity/route drift')
            report['sourceBinding'], bound_paths = source_binding(inputs, (args.runner or matrix.parent).absolute(), args.project.absolute(), manifest, declaration)
            audited = []
            for item in cases:
                audited.append(case_audit(inputs, matrix, item, manifest, rules))
                print(json.dumps({'caseAudited': item['id'], 'routeReached': audited[-1]['routeReached']}), file=sys.stderr, flush=True)
            expected_status = 'all_observed_routes_reached' if all(c['routeReached'] for c in cases) else 'observed_routes_incomplete'
            require(manifest['status'] == expected_status and manifest.get('finishedUtc'), 'Manifest completion/route summary differs')
            for relative in before_set:
                inputs.hashed(matrix / relative)
            require(sorted(p.relative_to(matrix).as_posix() for p in matrix.rglob('*') if p.is_file()) == before_set, 'Matrix file set changed during audit')
            report.update(result='Consistent', matrixExecuted=True, cases=audited,
                          observedRoutesReached=sum(c['routeReached'] for c in audited), observedRoutesTotal=16,
                          observationStatus=expected_status, balanceVerdict='Not assessed', old071EvidenceRead=False)
        report['filesUnchangedDuringAudit'] = inputs.unchanged()
        if not args.prepare:
            require(source_paths(args.project) == bound_paths, 'Complete runtime input file set changed during audit')
            require(sorted(p.relative_to(matrix).as_posix() for p in matrix.rglob('*') if p.is_file()) == before_set, 'Matrix file set changed after final hashes')
        code = 0
    except (OSError, ValueError, KeyError, TypeError, StopIteration) as error:
        report.update(result='Incomplete' if isinstance(error, FileNotFoundError) else 'Failed', errorType=type(error).__name__, error=str(error))
    report['scope'] = ['Independent Python standard-library artifact/source-map reconciliation; imports no observer implementation.',
                       'Prepared is not an executed matrix. Consistent does not mean every route reached, victory, gameplay quality or balanced civilizations.',
                       'Bootstrap uses different source-backed Traders Bend and Winding coordinates; never substitutes identical worker coordinates for both maps.',
                       'Payment, observed unfinished queue, completion, restored civilian and positive land-worker non-crate income remain separate stages.',
                       'Samples are sparse; movement counts describe recorded intervals, not every intervening order or continuous trajectory.',
                       'No Unity rerun, independently recreated simulation, pixels, native input, audio, two-host network, external account or causal 071 comparison proof.',
                       'Unity rulesHash is reconciled across artifacts; frozen raw Rules.json and all current runtime input SHA values are independently checked in matrix mode.']
    if args.output:
        output = args.output.absolute()
        require(output.parent == HERE and output.parent.resolve(strict=True) == HERE, 'Receipt must be in this new072 Diagnostics directory')
        raw = (json.dumps(report, ensure_ascii=False, sort_keys=True, indent=2, allow_nan=False)+'\n').encode()
        with os.fdopen(os.open(output, os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, 'O_NOFOLLOW', 0), 0o644), 'wb') as handle:
            handle.write(raw); handle.flush(); os.fsync(handle.fileno())
        print(json.dumps({'result': report['result'], 'receipt': str(output), 'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest()}))
    else:
        print(json.dumps(report, ensure_ascii=False, indent=2, allow_nan=False))
    return code


if __name__ == '__main__':
    sys.exit(main())
