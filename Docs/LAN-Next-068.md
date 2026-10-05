# 下一阶段：两真人与 AI 的可信 LAN（068）

2026-10-05。本文是可执行设计，**LAN 尚未实现或验收**。依据当前 0.10 运行源码 `2668c47a426f2cea8d4bc7b309cdbfa71533cc9d`，更新了 0.9 的只读 `Proposals/replay-lan-next.md` 中联网部分的假设。现有录像/回放/观察机制见 [Replay-067](Replay-067.md)；旧、新原生回放证据都不作为网络通过证据。

首批做同一发行包、两台 Apple silicon Mac、手动 IP 加入的 2v2 合作：房主 P1、远端真人 P3，同队；P2/P4 由房主运行 AI。房间只需两个人类席位的文明、牌组与 READY，以及房主的地图、种子、难度和开局按钮。暂不需要四真人大厅。房主是唯一权威模拟，客户端提交命令并显示收到的状态。

## 从现有代码改什么

| 当前入口与事实 | 下一阶段的具体改造 |
| --- | --- |
| [GameController.Command](../Assets/Frontier/Presentation/GameController.cs) 即时调用 `Recorder.Issue` / `World.Issue`，返回 `CommandResult`；`IsWatching` 拒绝下令 | 保留单机即时行为；LAN 增加提交编号、pending 与终态回执，房主自身的输入也走同一提交队列 |
| [GameControllerReplay](../Assets/Frontier/Presentation/GameControllerReplay.cs) 的 `IsWatching` 只表示回放；普通开局把 `ObserverPlayer` 置 0，回放才可切换 | 增加明确的 Single/Replay/LanHost/LanClient 会话角色和 `LocalPlayerSlot`；LAN 客户端不冒充 `IsWatching` 来绕过步进 |
| [GameControllerExpansion.AutomatedSlots](../Assets/Frontier/Presentation/GameControllerExpansion.cs) 默认控制 `1..N−1` | 使用冻结的 human/AI roster；构造/恢复调度器都验证两者不相交，首批只给 P2/P4 创建 AI；客户端没有 AI 调度器 |
| [RtsInput](../Assets/Frontier/Presentation/RtsInput.cs) 已使用观察槽位，部分 [FrontierHud](../Assets/Frontier/Presentation/FrontierHud.cs)、[扩展 HUD](../Assets/Frontier/Presentation/FrontierHudExpansion.cs) 仍固定 `player=0` | 命令身份、所有权和可操作按钮使用 `LocalPlayerSlot`；资源、迷雾、详情和声音使用观察身份。真人 LAN 对局默认并锁定观察自己的槽，回放继续自由观察 |
| `StartMatch`、`SetDeck`、`Resign`、开局镜头及普通结果界面仍以槽 0 为真人 | 两端分别使用自己的控制槽；真人牌组来自各自已验证的 READY 配置，不能给远端套 AI 的 military 牌组；研究、升时代、制造、船运、中立服务及理由预检一并改造 |
| [MatchSimulation](../Assets/Frontier/Simulation/MatchSimulation.cs) 每步 0.1 秒，先 AI、再 World、再完成回调；`World.Paused` 阻止步进 | 仅 Single/LanHost 可推进 World。保留原 AI→World 顺序，在每一步之前建立房主命令提交边界；收包与会话控制不依赖步进成功 |

`LocalPlayerSlot` 是控制身份，`ObserverPlayer` 是显示身份，两者不能互相赋值来转交单位。教学/战役保留现有固定主角规则，首批 LAN 只开放遭遇战。普通检查点载入/重开在 LAN 中先禁用，避免恢复时丢失真人 roster、连接与去重状态；个人 Preferences 仍可使用。主机迁移和联网观战另列后续。

## 房主队列、暂停和真实结果

