# B6 正常玩家海岸对局：中断，未取得自然结算

原生客户端 0.2.31／GUID `128a731d440e4b7a94abfcc74b88fa97`，对局 `d57aee12243c4599a9343986d5b2c1ab`，Coast Compact 镜像、Easy、时代上限 III、seed 314159、Military 25/25。独立菜单已保存并读图，不沿用 B5 的配置截图；菜单内旧 HUD 对局 ID 仍残留 B5，但实时／请求对局为空，不能将菜单归属误判成 B5。

[summary.json](summary.json) 状态为 **interrupted_no_natural_end_report**。旧运行进程在环境恢复后已不在，新客户端打开在主菜单，这是主代理的观察；现有档案无法独立证明旧进程退出原因。没有据此推断游戏崩溃、自然败局、投降或玩家全部被消灭。

[report-presence-audit.json](report-presence-audit.json) 记录实际检查 AppData Reports 的时间与精确对局文件匹配规则。当时该对局没有任何 `match_finished`、`menu_exit`、`quit` 报告或事件日志。没有完整报告就不能执行完整玩家支付／出生／自然结算审核；本目录没有补造报告、MatchEnded、获胜方或门槛覆盖结果。本局不能计作阶段 B 的完整原生对局通过。

保存的四组原生 F9 JSON/PNG 为：

- 独立菜单 `Frontier-20261002-142413-245-f279266-9935406f91514ae69b7437f8f6f7ae18`，画面显示 Coast 镜像、III、Easy、Military 25/25。
- 开局 `Frontier-20261002-142415-059-f279368-a5c02e27f19e48658eb6300d858ce9da`，世界时间 0.4 秒、paused=true。
- 进展 `Frontier-20261002-150044-650-f409972-ee274b5750344c00a865070b56151365`，世界时间 545.813782 秒。
- 最后同局 F9 `Frontier-20261002-150513-945-f426005-be3d02d5e7d84282aae1ff3c866d273e`，世界时间 664.884705 秒。

最后的已保存画面显示 **AGE 2** 和 `Age advancement queued`，城镇中心排队升级 Age 3／110s。排队点击及画面不能代替实际完成时代 III 的事件。没有更晚的同局结束 F9。[capture-audit.json](capture-audit.json) 保存全部 PNG SHA、尺寸、版本/GUID、同局请求／实时／HUD 配对校验；全部检查通过，但它只证明这些局部截图的身份与字节一致。

旧 `Builds/NativeInput055/Player.log` 片段从独立 B6 菜单 F9 起冻结，原日志第 2766–3841 行、104,023 字节，见 `native-input-b6-interrupted-excerpt.txt`。末尾明确为 `manual_pause world=705.374800`，随后 `focused=False frame=434302`，没有同局自然结算导出行。[input-trace-audit.json](input-trace-audit.json) 保存读取时整日志 SHA 和已归档片段 SHA；原日志后来可能被替换，不能把其现有路径描述成永久不变的完整记录。

[B6 F8 初步帧审计](../../Performance/normal-b6-window-001/frame-only-audit.json) 保留原始 600 秒窗口：世界 331.411011→590.202942、**258.709919 秒合格活动**，不足单窗口 300 秒；P95 17.319040 ms、P99 17.589251 ms。原始帧统计可独立重算，`allFramesFocused=false`，不合格失焦／暂停区间已排除。没有最终 Spawn/Death 账本，仍不能完成关联报告及真实实体计数审核。该窗口与本局完整验收均保持未完成。

[archive-receipt.json](archive-receipt.json) 记录所有原生文件、规则／卡组／解析器快照的来源路径、字节数、SHA 和复制一致性。没有操作 GUI、启动 Unity、修改源码或六份当前总览文档，也没有提交或推送。
