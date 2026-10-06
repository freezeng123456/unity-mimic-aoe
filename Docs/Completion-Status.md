# 当前完成状态 · 0.16.0 市场兑换与拥挤港区集结

2026-10-07。073新增真实付费的Food/Wood市场兑换、AI低时代Coin接续与完整Post/Ledger启动储备，以及拥挤港区的有界集结搜索。完整自动化和Mac构建已完成；三条普通I观察已实际关闭（runner exit0、源前后FP相同）：Ottoman44/45自然终局，Dutch42到2400秒cap；原生市场建造/扣费/完成已核，原生兑换点击因窗口定位错误未完成。073四包本地完整性已核，九文件私有Drive全字节SHA/owner-only读回通过；最末Git审查head见既有draft PR。六文明、五时代、四经典革命、曲河港区/海军、工作坊、课程/两条三章链、保存/录像和可信LAN沿用既有内容；没有新增国家或模型。规则仍为本项目P。[073内容](Content-073.md)、[073验证记录](Verification-073.md)、[073交付记录](Delivery-073.md)，基础操作沿用[073市场指南](Player-Guide-073.md)与[072基础指南](Player-Guide-072.md)。

## 已实现且通过自动化的内容

自己的完工Market提供 **RESOURCE EXCHANGE**，可用Coin买Food/Wood，或卖Food/Wood取得Coin；HUD支持100/500资源。初始每100资源买130C/卖70C，五lot逐笔变价合计买680C/卖320C；Food/Wood分别计价，每位玩家独立，同玩家多个Market及重建共用价格。整笔付款、到账与变价原子执行，非法/不足余额/暂停/淘汰和精度饱和均拒绝；规则限制价差与每步变价，防止买卖循环套利。真实Payment/MarketTrade和marketIncome/marketSold/marketBought进入保存、录像、LAN和完成报告，不能冒充采集或运输收入。

AI仅在安全Coin渠道不足、真实义务需要币时出售真实余粮，每次最多五lot；保留恢复Wood、升级/政治/首工义务及至少400F防守食物。Coin-only短缺也可申请真实可付的Post200W+Ledger200W/150C启动，短期筹币不会取消未付Ledger义务，紧急骑兵消费也须保留已批准的150C。Post奖励按币渠道需求选择Coin，仍须真实施工、研究和运输配送；没有设施/库存/工人/合法安全位置时仍允许失败。

集结保留原优先候选，扩至65个位置、7×7/5×5两种占地共130候选；保存延期搜索位置，每次SelectStage最多6次非直线FindPath（包括旧stage重验）。AtStage抵达判断另计，不能称整个Army Review总寻路上限。6/10/14实际抵达、4人补员及防守逻辑保持，不能仅按兵数派出。普通Gather的实际路线安全与动态护送尚未解决。

## 当前正式源码、测试与构建

| 项目 | 实际结果 |
|---|---|
| Runtime提交 | `519d7d34a0be85f75ee19e53458c636d1ba0b3e4` |
| 来源 | 732输入；`9b92812a922adb69a5ae501925417d8eba8e24b836f496274ebc8ec8679ba03d` |
| 正式完整测试 | `run-9q8yTJKS`；1192/1192Edit+419/419Play=**1611/1611 Passed**，0Failed/Skipped/Inconclusive |
| 独立完整回归核验 | 原XML/732输入与冻结/runtime Git blob一致；072旧1158Edit/409Play fullname保留，新增26Market+8StageEdit及10MarketUIPlay；独立报告的构建/普通局部分仍待续写 |
| 构建 | Unity6000.6.3f1、macOS arm64 Development/Mono；Candidate1/Build1实际exit0/Succeeded，前后源稳定；GUID `5f347ad9ec2f494984b0b49c4f896c31` |
| 普通三case | **实际关闭、exit0、sourceStable**；Ottoman44/45自然终局、Dutch42观察cap，逐case事件与账本见验证记录 |
| 本GUID原生 | **partial_market_scope，进程exit0**；三F9确认市场建造/100W扣费/完成；原生兑换、当前构建冷恢复/录像未验收 |
| 073私有交付 | **四ZIP本地独立核验、九云盘文件111794577B完整字节SHA/owner-only通过**；见交付记录 |
| 本轮Git审查 | runtime519d与SourceSnapshot0f656各有原身份；包外收据只写文档，最末head以既有私有draft PR为准 |

