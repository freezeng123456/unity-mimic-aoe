# Frontier Reckoning：战斗、船运与战争规则参考

本文件为原创 Unity RTS 的规则边界文档。它只把《Age of Empires III: Definitive Edition》官方公开资料能够直接支持的内容标为“已核实”；官方没有公开或版本依赖的细节标为“待核实”。本项目名称、文明、卡牌、文案、美术和数值均应保持原创，不提取商业游戏资产。

## 参考版本与证据范围

主要依据 World's Edge / Age of Empires 官方网站的 DE 介绍、发布说明和官方更新日志。更新日志会随版本改变数值，因此实现时必须记录目标参考版本和日期；下面不把某一条平衡补丁的瞬时数值当作永恒规则。

| 规则 ID | 结论 | 状态 | 官方一手来源 | 支持范围 |
|---|---|---|---|---|
| REF-SHIP-001 | 主城船运以卡牌表示；玩家在对局中随经验增长请求船运，内容可以是资源、单位或其他特殊奖励。 | 已核实 | [官方 DE overview](https://www.ageofempires.com/news/aoe3de-inside-overview/) | 核实系统性质，不核实每张卡的具体数值、时间或费用。 |
| REF-SHIP-002 | 每局带入可自定义牌组；官方介绍写明牌组最多 25 张，建牌时有 100+ 张卡可选，并可预设多个牌组。 | 已核实 | [官方 DE overview](https://www.ageofempires.com/news/aoe3de-inside-overview/) | 可作为本项目早期牌组合法性与 UI 的依据；本项目卡池规模可主动缩小。 |
| REF-SHIP-003 | 经验是船运机会的来源；官方明确举例，收集宝藏可获得经验、资源、单位或其他优势。 | 已核实 | [官方 DE overview](https://www.ageofempires.com/news/aoe3de-inside-overview/) | 支持“事件入账 XP”，不支持固定 XP 数值或完整事件表。 |
| REF-AGE-001 | 升时代时需要选择政治家；每个时代提供多个选择，政治家提供各自奖励，可包括资源、军事升级、单位等。 | 已核实 | [官方 DE overview](https://www.ageofempires.com/news/aoe3de-inside-overview/) | 支持 I–V 可扩展的政治家数据模型；费用、时间和具体奖励须按目标版本另查。 |
| REF-CIV-001 | 文明拥有不同的卡牌、单位、技术和文明加成；官方将英国、荷兰列为独立文明，并在更新中分别记录文明与卡牌改动。 | 已核实 | [官方 DE baseline changelog](https://www.ageofempires.com/news/aoe3de-baseline-update/), [官方 update 61213](https://www.ageofempires.com/news/age-of-empires-iii-definitive-edition-update-61213/) | 支持先做英国镜像，再做荷兰真实机制差异；具体实现内容必须由项目自己的原创设计定义。 |
| REF-CIV-002 | 官方更新日志记录过荷兰银行上限、采金加成等文明相关机制，也记录英国庄园等独有内容。 | 已核实（版本依赖） | [官方 update 20322](https://www.ageofempires.com/news/aoe3de-update-20322/), [官方 update 61213](https://www.ageofempires.com/news/age-of-empires-iii-definitive-edition-update-61213/) | 证明文明差异可以改变经济决策；不要直接复制这些商业游戏数值或卡牌文本。 |
| REF-COMBAT-001 | 官方更新日志使用明确的单位标签、目标加成/倍率、抗性和类别（例如重步兵、重骑兵、炮兵），并会说明倍率叠加案例。 | 已核实 | [官方 baseline changelog](https://www.ageofempires.com/news/aoe3de-baseline-update/) | 支持本项目使用标签驱动的攻击类型、目标类别和抗性数据；不支持从公开日志推导完整全单位表。 |
| REF-COMBAT-002 | 更新日志明确记载过“对重步兵的倍率”和“一般步兵倍率”的叠加案例，说明加成不是单一攻击减防公式。 | 已核实（存在性） | [官方 update 61213](https://www.ageofempires.com/news/age-of-empires-iii-definitive-edition-update-61213/) | 支持实现可解释的多标签修正管线；叠乘、叠加顺序、同源效果是否取最高等仍待核实。 |
| REF-COMBAT-003 | 官方资料区分单位的攻击、射程、速度、生命值等属性，并持续修正具体单位的伤害、射程、倍率和建造/击杀 XP bounty。 | 已核实（存在性） | [官方 DE baseline changelog](https://www.ageofempires.com/news/aoe3de-baseline-update/), [官方 update 61213](https://www.ageofempires.com/news/age-of-empires-iii-definitive-edition-update-61213/) | 支持共享数据源；不把日志中的单次修正误写成通用战斗公式。 |
| REF-COMBAT-004 | 伤害结算的完整弹道模型（追踪/非追踪、命中时机、目标死亡后的弹丸处理）以及最小射程、友伤、减速和炮兵部署规则，官方公开文章未完整定义。 | 待核实 | 未找到足够的官方一手公开规范 | 进入实现前需通过目标版本实机观察、官方补丁交叉核对或保守的主动简化，并在游戏规则文档中标注。 |
| REF-VICTORY-001 | DE 具有 Supremacy、Treaty、Deathmatch 等模式；官方更新还单独记录 Trade Monopoly 胜利条件。 | 已核实 | [官方 Empire Wars update](https://www.ageofempires.com/news/aoeiii-de-update-50830/), [官方 update 13088](https://www.ageofempires.com/news/aoe3de-update-13088/) | 首期应明确选择“标准遭遇战/Supremacy 风格”规则，不混入 Treaty、Deathmatch 或 Tycoon 条件。 |
| REF-VICTORY-002 | Trade Monopoly 是独立胜利条件；官方说明标准规则下地图有 2 个贸易站时可用，Treaty 中不可用。 | 已核实（版本依赖） | [官方 update 13088](https://www.ageofempires.com/news/aoe3de-update-13088/) | 首期陆战可后置；若加入，必须有占据、启动和持续计时状态，不得把摧毁首座城镇中心当默认胜利。 |
| REF-VICTORY-003 | Tycoon 模式有独立经济胜利：达到最终时代、领先 10,000 Tycoon 分并维持 5 分钟。 | 已核实（非首期模式） | [官方 Tycoon mode](https://www.ageofempires.com/news/ageiiide-tycoon-mode/) | 仅作为模式差异参考，不能移植到标准遭遇战。 |
| REF-VICTORY-004 | 新增的 Regicide 模式以消灭敌方 Regent 立即获胜；Independence 模式移除主城与 XP 支持。 | 已核实（后置模式） | [官方 Baltic Powers announcement](https://www.ageofempires.com/news/the-baltic-powers-expansion-coming-to-age-of-empires-iii-definitive-edition-join-the-open-beta/) | 证明胜负条件与主城系统是可配置的；首期不实现这些模式。 |
| REF-FOG-001 | 官方公开资料确认游戏包含地图与侦察导向的玩法（例如宝藏探索、贸易路线、地图控制），但没有给出可供复刻的战争迷雾状态规范。 | 部分核实 / 细节待核实 | [官方 DE overview](https://www.ageofempires.com/news/aoe3de-inside-overview/), [官方 update 13088](https://www.ageofempires.com/news/aoe3de-update-13088/) | 项目应采用未探索、已探索不可见、当前可见三态设计作为主动实现方案，并让单位、目标选择、自动攻击、小地图和 AI 共用可见性查询。三态的内部原作等价性未宣称。 |

## 明确待核实的关键数值与公式

以下项目不能仅凭上述官方文章确定：XP 门槛是否按固定表、递增比例或文明/模式修正计算；每次船运的等待/运输时间和多个船运是否排队；卡牌一次性、可重复与发送次数的完整规则；船运出生点受阻、接收建筑被毁、人口不足时的原作处理；多标签目标加成的叠加顺序、上限和抗性优先级；弹丸追踪与命中结算；迷雾下自动攻击和 AI 感知的精确边界；标准遭遇战的完整投降、消灭与队伍判定细节。实现前应以锁定的目标版本实机测试并保留观察记录，或将其标记为本项目主动简化。

## 可用的主动简化（不是原作规则声明）

1. 首期牌组限制为 10–12 张原创卡，保留时代限制、合法性检查、发送机会消耗、资源箱/单位增援/持续效果三类；先不声称 25 张与完整官方卡池。
2. XP 采用事件表（探索、宝藏、建造、战斗）和可配置阈值；所有值在 `Rules-FrontierReckoning.md` 中标为项目参数，直到完成目标版本核对。
3. 船运采用显式队列和可配置到达延迟；接收点受阻时进入等待/改用主城安全点的项目规则，并在 UI 告知玩家，不宣称与原作一致。
4. 战斗采用标签集合、攻击类型、抗性与目标修正的可审计结算顺序；同一攻击事件只结算一次，视觉动画不独立扣血。所有倍率先作为项目参数，不能冒充官方倍率。
5. 首期只实现三态战争迷雾和标准歼灭胜负：双方所有城镇中心及关键生产能力被摧毁、且无可继续作战的单位/建筑时判负；不把第一座城镇中心被摧毁直接判胜。投降可作为显式按钮与事件记录。
6. 英国镜像对战先覆盖 I–III 时代；荷兰机制差异与 IV–V 时代列入后续里程碑。阶段性版本须标记为“前三时代核心可玩对局”，不能称为完整复刻。

## 实现验收钩子

- 每次资源、人口、XP、卡牌发送和伤害都记录事件来源与模拟时间。
- 同一单位数据同时驱动按钮说明、费用、前置条件和运行时结算；共享配置资产不保存运行时生命值。
- 玩家与 AI 通过同一指令与经济接口；AI 在迷雾外不得读取敌方实时状态。
- 至少覆盖：XP 与船运队列、人口不足、接收点被毁、重复持续效果、目标死亡、标签倍率、暂停恢复、重新开始状态清理，以及未探索/已探索/当前可见三态。
- 最终完整性必须通过独立客户端从开局到结算的真实对局与视觉检查确认；编译成功、自动化测试通过或菜单存在都不构成完整对局证据。

## 来源清单

- World's Edge, “An Inside Overview of Age of Empires III: Definitive Edition”: https://www.ageofempires.com/news/aoe3de-inside-overview/
- World's Edge, “Age of Empires III: Definitive Edition — Release Day Gameplay Changelog”: https://www.ageofempires.com/news/aoe3de-baseline-update/
- World's Edge, “Age of Empires III: Definitive Edition — Update 13088”: https://www.ageofempires.com/news/aoe3de-update-13088/
- World's Edge, “Age of Empires III: Definitive Edition — Update 20322”: https://www.ageofempires.com/news/aoe3de-update-20322/
- World's Edge, “Age of Empires III: Definitive Edition — Update 61213”: https://www.ageofempires.com/news/age-of-empires-iii-definitive-edition-update-61213/
- World's Edge, “Try Tycoon Mode in Age of Empires III: DE!”: https://www.ageofempires.com/news/ageiiide-tycoon-mode/
- World's Edge, “The Baltic Powers Expansion Coming to Age of Empires III: Definitive Edition”: https://www.ageofempires.com/news/the-baltic-powers-expansion-coming-to-age-of-empires-iii-definitive-edition-join-the-open-beta/
