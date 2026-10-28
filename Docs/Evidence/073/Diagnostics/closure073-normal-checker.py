#!/usr/bin/env python3
"""Read closed 073 artifacts; never run Unity, a match, a build, or mutate originals."""
import collections
import datetime
import hashlib
import json
import math
import re
import stat
import subprocess
from pathlib import Path

REPO = Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
OUT = Path('/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.16.0/Candidate1')
ROOT = OUT / 'NormalObserved1'
MATRIX = ROOT / 'matrix'
DIAG = OUT / 'Diagnostics'
FP = '9b92812a922adb69a5ae501925417d8eba8e24b836f496274ebc8ec8679ba03d'
COMMIT = '519d7d34a0be85f75ee19e53458c636d1ba0b3e4'
RES = ('food', 'wood', 'coin')
failures = []
check_count = 0


def check(name, condition, details=None):
    global check_count
    check_count += 1
    if not condition:
        failures.append({'check': name, 'details': details})


def load(path):
    return json.loads(path.read_text())


def ref(path):
    before = path.lstat()
    check('ordinary-file:' + str(path), stat.S_ISREG(before.st_mode) and not path.is_symlink())
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    after = path.lstat()
    a = (before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns, before.st_mode)
    b = (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns, after.st_mode)
    check('stable-read:' + str(path), a == b)
    return {'path': str(path), 'bytes': before.st_size, 'sha256': digest.hexdigest(),
            'mode': oct(stat.S_IMODE(before.st_mode)),
            'identity': {'device': before.st_dev, 'inode': before.st_ino, 'mtimeNs': before.st_mtime_ns}}


def vec(value):
    return [float(value[x]) for x in RES]


def close(a, b, tolerance=0.01):
    return all(abs(x - y) <= tolerance for x, y in zip(a, b))


def event_ref(event, index):
    return {'line': index + 1, **{k: event[k] for k in ('time', 'kind', 'message', 'player', 'entity', 'target', 'amount')},
            **({'economy': event['economy']} if event.get('hasEconomy') else {})}


def snapshot_market_check(name, player, prefix):
    income, sold, bought = [0.0] * 3, [0.0] * 3, [0.0] * 3
    pressure = {'food': 0, 'wood': 0}
    for t in prefix:
        if t['player'] != player['id']:
            continue
        for i, value in enumerate(t['income']):
            income[i] += value
        for i, value in enumerate(t['sold']):
            sold[i] += value
        for i, value in enumerate(t['bought']):
            bought[i] += value
        pressure[t['resource']] = t['endPressure']
    for field, expected in (('marketIncome', income), ('marketSold', sold), ('marketBought', bought)):
        check(name + ':' + field, vec(player[field]) == expected, {'observed': player[field], 'expected': expected})
    check(name + ':food-pressure', player['marketFoodPressure'] == pressure['food'])
    check(name + ':wood-pressure', player['marketWoodPressure'] == pressure['wood'])


parse_time = lambda s: datetime.datetime.fromisoformat(s.replace('Z', '+00:00'))
manifest = load(MATRIX / 'manifest.json')
runner = load(ROOT / 'runner-receipt073.json')
declaration = load(DIAG / 'predeclared-observation073.json')
freeze = load(DIAG / 'source-freeze073-before-full.json')
full = load(REPO / 'Builds/Tests/run-9q8yTJKS/source-evidence.json')
native_runner = load(OUT / 'Native1/runner-receipt073.json')
native_scope = load(DIAG / 'native073-scope-audit.json')
rules = load(REPO / 'Assets/Resources/Rules.json')
paths = [MATRIX / 'manifest.json', ROOT / 'runner-receipt073.json', ROOT / 'normal.log',
         DIAG / 'predeclared-observation073.json', DIAG / 'source-freeze073-before-full.json',
         REPO / 'Builds/Tests/run-9q8yTJKS/source-evidence.json', OUT / 'Build1/runner-receipt073.json',
         OUT / 'Native1/runner-receipt073.json', DIAG / 'native073-scope-audit.json',
         DIAG / 'native073-scope-audit.md', REPO / 'Assets/Resources/Rules.json',
         REPO / 'Assets/Frontier/Editor/MarketObservation073.cs',
         REPO / 'Assets/Frontier/Simulation/Market073.cs', REPO / 'Assets/Frontier/Simulation/EconomyEvents.cs',
         REPO / 'Assets/Frontier/Simulation/MatchSimulation.cs', REPO / 'Assets/Frontier/Simulation/Landmarks.cs',
         REPO / 'Assets/Frontier/Simulation/ScenarioCodec.cs', REPO / 'Assets/Frontier/Simulation/RiverContent070.cs',
         DIAG / 'closure073-normal-checker-schema-mismatch-first.json',
         DIAG / 'closure073-normal-checker-schema-mismatch-first.md']
