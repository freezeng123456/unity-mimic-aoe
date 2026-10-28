#!/usr/bin/env python3
"""Bounded, read-only 069 metadata review; never operates GUI or simulation.

--plan is a root-maintained mapping of actual evidence, not an instruction to
invent missing observations. --out is an exclusive new receipt under Native.
No packaging, Unity imports, personal-directory reads, or network access.
"""
import argparse
import base64
from collections import Counter
import datetime as dt
import hashlib
import json
import math
from pathlib import Path
import plistlib
import re
import struct
import sys
import zlib

NATIVE = Path(__file__).resolve().parent
CANDIDATE = NATIVE.parent
APP = CANDIDATE / 'Build/Frontier Reckoning.app'
GUID = VERSION = UNITY = SOURCE_FP = SOURCE_COMMIT = RULES_HASH = None
SOURCE_INPUTS = None
PROFILE = NATIVE / 'profile'
IDENTITY_RECEIPT = None
IDENTITY_FACTS = None
IDENTITY_DATA = None
REPLAY_PROFILE = None
MAX_JSON = 4 * 1024 * 1024
MAX_SCENARIO = 2 * 1024 * 1024
MAX_STORED = 64 * 1024 * 1024
MAX_PAYLOAD = 256 * 1024 * 1024
MAX_TOTAL = 2 * 1024 * 1024 * 1024
HEX = re.compile(r'[0-9a-f]{64}\Z')
IDENT = re.compile(r'[a-z0-9_-]{1,64}\Z')
CAPTURE = re.compile(r'Frontier-\d{8}-\d{6}-\d{3}-f(\d+)-[0-9a-f]{32}\.json\Z')


def need(ok, reason):
    if not ok:
        raise ValueError(reason)


def utc():
    return dt.datetime.now(dt.timezone.utc).isoformat()


def utc_value(value):
    need(isinstance(value, str) and len(value) <= 64, 'Invalid UTC field')
    value = re.sub(r'(\.\d{6})\d+(?=Z|[+-]\d{2}:\d{2}$)', r'\1', value)
    result = dt.datetime.fromisoformat(value.replace('Z', '+00:00'))
    need(result.tzinfo is not None and result.utcoffset() == dt.timedelta(), 'UTC offset required')
    return result


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def unique(pairs):
    result = {}
    for key, value in pairs:
        need(key not in result, 'Duplicate JSON property: ' + key)
        result[key] = value
    return result


def loads(raw):
    if isinstance(raw, bytes):
        raw = raw.decode('utf-8', 'strict')
    value = json.loads(raw, object_pairs_hook=unique,
                       parse_constant=lambda _: (_ for _ in ()).throw(ValueError('Non-finite JSON constant')))
    stack = [value]
    while stack:
        item = stack.pop()
        if isinstance(item, dict):
            stack.extend(item.values())
        elif isinstance(item, list):
            stack.extend(item)
        elif isinstance(item, float):
            need(math.isfinite(item), 'Non-finite JSON number')
    return value


def inside(path, root=NATIVE):
    path = Path(path)
    if not path.is_absolute():
        path = CANDIDATE / path
    lexical = path.absolute()
    root = root.resolve()
    resolved = lexical.resolve()
    need(resolved == root or root in resolved.parents, 'Path escapes requested candidate scope')
    # Do not accept evidence through a link or link-containing directory.
    p = lexical
    while p != root and root in p.resolve().parents:
        need(not p.is_symlink(), 'Evidence path contains a symlink')
        p = p.parent
    return resolved


def read(path, limit=MAX_JSON):
    path = inside(path, CANDIDATE)
    need(path.is_file() and not path.is_symlink(), 'Expected regular evidence file')
    before = path.stat()
    need(before.st_size <= limit, 'Evidence exceeds bounded read size')
    with path.open('rb') as stream:
        raw = stream.read(limit + 1)
    need(len(raw) <= limit, 'Evidence grew beyond bounded read size')
    after = path.stat()
    need((before.st_ino, before.st_size, before.st_mtime_ns) ==
         (after.st_ino, after.st_size, after.st_mtime_ns), 'Evidence changed during read')
    return raw


def record(path, raw=None, limit=MAX_JSON):
    path = inside(path, CANDIDATE)
    raw = read(path, limit) if raw is None else raw
    return {'file': path.relative_to(CANDIDATE).as_posix(), 'bytes': len(raw), 'sha256': sha(raw)}


def integer(value, minimum=0, maximum=1000000000):
    need(type(value) is int and minimum <= value <= maximum, 'Invalid integer range/type')
    return value


def number(value, minimum=-1e12, maximum=1e12):
    need(type(value) in (int, float) and math.isfinite(value) and minimum <= value <= maximum,
         'Invalid finite number range/type')
    return value


def shape(value, spec):
    if isinstance(spec, dict):
        need(isinstance(value, dict) and set(value) == set(spec), 'Missing/unknown structured JSON fields')
        for key, sub in spec.items():
            shape(value[key], sub)
    elif isinstance(spec, tuple):
        sub, minimum, maximum = spec
        need(isinstance(value, list) and minimum <= len(value) <= maximum, 'Invalid array bound/type')
        for item in value:
            shape(item, sub)
    elif spec == 'bool':
        need(type(value) is bool, 'Boolean required')
    elif spec == 'int':
        integer(value, -2147483648, 2147483647)
    elif spec == 'height':
        integer(value, -2000, 10000)
    elif spec == 'surface':
        integer(value, 0, 3)
    elif spec == 'num':
        number(value)
    elif spec == 'str':
        need(isinstance(value, str) and len(value.encode('utf-8', 'strict')) <= 65536, 'Bounded string required')
    elif spec == 'largeStr':
        need(isinstance(value, str) and len(value.encode('utf-8', 'strict')) <= MAX_PAYLOAD, 'Bounded envelope string required')
    else:
        raise ValueError('Unknown verifier type')


V2 = {'x': 'num', 'y': 'num'}
COST = {'food': 'num', 'wood': 'num', 'coin': 'num'}
CONDITION = {k: 'str' for k in ('kind', 'objectId', 'regionId', 'objectiveId', 'triggerId')}
CONDITION.update(player='int', resource='int', amount='num', time='num')
ACTION = {k: 'str' for k in ('kind', 'objectiveId', 'regionId', 'objectId', 'triggerId', 'definition', 'targetId', 'message')}
ACTION.update(player='int', count='int', point=V2, resource='int', amount='num')
PLACEMENT = {'id': 'str', 'definition': 'str', 'position': V2, 'owner': 'int', 'remaining': 'num', 'complete': 'bool'}
OBJECTIVE = {k: 'str' for k in ('id', 'title', 'description', 'kind', 'objectId', 'regionId')}
OBJECTIVE.update(player='int', required='bool', resource='int', amount='num', holdSeconds='num', deadline='num', prerequisites=('str', 0, 32))
TRIGGER = {'id': 'str', 'enabled': 'bool', 'matchAny': 'bool', 'maxFires': 'int', 'repeatSeconds': 'num',
           'conditions': (CONDITION, 1, 8), 'actions': (ACTION, 1, 8)}
DOCUMENT = {'schema': 'int', 'id': 'str', 'name': 'str', 'description': 'str', 'rulesHash': 'str', 'width': 'int', 'height': 'int',
            'seed': 'int', 'waterHeightCm': 'height', 'victoryPlayer': 'int', 'enableElimination': 'bool',
            'heightsCm': ('height', 129 * 129, 129 * 129), 'surfaces': ('surface', 128 * 128, 128 * 128),
            'rivers': ({'id': 'str', 'points': (V2, 2, 512), 'halfWidths': ('num', 2, 512), 'smooth': 'bool'}, 0, 8),
            'bridges': ({'id': 'str', 'center': V2, 'direction': V2, 'halfLength': 'num', 'halfWidth': 'num', 'rampLength': 'num', 'height': 'num'}, 0, 32),
            'starts': ({'player': 'int', 'team': 'int', 'age': 'int', 'civilization': 'str', 'initialResources': COST,
                        'town': V2, 'explorer': V2, 'workers': (V2, 0, 99)}, 2, 4),
            'resources': (PLACEMENT, 0, 2048), 'objects': (PLACEMENT, 0, 2048), 'tradeRoute': (V2, 0, 512),
            'tradeSockets': ({'id': 'str', 'position': V2, 'waypoint': 'int'}, 0, 32),
            'treasures': ({'id': 'str', 'definition': 'int', 'position': V2, 'guardPositions': (V2, 0, 32)}, 0, 32),
            'neutralBindings': ({'definition': 'str', 'socketId': 'str'}, 0, 32),
            'regions': ({'id': 'str', 'center': V2, 'size': V2}, 0, 64), 'objectives': (OBJECTIVE, 0, 32), 'triggers': (TRIGGER, 0, 64)}
