#!/usr/bin/env python3
"""Finalize only the already archived C3 and two explicitly frozen C3 logs."""
import collections
import datetime
import hashlib
import json
from pathlib import Path
import re
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
A = ROOT / 'Docs/Evidence/055/NormalPlayer/river-attempt-003'
NOW = datetime.datetime.now(datetime.timezone.utc).isoformat()
MATCH = '8fb300efbc1a4169a953d9dab2b7caad'
sys.path.insert(0, str(ROOT / 'Tools'))
from verification_evidence import source_files, fingerprint


def dig(data):
    return {'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()}


def read(name):
    return json.loads((A / name).read_text())


def write(name, data):
    (A / name).write_text(json.dumps(data, indent=2, ensure_ascii=False, allow_nan=False) + '\n')


def preserve(source, name):
    data = source.read_bytes()
    target = A / name
    if target.exists():
        assert target.read_bytes() == data, name
    else:
        with target.open('xb') as f:
            f.write(data)
    return {'source': str(source), 'archive': name, **dig(data), 'bytesMatch': target.read_bytes() == data}


summary = read('summary.json')
finished_path = next(A.glob('*-match_finished.json'))
menu_path = next(A.glob('*-menu_exit.json'))
events_path = finished_path.with_name(finished_path.stem + '-events.jsonl')
menu_events = menu_path.with_name(menu_path.stem + '-events.jsonl')
finished = json.loads(finished_path.read_text())
menu = json.loads(menu_path.read_text())
events = [json.loads(line) for line in events_path.read_text().splitlines()]
lineage = summary['lineage']['players'][0]
rules = read('Rules.json')
definitions = {d['name']: d for d in rules['entities']}

logs = [preserve(ROOT / 'Builds/NativeInput055-C3-preserved' / name, 'native-input-full-' + name)
        for name in ('Player-ended.log', 'Player-menu.log')]
end_bytes = (A / logs[0]['archive']).read_bytes()
menu_bytes = (A / logs[1]['archive']).read_bytes()
assert menu_bytes.startswith(end_bytes)
log_lines = menu_bytes.decode('utf-8').splitlines(keepends=True)
excerpt = ''.join(log_lines[1569:]).encode()
(A / 'native-input-c3-menu-through-exit.log').write_bytes(excerpt)
log_audit = {'generatedAtUtc': NOW, 'status': 'passed', 'sources': logs,
             'endedLogExactPrefixOfMenuLog': True, 'sourceLineCount': len(log_lines),
             'excerpt': {'file': 'native-input-c3-menu-through-exit.log', 'firstSourceLine': 1570,
                         'lastSourceLine': len(log_lines), **dig(excerpt)},
             'scope': 'The two full immutable process snapshots include C2 history. The exact C3 excerpt starts at the menu click changing the opponent from Coast to River, before the C3 menu F9, and ends after C3 menu_exit and its F9. No later runtime log was read.'}
write('log-preservation-audit.json', log_audit)

frozen = read('055-source-evidence.json')
receipt = read('055-build-receipt.json')
actual_sources = source_files(ROOT)
source_differences = sorted(p for p in set(frozen['files']) | set(actual_sources)
                            if frozen['files'].get(p) != actual_sources.get(p))
runtime = {p: {'expectedSha256': sha, **dig((ROOT / 'Builds/macOS/Frontier Reckoning.app' / p).read_bytes())}
           for p, sha in receipt['compiledRuntimeSha256'].items()}
frozen_checks = {'sourceFileCount357': len(actual_sources) == len(frozen['files']) == 357,
                 'allSourceHashesMatch': not source_differences,
                 'fingerprintMatches': fingerprint(actual_sources) == frozen['sourceFingerprint'] == receipt['sourceFingerprint'],
                 'threeRuntimeHashesMatch': len(runtime) == 3 and all(v['sha256'] == v['expectedSha256'] for v in runtime.values()),
                 'reportVersionGuidMatch': finished['appVersion'] == receipt['appVersion'] == '0.2.31' and finished['buildGuid'] == receipt['buildGuid'] == '128a731d440e4b7a94abfcc74b88fa97'}
write('frozen-build-audit.json', {'generatedAtUtc': NOW, 'status': 'passed' if all(frozen_checks.values()) else 'failed',
      'checks': frozen_checks, 'sourceFingerprint': fingerprint(actual_sources), 'sourceFileCount': len(actual_sources),
      'sourceDifferences': source_differences, 'runtime': runtime,
      'scope': 'Read-only current 357-file source and three compiled runtime hash comparison with the archived frozen .31 receipts. No Unity/editor/build/test runtime was started.'})

report_diffs = {k: {'match_finished': finished.get(k), 'menu_exit': menu.get(k)}
                for k in set(finished) | set(menu) if finished.get(k) != menu.get(k)}
pair_checks = {'eventFilesByteIdentical': events_path.read_bytes() == menu_events.read_bytes(),
               'onlyReportReasonAndManagedBytesDiffer': set(report_diffs) == {'reason', 'managedBytes'},
               'sameMatchId': finished['matchId'] == menu['matchId'] == MATCH,
               'reportCountMatchesRaw': len(events) == finished['eventCount'] == menu['eventCount'] == 90759,
               'matchFinishedReason': finished['reason'] == 'match_finished', 'menuExitReason': menu['reason'] == 'menu_exit'}
write('report-pair-audit.json', {'generatedAtUtc': NOW, 'status': 'passed' if all(pair_checks.values()) else 'failed',
      'checks': pair_checks, 'differences': report_diffs, 'sources': {p.name: dig(p.read_bytes()) for p in [finished_path, menu_path, events_path, menu_events]}})

births, deaths = {}, {}
cannon_damage = []
controls = []
for line, e in enumerate(events, 1):
    if e['kind'] == 'Spawn':
        births[e['entity']] = {'line': line, 'event': e, 'definition': definitions.get(e['message'])}
    if e['kind'] == 'Death':
        deaths[e['entity']] = {'line': line, 'event': e}
    if e['kind'] == 'Damage' and e['player'] == 0:
        src, target = births.get(e['entity'], {}), births.get(e['target'], {})
        if (src.get('definition') or {}).get('id') == 'cannon':
            cannon_damage.append({'line': line, 'event': e, 'sourceSpawnLine': src['line'],
                                  'targetSpawnLine': target.get('line'), 'targetOwner': target.get('event', {}).get('player'),
                                  'targetDefinition': (target.get('definition') or {}).get('id'),
                                  'targetKind': (target.get('definition') or {}).get('kind')})
    if e['kind'] == 'Command' and line in (69115, 70567, 71112):
        controls.append({'line': line, 'event': e})
enemy_building_hits = [e for e in cannon_damage if e['targetOwner'] == 1 and e['targetKind'] == 1 and e['event']['amount'] > 0]
by_target = {}
for d in cannon_damage:
    row = by_target.setdefault(d['targetDefinition'], {'owner': d['targetOwner'], 'kind': d['targetKind'], 'events': 0, 'damage': 0})
    row['events'] += 1
    row['damage'] += d['event']['amount']
write('cannon-damage-audit.json', {'generatedAtUtc': NOW, 'status': 'failed-coverage-confirmed',
      'eventFile': events_path.name, 'eventsScanned': len(events), 'paidCannonBorn': summary['playerPaidCounts']['cannon'],
      'playerCannonDamageEventCount': len(cannon_damage), 'enemyBuildingDamageEventCount': len(enemy_building_hits),
      'targetBreakdown': by_target, 'damageEvidence': cannon_damage,
      'controlReview': {'commands': controls,
          'observation': 'At 1509.378540 seconds the recorded command is Move ×3, target 0, despite the attempted enemy-cannon click. Attack ×1 / ×4 with target 333 occur later at 1530.373413 / 1538.571411. The Move is not reclassified as an Attack. Intended model/tree overlap click is operator context, not proof of a source defect.'}})

captures = read('capture-audit.json')['captures']
def cap(prefix):
    return next(c for c in captures if c['json'].startswith(prefix))

visual_readings = [
 ('Frontier-20261003-041929', 'Opening menu: player River League, AI River League, Easy, age cap 5, seed 314159, Military 25/25.', 'archive image inspected'),
 ('Frontier-20261003-045413', 'Single Musketeer: HP 200/200, ATK 24, RANGE 12.', 'root independent image inspection'),
 ('Frontier-20261003-045447', 'Age III; single Musketeer: HP 200/200, ATK 24, RANGE 12, POP 1.', 'archive image inspected'),
 ('Frontier-20261003-045627', 'Age III; 10 selected Musketeers; first displayed HP 240/240, ATK 29, RANGE 12, POP 1.', 'archive image inspected'),
 ('Frontier-20261003-045749', '8 selected Musketeers; first displayed HP 170/240, ATK 29.', 'root independent image inspection'),
 ('Frontier-20261003-045832', 'Single Musketeer: HP 240/240, ATK 29.', 'root independent image inspection'),
 ('Frontier-20261003-051213', 'Age IV HUD at 20:20, Town Center selected.', 'archive image inspected'),
 ('Frontier-20261003-053534', 'Age V HUD at 27:21, Reached age 5 message, Town Center selected.', 'archive image inspected'),
 ('Frontier-20261003-054203', 'DEFEAT 29m 41s; kills/losses 95/106; both reached age 5; player 10 shipments. Overlay wording alone says elimination or resignation; raw MatchEnded and absence of Resign distinguish natural elimination.', 'archive image inspected')]
visual = [{'capture': cap(prefix)['json'], 'png': cap(prefix)['png'], 'worldTime': cap(prefix)['worldTime'],
           'reading': reading, 'provenance': provenance} for prefix, reading, provenance in visual_readings]

contexts = []
last_context = None
for i, text in enumerate(log_lines, 1):
    if i < 1570:
        continue
    if text.startswith('FRONTIER_UI context '):
        m = re.search(r'entity=(\d+) count=(\d+) world=([\d.]+) frame=(\d+)', text)
        if m:
            last_context = {'sourceLine': i, 'raw': text.rstrip(), 'entity': int(m[1]), 'count': int(m[2]), 'worldTimeRounded': float(m[3]), 'frame': int(m[4])}
    if text.startswith('FRONTIER_CAPTURE_OK ') and any(text.rstrip().endswith(cap(p)['json']) for p in ['Frontier-20261003-045413', 'Frontier-20261003-045447', 'Frontier-20261003-045627', 'Frontier-20261003-045749', 'Frontier-20261003-045832']):
        c = next(c for c in captures if text.rstrip().endswith(c['json']))
        entity = last_context['entity']
        paid = next(x for x in lineage['training'] if x['spawn']['entity'] == entity)
        contexts.append({'capture': c['json'], 'captureSourceLine': i, 'captureWorldTime': c['worldTime'],
                         'latestPrecedingContext': last_context.copy(), 'paidTraining': paid,
                         'death': deaths[entity], 'aliveAtCapture': births[entity]['event']['time'] <= c['worldTime'] < deaths[entity]['event']['time']})
tech = next(t for t in lineage['technologies'] if t['definition'] == 'veteran_musketeer')
base = next(d for d in rules['entities'] if d['id'] == 'musketeer')
modifier = next(t for t in rules['techs'] if t['id'] == 'veteran_musketeer')
write('visual-and-technology-audit.json', {'generatedAtUtc': NOW, 'status': 'passed', 'visualReadings': visual,
      'technology': tech, 'unitContextAssociations': contexts,
      'ruleValues': {'baseHP': base['hp'], 'baseAttack': base['damage'], 'healthBonus': modifier['health'], 'attackBonus': modifier['attack'],
                     'expectedMaxHP': base['hp'] * (1 + modifier['health']), 'expectedAttackBeforeDisplayRounding': base['damage'] * (1 + modifier['attack'])},
      'interpretation': 'Older paid Musketeers 172 and 158 were born before the technology. Captures show base stats before completion and upgraded stats in an old group after completion. Paid Musketeer 255 was born at 797.952209 after completion at 766.259949 and is associated through the input UI context with the later single-unit upgraded display. These are different units, not a same-unit before/after claim.',
      'limitations': 'F9 JSON has no selectedEntity field. Entity associations use the latest preceding FRONTIER_UI context plus birth/death chronology from immutable raw logs, separately from the visible pixel readings. Only the listed nine images were visually interpreted; all twenty pairs passed byte and metadata checks. In-menu HUD match/time fields can retain the prior hidden HUD; menu matchId/requestMatchId are empty as expected.'})

key_lines = set()
def collect_refs(obj):
    if isinstance(obj, dict):
        if isinstance(obj.get('line'), int) and 'kind' in obj:
            key_lines.add(obj['line'])
        for v in obj.values():
            collect_refs(v)
    elif isinstance(obj, list):
        for v in obj:
            collect_refs(v)
for k in ('ages', 'technologies', 'training', 'buildings', 'treasure', 'tradeXP', 'resourceModes', 'resourceDeliveries'):
    collect_refs(lineage[k])
collect_refs(lineage['bankIncome'][:1])
collect_refs(lineage['bankIncome'][-1:])
for k in ('shipmentSentEvidence', 'shipmentArrivedEvidence', 'openingEvidence', 'naturalEndEvidence'):
    collect_refs(summary[k])
key_lines.update([69115, 70567, 71112])
write('key-events-raw.json', {'source': events_path.name, 'lineNumbering': 'One-based original JSONL lines',
      'scope': 'Exact event payloads referenced by player age/technology/training/building/treasure/trade/shipment/opening/end lineage, first and last paid-bank income and the three reviewed control commands. Full raw events remain authoritative.',
      'events': [{'line': line, 'event': events[line-1]} for line in sorted(key_lines)]})

source_copy = read('source-copy-audit.json')['sources']
copy_checks = {name: dig((A / name).read_bytes()) == {'bytes': row['bytes'], 'sha256': row['sha256']} for name, row in source_copy.items()}
capture_checks = {c['json']: all(c['checks'].values()) for c in captures}
integrity_checks = {'originalCopiedBytesUnchanged': all(copy_checks.values()), 'frozenSourceAndRuntimeMatch': all(frozen_checks.values()),
                    'twentyCompleteCapturePairs': len(captures) == 20 and all(capture_checks.values()),
                    'eighteenMatchAndTwoMenuCaptures': sum(not c['inMenu'] for c in captures) == 18 and sum(c['inMenu'] for c in captures) == 2,
                    'reportPairConsistent': all(pair_checks.values()), 'logSnapshotsPreserved': all(l['bytesMatch'] for l in logs),
                    'technologyContextsAliveAtCapture': len(contexts) == 5 and all(c['aliveAtCapture'] for c in contexts),
                    'strictFailurePreciselyOneCannonGap': summary['failedChecks'] == ['requiredPlayerCoverage'] and summary['coverageGaps'] == ['cannonEnemyBuildingDamage'] and not enemy_building_hits}
write('verification.json', {'generatedAtUtc': NOW, 'archiveIntegrityStatus': 'passed' if all(integrity_checks.values()) else 'failed',
      'strictCStatus': summary['status'], 'strictChecksPassed': sum(summary['checks'].values()), 'strictChecksTotal': len(summary['checks']),
      'coveragePassed': sum(summary['coverage'].values()), 'coverageTotal': len(summary['coverage']),
      'failedChecks': summary['failedChecks'], 'coverageGaps': summary['coverageGaps'],
      'checks': integrity_checks, 'copiedBytesAudit': copy_checks,
      'naturalEnd': summary['naturalEndEvidence'], 'livingUnitsAndBuildingsAtEnd': summary['livingUnitsAndBuildingsAtEnd'],
      'scope': 'Archive integrity is distinct from gameplay acceptance. C3 is an evidence-complete natural defeat and a failed strict River I–V attempt. No later match data is included.'})

rows = '\n'.join(f"| {x['age']} | {x['payment']['time']:.6f} | {x['completion']['time']:.6f} | {x['payment']['line']} / {x['completion']['line']} |" for x in lineage['ages'])
tech_rows = '\n'.join(f"| {x['definition']} | {x['payment']['time']:.6f} | {x['completion']['time']:.6f} | {x['payment']['line']} / {x['completion']['line']} |" for x in lineage['technologies'])
capture_rows = '\n'.join(f"| {i} | {'menu' if c['inMenu'] else format(c['worldTime'], '.6f')} | [{c['png']}]({c['png']}) · [JSON]({c['json']}) | {c['sourceLogLine']} |" for i,c in enumerate(captures,1))
coverage_rows = '\n'.join(f"| `{k}` | {'PASS' if v else '**FAIL**'} |" for k,v in summary['coverage'].items())
readme = f'''# River normal-player attempt 003 — failed strict C, preserved

C3 ended naturally in **Defeat at {finished['time']:.6f} simulation seconds (29m 41s)**. The frozen `0.2.31` macOS player ran River League versus River League, Easy, seed 314159, age cap V, Military 25/25, autoplay false and simulation speed 1. Both players reached V. The player recorded 95 kills, 106 losses and 10 shipments. This is a failed attempt, not completion of C.

The canonical strict verifier returns **16/17 checks and 18/19 coverage items passed**. Its sole coverage gap is **`cannonEnemyBuildingDamage`**. Eight paid Field Cannons completed training, but all 153 player Field Cannon damage events hit enemy units; none hit an enemy building. See [strict summary](summary.json), [exact invocation and exit code](audit-invocation.json), [independent cannon scan](cannon-damage-audit.json) and [archive verification](verification.json). The latter's integrity PASS does not change strict C's FAIL.

## Identity and raw preservation

- Match: `{MATCH}`; version `0.2.31`; GUID `128a731d440e4b7a94abfcc74b88fa97`.
- Source fingerprint: `{frozen['sourceFingerprint']}`. [Frozen build audit](frozen-build-audit.json) confirms all 357 source hashes and all three compiled runtime hashes against the copied 055 receipts. No runtime was launched for archiving.
- [Natural end report]({finished_path.name}) and [raw events]({events_path.name}): 90,759 events. MatchEnded line 90721 says all opposing units and buildings eliminated; no Resign; final player/opponent living units and buildings `[0, 107]`. This disambiguates the generic defeat overlay wording.
- [Menu exit report]({menu_path.name}) and [menu raw events]({menu_events.name}) retain the same match and byte-identical event stream. Report differences are only `reason` and `managedBytes`; see [pair audit](report-pair-audit.json).
- Immediately after natural defeat, the shared C2/C3 process log was frozen as [Player-ended](native-input-full-Player-ended.log): 503,819 bytes, SHA256 `8ea281b865a572b9979449a197b1ac5405433ac5d4903055de40d85bafa2975a`.
- After MAIN MENU and its F9, before another match, [Player-menu](native-input-full-Player-menu.log) was frozen: 509,349 bytes, SHA256 `9f2fcbcc794a28166ec91c6516c67de525940e78ba6cd596279dd8de95f66320`. The ended log is its exact byte prefix. Both include earlier C2 history; the [C3 excerpt](native-input-c3-menu-through-exit.log) is precisely full-log lines 1570–{len(log_lines)}. [Log audit](log-preservation-audit.json) records the boundaries.
- The initial helper also retained a timestamped duplicate of the menu snapshot. `preliminary-archive-receipt.json` is the historical initial preservation checkpoint. [Final archive receipt](archive-receipt.json) inventories all final files except itself.

All reads after preservation used these snapshots and explicit C3 archive files. No later match reports, live logs, or GUI were used.

## Paid progress and coverage

| Age reached | Paid at (s) | Completed at (s) | Raw payment / completion line |
|---|---:|---:|---:|
{rows}

| Technology | Paid at (s) | Completed at (s) | Raw payment / completion line |
|---|---:|---:|---:|
{tech_rows}

Paid completed units: 19 Settlers, 15 Musketeers, 20 Pikemen, eight Field Cannons and ten Carabineers. Paid completed buildings include four Counting Houses and one Trading Post. The post completed at 219.804214 s, delivered three XP payments of 40, switched to Food at 295.008789 s and delivered nine payments of 75 Food (675 total). Player bank income total was 9852.25 Coin. Two treasures were claimed. The full payment→completion lineage is in [summary](summary.json); [key events](key-events-raw.json) preserves selected exact raw payloads with original line numbers.

| Strict coverage | Result |
|---|---|
{coverage_rows}

## Technology pixels and entity associations

The pre-upgrade single Musketeer at 665.684509 / 699.176331 s displays HP 200/200 and ATK 24. The old group at 771.558655 s displays HP 240/240 and ATK 29; at 798.652039 s it displays 170/240 and ATK 29. The new single Musketeer at 805.550354 s displays HP 240/240 and ATK 29. Veteran Musketeer completed at 766.259949 s; frozen rules give +20% HP and attack (200→240, 24→28.8 before display rounding).

F9 JSON has **no selected entity ID**. Separate original input contexts associate the pre-upgrade single unit with entity 172 (paid birth 299.009033 s), the old group's first unit with 158 (paid birth 251.206131 s), and the later new single unit with 255 (paid birth 797.952209 s). Birth/death chronology confirms each was alive at its capture. These are different units; this is not a same-unit before/after claim. [Visual and technology audit](visual-and-technology-audit.json) records the source log lines, raw lineage, visual readings and their provenance. The older group and the newly trained unit independently show the upgraded stats.

## Control review and limits

At 1509.378540 s, raw Command line 69115 records `Move ×3`, target 0, for the attempted enemy-cannon click. The later commands at 1530.373413 / 1538.571411 s target entity 333 and explicitly record `Attack ×1` / `Attack ×4`. The earlier movement is not attack evidence. The operator described a cannon/tree overlap click; raw events establish the accepted commands, not a source-code defect.

Only the nine images listed in the visual audit were visually interpreted; all 20 PNG/JSON pairs were byte/hash/dimension/identity/time checked. Menu matchId/requestMatchId are empty; their hidden HUD identity may retain the previous match. Natural end, paid advancement and coverage decisions use the authoritative raw events. This archive makes no fixed-FPS, large-battle or C-pass claim.

## Complete F9 inventory

There are two menu captures and 18 in-match captures, including technology before/after, Age IV, Age V and natural defeat. Original files are preserved unchanged at 2560×1440. Lines refer to the full Player-menu log.

| # | Simulation time (s) | Original pair | Log line |
|---:|---:|---|---:|
{capture_rows}

## Offline verification

The exact canonical invocation and exit 1 are retained in [audit-invocation.json](audit-invocation.json); [CLI output](verify-player-cli.txt) and [summary](summary.json) state the single expected failure. [Source copy audit](source-copy-audit.json), [capture audit](capture-audit.json), [frozen audit](frozen-build-audit.json), [pair audit](report-pair-audit.json) and [final receipt](archive-receipt.json) provide byte-level preservation checks. No source, core receipt, 056 evidence, GUI, commit or push was changed by this archival task.
'''
(A / 'README.md').write_text(readme)
preserve(Path(__file__), 'finalize-c3-evidence.py')
preserve(ROOT / 'Builds/archive-c3-evidence.py', 'archive-c3-evidence.py')
files = {p.name: dig(p.read_bytes()) for p in sorted(A.iterdir()) if p.is_file() and p.name != 'archive-receipt.json'}
final_receipt = {'generatedAtUtc': NOW, 'archiveIntegrityStatus': 'passed' if all(integrity_checks.values()) else 'failed',
                'strictCStatus': summary['status'], 'matchId': MATCH, 'appVersion': finished['appVersion'], 'buildGuid': finished['buildGuid'],
                'sourceFingerprint': frozen['sourceFingerprint'], 'coverageGaps': summary['coverageGaps'],
                'fileCountExcludingReceipt': len(files), 'bytesExcludingReceipt': sum(x['bytes'] for x in files.values()),
                'manifestScope': 'Every final regular file in this archive, excluding this receipt itself. The preliminary receipt remains an unchanged historical subset checkpoint.',
                'files': files}
write('archive-receipt.json', final_receipt)
assert all(integrity_checks.values()), integrity_checks
assert all(dig((A / n).read_bytes()) == row for n, row in files.items())
print(json.dumps({'archive': str(A), 'integrity': final_receipt['archiveIntegrityStatus'], 'strictC': summary['status'],
                  'coverageGaps': summary['coverageGaps'], 'filesExcludingReceipt': len(files),
                  'receipt': dig((A / 'archive-receipt.json').read_bytes()), 'cannonDamageEvents': len(cannon_damage),
                  'cannonEnemyBuildingDamageEvents': len(enemy_building_hits)}))
