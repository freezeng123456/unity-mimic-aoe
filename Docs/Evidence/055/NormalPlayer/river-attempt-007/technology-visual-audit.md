# C7 Veteran Musketeer：旧兵与新付费兵独立视觉审核

**结论：PASS，仅限本项科技对照。** 同一老Musk **221** 在Veteran前后由 **200/200 HP、24 ATK** 变为 **240/240 HP、29 ATK**；科技完成后另行付费出生的新Musk **290** 也实际显示 **240/240 HP、29 ATK**。

本审核只读已结束C7的冻结完整输入日志、归档原始事件/报告及原始F9像素；没有操作GUI、读取源码或改动归档/canonical，也没有沿用C6实体ID。完整C验收、自然结算和炮击由其独立审计负责。

- Match：`cd717dda89684d87b979629d3fbdf1a9`；版本`0.2.31`；GUID`128a731d440e4b7a94abfcc74b88fa97`。
- 冻结`Builds/NativeInput055-C7-preserved/Player-menu.log`与归档[native-input-full-Player-menu.log](native-input-full-Player-menu.log)逐字节一致。下述输入行号针对这一完整日志，不是截段日志。
- 事件行号针对[20261003-113916-806-cd717dda89684d87b979629d3fbdf1a9-match_finished-events.jsonl](20261003-113916-806-cd717dda89684d87b979629d3fbdf1a9-match_finished-events.jsonl)。116739条原始事件与配对报告计数相同。
- 元数据无`selectedEntity`。实体身份由最近一条`FRONTIER_UI context`、无中间context改变、F9帧/时间、Spawn/Death与原像素联合确定，不从截图文件名猜测。

## 三张有效对照

| 阶段 | 实际模拟时间 | 输入上下文 | 原像素读数 | 原始图 |
|---|---:|---|---|---|
| 老兵升级前 | 860.536926 | L16918：entity221、count1、frame1340468；L16920保存F9 | MUSKETEER；HP200/200、ATK24、RANGE12、POP1；Holding | [pre](Frontier-20261003-095939-831-f1340498-4ac303abbc2f4e55b464a56f278ff8fc.png) |
| 同一老兵升级后 | 990.505188 | L17180：entity221、count1、frame1381712；L17182保存F9 | MUSKETEER；HP240/240、ATK29、RANGE12、POP1；Reloading | [post](Frontier-20261003-102401-827-f1381744-1002c8a44f534ac190b647f07e431f36.png) |
| 科技后新付费兵 | 1111.775635 | L17797：entity290、count1、frame1408688；L17799保存F9 | MUSKETEER；HP240/240、ATK29、RANGE12、POP1；Idle | [new](Frontier-20261003-103135-038-f1408689-9ff2ad7a9d184361a6074a43216fc145.png) |

以上像素通过`view_image`直接查看完整原PNG，未裁改、未使用OCR值代替视觉读取。每张JSON的PNG SHA、match/version/GUID、captureFrame与hudUpdatedFrame、worldTime与hudWorldTime均核对一致。实际三张均为count1，但这只是本次观察，不新设单选验收条件；ATK29是HUD整数显示，不推导底层浮点数恰为29。

## 付款、出生与同一身份链

1. 老兵221在 **703.7752075195312** 秒出生（事件L20632），属于677.781555秒在军营171付费的5Musk批次（L19163–19164；实际扣375F125C，L20630–20639为5个Spawn及同一军营Unit trained）。L16512保存Group1；L16917及L17179分别用Digit1召回，随后的UI上下文均为221。该实体唯一Death在 **1509.9783935546875** 秒（L57182），晚于两张对照。
2. Veteran Musketeer在 **948.4154663085938** 秒由军营171实扣 **100F100C**（L30585），输入按钮动作对应完整日志L17123。**988.5056762695312** 秒Technology与ResearchComplete同时记账（L32208–32209），只发生一次；早于990.505188秒旧兵post，晚于860.536926秒pre。付款到完成约40.090210秒。
3. 科技完成后，**1078.2838134765625** 秒在军营171实扣 **75F25C** 并排入 **musketeer ×1**（L36225–36226；输入L17685）。**1104.2774658203125** 秒Musk290出生（L37253），紧接同一军营的Unit trained（L37254）与290的RallyAssigned（L37255）。这一区间只有该单Musk的付款/队列与一个己方Musk Spawn，没有竞争的Musk出生或该军营取消/退款队列记录；实际付款至出生约25.993652秒。
4. 1111.775635秒F9选择290，晚于它的出生；它的唯一Death在 **1357.315673828125** 秒（L49736）。随后 **1112.37548828125** 秒还存在明确`Move ×1`，actor290（L37587），进一步关联所选新兵与真实命令。

## 未采用的相邻截图与边界

[1110.575928秒截图](Frontier-20261003-103102-660-f1406760-8056abd2742a4dacb199575c2ef43f66.png)的最近上下文是L17764 **entity0/count0**，画面为“THE FRONTIER AWAITS / Select units or drag a selection box.”，因此不能证明新兵属性；原文件仍保留。有效新兵图是下一张1111.775635秒，HUD时钟**18:31**，不是把口述约18:32当作精确证据。

本审核不补造Ended F9，不将缺失终局截图隐去；自然Victory及结算路径由完整C7归档单独说明。

机器可读文件[technology-visual-audit.json](technology-visual-audit.json)包含19项逐项结果、所有输入源SHA/字节数、原始事件对象及完整日志原文/行号，可随C7原始文件一起归档。此Markdown的相对原始文件链接按复制到`river-attempt-007`根目录后解析。
