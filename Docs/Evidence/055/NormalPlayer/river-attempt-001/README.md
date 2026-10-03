# River native attempt 001 — failed, six required coverage gaps

本局为 `.31` / GUID `128a731d440e4b7a94abfcc74b88fa97` 的正常原生 River League 对 River League、Easy、时代上限 V、Economy 25/25 对局。对局 ID 为 `1498f3a84f374c0498900c25a6177872`。保存的正确菜单图是 `Frontier-20261002-224206-923-f722750-d09dfbbc70bc4a62a40860394c367688`；较早的菜单图仍为上限 III，保留为操作历史，未作为严格审计的配置依据。

**结论：失败，不是 C 通过。** [严格审核](summary.json)退出码 1：17 项检查中 16 项成立，仅 `requiredPlayerCoverage` 不成立。六个必需缺口是 `ageIV`、`ageV`、`paidTradePost`、`tradeXP`、`resourceMode`、`resourceTradeDelivery`。本局只能证明已经发生的局部覆盖，不替代完整 River I–V 路线。

实际 `match_finished` 报告记录自然败局，世界时间 **1486.584106 秒（24:46.584）**，winner=1，正常 1×，`autoplay=false`。原始账本共 **53,782 条事件**，只有一个 MatchEnded、没有 Resign；败方存活单位及建筑均为 0。之后的 `menu_exit` 报告保留同一终局，两份 events 文件字节完全相同。没有终局 F9 标记或图片，不补造截图，也不将主菜单画面写成终局图。详见 [报告配对](report-pair-audit.json)。

## 本局实际覆盖

II 于 **199.802994 秒**完成，III 于 **525.318787 秒**完成。付费生产完成：15 名工人、12 Musketeer、8 River Carabineer、2 Field Cannon、10 Pike、2 Hussar；付费建筑完成：1 Manor、1 Barracks、2 Counting House、1 Stable、1 Foundry、1 Watchtower。船运到达6次。付费单位与船运单位分开，未混计。

Bank 147 于253.906296秒完成，1481.485352秒被毁。Bank 235 于1031.095337秒完成，1076.484253秒被毁，仅存活约45.389秒。整局银行被动收入报告为3182.5C；“完成过两银行”不代表后续一直维持两银行。最终玩家仍为时代III，贸易收入全部为0。详见 [宝藏与银行审核](treasure-bank-audit.json)。

## Veteran Musketeer：同一旧兵前后与新兵

[科技审核](technology-audit.json)保留付款、研究完成、出生账本行号，以及每张F9前的原生选中实体日志：

| 对照 | 实体 | 世界秒 | 原始PNG可见数值 |
|---|---:|---:|---|
| 研究前旧兵 | 215 | 685.679626 | HP 200/200，ATK 24 |
| 支付 Veteran Musketeer | Barracks 148 | 688.378967 | 100F100C，账本付款 |
| 科技实际完成 | Barracks 148 | 728.469177 | Technology + ResearchComplete |
| 研究后同一旧兵 | 215 | 888.330139 | HP 205/240，ATK 29 |
| 研究后付费新兵出生 | 238 | 916.023376 | 890.029724秒已支付75F25C |
| 新兵F9 | 238 | 924.821228 | HP 240/240，ATK 29 |

HUD将攻击力显示为整数29；公开规则的24×1.2为28.8，原图本身未显示小数。旧兵已有战损，因此205是当时生命值，240才是科技后的生命上限。本对照只代表 Veteran Musketeer，不推论所有科技都已逐项实机验收。

较早的原始 Musketeer 154 在268.707秒也有截图，但628.993469秒已经死亡；它不是上述研究前后对照中的同一单位。其截图和死亡记录均保留。918.422791秒F9实际选中 Barracks 148，也不冒充新兵对照图。

## 两类宝藏与奖励领取的区别

- Abandoned Supplies 116：Pathfinder 8 于9.299999秒领取，生成木箱131；约75W已经实际采集并有 ResourceDepleted。
- Guarded Coin Cache 120：同一 Pathfinder 于294.808777秒领取，生成金币箱164。296.308868秒F9可见领取通知、金币箱和HP64/450的Pathfinder。**金币箱没有玩家采集事件，未确认150C到账；本局记录为领取宝藏但未收取金币奖励。**

这两次实际领取提供两类宝藏证据，不能写成“XP宝藏已领取”，也不能把箱子生成等同于资源入账。完整 Gather 行及未完成状态见 [treasure-bank-audit.json](treasure-bank-audit.json)。

## 付费炮兵实际命中 MARKET

[炮击审核](cannon-siege-audit.json)保留两门炮的独立支付、串行队列、出生与训练完成：炮218于698.876404秒出生，炮222于743.965393秒出生。炮组在966.211121秒向可见敌 Market 152接受 Attack ×2。

| 原events行 | 世界秒 | 攻击者 | 实际伤害 |
|---:|---:|---:|---:|
| 30034 | 969.210388 | 222 | Siege 130 |
| 30044 | 969.410339 | 218 | Siege 130 |
| 30197 | 974.309143 | 222 | Siege 130 |
| 30205 | 974.509094 | 218 | Siege 130 |
| 30379 | 979.607849 | 218 | Siege 130 |

合计 **5笔、650伤害**。974.908997秒目标F9显示 **MARKET HP580/1100**，对应当时前4笔、520伤害；第5笔发生在截图之后。972.809509秒F9显示选中两门Field Cannon、Reloading、Orders1。原始账本没有Market152死亡，不声称该建筑已被摧毁。

## 归档与身份边界

本目录保留两组完整最终 report/events、全部17组菜单/游戏F9 JSON与PNG、全量原生过程日志，以及规则、卡组排序源码、严格审计器和原055来源/构建receipt快照。

全量日志最先保存在 `Builds/NativeInput055-C1-preserved/Player-ended.log`，**697,588字节**，SHA-256 `c582284962864998787e9863654957d09729d70de9e04ef72c6f3c462eaa0e27`；本目录另存完全相同的 [native-input-full-preserved.log](native-input-full-preserved.log)。日志含前序对局，C1精确摘录采用原始第 **4026–7262行**，从其文明配置输入至本局menu_exit堆栈结束；另有1937行FRONTIER输入/UI/暂停/捕获/报告标记，并记录其原始行号。[日志审核](input-trace-audit.json)提供边界及SHA。

所有17组F9的PNG SHA、尺寸、版本/GUID、请求/实时/HUD对局身份和世界时间均通过字节配对。只读视觉检查覆盖正确菜单、科技旧兵前后和新兵、Carabineer、宝藏、炮组与Market图；不把纯JSON检查声称为对全部图片逐一视觉验收。详情见 [capture-audit.json](capture-audit.json)。

[冻结身份检查](frozen-identity-audit.json)确认357份列明源码和三项compiled runtime SHA仍匹配原055；没有启动客户端、重跑Unity测试或重建。来源复制逐项校验见 [source-copy-audit.json](source-copy-audit.json)，严格调用参数见 [audit-invocation.json](audit-invocation.json)，重要事件完整payload摘录见 [key-event-payloads.json](key-event-payloads.json)。完整文件SHA清单见 [archive-receipt.json](archive-receipt.json)。

此归档保留原始失败结果；未改变模拟、验收条件或其它证据，未操作GUI、提交或推送，也未将本局最终报告的整场帧均值视为独立F8性能窗口。
