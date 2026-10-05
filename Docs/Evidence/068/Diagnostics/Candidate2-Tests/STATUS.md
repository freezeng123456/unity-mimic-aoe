# 068 Candidate2 历史测试归档

本目录保存 `be6665d53e9c0c7fc08c1e9f805dfad2c6906de4` 的完整自动回归：817 EditMode + 368 PlayMode = 1185 项全部通过，旧 1097 个用例均实际运行、新增 LAN 88 项；630 输入指纹为 `38d592d516a6ad62d68bd42a5940f996851a7e469bfe20bb35bcc67b92428ad7`。

主任务随后在原生验证中确认房主大厅与退役伙伴的状态文字过时，并准备第三候选修复与完整回归。因此本目录是第二候选的历史诊断，不作为修复后或原生最终验收。`Docs/Evidence/068/Tests` 留给后续新冻结输入的结果。

归档从原 Tests 入口整体移动，八个原有文件的名称、字节和 SHA-256 均未改动。[relocation-receipt.json](relocation-receipt.json) 给出逐文件搬移核验；[archive-receipt.json](archive-receipt.json) 保留原始/脱敏哈希，[independent-review.json](independent-review.json) 保留评审时的 15 项源码绑定与用例检查。日志仅含两处实际设备 ID 替换，许可证序列号替换为零处，其他字节保留。

[原始评审 README](README.md) 及独立回执中的旧相对指引记录搬移前位置。本页提供搬移后的历史链接：[Candidate1 测试](../Candidate1-Tests/README.md)。原生验证与此后修正由主任务分别记录。
