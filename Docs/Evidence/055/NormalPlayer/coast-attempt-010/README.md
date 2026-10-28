# B10：Coast I–III 普通玩家严格审核通过

原生 macOS `0.2.31`、GUID `128a731d440e4b7a94abfcc74b88fa97`，对局 `f65f503a74b9415e89d0a875e9b0ce2d`。实际菜单为 Coast Compact 镜像、Easy、时代上限 III、seed 314159、**Economy 25/25**；最终报告为普通 1×、autoplay=false。本局在世界 **958.6129760742188 秒**自然败局，画面显示 **DEFEAT／15m 58s**。完整账本有唯一 MatchEnded、winner=1、无 Resign，败方存活单位及建筑全部为 0。

[严格审核](summary.json)退出码 **0**、状态 **passed**：17 项检查全部成立，`failedChecks=[]、coverageGaps=[]`。普通开局、真实支付与生产完成、时代 I–III、采集、船运、付费炮兵、炮击敌建筑和自然终局均有本局证据。该结果覆盖既定 Coast 严格路线，不替代 River I–V、D 规模夹具、其它硬件或统计平衡结论。

已完成 **3 名付费工人、9 Musketeer、5 Pike、2 门付费 Cannon**，以及 1 Manor、1 Barracks、1 Foundry。II 在 194.702682 秒、III 在 402.215332 秒完成。New Settlers、Coin Convoy 2、Hussar Reserves、Cannon Support 共 4 次船运实际到达；船运炮与两门付费炮分别记录，没有混计。

## 付费炮兵与建筑命中链

[炮兵审核](cannon-siege-audit.json)直接绑定原始 events 行号、完整支付／生产链、目标出生身份和实际命中：

| 环节 | 原 events 行 | 世界秒 |
|---|---:|---:|
| Foundry 179 支付 100 W、250 C／第一门炮入队 | 12765／12766 | 463.019043 |
| 付费炮 186 Spawn／Unit trained | 14359／14360 | 508.121796 |
| 炮 186 对敌 Stable 156 接受 `Attack ×1` | 16704 | 577.206116 |
| 第一、第二次 Siege 命中，各 130 | 16843／17065 | 580.505310／585.604065 |
| 后四次 Siege 命中，各 130 | 17289／17478／17646／17823 | 590.702820 至 605.999084 |
| 第二门付费炮 236 Spawn／Unit trained | 26136／26137 | 796.752502 |

炮 186 对敌 Stable 156 合计 **6 次 × 130 = 780** 实际伤害。目标身份来自 player=1 的 Stable 出生与完工记录；炮兵付款、出生和训练完成相互对应。

保存的炮兵 F9 在世界 512.921814 秒显示 Field Cannon、HP 220/220、Holding；出生的精确时间是上述 508.121796 秒，不能将截图时间当出生时刻。世界 583.804504 秒的 F9 显示炮兵 Reloading、Orders 1、HP 220/220。

目标 F9 的世界时间是 **585.204163 秒**，原图读到 **Stable HP 1850/1980**，当时只有第一笔 130 伤害。第二笔发生在 585.604065 秒，比该截图晚 **0.399902 秒**；前两笔合计 260 由后续账本证实，不能把该已保存截图写成 1720/1980 或已受两次命中。原始截图与事件记录均保留。

## 完整归档

保全日志的 B10 区间共出现 8 个原生完成标记；全部对应的 F9 JSON/PNG 已按原字节保存，并通过 PNG SHA、尺寸、版本／GUID、请求／实时／HUD 对局身份和时间配对：[capture-audit.json](capture-audit.json)。菜单、炮兵、Reloading、目标 Stable 和终局图分别做了只读视觉检查。

- `Frontier-20261002-215433-951-f552980-8527f5f55dfb46288a3e2ebb870425bb`：menu_economy，世界 0.000000 秒。
- `Frontier-20261002-215450-532-f553964-da8968b1aaa0400bbce1614bacbef7d2`：opening_paused，世界 0.700000 秒。
- `Frontier-20261002-221102-159-f611844-337e794840aa4dc79ded5923584f4925`：ii_ageiii_queued，世界 295.408813 秒。
- `Frontier-20261002-221409-821-f623024-5291247cb242495d8fa0af267a738ae5`：iii_completed，世界 403.215393 秒。
- `Frontier-20261002-222011-781-f644604-a0056f34501a4773bb0b49a79f03c9b0`：paid_cannon_on_field，世界 512.921814 秒。
- `Frontier-20261002-222236-089-f653150-207570e0c6af4786af085672f23af502`：cannon_reloading，世界 583.804504 秒。
- `Frontier-20261002-222256-090-f654333-2534f5f4c1c445eca406b0bb9b5c99e0`：enemy_stable_after_first_hit，世界 585.204163 秒。
- `Frontier-20261002-223939-011-f713946-53a287c25eeb42d39739acbfc890efad`：natural_end，世界 958.612976 秒。

菜单图是 Economy 25/25 的外部 UI 声明；原生 JSON 没有卡组配置字段。输入日志中切换按钮在点击前名为 `PRESET: MILITARY`，随后保存的菜单实际为 Economy，不把旧按钮标题误读成最终卡组。

保全来源 `Builds/NativeInput055-B10-preserved/Player-ended.log` 为 **380,968 字节**，SHA-256 `441389461115828dfa84d4f98228ddb1c3416817b9dca9fd66e6452aea42e9c3`。本目录保留原始第 **2170–3994 行**，从 B10 菜单卡组切换至终局截图结束，共 **175,239 字节**；另存 **1,110 行**输入／UI／暂停／选择／捕获／报告标记。[日志审核](input-trace-audit.json)记录精确来源行号与 SHA。没有读取当前活日志或 C 局数据。

完整 report 与 **33,001 条原始 events**、Rules、DeckLibrary、解析器及 055 来源／构建 receipt 快照均在本目录。[冻结身份检查](frozen-identity-audit.json)确认 357 份列明源码与三项 compiled runtime SHA 仍匹配原 055；未重跑 Unity 测试或重新构建。[调用记录](audit-invocation.json)、[来源复制审核](source-copy-audit.json)和[完整清单](archive-receipt.json)保留校验细节。

本局不重复采集或重审 F8；已通过的正常长窗口仍见 [B9 最终配对](../../Performance/normal-b9-final-001/README.md)。此次仅新增 B10 目录，未修改其它证据、源码或 canonical 文档，未操作 GUI、提交或推送。
