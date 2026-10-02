# B9 正常玩家 F8：最终配对与单窗 300 秒要求通过

对局 `7f161abd4f994a1aaf5be762ba4a23f5`、原生 macOS `0.2.31`、GUID `128a731d440e4b7a94abfcc74b88fa97`、Unity `6000.6.3f1`、实际 `2560×1440`。原始捕获 `Frontier-Performance-20261002-164113-027-cc8075cd77464c35a52858642f15c364.json` 按原字节保留，SHA-256 `08e5fc1e79e12dced3ba35f83c467229723a1c3499fc10b46e0b67f76940f8ba`。

[完整审核](f8-audit.json)状态 **passed**，退出码 0。调用明确指定 `--min-unpaused-seconds 300`，没有启用 `--allow-diagnostic`；normalPlayerCapture=true、diagnostic=false。36 项身份、模式、帧、事件与 memory 检查全部成立，额外的单窗时长要求也通过。此处完成了此前缺少的同局最终报告／完整事件配对。

| 指标 | 重算结果 |
|---|---:|
| 原始／有效／排除帧 | 20,003／20,003／0 |
| 单窗口有效未暂停时间 | 335.999332113266 秒 |
| 捕获墙钟 | 336.014378 秒 |
| 世界时间 | 80.099350 → 416.116180 秒 |
| Mean | 16.797447 ms |
| floor P95／P99 | 17.468212／17.587330 ms |
| 所有帧有焦点 | true |
| GC0／GC1／GC2 | 235／235／235 |

335 笔 memory 均与最终事件中的实际 Spawn/Death 前缀一致，每笔七项内部数值、分类、范围与时间检查通过。Managed bytes 范围 8,040,448–13,074,432，Unity allocated bytes 范围 113,111,417–117,343,497。视口数值只表示所记录的可见单位中心指标，不能证明所有单位无遮挡、完整外形或 HUD 外同屏。

配对报告 `20261002-215105-240-7f161abd4f994a1aaf5be762ba4a23f5-match_finished.json` 与完整同 stem events 共 37,880 条事件，最终世界时间 974.509094 秒，覆盖本窗口。原始报告、events、Rules、解析器和 055 来源／构建 receipt 快照保存在本目录；[冻结身份检查](frozen-identity-audit.json)确认 357 个列明源码文件和三项 compiled runtime 与原 055 构建相同。

[原初审目录](../normal-b9-window-001/README.md)的六文件／5,170,061 字节完全保留，原 pending 状态作为当时尚未有最终报告的历史记录；没有回写初审为 Passed。[保全检查](preliminary-preservation-audit.json)记录原目录全部文件物理 SHA。本目录单独保存最终结果、[调用参数](audit-invocation.json)和[完整 SHA 清单](archive-receipt.json)。原生保存标记亦原样保留，其原日志位置已由 [B9 完整日志摘录](../../NormalPlayer/coast-attempt-009/input-trace-audit.json)绑定。

本次 passed 只覆盖该正常 1× F8 窗口、记录的采样开销及最低时长。[B9 严格玩家审核](../../NormalPlayer/coast-attempt-009/README.md)仍因付费炮兵未出生、未炮击敌建筑而 failed；不能据此标完整 B 或 D 通过。50／100／200／400 规模夹具、400 档长期记录、其它硬件与无限长运行稳定性也不由本窗口推导。
