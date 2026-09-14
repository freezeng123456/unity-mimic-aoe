# 烽岸纪元 · Frontier Reckoning

**0.2.17 开发工作版本，Unity 原生陆战 RTS。当前菜单支持海岸盟约/河谷联盟分别选择、时代上限 III 或 V、一张固定对称地图和最多 25 张的自定义卡组。尚未达到完整陆战版或阶段 D 的验收标准。**

采集、建造、人口、生产、科技、时代、经验、船运、迷雾、军队和胜负使用统一模拟。两个文明具有实际经济与单位差异；当前数值的 P 项目设定与原作待核查资料分别记录。完整未完成项见 [Full-Scope-Audit.md](Docs/Full-Scope-Audit.md)。

0.2.17 将地图导航岩体替换为原创切面网格，并统一视觉底座与原有3×3阻挡格子的边界。村民揭雾规则未改；新增实际URP军队/村民坐标检查，在独立客户端验证单村民外出揭雾、离开后已探索变暗以及重开清理。全量EditMode128/128、PlayMode151/151通过，macOS arm64实际构建和运行。038正常局因测试误点在599.801秒退出，不能计作自然胜负；039为专门的局部视野检查。详见 [Iteration-039.md](Docs/Iteration-039.md) 和 [Native-Verification-039.md](Docs/Native-Verification-039.md)。

0.2.16 修复快速点击不同位置同类单位被误判为双击的问题，并在右键、UI、框选等操作之间清除旧候选。冻结 7 项回归旧版连续两次 3/7、新版 7/7，全量 EditMode119/119、PlayMode148/148；macOS arm64 实际构建。独立客户端记录了相隔 258.9ms、距离 76px 的两次单选，正常同点双击仍选六人；134.199秒局部检查主动认输，暂停、鼠标编队、重开和回菜单有原生证据。另补充封存0.2.15的969.910秒正常自然败局，玩家II、三次船运、III排队未完成。Ctrl/Command修饰键、失焦/桌面白屏、完整玩家主动攻城仍待解决；阶段D未完成。见 [Iteration-037.md](Docs/Iteration-037.md)、[Native-Verification-037.md](Docs/Native-Verification-037.md) 与 [Normal-Player-037.md](Docs/Normal-Player-037.md)。

0.2.15 修复普通HUD按钮之后新地面点击被220ms屏障吞掉的问题，覆盖训练后选人、取消后集结、攻击移动后指定目的地；模态、建造和暂停保护保留。冻结4项真实输入回归旧版连续两次1/4、新版4/4，全量EditMode119/119、PlayMode141/141；macOS arm64实际构建并检查12组原生画面。105.799秒检查局主动认输，完成新建造落地、暂停、重开与回菜单，但没有玩家经营、时代推进或船运，不作为完整对局验收。系统失焦与完整主动炮兵攻城仍待处理。见 [Iteration-036.md](Docs/Iteration-036.md) 和 [Native-Verification-036.md](Docs/Native-Verification-036.md)。

0.2.14 补齐经济与截图证据：付款/退款保存名义费用、实际余额变化及前后余额；修理记录工人、目标与HP变化；F9保存配套画面/状态/构建身份。全量EditMode119/119、PlayMode137/137，macOS arm64客户端已实际构建。正常玩家镜像局667.484秒自然败北，达到II、两次船运；518笔修理真实扣木66.569183，取消村民准确退100F，军营被毁释放5点预留人口。29组截图配对检查、暂停恢复和重开已验证。费用与战斗规则未改，034的像素时序异常没有复现，不能称已修复；完整主动攻城与阶段C/D仍未验收。见 [Iteration-035.md](Docs/Iteration-035.md) 与 [Native-Verification-035.md](Docs/Native-Verification-035.md)。

0.2.13 修复设备复位后首次按键/鼠标点击可能被旧缓存吞掉的问题，并按设备隔离旧动作与按住状态。冻结用例旧运行时 7/10→修复 10/10，全量 EditMode110/110、PlayMode129/129；macOS arm64 客户端已实际构建。新版本正常玩家镜像局在803.751秒自然败北，达到II、两次船运，并完成重新开始与返回菜单；三次失焦后恢复成功，但另外两次窗口恢复失败仍保留。人口预留的画面时序与微小修理付款日志精度继续待查。详见 [Iteration-034.md](Docs/Iteration-034.md) 与 [Native-Verification-034.md](Docs/Native-Verification-034.md)。尚未完成玩家主动炮兵攻城验收。

