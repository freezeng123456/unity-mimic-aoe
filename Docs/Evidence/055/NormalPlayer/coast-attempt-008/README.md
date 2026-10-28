# B8：主动返回菜单中断，不是自然败局

原生 macOS 客户端 0.2.31／GUID `128a731d440e4b7a94abfcc74b88fa97`，对局 `6cf241946653492ea782b2f968b5e229`，Coast Compact 镜像、Easy、III、seed 314159、Military 25/25、正常 1×、autoplay=false。对局在 **259.606628 世界秒**暂停后通过正常 `RETURN TO MENU` 主动中断；原报告 reason=`menu_exit`、winner=-1，没有 MatchEnded、投降或自然结算。

[summary.json](summary.json) 保留原严格玩家审核结果 **failed**，不是自然败局：无自然结束、有效 winner／结束时间、败方清空三个自然门槛未通过，另缺时代 II、时代 III、付费军队、付费炮、炮击敌建筑、玩家采集及船运到达。身份、正常开局、真实支付与完成链检查保持原样，并未为中断放宽成通过。[interruption-audit.json](interruption-audit.json) 将整体状态明确标为 `interrupted_via_return_to_menu_no_natural_end`。

开局 F9 世界时间 0，明确 **paused=false**。输入日志虽然有第一次 Escape 边沿，却没有当时对应的暂停记录；第一次实际 `manual_pause` 到世界 **223.604446 秒**才发生。主代理记录这段延迟跨过上下文整理期间，工人没有建立采集路线；事件独立证据是玩家 0 全局 `Command=0`、`Gather=0`，不能将鼠标工具调用意图当作实际命令或将未经确认的 Escape 当作暂停。

恢复后确有三次正常付费村民队列：在 224.404495／224.504501 秒分别扣除 100 F，总计 300 F。城镇中心串行训练，截止返回菜单时**只有 1 名付费村民完成出生**，另 2 个作业尚未完成。不能把三次支付／排队说成三名新村民已经出生；确实完成的支付、排队、Spawn、训练 XP 链保留在严格摘要。中断时事件重建存活单位和建筑为 `[9, 22]`，也不能将其判作玩家被消灭。

三组原始 JSON/PNG 均按字节保存并通过 SHA、尺寸、版本／GUID、请求／实时／HUD 对局配对：

- 独立菜单 `Frontier-20261002-162624-237-f4542-620719c6316844469e6b723c3bcc2697`。
- 开局 `Frontier-20261002-162634-257-f5132-da07e2a18a1a4372b3ad1ebe0331ef6a`，世界 0、尚未暂停。
- 中断前暂停 `Frontier-20261002-163139-350-f17060-c33d035acf854df49dc2b3dfcd60e898`，世界 259.606628，与 menu_exit 时间一致。

最后一张是**中断前暂停图**，不能冒充 DEFEAT／VICTORY 或自然结算 F9。后续 B9 的菜单／开局和实时隐藏敌军没有纳入本目录；卡组仍是主代理 GUI 配置记录与独立菜单图片配对的外部声明，原生元数据没有 deck 字段。

收到归档任务后立即复制 `Builds/NativeInput055-B8/Player.log` 为本地读时快照，再从 B8 独立菜单到 B8 `menu_exit` 标记截取原始第 96–206 行、10,603 字节。`native-input-b8-menu-exit-excerpt.txt` 保留原行字节，`native-input-b8-trace-only.txt` 保留这个区间的输入／UI／暂停／捕获标记；完整快照中后续 B9 部分没有解释或发布。[input-trace-audit.json](input-trace-audit.json) 保存本地快照与片段 SHA、原始行号。活日志仍可继续追加，不把原路径说成永久不变的完整归档。

原生 `menu_exit` 报告与完整事件日志、Rules／DeckLibrary／解析器快照的来源字节和 SHA 见 [archive-receipt.json](archive-receipt.json)。没有补造自然报告或修改游戏规则，也没有操作 GUI、读取 B9 隐藏状态、更新六份总览或提交／推送。本局不完成阶段 B 或整局实玩要求。
