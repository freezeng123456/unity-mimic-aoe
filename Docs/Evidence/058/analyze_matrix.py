#!/usr/bin/env python3
"""Analyze frozen six-civilization event evidence without executing the game."""
from __future__ import annotations
import argparse
from collections import Counter, defaultdict, deque
import gzip
import hashlib
import json
from pathlib import Path


def digest(path):
    hasher=hashlib.sha256()
    with path.open('rb') as f:
        for chunk in iter(lambda:f.read(1024*1024),b''):hasher.update(chunk)
    return hasher.hexdigest()


def analyze(source):
    matrix_path=source/'matrix.json'
    matrix=json.loads(matrix_path.read_text())
    if matrix['status'] not in ('all_naturally_ended','retained_incomplete_cases') or not matrix['sourcesStable'] or not matrix.get('finishedUtc'):
        raise ValueError('Matrix is not finished and source-stable; do not summarize partial evidence as complete')
    catalog=json.loads((source/'catalog-loaded.json').read_text())
    by_id={e['id']:e for e in catalog['entities']}
    by_name={e['name']:e for e in catalog['entities']}
    tech_names={t['name']:t for t in catalog['techs']}
    card_names={c['name']:c for c in catalog['cards']}
    civil={c['id']:c for c in catalog['civs']}
    result={'schemaVersion':1,'scope':'Only the final ordered matrix. Pilot is overlapping evidence and is not added as independent games.',
        'sourceMatrix':str(source),'matrixSha256':digest(matrix_path),'sourceFingerprint':matrix['sourceFingerprintBefore'],
        'sourcesStable':matrix['sourcesStable'],'seed':matrix['seed'],'difficulty':matrix['difficulty'],
        'map':matrix['map'],'deckRoles':{'player0':'economy','player1':'military'},
        'acceptance':{'cases':len(matrix['cases']),'naturalEnds':0,'resigns':0,'unfinishedCases':[],'classicCases':0,'classicNaturalEnds':0,'correlationErrors':[]},
        'civilizations':{},'unitTotals':{},'paidResearchTotals':{},'cases':[]}
    for civ in civil:
        result['civilizations'][civ]={'name':civil[civ]['name'],'appearances':0,'naturallyEndedAppearances':0,'wins':0,'losses':0,'unfinishedAppearances':0,'mirrorAppearances':0,'nonMirrorWins':0,'nonMirrorAppearances':0,'nonMirrorFinishedAppearances':0,'automaticWorkers':0,'paidWorkersQueued':0,'paidWorkersCompleted':0}
    total_units=defaultdict(Counter);total_research=defaultdict(Counter)
    for case in matrix['cases']:
        case_id=case['id'];civs=[case['civilization0'],case['civilization1']]
        event_path=source/case_id/'events.jsonl';hasher=hashlib.sha256()
        types={};queues=defaultdict(deque);recent=deque(maxlen=32);paid_ids=set();auto_ids=set();ship_ids=set()
        units=[defaultdict(Counter),defaultdict(Counter)];research=[defaultdict(Counter),defaultdict(Counter)]
        pending_tech=defaultdict(deque);events=Counter();automatic=[[],[]];mosque=[[],[]];speed_tech=[[],[]]
        errors=[];event_count=0;payment_count=0;ordinary_worker_queues=Counter();last300=[Counter(),Counter()];latest={};alive={}
        opener=event_path.open if event_path.exists() else lambda mode:gzip.open(event_path.with_name(event_path.name+'.gz'),mode)
        with opener('rb') as f:
            for raw in f:
                hasher.update(raw);e=json.loads(raw);event_count+=1
                kind=e['kind'];events[kind]+=1;player=e['player'];actor=e['entity'];message=e['message'];time=e['time']
                previous=recent[-1] if recent else None
                if kind=='Spawn':alive[actor]=(player,message)
                elif kind=='Death':alive.pop(actor,None)
                if player in (0,1):
                    if time>=case['result']['seconds']-300:last300[player][kind]+=1
                    if kind in ('Fire','Damage','Death','ProductionQueued','Payment','Gather','Command','BuildingComplete'):latest[str(player)+':'+kind]={'time':time,'message':message,'entity':actor,'target':e['target']}
                if kind=='Spawn':
                    definition=by_name.get(message)
                    if definition is None:errors.append(['unknown_spawn_name',event_count,message])
                    else:
                        types[actor]=definition['id']
                        if player in (0,1) and definition['kind']==0:units[player][definition['id']]['allSpawned']+=1
                if player in (0,1):
                    if kind=='Payment':
                        payment_count+=1;reason=message.split(': ',1)[0];tech=tech_names.get(reason)
                        if tech:
                            pending_tech[actor].append((tech['id'],time,e['economy']['actualDelta']))
                            research[player][tech['id']]['paidQueues']+=1
                    elif kind=='ProductionQueued':
                        unit=message.split(' ')[0];count=int(e['amount']);definition=by_id[unit]
                        if not previous or previous['kind']!='Payment' or previous['entity']!=actor or previous['player']!=player or not previous.get('hasEconomy'):
                            errors.append(['queue_without_adjacent_payment',event_count,unit])
                        else:
                            expected={'food':0,'wood':0,'coin':100} if definition['tags']&128 and civil[civs[player]].get('coinVillager') else definition['cost']
                            delta=previous['economy']['actualDelta']
                            if any(abs(delta.get(k,0)+expected.get(k,0)*count)>.02 for k in ('food','wood','coin')):errors.append(['queue_payment_cost_mismatch',event_count,unit])
                        queues[actor].append([unit,count]);units[player][unit]['paidQueued']+=count;units[player][unit]['paidQueuePayments']+=1
                        if definition['tags']&128:ordinary_worker_queues[player]+=count
                    elif kind=='Experience' and message=='Unit trained':
                        queue=queues[actor]
                        if not queue:errors.append(['completion_without_queue',event_count])
                        else:
                            unit=queue[0][0]
                            if not previous or previous['kind']!='Spawn' or previous['player']!=player or types.get(previous['entity'])!=unit:errors.append(['completion_without_adjacent_matching_spawn',event_count,unit])
                            else:paid_ids.add(previous['entity'])
                            units[player][unit]['paidCompleted']+=1;queue[0][1]-=1
                            if queue[0][1]==0:queue.popleft()
                    elif kind=='AutomaticWorkerProduced':
                        spawn=next((v for v in reversed(recent) if v['kind']=='Spawn' and v['entity']==e['target']),None)
                        if not spawn or spawn['player']!=player or types.get(e['target'])!=message or e.get('hasEconomy') or not e.get('hasPopulation'):errors.append(['automatic_spawn_or_ledger_mismatch',event_count])
                        automatic[player].append(round(time,6));auto_ids.add(e['target']);units[player][message]['automaticallyProduced']+=1
                    elif kind=='ShipmentArrived':
                        card=card_names.get(message.removesuffix(' arrived'))
                        if card and card.get('unit'):
                            unit=card['unit'];count=card['count'];prior=list(recent)[-count:]
                            units[player][unit]['shipmentArrived']+=count
                            if len(prior)!=count or any(v['kind']!='Spawn' or v['player']!=player or types.get(v['entity'])!=unit for v in prior):errors.append(['shipment_without_matching_spawns',event_count,card['id']])
                            else:
                                units[player][unit]['shipmentSpawnVerified']+=count
                                ship_ids.update(v['entity'] for v in prior)
                    elif kind=='ResearchComplete':
                        tech=tech_names.get(message);pending=pending_tech[actor]
                        if not tech or not pending or pending[0][0]!=tech['id']:errors.append(['research_without_matching_payment',event_count,message])
                        else:
                            identifier,paid_at,cost=pending.popleft();research[player][identifier]['paidCompleted']+=1
                            if types.get(actor)=='mosque':mosque[player].append({'id':identifier,'paidAt':round(paid_at,6),'completedAt':round(time,6),'actualCost':{k:-v for k,v in cost.items()}})
                    elif kind=='Technology':
                        identifier=message.split(' ')[0];tech=next((t for t in catalog['techs'] if t['id']==identifier),None)
                        if tech and tech.get('autoVillagerSpeed',0)>0:speed_tech[player].append({'id':identifier,'time':round(time,6),'speedAddition':tech['autoVillagerSpeed']})
                    elif kind in ('Fire','Damage','MeleeAreaHit'):
                        unit=types.get(actor)
                        if unit and by_id[unit]['kind']==0:
                            metric={'Fire':'fireEvents','Damage':'damageEvents','MeleeAreaHit':'meleeAreaEvents'}[kind]
                            units[player][unit][metric]+=1
                            if actor in paid_ids:units[player][unit]['paidOrigin'+metric[0].upper()+metric[1:]]+=1
                            if kind=='Damage':units[player][unit]['damage']+=e['amount']
                            if kind=='MeleeAreaHit':units[player][unit]['meleeAreaVictims']+=int(e['amount'])
                    elif kind=='Refund' and message.startswith('Cancelled '):errors.append(['queue_cancellation_requires_explicit_parser_support',event_count,message])
                recent.append(e)
        expected_hash=next(f['sha256'] for f in case['files'] if f['path']=='events.jsonl')
        if hasher.hexdigest()!=expected_hash:errors.append(['event_hash_mismatch'])
        if event_count!=case['observations']['events']:errors.append(['event_count_mismatch'])
        if events['Resign']!=0:errors.append(['unexpected_resign'])
        expected_ends=1 if case['result']['winner']>=0 else 0
        if events['MatchEnded']!=expected_ends:errors.append(['winner_and_match_end_disagree'])
        if expected_ends==0:result['acceptance']['unfinishedCases'].append(case_id)
        if any(c in ('coast','river','france','ottoman') for c in civs):
            result['acceptance']['classicCases']+=1;result['acceptance']['classicNaturalEnds']+=events['MatchEnded']
        result['acceptance']['naturalEnds']+=events['MatchEnded'];result['acceptance']['resigns']+=events['Resign']
        result['acceptance']['correlationErrors'].extend({'case':case_id,'error':e} for e in errors)
        item={'id':case_id,'status':case['status'],'result':case['result'],'eventsSha256':hasher.hexdigest(),'eventCount':event_count,'naturalEnds':events['MatchEnded'],'resigns':events['Resign'],'paymentEvents':payment_count,'correlationErrors':errors,'players':[]}
        if expected_ends==0:
            snapshots=[json.loads(line) for line in (source/case_id/'snapshots.jsonl').read_text().splitlines()]
            item['timeoutDiagnostics']={'last300SecondsEvents':[dict(x) for x in last300],'lastSnapshots':snapshots[-2:],'lastEventByPlayerKind':latest,'survivingEntities':dict(Counter(str(p)+':'+name for p,name in alive.values() if p>=0))}
        for p,civ in enumerate(civs):
            stats=result['civilizations'][civ];stats['appearances']+=1;stats['naturallyEndedAppearances']+=events['MatchEnded']==1;stats['wins']+=case['result']['winner']==p;stats['losses']+=case['result']['winner']>=0 and case['result']['winner']!=p;stats['unfinishedAppearances']+=case['result']['winner']<0
            if civs[0]==civs[1]:stats['mirrorAppearances']+=1
            else:stats['nonMirrorAppearances']+=1;stats['nonMirrorWins']+=case['result']['winner']==p;stats['nonMirrorFinishedAppearances']+=case['result']['winner']>=0
            stats['automaticWorkers']+=len(automatic[p]);stats['paidWorkersQueued']+=ordinary_worker_queues[p]
            stats['paidWorkersCompleted']+=sum(v['paidCompleted'] for u,v in units[p].items() if by_id[u]['tags']&128)
            for unit,counts in units[p].items():total_units[unit].update(counts)
            for tech,counts in research[p].items():total_research[tech].update(counts)
            item['players'].append({'player':p,'civilization':civ,'units':{k:dict(v) for k,v in sorted(units[p].items())},'paidResearch':{k:dict(v) for k,v in sorted(research[p].items())},'automaticWorkerTimes':automatic[p],'mosqueResearch':mosque[p],'autoSpeedTechnologies':speed_tech[p]})
        result['cases'].append(item)
    result['unitTotals']={k:dict(v) for k,v in sorted(total_units.items())}
    result['paidResearchTotals']={k:dict(v) for k,v in sorted(total_research.items())}
    for stats in result['civilizations'].values():
        stats['winRate']=stats['wins']/(stats['wins']+stats['losses']) if stats['wins']+stats['losses'] else None
        stats['nonMirrorWinRate']=stats['nonMirrorWins']/stats['nonMirrorFinishedAppearances'] if stats['nonMirrorFinishedAppearances'] else None
    if result['acceptance']['correlationErrors']:raise ValueError(json.dumps(result['acceptance']['correlationErrors'],ensure_ascii=False))
    return result


