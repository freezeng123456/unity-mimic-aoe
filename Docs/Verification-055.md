# 0.2.31 验证进度

2026-10-03，原定 **A–D 四个里程碑均已通过**。[C7同局严格审计](Evidence/055/NormalPlayer/river-attempt-007/summary.json)通过17/17检查、19/19覆盖：River镜像/Easy/V上限/Military25，正常1倍速、autoplay=false，2528.415771秒自然获胜，实际完成I–V、付费经济/军队/科技、银行、贸易、宝藏与炮击敌建筑。C1–C6分别保留6/10/1/3/1/2项缺口及失败结论，不跨局拼接覆盖。原生验收属于冻结0.2.31；0.2.32独立发布前核验已通过，见[056记录](Evidence/056/README.md)；源码包待最终干净提交后生成，不将旧GUID观察改称新版本实测。 最终客户端证据按版本、GUID与完整源码指纹绑定。

## 已完成的针对性检查

视觉、原生输入与音频PlayMode针对性首轮67项：62通过、5失败。4项队色比较的实际URP读回差值为2.98e-8至5.96e-8，修正为逐通道1e-5容差并保留全部人物/队色尺寸断言。另一项环境声对象层级混入事件声道，改成专属环境声子对象，保留原音乐/风声实际isPlaying、暂停、静音、菜单和换局断言。第二轮67/67通过，退出码0；XML见`Evidence/055/Targeted`。该轮只提供针对性证据；随后完整源码绑定结果见下。

新URP正常相机距离研究图5组已导出并具备PNG SHA回执，见[Art-Animation-055.md](Art-Animation-055.md)。这类隔离图不能替代正常原生对局。

证据解析工具共40项正负拒收回归通过：音频13、逐帧性能14、玩家账本13。其中7项新增检查覆盖长对局gzip归档的字节等价与损坏拒收，见[归档读取回归](Evidence/055/event-archive-verifier-regression.json)。这些是临时合成解析夹具，用于检查拒收逻辑，不代替真实客户端样本。

## 完整源码绑定检查

统一完整测试 `Builds/Tests/run-NKi6iyTL` 实际通过214/214 EditMode与210/210 PlayMode，报告终止时间分别为2026-10-02 11:03:12Z及11:04:26Z。完整Assets/Packages/ProjectSettings/Tools指纹为`1b45f97f3d90c9b76388419492a83c5622ccb2cf3a365030044403ccd309c1c7`，测试前后相同，报告及SHA见`Evidence/055/Tests`。源码证据校验工具8/8也通过。

贸易站未完成视野的固定红例0/1，最小运行时修复后最终地标针对性19/19。第一次绿轮18/19中失败项是测试把XP进度当累计经验；改为逐笔真实Trade route delivery经验账本后通过，XP规则没有修改。覆盖取消退款、重建再次扣款、真实敌方攻击摧毁、收入停止/视野撤回、四种收入、双方消灭/新局重置、真实路径和阻塞负例、可见发现去重。

## 原生构建、音频与历史玩家记录

客户端已实际构建并启动，GUID`128a731d440e4b7a94abfcc74b88fa97`，构建前后上述源码指纹不变。12局普通AI矩阵均自然结束，独立事件检查全部通过，V时代覆盖缺口如实保留，见[AI-Matrix-055.md](AI-Matrix-055.md)。

同版原生正常混音、暂停和未暂停主静音各20秒，三个窗口全部通过独立WAV复算，见[Native-Audio-Capture.md](Native-Audio-Capture.md)。这证明实际客户端混音与静音边界，未进行耳听。结算时静音候选被时长与模拟推进判据拒收，仍保留失败记录。

coast B5已在1073.2850模拟秒自然败局，实际III、12付费村民、15Pike+15Musk、2付费炮与6船运成立；严格[coast-iii审计](Evidence/055/NormalPlayer/coast-attempt-005/summary.json)仅缺炮击敌建筑，因此B5未通过；后续B10完整结果见下。原始match_finished/menu_exit及12组同局F9、菜单/结算图全部保留，未将炮击敌单位升级为建筑伤害。

