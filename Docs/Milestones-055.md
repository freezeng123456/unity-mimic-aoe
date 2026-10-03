# 四个原定里程碑验收 — 0.2.31

原定范围以 [Development-Contract.md](Development-Contract.md) 的 A–D 为准。跨机器签名、公证、海战、联网、战役与编辑器是后续范围，不能倒推为四个里程碑的新退出条件。截至2026-10-03本次更新，A/B/D已通过，C1–C6失败已归档；C4严格16/17检查、16/19覆盖，仅缺IV/V/特殊兵种，C5已1661.541382秒自然败局，严格16/17检查、18/19覆盖，仅缺 `ageV`；[C6](Evidence/055/NormalPlayer/river-attempt-006/README.md)已1367.213257秒自然败局，严格16/17检查、17/19覆盖，仅缺 `ageIV`、`ageV`；C7正由river_strategy操作GUI，尚未判定；最终0.2.32尚未启动；不能因为源码存在、测试通过或一局已开局而写成全部验收。

| 里程碑 | 原定退出条件 | 本次验收状态 |
|---|---|---|
| A | 独立 Unity/URP，镜头、选择、命令、建造、人口、生产、采集、基础战斗、群体导航；至少一次原生检查 | 通过。固定Unity/URP工程、424项全量回归和同版独立构建；0.2.31原生已实际采集、建造、人口/队列、II升级、付费步兵、交战、修理与驻军/释放。群体导航自动回归有源码绑定，不推导全部实战组合。 |
| B | coast镜像1V1、I–III、合法卡组/XP/真实船运、可见性、AI、菜单至结算；原生整局 | **通过。** B10经济25卡正常1x、实际III、3付费工/14步兵/2炮/4船运；付费炮186六次命中敌马厩156，总780伤害。958.6130秒自然败局、败方全灭，17项严格检查全部通过。失败/中断历史保持原结果。 |
| C | 第二文明、I–V、科技/特色单位/银行差异、贸易/宝藏、攻城及AI | **进行中。** C1/C2/C3/C4/C5/C6均自然败局并完整归档，分别保留六项/十项/一项/三项/一项/两项必需覆盖缺口。C3实际完成I–V、银行、特色单位、科技、贸易和宝藏，19项覆盖通过18项，仅缺炮击敌建筑。C4严格16/17检查、16/19覆盖，付费炮击建筑与Cargo Ledgers成立，仅缺IV/V/特殊兵种。C5已1661.541382秒自然败局，严格16/17检查、18/19覆盖，仅缺 `ageV`；[C6](Evidence/055/NormalPlayer/river-attempt-006/README.md)已1367.213257秒自然败局，严格16/17检查、17/19覆盖，仅缺 `ageIV`、`ageV`；C7正由river_strategy操作GUI，尚未判定；最终0.2.32尚未启动；不跨局拼接覆盖。 |
| D | 正式原创美术、动画、音频、更广规模验证 | **通过。** 原创模型/程序动画/通知/风声/主题曲、统一424项Unity测试及真实正常/暂停/主静音各20秒混音已有证据。50/100/200/400活动单位四档全部核验通过，400 attempt-002单窗317.674076有效秒；B9正常单窗335.999332有效秒也已最终关联通过，两项各自满足300秒门槛，见[性能实测](Performance-055.md)。不推导全400无遮挡同屏或固定帧率。 |

## 已保留的正常玩家对局

第三次对局 `91311172a2df4d3987ff11860a1a08fc` 使用冻结0.2.30，build GUID `21261224be4f40e692a3629fc4cc42fe`，coast镜像、Easy、上限III、普通初始资源、1倍速、autoplay=false。754.1629模拟秒自然失败，玩家II/AI III，玩家两次船运、12击杀/23损失；已保存菜单至结算操作证据、完整事件及文件SHA，见 `Evidence/055/NormalPlayer/attempt-003`。玩家起步资源分配与长时间驻军造成经济停顿；此结果不被包装为III时代/攻城通过，也不因此给玩家免费资源或削弱AI。

0.2.31首个正常coast局`9f0d7b0c338243b4a3c57107db5e1019`自然失败534.2166秒，玩家II、一次船运、5击杀/16损失。事件/菜单截图/结算归档在`Evidence/055/NormalPlayer/coast-attempt-001`；独立解析身份、正常开局、付款时序、船运与最终淘汰检查通过，但玩家覆盖明确缺III、付费军队/炮及炮击建筑。窗口失焦自动暂停、全屏状态下工具返回noWindowsAvailable在实际操作中出现；标准Window菜单恢复窗口模式后工具坐标点击恢复，不能据此推断普通鼠标有同样问题。第二局也未达到III，不记为通过。

## 0.2.31验证方法

