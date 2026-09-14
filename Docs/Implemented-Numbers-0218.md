# Frontier Reckoning 0.2.18 实际数值表

由 `Tools/export-numbers.py` 直接读取 `Assets/Resources/Rules.json` 生成。以下均为 P 项目规则；没有声称是 AoE3:DE 已核实精确数值。运行与体验证据另见 Iteration-0218.md。

规则 SHA-256：`8b68eb3f61cc9f042e34adc9bcfc86d24f70fdc8be223e5f004fee874a69bfd3`。

费用次序 F/W/C；时间为模拟秒，距离为世界米。文明覆盖值在第一张表明确列出。

## 全局

| 参数 | 配置值 |
| --- | --- |
| deckCapacity | 25 |
| populationLimit | 200 |
| initialXp | 150 |
| xpIncrease | 75 |
| constructionRefund | 0.75 |
| entities 数量 | 34 |
| techs 数量 | 37 |
| cards 数量 | 37 |
| ages 数量 | 5 |
| civs 数量 | 2 |

## 文明

| ID | 起始 F/W/C | 住房费用 | 住房赠村民 | 村民费用 | 村民上限 | 银行上限/每秒币 |
| --- | --- | --- | --- | --- | --- | --- |
| coast | 300/300/100 | 0/135/0 | True | 100/0/0 | 99 | 0/0 |
| river | 100/300/400 | 0/100/0 | False | 0/0/100 | 50 | 4/2.5 |

## 实体生产与建筑

| ID / 名称 | F/W/C | 时代/时间 | HP | 人口/上限供给 | 生产者/文明 | 半径/占地/驻扎 |
| --- | --- | --- | --- | --- | --- | --- |
| villager / Settler | 100/0/0 | 1/20 | 150 | 1/0 | town/共享 | 0.4/1×1/0 |
| explorer / Pathfinder | 0/0/0 | 1/20 | 450 | 0/0 | /共享 | 0.4/1×1/0 |
| pike / Pikeman | 60/40/0 | 2/22 | 180 | 1/0 | barracks/共享 | 0.4/1×1/0 |
| musketeer / Musketeer | 75/0/25 | 2/26 | 200 | 1/0 | barracks/共享 | 0.4/1×1/0 |
| skirmisher / Skirmisher | 60/0/60 | 3/28 | 140 | 1/0 | barracks/共享 | 0.4/1×1/0 |
| hussar / Hussar | 120/0/80 | 2/35 | 380 | 2/0 | stable/共享 | 0.65/1×1/0 |
| dragoon / Dragoon | 90/0/90 | 3/32 | 220 | 2/0 | stable/共享 | 0.65/1×1/0 |
| cannon / Field Cannon | 0/100/250 | 3/45 | 220 | 4/0 | foundry/共享 | 0.75/1×1/0 |
| mortar / Mortar | 0/100/300 | 4/50 | 180 | 4/0 | foundry/共享 | 0.75/1×1/0 |
| town / Town Center | 0/500/0 | 1/100 | 3500 | 0/20 | /共享 | 0.4/5×5/50 |
| house / Manor | 0/135/0 | 1/25 | 900 | 0/10 | /共享 | 0.4/3×3/0 |
| barracks / Barracks | 0/200/0 | 2/45 | 1800 | 0/0 | /共享 | 0.4/3×3/0 |
| stable / Stable | 0/200/0 | 2/45 | 1800 | 0/0 | /共享 | 0.4/3×3/0 |
| foundry / Foundry | 0/300/0 | 3/55 | 1600 | 0/0 | /共享 | 0.4/3×3/0 |
| market / Market | 0/100/0 | 1/30 | 1000 | 0/0 | /共享 | 0.4/3×3/0 |
| tower / Watchtower | 0/200/0 | 2/40 | 1800 | 0/0 | /共享 | 0.4/3×3/5 |
| farm / Farm | 0/300/0 | 2/50 | 1200 | 0/0 | /共享 | 0.4/3×3/0 |
| bank / Counting House | 350/350/0 | 2/70 | 1800 | 0/0 | /river | 0.4/3×3/0 |
| longbow / Longbow Guard | 60/45/0 | 2/28 | 100 | 1/0 | barracks/coast | 0.4/1×1/0 |
| carabineer / River Carabineer | 40/0/70 | 3/28 | 145 | 1/0 | stable/river | 0.65/1×1/0 |
| estate / Coin Estate | 0/500/0 | 3/50 | 1200 | 0/0 | /共享 | 0.4/3×3/0 |
| trade_post / Trading Post | 0/200/0 | 1/60 | 1600 | 0/0 | —/共享 | 2/4×4/0 |
| guardian / Camp Guardian | 0/0/0 | 1/20 | 85 | 0/0 | —/共享 | 0.4/1×1/0 |

