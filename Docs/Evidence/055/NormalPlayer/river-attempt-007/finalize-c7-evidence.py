#!/usr/bin/env python3
"""Finalize authorized ended C7 archive; no current runtime/source reads."""
from collections import Counter, defaultdict
import datetime
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
A = ROOT / 'Docs/Evidence/055/NormalPlayer/river-attempt-007'
NOW = datetime.datetime.now(datetime.timezone.utc).isoformat()
MATCH = 'cd717dda89684d87b979629d3fbdf1a9'


def digest(data):
    return {'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()}


def read(name):
    return json.loads((A / name).read_text())


def write(name, value):
    (A / name).write_text(json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + '\n')


summary = read('summary.json')
lineage = summary['lineage']['players'][0]
extract = read('ended-event-extraction-corrected.json')
frozen = read('frozen-build-audit.json')
finished_name = '20261003-113916-806-' + MATCH + '-match_finished.json'
menu_name = '20261003-113921-407-' + MATCH + '-menu_exit.json'
finished, menu = read(finished_name), read(menu_name)
events_name = finished_name[:-5] + '-events.jsonl'
menu_events_name = menu_name[:-5] + '-events.jsonl'
events = [json.loads(line) for line in (A/events_name).read_text().splitlines()]
captures = read('capture-audit.json')['captures']
log_data = (A/'native-input-full-Player-menu.log').read_bytes()
log_lines = log_data.decode().splitlines(keepends=True)
assert digest(log_data) == {'bytes': 1996408, 'sha256': '56f13d4eaec6caf58aa14580cc2299cf7b2b9e884510f5b4251eb4788fe788ab'}
assert len(log_lines) == 20703
excerpt = ''.join(log_lines[14959:]).encode()
(A/'native-input-c7-menu-through-exit.log').write_bytes(excerpt)

additional = {}
def copy_extra(source, name=None):
    name = name or source.name
    data = source.read_bytes()
    target = A/name
    if target.exists():
        assert target.read_bytes() == data, name
    else:
        target.write_bytes(data)
    additional[name] = {'sourcePath': str(source), **digest(data)}

copy_extra(ROOT/'Builds/C7-independent-review/visual.json', 'root-independent-visual.json')
preserved = ROOT/'Builds/NativeInput055-C7-preserved'
menu_jpeg = 'Frontier Reckoning Screenshot 2026-10-03 at 7.39.34 PM.jpeg'
copy_extra(preserved/menu_jpeg)
handoff = read('GUI-HANDOFF.json')
handoff_checks = []
for item in handoff['files']:
    data = (preserved/item['path']).read_bytes()
    assert digest(data) == {'bytes': item['bytes'], 'sha256': item['sha256']}
    archived_name = 'native-input-full-Player-menu.log' if item['path'] == 'Player-menu.log' else item['path']
    archived = A/archived_name
    if archived.exists():
        assert archived.read_bytes() == data
        location = archived_name
        scope = 'Current C7 archive member'
    else:
        # Two previous-menu C6 entries in the operator handoff are not C7 F9.
        prior = A.parent/'river-attempt-006'/item['path']
        assert prior.exists() and prior.read_bytes() == data
        location = '../river-attempt-006/' + item['path']
        scope = 'Previous C6 menu evidence, excluded from C7 capture count'
    handoff_checks.append({**item, 'frozenSourceMatches': True,
                           'archivedLocation': location, 'scope': scope})
write('handoff-preservation-audit.json', {'generatedAtUtc': NOW, 'receipt': 'GUI-HANDOFF.json',
      'receiptSha256': digest((A/'GUI-HANDOFF.json').read_bytes())['sha256'],
      'all11FrozenMembersVerified': len(handoff_checks) == 11,
      'members': handoff_checks,
      'scope': 'Operator handoff receipt contains two C6 menu files; they remain in C6 and are not counted or substituted as C7 evidence.'})
write('log-preservation-audit.json', {'generatedAtUtc': NOW,
      'originalFrozenMenuLog': {'file': 'native-input-full-Player-menu.log', **digest(log_data)},
      'endedSnapshotStatus': 'unavailable', 'reason': 'natural-end-to-menu-Escape',
      'prefixCheck': 'not_applicable_no_ended_snapshot', 'derivedEndedSnapshotCreated': False,
      'excerpt': {'file': 'native-input-c7-menu-through-exit.log', **digest(excerpt),
                  'firstSourceLine': 14960, 'lastSourceLine': len(log_lines),
                  'scope': 'Clearly labelled excerpt of the original complete menu log, not an ended snapshot.'},
      'terminalTrace': [{'sourceLine': line, 'raw': log_lines[line-1].rstrip()}
                        for line in range(20654, 20693) if log_lines[line-1].startswith('FRONTIER_')],
      'reportBeforeMenuBeforeF9': {'matchFinishedLine': 20663, 'escapeLine': 20676,
                                  'menuExitLine': 20677, 'F9InputLine': 20691, 'F9SavedLine': 20692}})

diffs = {k: {'match_finished': finished.get(k), 'menu_exit': menu.get(k)}
         for k in set(finished)|set(menu) if finished.get(k) != menu.get(k)}
pair_checks = {'sameMatchId': finished['matchId'] == menu['matchId'] == MATCH,
               'eventFilesByteIdentical': (A/events_name).read_bytes() == (A/menu_events_name).read_bytes(),
               'onlyReasonAndManagedBytesDiffer': set(diffs) == {'reason', 'managedBytes'},
               'eventCountMatches': len(events) == finished['eventCount'] == menu['eventCount'] == 116739}
write('report-pair-audit.json', {'generatedAtUtc': NOW, 'checks': pair_checks,
      'status': 'passed' if all(pair_checks.values()) else 'failed', 'differences': diffs,
      'sources': {name: digest((A/name).read_bytes()) for name in (finished_name, menu_name, events_name, menu_events_name)}})
natural = extract['naturalEnd']
natural_checks = {'oneNaturalMatchEnded': len(natural['matchEnded']) == 1,
                  'noResign': not natural['resignEvents'], 'winnerPlayer0': finished['winner'] == 0,
                  'loserAllUnitsAndBuildingsEliminated': natural['livingUnitsAndBuildingsByPlayer'] == [50, 0],
                  'canonicalLivingCountsAgree': natural['livingUnitsAndBuildingsByPlayer'] == summary['livingUnitsAndBuildingsAtEnd'],
                  'allRequiredAgesActuallyCompleted': [a['age'] for a in lineage['ages']] == [2, 3, 4, 5]}
write('natural-end-and-age-audit.json', {'generatedAtUtc': NOW, 'checks': natural_checks,
      'status': 'passed' if all(natural_checks.values()) else 'failed', **natural,
      'ages': lineage['ages'], 'playerFinalState': finished['players'][0],
      'opponentFinalState': finished['players'][1],
      'scope': 'Natural victory is proved by original MatchEnded/no-Resign and living ledger. Generic GUI elimination-or-resignation text alone is insufficient. All ages use completion, not payment.'})

first = extract['paidCannonEnemyBuildingDamage'][0]
cannon = next(c for c in extract['cannons'] if c['entity'] == 261)
all_hits = extract['paidCannonEnemyBuildingDamage'] + extract['unpaidCannonEnemyBuildingDamage']
target_totals = defaultdict(lambda: {'hits': 0, 'loggedDamage': 0})
for hit in extract['paidCannonEnemyBuildingDamage']:
    target = str(hit['event']['target'])
    target_totals[target]['hits'] += 1
    target_totals[target]['loggedDamage'] += hit['event']['amount']
cannon_checks = {'firstSourcePaidCannon261': cannon['paidLineage']['definition'] == 'cannon',
                 'firstHitEnemyPost179': first['event']['target'] == 179 and first['targetSpawn']['definition']['id'] == 'trade_post' and first['targetSpawn']['event']['player'] == 1,
                 'firstHit130': first['event']['amount'] == 130,
                 'firstHitRawLine34670': first['line'] == 34670,
                 'paidHits172': len(extract['paidCannonEnemyBuildingDamage']) == 172,
                 'paidLoggedDamage44200': extract['paidCannonBuildingDamageTotal'] == 44200,
                 'allSource192LinesMatchCanonical': sorted(h['line'] for h in all_hits) == [h['line'] for h in summary['cannonEnemyBuildingDamageEvidence']],
                 'correctedExtractionReferencesMatch': extract['referenceIssues'] == []}
write('cannon-building-audit.json', {'generatedAtUtc': NOW, 'checks': cannon_checks,
      'status': 'passed' if all(cannon_checks.values()) else 'failed',
      'firstPaidProof': {'cannon': cannon, 'targetSpawn': first['targetSpawn'], 'damage': first},
      'paidHits': extract['paidCannonEnemyBuildingDamage'], 'paidHitsByTarget': dict(target_totals),
      'paidHitCount': 172, 'paidLoggedDamage': 44200, 'allSourceHitCount': len(all_hits),
      'nonPaidSourceHitCount': len(extract['unpaidCannonEnemyBuildingDamage']),
      'scope': 'Paid source261 alone proves the required building hit. Canonical cannon evidence includes all sources; the20 additional non-paid hits remain separate. Damage sums are logged amounts, not capped HP removal. Initial auxiliary comparison scope mismatch and its correction are preserved.'})

def cap(prefix):
    return next(row for row in captures if row['json'].startswith(prefix))

def context(prefix):
    c = cap(prefix)
    preceding = []
    for line, raw in enumerate(log_lines[14959:c['sourceLogLine']-1], 14960):
        match = re.match(r'FRONTIER_UI context entity=(\d+) count=(\d+) world=([\d.]+) frame=(\d+)', raw)
        if match:
            preceding.append({'sourceLine': line, 'raw': raw.rstrip(), 'entity': int(match[1]),
                              'count': int(match[2]), 'worldTimeRounded': float(match[3]), 'frame': int(match[4])})
    return {'capture': c['json'], 'captureSourceLine': c['sourceLogLine'],
            'worldTime': c['worldTime'], 'latestPrecedingContext': preceding[-1]}

readings = [
    ('Frontier-20261003-092247', 'Opening menu: River League mirror, Easy, era limit5/5, Military25/25, seed314159.'),
    ('Frontier-20261003-102656', '17:13, enemy Trading Post selected; HP1648/1760.'),
    ('Frontier-20261003-102719', '17:22, same enemy Trading Post selected; HP1518/1760.'),
    ('Frontier-20261003-103213', '18:53, single River Carabineer, HP145/145, ATK15, RANGE12, POP1.'),
    ('Frontier-20261003-104637', '23:16, AGE4 and Reached age4; selected Town Center HP3500/3500.'),
    ('Frontier-20261003-110237', '29:00, AGE5 and Reached age5; selected Town Center HP3500/3500.')]
visual_rows = [{'json': cap(prefix)['json'], 'png': cap(prefix)['png'],
                'worldTime': cap(prefix)['worldTime'], 'reading': reading,
                'method': 'Original archived PNG inspected with view_image; pixels not altered.'}
               for prefix, reading in readings]
victory_name = 'Frontier Reckoning Screenshot 2026-10-03 at 7.39.21 PM.jpeg'
write('visual-and-sky-provenance.json', {'generatedAtUtc': NOW, 'nativeF9Readings': visual_rows,
      'enemyPostContexts': [context('Frontier-20261003-102656'), context('Frontier-20261003-102719')],
      'carabineerContext': context('Frontier-20261003-103213'),
      'carabineerPaidLineage': next(t for t in lineage['training'] if t['spawn']['entity'] == 293),
      'victoryImage': {'file': victory_name, **digest((A/victory_name).read_bytes()),
                       'sourceFrozenPath': str(preserved/victory_name), 'dimensions': [2560, 1504],
                       'nativeF9': False, 'method': 'Actual Sky screenshot copied losslessly by GUI operator; independently inspected with view_image by scale_run and root.',
                       'visible': {'result': 'VICTORY', 'elapsed': '42m8s', 'playerAge': 5, 'opponentAge': 4,
                                   'playerKills': 300, 'playerLosses': 115, 'shipmentsEach': 14},
                       'scope': 'No fabricated NativeCapture metadata or F9 identity claim. Match binding uses operator handoff, visible terminal values and frozen report/input sequence.'},
      'missing': {'terminalNativeF9': True, 'separateEndedLog': True, 'reason': 'natural-end-to-menu-Escape'},
      'technologyAudit': 'technology-visual-audit.json',
      'scope': 'Six native PNGs plus the actual Sky victory JPEG read here. Technology images audited separately by native_playtest. Other pairs are preserved and metadata-checked without invented visual descriptions.'})

root_summary = read('root-independent-summary.json')
root_frozen = read('root-independent-frozen-build.json')
root_full_comparison = read('root-independent-strict-comparison.json')
root_match_keys = ('status', 'checks', 'failedChecks', 'coverage', 'coverageGaps', 'match', 'playerPaidCounts', 'playerPaidBuildingCounts', 'livingUnitsAndBuildingsAtEnd')
independent_checks = {key: root_summary[key] == summary[key] for key in root_match_keys}
write('root-independent-comparison.json', {'generatedAtUtc': NOW, 'strictComparison': independent_checks,
      'rootFrozenPassed': root_frozen['status'] == 'passed',
      'sources': {name: digest((A/name).read_bytes()) for name in ('root-independent-summary.json', 'root-independent-frozen-build.json', 'root-independent-visual.json')},
      'scope': 'Original independent outputs copied unchanged from ignored Builds/C7-independent-review. File path fields retain original provenance. Independent357-source/3-runtime read completed at11:44:27Z, before .32 changes.'})

write('additional-source-copy-audit.json', {'generatedAtUtc': NOW, 'sources': additional})
(A/'finalize-c7-evidence.py').write_bytes(Path(__file__).read_bytes())
# Technology reviewer writes independent outputs; final seal waits for those copies.
technology = read('technology-visual-audit.json')
assert technology['result'] == 'PASS', technology.get('result')
assert len(technology['checks']) == 19 and all(technology['checks'].values())
assert technology['matchId'] == MATCH
prelim = read('preliminary-archive-receipt.json')['files']
original_copy = read('source-copy-audit.json')['sources']
integrity_checks = {
    'preliminaryMembersUnchanged': all(digest((A/name).read_bytes()) == entry for name, entry in prelim.items()),
    'allOriginalSourceCopiesUnchanged': all(digest((A/name).read_bytes()) == {'bytes': entry['bytes'], 'sha256': entry['sha256']} for name, entry in original_copy.items()),
    'frozen357Source3RuntimePassed': frozen['status'] == 'passed' and all(frozen['checks'].values()),
    'rootIndependentStrictMatches': all(independent_checks.values()),
    'rootIndependent22FieldsMatch': root_full_comparison['status'] == 'passed' and len(root_full_comparison['equalFields']) == 22 and all(root_summary[key] == summary[key] for key in root_full_comparison['equalFields']),
    'rootIndependentFrozenPassed': root_frozen['status'] == 'passed',
    'all24CapturePairsPass': len(captures) == 24 and all(all(c['checks'].values()) for c in captures),
    'twoMenus22InMatch': sum(c['inMenu'] for c in captures) == 2,
    'reportPairPassed': all(pair_checks.values()), 'naturalEndAndAgesPassed': all(natural_checks.values()),
    'paidCannonBuildingDamagePassed': all(cannon_checks.values()),
    'technologyLineageVisualPassed': technology['result'] == 'PASS' and len(technology['checks']) == 19 and all(technology['checks'].values()) and technology['matchId'] == MATCH,
    'strict17of17Coverage19of19': summary['status'] == 'passed' and sum(summary['checks'].values()) == len(summary['checks']) == 17 and sum(summary['coverage'].values()) == len(summary['coverage']) == 19,
    'noCoverageGaps': summary['coverageGaps'] == [],
    'missingSnapshotNotFabricated': not (A/'native-input-full-Player-ended.log').exists(),
    'elevenHandoffSourceHashesVerified': len(handoff_checks) == 11,
}
assert all(integrity_checks.values()), integrity_checks
write('verification.json', {'generatedAtUtc': NOW, 'archiveIntegrityStatus': 'passed', 'strictCStatus': 'passed',
      'strictChecksPassed': 17, 'strictChecksTotal': 17, 'coveragePassed': 19, 'coverageTotal': 19,
      'coverageGaps': [], 'checks': integrity_checks,
      'preservationDeviation': {'endedSnapshot': 'unavailable', 'terminalNativeF9': 'unavailable',
                               'reason': 'natural-end-to-menu-Escape', 'actualSkyVictoryImagePreserved': True},
      'scope': 'C7 same-match acceptance only; earlier failures remain unchanged. Source/runtime freeze captured before .32 edits. Finalization reads archived data and explicit completed reviewers, never live runtime.'})

age_rows = '\n'.join(f"|{r['age']}|{r['payment']['time']:.6f}|{r['completion']['time']:.6f}|{r['payment']['line']}/{r['completion']['line']}|" for r in lineage['ages'])
capture_rows = '\n'.join(f"|{i}|{'menu' if c['inMenu'] else format(c['worldTime'], '.6f')}|[PNG]({c['png']}) · [JSON]({c['json']})|{c['sourceLogLine']}|" for i, c in enumerate(captures, 1))
coverage_rows = '\n'.join(f'|`{k}`|PASS|' for k in summary['coverage'])
xp = sum(r['delivery']['amount'] for r in lineage['tradeXP'])
bank = sum(r['income']['amount'] for r in lineage['bankIncome'])
resource = defaultdict(float)
for row in lineage['resourceDeliveries']:
    resource[row['delivery']['message']] += row['delivery']['amount']
readme = f'''# River normal-player attempt007 — strict C passed

C7 naturally ended in **Victory at2528.415771 simulation seconds (42m8s)**, River mirror/Easy/V cap/Military25, seed314159, normal1x, autoplay false. Canonical strict verification passes **17/17 checks and19/19 coverage**, with no gaps. All evidence belongs to match `{MATCH}`, version0.2.31, GUID `128a731d440e4b7a94abfcc74b88fa97`, source fingerprint `{frozen['sourceFingerprint']}`. [Summary](summary.json), [invocation](audit-invocation.json), [CLI](verify-player-cli.txt), [verification](verification.json). Earlier C1–C6 failures remain unchanged; no cross-match coverage union.

## Natural ending and preservation deviation

The [match_finished report]({finished_name}) and [raw events]({events_name}) contain116,739 records. MatchEnded line116720 says all opposing units/buildings eliminated; winner0, no Resign, final living units/buildings[50,0]. Player reachedV versus opponentIV, kills/losses300/115,14 shipments each. [Menu-exit report]({menu_name}) and [events]({menu_events_name}) have byte-identical events; only reason and managedBytes differ. [Pair audit](report-pair-audit.json), [natural-end/age audit](natural-end-and-age-audit.json).

**No separate Player-ended.log and no terminal native F9 were saved.** The final observation's trailing Escape occurred after the Victory panel appeared and returned directly to menu. Frozen log lines20663/20676/20677 show match_finished→Escape→menu_exit; F9 came later at20691/20692. This is explicitly preserved in [deviation](end-capture-deviation.json) and [operator handoff](GUI-HANDOFF.json). No ended snapshot was reconstructed and no F9 was fabricated.

The actual [Sky Victory JPEG](<Frontier Reckoning Screenshot 2026-10-03 at 7.39.21 PM.jpeg>) is251,062B, SHA `10ec18c0de85f2ae2d281f8f0b1bada353b12183a7d2be194ff9689e8df28aca`, original2560×1504. It visibly shows VICTORY42m8s, V versusIV,300/115,14 shipments; independently viewed by scale_run and root. It is **not native F9**, has no invented NativeCapture metadata, and the generic elimination-or-resignation caption alone is not used to prove no Resign. [Visual/provenance audit](visual-and-sky-provenance.json), [root visual review](root-independent-visual.json).

The original complete [frozen menu log](native-input-full-Player-menu.log) is1,996,408B, SHA `56f13d4eaec6caf58aa14580cc2299cf7b2b9e884510f5b4251eb4788fe788ab`. It retains preceding shared-process history. The [C7 excerpt](native-input-c7-menu-through-exit.log) is clearly labelled original lines14960–20703, not an ended snapshot. [Log audit](log-preservation-audit.json). All11 files in the operator handoff were hash-verified; its two preceding C6 menu files remain in C6, excluded from C7's24 F9 pairs. [Handoff audit](handoff-preservation-audit.json). Operator reports a later get_app_state reopened an empty menu, no match started, then a second public QUIT and process check; this later operation is not fabricated into the pre-QUIT frozen log.

## Same-match progression, economy and technology

|Age|Paid(s)|Completed(s)|Original payment/completion lines|
|---|---:|---:|---|
{age_rows}

Paid completions:13 Settlers,41 Musketeers,25 Pikemen,20 Field Cannons,11 River Carabineers,10 Hussars; buildings5 houses,1 Trading Post,1 Barracks,4 banks,1 Foundry,1 Stable,3 Farms. Two treasures,14 arrived shipments, bank income{bank:g}C, six XP deliveries totalling{xp:g}XP and30 resource deliveries ({resource['Food']:g}F/{resource['Wood']:g}W) are linked to original payments/events. Cargo Ledgers completed557.510925s. Resource mode changed toFood565.109070s andWood611.897644s.

Veteran Musketeer paid948.415466s, applied/completed988.505676s. The independent [technology lineage/visual audit](technology-visual-audit.json) links the **same old Musk221** before/after to frozen UI context, payment, Spawn/Death and pixels:860.536926s HP200/200 ATK24→990.505188s HP240/240 ATK29. New paid Musk290 was born1104.277466s after technology and is single-selected at1111.775635s, HP240/240 ATK29. The1110.575928s capture has context0 and is not substituted as a new-unit attribute proof. F9 JSON contains no selectedEntity field. See [independent technology notes](technology-visual-audit.md).

Single paid Carabineer293 is shown at1133.170410s HP145/145 ATK15 RANGE12 POP1; its paid lineage and context are in the visual audit. Native1396.306152s/1740.522095s F9s show AGE4/AGE5 and TC3500/3500. Six non-technology native PNGs and the real Sky victory image were inspected here; the technology reviewer records its own actual image readings.

## Paid cannon building damage

Foundry242 paid500W1250C for five cannons at942.416931s (line30339); cannon261 spawned987.505920s (32142). Attack line34475 at1034.694458s targets enemy Trading Post179. Damage line34670 at1039.293335s is130 Siege from that paid cannon to the enemy Building. Original F9 at1033.094849s shows HP1648/1760 and at1042.192627s HP1518/1760; both frozen UI contexts select179. The source/target/payment/Fire/Damage chain establishes attribution, not a toast alone.

Across the full same match, paid cannons have172 positive enemy-building Damage events with logged total44,200. The canonical list has192 all-source cannon hits, including20 non-paid-source hits; these are separated, and damage sums are not capped net HP removal. [Cannon audit](cannon-building-audit.json). Veteran/Guard/Imperial Field Cannon upgrades completed1677.237549/1735.623291/1840.697632s and are retained in the raw lineage.

The auxiliary extractor's first comparison incorrectly compared172 paid-only lines with the canonical192 all-source list. Both original and corrected extraction/code are retained; same-scope comparison reconciles all192 lines and has no reference issues. [Scope correction](extraction-scope-correction.json), [corrected extraction](ended-event-extraction-corrected.json). This affected no canonical strict result or raw gameplay record.

## Freeze and independent review

[Frozen audit](frozen-build-audit.json) passed all357 source-file hashes, combined fingerprint and three actual runtime hashes before any .32 change. Root independently reread357+3 at11:44:27Z and reran strict on the explicit original report; [independent frozen output](root-independent-frozen-build.json), [independent strict output](root-independent-summary.json), [22-field comparison](root-independent-strict-comparison.json), [copy comparison](root-independent-comparison.json). The outputs are copied unchanged, retaining original path provenance. This archive remains .31 gameplay evidence; it does not claim a later version's tests, runtime or menu smoke.

The [preliminary receipt](preliminary-archive-receipt.json), original [copy audit](source-copy-audit.json), actual raw report/events/F9 and corrected archive helper stay unchanged. Final [receipt](archive-receipt.json) covers every final regular file except itself. Original capture/log bytes are not normalized. No source/core055/canonical/056 file or GUI was changed by finalization.

## All native F9 pairs

24 original PNG/JSON pairs:2 menus and22 in-match captures; all SHA/dimension/version/GUID/match/request/HUD checks passed. There is no terminal native F9. In-menu match IDs are blank; hidden prior HUD IDs are not a new-match identity claim.

|#|Simulation seconds|Original pair|Frozen log line|
|---:|---:|---|---:|
{capture_rows}

|Required coverage|Result|
|---|---|
{coverage_rows}
'''
(A/'README.md').write_text(readme)
files = {p.name: digest(p.read_bytes()) for p in sorted(A.iterdir()) if p.is_file() and p.name != 'archive-receipt.json'}
write('archive-receipt.json', {'generatedAtUtc': NOW, 'archiveIntegrityStatus': 'passed', 'strictCStatus': 'passed',
      'matchId': MATCH, 'appVersion': finished['appVersion'], 'buildGuid': finished['buildGuid'],
      'sourceFingerprint': frozen['sourceFingerprint'], 'strictChecks': '17/17', 'coverage': '19/19',
      'coverageGaps': [], 'fileCountExcludingReceipt': len(files),
      'bytesExcludingReceipt': sum(v['bytes'] for v in files.values()),
      'preservationDeviation': 'No terminal native F9 or separate ended snapshot; actual Sky Victory JPEG, frozen full menu log and both terminal ledgers preserved.',
      'manifestScope': 'Every final regular file except this receipt; preliminary checkpoint unchanged.', 'files': files})
print(json.dumps({'archive': str(A), 'members': len(files), 'totalFiles': len(files)+1,
      'totalBytes': sum(v['bytes'] for v in files.values())+(A/'archive-receipt.json').stat().st_size,
      'receipt': digest((A/'archive-receipt.json').read_bytes()), 'allChecksPassed': all(integrity_checks.values())}))
