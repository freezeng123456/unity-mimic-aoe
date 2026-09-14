#!/usr/bin/env python3
"""Export the shipped catalog, never copied values from an earlier document."""
import hashlib
import json
import re
from pathlib import Path

project = Path(__file__).resolve().parents[1]
version = re.search(r'bundleVersion: (.+)', (project / 'ProjectSettings/ProjectSettings.asset').read_text()).group(1).strip()
release = version.replace('.', '')
source = project / 'Assets/Resources/Rules.json'
rules = json.loads(source.read_text())
lines = [f'# Frontier Reckoning {version} 实际数值表', '',
         f'由 `Tools/export-numbers.py` 直接读取 `Assets/Resources/Rules.json` 生成。以下均为 P 项目规则；没有声称是 AoE3:DE 已核实精确数值。运行与体验证据另见 Iteration-{release}.md。', '',
         f'规则 SHA-256：`{hashlib.sha256(source.read_bytes()).hexdigest()}`。', '',
         '费用次序 F/W/C；时间为模拟秒，距离为世界米。文明覆盖值在第一张表明确列出。']

def cost(value):
    return '/'.join(f'{value.get(k, 0):g}' for k in ('food', 'wood', 'coin'))

def table(title, headers, rows):
    lines.extend(['', '## ' + title, '', '| ' + ' | '.join(headers) + ' |', '| ' + ' | '.join(['---'] * len(headers)) + ' |'])
    for row in rows:
        lines.append('| ' + ' | '.join(str(v).replace('|', ',').replace('\n', ' ') for v in row) + ' |')

table('全局', ['参数', '配置值'], [(k, rules[k]) for k in ('deckCapacity', 'populationLimit', 'initialXp', 'xpIncrease', 'constructionRefund')] + [(k + ' 数量', len(rules[k])) for k in ('entities', 'techs', 'cards', 'ages', 'civs')])
table('文明', ['ID', '起始 F/W/C', '住房费用', '住房赠村民', '村民费用', '村民上限', '银行上限/每秒币'],
      [(c['id'], cost(c['startingResources']), cost(c['houseCost']), c.get('houseVillager', False), '0/0/100' if c.get('coinVillager') else cost(next(d for d in rules['entities'] if d['id'] == 'villager')['cost']), c['villagerLimit'], f"{c.get('bankLimit',0)}/{c.get('bankRate',0)}") for c in rules['civs']])
table('实体生产与建筑', ['ID / 名称', 'F/W/C', '时代/时间', 'HP', '人口/上限供给', '生产者/文明', '半径/占地/驻扎'],
      [(d['id'] + ' / ' + d['name'], cost(d.get('cost', {})), f"{d.get('age',1)}/{d.get('time',20)}", d.get('hp',100), f"{d.get('population',1)}/{d.get('capacity',0)}", f"{d.get('producer','—')}/{d.get('civilization','') or '共享'}", f"{d.get('radius',.4)}/{d.get('width',1)}×{d.get('depth',1)}/{d.get('garrison',0)}") for d in rules['entities'] if d.get('kind',0) != 2])
table('战斗', ['ID', '基础/近战/攻城', '射程/最小射程', '周期/准备', '弹速/溅射半径', '移速/视野', '近/远/攻城抗性', '标签/加成'],
      [(d['id'], f"{d.get('damage',0)}/{d.get('meleeDamage',0)}/{d.get('siegeDamage',0)}", f"{d.get('range',1)}/{d.get('minRange',0)}", f"{d.get('period',2)}/{d.get('windup',.35)}", f"{d.get('projectileSpeed',30)}/{d.get('splash',0)}", f"{d.get('speed',4)}/{d.get('vision',18)}", f"{d.get('meleeResist',0)}/{d.get('rangedResist',0)}/{d.get('siegeResist',0)}", f"{d.get('tags',0)} / " + ', '.join(f"{b['tags']}×{b['multiplier']}" for b in d.get('bonuses',[]))) for d in rules['entities'] if d.get('damage',0) > 0])
table('实体经验 P 参数', ['ID', '完成建造 XP', '付费训练每人 XP', '被敌方击杀 XP'],
      [(d['id'], d.get('buildXp',0), d.get('trainXp',0), d.get('killXp',0)) for d in rules['entities'] if d.get('kind',0) != 2])
