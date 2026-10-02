# B9：974.509094 秒自然败局，付费炮兵出生与建筑炮击仍缺

原生 macOS 客户端 `0.2.31`，GUID `128a731d440e4b7a94abfcc74b88fa97`，对局 `7f161abd4f994a1aaf5be762ba4a23f5`。菜单图实际显示 Coast Compact 镜像、Easy、时代上限 III、seed 314159、Military 25/25；最终报告为普通 1×、autoplay=false。画面显示 **DEFEAT／16m 14s**，完整事件确认世界 **974.5090942382812 秒**自然消灭结束，winner=1，无 Resign，败方存活单位与建筑为 0。

[严格玩家审核](summary.json)状态保持 **failed**，退出码 1；17 项检查中只有 `requiredPlayerCoverage` 未通过，精确缺项是 `paidCannon` 和 `cannonEnemyBuildingDamage`。身份、正常开局、原始事件数量、支付与生产完成链、时代和自然终局检查均成立。没有为这次自然败局放宽验收要求，也不标完整 B 通过。

已完成的真实生产包括 **16 名付费工人、5 Pike、5 Longbow**，3 Manor、1 Barracks、1 Foundry。时代 II 在 318.510223 秒完成，III 在 766.059998 秒完成；5 次船运均到达，包括 Cannon Support。采集与两次宝藏取得均有事件；船运炮不能替代付费炮兵出生。

## 已付款排队的炮为何不计出生

[生产链审核](cannon-production-audit.json)保留原始事件行号和完整记录：

| 事件 | 原 events 行 | 世界秒 |
|---|---:|---:|
| Foundry 255 出生 | 29052 | 797.052429 |
| 船运 Field Cannon 257 出生／Cannon Support 到达 | 30310／30311 | 818.147278 |
| Foundry 完工 | 32328 | 859.437195 |
| 付费 100 W、250 C／`cannon ×1` 入队 | 32805／32806 | 871.034363 |
| Foundry 255 被毁 | 34702 | 910.124817 |

付费队列至 Foundry 死亡仅 **39.090454 秒**，目录规定训练时间为 45 秒；严格支付—出生—完成链中付费炮兵为 0。原始账本没有满足炮击敌建筑的记录。不能把排队付款或先前船运炮写成已完成付费训练。

## 归档与独立性能结果

共保存五组原始 F9 JSON/PNG，尺寸、PNG SHA、版本／GUID、请求／实时／HUD 对局 ID 和时间配对全部通过：[capture-audit.json](capture-audit.json)。菜单与终局图还分别做了只读视觉检查。

- 菜单 `Frontier-20261002-163158-239-f18180-c256dd02ced94ed199c67fc4834b6bf7`。
- 开局 `Frontier-20261002-163159-690-f18261-5f54c9a3b0bc40ddbea027cfc3fba805`，世界 1.300000 秒，明确 paused=true。
- II 阶段 `Frontier-20261002-164009-628-f47396-b0489a2a25734410baff888db2e77dd4`，世界 352.612305 秒。
- III 阶段 `Frontier-20261002-214232-905-f511035-afaa85a54e0046b79ba572e1e08935a0`，世界 769.459167 秒。
- 自然终局 `Frontier-20261002-215114-405-f541122-d6b51be5cdff4bb2a8c977202457cd42`，世界 974.509094 秒。

卡组仍是已查看菜单图的外部声明，与原始报告／事件配对；原生 JSON 没有卡组配置字段。截图不能单独区分结果文案中的消灭与投降，完整事件审核确认了自然消灭。

同局 [F8 最终配对审核](../../Performance/normal-b9-final-001/README.md)独立 **passed**：明确要求单窗有效至少 300 秒，实测 **335.999332 秒、20,003 帧**，335 笔 memory 已与本局最终 Spawn/Death 前缀配对。性能通过不补齐本局缺失的玩家行为，也不表示 D 四档规模全部完成。

`Builds/NativeInput055-B9-preserved/Player-ended.log` 读时保存为 201,674 字节，SHA-256 `88ccbde89e30b2b576c6e45d5d702d0b85f401c24f71131ec2877b7227f70a49`。本目录保留其 B9 菜单 F9 开始至终局截图结束的原始第 230–2138 行，共 180,405 字节；另保存 1,186 行输入／UI／暂停／选择／捕获／报告标记，来源行号与 SHA 见 [input-trace-audit.json](input-trace-audit.json)。没有读取正在进行的 B10 日志或隐藏状态。

[冻结身份检查](frozen-identity-audit.json)确认 055 清单内全部 357 份源码和三项 compiled runtime SHA 均仍匹配原构建；没有重跑 Unity 测试或重新构建。[来源复制检查](source-copy-audit.json)、[调用记录](audit-invocation.json)及 [完整归档清单](archive-receipt.json)保留原始字节、helper、Rules、DeckLibrary 与单独的 055 来源／构建 receipt 快照。

本目录只归档已结束 B9。未改游戏源码、六份 canonical 文档、此前尝试或 F8 初审，未操作 GUI、提交或推送。
