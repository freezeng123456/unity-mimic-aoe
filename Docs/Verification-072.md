# 0.15.0 验证记录 · 072

2026-10-06。运行提交 `bc127e02638caaeefd2310a1a3eff858f4f4cb2f`；718个输入（Assets、Packages、ProjectSettings、Tools）的指纹为 `04698dad49278a42db8a13a2ab997870bfb008c52f95313130026da5a7f35f62`。文档与原件归档不改变这些运行输入。源码/自动化/构建及本GUID五进程的9个声明原生范围已闭合；正常16局实际关闭，独立Consistent、路线9/16达到，原状态observed_routes_incomplete；私有交付待验。

## 正式完整回归与构建

`Builds/Tests/run-HACRyTBF`实际 **1158/1158 EditMode、409/409 PlayMode，共1567/1567 Passed**，Failed/Skipped/Inconclusive均0。Edit776.526388s、Play230.8869331s。独立 `Diagnostics/full-regression072-final1.json` 状态为 `passed_frozen_full_regression`，核原XML、718输入、071旧1117Edit/404Play名称保留及新增41Edit/5Play。名称保留不表示断言源码从未作合法修正；实际失败、诊断与修复可分别审阅。

| 原件 | SHA256 |
|---|---|
| EditMode.xml | `7afadc06362c49307cdd18548968aff7b10c76f721ea31ba2d2c075c3d6aa4f4` |
| PlayMode.xml | `25ebabe56bce8b39e84376ad14510d4c449587bdff05d50a4424d15ad9de2952` |

BuildFinal1由BuildMac直接构建：Unity6000.6.3f1、macOS arm64、Development/Mono、version0.15.0，实际exit0/Succeeded，构建前后718输入指纹相同；GUID `3288d9fe24e844748a1183f756874fbf`。可执行35151B，SHA `fb67cb57de257681b532b109b96d78cbfd27c2d0e26d51094eef6d129045e651`；Frontier.Runtime.dll1193472B，SHA `0f96f8490c87a33fd142b61d4fde915452157e1e22e2cb09221b23a06734ad83`。身份和runner收据核对真实文件，不由“打开应用”推定构建成功。

## 受控行为与过滤连通性

Focused21在同一04698/718冻结下实际51Edit+10Play全部Passed，两进程exit0、sourceStable：经济17、Army12、过滤连通10、NavigationSweep9、Clearance3；Play为ArmyControl4与RevolutionHud6。正式1567另行全量通过。

经济受控覆盖真实敌损Town→500W付款/施工/采集/完整cold；Post+Ledger实际付款/完成/TradeDelivery木收入；无隐藏木情报；有限来源提前启动、Bank/Estate竞争与连续维修预算；visible/hidden楼和中立Guardian；实际普通Build危险路拒绝、预检不改Live Nav/钱包/事件、已付地基不再付、合法近边距继续施工。军队覆盖成军/4人补员/可用时留守、长墙与曲河桥真实机动、Home随重建Town、可见经济线防守和完整cold。输入Play覆盖指定人/建筑伤害、观察权限和人类试玩。

过滤连通10项用公开原FindPath对照：实堵威胁通道、分数端点逃离最近节点孤岛、方向性edge filter、改变Nav.Version、同Version恢复真实新路、不同半径/过滤器独立、岸角身体/连续圆采样，以及固定seed的36条有界查询。负预检false只排除原过滤A*无路，true仍原路线终审；端点13×13全集、清线/对角和身体通行保留。上下文遇restoreGeneration变化即true回退，不拿旧负结论拒绝新路。这些是受控范围，不是所有地图/动态危险普遍安全证书。

倒计时Play用真实支付革命，实际生成90s和88s字形、文本bounds在47高按钮内、标题两行、稳定job及正常取消退款。clock仅高度15→18；更新仍走正常约1秒真实时间/指针按住保护。1.05秒真实等待后正常更新，未强制Invalidate/Rebuild。InputSystem/Canvas字形不是OS像素认证。

## 同一原生锚点的固定250步诊断

StepRun1/2各实际exit0，从原NativeA相同锚点与完整实际事件前缀恢复Controller；起始step14700、最终14950，时间1469.788208→1494.782104s。没有新开局、注资、未来输入注入或强制赢家。独立 `step-comparison072.json` 为285checks/0fail。

| 指标 | 原诊断 | 优化诊断 |
|---|---:|---:|
| 250步gross wall | 565254.4585ms | 3918.2488ms |
| 最慢单步 | 72990.4258ms | 276.0642ms |
| >500ms步数 | 11 | 0 |
| 最终事件数 | 62633 | 62633 |