## 战斗

| ID | 基础/近战/攻城 | 射程/最小射程 | 周期/准备 | 弹速/溅射半径 | 移速/视野 | 近/远/攻城抗性 | 标签/加成 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| villager | 5/5/5 | 1/0 | 2/0.35 | 30/0 | 4/18 | 0/0/0 | 128 /  |
| explorer | 14/14/12 | 1.5/0 | 2/0.35 | 0/0 | 4.5/24 | 0/0/0 | 512 /  |
| pike | 18/18/10 | 1/0 | 2/0.35 | 0/0 | 4/18 | 0/0/0 | 3 / 8×3 |
| musketeer | 24/12/10 | 12/0 | 3/0.35 | 30/0 | 4/18 | 0/0/0 | 3 /  |
| skirmisher | 20/0/10 | 16/0 | 3/0.35 | 30/0 | 4/18 | 0/0/0 | 5 / 2×2 |
| hussar | 35/35/16 | 1/0 | 2/0.35 | 0/0 | 6/18 | 0/0/0 | 24 /  |
| dragoon | 28/0/10 | 12/0 | 2.5/0.35 | 30/0 | 5/18 | 0/0/0 | 40 / 16×3 |
| cannon | 70/0/130 | 20/4 | 5/0.35 | 25/3 | 2.5/18 | 0/0/0 | 64 / 1×2, 3×2 |
| mortar | 90/0/350 | 24/8 | 7/0.35 | 25/3 | 2/18 | 0/0/0 | 64 /  |
| town | 9/0/0 | 16/0 | 3/0.35 | 25/0 | 0/18 | 0/0/0 | 256 /  |
| tower | 25/0/0 | 20/0 | 2/0.35 | 25/0 | 0/18 | 0/0/0 | 256 /  |
| longbow | 15/0/10 | 21/0 | 1.6/0.45 | 22/0 | 4.1/18 | 0/0/0 | 5 / 2×2 |
| carabineer | 15/0/10 | 12/0 | 2.5/0.35 | 30/0 | 7.0/18 | 0/0/0 | 40 / 16×3 |
| guardian | 8/8/0 | 1.3/0 | 2.2/0.35 | 0/0 | 3/9 | 0/0/0 | 1025 /  |

## 实体经验 P 参数

| ID | 完成建造 XP | 付费训练每人 XP | 被敌方击杀 XP |
| --- | --- | --- | --- |
| villager | 0 | 5 | 10 |
| explorer | 0 | 0 | 20 |
| pike | 0 | 5 | 10 |
| musketeer | 0 | 5 | 10 |
| skirmisher | 0 | 6 | 12 |
| hussar | 0 | 10 | 20 |
| dragoon | 0 | 9 | 18 |
| cannon | 0 | 18 | 35 |
| mortar | 0 | 20 | 40 |
| town | 75 | 0 | 75 |
| house | 20 | 0 | 20 |
| barracks | 30 | 0 | 30 |
| stable | 30 | 0 | 30 |
| foundry | 45 | 0 | 45 |
| market | 15 | 0 | 15 |
| tower | 30 | 0 | 30 |
| farm | 45 | 0 | 45 |
| bank | 30 | 0 | 30 |
| longbow | 0 | 6 | 11 |
| carabineer | 0 | 6 | 11 |
| estate | 45 | 0 | 45 |
| trade_post | 40 | 0 | 40 |
| guardian | 0 | 0 | 8 |

