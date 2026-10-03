#!/usr/bin/env python3
"""Finalize only archived, naturally ended C6; never read current runtime data."""
import datetime
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
A = ROOT / 'Docs/Evidence/055/NormalPlayer/river-attempt-006'
MATCH = '7558b19fc9984578a313082b633c6630'
NOW = datetime.datetime.now(datetime.timezone.utc).isoformat()


def dig(data):
    return {'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()}


def read(name):
    return json.loads((A / name).read_text())


def write(name, value):
    (A / name).write_text(json.dumps(value, indent=2, ensure_ascii=False, allow_nan=False) + '\n')


summary = read('summary.json')
lineage = summary['lineage']['players'][0]
rules = read('Rules.json')
definitions = {x['name']: x for x in rules['entities']}
frozen = read('frozen-build-audit-corrected.json')
frozen_bytes = (A / 'frozen-build-audit-corrected.json').read_bytes()
finished_path = A / '20261003-092107-193-7558b19fc9984578a313082b633c6630-match_finished.json'
menu_path = A / '20261003-092155-226-7558b19fc9984578a313082b633c6630-menu_exit.json'
events_path = finished_path.with_name(finished_path.stem + '-events.jsonl')
menu_events = menu_path.with_name(menu_path.stem + '-events.jsonl')
finished, menu = json.loads(finished_path.read_text()), json.loads(menu_path.read_text())
events = [json.loads(x) for x in events_path.read_text().splitlines()]
log_bytes = [(A / ('native-input-full-' + name)).read_bytes() for name in ('Player-ended.log', 'Player-menu.log')]
log_lines = log_bytes[1].decode().splitlines(keepends=True)
expected_logs = [(1436529, '7fc13556c97f8bd16bf4ae44dba11c9aee2d1c98f62a6ac39d948a41ac9ac3e4'),
                 (1442069, '322608e5ec91560eb9c9a8719b33d81274ebce7de41bbc2cdf487289ffa40d3d')]
logs = [{'file': 'native-input-full-' + name, **dig(data),
         'matchesSuppliedFrozenHash': dig(data) == {'bytes': expected[0], 'sha256': expected[1]}}
        for name, data, expected in zip(('Player-ended.log', 'Player-menu.log'), log_bytes, expected_logs)]
excerpt = ''.join(log_lines[11916:]).encode()
(A / 'native-input-c6-menu-through-exit.log').write_bytes(excerpt)
write('log-preservation-audit.json', {'generatedAtUtc': NOW, 'sources': logs,
      'endedLogExactPrefixOfMenuLog': log_bytes[1].startswith(log_bytes[0]),
      'excerpt': {'file': 'native-input-c6-menu-through-exit.log', 'firstSourceLine': 11917,
                  'lastSourceLine': len(log_lines), **dig(excerpt)},
      'scope': 'Two complete frozen shared-process snapshots retain earlier history. The C6 excerpt begins with its opening-menu F9 input and ends after its after-menu F9. No later shared log or C7 file is read.'})
diffs = {k: {'match_finished': finished.get(k), 'menu_exit': menu.get(k)}
         for k in set(finished) | set(menu) if finished.get(k) != menu.get(k)}
pair_checks = {'eventFilesByteIdentical': events_path.read_bytes() == menu_events.read_bytes(),
               'onlyReasonAndManagedBytesDiffer': set(diffs) == {'reason', 'managedBytes'},
               'sameMatchId': finished['matchId'] == menu['matchId'] == MATCH,
               'reportCountMatchesRaw': len(events) == finished['eventCount'] == menu['eventCount'] == 60226,
               'reasonsMatch': finished['reason'] == 'match_finished' and menu['reason'] == 'menu_exit'}
write('report-pair-audit.json', {'generatedAtUtc': NOW, 'status': 'passed' if all(pair_checks.values()) else 'failed',
      'checks': pair_checks, 'differences': diffs,
      'sources': {p.name: dig(p.read_bytes()) for p in [finished_path, menu_path, events_path, menu_events]}})

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
paid_cannon = next(x for x in lineage['training'] if x['spawn']['entity'] == 256)
attack_orders = [{'line': i, 'event': e} for i, e in enumerate(events, 1)
                 if e['kind'] == 'Command' and e['player'] == 0 and e['entity'] == 256 and e['target'] == 266]
fires = [{'line': i, 'event': e} for i, e in enumerate(events, 1)
         if e['kind'] == 'Fire' and e['entity'] == 256 and e['target'] == 266]
cannon_checks = {'threePositiveEnemyBuildingHits': len(building_hits) == 3,
    'sourceIsPaidCannon256': paid_cannon['definition'] == 'cannon' and all(x['event']['entity'] == 256 for x in building_hits),
    'targetIsEnemyFarm266': all(x['event']['target'] == 266 and x['targetDefinition'] == 'farm' for x in building_hits),
    'damageTotals390': sum(x['event']['amount'] for x in building_hits) == 390,
    'canonicalLinesMatch': [x['line'] for x in building_hits] == [x['line'] for x in summary['cannonEnemyBuildingDamageEvidence']],
    'lastFireBeforeDeathLastImpactAfterDeath': fires[-1]['event']['time'] < deaths[256]['event']['time'] < building_hits[-1]['event']['time']}
write('cannon-building-audit.json', {'generatedAtUtc': NOW, 'status': 'passed' if all(cannon_checks.values()) else 'failed',
    'checks': cannon_checks, 'sourceEvents': events_path.name, 'paidCannon': paid_cannon,
    'sourceSpawn': births[256], 'targetSpawn': births[266], 'attackOrders': attack_orders,
    'fires': fires, 'positiveBuildingDamage': building_hits, 'sourceDeath': deaths[256],
    'allPlayerCannonDamage': cannon_damage,
    'interpretation': 'Three paid-cannon hits total390. The first two Damage records precede the explicit954.114075s Attack command. The third projectile was fired before the cannon died and hit after its death. Exact damage comes from raw records, not the already reduced Farm bar or a toast.'})

capture_rows = read('capture-audit.json')['captures']


def cap(prefix):
    return next(x for x in capture_rows if x['json'].startswith(prefix))


def context_for(prefix):
    c = cap(prefix); metadata = read(c['json']); candidates = []
    for line_no, raw in enumerate(log_lines[11916:c['sourceLogLine'] - 1], 11917):
        m = re.match(r'FRONTIER_UI context entity=(\d+) count=(\d+) world=([\d.]+) frame=(\d+)', raw)
        if m: candidates.append({'sourceLine': line_no, 'raw': raw.rstrip(), 'entity': int(m[1]),
            'count': int(m[2]), 'worldTimeRounded': float(m[3]), 'frame': int(m[4])})
    context = candidates[-1]; entity = context['entity']; birth = births[entity]; death = deaths.get(entity)
    return {'capture': c['json'], 'captureSourceLine': c['sourceLogLine'], 'captureWorldTime': c['worldTime'],
        'captureFrame': metadata['captureFrame'], 'requestFrame': metadata['requestFrame'],
        'latestPrecedingContext': context, 'sourceSpawn': birth, 'death': death,
        'paidTraining': next((x for x in lineage['training'] if x['spawn']['entity'] == entity), None),
        'interveningTrace': [{'sourceLine': i, 'raw': raw.rstrip()} for i, raw in
            enumerate(log_lines[context['sourceLine']:c['sourceLogLine'] - 1], context['sourceLine'] + 1) if raw.startswith('FRONTIER_')],
        'aliveAtCapture': birth['event']['time'] <= c['worldTime'] and (death is None or c['worldTime'] < death['event']['time']),
        'contextFrameNotLaterThanCapture': context['frame'] <= metadata['captureFrame'],
        'scope': 'Selected ID is from frozen native input context; F9 JSON has no selectedEntity field.'}


visual_specs = [
 ('Frontier-20261003-081347', 'Opening menu: River mirror, Easy, era limit5, seed314159, Military25/25.'),
 ('Frontier-20261003-083930', 'Single Musketeer at09:34: HP200/200, ATK24, RANGE12, POP1, Holding/Orders0.'),
 ('Frontier-20261003-084653', 'Single old Musketeer at12:53: HP240/240, ATK29, RANGE12, POP1, Holding/Orders0.'),
 ('Frontier-20261003-085053', 'Single new Musketeer at14:06: HP240/240, ATK29, RANGE12, POP1, Idle/Orders0.'),
 ('Frontier-20261003-090208', 'At15:57 no unit is selected; enemy Farm has a reduced health bar and Attack order accepted remains visible. This is after the cannon death and third impact; the toast/bar alone does not quantify damage.'),
 ('Frontier-20261003-090305', 'Single River Carabineer at16:01: HP145/145, ATK15, RANGE12, POP1, Idle/Orders0. Five friendly cavalry are visible.'),
 ('Frontier-20261003-092137', 'DEFEAT22m47s; player kills/losses65/84, age3 versus4, shipments8 versus10. Raw MatchEnded/noResign resolves the generic elimination-or-resignation label.')]
visual = [{'capture': cap(prefix)['json'], 'png': cap(prefix)['png'], 'worldTime': cap(prefix)['worldTime'],
    'reading': reading, 'provenance': 'Original archived PNG inspected with view_image; no edited or generated pixels.'}
    for prefix, reading in visual_specs]
contexts = [context_for(x) for x in ('Frontier-20261003-083930', 'Frontier-20261003-084653', 'Frontier-20261003-085053')]
tech = next(x for x in lineage['technologies'] if x['definition'] == 'veteran_musketeer')
old_pre, old_post, new_post = contexts; tech_time = tech['completion']['time']
technology_checks = {'sameOldEntity162SingleSelected': [x['latestPrecedingContext']['entity'] for x in contexts[:2]] == [162, 162] and all(x['latestPrecedingContext']['count'] == 1 for x in contexts),
    'aliveAndFramesOrdered': all(x['aliveAtCapture'] and x['contextFrameNotLaterThanCapture'] for x in contexts),
    'oldBirthPreTechnologyPostOrder': old_pre['sourceSpawn']['event']['time'] < old_pre['captureWorldTime'] < tech_time < old_post['captureWorldTime'],
    'newPaidAndBornAfterTechnology': tech_time < new_post['paidTraining']['payment']['time'] < new_post['sourceSpawn']['event']['time'] < new_post['captureWorldTime'],
    'newEntity263DifferentFromOld': new_post['latestPrecedingContext']['entity'] == 263 != 162,
    'allPaidMusketeers': all(x['paidTraining']['definition'] == 'musketeer' for x in contexts),
    'technologyAppliedAndCompletedTogether': tech['applied']['time'] == tech_time,
    'actualPixelsReadBaseOldThenUpgradedOldAndNew': True}
base = next(x for x in rules['entities'] if x['id'] == 'musketeer')
modifier = next(x for x in rules['techs'] if x['id'] == 'veteran_musketeer')
write('visual-and-technology-audit.json', {'generatedAtUtc': NOW, 'status': 'passed' if all(technology_checks.values()) else 'failed',
    'checks': technology_checks, 'technology': tech, 'unitContextAssociations': contexts,
    'carabineerContext': context_for('Frontier-20261003-090305'), 'visualReadings': visual,
    'ruleValues': {'baseHP': base['hp'], 'baseAttack': base['damage'], 'healthBonus': modifier['health'], 'attackBonus': modifier['attack'],
        'expectedMaxHP': base['hp']*(1+modifier['health']), 'expectedAttackBeforeDisplayRounding': base['damage']*(1+modifier['attack'])},
    'scope': 'Seven original images visually read; all11 F9 pairs byte/metadata checked. Same-ID162 single selection is established by input context and alive intervals, not the group3 label alone. New263 is separately paid/born after technology. No cross-match substitution.'})

living_counts = [sum(births[n]['event']['player'] == player and (births[n]['definition'] or {}).get('kind') in (0, 1) for n in living) for player in (0, 1)]
natural = [{'line': i, 'event': e} for i, e in enumerate(events, 1) if e['kind'] == 'MatchEnded']
resign = [{'line': i, 'event': e} for i, e in enumerate(events, 1) if e['kind'] == 'Resign']
late_age = [{'line': i, 'event': e} for i, e in enumerate(events, 1) if e['player'] == 0
    and e['kind'] in ('Payment', 'ProductionQueued', 'AgeAdvanced') and any('age '+str(n) in e['message'].lower() for n in (4, 5))]
write('natural-end-and-age-audit.json', {'generatedAtUtc': NOW, 'matchEnded': natural, 'resignEvents': resign,
    'livingUnitsAndBuildingsAtEnd': living_counts, 'canonicalLivingCounts': summary['livingUnitsAndBuildingsAtEnd'],
    'completedPlayerAges': lineage['ages'], 'ageIVAndVPaymentQueueOrCompletionEvents': late_age,
    'townCenterDeath': deaths[1], 'scope': 'AgeIV payment1190.856323 precedes TC death1292.431519 by101.575195 seconds, less than the130-second age duration. No AgeIV/V completion; V was not paid. Payment is not completion.'})

ux_commands = [{'line': i, 'event': e} for i, e in enumerate(events, 1)
    if e['kind'] == 'Command' and e['player'] == 0 and e['entity'] == 255 and 1040 <= e['time'] <= 1162]
ux_trace = [{'sourceLine': i, 'raw': raw.rstrip()} for i, raw in enumerate(log_lines[14553:14668], 14554) if raw.startswith('FRONTIER_')]
write('input-ux-audit.json', {'generatedAtUtc': NOW, 'scope': 'Ended C6 only. Raw command/input facts are separate from static explanations and operator intent. No cause is assigned to every missed click.',
    'cannonGroupFirstActor255Commands': ux_commands, 'actorSpawn': births[255], 'actorDeath': deaths[255],
    'commandPositionMeaning': 'Command.position is the first accepted actor position emitted by World.Emit, not the clicked order destination. The group record gives first actor/count, not all member IDs.',
    'nativeInputTrace': ux_trace, 'resumeRealtime': 19693.175618, 'AKeyRealtime': 19693.1884,
    'leftDownRealtime': 19693.2965, 'leftUpRealtime': 19693.3471,
    'leftDownAfterResumeSeconds': 19693.2965-19693.175618,
    'commandsFromResumeTo1162': [{'line': i, 'event': e} for i, e in enumerate(events, 1) if e['kind']=='Command' and e['player']==0 and 1153.465 <= e['time'] <= 1162],
    'observations': ['L44965 at1142.568115 and L45553 at1152.465698 are Move x3 with target0, not Attack.',
        'Actor255 position changes from(29.041500,38.136761) to(51.049656,48.226082), then death at1153.965332. These are sampled actor positions, not click destinations or proof of all group trajectories.',
        'After resume1153.465, A is logged and left-down/up occur120.882/171.482ms after resume. No player Command appears before1162. Static RtsInput has a0.22s pointer barrier and permits keys immediately; this sequence is consistent with a suppressed target click and still-pending A mode, but the trace has no direct AttackMode state field.',
        'The first ADVANCE AGE click logs pointer down/up/click at1161.563/1161.663 but no action record; after a later Escape, the next click has an action at1189.957 and actual age payment1190.856. Do not replace these facts with an unlogged mode-state assertion.'],
    'staticSources': {'RtsInput.cs': '31-32,102-112,139-175,190,257-284', 'World.cs': '92-96,153'},
    'existingStaticDiscussion': '../InputProjection is not in this archive; repository Docs/Evidence/055/InputProjection/README.md remains unchanged. No new A-D gate or broader .32 delta.'})

refs = set()
def collect(obj):
    if isinstance(obj, dict):
        if isinstance(obj.get('line'), int) and 'kind' in obj: refs.add(obj['line'])
        for value in obj.values(): collect(value)
    elif isinstance(obj, list):
        for value in obj: collect(value)
for key in ('ages','technologies','training','buildings','treasure','tradeXP','resourceModes','resourceDeliveries'): collect(lineage[key])
for key in ('shipmentSentEvidence','shipmentArrivedEvidence','openingEvidence','naturalEndEvidence','cannonEnemyBuildingDamageEvidence'): collect(summary[key])
refs.update(x['line'] for x in attack_orders + fires + ux_commands + late_age)
refs.update([births[266]['line'], deaths[256]['line'], deaths[162]['line'], deaths[263]['line'], deaths[1]['line'], deaths[255]['line']])
write('key-events-raw.json', {'source': events_path.name, 'lineNumbering': 'One-based original JSONL lines',
    'events': [{'line': i, 'event': events[i-1]} for i in sorted(refs)]})

copies = read('source-copy-audit.json')['sources']
copy_checks = {name: dig((A/name).read_bytes()) == {'bytes': row['bytes'], 'sha256': row['sha256']} for name,row in copies.items()}
preliminary_checks = {name: dig((A/name).read_bytes()) == row for name,row in read('preliminary-archive-receipt.json')['files'].items()}
static_checks = {name: dig((A/name).read_bytes()) == {'bytes': row['bytes'], 'sha256': row['sha256']} for name,row in read('static-source-copy-audit.json').items()}
checks = {'originalCopiedBytesUnchanged': all(copy_checks.values()), 'preliminaryCheckpointUnchanged': all(preliminary_checks.values()),
    'staticSourceCopiesMatchFrozenEvidence': all(static_checks.values()),
    'correctedFrozenSourceAndRuntimeAuditPassed': frozen['status']=='passed' and all(frozen['checks'].values()),
    'correctedFrozenAuditUnchanged': (A/'frozen-build-audit-corrected.json').read_bytes()==frozen_bytes,
    'originalFailedToolProbePreserved': read('frozen-build-audit.json')['status']=='failed' and dig((A/'frozen-build-audit.json').read_bytes())['sha256']==frozen['correction']['initialAuditSha256'],
    'all11CapturePairsPass': len(capture_rows)==11 and all(all(c['checks'].values()) for c in capture_rows),
    'twoMenuNineInMatchCaptures': sum(c['inMenu'] for c in capture_rows)==2,
    'frozenLogsHashesAndPrefix': all(x['matchesSuppliedFrozenHash'] for x in logs) and log_bytes[1].startswith(log_bytes[0]),
    'reportPairVerified': all(pair_checks.values()), 'paidCannonEnemyBuildingDamage': all(cannon_checks.values()),
    'sameOldAndNewTechnologyAssociation': all(technology_checks.values()),
    'oneNaturalEndNoResign': len(natural)==1 and not resign and living_counts==[0,67]==summary['livingUnitsAndBuildingsAtEnd'],
    'strictFailurePreciselyIVAndV': summary['status']=='failed' and summary['failedChecks']==['requiredPlayerCoverage'] and summary['coverageGaps']==['ageIV','ageV']}
assert all(checks.values()), checks
write('verification.json', {'generatedAtUtc': NOW, 'archiveIntegrityStatus': 'passed', 'strictCStatus': summary['status'],
    'strictChecksPassed': sum(summary['checks'].values()), 'strictChecksTotal': len(summary['checks']),
    'coveragePassed': sum(summary['coverage'].values()), 'coverageTotal': len(summary['coverage']),
    'failedChecks': summary['failedChecks'], 'coverageGaps': summary['coverageGaps'], 'checks': checks,
    'copiedBytesAudit': copy_checks, 'preliminaryCheckpointAudit': preliminary_checks, 'staticSourceCopyAudit': static_checks,
    'naturalEnd': summary['naturalEndEvidence'], 'livingUnitsAndBuildingsAtEnd': living_counts,
    'rootIndependentReview': 'Root separately reran strict to ignored Builds/C6-independent-review/summary.json (exit1, same16/17 and17/19, ageIV/ageV) and independently verified357 source files plus3 runtime hashes. Those ignored files are not dependencies of this published archive.',
    'scope': 'Archive integrity PASS preserves strict gameplay failure and the original failed helper probe. Finalization reads archive copies only; no current game or C7 metadata.'})

age_rows='\n'.join(f"| {x['age']} | {x['payment']['time']:.6f} | {x['completion']['time']:.6f} | {x['payment']['line']} / {x['completion']['line']} |" for x in lineage['ages'])
coverage_rows='\n'.join(f"| `{k}` | {'PASS' if v else '**FAIL**'} |" for k,v in summary['coverage'].items())
capture_md='\n'.join(f"| {i} | {'menu' if c['inMenu'] else format(c['worldTime'],'.6f')} | [PNG]({c['png']}) · [JSON]({c['json']}) | {c['sourceLogLine']} |" for i,c in enumerate(capture_rows,1))
xp=sum(x['delivery']['amount'] for x in lineage['tradeXP'])
readme=f'''# River normal-player attempt006 — failed strict C, preserved

C6 naturally ended in **Defeat at 1367.213257 simulation seconds (22m47s)**. River mirror, Easy, seed314159, V cap, Military25/25, normal1x, autoplay false. Canonical strict C returns **16/17 checks and 17/19 coverage**; only **`ageIV` and `ageV`** are missing. IV was paid but did not complete; V was not paid. Other matches are not combined into acceptance. See [summary](summary.json), [invocation](audit-invocation.json), [CLI exit1](verify-player-cli.txt) and [archive verification](verification.json).

## Identity and original records

Match `{MATCH}`, version0.2.31, GUID `128a731d440e4b7a94abfcc74b88fa97`, source fingerprint `{frozen['sourceFingerprint']}`. All357 source files and all3 runtime hashes match the frozen .31 build. Root independently reproduced the same strict result and source/runtime match; ignored independent-review files are not required by this self-contained archive.

- [Natural-end report]({finished_path.name}) and [events]({events_path.name}): 60,226 events; MatchEnded line60193, no Resign, living units/buildings [0,67]. PlayerIII versus opponentIV, kills/losses65/84, ships8 versus10.
- [Menu-exit report]({menu_path.name}) and [events]({menu_events.name}) have byte-identical events. Only reason and managedBytes differ between reports; [pair audit](report-pair-audit.json).
- Frozen [ended log](native-input-full-Player-ended.log):1,436,529B, SHA `7fc13556c97f8bd16bf4ae44dba11c9aee2d1c98f62a6ac39d948a41ac9ac3e4`; [menu log](native-input-full-Player-menu.log):1,442,069B, SHA `322608e5ec91560eb9c9a8719b33d81274ebce7de41bbc2cdf487289ffa40d3d`. Ended bytes are an exact menu-log prefix. [C6 excerpt](native-input-c6-menu-through-exit.log) preserves original full-log lines11917–{len(log_lines)}; both full shared-process logs retain preceding history. [Log audit](log-preservation-audit.json).

No C7 runtime log, report, capture metadata or GUI was read. The [preliminary receipt](preliminary-archive-receipt.json) is unchanged; the [final receipt](archive-receipt.json) covers every final file except itself.

## Frozen-audit tool correction, original failure retained

The initial helper copy accidentally replaced lowercase `c5` inside its hardcoded fingerprint while changing C5 labels to C6. The actual recomputed source fingerprint, all357 hashes and all3 runtime hashes already matched the true .31 baseline; only the erroneous constant comparison failed. [Initial failed audit](frozen-build-audit.json) and [original helper](archive-ended-c6.py) are unchanged. The comparison literal was corrected in [corrected helper](archive-ended-c6-corrected.py); a separate full357+3 reread passed in [corrected frozen audit](frozen-build-audit-corrected.json), which retains the initial audit SHA and exact explanation. This was an archive-tool error, not a game/source discrepancy. No source or runtime bytes changed.

## Paid cannon hits enemy Farm

Foundry237 paid500W1250C for five cannons at778.956848s (Payment27155). Cannon256 spawned824.045837s (30421); enemy Farm266 spawned852.438904s (31567), owner1/Building, at(96,111).

Three positive Siege Damage records target that Farm: **35232 at947.515686s**, **35466 at952.614441s**, **35716 at957.713196s**, each130, total**390**. The explicit Attack command35548 at954.114075s follows the first two hits. Last Fire35700 at957.413269s precedes cannon Death35712 at957.613220s; its projectile hits after the cannon dies. [Cannon audit](cannon-building-audit.json) retains paid lineage, both entity identities, all Fire/Damage, the command and death.

The957.813171s F9 shows no selected unit, an already reduced enemy Farm health bar and the Attack-accepted toast. Exact damage and projectile timing are established by raw events, not the toast/bar. The earlier operator impression that the cannon had died without a building hit was mistaken.

## Same old Musketeer and newly born upgraded Musketeer

Veteran Musketeer was paid725.070007s (23837), applied/completed765.160217s (25772/25773). Old paid Musketeer162 was born250.206070s (5130), died1146.167236s (45180), and was alive in both single-unit screenshots:

| Capture time | Full-log UI context | Entity/count | Actual pixels |
|---:|---:|---|---|
|574.306824s|13058|162 / 1|HP200/200, ATK24|
|773.458191s|13464|162 / 1|HP240/240, ATK29|
|846.140442s|13697|263 / 1|HP240/240, ATK29|

New paid Musketeer263 was paid806.150208s (29403), born832.143860s (30818), and died1132.770508s (44308). Payment and birth both follow technology completion. Frozen Rules give200HP/24ATK plus20%, resulting240HP/28.8ATK displayed29. [Technology/visual audit](visual-and-technology-audit.json) preserves all contexts, intervening trace and raw birth/death/payment chains. Group3 alone is not used as identity proof. **F9 JSON has no selectedEntity field**; ID comes from frozen UI context, pixels are inspected independently.

## Progression and missing ages

| Age | Paid(s) | Completed(s) | Original payment/completion lines |
|---|---:|---:|---|
{age_rows}

IV was paid1190.856323s (47562), but TC1 died1292.431519s (56453), only101.575195s later, before the130-second upgrade completed. No IV completion or V payment/completion exists. [Natural-end/age audit](natural-end-and-age-audit.json).

Paid births:15 Settlers,15 Musketeers,10 Pikemen,6 Field Cannons,10 River Carabineers. Paid buildings:5 houses,1 trade post,1 barracks,3 banks,1 foundry,1 stable. Cannon255 was a shipment unit and is distinct from paid256–260/347. Single paid Carabineer274 is linked to the961.412292s F9 (HP145/145, ATK15, RANGE12); five cavalry are visible.

Cargo Ledgers completed308.909637s. Six XP deliveries total{xp:g}XP; trade changed toFood323.910553s and five Food deliveries total375F. Three banks generated5409.25C; two treasures were claimed. Final resources384.422058F/1.103040W/1181.625366C. These are ended-game facts, not a general causal claim. [Key raw events](key-events-raw.json) keep one-based original line references.

## Bounded input/UX observations

The [input audit](input-ux-audit.json) preserves actual commands and the19:00–19:22 trace. First actor255 received Move x4 at1059.088501s (40473), Move x3 at1142.568115s (44965), and Move x3 at1152.465698s (45553), all target0. They must not be described as Attack. Actor positions sampled by the latter records move from(29.041500,38.136761) to(51.049656,48.226082); actor255 dies1153.965332s. Command.position is the actor position, not the clicked destination; the record names only the first actor/count.

After resume1153.465, A input is logged, followed by a target left-down120.882ms after resume and left-up171.482ms after resume. No player Command appears through1162s. The copied [input source](RtsInput.cs) has a0.22s pointer barrier while fresh keys are allowed immediately. This is consistent with the click being suppressed and A remaining pending, but the trace contains no direct AttackMode state field. The first ADVANCE AGE pointer click has no action record; the later Escape and second click precede an actual action and payment. The audit distinguishes these trace facts from mode-state inference. Static model/pick geometry does not establish the cause of every missed click. Existing InputProjection documentation is unchanged; no new A–D gate or broader .32 delta is introduced.

## Complete F9 inventory and coverage

All11 pairs are original2560×1440 PNG/JSON:2 menus and9 in-match captures. All hash/dimension/version/GUID/match/HUD checks pass. Seven images were visually interpreted; others are preserved without invented descriptions. No IV/V screenshot is asserted. In-menu match/request IDs are empty and hidden prior HUD values are not treated as a new match.

| # | Simulation seconds | Original pair | Full menu-log line |
|---:|---:|---|---:|
{capture_md}

| Required coverage | Result |
|---|---|
{coverage_rows}

[Copy audit](source-copy-audit.json), [static-source copy audit](static-source-copy-audit.json), [capture audit](capture-audit.json), [verification](verification.json) and [SHA receipt](archive-receipt.json) distinguish complete preservation from strict gameplay failure. No source/core055 receipt/056/canonical file was edited by this archive task.
'''
(A/'README.md').write_text(readme)
(A/'finalize-c6-evidence.py').write_bytes(Path(__file__).read_bytes())
files={p.name:dig(p.read_bytes()) for p in sorted(A.iterdir()) if p.is_file() and p.name!='archive-receipt.json'}
write('archive-receipt.json', {'generatedAtUtc': NOW, 'archiveIntegrityStatus':'passed','strictCStatus':summary['status'],
    'matchId':MATCH,'appVersion':finished['appVersion'],'buildGuid':finished['buildGuid'],
    'sourceFingerprint':frozen['sourceFingerprint'],'coverageGaps':summary['coverageGaps'],
    'fileCountExcludingReceipt':len(files),'bytesExcludingReceipt':sum(x['bytes'] for x in files.values()),
    'manifestScope':'Every final regular file except this receipt; preliminary receipt and initial failed helper probe remain unchanged.', 'files':files})
assert all(dig((A/name).read_bytes())==value for name,value in files.items())
print(json.dumps({'archive':str(A),'archiveIntegrity':'passed','strictC':summary['status'],'gaps':summary['coverageGaps'],
    'filesExcludingReceipt':len(files),'receipt':dig((A/'archive-receipt.json').read_bytes()),
    'totalFiles':len(files)+1,'totalBytes':sum(x['bytes'] for x in files.values())+(A/'archive-receipt.json').stat().st_size,
    'technologySameOldAndNew':all(technology_checks.values()),'cannonBuildingHits':len(building_hits),'buildingDamage':390}))
