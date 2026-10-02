# 原生菜单输入与窗口恢复记录

本目录归档一次已经完成的窗口恢复观察。仍是原来的 macOS 原生客户端 0.2.31，构建 GUID `128a731d440e4b7a94abfcc74b88fa97`；没有重建客户端或修改游戏源码。`receipt.json` 保存版本、构建与运行时文件 SHA-256、当次启动参数、原始日志位置及摘录来源行号。

主代理通过 CLI 启动同一 `.app`，使用 `-screen-fullscreen 0 -screen-width 2560 -screen-height 1440 -frontierInputTrace -logFile Builds/NativeInput055/Player.log`。首次屏幕坐标 `(1950, 460)` 点击返回 `noWindowsAvailable`，当时没有确认到对应的原生输入日志。随后通过已暴露的 AX `Window → Fullscreen` 进入全屏，重新读取应用状态，再用同一菜单退出全屏并重新读取状态。恢复后的第一次相同坐标点击只产生悬停效果；第二次才确认原生输入和菜单动作。

摘录保留原日志第 41–53 行，包含 Unity 实时时间 `122.3563` 秒的鼠标按下、`122.3743` 秒的抬起，以及命中 `AI: River League` 的 UI raycast、down、up、action 和 click，世界时间为 0。主代理的实际界面观察显示 AI 从 River League 切换到 Coast Compact。日志独立证明输入传入了客户端并触发菜单动作；它本身不能证明此前没有日志的工具点击、窗口切换动作或最终显示文字，这些由完成后的操作记录补充。

`native-menu-input-excerpt.txt` 按原始行字节复制，SHA-256 为 `473f3b4ff9633c4d7f88dec8d0ac5342a72a310574c1c67a159360e3e8ba0779`。来源日志仍可能继续追加，因此 receipt 中整份日志的字节数和 SHA 仅表示归档时的一次读取；另保存第 53 行为止的固定前缀 SHA，不能把动态日志说成永久不变的完整归档。

这只是一次成功恢复后的菜单输入证据，不能推出所有焦点或全屏问题已经永久修复，也没有据此认定游戏源码存在具体错误。坐标依赖当时的窗口几何，不应写入游戏源码。此次仅归档已完成的菜单操作，没有操作 GUI、启动其他实例或读取活局的隐藏敌方状态。
