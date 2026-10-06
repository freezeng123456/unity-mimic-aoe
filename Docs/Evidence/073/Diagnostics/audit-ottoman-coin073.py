#!/usr/bin/env python3
"""Read only closed 072 raw ledger; write fresh 073 diagnostics."""
import collections,hashlib,json,pathlib,subprocess
ROOT=pathlib.Path('/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.15.0/Candidate1/NormalRoutesFinal1/matrix/winding-ottoman-44')
OUT=pathlib.Path(__file__).parent
REPO='/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project'
COMMIT='bc127e02638caaeefd2310a1a3eff858f4f4cb2f'
FILES=['events.jsonl','samples.jsonl','opening-world.json','early-terminal-world.json','result.json']
def digest_file(p):
 h=hashlib.sha256()
 with p.open('rb') as f:
  for b in iter(lambda:f.read(1024*1024),b''):h.update(b)
 s=p.stat();return {'path':str(p),'bytes':s.st_size,'sha256':h.hexdigest(),'mtimeNs':s.st_mtime_ns,'inode':s.st_ino}
before={n:digest_file(ROOT/n) for n in FILES}
source={}
for name in ['AiWorkforce.cs','AiResourceContinuity072.cs','SkirmishAI.cs','Economy.cs','World.cs','AiCivilizationEconomy.cs']:
 path='Assets/Frontier/Simulation/'+name
 raw=subprocess.check_output(['git','show',COMMIT+':'+path],cwd=REPO)
 source[path]={'commit':COMMIT,'bytes':len(raw),'sha256':hashlib.sha256(raw).hexdigest()}
opening=json.loads((ROOT/'opening-world.json').read_text());terminal=json.loads((ROOT/'early-terminal-world.json').read_text())
miners={2,268,256,250,243,236,234,231};retry={187,4,142,149,141}
spawn={};mines={};kinds=collections.Counter();gathers=collections.defaultdict(lambda:{'count':0,'amount':0.,'first':None,'last':None,'miners':set()})
depletion=[];evacuation=[];retry_commands=[];miner_recovery=[];enemy_window=[];post_coin_orders=[];last_flush=[];mine_credit_all=collections.defaultdict(float);payments=[];resources_count=0
selected=[]
def short(e,line,offset,length):
 return {**{k:e[k] for k in ('time','kind','message','player','entity','target','visibilityMask','amount','position')},'line':line,'byteOffset':offset,'lineBytes':length}
offset=0
with (ROOT/'events.jsonl').open('rb') as f:
 for line,raw in enumerate(f,1):
  e=json.loads(raw);k=e['kind'];t=e['time'];kinds[k]+=1;s=short(e,line,offset,len(raw));offset+=len(raw)
  if k=='Spawn':
   spawn[e['entity']]=s
   if e['message']=='Coin Mine':mines[e['entity']]=s
  if k=='ResourceDepleted' and e['entity'] in mines:depletion.append(s)
  if k=='Gather' and e['message']=='Coin':
   mine_credit_all[e['target']]+=e['amount']
   if e['player']==0:
    g=gathers[e['target']];g['count']+=1;g['amount']+=e['amount'];g['first']=g['first'] or s;g['last']=s;g['miners'].add(e['entity'])
    if t>975.5088500976562:last_flush.append(s)
  if 975.50<t<975.52 and e['player']==0 and e['entity'] in miners and k=='Command':evacuation.append(s)
  if 975<t<1011 and e['player']==0 and e['entity'] in miners and k in ('Garrison','RallyAssigned','Command'):miner_recovery.append(s)
  if 1050<t<1065 and e['player']==0 and e['entity'] in retry and k in ('Command','Garrison'):retry_commands.append(s)
  if 970<t<982 and e['player']==1 and k in ('AttackPrepared','Fire') and e['visibilityMask']&1:enemy_window.append(s)
  if e['player']==0 and k=='Command' and e['target'] in mines and t>1100:post_coin_orders.append(s)
  if e['player']==0 and k=='Payment' and t>975 and e['economy']['actualDelta']['coin']<0:payments.append({**s,'economy':e['economy']})
  if k=='Gather' and e['player']==0 and e['message']=='Coin' and 974<t<976:selected.append(s)
  if e['player']==1 and k=='AttackPrepared' and e['target'] in retry and 1052<t<1065:enemy_window.append(s)
