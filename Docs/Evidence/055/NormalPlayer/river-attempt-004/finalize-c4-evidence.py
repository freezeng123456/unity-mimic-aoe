#!/usr/bin/env python3
"""Finalize only the already archived C4 and two explicitly frozen C4 logs."""
import collections
import datetime
import hashlib
import json
from pathlib import Path
import re
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
A = ROOT / 'Docs/Evidence/055/NormalPlayer/river-attempt-004'
NOW = datetime.datetime.now(datetime.timezone.utc).isoformat()
MATCH = 'b4250e2ab6664083ad919de284cc0afb'
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

logs = [preserve(ROOT / 'Builds/NativeInput055-C4-preserved' / name, 'native-input-full-' + name)
        for name in ('Player-ended.log', 'Player-menu.log')]
end_bytes = (A / logs[0]['archive']).read_bytes()
menu_bytes = (A / logs[1]['archive']).read_bytes()
assert menu_bytes.startswith(end_bytes)
log_lines = menu_bytes.decode('utf-8').splitlines(keepends=True)
excerpt = ''.join(log_lines[5269:]).encode()
(A / 'native-input-c4-menu-through-exit.log').write_bytes(excerpt)
log_audit = {'generatedAtUtc': NOW, 'status': 'passed', 'sources': logs,
             'endedLogExactPrefixOfMenuLog': True, 'sourceLineCount': len(log_lines),
             'excerpt': {'file': 'native-input-c4-menu-through-exit.log', 'firstSourceLine': 5270,
                         'lastSourceLine': len(log_lines), **dig(excerpt)},
             'scope': 'The two full immutable process snapshots include C2/C3 history. The exact C4 excerpt starts at the menu click changing the opponent from River to Coast, before the C4 menu F9, and ends after C4 menu_exit and its F9. No later runtime log was read.'}
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
               'reportCountMatchesRaw': len(events) == finished['eventCount'] == menu['eventCount'] == 36218,
               'matchFinishedReason': finished['reason'] == 'match_finished', 'menuExitReason': menu['reason'] == 'menu_exit'}
write('report-pair-audit.json', {'generatedAtUtc': NOW, 'status': 'passed' if all(pair_checks.values()) else 'failed',
      'checks': pair_checks, 'differences': report_diffs, 'sources': {p.name: dig(p.read_bytes()) for p in [finished_path, menu_path, events_path, menu_events]}})

births, deaths, cannon_damage = {}, {}, []
attack_orders = []
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
    if e['kind'] == 'Command' and e['player'] == 0 and e['entity'] == 238 and e['target'] == 156:
        attack_orders.append({'line': line, 'event': e})
building_hits = [x for x in cannon_damage if x['targetOwner'] == 1 and x['targetKind'] == 1 and x['event']['amount'] > 0]
paid_cannon = next(x for x in lineage['training'] if x['definition'] == 'cannon')
rebuilt_town = next(x for x in lineage['buildings'] if x['definition'] == 'town')
cannon_checks = {'sixRealEnemyBuildingHits': len(building_hits) == 6,
                 'samePaidCannon238': paid_cannon['spawn']['entity'] == 238 and all(x['event']['entity'] == 238 for x in building_hits),
                 'enemyManor156': all(x['event']['target'] == 156 and x['targetDefinition'] == 'house' for x in building_hits),
                 'totalRecordedSiegeDamage780': sum(x['event']['amount'] for x in building_hits) == 780,
                 'matchesCanonicalEvidenceLines': [x['line'] for x in building_hits] == [x['line'] for x in summary['cannonEnemyBuildingDamageEvidence']]}
write('cannon-building-audit.json', {'generatedAtUtc': NOW, 'status': 'passed' if all(cannon_checks.values()) else 'failed',
      'checks': cannon_checks, 'eventFile': events_path.name, 'eventsScanned': len(events),
      'paidCannon': paid_cannon, 'sourceSpawn': births[238], 'targetSpawn': births[156],
      'attackOrders': attack_orders, 'positiveBuildingDamage': building_hits, 'cannonDeath': deaths[238],
      'allPlayerCannonDamage': cannon_damage,
      'scope': 'Independent ended-C4 event reconstruction links a paid Field Cannon to six positive enemy Building damage records. No GUI toast or other match is substituted for actual Damage.'})

