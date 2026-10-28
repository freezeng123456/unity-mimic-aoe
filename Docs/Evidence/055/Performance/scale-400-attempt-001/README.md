# scale-400-attempt-001

原生规模诊断，版本0.2.31，match `04bb279d7ae340fc9808dfceae57b739`。总体 `failed`；F8有效 `182.717414713` 秒，mean/P95/P99 `20.629719/33.581421/34.089245` ms。

见 [完整规模审计](scale-summary.json)、[逐帧及事件审计](f8-audit.json) 和 [性能说明](../../../../Performance-055.md)。sources内保留每个原始文件/归档的确切路径及SHA。真实GUI F8，自动保存/F9完成后Return To Menu和QUIT；menu_exit不是自然终局。

首次窗口因失焦只有182.717415有效秒，300秒要求失败；所有原始证据保留，未覆盖或拼接。后续attempt-002另行归档。

## 压缩归档

为使单文件适合仓库发布，此事件流现以[无损 gzip](20261003-033507-145-04bb279d7ae340fc9808dfceae57b739-menu_exit-events.jsonl.gz)保存；历史 `f8-audit.json`、`scale-summary.json`、原始身份、统计和 SHA 均未重写。[压缩 receipt](compressed-events-receipt.json)记录完整旧路径→新路径映射、原件保留位置、重审命令及哈希。

- 原始归档文件：`20261003-033507-145-04bb279d7ae340fc9808dfceae57b739-menu_exit-events.jsonl`，107590188 bytes，SHA256 `eed9039358a62733ceb209b140795e9ba50afaac884779623d509f1237c070cb`。原始历史 audit 的 `sources.events.archivedPath` 继续表示这一原始字节流；该冗余未跟踪 `.jsonl` 已从此目录移除。
- 压缩文件：`20261003-033507-145-04bb279d7ae340fc9808dfceae57b739-menu_exit-events.jsonl.gz`，6593574 bytes，SHA256 `913454186c3bcb06ee52377fd3f51a04a654c3aed7d606587bf1a1107274412c`。gzip level 9、mtime 0、无内嵌文件名；从保留原件再次压缩得到完全相同字节。
- 完整原件另存忽略目录：`Builds/Scale055/PublicationRaw/scale-400-attempt-001/20261003-033507-145-04bb279d7ae340fc9808dfceae57b739-menu_exit-events.jsonl`。逐块解压比较、EOF/CRC及解压 SHA 全部通过，解压结果与历史 raw SHA 精确相等。该本机副本不随Git发布，仓库 gzip 本身足以还原原始事件。
- 现有 `verify_performance.py` 使用 `.jsonl.gz`、原配对报告及 `--min-unpaused-seconds 300` 在忽略的 Builds 目录独立重审，结果仍为 **failed**、有效 **182.717414713 秒**。除生成时间及源文件路径/压缩哈希外，所有结果字段（身份、checks、requirements、computed、frameAudit、memoryAudit等）与原审计精确一致。历史审计文件字节保持原样，未再生成规模摘要。

在本目录核对解压 SHA：

```sh
gzip -dc '20261003-033507-145-04bb279d7ae340fc9808dfceae57b739-menu_exit-events.jsonl.gz' | shasum -a 256
```

重审时可以显式传入 `--events` 指向此 `.jsonl.gz`；原验证器也会在相邻 `.jsonl` 不存在时自动采用 `.jsonl.gz`。
