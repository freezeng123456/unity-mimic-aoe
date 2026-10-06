#!/usr/bin/env python3
"""Validate only closed074 evidence; authored Gather074, not independent implementation audit.
Never launches Unity, simulation, build, native UI, packages or external writes.
"""
import argparse
import collections
import datetime
import hashlib
import json
import math
import plistlib
import re
import stat
import subprocess
from pathlib import Path
from xml.etree import ElementTree as ET

REPO=Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
OUT=Path('/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.17.0/Candidate1')
ROOT=OUT/'NormalObserved1';MATRIX=ROOT/'matrix';DIAG=OUT/'Diagnostics'
FP='61d00af23e00d960dd040ef55bdfb0aa269b15745be1d1a2f0faec46377d6698'
COMMIT='ba0b57336c46d72738b71b59af923a149aff467d'
RES=('food','wood','coin');failures=[];check_count=0
parser=argparse.ArgumentParser()
parser.add_argument('--output-prefix',type=Path,default=DIAG/'validation074-closed-evidence-2a')
args=parser.parse_args()
json_target=args.output_prefix.with_suffix('.json');md_target=args.output_prefix.with_suffix('.md')
if json_target.exists() or md_target.exists():raise SystemExit('Fresh outputs required; no existing report is overwritten')

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


parse_time=lambda s:datetime.datetime.fromisoformat(s.replace('Z','+00:00'))
# Require completed receipts before reading any output which might still grow.
def closed_receipt(path,kind):
    r=load(path)
    if not (r.get('kind')==kind and r.get('status')=='completed_source_stable' and r.get('exitCode')==0 and
            r.get('sourceStable') is True and r.get('finishedUtc')):
        raise SystemExit('Not a closed successful source-stable receipt: '+str(path))
    return r
focused=closed_receipt(OUT/'Focused3/runner-receipt074.json','focused')
full_runner=closed_receipt(OUT/'Full2/runner-receipt074.json','full')
initial_focused=closed_receipt(OUT/'Focused1/runner-receipt074.json','focused')
initial_full=closed_receipt(OUT/'Full1/runner-receipt074.json','full')
red=load(OUT/'Focused2-ProjectionRed/runner-receipt074.json')
if not (red.get('kind')=='focused' and red.get('status')=='failed_or_source_changed' and red.get('exitCode')==2 and
        red.get('sourceStable') is True and red.get('finishedUtc')):
    raise SystemExit('Projection red is not an actual closed source-stable expected failure')
initial_reports_path=Path(initial_full['reportsDirectory'])
initial_full_evidence=load(initial_reports_path/'source-evidence.json')
build=closed_receipt(OUT/'Build1/runner-receipt074.json','build')
runner=closed_receipt(ROOT/'runner-receipt074.json','normal')
reports_path=Path(full_runner['reportsDirectory'])
full=load(reports_path/'source-evidence.json')
declaration=load(DIAG/'predeclared074.json')
manifest=load(MATRIX/'manifest.json')
rules=load(REPO/'Assets/Resources/Rules.json')
old_reports=REPO/'Builds/Tests/run-9q8yTJKS'
paths=[OUT/'Focused3/runner-receipt074.json',OUT/'Focused3/EditMode.xml',OUT/'Focused3/EditMode.log',
       OUT/'Full2/runner-receipt074.json',OUT/'Full2/wrapper-output.log',
       reports_path/'source-evidence.json',reports_path/'EditMode.xml',reports_path/'PlayMode.xml',
       reports_path/'EditMode.log',reports_path/'PlayMode.log',
       old_reports/'source-evidence.json',old_reports/'EditMode.xml',old_reports/'PlayMode.xml',
       OUT/'Build1/runner-receipt074.json',OUT/'Build1/build.log',
       ROOT/'runner-receipt074.json',ROOT/'normal.log',MATRIX/'manifest.json',DIAG/'predeclared074.json',Path(__file__)]
