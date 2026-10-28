# 066 最终测试证据

本目录保存本轮最终 EditMode / PlayMode 结果。XML 根记录与全部单项结果一致：**1,062 / 1,062 通过**，失败、跳过及未定结果均为 0。这里记录的是编辑器测试结果，不代替原生客户端、构建或云端交付验收。

## 输入身份

- 提交基线（主任务给定）：`c9747695d91fa203b05116a1fc0c15a8aac32de2`。
- 实际冻结输入指纹：`66cf7cc57e080eb40cfdf98310ee26affd93d690745dde2b457e8fb3eb0ee1ee`。
- 输入文件数：**573**；具体路径及哈希见未改动的 [source-evidence.json](source-evidence.json)。
- 本轮实际输入以该指纹和文件清单为准，不能仅凭提交基线认定工作区内容。
- 完成后的原始文件位于 `Builds/Tests/run-qBfLgMDn/`；归档前逐字节比较了全部五份文件。

## XML 中的实际结果

时间为 UTC；耗时直接取各 XML 的 `duration` 字段。

| 模式 | 结果 | 通过 / 总数 | 失败 / 跳过 / 未定 | 开始 UTC | 结束 UTC | 耗时（秒） |
| --- | --- | --- | --- | --- | --- | --- |
| EditMode | Passed | 710 / 710 | 0 / 0 / 0 | 2026-10-05 02:29:03Z | 2026-10-05 02:31:56Z | 173.0619012 |
| PlayMode | Passed | 352 / 352 | 0 / 0 / 0 | 2026-10-05 02:32:05Z | 2026-10-05 02:34:32Z | 146.5120681 |

[EditMode.xml](EditMode.xml) 和 [PlayMode.xml](PlayMode.xml) 保留原字节；`source-evidence.json` 中的两份报告 SHA 也逐一匹配这些 XML。

## 日志脱敏与哈希

仅把两份日志中各一个实际许可客户端 `Machine Id` 值替换为 `[REDACTED_MACHINE_ID]`，合计 **2 个设备标识**。未找到实际许可序列号、敏感 entitlement / user ID 或所检查的凭据模式。许可状态、token unavailable、序列化诊断及测试信息保留。回构比较确认除两个字段值外其余字节不变。

下表记录日志原文与归档脱敏版 SHA；原始值不会写入回执。

| 日志 | 原始 bytes | 原始 SHA-256 | 脱敏 bytes | 脱敏 SHA-256 |
| --- | --- | --- | --- | --- |
| [EditMode.log](EditMode.log) | 90825 | `cc3fe6cdb970213034df2c20e6843b49012a86fe86427277183b965185ab610a` | 90818 | `1f0b1d53c9f8fb2c99179bf400ce303c39e31499c7f7f1343d84f1697a9f2d58` |
| [PlayMode.log](PlayMode.log) | 737858 | `8675727f4816a402c57c27f60ac1d752e9b48f501ff4dc4f0e990bc32e62db35` | 737851 | `ca021cc148132bc52d3cd740a96c1b0b9b616dee9183ac6060592165d9ca86b1` |

未修改文件的发布哈希：

| 文件 | bytes | SHA-256（与原始文件一致） |
| --- | --- | --- |
| [EditMode.xml](EditMode.xml) | 574165 | `90344be8f5142e47b8371e51543e4e0ab5ea29d021d2c66c72b1126c6231ebda` |
| [PlayMode.xml](PlayMode.xml) | 334142 | `e0c5e04baa84fb1f7b51f37000d9f72a3b7071bf8eaafe0c27c3008fd92b7e5b` |
| [source-evidence.json](source-evidence.json) | 73542 | `9f8ebf94a9090ce0e108b93c43d2f7b684c8ab81fa068c9614a5731631e5750c` |

完整原始/脱敏文件清单、替换行号、计数与 XML 摘要见 [redaction-receipt.json](redaction-receipt.json)。本目录日志的发布哈希应取脱敏版；原始日志哈希用于与完成后的运行目录对照。
