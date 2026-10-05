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
GUID = '9fe1c3fe57614b13a1fd11bfaf2d9d8f'
VERSION = '0.12.0'
UNITY = '6000.6.3f1'
SOURCE_FP = 'fb4be2644846677a24aeb110026c0d47bac627b9fa523c6a758ebe892cbb4983'
SOURCE_COMMIT = '7b5c58c3755cd79be520e95935617c6367e912a0'
RULES_HASH = '5324ece4d54715613e941af737d8da34dcf2d5f517388373516b5e0f17b85d1f'
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
        need('-frontierDataDirectory' in args and Path(args[args.index('-frontierDataDirectory') + 1]).resolve() == (NATIVE / 'profile').resolve(), 'Process receipt data root mismatch')
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
    identity = app_identity()
    captures, documents, checkpoints = {}, {}, {}
    for path in sorted((NATIVE / 'profile/Captures').glob('*.json')):
        need(len(captures) < 2048, 'Capture count bound')
        try:
            captures[path.relative_to(CANDIDATE).as_posix()] = capture(path)
        except (ValueError, KeyError, TypeError) as error:
            raise ValueError(path.relative_to(CANDIDATE).as_posix() + ': ' + str(error)) from error
    for directory in ('profile/Scenarios', 'profile/ScenarioDrafts', 'profile/ScenarioExports', 'AuthoredStages'):
        for path in sorted((NATIVE / directory).glob('*.json')):
            need(len(documents) < 2048, 'Portable document count bound')
            try:
                documents[path.relative_to(CANDIDATE).as_posix()] = portable(path)
            except (ValueError, KeyError, TypeError) as error:
                raise ValueError(path.relative_to(CANDIDATE).as_posix() + ': ' + str(error)) from error
    for directory in ('profile/Saves', 'Checkpoint-fixtures'):
        for path in sorted((NATIVE / directory).glob('*.json')):
            need(len(checkpoints) < 64, 'Checkpoint count bound')
            try:
                checkpoints[path.relative_to(CANDIDATE).as_posix()] = checkpoint(path, documents)
            except (ValueError, KeyError, TypeError) as error:
                raise ValueError(path.relative_to(CANDIDATE).as_posix() + ': ' + str(error)) from error
    roots = [root_receipt(path, 'process') for path in processes] + [root_receipt(path, 'observation') for path in observations]
    phases, comparison = phase_plan(plan, captures, documents, checkpoints)
    source_review = {'status': 'NotProvided'}
    if plan.get('sourceReview'):
        source_path = inside(plan['sourceReview']); source_raw = read(source_path); source = loads(source_raw)
        need(source.get('result') == 'Passed' and source.get('sourceCommit') == SOURCE_COMMIT and source.get('sourceFingerprint') == SOURCE_FP and source.get('sourceInputs') == 664, 'Frozen source review identity mismatch')
        input_hashes = source['frozenInputHashes']
        need(isinstance(input_hashes, dict) and len(input_hashes) == 664 and sha(json.dumps(input_hashes, sort_keys=True, separators=(',', ':')).encode()) == SOURCE_FP, 'Frozen source input-map fingerprint mismatch')
        source_review = {**record(source_path, source_raw), 'status': 'Passed', 'sourceCommit': SOURCE_COMMIT, 'sourceFingerprint': SOURCE_FP, 'sourceInputs': 664, 'scope': 'Bound separate independent frozen-Git review; current working tree is not used.'}
    root_defects = sum(row.get('reportedFailureCount', 0) for row in roots)
    if 'RootDiagnosticDefects' in plan:
        need(root_defects == plan['RootDiagnosticDefects'], 'Expected root diagnostic failure count mismatch')
    facts = [identity[key] for key in ('boot', 'plist', 'executable', 'compiledRuntime')]
    facts += [row[0] for row in captures.values()] + [row[0]['png'] for row in captures.values()]
    facts += [row[0] for row in documents.values()] + [row[0] for row in checkpoints.values()] + roots
    facts += [item for root in roots for item in root.get('boundUiFiles', [])]
    if source_review.get('status') == 'Passed':
        facts.append(source_review)
    need(sum(item['bytes'] for item in facts) <= MAX_TOTAL, 'Total input byte bound')
    for item in facts:
        need(record(CANDIDATE / item['file'], limit=MAX_PAYLOAD)['sha256'] == item['sha256'], 'An inspected input changed during review')
    failed = any(p['metadataStatus'] == 'Failed' for p in phases) or comparison['status'] == 'Failed'
    return {'schema': 1, 'createdUtc': utc(), 'metadataInspection': 'Failed' if failed else 'Passed',
            'identity': identity, 'captures': [row[0] for row in captures.values()],
            'portableDocuments': [row[0] for row in documents.values()], 'checkpoints': [row[0] for row in checkpoints.values()],
            'rootReceiptInputs': roots, 'plannedPhases': phases, 'checkpointComparison': comparison,
            'inputsUnchangedDuringInspection': True, 'frozenSourceReviewInput': source_review,
            'RootDiagnosticDefects': root_defects, 'GUIoverall': 'FailedAsReportedByRoot' if root_defects else 'NotIndependentlyObserved',
            'candidateDisposition': plan.get('candidateDisposition', 'NotDeclared'),
            'nativeAcceptance': 'PreFixDiagnosticOnly' if root_defects or plan.get('candidateDisposition', '').startswith('pre-fix') else 'pending',
            'mediaCorrection': 'Original F9 pairs are PNG. Original Sky UI .png-named files are JPEG bytes; signature-based UI inspection preserves original bytes and failed prior receipts. JPEG is not relabeled as decoded PNG.',
            'scope': ['Read-only current Candidate1 F9/file/saved-state evidence; no Unity, GUI, simulation or packaging is run.',
                      'Mapped metadata can pass while missing GUI observations or later lifecycle phases remain pending.',
                      'Process args/source/exit fields are root receipt claims; no OS lifecycle or independent GUI certification.',
                      'Scenario format/rules/terrain/ID bounds are checked; runtime geography/catalog/playability rely on actual runtime/tests.',
                      'Old 0.11 archive, physical LAN, natural full-match, transport and foreground performance are outside this tool.']}


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
    parser.add_argument('--plan', type=Path)
    parser.add_argument('--process', action='append', default=[])
    parser.add_argument('--observation', action='append', default=[])
    parser.add_argument('--out', type=Path)
    parser.add_argument('--self-test', action='store_true')
    args = parser.parse_args()
    if args.self_test:
        need(not any((args.plan, args.out, args.process, args.observation)), 'Self-test cannot inspect formal inputs')
        print(json.dumps(self_test(), indent=2)); return 0
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
