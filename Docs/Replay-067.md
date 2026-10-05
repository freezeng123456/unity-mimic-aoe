# 0.10 本地录制与观战说明（067）

本文描述 `0.10.0` 当前源码的回放机制、文件边界和异常处理。玩家操作见 [玩家指南](Player-Guide-067.md)。测试、原生运行、性能及交付结果由对应证据单独记录，本文不作为验收结论。

回放覆盖同一客户端中的遭遇战、引导课程、原创战役和检查点恢复后的片段。它使用已有世界、AI、引导任务和固定步长调度器重建模拟；当前没有联网观战、远程玩家或跨构建兼容承诺。LAN 属于后续阶段。

## 记录什么

`ReplayDocument` 的格式版本为 1，包含运行身份、原始 `matchId`、独立 `segmentId`、起止标签、限制参数、稀疏锚点、外部操作和事件账本。

| 数据 | 用途 |
| --- | --- |
| 锚点 | 保存世界、AI 调度状态及可选引导尝试；包含模拟步数、输入和事件前缀位置、前缀哈希及状态摘要 |
| 外部操作 | 保存命令参数、接受或拒绝结果、同一步内的序号；另外记录暂停、引导选择变化和经济日志刷新 |
| 事件账本 | 保存该片段关联的完整事件历史；锚点不重复存放同一份历史，恢复时取所需前缀 |
| 运行身份 | 应用版本、原生 build GUID、Unity 版本、规则哈希、地图生成版本及引导目录修订 |

普通 AI 决策与课程脚本通过原有调度器重新执行，避免把它们再作为外部输入执行一次。真实拒绝的命令也会进入操作流；空命令、空参数通过显式标记保留。镜头、画面帧耗时和渲染时间累积不属于模拟状态摘要。

文件读取先校验字段、类型、身份、顺序、输入链、事件链和锚点摘要。播放时继续比较实际命令结果、实际产生的事件与模拟状态；出现差异会停止并显示原因。录像库中的“可打开”表示文件通过读取校验，不等于已经完整播放验证。

## 开始、保存和结束

正常开局、引导场景及检查点加载成功后自动创建录制器；基准压力场景跳过录制。默认每 300 个固定模拟步保存一个锚点，即约 30 秒模拟时间，另保存起点及导出边界。

暂停菜单的 `SAVE REPLAY` 导出当前有效片段并继续录制。同一段的后续保存原子更新同一个文件，已导出的对象拥有自己的数据，不随之后的模拟变化。保存并不代表对局自然结束。

自然对局结束、引导结果、返回菜单、重开、加载检查点、切换引导场景、进入观战及正常退出等边界会封闭当前段并安排保存。GUI 文件读写和目录扫描使用后台任务；写入依次排队，最多允许 4 个待完成写入。必须看到 `Recording saved` 或相应成功日志，才能确认文件已落盘。

对局报告和录像分别保存。`SaveReport` 先刷新经济事件并关闭应结束的录制，再写报告；报告目录不可写等 `IOException` 会显示报告错误，不撤销已排队的录像写入。录像自身的写入仍可能失败，应分别查看反馈。

恢复手动检查点会保留原始对局身份，创建新的 `segmentId`，从恢复时的世界和模拟步数开始录制，并标记 `loaded_checkpoint`。新段不补造检查点保存之前的输入历史；时间轴起点可以大于零。

## 播放、跳转与观察槽位

录像从 `RECORDING LIBRARY` 打开后默认暂停。播放速度只支持 `0.5×`、`1×`、`2×`、`4×`，改变播放节奏，不改变固定模拟步长 `0.1` 秒。

时间跳转先恢复目标之前最近的锚点，再正向执行到目标。GUI 每帧最多重建 20 个模拟步；`SEEKING` 时仍可移动镜头，播放按钮暂不可用。时间轴只接受文件实际记录的区间，不外推未记录的未来。

`VIEW P1` 等按钮循环选择该录像已有的槽位。界面 P1–P4 对应内部 `observerPlayer` 0–3；两槽录像只有 P1、P2。资源、人口、生产、单位选择、雾、小地图、字幕及事件音效跟随观察槽位，同队共享视野仍按原规则生效；全局槽位身份和配色不随观察者重编号。

观看期间可移动镜头、选择和查看对象，不能下达命令、建造、募兵、船运、投降或写入对局检查点。课程和战役在回放中完成也不会写入账号进度或解锁章节，观战不生成新的对局报告或录制。偏好设置仍属于当前玩家。

`RECORDED SEGMENT VERIFIED` 表示所记录片段到达末端并通过回放一致性检查。它不表示自然获胜。`INTERRUPTED RECORDING` 即使其有效前缀通过验证，也仍保留中断标记及原因。

## 容量限制和中断

