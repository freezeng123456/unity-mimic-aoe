# AoE3:DE 经验来源与船运门槛：有界核查

核查日期：2026-09-14。目标是判断普通采集、自然经验涓流、训练/建造/击杀经验以及船运门槛能否由公开资料冻结。检索优先使用 World's Edge / Age of Empires 官方页面；没有找到公开官方手册或教学逐项列出完整 XP 表，因此本文不把项目当前的 `0.1 XP/资源` 或 `150 + 75n` 写成原作规则。

## 官方一手证据（V）

| ID | 结论 | 参考版本/日期与来源 |
|---|---|---|
| XP22-V01 | 官方开发者总览明确：文明在对局中获得经验后可以请求 Home City shipments；船运内容可以是资源、单位或其他奖励。 | DE 开发者总览，约 2020-10-15：[An Inside Overview of Age of Empires III: Definitive Edition](https://www.ageofempires.com/news/aoe3de-inside-overview/) |
| XP22-V02 | 官方同一页面明确：收集宝藏会给予经验、资源、单位或其他优势。因此宝藏奖励经验是已核实来源；探索地图本身是否单独给经验没有被该页面确认。 | DE 开发者总览，约 2020-10-15：[An Inside Overview](https://www.ageofempires.com/news/aoe3de-inside-overview/) |
| XP22-V03 | 官方更新反复使用并调整建筑的 build XP bounty、单位/建筑的 kill XP bounty。这证明建造完成和击杀事件存在 XP bounty 概念，但更新片段不能重建所有对象的数值表。 | Update 61213（约 2021-12-09）[官方页面](https://www.ageofempires.com/news/age-of-empires-iii-definitive-edition-update-61213/)，Update 13.58326（约 2022-06-23）[官方页面](https://www.ageofempires.com/news/age-of-empires-iii-de-update-13-58326/) |
| XP22-V04 | 官方更新记录某些文明建筑或特殊机制产生 XP trickle，说明自然/被动 XP 可以是文明或建筑例外；不能由此推出所有普通村民都在采集时产生同样涓流。 | Update 50830（约 2021-08-12）[官方页面](https://www.ageofempires.com/news/aoeiii-de-update-50830/)，Update 19.18309（页面标注近期更新）[官方页面](https://www.ageofempires.com/news/age-of-empires-iii-definitive-edition-update-19-18309/) |
| XP22-V05 | 官方发行日变更日志明确写出“Per-shipment experience points required decreased to 72% (from 75%)”。这证明船运门槛存在递增/按船运计算的经验系数，并且文明或版本可以改变该系数；它没有给出基础门槛、每次增加量或完整曲线。 | Release Day Gameplay Changelog，约 2020-10-15：[Baseline Update](https://www.ageofempires.com/news/aoe3de-baseline-update/) |
| XP22-V06 | Empire Wars 官方说明船运 earned more frequently、arrive faster，并在 Commerce Age 额外奖励一次船运；这是该模式的特例，不能套用到标准遭遇战。 | Empire Wars，约 2021-05-11：[官方说明](https://www.ageofempires.com/news/aoeiii-de-empire-wars/) |
| XP22-V07 | 官方页面明确记录某些卡牌/建筑提供 XP trickle 或特定 XP 效果，因此 XP 不是只有击杀一种来源；这些是有条件的文明或内容例外。 | Update 38254（约 2021-06-03）[官方页面](https://www.ageofempires.com/news/aoe3de-update-38254/)、Update 50830 |

官方页面中与本问题最接近的直接表述，短引文均少于 25 个英文词：`Each treasure collected will grant you a bonus in the form of experience...`（XP22-V02）；`Per-shipment experience points required decreased to 72% (from 75%)`（XP22-V05）。其余结论均为释义。

## 仍不能冻结的项目（U）

- **普通采集是否给 XP：U。** 在本次官方检索范围内没有找到官方手册、教学页或补丁表明确说明每单位 Food/Wood/Coin 采集是否产生 XP，也没有找到可据此确定采集 XP 公式的官方数据。官方总览只说明资源即时进入库存。
- **通用自然 XP 涓流：U。** 官方更新能证明某些建筑/文明有 XP trickle，但没有给出标准遭遇战所有文明的通用被动速率。不能因社区常用的“passive trickle”说法把一个数值写成 V。
- **训练 XP 与建造 XP 是否分开：U/V 边界。** `build XP bounty` 与 `kill XP bounty` 的官方措辞证明至少存在按事件类别的 bounty；公开片段没有完整说明训练完成是否另有 XP、训练 XP 是否按单位成本或固定值计算，也没有说明建造取消、摧毁或转化的结算。因此“事件类别可能分开”是 V 的结构线索，具体公式和数值是 U。
- **击杀 XP：U/V 边界。** 官方更新证明 kill XP bounty 存在，但没有公开完整的单位、建筑、守卫、友军误伤、过杀或最后一击规则，不能把项目使用的实体 `xp` 直接称为原作公式。
- **船运曲线：U。** 72%/75% 只证明某次版本/文明修正了“每次船运所需经验”的比例；基础 XP 门槛、递增量、是否按 shipment level、文明修正、初始可用船运和四舍五入规则均未由本次官方页面冻结。
- **模式和版本差异：U。** Empire Wars 的更快船运和额外 Commerce Age 船运是模式规则；不能反推标准 Supremacy/遭遇战。Baseline Update 的 72% 也必须绑定发行版本和适用文明，不能当作当前所有文明通用常数。

## 二手线索（仍标 U，不是官方事实）

官方论坛讨论页有人声称普通游戏存在约 4 XP/约 3 秒的 passive trickle，并认为探索本身不给 XP、宝藏才给 XP；另一位参与者称建造和摧毁会给额外 XP。[Is it true that all activities generate experience?](https://forums.ageofempires.com/t/is-it-true-that-all-activities-generate-experience/254463)（2024-05-25）。这是社区讨论，未提供官方数据表或可复现实验记录，只能作为后续观测假设：`可能存在通用涓流`、`探索本身可能不奖励 XP`。该线索不能升级为 V，也不足以确定训练/采集是否给 XP。

## 对 Frontier Reckoning 0.2.1 的影响

当前项目的 `0.1 XP/资源`、建筑/训练/击杀复用实体 XP、普通经济循环不产生通用被动 XP，以及 `150 + 75n` 船运门槛，均继续标为 P 项目规则。它们可以用于可玩性和测试，但必须在 UI/数据文档中避免标成 AoE3:DE 精确复刻。

若以后要缩小不确定性，建议锁定一个具体 DE 客户端版本，建立仅用于观察的空场景并逐项记录：初始 XP、固定时间间隔的 XP 读数、单次资源采集前后读数、单个单位训练完成前后读数、建筑完工前后读数、击杀不同单位前后读数、宝藏收集前后读数，以及连续发送时每次达到门槛的 XP。每项都要区分自然时间经过、事件 bounty 和文明/建筑例外；没有这种版本绑定的事件账本，不应把社区曲线或项目曲线升级为 V。

## 本次有界检索范围

已检索官方域名 `ageofempires.com/news` 中与 DE 经验、船运、宝藏、建筑/击杀 bounty 和模式差异直接相关的页面：开发者总览、Baseline Update、Empire Wars、Updates 38254、50830、61213、13.58326、19.18309，并查询官方论坛中与“all activities generate experience”直接相关的讨论作为二手线索。没有找到可公开下载且逐项定义普通采集 XP、通用涓流、训练 XP 或完整标准遭遇战门槛曲线的官方手册/教学/实录数据；因此在上述范围内停止扩展检索，保留这些问题为 U。

