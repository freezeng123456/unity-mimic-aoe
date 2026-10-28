# 076验证记录 · 0.19.0开发中

2026-10-10。本轮合同[076](Content-076-Contract.md)，[分层验收矩阵](Acceptance-Matrix-076.md)。075已私有交付：[075](Delivery-075.md)，运行51bd61f/封包67b13e7，1723正式用例、原GUID及原生范围保持原身份，不能视为076测试。

本轮开始时新增医疗能力/订单、四经典文明付费医护链、原创建模和AI整补。以下门槛只有实际执行后才填数值与源码身份：

|门槛|当前证据|
|---|---|
|Focused1|cd23300、774输入FP3d0849cf53886a21e8d915e9106a1d85afeae51f72472958346a0f1639f47192；实际Edit75/77、exit2，Play未运行，源码稳定。两项受控权限/网络fixture附近有真实武装敌人，引发患者实际自动交战/战斗锁；调整fixture隔离而不放宽治疗规则，下一次重验。|
|Focused2|41546eb、774输入FP8b33e0973f1ef67273dc7e6acdb28bfeced58c534bfcf55978ab9e9cd4230fd8；实际Edit78/78、Play28/29、exit2、源码稳定。唯一失败为新医护手势在两个相邻相位只有1.64mm变化（要求>5mm），增强真实动作并保留原断言，待重验。|
|Focused3|911fc58、774输入FP51103973c58821571bc79da2b4e931801e0dce9c71bd262be513ebd236bba49a；实际Edit82/82、Play29/29、exit0、源码稳定。治疗动作及AI重试回归已通过；随后补充非空重试状态的冷恢复正向测试，另轮验证，不覆盖本次身份。|
|Focused4|b1a9b6a、774输入FPdabf34d26490cece872959764b8b9e515749584b180f3d7899f38d79b69a6f8f；实际Edit83/83、Play29/29、exit0、源码稳定。非空30s重试状态立即冷读及整窗阻止/到期真实Move重新接纳通过；随后仅同源码全回归/构建/普通观察。|
|全回归|Full1：b1a9b6a、774输入FPdabf34d26490cece872959764b8b9e515749584b180f3d7899f38d79b69a6f8f；实际Edit1347/1347、Play448/448，共1795/1795、exit0，运行前后源码稳定。原XML在Builds/Tests/run-nUQ5RfoV，原runner/log保留Candidate1/Full1。|
|macOS构建|Build1实际exit0、源码稳定同FP；app Info.plist版本0.19.0，boot.config新GUID `a86c515f3ab040649ea505e507193ff3`。实际app清单298文件/191062651字节（解包文件总量，不是ZIP大小）；原生验收另列下行，不由build成功推导。|
|普通AI|NormalObserved1 exit0、源码稳定同FP；首局t1768.615自然P0胜，曲河t2400.091观察cap、不计胜。5次真实付款/出生、34条同患者撤回→正数Heal→85%返回→后续Fire链；原事件87266/175321条与manifest/final一致，详见[普通结果](Ordinary-076-Results.md)。平均Step3.179/11.329ms，最大2102.354/2199.317ms，不是原生FPS或平衡对照。|
|新GUID原生|British/Winding42／standard默认开局已实际150W Church→800F LocalDefence AgeII→100C Priest153；自然受伤patient8 208→295（87自动Heal）→Stop→手动2HP→保存297。旧PID78563退出、新PID97963 LOAD schema5原slot1后同医生／订单续疗153HP至450且Idle；两段原生录像库实际无seek连续播放到Verified（冷段3813→4004／1x，开局0→3813／4x）。首局失败与restart_exit原件保留，不计自然败局；详细原件、帧／事件／进程边界与待验见[原生记录](Native-076.md)。|
|私有交付|封包快照f0f829b与运行时b1a9b6a区分：原Delivery1 exit2／原独立审计FAIL仅完整evidence ZIP超过100MiB，全部原件保留；独立transport审计PASS、十二文件294999523B已全字节／SHA／owner-only／父目录及子ID集合读回，远端两片完整拼接SHA一致。既有private／open／draft PR1已按f0快照精确读回且main不变；后补交付文档HEAD1f74195亦已独立GET读回private／open／draft及main不变，后续receipt归档不改变运行时／封包；详见[076交付](Delivery-076.md)，不覆盖075。|

专项第四轮112项中，本轮新增医护用例为56 Edit（核心28、AI19、复制/保存9）与16 Play（输入10、表现6），共72；其余40是相同新源码下的旧兼容回归，不宣称112项全是新功能用例。

所有受控测试预置资源/单位/伤员必须标明fixture，不称普通开局。实际HP/付款/退回前线由事件和完整状态证明；测试数不是玩法收益、平衡、FPS或双物理Mac的替代。

独立代码审查发现并在后续源码修复的真实问题：阵营退出残留医疗链接；治疗Command/Heal事件未同时保护隐藏患者与隐藏来源；超时退出恢复名单后立刻重新入队会使90秒上限失效。各自追加实际回归用例；未把静态检查当作已运行结果。
