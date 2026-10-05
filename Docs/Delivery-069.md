# 0.12 私有交付

当前运行提交 `28cd1b3191fafd49417d544e60dc898986568aa8`；源指纹 `4cbc889a5ba3d706768a2b568af42dc4ec8b4cf0d60e7b45a4cc706d9ddab3ad`、664输入、GUID `4546189bdd6b40fa92789fe697f7f0a7`，1,286项完整测试及受控原生范围见[069验证](Verification-069.md)。

交付包含源码、macOS arm64 Development/Mono客户端、当前构建原生图集及可导入曲河任务。源码只含允许的工程目录和069证据，排除Unity缓存、个人Saves/Account/Preferences及其他旧批次证据；旧版本文档保持历史身份。客户端提供示例场景和同GUID录像，录像从实际检查点第122步至901步，未签名公证；图集只含当前构建原件与说明，旧候选诊断不进入图集。

三包已冻结并独立逐项通过；源码快照 `ceae20928462716178bb740c914106730f890992` 的全部允许文件、664运行输入及Git blobs/模式一致。客户端完整319成员应用树和6项附加内容、66项图集批准清单与ZIP完全一致；合计1,475成员的CRC/SHA、权限/类型、路径和前后不变性通过。

| 包 | 字节 | SHA256 |
|---|---:|---|
| [源码](https://drive.google.com/file/d/1UIgCHMe9Jl3tsjv50bmHTrzJVVmZpjqn/view?usp=drivesdk) | 42,632,853 | c4173ef26ce0c6ff66377d105a276fa680be07c63c7dfa7ed9446951e693cd81 |
| [Mac客户端](https://drive.google.com/file/d/1JhM2MTkiu5P3XjOEAbqlisromEj6CbSP/view?usp=drivesdk) | 76,074,373 | 00540dd11de0bf75a96a1848a15a349ead5d2317a5dc47d89f9cb387b7d4b667 |
| [当前原生图集](https://drive.google.com/file/d/1zOJAQe4tZ7SrGj5L7xqa120chGgna_kc/view?usp=drivesdk) | 14,306,755 | 6162eae70f133fd06e0ef0ba2ae0f374f0af015f11228aed53ff937c3b98a097 |

已上传[原私有Google Drive项目的新0.12目录](https://drive.google.com/drive/folders/14fBcWdSSX5L1FAul8h0IGlH1xpY_Bw55)，父目录仍是 `1Dgm7Nx9Qwdaz-vhClbRekJ134tEOW4GH`。三个包、README、完整发行清单、独立ZIP收据与可直接导入的[曲河任务](https://drive.google.com/file/d/1zho2VvQhd-p-dnXS6_kzhYZ98E_kJSSy/view?usp=drivesdk)共七对象，完成后逐个读回名称/字节/父目录/权限，均仅owner用户权限；本地上传前后SHA相同。没有远程下载重算内容SHA，不能把元数据读回当远程字节证明。

`release-manifest.json` 505,512字节，SHA256 `5f1361b175aa1e9416985900a7b223cb1ddc91f8c9e43af14533fdc39adf4009`；独立最终ZIP收据4,604字节，SHA256 `f0d9e4f21352c9711f898c7b8d9fd222dfa538265257a70db6679d94ce5933b2`。原校验器曾误拒源码内已批准的 `Docs/Evidence/069/Diagnostics/Candidate1` 诊断子树；保留失败收据和原脚本，新v2仅放行该精确源码子树，客户端/图集与其他旧候选仍禁止，57项自检及正式完整核验通过。三个ZIP字节没有为此修改。

最终交付收据另在[069 Delivery证据](Evidence/069/Delivery/README.md)归档。它们和本页云端状态更新在源码包冻结之后写入Git，未重新写入源码ZIP；下载包保持原快照，运行输入及原生应用保持同一身份。

保持私有开发分支 `codex/land-rts-completion` 和[既有draft PR1](https://github.com/freezeng123456/unity-mimic-aoe/pull/1)，不合并main、不公开发布或扩大云盘共享。两台物理Mac、自定义合作自然整局及长窗性能仍未验收。
