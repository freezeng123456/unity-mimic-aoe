# 烽岸纪元 · Frontier Reckoning

独立 Unity 即时战略游戏：英国、荷兰、法国、奥斯曼四个经典文明核心玩法，加上远驿盟市、山岭工联两个原创文明，共六文明。五个时代、25张卡组与XP船运、银行、实车贸易、工坊制造、守卫宝藏和炮兵攻城，配有原创模型、动画与音频。使用本项目规则和候选数值，与 Age of Empires 及其发行商无关联。

**当前源码版本为 0.14.0，目标平台 macOS Apple silicon。** 英国、荷兰、法国、奥斯曼新增工业时代革命与平民重建：真实付费、90 秒转换工人、四张革命卡与 60 秒经济恢复；升 V 与革命永久互斥，两个原创文明沿用已有路线。新增内容使用本项目规则 P，并非商业游戏的完整国家制度或精确数值复刻。[操作指南](Docs/Player-Guide-071.md)、[内容合同](Docs/Content-071.md)。

运行提交 `3aeb0cb4b5f63ecd18553254583726b88cef9079`，704 输入，指纹 `098e5c97eb8e1a3f5cff1e0dd7db30a7fe761fc8f480c6b8517811fab5d8795a`。正式完整回归 `run-RkVr6ujs` 的 1117 EditMode 与 404 PlayMode，共 **1521/1521** 通过，失败、跳过和未决均为零；独立 25/25 检查核对实际叶结果、源码和 070 所有旧用例。Mac 构建成功，GUID `2d39f78f9f234ef092e172a35f800cae`。[正式测试原件](Docs/Evidence/071/Tests/README.md)、[当前状态](Docs/Completion-Status.md)、[具体限制](Docs/Known-Issues.md)。

普通时代 I 的八条路线已按真实开局资金、六工人和自然战争观察，**尚未全部达成**。英国革命局完成付费革命、研究、真实招工和自然资源收入；英国升 V 局已支付 4000 食物/4000 金币，2400 秒截止时研究仍在队列。荷兰与法国四局先自然结束；奥斯曼革命研究完成后马上自然结束，尚未产生新工人和采集收入，升 V 局也先自然结束。验收分别记录承诺、完成、经济恢复和胜负，不把未达成改写成通过。

**本构建的受控原生范围已验收。** 英国时代 IV 样本实际完成付款/取消、革命、平民重建、100F 招工、采箱收枪与停止复枪、普通砍树收入和遣散；独立收据为 `passed_observed_native_scope`，15 个必需范围无缺项或错误。8 个自有进程正常退出 0，46 对原始 F9 PNG/JSON、3 个存档、3 段录像、2 次冷恢复已核验；三个新观看进程分别完整前向播放 0→904、904→2441、2441→5118 步，均无 seek 并显示记录段已验证。[071 验证](Docs/Verification-071.md)、[最终原生收据](Docs/Evidence/071/Native/independent-native071-final.json)与[实际操作记录](Docs/Evidence/071/Native/native-journal071-final.json)。

原生编组 1 在革命完成后用 SAVE1 建立，再跨重建队列冷恢复召回；转换前编组跨革命与已打开母城跨转换自动切卡是自动化覆盖，本次没有实机观察。受控 IV 样本不代替普通 I 时代完整经营、其他三国原生整链或长期性能。**071 私有交付已完成**：当前源码、Mac客户端、实际图集与八局原文补充包已上传原私有Drive；九文件及目录的字节/父目录/仅owner权限全部读回。见[交付回执](Docs/Delivery-071.md)。

