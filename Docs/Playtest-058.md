# 烽岸纪元 0.3.1 · 本地新版本试玩说明

本轮加入英、荷、法、奥斯曼四个经典文明的核心经济和兵种玩法，并保留远驿盟市、山岭工联两个原创文明。下文使用客户端中的英文按钮名，数值是本项目候选平衡。0.3.1 源码已通过 **527 项测试（284 EditMode＋243 PlayMode）**；原36组合矩阵35局自然结束、1局山岭镜像超时（经典文明相关32组全部自然结束），同初源码延长观察在3652.01秒自然结束，原35/36不变。初构建完成法奥双向16x自动原生整局，最终源码仅修复菜单排版并新增布局回归；最终构建与1600×1000菜单回查已完成，奥斯曼简介完整且不遮挡按钮；原生工具限制使普通1x手动开局及按钮点击未完成。两次构建身份分开记录，进度见[完成状态](Completion-Status.md)。本指南不代替实际客户端验收。

## 选择文明与卡组

主菜单点 **YOU:** 选择自己，点 **AI:** 选择对手。文明面板第一页依次为 **BRITISH**、**DUTCH**、**FRENCH**、**OTTOMANS**；点 **NEXT 1/2** 可找到 **WAYFARER CITIES** 和 **HIGHLAND GUILD**，点 **PREVIOUS** 返回。双方可选择相同文明。

| 文明 | 经济特点 | 主要特色部队 | 当前可选卡牌 |
|---|---|---|---:|
| British（英国） | 135 木材的 Manor 完工赠村民；村民上限 99 | Longbowman 长弓兵，配合 Musketeer、Hussar | 46 |
| Dutch（荷兰） | 村民花费 100 钱币，上限 50；矿山采币快 15%；Bank 持续产币 | 时代 II Skirmisher，时代 III Ruyter、Halberdier | 48 |
| French（法国） | 120 食物的 Coureur des Bois，采集快 25%，上限 80 | 时代 III Cuirassier 胸甲骑兵，近战范围伤害 | 46 |
| Ottomans（奥斯曼） | 城镇中心免费自动招工，基础周期 55 秒、初始村民上限 25；Mosque 科技改善两项限制 | Janissary、Abus Gunner，船运专属 Spahi | 45 |
| Wayfarer Cities（远驿盟市） | 付货本、运输、到站结算的商路 | Caravan Escort、Road Outrider、Convoy Gunner | 50 |
| Highland Guild（山岭工联） | 工坊在场工人制造预制件和猎炮 | Field Engineer、Guild Culverin、Pavise Guard | 50 |

可选卡池已过滤文明不能使用的兵种和科技，因此卡池总数不等于一局携带数量。点 **PRESET:** 在 **ECONOMY**、**MILITARY**、**TECHNOLOGY** 三套 25 张预设间轮换；点 **EDIT DECK**，用 **[ADD]** / **[IN DECK]** 修改，**PREVIOUS** / **NEXT** 翻页，**DONE** 返回。组牌上限为 25 张。

将 **ERA LIMIT** 设为 `5 / 5`，点 **BEGIN SKIRMISH**。对局中按 **H** 或点 **HOME CITY [H]**，才能寄送已带入卡组的卡牌；船运需要机会、时代条件和运输时间，单位需要人口及出兵空间，资源箱仍需村民采集。

## 法国：更贵的猎民与胸甲骑兵

法国开局的 6 名工人均为 **Coureur des Bois**。选城镇中心后，点同名训练按钮支付 **120 食物**，不是普通 Settler 的价格。猎民仍负责采集、建造和维修；工作时使用工具，战斗时可以用枪，也可点 **MELEE MODE** 切换近战。**BUILD [B]**、**REPAIR [R]** 和 **DETAILS** 保留独立按钮。较强的自卫能力不能替代正规军，损失猎民会同时损失昂贵的经济单位。

时代 III 在 **Stable** 训练 **Cuirassier**：150 食物、150 钱币，占 3 人口。升级前主目标近战基础伤害 30，目标周围半径 2 内的其他敌方或守卫单位受到 50% 基础伤害，即 15，再各自计算克制与抗性。范围打击不波及建筑、资源、己方或驻扎单位。法国仍可使用 Hussar 和 Skirmisher，可先建立常规军再投入胸甲骑兵。

## 奥斯曼：看清免费招工为什么停下

选中 **Town Center**，信息区显示 **Free settlers** 的当前进度 / 实际周期，以及 **Workers 已有数量/村民上限**。正常状态显示 **Recruiting automatically**。自动招工不扣食物或钱币，也不占付费训练队列；界面不显示误导性的付费 Settler 按钮。

点 **AUTO SETTLERS: ON** 暂停该城镇中心的自动招工，按钮变为 **AUTO SETTLERS: OFF**；再点一次恢复。按钮文字表示当前状态。暂停保留已积累的进度。

