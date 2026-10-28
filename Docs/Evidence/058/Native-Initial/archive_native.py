#!/usr/bin/env python3
"""Archive completed native runs by exact build identity, or verify their bytes.

Preparation alone creates no manifest. Archive only after the operator has
finished all runs and native exports/log writes; this script never starts a game.
"""
import argparse
import collections
import datetime
import gzip
import hashlib
import io
import json
import math
from pathlib import Path
import re
import struct
import zlib


def require(ok, message):
    if not ok:
        raise ValueError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def number(value):
    return type(value) in (int, float) and math.isfinite(value)


def unique(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, 'Duplicate JSON field: ' + key)
        result[key] = value
    return result


def constant(value):
    raise ValueError('Nonfinite JSON constant: ' + value)


def finite_float(value):
    result = float(value)
    require(math.isfinite(result), 'Nonfinite JSON number')
    return result


def load(data):
    return json.loads(data, object_pairs_hook=unique, parse_constant=constant,
                      parse_float=finite_float)


def dump(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def safe_path(root, relative):
    path = Path(relative)
    require(not path.is_absolute() and '..' not in path.parts, 'Unsafe archive path')
    return root / path


def event_analysis(raw, report, entity_kinds):
    """Descriptive counts and strict identity/end checks; not a full payment audit."""
    kinds, spawns, commands, payments, research = [collections.Counter() for _ in range(5)]
    alive, seen, ends, starts, resigns, pauses = {}, set(), [], [], [], []
    count, previous = 0, -1
    for line, text in enumerate(io.BytesIO(raw), 1):
        require(text.strip(), 'Empty event line: ' + str(line))
        e = load(text)
        require(type(e.get('schemaVersion')) is int and e['schemaVersion'] == report['eventSchemaVersion'], 'Event schema mismatch')
        require(number(e['time']) and previous - .0001 <= e['time'] <= report['time'] + .0001, 'Event time outside monotonic report bounds')
        require(type(e['player']) is int and e['player'] in (-1, 0, 1), 'Invalid event player')
        require(type(e['entity']) is int and e['entity'] >= 0, 'Invalid event entity')
        # Death.target records the killer's player ID, including -1 for depletion;
        # other targets use entity IDs. Do not reinterpret the shared field.
        require(type(e['target']) is int and e['target'] >= (-1 if e['kind'] == 'Death' else 0), 'Invalid event target')
        require(number(e['amount']), 'Invalid event amount')
        require(isinstance(e['kind'], str) and isinstance(e['message'], str), 'Invalid event kind/message')
        previous = max(previous, e['time']); count += 1; kinds[e['kind']] += 1
        key = str(e['player']) + ':' + e['message']
        if e['kind'] == 'Spawn':
            require(e['entity'] > 0 and e['entity'] not in seen, 'Reused Spawn identity')
            require(e['message'] in entity_kinds, 'Unknown Spawn definition: ' + e['message'])
            seen.add(e['entity']); alive[e['entity']] = (e['player'], entity_kinds[e['message']]); spawns[key] += 1
        elif e['kind'] == 'Death':
            require(e['entity'] in alive and alive[e['entity']][0] == e['player'], 'Death without same-owner living Spawn')
            del alive[e['entity']]
        elif e['kind'] == 'Command':
            commands[key] += 1
        elif e['kind'] == 'Payment':
            payments[key] += 1
        elif e['kind'] == 'ResearchComplete':
            research[key] += 1
        if e['kind'] in ('MatchStarted', 'MatchEnded', 'PauseChanged'):
            row = {k: e[k] for k in ('time', 'player', 'entity', 'target', 'message')}
            {'MatchStarted': starts, 'MatchEnded': ends, 'PauseChanged': pauses}[e['kind']].append(row)
        if e['kind'] == 'Resign' or e['kind'] == 'Command' and e['message'].startswith('Resign'):
            resigns.append({'line': line, 'time': e['time'], 'player': e['player']})
    require(type(report['eventCount']) is int and count == report['eventCount'], 'Event count mismatch')
    require(len(starts) == 1, 'Expected one MatchStarted')
    players = report['players']; require(len(players) == 2, 'Expected two players')
    require(('; ' + players[0]['civilization'] + ' vs ' + players[1]['civilization'] + ';') in starts[0]['message'], 'Starting civilization identity mismatch')
    require(not resigns, 'Unexpected resign in smoke run')
    require(len(ends) <= 1, 'Multiple MatchEnded events')
    living = [sum(owner == p for owner, kind in alive.values()) for p in (0, 1)]
    living_combat = [sum(owner == p and kind in (0, 1) for owner, kind in alive.values()) for p in (0, 1)]
    if ends:
        require(type(report['winner']) is int and report['winner'] in (0, 1), 'Invalid natural winner')
        require(ends[0]['player'] == report['winner'] and abs(ends[0]['time'] - report['time']) <= .0001, 'End winner/time mismatch')
        require(ends[0]['message'] == 'All opposing units and buildings eliminated', 'Unexpected ending reason')
        require(living_combat[1 - report['winner']] == 0, 'Natural loser still has living units/buildings')
    else:
        require(report['winner'] == -1 and report['reason'] != 'match_finished', 'Unfinished report claims natural winner/end')
    return {'eventCount': count, 'eventKindCounts': dict(sorted(kinds.items())),
            'spawnCountsByPlayerAndName': dict(sorted(spawns.items())),
            'commandCountsByPlayerAndMessage': dict(sorted(commands.items())),
            'paymentEventCountsByPlayerAndMessage': dict(sorted(payments.items())),
            'researchCompleteCountsByPlayerAndName': dict(sorted(research.items())),
            'matchStarted': starts, 'matchEnded': ends, 'resignCount': len(resigns),
            'pauseChanged': pauses, 'livingOwnedEntitiesAtExport': living,
            'livingOwnedUnitsAndBuildingsAtExport': living_combat,
            'naturallyEnded': bool(ends),
            'scope': 'Exact event parsing, lifecycle/end identity and descriptive counts only. Payment counts do not prove debit/completion pairing; command.entity is representative. Spawn/name occurrences do not prove all mechanism effects or GUI visibility.'}


def build_summary(root, guid, version, menu_only=False, entity_kinds=None):
    reports, captures, grouped = [], [], collections.defaultdict(list)
    report_paths = sorted((root / 'Reports').glob('*.json'))
    require(not menu_only or not report_paths, 'Menu-only scope cannot contain match reports')
    if report_paths and entity_kinds is None:
        definitions = load((root / 'Rules.json').read_bytes())['entities']
        entity_kinds = {e['name']: e['kind'] for e in definitions}
        require(len(entity_kinds) == len(definitions) and all(type(k) is int and k in (0, 1, 2) for k in entity_kinds.values()), 'Invalid or duplicate entity definitions')
    for path in report_paths:
        r = load(path.read_bytes())
        require(r['buildGuid'] == guid and r['appVersion'] == version and r['platform'] == 'OSXPlayer', 'Wrong native report build')
        require(re.fullmatch(r'[0-9a-f]{32}', r['matchId']) is not None, 'Invalid match ID')
        require(type(r['autoplay']) is bool and number(r['simulationSpeed']), 'Invalid capture mode types')
        require(r['simulationSpeed'] == (16 if r['autoplay'] else 1), 'Unexpected smoke simulation mode')
        require(number(r['time']) and r['time'] >= 0, 'Invalid report time')
        event = path.with_name(path.stem + '-events.jsonl.gz')
        raw = gzip.decompress(event.read_bytes()); analysis = event_analysis(raw, r, entity_kinds)
        row = {'path': str(path.relative_to(root)), 'matchId': r['matchId'], 'reason': r['reason'],
               'autoplay': r['autoplay'], 'simulationSpeed': r['simulationSpeed'], 'time': r['time'],
               'winner': r['winner'], 'civilizations': [p['civilization'] for p in r['players']],
               'ages': [p['age'] for p in r['players']], 'rawEventsSha256': sha(raw), 'rawEventsBytes': len(raw),
               'eventArchive': str(event.relative_to(root)), 'analysis': analysis}
        reports.append(row); grouped[r['matchId']].append(row)
    require(menu_only or reports, 'No matching native reports')
    matches = []
    for mid, rows in sorted(grouped.items()):
        natural = [row for row in rows if row['analysis']['naturallyEnded']]
        finished = [row for row in rows if row['reason'] == 'match_finished']
        require(len(finished) <= 1, 'Repeated match_finished exports')
        require(not natural or finished, 'Natural match lacks match_finished report')
        if natural:
            require(len({row['rawEventsSha256'] for row in rows}) == 1, 'Post-end repeated exports differ')
        chosen = finished[0] if finished else max(rows, key=lambda r: r['time'])
        matches.append({**chosen, 'reportReasons': [r['reason'] for r in rows], 'reportFiles': [r['path'] for r in rows]})
    for path in sorted((root / 'Captures').glob('*.json')):
        c = load(path.read_bytes())
        require(c['buildGuid'] == guid and c['appVersion'] == version, 'Wrong capture build')
        require(c['pngFile'] == path.with_suffix('.png').name, 'Capture PNG basename mismatch')
        png = path.with_suffix('.png').read_bytes()
        require(png.startswith(b'\x89PNG\r\n\x1a\n') and sha(png) == c['pngSha256'], 'PNG bytes/hash mismatch')
        require(struct.unpack('>II', png[16:24]) == (c['width'], c['height']), 'PNG dimensions mismatch')
        require(type(c['inMenu']) is bool and type(c['paused']) is bool, 'Capture mode must be boolean')
        require(not c['matchId'] or c['matchId'] in grouped, 'Captured match missing final export')
        require(not menu_only or c['inMenu'] is True and not c['matchId'] and not c['requestMatchId'], 'Menu-only capture contains live match identity')
        captures.append({k: c[k] for k in ('utc', 'matchId', 'requestMatchId', 'worldTime', 'requestWorldTime', 'inMenu', 'paused', 'pngSha256', 'width', 'height') } | {'path': str(path.relative_to(root))})
    require(captures, 'No matching native captures')
    logs = []
    for path in sorted((root / 'Logs').rglob('*.log')):
        lines = path.read_text(encoding='utf-8', errors='replace').splitlines()
        diagnostics = [{'line': i, 'text': text} for i, text in enumerate(lines, 1)
                       if any(word in text for word in ('Exception', 'MemoryLeaks', 'prematurely finalized', 'Crash!!!', 'ERROR:'))]
        logs.append({'path': str(path.relative_to(root)), 'diagnostics': diagnostics})
    return {'schemaVersion': 1, 'appVersion': version, 'buildGuid': guid, 'menuOnly': menu_only,
            'counts': {'reports': len(reports), 'eventArchives': len(reports), 'captures': len(captures),
                       'logs': len(logs), 'distinctMatches': len(matches),
                       'naturalAutoplayMatches': sum(m['autoplay'] and m['analysis']['naturallyEnded'] for m in matches),
                       'unfinishedManualMatches': sum(not m['autoplay'] and not m['analysis']['naturallyEnded'] for m in matches)},
            'reports': reports, 'matches': matches, 'captures': captures, 'logs': logs,
            'scope': 'Native menu-only UI recheck: zero matches, no gameplay or final-build automatic-match evidence.' if menu_only else 'Native smoke archive, not full manual progression, normal-speed performance acceptance, statistical balance or all-six-civilization manual coverage.'}


def archive(args):
    root = getattr(args, 'destination', None) or Path(__file__).resolve().parent
    root.mkdir(parents=True, exist_ok=True)
    require(not (root / 'manifest.json').exists(), 'Refusing to overwrite a completed archive')
    require(getattr(args, 'resume', False) or not any((root / name).exists() for name in ('Reports', 'Captures', 'Logs')), 'Existing incomplete payload: require explicit --resume; different bytes still rejected')
    sources = []
    def store(path, category, relative, compressed=False):
        raw = path.read_bytes()
        payload = gzip.compress(raw, compresslevel=6, mtime=0) if compressed else raw
        if compressed:
            require(gzip.decompress(payload) == raw, 'Gzip round trip differs')
        target = safe_path(root, category + '/' + str(relative) + ('.gz' if compressed else ''))
        target.parent.mkdir(parents=True, exist_ok=True)
        require(not target.exists() or target.read_bytes() == payload, 'Existing payload differs: ' + str(target))
        target.write_bytes(payload)
        require(path.read_bytes() == raw and target.read_bytes() == payload, 'Source changed during copy')
        row = {'path': str(target.relative_to(root)), 'sourceCategory': category, 'sourcePath': str(relative),
               'bytes': len(payload), 'sha256': sha(payload), 'sourceBytesCompared': True}
        if compressed:
            row.update({'rawBytes': len(raw), 'rawSha256': sha(raw)})
        sources.append(row)
    selected_reports = []
    for category in ('Reports', 'Captures'):
        for path in sorted((args.source / category).glob('*.json')):
            data = load(path.read_bytes())
            if data.get('buildGuid') != args.build_guid or data.get('appVersion') != args.app_version:
                continue
            store(path, category, path.name)
            if category == 'Reports':
                selected_reports.append(data)
                store(path.with_name(path.stem + '-events.jsonl'), category, path.stem + '-events.jsonl', True)
            else:
                require(data['pngFile'] == path.with_suffix('.png').name, 'Unsafe/mismatched PNG name')
                store(path.with_suffix('.png'), category, path.with_suffix('.png').name)
    menu_only = getattr(args, 'menu_only', False)
    require(not selected_reports if menu_only else bool(selected_reports), 'Report count differs from explicit match/menu-only scope')
    log_names = getattr(args, 'log_file', None)
    log_paths = [safe_path(args.log_source, name) for name in log_names] if log_names else sorted(args.log_source.rglob('*.log'))
    require(len({p.resolve() for p in log_paths}) == len(log_paths), 'Duplicate log selection')
    for path in log_paths:
        require(path.suffix == '.log' and path.is_file(), 'Selected native log missing or wrong suffix')
        store(path, 'Logs', path.relative_to(args.log_source))
    require(any(s['sourceCategory'] == 'Logs' for s in sources), 'No native client logs')
    payload_paths = {str(p.relative_to(root)) for name in ('Reports', 'Captures', 'Logs') for p in (root / name).rglob('*') if p.is_file()}
    require(payload_paths == {s['path'] for s in sources}, 'Unexpected or stale payload in incomplete archive')
    rules_source = getattr(args, 'rules', None)
    if selected_reports:
        require(rules_source is not None, 'Rules snapshot required for unit/building classification')
        raw_rules = rules_source.read_bytes(); target = root / 'Rules.json'
        require(not target.exists() or target.read_bytes() == raw_rules, 'Existing Rules snapshot differs')
        target.write_bytes(raw_rules); require(rules_source.read_bytes() == raw_rules, 'Rules changed during copy')
        sources.append({'path': 'Rules.json', 'sourceCategory': 'Rules', 'sourcePath': 'Assets/Resources/Rules.json', 'originalPath': str(rules_source.resolve()), 'bytes': len(raw_rules), 'sha256': sha(raw_rules), 'sourceBytesCompared': True})
    summary = build_summary(root, args.build_guid, args.app_version, menu_only)
    dump(root / 'summary.json', summary)
    source_by_path = {row['path']: row for row in sources}
    inventory = []
    for path in sorted(root.rglob('*')):
        if not path.is_file() or '__pycache__' in path.parts:
            continue
        relative = str(path.relative_to(root)); data = path.read_bytes()
        inventory.append({'path': relative, 'bytes': len(data), 'sha256': sha(data), **source_by_path.get(relative, {})})
    manifest = {'schemaVersion': 1, 'generatedAtUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
                'buildGuid': args.build_guid, 'appVersion': args.app_version, 'menuOnly': menu_only, 'files': inventory,
                'sourceRoots': {'native': str(args.source.resolve()), 'logs': str(args.log_source.resolve())}}
    dump(root / 'manifest.json', manifest)
    (root / 'manifest.sha256').write_text(sha((root / 'manifest.json').read_bytes()) + '  manifest.json\n')
    return verify(root, args.source, args.log_source, rules_source)


def verify(root, source=None, log_source=None, rules_source=None):
    raw_manifest = (root / 'manifest.json').read_bytes()
    require(sha(raw_manifest) == (root / 'manifest.sha256').read_text().split()[0], 'Manifest SHA mismatch')
    manifest = load(raw_manifest); expected = {r['path'] for r in manifest['files']}
    require(len(expected) == len(manifest['files']), 'Duplicate manifest paths')
    actual = {str(p.relative_to(root)) for p in root.rglob('*') if p.is_file() and '__pycache__' not in p.parts}
    require(actual == expected | {'manifest.json', 'manifest.sha256'}, 'Archive inventory differs')
    for row in manifest['files']:
        data = safe_path(root, row['path']).read_bytes()
        require(len(data) == row['bytes'] and sha(data) == row['sha256'], 'File mismatch: ' + row['path'])
        original = gzip.decompress(data) if 'rawSha256' in row else data
        if 'rawSha256' in row:
            require(len(original) == row['rawBytes'] and sha(original) == row['rawSha256'], 'Raw event mismatch: ' + row['path'])
        if row.get('sourceCategory') == 'Rules':
            if rules_source is not None:
                require(rules_source.read_bytes() == original, 'Original Rules bytes differ')
            continue
        origin = log_source if row.get('sourceCategory') == 'Logs' else source
        if origin is not None and row.get('sourcePath'):
            category = '' if row['sourceCategory'] == 'Logs' else row['sourceCategory']
            require(safe_path(origin / category, row['sourcePath']).read_bytes() == original, 'Original bytes differ: ' + row['path'])
    rebuilt = build_summary(root, manifest['buildGuid'], manifest['appVersion'], manifest['menuOnly'])
    require(rebuilt == load((root / 'summary.json').read_bytes()), 'Summary differs from raw reanalysis')
    return {'verified': True, **rebuilt['counts'], 'manifestedFiles': len(expected),
            'originalNativeBytesCompared': source is not None, 'originalLogBytesCompared': log_source is not None,
            'originalRulesBytesCompared': rules_source is not None and not manifest['menuOnly'],
            'scope': rebuilt['scope']}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='command', required=True)
    create = sub.add_parser('archive')
    create.add_argument('--source', type=Path, required=True)
    create.add_argument('--log-source', type=Path, required=True)
    create.add_argument('--log-file', action='append', help='Exact relative log filename; repeat to select only this build phase logs')
    create.add_argument('--build-guid', required=True)
    create.add_argument('--app-version', required=True)
    create.add_argument('--destination', type=Path, help='Separate archive directory, e.g. Native-Initial; default is this script directory')
    create.add_argument('--rules', type=Path, default=Path(__file__).resolve().parents[4] / 'Assets/Resources/Rules.json')
    create.add_argument('--resume', action='store_true', help='Resume an incomplete attempt; existing payload must have identical bytes')
    create.add_argument('--menu-only', action='store_true', help='Explicit zero-match UI recheck; every capture must be a menu and no reports allowed')
    create.add_argument('--runs-completed', action='store_true', required=True,
                        help='Operator confirms runs and export/log writing completed; never use on live evidence')
    check = sub.add_parser('verify')
    check.add_argument('--source', type=Path)
    check.add_argument('--log-source', type=Path)
    check.add_argument('--archive-dir', type=Path, help='Archive to verify; default is this script directory')
    check.add_argument('--rules', type=Path, help='Optionally compare original Rules.json snapshot')
    args = parser.parse_args()
    try:
        if args.command == 'archive':
            require(re.fullmatch(r'[0-9a-f]{32}', args.build_guid) is not None and int(args.build_guid, 16) != 0, 'Explicit nonzero lowercase 32-digit GUID required')
            result = archive(args)
        else:
            result = verify(args.archive_dir or Path(__file__).resolve().parent, args.source, args.log_source, args.rules)
        print(json.dumps(result, ensure_ascii=False, indent=2))
    except (OSError, EOFError, zlib.error, ValueError, KeyError, TypeError, struct.error) as error:
        parser.exit(2, 'Cannot archive/verify native evidence: ' + str(error) + '\n')


if __name__ == '__main__':
    main()
