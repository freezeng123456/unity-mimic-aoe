# 烽岸纪元 · Frontier Reckoning

独立 Unity 即时战略游戏：英国、荷兰、法国、奥斯曼四个经典文明核心玩法，加上远驿盟市、山岭工联两个原创文明，共六文明。五个时代、25张卡组与XP船运、银行、实车贸易、工坊制造、守卫宝藏和炮兵攻城，配有原创模型、动画与音频。使用本项目规则和候选数值，与 Age of Empires 及其发行商无关联。

**当前源码版本为 0.9.0，目标平台 macOS Apple silicon。** 第三批已实现本地 2v2/四方 FFA、七个中文引导课程、三章相连的原创河流战役、持久音频与20动作改键，以及UI比例、阵营形状、高对比小地图和事件文字。[完整自动化1,062/1,062通过](Docs/Evidence/066/Tests/README.md)（710 EditMode + 352 PlayMode）；运行代码提交/来源基线 `c9747695d91fa203b05116a1fc0c15a8aac32de2`，573个冻结输入的指纹 `66cf7cc57e080eb40cfdf98310ee26affd93d690745dde2b457e8fb3eb0ee1ee`。四槽模式是一个玩家加AI，当前没有联网。**GUID `1969c7ee14f944879510f4ca0d0f241d` 的代表性原生操作与冷恢复已通过；本版性能未采样；最终三包本地核验及六载荷/第七回执上传、云端隐私/名称/大小/父目录元数据读回已通过，远端内容SHA与下载字节一致性未验证。** 玩法和操作见[内容说明](Docs/Content-066.md)、[玩家指南](Docs/Player-Guide-066.md)，证据见[066索引](Docs/Evidence/066/README.md)与[本地完成快照](Docs/Completion-Status-066.md)。

0.8的958项测试、生成地图、海军运输与有界压缩存档，以及原生长局冷恢复/两乘员登陆记录，保留在[0.8状态快照](Docs/Completion-Status-before-066.md)、[正常冷恢复](Docs/Evidence/065/Native/Normal/README.md)和[受控乘员证据](Docs/Evidence/065/Native/Fleet/README.md)，不重标为0.9结果。已交付0.7的工业、三槽存读档、796项测试与原生证据见[0.7完成快照](Docs/Completion-Status-064.md)和[0.7交付索引](Docs/Delivery-064.md)。机制说明仍可参考[经典工业](Docs/Classic-Industry-064.md)、[存档与续局](Docs/Save-Load-064.md)、[地图与海军内容](Docs/Naval-Content-065.md)；[AoE III 差距路线](Docs/AoE3-Gap-Roadmap-064.md)记录各批的独立实现和验收范围。

0.6.0 新增蜿蜒河谷、热带河湾、支流汇合、河口四张地图，连同旧陆图与直河图共六张固定布局。水域、岸高、斜桥、港位、鱼点与 AI 航路统一使用 MapLayout；河岸采用连续岸坡与材质混合，港区有岸仓与伸水栈桥，船有厚船舷、船员工作动作和尾流。实现见[曲河玩法](Docs/Rivers-063.md)，该版测试、来源指纹、机动与原生记录见[0.6.0 完成状态](Docs/Completion-Status-063.md)和[063证据](Docs/Evidence/063/README.md)。当前候选进度见[完成状态](Docs/Completion-Status.md)，旧版验收保留原版本身份。

## 启动与操作

0.9操作见[玩家指南](Docs/Player-Guide-066.md)。初次可从 **GUIDED LESSONS** 学习；**RIVER CAMPAIGN** 进入原创三章，完成记录用于解锁后续章节；**SETTINGS** 调整音频、可访问性和改键。原生已通过设置冷恢复、单课槽3恢复后实际完成及账号徽记再冷恢复、2v2曲河冷恢复/投降后盟友继续，以及四国FFA开局；[原生回执](Docs/Evidence/066/Native/README.md)记录范围。全部课程/战役或四人终局没有被宣称通过；下载入口及三包本地SHA见[0.9交付说明](Docs/Delivery-066.md)；云端已完成元数据核验，远端内容校验未完成。

