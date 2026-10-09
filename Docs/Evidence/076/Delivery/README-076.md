# Frontier Reckoning 0.19.0 — 下载与资料说明

这是原始本地交付的无损传输层，不是新构建。source.zip、macOS.zip、normal.zip 可直接解压使用；其余原清单和 README.md 保持原始字节。

## 重建完整证据 ZIP

必须下载 evidence.zip.part01 与 evidence.zip.part02，并放在同一目录。两个文件是顺序二进制片段，不是独立 ZIP；不要分别解压，也不要交换顺序。

```sh
cat evidence.zip.part01 evidence.zip.part02 > evidence.zip
shasum -a 256 evidence.zip
```

预期完整文件大小：122090550 字节。预期 SHA-256：

`101a05650de14ccb5588bd577ae130318d5ee544537767c3880b4deb2428bb7a`

核对后可解压 evidence.zip；在十二项全部下载且证据重建完成后，运行 `shasum -a 256 -c checksums.sha256` 核对原来的八项校验。checksums.sha256 是原件，不含新分片、此说明或 transport manifest；新传输文件的 SHA 见 transport-manifest076.json。重建前请先确认同目录没有需要保留的 evidence.zip，避免 shell 重定向覆盖同名文件。

## 如实保留的交付状态

原 Delivery1 打包 exit2：evidence.zip 大于严格的100MiB单件门槛。原独立审计 status FAIL，唯一错误是 `Actual artifact exceeds declared 100MiB delivery limit`。四个 ZIP 的全成员 CRC、EOF、SHA、Unix模式和原始输入对照均已完成；该体积失败没有被删除、重命名为通过或放宽阈值。原完整 evidence.zip 留在本地，不重压、不裁剪。

当前传输仅复制八个原件并按64MiB顺序拆分 evidence.zip，使每个传输文件均小于100MiB。独立 transport 审计须另行执行；本说明和 prepare receipt 本身不代表独立审计已通过，也不代表云端回读完成。

## 资料边界

源代码快照 `f0f829bc2ae5550fb13f3c1ebab403652c921065`；运行时代码 `b1a9b6a977a60b63cd3e9f9632179c1787f3d00d`；774项输入；版本0.19.0；构建GUID `a86c515f3ab040649ea505e507193ff3`。完整回归为1795项；原失败尝试、普通观察的封顶未胜局、原生首局失败和未执行计划均在原始证据中保留。

此传输层证明可无损重建原始字节，不增加整体目标完成、原生人类胜局、FPS性能验收、医护跨河/上下船生命周期、双物理Mac或075存档迁移结论。有关实际玩法、冷恢复和回放的结论，以原始各有界审计为准。没有合并主分支、公开发布、上传或变更云端共享权限。
