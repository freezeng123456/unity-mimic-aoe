#074 闭合证据验证

verified_closed_full_build_declared_three_cases；13657项检查，0项失败。

验证者编写了Gather074运行实现；本记录是对闭合原件的验证，不宣称独立实现审计。
运行提交 `ba0b57336c46d72738b71b59af923a149aff467d`；740输入FP `61d00af23e00d960dd040ef55bdfb0aa269b15745be1d1a2f0faec46377d6698`。最终完整回归1646项（1227Edit/419Play），旧1611 fullname保留，新增18Gather/17Home；Focused64另核，不能叠加为更多测试。
初始Focused63/Full1645的b72源码、预期ProjectionRed1的5af源码原件单列保留；只把最终61d源码的Focused64/Full1646作为最终通过证据。红绿使用同一个测试文件SHA，仅准入helper改变，未弱化断言。
实际构建版本0.17.0，GUID `1ada3d99ddf84c0f93e1b526e6360390`；无Prepare，exit0及sourceStable、真实app/boot/plist/executable核对。

|Case|实际结果|Events/samples|Market/assembled|采样拒绝行/累计数|末300秒Fire/正PvPDamage|wall/Step/maxStep|
|---|---|---:|---:|---:|---:|---|
|winding-ottoman-44|observation_cap 2400.090576s winner=-1|128312/162|14/25|2/2|887/886|177.007s/175.197s/1824.255ms|
|winding-ottoman-45|observation_cap 2400.090576s winner=-1|116344/162|15/18|4/18|891/889|160.056s/159.259s/1878.372ms|
|river_bend-river-42|observation_cap 2400.090576s winner=-1|111630/162|10/20|4/7|678/642|89.120s/87.578s/1756.872ms|

开局钱包与文明默认值相同；最高V、standard双AI、2400秒cap/1500比较点/30秒采样。所有actual自然终局或cap依原manifest核对，不指定旧073赢家、时长或事件总数。Dutch scenario envelope/custom_scenario/embedded document/hash按真实合同核对。

真实Gather正收入事件与最终gathered账本分别列于JSON。前者按每工人缓冲日志，后者按每tick float32累计；保留数值差异，不冒称精确总和。Market逐笔边际报价/实际付款及到账链、各边界及样本市场账本/价格、真实Post/Ledger/TradeDelivery、集结次数/作战/终局与原件核对；零触发照实保留。

gatherRoutesRejectedAtSample是30秒settled边界上的不可准入数，可含目标失效/隐藏、物理不通、投射工作点超实际采集范围、Hold或多队列不合资格、普通路径危险。不是独立工人人数、持续穿越或具体危险原因。代码仍在下次AI review处理，未加入逐帧安全约束。

Step/observer/finalWrite分项计时均为headless实际Stopwatch，不推为GUI帧率或独立性能改善。当前GUID原生交互/冷恢复/录像、封包和云盘交付均不在本验证范围。

选定原件、源码两次SHA/byte/mode/identity保持相同；没有原件/repo修改，没有运行Unity/测试/模拟/构建或原生操作，没有封包/上传。