1. TCP 作为首批可靠有序传输；I/O 线程只收发有界字节和独立 DTO，不调用 Unity、World 或 Recorder。主线程消费入站队列时赋予全局 `hostSequence`，定义跨连接的实际顺序；客户端时间戳或声称的未来 tick 不决定执行顺序。
2. 在已完成第 k 步的世界上，按 `hostSequence` 提交命令和共享暂停/恢复，给模拟操作分配同一步 `ordinal`，之后才运行 AI 与第 k+1 步。初始 k=0 及一帧内多步都遵守这个边界。房主本地输入与远端输入没有两套付款入口。
3. 完整深拷贝 [GameCommand](../Assets/Frontier/Simulation/Model.cs) 的全部字段，保留 actors 顺序与 append。合法提交仅在房主调用 `Recorder.Issue` 一次，实际 `accepted/reason` 成为回执；协议层拒绝另写网络诊断，不能伪造为世界接受。共享暂停使用 `Recorder.Pause`，房主录像继续记录真正的权威操作顺序。
4. `PumpNetwork` 与提交队列放在 `GameController.Update` 的暂停/终局步进判断之外。暂停时仍消费 heartbeat、回执、重连、游戏请求和 Resume：普通游戏命令由现有 `World.Issue` 明确拒绝，并缓存该拒绝；Pause/Resume 是独立会话操作。**不能把 Resume 排在“下一个 tick”或只在 Step 回调内执行，否则暂停后死锁。** 暂停时间不累积为恢复后的模拟债务；暂停变化也发布新状态 revision。
5. 两端 Esc、失焦和本地菜单只暂停本地输入。共享暂停/恢复由明确按钮申请并由房主队列处理，两端显示房主的暂停状态；现有 `PauseForFocusLoss` 直接暂停 World 的单机路径不能沿用到 LAN。心跳/超时用真实时间，不能用已停止的 `World.Time`。

LAN 增加 `SubmitCommand → RequestHandle` 与 `CommandResolved(sequence, result)`，不把 pending 塞进同步 `CommandResult.accepted`。UI 反馈分为 `Pending` 与 `Applied/Rejected`。待确认时保留请求对应的动作上下文与可取消视觉预览，不扣资源、不增加人口预留、不生成单位、不写第二份订单。只有终态回执触发成功/失败提示，且不能让晚到回执关闭后来新开的目标模式。资源和队列以权威状态为准；回执附 `appliedStep/ordinal/stateRevision`，界面等待对应或更新的状态，不把“网络已收到”显示成“募兵已付款”。

## 连接身份、去重与恢复

握手同时校验网络协议/DTO schema 与现有 `ReplayIdentity`：应用、build GUID、Unity、规则 hash、地图生成版本和目录修订。两台 Mac 使用同一份发行包及包 SHA；同版本号或同种子不足以兼容。READY 后冻结 roster、阵营、牌组、地图、目标与 seed，房主生成 `matchId/matchEpoch` 和初始状态摘要，再允许命令。

连接由房主绑定 `peerId → slot`。房主从绑定推导 `command.player`，拒绝越权 actors，并检查命令类型、有限坐标、字符串/数组/计数、包长与速率。初始实现需显式上限：单命令 64 KiB、actors 1024、item/choice 各 256 字符；每 peer 至多 256 个 pending，房主至多 1024 个待提交；超额返回可见拒绝。这些是协议限制，不是已证明的大军性能。

去重键为 `(matchId, matchEpoch, peerId, requestSequence)`，覆盖已收、待执行、已接受及已拒绝。请求序号单调增长，并保存 payload 摘要；相同键和相同 payload 返回原状态/结果，不再 Issue，相同键而不同 payload 拒绝并显示协议错误。终态结果保留有界窗口和已提交水位，窗口外旧序号明确返回过期，不能在缓存淘汰后当成新命令执行。拒绝也必须缓存，防止暂停时的拒绝在恢复后重试变成付款。

建议首批 heartbeat 每 1 秒、5 秒无响应判定断连、30 秒重连宽限，作为待实际测量调整的默认值。客户端断连后停止提交，显示离线/重连；房主继续模拟并保留其人类席位，不自动再给该槽 AI。宽限超时由房主提交该槽投降，旧会话不再接纳它。房主消失时客户端冻结最后确认画面并显示连接错误，不自建第二个 World 续局。

重连用房主签发的随机会话凭据恢复原 peer/slot，换连接 generation 并废止旧 socket，**不因重连换 matchEpoch 或清空去重表**。双方交换最后确认请求水位和状态 revision；未确认请求沿用原 sequence 重试，恢复已执行的原结果。新比赛才生成新 matchEpoch，旧 epoch 请求明确拒绝。凭据只用于可信 LAN 的席位恢复，不构成互联网身份认证。

## 状态复制与成本边界