## 观察范围与待填项

闭合072 Ottoman44原100055事件的只读诊断确认：本地矿耗尽后8名矿工因敌袭从仍有资源的中央矿撤回，此后五次尝试另一中央矿均回撤而未采币；不是991秒全图矿耗尽或首次Town丢失导致停币。普通AiWorkforce筛目标点，实际Gather路径仍可能穿过敌区；中间A*轨迹和首次威胁判定未记录，不追加精确路径因果。073市场与Coin-only贸易接续针对供给缺口，不能视为普通路线/动态安全修复。[验证记录](Verification-073.md)列原诊断位置。

072 Dutch42的3303.190543s总wall含建局、Step、采样/事件写盘和最终保存；旧样本没有wall时间，不能定位主耗时。末段双方hasStage=false/wave空、72/70陆军持续Muster和300秒零AttackPrepared/Fire/Damage是已证实调度失败；不是已证明全部候选失败原因或主要CPU热点。073扩展候选有受控通过，三case已关闭，付款/运输收入、集结/攻击、敌损与工人存活依实际事件逐项审计。观察计时区分Step、采样/写盘和最终保存，不认证原生FPS、全面性能改善或文明平衡。

两Ott实际26笔卖Food；P1在两局都由70C市场收入补足150C Ledger费用并研究完成、收到金币运输。P0没有完成Ledger；Dutch两方有实际Ledger/运输，零市场交换。三局末300秒均有实际正PvP伤害（2749/2280/330），不等于全部cohort达目标、六文明平衡或动态路径已安全。[073验证](Verification-073.md)记录全部实际边界与计时。
Native1市场137正常100W付款且完成，三1600×1000F9已看；坐标click/drag后续noWindowsAvailable，原生兑换未完成（0MarketTrade）。键盘F9和AX菜单退出可用、进程exit0；当前GUID冷恢复/录像未验，不挪用072证据。
[073私有交付](Delivery-073.md)记录全部真实Drive文件/哈希、四包25319项完整性检查、sourceprofile1008路径/4254省略，以及包外收据与源码快照的区别。

## 历史保持原身份

0720.15.0运行提交 `bc127e02638caaeefd2310a1a3eff858f4f4cb2f`、718输入FP04698、GUID `3288d9fe24e844748a1183f756874fbf`，正式1567项、9个声明原生范围及私有11文件字节/SHA/owner-only交付均属原版。16普通局独立Consistent，但仅9/16路线达到、原状态observed_routes_incomplete；15笔500W Town恢复付款仅5笔完工，Dutch42性能与Gather安全限制保留。其[完成快照](Completion-Status-072.md)、[验证](Verification-072.md)、[交付](Delivery-072.md)、[原限制](Known-Issues-072.md)与Evidence/072原件保持不变，旧截图/存档/录像/包不重标073。

071正式1521项、英国受控IV原生/私有交付与八条仅1/8完整经济恢复仍为原身份，见[071快照](Completion-Status-071.md)、[071验证](Verification-071.md)、[071交付](Delivery-071.md)。070三章及更早工业/录像/工作坊/LAN各保留旧GUID。普通Gather全程安全、动态护送、完整商业国别制度/卡池、美术/语音、长期帧率/内存、Release/签名公证和两台物理Mac分别仍待证据。[差距方向](AoE3-Gap-Roadmap.md)、[限制](Known-Issues.md)。
