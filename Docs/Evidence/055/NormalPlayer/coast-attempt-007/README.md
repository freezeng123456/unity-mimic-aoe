# B7 正常海岸对局：自然败局，仍缺炮击敌建筑

原生 macOS 客户端 0.2.31／GUID `128a731d440e4b7a94abfcc74b88fa97`，对局 `781c4090a2474a91964105c79eab9e27`，Coast Compact 镜像、Easy、时代上限 III、seed 314159、Military 25/25，正常 1×、autoplay=false。**835.643005 模拟秒**自然败局，winner=1；最终按完整 Spawn/Death 重建存活单位和建筑为 `[0, 68]`，无 Resign、无诊断开局死亡。

[summary.json](summary.json) 与 [menu-exit-summary.json](menu-exit-summary.json) 的严格 `coast-iii` 均仅缺 **cannonEnemyBuildingDamage**。版本／GUID／对局身份、合法开局、真实支付、完成链与最低升级／生产时间、采集、船运及自然结束检查全部通过。本局仍不能计作阶段 B 通过。

| 实际完成 | 原始账本证据 |
|---|---|
| 付费单位 | 6 名 Settlers、10 名 Pikemen、5 名 Musketeers、1 门 Field Cannon |
| 付费建筑 | 2 座 Manor、1 座 Barracks、1 座 Foundry |
| 时代 II | 3825 行、197.202835 秒付 800 F；6456 行、287.308319 秒完成 |
| 时代 III | 14589 行、482.220215 秒付 1200 F / 1000 C；19204 行、592.302429 秒完成 |
| 付费炮 223 | 20741/20742 行、628.493591 秒付 100 W / 250 C 并排队；22644/22645 行、673.582581 秒出生／完成；30656 行、830.544250 秒死亡 |
| 舰运炮 217 | 629.793274 秒出生，与同刻 Cannon Support 到达配对；658.386292 秒死亡，不计入付费炮数量 |
| 自然结束 | 30930 行、835.643005 秒 MatchEnded：全部敌对单位和建筑消灭；玩家船运到达共 4 次 |

[player-command-audit.json](player-command-audit.json) 保留两门实际炮的来源、队列、自动集结、Fire、Damage 与死亡。付费炮 223 生存约 156.961670 秒，发生 14 次 Fire、29 次 Damage；舰运炮 217 发生 6 次 Fire、6 次 Damage。所有伤害目标都是敌方**单位**，敌建筑伤害列表为空，两门炮均没有自身作为代表实体的玩家 Command。事件日志确有 4 次其他玩家军队代表实体的显式军令；多选 Command 只记首个实体与选择数，不能证明每一名单位或物理手势。

独立菜单 `Frontier-20261002-152842-784-f3967-3146912b95dd46eb81b2b734990cc6df`，以及五组同局开局／进展 F9 原始 JSON/PNG 均按字节保存。开局世界 0.6 秒，随后进展为 288.508392、482.320221、619.695740、690.078552 秒。[capture-audit.json](capture-audit.json) 的 PNG SHA／尺寸、版本／GUID、请求／实时／HUD 对局和时间配对全部通过。菜单卡组为主代理实际 GUI 操作记录与独立图片配对的 Military 25/25 声明；报告、事件、F9 元数据没有 deck 字段，不能伪造配置事件。

**没有同局原生结算 F9。** 主代理在 Sky 工具中看到了 DEFEAT 结算，随后 Escape 返回菜单；临时 `12.22.08 AM.jpeg` 在归档复制前已经删除，没有保存成功。目录没有捏造 JPEG，也没有用菜单图代替结束图。自然结束以完整原生 report/events 为独立证据，截图缺失保留为实况证据限制。

两个 F8 窗口均已关联同局最终报告与完整 Spawn/Death，身份、原始帧统计及实体／内存计数审核通过，但都未达到 300 秒活动窗口：

| F8 | 合格活动 | 平均／P95／P99 帧 ms | 聚焦记录 |
|---|---:|---|---|
| [第一窗口](../../Performance/normal-b7-window-001/summary.json) | 259.774107 秒 | 26.827854 / 74.995621 / 87.307381 | allFramesFocused=true |
| [第二窗口](../../Performance/normal-b7-window-002/summary.json) | 135.600574 秒 | 34.993698 / 81.002205 / 88.682709 | allFramesFocused=false，失焦与暂停区间按规则排除 |

两窗口 `passed` 仅表示关联与统计审核，无默认 300 秒门槛。不能把两段活动相加冒充一个 300 秒窗口，也不能宣称全程 60 FPS 或所有单位无遮挡可见。第二捕获确在世界 690.078552 秒开始，UTC 15:56:39.580816，并于 16:06:39.596009 自动停止；原始 metadata 与两个独立导出文件均保留。

当前默认 `Player.log` 已与首次 B7 读取时不同：初次工具读取有两次性能 saved 标记及 F9 标记；后续文件只剩新读取的 4,086 字节／52 行且无 B7 标记。最终目录不重构原生日志行、不声称旧日志字节仍可读取。[capture-marker-audit.json](capture-marker-audit.json) 记录这个边界、当前读取 SHA 和第二 F8 原始导出 metadata。这个默认运行也没有启用 `frontierInputTrace`，不能凭日志缺失推断鼠标输入没有传入。

原始事件文件各 19,910,349 字节，小于 100 MB，原样归档而未 gzip。[archive-receipt.json](archive-receipt.json) 保存所有原生复制来源与 SHA、Rules／DeckLibrary／解析器快照。只读归档未操作 GUI、修改源码或六份当前总览，也没有提交或推送。

从项目根复核，预期退出码 1，仅缺 `cannonEnemyBuildingDamage`：

```sh
python3 Docs/Evidence/055/verify_player.py \
  Docs/Evidence/055/NormalPlayer/coast-attempt-007/20261002-162202-453-781c4090a2474a91964105c79eab9e27-match_finished.json \
  --build-guid 128a731d440e4b7a94abfcc74b88fa97 --app-version 0.2.31 \
  --match-id 781c4090a2474a91964105c79eab9e27 --scenario coast-iii \
  --deck-preset military \
  --rules Docs/Evidence/055/NormalPlayer/coast-attempt-007/Rules.json \
  --menu-capture Docs/Evidence/055/NormalPlayer/coast-attempt-007/Frontier-20261002-152842-784-f3967-3146912b95dd46eb81b2b734990cc6df.json \
  --output /tmp/frontier-b7-recheck.json
```