070 三章通关、侦察检查点恢复及 [0.13 私有 Google Drive](https://drive.google.com/drive/folders/13SGjI5BVUbaT3pRwjOSOn1gIMcEaKQ7J) 保持旧 GUID 与旧包身份，见 [070 完成快照](Docs/Completion-Status-070.md)和[070 交付](Docs/Delivery-070.md)。

此前0.12工作坊、冻结场景存档/录像与可信LAN仍保留。[工作坊操作](Docs/Scenario-Editor-069.md)、[旧容量合同](Docs/Scenario-069-Contract.md)。历史0.12候选2的1286项、GUID4546189b、原生编辑/冷恢复/录像和私有三包保持其旧身份，见[069验证](Docs/Verification-069.md)和[069交付](Docs/Delivery-069.md)。

以下0.11及更早结果保留原版本身份。

[0.12私有Google云盘交付](https://drive.google.com/drive/folders/14fBcWdSSX5L1FAul8h0IGlH1xpY_Bw55)含Mac客户端、源码、实际原生图集、可导入曲河任务及核验清单；七对象名称/字节/父目录和owner权限已读回，本地包1,475成员独立核验通过，远程内容SHA未重算。详细冻结身份见[069交付](Docs/Delivery-069.md)。

**历史 0.11.0 可信 LAN 合作**：房主 P1 与远端 P3 同队，P2/P4 AI 只在房主运行。两端指令由房主验证并按序执行，客户端复制状态而不运行第二套模拟；生产取消使用稳定任务身份，未确认请求重连重试且防止重复付费。操作见[两人合作指南](Docs/Player-Guide-068.md)，机制与范围见[LAN 实现](Docs/LAN-068.md)。该版完整自动化 **1,185/1,185** 通过（817 EditMode + 368 PlayMode，`run-Z47ikuJY`）；运行提交 `dca073708b671c60a1fe9ff243a67e4ace3fe157`，630 输入指纹 `62240c2373c773b5aaf807c72815acab2e76577504f3a0569218702864730ebe`。[历史正式测试](Docs/Evidence/068/Tests/README.md)保留实际 XML、源码清单和独立复核。

0.11最终原生 GUID `fa26339934914cd2813df9763de6bba7` 已完成**同一台 Mac 两个独立进程的真实 TCP 合作操作**：双方准备/开局、法国连续训练及第二项取消退款、英国训练、远端移动、本地菜单和共享暂停、18 秒客户端挂起后的房主离线恢复/重连、退出及房主保存录制段，已独立核对保存容器和4442条退出事件账本。两端暂停状态摘要对齐，两个进程正常退出0。[历史原生证据](Docs/Evidence/068/Native/README.md)与[0.11完成快照](Docs/Completion-Status-068.md)分别记录 UI、元数据、日志及录制段。两台物理 Mac、真实网卡/防火墙链路、自然完整比赛、该段原生冷回放和该版性能仍待验证。

历史0.11源码、macOS arm64客户端和该版原生图集已冻结并上传至[原私有Google Drive项目的0.11目录](https://drive.google.com/drive/folders/1K_xqcyeJAc87F6DJpmDb0CNflM0PtDcC)，独立核验1426个成员和云端七对象元数据/权限通过。[0.11交付说明](Docs/Delivery-068.md)记录源码快照与运行提交、完整哈希及实际范围。

以下 0.10 结果保留原构建身份，不扩大为当前版验收。

**最终 GUID `2d94fdd6d9734b259f5231c1fd3f2b99` 已通过手动导出片段 `bef1332be33c4e38b0594b3e4bf8eb13` 的新进程冷启动、从第 0 步完整前向播放至第 5622 步（9:22 模拟时间），并显示 `RECORDED SEGMENT VERIFIED`。** 接受的 `Train villager` 和无选取单位而拒绝的 `Stop` 均被重现；四观察位分别为 `coast=British`、`river=Dutch`、`france=French`、`ottoman=Ottomans`。原生 ±30 秒、中点跳转与时间轴填充/手柄修复已核验。该 `manual_save` 片段没有自然终局含义；坏文件错误提示及原世界保留、个人存储和观看输出字节核对已通过；范围见[最终原生证据](Docs/Evidence/067/Native/README.md)。长窗性能未验收，最终三包及私有云盘七对象已通过本地核验和元数据读回，见[0.10交付](Docs/Delivery-067.md)。

旧 GUID `2158deff98354bc981f074397534ad74` 的 35:50 片段保留在[候选 1 诊断](Docs/Evidence/067/Diagnostics/FrozenCandidate1/Native/README.md)，不作为最终构建的原生证明。

0.9 的本地团队、课程、战役和持久设置保留在[内容说明](Docs/Content-066.md)、[玩家指南](Docs/Player-Guide-066.md)及[完成快照](Docs/Completion-Status-066.md)。该版 1,062 项测试、GUID `1969c7ee14f944879510f4ca0d0f241d` 的代表性原生操作、冷恢复和已上传三包见[066索引](Docs/Evidence/066/README.md)与[交付说明](Docs/Delivery-066.md)，不重标为 0.10 结果。

0.8的958项测试、生成地图、海军运输与有界压缩存档，以及原生长局冷恢复/两乘员登陆记录，保留在[0.8状态快照](Docs/Completion-Status-before-066.md)、[正常冷恢复](Docs/Evidence/065/Native/Normal/README.md)和[受控乘员证据](Docs/Evidence/065/Native/Fleet/README.md)，不重标为0.9结果。已交付0.7的工业、三槽存读档、796项测试与原生证据见[0.7完成快照](Docs/Completion-Status-064.md)和[0.7交付索引](Docs/Delivery-064.md)。机制说明仍可参考[经典工业](Docs/Classic-Industry-064.md)、[存档与续局](Docs/Save-Load-064.md)、[地图与海军内容](Docs/Naval-Content-065.md)；[当前 AoE III 差距路线](Docs/AoE3-Gap-Roadmap.md)区分内容实现与各版验收。

0.6.0 新增蜿蜒河谷、热带河湾、支流汇合、河口四张地图，连同旧陆图与直河图共六张固定布局。水域、岸高、斜桥、港位、鱼点与 AI 航路统一使用 MapLayout；河岸采用连续岸坡与材质混合，港区有岸仓与伸水栈桥，船有厚船舷、船员工作动作和尾流。实现见[曲河玩法](Docs/Rivers-063.md)，该版测试、来源指纹、机动与原生记录见[0.6.0 完成状态](Docs/Completion-Status-063.md)和[063证据](Docs/Evidence/063/README.md)。当前候选进度见[完成状态](Docs/Completion-Status.md)，旧版验收保留原版本身份。

## 启动与操作

主菜单 **SCENARIO WORKSHOP** 打开地图工作坊。**NEW** 展开模板，左侧页面选择编辑类别，右侧属性修改具体数据；**VALIDATE** 检查地形、引用和时代上限，**PLAYTEST** 运行独立场景副本。试玩暂停或结算后 **RETURN TO EDITOR** 返回草稿；**SAVE / SAVE AS** 写地图库，**IMPORT / EXPORT** 使用便携场景文件。撤销历史属于当前会话，跨进程恢复的是文档内容；详见[0.12操作说明](Docs/Scenario-Editor-069.md)。

两人合作从主菜单 **LAN CO-OP** 进入，房主 **HOST CO-OP**，朋友填写房主局域网地址和端口 **JOIN HOST**；两端 **READY** 后房主 **START CO-OP**。LAN 的 Esc/失焦只打开本地菜单；**PAUSE MATCH / RESUME MATCH** 共享暂停。离线时等待完整重同步，客户端不自行续局；房主仍可恢复共享暂停。见[0.11指南](Docs/Player-Guide-068.md)。

0.10 录像操作见[玩家指南](Docs/Player-Guide-067.md)：正常开局、课程和检查点恢复会自动创建录制段；暂停菜单 **SAVE REPLAY** 保存当前片段并继续录制，主菜单或暂停菜单 **RECORDING LIBRARY** 打开后默认暂停。**PLAY / PAUSE**、**0.5× / 1× / 2× / 4×**、**−30s / +30s** 与时间轴控制播放；**VIEW P1–P4** 仅切换已有观察槽位。镜头、选取、资源与迷雾跟随该槽位，观看不会下达命令、保存检查点或写入任务完成记录。上述最终构建已核验手动导出、冷启动完整播放和代表性跳转，更多课程、长片段与操作组合仍需分别验收。

初次可从 **GUIDED LESSONS** 学习，**RIVER CAMPAIGN** 进入原创三章，**SETTINGS** 调整音频、可访问性和改键。基础操作沿用[0.9指南](Docs/Player-Guide-066.md)，其设置/单课/团队冷恢复证据仅属于对应旧构建。

历史[0.9私有交付目录](https://drive.google.com/drive/folders/1wzczmUUktYSDSn92KuwpUQ4dZkIK-h-3)提供源码、macOS客户端和原生图集；源码快照为`e75407c78522374ed7e327a1ed1727212d27ffd3`，该版运行代码为`c9747695d91fa203b05116a1fc0c15a8aac32de2`，上传后三ZIP未重建。包外回执见[Delivery-066](Docs/Delivery-066.md)，私有开发分支推送及草稿PR说明读回已通过；[远端回执](Docs/Evidence/066/Delivery/github-delivery-readback.json)绑定首轮交付提交`72f84c8`，后续仅追加包外文档，最末提交以PR当前head为准。

历史客户端下载和核验见[0.7交付索引](Docs/Delivery-064.md)与[0.8交付说明](Docs/Delivery-065.md)所列包外回执。0.7客户端只有六张固定地图；当前源码菜单有九图循环、**MAP, SEED & OBJECTIVES** 和本地槽位配置，以及后来新增的录像、合作和工作坊，须使用对应版本客户端体验。

源码通过 Unity Hub 使用 **Unity 6000.6.3f1** 打开；主场景为 `Assets/Frontier/Scenes/FrontierSkirmish.unity`。自由对战中选择文明、时代上限、难度、地图、牌组和模式，再点击 **BEGIN SKIRMISH**；2v2槽1+3同队、槽2+4同队，玩家只控制槽1。历史[0.8指南](Docs/Playtest-065.md)、[0.7指南](Docs/Playtest-064.md)和[0.6指南](Docs/Playtest-063.md)保留给对应版本。

| 操作 | 功能 |
|---|---|
| 左键 / 框选 / 双击 | 选择对象 / 多选 / 选中画面内同类 |
| 右键 | 按目标移动、攻击、采集、施工或修理 |
| T / V / M / F | 城镇中心 / 空闲村民 / 逐个军队单位 / 聚焦选择 |
| B / H | 村民建造 / 主城船运 |
| R后左键 | 显式修理；用于工坊维修或工程兵修炮 |
| A后左键 / J / S | 攻击移动或攻击 / 原地守卫 / 停止 |
| G后点己方建筑 | 驻军；选建筑点击 UNGARRISON 撤出 |
| E / U / K；BOARD SHIP / UNLOAD / HARBOR REPAIR | 陆军岸边会合登船 / 指定岸段卸载 / 舰船靠己方码头付费维修 |
| MAP, SEED & OBJECTIVES / SETTLEMENT | 选择地图种子与目标 / 接触中立据点后缔约、招募和研究 |
| 模式按钮 / LOCAL PLAYERS & TEAMS | 1v1、2v2或四方FFA / 配置各槽文明 |
| GUIDED LESSONS / RIVER CAMPAIGN / SETTINGS | 七课 / 三章原创战役 / 持久音频、可访问性与20动作改键 |
| SCENARIO WORKSHOP / VALIDATE / PLAYTEST | 自定义地图与任务编辑 / 检查实际开局 / 运行独立副本 |
| UNDO / REDO / PREV DRAFT / DRAFT HISTORY | 整笔撤销/重做 / 恢复当前会话中的此前草稿 |
| LAN CO-OP / HOST CO-OP / JOIN HOST / READY | 两真人合作对抗房主 AI / 建房 / 加入 / 准备 |
| SAVE REPLAY / RECORDING LIBRARY | 保存录制片段并继续游戏 / 在原构建中打开录像 |
| VIEW P1–P4 / SPEED / TIMELINE | 切换观察槽位 / 调整播放速度 / 跳到记录区间中的位置 |
| REVOLUTION / CIVILIAN GOVERNMENT / DISBAND | IV 时代政治预览与真实付费 / 恢复正常工人来源 / 确认后遣散陆地单位释放人口 |
| Ctrl或Command + 数字 / 数字 | 保存编组 / 召回编组 |
| Esc | 取消操作、关闭面板或暂停；以可见暂停界面为准 |

经典四文明的革命需要五时代比赛与时代 IV：800 食物/800 木材/800 金币，90 秒完成；平民重建另付 400 食物/400 金币，60 秒完成。革命军只能采己方补给箱，恢复后的新工人才能重新采自然资源。

开局先采集资源箱，再维持食物、木材与钱币收入。箱子和时代奖励须由工人实际采集；同一资源点最多8名有效工人。荷兰银行完工后持续产币，主城船运需要XP机会和运输时间。奥斯曼城镇中心显示免费招工进度、工人上限和阻塞原因；**AUTO SETTLERS: ON/OFF** 控制暂停与恢复。炮兵优先框选并配备护卫；驻军工人不会采集，安全后应及时撤出。

单机局失焦会自动暂停，回来后手动恢复。LAN 失焦只打开本地菜单，世界保持共享暂停状态；普通存读档入口须先离开 LAN。**SAVE MATCH** 写三个手动检查点，主菜单 **LOAD SAVED MATCH** 或暂停菜单 **LOAD MATCH** 读取后先暂停。检查点包含AI、迷雾、队列、工厂、镜头、编组、乘员、完整事件及可选任务尝试；自定义对局还包含场景文档、内容哈希和触发器/目标状态。选择已有槽会替换该检查点。外层封装仍为schema 2 gzip-base64，文件限64 MiB、解压原文限256 MiB，失败保留旧槽。读档要求应用版本和规则一致，不提供旧应用版本到 0.14.0 的迁移。任务完成/解锁另存于本地Account，音频和改键另存于Preferences，读取检查点不回滚它们。录像另存于 `Replays`，与检查点分别保存；要求应用、原生 build GUID、Unity、规则、生成与场景运行版本等身份匹配。同为当前版本号的重新构建也可能拒绝旧录像。F8/F9/F10是保留的诊断采集键；默认动作和改键冲突规则见[指南](Docs/Player-Guide-066.md)。

## 六文明玩法

| 文明 | 经济约束 | 特色部队 |
|---|---|---|
| 英国 British | 庄园完工赠村民，木材投入兼顾住房和劳动力 | Longbowman；保留火枪手和骠骑兵 |
| 荷兰 Dutch | 钱币村民、50工人上限、银行收入、矿山采币加速 | II时代散兵；III时代Ruyter和Halberdier |
| 法国 French | 120食物猎民，采集快25%，工人上限80 | Coureur des Bois；III时代范围近战Cuirassier |
| 奥斯曼 Ottomans | 免费自动招工，初始55秒与25工人上限；清真寺付费研究扩展 | Janissary、II时代炮厂Abus、仅船运Spahi |
| 远驿盟市 | 商车每次先支付60食物，到达己方驿馆才收入钱币；护商和拦截产生实际经济风险 | 护商卫士、道路骑哨、车队火铳手 |
| 山岭工联 | 村民放弃采集并在工坊制造；预制件加速付费建筑施工 | 野战工兵、工联猎炮、盾枪卫士 |

一套卡组最多25张，合法卡按文明与单位/科技限制筛选；经济、军事、科技三套预设可直接选择。时代II–V每次三选一，共72个文明晋级选项。当前规则共有 67 个实体、220 项科技、169 张卡定义及四个革命分支；其中包含资源、建筑、升级等级和路线条目，不能将条目数当作同等数量的独立系统。

本项目实现经典文明的核心机制，不等于《帝国时代 III》的完整国家内容或商业游戏内容量。工业时代两张工厂马车卡、真实马车移动建造、资源或重炮生产模式已接入；英国工厂生产火箭，法国/荷兰生产重型加农炮，奥斯曼生产巨炮。其数值使用已标明的本项目规则。四个经典文明已有项目版革命和平民重建；荷兰 Envoy、法国 Native Scout、完整政治家与商业革命国家制度、全套皇家卫队和治疗、完整官方卡池仍未实现。当前有六张固定地图、三个生成模板、三个原创盟友、三种地图目标，以及七课、原三章和另一条三章远征链；0.12已新增原创地图/任务编辑器，但没有完整商业中立势力系统、商业场景导入、任意脚本或在线地图市场，数值未经广泛对战平衡验证。

## 历史验收与版本来源

| 范围 | 已验证结果 |
|---|---|
| A：基础RTS | Unity/URP独立工程、选择与命令、经济、建造、生产、战斗、修理及驻军；原生运行与导航回归覆盖 |
| B：海岸I–III整局 | B10严格17/17；付费造炮、真实船运与炮击敌建筑成立，958.612976秒自然战败 |
| C：河谷I–V整局 | C7同局17/17检查、19/19覆盖；科技作用于老兵和新兵、银行、特色兵、贸易、宝藏及攻城成立，2528.415771秒自然获胜 |
| D：原创视听与规模 | 原创美术和程序动画；正常/暂停/主静音各20秒实际混音；50/100/200/400活动单位验证，正常与400长窗分别达到300有效秒 |
| 0.2.32独立验证 | 精确两文件菜单/版本差异、其余355文件一致；新214项EditMode与210项PlayMode全部通过；新客户端菜单→普通开局→采集→菜单→退出及完整ZIP核验 |

A–D完整实战、音频与性能属于冻结 **0.2.31**，GUID `128a731d440e4b7a94abfcc74b88fa97`。0.2.32只改菜单说明和版本号，使用新GUID `947f7c0c0da44ec3a806c2dcb69ca04e`及独立测试/运行记录。[源码连续性核验](Docs/Evidence/056/release-equivalence.json)支持引用旧验收证据；旧对局不会被改称新版本实战。

C1–C6失败结果原样保留，不跨局拼接。C7缺少单独终局F9和菜单前日志快照，真实胜利截图、完整日志及终局账本均已保存，见[C7归档](Docs/Evidence/055/NormalPlayer/river-attempt-007/README.md)。

- [里程碑与历史](Docs/Milestones-055.md) · [0.2.31验证索引](Docs/Verification-055.md) · [0.2.32证据](Docs/Evidence/056/README.md)
- [性能实测](Docs/Performance-055.md) · [原创美术与动画](Docs/Art-Animation-055.md) · [资源许可](Docs/Asset-Licenses.md)
- [开发合同](Docs/Development-Contract.md) · [已知限制](Docs/Known-Issues.md) · [GitHub PR #1](https://github.com/freezeng123456/unity-mimic-aoe/pull/1)

0.3.0 的480项测试、16局矩阵、原生观察与客户端交付保留在[057完成状态](Docs/Completion-Status-057.md)和[057验证记录](Docs/Evidence/057/README.md)。0.3.1再次修改规则、AI、输入显示和模型，使用独立[058证据](Docs/Evidence/058/README.md)；旧完整人工对局、音频和性能不会被改称本版验收。

## 交付边界

当前 0.14.0 的 1521 项正式自动化、八条普通路线观察与英国受控 IV 原生范围已记录；071私有交付已上传并完成元数据/权限读回，见[交付回执](Docs/Delivery-071.md)。0.12 的 1286 项及受控原生、0.11 的 1185 项与同机双进程合作均保持历史身份。实体双Mac、跨网卡/防火墙、长窗延迟/性能、完整自然终局、正式签名公证 Release 及 Windows/Linux/Intel Mac 仍待分别验收。可信 LAN 提供完整世界投影，未包含竞技防作弊、加密、公网匹配/NAT/中继或主机迁移；同版本不同构建可能拒绝连接。原创场景编辑和四个项目版革命分支已落入代码，完整文明/卡池/商业革命国家制度、商业编辑器全功能、美术、单位语音和商业海战仍有差距。历史交付与验收保持各自版本身份；本批边界见[完成状态](Docs/Completion-Status.md)、[已知限制](Docs/Known-Issues.md)和[当前差距路线](Docs/AoE3-Gap-Roadmap.md)。
