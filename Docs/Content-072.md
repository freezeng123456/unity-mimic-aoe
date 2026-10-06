# 0.15.0 内容合同候选 · 经营延续、集结与指定调度（072）

2026-10-06。072整合实际军队集结、经营延续、可见危险、指定攻击、正常观察权限及队列显示。项目采用原创规则P与现有六文明，不宣称商业《帝国时代III》精确国家数值、完整国家制度或完整美术复刻。当前源码/构建已正式通过，五个本GUID原生进程的9个声明范围及正常16局独立核验已闭合；自然路线9/16达到、原状态observed_routes_incomplete，私有交付仍待验，见[072验证](Verification-072.md)。

## 本批内容与既有文明

072补强有限资源后的合法收入和失Town恢复、实际陆军集结/补员与经济线防守、当前可见中立守卫安全，以及正常人类对指定敌人/建筑下令。没有新增文明、免费经济来源、强升IV工厂、全图敌情或必胜终局。

| 既有文明 | 项目现有特点 | 本批关系 |
|---|---|---|
| British（coast） | Manor完成正常招工；长弓、火枪与轻骑组合 | 住房135W；经典革命与正常工人恢复规则保留，有限木供应与Town恢复按真实费用 |
| Dutch（river） | 金币训练工人、银行、矿采优势与较小工人上限；散兵/轻骑体系 | 住房100W；银行350F/350W可与木启动预算竞争，正常银行收入参与币缺口判断 |
| French | Coureur采集优势、胸甲与散兵，工人仍有真实费用/人口 | 住房100W；Estate/Factory/Trade必须正常取得，不用兵转换研究代替实际平民收入 |
| Ottomans | Town自动招工计时/上限、清真寺、耶尼切里与Abus，部分支援以货运取得 | 住房100W；自动招工仍需真实时间、出口、上限与人口，革命军事阶段按既有规则关闭新来源 |
| Wayfarer Cities | 脆弱付费货运路线与护送/车队兵种 | 住房100W；正常制造/贸易开销共同守核心预算，不能白拿车队收益 |
| Highland Guild | 工人投入workshop制造预制件与反炮兵，工程师/盾卫体系 | 住房100W；制造支出参与储备保护，仍需实际工人与正常配方 |

## 陆军真实集结、波次与防守

AI维护Muster、March、Defend三阶段。陆军候选必须活着、属于自己、非水面军、非驻防、非Explorer；革命补给守卫、已经Embark、伤兵撤回与文明支援保留其既有规则，不硬抢所有订单。

Home优先活Town（完工优先，同状态取较小ID）；无活Town才从当前安全自有Worker/Factory/Farm/Estate选择最接近原开局的救援点，最终回退开局位置。Town重建后动态跟随；不会永远留在已经被毁的开局镇。集结点必须当前可见、身体可走、容得下队列、当前可见军队/武装建筑安全，并从Home实际可达。生产建筑通过普通Rally命令设置真实旗，包括选artillery的Factory。

首次出发门槛：时代I/II为6、III为10、IV/V为14名真正到齐的兵。到齐不是只看直线距离：距stage<=10，同时直线可通或实际身体路线总长<=14；对岸、长墙后方虽近但绕路很长，不算已经集合。按稳定ID组成固定第一波，存在额外已到齐人员时先留至多2名守卫；刚好达到出发数时没有额外兵，不保证恒定2名守军。

March期间新兵先在stage等待。可用时补至2名留守；其余至少4名真正到齐后才作为一次补员加入wave，不把每名单兵即时送向远处军团。战损使波次少于max(3,初始规模/2)时清旧队列、回Muster。这里的清队列指planner成员计划，不退款/复活/改生产或军力。

当前可见非Explorer军事单位威胁自有Town、工人或经济建筑时，防守优先；完工武装敌楼只有真实自有资产处在武器射程内才触发回防。只有敌工人或远处敌楼不让全军撤回。威胁解除后重新集结，目标不能跳过防守或到齐门槛。没有防守时再用已知敌国记忆、搜索及地图公开目标决定去向；不读取隐藏敌军/位置。固定波次经普通组命令与formation，桥连通与运输分岸仍用公开陆军/海军调度，不传送过河。

这些门槛是策略条件，不是固定胜负、兵种平衡、所有路线无堵塞或每帧两兵留守保证。阶段/成员等决定下一步的状态进入AiSnapshot；同身份cold恢复保留波次、stage、目的地、计时和锚点。缺旧armyPlan DTO时在兼容状态解码内按确定Muster迁移，不提供071应用到072原生迁移。

## 指定攻击与操作权限