0.2.12 修复炮兵目标在攻击准备期间进入最小射程后仍可发射的问题：发射前重查距离，取消这一发并保留装填期限，已发炮弹沿用既有规则。固定规则红绿3/4→4/4，最终EditMode110/110、PlayMode115/115；macOSarm64客户端已实际构建。项目规则与证据见 [Iteration-033.md](Docs/Iteration-033.md) 和 [Native-Verification-033.md](Docs/Native-Verification-033.md)。完整玩家主动攻城与失焦恢复问题仍未验收。

0.2.9 补齐资源集结：选择城镇中心后右键可见资源，新训练或主动出城的村民会自动采集；更改集结点影响尚未出生的单位，右键地面会清除旧资源目标。非村民右键资源会移动，混合选择按能力分别下令，Shift 保留各自队列。资源与地面集结分别显示青色和金色旗帜。这些是明确记录的项目规则，原作尚未核实的边界见 [Resource-Rally-029.md](Docs/Resource-Rally-029.md)。

0.2.11 增加正常单机失焦自动暂停：返回窗口后需手动恢复，暂停面板 X 与 RESUME 一致，旧输入与待指定目标会取消。固定失焦测试旧版2/9→修复9/9；最终 EditMode104/104、PlayMode111/111。原生168.001秒局部检查记录两次真实停表、三名付费村民的出生/采集、主城替换和新局清理，见 [Native-Verification-031.md](Docs/Native-Verification-031.md)。032 补充本版正常玩家局：936.718 秒自然败北、III、三次船运，炮兵六次开火均面向敌单位，尚未完成主动攻城；详见 [Normal-Player-032.md](Docs/Normal-Player-032.md)。本轮新增四项炮兵输入/移动/部署/建筑命中测试，全量 PlayMode 115/115；运行时未改，EditMode 104/104 沿用 031 结果。桌面抓图发白和失焦后的自动化输入不稳定尚未解决。

封存 0.2.9 正常玩家局在 1010.500 秒自然败北，达到 III、使用四次船运，船运和付费生产的两门炮均参与军队战斗；没有对敌方建筑造成伤害。首座 TC 毁后继续约33秒才判负。详见 [Normal-Player-029.md](Docs/Normal-Player-029.md)。完整玩家核心对局、阶段 C 与 D 仍未通过。Ctrl/Command 编队保存的原生修饰键接收仍待独立核实，可通过 GROUPS 面板保存并按数字调用。

## 启动入口

客户端在独立的 `Frontier Reckoning.app` 中。macOS 双击此 app，选择双方文明、时代上限与卡组后点击 **BEGIN SKIRMISH**。通过 macOS 应用入口启动；不要把 `.app/Contents/MacOS` 内部可执行文件当作常规入口。0.2.17 构建为 **macOS Apple silicon / Development / Mono**，未构建 Windows、Linux 或 Intel Mac 客户端，也未做跨机器签名、公证和安装验收。

默认渲染窗口 2560×1440，在本机 Retina 显示器上对应约 1280×720 点。窗口可调整大小。当前游戏 UI 为英文，下面提供中文操作说明。此版本没有存档功能；关闭、重新开始会结束当前局。

Unity 工程通过 Hub 打开本目录，使用 **6000.6.0f1**（正式非 LTS 版本，已锁定实际依赖并在本机验证）。场景入口为 `Assets/Frontier/Scenes/FrontierSkirmish.unity`，打开后按 Play。场景使用真实 Camera、Light 和 GameController；运行时创建原生 GameObject 网格、Canvas、EventSystem 与 AudioSource。没有网页或浏览器运行依赖。

## 一局怎样开始

