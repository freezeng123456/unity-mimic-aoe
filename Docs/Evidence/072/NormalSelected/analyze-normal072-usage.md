# 072 正常局离线经济分析脚本

本次交付仅为 `analyze-normal072.py` 和本文。当前没有072 Matrix，不包含实际运行结果；脚本不启动Unity、不导入observer、不重新执行模拟、不访问网络。Formal run的完成情况与本脚本准备情况是两项证据；已中止的正式run不能计作通过。

## 何时运行

只能在16局正常观察全部退出、`runner-receipt.json` 有实际 `exitCode=0` / `finishedAtUtc`、源码before/after一致后运行。`manifest.json` 必须已关闭，且16局 `result.json` 与manifest逐项一致。真实路线可以incomplete；未关闭、观察exception、abort、来源改变会退出2且不生成统计结果。

矩阵路径由根代理实际执行后决定，以下 `/absolute/...` 为路径占位，不代表已有证据。`--rules` 必须指向该runner源码冻结中的真实 `Assets/Resources/Rules.json`，其原始文件SHA必须与source-before的记录相同。脚本不自动使用可能已经更新的Rules。

```sh
python3 -B /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.15.0/Candidate1/Diagnostics/analyze-normal072.py \
  --matrix /absolute/ClosedNormalRoutes072/matrix \
  --runner /absolute/ClosedNormalRoutes072 \
  --rules /absolute/Frozen072Source/Assets/Resources/Rules.json \
  --output /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.15.0/Candidate1/Diagnostics/normal072-economics
```

可选 `--baseline071 /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.14.0/Candidate1/NormalRoutes/independent-normal-routes-071.json`。这只读取已关闭的八局独立汇总，不读取1.6GB旧矩阵，不修改071材料。历史描述不构成因果比较。

输出两个**新文件**：`normal072-economics.json` 和 `.md`。只能直接写入本脚本所在072 Diagnostics；同名文件存在时拒绝覆盖，须换新basename。标准错误逐局报告进度，标准输出只打印成功路径、bytes与SHA。错误时退出2；只有完整成功才写结果。脚本不修改任何输入。

## 读取范围与来源

- runner的 `source-before.json`、`source-after.json`、`runner-receipt.json`，matrix的 `manifest.json`。
- 每局 `result.json`、`opening-world.json`、`events.jsonl`、`samples.jsonl`；另读取实际 `comparison-2400-world.json` 或 `early-terminal-world.json`。
- 指定的冻结Rules原文，以及可选071独立汇总。
- events/samples逐行读取和SHA计算，不将全部events载入内存。一次只保留一局的实体角色/支付/收入元数据与约30秒采样的投影；忽略samples中的AI simulation子树，不用其信息驱动AI。
- 不打开含完整历史ledger的 `final-world.json` 或 `final-simulation.json`，不读取账号、凭据或外部服务。

每个实际读取文件列出绝对路径、bytes、SHA、stat identity；打开时、读完时和最终输出前检查身份稳定。JSON拒绝重复属性、NaN和Infinity。该记录是未签名本地一致性材料，不是防作弊认证或历史付款真实性的外部证明。完整运行输入逐文件核验仍由 `independent-normal072.py` 负责；本脚本只核源码冻结映射和指定Rules内容，不重复完整源码审计。

## 输出字段

顶层包含 `schema=1`、`kind=descriptive_normal072_economics`、`status=analyzed_closed_actual_data`、`sourceFingerprint`、`unityRulesHash`、16个 `cases`、`summary`、`inputsRead`、`limits`；可选 `baseline071`。该status表示解析已关闭材料完成，不代表16条路线全部成功。

每局双方独立统计，P1不会复用P0的路线结果。记录真实终点、胜者、是否自然终局、是否到达2400边界及其实际固定步长时间。`observerPlayer0RouteReached` / `observerStatus` 原样标注为observer字段，本脚本没有完整独立路线认证。

每位玩家：

