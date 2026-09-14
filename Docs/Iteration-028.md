# 0.2.8 输入转换迭代

2026-09-14。本轮完成 0.2.8 候选的专项输入回归与独立客户端输入检查，仍不代表完整陆战版本或阶段 D 完成。

六项模态诊断的冻结源码哈希为 `d36963985d5b8c4d3d9331fbd4cffd05a902d69543ec7c4d4299ed55e0e86f7e`。早期 `Modal-before.xml` 与 `Modal-red.xml` 是不同错误夹具；修正后的冻结基线 `Modal-red-corrected.xml` 为 2/6，指针门控为 `Modal-pointer-gate.xml` 4/6，最终 `Modal-green.xml` 6/6。新增恢复测试的过早断言在中间全量运行中为 82/83；将“真实恢复按钮后新的按键”用例分两次事件更新后，`Modal-expanded` 为 8/8；另保留同更新周期版本，允许关闭面板后的 Canvas render pass，再验证FIFO未丢失。最终专项用例共9项。最终全量结果为 PlayMode 84/84（19.445 秒，UTC 05:33:45）和 EditMode 94/94（6.071 秒，UTC 05:34:21）。

0.2.8 macOS arm64 Development Mono 客户端已构建：GUID `c88b59e957214754b25e1aac1bc1a167`，172866567 字节，123 项运行时输入哈希。143.6996 秒原生输入检查完成菜单、开局、模态、快捷键、重开、暂停/恢复、返回菜单和退出；详见 [Native-Verification-028.md](Native-Verification-028.md)。

本轮没有新的 0.2.8 完整玩家对局、性能矩阵或声音验收。0.2.7 正常玩家 II→III 支付未完成败局见 [Normal-Player-028.md](Normal-Player-028.md)，不构成完整核心 III/炮兵攻城门槛通过。
