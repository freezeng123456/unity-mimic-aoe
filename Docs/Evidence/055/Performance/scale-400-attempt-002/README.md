# scale-400-attempt-002

原生规模诊断，版本0.2.31，match `e61920e74b1f467c97052ffb138fcdc6`。总体 `passed`；F8有效 `317.674076202` 秒，mean/P95/P99 `24.152214/33.990250/40.640369` ms。

见 [完整规模审计](scale-summary.json)、[逐帧及事件审计](f8-audit.json) 和 [性能说明](../../../../Performance-055.md)。sources内保留每个原始文件/归档的确切路径及SHA。真实GUI F8，自动保存/F9完成后Return To Menu和QUIT；menu_exit不是自然终局。

明确要求单窗口至少300有效秒，正式审计通过；13,153帧全部有效且有焦点。定期公开GUI左键点击HUD静态FOOD文字维持前台；未修改采样器或游戏命令。

## 压缩归档

为使单文件适合仓库发布，此事件流现以[无损 gzip](20261003-034255-660-e61920e74b1f467c97052ffb138fcdc6-menu_exit-events.jsonl.gz)保存；历史 `f8-audit.json`、`scale-summary.json`、原始身份、统计和 SHA 均未重写。[压缩 receipt](compressed-events-receipt.json)记录完整旧路径→新路径映射、原件保留位置、重审命令及哈希。

- 原始归档文件：`20261003-034255-660-e61920e74b1f467c97052ffb138fcdc6-menu_exit-events.jsonl`，108238315 bytes，SHA256 `5df984b4d846751962c7e270bc6077be8e06c34537592a7b601b7534614f71e5`。原始历史 audit 的 `sources.events.archivedPath` 继续表示这一原始字节流；该冗余未跟踪 `.jsonl` 已从此目录移除。
- 压缩文件：`20261003-034255-660-e61920e74b1f467c97052ffb138fcdc6-menu_exit-events.jsonl.gz`，6622088 bytes，SHA256 `d5c923ee3bc297a4079df0557ea81242c50d308376674c47ebf84bc6674b954f`。gzip level 9、mtime 0、无内嵌文件名；从保留原件再次压缩得到完全相同字节。
- 完整原件另存忽略目录：`Builds/Scale055/PublicationRaw/scale-400-attempt-002/20261003-034255-660-e61920e74b1f467c97052ffb138fcdc6-menu_exit-events.jsonl`。逐块解压比较、EOF/CRC及解压 SHA 全部通过，解压结果与历史 raw SHA 精确相等。该本机副本不随Git发布，仓库 gzip 本身足以还原原始事件。
- 现有 `verify_performance.py` 使用 `.jsonl.gz`、原配对报告及 `--min-unpaused-seconds 300` 在忽略的 Builds 目录独立重审，结果仍为 **passed**、有效 **317.674076202 秒**。除生成时间及源文件路径/压缩哈希外，所有结果字段（身份、checks、requirements、computed、frameAudit、memoryAudit等）与原审计精确一致。历史审计文件字节保持原样，未再生成规模摘要。

在本目录核对解压 SHA：

```sh
gzip -dc '20261003-034255-660-e61920e74b1f467c97052ffb138fcdc6-menu_exit-events.jsonl.gz' | shasum -a 256
```

重审时可以显式传入 `--events` 指向此 `.jsonl.gz`；原验证器也会在相邻 `.jsonl` 不存在时自动采用 `.jsonl.gz`。