for case in manifest['cases']:
    paths += sorted((MATRIX / case['id']).iterdir())
before_refs = [ref(p) for p in paths]
files = {}
for root_name in ('Assets', 'Packages', 'ProjectSettings', 'Tools'):
    for p in sorted((REPO / root_name).rglob('*')):
        if p.is_file() and not any(x.startswith('.') or x == '__pycache__' for x in p.relative_to(REPO).parts):
            files[p.relative_to(REPO).as_posix()] = hashlib.sha256(p.read_bytes()).hexdigest()
actual_fp = hashlib.sha256(json.dumps(files, sort_keys=True, separators=(',', ':')).encode()).hexdigest()
check('current-freeze-full-732-map', len(files) == 732 and files == freeze['files'] == full['files'])
check('source-fingerprint', actual_fp == FP == runner['sourceFingerprintBefore'] == runner['sourceFingerprintAfter'] == manifest['sourceFingerprint'])
check('runner-closed-source', runner['exitCode'] == 0 and runner['status'] == 'observer_closed_same_source' and runner['sourceStable'] is True)
check('runner-method-no-intervention', runner['method'] == 'Frontier.Editor.MarketObservation073.RunNormal' and runner['preparationRun'] is False and runner['caseInterventions'] is False)
check('runtime-commit', runner['runtimeCommit'] == COMMIT)
check('full-evidence-path', runner['fullTestEvidence'] == str(REPO / 'Builds/Tests/run-9q8yTJKS/source-evidence.json'))
check('manifest-schema-completion', manifest['schema'] == 'normal-market073-v1' and manifest['status'] == 'observed_three_declared_cases' and bool(manifest['finishedUtc']))
check('manifest-version', manifest['version'] == '0.16.0')
check('manifest-bounds', (manifest['limit'], manifest['comparison'], manifest['sampleInterval']) == (2400, 1500, 30))
expected_cases = [('winding-ottoman-44', 'winding', 'ottoman', 44), ('winding-ottoman-45', 'winding', 'ottoman', 45), ('river_bend-river-42', 'traders_bend070', 'river', 42)]
check('exact-three-cases', [(c['id'], c['map'], c['civilization'], c['seed']) for c in manifest['cases']] == expected_cases)
check('predeclared-three-identities', [(c['map'], c['civilization'], c['seed']) for c in declaration['cases']] == [('winding','ottoman',44),('winding','ottoman',45),('river_bend','river',42)])
check('predeclared-observer-and-source', declaration['sourceFingerprint'] == FP and declaration['observerSourceSha256'] == files['Assets/Frontier/Editor/MarketObservation073.cs'])
check('predeclared-no-gifts-orders-intervention', declaration['bootstrapGifts'] is False and declaration['forcedOrders'] is False and declaration['runtimeIntervention'] is False and declaration['bothPlayersAI'] is True)
check('predeclaration-before-actual-run', parse_time(declaration['declaredAtUtc']) < parse_time(runner['startedUtc']))
check('native-exchange-limitation', native_scope['wholeNativeMarketAcceptance'] is False and native_scope['marketTrades'] == 0)
check('actual-shared-host-overlap', native_runner['concurrentNormalObservation'] is True and parse_time(native_runner['startedUtc']) < parse_time(runner['finishedUtc']) and parse_time(native_runner['finishedUtc']) > parse_time(runner['startedUtc']))
reports = []

