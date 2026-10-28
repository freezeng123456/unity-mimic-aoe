# 076普通AI观察实测

绑定源码 b1a9b6a，774输入FPdabf34d26490cece872959764b8b9e515749584b180f3d7899f38d79b69a6f8f，与Full1/Build1一致。默认AgeI、默认钱包/单位、standard AI；未注资/制造伤员/强制医疗命令。原件及逐事件引用见 [实际分析](Evidence/076/Validation1/NormalObserved1/analysis.md)。

|地图/方|付费队列/出生|实际治疗HP|撤回/85%返回|同人完整回火链|
|---|---:|---:|---:|---:|
|frontier P0 ottoman|1/1|1550.000000|11/9|7|
|frontier P1 ottoman|1/1|1342.000015|4/4|4|
|winding P0 ottoman|1/1|2652.799957|10/9|9|
|winding P1 coast|2/2|2487.200000|16/14|14|

合计5次实际付费训练/出生、34条严格 Retreat→正数HealingReceipt→精确85%Return→同患者Fire（下一次撤回/死亡之前）。不把只有出生/治疗/释放而无回火算完整链。

frontier镜像seed42：1768.615秒P0自然获胜；winding奥斯曼对英国seed44：2400.091秒观察上限、winner=-1，不计胜。事件原JSONL/manifest/final完整World分别87266和175321条一致，orphanReturns=0。

未闭合：frontier P0两名85%返回后终局前未再开火、两名仍在恢复；winding P0/P1各一名恢复中真实死亡，P1末尾一名刚进入恢复。医生真实死亡1名（frontier P1）。无本次观察到的90秒超时/安全/手工/不可用释放，不据此声称这些路线普通局已覆盖。

头less Step平均3.179/11.329ms、最大2102.354/2199.317ms（frontier/winding），仍有约2.2秒慢步；不是原生FPS、百分位、总体加速或医疗收益对照。30秒样本存在重复末尾行，分析保留最后行而不重复累计。

两局只证明这两组默认开局的实际使用，不证明平衡、因果胜率或经济无损。matched-seed关闭医疗对照、更多地图/文明、真实曲河医疗/运输仍单独验收。当前GUID原生付费、冷恢复与两段录像播放范围见[原生结果](Native-076.md)，不从普通AI观察推导。