River完整路线由[C7](Evidence/055/NormalPlayer/river-attempt-007/summary.json)同局完成，17/17严格检查与19/19覆盖通过；C1–C6历史失败见下，不跨局拼接。50/100/200/400活动单位压力证据及400长窗口已完成，正常玩家长窗口由B9独立最终配对通过。B5实际197.825666有效秒，首个145.924秒及B4短窗口仍按未满单300秒保留，见[Performance-055.md](Performance-055.md)。

B6 `d57aee12243c4599a9343986d5b2c1ab`中断证据已[封存](Evidence/055/NormalPlayer/coast-attempt-006/README.md)：旧NativeInput055日志末次705.3748秒手动暂停后失焦，恢复时sky重新启动客户端在菜单。没有同局自然结束、menu_exit、quit报告或完整events，不能判退出原因/获胜方/败方全消灭；最后F9 664.8847秒仍II+III排队，不当作时代完成。20文件/2,821,359字节、13份原生来源及4组F9按字节校验；F8 258.709919秒仅原始帧初审，无最终账本关联且不足300。B7开始时的过程观察保留，其自然结算及最终严格结果见下。

B7已取得[真实最终审核](Evidence/055/NormalPlayer/coast-attempt-007/summary.json)：match`781c4090a2474a91964105c79eab9e27`在835.6430秒自然败局、双方III、末局存活单位/建筑[0,68]；6付费工、10Pike+5Musk、1付费炮与4船运成立。独立重跑严格coast-iii退出码1，仅缺炮击敌建筑，其余身份/正常开局/支付/生产/自然消灭检查成立，不标完整B通过。两份最终配对F8原始帧与各593笔内存/实际事件前缀复算一致，有效259.774107/135.600574秒均不足300；passed仅指统计/关联校验。

B8的[最终归档](Evidence/055/NormalPlayer/coast-attempt-008/README.md)为259.6066秒menu_exit中断、无自然结算；己方Command/Gather均0，首次真实暂停223.6044秒，只有1名付费工实际出生、另2名排队。B9已974.5091秒自然败局；严格审计缺付费炮出生及炮击敌建筑，不认证B。B10经济卡组后续完整通过，见下。这段B8/B9过程记录时，D四档规模与400长档尚未执行；其后已完成并通过，见D段。0.2.32独立发布前核验已通过，见[056记录](Evidence/056/README.md)；源码包待最终干净提交后生成；解析脚本与合成回归不当作原生烟测。

B9[正常F8最终配对](Evidence/055/Performance/normal-b9-final-001/f8-audit.json)单窗335.999332有效秒、20,003帧全部有效/有焦点，最终report/events、逐帧统计及335笔内存与实际实体计数关联全部通过。明确传入300秒最低时长，状态passed且无失败检查。原初审目录逐字节保留；此项通过不替代B完整路线或D四档规模。

## B完整路线已通过

[B10最终严格审核](Evidence/055/NormalPlayer/coast-attempt-010/summary.json)对应match `f65f503a74b9415e89d0a875e9b0ce2d`、原0.2.31/GUID、coast镜像、Easy、III上限、Economy25、正常1x/无autoplay。17项检查全部true、failedChecks与coverageGaps均空：实际II/III、3付费工、9Musk+5Pike、2付费炮、4船运、正常采集及炮击敌建筑成立。炮186在580.5053–605.9991秒六次命中敌马厩156，每次130，总780。958.612976秒自然败局，败方存活单位/建筑0，无Resign。自然战败是实际结果，完整操作路线通过不表示获胜或平衡结论。C7同局五时代路线及D四档规模均已通过，见下。

## C1原生河谷首局

[C1严格审核](Evidence/055/NormalPlayer/river-attempt-001/summary.json)绑定同版同GUID、match `1498f3a84f374c0498900c25a6177872`。1486.584106秒自然败局、败方全灭，无Resign；53,782事件与最终报告匹配。17项中16项成立，覆盖项失败；六个缺口为IV、V、付费贸易站、XP贸易、资源模式、实际资源交付。银行、付费Carabineer、付费科技、两宝藏及付费火炮攻击敌建筑已有真实证据，保留原结果，不宣称C完整通过。

## C2原生河谷第二局