for case in manifest['cases']:
    cid = case['id']
    folder = MATRIX / cid
    worlds = {name: load(folder / (name + '-world.json')) for name in ('opening', 'comparison', 'final')}
    sims = {name: load(folder / (name + '-simulation.json')) for name in ('opening', 'comparison', 'final')}
    scenario_identity = None
    if case['civilization'] == 'river':
        envelope = load(folder / 'opening-scenario.json')
        document = json.loads(envelope['payload'])
        payload_sha = hashlib.sha256(envelope['payload'].encode()).hexdigest()
        check(cid + ':scenario-envelope-sha', envelope['schema'] == 1 and envelope['encoding'] == 'frontier-scenario-json-v1' and payload_sha == envelope['sha256'])
        check(cid + ':scenario-declared-identity', (document['id'],document['seed'],document['rulesHash']) == (case['map'],case['seed'],manifest['rulesHash']))
        river_wallet = next(x for x in rules['civs'] if x['id']=='river')['startingResources']
        check(cid + ':scenario-normal-starts', all(s['civilization']=='river' and s['age']==1 and vec(s['initialResources'])==vec(river_wallet) for s in document['starts']))
        scenario_identity = {'declaredMapAlias':'river_bend','observerScenarioId':document['id'],'worldMapId':'custom_scenario','scenarioHash':payload_sha,'envelopeShaVerified':True,'embeddedDocumentEqualsPayloadAtAllBoundaries':True}
    events = [json.loads(line) for line in (folder / 'events.jsonl').read_text().splitlines()]
    samples = [json.loads(line) for line in (folder / 'samples.jsonl').read_text().splitlines()]
    for name, world in worlds.items():
        expected_map = 'custom_scenario' if scenario_identity else case['map']
        check(cid + ':' + name + ':identity', (world['schema'], world['seed'], world['maxAge'], world['rulesHash'], world['mapId'], world['paused']) == (4, case['seed'], 5, manifest['rulesHash'], expected_map, False))
        if scenario_identity:
            check(cid + ':' + name + ':scenario-payload-and-hash', world['hasScenarioDocument'] is True and world['scenarioHash'] == payload_sha and world['scenarioDocument'] == document)
        else:
            check(cid + ':' + name + ':generated-map-identity', world['hasScenarioDocument'] is False and world['scenarioHash'] == '')
        check(cid + ':' + name + ':ai-boundary', sims[name]['difficultyId'] == 'standard' and sorted(x['player'] for x in sims[name]['controllers']) == [0, 1] and sims[name]['accumulator'] == 0)
        check(cid + ':' + name + ':players', [x['state']['id'] for x in world['players']] == [0, 1] and all(x['state']['civilization'] == case['civilization'] for x in world['players']))
        check(cid + ':' + name + ':snapshot-events-excluded', len(world['events']) == 0)
    check(cid + ':opening-time-and-step', worlds['opening']['time'] == 0 and sims['opening']['stepCount'] == 0)
    civ = next(x for x in rules['civs'] if x['id'] == case['civilization'])
    check(cid + ':ordinary-starting-wallets', all(x['state']['age'] == 1 and vec(x['state']['resources']) == vec(civ['startingResources']) for x in worlds['opening']['players']))
    check(cid + ':comparison-crossed-boundary', case['comparisonReached'] is True and 1500 <= worlds['comparison']['time'] < 1500.2 and 0 < sims['comparison']['stepCount'] < sims['final']['stepCount'])
    check(cid + ':final-time-and-step', worlds['final']['time'] == case['time'] and sims['final']['stepCount'] == case['steps'] and worlds['final']['winner'] == case['winner'])
    check(cid + ':terminal-or-cap', (case['status'] == 'natural_terminal' and case['time'] < 2400 and case['winner'] in (0, 1)) or (case['status'] == 'observation_cap' and 2400 <= case['time'] < 2400.2 and case['winner'] == -1))
    entities = {x['state']['id']: x for x in worlds['final']['entities']}
    counts = collections.Counter(e['kind'] for e in events)
    check(cid + ':events-ordinary-schema', all(e['schemaVersion'] == 2 and 0 <= e['time'] <= case['time'] for e in events))
    check(cid + ':events-ordered', all(events[i]['time'] <= events[i+1]['time'] for i in range(len(events)-1)))
    check(cid + ':all-spawn-entities-preserved', {e['entity'] for e in events if e['kind'] == 'Spawn'} == set(entities))
    check(cid + ':no-scenario-gift-events', counts['ScenarioResourceGranted'] == 0)
    check(cid + ':sample-paired-players', len(samples) % 2 == 0 and all(samples[i]['time'] == samples[i+1]['time'] and (samples[i]['player'], samples[i+1]['player']) == (0, 1) for i in range(0, len(samples), 2)))
    check(cid + ':sample-time-boundaries', 0 < samples[0]['time'] <= 0.2 and samples[-1]['time'] == case['time'])
    check(cid + ':sample-order', all(samples[i]['time'] <= samples[i+2]['time'] for i in range(len(samples)-2)))
    trades = []
    prices = {(p, resource): rules['market']['basePrice'] for p in (0, 1) for resource in ('food', 'wood')}
    max_transaction_error = 0
    economy_events = 0
    market_payments = []
    for index, e in enumerate(events):
        if e.get('hasEconomy'):
            economy_events += 1
            eco = e['economy']
            error = max(abs((eco['balanceAfter'][k] - eco['balanceBefore'][k]) - eco['actualDelta'][k]) for k in RES)
            max_transaction_error = max(max_transaction_error, error)
            check(cid + ':economy-actual-delta:' + str(index), error <= 0.002, error)
        if e['kind'] != 'MarketTrade':
            continue
        match = re.fullmatch(r'(Buy|Sell) (\d+) (Food|Wood) for (\d+) C', e['message'])
        check(cid + ':market-description:' + str(index), match is not None)
        if match is None:
            continue
        verb, units, resource, coin = match.groups()
        units, coin, resource = int(units), int(coin), resource.lower()
        buy = verb == 'Buy'
        lots = units // rules['market']['lotSize']
        check(cid + ':market-valid-lots:' + str(index), units == lots * rules['market']['lotSize'] and 1 <= lots <= rules['market']['maxLots'])
        price = prices[e['player'], resource]
        quoted = 0
        for _ in range(lots):
            quoted += price + (rules['market']['spread'] if buy else -rules['market']['spread'])
            price = min(rules['market']['maxPrice'], max(rules['market']['minPrice'], price + (rules['market']['priceStep'] if buy else -rules['market']['priceStep'])))
        prices[e['player'], resource] = price
        check(cid + ':marginal-quote:' + str(index), quoted == coin, {'observed': coin, 'expected': quoted})
        goods = [float(units) if k == resource else 0.0 for k in RES]
        money = [0.0, 0.0, float(coin)]
        paid, income = (money, goods) if buy else (goods, money)
        payment = events[index - 1]
        eco = e['economy']
        pe = payment['economy']
        check(cid + ':market-paired-payment:' + str(index), payment['kind'] == 'Payment' and (payment['time'], payment['player'], payment['entity']) == (e['time'], e['player'], e['entity']) and payment['message'].startswith(e['message'] + ':'))
        check(cid + ':market-payment-request:' + str(index), vec(pe['requestedDelta']) == [-x for x in paid])
        check(cid + ':market-payment-actual:' + str(index), close(vec(pe['actualDelta']), [-x for x in paid]))
        check(cid + ':market-affordable-wallet:' + str(index), all(x >= y for x, y in zip(vec(pe['balanceBefore']), paid)))
        check(cid + ':market-credit-request:' + str(index), vec(eco['requestedDelta']) == income)
        check(cid + ':market-credit-actual:' + str(index), close(vec(eco['actualDelta']), income))
        check(cid + ':atomic-payment-credit-chain:' + str(index), pe['balanceAfter'] == eco['balanceBefore'])
        check(cid + ':owned-market-entity:' + str(index), entities[e['entity']]['definition'] == 'market' and entities[e['entity']]['state']['owner'] == e['player'])
        completions = [(i, v) for i, v in enumerate(events[:index]) if v['kind'] == 'BuildingComplete' and v['entity'] == e['entity']]
        check(cid + ':completed-market-before-exchange:' + str(index), bool(completions))
        trades.append({'player': e['player'], 'time': e['time'], 'resource': resource, 'buy': buy,
                       'income': income, 'sold': [0.0]*3 if buy else paid, 'bought': income if buy else [0.0]*3,
                       'endPressure': price-rules['market']['basePrice'], 'payment': event_ref(payment, index-1), 'credit': event_ref(e, index)})
        market_payments.append(index-1)
    check(cid + ':exact-market-count', len(trades) == case['marketTrades'] == counts['MarketTrade'])
    for name, world in worlds.items():
        prefix = [t for t in trades if t['time'] <= world['time']]
        for pw in world['players']:
            snapshot_market_check(cid + ':' + name + ':P' + str(pw['state']['id']), pw['state'], prefix)
            pid = pw['state']['id']
            income = [sum(e['amount'] for e in events if e['kind']=='TradeDelivery' and e['player']==pid and e['time']<=world['time'] and e['message'].lower()==k) for k in RES]
            check(cid + ':' + name + ':trade-income:P' + str(pid), vec(pw['state']['tradeIncome']) == income)
            cargo_completed = any(e['kind']=='ResearchComplete' and e['player']==pid and e['time']<=world['time'] and e['message']=='Cargo Ledgers' for e in events)
            check(cid + ':' + name + ':cargo-ledger-tech:P' + str(pid), ('cargo_ledgers' in pw['technologies']) == cargo_completed)
    for i, sample in enumerate(samples):
        p = {'id': sample['player'], 'marketIncome': sample['marketIncome'], 'marketSold': sample['marketSold'], 'marketBought': sample['marketBought'], 'marketFoodPressure': sample['foodPressure'], 'marketWoodPressure': sample['woodPressure']}
        snapshot_market_check(cid + ':sample' + str(i), p, [t for t in trades if t['time'] <= sample['time']])
        army = sample['army']
        check(cid + ':sample-cursor' + str(i), 0 <= army['stageSearchCursor'] < 130 and (not army['hasStage'] or army['stageSearchCursor'] == 0))
    final_controllers = {x['player']: x for x in sims['final']['controllers']}
    for sample in samples[-2:]:
        player = next(x['state'] for x in worlds['final']['players'] if x['state']['id'] == sample['player'])
        check(cid + ':final-sample-player-ledger:' + str(sample['player']), sample['age'] == player['age'] and sample['wallet'] == player['resources'] and sample['spent'] == player['spent'] and sample['gathered'] == player['gathered'])
        check(cid + ':final-sample-controller-army:' + str(sample['player']), sample['army'] == final_controllers[sample['player']]['armyPlan'])
    assembled = [(i, e) for i, e in enumerate(events) if e['kind'] == 'AIArmyPhase' and 'assembled' in e['message']]
    check(cid + ':exact-assembled-count', len(assembled) == case['stageDepartures'])
    command_counts = collections.Counter(e['message'].split()[0] for e in events if e['kind'] == 'Command')
    tail_start = case['time'] - 300
    tail = [(i, e) for i, e in enumerate(events) if e['time'] >= tail_start]
    tail_counts = collections.Counter(e['kind'] for _, e in tail)
    tail_commands = collections.Counter(e['message'].split()[0] for _, e in tail if e['kind'] == 'Command')
    pvp_damage = [(i, e) for i, e in enumerate(events) if e['kind'] == 'Damage' and e['player'] in (0,1) and e['target'] in entities and entities[e['target']]['state']['owner'] == 1-e['player'] and e['amount'] > 0]
    tail_pvp = [(i, e) for i, e in pvp_damage if e['time'] >= tail_start]
    check(cid + ':actual-final-window-combat', tail_counts['Fire'] > 0 and tail_counts['Damage'] > 0 and len(tail_pvp) > 0)
    ledgers = []
    bridges = []
    for pw in worlds['final']['players']:
        p = pw['state']; pid = p['id']
        deliveries = [(i, e) for i, e in enumerate(events) if e['kind'] == 'TradeDelivery' and e['player'] == pid]
        totals = [sum(e['amount'] for _, e in deliveries if e['message'].lower() == k) for k in RES]
        check(cid + ':final-trade-income:' + str(pid), vec(p['tradeIncome']) == totals)
        pay = [(i, e) for i, e in enumerate(events) if e['kind'] == 'Payment' and e['player'] == pid and e['message'].startswith('Cargo Ledgers:')]
        complete = [(i, e) for i, e in enumerate(events) if e['kind'] == 'ResearchComplete' and e['player'] == pid and e['message'] == 'Cargo Ledgers']
        has_ledger = 'cargo_ledgers' in pw['technologies']
        check(cid + ':ledger-technology-completion:' + str(pid), has_ledger == bool(complete))
        ledgers.append({'player': pid, 'technologyInFinal': has_ledger, 'payments': [event_ref(e, i) for i, e in pay], 'researchCompletions': [event_ref(e, i) for i, e in complete], 'tradeDeliveries': len(deliveries), 'resourceIncome': dict(zip(RES, totals)), 'coinDeliveries': [event_ref(e, i) for i, e in deliveries if e['message'] == 'Coin'], 'completedPosts': [event_ref(e, i) for i, e in enumerate(events) if e['kind'] == 'BuildingComplete' and e['player'] == pid and entities[e['entity']]['definition'] == 'trade_post']})
        for index, payment in pay:
            check(cid + ':ledger-payment-cost:' + str(index), vec(payment['economy']['requestedDelta']) == [0.0,-200.0,-150.0])
            preceding = [t for t in trades if t['player'] == pid and t['time'] == payment['time'] and t['credit']['line'] < index+1]
            for t in preceding:
                check(cid + ':bridge-credit-ledger-chain:' + str(index), t['credit']['economy']['balanceAfter'] == payment['economy']['balanceBefore'])
                bridges.append({'player': pid, 'marketCredit': t['credit'], 'ledgerPayment': event_ref(payment,index), 'preSaleCoinInsufficientFor150C': t['credit']['economy']['balanceBefore']['coin'] < 150, 'postSaleCoinAffords150C': t['credit']['economy']['balanceAfter']['coin'] >= 150, 'actualCompletion': [event_ref(e,i) for i,e in complete], 'actualCoinDeliveriesAfterResearch': [event_ref(e,i) for i,e in deliveries if e['message']=='Coin' and complete and i>complete[0][0]]})
    ended = [(i,e) for i,e in enumerate(events) if e['kind']=='MatchEnded']
    check(cid + ':terminal-events', (case['status']=='natural_terminal' and len(ended)==1 and ended[0][1]['time']==case['time'] and ended[0][1]['player']==case['winner']) or (case['status']=='observation_cap' and not ended))
    elapsed = case['stepSeconds'] + case['observerSeconds'] + case['finalWriteSeconds']
    check(cid + ':stopwatch-nonnegative-ordered', 0 <= elapsed <= case['wallSeconds'] and 0 <= case['stepsOver500ms'] <= case['stepsOver100ms'] <= case['steps'])
    sample0 = samples[::2]
    acc = 0
    timing_points=[]
    for sample in sample0:
        acc += sample['intervalStepSeconds']; timing_points.append((sample['time'],acc))
    lower=max((x for x in timing_points if x[0] <= 1500),default=None)
    upper=min((x for x in timing_points if x[0] >= 1500),default=None)
    check(cid + ':sample-step-accumulation', abs(acc-case['stepSeconds']) < 0.00001)
    reports.append({'id':cid,'declaredIdentity':case,'scenarioIdentity':scenario_identity,'eventCount':len(events),'sampleCount':len(samples),'eventKinds':dict(counts),'boundarySnapshots':{n:{'time':worlds[n]['time'],'stepCount':sims[n]['stepCount'],'winner':worlds[n]['winner'],'playerAges':[p['state']['age'] for p in worlds[n]['players']],'politicalPhases':[p['state']['politicalPhase'] for p in worlds[n]['players']]} for n in worlds},'market':{'trades':trades,'count':len(trades),'sellCount':sum(not t['buy'] for t in trades),'buyCount':sum(t['buy'] for t in trades),'snapshotsAndAllSamplesLedgerReconciled':True,'scope':'Actual AI sells only; native buy/sell click and zero-trigger Dutch exchange are not inferred.'},'economy':{'transactionEvents':economy_events,'maxActualDeltaArithmeticError':max_transaction_error,'marketPairingTolerance':0.01,'openingComparisonAndFinalTradeIncomeReconciled':True,'openingComparisonAndFinalCargoLedgerTechnologyReconciled':True},'postLedger':ledgers,'sameTickMarketToLedgerBridges':bridges,'army':{'assembledCount':len(assembled),'assembledByPlayer':dict(collections.Counter(e['player'] for _,e in assembled)),'assembledEvents':[event_ref(e,i) for i,e in assembled],'commandCounts':dict(command_counts),'fireCount':counts['Fire'],'damageCount':counts['Damage'],'pvpDamageCount':len(pvp_damage),'last300Seconds':{'start':tail_start,'end':case['time'],'eventKinds':dict(tail_counts),'commandCounts':dict(tail_commands),'positivePvpDamageCount':len(tail_pvp),'positivePvpDamageAmount':sum(e['amount'] for _,e in tail_pvp),'lastPvpDamage':event_ref(tail_pvp[-1][1],tail_pvp[-1][0]) if tail_pvp else None},'sampleArmyPhases':dict(collections.Counter(s['army']['phase'] for s in samples)),'nonzeroCursorSamples':sum(s['army']['stageSearchCursor']!=0 for s in samples),'maxSampleCursor':max(s['army']['stageSearchCursor'] for s in samples),'limits':['Command counts are accepted issued orders, not one event per actor or proof that each order reached its destination.','Fire and positive PvP Damage prove actual combat; they do not certify each assembled cohort or every intermediate trajectory.','30-second army samples do not certify continuous travel geometry.']},'timing':{'wallSeconds':case['wallSeconds'],'stepSeconds':case['stepSeconds'],'observerSeconds':case['observerSeconds'],'finalWriteSeconds':case['finalWriteSeconds'],'otherSetupAndNonStepSeconds':case['wallSeconds']-elapsed,'meanStepMsDerived':case['stepSeconds']*1000/case['steps'],'maxStepMs':case['maxStepMs'],'stepsOver100ms':case['stepsOver100ms'],'stepsOver500ms':case['stepsOver500ms'],'stepSecondsNear1500Bracket':{'lower':lower,'upper':upper,'exactBoundaryStepTimingRecorded':False},'sharedHostNativeOverlap':{'nativeStarted':native_runner['startedUtc'],'nativeFinished':native_runner['finishedUtc'],'overlappedThisCase':parse_time(native_runner['startedUtc'])<parse_time(case['finishedUtc']) and parse_time(native_runner['finishedUtc'])>parse_time(case['startedUtc'])},'limits':['Stopwatch records synchronous headless simulation and observer costs; no frame presentation or FPS is measured.','Native1 shared this CPU during all three cases; these are not isolated comparative performance benchmarks.','1500 world/simulation snapshot is exact observed crossing; sparse timing samples only bracket its cumulative step cost.']}})