经验来自实际完成与死亡事件。付费训练成功出生后按人数发放，出口阻塞期间不发；取消或生产建筑被毁不发。住房赠村民、船运、时代增援及开局单位不另发训练经验。三项参数独立，均为项目过渡数值；详情见 Reference-XP-041.md。

标签位值：Infantry=1，HeavyInfantry=2，LightInfantry=4，Cavalry=8，HeavyCavalry=16，LightCavalry=32，Artillery=64，Villager=128，Building=256，Explorer=512，Guardian=1024。组合为按位或。当前命中多个加成取最大倍率；伤害乘以 1−对应抗性。范围次目标按该目标类型重算基础/攻城值与加成后乘0.5。炮兵部署2秒。以上是当前主动简化，未宣称原作等价。

## 驻扎火力 P 参数（社区二级参考，待指定原作版本实测）

| 建筑 | 总容量 | 贡献火力上限 | 贡献者标签 | 每人基础攻击 | 攻击周期 |
| --- | --- | --- | --- | --- | --- |
| town | 50 | 10 | 128 | 9 | 3 |

火力只统计存活、本方、驻扎双向关系有效且标签匹配的单位；最多计入配置上限。空城不进行普通陆战射击。发射时固定弹丸伤害，驻扎变化不重置冷却。非村民可庇护但不贡献火力是本项目简化；没有实现特殊文明炮击或海上自动攻击。

## 资源与持续设施

| ID | 资源 | 储量（持续建筑不耗尽） | 每村民每秒 |
| --- | --- | --- | --- |
| farm | Food | -1 | 0.5 |
| berry | Food | 1000 | 0.67 |
| deer | Food | 1400 | 0.84 |
| tree | Wood | 1500 | 0.5 |
| mine | Coin | 1200 | 0.6 |
| crate_food | Food | 300 | 6 |
| crate_wood | Wood | 300 | 6 |
| crate_coin | Coin | 300 | 6 |
| estate | Coin | -1 | 0.45 |

## 时代

| 时代/名称 | 费用 F/W/C | 时间 | 经济奖励箱 F/W/C | 军事奖励 |
| --- | --- | --- | --- | --- |
| 1 / Exploration | 0/0/0 | 0 | 0/0/0 | 0×— |
| 2 / Commerce | 800/0/0 | 90 | 200/0/0 | 1×pike |
| 3 / Fortress | 1200/0/1000 | 110 | 0/200/0 | 2×musketeer |
| 4 / Industrial | 2000/0/1200 | 130 | 0/600/400 | 2×cannon |
| 5 / Imperial | 4000/0/4000 | 160 | 1000/1000/1000 | 2×mortar |

## 科技

