# Frontier Reckoning 实际配置与开放范围

本文件对应当前 `Assets/Resources/Rules.json`、`Model.cs` 与 `Economy.cs`。所有数值均为本项目 P 参数，不是 AoE3:DE 精确数据。

## 全局参数

| 项目 | 当前值 |
|---|---:|
| 卡组容量 | 8 |
| 人口上限 | 200 |
| 初始船运门槛 | 150 XP |
| 后续门槛增量 | 75 XP |
| 施工取消退款 | 原始费用的 75% |
| 船运 | 默认 35 秒，每名玩家串行一项 |
| 初始库存 | 300 Food / 300 Wood / 100 Coin |
| 初始单位 | 每方 6 villager、1 explorer、1 town |

## 单位、建筑、资源

| ID | 费用 F/W/C | HP | 时间秒 | 人口/容量 | 时代 | 生产者或率 |
|---|---:|---:|---:|---:|---:|---|
| villager | 100/0/0 | 150 | 20 | 1/0 | I | town |
| explorer | 0/0/0 | 450 | 20 | 0/0 | I | 开局 |
| pike | 60/40/0 | 180 | 22 | 1/0 | II | barracks，batch 5 |
| musketeer | 75/0/25 | 200 | 26 | 1/0 | II | barracks，batch 5 |
| skirmisher | 60/0/60 | 140 | 28 | 1/0 | III | barracks，batch 5 |
| hussar | 120/0/80 | 380 | 35 | 2/0 | II | stable，batch 5 |
| dragoon | 90/0/90 | 220 | 32 | 2/0 | III | stable，batch 5 |
| cannon | 0/100/250 | 220 | 45 | 4/0 | III | foundry，batch 5 |
| mortar | 0/100/300 | 180 | 50 | 4/0 | IV | foundry，batch 5 |
| town | 0/500/0 | 3500 | 100 | 0/20 | I | 远程15/射程16/驻守10 |
| house | 0/135/0 | 900 | 25 | 0/10 | I | coast 完成后赠村民 |
| barracks | 0/200/0 | 1800 | 45 | 0/0 | II | 步兵 |
| stable | 0/200/0 | 1800 | 45 | 0/0 | II | 骑兵 |
| foundry | 0/300/0 | 1600 | 55 | 0/0 | III | 炮兵 |
| market | 0/100/0 | 1000 | 30 | 0/0 | I | 科技 |
| tower | 0/200/0 | 1800 | 40 | 0/0 | II | 远程25/射程20/驻守5 |
| farm | 0/300/0 | 1200 | 50 | 0/0 | II | 无限 Food，0.5/s |
| bank | 0/200/100 | 1800 | 55 | 0/0 | II | river，0.5 Coin/s，最多5 |

资源点为 berry 1000 Food、deer 1400 Food、tree 1500 Wood、mine 1200 Coin，基础率为 0.67、0.84、0.50、0.60/s。资源箱 `amount=300`，采集率 6/s，到达时生成实体而非直接入账。

## 时代、科技、卡牌与文明

I–V 均已配置：I 无费用，II 800 Food/90s，III 1200 Food+1000 Coin/110s，IV 2000 Food+1200 Coin/130s，V 4000 Food+4000 Coin/160s。`World.MaxAge=3`，本地菜单与对局只开放 I–III；IV/V 是配置预留。

科技：survival（Food +10%）、steel（Coin +15%）、masonry（建筑生命 +10%）、drill（步兵/骑兵速度 +10%），按已完成集合派生且重复应用不叠加。共 14 张原创卡，容量 8；`food_crates` 可重复，其余默认一次性。配置文明为 coast 与 river；本地菜单只开放 coast 镜像 1V1，river 尚未开放。

## 验证边界

已做 JSON 与跨引用静态检查；未启动 Unity，因此未声称导入、构建、视觉、音频或完整对局验收。