最终World、Controller及1343条新事件文件分别逐字节相等，digest同为 `59d3818268c962b1584cc4d9530168778ad45ca38604c60f7c5828aca9b4d75f`。当前可见圆在每Survey固定，木/币共享；同Survey/radius弱连通仅负向排除，肯定仍原A*；不新增跨事件缓存或DTO。

这是instrumented Editor诊断，不是原生GUI帧率或Release保证。候选与正式测试并行，编译/缓存/CPU环境及JSONL写入影响时间，嵌套计时不能相加。原NativeA71.482599s观察间隔中AdvanceFrame71.452664s/10步及67.594797s未采帧尾部保留。最初UPM连接/缺包编译探针保留，不改写失败为成功。

## 正常时代I矩阵 · 16条关闭，路线9条达到

`NormalRoutesFinal1`实际2026-10-06T11:37:20.0757060Z→13:52:28.3340010Z运行，16条全部关闭，runner实际exit0/sourceStable。原始状态 **observed_routes_incomplete**，独立[final1检查](Evidence/072/NormalSelected/independent-normal072-final1.json)为 **Consistent**，matrixExecuted=true、old071EvidenceRead=false、balanceVerdict=Not assessed。844份`filesUnchangedDuringAudit`是实际稳定文件引用数组，不是boolean或额外游戏测试；正式计数仍1567。

预声明两图/四经典文明/两政治偏好，普通I真实6工人及文明资金，3600模拟秒cap或自然终局、另记2400边界与30秒稀疏样本；没有运行注资、强制路线或赢家。以下P0结果从[实际manifest](Evidence/072/NormalSelected/matrix-manifest.json)读取并由固定checker核事件/付款/队列/完成与源身份：

| 图 / 文明 / seed | 请求路线 | 实际路线结果 | 终点模拟秒 / 时代 | 到2400边界 |
|---|---|---|---|---|
| Traders’ Bend / British42 | 革命 | ended_before_route | 2158.855225 / IV | 否 |
| Traders’ Bend / British43 | 升V | route_reached | 2458.747803 / V | 是 |
| Traders’ Bend / Dutch42 | 革命 | route_reached；cap，未自然终局 | 3600.061279 / IV | 是 |
| Traders’ Bend / Dutch43 | 升V | ended_before_route | 2107.204834 / IV | 否 |
| Traders’ Bend / French42 | 革命 | route_reached | 2726.809326 / IV | 是 |
| Traders’ Bend / French43 | 升V | route_reached | 2944.922119 / V | 是 |
| Traders’ Bend / Ottoman42 | 革命 | route_reached | 2228.523193 / IV | 否 |
| Traders’ Bend / Ottoman43 | 升V | ended_before_route | 2214.609619 / IV | 否 |
| Winding River / British44 | 革命 | route_reached | 1849.295532 / IV | 否 |
| Winding River / British45 | 升V | route_reached | 2746.128174 / V | 是 |
| Winding River / Dutch44 | 革命 | route_reached | 1847.995850 / IV | 否 |
| Winding River / Dutch45 | 升V | ended_before_route | 1693.133667 / IV | 否 |
| Winding River / French44 | 革命 | route_reached | 2083.882080 / IV | 否 |
| Winding River / French45 | 升V | ended_before_route | 1764.116333 / IV | 否 |
| Winding River / Ottoman44 | 革命 | ended_before_route | 1982.762939 / II | 否 |
| Winding River / Ottoman45 | 升V | ended_before_route | 1725.525757 / II | 否 |

9/16达到声明路线：革命6/8，升V3/8；其余7条为提前自然终局前未达到请求路线（革命2、升V5）。革命6条均有真实革命/政府支付与完工、首平民和正普通陆工非crate收入；升V3条均真实支付并完工。并非三资源全部恢复：Dutch42恢复后Wood=0，WindingFrench44恢复后Wood=0。15局自然终局、1局cap；5局到达2400，11局在2400前自然终局。这是固定种子镜像的描述结果，不是文明胜率、整体平衡或071→072因果改善。

[经济JSON/MD](Evidence/072/NormalSelected/normal072-economics-final1.md)实际analyzed_closed_actual_data，分别核16局/32位玩家。以下计数为观察到相应阶段的玩家数，允许同一玩家有多座设施，不是建筑总数：

| 链条 | 正常资源付款 | 完工/激活 | 正Wood | 正Coin |
|---|---:|---:|---:|---:|
| Trading Post | 27 | 26 | 25 | 21 |
| Cargo Ledgers研究 | 26 | 25 | 与Post实际配送分开 | 与Post实际配送分开 |
| Dutch Bank | 8 | 8 | 0 | 8 |
| Estate | 29 | 29 | 0 | 29 |
| Factory | 不虚构资源Foundation付款；实际消耗合法wagon | 22 | 19 | 21 |

