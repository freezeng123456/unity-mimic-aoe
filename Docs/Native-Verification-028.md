# 0.2.8 原生客户端输入验证

客户端：macOS Apple silicon Development Mono，Build GUID `c88b59e957214754b25e1aac1bc1a167`，172866567 字节，123 项运行时输入哈希。证据位于 `Verification-0.2.8`：`native-input.json`、`native-input-events.jsonl`、`NativeInput.log`、`native-restart-quit.json` 和 `input-*.jpeg`。

第一局 `reason=menu_exit` 的 143.6996 秒原生检查覆盖菜单到开局、模态与快捷键、暂停/恢复、采集训练和返回菜单。随后又从菜单开新局、调用旧编队确认清理、按 T 确认新快捷键可用，再通过真实 RESTART 按钮重开。最后的 `reason=quit` 报告是重开后 6.4 秒的另一局；退出码来自客户端进程。实测时序为 ESC→T 33.9 ms、ESC→V 50.5 ms、庄园鼠标抬起→G 26.4 ms、X 抬起→T 1 ms、Resume 抬起→T 25.1 ms。模态打开时 T 被忽略；GROUPS 中 SAVE 0 后立即 V 成功，随后数字 0 正确召回城镇中心；浆果 Gather 产生 27.60394 食物实际入账；村民训练支付 100 食物并使人口变为 7。重开恢复 300/300/100、6 人口并清空选择；退出最终 `exitCode=0`。

截图 `input-escape-right-blocked.jpeg` 不是通过证据。该次 ESC 到右键实际间隔 564.9 ms，超过约 0.22 秒指针防穿透窗口，按当前项目契约产生 Move 属于预期；它必须与 PlayMode 指针屏障用例分开解释。

尚无新的 0.2.8 完整玩家对局、性能矩阵、人工声音试听、完整玩家 III、炮兵攻城、五时代或第二文明验收。测试和该原生局不构成完整陆战或阶段 C/D 通过。

原始日志中的按键时间来自 InputSystem 捕获，间隔用于确认触发落在保护窗口内，不能当作端到端输入延迟或人类连续操作流畅度的测量。原生测试无人工资源/单位注入，使用正常AI和1倍模拟速度。图像已由主任务逐步检查；没有监听音频。退出日志包含 Unity Development 的线程/分配器清理诊断，未据此宣称零泄漏或最终稳定性通过。
