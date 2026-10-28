# 068 完整回归测试（Candidate3）

`run-Z47ikuJY` 实际通过 EditMode 817/817、PlayMode 368/368，共 1185 项；失败、跳过和不确定结果均为零。运行源码提交为 `dca073708b671c60a1fe9ff243a67e4ace3fe157`，630 输入指纹为 `62240c2373c773b5aaf807c72815acab2e76577504f3a0569218702864730ebe`。独立审查将冻结文件、测试清单和 Git 提交中的每个输入逐字节比对，重读确认未漂移，当前源码验证器退出码为 0。

原 067 的全部 1097 个用例按完整名称逐项找到并通过：非回放 1062、回放 35（10 核心、20 存储、5 控制器/UI）。新增 LAN 88 项实际发现并通过，分为 77 EditMode 和 11 PlayMode，覆盖 8 个测试组。Candidate2 的全部用例名称也在本轮实际运行；第三候选对状态文本的回归断言绑定本提交。

[EditMode.xml](EditMode.xml)、[PlayMode.xml](PlayMode.xml)、[source-evidence.json](source-evidence.json) 与构建前 [source-freeze.json](source-freeze.json) 均原字节保存。两份日志仅替换实际许可证设备字段：EditMode.log 第 95 行；PlayMode.log 第 94 行，共 2 个 Machine Id、0 个 Serial number，替换为 `[REDACTED_DEVICE_ID]`。其他字节、行数、换行、引擎与序列化警告、不可用 token 诊断和退出记录保留，原始日志仍只读保存在本地运行目录。

[archive-receipt.json](archive-receipt.json) 记录原始/归档 SHA-256、字节数和具体脱敏行号；[independent-review.json](independent-review.json) 记录全部 16 项独立检查、实际测试组数量与旧用例发现结果。旧候选分别保留在 [Candidate1](../Diagnostics/Candidate1-Tests/README.md) 和 [Candidate2](../Diagnostics/Candidate2-Tests/STATUS.md)，其字节和哈希未改动。

本目录说明已冻结源码的完整自动回归通过。原生双进程 LAN、性能、打包和云端交付按各自证据验收。
