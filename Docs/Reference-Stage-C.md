# Stage C 规则参考：英国、荷兰、时代与主城船运

访问日期：2026-09-14。本文只把官方 Age of Empires / World's Edge 页面直接写出的内容标为“官方支持”。AoE3:DE 的完整平衡数据、内部 proto 定义和当前客户端实测数据未被公开页面完整复述；缺少一手证据的精确数值保持“不确定”。本文不下载游戏素材，也不把项目 P 参数写成原作规则。

## 官方一手证据

| ID | 官方支持内容 | 原始链接 |
|---|---|---|
| STC-R01 | 官方开发者总览明确：村民采集资源即时加入库存，不需要返回城镇中心或投放点。 | [An Inside Overview of Age of Empires III: Definitive Edition](https://www.ageofempires.com/news/aoe3de-inside-overview/) |
| STC-R02 | Explorer 可探索地图、击败宝藏守卫；宝藏可给经验、资源、单位或其他优势。 | [官方开发者总览](https://www.ageofempires.com/news/aoe3de-inside-overview/) |
| STC-R03 | 经验推进 Home City shipments；船运可以是资源、单位或其他奖励，并由玩家预先配置 deck。官方总览记载 deck 最多 25 张、可选卡牌超过 100 张。 | [官方开发者总览：Shipments](https://www.ageofempires.com/news/aoe3de-inside-overview/) |
| STC-R04 | 时代推进需要选择 Politician；不同政治家提供独特奖励，例如资源、军队升级、军事单位或气球。 | [官方开发者总览：Politicians](https://www.ageofempires.com/news/aoe3de-inside-overview/) |
| STC-R05 | DE 发行日变更将 Discovery/Colonial 改称 Exploration/Commerce；同一官方页面的政治家列表展示 Commerce、Fortress、Industrial、Imperial，形成 I–V 时代名称链。 | [官方发布日玩法变更](https://www.ageofempires.com/news/aoe3de-baseline-update/) |
| STC-R06 | 官方技术访谈说明 DE 决定完全解锁船运卡牌和时代政治家；卡组与文明优势组合形成策略。 | [官方技术访谈](https://www.ageofempires.com/news/interview-joss-ellis/) |
| STC-R07 | 英国 Manor House 被官方更新列为 British 建筑，并有独立成本调整；官方卡牌更新也直接引用 Manor Houses。该证据支持英国住房属于文明机制，但公开片段没有完整村民生成公式。 | [官方更新 61213](https://www.ageofempires.com/news/age-of-empires-iii-definitive-edition-update-61213/)、[官方发布日变更](https://www.ageofempires.com/news/aoe3de-baseline-update/) |
| STC-R08 | 荷兰 Bank 被官方更新列为 Dutch 建筑；官方记录 Bank build XP bounty，另一次官方更新写明荷兰 AI 会建造更多 Banks。 | [官方更新 61213](https://www.ageofempires.com/news/age-of-empires-iii-definitive-edition-update-61213/)、[官方更新 19.18309](https://www.ageofempires.com/news/age-of-empires-iii-definitive-edition-update-19.18309/) |
| STC-R09 | 官方更新 20322 记录荷兰 starting-game mining bonus 从 +15% 调整为 +20%，并说明该变化用于提升早期 coin income、允许延迟建 Bank 的策略；同页还记录 Bank of Amsterdam/Rotterdam 卡提高 Bank build limit，并提高 Settler build limit。 | [官方更新 20322](https://www.ageofempires.com/news/aoe3de-update-20322/) |
| STC-R10 | 官方更新记录 Dutch starting resources 曾调整为 50 food、400 coin、200 wood；这是特定更新版本的状态，不能直接当作所有当前版本的冻结起始值。 | [官方更新 61213](https://www.ageofempires.com/news/age-of-empires-iii-definitive-edition-update-61213/) |
| STC-R11 | 官方发行日变更明确多数军事单位可批量训练，并将其描述为快速招募成组部队的机制。 | [官方开发者总览：Batch Training](https://www.ageofempires.com/news/aoe3de-inside-overview/) |
| STC-R12 | 官方 Empire Wars 页面说明该模式会更频繁、更快地产生船运，并在进入 Commerce Age 时额外奖励船运；这是 Empire Wars 特例，不应套到标准遭遇战。 | [官方 Empire Wars 说明](https://www.ageofempires.com/news/aoeiii-de-empire-wars/) |

## 不确定或未冻结的原作细节

- 各版本标准遭遇战的五时代升级费用、升级时间、前置建筑和每个政治家的完整奖励表，官方概览没有提供完整平衡表；不能从 Empire Wars 起始条件反推标准模式。
- 船运经验的精确门槛、递增曲线、发送冷却、运输时间、接收建筑、出生点受阻和人口不足行为，官方页面只确认“经验触发船运”和卡牌内容类别。
- 英国 Manor House 的村民生成速率、触发时点、住房上限关系、卡牌/时代修正未由上述公开官方页面完整说明。
- 荷兰 Bank 的精确钱币被动收入、建造上限、Settler 上限、驻扎行为和卡牌叠加规则未由上述公开页面完整给出；更新中的单个 bounty/limit 改动不能重建完整数据表。
- “银行与钱币村民”作为一句概括会混淆不同版本、文明和更新状态；在没有官方当前数据源时，应记录版本和证据粒度。
- 帝国时代升级后的单位、建筑、科技和经济效果没有在这些概览页完整列出；只能确认 Imperial 是第五时代及政治家奖励框架的一部分。

## Frontier Reckoning 的主动简化（P）

这些是本项目选择，不是原作核实值：

1. 0.1.3 历史基线为 coast 镜像 I–III。0.2.0 当前菜单提供 coast/river 与 I–III 或 I–V；模拟默认 MaxAge=3 保留用于阶段 B 测试，菜单默认上限为5。IV/V 支付、奖励和升级链有自动测试，尚无完整人工五时代对局证据。
2. 0.1.3 历史卡组容量8。0.2.0 当前容量25、原创卡库37张（按文明过滤），有自定义增删和合法性校验；容量与官方概览一致，不代表全部原作卡牌内容或平衡已复现。
3. XP 船运门槛采用 150 + 75n；船运默认串行、travel 35 秒；人口不足或接收建筑缺失时等待，不丢失卡牌。
4. coast 以住房完成触发一次村民赠送；river 以 Bank 的 coin trickle 和 coin-oriented settler route 形成真正的资源分配差异。两者的项目数值见 `Implemented-Numbers-020.md`。
5. 标准陆战不实现实体远洋运输；“shipment”是规则事件和到达实体，后续接收点、箱子采集和人口检查仍需对局验证。

## 第二文明的最小完整实现建议

建议第二文明采用 river 方案，但必须实现可改变决策的闭环，而不是给所有单位统一加百分比：

- **村民生产路线改变**：river 的 villager 由 coin 支付；玩家必须在食物、木材与钱币之间重新分配早期工人。
- **Bank 经济建筑**：Bank 占用建造位置并按模拟时间产生 Coin；设置有限建造上限，迫使玩家在更多住房、军事建筑和 Bank 之间取舍。
- **起始与卡组改变**：river 使用一组专属 coin crate、Bank charter、coin-oriented villager 船运卡；这些卡改变资源时序，而不是只增加攻击力。
- **军事出口保持共同框架**：river 不免费获得额外军队；其优势应体现在 Coin 经济和更早的高 coin 成本单位选择，失败时则因食物/木材不足而受限。
- **时代奖励分叉**：II 时代提供 Bank/coin 经济选择，III 时代提供军事或经济选择；每种选择只能从实际资源与船运账本产生效果。
- **验收条件**：一场 river 镜像对局必须能观察到 Bank 选址、coin villager 费用、住房机会成本、船运时序和军事生产之间的不同决策；若只换颜色或统一百分比，视为未完成文明差异。

## 版本冻结建议

当前能安全冻结的是“官方机制概览层”：即时采集、探索宝藏、XP 船运、卡组、政治家时代奖励、批量训练、五时代命名和英荷特色建筑存在。不能冻结为原作精确数值的是成本、速率、上限、经验曲线和完整升级奖励。若后续需要数值级复现，应选定具体客户端版本，逐项从该版本的官方更新记录与受许可的游戏内观察记录建立独立版本账本。
