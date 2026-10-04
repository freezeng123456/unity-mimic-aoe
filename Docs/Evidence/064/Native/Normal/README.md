# 0.7.0 原生普通局存档冷恢复

此目录确认一次普通开局的**保存 → 同槽覆盖 → 退出进程 → 新进程菜单载入 → 重存一致 → 继续操作**。所有证据属于 0.7.0 macOS arm64 Development / Mono 客户端，GUID `48cbcf92c92f409c945f83eeb9b8d500`，match `1e167242b3c54d649f3e7b10fdd0b294`；构建与完整测试绑定见 [客户端回执](../client-build.json) 和 [Native 总览](../README.md)。

地图 `winding`，玩家 `coast`，Standard AI，25 张正常卡组，I 时代，1× 模拟，autoplay 关闭。原始材料来自本地 `outputs/frontier-reckoning-0.7.0/Native/Normal`；本次只读归档没有启动客户端或修改存档。本场景未完成自然终局，不能作为完整工业对局、压力测试或长期性能验收。

## 冷恢复与继续操作

- [Player-before-quit.sanitized.log](Player-before-quit.sanitized.log) 原始第 64、100 行分别记录 `FRONTIER_SAVE slot=1`，世界时间 135.1991 和 184.002。A 与 B 保留了同槽覆盖前后的实际文件。
- [process-exit.json](process-exit.json) 记录旧 PID 50307 退出后原生实例数为 0，slot 1 文件哈希未变，新启动 PID 为 50933。该回执没有记录进程 exit code，不据此推断退出码。
- [Player-after-cold-restart.sanitized.log](Player-after-cold-restart.sanitized.log) 原始第 39 行记录新进程 `FRONTIER_LOAD slot=1`，时间 184.002；第 73 行将刚载入状态重存到 slot 2，第 121 行记录续局到 312.4099 后再次保存 slot 2。
- `checkpoint-B.json` 与 `checkpoint-cold-roundtrip.json` **逐字节一致**，SHA `b54bd30d20e25a0b40274d9ed3a50899fd05386fa7833420651b7f51edb8c3eb`。全部 payload 字段、世界/AI 控制器、固定步余量、队列、编队、选择、镜头和音频设置均一致；四份存档 envelope 的 payload 校验和也均有效。
- 退出前和冷载入后 F9 的 match、world time、资源、人口、原训练队列及 elapsed、镜头 focus/zoom 一致，冷载入保持暂停。详见 [原始冷载入回执](cold-load-verification.json) 与 [独立复核](hash-verification.json)。
- 继续后保留全部旧事件前缀，simulation step 从 1840 增至 3124；旧城镇队列在世界时间 204.003250122 产生 Settler 144，人口从 7+1/20 到 8+0/20。世界时间 254.906356812 有玩家公开 `Gather ×1` 命令（实体 143 → 资源 9），随后累计真实食物采集增加；最终检查点时间为 312.409851074。

编队操作只确认 **HUD 的 SAVE 2 按钮创建编队、实际数字键 2 召回**。B 和冷重存都保留 `group 2 = [1]`，召回 F9 的镜头定位城镇 `(24, 28)`。此前自动化 Meta+数字组合没有在 A 中形成编队；不宣称 Ctrl/Command+数字的原生创建快捷键已通过。

| 检查点 | 世界时间（秒） | 人口 | 字节 | 文件 SHA-256 |
| --- | ---: | --- | ---: | --- |
| [checkpoint-A.json](checkpoint-A.json) | 135.199050903 | 6+1/20 | 2,350,988 | `833107a69cb28674f098ed4d3fda541bfc9628de4b06c38c9365606c47c73309` |
| [checkpoint-B.json](checkpoint-B.json) | 184.002029419 | 7+1/20 | 3,017,924 | `b54bd30d20e25a0b40274d9ed3a50899fd05386fa7833420651b7f51edb8c3eb` |
| [checkpoint-cold-roundtrip.json](checkpoint-cold-roundtrip.json) | 184.002029419 | 7+1/20 | 3,017,924 | `b54bd30d20e25a0b40274d9ed3a50899fd05386fa7833420651b7f51edb8c3eb` |
| [checkpoint-continued.json](checkpoint-continued.json) | 312.409851074 | 8+0/20 | 4,782,539 | `24e670cc987221d62a79609741687b16eb28dbf875faa8892f6922563e868ecf` |

## F9 实际配对

7 对 JSON/PNG 均逐字节复制，独立核对 PNG SHA、1600×1000 尺寸、版本/GUID/match，以及 request/capture/HUD frame 与 world time 的对应关系。下表依据归档数据标记阶段。