首次加入/重连发送受限的无事件历史 `WorldSnapshot` 投影及 `completedStep/stateRevision`；可复用 [WorldSnapshot.CaptureState(false)](../Assets/Frontier/Simulation/WorldSnapshot.cs) 作基线，AI 内部状态留在房主。客户端不运行 `Simulation.AdvanceFrame`、`World.Tick`、`World.Issue` 或本地付款。

连续更新使用 `Delta{baseRevision, revision, completedStep, ...}`，覆盖实体创建/更新/删除、玩家经济/队列/科技/船运、载员、目标、中立状态、炮弹及视野。客户端仅在 `baseRevision == currentRevision` 时原子应用；重复 revision 丢弃，缺基线或摘要不符请求 FullResync，期间保持旧画面并阻止新命令。FullResync 后再消费能衔接的新 delta，不能混合两个基线。协议分别限制帧长、展开字节、集合数量和入站队列；初始全量原文上限建议 32 MiB，超限明确失败，不能借用文件保存上限声称网络无界可用。

客户端新增受验证的 ReplicaApply 路径，直接更新显示状态；不能通过 Spawn/Remove/Issue 派生第二份 XP、损失或付款。保留实体 ID 与 WorldView 模型缓存，插值只影响画面。不要每 0.1 秒完整 Restore/ResetWorld 或复制全部 Events。事件用独立递增序号和有限尾部，首次加入/全量恢复静默设置游标，之后只播放未消费事件；旧事件不能在重连后重复提示/发声。两端比较的是已传输投影摘要，不能把缺少 AI 的客户端投影冒充完整 `ReplayDigest`。

首批可在可信朋友 LAN 中传完整世界投影以复用渲染。界面迷雾不隐藏客户端进程中持有的数据，**这不提供防作弊、敌情保密或加密**。如以后做竞技联网，需每接收者过滤状态和独立 replica 合同；公网匹配、NAT/中继、跨平台互通另行实现。

## 按顺序实施与验收

| 顺序 | 可交付改动 | 必须验证 |
| --- | --- | --- |
| 1 | 会话角色、LocalPlayerSlot、真人/AI roster、两席 READY；保留 Single/Replay | 远端 P3 的文明/牌组/资源/迷雾/输入与所有权正确；P3 无 AI；既有单机、回放与个人存储不回归 |
| 2 | 有界 TCP、兼容握手、host 队列、pending/终态结果、去重与暂停控制 | 相同请求重传只付款一次；拒绝重传仍拒绝；暂停时可恢复；无 socket 线程写 World |
| 3 | 初始投影、delta/revision、FullResync、事件游标及重连水位 | 错基线不误应用；断线恢复原槽与经济/订单，未确认请求不重复付款；旧 epoch/旧连接被拒绝 |
| 4 | 两个独立原生进程的 loopback | 真实 socket 分帧、延迟/断开注入、不同渲染节奏、pending 与真实 UI 下令、host 录像和日志互相对应；这是本机协议验证 |
| 5 | 两台物理 Mac 的真实 LAN | 同包 SHA/build、不同设备与 LAN IP；加入/READY/正常合作至自然结算；双方真实付款/指定攻击/船运；失焦、共享暂停恢复、真实客户端掉线/重连、房主退出与错误提示 |

本机纯 DTO/队列测试可验证长度、身份、序号、水位、所有权、结果缓存、delta 与恢复的逻辑；双进程可验证实际 transport 和两套原生 UI。它们不能替代两台物理 Mac 的路由、系统防火墙可达性、Wi‑Fi 行为、后台调度和真实连接恢复验收。

双设备证据记录两端包/设备/会话/epoch/slot、房主应用步与 ordinal、请求 sequence/真实结果、状态 revision/投影摘要、截图及日志；测量 RTT、从提交至执行的延迟、发送字节/队列深度、帧时间与内存。至少保留一次已应用但回执未确认后重连的请求，证明未重复付款；两端自然结算一致与 10–20 分钟运行样本分别记录，不用加速模拟时长替代真实运行窗口。

后续实施沿用已有私有仓库、私有开发分支和私有交付范围，可按本顺序继续，不新增一次重复授权环节。公开发布/服务器部署、合并及扩大共享仍保持原有边界。第二台设备缺席时如实标注物理 LAN 未验收，继续完成能独立验证的实现与本机证据。
