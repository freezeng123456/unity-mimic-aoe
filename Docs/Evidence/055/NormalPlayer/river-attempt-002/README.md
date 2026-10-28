# River native attempt 002 — failed, ten required coverage gaps

本局为原生 `.31`、GUID `128a731d440e4b7a94abfcc74b88fa97`，玩家 River League、AI Coast Compact、Easy、上限V、Military 25/25、seed314159。实际对局ID为 `0b725a5116d2415f855f4443c4023e0e`。配置由保存的原生菜单PNG独立视觉检查，JSON不含卡组配置字段；卡组仍是外部UI声明，不伪称存在DeckConfigured事件。

**结果为失败，不是C通过。** [严格审核](summary.json)退出码1，17项检查中16项成立，只有 `requiredPlayerCoverage` 失败。十个必需缺口为：`paidCannon`、`cannonEnemyBuildingDamage`、`ageIV`、`ageV`、`specialUnit`、`paidTechnology`、`paidTradePost`、`tradeXP`、`resourceMode`、`resourceTradeDelivery`。

最终报告和 **49,623条原始事件**确认正常1×、`autoplay=false`、自然败局 **1081.682983秒（18:01.683）**，winner=1，唯一MatchEnded、没有Resign，败方所有单位及建筑均已被消灭。终局F9直接显示DEFEAT、18m1s、双方时代3/4。之后menu_exit与match_finished的events文件字节完全相同，详情见 [report-pair-audit.json](report-pair-audit.json)。

## 实际完成与没有完成的部分

玩家II于201.103073秒完成，III于767.659607秒完成。付费出生共16名Settler、15名Musketeer和5名Pikeman；付费完成2间Manor、1间Barracks、2间Counting House和1座Watchtower。付费工人和船运3名Settler分别记录，不能混计。

两个银行150/197分别在264.506927/426.716827秒完成，876.433044/1081.682983秒被毁，账本银行收入分别1529.75C和1637.5C。两类宝藏均有真实领取及奖励证据：Abandoned Supplies116于10.400003秒生成木箱131，75W已经采完并于54.199745秒耗尽；Surveyor Records117于57.699692秒领取，同刻Experience明确授予100XP。见 [treasure-bank-audit.json](treasure-bank-audit.json)。

## IV付款不等于IV完成

[时代IV中断审核](ageiv-interruption-audit.json)保留完整付款和Town Center死亡证据：

| 原events行 | 世界秒 | 实际事件 |
|---:|---:|---|
| 43114 | 998.003357 | TC1支付2000F1200C以升级IV |
| 47587 | 1045.491821 | TC1被毁 |

间隔只有 **47.488464秒**，不足IV的130秒研究时间。没有玩家IV的AgeAdvanced，终局时代仍为III。因此不将付款、排队或资源扣除算作完成IV，也不推断已取得IV奖励。

## 最后两张军船的真实状态

玩家共发送6张船，只有5张到达。Cannon Support于995.403992秒发送、1030.395508秒到达，生成Field Cannon288；它在1043.792236秒被毁。它属于船运炮，没有付费生产链，不能填补 `paidCannon`。本局也没有炮兵对敌建筑的实际Damage证据。

Hussar Reserves于995.603943秒发送，终局前没有ShipmentArrived；不把已经点击发送的4Hussar当成场上援军。其他已到达船为River Coin、Settler Reserves、Wood Convoy2、Mounted Guard。详见 [shipment-rescue-audit.json](shipment-rescue-audit.json)。

## 归档与校验

保留本局两组完整最终report/events、全部6组F9 JSON/PNG、全量原生客户端日志、规则及卡组排序源码、严格审核器、055源码/构建receipt快照。没有删去未达成部分、修改规则或用其它对局的成功片段补本局缺口。

全量日志先保存至 `Builds/NativeInput055-C2-preserved/Player-ended.log`，**148,750字节**，SHA-256 `e6745e48d2e8d50ae3a598c2228fe5dbd81047a4badca04e75e5e92024cfc387`；目录内 [native-input-full-preserved.log](native-input-full-preserved.log)为完全相同副本。本次进程只含C2，精确摘录原始第 **39–1569行**，从启动后的首次focus标记至menu_exit堆栈结束；另保留1045条FRONTIER标记和原始行号，见 [input-trace-audit.json](input-trace-audit.json)。

6组F9的原始PNG哈希、尺寸、版本/GUID、请求/实时/HUD对局身份及世界时间均配对成立。其中菜单、61.500秒Pathfinder、504.922秒暂停画面和终局图片做了只读视觉检查；504.922秒图显示MATCH PAUSED，原始metadata的paused字段也为true。见 [capture-audit.json](capture-audit.json)。

[冻结身份检查](frozen-identity-audit.json)确认357份列明源码和3项compiled runtime SHA仍匹配原055；没有运行Unity、重建或操作GUI。调用参数见 [audit-invocation.json](audit-invocation.json)，原字节复制校验见 [source-copy-audit.json](source-copy-audit.json)，重要事件完整payload见 [key-event-payloads.json](key-event-payloads.json)，最终文件SHA清单见 [archive-receipt.json](archive-receipt.json)。本次只新增本归档，未提交或推送。
