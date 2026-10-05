# 069 证据索引

0.12候选2最终运行GUID `4546189bdd6b40fa92789fe697f7f0a7`；[完整验证范围](../../Verification-069.md)。

- [Tests](Tests/README.md)：实际1,286项正式完整测试、源清单、完整用例独立比较。
- [Native](Native/README.md)：37张实际UI、23对F9、四进程、源文件移走/冷恢复/冷草稿/录像和独立核验。
- Build-And-Logs：实际构建身份、前置源清单、Unity构建与四次原生日志。
- Diagnostics/Candidate1：旧候选测试、来源与真实两个GUI缺陷，严格排除当前构建验收。
- [Original-0.11-Replay](Original-0.11-Replay/README.md)：原0.11 GUID的冷回放续验，独立保留旧版本身份。

`archive-receipt-candidate2.json` 逐项记录139份新归档的原始绝对来源、归档相对路径、字节和SHA；载荷31,009,513字节，收据SHA256 `79fb07216e5eca1027ca42fe73b40414f363cb46a13da94c47adc4b9a0b0c072`。已存在的Original-0.11-Replay未重写。归档不是原始资料目录：Native/Captures/Main与Replay分别对应两个真实profile/Captures；复查工具应使用收据映射重建输入并另提供实际应用包，不能仅凭归档声称重新运行GUI。