双方共30条敌国记账Town实体Death，涉及16位玩家，包含未完成地基；无存活Town时实际15笔500W正常重建付款，涉及13位玩家，其中5笔完工、10笔未完工。不能把政府“Civilian Rebuilt”阶段混成建筑Town重建，也不能把付费地基算完成。付款事件的firstPaymentAt与observer首次已付队列observedQueueAt是不同时间字段。贸易/车队收入在经济分析中另计，observer Gather+Passive收入不覆盖它们。

**保留限制：** Dutch Traders’ Bend42 wall3303.190543s约55分3秒，模拟3600.061279s且未自然终局；矩阵关闭不认证倍率/FPS。WindingOttoman44停留Age2自然失败，有界[缺币诊断](Evidence/072/NormalSelected/072-winding-ottoman44-live-investigation.md)表明采币已在首Town丢失前停滞，钱包11.140015C；初始矿源/路径/工人订单原因未证实，Market当前没有资源兑换。晚段敌袭实际杀工人的事实与初始停止原因分别记录。该历史诊断原件的当时pending文字保留，当前关闭由final1检查证明。

30秒样本/缓冲账本只能描述各自收入区间、已记录军队位置/订单/路径，不能给出逐秒采集、连续机动或普遍安全证明。完整原件外部保留；[NormalSelected](Evidence/072/NormalSelected/README.md)逐字归档15份摘要/源工具、6,435,792B与独立bytes/SHA/mode索引，不复制巨型samples/events/final-world。旧071原身份不改。

## 当前GUID原生 · 9个声明范围闭合

**native_scopes_complete**。本GUID D/E/F/G/H五个真实Mac进程均已实际exit0，9个声明范围接受，pending=[]；最终journal的releaseAcceptance为 `accepted_declared_native_scopes_only`，整体release仍false。原始目录为 `NativeObserved/FinalGuid072`；旧running journal与scope plan保持原字节。[最终journal原件](Evidence/072/Native/FinalJournal/native-journal072-final.json)逐字归档，SHA `4d98e829279aa259e1053cd87e8513c6a6fdd6c83769a38adeee4259c2564bc3`，116原件269,005,909B/40对F9及16个外部审计引用均复核稳定。8份源审计3659checks/0fail是合计检查次数，不是去重测试数。原生操作者是Codex root agent通过公开Sky工具，未将其称为人类用户；仅抽查声明选定像素。自然DEFEAT/结果窗不用于替代launcher的实际退出收据。

| 进程 | 真实观察及独立证据 | 范围 |
|---|---|---|
| D / PID83036 | 普通I Dutch mirror、seed42、正常6工人100F/300W/400C；实际42m32自然终局与exit0。[军队审计](Evidence/072/Native/BoundedArmy/README.md)457checks/0fail | 实际6/10/14成军，同时间普通AttackMove、4人及以上补员、可见防守与战损重集结。30秒锚点不证明每帧留守或最优路线 |
| E / PID84132 | [攻击/政治审计](Evidence/072/Native/SelectedScopes/072-final-processe-audit.json)92checks/0fail；[保存/录像原件链](Evidence/072/Native/SelectedScopes/072-final-processe-save-replay-audit.json)205checks/0fail | 声明受控IV与实际A模式+左键目标输入，非正常I经济路线、右键或移动工人追击 |
| F / PID85449 | [冷状态/续跑](Evidence/072/Native/SelectedScopes/072-final-processf-cold-save-replay-audit.json)397checks/0fail；[续跑原图](Evidence/072/Native/SelectedScopes/072-final-processf-continuation-pixels-audit.json)35checks/0fail | 新进程无scenario/autoplay加载E Slot1；完整初始World/Controller按记录器约定归一后全等；政府完成及新工人89。没有自然采集或编组恢复声明 |
| G / PID85848 | [只读/相机](Evidence/072/Native/SelectedScopes/072-final-processg-camera-readonly-audit.json)77checks/0fail | 实际选中GUNBOAT165与Town，仅Details/Focus；M/F、T/F与minimap移动相机。A/S在输入入口被忽略，没有到World.Issue；不伪造拒绝命令事件 |
| H / PID85769 | [实际短段前向](Evidence/072/Native/SelectedScopes/072-final-processh-forward-audit.json)506checks/0fail | 起播前seek300/600，实际1×播放600→1111（59.999657→111.098877s），期间无seek；未到5490，Verified=false |

E的实际接受攻击事件166针对Guardian84，Hussar77产生3次35伤害并在86.999245s出现Death；事件178指定House83，57次16攻城伤害并在239.005386s出现Death。两个目标在各自接受命令前均无incoming Fire/Damage，House的命令后原图只证明接受目标，毁楼由实际事件闭合，不能把该张图标成已毁。

