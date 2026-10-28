# 076私有交付 · 0.19.0野战医疗 Candidate1

2026-10-10。Google Drive十二个传输文件已实际完整上传并逐件全字节读回；本地独立传输审计与云端字节／SHA／owner-only／父目录及完整子ID集合分别验证。原Delivery1的体积门槛失败如实保留，不被传输层的成功改写。游戏内容与剩余玩法门槛仍见[076验证](Verification-076.md)、[原生边界](Native-076.md)、[分层矩阵](Acceptance-Matrix-076.md)。

## 版本、快照与私有Git身份

- **运行时代码**：`b1a9b6a977a60b63cd3e9f9632179c1787f3d00d`。Focused4／Full1／Build1／NormalObserved1使用同一774项输入，FP `dabf34d26490cece872959764b8b9e515749584b180f3d7899f38d79b69a6f8f`；Full1实际1795/1795、Build1实际exit0。
- **封包源码快照**：`f0f829bc2ae5550fb13f3c1ebab403652c921065`，区别于运行时提交；此快照增加已验证文档与证据，不改变上述运行时输入。`source.zip`为该快照的实际Git archive流转换的可移植源码ZIP，1201个文件；它不是稍后交付文档提交的快照。
- **实际app**：0.19.0，GUID `a86c515f3ab040649ea505e507193ff3`；`macOS.zip`保持原Build1，解包清单298文件／191062651字节，与原始Build1逐文件SHA一致。ZIP大小另列下表，不混同解包总量。
- 既有私有分支 `codex/land-rts-completion` 的快照 `f0f829bc2ae5550fb13f3c1ebab403652c921065` 已推送；[既有draft PR1](https://github.com/freezeng123456/unity-mimic-aoe/pull/1)已实际更新并读回为private／open／draft、该head与原main。实际标题为 **Expand Frontier Reckoning with paid medical support and army recovery**，正文精确读回通过；main仍为 `0bb044adbe40d0cab952e8c3b44e40eca8a6d3fe`，无合并、Release或部署。[快照PR读回原件](Evidence/076/Delivery/github076-snapshot-draft-readback.json)。
- 后补交付文档提交 `1f74195c8cf5647ae9eaa4c2e757564dac6eabd3` 已实际非强制推送，并由独立GET再次核验private／open／draft、精确标题正文／HEAD及main不变，见[文档交付HEAD读回](Evidence/076/Delivery/github076-delivery-readback.json)。后续仅归档此receipt与本文的提交不改变运行时或封包，其最新HEAD另用包外只读receipt核验，不递归把本文自己的提交号写入自身。本页与后补receipt均不声称已在冻结ZIP或云盘十二文件之内。

## 原始封包失败与独立传输审计：两个结论分别保留

原始 `Candidate1/Delivery1` 保留九个完整原件。实际打包 **exit2**，manifest状态 `COMPLETE_BUT_SIZE_LIMIT_EXCEEDED`：只有完整 `evidence.zip` 的 **122090550字节**超过严格的100MiB单件门槛。原独立审计状态 **FAIL**，唯一错误为 `Actual artifact exceeds declared 100MiB delivery limit`；该错误发生在四个ZIP的全成员检查完成之后。source1201／macOS319／evidence337／normal33个成员均实际完成CRC、EOF、SHA、Unix模式与原始输入比对，未删除首轮失败或裁剪证据。完整ZIP原件留在本地，不以分片上传冒称完整ZIP单件小于门槛。[原失败审计](Evidence/076/Delivery/package-audit.json)、[说明](Evidence/076/Delivery/package-audit.md)。

新建的独立 `DeliveryTransport1` 只逐字节复制另外八个原文件，按64MiB顺序把同一完整evidence ZIP拆成两个二进制片段，再添加 `README-076.md` 与transport manifest，共十二文件。未重新压缩、重建游戏、修改原manifest／checksum或放宽门槛。两片本身**不是独立ZIP**。

`TransportAudit1/audit.json` 实际状态 **PASS_LOCAL_TRANSPORT_CONSISTENCY**，12667字节，SHA256 `54234f3985b973ac77b61bc9fb67f9a49d5d0a36f80957d269c7cea8b020526f`。独立脚本不导入prepare，使用只读可寻址的分片拼接流重新检查完整evidence ZIP全部337成员CRC／EOF／SHA／模式；另外三个ZIP也重新完整检查，八个副本与原件一致，原八项checksum均通过，十二个实际传输文件均严格小于100MiB。[传输审计](Evidence/076/Delivery/transport-audit.json)、[说明](Evidence/076/Delivery/transport-audit.md)、[传输manifest](Evidence/076/Delivery/transport-manifest076.json)。传输成功不是将原Delivery1失败改判为通过。

## 云盘十二文件与精确字节身份

- 候选目录：[0.19.0 · 076 野战医疗 · Candidate1](https://drive.google.com/drive/folders/1CgcNrTzLaVKXoDwtBTaYJ1I2mD7Wegnv)。实际父目录：[Frontier Reckoning](https://drive.google.com/drive/folders/1Dgm7Nx9Qwdaz-vhClbRekJ134tEOW4GH)。
- 最终receipt时间：2026-10-09T22:05:58.321289+00:00，即北京时间2026-10-10 06:05:58。逐件完整远端文件流读回，实际字节数及SHA与本地传输原件一致，总 **294999523字节**。
- 十二文件、候选目录及项目父目录实际权限元数据均为 `shared=false`、仅owner；所有文件的parent ID均为该候选目录。最终直接子项恰为十二个已验证文件，子ID集合与上传ID集合一致。没有调用共享ACL修改，也没有公开链接权限。
- 远端两片也按正确顺序实际拼接校验，结果 **122090550字节／SHA256 `101a05650de14ccb5588bd577ae130318d5ee544537767c3880b4deb2428bb7a`**，与本地完整原evidence ZIP一致；不是仅依靠两片各自校验或上传成功提示。
- [最终十二文件回读receipt](Evidence/076/Delivery/drive076-final-twelve-verified.json)：19421字节，SHA256 `1b562e2582dec1745ea13e04397909cc13ff3fba47257349fce716f88d972c68`。该receipt是后补独立收据，不是十二个上传对象之一，未声称上传自身。

|文件|精确字节数|SHA-256|实际观察到的云盘链接|
|---|---:|---|---|
|`README-076.md`|2487|`bb91028fb538ea83c680eb58427b7e266651e4ef1dd4226273c2fc8d5fc67685`|[打开](https://drive.google.com/file/d/17Xxuv21a-P0qKX9V74AykkFFCqmbitsx/view?usp=drivesdk)|
|`README.md`|1796|`2e9e0cfcc17f78fcd8cdbdde7e2a5e35d6daa491f49fdef6ae75f1f51373174b`|[打开](https://drive.google.com/file/d/1llVXOSnGVzlgNRZf9eDvGbgYjjytCBcA/view?usp=drivesdk)|
|`actual-app-manifest076.json`|204218|`f05d5fbccfec46f54a1696ff7450ff01f541252c275669319af1b5dc48a90c4d`|[打开](https://drive.google.com/file/d/1YeOXvi52xrTqMM-F3uv9u5FKA5QkIbr3/view?usp=drivesdk)|
|`checksums.sha256`|657|`bae17e26dc8bc050c63d594392be0268ba5bb9240ef8c2ce4762e3e8ab6c254c`|[打开](https://drive.google.com/file/d/15niRMd-cdbfiuH60yzx4zFmYOhSsvGsA/view?usp=drivesdk)|
|`evidence.zip.part01`|67108864|`ab3e2917e56fafb1a860fe18500007d8037724c12b321ceeb03f390a1249fe31`|[打开](https://drive.google.com/file/d/1riAn4jf-qsX260cYBkzC1ZwaaAqK-s0a/view?usp=drivesdk)|
|`evidence.zip.part02`|54981686|`6fca769045f8f8e91c48eb2074d873562498176066857e3403ea10780b54aca2`|[打开](https://drive.google.com/file/d/1tQ9WTbkJh0BAluVZneXRbLVKIN_WgfvE/view?usp=drivesdk)|
|`macOS.zip`|76004155|`ef6be719db8ef73a96ebaf45f27d040f5316f4c192133724aaef18a81098c690`|[打开](https://drive.google.com/file/d/1mgkfeCVhrk3w2rK7zntHN6_2Qeo-7QqY/view?usp=drivesdk)|
|`normal.zip`|36922998|`23d5f9f35ddb633d08b6a0cbd36e7c8c3b639e6c93da57a657a827be162ebdb1`|[打开](https://drive.google.com/file/d/19yvBRXihNDy-W4bYdO_0QeeG4Twl0T32/view?usp=drivesdk)|
|`package-manifest076.json`|1369366|`2bc94f869674fd1c6cfb9c7710026966d90bb2ef4d1e510b22643f90d109678b`|[打开](https://drive.google.com/file/d/1fHCATLL05QHlOCovIURG_YZkUCWupHWL/view?usp=drivesdk)|
|`source.manifest.json`|151713|`4f427b044fe221a837436ae3956cd2604121d567341e86b8d4e2205ebd4389a0`|[打开](https://drive.google.com/file/d/16FOyPJt7GH3IXRJdOqptBA5Zdhk453lg/view?usp=drivesdk)|
|`source.zip`|58231890|`2720158793eeaa807dd47255ea108507e58276f217c541c4c1173ebc1e2eb6d7`|[打开](https://drive.google.com/file/d/1P48ZefDl8Vga0scmeh_zvUR4qQi4d7Bb/view?usp=drivesdk)|
|`transport-manifest076.json`|19693|`8e4725249adcbb26636de17108a0f9087666bc4d4ad32ba4263d7949af882352`|[打开](https://drive.google.com/file/d/1mXYl5bRZ8rPzvrYgykoRitMmRjprYzgb/view?usp=drivesdk)|

## 下载、重建与校验

`source.zip`、`macOS.zip`、`normal.zip`为可直接使用的完整ZIP。另将两个证据片段下载至同一目录，按文件名顺序重建；先确认目标目录没有需保留的同名 `evidence.zip`，避免shell重定向覆盖旧文件：

```sh
cat evidence.zip.part01 evidence.zip.part02 > evidence.zip
shasum -a 256 evidence.zip
```

预期大小 **122090550字节**，预期SHA256：

```text
101a05650de14ccb5588bd577ae130318d5ee544537767c3880b4deb2428bb7a
```

核对后解压完整evidence ZIP；十二项下载完成且证据重建后，再运行原校验文件：

```sh
shasum -a 256 -c checksums.sha256
```

原 `checksums.sha256` 仍只记录原八项对象，其中evidence行指向重建后的完整ZIP；不包含新分片、README-076或transport manifest。新传输对象的校验值见上表与transport manifest。分片保持完整ZIP的压缩字节，不是独立压缩包，勿分别解压或交换顺序。[下载说明原件](Evidence/076/Delivery/README-076.md)。

## 完整原件与验收局限

- evidence ZIP保留Focused1／2首轮失败及Focused3／4后续验证、Full1实际原报告、Build1原日志／receipt／app清单、完整Native1、完整NormalObserved1原始JSONL／样本／快照及分析、当时Diagnostics及声明／文档。Normal原始观察另有完整normal ZIP；没有删减以满足门槛。
- 原生英国Winding42的第一局失败／Restart原件保留且不是自然败局；付费第二局menu_exit不是完整人工胜局。原生付费医生、Stop／手动恢复、297HP保存、新PID续疗至450及两段连续原生回放的已观察范围保持[原生审计](Native-076.md)原边界。封包与上传不增加原生Otto、超射程手动接近、返战Fire、真实两桥／侧栏、驻扎／上下船医疗或完整AI磁盘会话的覆盖。
- 两局普通AI的5名付费医生、34条严格同患者整补回火链保持原观察；曲河局达到cap，不计胜。医疗关闭matched-seed对照、更多文明／地图、慢Step归因、FPS、大军同屏、完整人工自然胜负、双物理Mac、跨版本存档迁移及Release公证仍独立待验。
- 075九文件私有交付、源码／GUID／截图／存档／录像及其原验收保持原身份，[075交付](Delivery-075.md)未覆盖或重命名为076；整体内容差距目标仍进行中。
- 原封包、transport与后补cloud／Git交付receipt分别保留。[后补交付copy index](Evidence/076/Delivery/delivery-copy-index076.json)单列原件／拷贝；后补receipt／helpers／本页属于后续仓库文档层，提交后的最终HEAD另行读回，不倒写冻结source.zip／evidence.zip，也不混称已在十二个云盘对象中。未合并main、公开发布、部署或修改云端共享权限。