1. 开局有城镇中心、六名村民和一名探险家。先选村民，右键家旁的食物箱和木材箱，箱中资源需要实际采集。资源栏中的初始库存是开局配置，后续增长来自实际工作或有明确来源的效果。
2. 留村民采猎物、浆果、树和矿。按 **B** 打开建造菜单，点击建筑只进入预览；再点击地图才放下并扣费。海岸盟约的庄园完工提供 10 人口上限并产出一名村民，缺人口或出口受阻时奖励等待。
3. 按 **T** 选城镇中心，点击 Settler 持续训练；右键可见资源可设置新村民的采集集结点。资源不足、人口不足或时代不足时按钮会禁用，悬停可读原因。队列按钮可取消尚未完成订单，返还其已付费用。
4. 真实采集、生产、完工和战斗积累 XP。按 **H** 打开主城，获得机会后主动选卡发送。船运串行等待 35 秒；资源卡产生箱子，单位卡需要人口和出生空间。主城面板显示实时队列和等待原因。
5. 在城镇中心选择 **ADVANCE AGE**，支付数据表中的费用并选择资源或军事奖励。时代 II 开放军营、马厩和塔；时代 III 开放铸炮厂。发展经济的同时侦察中央通道及两侧路线。
6. 军营、马厩和铸炮厂训练不同兵种。使用 **TRAIN: SINGLE / TRAIN: BATCH** 切换单个与允许的整批数量；Shift 也可请求批量。按钮显示整批费用，统一检查费用和人口。兵种克制、射程、抗性与攻城伤害分别结算，面板说明来自规则配置。
7. 分组军队、攻击移动、集中攻击并摧毁敌方单位与建筑。摧毁第一座城镇中心不会直接获胜；本版本使用完全消灭或认输。结算页的经济、击杀/损失、时代和船运来自该局记录。

探险家可右键金色宝藏箱领取。若有守卫，先右键守卫攻击；清除后靠近收集3秒。经验直接进入XP，木材/钱币奖励会生成资源箱，需要村民实际采集。选村民打开 B → Trading Post，将预览放到四个木桩标出的路线站点（6米内吸附），完工后商队经过才给收益。默认XP；时代II研究 Cargo Ledgers 后可在贸易站面板选择食物/木材/钱币。

## 鼠标与键盘

| 操作 | 输入 |
|---|---|
| 单选 / 框选 | 左键点击 / 拖动；框内有单位时优先选择单位 |
| 追加或移除 | Shift 点击；Shift 框选追加 |
| 同类选择 | 双击己方单位，选择当前屏幕内同类；两次释放相距不超过8输入像素且间隔小于0.3秒 |
| 编队 | Ctrl 或 Command + 0–9 保存；0–9 调用并定位；GROUPS 面板可用鼠标保存/调用 |
| 镜头 | 方向键、鼠标到窗口边缘、中键拖动；滚轮缩放 |
| 上下文命令 | 右键地面、敌人、资源或己方建筑：移动、攻击、采集、施工、修理、驻扎 |
| 排队命令 | Shift + 右键；Shift 建筑放置追加施工 |
| 攻击移动 / 停止 / 驻守 | A 后点目标 / S / J |
| 快捷定位 | V 或句号：空闲村民；T：城镇中心；M：军事单位；F：当前选择 |
| 建造 / 主城 | B / H |
| 小地图 | 左键定位或拖动；右键向对应位置下令；A 后左键可攻击移动 |
| 集结点 | 选择生产建筑后右键地图；资源目标使新村民自动采集 |
| 明确驻扎 | G 或 GARRISON [G]，再左键己方已完工接收建筑；G / 右键 / Esc 取消；Shift 排队 |
| 驻扎释放 / 近远程切换 | 所选对象面板上的 UNGARRISON / MELEE MODE、RANGED MODE |
| 取消 / 暂停 | Esc 先取消放置/攻击/驻扎模式，再关闭面板或暂停；正常单机失焦自动暂停，返回后用 Esc、RESUME 或暂停 X 恢复 |
| 拆除 | 选己方已完工建筑，DEMOLISH，再在游戏内确认；无退款 |
| 截图 | F9，保存原生PNG和配套状态JSON到 Captures |

