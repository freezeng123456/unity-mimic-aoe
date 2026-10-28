#!/usr/bin/env python3
"""Read-only fixture receipt/event audit; create a NEW report, never overwrite.

--help only displays usage. Audit inputs and output are always explicit.
The result covers controlled editor fixtures, not native rendering/gameplay.
"""
import argparse
import hashlib
import importlib.util
import json
import math
from pathlib import Path
from datetime import datetime, timezone
import xml.etree.ElementTree as ET

REPO = Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
READER_PATH = Path(__file__).resolve().with_name('save-envelope-reader.py')
_reader_spec = importlib.util.spec_from_file_location('frontier_save_envelope', READER_PATH)
if _reader_spec is None or _reader_spec.loader is None:
    raise ValueError('Missing external save-envelope-reader.py dependency')
save_envelope = importlib.util.module_from_spec(_reader_spec)
_reader_spec.loader.exec_module(save_envelope)

def require(ok, message):
    if not ok:
        raise AssertionError(message)

def read(path):
    return json.loads(path.read_text())

def sha(data):
    return hashlib.sha256(data).hexdigest()

def close(a, b):
    return abs(a - b) < .0001

def fields(text):
    decoder = json.JSONDecoder()
    position = text.index('{') + 1
    result = {}
    while True:
        while text[position].isspace() or text[position] == ',':
            position += 1
        if text[position] == '}':
            return result
        key, position = decoder.raw_decode(text, position)
        while text[position].isspace():
            position += 1
        require(text[position] == ':', 'JSON colon')
        position += 1
        while text[position].isspace():
            position += 1
        start = position
        _, position = decoder.raw_decode(text, position)
        result[key] = text[start:position]