| 信息区提示 | 处理方法 |
|---|---|
| Paused by player | 点 **AUTO SETTLERS: OFF** 恢复 |
| Town center queue busy | 等城镇中心的时代晋级等队列完成 |
| Need more housing | 建房或释放人口；预留中的人口也计入限制 |
| Settler limit reached (…) | 研究 Mosque 的村民上限科技，单纯建房不能提高文明村民上限 |
| Town center exit blocked | 腾出城镇中心出兵位置 |

阻塞期间不会在后台积攒一批免费村民，障碍消失后按保留进度继续。免费招工同样受整个对局的人口上限约束。

时代 I 选村民按 **B**，建造 **Mosque**（100 木材）。完工后选中清真寺，点 **TECHNOLOGIES**；**只建清真寺不会自动完成下列研究**。

| 科技按钮 | 时代 / 木材费用 | 效果 |
|---|---|---|
| Millet System | I / 75 | 自动招工速度增加 10% |
| Koprulu Viziers | II / 150 | 再增加 15%，需要 Millet System |
| Grand Bazaar | III / 250 | 再增加 20%，需要 Koprulu Viziers |
| Galata Tower District | II / 100 | 村民上限 25 → 45 |
| Topkapi | III / 200 | 上限 45 → 70，需要 Galata Tower District |
| Tanzimat | IV / 400 | 上限 70 → 99，需要 Topkapi |

每项基础研究时间 40 秒。招工速度加成相加后换算周期，不能直接当作减少同等百分比秒数；只研究上述三项速度科技时，周期从 55 秒变为约 37.9 秒。卡牌和时代路线也可能改变速度，以城镇中心显示的实际周期为准。这些招工科技不加速军事单位训练。

奥斯曼部队操作：

- **Janissary**：时代 II，**Barracks** 训练，100 食物、25 钱币、1 人口。枪击与近战弯刀是两种攻击；对骑兵的额外伤害只属于近战，可用 **MELEE MODE** 主动切换。
- **Abus Gunner**：时代 II，**Foundry** 训练，50 食物、100 钱币、2 人口。奥斯曼铸炮厂从时代 II 可建。阿布斯使用手持炮管，但规则分类仍是步兵，不能按有炮架的 Artillery 对待。
- **Spahi**：精锐重骑，占 3 人口，**不能在 Stable 付费训练**。先在卡组加入 **2 Spahi**（III）、**3 Spahi**（IV）或 **5 Spahi**（V），再从 **HOME CITY [H]** 寄送；仍需船运机会、人口和运输时间。

## 英国与荷兰的阵容变化

英国的 **Manor** 完工赠村民，扩人口也能扩经济；赠送仍受村民和人口限制。时代 II 的 **Longbowman** 用 60 食物、45 木材训练，射程长，对重步兵有效。英国当前名单没有普通 Skirmisher。

荷兰 **Bank** 从时代 II 开放，花费 350 食物、350 木材；基础上限 4 座，每座完工后每秒产生 2.5 钱币。村民需要钱币，矿山加成和银行收益要兼顾生产与部队开销。

荷兰的 **Skirmisher** 从时代 II 开始可训练；**Ruyter**（时代 III，40 食物、70 钱币、1 人口）承担远程反骑兵职责，**Halberdier**（时代 III，50 食物、70 钱币、1 人口）是持长柄戟的近战反骑兵。荷兰名单不提供普通 Musketeer 和 Dragoon。

## 时代路线与保留内容

选城镇中心，点 **ADVANCE AGE**，当前文明每次晋级有三个选项。支付时代费用、队列完成后，奖励和对应科技才生效；每次晋级可以换选另一条路线。

| 文明 | 三条路线 |
|---|---|
| British | Homestead Expansion（采集）、Local Defence（训练）、Longbow Command（长弓） |
| Dutch | Banking Houses（银行）、Commercial Expansion（采币）、Contract Army（骑兵） |
| French | Rural Homesteads（猎民采集）、Cavalry Commission（骑兵）、Infantry Commission（步兵） |
| Ottomans | Provincial Administration（自动招工）、Janissary Commission（禁卫军）、Gunner Commission（阿布斯） |

远驿的 **TRADE ROUTE** 和每次 **60 食物货本**、山岭的 **Standard Jigs → MANUFACTURE**、右键工坊派工、**REPAIR [R]** 和盾阵仍可使用；完整操作见 [0.3.0 试玩说明](Playtest-057.md) 的远驿与山岭章节。该旧文档中的原两文明名称和卡池数字属于旧版，当前选择菜单以上表为准。

本轮复现上述经典文明的核心玩法，卡牌、时代路线和具体数值采用本项目规则，并非《帝国时代 III》完整国家与商业游戏内容量复刻。Envoy、Native Scout、Factory、Rocket、奥斯曼巨炮、完整政治家/革命、全套皇家卫队与治疗、完整官方卡池尚未加入。地图仍为 **THE TWO OUTCROPS**；新增地图、中立势力和单人场景留待后续，不计入本轮。当前样本也不足以证明六文明已充分平衡。

常用操作：右键执行移动、采集、建造或攻击；**Shift** 排队；**A** 攻击移动；**S** 停止；**V** 定位闲置工人；**T** 定位城镇中心；**Esc** 暂停。窗口失焦会暂停，返回后点 **RESUME**。
