# 045 难度改动独立审查

本审查只覆盖045本轮难度接入，并经根代理复核修订。before中存在SkirmishAI、MatchSimulation、Model、Rules、GameController、FrontierHud、NativeCapture及StrategyAudit等快照，前后差异保存在runtime-diff.patch。

当前 `AiDifficultyProfile` 只从 `RuleCatalog.Difficulty(id)` 拷贝 id/name/description/DecisionSeconds/ArmySeconds，并拒绝 NaN、Infinity 和小于 `MatchSimulation.StepSeconds` 的间隔。`SkirmishAI` 仍通过原有 `World.Entities`、`World.IsVisible`、`World.Issue`、`World.UnitCost`、`World.CanAfford` 和玩家资源/人口状态决策；本轮只把 `thinkAt` 重置值替换为 `Difficulty.DecisionSeconds`，把军队重评时间替换为 `Difficulty.ArmySeconds`。没有发现向 AI 发放资源、人口、视野、伤害、命中或免费 Spawn 的分支。

`MatchSimulation` 从 Rules 创建一个 profile，并把同一 profile 传给所有自动玩家；World 的固定 `StepSeconds=.1`、经济/战斗 Tick、Fog 和胜负路径不随难度改变。当前 Rules.json 的 Standard 实值为 `decisionSeconds=1.2`、`armySeconds=7`。与 before 的 diff 显示 SkirmishAI 只新增 profile 构造并把原有 1.2/7 常量替换为 profile 值；MatchSimulation 只新增 difficulty 参数、profile 快照和向 controller 传递。GameController 只传递菜单选择到 MatchSimulation；报告和 NativeCapture 记录难度及两个间隔。

本轮实际风险是时间间隔改变会改变合法决策发生时机：Hard 可能更频繁重发指令，需观察是否打断攻击准备、重复排队或重复支付；Easy 可能因重评过慢错过人口、住房或防守窗口。`MatchSimulation.AdvanceFrame` 仍限制每帧最多 10 步，过载时会累计到 2 秒；因此极端性能下墙钟决策间隔不等于模拟时间间隔，必须在日志中同时记录 StepCount/World.Time。

现有 `AiDifficultyTests`/`AiDifficultySetupTests` 能证明 profile 可加载、菜单选择可传入 Simulation、报告/截图元数据携带两个间隔；这些测试不能证明三档平衡、没有重复命令副作用，或原生玩家体验已通过。需要用相同 seed、文明、地图和初始资源对照三档，比较支付、Gather、人口、Spawn、Fog 查询和伤害事件；差异应只来自 AI 决策时间点及合法后果。

结论：当前代码路径支持“难度档案只调整 AI 两个重评间隔”的设计判断；前后代码差异与Rules原有数据均已核对。尚未有证据证明三档已平衡，也不能把此改动称为 AoE3:DE 精确难度实现。

根代理复核修正了早期审查误称部分before文件缺失的问题，实际文件存在且已纳入runtime-diff.patch。Rules除新增aiDifficulties外，原有全部数据逐项相等；Standard数值1.2/7已核实。
