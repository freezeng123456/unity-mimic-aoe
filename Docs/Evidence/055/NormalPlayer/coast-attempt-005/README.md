# 0.2.31 正常玩家海岸对局 B5：缺炮击敌建筑

本目录保存原生 macOS 客户端 0.2.31、构建 GUID `128a731d440e4b7a94abfcc74b88fa97`、对局 `145add218156473c9a2872545115abe3`。海岸镜像、Easy、时代上限 III、seed 314159，正常 1×，autoplay=false。对局于 **1073.285034 模拟秒**自然败局，winner=1；两侧均实际达到时代 III，事件重建末局存活单位和建筑为 `[0, 79]`，无 Resign 或诊断开局死亡。

[summary.json](summary.json) 和 [menu-exit-summary.json](menu-exit-summary.json) 严格 `coast-iii` 均仅缺 **cannonEnemyBuildingDamage**。身份、正常开局、真实支付、生产与升级完成时间、合法船运、采集、自然结束全部通过。玩家实际训练了 12 名付费村民、15 名长枪兵、15 名火枪兵和 2 门付费炮；建成 4 座 Manor、2 座 Barracks、1 座 Farm、1 座 Foundry、1 座 Tower。本局不能计作阶段 B 通过。

| 实际结果 | 原始事件证据（JSONL 行号） |
|---|---|
| 时代 II | 3190 行、157.2004 秒付 800 F；5442 行、247.3059 秒完成 |
| 时代 III | 17233 行、510.5219 秒付 1200 F / 1000 C；22160 行、620.5955 秒完成 |
| 付费炮 249 | 24671/24672 行、673.3826 秒付 100 W / 250 C 并排队；26729/26730 行、718.4716 秒出生与训练完成；28814 行、760.1614 秒死亡 |
| 付费炮 262 | 24688/24689 行、673.7825 秒付 100 W / 250 C 并排队；28980/28981 行、763.5606 秒出生与训练完成；29846 行、782.4560 秒死亡 |
| 舰运炮 301 | 36349 行、894.9285 秒出生，配对同刻 Cannon Support 到达；36937 行、901.7269 秒 `Move ×1`；37247 行、903.8264 秒死亡 |

两门付费炮在同一 Foundry 单独排队并串行完成，每门训练时间符合 Rules 的 45 秒，不能把第二门当作同时出生。舰运炮 301 单独标为船运来源，不计入付费炮数量。[player-command-audit.json](player-command-audit.json) 保存全部三门炮的出生来源、支付链、自动集结、显式军令、Fire、Damage 和死亡；同时保存 39 次玩家付费军队或舰运军队代表实体的 Move/Hold/Attack 等军令。多选 Command 只记录首个实体及选择数量，不能据此识别全部被选单位。

三门炮均发生了真实射击与伤害，但目标全部是敌方单位：249 伤害敌方炮，262 伤害骑兵、散兵和长弓兵，301 伤害骑兵和火枪兵。Fire 或单位伤害不能代替敌建筑伤害；建筑伤害列表确为空。

独立菜单 `Frontier-20261002-130457-257-f9344-291d656133a24ff4a24cebd3f5b58927` 和结算 `Frontier-20261002-142203-531-f271518-c403db1cea8d47eaabe562464fcab12a` 的已保存 PNG 均从磁盘独立读图。菜单显示 Coast Compact 镜像、III、EASY、seed 314159、**MILITARY 25/25**。结算显示 DEFEAT、17m53s、采集 F5312/W2902/C1791、55 kills/82 losses、双方时代 3、船运 6/9，HUD 人口 0+0/0。报告和 F9 元数据没有 deck 字段，因此卡组仍是与真实菜单图配对的外部声明，而非伪造的 DeckConfigured 事件。

全部 **12 组同局 F9**（开局、进展、结算）及 1 组独立菜单 F9 的原始 JSON/PNG 均已保存。开局世界时间 0.5 秒、正常资源 300/300/100、6 人口；结算世界时间与自然报告完全一致。[capture-audit.json](capture-audit.json) 独立核对 PNG SHA/尺寸、版本/GUID、请求与实时/HUD 对局及世界时间，不声称逐像素审核每张进展图或物理手势。

输入日志仍可能继续追加，故仅冻结本局菜单 F9 到自然结算、结算 F9 及 menu_exit 的完整片段。`native-input-b5-excerpt.txt` 保留原日志第 86–2752 行；`native-input-cannon301-excerpt.txt` 专门保留 900.927 秒选中 301 和随后右键 down/up。最终事件也确有 901.7269 秒的同实体 Move，不能再称“右键没有进入客户端”。[input-trace-receipt.json](input-trace-receipt.json) 保存片段、固定前缀及读取时整日志 SHA；整日志只是当次读取，不是永久不变的完整归档。事件没有渲染帧时间，因此不推断精确逐帧因果关系，也不将一次输入证明当作永久窗口修复。

[B5 F8 完整关联摘要](../../Performance/normal-b5-window-001/summary.json) 在 2560×1440、所有帧聚焦、600.0019 墙钟秒内录得 35,776 帧，11,772 帧符合相邻帧均未暂停且聚焦的条件，**197.825666 秒**合格活动时长；均值 16.8048 ms、P95 17.3505 ms、P99 19.2572 ms。身份、帧统计及 Spawn/Death 与内存计数审核通过，未设置 300 秒门槛；样本不足 300 秒，不能完成长时验收。

原始事件文件各 32,364,271 字节，小于 100 MB，故按原字节归档，没有为 gzip 准备工具重复压缩。规则、卡组和解析器快照及所有原生文件的来源 SHA、字节数、复制一致性见 [archive-receipt.json](archive-receipt.json)。GUI 操作、游戏源码和当前总览文档均未修改。

从项目根复核，预期退出码为 1，仅缺 `cannonEnemyBuildingDamage`：

```sh
python3 Docs/Evidence/055/verify_player.py \
  Docs/Evidence/055/NormalPlayer/coast-attempt-005/20261002-142202-941-145add218156473c9a2872545115abe3-match_finished.json \
  --build-guid 128a731d440e4b7a94abfcc74b88fa97 --app-version 0.2.31 \
  --match-id 145add218156473c9a2872545115abe3 --scenario coast-iii \
  --deck-preset military \
  --rules Docs/Evidence/055/NormalPlayer/coast-attempt-005/Rules.json \
  --menu-capture Docs/Evidence/055/NormalPlayer/coast-attempt-005/Frontier-20261002-130457-257-f9344-291d656133a24ff4a24cebd3f5b58927.json \
  --output /tmp/frontier-b5-recheck.json
```