| 阶段 | 世界时间（秒） | JSON / PNG |
| --- | ---: | --- |
| 检查点 A 的暂停状态 | 135.199050903 | [Frontier-20261004-200216-465-f10567-e118af8bac2545a596dd7803a63aa119.json](Frontier-20261004-200216-465-f10567-e118af8bac2545a596dd7803a63aa119.json) / [PNG](Frontier-20261004-200216-465-f10567-e118af8bac2545a596dd7803a63aa119.png) |
| 同一 A 状态的再次 F9 | 135.199050903 | [Frontier-20261004-200257-266-f12989-cbf4a6ac05e041fab6a501d892ab2024.json](Frontier-20261004-200257-266-f12989-cbf4a6ac05e041fab6a501d892ab2024.json) / [PNG](Frontier-20261004-200257-266-f12989-cbf4a6ac05e041fab6a501d892ab2024.png) |
| 覆盖后的检查点 B，退出前 | 184.002029419 | [Frontier-20261004-200536-879-f22450-fb24812a82e4446dae25234a8ad7e577.json](Frontier-20261004-200536-879-f22450-fb24812a82e4446dae25234a8ad7e577.json) / [PNG](Frontier-20261004-200536-879-f22450-fb24812a82e4446dae25234a8ad7e577.png) |
| 新进程菜单载入 B，仍暂停 | 184.002029419 | [Frontier-20261004-200749-234-f3614-ce7ac0c82430412cb57c184797134fba.json](Frontier-20261004-200749-234-f3614-ce7ac0c82430412cb57c184797134fba.json) / [PNG](Frontier-20261004-200749-234-f3614-ce7ac0c82430412cb57c184797134fba.png) |
| 恢复编队 2 后镜头定位城镇，已继续 0.1 秒 | 184.102035522 | [Frontier-20261004-200915-954-f8740-30b271d422f54b489e7e28d1c13fc381.json](Frontier-20261004-200915-954-f8740-30b271d422f54b489e7e28d1c13fc381.json) / [PNG](Frontier-20261004-200915-954-f8740-30b271d422f54b489e7e28d1c13fc381.png) |
| 原有训练完成，人口 8+0/20 | 254.906356812 | [Frontier-20261004-201026-816-f12942-e4c03ba657814b4dadc128010d241773.json](Frontier-20261004-201026-816-f12942-e4c03ba657814b4dadc128010d241773.json) / [PNG](Frontier-20261004-201026-816-f12942-e4c03ba657814b4dadc128010d241773.png) |
| 继续采集后暂停并保存 slot 2 | 312.409851074 | [Frontier-20261004-201125-083-f16380-adba47f2bb094961850604ee371f4302.json](Frontier-20261004-201125-083-f16380-adba47f2bb094961850604ee371f4302.json) / [PNG](Frontier-20261004-201125-083-f16380-adba47f2bb094961850604ee371f4302.png) |

## 普通经济短窗

[normal-economy-performance.json](normal-economy-performance.json) 为存档操作前的 F8 原始样本：UTC `2026-10-04T20:00:23.0018440Z` 至 `2026-10-04T20:01:51.4352780Z`，Apple M3 Pro，OSXPlayer，1600×1000，1×。世界时间 21.900047302 → 110.298889160，属于 I 时代小规模经济画面。没有使用 autoplay 或 benchmark。

| 指标 | 实测 |
| --- | ---: |
| 墙钟窗口 / 纳入帧间隔合计 | 88.431516459 / 88.415243320 秒 |
| 总帧 / eligible / 排除 | 5264 / 5264 / 0 |
| 平均 / P95 / P99 | 16.796209335 / 17.378463745 / 17.605960846 ms |
| 最大帧 | 33.309082031 ms |
| 超过 33⅓ / 50 / 100 ms 的帧 | 0 / 0 / 0 |
| 实际 simulation steps | 884 |
| 平均每帧分配 | 9583.374 bytes |
| 内存采样数 | 89 |

全部 5264 帧 focused、unpaused、eligible，没有区间内失焦或暂停变化。P95/P99 按原生实现 `sorted[floor((n - 1) * p)]` 独立重算一致。Managed memory 首尾为 13,127,680 → 15,044,608 bytes，Unity allocated 为 126,834,761 → 130,888,607 bytes；没有 render-thread counter 样本。此约 88 秒样本少于 300 秒，不证明 200/400 陆军加 12 船压力表现、整局战斗、长期无泄漏、Release 或跨平台结果。

## 归档完整性与日志

[hash-verification.json](hash-verification.json) 保存所有原始 JSON/PNG、检查点、脱敏日志的字节/哈希与独立复核结果，不包含自身哈希。原始日志只留本地，发布副本名为 `.sanitized.log`：按 Licensing、凭据和 PEM 私钥模式扫描，两份日志均未命中，因此清洗后字节与输入相同。回执记录各自原始 SHA、规则、0 条删除及所引用的保存/载入行号。

本证据不扩大为损坏/版本拒绝、写入中强制终止、工业生命周期或所有快捷键的原生验证；相应自动化测试与其他原生验收须分别引用。slot 1 的 B 检查点与 slot 2 的继续状态已分别留档。