| 限制 | 当前数值 | 达到边界的处理 |
| --- | --- | --- |
| 外部操作 | 200,000 条 | 停止继续记录，保存可表示的有效前缀及中断原因 |
| 事件历史 | 1,000,000 条 | 越界后回退到最近有效锚点所覆盖的前缀 |
| 稀疏锚点 | 10,000 个 | 停止录制并保留最近有效锚点覆盖的前缀 |
| 单条命令 | actors 至多 100,000；item、choice 各至多 4,096 字符；位置必须有限 | 无法表示时先冻结有效前缀；实际游戏仍按正常命令路径处理输入 |
| 文件存储 | 64 MiB，即 67,108,864 字节 | 拒绝读入或写入超限文件，保留已有文件 |
| 解压原文 | 256 MiB，即 268,435,456 UTF-8 字节 | 拒绝超限压缩展开或编码，不自动裁剪新文件 |
| 录像目录 | 单目录至多 256 个匹配文件 | 超限显示目录错误，不自动删除文件 |

录制器中断与文件写入失败是不同状态。前者用 `incomplete=true` 和非空 `incompleteReason` 标明，可能早于现场已经继续推进的世界；后者显示保存错误，保留旧文件和仍可用的内存导出，不会把字节超限自动转换成一份较小的成功录像。原子更新需要临时文件与替换成功，失败时不会先删除旧文件。

上述字节上限分别约束落盘 JSON 和解压后的 UTF-8 原文，不是整个进程的内存上限，也不保证某个固定录制时长。

## 格式与兼容

新写入使用存储容器 schema 2：单一 gzip 成员、规范 Base64 和原文 SHA-256。读取还接受容器 schema 1 的明文包装，但其中必须仍是当前合法回放文档；旧对局存档、旧回放文档格式及未知未来格式均不能据此导入。

读取拒绝非法 UTF-8、不成对 Unicode 转义、重复或未知字段、缺失字段、错误标量类型、非有限数值、无效顺序、错误摘要、损坏 CRC、截断、拼接 gzip 成员及尾随数据。损坏或不兼容文件保留原文，在目录中显示错误并禁止打开；保存也不会静默覆盖这样的目标文件。

回放要求应用版本、build GUID、Unity 版本、规则哈希、地图生成版本及引导目录修订全部匹配。相同 `0.10.0` 版本号的另一次原生构建也可能因 GUID 不同而拒绝。编辑器使用 `editor:<runtime module MVID>` 作为构建身份，不冒充原生 build GUID。需要长期留存时，应同时保留生成录像的原始应用构建。

后台线程只处理已分离的普通 DTO、规则副本与文件，世界替换和界面操作留在主线程。Unity 6 官方文档允许后台线程调用 `JsonUtility.ToJson`，并要求序列化期间不修改输入对象；`FromJson` 支持后台线程中的普通类和结构体。本实现以独立导出数据遵守这一边界。[ToJson 文档](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/JsonUtility.ToJson.html)、[FromJson 文档](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/JsonUtility.FromJson.html)。

## 文件、CLI 与捕获元数据

默认录像位于 `Application.persistentDataPath/Replays/replay-<segmentId>.replay.json`，独立于 `Saves`、`Account`、`Preferences` 和 `Reports`。

| 参数 | 行为 |
| --- | --- |
| `-frontierReplay <文件>` | 启动后异步打开指定录像，成功后进入暂停的观战状态 |
| `-frontierReplayDirectory <目录>` | 指定录像保存与目录扫描位置，不移动其他玩家数据 |
| `-frontierReportDirectory <目录>` | 指定普通对局报告及事件 JSONL 输出位置，不改变录像位置 |

参数路径经 `Path.GetFullPath` 解析，建议使用带引号的绝对路径。`-frontierReplay` 在普通启动分支中优先于自动对战和引导开局；不要把它与基准压力参数组合为普通观战操作。

F9 PNG 的附属 JSON 升为捕获 schema 4，保留真实应用版本和 build GUID，并记录回放身份、片段、文件关联、起止与当前步、seek、暂停、速度、验证、错误、中断及观察槽位。统计按观察玩家采样；历史已保存路径单独记录，未保存的新段不会关联上一局文件。schema 4 只描述截图时状态，不替代回放一致性验证，旧版本捕获证据仍保持其原格式。

## 源码依据

- 文档与一致性：[ReplayModel.cs](../Assets/Frontier/Simulation/ReplayModel.cs)、[ReplayDigest.cs](../Assets/Frontier/Simulation/ReplayDigest.cs)。
- 录制与播放：[ReplayRecorder.cs](../Assets/Frontier/Simulation/ReplayRecorder.cs)、[ReplaySession.cs](../Assets/Frontier/Simulation/ReplaySession.cs)、[MatchSimulation.cs](../Assets/Frontier/Simulation/MatchSimulation.cs)。
- 文件校验：[ReplayStore.cs](../Assets/Frontier/Simulation/ReplayStore.cs)、[ReplayJson.cs](../Assets/Frontier/Simulation/ReplayJson.cs)、[MatchSaveStore.cs](../Assets/Frontier/Simulation/MatchSaveStore.cs)。
- 用户操作：[GameControllerReplay.cs](../Assets/Frontier/Presentation/GameControllerReplay.cs)、[FrontierHudReplay.cs](../Assets/Frontier/Presentation/FrontierHudReplay.cs)、[NativeCapture.cs](../Assets/Frontier/Presentation/NativeCapture.cs)。