| 字段 | 含义 |
|---|---|
| `facilityFirsts`, `facilities` | Town/Post/Bank/Estate/Factory的首次真实资源付款、每座实体的付款交易、完工、死亡、首次正Wood/Coin；事件行号指向本局原events.jsonl。 |
| `cargoLedgers` | 研究付款、研究完成、实际Technology激活及相关卡片发送/到达各自列出。卡片激活不伪装成研究付款；付款不等于研究完成。 |
| `factoryEntitlements` | 工厂卡片真实发送/到达、wagon消耗后的FactoryFoundationStarted。Factory无normal资源Foundation付款，付款字段保持null，不能称免费资源设施。 |
| `townEnemyDamage`, `townEnemyLosses` | 只计真实敌方Damage与有敌方玩家击杀归属的Town死亡，核同刻致死Damage。Damage的target是受伤实体，Death的target是击杀玩家，不混用。 |
| `normalTownRecoveries` | 无存活Town时的真实normal Foundation付款，绑定同玩家同刻随后Spawn的Town实体，再分别记录完工/死亡及最近敌损链接；已付未完工保留incomplete。 |
| `lifeAccounting` | 开局存活、后来Spawn、实际UnitTrained/AutomaticWorkerProduced/FactoryArtilleryProduced、死亡、敌方归属死亡、革命WorkerConverted与终点存活。转换使用原ID，不是死亡或新产兵；主动移除与战死分开。 |
| `firstPositiveResourceIncome`, `firstPositiveOrdinaryLandWorkerIncome` | 各资源首次正收入账本；普通陆地Worker非crate采集另记，革命军收crate不当作平民经济恢复。 |
| `loggedIncomeTotalsByChannel` | Gather / BankIncome / FactoryIncome / 资源TradeDelivery / CaravanDelivered分项收入。XP、Refund、开局发放、钱包兑换不当作赚取资源。 |
| `incomeWindows` | 完整真实观察与实际comparison边界两窗口，包含账本收入最长间隔、替代渠道Wood/Coin最长间隔、稀疏累计收入最长平坦区间。 |
| `political` | V/革命/重建政府付款与实际AgeAdvanced/RevolutionCompleted/CivilianRebuilt分开；终点付费队列保留elapsed/duration，不许预测为已经完成。 |

生死统计按事件发生时的真实definition分类，并核角色守恒和终点worker/landSoldier数量与原sample一致。`workers`沿用observer的Villager标签定义；`landSoldiers`排除waterborne和Explorer；navalMilitary/explorers另列。死因无hostile credit不自动判为战死；明确UnitDisbanded另标注。

## 木材、收入gap和比较边界

`rawFiniteWoodInCurrentVision` **只在opening与实际comparison/early-terminal世界投影**计算。先按当前可见/自有筛选，再排除crate、waterborne、可持续资源建筑及已死/耗尽来源。由于samples没有资源实体remaining和完整visibility，本脚本不插值生成逐30秒可见木总量，不给出全图隐藏木总量或“全地图枯竭”的时刻。

该数字是离线观察者可由快照重建的原始可见量，未证明设施安全、工人合格、CanGather和路径可达，**不是**AiResourceContinuity072的 `VisibleWoodRemaining`，也不能赋予AI隐藏的全图情报。安全/可达决策合同应引用实际源码及对应测试，不能由该数字代替。

`loggedEarnedIncome` / `loggedAlternativeChannelIncome` 的longest字段是正收入**记录时间戳**之间的最大间隔，并标记opening/end截断。Gather/Bank/Factory会缓存并通常约每来源1秒刷账、切来源/移除/结束时也刷账，所以不能直接解释成每秒采集实际停止的精确时长。

`sampledCumulativeEarnedIncome` 用累计gathered+passive+真实trade的端点差，不受花费影响。observer samples的gathered+passive和result.income都不包含tradeIncome；本脚本额外合并真实资源TradeDelivery和CaravanDelivered，排除XP。

每段平坦区间列出原始端点和实际间距，正常约30秒；opening、真实2400/early-terminal投影与终点提供附加边界。出现正增量的一段不能证明一直在采集，平坦段前后两个正收入段内部的停止/恢复时刻也未知。不将稀疏样本报成连续逐帧动作验证。

072的完整上限3600秒、第二张Winding图和新调度源码与071不同。只有实际到达2400的局提供对应观察窗口；提前自然终局以真实时间截止，不补成2400。若附071旧汇总，只描述其1/8完整路线结果和逐局真实终点，不能称“同2400全部达成”“证明修复造成胜率改善”或完整《帝国时代3》平衡验收。

## 当前验证与后续

准备阶段只允许脚本语法、`--help`和内存中解析/统计边界检查；没有072 Matrix、没有真实经济统计结果。这些检查不等于运行游戏、全量解析真实矩阵或收入设施实机成功。矩阵真正关闭后，由根代理运行上述命令并独立复核产生的新JSON/Markdown。若真实输入触发拒绝或分类冲突，保留原材料，只修分析器并换新输出basename，不补资源、不扩大已声明观察时间、不改游戏原事件。
