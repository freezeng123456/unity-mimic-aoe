# Candidate2 070 独立证据核对

核对日期：2026-10-06。本次逐个读取实际 NUnit 叶子用例，独立计算 SHA-256，并核对源码冻结、测试记录及应用构建；未启动 Unity，未改项目源码或文档。

**全部 25 项审计检查通过。正式测试实际 967/967 EditMode + 397/397 PlayMode = 1364/1364 Passed，无失败、跳过或不确定项。**

| 绑定项 | 实际核对结果 |
|---|---|
| 运行提交 | `8b6598d0939262571d0a595ba1499e053f00e1b7`，与冻结及构建回执一致 |
| 当前运行输入 | 682 个，当前、测试记录与冻结清单逐文件完全一致 |
| 当前输入指纹 | `ab5ae20e5ff0de9d5e3b2fdd9346ed9f158cb4082a05933e4d6b074cd7333047` |
| 测试目录 | `/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project/Builds/Tests/run-u4J3ZXEB` |
| EditMode XML SHA-256 | `79722d3a5cc1f75748d12abfa25118fcf5d52c73518ad8ef32a4b763d2a334af` |
| PlayMode XML SHA-256 | `1a28445bddce5fb860422a42718b05e8ffb42dd6b7a9872296e2d412caee7f7a` |
| 构建 GUID | `1e9cea464e46478d9193c121b4ce3534`，应用内 boot.config 与构建回执一致 |
| 可执行文件 SHA-256 | `1bd40eec878cc11bc2da1c503be80ab81d8b50f51725dd7c294e8af025ebd1f4`，实际字节数 35151 与回执一致 |
| Frontier.Runtime.dll SHA-256 | `0973060049f798faf81fc6f42881b5c34d3f2bed9615e77beb11fa463239d767`，实际字节数 1134080 与回执一致 |
| 构建日志 | 实际 Success 与 FRONTIER_BUILD Succeeded；日志 SHA 与回执一致；构建开始晚于正式测试证据记录 |

当前 Assets、Packages、ProjectSettings、Tools 内没有工作树改动。另有以下文档改动，不属于 682 个运行输入；因此不声称整个 Git 工作树干净：

- ` M Docs/AoE3-Gap-Roadmap.md`
- ` M Docs/Completion-Status.md`
- ` M Docs/Content-070.md`
- ` M Docs/Known-Issues.md`
- ` M README.md`
- `?? Docs/Evidence/070/`
- `?? Docs/Player-Guide-070.md`

| 正式全套直接包含的相关 suite | 实际通过叶子数 |
|---|---|
| `GuidedPlay066Tests` | 29/29 |
| `ScenarioGroups070Tests` | 27/27 |
| `RiverContent070Tests` | 25/25 |
| `RiverCampaignRestore070Tests` | 16/16 |
| `RiverMissionBranches070Tests` | 4/4 |
| `RiverContentController070Tests` | 6/6 |

新增 `ScoutRaidWaitsThroughTheDefaultCrossingThenReallyAttacksTheSupplyCamp` 已在正式全套中通过：无命令开场真实推进 100 步、前锋及探索者正常过桥时两骑仍存活且未接到袭营命令；调查后触发普通 `AttackMove ×2`，核验营地请求坐标和实际合法可达终点，再由真实行军进入营地 25 格范围并造成正常 Damage。该证明没有注入位置、HP、时间或胜负，也没有扩大原先猜测的 8 格终点阈值。

原 7 课/3 章的真实通关与冷恢复参数用例仍全部通过。070 指定部队组与指定运输舰、空船护送反例、三个新章真实通关、陆路及水路撤退、阶段边界冷存档、Convoy 完整 Replay、账号独立解锁、瞭望 120 木材与仓库 100 木材/80 钱币一次性补给、实际受伤后的付费 DockRepair、真实约 360 秒调查截止失败及重试，均对应正式全套的通过叶子用例；JSON 保留六个相关 suite 共 107 个具体用例名称及结果。

本报告只确认上述自动测试、当前源码与构建绑定。Candidate2 原生鼠标键盘/焦点/画面/性能及新进程冷恢复、两台物理设备联机、普通地图实际付费造港造船及完整 AI 歼灭对局/文明与两侧平衡、护送东渠绕行未由本报告验收。固定章节的必需目标期限先于 1200 秒总时限，因此只记录自然可达的调查目标截止用例，未伪造总时限覆盖。

Candidate1 的测试、构建与原生证据保持独立，未替换为 Candidate2，也未用其 XML/构建身份支撑本报告。机器可读结果见 [independent070-test-audit.json](/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.13.0/Candidate2/Diagnostics/independent070-test-audit.json)。
