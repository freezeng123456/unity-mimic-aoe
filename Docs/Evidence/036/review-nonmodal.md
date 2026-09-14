# 036 独立静态审查记录

/root/siege_audit_032 只读审查普通 HUD 手势与生命周期边界；没有执行 Unity，也不是原生操作证据。

结论：callback 执行前检查 ModalVisible，可移除普通训练/取消/攻击移动对下一次独立世界点击的 220ms 屏蔽。UiPointerDispatcher 在释放上消费原 UI 手势，拖出释放不触发按钮或世界操作。模态内按钮保留既有屏蔽；BeginBuild、SetPaused/TogglePause、ESC 自身仍保留保护；StartMatch/ReturnToMenu 清理旧选择与输入。不得在 callback 之后才检查 ModalVisible，因为 callback 可能关闭或打开面板。

需要回归实际 down/up 流、模态关闭后的快速指针、暂停/重开与建造。键盘导航直接触发 UGUI submit 和系统焦点恢复不由此审查证明。