[C2完整归档](Evidence/055/NormalPlayer/river-attempt-002/README.md)绑定match `0b725a5116d2415f855f4443c4023e0e`、原0.2.31/GUID，River对Coast、Easy、V上限、Military25。1081.682983秒自然败局、败方全灭、无Resign；49,623条事件与最终报告匹配，终局F9显示DEFEAT。17项检查中16项成立，仅requiredPlayerCoverage失败，十缺口为付费炮、炮击敌建筑、IV、V、Carabineer、付费科技、付费贸易站、XP贸易、资源模式、实际资源交付。

16付费工、15Musk、5Pike、两银行及75W/100XP两宝藏成立。IV在998.003357秒支付2000F1200C，1045.491821秒TC被毁，47.488464秒不足130秒，未完成IV。炮288于1030.395508秒作为Cannon Support到达，属于船运而非付费生产；Hussar Reserves只发送未到达。

离线独立复核39文件清单、所有文件SHA和receipt SHA `1013cf80503cc10254e62f61951aae36785d7cac16eb603cc1c63571bbe4ca97`一致；从归档report/events重跑严格审计，仍16/17与相同十缺口。6组F9、输入日志、357项源码及3项compiled runtime绑定见本局归档。C2原始失败结果保持不变。

## C3原生河谷第三局

[C3完整归档](Evidence/055/NormalPlayer/river-attempt-003/README.md)绑定match `8fb300efbc1a4169a953d9dab2b7caad`、原0.2.31/GUID，River镜像/Easy/V/Military25。1781.212158秒自然败局、败方全灭、无Resign，90,759条事件及两组终局报告配对一致。严格检查16/17，覆盖18/19，仅 `cannonEnemyBuildingDamage` 失败。

I–V实际完成，付费工/军/炮、银行被动币、Carabineer、付费科技、宝藏、贸易站、XP贸易及Food实际交付均成立。八门付费炮出生，但153次我方炮伤害全部命中敌单位。科技原图与输入/出生链单独绑定，旧兵群和新兵均有升级状态；不把不同选中单位写成同一单位前后对照。

独立核对73文件共147,020,138字节和全部SHA；receipt SHA为 `8afbbbea487a0b28b09baff51f9ddaa93218ac1d987bc5058936daba34e2ee48`。20组F9、两份冻结日志、357源码及3项runtime身份通过。C3仍为失败；后续C4另列如下，不跨局拼接成C通过。

## D四档规模与长窗口已通过

2026-10-03，同版单客户端、无并行Editor完成50/100/200各25秒及400共360秒的原生活动单位夹具，全部实际2560×1440。50/100/200的真实F8有效秒分别18.947394、18.349425、18.490229，只作短诊断。400 attempt-002单F8窗口**317.674076有效秒**，13,153帧全部有效/有焦点，明确的300秒要求通过；逐帧mean24.152214ms、floor P95 33.990250ms/P99 40.640369ms。

[50](Evidence/055/Performance/scale-050-attempt-001/scale-summary.json)、[100](Evidence/055/Performance/scale-100-attempt-001/scale-summary.json)、[200](Evidence/055/Performance/scale-200-attempt-001/scale-summary.json)、[400通过档](Evidence/055/Performance/scale-400-attempt-002/scale-summary.json)的初末实际存活数、双方数量、真实Spawn/Death前缀、全部内存计数、自动F9和同局menu_exit报告匹配；357项冻结源码、compiled runtime、版本及GUID与055receipt一致。每档真实按F8后等自动保存，再正常返回菜单/QUIT，不称为自然对局。

[400首次失败](Evidence/055/Performance/scale-400-attempt-001/scale-summary.json)只182.717415有效秒，因失焦未达300秒，所有原始证据保留。第二次通过周期性公开GUI点击HUD静态文字维持前台，未改采样器、源码或规则。D的原创视觉/动画/音频与更广规模条件现已满足，完整实测和内存/GC见[Performance-055.md](Performance-055.md)；这不证明全400无遮挡同屏、固定FPS、跨设备性能或C完整路线。

## C4原生河谷第四局

[C4完整归档](Evidence/055/NormalPlayer/river-attempt-004/README.md)对应 `b4250e2ab6664083ad919de284cc0afb`、原0.2.31/GUID，River对Coast/Easy/V/Military25。994.504211秒自然败局、败方全灭、无Resign；36,218条事件和两组最终报告配对一致。严格16/17检查、16/19覆盖，仅 `ageIV`、`ageV`、`specialUnit` 失败。

