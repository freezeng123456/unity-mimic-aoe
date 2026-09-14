# AoE3:DE XP 来源核查与 Frontier Reckoning 0.2.0 边界

访问日期：2026-09-14。只采用 Age of Empires / World's Edge 官方一手页面作为“V 官方明确事实”。官方页面未说明某行为时，记为 U 未知，不将沉默解释为“不会产生 XP”。当前项目数值属于 P 项目规则。

## V：官方明确事实

| ID | 结论 | 官方一手证据 |
|---|---|---|
| XP-V01 | Explorer 收集宝藏、击败守卫后，宝藏奖励可包括经验、资源、单位或其他优势。 | [官方 AoE3:DE 开发者总览](https://www.ageofempires.com/news/aoe3de-inside-overview/) |
| XP-V02 | 随着文明在对局中获得经验，玩家可以从 Home City 请求船运；船运可为资源、单位或其他奖励。 | [官方开发者总览](https://www.ageofempires.com/news/aoe3de-inside-overview/) |
| XP-V03 | 官方更新明确记录建筑的 build XP bounty、单位/建筑的 kill XP bounty，以及不同建筑/单位的 XP bounty 调整，说明建造完成和击杀事件属于真实 XP 结算对象。 | [官方更新 61213](https://www.ageofempires.com/news/age-of-empires-iii-definitive-edition-update-61213/)、[官方更新 13.58326](https://www.ageofempires.com/news/age-of-empires-iii-de-update-13-58326/) |
| XP-V04 | 官方更新记录某些文明建筑、科技或特殊机制会产生 XP trickle；这是文明/建筑例外，不能推广为普通村民采集规则。 | [官方更新 50830](https://www.ageofempires.com/news/aoeiii-de-update-50830/)、[官方更新 19.18309](https://www.ageofempires.com/news/age-of-empires-iii-definitive-edition-update-19-18309/) |
| XP-V05 | 官方 Empire Wars 页面说明该模式船运更频繁、更快，并在进入 Commerce Age 时额外奖励船运；这是模式特例，不能推导标准遭遇战的 XP 曲线。 | [官方 Empire Wars 说明](https://www.ageofempires.com/news/aoeiii-de-empire-wars/) |

## U：公开官方页面未足以冻结的细节

- 普通村民采集 Food/Wood/Coin 是否每单位资源产生 XP、是否仅产生资源而不产生 XP、以及是否存在被动 XP，官方开发者总览没有给出精确说明。没有找到官方页面可以据此排除普通采集 XP。
- 普通单位训练完成是否产生 XP、XP 数量如何按成本计算，公开页面只有 XP bounty/更新片段，不能重建完整公式。
- 建筑完成 XP 的具体公式、建筑类型差异、取消/摧毁/建筑被敌方接管后的结算，公开更新只显示局部 bounty 调整。
- 击杀 XP 是否按被击杀单位成本、单位类型、建筑类型或文明例外计算，不能仅凭更新中的个别 bounty 确定。
- 贸易站、贸易路线、市场交易、资源箱、时代推进和政治家奖励是否以及如何给 XP，公开开发者概览未列完整数值表。
- 英国、荷兰以及其他文明的 XP 例外、Bank/Church/特殊建筑 XP trickle 与标准村民采集的关系，需要锁定具体客户端版本后用官方记录加受许可的游戏内观察补全。
- XP 的初始值、船运门槛、递增曲线、送出时机和卡牌到达时间未由这些官方页面完整冻结；不同模式和更新可能不同。

## P：Frontier Reckoning 0.2.0 当前实现

| 项目 | 当前项目规则 | 证据/边界 |
|---|---:|---|
| 采集 XP | 每次实际采集资源量 × 0.1 XP | `Economy.cs` 的 `AwardXp(worker.owner, amount*.1f, ..., "Gathering")`；这是 P 偏离/简化，不是原作结论 |
| 建造完成 XP | 使用实体定义中的 `xp`，在建筑完工时奖励 | `Economy.cs`；具体配置值来自 `Rules.json` |
| 训练完成 XP | 使用单位定义中的 `xp` | `Economy.cs`；具体配置值来自 `Rules.json` |
| 击杀 XP | 使用被移除实体定义中的 `xp` 作为 Combat bounty | `World.Remove` / `AwardXp`；不宣称原作公式 |
| 船运门槛 | `initialXp + xpIncrease * xpLevels`，当前配置为 150 + 75n | `RuleCatalog` 与 `Rules.json`；项目 P 参数 |
| 资源箱 | 船运先生成 crate，村民采集才产生项目采集 XP | `Economy.cs`；不直接把箱子内容写入库存 |
| 被动 XP | 当前普通经济循环没有通用被动 XP | 仅描述项目实现，不排除原作存在未被官方概览说明的机制 |
| Bank/特殊收入 | Bank 产生 Coin passive income；该收入不计入 `gathered`，当前不自动给 XP | `Progression.cs`、`Economy.cs`；项目 P 参数 |

## 实现与研究结论

当前项目的普通采集 `0.1 XP/资源单位` 没有官方一手资料支持，因此必须标注为 P 项目规则，不能称为 AoE3:DE 复刻。官方资料明确支持宝藏奖励 XP、船运由经验触发、建筑/击杀 bounty 和少数文明/建筑 XP 例外；普通村民采集、训练完成、贸易和全部 XP 公式仍为 U，待以后冻结具体参考客户端并建立逐事件观察账本。

本文件不修改运行时代码和数值表，也不把自动测试、源码存在或旧客户端截图当作 XP 体验验收。若后续要改变当前 `0.1 XP/资源`，应先记录参考版本、观测方法、事件日志与对局影响，再更新 P 参数及测试。
