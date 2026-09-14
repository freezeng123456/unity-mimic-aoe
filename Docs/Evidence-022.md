# 0.2.2 证据入口

用户交付目录的 Verification-0.2.2 与源码包/客户端包分别保存。源码中的本文件是索引，不将文字摘要当作运行证据。

- EditMode.xml / EditMode.log：64/64规则测试。
- PlayMode.xml / PlayMode.log：11/11场景生命周期测试。
- Build.log：macOS arm64最终原生构建结果172697962 bytes。
- source-hashes.json：构建完成后的Assets/Packages/ProjectSettings文件哈希。
- StrategyMatrix/：六组当前源码的标准资源/迷雾AI对局原始result、30秒快照和事件。
- strategy-summary.json：上述六组结果及最终状态的机器可读索引。
- build-receipt.json：客户端版本、实际BuildGUID、构建与验收状态。
- native-river-coast.json / native-river-coast-events.jsonl / NativeDuel.log：最终原生自动局原始记录。
- SourceValidation.txt / package-verification.json：版本、依赖、meta、场景、私有路径、ZIP完整性检查。

0.2.1的全新导入54/54+8/8属于旧版本。本次0.2.2在当前工程完成64/64+11/11与原生构建，未重新执行清缓存导入，不引用旧结果作为新版本导入证据。没有本轮原生手动完整对局、听感/画面验收或50/100/200/400长期性能结果。