付费炮238（729.568909秒出生）对敌Manor156在800.651550–826.145325秒造成六次各130 Siege伤害，原始命令、支付/出生和目标身份均已关联。Cargo Ledgers389.014526秒完成，`paidTechnology`成立；没有本局Veteran Musketeer升级证据。贸易、银行和宝藏均按C4账本独立通过。

65文件共59,908,543字节、16组F9、两份冻结日志、357源文件和3项runtime已通过归档核验；[receipt](Evidence/055/NormalPlayer/river-attempt-004/archive-receipt.json) SHA为 `cc28fc8e47999c072aeb04a0df04af0f4e8536ac70daebbaea547a14f8647e59`。C4仍失败；C5已1661.541382秒自然败局，严格16/17检查、18/19覆盖，仅缺 `ageV`；[C6](Evidence/055/NormalPlayer/river-attempt-006/README.md)已1367.213257秒自然败局，严格16/17检查、17/19覆盖，仅缺 `ageIV`、`ageV`，也不跨局拼接覆盖。

## C5原生河谷第五局

[C5完整归档](Evidence/055/NormalPlayer/river-attempt-005/README.md)为River镜像/Easy/V/Military25，match `d69b5316feda422f8a27d23545e13c5b`，原0.2.31/GUID。1661.541382秒自然败局、败方全灭、无Resign，79,417条事件与menu_exit配对一致。严格16/17检查、18/19覆盖，仅 `ageV` 缺失，V未付款/未完成。

老Musk173在825.245544/838.142395秒两张单选图均由原始UI context关联，HP200/200、ATK24变为HP240/240、ATK29；科技836.342834秒完成，173在338.111420秒出生、918.922668秒死亡。新Musk256在科技后840.941711秒付款、866.935364秒出生，874.833435秒单选图显示HP240/240、ATK29。F9 JSON没有selectedEntity字段，ID来自冻结输入上下文及出生/死亡链。 [科技视觉审计](Evidence/055/NormalPlayer/river-attempt-005/visual-and-technology-audit.json)通过；[炮击审计](Evidence/055/NormalPlayer/river-attempt-005/cannon-building-audit.json)独立确认付费炮261对敌Foundry239两次各130 Siege，共260，原行34455/34716。

67文件共127,494,066字节、17组F9和双冻结日志已SHA核验；357份.31源码及3项runtime冻结审核通过，原身份不变。[receipt](Evidence/055/NormalPlayer/river-attempt-005/archive-receipt.json) SHA为 `299eacd347eb2e951df0804e0a143de562adda1d1dfff469b08cee383bc026d0`。C5仍失败；[C6](Evidence/055/NormalPlayer/river-attempt-006/README.md)已1367.213257秒自然败局，严格16/17检查、17/19覆盖，仅缺 `ageIV`、`ageV`。

## C6原生河谷第六局

[C6完整归档](Evidence/055/NormalPlayer/river-attempt-006/README.md)对应 `7558b19fc9984578a313082b633c6630`、原0.2.31/GUID，River镜像/Easy/V/Military25、正常1x、无autoplay。1367.213257秒自然败局、败方全灭、无Resign；60,226条事件和menu_exit事件逐字节一致，最终玩家III/对手IV。严格16/17检查、17/19覆盖仅缺 `ageIV`、`ageV`；IV1190.856323秒付款，TC1292.431519秒死亡，升级未完成；V未付款。

[炮击审计](Evidence/055/NormalPlayer/river-attempt-006/cannon-building-audit.json)确认付费炮256对敌Farm266原行35232/35466/35716三次各130 Siege，共390；[科技视觉审计](Evidence/055/NormalPlayer/river-attempt-006/visual-and-technology-audit.json)确认同一老Musk162升级前后及科技后付费新生Musk263的UI context、出生/死亡与像素关联。F9 JSON没有selectedEntity字段，不以截图元数据直接声称单位ID。

63文件共94,778,838字节、11组F9、双冻结日志已完整保全；[receipt](Evidence/055/NormalPlayer/river-attempt-006/archive-receipt.json) SHA为 `a09a6581238f86db4f55ef882e8a754e94b0f82daf616c046019d9ca387127ee`，root独立核对全部62成员bytes/SHA并重跑strict，关键字段一致。首次frozen审计失败来自归档helper硬编码FP误改，原始probe保留；纠正审计与root独立357源码/3项runtime全量核验均通过，运行源码未变化。C6仍失败。

