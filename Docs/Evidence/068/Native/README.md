# 068 原生同机双进程 TCP 验收

[native-acceptance.json](native-acceptance.json) 的通过范围是：同一台 Mac 上两个分别启动的 macOS arm64 Development/Mono 原生进程，使用真实 loopback TCP 的 P1/P3 双真人合作与 P2/P4 AI。最终 Candidate3 绑定提交 `dca073708b671c60a1fe9ff243a67e4ace3fe157`、构建 GUID `fa26339934914cd2813df9763de6bba7`、630 输入指纹 `62240c2373c773b5aaf807c72815acab2e76577504f3a0569218702864730ebe`。源码完整回归为 [Tests](../Tests/README.md) 的 `run-Z47ikuJY`：817+368=1185 项。

[actual-ui-observations.json](actual-ui-observations.json) 原字节保留 root 的 22 项操作观察，另保留 24 张 [UI 图片](UI)。[inspection-independent.json](inspection-independent.json) 独立复跑保存的检查脚本，对 8 组 host、8 组 client F9 PNG/JSON 核验哈希、schema5、构建和 MatchId、角色/控制槽/观察槽及主机独有 Simulation/Recorder；[inspection-final.json](inspection-final.json) 同时原字节保留。22 个 UI 观察引用的图片哈希实际逐项匹配。角色控制槽为 host0/client2，文明为英国 `coast`、荷兰 `river`、法国 `france`、奥斯曼 `ottoman`，team0 的 P1/P3 真人对 team1 的 P2/P4 AI；地图 `generated_river`，seed314159，起始 300F/300W/100C。

|实际原生操作|对应保存证据|
|---|---|
|双方 READY、host START|UI01–06，首帧 host10/client13 步、正常资源|
|P3 法国两次 Train，再取消第二个同类作业|命令300/311/530步；300→180→60→180F，reserved2→1；index1取消后首job elapsed23.50005/28 秒（约5秒剩余），之后继续并仅完成一次|
|P1 英国 Train|826步，扣100F，300→200F，host831步 reserved1|
|Esc 本地菜单|client535→543步，localMenuPaused=true/sharedPaused=false，世界与首job持续推进|
|host 共享 Pause|双方845步/84.49928283691406秒/eventCursor1579/digest相同|
|18秒 SIGSTOP 故障中，host 仍可恢复|UI14/15 与故障记录，host845步 connected=false/sharedPaused=true→851步 connected=false/sharedPaused=false；本地菜单仍开着|
|SIGCONT 后重新同步|UI16–18，双方1663步/166.3009490966797秒/eventCursor3159/digest相同，同 MatchId|
|client 恢复、V选择、右键 Move|1884步 accepted、actor104 目的地；UI19与 client1889步快照|
|client Leave、host继续、保存、host离开/双方Quit|UI21主菜单、22/23稳定 Ally left 文字、24主菜单；录像1902步 slot2 Resign，两个 owned exec sessions exit0|

[replay-inspection-final.json](replay-inspection-final.json) 独立检查最终 [host-only 录像](host/Replays/replay-c6bb09c7691f46028ffb31a9c94bb54e.replay.json) 与主机 menu_exit 报告：startKind=`lan_host`、finishReason=`menu_exit`，0→2210步/221.0042877秒，9 anchors、11 records、4442 events，incomplete=false、winner=-1。训练两次扣费及取消退款依据 Payment/Refund 事件，先前作业进度依据 F9 队列和 UnitTrained 事件；日志的5条 remote RESULT 均唯一对应 host APPLY。hash/账本检查明细在摘要中。本次离线检查没有启动 Unity 重新模拟或运行原生冷回放。录像不含 LAN stable queue ID/expectedQueueId，能够证明此轮取消 index1、退款和原首job保留；wire身份约束另由自动测试验证。

最终保存文件是退出时更新的封闭记录段，尚未自然胜负结束；早先 SAVE REPLAY 手动前缀字节未单独保留，不能把最终 menu_exit 文件冒充原手动前缀。Inspector 的 nativeAcceptance=pending 表示其职责仅为元数据检查；本目录的 scoped [native-acceptance.json](native-acceptance.json) 合并 root 的真实 UI 观察、独立静态证据核对和离线录像检查，没有扩大为实体双机验收。

[Build/client-build.json](Build/client-build.json)、[Build/build-identity.json](Build/build-identity.json)、[Build/boot.config](Build/boot.config) 与 [source-evidence.json](source-evidence.json) 绑定最终构建和源码冻结。可执行 SHA 实读匹配 descriptor；[archive-receipt.json](archive-receipt.json) 给出所有来源/归档字节、SHA-256、精确字段筛查与原文件不变检查。两份 owned player.log 的 licensing Machine Id/Serial number 和凭据规则实际命中0处；普通诊断行、录像、事件账本、PNG/JSON 与 root 观察全部原字节保留。原生检查脚本也一并保留供审阅；它按原候选位置运行，报告路径与原日志哈希通过 receipt 映射到归档。没有 `.app`、可执行文件或个人目录副本。

两台实体 Mac 的真实网卡/防火墙、原生 ACK丢失恰好一次付费故障、此次 LAN 的直接目标攻击、原生冷回放、自然完整对局、长窗性能/内存/延迟和签名 Release/其他平台均保持待验收。Candidate2 的旧文字问题及其旧构建证据独立保存在 [诊断](../Diagnostics/Candidate2-Native/README.md)，不用于证明本次最终构建。
