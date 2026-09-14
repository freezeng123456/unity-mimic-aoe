# 标准陆战贸易路线、贸易站与宝藏规则核查

访问日期：2026-09-14。本文服务于 Frontier Reckoning 的阶段 C 研究合同，只使用 Age of Empires / World's Edge 的公开一手页面作为原作证据。页面通常记录机制存在、地图脚本和修复，而不是完整的当前客户端平衡表；因此成本、建造时间、车速、收益和刷新周期没有一手证据时均保持 U。本文不下载或复用商业游戏素材。

## 结论边界

### V：官方页面明确支持

| ID | 已核实机制 | 一手来源 |
|---|---|---|
| TT-V01 | 标准地图可以有贸易路线及其 Trade Post socket；官方更新把 1v1 的四个 socket、路线对称性和 socket 生成问题作为地图平衡与修复对象。 | [Update 56860](https://www.ageofempires.com/news/age-of-empires-iii-definitive-edition-update-56860/)、[Update 54545](https://www.ageofempires.com/news/aoeiii-definitive-edition-update-54545/) |
| TT-V02 | 玩家可在贸易路线 socket 建造 Trading Post；官方还区分贸易路线贸易站和 Native Settlement 贸易站。贸易站可提供路线相关的资源/单位/科技功能，具体内容随地图、文明和升级变化。 | [Update 13.12327](https://www.ageofempires.com/news/age-of-empires-iii-de-update-13-12327/)、[Update 43871](https://www.ageofempires.com/news/aoeiiide-update-43871/) |
| TT-V03 | 贸易路线上的贸易站可以切换其产生的资源；官方更新明确提到 AI 能改变贸易站在贸易路线上收集的资源，且升级影响这种能力。 | [Update 54545](https://www.ageofempires.com/news/aoeiii-definitive-edition-update-54545/) |
| TT-V04 | Stagecoach 是贸易路线升级；官方更新明确写到在研究 Stagecoach 前，贸易路线不可提供 Influence。由此可确认贸易路线有可研究的阶段性升级和资源类型状态，但不能由该片段恢复完整升级树。 | [Update 13.27885](https://www.ageofempires.com/news/ageiii_de-update-13-27885/)、[Update 54545](https://www.ageofempires.com/news/aoeiii-definitive-edition-update-54545/) |
| TT-V05 | 贸易站提供可见性/视野贡献；官方更新描述被转换的 settlement socket 会提供周围视野，建造 Trading Post 会进一步提高该视野。该页面还给出一个特定机制下的 0.35 XP/s，但不能推广为所有标准贸易站的当前数值。 | [Update 13.12327](https://www.ageofempires.com/news/age-of-empires-iii-de-update-13-12327/) |
| TT-V06 | 贸易路线是可争夺的地图目标。官方地图说明把控制贸易路线描述为早期竞争点，并明确指出忽略它可能带来严重经济后果。 | [Update 13.690](https://www.ageofempires.com/news/age-of-empires-iii-de-update-13-690/) |
| TT-V07 | Explorer 可以探索地图并击败宝藏守卫；宝藏奖励可包括经验、资源、单位或其他优势。 | [An Inside Overview of Age of Empires III: Definitive Edition](https://www.ageofempires.com/news/aoe3de-inside-overview/) |
| TT-V08 | 宝藏和守卫是地图生成对象，官方会调整宝藏集合、难度分布、守卫位置和宝藏可达性；官方修复也特别防止宝藏生成在 native settlement 内或守卫落在悬崖上。 | [Update 26865](https://www.ageofempires.com/news/aoeiii-de-update-26865/)、[Update 13.58326](https://www.ageofempires.com/news/age-of-empires-iii-de-update-13-58326/)、[Update 19.18309](https://www.ageofempires.com/news/age-of-empires-iii-de-update-19-18309/) |
| TT-V09 | 宝藏并非全部无人看守：官方更新同时讨论 guarded 与 unguarded coin treasures，并调整两者数量/频率；因此守卫强度和奖励分布是地图/宝藏集合的一部分。 | [Update 13.58326](https://www.ageofempires.com/news/age-of-empires-iii-de-update-13-58326/) |
| TT-V10 | 贸易路线与宝藏可共同构成探索和控制目标；官方对 Saharan Routes 的说明同时要求控制被 bandit 占据的贸易路线、寻找宝藏，并将两者作为地图核心玩法。该结论适用于该地图设计语境，不等于每张标准地图都采用相同布局。 | [Update 50830](https://www.ageofempires.com/news/aoeiii-de-update-50830/) |

### U：公开页面不足以冻结

- 标准遭遇战每张地图的 socket 数量、路线长度、车队/贸易单位速度、生成节奏和对称约束的完整公式。
- 手动建造 Trading Post 的准确成本、施工时间、占地、建造者规则、摧毁/夺取/重建行为，以及路线贸易站与 native settlement 贸易站的全部差异。
- 默认贸易站资源类型、tick 频率、每分钟收益、Stagecoach 及后续路线升级的完整效果和前置条件。`0.35 XP/s` 是官方某个 settlement 转换/贸易站语境的明确片段，不能当作普遍标准值。
- Trade Monopoly 的完整胜负条件、倒计时、队伍共享规则和被摧毁贸易站的处理；官方页面只明确某些版本中两个贸易站即可启用该胜利条件，不能替代锁定客户端版本后的完整规则表。[Update 13088](https://www.ageofempires.com/news/aoe3de-update-13088/)
- 宝藏的完整奖励表、奖励数值、难度分层、刷新/一次性规则、守卫编成、Explorer 的攻击与收集时间，以及多人争抢时的归属判定。
- 宝藏守卫是否给独立击杀经验、宝藏收集经验与奖励之间如何拆分；现有 XP 文档只把宝藏奖励经验标为 V，不推导未公布公式。
- 标准地图是否保证每名玩家可达的宝藏数量、路线 socket 距离、资源公平性和绕行路线；官方只在具体地图更新中说明局部修正。

## Frontier Reckoning 的实现合同

下面是项目提案（P），用于形成真实的探索和经济决策闭环，不是 AoE3:DE 数值复刻。任何数值都必须在项目数据表中标记 P，直到锁定参考客户端并完成逐事件观察。

### P0：地图对象与可见性

在首张 1v1 陆图生成一条双方都可达的陆上路线，放置至少 3 个中立 socket，并在每个出生区外放置至少 2 个有守卫和 1 个无守卫宝藏。socket、宝藏、守卫遵守未探索/已探索/当前可见三态战争迷雾；不可见目标不得被玩家 UI、自动攻击或 AI 读取。地图生成日志记录 seed、坐标、对象类型、可达性和所属 socket。

### P1：贸易站最小闭环

玩家或 AI 的村民可在空 socket 发起建造，施工期间可被打断，完工后占用该 socket并提供视野。贸易站至少有两种可选择的资源路线模式；模式切换写入事件日志，并由固定模拟时间周期结算收入。敌方可攻击并摧毁贸易站，摧毁后停止收入和视野；重建必须重新支付 P 成本。路线收益必须进入资源账本，不能用脱离对象状态的全局定时加钱伪装。

建议首个原型参数（P）：木材 200、施工 60 秒、每 10 秒产生 35 食物或 25 木材或 20 钱币，单个 socket 同时只能有一个贸易站。以上数字只用于可玩性占位，不能写成原作值；在 `Implemented-Numbers-020.md` 或单独 trade 数据表中保留来源标记。

### P2：宝藏最小闭环

Explorer 对可见宝藏发起收集；有守卫时必须先击败守卫，守卫死亡后宝藏才可收集。收集结算一次性奖励，奖励类型至少覆盖资源箱、经验和一个短期军事/经济效果中的两类；奖励进入真实资源、XP 或效果账本。宝藏被收集后从世界移除并记录 `TreasureClaimed`，重新开始对局必须清除所有运行时状态。无守卫宝藏仍需探索和到达，不能在地图载入时直接入账。

建议首个原型参数（P）：无守卫宝藏 100 食物或 75 木材；有守卫宝藏 150 钱币或 100 XP；守卫由 2–4 个原创轻型单位组成。奖励、守卫和成本数字均为 P；不要用这些数值宣称复刻原作。

### P3：AI 与验收

AI 与玩家通过同一建造、攻击、收集和资源结算接口行动。AI 只能在视野中发现宝藏或敌方贸易站；至少有一个策略分支会在早期派 Explorer 争夺宝藏，另一个分支会优先占贸易站或防守路线。标准对局记录应能回答：谁先发现、谁承担守卫战斗、贸易站何时完工、切换了什么资源、收益何时入账、宝藏是否因争夺而失效。

最低验收包括：

1. 通过资源账本重放一座贸易站从建造、产出、被摧毁到停止产出的完整生命周期；没有隐藏收入。
2. 通过事件日志验证同一宝藏只能被收集一次，守卫未死亡时不能领取，收集奖励与 XP 各只结算一次。
3. 用两个不同 seed 验证路线 socket、宝藏和守卫均可达，且没有生成到建筑、悬崖或不可见越界位置。
4. 在一场玩家对 AI 的正常速度对局中观察到至少一次侦察、宝藏争夺或贸易站争夺，并从开局持续到胜负结算；自动化测试和菜单显示不能替代这项体验验证。
5. 在暂停、重新开始、玩家失败和 AI 失败后确认贸易收入、宝藏状态、守卫实体、视野和事件日志不会跨局残留。

## 与当前 0.2.0 的关系

当前工程已具备原创 Explorer 实体和战争迷雾/资源账本框架；本文件记录的贸易路线、Trade Post socket、贸易收益、宝藏守卫和收集奖励仍应按“尚未实现”管理。实现这些对象前，不应把当前 1v1 对局称为包含贸易与宝藏的完整陆战版本。当前项目已有的 Bank、船运和 XP 数值继续以 `Implemented-Numbers-020.md`、`Reference-XP-020.md` 的 P/U 边界为准；本文件不修改运行时代码或既有数值表。

