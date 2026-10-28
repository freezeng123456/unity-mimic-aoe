# 074 验证记录

2026-10-07。运行源码 `ba0b57336c46d72738b71b59af923a149aff467d`，740输入，FP `61d00af23e00d960dd040ef55bdfb0aa269b15745be1d1a2f0faec46377d6698`。

最终完整 `run-xijamd2m`：1227 EditMode + 419 PlayMode，**1646/1646 Passed**，零失败/跳过/未决，实际exit0且前后源码稳定。073旧1611 fullname保留，新增18 Gather和17 Home；Focused3的64项是子集，不叠加到完整总数。完整原件见[Full2源码证据](Evidence/074/Tests/Full2/source-evidence.json)。

最初b72源码Focused1 63/Full1 1645通过，后续投影距离用例在5af源码实际0/1失败：普通Gather可走到投射工作点，但边缘距离2.6大于实际采集范围1.5，16秒真实运动无木材收入。最终准入guard修正后，同一测试文件/断言未弱化，Focused3 64通过，包括清障后的真实正收入。历史原件和各自指纹分别保留。

实际Mac Build1：0.17.0，Unity6000.6.3f1，arm64 Development/Mono，Succeeded/exit0，源码前后稳定，GUID `1ada3d99ddf84c0f93e1b526e6360390`。未调用Prepare。当前GUID原生交互、冷恢复、录像、FPS和发布签名未验收。

3条普通I观察已关闭：0自然终局、3达到2400秒上限。预声明 winding Ottoman44/45、river_bend Dutch42；I时代、上限V、standard、双AI、2400秒cap、1500比较点、30秒采样。没有运行注资、强制命令或干预。

|对局|实际结果/模拟秒|真实Gather事件 P0/P1|市场/集结|拒绝采样行/累计数|末300秒正PvP伤害|
|---|---|---:|---:|---:|---:|
|winding-ottoman-44|observation_cap / 2400.090576|44519/60192|14/25|2/2|886|
|winding-ottoman-45|observation_cap / 2400.090576|56027/39887|15/18|4/18|889|
|river_bend-river-42|observation_cap / 2400.090576|37989/45156|10/20|4/7|642|

真实Gather事件与每tick float32 gathered账本分别核对；缓冲日志的double总和不冒称与账本精确相等。市场按每笔报价、实际扣费/到账、样本和边界账本核对；Post/Ledger、运输、集结、伤害和自然终局/cap按原事件核对，零触发原样保留。完整闭合证据检查实际13657项、零失败，见[报告](Evidence/074/Diagnostics/validation074-closed-evidence-2a.md)。验证者编写了Gather实现，不宣称独立实现审计。

拒绝采样数包含隐藏/失效/耗尽目标、资格、物理不通、工作点超距离和普通路径危险；它不是独立工人数、穿越敌区次数或完整轨迹。standard约1.2秒AI审查仍允许两个审查点之间发生新风险。headless Step/观察/写盘计时不认证GUI帧率、纯Home性能因果或六文明平衡。

选定原件在Evidence/074，完整三局原件另在normal.zip；封包与云盘结果见[交付记录](Delivery-074.md)。073的1611项、旧GUID市场建造/100W付款/完成及原生兑换未完成保持旧身份，见[073验证](Verification-073.md)。
