# Candidate2 原生 LAN 诊断

此目录保留 0.11 Candidate2 的诊断证据。它绑定运行提交 `be6665d53e9c0c7fc08c1e9f805dfad2c6906de4`、构建 GUID `27816122099c4ea1909323eb6500bde3`、630 输入源码指纹 `38d592d516a6ad62d68bd42a5940f996851a7e469bfe20bb35bcc67b92428ad7`。两原生进程在同一台 Mac 上运行；root 报告 host PID 24867、client PID 24926 均正常退出。它不能证明 Candidate3 或两台实体 Mac 的网络验收。

[inspection-final.json](inspection-final.json) 对 3 组 host、7 组 client 原生 PNG/JSON 完成记录核验：PNG 哈希及尺寸、schema5、相同构建和 MatchId、host 控制槽/观察槽 0 且独有 Simulation/Recorder，client 控制槽/观察槽 2 且无 Simulation/Recorder，均符合。双方共享暂停快照实际一致：819 步、81.89932250976562 秒、eventCursor 1533；重连后另有 1373 步、137.29917907714844 秒、eventCursor 2526 的一致快照。暂停期间发布 revision 可以增加；local menu 与 shared pause 分开核验。4 条 client RESULT 均唯一对应 host APPLY。报告中的元数据 verified 不是完整原生验收 Passed。

[replay-inspection-final.json](replay-inspection-final.json) 检查唯一的 [host-only 录像](host/Replays/replay-5810162ebf7a49b2a1a272b1fc7d040f.replay.json) 与 menu_exit 报告。最终文件的 startKind 为 `lan_host`，finishReason 为 `menu_exit`，包含 0 至 1597 步、7 个 anchors、9 条外部 records、3035 条事件；incomplete=false、winner=-1。这个已封闭记录段仍未自然终局，不能称为完整胜负对局。root 的早先 SAVE REPLAY 手动前缀已被同段后续保存更新，未单独保留当时前缀字节。

|实际命令|步/序号|可核查结果|
|---|---|---|
|P3 法国 Train coureur|447 / 0|accepted，Payment event954 扣 120F，300→180；client 快照另见 reserved1|
|P1 英国 Train villager|810 / 0|accepted，Payment event1512 扣 100F，300→200；host 快照另见 reserved1|
|Pause / Resume|819 / 0、1|同一 81.8993225 秒边界，暂停仍可消费恢复命令|
|P3 Move actor104|964 / 0|accepted，录像/快照均有实际目的地与后续位置|
|Pause / Resume / P3 Resign|1373 / 0、1、2|accepted，实际 Resign event2527；后续 host 快照 slot2.resigned=true、sharedPaused=false|

该离线检查验证保存包完整性、记录/事件/锚点哈希以及录像和报告的事实对应；没有启动 Unity 重新模拟或进行原生冷回放。结构化日志不含命令类型或支付值，支付依据来自录像事件和快照，不能只凭日志的 accepted 布尔值推断。

[actual-ui-observations.json](actual-ui-observations.json) 逐项保留 root 实际操作观察，并标记存储证据的覆盖范围：READY/START、各自付费训练、Esc 本地菜单、host 共享暂停、client 恢复/V 选择/右键移动、18 秒 owned-client SIGSTOP/try-finally SIGCONT、同 Match 重连、host 保存录像、client 离开及 host 恢复/远端投降。[UI](UI) 保留 5 张命名截图。没有保存 outage 当刻的 host 截图，也没有 client 离开后的主菜单截图；这些静态图像覆盖缺口没有补造。Candidate2 在永久离开后仍显示 waiting-to-reconnect 的旧文字，保留为诊断，Candidate3 修复需由自己的证据证明。

[Build/build-identity.json](Build/build-identity.json)、原始 [Build/client-build.json](Build/client-build.json)、[Build/boot.config](Build/boot.config) 与 [source-evidence.json](source-evidence.json) 保留构建及冻结来源。可执行文件 SHA 实读匹配原构建 manifest；manifest 的历史 fullTests=in progress/nativeAcceptance=pending 字段原样保留，不改写。对应完整自动回归为 [Candidate2-Tests](../Candidate2-Tests/README.md) 的 817+368=1185 项，原生证据单独核验。

[archive-receipt.json](archive-receipt.json) 列出每个来源与归档的字节数、SHA-256、变换与原文件保持情况。PNG/JSON、录像、报告、命名 UI 图片和冻结材料均原字节复制。两份 owned player.log 按精确 licensing Machine Id/Serial number 及凭据字段规则筛查，实际命中 0 处；不存在为了声称脱敏而删掉普通诊断行的操作。原日志和快照留在本地 Candidate2，归档无需包含 `.app` 或读取个人目录。inspection 的路径/原日志哈希指向原候选，归档映射和日志筛查字节凭据见 receipt。

两台实体 Mac、真实链路故障、原生冷回放、1×性能和 Candidate3 验收均保持待验证。本目录仅为 Candidate2 诊断，不是最终发布通过声明。