普通单机的本地席位属于人类，AI只处理配置的自动席位。正常鼠标右键可见敌兵/建筑产生指定Attack，A模式点地面为AttackMove；显式目标跟随移动目标及实际近战准备，HOLD/驻停自动攻击不为准备近战私自追远。权限、视野、伤害、路径与目标生死仍按World规则检查。人类选择的目标不因对手AI review而被覆盖。

Autoplay是自动观战，包括声明的正常或authored观察：人类可以选择、聚焦、看乘员/属性、移动相机，不能给部队、经济或政治下令；拒绝在指针和直接命令入口发生。HUD明确AI OBSERVATION及read-only，空选择不再暗示右键下令。工作坊Editor PLAYTEST保留人类控制并以1×开始，普通BEGIN SKIRMISH也明确退出观战。Replay继续原只读观看合同。

当前1567项完整回归含指定建筑/移动人物真实输入和伤害、观察权限及编辑试玩的4项Play用例；Focused21也实际通过这些实例。本GUID E实际用A模式与左键指定Guardian84、House83，由接受命令、实际伤害和Death分别闭合；G实际选择/聚焦、minimap移动镜头，观战A/S被输入入口忽略。E原图可见90s、50s、17s革命clock与60s政府clock。右键及移动工人追击仍只按其独立测试范围记录，不能扩成此次OS原生操作。见[选定原生审计](Evidence/072/Native/SelectedScopes/README.md)。

## 有限木材与真实核心费用

计划只调查自有及当前可见、安全可达来源；有限木量按真实费用需求提前判断，不从离线全图森林读取隐藏供应。完工银行与工厂真实模式/队列、当前合法货运/箱子、Post/Ledger配送及当前资源决定短缺，不把排队工厂收入当已取得。

Town500W、Farm300W、Estate500W、Dutch Bank350F/350W；英国Manor135W，其他文明房屋100W，预算使用World.EntityCost而非统一House135。Town活工缺镇时保护恢复预算。普通建造/训练/科研、文明制造与贸易、船坞/造船/维修及中立势力费用共同守核心储备；已经真付费且活着的Town地基只续施工，不锁第二个500W。紧急当前可见军事反制可释放非Town核心积蓄Food/Coin，仍保护Wood；Town恢复不采用此例外。

合法Wood贸易启动是Post200W加Ledger200W/150C的完整400W/150C依赖，Ledger45秒，Post60秒。不能因为单独付得起Bank350W而毁掉唯一完整启动。CoreBank/Estate与未付Post/Ledger竞争时，会明确暂缓必要Core消费，保护尚未付款的独立费用；已经付Post并不意味着Ledger免费。Town已付得起500W且存在合法工人/位置时仍优先Town。Town不足而有可完整支付收入启动时允许有界收入供给例外，不给普通非核心消费无条件放开500W。

尚未付款的政治愿望不能永锁必要收入设施，Core收入动作可有界释放相关政治积蓄；真正革命/政府研究依然全额付费、文明/时代/队列合法。800W/0C无木来源受控输入可以只付Estate并取得Coin，不能凭空凑900W双渠道；1300W控制输入才实际验证完整Estate/Post/Ledger链与正Wood。无工人、真实钱、时代、合法来源或安全普通路时，正常断档与败局保留。

Repair/DockRepair固定步按真实下一笔支出与并发累计守核心恢复费，停止本AI将动用储备的维修，不在两个review之间继续偷花恢复预算。普通人类订单与正常支付权限未被该AI预算变成强制人类钱包冻结。

## 工业与贸易的机会成本

Factory仍需经典文明合法IV工业货运、真实Factory Wagon和普通可见地基；零基础建造木价不意味着免费Factory（成本是货运机会/工厂车消耗及建设时间）。食/木/币资源模式与artillery互斥；研究暂停收入，切换重置造炮进度，满人口与出口仍约束实际产出。

Trading Post只能合法socket建造，Ledger研究或当前牌组合法科技货运完工后才解锁资源奖励。WOOD/COIN模式必须等路线车实际配送。原创Wayfarer的货物Caravan是在Town付货费、到waystation取得币的另一机制；被毁或Move/Stop会损失货，不混同Post的奖励。

经济完整链分别观察付款、完工、首个实际正收入；government完成、首工人、自然Gather恢复、付V与队列完成也分别记录。不能用全部Gather+Passive计数替代TradeDelivery/CaravanDelivered，否则会漏合法贸易收入。

## 可见守卫与实际施工路线

活着、当前可见且有武器的中立Camp Guardian进入独立经济安全列表，和当前敌国军人/完工武装建筑一起影响资源、施工、工厂/贸易站、捕鱼/码头调查与避难。隐藏、死亡、无武器或Explorer不影响这一判断。中立ID不入战略敌国memory，不能污染旧Restore敌国所有权校验；Army/搜索/反兵种目标仍敌国集合。

