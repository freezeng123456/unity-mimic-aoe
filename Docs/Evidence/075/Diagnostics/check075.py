#!/usr/bin/env python3
"""Read-only, bounded-scope closure audit; never launches Unity or changes evidence.

Exit 0: requested machine-checkable scopes closed; 2: any pending; 1: discrepancy.
Native F9 identity is not native interaction acceptance. Output paths are exclusive.
"""
import argparse
from collections import Counter, defaultdict, deque
from datetime import datetime, timezone
import hashlib
import json
import math
import os
from pathlib import Path
import plistlib
import re
import struct
import sys
import xml.etree.ElementTree as ET

DEFAULT_REPO = Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
BASE = Path(__file__).resolve().parents[1]
COST = ('food', 'wood', 'coin')
SCOUTS = ('envoy', 'native_scout')
CASES = {'river-bend-dutch42': ('river', 'traders_bend070', 42),
         'winding-french44': ('france', 'winding', 44)}


def sha(path):
    h = hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda: f.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def reference(path):
    return {'path': str(path), 'bytes': path.stat().st_size, 'sha256': sha(path)}


def parse_json(value):
    def duplicate(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError('Duplicate JSON property: ' + key)
            result[key] = value
        return result
    return json.loads(value, object_pairs_hook=duplicate,
                      parse_constant=lambda value: (_ for _ in ()).throw(ValueError('Nonfinite JSON: ' + value)))


def load(path):
    return parse_json(path.read_text(encoding='utf-8'))


def fingerprint(files):
    return hashlib.sha256(json.dumps(files, sort_keys=True, separators=(',', ':')).encode()).hexdigest()


def f32(value):
    return struct.unpack('<f', struct.pack('<f', value))[0]


def same_float(a, b):
    return f32(a) == f32(b)


def cost(value):
    return {k: float(value.get(k, 0)) for k in COST}


def costs_equal(a, b):
    return all(same_float(a.get(k, 0), b.get(k, 0)) for k in COST)


class Pending(Exception):
    pass


class EvidenceFailure(ValueError):
    def __init__(self, reason, detail):
        super().__init__(reason)
        self.detail = detail


class Audit:
    def __init__(self, args):
        self.args = args
        self.parts = {}
        self.expected = args.expected_fingerprint
        self.version = ''
        self.guid = ''
        self.rules = None

    def require(self, condition, reason):
        if not condition:
            raise ValueError(reason)

    def file(self, path):
        if not path.is_file():
            raise Pending('Missing: ' + str(path))
        return path

    def section(self, name, fn):
        try:
            detail = fn()
            self.parts[name] = {'status': 'PASS', **detail}
        except EvidenceFailure as error:
            self.parts[name] = {'status': 'FAIL', 'reason': str(error), **error.detail}
        except Pending as error:
            self.parts[name] = {'status': 'PENDING', 'reason': str(error)}
        except (ValueError, KeyError, TypeError, OSError, ET.ParseError, AssertionError) as error:
            self.parts[name] = {'status': 'FAIL', 'reason': str(error)}

    def sources(self):
        files = {}
        for directory in ('Assets', 'Packages', 'ProjectSettings', 'Tools'):
            for root, dirs, names in os.walk(self.args.repo / directory):
                dirs[:] = sorted(d for d in dirs if not d.startswith('.') and d != '__pycache__')
                for name in sorted(names):
                    if name.startswith('.'):
                        continue
                    path = Path(root) / name
                    files[path.relative_to(self.args.repo).as_posix()] = sha(path)
        current = fingerprint(files)
        if self.expected:
            self.require(current == self.expected, 'Current source fingerprint differs from --expected-fingerprint')
        self.expected = current
        settings = (self.args.repo / 'ProjectSettings/ProjectSettings.asset').read_text()
        version = re.search(r'^  bundleVersion: (.+)$', settings, re.M)
        self.require(version is not None, 'Source bundleVersion is missing')
        self.version = version.group(1)
        self.rules = load(self.args.repo / 'Assets/Resources/Rules.json')
        self.require(self.version == '0.18.0', '075 checker expects the declared 0.18.0 source version')
        return {'sourceFingerprint': current, 'files': len(files), 'version': self.version,
                'rulesFile': reference(self.args.repo / 'Assets/Resources/Rules.json'),
                'scope': 'One source-tree enumeration; raw Rules.json SHA is not Unity RulesHash.'}

    def runner(self, path, kind):
        receipt_path = self.file(path / 'runner-receipt075.json')
        r = load(receipt_path)
        if r.get('status') == 'running':
            raise Pending('Runner is still running; live partial outputs were not audited: ' + str(path))
        self.require(r.get('kind') == kind and r.get('status') == 'closed', 'Wrong/unclosed runner kind/status')
        self.require(r.get('exitCode') == 0 and r.get('sourceStable') is True, 'Runner nonzero actual exit or unstable source')
        self.require(r.get('changedSources') == [], 'Runner reports changed source files')
        fp = r['sourceFingerprintBefore']
        self.require(fp == r['sourceFingerprintAfter'] == fingerprint(r['sourcesBefore']), 'Runner source map/fingerprint inconsistency')
        self.require(fp == self.expected, 'Runner belongs to a different candidate fingerprint: ' + str(path))
        self.require(r.get('finishedUtc') and r.get('startedUtc'), 'Missing runner closure timestamps')
        commands = r.get('commands', [])
        self.require(len(commands) == (2 if kind == 'focused' else 1), 'Unexpected runner command count')
        self.require(all(c.get('status') == 'closed' and c.get('exitCode') == 0 and c.get('pid', 0) > 0 and c.get('finishedUtc') for c in commands), 'A child process lacks actual successful closure')
        return r, {'runner': reference(receipt_path), 'sourceFingerprint': fp,
                   'actualExitCodes': [c['exitCode'] for c in commands], 'gitHeadBefore': r['gitHeadBefore']}

    def tests(self, path, kind):
        raw_path = self.file(path / 'runner-receipt075.json')
        raw = load(raw_path)
        if raw.get('status') == 'closed' and raw.get('exitCode') != 0:
            # The runner only sets reportsDirectory on full success. Preserve its
            # original receipt and recover the announced real test directory.
            reports = Path(raw.get('reportsDirectory', path))
            if kind == 'full' and not raw.get('reportsDirectory'):
                stdout = path / 'stdout-0.log'
                if stdout.is_file():
                    with stdout.open(errors='strict') as stream:
                        for line in stream:
                            found = re.match(r'^This run writes fresh results to: (.+)\n?$', line)
                            if found:
                                reports = Path(found.group(1).strip())
                                break
            detail = {'runner': reference(raw_path), 'actualExitCode': raw.get('exitCode'),
                      'sourceStableRecorded': raw.get('sourceStable'), 'sourceFingerprint': raw.get('sourceFingerprintAfter'),
                      'sameSourceAsCurrent': raw.get('sourceFingerprintAfter') == self.expected, 'tests': {}}
            for mode in ('EditMode', 'PlayMode'):
                xml_path = reports / (mode + '.xml')
                if xml_path.is_file():
                    xml = ET.parse(xml_path).getroot()
                    detail['tests'][mode] = {**reference(xml_path), 'result': xml.attrib.get('result'),
                        'total': int(xml.attrib.get('total', 0)), 'passed': int(xml.attrib.get('passed', 0)),
                        'failed': int(xml.attrib.get('failed', 0)),
                        'failedCases': [c.attrib.get('fullname') for c in xml.findall('.//test-case') if c.attrib.get('result') == 'Failed']}
                else:
                    detail['tests'][mode] = {'status': 'NOT_PRODUCED', 'path': str(xml_path)}
            raise EvidenceFailure('Runner actually exited nonzero; available XML is diagnostic, not a passing closure', detail)
        r, result = self.runner(path, kind)
        reports = Path(r.get('reportsDirectory', path))
        evidence_path = self.file(reports / 'source-evidence.json')
        e = load(evidence_path)
        self.require(e.get('schema') == 1 and e['sourceFingerprint'] == self.expected == fingerprint(e['files']), 'Test source evidence identity mismatch')
        self.require(e['files'] == r['sourcesBefore'], 'Runner/test source maps differ')
        result['sourceEvidence'] = reference(evidence_path)
        result['tests'] = {}
        for mode in ('EditMode', 'PlayMode'):
            p = self.file(reports / (mode + '.xml'))
            xml = ET.parse(p).getroot()
            cases = xml.findall('.//test-case')
            total = int(xml.attrib['total'])
            self.require(xml.tag == 'test-run' and xml.attrib['result'] == 'Passed' and total > 0,
                         mode + ' does not contain a complete passing run')
            self.require(int(xml.attrib['passed']) == total == len(cases) and int(xml.attrib['failed']) == 0 and
                         all(c.attrib.get('result') == 'Passed' for c in cases), mode + ' leaf outcomes/counts disagree')
            self.require(r['tests'][mode] == xml.attrib, mode + ' runner XML attributes differ')
            er = e['reports'][mode]
            self.require(er['file'] == p.name and er['sha256'] == sha(p) and er['passed'] == total, mode + ' source evidence hash/count differs')
            result['tests'][mode] = {**reference(p), 'passed': total, 'durationSeconds': float(xml.attrib['duration'])}
        result['totalPassed'] = sum(v['passed'] for v in result['tests'].values())
        return result

    def build(self):
        path = self.args.build
        _, result = self.runner(path, 'build')
        log = self.file(path / 'build.log')
        lines = log.read_text(errors='strict').splitlines()
        succeeded = [(i + 1, line) for i, line in enumerate(lines) if re.match(r'^FRONTIER_BUILD Succeeded bytes=\d+ path=', line)]
        self.require(len(succeeded) == 1, 'Build log lacks exactly one actual FRONTIER_BUILD Succeeded record')
        app = path / 'Frontier Reckoning.app'
        boot = self.file(app / 'Contents/Resources/Data/boot.config')
        matches = re.findall(r'^build-guid=([0-9a-f]{32})$', boot.read_text(), re.M)
        self.require(len(matches) == 1, 'Actual app boot.config lacks one build GUID')
        self.guid = matches[0]
        plist_path = self.file(app / 'Contents/Info.plist')
        info = plistlib.loads(plist_path.read_bytes())
        self.require(info['CFBundleShortVersionString'] == self.version, 'Built app version differs from tested source')
        exe = self.file(app / 'Contents/MacOS' / info['CFBundleExecutable'])
        with exe.open('rb') as f:
            header = f.read(8)
        self.require(header[:4] == b'\xcf\xfa\xed\xfe' and struct.unpack('<I', header[4:])[0] == 0x100000c, 'Client is not a thin arm64 Mach-O')
        result.update(buildGuid=self.guid, appVersion=info['CFBundleShortVersionString'],
                      buildLog=reference(log), succeededLine=succeeded[0][0],
                      bootConfig=reference(boot), infoPlist=reference(plist_path), executable=reference(exe),
                      scope='Build success/arm64/version/GUID only, not native gameplay acceptance.')
        return result

    def observation(self):
        _, result = self.runner(self.args.observations, 'normal')
        observed = self.args.observations / 'Observed'
        manifest_path = self.file(observed / 'manifest.json')
        m = load(manifest_path)
        self.require(m.get('schema') == 'scouting075-v1' and m.get('status') == 'closed_two_declared_ordinary_cases', 'Observation manifest not fully closed')
        self.require(m['sourceFingerprint'] == self.expected and m['version'] == self.version, 'Observation source/version differs')
        self.require(m['cap'] == 2400 and m['sampleInterval'] == 30, 'Declared observation cap/sample interval changed')
        self.require({c['id'] for c in m['cases']} == set(CASES) and len(m['cases']) == 2, 'Observation case set changed')
        result['manifest'] = reference(manifest_path)
        result['cases'] = [self.case(observed / c['id'], c, m) for c in m['cases']]
        result['scope'] = 'Two declared ordinary AI openings and recorded paid behavior; no balance, whole-map exploration, native FPS, or cold-restore claim.'
        return result

    def case(self, directory, summary, manifest):
        identity = CASES[summary['id']]
        self.require((summary['civilization'], summary['map'], summary['seed']) == identity, 'Declared case identity changed')
        required = ('opening-world.json', 'opening-simulation.json', 'final-world.json', 'final-simulation.json', 'events.jsonl', 'samples.jsonl')
        files = {name: reference(self.file(directory / name)) for name in required}
        opening = load(directory / 'opening-world.json')
        final = load(directory / 'final-world.json')
        initial_sim = load(directory / 'opening-simulation.json')
        simulation = load(directory / 'final-simulation.json')
        civ = next(c for c in self.rules['civs'] if c['id'] == identity[0])
        wallet = civ.get('startingResources', dict(food=300, wood=300, coin=100))
        definitions = {d['id']: d for d in self.rules['entities']}
        self.require(opening['time'] == 0 and opening['winner'] == -1 and not opening['paused'], 'Opening is not an active time-zero world')
        self.require(opening['seed'] == final['seed'] == identity[2] and opening['rulesHash'] == final['rulesHash'] == manifest['rulesHash'], 'World identity/rules hash differs')
        self.require(opening['teams'] == final['teams'] == [0, 1], 'Not the declared two independent players')
        for p in opening['players']:
            self.require(p['state']['age'] == 1 and p['state']['civilization'] == identity[0] and costs_equal(p['state']['resources'], wallet), 'Opening age/civilization/default wallet mismatch')
            self.require(all(costs_equal(p['state'][name], {}) for name in ('gathered', 'spent', 'refunded', 'passiveIncome', 'tradeIncome', 'marketIncome')), 'Opening already contains economic activity')
        self.require(not any(e['definition'] in SCOUTS for e in opening['entities']), 'New scouts injected into opening')
        for s in (initial_sim, simulation):
            self.require(s['difficultyId'] == 'standard' and [a['player'] for a in s['controllers']] == [1, 0], 'Not ordinary standard both-AI simulation')
        self.require(initial_sim['stepCount'] == 0 and simulation['stepCount'] == summary['steps'], 'Simulation step summary mismatch')
        self.require(same_float(final['time'], summary['seconds']) and final['winner'] == summary['winner'], 'Final world/manifest outcome mismatch')
        terminal = final['winner'] != -1
        self.require(summary['status'] == ('natural_terminal' if terminal else 'observation_cap'), 'Terminal/cap label contradicts actual winner')
        self.require(terminal or final['time'] >= manifest['cap'], 'Observation stopped early without terminal state')
        self.require(final['time'] <= manifest['cap'] + .11, 'Observation exceeded its declared fixed cap')
        # Floating simulation time accumulates as float32, not exact steps * 0.1.
        expected_time = 0.0
        for _ in range(simulation['stepCount']):
            expected_time = f32(expected_time + f32(.1))
        self.require(same_float(final['time'], expected_time), 'World time differs from actual fixed-step accumulation')
        entities = {e['state']['id']: e for e in final['entities']}
        opening_events = opening['events']
        events = final['events']
        self.require(len(events) == summary['events'] and events[:len(opening_events)] == opening_events, 'Final event count/opening prefix mismatch')
        starts = [e for e in opening_events if e['kind'] == 'StartingResources']
        self.require(sorted(e['player'] for e in starts) == [0, 1] and all(e['time'] == 0 for e in starts), 'Opening lacks exactly two actual StartingResources events')
        if identity[0] == 'river':
            self.require(opening['hasScenarioDocument'] and opening['scenarioDocument']['id'] == identity[1], 'Dutch case is not the declared authored map')
            self.require(all(s['age'] == 1 and s['civilization'] == identity[0] and costs_equal(s['initialResources'], wallet) for s in opening['scenarioDocument']['starts']), 'Authored opening overrides are not the default age-I wallet')
            files['opening-scenario.json'] = reference(self.file(directory / 'opening-scenario.json'))
        else:
            self.require(opening['mapId'] == identity[1] and not opening['hasScenarioDocument'], 'French case map differs')
        counts = Counter()
        birth_ids = set()
        spawn_events = {}
        paid = Counter()
        births = Counter()
        queues = defaultdict(deque)
        transactions = {p: {kind: dict.fromkeys(COST, 0.0) for kind in ('Payment', 'Refund')} for p in (0, 1)}
        previous = None
        seen_events = 0
        with (directory / 'events.jsonl').open() as stream:
            for index, line in enumerate(stream):
                event = parse_json(line)
                seen_events += 1
                self.require(index < len(events) and event == events[index], 'events.jsonl differs from final world at line ' + str(index + 1))
                self.require(index == 0 or event['time'] >= previous['time'], 'Events are not monotonic')
                kind = event['kind']; counts[kind] += 1
                if kind == 'Spawn':
                    self.require(event['entity'] not in spawn_events, 'Duplicate Spawn identity')
                    spawn_events[event['entity']] = event
                if event.get('hasEconomy'):
                    tx = event['economy']
                    for k in COST:
                        self.require(same_float(tx['actualDelta'][k], f32(f32(tx['balanceAfter'][k]) - f32(tx['balanceBefore'][k]))), 'Transaction actual delta differs at line ' + str(index + 1))
                        self.require(same_float(tx['balanceAfter'][k], f32(f32(tx['balanceBefore'][k]) + f32(tx['requestedDelta'][k]))), 'Transaction requested payment differs at line ' + str(index + 1))
                    if kind in ('Payment', 'Refund'):
                        sign = -1 if kind == 'Payment' else 1
                        for k in COST:
                            transactions[event['player']][kind][k] = f32(transactions[event['player']][kind][k] + f32(sign * tx['requestedDelta'][k]))
                if kind == 'ProductionQueued' and event['message'].split(' ×')[0] in SCOUTS:
                    unit = event['message'].split(' ×')[0]; definition = definitions[unit]
                    self.require(previous is not None and previous['kind'] == 'Payment' and previous.get('hasEconomy') and previous['player'] == event['player'] and previous['entity'] == event['entity'] and previous['time'] == event['time'], 'Scout queue lacks preceding actual payment')
                    self.require(int(event['amount']) == 1 and all(same_float(-previous['economy']['requestedDelta'][k], definition['cost'].get(k, 0)) for k in COST), 'Scout payment/count differs from project rules')
                    self.require(entities[event['entity']]['definition'] == definition['producer'], 'Scout producer identity differs')
                    queues[(event['entity'], unit)].append(index); paid[unit] += 1
                if kind == 'UnitTrained' and event['message'] in SCOUTS:
                    unit = event['message']; key = (event['entity'], unit)
                    self.require(queues[key], 'Scout birth has no prior paid queue')
                    queues[key].popleft()
                    self.require(event['target'] in entities and entities[event['target']]['definition'] == unit and entities[event['target']]['state']['owner'] == event['player'], 'Scout birth target is not the actual owned entity')
                    # Bind the actual unique ID/time, without assuming a bounded
                    # number of intervening XP or shipment-token events.
                    spawned = spawn_events.get(event['target'])
                    self.require(spawned and spawned['time'] == event['time'] and spawned['player'] == event['player'], 'Scout birth lacks its actual Spawn event')
                    self.require(event['target'] not in birth_ids, 'Scout has duplicate UnitTrained birth')
                    birth_ids.add(event['target'])
                    births[unit] += 1
                if kind == 'Refund' and event['message'].startswith('Cancelled '):
                    unit = event['message'][10:].split(':')[0]
                    if unit in SCOUTS and queues[(event['entity'], unit)]:
                        queues[(event['entity'], unit)].popleft()
                if kind == 'Death':
                    for unit in SCOUTS:
                        queues[(event['entity'], unit)].clear()
                previous = event
        self.require(seen_events == len(events), 'External event stream is truncated')
        self.require(birth_ids == {e['state']['id'] for e in final['entities'] if e['definition'] in SCOUTS}, 'A scout entity lacks an actual paid birth')
        for e in final['entities']:
            if e['state']['dead'] or e['state']['hp'] <= 0:
                continue
            for unit in SCOUTS:
                queued = sum(j['count'] for j in e['queue'] if j['kind'] == 'Train' and j['item'] == unit)
                self.require(queued == len(queues[(e['state']['id'], unit)]), 'Outstanding paid scout queues differ from final producer state')
        expected_scout = 'envoy' if identity[0] == 'river' else 'native_scout'
        scout_ids = {e['state']['id'] for e in final['entities'] if e['definition'] == expected_scout}
        command_count = sum(e['kind'] == 'Command' and e['entity'] in scout_ids and e['message'].startswith('Scout ×') for e in events)
        stealth_count = sum(e['kind'] == 'Stealth' and e['entity'] in scout_ids and e['amount'] == 1 for e in events)
        self.require(paid[expected_scout] > 0 and births[expected_scout] > 0 and command_count > 0, 'Declared scout behavior was not exercised by actual paid birth/Scout commands')
        if expected_scout == 'native_scout':
            self.require(stealth_count > 0, 'French ordinary observation contains no actual stealth entry')
        samples = [parse_json(line) for line in (directory / 'samples.jsonl').read_text().splitlines()]
        self.require(len(samples) >= 4 and len(samples) % 2 == 0, 'Samples lack paired opening/final players')
        for i in range(0, len(samples), 2):
            pair = samples[i:i+2]
            self.require([s['player'] for s in pair] == [0, 1] and pair[0]['time'] == pair[1]['time'], 'Sample player/time pairing differs')
            if i:
                self.require(pair[0]['time'] >= samples[i-2]['time'], 'Sample times are not monotonic')
                self.require(all(pair[p]['exploredCells'] >= samples[i-2+p]['exploredCells'] for p in (0, 1)), 'Explored fog history decreased')
            for s in pair:
                self.require(s['scouts'] == len(s['scoutIds']) == len(s['scoutStatus']) and len(set(s['scoutIds'])) == s['scouts'], 'Sample scout identities/count differ')
                self.require(all(i in birth_ids and entities[i]['state']['owner'] == s['player'] and spawn_events[i]['time'] <= s['time'] for i in s['scoutIds']), 'Sample contains an unproduced/foreign/future scout')
                self.require(all(0 <= s[k] <= s['scouts'] for k in ('scoutOrders', 'stealthScouts', 'concealedScouts')) and s['concealedScouts'] <= s['stealthScouts'], 'Sample scout subset counts invalid')
        self.require(all(s['time'] == 0 and s['age'] == 1 and s['scouts'] == s['queuedScouts'] == 0 and costs_equal(s['wallet'], wallet) for s in samples[:2]), 'Opening samples do not match declared ordinary opening')
        def fog_at(player, point):
            if not (0 <= point['x'] < 128 and 0 <= point['y'] < 128):
                return 0
            x = min(127, max(0, round(point['x'])))
            y = min(127, max(0, round(point['y'])))
            return final['visibility'][player*16384 + x*128 + y]

        def visible(player, entity):
            state = entity['state']
            if state['dead'] or state['hp'] <= 0 or state['garrisonedIn'] != 0:
                return False
            owner = state['owner']
            if owner != player and fog_at(player, state['position']) != 2:
                return False
            if not state['stealth'] or owner == player or final['teams'][owner] == final['teams'][player] or final['time'] < state['revealedUntil']:
                return True
            for d in final['entities']:
                ds = d['state']; definition = definitions[d['definition']]
                if ds['owner'] < 0 or final['teams'][ds['owner']] != final['teams'][player]:
                    continue
                dp = final['players'][ds['owner']]['state']
                if ds['dead'] or ds['hp'] <= 0 or ds['construction'] < 1 or ds['garrisonedIn'] != 0 or dp['eliminated'] or dp['resigned'] or definition['kind'] not in (0, 1) or definition.get('detectionRange', 0) <= 0 or fog_at(player, ds['position']) != 2:
                    continue
                dx = f32(f32(ds['position']['x']) - f32(state['position']['x']))
                dy = f32(f32(ds['position']['y']) - f32(state['position']['y']))
                if f32(f32(dx*dx) + f32(dy*dy)) <= min(8, definition['detectionRange'])**2:
                    return True
            return False

        ledgers = []
        for player in (0, 1):
            p = final['players'][player]['state']; sample = samples[-2+player]
            self.require(p['civilization'] == identity[0], 'Final civilization differs from declared opening')
            alive = [e for e in final['entities'] if e['state']['owner'] == player and not e['state']['dead'] and e['state']['hp'] > 0]
            scouts = [e for e in alive if e['definition'] in SCOUTS]
            self.require(same_float(sample['time'], final['time']) and sample['age'] == p['age'], 'Final sample time/age mismatch')
            self.require(sample['scoutIds'] == [e['state']['id'] for e in scouts] and sample['scoutStatus'] == [e['state']['status'] for e in scouts], 'Final sampled scout identities/state mismatch')
            self.require(sample['scoutOrders'] == sum(bool(e['orders']) and e['orders'][0]['kind'] == 16 for e in scouts), 'Final Scout order count mismatch')
            self.require(sample['stealthScouts'] == sum(e['state']['stealth'] for e in scouts), 'Final stealth state count mismatch')
            self.require(sample['exploredCells'] == sum(v != 0 for v in final['visibility'][player*16384:(player+1)*16384]), 'Final explored cell sample differs from actual fog')
            self.require(sample['concealedScouts'] == sum(e['state']['stealth'] and not visible(1-player, e) for e in scouts), 'Final concealment sample differs from actual fog/detectors/timers')
            self.require(sample['queuedScouts'] == sum(j['count'] for e in alive for j in e['queue'] if j['kind'] == 'Train' and j['item'] in SCOUTS), 'Final queued scout count mismatch')
            for sample_key, state_key in [('wallet', 'resources'), ('gathered', 'gathered'), ('spent', 'spent')]:
                self.require(costs_equal(sample[sample_key], p[state_key]), 'Final sample ledger differs: ' + sample_key)
            for kind, field in [('Payment', 'spent'), ('Refund', 'refunded')]:
                self.require(costs_equal(transactions[player][kind], p[field]), 'Actual transaction accumulation differs from final ' + field)
            residual = {k: p['resources'][k] - (wallet.get(k, 0) + sum(p.get(field, {}).get(k, 0) for field in ('gathered', 'passiveIncome', 'tradeIncome', 'marketIncome', 'refunded')) - p['spent'][k]) for k in COST}
            ledgers.append({'player': player, 'finalWallet': p['resources'], 'spent': p['spent'], 'refunded': p['refunded'], 'actualPaymentEventsMatched': True,
                            'aggregateWalletResidual': residual, 'residualScope': 'Informational float-account aggregation; not claimed exact reconstruction of per-tick income rounding.'})
        self.require(counts['StartingResources'] == 2, 'Unexpected repeated opening resource grant event')
        self.require(counts['ScenarioResourceGranted'] == 0, 'Ordinary observation contains a scripted resource grant')
        if final['time'] >= 1500:
            for suffix in ('world', 'simulation'):
                name = 'comparison1500-' + suffix + '.json'; files[name] = reference(self.file(directory / name))
            comparison = load(directory / 'comparison1500-world.json')
            self.require(1500 <= comparison['time'] <= 1500.11 and events[:len(comparison['events'])] == comparison['events'], '1500 comparison snapshot is not an actual event prefix')
        return {'id': summary['id'], 'outcome': summary['status'], 'winner': final['winner'], 'seconds': final['time'], 'steps': simulation['stepCount'],
                'files': files, 'eventCount': len(events), 'samplePairs': len(samples)//2, 'scoutPaidQueues': dict(paid), 'scoutBirths': dict(births),
                'actualScoutCommandEvents': command_count, 'actualStealthEntryEvents': stealth_count, 'ledgers': ledgers}

    def native(self):
        if not self.guid:
            raise Pending('Native identity cannot close before a successful same-source build GUID')
        captures = self.args.native / 'Captures'
        if not captures.is_dir():
            raise Pending('Native/Captures not present')
        pairs = []
        for path in sorted(captures.glob('Frontier-*.json')):
            m = load(path)
            self.require(m['schemaVersion'] == 6 and m['appVersion'] == self.version and m['buildGuid'] == self.guid, 'Native capture belongs to a different schema/version/GUID: ' + path.name)
            self.require(m['pngFile'] == path.stem + '.png', 'Capture PNG is not its same-stem actual file')
            png = self.file(path.with_suffix('.png'))
            with png.open('rb') as f:
                header = f.read(24)
            self.require(header[:8] == b'\x89PNG\r\n\x1a\n' and header[12:16] == b'IHDR', 'Invalid PNG header')
            self.require(struct.unpack('>II', header[16:24]) == (m['width'], m['height']) and sha(png) == m['pngSha256'], 'PNG dimensions/hash differ from metadata')
            self.require(m['captureFrame'] >= m['requestFrame'] and m['requestMatchId'] == m['matchId'], 'Capture request/frame/match mismatch')
            if not m['inMenu']:
                self.require(m['hudUpdatedFrame'] == m['captureFrame'] and same_float(m['hudWorldTime'], m['worldTime']) and m['hudMatchId'] == m['matchId'], 'HUD/frame/world pairing mismatch')
            pairs.append({'metadata': reference(path), 'png': reference(png), 'matchId': m['matchId'], 'worldTime': m['worldTime']})
        if not pairs:
            raise Pending('No actual final-GUID native F9 pairs')
        return {'buildGuid': self.guid, 'pairs': pairs, 'scope': 'PNG/metadata/HUD/build identity only; no inferred input, combat, cold restore, performance, or native feature pass.'}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo', type=Path, default=DEFAULT_REPO)
    parser.add_argument('--focused', type=Path, default=BASE/'Focused6')
    parser.add_argument('--full', type=Path, default=BASE/'Full2')
    parser.add_argument('--build', type=Path, default=BASE/'Build1')
    parser.add_argument('--observations', type=Path, default=BASE/'NormalObserved1')
    parser.add_argument('--native', type=Path, default=BASE/'Native')
    parser.add_argument('--expected-fingerprint')
    parser.add_argument('--json', type=Path, help='Optional exclusive new output JSON; never overwrites')
    parser.add_argument('--markdown', type=Path, help='Optional exclusive new brief Markdown; never overwrites')
    args = parser.parse_args()
    if args.json and args.markdown and args.json.resolve() == args.markdown.resolve():
        parser.error('JSON and Markdown outputs must be distinct new paths')
    for p in (args.json, args.markdown):
        if p and p.exists():
            parser.error('Refusing existing output: ' + str(p))
        if p and args.repo.resolve() in p.resolve().parents:
            parser.error('Checker outputs must remain outside the repository')
    audit = Audit(args)
    audit.section('currentSource', audit.sources)
    if audit.parts['currentSource']['status'] == 'PASS':
        audit.section('focused', lambda: audit.tests(args.focused, 'focused'))
        audit.section('full', lambda: audit.tests(args.full, 'full'))
        audit.section('build', audit.build)
        audit.section('ordinaryObservations', audit.observation)
        audit.section('nativeCaptureIdentity', audit.native)
    status = 'FAIL' if any(s['status'] == 'FAIL' for s in audit.parts.values()) else 'PENDING' if any(s['status'] == 'PENDING' for s in audit.parts.values()) else 'PASS_CHECKED_SCOPES'
    report = {'schema': 'check075-v1', 'utc': datetime.now(timezone.utc).isoformat(), 'status': status,
              'checker': reference(Path(__file__).resolve()), 'sections': audit.parts,
              'boundaries': ['No Unity, native app, test, build, packaging, or input replay executed.',
                             'Existing original receipts, logs, XML, checkpoints, events and PNGs were read without modification.',
                             'PASS_CHECKED_SCOPES is evidence consistency only; native interaction acceptance requires separately reviewed action/process/checkpoint receipts.',
                             'No upload, private-cloud byte readback, package, natural-match win, balance, or FPS is inferred.']}
    text = json.dumps(report, indent=2, ensure_ascii=False) + '\n'
    if args.json:
        with args.json.open('x') as f:
            f.write(text)
    if args.markdown:
        md = '# 075 evidence closure check\n\nStatus: **' + status + '**.\n\n'
        for name, section in audit.parts.items():
            md += '- ' + name + ': **' + section['status'] + '**' + (' — ' + section['reason'] if 'reason' in section else '') + '.\n'
        md += '\nThis is a read-only evidence consistency check. Native picture identity does not establish successful gameplay, cold restoration, performance, packaging, or upload. Missing/running scopes remain pending.\n'
        with args.markdown.open('x') as f:
            f.write(md)
    print(json.dumps({'status': status, 'sections': {k: v['status'] for k, v in audit.parts.items()}, 'json': str(args.json) if args.json else None}))
    return 1 if status == 'FAIL' else 2 if status == 'PENDING' else 0


if __name__ == '__main__':
    sys.exit(main())