E革命实付800F/800W/800C，90.105499s后六名原工人ID3–8变志愿兵，ID/人口保持、满HP150→190。原图初始Volunteer Assembly两行/90s，后来50s/17s，以及政府60s已独立查看；后来标题普遍完整不在声明中。政府实付400F/400C→取消全退→再次实付，Slot1保存时已付款elapsed0.10000000149/60s、wallet100F/0W/0C。367事件与E replay/slot严格全等，20锚点/10记录链独立重算。

F冷录段从step5490、549.013s暂停开始。完整初始状态仅按记录器约定置World.events=[]、simulation.accumulator=0后与E存档全等，未抹去其他差异。政府在608.998352s真正完成，随后正常Train付款100F、预留1人口、658.086365s出现Settler89/HP150；原六名志愿兵仍190HP。原图可见空政府队列、Settler20s及选中新工人150HP。该工人未实际采木；F前五张1600×1000、随后3456×2168和3416×1884的变尺寸保留，不假设统一分辨率。

H实际导出的F9 currentStep为0→600→1111，原图WATCHING/PAUSED、timeline0:00→1:00→1:51 of9:09，XP0→20→28，未报告error/fileError/libraryError。前向区间覆盖原接受Attack record step787及要求锚点900；nextAnchor1200、recordIndex3、eventCount178是原录像/实现推导，不是F9实际导出字段。没有H终点完整World/Controller或quit事件报告，不称终点全状态相等或整段verified。

G与D初始完整World/Controller、52公共锚点及前65117事件字节一致；G提前正常QUIT时另外62条（58 Gather+4 BankIncome）经济flush尾部保留，D继续下一Tick，不能宣称全部事件相同。G没有人类gameplay录制记录。资源在AI继续模拟时正常变化，不用钱包不变代替只读权限证明。

### D有限原生性能窗

[独立F8审计](Evidence/072/Native/BoundedPerformance/README.md)1890checks/0fail，只核155.623315真实秒（世界857.737610→2201.997314s），与headless矩阵并发，M3Pro、1600×1000。1872连续frame中887纳入、985排除（984失焦+1refocus边界）；请求16×、实际8.637907×。纳入mean68.508781ms、p95366.672089ms、p99416.679077ms；原始最大实时间隔1275.270416ms，末尾未采帧90.35825ms，该窗口未见>10秒gap。

这不是完整42m32对局、固定FPS、隔离硬件基准或零泄漏认证。实际存在追赶和速度差；原始Player.log关闭Mono/MemoryLeaks诊断保留（D3296486、E3259006 allocatedMemory），exit0不代表没有泄漏。旧47/f991 NativeA卡顿、NativeC丢clock及Focused19刷新失败仍保持原身份。

## 私有交付 · 待验

**PENDING_DELIVERY**。已按原图选定10张GalleryFinal1素材（8张E/D、1张G港区、1张F新Settler），不修改像素；选图/PNG CRC与复制检查不等于终包或云端交付。`Diagnostics/072-gallery-selection-final1.json`记录实际选择与原路径/SHA。新身份包、独立成员CRC/SHA/bytes/模式核验、owner-only上传/权限与字节读回尚待完成。未建立新Drive完成链接、公开发行或扩大分享。历史071交付见[Delivery-071](Delivery-071.md)，不重新标成072。

## 当前原件位置

完整XML、日志和source-evidence在本地项目 `Builds/Tests/run-HACRyTBF/`。外部Candidate1根为 `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.15.0/Candidate1`，当前有 `Diagnostics/full-regression072-final1.json`、`BuildFinal1/runner-receipt.json`/`build-identity.json`/构建原件、`Focused21/`、`Diagnostics/PerfProbe072/StepRun1`/`StepRun2`和独立对照。已封存的选定原生审计MD/JSON逐字节复制到[SelectedScopes](Evidence/072/Native/SelectedScopes/README.md)，见独立复制bytes/SHA/mode索引；完整PNG、Slot/Replays/Profile保持外部冻结原件，最终journal及说明已另归档到[FinalJournal](Evidence/072/Native/FinalJournal/README.md)，矩阵已实际退出并经固定独立checker核验，15份选定metadata/经济分析/工具原件已归档到[NormalSelected](Evidence/072/NormalSelected/README.md)，完整巨型原件保留外部供独立封存。尚未复制到Git的路径不伪造 `Evidence/072`链接。

更早完整源47/a132/f991与071/070原文保持其身份。保存/录像严格appVersion/buildIdentity；不承诺071→072原生迁移、跨GUID播放、双物理Mac、签名Release、60FPS或零泄漏。[内容合同](Content-072.md)、[玩家操作](Player-Guide-072.md)、[限制](Known-Issues.md)。
