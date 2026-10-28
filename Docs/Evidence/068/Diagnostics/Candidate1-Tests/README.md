# 068 第一完整候选回归（历史诊断）

此目录保存 `run-A7qUCXDB`，绑定运行源码提交 `89fff770611a5e2b565e99e6dcbfb57235f56b45`、630 输入和指纹 `814a79ac7a1023b6a256985b1bf68d6d8824199d0cdbb343b713edd2345db3ef`。这是后续 HUD 修复前的 Candidate1，不作为当前最终验收。

实际结果为 EditMode 817/817、PlayMode 367/367，共 1184 项；失败、跳过和不确定结果均为零。独立检查逐项找到并确认原 067 的全部 1097 个用例通过，其中非回放用例 1062、回放用例 35；新增 LAN 用例 87（77 EditMode、10 PlayMode）。完成后发现共享暂停按钮在远端断线时会禁用房主恢复，主任务随后修正 HUD 条件并新增真实生命周期用例，使用新冻结提交重跑完整回归。

[EditMode.xml](EditMode.xml)、[PlayMode.xml](PlayMode.xml) 和 [source-evidence.json](source-evidence.json) 与原始文件逐字节一致。[EditMode.log](EditMode.log) 与 [PlayMode.log](PlayMode.log) 只把实际许可 `Machine Id` 字段的值替换为 `[REDACTED_DEVICE_ID]`，各 1 处，行号分别为 85 和 84；实际 Serial number 字段替换为 0 处。其余日志字节，包括不可用 token 消息、序列化诊断、引擎警告和退出记录，全部保留。

[archive-receipt.json](archive-receipt.json) 记录原始与归档字节数、SHA-256、具体行号及沿用 067 的脱敏规则；[independent-review.json](independent-review.json) 记录历史提交绑定、全部用例发现检查与当前源码已不同的边界。原始日志仍只读保留在 `Builds/Tests/run-A7qUCXDB`。本目录不证明原生双进程 LAN、性能或交付通过。
