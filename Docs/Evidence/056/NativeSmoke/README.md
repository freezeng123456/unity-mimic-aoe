# 0.2.32 原生菜单、开局、采集与退出检查

**通过，仅限本次短流程smoke。** 实际从`Builds/macOS-0.2.32/Frontier Reckoning.app`启动，使用全新独立`Builds/NativeSmoke056/Player.log`。所有4组F9与menu_exit报告均为 **0.2.32 / GUID `947f7c0c0da44ec3a806c2dcb69ca04e`**。GUI只通过公开Sky操作；运行期间仅看普通界面，QUIT后才读取日志、事件与F9元数据。

## 实际路径与证据

| 步骤 | 实际记录 |
|---|---|
| 菜单 | [开始菜单PNG](Frontier-20261003-115909-214-f1801-cea28dd8f887438a9c6a7c1b0b7b8163.png)与[元数据](Frontier-20261003-115909-214-f1801-cea28dd8f887438a9c6a7c1b0b7b8163.json)；实际读到`FIVE-ERA LAND GAME`和`Banks · Trade routes · Guarded treasures · Original visuals and audio`。旧菜单DEVELOPMENT后缀和still in development说明已移除，标题、说明及所有按钮完整可读，无观察到的裁切/重叠。Unity Development Build水印保留，与构建配置一致。 |
| 普通开局 | [开局PNG](Frontier-20261003-115911-442-f1924-7b08ea9b65db467582bed5a2a31f8c4c.png)；match`fd093483132f4bff991d9082260fd7a4`，Coast对River、Standard、V上限、Economy25；1x、autoplay=false。0.900000秒F300/W300/C100，普通起始6工。 |
| 实际采集 | V选择工人、右键食物箱。事件L374于19.300037秒记录玩家0、村民2对资源35的`Gather ×1`。9条Gather共46.799996食物，与报告46.799976在浮点容差内一致。[采集PNG](Frontier-20261003-115956-721-f4606-399eaaf9b50842219fcb5eb0607dcd81.png)在26.400064秒显示SETTLER、Gathering Food、Orders1，FOOD341；精确库存340.800415。 |
| 返回菜单 | 暂停界面点击RETURN TO MENU，27.400068秒生成[menu_exit报告](20261003-120020-122-fd093483132f4bff991d9082260fd7a4-menu_exit.json)与[475条事件](20261003-120020-122-fd093483132f4bff991d9082260fd7a4-menu_exit-events.jsonl)；随后[回菜单PNG](Frontier-20261003-120020-926-f6037-94aa506bd11d442495fab6818fd5ccf3.png)再次完整显示新菜单。 |
| 公共QUIT | 点击菜单QUIT，随后[专用日志](Player.log)显示Input Shutdown与CodeReloadManager destroyed；[确切exe进程检查](process-exit-check.json)于2026-10-03T12:00:47.402459Z返回空。QUIT后没有再调用Sky app-state，不重开客户端。 |

[smoke-summary.json](smoke-summary.json)保存17项本流程核对、原始文件配对、事件位置与采集数值；[operator-observations.json](operator-observations.json)明确记录人工控制步骤及像素观察，不冒充引擎输入轨迹。原始输入trace未启用，但Gather命令、资源交付、menu_exit及shutdown均有相应记录。

## 身份、完整性与保留诊断

[app-identity.json](app-identity.json)记录退出后读取的确切新app路径、三个compiled文件SHA、plist语义版本与boot.config GUID；[原始plist](app-Info.plist)和[原始boot.config](app-boot.config)一并保留。`CFBundleVersion=0`是冻结macOS buildNumber，语义版本取`CFBundleShortVersionString=0.2.32`；未改源码或客户端。

首个用共享bundle ID查询窗口的Sky调用返回“Ambiguous app identifier”，因为旧、新app同ID；该查询失败后改用确切新app绝对路径。此工具错误在operator记录中完整保留，未将失败查询写成成功。新专用日志的Mono路径及所有原生证据均配对新app。

日志保留6条`Thread … may have been prematurely finalized`及Unity `MemoryLeaks`退出遥测（allocatedMemory3138875）。没有所扫描的运行时Exception/Error/crash模式；本smoke不据此诊断这些退出遥测，也不宣称无内存泄漏。

这是授权的短流程，**主动menu_exit、没有自然胜负或整局结算**；它不替代、不重标0.2.31原生A–D、音频或性能证据。新完整424测试、精确两文件差异和构建回执由056其他证据分别核验。

[receipt.json](receipt.json)列出除其自身与`receipt.sha256`外每个归档成员的字节数/SHA256，并声明清单边界；`receipt.sha256`封存receipt本身的哈希。所有原生文件按字节复制，未改写。
