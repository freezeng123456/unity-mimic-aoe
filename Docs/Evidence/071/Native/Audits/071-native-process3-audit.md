# 071 Native Process3 独立原始审计

**原始槽一冷加载后真实推进并完成革命；控制组写入了新槽二；政府研究已付款但仍未开始计时。** 范围为已退出Process3/ProfileB与作为复制来源的ProfileA槽一，不构成政府完成、首工恢复或普通开局验收。

ProfileB槽一与原A槽一逐字节一致，SHA `b21281969465c72f1cb8668a9df6188e57a6e44def5cb9552a722c0fc78722b8`。Process3记录pid58827、exitCode0；原生LOAD日志、录像startKind=loaded_checkpoint与初始anchor均对应904步、世界90.399193s。初始anchor与原World仅events规范化留空，与原simulation仅accumulator规范化归零；政治队列与六工人并未换成新开局。

从原队列0/90s正常resume，RevolutionCompleted发生在**180.501816s**，较90.399193s相差**90.102623世界秒**，与90秒生产在固定步长与浮点累加下的量化一致。F9中间进度为8.499996/90、40.399956/90，之后队列为空且单位3–8变成revolutionary。六个稳定ID不变、人口仍6，原满血150/150比例转换为190/190。P0仍时代IV，政治阶段Revolutionary。原录制与新段的继承132事件前缀一致；新段没有重付革命或重放退款。

**244.105698s**在Town1接受civilian_rebuilding Research，实际扣**400F/400C**，500F/0W/400C→100F/0W/0C，随即暂停。槽二队列paid400F/400C、**elapsed0/duration60**，civilianRebuiltAt=0，无CivilianRebuilt。世界时间244秒不能当作政府研究已推进60秒。后续ProfileC需要真实冷恢复和继续该队列。

槽二真实groups=`[{number:1,actors:[3]}]`，actor3为自有存活革命兵。UI日志在197.903s实际执行SAVE1，208.204s附近Digit1后context回到entity3。原生选择context在163.101s与180.502s均为ID3，支持选中ID跨转换保留；**控制组是在转换完成之后才保存**，不能据此声称“转换前建组跨转换继承”。槽二最终选中Town1，和已持久化组1是两个状态。

P0获得的20XP来自200.203s真实敌方Pathfinder击杀的Combat bounty，shipmentTokens仍0；本过程没有采集Gather收入，政府付款来自受控场景原预算。它不是箱子采集或自然资源恢复的证明。

槽二、录像和报告的234条事件语义完全一致。新录像segmentId=`9e79e4ab27b4410cbba5e09f8f4a5b94`，startKind=loaded_checkpoint、finishReason=menu_exit、incomplete=false；四条记录为resume、Research、pause、flush。初末anchor分别904与2441步。录像文件payload SHA和原始state/event/input digest完整保存在JSON，未在本任务执行前向ReplaySession。

9组F9的PNG SHA、1600×1000尺寸、schema6、构建／match／scenario身份、请求与HUD时间及资源文本均核对。按根代理的原生操作记录，母城在世界约162s关闭，转换后才重开查看革命卡组；因此**不宣称原生观察到already-open母城跨转换自动刷新**。像素级观测由根代理原生验收另行记录。

所有26个Process3/ProfileB原始文件及1个A来源槽的字节SHA已记录并在输出前后复读确认未变。构建身份为0.14.0 / `2d39f78f9f234ef092e172a35f800cae`，match保持`f496cf03970641248584b719cdf87515`，scenarioHash保持`95eecd198899733720bcdb76556c2c8524e514881b002da9adf8756838977319`。政府研究完整60秒、组1的ProfileC冷恢复、首名工人及真实采箱／自然采集需要后续独立证据。本审计未控制GUI、运行Unity、复制或修改原始文件。

完整证据见`071-native-process3-audit.json`，含来源/复制/新槽/录像字节与payload SHA、所有原始文件清单、政治转换与研究付款、控制组日志与F9配对资料。