opening_mines={e['state']['id']:{'remaining':e['state']['remaining'],'position':e['state']['position']} for e in opening['entities'] if e['definition']=='mine'}
terminal_mines={e['state']['id']:{'remaining':e['state']['remaining'],'dead':e['state']['dead']} for e in terminal['entities'] if e['definition']=='mine'}
samples=[]
for raw in (ROOT/'samples.jsonl').open():
 s=json.loads(raw)
 if s['player']!=0:continue
 if 901<s['time']<1235 or s['time']>1774:
  control=next(c for c in s['simulation']['controllers'] if c['player']==0)
  samples.append({k:s[k] for k in ('time','age','workers','soldiers','resources','spent','gathered','passive') }|{'agePlan':control['agePlan'],'buildings':[{'id':b['id'],'definition':b['definition'],'complete':b['complete']} for b in s['buildings']]})
assert len(evacuation)==8 and {e['entity'] for e in evacuation}==miners and all(e['message']=='Garrison ×1' and e['target']==1 for e in evacuation)
assert len([e for e in retry_commands if e['message']=='Gather ×1' and e['target']==104])==5
assert not post_coin_orders
assert all(e['remaining']==0 for e in terminal_mines.values())
assert gathers[103]['last']['time']>1009 and gathers[104]['last']['time']<910
flat=[s for s in samples if s['time']>=991];assert len({s['gathered']['coin'] for s in flat})==1 and all(s['passive']['coin']==0 for s in flat)
for g in gathers.values():g['miners']=sorted(g['miners'])
after={n:digest_file(ROOT/n) for n in FILES};assert before==after
result={
 'status':'closed_raw_ledger_cause_localized_without_rerun','rawRuntimeCommit':COMMIT,'case':'winding-ottoman-44','player':0,
 'scope':{'eventLedgerFullStream':True,'finalWorldLargeFileRead':False,'openingAndEarlyTerminalWorldRead':True,'noRuntimeIntervention':True,'noCodeEdits':True,'noOriginalFilesChanged':True},
 'rawFiles':before,'frozenSourceFiles':source,'eventCount':sum(kinds.values()),'eventKinds':dict(kinds),
 'openingMines':opening_mines,'terminalMines':terminal_mines,'mineDepletion':depletion,'p0CoinGatherBySource':dict(gathers),'allPlayersLoggedGatherCoinByTarget':dict(mine_credit_all),
 'mine103EvacuationCommands':evacuation,'mine103MinerGarrisonAndFoodWoodReassignment':miner_recovery,'mine104RetryAndReturnCommands':retry_commands,'nearbyVisibleEnemyEvidence':enemy_window,'lastRegularMiningFlushEvents':selected,'delayedCoinFlushAfterEvacuation':last_flush,'p0CoinOrdersAfter1100':post_coin_orders,'p0CoinPaymentsAfterMiningLost':payments,'p0Samples':samples,
 'confirmed':[
  'Local mines 16 and 37 depleted at 687.679 and 936.418. Coin continued from contested central mine 103 until all eight current miners were ordered into Town 1 at 975.508850.',
  'The 975.508850 Garrison commands coincide with a real visible enemy advance: Abus 254/222 acquired miner 234 at 975.808777/976.008728, and Janissary 253 acquired the same miner at 978.008240.',
  'Mine 103/104 were not depleted at the income stop; player 1 exhausted them at 1251.441528/1233.145996. All six mines are empty at terminal state.',
  'All eight miners survived the immediate withdrawal and entered Town 1 at 985.906311-988.005798; following ungarrison at 992.304749, they received wood/food orders. Logged Gather events until 1009.200623 are delayed income-log flushes, not new mining after the withdrawal.',
  'Player 0 made five later assignments to mine 104 at 1052.290161-1057.088989; every named worker instead received Garrison shortly after, and none earned any Coin from mine 104 on these attempts.',
  'Cumulative P0 GatherCoin is exactly flat at 2841.779785 from sample 991.905 through terminal, and passive Coin remains 0. No P0 commands target a mine after 1100.'
 ],
 'sourceSupportedExplanation':[
  'AiWorkforce selects candidates from GatherableResource plus Safe(resource.position), rates/quotas and Euclidean distance only. It does not test each candidate with CanGather(worker,resource), endpoint reachability, or the full threat-cleared route used by AiResourceContinuity072.',
  'SeekShelter is invoked when the current worker point is unsafe. It orders an available own garrison even across long travel distance. The mine-104 assignments followed by 1.2-2.4-second returns show a source can be accepted while its ordinary approach crosses current enemy weapons.',
  'For Age II non-Dutch Ottoman, finite coin loss yields NeedsCoin but no Estate (requires Age III) or Bank (Dutch-only). TradeBootstrap/LedgerBootstrap are guarded by NeedsWood. With hundreds of excess Wood, coin-only loss never authorizes the recovery Post/Ledger path; regular InvestInRoute is additionally gated by underAttack and saving.',
  'At sample 991.905 wallet 1789.740F/935.497W/371.140C can fund a full ordinary Post 200W + Ledger 200W150C start, but Coin drops below 150 by sample 1052.090 and below the minimum Janissary 25C by 1142.368. The full fee must be protected before discretionary military spend exhausts Coin.'
 ],
 'limits':[
  'No intermediate complete world snapshot exists at 975.508850. Exact threat geometry at that instant cannot be reconstructed from event positions alone; the source branch and near-time visible targeting events support raid-triggered shelter, but do not identify which exact threat predicate fired first.',
  'No intermediate path payload is logged on a Gather command. Mine-104 approach crossing hostile space is directly supported by point changes, Garrison and enemy acquisition records, but the exact A* segment sequence cannot be rederived from this ledger alone.',
  'No counterfactual restart or new natural match was run. Proposed fixes are not yet demonstrated to win this case or improve balance.',
  'Sum of logged Float32 Gather amounts need not bit-match the repeatedly added wallet/gathered accumulators; sums are descriptive and are not asserted to be an exact financial reconciliation.'
 ],
 'recommendedFixes':[
  'Keep correct shelter from hostile mine raids. Make ordinary worker assignments and continuity evaluate the same per-worker domain, reachable endpoints and safe transit rules, and ensure the issued movement actually follows the checked route or declines.',
  'Allow genuinely payable coin-only Post/Ledger recovery as well as wood shortages. Protect both still-unpaid startup costs early; preserve the higher-priority 500W Town recovery and genuine age/political commitments, and reject incomplete startup budgets.',
  'Add a player-facing Market buy/sell transaction with atomic explicit costs and deterministic price state, providing a usable low-age Coin bridge from actual surplus Food/Wood rather than wallet gifts. AI can use the same legal command under bounded reserve rules.',
  'Add meaningful fixtures for remaining-but-contested mines, safe source with unsafe transit, and Age II Ottoman Coin-only deficit with funded versus unfunded 400W150C bootstrap. Then observe a predeclared new natural mirror case under the new runtime identity.'
 ]}