paths += [OUT/'Focused1/runner-receipt074.json',OUT/'Focused1/EditMode.xml',OUT/'Focused1/EditMode.log',
          OUT/'Full1/runner-receipt074.json',OUT/'Full1/wrapper-output.log',
          initial_reports_path/'source-evidence.json',initial_reports_path/'EditMode.xml',initial_reports_path/'PlayMode.xml',
          initial_reports_path/'EditMode.log',initial_reports_path/'PlayMode.log',
          OUT/'Focused2-ProjectionRed/runner-receipt074.json',OUT/'Focused2-ProjectionRed/EditMode.xml',OUT/'Focused2-ProjectionRed/EditMode.log',
          DIAG/'validate074-closed-evidence.py',DIAG/'validate074-closed-evidence-2.py']
for case in manifest['cases']:paths+=sorted((MATRIX/case['id']).iterdir())
app=OUT/'Build1/Frontier Reckoning.app'
app_paths=[app/'Contents/Info.plist',app/'Contents/Resources/Data/boot.config',
           app/'Contents/MacOS/Frontier Reckoning',app/'Contents/Resources/Data/Managed/Frontier.Runtime.dll']
paths+=app_paths
source_paths=[];files={}
for directory in ('Assets','Packages','ProjectSettings','Tools'):
    for p in sorted((REPO/directory).rglob('*')):
        if p.is_file() and not any(x.startswith('.') or x=='__pycache__' for x in p.relative_to(REPO).parts):
            source_paths.append(p);files[p.relative_to(REPO).as_posix()]=hashlib.sha256(p.read_bytes()).hexdigest()
paths+=source_paths
before_refs=[ref(p) for p in paths]
actual_fp=hashlib.sha256(json.dumps(files,sort_keys=True,separators=(',',':')).encode()).hexdigest()
check('740-exact-source-map',len(files)==740 and files==full['files']==focused['sourceFilesBefore']==full_runner['sourceFilesBefore']==build['sourceFilesBefore']==runner['sourceFilesBefore'])
check('fingerprint-all-receipts',actual_fp==FP==full['sourceFingerprint']==full_runner['reportsFingerprint']==manifest['sourceFingerprint'] and
      all(r['sourceFingerprintBefore']==r['sourceFingerprintAfter']==FP and r['changedSources']==[] for r in (focused,full_runner,build,runner)))
check('closed-UTC-order',all(parse_time(r['startedUtc'])<parse_time(r['finishedUtc']) for r in (focused,full_runner,build,runner)) and
      parse_time(full_runner['finishedUtc'])<=parse_time(build['startedUtc']) and parse_time(build['finishedUtc'])<=parse_time(runner['startedUtc']))
check('no-preparation-intervention',all(r['preparationRun'] is False and r['caseInterventions'] is False for r in (focused,full_runner,build,runner)))
check('runtime-receipt-heads',all(r['gitHeadBefore']==COMMIT for r in (full_runner,build,runner)))
# Compare every source blob from the committed runtime, not only git HEAD text.
tree=subprocess.check_output(['git','ls-tree','-r','-z',COMMIT,'Assets','Packages','ProjectSettings','Tools'],cwd=REPO)
entries=[]
for row in tree.split(b'\0'):
    if not row:continue
    metadata,path=row.split(b'\t',1);mode,kind,oid=metadata.decode().split();path=path.decode()
    if not any(x.startswith('.') or x=='__pycache__' for x in Path(path).parts):entries.append((path,mode,kind,oid))
check('git-runtime-source-count',len(entries)==740 and {p for p,_,_,_ in entries}==set(files))
blob_data=subprocess.check_output(['git','cat-file','--batch'],input=('\n'.join(e[3] for e in entries)+'\n').encode(),cwd=REPO)
cursor=0;git_files={}
for path,mode,kind,oid in entries:
    end=blob_data.index(b'\n',cursor);header=blob_data[cursor:end].decode().split();size=int(header[2]);cursor=end+1
    blob=blob_data[cursor:cursor+size];cursor+=size+1
    check('git-blob-header:'+path,header[0]==oid and header[1]==kind=='blob' and mode in ('100644','100755'))
    git_files[path]=hashlib.sha256(blob).hexdigest()