| ID | 时代/建筑/文明 | 费用/时间 | 前置 | 匹配单位/标签/资源 | 效果 |
| --- | --- | --- | --- | --- | --- |
| survival | 1/market/共享 | 0/75/0/30 | — |  / 0 / Food | gather=0.1 |
| steel | 2/market/共享 | 0/125/75/40 | — |  / 0 / Coin | gather=0.15 |
| masonry | 2/market/共享 | 0/150/0/45 | — |  / 256 / None | health=0.1 |
| drill | 3/barracks/共享 | 0/200/100/50 | — |  / 9 / None | speed=0.1 |
| veteran_pike | 3/barracks/共享 | 100/0/100/40 | — | pike / 0 / None | attack=0.2, health=0.2 |
| guard_pike | 4/barracks/共享 | 300/0/300/55 | veteran_pike | pike / 0 / None | attack=0.3, health=0.3 |
| imperial_pike | 5/barracks/共享 | 600/0/600/70 | guard_pike | pike / 0 / None | attack=0.5, health=0.5 |
| veteran_musketeer | 3/barracks/共享 | 100/0/100/40 | — | musketeer / 0 / None | attack=0.2, health=0.2 |
| guard_musketeer | 4/barracks/共享 | 300/0/300/55 | veteran_musketeer | musketeer / 0 / None | attack=0.3, health=0.3 |
| imperial_musketeer | 5/barracks/共享 | 600/0/600/70 | guard_musketeer | musketeer / 0 / None | attack=0.5, health=0.5 |
| veteran_skirmisher | 3/barracks/共享 | 100/0/100/40 | — | skirmisher / 0 / None | attack=0.2, health=0.2 |
| guard_skirmisher | 4/barracks/共享 | 300/0/300/55 | veteran_skirmisher | skirmisher / 0 / None | attack=0.3, health=0.3 |
| imperial_skirmisher | 5/barracks/共享 | 600/0/600/70 | guard_skirmisher | skirmisher / 0 / None | attack=0.5, health=0.5 |
| veteran_hussar | 3/stable/共享 | 100/0/100/40 | — | hussar / 0 / None | attack=0.2, health=0.2 |
| guard_hussar | 4/stable/共享 | 300/0/300/55 | veteran_hussar | hussar / 0 / None | attack=0.3, health=0.3 |
| imperial_hussar | 5/stable/共享 | 600/0/600/70 | guard_hussar | hussar / 0 / None | attack=0.5, health=0.5 |
| veteran_dragoon | 3/stable/共享 | 100/0/100/40 | — | dragoon / 0 / None | attack=0.2, health=0.2 |
| guard_dragoon | 4/stable/共享 | 300/0/300/55 | veteran_dragoon | dragoon / 0 / None | attack=0.3, health=0.3 |
| imperial_dragoon | 5/stable/共享 | 600/0/600/70 | guard_dragoon | dragoon / 0 / None | attack=0.5, health=0.5 |
| veteran_cannon | 3/foundry/共享 | 100/0/100/40 | — | cannon / 0 / None | attack=0.2, health=0.2 |
| guard_cannon | 4/foundry/共享 | 300/0/300/55 | veteran_cannon | cannon / 0 / None | attack=0.3, health=0.3 |
| imperial_cannon | 5/foundry/共享 | 600/0/600/70 | guard_cannon | cannon / 0 / None | attack=0.5, health=0.5 |
| guard_mortar | 4/foundry/共享 | 300/0/300/55 | — | mortar / 0 / None | attack=0.3, health=0.3 |
| imperial_mortar | 5/foundry/共享 | 600/0/600/70 | guard_mortar | mortar / 0 / None | attack=0.5, health=0.5 |
| veteran_longbow | 3/barracks/coast | 100/0/100/40 | — | longbow / 0 / None | attack=0.2, health=0.2 |
| guard_longbow | 4/barracks/coast | 300/0/300/55 | veteran_longbow | longbow / 0 / None | attack=0.3, health=0.3 |
| imperial_longbow | 5/barracks/coast | 600/0/600/70 | guard_longbow | longbow / 0 / None | attack=0.5, health=0.5 |
| veteran_carabineer | 3/stable/river | 100/0/100/40 | — | carabineer / 0 / None | attack=0.2, health=0.2 |
| guard_carabineer | 4/stable/river | 300/0/300/55 | veteran_carabineer | carabineer / 0 / None | attack=0.3, health=0.3 |
| imperial_carabineer | 5/stable/river | 600/0/600/70 | guard_carabineer | carabineer / 0 / None | attack=0.5, health=0.5 |
| sawmills | 2/market/共享 | 100/0/50/50 | — |  / 128 / Wood | gather=0.15 |
| crop_rotation | 3/market/共享 | 0/200/150/50 | survival |  / 128 / Food | gather=0.2 |
| deep_mining | 4/market/共享 | 300/300/0/50 | steel |  / 128 / Coin | gather=0.25 |
| steam_workshops | 5/market/共享 | 800/800/800/50 | crop_rotation |  / 128 / None | gather=0.3 |
| bank_charter | 2/market/river | 200/200/0/50 | — |  / 0 / None | bankLimit=1 |
| bank_ledgers | 4/market/river | 0/300/300/60 | bank_charter |  / 0 / None | bankIncome=0.25 |
| cargo_ledgers | 2/trade_post/共享 | 0/200/150/45 | — |  / 0 / None |  |