Safe半径仍max(12,range+4)，严格圆内部不安全、等半径边界安全。来源须实际Gatherable、当前视野、可调陆工和身体可达；有威胁只在普通ClearLine与全SegmentSafe共同证明时快速认可，其余保留filtered搜索。无威胁也不省身体路径/终点检查，不任意截断资源或导航节点。

有当前威胁时，TryConstructionWorker针对真实BuildPoint创建至多一次本查询局部导航副本，加入World.BuildingFootprintCells的精确未来地基（包括码头旋转/栈桥），已有地基用当前Nav。实际身体起点、+.85 WorkPoint/NearestWalkable、普通ClearLine/FindPath全线段须安全；只存在另一条filtered安全绕路不足以认可。实际EdgeDistance<=1.35已满足停止条件的合法builder可原地真付款/进度，无虚构外绕。预检不改liveWorld/Nav版本、事件、钱包或存档；码头普通Move调查也逐段核查。

这是review时的静态普通路预检。后续武器、动态盟友避让、fine recovery、detour与重算可能改变轨迹；不是全程护送或每帧安全约束。实际Gather依然用World普通WorkPoint/导航，安全可达来源的filtered路径没有被写入真实订单，不能声称所有Gather轨迹都通过相同安全路。该边界仍为后续内容/调度优化方向。

## 保存、调查复用与连通预检

单次同步Skirmish review只复用SourceSurvey木量/木可用/币可用，Begin在think gate之后，finally End清空。Time/EventCount/NavVersion与本helper和相关component的accepted Send显式失效；每个Evaluate重新计算当前钱包、义务、队列与Plan，公开subset/default调用不缓存，不新增跨Tick或存档DTO。不能以“所有World.Issue都必然发事件”为缓存正确性前提。

当前候选在每次实际SourceSurvey内复制当前可见EconomicThreat的危险圆；木调查与币可用性调查共享这一不可变几何，判定仍保留原Alive、派别/Guardian、当前视野、武器/Explorer条件和严格max(12,range+4)半径。它不从后续事件或隐藏敌情更新圈；相关事件/命令使下一次调查重新取得输入，圈及其过滤连通上下文不跨调查保留。

每次Survey按身体radius独立惰性建立过滤弱连通分量。`MayConnect=false`仅提前排除原过滤A*确实不可达的目标，`true`仍继续原FindPath和精确终点验证。保留普通ClearLine的现有快速认可、对角/岸角身体通行与原采样、全部13×13端点连接候选；不能只查最接近整格、随意截断候选或把方向性边当成双向可达授权。弱连通在任一边方向可通时联结，是保守排除条件；真实有向路线仍由原A*决定。调用只传原point Safe过滤，不偷偷给原A*增加连续Segment过滤，也不改原路径顺序或平滑。

上下文捕获Nav.Version及只在RestoreState递增的restoreGeneration。版本变化或真实恢复（包括snapshot恢复后Version恰好不变）时旧上下文返回true，回退原搜索；不能用旧负结论拒绝新开的通路。过滤上下文只在本调查/radius内使用，常规身体图缓存仍依其原半径/版本规则；没有新增保存DTO、跨事件过滤图、强制绕路或永久危险地图。

## 队列文字与真实刷新

生产队列clock矩形为(6,29,68,18)，完整容纳在80×47按钮内；长标题仍用68×26及9–12字号范围。Focused21的实际付费革命Play实例生成90s/88s字形并核按钮内边界、稳定付费队列与实际取消退款；不是只查text字符串。E新GUID原图实际显示初始Volunteer Assembly两行与90s，后来50s/17s，以及政府60s；不把后来所有标题或原生自动观战字号都说成已验。

队列沿用正常约1秒真实时间refreshAt节流与按住UI/左鼠标时的指针保护。17个0.1s模拟步不能替代1秒真实等待；测试等待正常1.05秒再正常更新/渲染，未强制Invalidate/Rebuild或绕过指针保护。这个合同允许按住/卡顿时更晚刷新，不承诺每模拟秒或绝对1秒内显示。追赶提示采用中性“Simulation is catching up.”，不把原观察卡顿归因于尚未证实的渲染原因。

## 行为与历史边界

当前完整1567及Focused21实际经济17、Army12、过滤连通10和相关导航/Play项均Passed。实际Build反例保留：filtered替代可走但实际普通路危险、预检无Live Nav/钱/事件污染、人类真付费后stored path危险、已有地基不再付款、合法近边距正常进度。隐藏Guardian→Reveal/真实Garrison/900步完整cold与不入memory、原river38个worker转换/完整cold强断言保留；新完整回归绑定04698与718输入，旧完整结果仍保持旧身份。