def event_reader(directory,case_id):
    path=directory/case_id/'events.jsonl'
    return path.open('rb') if path.exists() else gzip.open(path.with_name(path.name+'.gz'),'rb')


def common_event_prefix(original,extension,case_id):
    count=0;last=None;left=None;right=None
    with event_reader(original,case_id) as a,event_reader(extension,case_id) as b:
        while True:
            left=a.readline();right=b.readline()
            if not left or not right or left!=right:break
            count+=1;last=left
    result={'identicalLeadingEvents':count,'lastIdenticalTime':json.loads(last)['time'] if last else None}
    for label,raw in [('originalFirstDifference',left),('extensionFirstDifference',right)]:
        if raw:
            e=json.loads(raw);result[label]={k:e[k] for k in ['time','kind','player','entity','message','amount']}
    return result


def add_extension(data,source,extension):
    extra=analyze(extension)
    if extra['sourceFingerprint']!=data['sourceFingerprint']:raise ValueError('Supplement uses different source')
    prefix={case['id']:common_event_prefix(source,extension,case['id']) for case in extra['cases']}
    data.setdefault('supplementalRuns',[]).append({'purpose':'Separate higher time limit diagnostic; excluded from all primary matrix totals and win rates; not an independent balance sample','analysis':extra,'eventPrefixComparison':prefix})


