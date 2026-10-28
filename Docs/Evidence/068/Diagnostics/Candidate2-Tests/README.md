# 068 完整回归测试

`run-cJvXP8if` 实际通过 EditMode 817/817、PlayMode 368/368，共 1185 项；失败、跳过和不确定结果均为零。源码提交为 `be6665d53e9c0c7fc08c1e9f805dfad2c6906de4`，630 输入指纹为 `38d592d516a6ad62d68bd42a5940f996851a7e469bfe20bb35bcc67b92428ad7`。独立审查将各输入与该提交逐字节比对，并在审查结束重读确认没有漂移，当前源码验证器退出码为 0。

原 067 的全部 1097 个用例按完整名称逐项找到并通过，其中非回放用例 1062、回放用例 35（10 核心、20 存储、5 控制器/UI）。新增 8 组 LAN 用例 88 项，包含 77 EditMode 和 11 PlayMode。新加入的 `DisconnectedHostCanResumeTheSharedPauseThroughItsActualMenu` 实际通过，耗时 1.29913 秒。

[EditMode.xml](EditMode.xml)、[PlayMode.xml](PlayMode.xml) 与 [source-evidence.json](source-evidence.json) 均原字节保留。[EditMode.log](EditMode.log)、[PlayMode.log](PlayMode.log) 仅替换实际许可设备 ID：EditMode.log 第 87 行；PlayMode.log 第 94 行，共 2 处；Serial number 字段实际替换 0 处。统一替换为 `[REDACTED_DEVICE_ID]`，其他日志内容、行数及警告保留，原始日志仍只读留在本地运行目录。

[archive-receipt.json](archive-receipt.json) 记录原始与脱敏 SHA-256、字节数、行号和沿用 067 的规则；[independent-review.json](independent-review.json) 给出 15 项独立检查、各组实际发现数量和旧用例完整发现结果。此前 1184 项 Candidate1 及其旧指纹保留在 [历史诊断](../Diagnostics/Candidate1-Tests/README.md)，不重标为本提交。

这些结果说明已冻结源码的完整自动回归通过；原生双进程 LAN、性能、打包和云端交付由独立证据验收。
