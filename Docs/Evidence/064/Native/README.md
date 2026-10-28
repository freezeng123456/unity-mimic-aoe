# 0.7.0 原生证据

已归档当前客户端身份、一次普通局保存/覆盖/进程重启/冷载入/续局、四文明受控工业展示、两组最终 200/400 陆军加 12 船压力样本，以及一局正常规则双 AI 自然终局。压力统计各约 80 秒，焦点仅有预热期及保存时点记录；这些材料不证明长期战争、全程焦点或最终 ZIP/Drive 交付完成。

[client-build.json](client-build.json) 为原始构建回执的逐字节副本：

| 身份 | 值 |
| --- | --- |
| 应用版本 | 0.7.0 |
| Unity | 6000.6.3f1 |
| 架构 / 类型 | macOS arm64 / Development · Mono |
| Build GUID | `48cbcf92c92f409c945f83eeb9b8d500` |
| Runtime commit | `5cce74ccf7222133af9bad633923f452b100bfb2` |
| 源码指纹 | `8342b85bf835fd48d5c4dfc8f612ed64770fc5b7d026d96c4f539caaa96d79b0` |
| 冻结输入文件数 | 497 |
| 自动化测试 | EditMode 466/466 + PlayMode 330/330 |
| 回执 SHA-256 | `d65ad21a4ed0b9c6e03e9c608d8a337e23f24d4ff715eeb25776c4fac02fca26` |

构建回执绑定测试 XML 的相同 SHA，记录 build 后源码指纹核验及实际可执行文件和 boot.config 的哈希；构建后移除 1 条本地调试地址的包装变换已在回执中披露。此处没有生成最终分包回执或重新核验 ZIP 内容。

| 原生项目 | 当前证据与边界 |
| --- | --- |
| 普通局冷恢复 | [Normal](Normal/README.md)：7 对 F9、A/B/冷重存/继续四份检查点、进程退出回执、保存/载入日志和逐字段复核。Standard AI、正常 25 张卡组、I 时代；不是自然终局。 |
| 普通经济短窗 | [原始 F8](Normal/normal-economy-performance.json)：1600×1000、约 88.43 秒、5264 eligible 帧，P95/P99 17.378464/17.605961 ms；不满足 300 秒长窗或压力验收。 |
| 四文明工业展示 | [Industry](Industry/README.md)：最终四份存档及报告、10 对实际原生 F9；IV 时代、补足 XP、两张工业卡、空 AI controllers。分别选中 Rocket、Heavy Cannon、Great Bombard，英国另验证模式切换。不是正常开局工业对局。 |
| 200/400 陆军加 12 船 | [Benchmarks](Benchmarks/README.md)：各约 90 秒总 active 含 10 秒预热，约 80 秒测量、800 实际步，212/412 活体保留。P95/P99 为 17.063089/17.582590 与 32.546711/33.583000 ms；400 组有 13 帧 >50 ms、1 帧 >100 ms。焦点没有连续仪器化记录，不能称全时检测。 |
| 正常规则自然终局 | [Autoplay](Autoplay/README.md)：法国对奥斯曼、Standard、16×模拟、正常 I 时代开局，1047.691 秒自然胜利，时代4/2。法国真实船运马车并生成工厂地基，终局未完工、无工业重炮；不用于普通速度性能或广泛平衡结论。 |

完整自动化测试见 [Tests](../Tests/README.md)。原生拒绝损坏/旧版本存档、写入途中强制终止、Ctrl/Command 编队创建快捷键没有被本次普通局证据覆盖。两份最终压力日志仍记录线程 prematurely finalized 与 MemoryLeaks 退出遥测，保留现象且不据此判定根因。长期战争/水战、长期内存、Release 和跨平台限制仍以 [Known-Issues](../../../Known-Issues.md) 为准。