源代码测试绑定统一文件指纹，随后构建同版本原生客户端并记录GUID。图形研究图、真实客户端截图、原生混音WAV、正常玩家事件、AI矩阵、短期压力夹具及长期逐帧样本分别归档；它们的证明范围不同。F8只观察渲染帧和内存、F10只读取AudioListener混音，两者均不修改游戏规则。正常对局中的暂停和焦点变化明确保留，性能统计只使用有焦点且未暂停的有效帧。

0.2.31第二个正常coast局`0d281a3fa942437fabb6798fe2acf664`自然失败662.7852秒，玩家II/AI III；真实付费军营和5长枪兵完成。普通开局、逐笔支付/生产时序、自然完全淘汰通过；自身III、付费工人/炮、炮击敌建筑和船运仍缺失。见`Evidence/055/NormalPlayer/coast-attempt-002`。Play Again沿用B1菜单配置，没有独立新配置图或该局结算F9，不用其他对局图替代。

0.2.31第三个正常coast局`62dbc87afc3d4296a932fd41997856aa`自然失败810.9490秒，自身II/AI III；2付费村民、5Pike+5Musk、2Manor/Barracks与3次船运全部真实完成。780.0566秒缴费III，791.6537秒TC毁，升级中断；严格拒将缴费当达到III。三个玩家覆盖缺口仅实际III、付费炮、炮击敌建筑；独立菜单/开局/同局Defeat F9及原始账本见`Evidence/055/NormalPlayer/coast-attempt-003`。

0.2.31第五局`145add218156473c9a2872545115abe3`已自然结算，双方III，1073.2850秒败方全部消灭（末局存活单位/建筑0），55击杀/82损失。两付费炮与另1船运炮有真实射击及敌单位伤害，但敌建筑炮击0；独立严格验收仅缺`cannonEnemyBuildingDamage`，不标记B通过。12组同局F9/军备菜单与结算图、支付与命令链见[归档](Evidence/055/NormalPlayer/coast-attempt-005/README.md)。同局F8完整关联有效197.825666秒仍不足300秒，不标记长期性能通过。

第六局`d57aee12243c4599a9343986d5b2c1ab`按中断保留，见[完整归档](Evidence/055/NormalPlayer/coast-attempt-006/README.md)。旧日志最后705.3748秒手动暂停后失焦，恢复时sky新开客户端回菜单；旧进程退出原因未证实。四组F9中的最后664.8847秒为II、III排队，没有任何同局match_finished/menu_exit/quit或完整events，不认证III完成/自然战败。20文件2,821,359字节、13份按字节原生来源及4组配对封存。F8有效258.709919秒仍不足300且无最终账本配对。当时第七局已开始的观察作为历史保留；其最终结果见下，不把这段过程记录当作验收。

第七局`781c4090a2474a91964105c79eab9e27`已835.6430秒自然败局，双方III、败方单位/建筑全0，无Resign。6付费村民、10Pike+5Musk、1付费炮、4船运与实际III成立，严格[摘要](Evidence/055/NormalPlayer/coast-attempt-007/summary.json)仅缺`cannonEnemyBuildingDamage`，不标B通过。两份同局完整配对F8有效259.774107/135.600574秒，均不足300，不标正常长性能完成。

第八局`6cf241946653492ea782b2f968b5e229`已[归档](Evidence/055/NormalPlayer/coast-attempt-008/README.md)：首次实际暂停223.6044秒，己方Command/Gather均0；三次付费村民排队仅1名完成出生、2名仍在队列。259.6066秒menu_exit为主动中断，不是自然结算或B通过。第九局采用恢复/关闭面板后700毫秒间隔，最终974.5091秒自然败局；[严格审计](Evidence/055/NormalPlayer/coast-attempt-009/summary.json)仅缺付费炮出生及炮击敌建筑。B10最终完整通过，见下；River C继续独立正常玩家路线。

第十局 `f65f503a74b9415e89d0a875e9b0ce2d` 使用合法Economy25，正常1x、coast镜像/III上限。958.612976秒自然败局，败方单位与建筑均0；[严格审核](Evidence/055/NormalPlayer/coast-attempt-010/summary.json)17项检查全部true、coverageGaps为空，**B通过**。3付费村民、9Musk+5Pike、2付费炮、4船运均有付款/出生关联；炮186在580.5053–605.9991秒对敌马厩156六次造成各130攻城伤害。自然战败如实记录，不声明本局获胜或统计平衡。

## C与D的本次收口记录

C1 `1498f3a84f374c0498900c25a6177872` 在1486.584106秒自然败局、败方全灭、无Resign；[严格审计](Evidence/055/NormalPlayer/river-attempt-001/summary.json)17项中16项成立，requiredPlayerCoverage失败，六个缺口为 `ageIV`、`ageV`、`paidTradePost`、`tradeXP`、`resourceMode`、`resourceTradeDelivery`。银行、付费独有兵/科技、两宝藏及付费炮击敌建筑已有证据。C1失败结果保持不变。