TRIGGER_STATE = {'id': 'str', 'lastError': 'str', 'enabled': 'bool', 'fires': 'int', 'nextAt': 'num'}
OBJECTIVE_STATE = {'id': 'str', 'status': 'str', 'reason': 'str', 'progress': 'num', 'elapsed': 'num'}


def document(doc):
    shape(doc, DOCUMENT)
    need(doc['schema'] == 1 and doc['width'] == doc['height'] == 128, 'Unsupported scenario dimensions/schema')
    need(bool(IDENT.fullmatch(doc['id'])) and doc['rulesHash'] == RULES_HASH, 'Scenario identity/rules mismatch')
    need(len(doc['resources']) + len(doc['objects']) <= 2048, 'Combined placement bound')
    need(sum(len(r['points']) for r in doc['rivers']) <= 512, 'Combined river point bound')
    for river in doc['rivers']:
        need(len(river['points']) == len(river['halfWidths']), 'Mismatched river controls')
        for width in river['halfWidths']:
            number(width, 1, 48)
    for i, start in enumerate(doc['starts']):
        need(start['player'] == i, 'Noncontinuous scenario player slots')
        integer(start['team'], 0, 3)
        integer(start['age'], 1, 5)
    for trigger in doc['triggers']:
        integer(trigger['maxFires'], 1, 64)
        number(trigger['repeatSeconds'], .1, 86400)
        for condition in trigger['conditions']:
            need(condition['kind'] in {'time', 'object_dead', 'units_in_region', 'resource', 'objective_complete', 'objective_failed', 'trigger_fired'}, 'Unknown scenario condition kind')
        for action in trigger['actions']:
            need(action['kind'] in {'message', 'spawn', 'order_move', 'order_attack', 'grant_resource', 'complete_objective', 'fail_objective', 'win', 'lose', 'enable_trigger', 'disable_trigger'}, 'Unknown scenario action kind')
    need(all(o['kind'] in {'manual', 'hold_region', 'destroy_object', 'reach_region', 'collect_resource'} for o in doc['objectives']), 'Unknown scenario objective kind')
    ids = [row['id'] for key in ('rivers', 'bridges', 'resources', 'objects', 'tradeSockets', 'treasures', 'regions', 'objectives', 'triggers') for row in doc[key]]
    need(all(IDENT.fullmatch(x) for x in ids) and len(ids) == len(set(ids)), 'Invalid/duplicate authored ID')
    return doc


def portable(path):
    raw = read(path, MAX_SCENARIO)
    value = loads(raw)
    shape(value, {'schema': 'int', 'encoding': 'str', 'payload': 'largeStr', 'sha256': 'str'})
    need(value['schema'] == 1 and value['encoding'] == 'frontier-scenario-json-v1', 'Unsupported scenario envelope')
    payload = value['payload'].encode('utf-8', 'strict')
    need(len(payload) <= MAX_SCENARIO and HEX.fullmatch(value['sha256']) and sha(payload) == value['sha256'], 'Scenario payload SHA mismatch')
    doc = document(loads(payload))
    return {**record(path, raw), 'contentHash': value['sha256'], 'id': doc['id'], 'title': doc['name'],
            'players': len(doc['starts']), 'rivers': len(doc['rivers']), 'heightVertices': len(doc['heightsCm']),
            'surfaceCells': len(doc['surfaces']), 'triggers': [{'id': t['id'], 'maxFires': t['maxFires']} for t in doc['triggers']],
            'status': 'Passed', 'runtimeGeography': 'NotEvaluated',
            'validationScope': 'Envelope/payload SHA, exact DTO field types and bounded arrays only; an authored draft can be geographically unplayable.'}, doc


def png_bytes(raw):
    need(len(raw) <= MAX_STORED and raw[:8] == b'\x89PNG\r\n\x1a\n', 'Invalid PNG signature/size')
    offset, width, height, saw_data, saw_end = 8, 0, 0, False, False
    while offset < len(raw):
        need(offset + 12 <= len(raw), 'Truncated PNG chunk header')
        length = struct.unpack_from('>I', raw, offset)[0]
        end = offset + 12 + length
        need(end <= len(raw), 'Truncated PNG chunk body')
        kind, body = raw[offset + 4:offset + 8], raw[offset + 8:end - 4]
        expected = struct.unpack_from('>I', raw, end - 4)[0]
        need(zlib.crc32(kind + body) & 0xffffffff == expected, 'PNG chunk CRC mismatch')
        if offset == 8:
            need(kind == b'IHDR' and length == 13, 'PNG must begin with IHDR')
            width, height = struct.unpack_from('>II', body)
            integer(width, 1, 16384); integer(height, 1, 16384)
        elif kind == b'IHDR':
            raise ValueError('Duplicate PNG IHDR')
        if kind == b'IDAT':
            saw_data = True
        if kind == b'IEND':
            need(length == 0 and end == len(raw), 'Invalid IEND/trailing PNG data')
            saw_end = True
        offset = end
    need(saw_data and saw_end, 'PNG has no complete image data')
    return width, height


CAPTURE_FIELDS = set('schemaVersion eventSchemaVersion pngFile pngSha256 utc appVersion buildGuid unityVersion matchId requestMatchId requestFrame captureFrame width height seed requestWorldTime worldTime inMenu paused hasPlayer resources population hudResources hudMatchId hudUpdatedFrame hudWorldTime camera aiDifficulty mapId aiDecisionSeconds aiArmySeconds production generatedTerrain requiresTransport mapGeneratorVersion objective units neutralServices players skirmishMode guidedId guidedStatus guidedStage preferences observerPlayer replay lan scenario'.split())
REPLAY_SPEC = {k: 'bool' for k in ('watching', 'recording', 'seek', 'paused', 'worldPaused', 'verified', 'incomplete')}
REPLAY_SPEC.update({k: 'str' for k in ('segment', 'file', 'lastSavedFile', 'error', 'incompleteReason', 'fileError', 'libraryError')})
REPLAY_SPEC.update({k: 'int' for k in ('startStep', 'endStep', 'currentStep', 'observerPlayer')})
REPLAY_SPEC.update(speed='num', identity={'appVersion': 'str', 'buildIdentity': 'str', 'unityVersion': 'str', 'rulesHash': 'str',
                                        'mapGenerationVersion': 'int', 'guidedCatalogRevision': 'int', 'scenarioFormatVersion': 'int', 'scenarioRuntimeVersion': 'int'})
LAN_SPEC = {k: 'str' for k in ('role', 'connection', 'projectedDigest')}
LAN_SPEC.update({k: 'int' for k in ('localPlayer', 'pending', 'completedStep', 'port', 'revision', 'eventCursor')})
LAN_SPEC.update({k: 'bool' for k in ('connected', 'localMenuPaused', 'sharedPaused', 'hasSimulation', 'hasRecorder')})
LAN_SPEC.update(rttMilliseconds='num')