check('exact-Git-before-full-build-normal-current-map',git_files==files)
check('git-blob-stream-consumed',cursor==len(blob_data))

def xml_run(path,expected):
    root=ET.parse(path).getroot();attrs=root.attrib;names=[n.attrib['fullname'] for n in root.findall('.//test-case')]
    check('full-passed:'+str(path),root.tag=='test-run' and attrs.get('result')=='Passed' and
          int(attrs['total'])==int(attrs['passed'])==expected and all(int(attrs.get(k,0))==0 for k in ('failed','skipped','inconclusive')))
    check('exact-test-case-count:'+str(path),len(names)==expected and len(set(names))==expected and all(n.attrib.get('result')=='Passed' for n in root.findall('.//test-case')))
    return {'path':str(path),'attributes':attrs,'fullnames':names,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
regression={};old_counts={'EditMode':1192,'PlayMode':419};new_counts={'EditMode':1227,'PlayMode':419}
for mode in ('EditMode','PlayMode'):
    old=xml_run(old_reports/(mode+'.xml'),old_counts[mode]);new=xml_run(reports_path/(mode+'.xml'),new_counts[mode])
    missing=sorted(set(old['fullnames'])-set(new['fullnames']));added=sorted(set(new['fullnames'])-set(old['fullnames']))
    check('all-old-fullnames-retained:'+mode,not missing,missing)
    check('exact-additional-cases:'+mode,len(added)==(35 if mode=='EditMode' else 0),added)
    if mode=='EditMode':check('new-Gather18-Home17',sum('AiGatherRoutes074Tests.' in n for n in added)==18 and sum('AiArmyHome074Tests.' in n for n in added)==17)
    result=full['reports'][mode]
    check('full-source-evidence-report:'+mode,result['file']==mode+'.xml' and result['sha256']==new['sha256'] and result['passed']==new_counts[mode])
    regression[mode]={'oldCount':old_counts[mode],'actual':new,'oldFullnamesAllRetained':not missing,'addedFullnames':added,'missingFullnames':missing}
focused_xml=xml_run(OUT/'Focused3/EditMode.xml',64)
check('focused-fullnames-contained',set(focused_xml['fullnames'])<=set(regression['EditMode']['actual']['fullnames']))
check('focused-new-Gather18-Home17',sum('AiGatherRoutes074Tests.' in n for n in focused_xml['fullnames'])==18 and sum('AiArmyHome074Tests.' in n for n in focused_xml['fullnames'])==17)
# Keep initial success and the meaningful expected failure separate from final acceptance.
INITIAL_FP='b72d96ee8ef5018ae7c2543c552117bb3e9370f525fcc2d97470c1ce9aeacdb9'
RED_FP='5af214997fe643ce5840b14479486486db12cc83282dee572e8330edd449a7b7'
projection_name='Frontier.Tests.AiGatherRoutes074Tests.ReachableProjectedWorkPointOutsideGatherRangeCannotForecastOrAssignWoodAndClearingRestoresIncome'
check('initial-source-bound',initial_full_evidence['sourceFingerprint']==initial_full['reportsFingerprint']==INITIAL_FP and
      initial_full_evidence['files']==initial_focused['sourceFilesBefore']==initial_full['sourceFilesBefore'] and
      all(r['sourceFingerprintBefore']==r['sourceFingerprintAfter']==INITIAL_FP and r['changedSources']==[] for r in (initial_focused,initial_full)))
check('initial-runtime-head',initial_full['gitHeadBefore']=='4344edacffad01bfc8d49ff90cb8361534f7702d')
initial_xml=xml_run(OUT/'Focused1/EditMode.xml',63)
initial_regression={}
for mode in ('EditMode','PlayMode'):
    previous=xml_run(initial_reports_path/(mode+'.xml'),1226 if mode=='EditMode' else 419)
    check('initial-full-retained-in-final:'+mode,set(previous['fullnames'])<=set(regression[mode]['actual']['fullnames']))
    check('initial-full-source-report:'+mode,initial_full_evidence['reports'][mode]['sha256']==previous['sha256'])
    initial_regression[mode]=previous
check('initial-focused-new-Gather17-Home17',sum('AiGatherRoutes074Tests.' in n for n in initial_xml['fullnames'])==17 and
      sum('AiArmyHome074Tests.' in n for n in initial_xml['fullnames'])==17)
check('initial-focused-contained',set(initial_xml['fullnames'])<=set(initial_regression['EditMode']['fullnames']))
check('red-source-bound',red['sourceFingerprintBefore']==red['sourceFingerprintAfter']==RED_FP and red['changedSources']==[] and
      len(red['sourceFilesBefore'])==740)
red_xml=ET.parse(OUT/'Focused2-ProjectionRed/EditMode.xml').getroot();red_cases=red_xml.findall('.//test-case')
red_message=red_cases[0].findtext('failure/message') if red_cases else ''
check('meaningful-single-projection-red',int(red_xml.attrib['total'])==int(red_xml.attrib['failed'])==1 and
      int(red_xml.attrib['passed'])==0 and len(red_cases)==1 and red_cases[0].attrib['fullname']==projection_name and
      red_cases[0].attrib['result']=='Failed' and 'A reachable projected goal outside real income range is not an available source' in red_message and
      'Expected: False' in red_message and 'But was:  True' in red_message)
check('projection-red-test-retained-green',projection_name in focused_xml['fullnames'] and projection_name in regression['EditMode']['actual']['fullnames'])
test_source='Assets/Frontier/Tests/EditMode/AiGatherRoutes074Tests.cs';helper_source='Assets/Frontier/Simulation/AiGatherRoutes074.cs'
changed_red_green=sorted(k for k in files if red['sourceFilesBefore'].get(k)!=files[k])
changed_initial_red=sorted(k for k in red['sourceFilesBefore'] if initial_full['sourceFilesBefore'].get(k)!=red['sourceFilesBefore'][k])
check('only-projection-test-added-before-red',changed_initial_red==[test_source],changed_initial_red)
check('same-regression-test-unweakened-red-to-green',red['sourceFilesBefore'][test_source]==files[test_source])
check('only-admission-guard-changed-red-to-green',changed_red_green==[helper_source],changed_red_green)
check('provenance-run-order',parse_time(initial_focused['finishedUtc'])<=parse_time(initial_full['startedUtc']) and
      parse_time(initial_full['finishedUtc'])<=parse_time(red['startedUtc']) and
      parse_time(red['finishedUtc'])<=parse_time(focused['startedUtc']) and
      parse_time(focused['finishedUtc'])<=parse_time(full_runner['startedUtc']))
provenance={'scope':'Initial63/1645 and expected red1 establish provenance; only final64/1646 are final-source passing results, counts are not added together.',
    'initialFocused':initial_xml,'initialFull':initial_regression,
    'initialSourceFingerprint':INITIAL_FP,'initialReceipts':{'focused':initial_focused,'full':initial_full},
    'projectionRed':{'receipt':red,'xmlAttributes':red_xml.attrib,'fullname':projection_name,'message':red_message,
      'xmlSha256':hashlib.sha256((OUT/'Focused2-ProjectionRed/EditMode.xml').read_bytes()).hexdigest(),
      'sourceFingerprint':RED_FP,'priorMovementAndZeroIncomeAssertionsPassedByFailureLocation':True},
    'greenProjectionSameTestSourceHash':red['sourceFilesBefore'][test_source],
    'initialToRedChangedSources':changed_initial_red,'redToGreenChangedSources':changed_red_green}
info=plistlib.loads(app_paths[0].read_bytes());boot=app_paths[1].read_text();guid_match=re.search(r'^build-guid=([0-9a-f]{32})$',boot,re.M)
check('build-real-GUID',guid_match is not None and build['bootConfig']==boot)
check('build-version-executable',info['CFBundleShortVersionString']==build['buildVersion']=='0.17.0' and stat.S_IMODE(app_paths[2].stat().st_mode)==0o755 and build['mainExecutableMode']=='0o755')
check('build-method-log',build['command'][build['command'].index('-executeMethod')+1]=='Frontier.Editor.BuildTools.BuildMac' and 'FRONTIER_BUILD Succeeded' in (OUT/'Build1/build.log').read_text())
check('normal-method',runner['command'][runner['command'].index('-executeMethod')+1]=='Frontier.Editor.GatherObservation074.RunNormal')
check('manifest-schema-completion',manifest['schema']=='normal-gather074-v1' and manifest['status']=='observed_three_declared_cases' and bool(manifest['finishedUtc']) and runner['manifestStatus']==manifest['status'])
check('manifest-version-source',manifest['version']=='0.17.0' and runner['manifestSourceFingerprint']==FP)
check('manifest-bounds',(manifest['limit'],manifest['comparison'],manifest['sampleInterval'])==(2400,1500,30))
expected_cases=[('winding-ottoman-44','winding','ottoman',44),('winding-ottoman-45','winding','ottoman',45),('river_bend-river-42','traders_bend070','river',42)]
check('exact-three-declared-cases',[(c['id'],c['map'],c['civilization'],c['seed']) for c in manifest['cases']]==expected_cases)
planned=declaration['plannedValidation']
check('predeclared-cases-and-bounds',[(c['map'],c['civilization'],c['seed']) for c in planned['normalCases']]==[('winding','ottoman',44),('winding','ottoman',45),('river_bend','river',42)] and
      (planned['normalAge'],planned['normalMaxAge'],planned['difficulty'],planned['normalLimitSeconds'],planned['comparisonSeconds'],planned['sampleIntervalSeconds'])==(1,5,'standard',2400,1500,30))
check('predeclared-no-injections-orders',planned['normalResourceInjection'] is False and planned['normalForcedOrders'] is False)
check('predeclaration-before-run',parse_time(declaration['createdAtUtc'])<parse_time(focused['startedUtc'])<parse_time(runner['startedUtc']))
reports=[]
for case in manifest['cases']:
    cid = case['id']
    folder = MATRIX / cid
    names=('opening','comparison','final') if case['comparisonReached'] else ('opening','final')
    worlds = {name: load(folder / (name + '-world.json')) for name in names}
    sims = {name: load(folder / (name + '-simulation.json')) for name in names}
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
    check(cid + ':comparison-presence',case['comparisonReached']==(case['time']>=1500))
    if case['comparisonReached']:check(cid + ':comparison-crossed-boundary',1500 <= worlds['comparison']['time'] < 1500.2 and 0 < sims['comparison']['stepCount'] <= sims['final']['stepCount'])
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
        check(cid+':sample-gather-counts:'+str(i),isinstance(sample['gatherOrders'],int) and isinstance(sample['gatherRoutesRejectedAtSample'],int) and 0<=sample['gatherRoutesRejectedAtSample']<=sample['gatherOrders']<=sample['workers'])
        check(cid+':sample-real-gather-ledger:'+str(i),all(math.isfinite(sample['gathered'][k]) and sample['gathered'][k]>=0 for k in RES))
        if i>=2:check(cid+':gather-ledger-monotonic:'+str(i),all(sample['gathered'][k]>=samples[i-2]['gathered'][k] for k in RES))
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
    late_combat_observed=tail_counts['Fire']>0 and tail_counts['Damage']>0 and len(tail_pvp)>0
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
                # A strategy review can pay for buildings/technology after its
                # exchange and before this research. Gather logs are buffered
                # across ticks, so their emission order is not a wallet chain.
                intervening=[(j,v) for j,v in enumerate(events[t['credit']['line']:index],start=t['credit']['line'])
                    if v.get('hasEconomy') and v['player']==pid and v['kind']!='Gather']
                wallet=t['credit']['economy']['balanceAfter']
                for j,v in intervening:
                    check(cid+':bridge-intervening-wallet-chain:'+str(index)+':'+str(j),
                          v['time']==payment['time'] and v['economy']['balanceBefore']==wallet)
                    wallet=v['economy']['balanceAfter']
                check(cid + ':bridge-credit-ledger-chain:' + str(index),wallet==payment['economy']['balanceBefore'])
                bridges.append({'player': pid, 'marketCredit': t['credit'], 'ledgerPayment': event_ref(payment,index),
                    'interveningInstantaneousEconomy':[event_ref(v,j) for j,v in intervening],
                    'directWalletChain':not intervening,
                    'scope':'Actual same-tick wallet chain; intervening payments/credits retained. Buffered Gather emission is excluded from instantaneous chronology.',
                    'preSaleCoinInsufficientFor150C': t['credit']['economy']['balanceBefore']['coin'] < 150, 'postSaleCoinAffords150C': t['credit']['economy']['balanceAfter']['coin'] >= 150, 'actualCompletion': [event_ref(e,i) for i,e in complete], 'actualCoinDeliveriesAfterResearch': [event_ref(e,i) for i,e in deliveries if e['message']=='Coin' and complete and i>complete[0][0]]})
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

    gathering=[(i,e) for i,e in enumerate(events) if e['kind']=='Gather']
    gather_reports=[]
    for pid in (0,1):
        ge=[(i,e) for i,e in gathering if e['player']==pid]
        check(cid+':real-positive-gather-events:P'+str(pid),bool(ge) and all(e['amount']>0 and math.isfinite(e['amount']) and e['message'].lower() in RES and
              e['entity'] in entities and entities[e['entity']]['state']['owner']==pid and e['target'] in entities and
              rules['entities'][next(j for j,d in enumerate(rules['entities']) if d['id']==entities[e['target']]['definition'])].get('resource',0)>0 for _,e in ge))
        sums={k:sum(e['amount'] for _,e in ge if e['message'].lower()==k) for k in RES}
        player=next(p['state'] for p in worlds['final']['players'] if p['state']['id']==pid)
        check(cid+':final-gathered-positive:P'+str(pid),all(math.isfinite(player['gathered'][k]) and player['gathered'][k]>=0 for k in RES) and any(player['gathered'][k]>0 for k in RES))
        # Gather events flush small per-worker buckets. The player ledger sums
        # each tick in float32 and is not the double sum of those event buckets.
        gather_reports.append({'player':pid,'eventCount':len(ge),'loggedPositiveAmountsByResource':sums,'finalCumulativeFloatLedger':player['gathered'],
             'ledgerMinusLoggedDoubleSum':{k:player['gathered'][k]-sums[k] for k in RES},'firstPositiveEvent':event_ref(ge[0][1],ge[0][0]) if ge else None,
             'lastPositiveEvent':event_ref(ge[-1][1],ge[-1][0]) if ge else None})
    rejections=[{'time':s['time'],'player':s['player'],'gatherOrders':s['gatherOrders'],'rejected':s['gatherRoutesRejectedAtSample']} for s in samples if s['gatherRoutesRejectedAtSample']>0]
    reports.append({'id':cid,'declaredIdentity':case,'scenarioIdentity':scenario_identity,'eventCount':len(events),'sampleCount':len(samples),'eventKinds':dict(counts),
      'boundarySnapshots':{n:{'time':worlds[n]['time'],'stepCount':sims[n]['stepCount'],'winner':worlds[n]['winner'],'playerAges':[p['state']['age'] for p in worlds[n]['players']]} for n in worlds},
      'gather':{'actualCredits':gather_reports,'sampledRejectionRows':rejections,'sampleRowsWithRejections':len(rejections),'sumRejectedAcrossSampleRows':sum(r['rejected'] for r in rejections),
        'scope':'Count of current Gather orders not admitted at a30s settled sample: includes hidden/depleted/blocked/unreachable/out-of-extraction-range/ineligible/unsafe ordinary routes; not a distinct-worker count or proof of intermediate geometric danger.'},
      'market':{'trades':trades,'count':len(trades),'sellCount':sum(not t['buy'] for t in trades),'buyCount':sum(t['buy'] for t in trades),'snapshotsAndAllSamplesLedgerReconciled':True,
        'scope':'Actual event-triggered exchanges only. Zero exchanges are preserved; no native GUI interactions inferred.'},
      'economy':{'transactionEvents':economy_events,'maxActualDeltaArithmeticError':max_transaction_error,'marketPairingTolerance':0.01,'boundaryTradeIncomeReconciled':True,'boundaryCargoLedgerTechnologyReconciled':True},
      'postLedger':ledgers,'sameTickMarketToLedgerBridges':bridges,
      'army':{'assembledCount':len(assembled),'assembledByPlayer':dict(collections.Counter(e['player'] for _,e in assembled)),'commandCounts':dict(command_counts),
        'fireCount':counts['Fire'],'damageCount':counts['Damage'],'positivePvpDamageCount':len(pvp_damage),'lateCombatObserved':late_combat_observed,
        'last300Seconds':{'start':tail_start,'end':case['time'],'eventKinds':dict(tail_counts),'positivePvpDamageCount':len(tail_pvp),
          'positivePvpDamageAmount':sum(e['amount'] for _,e in tail_pvp),'lastPvpDamage':event_ref(tail_pvp[-1][1],tail_pvp[-1][0]) if tail_pvp else None},
        'sampleArmyPhases':dict(collections.Counter(s['army']['phase'] for s in samples)),'maxSampleCursor':max(s['army']['stageSearchCursor'] for s in samples)},
      'timing':{'wallSeconds':case['wallSeconds'],'stepSeconds':case['stepSeconds'],'observerSeconds':case['observerSeconds'],'finalWriteSeconds':case['finalWriteSeconds'],
        'otherSetupAndNonStepSeconds':case['wallSeconds']-elapsed,'meanStepMsDerived':case['stepSeconds']*1000/case['steps'],'maxStepMs':case['maxStepMs'],
        'stepsOver100ms':case['stepsOver100ms'],'stepsOver500ms':case['stepsOver500ms'],'stepSecondsNear1500Bracket':{'lower':lower,'upper':upper,'exactBoundaryStepTimingRecorded':False}}})

after_refs=[ref(p) for p in paths]
check('all-closed-originals-and-source-references-two-read-stable',before_refs==after_refs)
check('exact-three-cases-validated',len(reports)==3)
report={'schema':'frontier074-closed-evidence-validation-v2','validatedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
  'status':'verified_closed_full_build_declared_three_cases' if not failures else 'failed_closed_evidence_validation','acceptedClosedEvidenceScope':not failures,
  'validatorRole':'Gather074 implementation author validating closed recorded evidence; not an independent implementation audit.',
  'sourceFingerprint':actual_fp,'sourceCount':len(files),'runtimeCommit':COMMIT,'sourceMapEqualsBeforeFullFocusedBuildNormalAndGit':files==git_files==full['files'],
  'appVersion':manifest['version'],'buildGuid':guid_match.group(1) if guid_match else None,'rulesHashObservedConsistent':manifest['rulesHash'],
  'focused':focused_xml,'regression':regression,'initialAndRedGreenProvenance':provenance,'runnerReceipts':{'focused':focused,'full':full_runner,'build':build,'normal':runner},'predeclaration':declaration,
  'caseReports':reports,'checks':check_count,'failures':failures,'references':before_refs,'originalsStableDuringValidation':before_refs==after_refs,
  'nativeCurrentBuildValidated':False,'externalDeliveryValidated':False,'overallGameGapGoalComplete':False,
  'limits':['Exactly three predeclared ordinary ageI two-AI cases; cap and all actual outcomes retained, not balance certification.',
    'Gather admission is conservative and periodic at AI reviews. Depletion retargeting, new vision, steering and recovery between reviews remain outside continuous safety guarantees.',
    'Rejected sample rows aggregate authority/eligibility/path causes and do not reconstruct intermediate paths or exact causes.',
    'Gather credit events and per-tick float32 gathered ledgers are real separate aggregates; their sums are not falsely declared byte-exact.',
    'Headless Stopwatch Step/observer timing does not certify native FPS, isolated CPU performance or pure causal Home speedup.',
    'No currentGUID native interaction/cold/replay, package integrity or cloud delivery is certified by this report.',
    'Original boundary World snapshots exclude event arrays; full separate events.jsonl read and hashed.'],
  'actions':{'originalsReadOnly':True,'editedRepo':False,'ranUnity':False,'ranTests':False,'reranSimulation':False,'built':False,'nativeActions':False,'packageOrCloudMutation':False}}
with json_target.open('x') as f:f.write(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
lines=['#074 闭合证据验证','',f"{report['status']}；{check_count}项检查，{len(failures)}项失败。",'',
 '验证者编写了Gather074运行实现；本记录是对闭合原件的验证，不宣称独立实现审计。',
 f"运行提交 `{COMMIT}`；740输入FP `{actual_fp}`。最终完整回归1646项（1227Edit/419Play），旧1611 fullname保留，新增18Gather/17Home；Focused64另核，不能叠加为更多测试。",
 '初始Focused63/Full1645的b72源码、预期ProjectionRed1的5af源码原件单列保留；只把最终61d源码的Focused64/Full1646作为最终通过证据。红绿使用同一个测试文件SHA，仅准入helper改变，未弱化断言。',
 f"实际构建版本0.17.0，GUID `{report['buildGuid']}`；无Prepare，exit0及sourceStable、真实app/boot/plist/executable核对。",'',
 '|Case|实际结果|Events/samples|Market/assembled|采样拒绝行/累计数|末300秒Fire/正PvPDamage|wall/Step/maxStep|',
 '|---|---|---:|---:|---:|---:|---|']
for r in reports:
 c=r['declaredIdentity'];a=r['army'];g=r['gather'];t=r['timing'];tail=a['last300Seconds']
 lines.append(f"|{r['id']}|{c['status']} {c['time']:.6f}s winner={c['winner']}|{r['eventCount']}/{r['sampleCount']}|{r['market']['count']}/{a['assembledCount']}|{g['sampleRowsWithRejections']}/{g['sumRejectedAcrossSampleRows']}|{tail['eventKinds'].get('Fire',0)}/{tail['positivePvpDamageCount']}|{t['wallSeconds']:.3f}s/{t['stepSeconds']:.3f}s/{t['maxStepMs']:.3f}ms|")
lines+=['','开局钱包与文明默认值相同；最高V、standard双AI、2400秒cap/1500比较点/30秒采样。所有actual自然终局或cap依原manifest核对，不指定旧073赢家、时长或事件总数。Dutch scenario envelope/custom_scenario/embedded document/hash按真实合同核对。',
 '','真实Gather正收入事件与最终gathered账本分别列于JSON。前者按每工人缓冲日志，后者按每tick float32累计；保留数值差异，不冒称精确总和。Market逐笔边际报价/实际付款及到账链、各边界及样本市场账本/价格、真实Post/Ledger/TradeDelivery、集结次数/作战/终局与原件核对；零触发照实保留。',
 '','gatherRoutesRejectedAtSample是30秒settled边界上的不可准入数，可含目标失效/隐藏、物理不通、投射工作点超实际采集范围、Hold或多队列不合资格、普通路径危险。不是独立工人人数、持续穿越或具体危险原因。代码仍在下次AI review处理，未加入逐帧安全约束。',
 '','Step/observer/finalWrite分项计时均为headless实际Stopwatch，不推为GUI帧率或独立性能改善。当前GUID原生交互/冷恢复/录像、封包和云盘交付均不在本验证范围。',
 '','选定原件、源码两次SHA/byte/mode/identity保持相同；没有原件/repo修改，没有运行Unity/测试/模拟/构建或原生操作，没有封包/上传。']
if failures:lines+=['','失败检查：','```json',json.dumps(failures,ensure_ascii=False,indent=2),'```']
with md_target.open('x') as f:f.write('\n'.join(lines)+'\n')
for p in (json_target,md_target):print(json.dumps({'path':str(p),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}))
print(json.dumps({'status':report['status'],'checks':check_count,'failureCount':len(failures),'failures':failures[:20],
                 'cases':[{'id':r['id'],'status':r['declaredIdentity']['status'],'events':r['eventCount'],'samples':r['sampleCount'],'rejectedSampleRows':r['gather']['sampleRowsWithRejections']} for r in reports]},ensure_ascii=False))
raise SystemExit(0 if not failures else 1)
