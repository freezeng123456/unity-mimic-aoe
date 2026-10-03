# 0.3.0 原生客户端烟测归档

构建 GUID：`e6f13b0328df478cb19cb1bc3989dc5b`。平台 `OSXPlayer`，Unity `6000.6.3f1`，Apple M3 Pro，1600×1000。仅归档匹配该 GUID 和版本 `0.3.0` 的报告、截图；误启动的旧版 `0.2.32` 不在此归档中。

## 实际完成范围

5 份报告对应 **2 局 16× 自动对战和 1 次 1× 普通开局**，不是 5 局或 4 局完整对战。两局自动对战各有 `match_finished` 和 `menu_exit` 报告，重复报告的事件文件分别逐字节相同。地图种子均为 `314159`，Standard AI。

| 模式／Match ID | P0 → P1 | 最终模拟时间 | 胜者 | 终局时代 | 原始事件数 |
|---|---|---:|---|---|---:|
| 自动 `50e3780641784d269f2d913c41f11e35` | highland → wayfarer | 1303.6288 秒 | P1 | III / IV | 52944 |
| 自动 `a2725beba57d4b1982323301fcf3ec5f` | wayfarer → highland | 1382.1096 秒 | P0 | IV / III | 60798 |
| 普通 `9c21044befae495fa1d901ac7cd40fba` | highland → highland | 150.5000 秒退出 | 未结束 | I / I | 2695 |

两局自动对战各有且只有 1 条 `MatchEnded`，原因均为 `All opposing units and buildings eliminated`，没有 `Resign` 事件或投降命令。首局缺少终局 F9 截图，以报告及事件为终局证据；次局有实际 [Victory 截图](Captures/Frontier-20261003-164557-199-f5347-3a7e51e5f0e64d6f99d6bd08728295a8.png)。自动局合计记录 155 次装货与 155 次送达、4 次制造完成、4 次预制件使用、73 条盾阵状态事件；这些记录不等于盾阵实际减伤次数。首局没有实付修炮；次局 P1 在 1058.3887–1059.7883 秒有 15 条实付维修 tick，Field Engineer（实体 283）修复 Field Cannon（实体 288），实际恢复 26 HP（194→220），累计扣除 5.909042 木材和 14.772598 金币。这是一段连续维修，不能计为 15 次独立维修任务。

普通局在 8.60 秒记录 P0 `Gather ×6`，目标实体 35；最终采集统计 827.1230 食物、余额 1127.2167 食物，玩家没有消费、发船或升级。事件还记录 17.40、40.50、135.80 秒三次 `focus_lost → manual_resume`。这是开局操作烟测，不是手动 I–V 通关或升级成功验收。

## 可见界面与操作证据

- [四文明选择窗口](Captures/Frontier-20261003-164623-579-f6898-5da11923c9d6424c9a8322d93eccaa0f.png)：显示四个文明选项。
- [科技预设及牌组第 7/7 页](Captures/Frontier-20261003-164656-329-f8846-ccc110b2634f4e038eae41a468903c51.png)：显示 Deck 25/25；不能推断所有卡牌均已实际发送。
- [普通开局 8.6 秒](Captures/Frontier-20261003-164721-414-f10340-8aeba4c58251411098285c996a0848b7.png)、[25.1 秒城镇中心选中](Captures/Frontier-20261003-164751-188-f12100-736a63c549504009974f810aa3045b92.png)、[退出前 150.4 秒](Captures/Frontier-20261003-165207-813-f25068-0360806ad167446194dad4f61d19d440.png)。

主执行代理现场观察了框选村民、右键食物箱后转采浆果、T 选中城镇中心、暂停恢复和 Cmd+Q 退出；截图及事件只分别支持其中可见或已记录的部分，不是完整输入回放。全部 15 张 F9 元数据的 `paused` 为 false；暂停恢复另由 `PauseChanged` 事件支持。

坐标点击工具阶段性出现 `noWindowsAvailable`，曾恢复，最后又出现；键盘退出正常。该限制尚不能归因为游戏引擎问题。主执行代理已通过 shell 确认相关游戏进程退出；本归档程序没有启动或控制游戏。

## 文件及校验

保留 5 份原始报告、5 份 gzip 事件、15 份截图 JSON 与 15 张 PNG、3 份完整客户端日志，共 43 个原始证据载荷。`summary.json` 按唯一 Match ID 汇总数据。`manifest.json` 为全部证据、说明和验证脚本记录 SHA-256；事件同时保存原始未压缩字节数及 SHA-256。`manifest.sha256` 校验清单本身。

执行独立离线复核：

```sh
python3 Docs/Evidence/057/Native/verify_native.py
```

如原始运行资料仍在本机，可额外逐字节比较原始报告、解压事件、截图和日志：

```sh
python3 Docs/Evidence/057/Native/verify_native.py \
  --source "$HOME/Library/Application Support/Frontier Workshop/Frontier Reckoning" \
  --log-source Builds/Native-0.3.0
```

归档时已实际执行带两个源目录的校验：全部载荷与原始文件逐字节一致、5 份事件解压后与原文相同、15 张 PNG 的哈希与对应截图元数据一致。

3 份日志均保留 Unity 退出时的 6 条 `Thread … may have been prematurely finalized` 和 1 条 `MemoryLeaks` 诊断。未据此判定具体泄漏原因，也不宣称无警告或无泄漏。帧时间字段保留在报告中，但 16× 自动对战不构成正常速度性能验收。本轮没有覆盖手动四文明全流程、多地图、统计平衡或所有卡牌效果。