**人口显示为“已用 + 队列预留 / 当前上限”。人口不是单位实体数。** 住房被毁不会删除已有单位；已预留生产仍可完成，新生产预留和单位船运会检查上限。被摧毁生产建筑的队列不退款，这是项目规则。

## 工程与验证

- `Assets/Resources/Rules.json`：单位、建筑、资源、科技、时代、文明、卡牌和全局规则的权威配置。
- `Assets/Frontier/Simulation`：运行时状态、统一指令、经济、战斗、迷雾、网格寻路、AI；10 Hz 固定模拟。
- `SourceAssets/Characters/Infantry024`：原创可编辑 Blender 人物源和生成脚本；游戏运行只依赖已导入 FBX。
- `Assets/Frontier/Presentation`：Unity 原生输入、模型、UI、动画和声音；不负责发资源或扣血。
- `Assets/Frontier/Tests`：EditMode 规则测试与 PlayMode 生命周期/输入测试。
- `Packages/manifest.json` 和 `packages-lock.json`：锁定直接依赖及实际解析版本；URP 17.6.0、Input System 1.20.0、UGUI 2.6.0、Test Framework 1.8.0。
- `Docs/Development-Contract.md`、`Reference-*.md` 规则研究文档：证据级别、已核实参考、项目简化和待核实规则。
- `Docs/Implemented-Numbers-026.md`：当前项目数值（0.2.7–0.2.16 未改数值）；`Docs/Full-Scope-Audit.md`：完整需求验收状态。
- `Docs/Asset-Licenses.md`、`Docs/Licenses`：素材与依赖许可全文、来源和哈希。
- `Docs/Native-Verification-031.md`：封存0.2.11的原生失焦、恢复与新局检查；`Docs/Normal-Player-031.md`：封存0.2.10正常玩家局。
- `Docs/Performance-027.md`：封存0.2.7客户端50/100/200/400活动单位的历史性能诊断；0.2.16未重测该矩阵。
- `Docs/Evidence/037`：本次固定红绿与全量测试XML、构建输入、0.2.16局部原生检查、0.2.15正常败局、配对截图和可复算脚本。
- `Docs/Iteration-037.md`：本轮双击修复及验证；较早 Verification-Report/Performance-Report 保留为明确版本的历史样本。
- `Docs/Full-Scope-Audit.md`：当前未解决问题与明确未实现范围；Known-Issues.md 为此前版本记录。

在 macOS 运行 `Tools/test.sh` 执行 Unity 测试；`Tools/build-macos.sh` 准备原生场景和 URP 配置并生成客户端。可通过 `UNITY_EDITOR` 指定同版本编辑器，通过 `FRONTIER_BUILD_PATH` 指定输出 app。脚本根据自身位置找到项目，不依赖开发者的私人绝对路径。首次打开需要 Unity 通过 Package Manager 恢复依赖。

`Tools/compare-event-logs.py <预期JSONL> <实际JSONL>` 可比较完整事件文件；不承诺跨平台重放。`Tools/build-character.sh` 用 Blender 5.2.1 LTS 再生原创人物源，先审查输出后再替换运行时FBX。

`Tools/verify-delivery.py --reports-dir <报告目录>` 只检查源码包装、元数据、版本和真实 XML 测试结果；它不能证明游戏已完成。命令行 `-frontierAutoplay -frontierAutoSpeed 8` 用于客户端自动完整对局验证；`-frontierBenchmark 50`（或 100、200、400）是单独压力夹具，提升运行时生命以维持活动数量，与正常对局明确分开。`-frontierInputTrace` 记录鼠标、键盘与 UGUI 按钮事件用于排查输入。

客户端自己的 `Reports`、`Captures`、`Benchmarks` 目录位于 Unity 的持久化应用数据目录，即 macOS 用户 Library/Application Support/Frontier Workshop/Frontier Reckoning 下。正常游戏不从验证报告中加载资源或单位。

源码压缩包排除 Library、Temp、Logs、Builds、obj 等可再生成缓存；客户端另行打包。未实现存档或确定性回放，不宣称具备联网同步能力。独立项目名称、文案、网格、UI 与合成音频均为本项目制作，不隶属于《帝国时代》官方。