after_refs=[ref(p) for p in paths]
check('all-selected-original-references-two-read-stable',before_refs==after_refs)
check('all-declared-cases-read',len(reports)==3)
report={'schema':1,'auditedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'status':'verified_closed_three_declared_cases_with_documented_limits' if not failures else 'failed_independent_closed_case_audit','acceptedDeclaredObservationScope':not failures,'releaseDeliveryComplete':False,'sourceFingerprint':actual_fp,'sourceCount':len(files),'runtimeCommit':COMMIT,'appVersion':manifest['version'],'rulesHashObservedConsistent':manifest['rulesHash'],'rulesHashLimit':'JsonUtility semantic catalog hash is cross-checked across manifest/snapshots; current raw Rules.json is separately frozen by SHA, not equated to the semantic hash.','sourceMapEqualsBeforeFullAndFull':files==freeze['files']==full['files'],'runnerActual':runner,'predeclaration':declaration,'references':before_refs,'referencesUnchangedDuringAudit':before_refs==after_refs,'caseReports':reports,'checks':check_count,'failures':failures,'nativeReferenceScopeOnly':{'status':native_scope['status'],'wholeNativeMarketAcceptance':False,'marketTrades':0,'referenceOnly':'Existing native073-scope-audit was read and hashed; this checker does not re-audit pixels or native raw events.','remaining':native_scope['unverified'],'limitations':native_scope['limitations']},'limits':['Exactly three predeclared ordinary AI cases; no full civilization/map matrix, all balance acceptance, or causal 072/073 timing comparison.','Ottoman market sells and same-tick P1 ledger bootstrap are verified; Ottoman P0 did not complete Cargo Ledgers and Dutch did not exchange at the market.','Successful source/test/build/observer identity does not complete native market interaction, current-GUID cold/replay or external delivery.','Original snapshots intentionally exclude event arrays; separate complete events.jsonl is read, counted and hashed.'],'actions':{'readOnlyOriginals':True,'ranUnity':False,'ranTests':False,'reranSimulation':False,'editedRepo':False,'readOld072RawOrDrive':False,'committedOrUploaded':False}}
(DIAG/'closure073-normal.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
lines=['# 073 三局自然观察 · 独立实际审计','',f"Status: {report['status']}。{check_count}项检查，{len(failures)}项失败；这里只验收声明的三局实际输出。",'',f"Actual runtime `{COMMIT}`；732项source map等before-full/full/current，FP `{actual_fp}`。runner实际exit0、sourceStable、无Prepare/注资/强制命令/运行干预；版本0.16.0。manifest与全部world的rulesHash `{manifest['rulesHash']}`一致；raw Rules.json SHA与语义rulesHash分别处理。",'', '初次checker把Dutch World.MapId假设为scenario id，产生3个checker身份断言失败。原失败JSON/MD已逐字保留为[checker-schema-mismatch-first](closure073-normal-checker-schema-mismatch-first.json)及[原说明](closure073-normal-checker-schema-mismatch-first.md)；修为真实custom_scenario与嵌入doc.id/payload SHA合同后重新只读审计。此处是checker schema假设纠正，未修改游戏或原case，不称游戏case失败。', '', '| Case | 结果 | Events / samples | Market / assembled | 最后300秒Fire / Damage / 正PvP Damage | wall / step / max step |','|---|---|---:|---:|---:|---|']
for r in reports:
 c=r['declaredIdentity']; a=r['army']; t=r['timing']; last=a['last300Seconds']; lines.append(f"| {r['id']} | {c['status']} {c['time']:.6f}s P{c['winner']} | {r['eventCount']} / {r['sampleCount']} | {r['market']['count']} / {a['assembledCount']} | {last['eventKinds'].get('Fire',0)} / {last['eventKinds'].get('Damage',0)} / {last['positivePvpDamageCount']} | {t['wallSeconds']:.3f}s / {t['stepSeconds']:.3f}s / {t['maxStepMs']:.3f}ms |")
lines += ['', '开局均普通I时代且钱包等文明默认值；1500 crossing实际1500.080811s、15003 steps；final-world时间、胜者与final-simulation stepCount逐项等manifest。Dutch声明river_bend，observer固定scenario id traders_bend070，实际World.MapId为custom_scenario；opening-scenario envelope payload SHA与三边界嵌入doc/hash一致，已按真实Scenario路径核对。两Ott实际清除敌方所有单位建筑自然终局；Dutch2400.090576s cap并未结束，不称赢局。所有sample配对、最终钱包/账本/army状态与终局快照相符。', '', '两Ott共26次真实市场卖Food，0次买入；全部逐笔依据边际报价、前序Payment的真实扣款、MarketTrade到账、钱包连贯性、归属及完成市场核对。每个opening/comparison/final和全部samples的marketIncome/Sold/Bought与price pressure等事件前缀。P0两局分别1400F→707C与3000F→1042C；P1均200F→137C。Dutch0次交换与零market账本明确保留。', '', '两Ott P1在827.944885s由80.705048C卖100F增加70C至150.705048C，同tick实际付200W/150C研究Cargo Ledgers，873.033875s实际完成，897.727844s收到50C。它验证不足150C到可支付账簿的实际桥梁；P0没有完成账簿，不扩大为双方均完成。Dutch双方实际完成Cargo Ledgers并收到商路资源，市场交换未触发。开局/1500/final的tradeIncome逐资源等对应TradeDelivery事件前缀，CargoLedgers technology等实际ResearchComplete前缀。', '', 'Army assembled14/14/21与manifest精确相符；AttackMove/Attack发令与真实Fire、正PvP Damage分别计数，三局最后300秒均存在实际交战。30秒sample不认证每支cohort全程路径或每条命令均达目的地。', '', 'Step、observer、finalWrite与wall分项均为实际Stopwatch；Native1真实运行15:52:56–16:05:10，与三局均重叠并共享CPU。max step仍约1.8秒，慢步数量保留；不称native FPS或隔离性能改善。1500状态 crossing有真实快照，performance samples只给累计step成本上下括界，不制造精确1500 wall字段。', '', '原生既有scope审计仅引用：市场137实际100W付款并完成、3组F9，而buy/sell/modal/500/unaffordable、当前GUID cold/replay未完成。后续点击noWindowsAvailable、0 MarketTrade保留；本审计未重新验像素。release/delivery仍false。', '', '所有选定原件两次SHA/bytes/mode/identity读取稳定，原件未改。没有Unity/测试/模拟重跑、repo编辑、旧072原始局/云盘重复审计、提交或上传。详细逐笔事件行号和原件见[closure073-normal.json](closure073-normal.json)。']
if failures:
 lines += ['', '实际失败检查：', '```json', json.dumps(failures,ensure_ascii=False,indent=2), '```']
(DIAG/'closure073-normal.md').write_text('\n'.join(lines)+'\n')
for p in (DIAG/'closure073-normal.json',DIAG/'closure073-normal.md'):
 print(json.dumps({'path':str(p),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}))
print(json.dumps({'status':report['status'],'checks':check_count,'failures':failures,'cases':[{'id':r['id'],'events':r['eventCount'],'samples':r['sampleCount'],'market':r['market']['count'],'tailPvpDamage':r['army']['last300Seconds']['positivePvpDamageCount']} for r in reports]},ensure_ascii=False))
