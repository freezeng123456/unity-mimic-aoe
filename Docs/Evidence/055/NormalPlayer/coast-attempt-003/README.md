# 0.2.31 正常玩家海岸对局 B3：未通过

本目录保留原生客户端 GUID `128a731d440e4b7a94abfcc74b88fa97`、对局 `62dbc87afc3d4296a932fd41997856aa`。海岸镜像、Easy、时代上限 III、seed 314159、正常 1×、autoplay=false；810.9490 模拟秒（约 13 分 30.9 秒）自然败局，玩家 0 为时代 II，AI 1 为时代 III，winner=1。事件第 27328 行记录全部敌对单位和建筑消灭；按实体事件重建的末局存活单位与建筑为 `[0, 64]`，没有投降或开局诊断死亡。

[summary.json](summary.json) 的严格 `coast-iii` 结果为 **failed**，仅玩家覆盖项未通过：缺 `ageIII`、`paidCannon`、`cannonEnemyBuildingDamage`。身份、事件计数、正常开局、支付与完成链、时代耗时、自然结算和 loser=0 检查通过；本局不能算阶段 B 通过。

| 玩家实际完成 | 原始事件证据（JSONL 行号） |
|---|---|
| 2 名付费村民 | 支付/排队 3371/3372，出生/训练 XP 4159/4160；第二名 10845/10846、11786/11787，各支付 100 F |
| 5 名付费长枪兵 | 11953 支付 300 F / 200 W，11954 排队，13011–13020 出生与训练 XP，480.6201 秒完成 |
| 5 名付费火枪兵 | 13197 支付 375 F / 125 C，13198 排队，14358–14368 出生与训练 XP，510.1219 秒完成 |
| 2 座 Manor、1 座 Barracks | Manor 支付/完成 714/1105、1151/1585，各 135 W；Barracks 8572/10530，200 W |
| 时代 II | 2089 行、120.9987 秒支付 800 F；3802 行、211.1037 秒完成 |
| 3 次玩家船运到达 | 3820 行 New Settlers；10555 行 Coin Convoy 2；16905 行 Food Convoy 2；合法预设、发送及到达审核见摘要 |

时代 III **没有完成**：第 25924 行、780.0566 秒支付 1200 F / 1000 C；第 26502 行、791.6537 秒玩家主城死亡，两者只相隔 11.5972 秒，Rules 的该升级时间为 110 秒，且没有玩家 `AgeAdvanced` III 事件。付款不能算时代完成。摘要保留这条未完成升级及主城死亡证据，不把 UI 点击意图当作结果。没有付费火炮，也没有玩家火炮对敌方建筑造成伤害的证据。

独立菜单 `Frontier-20261002-114351-906-f93075-3f34c7fa33034e31be3ac463c19951fb.json/png` 已保存并读图，显示 Coast Compact 镜像、ERA LIMIT 3/5、EASY、seed 314159、ECONOMY、25/25。开局 F9 为 `Frontier-20261002-114409-114-f94080-6c0dc261fde34f8ba6b897f1cdeb1108`，正常初始资源 300/300/100、人口 6+0/20、世界时间 0。最新同局结算 F9 为 `Frontier-20261002-120126-267-f146198-2ddc5fac42f3417c9aa6441a15de233d`，世界时间等于自然结算，画面显示 DEFEAT、13m30s、玩家时代 2／对手时代 3。[capture-audit.json](capture-audit.json) 保存 SHA、尺寸、版本/GUID、请求/实时/HUD 对局配对检查；这次仅读取已落盘 PNG，没有操作 GUI。

`economy` 仍是配对菜单图的外部配置声明。报告、事件和 F9 元数据没有 deck 字段；`DeckLibrary.cs` 与 `Rules.json` 快照可复现该声明对应的合法卡组，不能冒充事件记录了配置。事件和结算图不能代替手势、实战识别、声音耳听或性能验收。归档时没有同局 `menu_exit` 导出，未拿其他对局补齐。

[archive-receipt.json](archive-receipt.json) 记录原生来源与归档字节相等、文件 SHA，以及解析器/Rules/DeckLibrary 快照。当前审计脚本与快照 SHA 相同；从项目根重跑以下命令，预期退出码为 1。解析器的 DeckLibrary 定位依赖正常安装路径，勿直接从深层快照目录执行；临时输出避免覆盖附加说明后的摘要。

```sh
python3 Docs/Evidence/055/verify_player.py \
  Docs/Evidence/055/NormalPlayer/coast-attempt-003/20261002-120047-817-62dbc87afc3d4296a932fd41997856aa-match_finished.json \
  --build-guid 128a731d440e4b7a94abfcc74b88fa97 --app-version 0.2.31 \
  --match-id 62dbc87afc3d4296a932fd41997856aa --scenario coast-iii \
  --deck-preset economy \
  --rules Docs/Evidence/055/NormalPlayer/coast-attempt-003/Rules.json \
  --menu-capture Docs/Evidence/055/NormalPlayer/coast-attempt-003/Frontier-20261002-114351-906-f93075-3f34c7fa33034e31be3ac463c19951fb.json \
  --output /tmp/frontier-b3-recheck.json
```
