# AoE3:DE XP 参考机制核查

访问日期：2026-09-14。仅查阅 World's Edge / Age of Empires 官方一手页面。未启动游戏，未修改工程。官方页面没有逐单位完整数据表时，记为未核实，不将沉默解释为 0。

## 直接支持的机制

官方更新明确使用并调整 build XP bounty 与 kill XP bounty。61213 更新写明 Bank 的 build XP bounty 从 140 调为 105，并相应调整 kill XP bounty；这证明建筑建造与击杀是分别存在的 XP bounty 概念，但不提供 British 普通单位的完整表。[Update 61213](https://www.ageofempires.com/news/age-of-empires-iii-definitive-edition-update-61213/)。

13.58326 更新在法国 Napoleon 战争条目中说明 Sansculottes 的 build XP 下降、combat XP generation 提高。这是法国特殊单位的机制变化，证明生产相关 XP 与战斗 XP 可以分别调整，但没有提供 British 单位数值，不能外推到 Settler/Pikeman/Musketeer/Hussar/Longbow/Cannon。[Update 13.58326](https://www.ageofempires.com/news/age-of-empires-iii-de-update-13-58326/)。38254 更新给出 Town Center build 100→50、kill 100（Lakota 40/80），进一步证明 kill 值不必等于 build 值。[Update 38254](https://www.ageofempires.com/news/aoe3de-update-38254/)。

官方开发者总览说明经验用于请求 Home City shipment，宝藏奖励可能包含经验，但没有列出普通单位训练 XP、普通单位击杀 XP 或逐单位 bounty 数值。[An Inside Overview](https://www.ageofempires.com/news/aoe3de-inside-overview/)。

## 目标单位与 Frontier 边界

截至本次检索，没有在官方公开一手页面中找到 British 的 Settler、Pikeman、Musketeer、Hussar、Longbow 或 Cannon 的精确 train XP、kill XP bounty 数值，也没有找到官方页面说明训练完成 XP 是否是独立于 build/kill bounty 的通用奖励。上述精确值必须标为未核实，不能写成 0；Frontier `Rules.json` 的 `xp: 0` 只能说明当前 P 参数。

官方材料支持的最小模型是：建筑 build bounty、对象 kill bounty，以及特殊单位的生产/战斗 XP 机制可以分别存在；普通单位训练完成是否给独立 XP、公式和逐单位 kill bounty 尚未冻结。

0.2.17及更早版本的 Frontier 使用单一实体 `xp` 字段：建筑完成使用 `b.xp`，单位训练使用 `d.xp`，被击杀实体使用 `e.xp` 作为 Combat bounty。该设计是项目简化，不是已核实的 AoE3:DE 数据模型。若要进一步复现精确数值，仍需锁定具体参考客户端版本并记录逐单位事件账本；041已根据此机制拆分参数，具体实施边界见下方合同。

## 未核实范围

官方页面未提供普通单位训练 XP 统一公式，未提供六类 British 目标单位逐单位 kill bounty 表，也未证明普通单位训练必然产生正 XP。不能依据官方沉默推断为 0，也不能把项目 0 值升级成原作事实。

## 041 实施合同与过渡参数

# 041 经验来源补齐：实施前合同

040 原生正常对局自然战败后，逐对象规则检查确认普通单位 xp 均为 0；训练和击杀虽有调用路径，实际不产生正经验。该数据缺口削弱了军队生产、交战与主城船运的联系。不能把本轮修复称为完整原作经验系统，也不把它用来替代尚未完成的主动攻城验收。

参考机制及未核实范围见 reference-xp.md。根已重新打开官方 61213、38254、13.58326 页面，核实建筑完成与击杀 bounty 可独立调整、法国特殊单位存在生产相关与战斗相关 XP；尚未核实英式普通兵种完整数值。以下是明确的 P 项目过渡数值。

规则数据将把 EntityDef.xp 拆成 buildXp、trainXp、killXp。建筑 buildXp 与 killXp 暂保留各自原来的非零 xp；普通可训练单位 trainXp 取当前基础资源总费用的 5% 向上取整，killXp 取 10% 向上取整，并逐项写入配置，不在运行时套公式。探险家不能训练，trainXp=0、killXp=20；守卫保持 killXp=8。自然资源三项均为 0。文明费用覆盖、科技降价与战场免费增援不改变固定击杀赏金。这些比例并非原作公式。

训练经验只在付费 Train 订单成功出生后按实际人数发放；排队、退款、取消、出口阻塞、生产建筑被毁不提前发。住房赠村民、开局实体、船运与时代增援不作为付费训练发经验。建筑只在施工真正完成时给 buildXp；击杀只在有效敌方对象第一次死亡时给 killXp，沿用现有归属机制。未完工敌方建筑的击杀仍沿用该建筑固定 bounty，是保留的项目简化，后续须核对参考规则。

玩家与 AI 共用数据和入口。生产按钮说明、对象详情、导出数值表显示对应经验，持续采集 0.1 XP/资源、时代 100 XP、机会门槛 150+75n 与船运 35 秒本轮保持当前 P 规则；不得把这些未核实数值改称参考事实。

实现清单：先运行真实生产/伤害路径的两个正经验回归，确认现版失败；拆字段与填入逐项参数；覆盖训练取消/受阻/死亡重复调用/赠送单位、建筑完成与击杀独立值；全量规则与场景回归；构建新版本原生客户端，检查详情数值和一次付费村民训练的经验变化。整局体验需继续独立验证。

# 041 显式经验配置 P

这些是项目过渡参数，不是已核实原作数值。

|ID|完成建造|付费训练|敌方击杀|
|---|---:|---:|---:|
|villager|0|5|10|
|explorer|0|0|20|
|pike|0|5|10|
|musketeer|0|5|10|
|skirmisher|0|6|12|
|hussar|0|10|20|
|dragoon|0|9|18|
|cannon|0|18|35|
|mortar|0|20|40|
|town|75|0|75|
|house|20|0|20|
|barracks|30|0|30|
|stable|30|0|30|
|foundry|45|0|45|
|market|15|0|15|
|tower|30|0|30|
|farm|45|0|45|
|bank|30|0|30|
|berry|0|0|0|
|deer|0|0|0|
|tree|0|0|0|
|mine|0|0|0|
|crate_food|0|0|0|
|crate_wood|0|0|0|
|crate_coin|0|0|0|
|longbow|0|6|11|
|carabineer|0|6|11|
|estate|45|0|45|
|trade_socket|0|0|0|
|trade_post|40|0|40|
|guardian|0|0|8|
|treasure_wood|0|0|0|
|treasure_xp|0|0|0|
|treasure_coin|0|0|0|

旧版单字段说明仅描述0.2.17及更早版本；0.2.18改用独立buildXp/trainXp/killXp配置。宝藏奖励的xp字段与玩家当前xp余额不在此迁移范围。