lines.extend(['', '经验来自实际完成与死亡事件。付费训练成功出生后按人数发放，出口阻塞期间不发；取消或生产建筑被毁不发。住房赠村民、船运、时代增援及开局单位不另发训练经验。三项参数独立，均为项目过渡数值；详情见 Reference-XP-041.md。'])
lines.extend(['', '标签位值：Infantry=1，HeavyInfantry=2，LightInfantry=4，Cavalry=8，HeavyCavalry=16，LightCavalry=32，Artillery=64，Villager=128，Building=256，Explorer=512，Guardian=1024。组合为按位或。当前命中多个加成取最大倍率；伤害乘以 1−对应抗性。范围次目标按该目标类型重算基础/攻城值与加成后乘0.5。炮兵部署2秒。以上是当前主动简化，未宣称原作等价。'])
table('驻扎火力 P 参数（社区二级参考，待指定原作版本实测）', ['建筑', '总容量', '贡献火力上限', '贡献者标签', '每人基础攻击', '攻击周期'], [(d['id'], d.get('garrison',0), d.get('garrisonAttackLimit',0), d.get('garrisonAttackTags',128), d.get('damage',0), d.get('period',2)) for d in rules['entities'] if d.get('garrisonAttackLimit',0)>0])
lines.extend(['', '火力只统计存活、本方、驻扎双向关系有效且标签匹配的单位；最多计入配置上限。空城不进行普通陆战射击。发射时固定弹丸伤害，驻扎变化不重置冷却。非村民可庇护但不贡献火力是本项目简化；没有实现特殊文明炮击或海上自动攻击。'])
resources = ['None', 'Food', 'Wood', 'Coin']
table('资源与持续设施', ['ID', '资源', '储量（持续建筑不耗尽）', '每村民每秒'], [(d['id'], resources[d['resource']], d.get('amount',0), d.get('gatherRate',0)) for d in rules['entities'] if d.get('resource',0) != 0])
table('时代', ['时代/名称', '费用 F/W/C', '时间', '经济奖励箱 F/W/C', '军事奖励'], [(str(a['age']) + ' / ' + a['name'], cost(a.get('cost',{})), a.get('time',0), cost(a.get('economicReward',{})), str(a.get('militaryCount',0)) + '×' + str(a.get('militaryUnit','—'))) for a in rules['ages']])
table('科技', ['ID', '时代/建筑/文明', '费用/时间', '前置', '匹配单位/标签/资源', '效果'],
      [(t['id'], f"{t.get('age',1)}/{t.get('building','')}/{t.get('civilization','') or '共享'}", cost(t.get('cost',{})) + '/' + str(t.get('time',30)), t.get('requires','') or '—', ','.join(t.get('units',[])) + f" / {t.get('tags',0)} / {resources[t.get('resource',0)]}", ', '.join(f"{k}={t[k]}" for k in ('attack','health','gather','speed','bankLimit','bankIncome') if t.get(k,0) != 0)) for t in rules['techs']])
lines.extend(['', '科技在已完成集合中去重；按单位 ID 和标签匹配，从基础属性计算加成和，作用于现有、在训完成后及未来实体。现有生命按最大生命变化保持比例。银行上限加法，银行收入乘以1+加成和。单位升级前置逐行以此表为准。'])
table('全部卡牌', ['ID/名称', '时代/文明', '内容', '运输时间', '可重复'],
      [(c['id'] + '/' + c['name'], f"{c.get('age',1)}/{c.get('civilization','') or '共享'}", f"箱 {cost(c.get('crates',{}))}; {c.get('count',0)}×{c.get('unit','')}; 科技 {c.get('tech','')}", c.get('travel',35), c.get('repeatable',False)) for c in rules['cards']])
lines.extend(['', '卡组合法范围1–25张且无重复，文明与时代由统一模拟检查。经验门槛=initialXp+xpIncrease×已产生机会数；采集经验0.1/资源是未核实的项目规则。运输串行；资源箱必须采集、单位交付受人口/单位上限/出口限制、科技效果幂等。接收失败保持等待，不凭空退款或补发。', ''])
landmarks=rules.get('landmarks',{})
table('探索与贸易 P 参数', ['参数', '值'], [(key, json.dumps(value, ensure_ascii=False)) for key,value in landmarks.items() if key != 'treasures'])
table('地图宝藏 P 配置', ['实体','位置','守卫数','资源箱 F/W/C','XP'], [(t['entity'],t['position'],t['guards'],cost(t['crates']),t['xp']) for t in landmarks.get('treasures',[])])
lines.extend(['', 'Cargo Ledgers 的特殊效果：解锁资源交付，并使用上表 tradeUpgradeMultiplier 乘每次商队交付。守卫与商队规则、主动简化和状态见 Landmarks-Implementation.md。'])
target = project / f'Docs/Implemented-Numbers-{release}.md' 
target.write_text('\n'.join(lines))
print(f'Exported {target.name}: {len(rules["entities"])} entities, {len(rules["techs"])} techs, {len(rules["cards"])} cards.')
