# 普通规则 AI 矩阵 — 0.2.31

12局均自然消灭结束，2400模拟秒上限内没有超时、异常或主动认输。独立事件审计全部身份/经济/生产/船运/胜负检查通过；完整报告见[summary.json](Evidence/055/AiMatrix/summary.json)。这是Editor无图形回归，不替代正常玩家操作、原生表现、声音或性能验收，也不代表统计平衡结论。

每种Easy/Standard/Hard难度各包含coast经济对river军备、完整文明与卡组交换后的反向对局、coast经济镜像、river军备镜像。地图为固定128×128手工陆图，seed314159；不同标签不能算不同地图。普通初始资源、费用、生产与AI信息入口保持正常规则。卡组从实际Catalog及DeckLibrary加载并验证，双方交换时文明、卡组和初始资源一起交换。

| 对局 | 胜者席位 | 模拟秒 | 双方最终时代 | 付费完成火炮 | 火炮伤害敌建筑事件 |
|---|---:|---:|---|---|---|
| easy-cross-forward | 0 | 1482.9 | IV / III | 8 / 3 | 157 / 7 |
| easy-cross-reverse | 0 | 2200.4 | V / IV | 21 / 11 | 48 / 5 |
| easy-coast-mirror | 0 | 1260.3 | IV / III | 6 / 4 | 129 / 0 |
| easy-river-mirror | 0 | 1556.1 | IV / III | 6 / 6 | 97 / 13 |
| standard-cross-forward | 0 | 990.1 | III / II | 1 / 0 | 33 / 0 |
| standard-cross-reverse | 1 | 1109.8 | II / IV | 0 / 4 | 0 / 79 |
| standard-coast-mirror | 1 | 2195.5 | IV / V | 10 / 17 | 19 / 242 |
| standard-river-mirror | 0 | 1452.5 | IV / III | 8 / 4 | 136 / 0 |
| hard-cross-forward | 0 | 1429.3 | IV / III | 7 / 4 | 131 / 10 |
| hard-cross-reverse | 1 | 1777.5 | IV / V | 4 / 10 | 0 / 131 |
| hard-coast-mirror | 0 | 2279.6 | V / IV | 18 / 18 | 196 / 3 |
| hard-river-mirror | 1 | 2140.6 | IV / V | 15 / 18 | 13 / 213 |

矩阵总执行墙钟121.23秒。每个自然结束检查败方存活单位与建筑为零，而非以第一座TC死亡代替结算。付费训练通过Payment→ProductionQueued→Spawn/Unit trained关联，船运不混入付费生产；火炮伤害同时核对实际炮兵出生身份与敌方建筑目标。账本浮点累计残差保留诊断值，不用任意宽容差制造平衡通过。

按文明/难度合并后，采集、付费工人/军队/建筑、III/IV、船运、付费火炮/敌建筑伤害、宝藏、贸易XP和资源收入均有实际事件。coast/Easy没有V、river/Standard没有V，原因是这些样本先自然结束；其余四组有V。这些false字段保留为覆盖缺口，不修改难度或强制延长对局来抹平。

完整源指纹`1b45f97f3d90c9b76388419492a83c5622ccb2cf3a365030044403ccd309c1c7`在测试、构建与矩阵前后相同。矩阵保存全部Assets/Packages/ProjectSettings/Tools哈希、编译程序集SHA/MVID/来源时间，以及214/214 EditMode和210/210 PlayMode当前测试回执身份。此回归的Unity版本为6000.6.3f1，应用0.2.31；它不是原生客户端的build GUID记录。

原始events/snapshots以gzip无损存档，压缩前后逐字节SHA复核记录在[compressed-logs.json](Evidence/055/AiMatrix/compressed-logs.json)，文件清单见[archive-receipt.json](Evidence/055/AiMatrix/archive-receipt.json)。原669,698,888字节日志压为44,814,727字节，不删事件、不抽样。

复算：`python3 Docs/Evidence/055/audit_ai_matrix.py Docs/Evidence/055/AiMatrix --output /tmp/frontier-ai-055-audit.json`。归档测试回执有同SHA路径回退，因此源码包不依赖本机Builds目录存在。
