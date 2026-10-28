# 0.2.31 正常玩家海岸对局 B2：未通过

本目录保留原生客户端 GUID `128a731d440e4b7a94abfcc74b88fa97`、对局 `0d281a3fa942437fabb6798fe2acf664` 的第二次海岸镜像验收。实际规则为 Easy、时代上限 III、seed 314159、正常 1×、autoplay=false。结算在 662.7852 模拟秒（约 11 分 2.8 秒）自然发生：玩家 0 为时代 II，AI 1 为时代 III，winner=1。事件第 20445 行为自然淘汰，重建末局存活单位与建筑数为 `[0, 56]`，没有投降或开局诊断死亡。

[summary.json](summary.json) 的严格 `coast-iii` 审核结果为 **failed**。身份、原始字节、支付与完成链、时代耗时、自然结算检查通过；玩家覆盖仍缺 `ageIII`、`paidWorkers`、`paidCannon`、`cannonEnemyBuildingDamage`、`shipmentArrived`，不能当作阶段 B 通过。

玩家支付完成 5 名长枪兵（第 12753 行支付 300 F / 200 W，第 12754 行排队，第 14008–14017 行出生与训练 XP）、1 座 Manor（第 6067 行支付，第 6752 行完成）、1 座 Barracks（第 9013 行支付，第 10177 行完成）。时代 II 在第 4747 行、279.0078 秒支付 800 F，第 7231 行、369.1133 秒完成。没有玩家付费村民、付费火炮或到达船运；住宅赠送村民不能计作付费训练。

菜单证据复用 B1 的 `Frontier-20261002-110854-267-f3779-84a6e6987e87433489426b6a6c071d6d.json/png`。该图显示 Coast Compact 镜像、ERA LIMIT 3/5、EASY、ECONOMY、25/25。B2 通过 **PLAY AGAIN** 沿用配置，没有独立的新 B2 菜单配置截图。`economy` 是这组 UI 配对声明；报告、事件与 F9 元数据没有 deck 字段，`DeckLibrary.cs` 和 `Rules.json` 只能复现该声明对应的合法预设，不能冒充记录了卡组配置的事件。

`match_finished` 与 `menu_exit` 的两份原始报告及 JSONL 均按原字节保存；它们是同一已结束对局的两次导出。核心对局字段相同，两个事件文件 SHA-256 都是 `d5296eb050e5009fe08aae91b422090d998da74e99ff09347359196439c16799`；报告仅 `reason` 与导出时的 `managedBytes` 不同。[menu-exit-summary.json](menu-exit-summary.json) 是伴随导出的独立审核，也未通过玩家覆盖。

归档时未找到属于这个 match 的独立结算 F9。其他对局的结算图不能补作 B2 证据。事件证明发生的支付与结果，不能代替鼠标手势、实战画面、声音耳听或性能验收。

[archive-receipt.json](archive-receipt.json) 记录 6 个原生来源文件的来源路径、源与归档 SHA、字节相等结果，以及本目录解析器、规则、卡组源、摘要的 SHA。解析器快照只读保留；当前脚本与快照 SHA 相同，可从项目根用正常安装路径重跑以下命令，预期退出码为 1。归档脚本的 DeckLibrary 来源定位依赖该路径，不能直接从深层快照目录执行。临时输出避免覆盖带有配置沿用说明的归档摘要：

```sh
python3 Docs/Evidence/055/verify_player.py \
  Docs/Evidence/055/NormalPlayer/coast-attempt-002/20261002-114322-602-0d281a3fa942437fabb6798fe2acf664-match_finished.json \
  --build-guid 128a731d440e4b7a94abfcc74b88fa97 --app-version 0.2.31 \
  --match-id 0d281a3fa942437fabb6798fe2acf664 --scenario coast-iii \
  --deck-preset economy \
  --rules Docs/Evidence/055/NormalPlayer/coast-attempt-002/Rules.json \
  --menu-capture Docs/Evidence/055/NormalPlayer/coast-attempt-002/Frontier-20261002-110854-267-f3779-84a6e6987e87433489426b6a6c071d6d.json \
  --output /tmp/frontier-b2-recheck.json
```
