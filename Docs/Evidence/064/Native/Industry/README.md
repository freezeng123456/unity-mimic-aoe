# 0.7.0 四文明工业原生展示

四文明共 **10 对实际 F9 JSON/PNG** 已归档并独立核验，全部来自 0.7.0 最终 macOS arm64 Development / Mono 客户端，GUID `48cbcf92c92f409c945f83eeb9b8d500`。本目录确认受控工业存档在该客户端中的实际渲染、工厂模式、炮兵选中面板，以及英国样本的公开生产模式切换；不作为正常开局达到工业时代、自然终局、竞争强度或压力性能证明。

## 身份与范围

- Runtime commit：`5cce74ccf7222133af9bad633923f452b100bfb2`。
- 源码指纹：`8342b85bf835fd48d5c4dfc8f612ed64770fc5b7d026d96c4f539caaa96d79b0`，与 [构建回执](../client-build.json) 和 [796 项完整测试](../../Tests/README.md) 一致。
- 最终导出 [manifest](fixture-source/manifest.json) 的导出前/后指纹相同；其 `testReceiptSha256` 为 `d21c660852280aa7262b33f668f8f6b4d8343dcf9144cd0143a0308c8bb23a37`，对应最终 `run-LOUCjHNW` 回执。
- `fixtureScope`：Winding 原始开局实体与资源，时代上限 V；玩家起始 IV，显式输入 265 XP，卡组替换为 `factory_wagon` 与 `robber_barons` 两张工业卡。AI controllers 为空，autoplay 关闭，模拟速度 1×。这一步是受控存档生成，不含原生截图。
- `nativeScope`：最终原生客户端实际载入上述存档、恢复运行并用公开 UI 操作，随后产生实际 F9。四份炮兵选中 PNG 已独立看图，分别显示 ROCKET、HEAVY CANNON、HEAVY CANNON、GREAT BOMBARD；原生操作者记录使用真实军事循环键 M 选中。此步骤提供原生渲染证据，继承前述受控条件。

原始四份 `capture-index.json` 与导出报告保持字节不变，其中 `fixtureScope` 的“not native screenshot evidence”限定的是生成夹具这一步；`nativeScope` 明确指随后实际客户端 F9。统一口径与逐项校验保存在 [hash-verification.json](hash-verification.json)，不把两类证据混为一项正常开局验收。

## 四文明实际画面

| 文明 | 已看图的炮兵标签 | 原生 PNG | 原始索引 |
| --- | --- | --- | --- |
| 英国 `coast` | `ROCKET` | [coast 工厂](coast/Frontier-20261004-201412-506-f26336-ea3bc6d470dc451bb4b95aabfad0ed29.png) / [炮兵选中](coast/Frontier-20261004-201756-591-f39634-682c9935c34243daac5352fb8fc43e8e.png) | [coast index](coast/capture-index.json) |
| 荷兰 `river` | `HEAVY CANNON` | [river 工厂](river/Frontier-20261004-202255-490-f57334-4e4eabd072444940a2a73c7c210cf4a0.png) / [炮兵选中](river/Frontier-20261004-202440-226-f63538-ec6450a98c38411787a3adf3370467b0.png) | [river index](river/capture-index.json) |
| 法国 `france` | `HEAVY CANNON` | [france 工厂](france/Frontier-20261004-202503-060-f64867-067b4bc6885d481fa21de62ec98d6949.png) / [炮兵选中](france/Frontier-20261004-202527-021-f66285-2c31df51374945788987df38d8f94021.png) | [france index](france/capture-index.json) |
| 奥斯曼 `ottoman` | `GREAT BOMBARD` | [ottoman 工厂](ottoman/Frontier-20261004-202553-146-f67815-6f185dcfd9aa4911b52cba67ecb5eeb8.png) / [炮兵选中](ottoman/Frontier-20261004-202615-744-f69149-c122a5f8abfc447b8e2f27122f3f21ae.png) | [ottoman index](ottoman/capture-index.json) |

