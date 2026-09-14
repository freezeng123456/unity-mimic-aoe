# 035 经济与截图证据契约（实施前建立，实施中补充）

这是 P 项目可追溯性规则，不声称原作使用相同日志或截图实现。原作修理价格、建造退款细节仍按既有参考文档的 R/P/U 状态；本轮不更改价格、速度或退款比例。

034 原生正常局有 1243 笔真实 Repair 付款，但 Cost.ToString() 把每步金额显示为零。当前付款事件无法从文本还原金额与修理结果。目标是保存每次成功付款/退款的名义变化、实际浮点余额变化、前后余额；修理付款同时记录真实目标 ID 和 HP 前后值。失败、暂停、已满血、重复取消不得产生付款或退款。该记录捕捉已有结算，不参与再次扣款或治疗。

事件格式增加版本号；付款/退款附经济记录，正号是收入，负号是支出。名义变化来自规则计算，实际变化来自余额相减，两者分开以显露浮点累计误差。既有消息和 amount 字段保持用途，不把 amount=0 解读为免费。采集/银行原有按来源合并的 Gather/BankIncome 记录继续保留，本轮不将其改称全局逐位精确账本。

新增训练入队事件，记录真实已预留人口。Spawn、Death、BuildingComplete、付款/退款、训练入队等人口相关节点携带 used/reserved/cap 的值快照。付款先于训练入队，付款事件中的人口值是该付款时刻，不混同于之后的 reservation；死亡事件应反映清理后状态。历史 JSONL 没有这些字段，应按旧版本处理，不能用默认零补出旧事实。

034 的人口显示疑点尚未在场景测试复现：实际训练5火枪兵→敌方致命伤害→死亡清理与真实HUD文字两例在旧运行时通过2/2。不能据此宣称原生像素问题已解决。后续 F9 在已渲染帧结束时采集画面并保存旁边的 JSON：实际版本/GUID、帧号、模拟时间、玩家人口/余额、HUD实际文字、玩家生产建筑与队列。只记录玩家已有信息，不通过截图界面泄露迷雾后的敌情。图和元数据用于区分模拟、UI与最终像素，不影响经济结算。

验收包括修理小额付款、多人修理、接近满血的最后一次修理、无法支付、暂停、满血、训练取消与重复取消、建筑死亡释放预留、JSON序列化后的字段一致性；必须先在旧事件格式看到缺失数据的失败，再应用改动。之后进行原生客户端的真实修理、F9图与状态配对、暂停恢复及正常对局检查。自动断言不代替原生视觉验收。

实现补充：SimEvent.schemaVersion=2，以 hasEconomy / hasPopulation 明确区分有记录与无记录，不能仅检查嵌套对象非空（Unity反序列化可能生成默认对象）。repair 的 hasRepair、healthTarget、hpBefore、hpAfter 均位于 economy 中。训练入队事件名为 ProductionQueued。

F9元数据格式为 schemaVersion=1，关联 eventSchemaVersion=2；MatchId采用每局新GUID。HUD除了原文，还保存其最后更新frame/time/matchId，便于识别同帧不同Update先后顺序；截图不强制更新UI。图片SHA、尺寸单列，不能混入HUD文字。PNG后成功写入同名JSON才报告完成，孤立PNG视为不完整输出。Editor批处理与无图形设备明确不提供截图。F9读取/编码会产生一次性同步开销，不算普通帧性能结论。

API依据：已检查 [Unity 6.6 CaptureScreenshotAsTexture](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/ScreenCapture.CaptureScreenshotAsTexture.html) 与 [WaitForEndOfFrame](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/WaitForEndOfFrame.html) 文档。该机制在摄像机和GUI完成后获取帧；Editor批处理不会继续等待帧结束的协程。因此元数据测试与实际图像测试分开。