path=OUT/'073-winding-ottoman44-closed-coin-audit.json';path.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
md='''# 073 closed Ottoman coin-income audit\n\nThis is a read-only audit of the completed 072 winding-ottoman-44 case, not a new game or a counterfactual pass. The full original event ledger was streamed. Opening and early-terminal world files and all player samples were read. The 156 MB final world was not needed. Five original file hashes/stats were unchanged after the audit. Runtime source was read from frozen commit `bc127e02638caaeefd2310a1a3eff858f4f4cb2f`, independently of any new edits.\n\nThe first enduring Coin stop is a raid-induced retreat from a still-live central mine, after both local mines were depleted. It is not the first Town loss and is not whole-map depletion at 991 seconds.\n\n| World seconds | Original observation |\n|---|---|\n| 687.679 | P0 local mine 16 depleted. |\n| 936.418 | P0 local mine 37 depleted; workers have already been sent to central mine 103. |\n| 975.509 | All eight current mine-103 workers receive Garrison orders to Town 1. |\n| 975.809 / 976.009 / 978.008 | Visible P1 Abus 254/222 and Janissary 253 acquire retreating miner 234. |\n| 985.906–988.006 | All eight named miners enter Town 1; they survive immediate retreat. |\n| 991.905 | 1789.740 F / 935.497 W / 371.140 C; gathered Coin 2841.779785 stays flat through terminal. |\n| 992.305–997.104 | Workers ungarrison and receive Wood/Food orders. |\n| 1009.201 | Last delayed flush of an earlier Coin credit. This event does not indicate mining at 1009 seconds. |\n| 1052.290–1057.089 | Five workers are sent to mine 104. They return to garrison after 1.2–2.4 seconds and never gather from it on these attempts. |\n| 1052.090 / 1142.368 | Wallet Coin falls to 141.140 / 11.140, beneath Ledger and then every ordinary Age II army price. |\n| 1233.146 / 1251.442 | P1 depletes central mines 104 / 103. |\n| 1982.763 | P0 loses; all mines are exhausted. |\n\nThe event references in the JSON include original line numbers and byte offsets. Gather events use delayed buffered emission: the last seven post-retreat events are tiny credits flushed from workers already back near Town. Do not use their emission times to infer actual income.\n\n## Source mechanism\n\n`AiWorkforce` screens source positions for current visible danger, then scores Euclidean distance and demand. It does not check the whole worker-to-source path or per-worker `CanGather`/reachability before issuing the ordinary Gather order. `AiResourceContinuity072` has stricter safe route checks. The five attempted approaches to mine 104 are consistent with that mismatch: the endpoint is accepted, but the worker enters hostile ground and is ordered back to shelter. The exact intermediate A* paths and first threat predicate at 975.509 were not logged and are not claimed.\n\nAt Age II Ottoman cannot use the Age III Estate or Dutch Bank. A Coin-only shortage with excess Wood also does not enable `TradeBootstrap`/`LedgerBootstrap`, because those conditions currently begin with `NeedsWood`. Regular route investment additionally stops under attack or age saving. At 991.905 there is enough to pay the full 400 W / 150 C Post + Ledger startup; waiting until 1052 loses that opportunity. A reserve must protect both genuinely unpaid steps before army expenditure drains Coin.\n\n## Supported repair direction\n\nKeep the correct mining withdrawal. Unify safe transit checks for ordinary workers and continuity, and carry the checked movement path into execution or decline unsafe assignments. Extend payable Coin-only trade recovery while retaining Town and real political obligations. Add an actual player Market buy/sell command using Food/Wood surplus, atomic cost checks and deterministic state; AI should invoke that same command without free resources.\n\nMeaningful regression cases should cover a still-live contested mine, a safe endpoint with an unsafe approach, and funded/unfunded Age II Ottoman Coin-only startup. A fresh predeclared natural match is still required after implementation; this audit establishes mechanism, not new balance or a win.\n'''
(OUT/'073-winding-ottoman44-closed-coin-audit.md').write_text(md)
print(json.dumps({'status':result['status'],'eventCount':result['eventCount'],'unchangedOriginalFiles':len(before),'json':digest_file(path),'md':digest_file(OUT/'073-winding-ottoman44-closed-coin-audit.md')},ensure_ascii=False))