captures = read('capture-audit.json')['captures']
def cap(prefix):
    return next(c for c in captures if c['json'].startswith(prefix))

visual_readings = [
 ('Frontier-20261003-054706', 'Opening menu: River League versus Coast Compact, Easy, age cap 5, seed 314159, Military 25/25.'),
 ('Frontier-20261003-060201', 'Paused at 5:38, Age II, Town Center selected, HP 3488/3500 and no sheltered settlers.'),
 ('Frontier-20261003-061111', 'At 7:54, Trading Post selected, [ACTIVE] Coin, Caravan delivery: Coin and Trade delivery mode changed.'),
 ('Frontier-20261003-061738', 'Age III at 10:53; Reached age 3 message, Town Center HP 2354/3500 and five sheltered/firing settlers.'),
 ('Frontier-20261003-062538', 'At 13:04, single Field Cannon selected, HP 220/220, ATK 70, RANGE 20, POP 4, Moving and Orders 1. Enemy buildings visible; this image alone does not yet prove a hit.'),
 ('Frontier-20261003-062556', 'At 13:23, the Field Cannon is Reloading with Orders 1 and remains HP 220/220. A visible enemy Manor has a partially reduced health bar. Exact target identity and damage amount are established by raw events, not pixel inference.'),
 ('Frontier-20261003-063525', 'DEFEAT at 16m 34s; player kills/losses 29/48, ages 3 versus 4, shipments 6 versus 9. Generic elimination-or-resignation overlay wording is disambiguated by the raw natural MatchEnded/no-Resign audit.')]
visual = [{'capture': cap(prefix)['json'], 'png': cap(prefix)['png'], 'worldTime': cap(prefix)['worldTime'],
           'reading': reading, 'provenance': 'Archived original image visually inspected'} for prefix, reading in visual_readings]
contexts = []
last_context = None
for i, text in enumerate(log_lines, 1):
    if i < 5270:
        continue
    if text.startswith('FRONTIER_UI context '):
        m = re.search(r'entity=(\d+) count=(\d+) world=([\d.]+) frame=(\d+)', text)
        if m:
            last_context = {'sourceLine': i, 'raw': text.rstrip(), 'entity': int(m[1]), 'count': int(m[2]),
                            'worldTimeRounded': float(m[3]), 'frame': int(m[4])}
    if text.startswith('FRONTIER_CAPTURE_OK ') and any(text.rstrip().endswith(cap(p)['json']) for p in ['Frontier-20261003-062538', 'Frontier-20261003-062556']):
        c = next(c for c in captures if text.rstrip().endswith(c['json']))
        contexts.append({'capture': c['json'], 'captureSourceLine': i, 'captureWorldTime': c['worldTime'],
                         'latestPrecedingContext': last_context.copy(), 'paidCannon': paid_cannon,
                         'aliveAtCapture': births[238]['event']['time'] <= c['worldTime'] < deaths[238]['event']['time']})
write('visual-and-feature-audit.json', {'generatedAtUtc': NOW, 'status': 'passed', 'visualReadings': visual,
      'cannonCaptureContexts': contexts, 'paidTechnologies': lineage['technologies'],
      'technologyScope': 'Only Cargo Ledgers completed as a paid player technology in C4. No Veteran Musketeer completion or before/after upgraded-Musketeer proof is claimed.',
      'tradeMode': lineage['resourceModes'], 'tradeXpDeliveries': len(lineage['tradeXP']),
      'tradeXpTotal': sum(x['delivery']['amount'] for x in lineage['tradeXP']),
      'resourceDeliveries': len(lineage['resourceDeliveries']),
      'resourceDeliveryTotal': sum(x['delivery']['amount'] for x in lineage['resourceDeliveries']),
      'limitations': 'Seven original images were visually interpreted; all sixteen pairs received byte/hash/dimension/identity/time checks. F9 JSON has no selectedEntity field; cannon associations use the preceding native input context and raw birth/death chronology. Menu matchId/requestMatchId are empty; hidden HUD identity/time may retain an earlier match.'})