旧失败、终止收据及最初UPM连接/缺包编译探针保留。纯预算输入排除非测守卫、唯一树输入排除crate、工地反例初始Reveal均为声明受控条件，不证明自然地图缺NPC或AI可以透视。原NativeA卡顿、NativeC旧GUIDclock缺失与Focused19的实时刷新测试失败也保留；后续新证据按自己的源/范围记录。

071正常八局仅1局完整恢复、英国V已付但旧cap未完成、奥斯曼研究后立即终局未恢复经济均保留，旧071包/截图GUID不改。没有新增文明、船只轮廓或地图建模；这些内容沿用已经存在的实现，此后新增路线与美术修改仍需各自证据。

## 当前验收与版本身份

2026-10-06。当前运行提交 `bc127e02638caaeefd2310a1a3eff858f4f4cb2f`，718个运行输入，指纹 `04698dad49278a42db8a13a2ab997870bfb008c52f95313130026da5a7f35f62`。文档和原件归档不改变这些运行输入；自动化、原生与自然路线各自验收。

| 项目 | 实际状态 |
|---|---|
| 正式完整回归 | `run-HACRyTBF`：1158/1158 EditMode + 409/409 PlayMode = **1567/1567 Passed**，0Failed/Skipped/Inconclusive；Edit776.526388s、Play230.8869331s；独立 `full-regression072-final1.json` 为 `passed_frozen_full_regression` |
| 来源与旧用例 | 完整源码清单718项与04698指纹一致；071原1117Edit/404Play名称保留，新41Edit/5Play；保留名称不代表测试源从未修改，合法语义修正与历史失败另行保留 |
| 定向Focused21 | 51Edit+10Play全部通过、两进程exit0，718输入前后稳定；包括连通预检10项与真实付费革命倒计时字形/边界/刷新/取消 |
| 当前构建 | Unity6000.6.3f1，macOS arm64 Development/Mono，version0.15.0；BuildFinal1实际Succeeded/exit0、源前后稳定；GUID `3288d9fe24e844748a1183f756874fbf` |
| 当前构建原生 | **native_scopes_complete**：[最终journal](Evidence/072/Native/FinalJournal/native-journal072-final.json)9个声明范围/5个实际exit0/40对F9，pending=[]；指定攻击、政治/保存冷恢复/新工人、观战相机与H短段前向按各实际范围闭合。H仅step600→1111、Verified=false，D性能仅155.623s窗口 |
| 16局正常I矩阵 | **observed_routes_incomplete / Consistent**：`NormalRoutesFinal1`16条关闭、runner exit0/sourceStable；革命6/8、升V3/8达到，7条未达到。15自然终局+1cap，仅5局到2400边界；[实际原件](Evidence/072/NormalSelected/README.md)按32位玩家分别记录设施付款/完工/正收入、Town恢复与军力，未评估平衡 |
| 072私有交付 | **PENDING_DELIVERY**：对应新身份的源码/客户端/原始图集包、独立包核验及owner-only上传/权限/字节读回尚未闭合，不生成新Drive完成链接 |

正常恢复不是必成：15笔正常500W Town重建付款5笔完工、10笔未完工；6条达到革命路线仍不意味着三资源均恢复，Dutch42及WindingFrench44恢复后Wood=0。Dutch42 wall3303.190543s约55分3秒、模拟3600.061279s未自然终局；WindingOttoman44停留Age2/缺Coin，初始停止原因未证实，Market没有资源兑换。这些限制不因独立Consistent而移除。详细原件、SHA和范围见[072验证](Verification-072.md)。历史源47/a132、716输入的1556通过与GUID `f9912d56d5d04a198a5f495ea9e8eb6e` 构建仅属此前候选，不能替代当前原生。该旧批两条coast case（42提前自然终局；43已付V并完成）保留原身份，不当作当前16局结果。

## 固定锚点性能诊断

StepRun1/2各从原NativeA同一锚点与完整事件前缀实际续跑250步并exit0。gross step565254.4585→3918.2488ms、最慢单步72990.4258→276.0642ms；独立285checks/0fail，最终World/Controller/新事件原字节相等，finalEvents均62633、digest均 `59d3818268c962b1584cc4d9530168778ad45ca38604c60f7c5828aca9b4d75f`。

这是带计时Editor诊断，不是新GUID GUI/Release帧率、全部自然地图或长期性能。候选与正式测试并发，编译/缓存/CPU与文件写入影响墙钟，嵌套计时不能相加；旧NativeA71.48秒卡顿与未采帧尾部保持原件。见[诊断范围](Verification-072.md)。
