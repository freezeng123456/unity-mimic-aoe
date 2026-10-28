#!/usr/bin/env python3
"""Finalize only copied, naturally ended C5 evidence. Never read live runtime state."""
import datetime
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
A = ROOT / 'Docs/Evidence/055/NormalPlayer/river-attempt-005'
MATCH = 'd69b5316feda422f8a27d23545e13c5b'
NOW = datetime.datetime.now(datetime.timezone.utc).isoformat()


def dig(data):
    return {'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()}


def read(name):
    return json.loads((A / name).read_text())


def write(name, data):
    (A / name).write_text(json.dumps(data, ensure_ascii=False, indent=2, allow_nan=False) + '\n')


summary = read('summary.json')
lineage = summary['lineage']['players'][0]
rules = read('Rules.json')
definitions = {x['name']: x for x in rules['entities']}
frozen = read('frozen-build-audit.json')
frozen_bytes = (A / 'frozen-build-audit.json').read_bytes()
finished_path = A / '20261003-081035-834-d69b5316feda422f8a27d23545e13c5b-match_finished.json'
menu_path = A / '20261003-081203-427-d69b5316feda422f8a27d23545e13c5b-menu_exit.json'
events_path = finished_path.with_name(finished_path.stem + '-events.jsonl')
menu_events = menu_path.with_name(menu_path.stem + '-events.jsonl')
finished, menu = json.loads(finished_path.read_text()), json.loads(menu_path.read_text())
events = [json.loads(line) for line in events_path.read_text().splitlines()]
log_paths = [A / ('native-input-full-' + name) for name in ('Player-ended.log', 'Player-menu.log')]
log_bytes = [p.read_bytes() for p in log_paths]
expected_logs = [('Player-ended.log', 1141452, '7b65d7aeed3316d20516540518aaa0d26ab99c2be483d8c0a94c12f75e71cded'),
                 ('Player-menu.log', 1146985, '72baf7f599209dd49578596d0f0988da6be8fcd86d3da617d6908e74498a0cc0')]
logs = [{'archive': p.name, 'rootReportedSource': 'Builds/NativeInput055-C5-preserved/' + exp[0],
         **dig(data), 'matchesRootFrozenHash': dig(data) == {'bytes': exp[1], 'sha256': exp[2]}}
        for p, data, exp in zip(log_paths, log_bytes, expected_logs)]
log_lines = log_bytes[1].decode('utf-8').splitlines(keepends=True)
excerpt = ''.join(log_lines[7651:]).encode()
(A / 'native-input-c5-menu-through-exit.log').write_bytes(excerpt)
write('log-preservation-audit.json', {'generatedAtUtc': NOW, 'status': 'passed', 'sources': logs,
      'endedLogExactPrefixOfMenuLog': log_bytes[1].startswith(log_bytes[0]), 'sourceLineCount': len(log_lines),
      'excerpt': {'file': 'native-input-c5-menu-through-exit.log', 'firstSourceLine': 7652,
                  'lastSourceLine': len(log_lines), **dig(excerpt)},
      'scope': 'Full frozen shared-process snapshots retain preceding match history. C5 excerpt starts with the Coast-to-River opponent menu click and ends after the final menu F9. No subsequently appended Player.log or other match was read.'})

diffs = {k: {'match_finished': finished.get(k), 'menu_exit': menu.get(k)}
         for k in set(finished) | set(menu) if finished.get(k) != menu.get(k)}
pair_checks = {'eventFilesByteIdentical': events_path.read_bytes() == menu_events.read_bytes(),
               'onlyReasonAndManagedBytesDiffer': set(diffs) == {'reason', 'managedBytes'},
               'sameMatchId': finished['matchId'] == menu['matchId'] == MATCH,
               'reportCountMatchesRaw': len(events) == finished['eventCount'] == menu['eventCount'] == 79417,
               'reasonsMatch': finished['reason'] == 'match_finished' and menu['reason'] == 'menu_exit'}
write('report-pair-audit.json', {'generatedAtUtc': NOW, 'status': 'passed' if all(pair_checks.values()) else 'failed',
      'checks': pair_checks, 'differences': diffs, 'sources': {p.name: dig(p.read_bytes()) for p in [finished_path, menu_path, events_path, menu_events]}})

births, deaths, living, cannon_damage = {}, {}, set(), []
for line, e in enumerate(events, 1):
    if e['kind'] == 'Spawn':
        births[e['entity']] = {'line': line, 'event': e, 'definition': definitions.get(e['message'])}
        living.add(e['entity'])
    if e['kind'] == 'Death':
        deaths[e['entity']] = {'line': line, 'event': e}
        living.discard(e['entity'])
    if e['kind'] == 'Damage' and e['player'] == 0:
        source, target = births.get(e['entity'], {}), births.get(e['target'], {})
        if (source.get('definition') or {}).get('id') == 'cannon':
            cannon_damage.append({'line': line, 'event': e, 'sourceSpawnLine': source['line'],
                'targetSpawnLine': target.get('line'), 'targetOwner': target.get('event', {}).get('player'),
                'targetDefinition': (target.get('definition') or {}).get('id'),
                'targetKind': (target.get('definition') or {}).get('kind')})
building_hits = [x for x in cannon_damage if x['targetOwner'] == 1 and x['targetKind'] == 1 and x['event']['amount'] > 0]
paid_cannon = next(x for x in lineage['training'] if x['spawn']['entity'] == 261)
attack_orders = [{'line': i, 'event': e} for i, e in enumerate(events, 1)
                 if e['kind'] == 'Command' and e['player'] == 0 and e['entity'] == 261 and e['target'] == 239]
cannon_checks = {'twoPositiveEnemyBuildingHits': len(building_hits) == 2,
                 'samePaidCannon261': paid_cannon['definition'] == 'cannon' and all(x['event']['entity'] == 261 for x in building_hits),
                 'enemyFoundry239': all(x['event']['target'] == 239 and x['targetDefinition'] == 'foundry' for x in building_hits),
                 'recordedDamage260': sum(x['event']['amount'] for x in building_hits) == 260,
                 'matchesCanonicalLines': [x['line'] for x in building_hits] == [x['line'] for x in summary['cannonEnemyBuildingDamageEvidence']]}
write('cannon-building-audit.json', {'generatedAtUtc': NOW, 'status': 'passed' if all(cannon_checks.values()) else 'failed',
      'checks': cannon_checks, 'eventFile': events_path.name, 'eventsScanned': len(events),
      'paidCannon': paid_cannon, 'sourceSpawn': births[261], 'targetSpawn': births[239],
      'attackOrders': attack_orders, 'positiveBuildingDamage': building_hits, 'cannonDeath': deaths[261],
      'allPlayerCannonDamage': cannon_damage,
      'scope': 'Independent C5 raw event reconstruction, including paid source lineage and enemy Building identity; no toast or other game substitutes for Damage.'})

capture_rows = read('capture-audit.json')['captures']
def cap(prefix):
    return next(c for c in capture_rows if c['json'].startswith(prefix))

visual_specs = [
 ('Frontier-20261003-063821', 'Opening menu: River mirror, Easy, era limit 5, seed314159, Military preset 25/25.'),
 ('Frontier-20261003-071956', 'Single Musketeer before upgrade: HP200/200, ATK24, RANGE12, POP1, Reloading/Orders1.'),
 ('Frontier-20261003-072026', 'Single old Musketeer after upgrade: HP240/240, ATK29, RANGE12, POP1, Holding/Orders0.'),
 ('Frontier-20261003-072219', 'Single newly born Musketeer: HP240/240, ATK29, RANGE12, POP1, Idle/Orders0.'),
 ('Frontier-20261003-072710', 'Selected enemy Foundry before the cannon command: HP1760/1760; visible enemy building and friendly cannon.'),
 ('Frontier-20261003-072733', 'Selected Field Cannon at16:21: HP220/220, ATK70, RANGE20, POP4, Reloading/Orders1; visible enemy Foundry health bar reduced. Exact damage comes from raw events.'),
 ('Frontier-20261003-073316', 'Five selected River Carabineers at18:00, displayed HP145/145, ATK15, RANGE12, POP1.'),
 ('Frontier-20261003-073738', 'AgeIV HUD at19:15; Town Center HP2840/3500, 13 sheltered settlers and10/10 firing; Food Convoy3 arrival notification.'),
 ('Frontier-20261003-081058', 'Natural-end image: DEFEAT27m41s, player98/96 kills/losses, age4 versus5, shipments9 versus12. The generic elimination-or-resignation text is disambiguated by raw MatchEnded/noResign.')]
visual = [{'capture': cap(prefix)['json'], 'png': cap(prefix)['png'], 'worldTime': cap(prefix)['worldTime'],
           'reading': value, 'provenance': 'Archived original PNG inspected with view_image; no generated or edited pixels.'} for prefix, value in visual_specs]

def context_for(prefix):
    c = cap(prefix)
    metadata = read(c['json'])
    candidates = []
    for line_no, text in enumerate(log_lines[7651:c['sourceLogLine'] - 1], 7652):
        m = re.match(r'FRONTIER_UI context entity=(\d+) count=(\d+) world=([\d.]+) frame=(\d+)', text)
        if m:
            candidates.append({'sourceLine': line_no, 'raw': text.rstrip(), 'entity': int(m[1]),
                'count': int(m[2]), 'worldTimeRounded': float(m[3]), 'frame': int(m[4])})
    context = candidates[-1]
    entity = context['entity']
    birth, death = births[entity], deaths.get(entity)
    return {'capture': c['json'], 'captureSourceLine': c['sourceLogLine'], 'captureWorldTime': c['worldTime'],
            'captureFrame': metadata['captureFrame'], 'requestFrame': metadata['requestFrame'],
            'latestPrecedingContext': context, 'sourceSpawn': birth, 'death': death,
            'paidTraining': next((x for x in lineage['training'] if x['spawn']['entity'] == entity), None),
            'aliveAtCapture': birth['event']['time'] <= c['worldTime'] and (death is None or c['worldTime'] < death['event']['time']),
            'contextFrameNotLaterThanCapture': context['frame'] <= metadata['captureFrame'],
            'scope': 'Entity ID comes from the frozen native UI context, not from F9 JSON.'}

contexts = [context_for(p) for p in ['Frontier-20261003-071956', 'Frontier-20261003-072026', 'Frontier-20261003-072219']]
tech = next(x for x in lineage['technologies'] if x['definition'] == 'veteran_musketeer')
old_pre, old_post, new_post = contexts
tech_time = tech['completion']['time']
technology_checks = {
    'sameOldEntity173SingleSelected': [x['latestPrecedingContext']['entity'] for x in contexts[:2]] == [173, 173] and all(x['latestPrecedingContext']['count'] == 1 for x in contexts),
    'allContextsAliveAndFrameOrdered': all(x['aliveAtCapture'] and x['contextFrameNotLaterThanCapture'] for x in contexts),
    'oldBornBeforePreAndTechnology': old_pre['sourceSpawn']['event']['time'] < old_pre['captureWorldTime'] < tech_time < old_post['captureWorldTime'],
    'newPaidAndBornAfterTechnology': tech_time < new_post['paidTraining']['payment']['time'] < new_post['sourceSpawn']['event']['time'] < new_post['captureWorldTime'],
    'newEntity256DifferentFromOld': new_post['latestPrecedingContext']['entity'] == 256 != 173,
    'allPaidMusketeers': all(x['paidTraining']['definition'] == 'musketeer' for x in contexts),
    'technologyAppliedAndCompletedTogether': tech['applied']['time'] == tech['completion']['time'],
    'pixelsReadBaseThenUpgradedOldAndNew': True}
base = next(d for d in rules['entities'] if d['id'] == 'musketeer')
modifier = next(d for d in rules['techs'] if d['id'] == 'veteran_musketeer')
write('visual-and-technology-audit.json', {'generatedAtUtc': NOW, 'status': 'passed' if all(technology_checks.values()) else 'failed',
      'checks': technology_checks, 'visualReadings': visual, 'technology': tech,
      'unitContextAssociations': contexts, 'cannonCaptureContexts': [context_for('Frontier-20261003-072710'), context_for('Frontier-20261003-072733')],
      'ruleValues': {'baseHP': base['hp'], 'baseAttack': base['damage'], 'healthBonus': modifier['health'], 'attackBonus': modifier['attack'],
                     'expectedMaxHP': base['hp'] * (1 + modifier['health']), 'expectedAttackBeforeDisplayRounding': base['damage'] * (1 + modifier['attack'])},
      'interpretation': 'Same paid old Musketeer173 is associated with both825.245544s(base200HP/24ATK) and838.142395s(upgraded240HP/29ATK) single-unit screenshots. Newly paid/born Musketeer256 after the836.342834s completion is separately displayed with upgraded240HP/29ATK at874.833435s.',
      'limitations': 'F9 JSON has no selectedEntity. IDs are from original input UI context and raw birth/death chronology; pixels are read separately. Nine images were visually interpreted; all17 pairs were byte/metadata checked. No cross-game substitution.'})

living_counts = [sum(births[n]['event']['player'] == player and (births[n]['definition'] or {}).get('kind') in (0, 1) for n in living) for player in (0, 1)]
natural = [{'line': i, 'event': e} for i, e in enumerate(events, 1) if e['kind'] == 'MatchEnded']
resign = [{'line': i, 'event': e} for i, e in enumerate(events, 1) if e['kind'] == 'Resign']
age_v = [{'line': i, 'event': e} for i, e in enumerate(events, 1) if e['player'] == 0 and e['kind'] in ('Payment', 'ProductionQueued', 'AgeAdvanced') and 'age 5' in e['message'].lower()]
write('natural-end-and-age-audit.json', {'generatedAtUtc': NOW, 'matchEnded': natural, 'resignEvents': resign,
      'livingUnitsAndBuildingsAtEnd': living_counts, 'canonicalLivingCounts': summary['livingUnitsAndBuildingsAtEnd'],
      'playerAges': lineage['ages'], 'playerAgeVPaymentQueueOrCompletionEvents': age_v,
      'scope': 'AgeV was neither paid nor completed in this ended match. All surviving entities are reconstructed from Spawn/Death and frozen definition kinds.'})

refs = set()
def collect_refs(obj):
    if isinstance(obj, dict):
        if isinstance(obj.get('line'), int) and 'kind' in obj:
            refs.add(obj['line'])
        for value in obj.values():
            collect_refs(value)
    elif isinstance(obj, list):
        for value in obj:
            collect_refs(value)
for key in ('ages', 'technologies', 'training', 'buildings', 'treasure', 'tradeXP', 'resourceModes', 'resourceDeliveries'):
    collect_refs(lineage[key])
for key in ('shipmentSentEvidence', 'shipmentArrivedEvidence', 'openingEvidence', 'naturalEndEvidence', 'cannonEnemyBuildingDamageEvidence'):
    collect_refs(summary[key])
refs.update([x['line'] for x in attack_orders] + [births[239]['line'], deaths[261]['line'], deaths[173]['line'], deaths[256]['line']])
write('key-events-raw.json', {'source': events_path.name, 'lineNumbering': 'One-based original JSONL lines',
      'scope': 'Exact C5 progression, technology, paid units, trade, treasures, building damage and natural-end payloads. Full raw events remain authoritative.',
      'events': [{'line': i, 'event': events[i - 1]} for i in sorted(refs)]})

copies = read('source-copy-audit.json')['sources']
copy_checks = {name: dig((A / name).read_bytes()) == {'bytes': row['bytes'], 'sha256': row['sha256']} for name, row in copies.items()}
preliminary = read('preliminary-archive-receipt.json')['files']
preliminary_checks = {name: dig((A / name).read_bytes()) == row for name, row in preliminary.items()}
checks = {'originalCopiedBytesUnchanged': all(copy_checks.values()), 'preliminaryCheckpointBytesUnchanged': all(preliminary_checks.values()),
          'frozenSourceAndRuntimePassedBeforeRelease': frozen['status'] == 'passed' and all(frozen['checks'].values()),
          'frozenBuildAuditRetainedUnchanged': (A / 'frozen-build-audit.json').read_bytes() == frozen_bytes,
          'all17CapturePairsPass': len(capture_rows) == 17 and all(all(c['checks'].values()) for c in capture_rows),
          'twoMenuFifteenMatchCaptures': sum(c['inMenu'] for c in capture_rows) == 2,
          'logsPreservedWithEndedPrefix': all(x['matchesRootFrozenHash'] for x in logs) and log_bytes[1].startswith(log_bytes[0]),
          'reportsAndEventsPair': all(pair_checks.values()), 'paidCannonEnemyBuildingDamage': all(cannon_checks.values()),
          'sameOldAndNewMuskTechnologyAssociation': all(technology_checks.values()),
          'oneNaturalEndWithoutResign': len(natural) == 1 and not resign and living_counts == [0, 95] == summary['livingUnitsAndBuildingsAtEnd'],
          'noPlayerAgeVPaymentOrCompletion': not age_v,
          'strictFailurePreciselyAgeV': summary['status'] == 'failed' and summary['failedChecks'] == ['requiredPlayerCoverage'] and summary['coverageGaps'] == ['ageV']}
write('verification.json', {'generatedAtUtc': NOW, 'archiveIntegrityStatus': 'passed' if all(checks.values()) else 'failed',
      'strictCStatus': summary['status'], 'strictChecksPassed': sum(summary['checks'].values()), 'strictChecksTotal': len(summary['checks']),
      'coveragePassed': sum(summary['coverage'].values()), 'coverageTotal': len(summary['coverage']),
      'failedChecks': summary['failedChecks'], 'coverageGaps': summary['coverageGaps'], 'checks': checks,
      'copiedBytesAudit': copy_checks, 'preliminaryCheckpointAudit': preliminary_checks,
      'naturalEnd': summary['naturalEndEvidence'], 'livingUnitsAndBuildingsAtEnd': living_counts,
      'scope': 'Archive integrity PASS preserves strict gameplay failure. Frozen source/runtime audit was completed separately before any release edits; finalization reads archive copies only.'})

age_rows = '\n'.join(f"| {x['age']} | {x['payment']['time']:.6f} | {x['completion']['time']:.6f} | {x['payment']['line']} / {x['completion']['line']} |" for x in lineage['ages'])
coverage_rows = '\n'.join(f"| `{key}` | {'PASS' if value else '**FAIL**'} |" for key, value in summary['coverage'].items())
captures_md = '\n'.join(f"| {i} | {'menu' if c['inMenu'] else format(c['worldTime'], '.6f')} | [{c['png']}]({c['png']}) · [JSON]({c['json']}) | {c['sourceLogLine']} |" for i, c in enumerate(capture_rows, 1))
readme = f'''# River normal-player attempt005 — failed strict C, preserved

C5 ended naturally in **Defeat at1661.541382 simulation seconds (27m41s)**. River mirror, Easy, seed314159, V cap, Military25/25, normal1x and autoplay false. Canonical strict C returns **16/17 checks and18/19 coverage**; the only gap is **`ageV`**. PlayerV was neither paid nor completed. This remains a failed C attempt; other matches are not combined with it. See [strict summary](summary.json), [invocation and exit1](audit-invocation.json), [CLI](verify-player-cli.txt) and [archive verification](verification.json).

## Identity, raw records and preservation

- Match `{MATCH}`, version0.2.31, GUID`128a731d440e4b7a94abfcc74b88fa97`, source fingerprint`{frozen['sourceFingerprint']}`. [Frozen build audit](frozen-build-audit.json) verified all357 current source hashes and three .31 runtime hashes before any .32 change. Finalization preserves that result without re-reading current source or app.
- [Natural-end report]({finished_path.name}) / [events]({events_path.name}) contain79,417 records. MatchEnded is original line79380; noResign exists, living units/buildings reconstruct to[0,95]. PlayerIV versus opponentV, kills/losses98/96, shipments9 versus12. [Natural-end/age audit](natural-end-and-age-audit.json).
- [Menu-exit report]({menu_path.name}) / [events]({menu_events.name}) have identical raw events; report differences are only reason and managedBytes. [Pair audit](report-pair-audit.json).
- Frozen [ended log](native-input-full-Player-ended.log):1,141,452B, SHA`7b65d7aeed3316d20516540518aaa0d26ab99c2be483d8c0a94c12f75e71cded`. Frozen [menu log](native-input-full-Player-menu.log):1,146,985B, SHA`72baf7f599209dd49578596d0f0988da6be8fcd86d3da617d6908e74498a0cc0`. Ended bytes are an exact menu-log prefix. Complete snapshots retain earlier process history; [C5 excerpt](native-input-c5-menu-through-exit.log) is full-log lines7652–{len(log_lines)}, from the opponent-menu click through final menu F9. [Log audit](log-preservation-audit.json).

No later live shared log, report, capture or GUI was read. The [preliminary checkpoint](preliminary-archive-receipt.json) retains its original subset hashes; the [final receipt](archive-receipt.json) covers every final file except itself.

## Paid cannon damages enemy Foundry

Foundry235 paid100W250C at836.842712s (Payment27038, queue27039). Field Cannon261 actually spawned at881.931702s (29161, completion29162). Enemy Foundry239 is player1/Building, spawned812.448669s (25914) at(96,111). Attack command34219 at973.209412s targets239.

Raw positive Siege damage: **line34455 at978.008240s:130**, **line34716 at983.106995s:130**; total260. Cannon261 later died1104.877319s. [Independent cannon audit](cannon-building-audit.json) links payment/birth, target identity, command and both hits, and agrees with canonical evidence lines.

The972.209656s F9 selects enemy Foundry239 and shows1760/1760HP. At981.107483s, F9 selects Cannon261, showsReloading/Orders1, and the Foundry bar is reduced; this image follows the first hit and precedes the second. Exact two-hit damage is taken from raw Damage, not the health-bar pixels or Attack toast.

## Same old Musketeer before/after and new upgraded Musketeer

Veteran Musketeer was paid796.252625s (25116), applied/completed836.342834s (27016/27017). Old paid Musketeer173 was born338.111420s (7361), died918.922668s (31038), and remained alive for both captures:

| Capture time | Frozen UI context line | Entity/count | Actual pixel reading |
|---:|---:|---|---|
|825.245544s|9488|173 /1|HP200/200, ATK24|
|838.142395s|9577|173 /1|HP240/240, ATK29|
|874.833435s|9745|256 /1|HP240/240, ATK29|

New Musketeer256 was paid after technology at840.941711s (27241), born866.935364s (28456), and died931.919495s (31795). Its874.833435s single-unit display therefore belongs to a newly paid and born upgraded unit. Frozen rules give200HP/24attack and +20% →240HP/28.8attack, shown as29. The old173 pair uses the same entity ID, unlike C3's different pre/post IDs.

**F9 JSON has no selectedEntity field.** Entity associations use the preceding original `FRONTIER_UI context` line, its frame/world time and raw Spawn/Death/payment lineage; selected-unit HP/ATK are read separately from actual PNGs. [Visual and technology audit](visual-and-technology-audit.json) contains all payloads, checks and nine explicit visual readings. This demonstrates the C5 technology behavior, while strict C still fails on ageV.

## Paid progression and the remaining gap

|Age|Paid(s)|Completed(s)|Payment/completion raw line|
|---|---:|---:|---:|
{age_rows}

Paid births:22 Settlers,20 Musketeers,10 Pikemen,5 Field Cannons,5 River Carabineers and5 Hussars. Paid buildings:3 houses,1 barracks,1Trading Post,4banks,1Foundry,1Stable and2Farms. Five paid Carabineers are visible in the1080.583252s F9; IV is visible at1155.265015s. NoV screenshot is asserted because V was not reached.

Cargo Ledgers was paid366.013123s, completed411.115875s. Trade switched toFood at537.215881s; seven XP deliveries total330XP,21 Food deliveries total1575F. Four paid banks generated7418C; two treasures were claimed. Final player resources were2924.944824F/122.467712W/2696.176270C. These are ended-game facts, not a cause-of-defeat claim. [Key raw events](key-events-raw.json) retain exact original line numbers.

|Strict coverage|Result|
|---|---|
{coverage_rows}

## All17 F9 pairs

Two menus and15 in-match PNG/JSON pairs are retained at2560×1440. Hashes, dimensions, version/GUID, match/request/HUD identities and times pass. Only the nine images listed in the visual audit were visually interpreted; the remainder are preserved without invented descriptions. In-menu matchId/requestMatchId are empty; hidden prior HUD values are not assigned to a new match.

|#|Simulation time(s)|Original pair|Full menu-log line|
|---:|---:|---|---:|
{captures_md}

[Copy audit](source-copy-audit.json), [capture audit](capture-audit.json), [verification](verification.json) and [SHA receipt](archive-receipt.json) preserve the distinction between archive integrity and strict acceptance. No source/core055 receipt/056/canonical document or subsequent game was changed by this archival task.
'''
(A / 'README.md').write_text(readme)
(A / 'finalize-c5-evidence.py').write_bytes(Path(__file__).read_bytes())
files = {p.name: dig(p.read_bytes()) for p in sorted(A.iterdir()) if p.is_file() and p.name != 'archive-receipt.json'}
write('archive-receipt.json', {'generatedAtUtc': NOW, 'archiveIntegrityStatus': 'passed' if all(checks.values()) else 'failed',
      'strictCStatus': summary['status'], 'matchId': MATCH, 'appVersion': finished['appVersion'], 'buildGuid': finished['buildGuid'],
      'sourceFingerprint': frozen['sourceFingerprint'], 'coverageGaps': summary['coverageGaps'],
      'fileCountExcludingReceipt': len(files), 'bytesExcludingReceipt': sum(x['bytes'] for x in files.values()),
      'manifestScope': 'Every final regular file except this receipt; preliminary receipt remains an unchanged subset checkpoint.',
      'files': files})
assert all(checks.values()), checks
assert all(dig((A / name).read_bytes()) == value for name, value in files.items())
print(json.dumps({'archive': str(A), 'integrity': 'passed', 'strictC': summary['status'], 'gaps': summary['coverageGaps'],
      'filesExcludingReceipt': len(files), 'receipt': dig((A / 'archive-receipt.json').read_bytes()),
      'technologySameOldAndNew': all(technology_checks.values()), 'cannonBuildingHits': len(building_hits), 'buildingDamage':260}))