def capture(path):
    path = inside(path)
    name = CAPTURE.fullmatch(path.name)
    need(name, 'Invalid F9 filename')
    raw = read(path)
    value = loads(raw)
    need(isinstance(value, dict) and set(value) == CAPTURE_FIELDS, 'F9 schema6 missing/unknown fields')
    need(type(value['schemaVersion']) is int and value['schemaVersion'] == 6, 'Unsupported F9 schema')
    integer(value['eventSchemaVersion'], 2, 2)
    need(value['appVersion'] == VERSION and value['buildGuid'] == GUID and value['unityVersion'] == UNITY, 'F9 native build mismatch')
    need(value['pngFile'] == path.with_suffix('.png').name and HEX.fullmatch(value['pngSha256']), 'F9 PNG basename/hash field mismatch')
    for key in ('requestFrame', 'captureFrame', 'width', 'height', 'observerPlayer'):
        integer(value[key])
    need(value['captureFrame'] == int(name[1]) and value['requestFrame'] <= value['captureFrame'], 'F9 rendered frame mismatch')
    need(value['matchId'] == value['requestMatchId'] and isinstance(value['matchId'], str), 'F9 changed match between request and frame end')
    for key in ('requestWorldTime', 'worldTime', 'hudWorldTime'):
        number(value[key], 0)
    need(value['requestWorldTime'] <= value['worldTime'] + .001, 'F9 time moved backwards within frame')
    for key in ('inMenu', 'paused', 'hasPlayer', 'generatedTerrain', 'requiresTransport'):
        shape(value[key], 'bool')
    shape(value['replay'], REPLAY_SPEC); shape(value['lan'], LAN_SPEC); shape(value['resources'], COST)
    shape(value['camera'], {'focus': V2, 'pointer': V2, 'applicationFocused': 'bool', 'orthographicSize': 'num'})
    shape(value['scenario'], {'authored': 'bool', 'editorOpen': 'bool', 'playtest': 'bool', 'id': 'str', 'title': 'str',
                              'contentHash': 'str', 'draftHash': 'str', 'winner': 'int',
                              'objectives': (OBJECTIVE_STATE, 0, 32), 'triggers': (TRIGGER_STATE, 0, 64)})
    scenario = value['scenario']
    need(not scenario['editorOpen'] or HEX.fullmatch(scenario['draftHash']), 'Editor capture lacks draft hash')
    need(not scenario['authored'] or HEX.fullmatch(scenario['contentHash']), 'Authored capture lacks world hash')
    need(not scenario['playtest'] or scenario['authored'], 'Playtest is not authored world')
    for trigger in scenario['triggers']:
        integer(trigger['fires'], 0, 64); number(trigger['nextAt'], 0)
    need(len({t['id'] for t in scenario['triggers']}) == len(scenario['triggers']), 'Duplicate runtime trigger IDs')
    identity = value['replay']['identity']
    need(identity['appVersion'] == VERSION and identity['buildIdentity'] == GUID and identity['unityVersion'] == UNITY and
         identity['rulesHash'] == RULES_HASH and identity['scenarioFormatVersion'] == identity['scenarioRuntimeVersion'] == 1,
         'F9 replay/runtime identity mismatch')
    utc_value(value['utc'])
    png = path.with_suffix('.png'); pixels = read(png, MAX_STORED)
    dimensions = png_bytes(pixels)
    need(dimensions == (value['width'], value['height']) and sha(pixels) == value['pngSha256'], 'F9 PNG dimensions/SHA mismatch')
    return {**record(path, raw), 'png': record(png, pixels), 'status': 'Passed', 'utc': value['utc'],
            'frame': value['captureFrame'], 'matchId': value['matchId'], 'worldTime': value['worldTime'],
            'completedStep': value['replay']['currentStep'], 'observerPlayer': value['observerPlayer'],
            'inMenu': value['inMenu'], 'paused': value['paused'], 'applicationFocused': value['camera']['applicationFocused'],
            'scenario': scenario}, value


def decode_save(raw):
    need(len(raw) <= MAX_STORED, 'Checkpoint stored size bound')
    envelope = loads(raw)
    shape(envelope, {'schema': 'int', 'payload': 'largeStr', 'sha256': 'str', 'encoding': 'str', 'payloadBase64': 'largeStr'})
    need(HEX.fullmatch(envelope['sha256']), 'Checkpoint SHA field invalid')
    if envelope['schema'] == 1:
        need(envelope['encoding'] == envelope['payloadBase64'] == '', 'Legacy checkpoint envelope ambiguity')
        payload = envelope['payload'].encode('utf-8', 'strict')
    else:
        need(envelope['schema'] == 2 and envelope['encoding'] == 'gzip-base64' and envelope['payload'] == '', 'Unsupported checkpoint encoding')
        encoded = base64.b64decode(envelope['payloadBase64'], validate=True)
        need(len(encoded) >= 18 and encoded[:4] == b'\x1f\x8b\x08\x00', 'Checkpoint requires one gzip member without optional header')
        inflater = zlib.decompressobj(-15)
        payload = inflater.decompress(encoded[10:], MAX_PAYLOAD + 1)
        need(len(payload) <= MAX_PAYLOAD and inflater.eof and not inflater.unconsumed_tail and len(inflater.unused_data) == 8,
             'Truncated/concatenated/trailing or oversized checkpoint gzip')
        crc, size = struct.unpack('<II', inflater.unused_data)
        need(crc == zlib.crc32(payload) & 0xffffffff and size == len(payload) & 0xffffffff, 'Checkpoint gzip CRC/ISIZE mismatch')
    need(0 < len(payload) <= MAX_PAYLOAD and sha(payload) == envelope['sha256'], 'Checkpoint payload SHA/size mismatch')
    return loads(payload), envelope['sha256']


def checkpoint(path, documents):
    raw = read(path, MAX_STORED)
    saved, payload_hash = decode_save(raw)
    need(isinstance(saved, dict) and saved.get('appVersion') == VERSION and isinstance(saved.get('matchId'), str) and saved['matchId'], 'Checkpoint version/match missing')
    world = saved.get('world')
    need(isinstance(world, dict) and type(world.get('schema')) is int and world['schema'] == 4 and world.get('hasScenarioDocument') is True, 'Checkpoint must embed schema4 authored world')
    doc = document(world['scenarioDocument']); state = world['scenarioState']; content_hash = world['scenarioHash']
    number(world['time'], 0); integer(world['winner'], -2, len(doc['starts']) - 1)
    integer(saved['simulation']['stepCount'])
    need(HEX.fullmatch(content_hash) and state['documentHash'] == content_hash and doc['rulesHash'] == world['rulesHash'] == RULES_HASH,
         'Checkpoint embedded scenario identity mismatch')
    matches = [name for name, (facts, reference) in documents.items() if facts['contentHash'] == content_hash and reference == doc]
    need(matches, 'No independently SHA-verified portable document matches entire embedded checkpoint DTO')
    shape(state, {'schema': 'int', 'winner': 'int', 'documentHash': 'str', 'reason': 'str', 'scenarioVictory': 'bool',
                  'objectiveStates': (OBJECTIVE_STATE, 0, 32), 'triggerStates': (TRIGGER_STATE, 0, 64),
                  'bindings': ({'objectId': 'str', 'entityIds': ('int', 0, 100000)}, 0, 100000)})
    need(state['schema'] == 1 and state['winner'] == world['winner'], 'Checkpoint runtime result mismatch')
    need([t['id'] for t in state['triggerStates']] == [t['id'] for t in doc['triggers']], 'Checkpoint trigger roster mismatch')
    need([o['id'] for o in state['objectiveStates']] == [o['id'] for o in doc['objectives']], 'Checkpoint objective roster mismatch')
    for live, definition in zip(state['triggerStates'], doc['triggers']):
        integer(live['fires'], 0, definition['maxFires']); number(live['nextAt'], 0)
    events = world['events']; need(isinstance(events, list) and len(events) <= 1000000, 'Checkpoint event bound')
    for event in events:
        need(isinstance(event, dict) and isinstance(event.get('kind'), str), 'Malformed checkpoint event')
        number(event.get('time'), 0, world['time'] + .01)
        integer(event.get('schemaVersion'), 2, 2); shape(event.get('hasEconomy'), 'bool')
        if event['hasEconomy']:
            need(isinstance(event.get('economy'), dict), 'Missing event economy transaction')
            for key in ('requestedDelta', 'actualDelta', 'balanceBefore', 'balanceAfter'):
                shape(event['economy'].get(key), COST)
    grants = [event for event in events if event['kind'] == 'ScenarioResourceGranted']
    summaries = [{'eventIndex': i, 'player': ev.get('player'), 'time': ev.get('time'), 'amount': ev.get('amount'),
                  'grantedDelta': ev.get('economy', {}).get('actualDelta'),
                  'economy': ev.get('economy'), 'message': ev.get('message')} for i, ev in enumerate(events) if ev['kind'] == 'ScenarioResourceGranted']
    return {**record(path, raw), 'status': 'Passed', 'payloadSha256': payload_hash, 'contentHash': content_hash,
            'matchedPortableFiles': matches, 'matchId': saved['matchId'], 'worldTime': world['time'], 'winner': world['winner'],
            'completedStep': saved['simulation'].get('completedStep', saved['simulation'].get('stepCount')),
            'triggers': state['triggerStates'], 'objectives': state['objectiveStates'], 'eventCount': len(events),
            'scenarioResourceGrantedCount': len(grants), 'scenarioGrants': summaries}, saved