key_lines = set()
def collect_refs(obj):
    if isinstance(obj, dict):
        if isinstance(obj.get('line'), int) and 'kind' in obj:
            key_lines.add(obj['line'])
        for value in obj.values():
            collect_refs(value)
    elif isinstance(obj, list):
        for value in obj:
            collect_refs(value)
for k in ('ages', 'technologies', 'training', 'buildings', 'treasure', 'tradeXP', 'resourceModes', 'resourceDeliveries'):
    collect_refs(lineage[k])
collect_refs(lineage['bankIncome'][:1])
collect_refs(lineage['bankIncome'][-1:])
for k in ('shipmentSentEvidence', 'shipmentArrivedEvidence', 'openingEvidence', 'naturalEndEvidence', 'cannonEnemyBuildingDamageEvidence'):
    collect_refs(summary[k])
key_lines.update([x['line'] for x in attack_orders] + [births[156]['line'], deaths[238]['line'], deaths[1]['line']])
write('key-events-raw.json', {'source': events_path.name, 'lineNumbering': 'One-based original JSONL lines',
      'scope': 'Selected exact raw payloads for C4 paid progression, economy, shipments, building damage, initial-TC loss/reconstruction and natural end. Full raw events remain authoritative.',
      'events': [{'line': line, 'event': events[line - 1]} for line in sorted(key_lines)]})

source_copy = read('source-copy-audit.json')['sources']
copy_checks = {name: dig((A / name).read_bytes()) == {'bytes': row['bytes'], 'sha256': row['sha256']} for name, row in source_copy.items()}
integrity_checks = {'originalCopiedBytesUnchanged': all(copy_checks.values()), 'frozenSourceAndRuntimeMatch': all(frozen_checks.values()),
                    'sixteenCompleteCapturePairs': len(captures) == 16 and all(all(c['checks'].values()) for c in captures),
                    'fourteenMatchAndTwoMenuCaptures': sum(not c['inMenu'] for c in captures) == 14 and sum(c['inMenu'] for c in captures) == 2,
                    'reportPairConsistent': all(pair_checks.values()), 'logSnapshotsPreserved': all(l['bytesMatch'] for l in logs),
                    'paidCannonBuildingDamageConfirmed': all(cannon_checks.values()),
                    'cannonInputContextAndLifetimeMatch': len(contexts) == 2 and all(c['aliveAtCapture'] and c['latestPrecedingContext']['entity'] == 238 and c['latestPrecedingContext']['count'] == 1 for c in contexts),
                    'strictFailurePreciselyThreeGaps': summary['failedChecks'] == ['requiredPlayerCoverage'] and summary['coverageGaps'] == ['ageIV', 'ageV', 'specialUnit']}
write('verification.json', {'generatedAtUtc': NOW, 'archiveIntegrityStatus': 'passed' if all(integrity_checks.values()) else 'failed',
      'strictCStatus': summary['status'], 'strictChecksPassed': sum(summary['checks'].values()), 'strictChecksTotal': len(summary['checks']),
      'coveragePassed': sum(summary['coverage'].values()), 'coverageTotal': len(summary['coverage']),
      'failedChecks': summary['failedChecks'], 'coverageGaps': summary['coverageGaps'], 'checks': integrity_checks,
      'copiedBytesAudit': copy_checks, 'naturalEnd': summary['naturalEndEvidence'],
      'livingUnitsAndBuildingsAtEnd': summary['livingUnitsAndBuildingsAtEnd'],
      'scope': 'C4 archive integrity is distinct from strict gameplay acceptance. No cross-game union or later-match data is used.'})