## River C7：同一局完整通过

[C7归档](Evidence/055/NormalPlayer/river-attempt-007/README.md)绑定 `cd717dda89684d87b979629d3fbdf1a9`、0.2.31及GUID `128a731d440e4b7a94abfcc74b88fa97`。River镜像/Easy/V/Military25，正常1x、无autoplay；2528.415771秒自然获胜，116,739条事件、唯一MatchEnded、无Resign。严格17/17检查、19/19覆盖全部通过，coverageGaps为空；玩家最终V、对手IV，300击杀/115损失、14次船运。

II/III/IV/V分别于407.015625/850.039490/1393.506836/1736.123169秒实际完成。同局银行收入、付费Carabineer/科技、两宝藏、XP贸易与资源交付以及付费炮对敌建筑伤害均由原始付款/完成/命中链核验。357份源码与3项compiled runtime冻结身份经独立审核通过。

同一老Musk221在860.536926/990.505188秒的原始单选图由冻结输入上下文与出生/死亡链关联，HP200/200、ATK24变为HP240/240、ATK29；Veteran Musk988.505676秒完成。科技后另行付费的新Musk290于1104.277466秒出生，1111.775635秒原图显示HP240/240、ATK29。19项[科技专项核验](Evidence/055/NormalPlayer/river-attempt-007/technology-visual-audit.json)全部通过；1110.575928秒空选择图不作为新兵属性证明。F9元数据没有selectedEntity字段，ID来自输入上下文和事件链。

付费炮261于987.505920秒出生，1039.293335秒对敌贸易站179造成130 Siege伤害；同目标F9生命1648→1518与Fire/Damage及付费来源一致，命中结论不由Attack提示代替。

完整归档97文件共220,994,877字节，含24组原生F9、真实sky胜利图及完整冻结输入日志；[receipt](Evidence/055/NormalPlayer/river-attempt-007/archive-receipt.json) SHA为 `73304c7dcf6d9541950b34479ece0ff691f39642d48b996833ad8dd3ba44027e`。root独立复核全部96成员bytes/SHA及文件集合。

C7终局采样存在明确偏差：观测工具尾部的Escape在VICTORY弹窗出现后直接返回主菜单，没有终局F9或菜单前Player-ended副本。真实sky胜利截图、完整冻结Player-menu.log、match_finished/menu_exit原始报告及事件和返回菜单F9均已保全；未伪造缺失文件。见[终局取证偏差](Evidence/055/NormalPlayer/river-attempt-007/end-capture-deviation.json)。C7通过不改写C1–C6失败，也不把合成解析测试或AI对局当作玩家流程。

## 0.2.32独立交付校验

原定A–D的正常玩家与规模证据保留在0.2.31/GUID `128a731d440e4b7a94abfcc74b88fa97`。发布源码提交 `59a66b64c027224289761ad6bb409e8e41804fee` 仅改 `FrontierHud.cs` 两处菜单文字和 `ProjectSettings.asset` 版本号，共2文件3处文本变更，不改规则或玩法。

新源码指纹 `7340a04ba7f4d10c35a4dde158a8c517e0205e875daacc1a58005dcb61730adf` 的214/214 EditMode、210/210 PlayMode已重新通过；5份原始测试文件在[056 Tests](Evidence/056/Tests/source-evidence.json)。独立macOS arm64构建已exit 0，GUID `947f7c0c0da44ec3a806c2dcb69ca04e`，bundle共299文件、173,040,973字节。新客户端原生烟测已实际完成菜单、普通开局、Gather、返回菜单和QUIT：match `fd093483132f4bff991d9082260fd7a4`，27.400068秒、1x、autoplay=false，采集46.799976F，winner=-1；这是主动返回菜单的短烟测，不是完整对局。版本衔接初审与客户端ZIP逐文件bytes/mode/CRC核验均通过；299个bundle文件、618个ZIP文件，ZIP SHA `f3ec2180ef38c602db43cc5a1db001cf162ec4634cc066704c02492eb16e9fef`。发布前状态为ready_for_packaging，最终源码包/交付manifest在干净提交后生成，见[056最终记录](Evidence/056/README.md)；未把0.2.31整局重标成0.2.32观察。
