# 037 双击选择修复只读审查

审查对象：`work/verification/037/before/RtsInput.cs`、当前 `RtsInput.cs`、契约 `double-click-contract.md` 与 `DoubleClickSelectionTests.cs`。未运行 Unity、未修改 Assets。

当前改动与契约相符：双击候选同时检查 `edge.time-lastClickTime >= 0`、小于 `.3s`、同一友方 definition，以及 release 点距离平方不超过 `64`（8 input pixels）。空地、远距离同类单位、右键、框选、minimap、键盘、UI pointer、目标模式、device reset、pause/focus/modal 等路径均通过 `ResetDoubleClick` 清候选；单击后再写入候选时只接受 `owner==0` 的 picked entity。`ClickSlopSquared` 同时替代原拖拽阈值 64，未改变数值。

`DoubleClickSelectionTests.cs` 的 7 项覆盖了：近点同类 select-all、远点同类保持单选、右键后隔离、空地隔离、超过 0.3 秒、拖框隔离、真实 UGUI STOP 手势隔离。测试使用 queued `InputSystem.QueueStateEvent`，通过 `Ready()` 走实际 FIFO/UGUI 路径，且检查 WorldPoint、订单和 Command 事件；不是直接调用 `onClick.Invoke`。旧运行时 3/7 通过、新运行时应由 root 的 green 运行提供 7 项结果；当前审查不把尚未完成的运行提升为已验证。

未发现暂停、重开、建造、Shift 追加、Ctrl/Command 编队或既有攻击/采集规则被直接破坏：`SuppressWorldInput`、`ResetSelection`、`BeginBuild`、`CancelTargetMode` 和 device reset 都清候选，普通 `IssueOrder` 也在下令前清候选。测试的右键隔离覆盖了实际 Gather command，UI 测试覆盖了真实 STOP command。

## 仍需记录的边界

1. 修复将候选位置存为 `Vector2`，并只比较连续完成左键的 release 位置；这符合契约的 input-pixel 定义，但没有硬件鼠标/不同 Retina 缩放下的原生验证。
2. 测试没有独立覆盖 `Shift` 双击追加结果、暂停/焦点中断后的候选清除、目标模式转换后立即单击、设备 reset 后立即单击、或三连击的第三次语义。代码路径静态上已清候选或按当前候选重新判断；这些是未覆盖项，不是已发现缺陷。
3. 当前双击成功后保留最后一次类型/时间/位置作为候选，因此第三次在 0.3 秒且 8 像素内仍可能再次触发同类 select-all。若产品要求严格“两次后候选失效”，应增加专门契约和测试；当前契约只规定“两次连续点击”，没有把三连击行为定义为阻塞问题。

结论：没有高置信阻塞缺陷。最终判断仍依赖 root 完成 green XML/运行结果；本审查不证明真实硬件修饰键、系统焦点恢复或完整玩家攻城。