当前[0.9私有交付目录](https://drive.google.com/drive/folders/1wzczmUUktYSDSn92KuwpUQ4dZkIK-h-3)提供源码、macOS客户端和原生图集；源码快照为`e75407c78522374ed7e327a1ed1727212d27ffd3`，运行代码仍为上述c974，上传后三ZIP未重建。包外回执见[Delivery-066](Docs/Delivery-066.md)，GitHub最终推送/PR更新仍待主任务完成，私有draft保留。

历史客户端下载和核验见[0.7交付索引](Docs/Delivery-064.md)与[0.8交付说明](Docs/Delivery-065.md)所列包外回执。0.7客户端只有六张固定地图；当前源码菜单有九图循环、**MAP, SEED & OBJECTIVES** 和本地槽位配置，不能用历史客户端演示新增0.9功能。

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
| Ctrl或Command + 数字 / 数字 | 保存编组 / 召回编组 |
| Esc | 取消操作、关闭面板或暂停；以可见暂停界面为准 |

开局先采集资源箱，再维持食物、木材与钱币收入。箱子和时代奖励须由工人实际采集；同一资源点最多8名有效工人。荷兰银行完工后持续产币，主城船运需要XP机会和运输时间。奥斯曼城镇中心显示免费招工进度、工人上限和阻塞原因；**AUTO SETTLERS: ON/OFF** 控制暂停与恢复。炮兵优先框选并配备护卫；驻军工人不会采集，安全后应及时撤出。

正常玩家局失焦会自动暂停，回来后手动恢复。**SAVE MATCH** 写三个手动检查点，主菜单 **LOAD SAVED MATCH** 或暂停菜单 **LOAD MATCH** 读取后先暂停。检查点包含AI、迷雾、队列、工厂、镜头、编组、乘员、完整事件及可选任务尝试；选择已有槽会替换该检查点。写入使用schema 2 gzip-base64，文件仍限64 MiB、解压原文限256 MiB，失败保留旧槽。读档要求应用版本和规则一致，不提供0.8→0.9迁移。任务完成/解锁另存于本地Account，音频和改键另存于Preferences，读取检查点不回滚它们。F8/F9/F10是保留的诊断采集键；默认动作和改键冲突规则见[指南](Docs/Player-Guide-066.md)。

## 六文明玩法

| 文明 | 经济约束 | 特色部队 |
|---|---|---|
| 英国 British | 庄园完工赠村民，木材投入兼顾住房和劳动力 | Longbowman；保留火枪手和骠骑兵 |
| 荷兰 Dutch | 钱币村民、50工人上限、银行收入、矿山采币加速 | II时代散兵；III时代Ruyter和Halberdier |
| 法国 French | 120食物猎民，采集快25%，工人上限80 | Coureur des Bois；III时代范围近战Cuirassier |
| 奥斯曼 Ottomans | 免费自动招工，初始55秒与25工人上限；清真寺付费研究扩展 | Janissary、II时代炮厂Abus、仅船运Spahi |
| 远驿盟市 | 商车每次先支付60食物，到达己方驿馆才收入钱币；护商和拦截产生实际经济风险 | 护商卫士、道路骑哨、车队火铳手 |
| 山岭工联 | 村民放弃采集并在工坊制造；预制件加速付费建筑施工 | 野战工兵、工联猎炮、盾枪卫士 |

一套卡组最多25张，合法卡按文明与单位/科技限制筛选；经济、军事、科技三套预设可直接选择。时代II–V每次三选一，共72个文明晋级选项。当前规则共有66个实体、219项科技和153张卡定义；其中包含资源、建筑、升级等级和路线条目，不能将条目数当作同等数量的独立系统。

本项目实现经典文明的核心机制，不等于《帝国时代 III》的完整国家内容或商业游戏内容量。工业时代两张工厂马车卡、真实马车移动建造、资源或重炮生产模式已接入；英国工厂生产火箭，法国/荷兰生产重型加农炮，奥斯曼生产巨炮。其数值使用已标明的本项目规则。荷兰Envoy、法国Native Scout、完整政治家/革命、全套皇家卫队与治疗、完整官方卡池尚未实现。当前有六张固定地图、三个生成模板、三个原创盟友、三种地图目标，以及七课和三章固定场景原创战役；没有通用场景编辑器或完整商业中立势力系统，数值未经广泛对战平衡验证。

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

目标为macOS arm64、Development/Mono、URP。本轮1,062项是编辑器自动化结果，0.9另有上述代表性原生验收；三包本地核验及云端元数据已通过，远端SHA/下载字节一致性未验证，本版性能未采样。历史0.8正常续局/受控操作不扩大为0.9整局、自然胜负或1×性能。原生帧率样本受机器、分辨率、镜头和活动量限制，不能保证通用60FPS。Windows、Linux、Intel Mac及跨机器签名、公证尚未验证。七课、三章原创战役和本地团队已实现；联网合作、可毁桥、完整商业海战、地图编辑器和通用回放仍缺。完整边界见[完成状态](Docs/Completion-Status.md)与[已知限制](Docs/Known-Issues.md)。