科技在已完成集合中去重；按单位 ID 和标签匹配，从基础属性计算加成和，作用于现有、在训完成后及未来实体。现有生命按最大生命变化保持比例。银行上限加法，银行收入乘以1+加成和。单位升级前置逐行以此表为准。

## 全部卡牌

| ID/名称 | 时代/文明 | 内容 | 运输时间 | 可重复 |
| --- | --- | --- | --- | --- |
| food_crates/Field Rations | 1/coast | 箱 300/0/0; 1×; 科技  | 35 | True |
| wood_crates/Timber Shipment | 1/coast | 箱 0/300/0; 1×; 科技  | 35 | False |
| coin_crates/Minted Chest | 2/coast | 箱 0/0/300; 1×; 科技  | 35 | False |
| settlers/New Settlers | 1/coast | 箱 0/0/0; 2×villager; 科技  | 35 | False |
| pikes/Pike Company | 2/coast | 箱 0/0/0; 3×pike; 科技  | 35 | False |
| muskets/Musketeer Company | 2/coast | 箱 0/0/0; 3×musketeer; 科技  | 35 | False |
| field_tools/Improved Tools | 1/coast | 箱 0/0/0; 1×; 科技 survival | 35 | False |
| bankers/Bank Charter | 2/river | 箱 0/0/0; 0×; 科技 bank_charter | 35 | False |
| river_coin/River Coin | 1/river | 箱 0/0/250; 1×; 科技  | 35 | False |
| hussars/Mounted Guard | 2/共享 | 箱 0/0/0; 2×hussar; 科技  | 35 | False |
| skirmishers/Ranger Company | 3/共享 | 箱 0/0/0; 3×skirmisher; 科技  | 35 | False |
| dragoons/Dragoon Escort | 3/共享 | 箱 0/0/0; 2×dragoon; 科技  | 35 | False |
| cannon_support/Cannon Support | 3/共享 | 箱 0/0/0; 1×cannon; 科技  | 35 | False |
| estate/Stone Foundations | 3/共享 | 箱 0/0/0; 1×; 科技 masonry | 35 | False |
| food_supply_2/Food Convoy 2 | 2/共享 | 箱 600/0/0; 0×; 科技  | 35 | False |
| food_supply_3/Food Convoy 3 | 3/共享 | 箱 1000/0/0; 0×; 科技  | 35 | False |
| food_supply_4/Food Convoy 4 | 4/共享 | 箱 1400/0/0; 0×; 科技  | 35 | False |
| food_supply_5/Food Convoy 5 | 5/共享 | 箱 2000/0/0; 0×; 科技  | 35 | True |
| wood_supply_2/Wood Convoy 2 | 2/共享 | 箱 0/600/0; 0×; 科技  | 35 | False |
| wood_supply_3/Wood Convoy 3 | 3/共享 | 箱 0/1000/0; 0×; 科技  | 35 | False |
| wood_supply_4/Wood Convoy 4 | 4/共享 | 箱 0/1400/0; 0×; 科技  | 35 | False |
| wood_supply_5/Wood Convoy 5 | 5/共享 | 箱 0/2000/0; 0×; 科技  | 35 | True |
| coin_supply_2/Coin Convoy 2 | 2/共享 | 箱 0/0/600; 0×; 科技  | 35 | False |
| coin_supply_3/Coin Convoy 3 | 3/共享 | 箱 0/0/1000; 0×; 科技  | 35 | False |
| coin_supply_4/Coin Convoy 4 | 4/共享 | 箱 0/0/1400; 0×; 科技  | 35 | False |
| coin_supply_5/Coin Convoy 5 | 5/共享 | 箱 0/0/2000; 0×; 科技  | 35 | True |
| reserve_pike/Pikeman Reserves | 3/共享 | 箱 0/0/0; 6×pike; 科技  | 35 | False |
| reserve_musketeer/Musketeer Reserves | 3/共享 | 箱 0/0/0; 5×musketeer; 科技  | 35 | False |
| reserve_hussar/Hussar Reserves | 3/共享 | 箱 0/0/0; 4×hussar; 科技  | 35 | False |
| reserve_skirmisher/Skirmisher Reserves | 4/共享 | 箱 0/0/0; 6×skirmisher; 科技  | 35 | False |
| reserve_dragoon/Dragoon Reserves | 4/共享 | 箱 0/0/0; 4×dragoon; 科技  | 35 | False |
| reserve_cannon/Field Cannon Reserves | 4/共享 | 箱 0/0/0; 2×cannon; 科技  | 35 | False |
| reserve_mortar/Mortar Reserves | 5/共享 | 箱 0/0/0; 2×mortar; 科技  | 35 | True |
| reserve_longbow/Longbow Guard Reserves | 3/coast | 箱 0/0/0; 6×longbow; 科技  | 35 | False |
| reserve_carabineer/River Carabineer Reserves | 3/river | 箱 0/0/0; 6×carabineer; 科技  | 35 | False |
| reserve_villager/Settler Reserves | 2/共享 | 箱 0/0/0; 3×villager; 科技  | 35 | False |
| river_ledgers/Ledger Dispatch | 4/river | 箱 0/0/0; 0×; 科技 bank_ledgers | 35 | False |