def configure(candidate, receipt):
    global CANDIDATE, NATIVE, APP, PROFILE, GUID, VERSION, UNITY, SOURCE_FP, SOURCE_COMMIT, RULES_HASH, SOURCE_INPUTS
    global IDENTITY_RECEIPT, IDENTITY_FACTS, IDENTITY_DATA
    if candidate is not None:
        CANDIDATE = Path(candidate).resolve(strict=True)
        NATIVE = CANDIDATE / 'Native'
    need(CANDIDATE.is_dir() and NATIVE.is_dir(), 'Candidate/Native directories must already exist')
    IDENTITY_RECEIPT = inside(receipt, CANDIDATE)
    raw = read(IDENTITY_RECEIPT); value = loads(raw)
    need(isinstance(value, dict) and type(value.get('schema')) is int and value['schema'] == 1, 'Identity receipt schema1 required')
    for key in ('appVersion', 'buildGuid', 'unityVersion', 'runtimeCommit', 'sourceFingerprint', 'sourceInputs', 'rulesHash'):
        need(key in value, 'Missing identity receipt field: ' + key)
    need(isinstance(value['appVersion'], str) and re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+', value['appVersion']), 'Invalid application version')
    need(isinstance(value['buildGuid'], str) and re.fullmatch(r'[0-9a-f]{32}', value['buildGuid']), 'Invalid lowercase build GUID')
    need(isinstance(value['runtimeCommit'], str) and re.fullmatch(r'[0-9a-f]{40}(?:[0-9a-f]{24})?', value['runtimeCommit']), 'Full frozen Git commit ID required')
    need(isinstance(value['sourceFingerprint'], str) and HEX.fullmatch(value['sourceFingerprint']) and isinstance(value['rulesHash'], str) and HEX.fullmatch(value['rulesHash']), 'Invalid source/rules hash')
    need(isinstance(value['unityVersion'], str) and 1 <= len(value['unityVersion']) <= 64, 'Invalid Unity version')
    integer(value['sourceInputs'], 1, 1000000)
    GUID, VERSION, UNITY = value['buildGuid'], value['appVersion'], value['unityVersion']
    SOURCE_FP, SOURCE_COMMIT, SOURCE_INPUTS, RULES_HASH = value['sourceFingerprint'], value['runtimeCommit'], value['sourceInputs'], value['rulesHash']
    APP = inside(value.get('appRelativePath', 'Build/Frontier Reckoning.app'), CANDIDATE)
    PROFILE = inside(value.get('profileRelativePath', 'Native/profile'))
    need(APP.name.endswith('.app') and APP.is_dir(), 'Identity app must be an existing candidate .app')
    need(PROFILE.is_dir(), 'Owned Native profile directory must exist')
    IDENTITY_DATA = value; IDENTITY_FACTS = record(IDENTITY_RECEIPT, raw)


def app_identity():
    boot = APP / 'Contents/Resources/Data/boot.config'; raw = read(boot, 65536)
    matches = re.findall(rb'^build-guid=([0-9a-f]{32})\s*$', raw, re.M)
    need(matches == [GUID.encode()], 'Candidate native boot GUID mismatch')
    info_path = APP / 'Contents/Info.plist'; info_raw = read(info_path, 1024 * 1024); info = plistlib.loads(info_raw)
    version = info.get('CFBundleShortVersionString', info.get('CFBundleVersion'))
    need(version == VERSION, 'App Info.plist version mismatch')
    executable = APP / 'Contents/MacOS/Frontier Reckoning'
    runtime = APP / 'Contents/Resources/Data/Managed/Frontier.Runtime.dll'
    return {'appVersion': VERSION, 'buildGuid': GUID, 'unityVersion': UNITY, 'expectedSourceFingerprint': SOURCE_FP,
            'expectedSourceCommit': SOURCE_COMMIT, 'boot': record(boot, raw), 'plist': record(info_path, info_raw),
            'executable': record(executable, limit=MAX_STORED), 'compiledRuntime': record(runtime, limit=MAX_STORED),
            'sourceBindingScope': 'Expected frozen identity plus root process receipt; this script does not reconstruct Unity compilation.'}


def ui_image(raw):
    if raw[:8] == b'\x89PNG\r\n\x1a\n':
        width, height = png_bytes(raw)
        return {'mediaType': 'image/png', 'width': width, 'height': height, 'validation': 'PNG chunks/CRC/IHDR/IEND'}
    need(0 < len(raw) <= MAX_STORED and raw[:2] == b'\xff\xd8' and raw[-2:] == b'\xff\xd9', 'Invalid UI JPEG signature/EOI/size')
    offset, dimensions, segments = 2, None, 0
    while offset < len(raw) - 2:
        need(raw[offset] == 255, 'Invalid JPEG marker boundary')
        while offset < len(raw) and raw[offset] == 255:
            offset += 1
        need(offset < len(raw), 'Truncated JPEG marker')
        marker = raw[offset]; offset += 1; segments += 1
        need(segments <= 10000 and marker != 0, 'JPEG header marker/count bound')
        if marker in (0xd8, 0xd9, 0x01) or 0xd0 <= marker <= 0xd7:
            continue
        need(offset + 2 <= len(raw), 'Truncated JPEG segment length')
        length = struct.unpack_from('>H', raw, offset)[0]
        need(length >= 2 and offset + length <= len(raw), 'Truncated JPEG segment')
        body = raw[offset + 2:offset + length]
        if marker in (0xc0, 0xc1, 0xc2):
            need(dimensions is None and len(body) >= 6, 'Invalid/duplicate JPEG frame header')
            precision = body[0]; height, width = struct.unpack_from('>HH', body, 1); components = body[5]
            need(precision in (8, 12, 16) and 1 <= components <= 4 and len(body) == 6 + 3 * components, 'Invalid JPEG SOF component layout')
            integer(width, 1, 16384); integer(height, 1, 16384); dimensions = width, height
        offset += length
        if marker == 0xda:
            need(dimensions is not None, 'JPEG scan precedes frame')
            width, height = dimensions
            return {'mediaType': 'image/jpeg', 'width': width, 'height': height, 'validation': 'JPEG SOI/SOF dimensions/segment bounds/SOS/final EOI; no pixel decode'}
    raise ValueError('JPEG has no frame/scan')


def root_receipt(path, kind):
    path = inside(path); raw = read(path); value = loads(raw)
    need(isinstance(value, dict) and value.get('schema', 1) == 1, 'Invalid root receipt')
    if kind == 'process' and 'args' in value:
        args = value['args']; need(isinstance(args, list) and all(isinstance(x, str) for x in args), 'Invalid process arguments')
        need(args and Path(args[0]).resolve() == (APP / 'Contents/MacOS/Frontier Reckoning').resolve(), 'Process receipt executable mismatch')
        need('-frontierDataDirectory' in args, 'Process receipt data root missing')
        data_root = inside(args[args.index('-frontierDataDirectory') + 1])
        allowed_roots = [PROFILE] + ([REPLAY_PROFILE] if REPLAY_PROFILE is not None else [])
        need(data_root in allowed_roots, 'Process receipt data root mismatch')
        if data_root == REPLAY_PROFILE:
            need('-frontierReplay' in args, 'Extra exact replay profile requires explicit replay input')
            replay_input = inside(args[args.index('-frontierReplay') + 1])
            need(replay_input.parent == PROFILE / 'Replays', 'Cold replay input must be an owned original recording')
        if 'sourceCommit' in value or 'sourceFingerprint' in value:
            need(value.get('sourceCommit') == SOURCE_COMMIT and value.get('sourceFingerprint') == SOURCE_FP, 'Declared process source receipt mismatch')
        integer(value['pid'], 1); utc_value(value['startedAtUtc'])
    if 'buildGuid' in value:
        need(value['buildGuid'] == GUID, 'Root receipt GUID mismatch')
    if 'exitedAtUtc' in value:
        end = utc_value(value['exitedAtUtc'])
        if 'startedAtUtc' in value:
            need(end >= utc_value(value['startedAtUtc']), 'Root lifecycle time order mismatch')
    if 'exitCode' in value:
        integer(value['exitCode'], -128, 255)
    ui_files = []
    if kind == 'observation':
        need(isinstance(value.get('observations'), list), 'Root observation array required')
        for row in value['observations']:
            need(isinstance(row, dict), 'Malformed root observation')
            for key in ('screenshot', 'screenshotFile', 'uiFile', 'image', 'file'):
                name = row.get(key)
                if isinstance(name, str) and name.lower().endswith(('.png', '.jpg', '.jpeg')):
                    reference = Path(name)
                    if not reference.is_absolute() and not name.startswith('Native/'):
                        reference = NATIVE / reference
                    p = inside(reference); pixels = read(p, MAX_STORED); media = ui_image(pixels); ui_files.append({**record(p, pixels), **media})
    return {**record(path, raw), 'kind': kind, 'inputStructure': 'Passed',
            'observationCount': len(value.get('observations', [])), 'reportedPid': value.get('pid'),
            'sourceBindingStatus': 'DeclaredExpectedSource' if 'sourceCommit' in value and 'sourceFingerprint' in value else 'NotReportedByThisReceipt',
            'reportedExitCode': value.get('exitCode', value.get('exit_code')), 'boundUiFiles': ui_files,
            'dataProfile': str(data_root.relative_to(CANDIDATE)) if kind == 'process' and 'args' in value else None,
            'reportedFailureCount': len(value.get('failures', [])), 'reportedFailures': value.get('failures', []),
            'scope': 'Root-authored receipt is preserved as an input. No independent GUI/action/process-exit assertion is created.'}


def phase_plan(plan, captures, documents, checkpoints):
    phases = []
    for item in plan.get('phases', []):
        need(isinstance(item, dict) and isinstance(item.get('id'), str), 'Invalid phase mapping')
        sources = item.get('captures', [])
        need(isinstance(sources, list) and all(isinstance(x, str) for x in sources), 'Invalid phase capture paths')
        rows = [captures.get(name) for name in sources]
        doc_sources = item.get('portableFiles', [])
        need(isinstance(doc_sources, list) and all(isinstance(x, str) for x in doc_sources), 'Invalid portable phase paths')
        doc_rows = [documents.get(name) for name in doc_sources]
        tests = []
        if not (sources or doc_sources) or any(row is None for row in rows + doc_rows):
            status = 'pending'
        else:
            for row in rows:
                value = row[1]; expected = item.get('expect', {})
                need(isinstance(expected, dict), 'Invalid explicit phase expectations')
                for key, wanted in expected.items():
                    actual = value
                    for part in key.split('.'):
                        need(isinstance(actual, dict) and part in actual, 'Unknown phase assertion field')
                        actual = actual[part]
                    tests.append({'field': key, 'expected': wanted, 'actual': actual, 'ok': actual == wanted})
            for row in doc_rows:
                expected = item.get('portableExpect', {})
                need(isinstance(expected, dict), 'Invalid portable assertions')
                for key, wanted in expected.items():
                    need(key in row[0], 'Unknown portable assertion field')
                    actual = row[0][key]
                    tests.append({'field': 'portable.' + key, 'expected': wanted, 'actual': actual, 'ok': actual == wanted})
                for key, capture_key in (('documentHashEqualsCaptureDraft', 'draftHash'), ('documentHashEqualsWorld', 'contentHash')):
                    if item.get(key) is True:
                        need(rows, 'Document/capture comparison needs capture evidence')
                        actual = rows[-1][1]['scenario'][capture_key]
                        tests.append({'field': key, 'expected': row[0]['contentHash'], 'actual': actual, 'ok': actual == row[0]['contentHash']})
            for key, state_array, state_field in (('triggerFires', 'triggers', 'fires'), ('objectiveStates', 'objectives', 'status')):
                assertions = item.get(key, {})
                need(isinstance(assertions, dict), 'Invalid runtime state assertions')
                if assertions:
                    need(rows, 'Runtime assertions need a mapped capture')
                    state = {r['id']: r for r in rows[-1][1]['scenario'][state_array]}
                    for identifier, wanted in assertions.items():
                        actual = state.get(identifier, {}).get(state_field)
                        tests.append({'field': state_array + '.' + identifier + '.' + state_field, 'expected': wanted, 'actual': actual, 'ok': actual == wanted})
                        if state_array == 'triggers':
                            tests.append({'field': state_array + '.' + identifier + '.lastError', 'ok': state.get(identifier, {}).get('lastError') == ''})
            hashes = item.get('draftHashes')
            if hashes is not None:
                need(isinstance(hashes, list) and len(hashes) == len(rows), 'Draft sequence mapping length mismatch')
                actual = [row[1]['scenario']['draftHash'] for row in rows]
                tests.append({'field': 'explicitDraftHashSequence', 'expected': hashes, 'actual': actual, 'ok': actual == hashes})
                ordered = all(utc_value(a[1]['utc']) < utc_value(b[1]['utc']) and a[1]['captureFrame'] < b[1]['captureFrame'] for a, b in zip(rows, rows[1:]))
                tests.append({'field': 'orderedCaptureFramesAndUtc', 'ok': ordered})
            status = 'Passed' if all(test['ok'] for test in tests) and tests else 'pending' if not tests else 'Failed'
        phases.append({'id': item['id'], 'metadataStatus': status, 'captures': sources, 'portableFiles': doc_sources, 'checks': tests,
                       'actualGuiStatus': 'pending', 'scope': 'Only mapped metadata assertions are evaluated; root GUI evidence remains separate.'})
    comparison = {'status': 'pending', 'scope': 'Requires named before and after checkpoints, unchanged embedded scenario/match, actual advancing simulation, equal grant count and runtime fires.'}
    pair = plan.get('checkpointComparison')
    if isinstance(pair, dict) and pair.get('before') in checkpoints and pair.get('after') in checkpoints:
        before, saved_a = checkpoints[pair['before']]; after, saved_b = checkpoints[pair['after']]
        events_a = saved_a['world']['events']; events_b = saved_b['world']['events']
        checks = {'sameScenarioHash': before['contentHash'] == after['contentHash'], 'sameMatch': before['matchId'] == after['matchId'],
                  'timeAdvanced': after['worldTime'] > before['worldTime'], 'stepsAdvanced': after['completedStep'] > before['completedStep'],
                  'eventPrefixRetained': events_b[:len(events_a)] == events_a,
                  'rewardCountUnchanged': before['scenarioResourceGrantedCount'] > 0 and before['scenarioResourceGrantedCount'] == after['scenarioResourceGrantedCount'],
                  'triggerErrorsEmpty': all(t['lastError'] == '' for t in after['triggers'])}
        if isinstance(pair.get('rewardCount'), int):
            checks['explicitExpectedRewardCount'] = before['scenarioResourceGrantedCount'] == pair['rewardCount']
        trigger = pair.get('triggerId')
        if isinstance(trigger, str) and trigger:
            checks['explicitOnceTriggerBeforeAndAfter'] = all(any(t['id'] == trigger and t['fires'] == 1 for t in row['triggers']) for row in (before, after))
        else:
            checks['allTriggerFiresUnchanged'] = [(t['id'], t['fires']) for t in before['triggers']] == [(t['id'], t['fires']) for t in after['triggers']]
        deleted = pair.get('deletedSource')
        if isinstance(deleted, str) and deleted:
            path = inside(deleted); checks['declaredSourceAbsentAtInspection'] = not path.exists()
        comparison = {'status': 'Passed' if all(checks.values()) else 'Failed', 'before': pair['before'], 'after': pair['after'],
                      'checks': checks, 'scope': 'Independent saved-state comparison. Absent source alone does not prove when/why deletion occurred; root lifecycle/actions remain separate.'}
    return phases, comparison


def verify(plan, processes, observations):
    need(IDENTITY_FACTS is not None, 'Explicit identity receipt required')
    identity = app_identity()
    for key in ('executable', 'compiledRuntime'):
        if key in IDENTITY_DATA:
            declared = IDENTITY_DATA[key]
            need(isinstance(declared, dict) and declared.get('bytes') == identity[key]['bytes'] and declared.get('sha256') == identity[key]['sha256'], 'Build receipt frozen binary mismatch: ' + key)
    captures, documents, checkpoints = {}, {}, {}
    profiles = [PROFILE] + ([REPLAY_PROFILE] if REPLAY_PROFILE is not None else [])
    for owned_profile in profiles:
        for path in sorted((owned_profile / 'Captures').glob('*.json')):
            need(len(captures) < 2048, 'Capture count bound')
            try:
                facts, value = capture(path)
                facts['dataProfile'] = owned_profile.relative_to(CANDIDATE).as_posix()
                captures[path.relative_to(CANDIDATE).as_posix()] = facts, value
            except (ValueError, KeyError, TypeError) as error:
                raise ValueError(path.relative_to(CANDIDATE).as_posix() + ': ' + str(error)) from error
    for directory in (PROFILE / 'Scenarios', PROFILE / 'ScenarioDrafts', PROFILE / 'ScenarioExports', NATIVE / 'AuthoredStages'):
        for path in sorted((NATIVE / directory).glob('*.json')):
            need(len(documents) < 2048, 'Portable document count bound')
            try:
                documents[path.relative_to(CANDIDATE).as_posix()] = portable(path)
            except (ValueError, KeyError, TypeError) as error:
                raise ValueError(path.relative_to(CANDIDATE).as_posix() + ': ' + str(error)) from error
    for directory in (PROFILE / 'Saves', NATIVE / 'Checkpoint-fixtures'):
        for path in sorted((NATIVE / directory).glob('*.json')):
            need(len(checkpoints) < 64, 'Checkpoint count bound')
            try:
                checkpoints[path.relative_to(CANDIDATE).as_posix()] = checkpoint(path, documents)
            except (ValueError, KeyError, TypeError) as error:
                raise ValueError(path.relative_to(CANDIDATE).as_posix() + ': ' + str(error)) from error
    roots = [root_receipt(path, 'process') for path in processes] + [root_receipt(path, 'observation') for path in observations]
    phases, comparison = phase_plan(plan, captures, documents, checkpoints)
    cold_replay, replay_facts = verify_cold_replay(captures, documents, processes) if REPLAY_PROFILE is not None else ({'status': 'NotRequested'}, [])
    source_review = {'status': 'NotProvided'}
    if plan.get('sourceReview'):
        source_path = inside(plan['sourceReview']); source_raw = read(source_path); source = loads(source_raw)
        need(source.get('result') == 'Passed' and source.get('sourceCommit') == SOURCE_COMMIT and source.get('sourceFingerprint') == SOURCE_FP and source.get('sourceInputs') == SOURCE_INPUTS, 'Frozen source review identity mismatch')
        input_hashes = source['frozenInputHashes']
        need(isinstance(input_hashes, dict) and len(input_hashes) == SOURCE_INPUTS and sha(json.dumps(input_hashes, sort_keys=True, separators=(',', ':')).encode()) == SOURCE_FP, 'Frozen source input-map fingerprint mismatch')
        source_review = {**record(source_path, source_raw), 'status': 'Passed', 'sourceCommit': SOURCE_COMMIT, 'sourceFingerprint': SOURCE_FP, 'sourceInputs': SOURCE_INPUTS, 'scope': 'Bound separate independent frozen-Git review; current working tree is not used.'}
    root_defects = sum(row.get('reportedFailureCount', 0) for row in roots)
    if 'RootDiagnosticDefects' in plan:
        need(root_defects == plan['RootDiagnosticDefects'], 'Expected root diagnostic failure count mismatch')
    facts = replay_facts + [identity[key] for key in ('boot', 'plist', 'executable', 'compiledRuntime')] + [IDENTITY_FACTS]
    facts += [row[0] for row in captures.values()] + [row[0]['png'] for row in captures.values()]
    facts += [row[0] for row in documents.values()] + [row[0] for row in checkpoints.values()] + roots
    facts += [item for root in roots for item in root.get('boundUiFiles', [])]
    if source_review.get('status') == 'Passed':
        facts.append(source_review)
    need(sum(item['bytes'] for item in facts) <= MAX_TOTAL, 'Total input byte bound')
    for item in facts:
        need(record(CANDIDATE / item['file'], limit=MAX_PAYLOAD)['sha256'] == item['sha256'], 'An inspected input changed during review')
    failed = any(p['metadataStatus'] == 'Failed' for p in phases) or comparison['status'] == 'Failed' or cold_replay['status'] == 'Failed'
    return {'schema': 1, 'createdUtc': utc(), 'metadataInspection': 'Failed' if failed else 'Passed',
            'identity': identity, 'identityReceiptInput': IDENTITY_FACTS, 'captures': [row[0] for row in captures.values()],
            'portableDocuments': [row[0] for row in documents.values()], 'checkpoints': [row[0] for row in checkpoints.values()],
            'rootReceiptInputs': roots, 'plannedPhases': phases, 'checkpointComparison': comparison, 'coldReplayComparison': cold_replay,
            'captureProfileRoots': [p.relative_to(CANDIDATE).as_posix() for p in profiles],
            'inputsUnchangedDuringInspection': True, 'frozenSourceReviewInput': source_review,
            'RootDiagnosticDefects': root_defects, 'GUIoverall': 'FailedAsReportedByRoot' if root_defects else 'NotIndependentlyObserved',
            'candidateDisposition': plan.get('candidateDisposition', 'NotDeclared'),
            'nativeAcceptance': 'PreFixDiagnosticOnly' if root_defects or plan.get('candidateDisposition', '').startswith('pre-fix') else 'pending',
            'mediaCorrection': 'F9 requires actual PNG. Root UI PNG/JPEG are checked by actual signature; .jpg and legacy .png-named JPEG are supported without conversion or alteration. JPEG headers/SOF/EOI checks are not pixel decoding.',
            'scope': ['Read-only explicitly identity-bound candidate F9/file/saved-state evidence; no Unity, GUI, simulation or packaging is run.',
                      'Mapped metadata can pass while missing GUI observations or later lifecycle phases remain pending.',
                      'Process args/source/exit fields are root receipt claims; no OS lifecycle or independent GUI certification.',
                      'Scenario format/rules/terrain/ID bounds are checked; runtime geography/catalog/playability rely on actual runtime/tests.',
                      'Old 0.11 archive, physical LAN, natural full-match, transport and foreground performance are outside this tool.']}



def raw_object_parts(text):
    """Extract exact JSON child token bytes; Unity float spelling is preserved."""
    decoder = json.JSONDecoder(); parts = {}; pos = 1
    need(text.startswith('{') and text.endswith('}'), 'Raw JSON object required')
    while True:
        while text[pos].isspace(): pos += 1
        if text[pos] == '}': break
        key, pos = decoder.raw_decode(text, pos)
        while text[pos].isspace(): pos += 1
        need(text[pos] == ':', 'Raw JSON property separator'); pos += 1
        while text[pos].isspace(): pos += 1
        start = pos; _, pos = decoder.raw_decode(text, pos)
        need(key not in parts, 'Duplicate raw JSON key'); parts[key] = text[start:pos]
        while text[pos].isspace(): pos += 1
        if text[pos] == '}': break
        need(text[pos] == ',', 'Raw JSON property delimiter'); pos += 1
    need(pos == len(text)-1, 'Raw JSON object trailing data')
    return parts


def raw_array_parts(text, maximum):
    decoder = json.JSONDecoder(); parts = []; pos = 1
    need(text.startswith('[') and text.endswith(']'), 'Raw JSON array required')
    while True:
        while text[pos].isspace(): pos += 1
        if text[pos] == ']': break
        need(len(parts) < maximum, 'Raw array token bound')
        start = pos; _, pos = decoder.raw_decode(text, pos); parts.append(text[start:pos])
        while text[pos].isspace(): pos += 1
        if text[pos] == ']': break
        need(text[pos] == ',', 'Raw JSON array delimiter'); pos += 1
    need(pos == len(text)-1, 'Raw JSON array trailing data')
    return parts


def verify_cold_replay(captures, documents, processes):
    before_path = NATIVE / 'replay-before-cold-view.json'; after_path = NATIVE / 'replay-after-cold-view.json'
    before_raw = read(before_path); after_raw = read(after_path)
    before = loads(before_raw); after = loads(after_raw)
    need(before.get('schema') == after.get('schema') == 1, 'Replay root receipt schema mismatch')
    need(inside(before['isolatedProfile']) == REPLAY_PROFILE and before['profileFiles'] == [], 'Replay before receipt must report exact fresh empty profile')
    need(utc_value(before['atUtc']) < utc_value(after['atUtc']), 'Replay root receipt time order')
    source = inside(before['sourceReplay']); need(source.parent == PROFILE / 'Replays', 'Replay source must be original owned recording')
    raw = read(source, MAX_STORED); replay, payload_hash = decode_save(raw); source_facts = record(source, raw)
    need(before['bytes'] == after['sourceBytes'] == len(raw) and before['sha256'] == after['sourceSha256'] == sha(raw) and after['sourceReplayUnchanged'] is True, 'Replay before/after file SHA binding mismatch')
    # Strict envelope decode already bounded and verified gzip CRC/SHA. Reinflate
    # only those validated bytes to retain raw Unity JSON lexical tokens for chains.
    envelope = loads(raw)
    if envelope['schema'] == 1:
        payload = envelope['payload'].encode('utf-8', 'strict')
    else:
        inflater = zlib.decompressobj(-15)
        payload = inflater.decompress(base64.b64decode(envelope['payloadBase64'], validate=True)[10:], MAX_PAYLOAD+1)
    need(sha(payload) == payload_hash and len(payload) <= MAX_PAYLOAD, 'Replay raw payload does not match strict decoded SHA')
    text = payload.decode('utf-8', 'strict'); parts = raw_object_parts(text)
    fields = set('schema segmentId matchId startKind finishReason identity incomplete incompleteReason recordLimit eventLimit anchorInterval anchors records events'.split())
    need(isinstance(replay, dict) and set(replay) == fields and replay['schema'] == 1, 'Replay DTO schema/fields mismatch')
    need(replay['startKind'] == before['startKind'] == 'loaded_checkpoint' and replay['finishReason'] == before['finishReason'] == 'match_finished' and replay['incomplete'] is before['incomplete'] is False and replay['incompleteReason'] == '', 'Cold replay segment scope/status mismatch')
    shape(replay['identity'], REPLAY_SPEC['identity'])
    identity = replay['identity']
    need(identity['appVersion'] == VERSION and identity['buildIdentity'] == GUID and identity['unityVersion'] == UNITY and identity['rulesHash'] == RULES_HASH and identity['scenarioFormatVersion'] == identity['scenarioRuntimeVersion'] == 1, 'Cold replay source identity mismatch')
    integer(replay['recordLimit'], 1, 200000); integer(replay['eventLimit'], 1, 1000000); integer(replay['anchorInterval'], 1, 3000)
    anchors, records, events = replay['anchors'], replay['records'], replay['events']
    need(isinstance(anchors, list) and 0 < len(anchors) <= 10000 and isinstance(records, list) and len(records) <= replay['recordLimit'] and isinstance(events, list) and len(events) <= replay['eventLimit'], 'Replay array bounds')
    need((len(anchors), len(records), len(events)) == (before['anchors'], before['records'], before['events']), 'Replay reported collection counts mismatch')
    need(anchors[0]['recordIndex'] == 0 and anchors[-1]['recordIndex'] == len(records) and anchors[-1]['eventCount'] == len(events), 'Replay initial/final stream boundaries mismatch')
    raw_events = raw_array_parts(parts['events'], replay['eventLimit']); raw_records = raw_array_parts(parts['records'], replay['recordLimit']); raw_anchors = raw_array_parts(parts['anchors'], 10000)
    event_hashes = [sha(b'frontier-replay-events-v1')]
    for raw_event, event in zip(raw_events, events):
        need(isinstance(event, dict) and event.get('schemaVersion') == 2 and isinstance(event.get('kind'), str), 'Cold replay event schema')
        number(event['time'], 0, anchors[-1]['world']['time'] + .01)
        event_hashes.append(sha((event_hashes[-1]+'\n'+raw_event).encode('utf-8')))
    input_hashes = [sha(b'frontier-replay-inputs-v1')]
    last_step = anchors[0]['completedStep']; ordinal = -1; event_count = anchors[0]['eventCount']
    for raw_record, item in zip(raw_records, records):
        integer(item['completedStep'], last_step, anchors[-1]['completedStep']); integer(item['kind'], 0, 3)
        expected = ordinal+1 if item['completedStep'] == last_step else 0
        need(item['ordinal'] == expected, 'Cold replay record ordinal mismatch')
        integer(item['eventCount'], event_count, len(events)); need(item['eventHash'] == event_hashes[item['eventCount']], 'Cold replay record event-chain mismatch')
        last_step, ordinal, event_count = item['completedStep'], item['ordinal'], item['eventCount']
        input_hashes.append(sha((input_hashes[-1]+'\n'+raw_record).encode('utf-8')))
    summaries = []; previous = (-1,-1,-1)
    for anchor, token in zip(anchors, raw_anchors):
        step, index, count = anchor['completedStep'], anchor['recordIndex'], anchor['eventCount']
        integer(step, 0); integer(index, 0, len(records)); integer(count, 0, len(events))
        need(step > previous[0] and index >= previous[1] and count >= previous[2], 'Cold replay nonmonotonic anchors')
        need(anchor['eventHash'] == event_hashes[count] and anchor['inputHash'] == input_hashes[index], 'Cold replay anchor event/input chain mismatch')
        world = anchor['world']; doc = document(world['scenarioDocument']); content_hash = world['scenarioHash']; state = world['scenarioState']
        need(world['schema'] == 4 and world['hasScenarioDocument'] is True and world['rulesHash'] == RULES_HASH and anchor['simulation']['stepCount'] == step, 'Cold replay embedded world/runtime step identity mismatch')
        matches = [name for name,(facts,reference) in documents.items() if facts['contentHash'] == content_hash and reference == doc]
        need(matches and state['documentHash'] == content_hash and state['winner'] == world['winner'], 'Cold replay complete embedded portable DTO mismatch')
        a = raw_object_parts(token)
        projection = '{"world":'+a['world']+',"simulation":'+a['simulation']+',"hasGuided":'+a['hasGuided']+',"guided":'+a['guided']+',"eventHash":'+a['eventHash']+'}'
        need(sha(projection.encode('utf-8')) == anchor['stateDigest'], 'Cold replay exact raw JSON projection state digest mismatch')
        summaries.append({'completedStep':step, 'worldTime':world['time'], 'recordIndex':index, 'eventCount':count, 'contentHash':content_hash, 'winner':world['winner'], 'scenarioReason':state['reason'], 'scenarioVictory':state['scenarioVictory'], 'triggers':state['triggerStates'], 'objectives':state['objectiveStates'], 'matchedPortableFiles':matches, 'exactRawEventInputAndStateDigests':'Passed'})
        previous = step,index,count
    selected = sorted([row for row in captures.values() if row[0]['dataProfile'] == REPLAY_PROFILE.relative_to(CANDIDATE).as_posix()], key=lambda row:utc_value(row[1]['utc']))
    need(len(selected) >= 3, 'Cold replay needs actual start/play/end captures')
    for facts, value in selected:
        r = value['replay']; need(r['watching'] is True and r['recording'] is False and r['seek'] is False and r['incomplete'] is False and all(r[k] == '' for k in ('error','incompleteReason','fileError','libraryError')), 'Cold replay F9 status errors/seek')
        need(r['segment'] == replay['segmentId'] and value['matchId'] == replay['matchId'] and inside(r['file']) == source and (r['startStep'],r['endStep']) == (summaries[0]['completedStep'],summaries[-1]['completedStep']) and value['scenario']['contentHash'] == summaries[-1]['contentHash'], 'Cold replay capture/source binding mismatch')
    start, end = selected[0][1], selected[-1][1]
    need(start['replay']['currentStep'] == summaries[0]['completedStep'] and start['replay']['paused'] is True and start['replay']['verified'] is False, 'Cold replay actual first capture is not its paused segment start')
    need(any(v['replay']['speed'] == 4 and v['replay']['paused'] is False and v['replay']['currentStep'] > summaries[0]['completedStep'] for _,v in selected), 'Cold replay lacks advancing actual 4x capture')
    need(all(a[1]['replay']['currentStep'] <= b[1]['replay']['currentStep'] and a[1]['captureFrame'] < b[1]['captureFrame'] for a,b in zip(selected,selected[1:])), 'Cold replay capture stream backwards')
    need(end['replay']['currentStep'] == summaries[-1]['completedStep'] and end['replay']['verified'] is True and end['scenario']['winner'] == summaries[-1]['winner'] and len({v['replay']['observerPlayer'] for _,v in selected if v['replay']['verified']}) >= 2, 'Cold replay verified boundary/observer evidence missing')
    store_names = ('Saves','Account','Preferences','Scenarios','ScenarioDrafts','ScenarioExports','Reports','Replays')
    need(set(after['isolatedStores']) == set(store_names) and all(after['isolatedStores'][k] == [] for k in store_names), 'Cold replay after receipt nonempty store')
    actual_stores = {}
    for name in store_names:
        directory = REPLAY_PROFILE / name
        need(not directory.is_symlink(), 'Cold replay store symlink')
        paths = sorted(directory.rglob('*')) if directory.exists() else []
        need(not paths, 'Cold replay isolated store is not actually empty: '+name)
        actual_stores[name] = []
    matching_processes = []
    for process_path in processes:
        process_raw = read(process_path); process = loads(process_raw); args = process.get('args',[])
        if '-frontierDataDirectory' in args and inside(args[args.index('-frontierDataDirectory')+1]) == REPLAY_PROFILE:
            need(process.get('pid') == after['pid'] and process.get('exitCode') == after['exitCode'] == 0 and '-frontierReplay' in args and inside(args[args.index('-frontierReplay')+1]) == source, 'Cold replay exact process receipt binding mismatch')
            need(utc_value(before['atUtc']) <= utc_value(process['startedAtUtc']) <= utc_value(start['utc']) <= utc_value(end['utc']) <= utc_value(process['exitedAtUtc']) <= utc_value(after['atUtc']), 'Cold replay process/capture/receipt UTC window mismatch')
            matching_processes.append(record(process_path,process_raw))
    need(len(matching_processes) == 1, 'Cold replay requires exactly one explicit owned process receipt')
    return {'status':'Passed','source':source_facts,'payloadSha256':payload_hash,'isolatedProfile':REPLAY_PROFILE.relative_to(CANDIDATE).as_posix(),'segmentId':replay['segmentId'],'matchId':replay['matchId'],'startKind':replay['startKind'],'finishReason':replay['finishReason'],'incomplete':False,'anchors':summaries,'recordCount':len(records),'eventCount':len(events),'captureFiles':[row[0]['file'] for row in selected],'sourceFileMatchesRootBeforeAndAfterSha':True,'actuallyEmptyIsolatedStoresAtInspection':actual_stores,'boundProcessReceipts':matching_processes,'scope':'Read-only exact raw event/input hash chains and anchor-state projections plus completed F9 metadata. No simulation rerun, independent GUI operation, zero-start full match or physical LAN claim.'}, [source_facts,record(before_path,before_raw),record(after_path,after_raw)]


def self_test():
    checks = []
    def rejects(label, callback):
        try:
            callback()
        except (ValueError, KeyError, TypeError, zlib.error):
            checks.append(label); return
        raise AssertionError('Accepted invalid fixture: ' + label)
    rejects('duplicate JSON key', lambda: loads('{"a":1,"a":2}'))
    rejects('JSON Infinity', lambda: loads('{"a":Infinity}'))
    rejects('overflowed finite JSON number', lambda: loads('{"a":1e400}'))
    rejects('Boolean is not integer', lambda: integer(True))
    rejects('candidate path traversal', lambda: inside('../../outside.json'))
    payload = b'{"fixture":true}'
    body = zlib.compressobj(wbits=-15); compressed = body.compress(payload) + body.flush()
    member = b'\x1f\x8b\x08\x00' + b'\x00'*6 + compressed + struct.pack('<II', zlib.crc32(payload), len(payload))
    env = {'schema': 2, 'encoding': 'gzip-base64', 'payload': '', 'payloadBase64': base64.b64encode(member).decode(), 'sha256': sha(payload)}
    need(decode_save(json.dumps(env).encode())[0] == {'fixture': True}, 'Valid bounded save fixture')
    checks.append('valid single-member gzip SHA CRC ISIZE')
    for label, bad in [('truncated gzip footer', member[:-1]), ('concatenated gzip', member + member), ('trailing gzip garbage', member + b'x'), ('wrong gzip CRC', member[:-8] + b'\x00'*8)]:
        damaged = dict(env, payloadBase64=base64.b64encode(bad).decode())
        rejects(label, lambda damaged=damaged: decode_save(json.dumps(damaged).encode()))
    frame = bytes([8]) + struct.pack('>HHB', 10, 20, 1) + bytes([1, 0x11, 0])
    scan = bytes([1, 1, 0, 0, 63, 0])
    jpeg = b'\xff\xd8\xff\xc0' + struct.pack('>H', len(frame) + 2) + frame + b'\xff\xda' + struct.pack('>H', len(scan) + 2) + scan + b'\x00\xff\xd9'
    need(ui_image(jpeg)['mediaType'] == 'image/jpeg' and ui_image(jpeg)['width'] == 20, 'Valid bounded JPEG header fixture')
    checks.append('JPEG UI header dimensions/signature remain JPEG despite filename extension')
    rejects('truncated UI JPEG EOI', lambda: ui_image(jpeg[:-1]))
    return {'result': 'Passed', 'count': len(checks), 'checks': checks,
            'scope': 'In-memory malformed metadata/gzip/path fixtures only; no formal evidence, app launch or GUI claim.'}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--candidate', type=Path)
    parser.add_argument('--identity-receipt', type=Path)
    parser.add_argument('--replay-profile', type=Path, help='Only the exact owned Native/replay-profile is allowed')
    parser.add_argument('--plan', type=Path)
    parser.add_argument('--process', action='append', default=[])
    parser.add_argument('--observation', action='append', default=[])
    parser.add_argument('--out', type=Path)
    parser.add_argument('--self-test', action='store_true')
    args = parser.parse_args()
    if args.self_test:
        need(not any((args.candidate, args.identity_receipt, args.replay_profile, args.plan, args.out, args.process, args.observation)), 'Self-test cannot inspect formal inputs')
        print(json.dumps(self_test(), indent=2)); return 0
    need(args.identity_receipt is not None, '--identity-receipt is required; no historical identity fallback')
    configure(args.candidate, args.identity_receipt)
    global REPLAY_PROFILE
    if args.replay_profile is not None:
        REPLAY_PROFILE = inside(args.replay_profile)
        need(REPLAY_PROFILE == NATIVE / 'replay-profile' and REPLAY_PROFILE.is_dir() and REPLAY_PROFILE != PROFILE, 'Only exact owned Native/replay-profile may be added')
    need(args.out is not None, '--out required; use a new receipt basename')
    output = inside(args.out)
    need(not output.exists() and not output.is_symlink() and output.parent == NATIVE, 'Receipt must be a new direct Native child')
    plan_raw = read(args.plan) if args.plan else b'{}'
    plan = loads(plan_raw); need(isinstance(plan, dict) and plan.get('schema', 1) == 1, 'Invalid acceptance plan')
    processes = args.process or [str(path) for path in sorted(NATIVE.glob('process-*.json'))]
    observations = args.observation or ([str(NATIVE / 'actual-ui-observations.json')] if (NATIVE / 'actual-ui-observations.json').exists() else [])
    try:
        result = verify(plan, processes, observations)
        result['planInput'] = record(args.plan, plan_raw) if args.plan else {'status': 'NotProvided'}
        result['verifier'] = record(Path(__file__), limit=MAX_JSON)
        if args.plan:
            need(read(args.plan) == plan_raw, 'Plan changed during review')
        code = 2 if result['metadataInspection'] == 'Failed' else 0
    except (ValueError, KeyError, TypeError, OSError, zlib.error, struct.error, UnicodeError, RecursionError) as error:
        result = {'schema': 1, 'createdUtc': utc(), 'metadataInspection': 'Failed', 'errorType': type(error).__name__,
                  'error': str(error), 'nativeAcceptance': 'pending', 'scope': 'Failure preserves all input evidence.'}
        code = 2
    with output.open('x', encoding='utf-8') as stream:
        json.dump(result, stream, ensure_ascii=False, sort_keys=True, indent=2, allow_nan=False); stream.write('\n')
    print(json.dumps({'metadataInspection': result['metadataInspection'], 'receipt': str(output), **record(output)}, ensure_ascii=False))
    return code


if __name__ == '__main__':
    sys.exit(main())
