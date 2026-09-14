# Frontier Reckoning：经济、时代与文明机制参考

本文件为 Unity/C# 原创 RTS **Frontier Reckoning** 的规则研究底稿。参考对象是《Age of Empires III: Definitive Edition》（以下简称 AoE3:DE）的标准遭遇战体验；本项目不使用其源代码、文案、插画或商业游戏素材。

## 证据边界

“已核实”只表示下方链接中的官方 Age of Empires / World's Edge 一手资料直接支持该描述。官方文章通常是机制概览或更新记录，并不等同于完整的当前数据文件；因此没有公开一手证据的采集速率、退款比例、年龄费用等内容保持“待核实”。“项目建议”是 Frontier Reckoning 的初期可玩数值，不应被误读为 AoE3:DE 原作数值。

## 已核实的参考机制

| 规则 ID | 状态 | 规则 / 具体支持内容 | 来源 |
|---|---|---|---|
| ECO-001 | 已核实 | 村民采集到的资源会**即时加入**库存，不需要回到城镇中心或投放点。这是与传统搬运式 RTS 的关键差异。 | [官方开发者总览：Resource Gathering](https://www.ageofempires.com/news/aoe3de-inside-overview/) |
| ECO-002 | 已核实 | 官方总览将资源采集作为地图扩张与争夺资源点的核心；更新说明和卡牌说明直接出现 food、wood、coin 等资源。 | [官方开发者总览](https://www.ageofempires.com/news/aoe3de-inside-overview/)、[官方发布日玩法变更](https://www.ageofempires.com/news/aoe3de-baseline-update/) |
| ECO-003 | 已核实 | 官方公开资料明确使用 food（食物）、wood（木材）、coin（钱币）作为主要经济资源；部分文明/模式还存在 export、influence 等额外资源。首期陆战框架至少实现前三者。 | [官方更新 61213（荷兰起始资源）](https://www.ageofempires.com/news/age-of-empires-iii-definitive-edition-update-61213/)、[官方游戏页](https://www.ageofempires.com/games/aoeiii) |
| ECO-004 | 已核实 | 资源点与贸易站是地图控制目标；官方总览明确说地图设计推动玩家争夺开放资源位置和 Trading Posts。 | [官方开发者总览：Map Layouts](https://www.ageofempires.com/news/aoe3de-inside-overview/) |
| ECO-005 | 已核实 | Home City 船运由经验推进；玩家从主城请求资源、单位或其他奖励，船运以卡牌表示。 | [官方开发者总览：Shipments](https://www.ageofempires.com/news/aoe3de-inside-overview/) |
| ECO-006 | 已核实 | 玩家预先配置卡组；官方总览记载卡组最多 25 张、可选卡牌超过 100 张。正式发行后的机制概览不能单独证明所有文明/模式都使用同一容量，故工程中将容量做成配置。 | [官方开发者总览：Shipments](https://www.ageofempires.com/news/aoe3de-inside-overview/) |
| ECO-007 | 已核实 | 时代推进需要选择 Politician；每个时代有多个选项，并在推进时给予独特奖励，例如资源、军队升级、单位或气球。 | [官方开发者总览：Politicians](https://www.ageofempires.com/news/aoe3de-inside-overview/) |
| ECO-008 | 已核实 | 官方发行日变更将 Discovery Age / Colonial Age 的名称更新为 Exploration Age / Commerce Age；官方政治家列表还列出 Fortress、Industrial、Imperial，支持 I—V 五时代框架。 | [官方发布日玩法变更：Representation Changes 与 Politicians](https://www.ageofempires.com/news/aoe3de-baseline-update/) |
| ECO-009 | 已核实 | 多数军事单位可批量训练；批量训练被官方描述为可快速招募成组部队的系统，并可与主城船运配合。 | [官方开发者总览：Batch Training](https://www.ageofempires.com/news/aoe3de-inside-overview/) |
| ECO-010 | 已核实 | 英国的 Manor House 是文明相关建筑；官方更新明确记录其成本变化，官方卡牌记录还明确提到 Manor Houses。该证据支持“英国住房是文明经济机制的一部分”，但不单独公开其完整村民生成公式。 | [官方更新 61213：British / Manor House](https://www.ageofempires.com/news/age-of-empires-iii-definitive-edition-update-61213/)、[官方发布日玩法变更：British cards](https://www.ageofempires.com/news/aoe3de-baseline-update/) |
| ECO-011 | 已核实 | 荷兰 Bank 是文明相关建筑；官方更新记录 Bank 的建造 XP bounty，另一次官方更新说明荷兰 AI 会建造更多 Banks。官方更新还记录荷兰起始资源与采矿加成变化，证明其经济路线与普通文明不同。 | [官方更新 61213：Dutch / Bank](https://www.ageofempires.com/news/age-of-empires-iii-definitive-edition-update-61213/)、[官方更新 19.18309：Dutch AI](https://www.ageofempires.com/news/age-of-empires-iii-definitive-edition-update-19-18309/)、[官方更新 20322：Dutch mining bonus](https://www.ageofempires.com/news/aoe3de-update-20322/) |
| ECO-012 | 已核实 | 官方技术访谈说明 DE 早期设计决定把船运卡牌和时代政治家全部解锁，以避免原版需要长时间游玩解锁好卡造成不公平；卡组与文明优势组合形成策略。 | [官方技术访谈：Cards & Politicians](https://www.ageofempires.com/news/interview-joss-ellis/) |
| ECO-013 | 已核实 | 经验不仅来自被动时间：官方总览明确提到 Explorer 击败宝藏守卫并收集宝藏可获得经验、资源、单位或其他优势。 | [官方开发者总览：Explorer](https://www.ageofempires.com/news/aoe3de-inside-overview/) |

## 尚未由公开官方一手资料核实的内容

这些项目不能在本工程中伪称为原作规则，须以后续官方可引用资料、受许可的数据源或游戏内实测（并记录版本）补齐：

- ECO-U01：各资源点（猎物、浆果、树木、矿藏、资源箱、农田等）的精确储量、采集率、衰减、刷新与资源箱入账时点。
- ECO-U02：村民多人施工的时间公式、施工效率上限、建造中的单位碰撞，以及建筑取消后的精确退款比例和退款时点。
- ECO-U03：人口上限、住房被毁后的超人口处理、训练/船运在出口堵塞或人口不足时的精确排队与失败行为。
- ECO-U04：五时代升级的每一项精确费用、升级时间、前置条件、政治家列表与奖励（官方概览证明机制存在，未提供完整平衡表）。
- ECO-U05：英国 Manor House 是否、何时、以何种速率额外生成 Settler，以及该效果与住房上限、卡牌和时代的关系。公开更新片段只足以确认建筑与成本/卡牌存在。
- ECO-U06：荷兰 Bank 的精确被动钱币收入、建造上限、人口/村民上限、卡牌对上限的作用，以及是否存在驻扎/替代采集行为。更新说明只提供局部变更，不能据此重建完整数据。
- ECO-U07：采集率科技对正在采集者、之后派出的村民、不同资源类型的作用范围，及科技重复应用/读档行为。
- ECO-U08：船运经验门槛、每次船运的经验曲线、发送冷却/队列、到达时间、接收建筑被毁或出生点受阻时的精确处理、一次性与可重复卡牌规则。
- ECO-U09：建筑、单位和科技的完整成本表、生产时间、批量训练折扣/队列规则与取消退款。

## Frontier Reckoning 初期项目建议（不是原作核实值）

以下数值只用于先做可复现的镜像 1V1 垂直切片，所有值都应放入可配置数据资产，并在游戏内标为“项目平衡参数”。它们刻意不冒充 AoE3:DE 数值。

| 参数 | 初期建议 | 用途与验证要求 |
|---|---:|---|
| 基础资源 | food / wood / coin | 先采用三资源；资源箱为一次性资源事件。验证所有增量都来自采集、卡牌、奖励或交易行为。 |
| 时代数量 | 5（先开放 I—III） | 数据模型不可写死三时代；早期 UI 明确显示“阶段性范围”。 |
| 初始人口上限 | 20 | 仅为镜像对战测试值；住房建成后增加上限，住房被毁后的超人口规则需写入测试。 |
| 村民采集 | 直接入账，固定模拟步长结算 | 模拟时间与渲染帧解耦；后续替换为经过核实的资源点/工具参数。 |
| 建造取消 | 返还 75% 已支付资源 | 项目建议；必须用 EditMode/PlayMode 覆盖取消、重复扣款与摧毁中断。 |
| 批量生产 | 默认队列支持 5 个单位 | 项目建议；批量训练不自动给予原作未核实的折扣。 |
| 船运卡组 | 10 张可选、最多 6 张带入对局 | 项目缩小版，保留卡组合法性、时代限制、发送次数与到达事件。 |
| 船运经验 | 100 XP 获得一次机会 | 项目建议；宝藏、建造、战斗和时代事件分别记录 XP 来源。 |
| 英国差异 | Manor House：住房 +5；完成指定升级后额外生成村民 | 项目机制草案，直到 ECO-U05 核实前不得宣称为原作复刻。 |
| 荷兰差异 | Bank：占建筑上限，按固定模拟步长产生 coin；普通住房不替代 Bank | 项目机制草案，直到 ECO-U06 核实前不得宣称为原作复刻。 |

## 实现清单（仅经济/时代范围）

1. 建立 `ResourceType`、资源点、村民工作状态、直接入账账本与事件日志；每笔增量记录来源实体、时间、资源类型和数量。
2. 建立 `AgeDefinition`、`PoliticianDefinition`、文明配置和可扩展时代列表；先做 I—III，UI 与文档明确未完成 IV—V。
3. 建立统一人口账本：训练、船运、住房、建筑摧毁、超人口、出口堵塞共用同一结算接口。
4. 建立 `ShipmentCardDefinition` 与卡组合法性检查：文明、时代、次数、资源箱/单位/持续效果三类分开结算。
5. 建立英国/荷兰最小差异并做镜像对局：英国住房决策与荷兰 Bank 钱币路线必须改变村民分配和时代计划，而不是只换颜色。
6. 为 ECO-U01—U09 建立待核实清单；在没有证据前，所有精确原作数值只进入实验记录，不进入“已核实参考规则”。

## 参考资料（官方一手）

- [An Inside Overview of Age of Empires III: Definitive Edition](https://www.ageofempires.com/news/aoe3de-inside-overview/)
- [Age of Empires III: Definitive Edition — Release Day Gameplay Changelog](https://www.ageofempires.com/news/aoe3de-baseline-update/)
- [Age of Empires III: Definitive Edition — Update 61213](https://www.ageofempires.com/news/age-of-empires-iii-definitive-edition-update-61213/)
- [Age of Empires III: Definitive Edition — Update 20322](https://www.ageofempires.com/news/aoe3de-update-20322/)
- [The Technical Side of Age of Empires III: Definitive Edition](https://www.ageofempires.com/news/interview-joss-ellis/)
- [Age of Empires III: Definitive Edition — Update 19.18309](https://www.ageofempires.com/news/age-of-empires-iii-definitive-edition-update-19-18309/)