卡组合法范围1–25张且无重复，文明与时代由统一模拟检查。经验门槛=initialXp+xpIncrease×已产生机会数；采集经验0.1/资源是未核实的项目规则。运输串行；资源箱必须采集、单位交付受人口/单位上限/出口限制、科技效果幂等。接收失败保持等待，不凭空退款或补发。


## 探索与贸易 P 参数

| 参数 | 值 |
| --- | --- |
| caravanSpeed | 4 |
| tradeXp | 40 |
| tradeUpgradeMultiplier | 1.25 |
| claimTime | 3 |
| guardLeash | 10 |
| tradeResources | {"food": 60, "wood": 50, "coin": 40} |
| route | [{"x": 18, "y": 76}, {"x": 44, "y": 76}, {"x": 64, "y": 64}, {"x": 84, "y": 52}, {"x": 110, "y": 52}] |
| sockets | [{"position": {"x": 44, "y": 81}, "waypoint": 1}, {"position": {"x": 64, "y": 67}, "waypoint": 2}, {"position": {"x": 84, "y": 47}, "waypoint": 3}] |

## 地图宝藏 P 配置

| 实体 | 位置 | 守卫数 | 资源箱 F/W/C | XP |
| --- | --- | --- | --- | --- |
| treasure_wood | {'x': 40, 'y': 30} | 0 | 0/75/0 | 0 |
| treasure_xp | {'x': 47, 'y': 40} | 2 | 0/0/0 | 100 |
| treasure_coin | {'x': 38, 'y': 63} | 2 | 0/0/150 | 0 |
| treasure_wood | {'x': 88, 'y': 98} | 0 | 0/75/0 | 0 |
| treasure_xp | {'x': 81, 'y': 88} | 2 | 0/0/0 | 100 |
| treasure_coin | {'x': 90, 'y': 65} | 2 | 0/0/150 | 0 |

Cargo Ledgers 的特殊效果：解锁资源交付，并使用上表 tradeUpgradeMultiplier 乘每次商队交付。守卫与商队规则、主动简化和状态见 Landmarks-Implementation.md。