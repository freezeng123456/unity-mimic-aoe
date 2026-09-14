# Coast 037：经济预设与 Age III 炮兵路线（未执行）

本文件只依据当前 `Assets/Resources/Rules.json` 与 `DeckLibrary.cs` 静态规则编排，未执行，不能作为原生对局验证，也不读取实时敌情。

当前 Coast 普通开局资源为 300 食物、300 木材、100 钱币，6 名村民。项目规则容量为 25；`DeckLibrary.Preset` 先按 economy 的非军事优先级排序，再取 25 张，因此以下是完整 economy 预设的 UI 排序序号，而不是只取前 8 张：

| 页码/序号 | 卡牌 | 时代 | 效果 |
|---:|---|---:|---|
| 1 | `field_tools` | I | Improved Tools，科技 |
| 2 | `food_crates` | I | 300 食物 |
| 3 | `settlers` | I | 2 名村民 |
| 4 | `wood_crates` | I | 300 木材 |
| 5 | `coin_crates` | II | 300 钱币 |
| 6 | `coin_supply_2` | II | **600 钱币**，Coin Convoy 2 |
| 7 | `food_supply_2` | II | 600 食物 |
| 8 | `reserve_villager` | II | 3 名村民 |
| 9 | `wood_supply_2` | II | **600 木材** |
| 20 | `hussars` | II | 2 名 Hussar |
| 21 | `muskets` | II | 3 名 Musketeer |
| 22 | `pikes` | II | 3 名 Pikeman |
| 23 | `cannon_support` | III | 1 门 Field Cannon |

因此可执行的前三卡建议是：

1. **`settlers`**：第一张船运优先补 2 名村民，支撑 Age II 前的食物和木材采集，并尽早达到持续生产和住房扩张的村民规模。
2. **`coin_supply_2`**：进入 Age II 后优先考虑 600 钱币，为 Age III 的 1000 钱币和炮兵生产所需钱币建立储备；该卡不是 300 钱币的 `coin_crates`，两者要区分。
3. **`wood_supply_2`** 或 **`cannon_support`**：Age II 仍缺住房、Barracks、Tower 的木材时选 600 木材；已经有 Age III、Foundry 完成且缺炮兵时选 `cannon_support`。`cannon_support` 需要 Age III，不能提前发送。

## 经济与生产里程碑

- **里程碑 1：Age II 前**。完成至少 1 座 Manor（135 木材、25 秒），保持城镇中心村民训练，准备 800 食物升级费用；木材还要预留 Barracks 200 木材。升级选择 economic 会在完成时给 200 食物，受到压力时可选 military 获 1 名 Pikeman。
- **里程碑 2：Age II 防守**。Age II 完成后建 Barracks（200 木材、45 秒），先生产 5 Pikeman（300 食物 + 200 木材、22 秒、批 5）应对骑兵，或 5 Musketeer（375 食物 + 125 钱币、26 秒、批 5）应对步兵；可用 `muskets`/`pikes` 船运各补 3 名。
- **里程碑 3：Age III 炮兵**。准备 1200 食物 + 1000 钱币，升级耗时 110 秒；Age III 完成后建 Foundry（300 木材、55 秒），再生产 Cannon（100 木材 + 250 钱币、45 秒，Age III，批 5），或发送 `cannon_support` 补 1 门。不要把船运发送当作 Cannon 已经 Spawn。

## 基础操作顺序

开局 5 名村民采食、1 名砍木，探险家探路；木材到 135 建 Manor。达到 800 食物后用 `T` 定位城镇中心并升级，升级期间继续采集并预留 Barracks、住房和后续 Age III 木材。`H` 打开主城/船运面板。首波军队在城镇中心或 Tower 附近防守；Pikeman 保护远程单位和炮兵，Musketeer/Longbow 集火敌军。敌军接近时将村民驻守，释放后恢复采集。

本路线不承诺固定攻击时间或胜负；AI 读取自身经济和可见威胁，静态代码没有固定首次进攻秒数。所有卡牌、生产和炮兵是否实际到达，应由独立事件或画面状态核验。
