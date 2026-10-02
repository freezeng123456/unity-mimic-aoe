# B9 输入修饰键记录：后续按键的 Shift 标志已恢复

本目录只保存 B9 进行期间的己方输入、选择、UI 上下文和暂停诊断。对局关联由 native-playtest 操作者声明为 `7f161abd4f994a1aaf5be762ba4a23f5`、客户端 `0.2.31`、GUID `128a731d440e4b7a94abfcc74b88fa97`；单条输入行不含 matchId 或 GUID，本摘录不独立认证这些身份。

## 已确认的日志变化

- 原日志第 1467–1612 行内，31 条已记录输入边沿均为 `shift=True、save=False`。这只描述已记录事件，不能证明整个区间每一时刻的按键状态。
- 第 1469 行 `t=18537.4063` 点击 `(1545,502)` 为 `shift=True`；第 1472 行选择结果为 12 项，第 1484 行 UI 上下文为 `entity=1、count=12`。
- 第 1504–1505 行 `t=18537.9836→18537.9865` 拖动 `(1391,626)→(1464,525)`，两边沿均为 `shift=True`；第 1507 行为 `drag=True、selected=12`。这些是当前己方选择记录，不是敌方实体状态。
- 第 1612 行 `t=18635.0305` Escape 仍为 `shift=True、save=False`；第 1613 行随后明确记录 `paused=True、reason=manual_pause`。
- 第 1614 行 J（`t=18737.0691`）、第 1615 行 T（`t=18757.9631`）均为 **`shift=False、save=False`**。二者之前没有新的恢复记录；按键边沿进入日志不等同于暂停中的世界命令成功执行。

操作者报告在这段时间通过公开 Sky `key: "shift+j"` 完整组合进行了修饰键复位。该操作来自操作者的工具使用记录；原生日志独立支持的是后续 J/T 标志变成 False。日志没有独立记录 Shift 释放边沿，不能确定精确释放时间，也不能确定之前是谁或为何持有 Shift，不归因于人类。

冻结源码的 `DiscreteInputBuffer` 把 Shift 状态写入输入边沿；`RtsInput` 将 `edge.shift` 传给单击和框选，True 时保留或切换既有选择，False 时先清空。上述记录与追加选择机制一致。[源码摘录](source-modifier-excerpts.txt)的三个文件 SHA 均与 055 冻结来源相同。输入边沿先记录，暂停时世界输入仍可能被过滤，因此不把 J/T 日志认定为 Hold 或定位命令已生效。

## 来源与边界

读取 UTC：`2026-10-02T21:40:01.670901+00:00`。源路径 `Builds/NativeInput055-B8/Player.log`；读时 **160,610 字节**，SHA-256 `b952a6c72e88222af05294743b9597297eb4beeccebdb3890533c7cedced41aa`。完整本机快照为 `Builds/InputModifiers055/b9-readtime-20261002T214001670901Z.log`。读取前后源尺寸与修改时间相同，复制 SHA 一致；原路径仍可追加，这个 SHA 只描述当次快照。

[原始摘录](native-input-modifiers-excerpt.txt)从原日志第 1467–1615 行按明确前缀筛出 72 行／5,602 字节，保留原始行字节和顺序，省去 Unity 调用栈。[审核 JSON](input-modifier-audit.json)逐行记录原始行号、字节数与 SHA，并保存前后 flags、读时快照身份及源码 SHA。[归档清单](archive-receipt.json)列出目录内其它文件的物理 SHA。

状态为 `input_flags_observed_recovered_gameplay_effect_unverified`。本次没有读取实时事件账本或隐藏敌方状态，没有操作 GUI、修改游戏源码，也没有把工具错误当作原生日志证据。后续世界命令、采集和胜负效果仍以实际画面及结束后的严格归档为准，不标全面 Passed 或 B/D 完成。

既有 [InputTiming](../InputTiming/README.md)记录的是恢复后的 0.22 秒输入屏障；本修饰键诊断是独立观察，没有修改或替代那份结论。