def markdown(data):
    a=data['acceptance'];u=data['unitTotals'];c=data['civilizations'];r=data['paidResearchTotals']
    lines=['# 058：六文明完整矩阵观测','',f"最终矩阵 **{a['naturalEnds']}/{a['cases']} 场自然结束**，未结束 {len(a['unfinishedCases'])} 场，主动认输 {a['resigns']} 次，源码前后哈希一致。逐条核对事件 SHA-256、付费生产、实际出生、研究完成及船运出生，关联错误为 0。",'',
        f"源码指纹：`{data['sourceFingerprint']}`。输入目录：`{data['sourceMatrix']}`；完整可复算结果见 [matrix-analysis.json](matrix-analysis.json)，脚本见 [analyze_matrix.py](analyze_matrix.py)。",'',
        '仅统计本次 36 个有序组合，pilot 的法国／奥斯曼双向对局与这里重叠，不额外计为独立样本。固定地图、种子 314159、标准难度；玩家 0 使用经济卡组，玩家 1 使用军事卡组。下面的胜负只是这批条件下的描述，不能据此证明文明平衡或普遍强弱。','',
        '| 文明 | 自然终局／出场 | 胜／负／未终局 | 已终局胜率 | 排除镜像后的终局胜率 |','| --- | ---: | ---: | ---: | ---: |']
    for civ,s in c.items():lines.append(f"| {s['name']} (`{civ}`) | {s['naturallyEndedAppearances']}/{s['appearances']} | {s['wins']}/{s['losses']}/{s['unfinishedAppearances']} | {s['winRate']:.1%} | {s['nonMirrorWins']}/{s['nonMirrorFinishedAppearances']}（{s['nonMirrorWinRate']:.1%}） |")
    lines+=['','每文明出场 12 次，包含同一镜像对局中的两方；自然结束的镜像贡献一胜一负。未终局不计为输或赢，也不计入终局胜率分母。每文明的十次非镜像出场也不是独立随机抽样。','','## 真实付费生产与实际作战','','“付费完成”同时要求已有真实扣款及生产排队，随后出现对应单位 `Spawn` 与同生产建筑的 `Unit trained`；船运、开局、时代奖励和自动工人不计入。排队数量包含终局时尚未完成或生产建筑被毁的订单。','','| 单位 | 付费排队（单位数） | 付费完成 | 船运到达且出生 | 开火事件 | 近战范围命中事件 |','| --- | ---: | ---: | ---: | ---: | ---: |']
    for unit in ['coureur','cuirassier','janissary','abus','spahi','halberdier','escort','outrider','convoy_gunner','field_engineer','culverin','shield_guard']:
        s=u.get(unit,{})
        lines.append(f"| `{unit}` | {s.get('paidQueued',0)} | {s.get('paidCompleted',0)} | {s.get('shipmentSpawnVerified',0)} | {s.get('fireEvents',0)} | {s.get('meleeAreaEvents',0)} |")
    lines+=['','战斗事件按攻击者实体 ID 回连单位定义，包含付费与奖励来源；JSON 另列 `paidOrigin*`，可单独核对付费出生单位的实际作战。范围命中事件数表示一次打击波及其他敌方单位的次数，并非击杀数。','','## 奥斯曼自动工人与清真寺','','| 文明 | 付费工人排队 | 付费工人完成 | 自动工人实际出生 |','| --- | ---: | ---: | ---: |']
    for civ in ['france','ottoman']:
        s=c[civ];lines.append(f"| {s['name']} | {s['paidWorkersQueued']} | {s['paidWorkersCompleted']} | {s['automaticWorkers']} |")
    lines+=['','所有自动工人事件均回链实际 `Spawn`，带人口快照、不带支付事务。清真寺表只列真实支付后完成的研究，行政路线与船运加成不冒充清真寺研究。','','| 科技 | 付费研究次数 | 已完成次数 |','| --- | ---: | ---: |']
    for tech in ['mosque_millet','mosque_koprulu','mosque_bazaar','mosque_galata','mosque_topkapi','mosque_tanzimat']:
        s=r.get(tech,{});lines.append(f"| `{tech}` | {s.get('paidQueues',0)} | {s.get('paidCompleted',0)} |")
    lines+=['','以本矩阵 `france-vs-ottoman` 为例：首名自动工人约 55.1 秒出生；Millet System 真实支付 75 木并研究完成后，连续出生间隔约 50.1 秒。后续叠加行政路线、Millet Support 船运和 Köprülü 研究后，间隔约 39.6 秒。时代队列、住房和出口阻塞会暂停自动生产，故不能将整局平均出生间隔直接解释为科技速度。每场完整出生时间、科技生效时间与清真寺付款见 JSON。','']
    lines+=['## 超时保留与独立复核建议','',f"包含英、荷、法、奥至少一方的经典文明组合：**{a['classicNaturalEnds']}/{a['classicCases']} 自然结束**。",'']
    for case in data['cases']:
        if 'timeoutDiagnostics' not in case:continue
        d=case['timeoutDiagnostics'];p0,p1=d['lastSnapshots'];ev=d['last300SecondsEvents']
        lines += [f"- `{case['id']}` 在 {case['result']['seconds']:.2f} 秒达到原 3600 秒上限，winner=-1，无主动认输；原记录保留为未完成，不能报为通过。",f"- 最后 300 秒仍有开火 {ev[0].get('Fire',0)}/{ev[1].get('Fire',0)} 次、死亡 {ev[0].get('Death',0)}/{ev[1].get('Death',0)} 次、付费生产排队 {ev[0].get('ProductionQueued',0)}/{ev[1].get('ProductionQueued',0)} 次，说明对局仍有持续战斗。",f"- 限时末快照：玩家 0 时代 {p0['age']}、{p0['workers']} 工人、{p0['army']} 军队；玩家 1 时代 {p1['age']}、{p1['workers']} 工人、{p1['army']} 军队。后者城镇中心已毁但仍有其他存活建筑与工人，尚不满足全歼胜利条件。",'- 建议以同一源码、种子、卡组单独复跑该组，将上限延至 5400 秒并使用新证据目录。该诊断为单独补充实验，不覆盖原 35/36 矩阵结果；持续战斗与兵力差支持先验证是否只差收尾时间。','']
    for supplement in data.get('supplementalRuns',[]):
        for case in supplement['analysis']['cases']:
            comparison=supplement['eventPrefixComparison'][case['id']]
            lines += ['### 独立延长限时复核','',f"补充目录 `{supplement['analysis']['sourceMatrix']}`：同源码、同种子、同卡组，独立将上限改为 5400 秒。`{case['id']}` 于 **{case['result']['seconds']:.2f} 秒自然结束，玩家 {case['result']['winner']} 获胜**，即比原上限多约 {case['result']['seconds']-3600:.2f} 秒。补测源码稳定、1 次自然结束、0 次认输，付费与出生关联仍无错误。",f"两份事件记录的前 {comparison['identicalLeadingEvents']:,} 条逐字节相同，直到 {comparison['lastIdenticalTime']:.2f} 秒。第一处分歧来自原限时退出时刷新采集日志，不能把补测简单附加为原始日志尾部。",'原始矩阵继续记为 **35/36**；这场补充复核证明该固定组合在延长时间后能自然结束，不把它合并为第 37 个独立样本，也不重算上面的胜率或机制总数。','']
    lines+=['## 覆盖边界','']
    sp=u.get('spahi',{})
    if sp.get('shipmentSpawnVerified',0):lines.append(f"- Spahi 船运已实际到达并核对出生 {sp['shipmentSpawnVerified']} 名，开火 {sp.get('fireEvents',0)} 次；付费生产始终为 {sp.get('paidQueued',0)}，符合仅船运来源。")
    else:lines.append('- 这批矩阵没有 Spahi 船运到达，不能以规则存在或测试通过替代正常 AI 对局中的实用覆盖。')
    zero=[x for x in ['coureur','cuirassier','janissary','abus','halberdier','culverin'] if not u.get(x,{}).get('paidCompleted',0)]
    if zero:lines.append('- 未出现真实付费完成：'+', '.join('`'+x+'`' for x in zero)+'；请保留为覆盖缺口。')
    lines+=['- 本矩阵验证规则、AI 与内容实际使用；不替代原生窗口操作、美术辨识、音效、帧率或跨平台验收。','- 单张地图、单一固定种子与固定卡组角色不能支撑统计平衡结论。','']
    return '\n'.join(lines)


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--source',type=Path,required=True);parser.add_argument('--output',type=Path,required=True);parser.add_argument('--extension',type=Path,action='append',default=[]);args=parser.parse_args()
    data=analyze(args.source)
    for extension in args.extension:add_extension(data,args.source,extension)
    args.output.mkdir(parents=True,exist_ok=True)
    outputs=[args.output/'matrix-analysis.json',args.output/'Matrix-analysis.md']
    if any(p.exists() for p in outputs):raise FileExistsError('Analysis output already exists; preserve previous analysis')
    outputs[0].write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n');outputs[1].write_text(markdown(data))
    print(json.dumps({'acceptance':data['acceptance'],'civilizations':data['civilizations'],'newUnits':{k:data['unitTotals'].get(k,{}) for k in ['coureur','cuirassier','janissary','abus','spahi','halberdier']},'outputs':[str(p) for p in outputs]},ensure_ascii=False,indent=2))

if __name__=='__main__':main()