age_rows = '\n'.join(f"| {x['age']} | {x['payment']['time']:.6f} | {x['completion']['time']:.6f} | {x['payment']['line']} / {x['completion']['line']} |" for x in lineage['ages'])
coverage_rows = '\n'.join(f"| `{k}` | {'PASS' if v else '**FAIL**'} |" for k, v in summary['coverage'].items())
hit_rows = '\n'.join(f"| {x['line']} | {x['event']['time']:.6f} | {x['event']['entity']} → {x['event']['target']} | {x['event']['amount']:.0f} |" for x in building_hits)
capture_rows = '\n'.join(f"| {i} | {'menu' if c['inMenu'] else format(c['worldTime'], '.6f')} | [{c['png']}]({c['png']}) · [JSON]({c['json']}) | {c['sourceLogLine']} |" for i, c in enumerate(captures, 1))
readme = f'''# River normal-player attempt 004 — failed strict C, preserved

C4 ended naturally in **Defeat at {finished['time']:.6f} simulation seconds (16m 34s)**. The frozen `0.2.31` macOS player ran River League versus Coast Compact, Easy, seed 314159, age cap V, Military 25/25, autoplay false and simulation speed 1. The player reached III; the opponent reached IV. Player kills/losses were 29/48 and shipments sent were six.

Canonical strict C returns **16/17 checks and 16/19 coverage items passed**. Only **`ageIV`, `ageV`, `specialUnit`** are missing. **Paid Field Cannon → enemy building damage is confirmed**, as is paid Cargo Ledgers. C4 remains a failed attempt; its successful cannon path is not combined with another match's V/Carabineer path. See [strict summary](summary.json), [exact invocation/exit 1](audit-invocation.json) and [archive verification](verification.json). Integrity PASS does not turn strict C into PASS.

## Identity and preservation

- Match `{MATCH}`, version `0.2.31`, GUID `128a731d440e4b7a94abfcc74b88fa97`; source fingerprint `{frozen['sourceFingerprint']}`. [Frozen build audit](frozen-build-audit.json) checks all 357 source files and three compiled runtime hashes against the preserved .31 receipts.
- [Natural-end report]({finished_path.name}) and [raw events]({events_path.name}) contain 36,218 events. MatchEnded line 36184 at 994.504211 s states all opposing units and buildings eliminated. No Resign exists; reconstructed final living units/buildings are `[0, 77]`. This establishes natural defeat despite the overlay's generic elimination-or-resignation wording.
- [Menu-exit report]({menu_path.name}) and [events]({menu_events.name}) retain byte-identical event data; report differences are only `reason` and `managedBytes`. See [pair audit](report-pair-audit.json).
- [Full ended log](native-input-full-Player-ended.log): 732,582 bytes, SHA256 `763036d6b69eb8285651c303ec9bfb85864d782255e98d5e75c9e797cda17672`.
- [Full menu log](native-input-full-Player-menu.log): 738,112 bytes, SHA256 `a3de7b8f1370ced87bc93bb604ab1af5aeeab8c1f9168b00d7ba804c8f79e7ee`. The ended log is its exact byte prefix. These immutable shared-process snapshots include C2/C3 history; the [exact C4 excerpt](native-input-c4-menu-through-exit.log) covers full-log lines 5270–7651, starting with the River-to-Coast opponent menu click. [Log preservation audit](log-preservation-audit.json) records boundaries and byte identities.
- The helper's timestamped duplicate of the menu snapshot and its preliminary receipt are retained as the unchanged initial preservation checkpoint. [Final receipt](archive-receipt.json) covers every final archive file except itself.

No later live process log or match report was read. All gameplay interpretation below uses this ended C4's copied files.

## Paid cannon and actual building damage

Foundry entity 224 paid for one Field Cannon at **684.479919 s**, raw Payment line 21579 and queue line 21580: **100 Wood / 250 Coin**. Cannon entity **238** spawned at **729.568909 s**, line 23415, followed by paid-training completion line 23416. Enemy entity **156** is a player-1 Manor (`house`, Building kind 1), spawned at 242.505600 s, line 4962.

Raw Command line 25968 at **783.055847 s** is `Attack ×1`, entity 238, target 156. Positive building damage follows:

| Raw line | Time (s) | Source → target | Siege damage |
|---:|---:|---|---:|
{hit_rows}

Total recorded Siege damage is **780**. The cannon died at 826.745178 s after those six hits. [Independent cannon audit](cannon-building-audit.json) preserves complete payloads, identities and paid lineage; its result agrees with the canonical evidence.

The 784.655457 s F9 shows the single cannon still Moving; it is not yet hit proof. The 803.050964 s F9 shows Reloading and a partially reduced Manor health bar. The preceding native UI context (full log line 7247) selects entity 238, count 1; F9 JSON itself has no selectedEntity field. The exact six-hit conclusion comes from Damage events rather than GUI acceptance or estimated pixels. [Visual/feature audit](visual-and-feature-audit.json) separates these observations.

## Progress and remaining gaps

| Age reached | Paid at (s) | Completed at (s) | Payment / completion raw line |
|---|---:|---:|---:|
{age_rows}

Player paid births were 14 Settlers, five Pikemen, eight Musketeers and one Field Cannon. Paid buildings include one house, bank, Trading Post, barracks, tower, Foundry and reconstructed Town Center. Initial TC entity 1 died at 801.351379 s; a new TC entity 249 was paid at 844.440857 s and completed at 867.335266 s. It did not produce a natural win or further age completion.

Cargo Ledgers was paid at **343.911774 s** (line 8489), applied/completed at **389.014526 s** (lines 10344/10345). It is the only paid player technology; **no Veteran Musketeer upgrade or before/after upgraded-unit evidence is claimed**. Trading Post 159 switched to Coin at 474.919769 s, directly visible in its F9. C4 had six XP deliveries totaling 270 XP, 18 Coin deliveries totaling 900 Coin, 570.5 passive bank Coin, and two claimed treasures. These preserve the paidTechnology, trade and bank coverage without implying IV/V or Carabineer completion. [Key raw payloads](key-events-raw.json) retain original one-based event lines.

| Strict coverage | Result |
|---|---|
{coverage_rows}

## Complete F9 inventory

All **16** original PNG/JSON pairs are preserved: two menus and fourteen in-match captures. All PNG hashes, dimensions (2560×1440), build/match identities and world/request/HUD times were checked. Seven images listed in [visual/feature audit](visual-and-feature-audit.json) were visually interpreted. In-menu matchId/requestMatchId are empty; hidden HUD values may retain prior state and are not assigned to the menu's match identity.

| # | Simulation time (s) | Original pair | Full menu-log line |
|---:|---:|---|---:|
{capture_rows}

The existing strict verifier was run once on the archived C4 with scenario `river-v`, deck preset `military`, its opening menu F9 and the frozen .31 version/GUID. [CLI output](verify-player-cli.txt), [copy audit](source-copy-audit.json), [capture audit](capture-audit.json), [verification](verification.json) and [final SHA receipt](archive-receipt.json) make the outcome reviewable. No source, core055 receipt, 056 evidence, other canonical document, GUI, commit or push was changed by this archival task.
'''
(A / 'README.md').write_text(readme)
preserve(Path(__file__), 'finalize-c4-evidence.py')
preserve(ROOT / 'Builds/archive-c4-evidence.py', 'archive-c4-evidence.py')
files = {p.name: dig(p.read_bytes()) for p in sorted(A.iterdir()) if p.is_file() and p.name != 'archive-receipt.json'}
write('archive-receipt.json', {'generatedAtUtc': NOW, 'archiveIntegrityStatus': 'passed' if all(integrity_checks.values()) else 'failed',
      'strictCStatus': summary['status'], 'matchId': MATCH, 'appVersion': finished['appVersion'], 'buildGuid': finished['buildGuid'],
      'sourceFingerprint': frozen['sourceFingerprint'], 'coverageGaps': summary['coverageGaps'],
      'fileCountExcludingReceipt': len(files), 'bytesExcludingReceipt': sum(x['bytes'] for x in files.values()),
      'manifestScope': 'Every final regular file in this archive except this receipt. The preliminary receipt remains an unchanged historical subset checkpoint.',
      'files': files})
assert all(integrity_checks.values()), integrity_checks
assert all(dig((A / n).read_bytes()) == row for n, row in files.items())
print(json.dumps({'archive': str(A), 'integrity': 'passed', 'strictC': summary['status'], 'gaps': summary['coverageGaps'],
                  'filesExcludingReceipt': len(files), 'receipt': dig((A / 'archive-receipt.json').read_bytes()),
                  'cannonBuildingHits': len(building_hits), 'recordedBuildingDamage': 780}))