C2 `0b725a5116d2415f855f4443c4023e0e` 为River对Coast、Easy、V上限、Military25，1081.682983秒自然败局、败方全灭、无Resign。[严格审计](Evidence/055/NormalPlayer/river-attempt-002/summary.json)仍为16/17，十缺口为 `paidCannon`、`cannonEnemyBuildingDamage`、`ageIV`、`ageV`、`specialUnit`、`paidTechnology`、`paidTradePost`、`tradeXP`、`resourceMode`、`resourceTradeDelivery`。IV付款后仅47.488464秒TC被毁，不足130秒；Cannon Support属于船运，不能计为付费炮。39文件完整归档经SHA复核，原始失败结果保持不变。

C3 `8fb300efbc1a4169a953d9dab2b7caad` 为River镜像、Easy、V上限、Military25，1781.212158秒自然败局、败方全灭、无Resign。[严格审计](Evidence/055/NormalPlayer/river-attempt-003/summary.json)16/17检查与18/19覆盖成立，仅缺 `cannonEnemyBuildingDamage`。II/III/IV/V均实际完成；19付费工、15Musk、20Pike、8炮、10Carabineer及4银行、付费贸易站/科技均有付款和完成链。73文件、20组F9、两份冻结日志、90,759条事件已归档并通过SHA复核。C3失败结论保持不变。

C4 `b4250e2ab6664083ad919de284cc0afb` 为River对Coast、Easy、V上限、Military25，994.504211秒自然败局、败方全灭、无Resign。[严格审计](Evidence/055/NormalPlayer/river-attempt-004/summary.json)16/17检查与16/19覆盖成立，仅缺 `ageIV`、`ageV`、`specialUnit`。付费炮238在800.651550–826.145325秒对敌Manor156六次各130伤害，Cargo Ledgers于389.014526秒完成；不声称Musketeer升级。65文件、16组F9、两份冻结日志和36,218条事件已保全并校验SHA。C5已1661.541382秒自然败局，严格16/17检查、18/19覆盖，仅缺 `ageV`；[C6](Evidence/055/NormalPlayer/river-attempt-006/README.md)已1367.213257秒自然败局，严格16/17检查、17/19覆盖，仅缺 `ageIV`、`ageV`；C7正由river_strategy操作GUI，尚未判定；最终0.2.32尚未启动；不与C3或其他局拼接成C通过。

D的50/100/200短档与400长档均在同版单客户端、无并行Editor下完成真实GUI F8、自动F9及正常menu_exit/QUIT，原始事件与单位/内存计数、版本/GUID/源码/runtime核验通过。400 attempt-002单窗317.674076有效秒，原始13,153帧全部有效/有焦点；首次attempt-001仅182.717415有效秒的失焦失败完整保留。正常玩家B9单335.999332有效秒另行通过，不用正常局代替压力夹具，也不把多个窗口相加。数据和边界见[Performance-055.md](Performance-055.md)。

C5 `d69b5316feda422f8a27d23545e13c5b` 为River镜像/Easy/V/Military25，1661.541382秒自然败局。[严格审计](Evidence/055/NormalPlayer/river-attempt-005/summary.json)16/17检查、18/19覆盖，仅缺 `ageV`，V未付费。付费炮261对敌Foundry239两次130伤害，同一老Musk173前后与科技后新生Musk256的输入/出生/死亡/像素关联均通过。67文件、17组F9及79,417事件已完整归档；[C6](Evidence/055/NormalPlayer/river-attempt-006/README.md)已1367.213257秒自然败局，严格16/17检查、17/19覆盖，仅缺 `ageIV`、`ageV`；C7正由river_strategy操作GUI，尚未判定；最终0.2.32尚未启动，不将C3达到V与C5炮击合并成通过。

C6 `7558b19fc9984578a313082b633c6630` 为River镜像/Easy/V/Military25，1367.213257秒自然败局。[严格审计](Evidence/055/NormalPlayer/river-attempt-006/summary.json)16/17检查、17/19覆盖，仅缺 `ageIV`、`ageV`；IV付款后TC毁、未完成，V未付款。付费炮256对敌Farm266三次130伤害共390，同一老Musk162升级前后与新生Musk263科技关联均通过。63文件、11组F9、双冻结日志及60,226事件完整归档并经独立SHA/strict复核；helper首次FP比较错误保留，纠正及独立357源码/3runtime审计确认冻结身份不变。C1–C6覆盖缺口依次6/10/1/3/1/2项；C7由river_strategy操作GUI进行中，尚未判定；最终0.2.32尚未启动，不跨局合并验收。
