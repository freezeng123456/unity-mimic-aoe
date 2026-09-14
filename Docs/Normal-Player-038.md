# 038 对局事后分析

本报告基于导出的 `menu_exit` JSON、对应事件 JSONL 和最终 Player.log 快照。对局 ID 为 `4e8735fbe8764cec984d4c2ca0d9c3e6`，appVersion 0.2.16，seed 314159，`autoplay=false`，simulationSpeed 1.0。导出原因是 `menu_exit`，`winner=-1`，世界时间 599.800598 秒；因此不是自然胜负，也不能计为自然结算。约 9:59 返回主菜单是 agent 的 GUI 误点造成的：暂停界面内 Sky `(640,400)` 映射原生约 `(1280,704)`，命中了 RETURN TO MENU。

## 真实对局事件

玩家 0 初始为 Age I，289.608 秒完成 Age II；未出现 Age III 完成。玩家 1 在 207.703 秒进入 Age II、587.704 秒进入 Age III。玩家 0 船运到达两次：New Settlers 200.403 秒、Coin Convoy 2 349.712 秒；玩家 1 的船运事件独立保留，未合并到玩家统计。

玩家 0 的 TC 是实体 1，90 火力并非空配置。逐行按 `player==0 && entity==1` 统计：AttackPrepared 53、Fire 52、Damage 52、Impact 52。52 条 TC Damage 的目标全部能映射到 Spawn，均为敌方单位：Musketeer 18、Hussar 30、Pikeman 4；未命中敌方建筑；Death事件的entity是死者、target是击杀方玩家，不能按entity==1查TC击杀。先前“没有实体 1 Fire/Damage/Impact”的结论已废止，原因是此前观察的是未按 `entity==1` 分组的早期事件，漏掉了 TC 后续攻击。
根按Death.target==0与World.Remove规则独立复核：14名敌方单位（6火枪兵、5骠骑兵、2长枪兵、1探险家）阵亡，另有2名守卫单独计数。与总kills=14、guardians=2一致。依据同一时刻死亡前的Damage可追溯来源，见kill-attribution-root.json。

玩家 0 在 457.619 秒排入 `pike ×5`，生产建筑实体 167（Barracks）在 353.612 秒完工，随后于 478.120 秒被摧毁；事件未显示该批 5 长枪出生，故该训练在完成前被建筑摧毁而取消/丢失。玩家 0 仍有 10 名村民驻扎相关事件，但没有从该队列推导出免费出生。

玩家 0 的建筑/经济事件包括两座 Manor 完工（113.199、310.910 秒）、Barracks 完工（353.612 秒）、9 笔 Payment、4,048 条 Gather、4 次 ResourceDepleted。玩家 0 事件中有 76 条 Fire、52 条 Impact 和 76 条 Damage；这些是实际开火/命中记录，但主要目标为守卫和敌方单位。没有将守卫 kill 与常规敌军 kill 混为一谈；终局kills=14与敌方单位Death逐项核对一致，守卫另计2名。

## 交付边界

原始报告、事件文件和 Player.log 已复制到本目录，并在 `evidence-sha256.txt` 保存 SHA-256。该局证明了经济采集、Age II、两次玩家船运到达、生产付款/排队、TC/兵营相关生命周期和真实战斗事件均有记录；它也明确显示玩家 0 没有完成 Age III，首批长枪未出生，TC有52次实际开火/命中/伤害。玩家没有造成敌方建筑伤害。由于最终是 agent 误点返回主菜单且 `winner=-1`，这不是自然结算，也不是完整人工对局验收。
