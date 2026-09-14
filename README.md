# 烽岸纪元 · Frontier Reckoning

**0.2.29开发工作版本，Unity原生陆战RTS。两种文明、I–V、固定地图、自定义卡组和三档AI已有实现；尚未达到完整陆战或阶段D验收标准。**

本轮修复重复确认同一移动、攻击移动或追击目标时路线被清空后继承旧重试时间的问题。相同意图保留当前路线与准备中的攻击；真正改向立即重新规划，Shift追加仍保留当前路线。实时阴影继续关闭。

EditMode190/190通过；完整PlayMode和新客户端构建尚未在本轮重跑。详见 [Iteration-053.md](Docs/Iteration-053.md)。**本轮未人工启动0.2.29独立客户端**，自动检查不等同于玩家整局、视听或性能验收。

采集、建造、人口、生产、科技、时代、经验、船运、迷雾、军队和胜负使用统一模拟。两个文明具有实际经济与单位差异；当前数值的 P 项目设定与原作待核查资料分别记录。完整未完成项见 [Full-Scope-Audit.md](Docs/Full-Scope-Audit.md)。

0.2.19 扩大镜头边缘滚动的触发区域，并随窗口大小调整；避免按钮和小地图悬停时意外滚屏，补上常驻镜头/小地图操作提示。全量EditMode145/145、PlayMode159/159通过，macOS arm64新客户端已构建并实际运行，四边滚屏、进入黑暗区域下令及村民到达、暂停与返回菜单已取得原生证据。四边原生操作验收结果见 [Iteration-042.md](Docs/Iteration-042.md)。向未探索地面移动、攻击移动和小地图下令已有真实输入回归；完整核心对局仍未验收。

本版包含0.2.18的经验改动：建造、付费训练和击杀使用独立数据参数，付费训练在实际出生后发经验；免费赠送、船运与时代增援不重复发训练经验。相关参数属于明确标注的项目设定，见 [Iteration-041.md](Docs/Iteration-041.md)、[Reference-XP-041.md](Docs/Reference-XP-041.md) 和 [Implemented-Numbers-0218.md](Docs/Implemented-Numbers-0218.md)。0.2.18原生受控经验专项未完成；0.2.19的新独立局已实际验证100F训练扣费、出生后5XP与人口/队列同步，见 [Native-Verification-042.md](Docs/Native-Verification-042.md)。尚未验证完整经验到船运的原生链条。

最近自然正常镜像局040使用封存0.2.17，在653.987秒自然战败，玩家II时代、一船运，没有形成主动军队攻城。首次TC毁后仍有村民时比赛继续；这证明部分失败生命周期，不能称核心主动对局通过。更早版本验证与修订保存在分版本Docs以及 [此前README](Docs/Archive/README-0217.md)。

## 启动入口

客户端在独立的 `Frontier Reckoning.app` 中。macOS 双击此 app，选择双方文明、时代上限、AI 难度与卡组后点击 **BEGIN SKIRMISH**。通过 macOS 应用入口启动；不要把 `.app/Contents/MacOS` 内部可执行文件当作常规入口。0.2.28 构建目标为 **macOS Apple silicon / Development / Mono**，未构建 Windows、Linux 或 Intel Mac 客户端，也未做跨机器签名、公证和安装验收。

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
| 镜头 | 方向键、鼠标停在游戏窗口内部边缘附近、中键拖动；滚轮缩放；鼠标移出窗口后不继续边缘滚动 |
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
- `Docs/Implemented-Numbers-0218.md`：当前项目数值，含独立建造/训练/击杀经验；`Docs/Full-Scope-Audit.md`：完整需求验收状态。
- `Docs/Asset-Licenses.md`、`Docs/Licenses`：素材与依赖许可全文、来源和哈希。
- `Docs/Native-Verification-031.md`：封存0.2.11的原生失焦、恢复与新局检查；`Docs/Normal-Player-031.md`：封存0.2.10正常玩家局。
- `Docs/Performance-027.md`：封存0.2.7客户端50/100/200/400活动单位的历史性能诊断；0.2.28未重测该矩阵。
- `Docs/Evidence/042`：镜头边缘有效红例、规则与场景测试、构建输入和本轮可用原生证据；`Docs/Iteration-042.md`记录实际验证边界。
- `Docs/Evidence/041`：经验结算回归与0.2.18构建证据，受控原生XP专项尚未完成。
- `Docs/Full-Scope-Audit.md`和`Docs/Known-Issues.md`：完整目标与未解决范围；历史报告不自动升级为当前版本的验收结果。

在 macOS 运行 `Tools/test.sh` 执行 Unity 测试；`Tools/build-macos.sh` 准备原生场景和 URP 配置并生成客户端。可通过 `UNITY_EDITOR` 指定同版本编辑器，通过 `FRONTIER_BUILD_PATH` 指定输出 app。脚本根据自身位置找到项目，不依赖开发者的私人绝对路径。首次打开需要 Unity 通过 Package Manager 恢复依赖。

`Tools/compare-event-logs.py <预期JSONL> <实际JSONL>` 可比较完整事件文件；不承诺跨平台重放。`Tools/build-character.sh` 用 Blender 5.2.1 LTS 再生原创人物源，先审查输出后再替换运行时FBX。

`Tools/verify-delivery.py --reports-dir <报告目录>` 只检查源码包装、元数据、版本和真实 XML 测试结果；它不能证明游戏已完成。命令行 `-frontierAutoplay -frontierAutoSpeed 8` 用于客户端自动完整对局验证；`-frontierBenchmark 50`（或 100、200、400）是单独压力夹具，提升运行时生命以维持活动数量，与正常对局明确分开。`-frontierInputTrace` 记录鼠标、键盘与 UGUI 按钮事件用于排查输入。

客户端自己的 `Reports`、`Captures`、`Benchmarks` 目录位于 Unity 的持久化应用数据目录，即 macOS 用户 Library/Application Support/Frontier Workshop/Frontier Reckoning 下。正常游戏不从验证报告中加载资源或单位。

源码压缩包排除 Library、Temp、Logs、Builds、obj 等可再生成缓存；客户端另行打包。未实现存档或确定性回放，不宣称具备联网同步能力。独立项目名称、文案、网格、UI 与合成音频均为本项目制作，不隶属于《帝国时代》官方。
