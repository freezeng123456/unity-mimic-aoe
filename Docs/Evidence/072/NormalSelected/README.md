# 072 已关闭正常I矩阵 · 选定原件

2026-10-06。`NormalRoutesFinal1`实际开始2026-10-06T11:37:20.0757060Z，结束13:52:28.3340010Z；16条均已关闭，runner实际exit0/sourceStable。当前运行提交 `bc127e02638caaeefd2310a1a3eff858f4f4cb2f`、718输入、FP `04698dad49278a42db8a13a2ab997870bfb008c52f95313130026da5a7f35f62`，当前客户端GUID `3288d9fe24e844748a1183f756874fbf`。

## 实际结果与原件

[矩阵manifest](matrix-manifest.json)状态 **observed_routes_incomplete**：P0共9/16达到声明路线，革命6/8、升V3/8；其余7条ended_before_route。15局自然终局，1局Dutch Traders’ Bend42达到3600.061279模拟秒cap。仅5局到达2400观察边界，其余11局在2400前自然终局；不能把提前终局填充成完整2400窗口。

[独立检查](independent-normal072-final1.json)实际 **Consistent**，matrixExecuted=true、old071EvidenceRead=false、balanceVerdict=Not assessed。`filesUnchangedDuringAudit`是844份路径/bytes/SHA/文件身份的稳定引用记录数组，不是boolean，也不是844项游戏测试。它核源码和原件一致，不重新执行Unity或认证文明平衡。

[经济JSON](normal072-economics-final1.json)状态analyzed_closed_actual_data，16局/32位玩家；[经济MD](normal072-economics-final1.md)按玩家区分真实设施付款、完工、正收入、Town敌损/重建、工人和陆军生死。P0 routeReached仅沿用observer字段，经济分析不自行认证路线。支付事件时间与observer首次观察到已付款队列的时间分别保留，不混成同一时刻。

| 选定原件 | 来源及边界 |
|---|---|
| [runner](runner-receipt.json)、[before](source-before.json)、[after](source-after.json) | 实际路由进程exit0、718项前后源清单一致；不是新游戏/新测试 |
| [预声明](predeclared-observation072-final1.json) | 原始16case/3600cap/2400comparison/30秒采样；历史baseCommit、旧声明与repeatReason原样保留，以runtimeCommit/sourceFingerprint绑定本次运行 |
| [observer](NormalRoutes072.cs)、[runner入口](run072_absolute_frozen.py) | 冻结实现逐字快照，未修改Assets/Tools，未重新执行模拟 |
| [固定独立checker](independent-normal072.py) | SHA `8579ec7a2bfdda5987c830a29bebfd94c1c0c17fc58b04af93f710c63661858c`，未为了结果修改 |
| [经济分析工具](analyze-normal072.py)、[工具说明](analyze-normal072-usage.md) | 原始分析/指标定义；说明的准备时点字段保持历史身份 |
| [曲河Ottoman44诊断JSON](072-winding-ottoman44-live-investigation.json)、[MD](072-winding-ottoman44-live-investigation.md) | 当时有界live-prefix/晚段事件调查，原先“final audit pending”文字原样保留；当前矩阵闭合由独立final1原件证明。初始Coin停止原因仍未证实 |

## 明确保留的限制

- 达到革命路线指真实支付/革命与政府完工、首平民及正的普通陆工非crate收入；不表示三资源均恢复。Dutch42与WindingFrench44的恢复后Wood=0。升V路线只认证真实支付和时代V完工，不替代平民恢复。
- Traders’ Bend Dutch42耗3303.190543真实秒约55分3秒；模拟3600.061279秒，未自然终局。完整关闭不认证整局流畅度、目标倍率或固定FPS。
- Winding Ottoman44终点1982.762939秒、Age2、P0自然失败。采币累计在991.904846秒已达2841.779785后保持，1142.368164秒钱包11.140015C，早于首Town损失。初始矿耗尽/危险路径/工人订单等具体原因未证实；Market当前仅提供经济科技，没有资源兑换。晚段敌袭确实杀死工人，不能作为初始停币的唯一原因。
- 30秒稀疏样本与缓冲账本时间戳分别只能描述各自区间；不能认证逐秒采集、连续轨迹、最优路线、全部Gather安全或胜率。Factory消耗合法货运wagon，不虚构普通Foundation资源付款。
- 071证据没有用于本次独立核验；这里不提出071→072因果改善、全部路线成功或商业内容等价结论。正式测试仍1567，统计行与工具检查不加入该计数。

## 复制身份与完整外部资料

15份原件共6,435,792B逐字复制。每份原件的绝对来源、复制前/后SHA/bytes/mode/lstat身份、副本身份见[独立复制索引](normal-selected-copy-index072.json)。已有根allowlist与复制收据保持历史原字节，未重新解释为本子目录总清单。

完整16case samples/events/opening/comparison/final-world与原始日志保留在外部 `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.15.0/Candidate1/NormalRoutesFinal1`。本目录不复制巨型样本/事件/最终世界，不生成终包seal/ZIP，不提交/上传；072私有交付 **PENDING_DELIVERY**。原生的9个声明范围另由[最终journal](../Native/FinalJournal/README.md)证明。

[072验证](../../../Verification-072.md)、[当前状态](../../../Completion-Status.md)、[限制](../../../Known-Issues-072.md)。
