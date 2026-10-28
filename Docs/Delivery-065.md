# 0.8.0 冻结交付目标与包外回执入口

私有目标目录：[Frontier Reckoning 0.8.0 · Google Drive](https://drive.google.com/drive/folders/1ZMIFy9vwEc41jWC9zBCxHQoylmYQi5m0)。目录 ID `1ZMIFy9vwEc41jWC9zBCxHQoylmYQi5m0`，父目录 ID `1Dgm7Nx9Qwdaz-vhClbRekJ134tEOW4GH`；创建后读回为仅 owner、`shared=false`。本页记录源码冻结时的交付目标和核验入口，**不是云端文件已上传、可下载或验收通过的实时证明**。

冻结身份为958/958完整自动化（Edit618/Play340）、运行提交 `c4365e31dbea9dd7e5d5c253b661ee2d3a25879a`、531个输入、指纹 `d7bbe1e4f0e52210bf64b4787864449a438be87fb14952173cdd4d60d0563eb6`；macOS arm64 Development / Mono，GUID `5fbcf668506b4814bc34d27b3902a237`。正常长局和受控两乘员真正冷恢复/继续操作已独立核验。最终分包、上传和读回完成后，以同目录的**包外** `release-manifest.json`、`package-contents-verification.json`、`drive-upload-verification.json` 为实际状态准，匹配此运行身份与真实文件ID/大小/父目录/私有权限。

| 冻结交付目标 | 用途 | 实际结果入口 |
| --- | --- | --- |
| Frontier-Reckoning-0.8.0-macOS-arm64.zip | 完整 macOS Apple silicon App、中文说明、许可证与构建身份快照 | 包外release-manifest与内容核验；不以包内快照推断云状态 |
| Frontier-Reckoning-0.8.0-source.zip | 冻结源码、工具、065 自动化/原生证据；排除缓存与历史全量raw，保留明确标识的小历史审计 | 原始输入、归档成员SHA、冻结文档提交与实际ZIP SHA |
| Frontier-Reckoning-0.8.0-gallery.zip | 最终新GUID的选定原生F9 PNG/JSON、索引和受控来源说明 | 配对图像/metadata SHA、GUID及索引；不是所有机制穷举 |
| release-manifest.json | 包外最终运行/文档提交、指纹、GUID、三ZIP大小和SHA-256 | 分包后生成的真实记录 |
| package-contents-verification.json | 包外实际ZIP CRC、条目、解压字节、文件模式、链接和来源核验 | 实际分包后的核验结果 |
| drive-upload-verification.json | 包外上传后读回的真实文件ID、大小、父目录及私有权限 | 最终上传/读回结果与相关云链接 |

源码ZIP在上传前冻结；云端回执生成后不回写或重压该ZIP。本页不预填ZIP哈希或文件ID，也不把包含自身的ZIP哈希写入包内自身文档。包内客户端manifest仅是构建身份快照，包外release-manifest记录最终包哈希。实际已上传范围和权限需要读回，文件夹存在或工具exit0不能代替这一验证。

当前实现和验收边界见 [Completion-Status](Completion-Status.md)，操作见 [Playtest-065](Playtest-065.md)，项目规则见 [Naval-Content-065](Naval-Content-065.md)。新保存完整事件，schema2 gzip-base64固定64 MiB文件/256 MiB原文上限，旧同版本同规则schema1可读；这不是0.7跨版本迁移。129,656事件长局的原文85,654,935字节/存储8,462,877字节真正冷载重存全文件一致，并继续至2222.317秒、140,982事件再存；主动quit和16×模拟不称自然终局或FPS。受控两乘员/编组冷恢复、登陆/召回另有独立归档。

最终资料从输出 `FinalCandidate` 的冻结内容取文件。[正常证据](Evidence/065/Native/Normal/README.md)、[受控乘员](Evidence/065/Native/Fleet/README.md)、[受限场景检查](Evidence/065/Native/Scenes/README.md)、[展示来源](Evidence/065/Previews/README.md)与[只读复核工具](Evidence/065/VerificationTools/README.md)分别保留范围。949/952项和ac217历史失败不混称新GUID结果；ac217的1706秒保存失败、辅助文件仍为842秒旧槽，完整raw保留外部，小审计在Historical-ac217。最终图集只收经核对的新GUID配对F9。

已交付 0.7 文件仍在 [Delivery-064](Delivery-064.md) 的历史目录；0.8 不覆盖或改名旧文件。目标仍是 macOS arm64 Development / Mono 开发试玩，Release、跨机器签名/公证、其他平台与长期表现继续受 [Known-Issues](Known-Issues.md) 所列范围限制。
