# 0.14.0 私有交付 · 071

源码证据快照阶段记录。原有私有 Google Drive 项目下已创建 [071 目录](https://drive.google.com/drive/folders/1L3kcwBS4xHfoYiKEYfnt1DpZrNylEzIZ)，目录父节点、shared=false、仅 owner 权限已重新读回，未扩大共享范围。

八局正常开局的 [完整原始补充 ZIP](https://drive.google.com/file/d/1L06QagWEyRj5fouCvHHPufaRbV261_2z/view?usp=drivesdk)及 [成员清单](https://drive.google.com/file/d/1EE8LNE8mbT68mjp2Vv0xugj2nrF3gOiu/view?usp=drivesdk)已上传，远端名称、精确字节、父目录及仅 owner 权限已读回。ZIP 为 78,983,060 字节，SHA256 `5a5e34e21bb923e229b9819d71c8e88f47efecbd02c5cc3c1b91dfb3f2e65fda`，56 成员逐项本地核验。未下载远端重新计算 SHA。

源码、macOS arm64 客户端和实际原生图集将按本次明确的源码快照提交打包，再追加发行包哈希、上传链接与读回回执。交付回执提交不改变运行输入，也避免将包自身哈希循环写入其源码 ZIP。此源码快照中的本页只记录当时已完成的上传，三个主包的最终结果以分支后续本页及 Delivery 证据为准。

运行提交 `3aeb0cb4b5f63ecd18553254583726b88cef9079`；704 输入指纹 `098e5c97eb8e1a3f5cff1e0dd7db30a7fe761fc8f480c6b8517811fab5d8795a`；构建 GUID `2d39f78f9f234ef092e172a35f800cae`。正式 1521/1521 回归通过；英国受控 IV 原生付费生命周期、两冷恢复、三完整前向回放及终局 HUD 共 68 项检查通过。正常路线只有 1/8 完整，原生 Mono/MemoryLeaks 诊断、排版和长期性能限制继续保留。[完整验证](Verification-071.md)、[操作](Player-Guide-071.md)。

既有开发分支与 [草稿 PR1](https://github.com/freezeng123456/unity-mimic-aoe/pull/1)作为审查入口；不合并 main，不公开发行或部署，不修改共享范围。