每文明都有完工工厂 136 的 `coin` 模式与工厂 138 的 `artillery` 模式；英国的中间切换图明确另记为 `wood`。每对 F9 核对了版本/GUID/match、request/capture/HUD 帧号、世界时间、HUD 四时代及资源/人口舍入、PNG SHA 与 1600×1000 尺寸。全部为游戏内有玩家且窗口获得焦点的图像。原始 F9 中的 `aiDifficulty: standard` 是难度配置名，不代表 AI 正在运行；无 AI 的依据是绑定存档的空 controllers。

所有初始工厂 F9 的世界时间、资源和实体/模式与对应导出存档吻合。原始存档内部 payload 校验和、文件字节/哈希、四文明 match ID、炮兵实体 139 及其定义均核对通过。每案事件账本包含两次真实 `ShipmentArrived`、两次 `FactoryFoundationStarted`、一次 `FactoryArtilleryProduced`；港口实付 200 木。没有为展示直接授予资源、血量或成品实体。

## 英国原生模式切换

英国多保留两对操作图：工厂 138 从 ARTILLERY 切为 [WOOD](coast/Frontier-20261004-201604-934-f33021-b871703bf0724dcaaa0ffc01bdc1d674.png)，随后切回 [ARTILLERY](coast/Frontier-20261004-201709-300-f36842-bc46aec5fff74dc8b3c76cd53bc78400.png)。对应 F9 工厂模式逐项一致，木材由 100 增至 453.646484375（增加 353.646484375），人口由初始 12 到继续生产后的 18。该原生样本确认这两次模式切换及其可见经济/人口结果，不扩展为所有研究、修理、出口受阻或对敌作战均已原生重测。

## 最终夹具来源

以下四份存档、四份报告与 manifest 来自最终 `PreviewSaves`，均逐字节保留；早期 `PreviewSaves-first` 未用于本归档。

| 文明 | 原始存档 | 报告 | 存档 SHA-256 |
| --- | --- | --- | --- |
| `coast` | [industrial-coast.json](fixture-source/industrial-coast.json) | [case report](fixture-source/industrial-coast-report.json) | `03e1cd30741d029971aceffd42c8724f5e0e0d186ec43b1a0aef0f17561aeec8` |
| `river` | [industrial-river.json](fixture-source/industrial-river.json) | [case report](fixture-source/industrial-river-report.json) | `bc7bfabb6f8a8b00f615f9ea1eb3cd76736d2fb6e6cdb7e5866e811b5838a5c8` |
| `france` | [industrial-france.json](fixture-source/industrial-france.json) | [case report](fixture-source/industrial-france-report.json) | `fa88d9352dc816bb4ee11b5ec7711cc34c89ec2030eaa8f0497c783e422adadc` |
| `ottoman` | [industrial-ottoman.json](fixture-source/industrial-ottoman.json) | [case report](fixture-source/industrial-ottoman-report.json) | `73693c097832a18ee7b6c52ec75713887a0531824b00b2c1718062036f8fba22` |

存档生成时暂停、相机 orthographic size 为 15；原生 F9 在恢复运行后取得，因此其 `paused` 状态可与生成存档不同。四份工业卡组均只有两张卡，不能称为正常 25 卡开局；正常 I 时代 Standard AI 的退出/重启/续局另见 [Normal](../Normal/README.md)。

## 临时槽位管理与完整性

[slot3-management.json](slot3-management.json) 记录 slot 3 原本不存在，仅用作此次管理的展示临时槽，最后移除了该展示槽。其最后 Ottoman match 与 SHA 和本目录的最终 Ottoman 存档一致。归档时另行只读核对：slot 1 与 Normal 的 `checkpoint-B.json` 逐字节一致，slot 2 与 `checkpoint-continued.json` 逐字节一致，slot 3 不存在。本次审核没有写入或删除任何玩家槽位。

[hash-verification.json](hash-verification.json) 记录所有原始材料的字节/哈希、10 对 F9 校验、四张选中标签的看图记录、来源与上述范围。它不包含自身或 README 的哈希，不代替最终分包或云端交付回执。本目录没有采集新的性能窗口，也没有进行压缩、Unity 启动或实时 GUI 操作。