def audit_previews(root, test_path, destination, project, runtime_commit):
    require(not destination.exists(), 'Output already exists; choose a new report path')
    manifest = read(root / 'manifest.json')
    require(manifest['status'] == 'exported', 'Manifest incomplete')
    fingerprint = manifest['sourceFingerprint']
    require(fingerprint == manifest['sourceFingerprintAfter'], 'Export changed source fingerprint')
    tests = read(test_path)
    require(sha(test_path.read_bytes()) == manifest['testReceiptSha256'], 'Test receipt SHA')
    require(tests['sourceFingerprint'] == fingerprint, 'Test/export fingerprint differs')
    require(sha(json.dumps(tests['files'], sort_keys=True, separators=(',', ':')).encode()) == fingerprint, 'Test source-file manifest fingerprint')
    require(set(tests['reports']) == {'EditMode', 'PlayMode'}, 'Both test suites are required')
    counts = {}
    for name, report in tests['reports'].items():
        path = test_path.parent / report['file']
        require(sha(path.read_bytes()) == report['sha256'], name + ' XML SHA')
        xml = ET.parse(path).getroot()
        require(xml.get('result') == 'Passed' and xml.get('passed') == xml.get('total') and xml.get('failed') == '0', name + ' XML status')
        require(int(xml.get('passed')) == report['passed'], name + ' count')
        counts[name] = report['passed']
    require(all(count > 0 for count in counts.values()), 'Empty test suite')
    rules_path = project / 'Assets/Resources/Rules.json'
    require(sha(rules_path.read_bytes()) == tests['files']['Assets/Resources/Rules.json'], 'Population rules differ from tested source')
    rules = {e['id']: e for e in read(rules_path)['entities']}
    case_ids = [entry['id'] for entry in manifest['previews']]
    require(len(case_ids) == 4 and set(case_ids) == {'naval-strait-314159', 'neutral-delta-271828', 'bridge-river-161803', 'monitor-harbor-161803'}, 'Expected four distinct preview cases')
    audit = dict(schema=1, recordedAtUtc=datetime.now(timezone.utc).isoformat(), status='verified_fixture_archive',
        scope='Read-only receipt/event/file audit of controlled editor fixtures. No independent Unity simulation replay, native rendering/input, normal opening/age advancement, ended match, performance or build-GUID acceptance. Cold JSON/20-tick continuation success is the exporter recorded result; envelope and exact raw world/simulation payload hashes are independently recomputed.',
        runtimeCommitReported=runtime_commit, sourceFingerprint=fingerprint,
        previewsDirectory=str(root), manifestSha256=sha((root / 'manifest.json').read_bytes()), tests=counts,
        testReceiptPath=str(test_path), testReceiptSha256=sha(test_path.read_bytes()),
        verifierSha256=sha(Path(__file__).read_bytes()),
        saveEnvelopeReaderSha256=sha(READER_PATH.read_bytes()), cases=[])
    for entry in manifest['previews']:
        directory = root / entry['id']
        receipt = read(directory / 'receipt.json')
        require(receipt == entry, 'Manifest/receipt content differs')
        require(receipt['status'] == 'exported' and receipt['sourceFingerprint'] == fingerprint, 'Case status/FP')
        decoded = save_envelope.read_envelope(directory / 'preview.json')
        raw, saved = decoded['raw'], decoded['saved']
        exact = fields(decoded['payloadText'])
        world, simulation = saved['world'], saved['simulation']
        events = read(directory / 'events.json')['events']
        entities = {e['state']['id']: e for e in world['entities']}
        require(sha(raw) == receipt['saveSha256'] and len(raw) == receipt['saveBytes'], 'Save file SHA/bytes')
        require(sha((directory / 'events.json').read_bytes()) == receipt['eventsSha256'] and len(events) == receipt['eventCount'], 'Events file SHA/count')
        require(events == world['events'], 'Saved events differ from separate event log')
        require(sha(exact['world'].encode()) == receipt['worldJsonSha256'], 'Exact raw world JSON SHA')
        require(sha(exact['simulation'].encode()) == receipt['simulationJsonSha256'], 'Exact raw simulation JSON SHA')
        require(saved['appVersion'] == '0.8.0' and saved['matchId'] == receipt['matchId'] and world['seed'] == receipt['seed'] and world['mapId'] == receipt['map'], 'Saved identity')
        require(world['maxAge'] == receipt['maxAge'] == (4 if entry['id'].startswith('monitor') else 3), 'Age cap')
        require(world['paused'] and saved['simulationSpeed'] == 1 and not saved['autoplay'] and simulation['controllers'] == [], 'Pause/speed/AI')
        require(world['winner'] == receipt['winner'] == -1 and world['time'] == receipt['worldTime'], 'Final clock/winner')
        require(saved['cameraFocus'] == receipt['cameraFocus'] and saved['cameraSize'] == receipt['cameraSize'] and saved['selection'] == receipt['selection'], 'Camera/selection')
        require(receipt['coldRoundTripPassed'] and receipt['coldContinuationSteps'] == 20, 'Exporter cold continuation assertion')
        require(world['rulesHash'] == receipt['rulesHash'], 'Rules identity')
        for grant in receipt['grants']:
            require(grant['controlledAge'] == world['players'][grant['player']]['state']['age'] == receipt['maxAge'], 'Grant age')
            emitted = [e for e in events if e['kind'] == 'ControlledPreviewResourceGrant' and e['player'] == grant['player']]
            require(len(emitted) == 1 and emitted[0]['time'] == 0 and emitted[0]['hasEconomy'], 'Grant event')
            transaction = emitted[0]['economy']
            require(transaction['actualDelta'] == transaction['requestedDelta'] == grant['amount'] and transaction['balanceBefore'] == grant['before'] and transaction['balanceAfter'] == grant['after'], 'Grant transaction')
        for command in receipt['commands']:
            require(command['accepted'], 'Rejected required command')
            if command['command']['kind'] in (1, 2, 22, 23, 24):
                span = events[command['firstEvent']:command['firstEvent'] + command['eventCount']]
                payments = [e for e in span if e['kind'] == 'Payment']
                require(len(payments) == 1 and payments[0]['hasEconomy'], 'Actual paid-command event')
                transaction = payments[0]['economy']
                require(transaction['balanceBefore'] == command['resourcesBefore'] and transaction['balanceAfter'] == command['resourcesAfter'] and sum(transaction['actualDelta'].values()) < 0, 'Actual debit and balances')
        for building in receipt['buildings']:
            entity = entities[building['entity']]
            require(entity['definition'] == building['definition'] and entity['state']['paidConstruction'] == building['paid'] and entity['state']['construction'] == 1 and not entity['state']['dead'], 'Completed paid building')
        for birth in receipt['births']:
            entity = entities[birth['entity']]
            require(entity['definition'] == birth['definition'] and entity['state']['owner'] == birth['player'], 'Born entity identity')
            indices = [i for i, e in enumerate(events) if i >= birth['firstEvent'] and e['kind'] == 'Spawn' and e['entity'] == birth['entity']]
            require(len(indices) == 1, 'Unique actual unit Spawn')
            # A later queue may start at the exact same simulation time. Event
            # index through this Spawn is the proper causal window, not <= time.
            window = events[birth['firstEvent']:indices[0] + 1]
            payments = [e for e in window if e['kind'] == 'Payment' and e['entity'] == birth['producer']]
            require(len(payments) == birth['paymentEvents'] == birth['spawnEvents'] == 1, 'One actual pre-birth production payment')
            require(close(events[indices[0]]['time'], birth['bornAt']), 'Actual birth time')
        for move in receipt['movements']:
            require(move['arrived'] and move['distance'] < .85 and move['observedAt'] >= move['startedAt'], 'Real movement arrival')
            distance = math.hypot(move['actual']['x'] - move['destination']['x'], move['actual']['y'] - move['destination']['y'])
            require(close(distance, move['distance']), 'Recomputed arrival distance')
            commands = [c for c in receipt['commands'] if c['time'] == move['startedAt'] and c['command']['kind'] == 0 and c['command']['order'] == 0 and c['command']['actors'] == [move['entity']]]
            require(len(commands) == 1 and commands[0]['command']['point'] == move['requested'], 'Public Move linkage')
        for player, snapshot in enumerate(world['players']):
            population, state = receipt['finalPopulation'][player], snapshot['state']
            used = sum(rules[e['definition']]['population'] for e in world['entities'] if not e['state']['dead'] and e['state']['hp'] > 0 and e['state']['owner'] == player and rules[e['definition']]['kind'] == 0)
            require(used == population['used'] == state['usedPopulation'], 'Real unit census population')
            require(population['reserved'] == state['reservedPopulation'] == 0, 'No incomplete reserved production')
            require(population['capacity'] == state['populationCap'] and state['resources'] == receipt['finalResources'][player], 'Final player receipt')
        for ally in receipt['allies']:
            state = next(s for s in world['neutralAllies'] if s['id'] == ally['id'])
            require(state['owner'] == 0 and state['site'] == ally['site'] and ally['tech'] in world['players'][0]['technologies'], 'Completed real treaty and research')
        if entry['id'].startswith('bridge'):
            objective = world['mapObjective']
            require(objective['mode'] == 'defend_bridge' and objective['status'] == 'active' and 0 < objective['progress'] < 180, 'Active unfinished bridge')
            for actor in receipt['friendlyUnits']:
                position = entities[actor]['state']['position']
                require(math.hypot(position['x'] - objective['center']['x'], position['y'] - objective['center']['y']) < 9, 'Real guard in bridge radius')
        audit['cases'].append(dict(id=entry['id'], status='verified_fixture_archive', matchId=saved['matchId'], map=receipt['map'], seed=receipt['seed'], maxAge=receipt['maxAge'], worldTime=world['time'],
            coldContinuationStepsRecorded=20, exactRawWorldSimulationHashesVerified=True, paidBuildingCount=len(receipt['buildings']), paidBirthCount=len(receipt['births']), moveArrivalCount=len(receipt['movements']),
            finalPopulation=receipt['finalPopulation'], allies=receipt['allies'], objective=receipt['objective'], selection=saved['selection'], cameraFocus=saved['cameraFocus'], cameraSize=saved['cameraSize'],
            saveSha256=sha(raw), saveEnvelopeSchema=decoded['schema'],
            rawPayloadBytes=decoded['payloadBytes'], rawPayloadSha256=decoded['payloadSha256'],
            receiptSha256=sha((directory / 'receipt.json').read_bytes()), eventsSha256=sha((directory / 'events.json').read_bytes())))
    with destination.open('x') as stream:
        json.dump(audit, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
    print(destination)
    print('SHA256', sha(destination.read_bytes()))
    print('Tests', counts, 'verified cases', len(audit['cases']))

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--previews', required=True, type=Path, help='Completed four-case preview export directory')
    parser.add_argument('--tests', required=True, type=Path, help='Matching test run directory or source-evidence.json file')
    parser.add_argument('--output', required=True, type=Path, help='New JSON audit report; existing files are refused')
    parser.add_argument('--project', type=Path, default=REPO, help='Project containing the tested Rules.json for population census')
    parser.add_argument('--runtime-commit', help='Optional runtime commit supplied by the operator; recorded as provenance, not independently verified')
    args = parser.parse_args()
    tests = args.tests.expanduser().resolve()
    if tests.is_dir():
        tests /= 'source-evidence.json'
    audit_previews(args.previews.expanduser().resolve(), tests, args.output.expanduser().absolute(),
                  args.project.expanduser().resolve(), args.runtime_commit)

if __name__ == '__main__':
    main()
