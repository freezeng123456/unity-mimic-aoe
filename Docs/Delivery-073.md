# 0.16.0 私有交付 · 073

2026-10-07。源码、Mac客户端、选定证据及完整三局原件已封包，并上传原私有Google Drive；**九文件111794577B均实际读回全字节、SHA相同且仅owner可访问**。正式1611项回归、构建、三局观察通过其声明范围；当前GUID原生兑换交互未完成，建造/扣费/完工范围已核。[内容](Content-073.md)、[验证](Verification-073.md)、[状态](Completion-Status.md)、[限制](Known-Issues.md)。

## 身份与源码选择

| 项目 | 实际身份 |
|---|---|
| Runtime | 519d7d34a0be85f75ee19e53458c636d1ba0b3e4 |
| 源码ZIP快照 | 0f656d39c6715a27a6d85414dac7c16c3c7645bd |
| 运行输入 | 732；FP9b92812a922adb69a5ae501925417d8eba8e24b836f496274ebc8ec8679ba03d |
| 构建 | 0.16.0；Unity6000.6.3f1；arm64 Development/Mono；Build1实际exit0/sourceStable |
| 原生GUID | 5f347ad9ec2f494984b0b49c4f896c31 |
| 回归 | run-9q8yTJKS；1192Edit+419Play=1611/1611Passed |

profile为all-tracked-except-Docs-Evidence-073-v1：1008选中tracked路径23985861B、4254省略路径3420117514B，完整分区记录Git path/mode/blob/bytes。包含全部Assets/Packages/ProjectSettings/Tools、根文件、SourceAssets及非Evidence文档；明确省略Docs/Evidence/**，不是完整Git树或历史归档。选定61份原件+索引另在evidence.zip，完整28份普通局原件在normal.zip。后续Git提交仅写包外交付收据，不改运行输入或重建原ZIP；源码ZIP内文档为封包前快照，终态以本页和云盘README为准。

## 下载与真实字节核对

[0.16.0私有目录](https://drive.google.com/drive/folders/1tmsiSFugort0xgY43XYsDQm4TtKATdBP)，父目录为原Frontier Reckoning项目。父目录、候选目录及九文件实际metadata均shared=false、仅一项user owner；没有共享修改。

| 原件 | bytes | SHA256 | 下载 |
|---|---:|---|---|
| source.zip | 16296933 | `8b2c5df6983a6876e05b04dd5fd136be7ad547e19b550167c0789e7daecf761c` | [文件](https://drive.google.com/file/d/1_NKWQHV7TKtVnMxrjHxT8xvdZmSpivKg/view?usp=drivesdk) |
| macOS.zip | 75955219 | `f341b957fbc7f747a4c299d8ba5264f774d8739c0519d06e2afa2a7966622a91` | [文件](https://drive.google.com/file/d/125ZKVJ7P3AYyhs6BsXJpNP4hqUxeYobz/view?usp=drivesdk) |
| evidence.zip | 4945039 | `b3dd2a39711ff93d88f8e7504247acf961946fca209ec0d05ba7408e30027018` | [文件](https://drive.google.com/file/d/1k863sr5GVApNezINA04DxzZLJQLAgp4-/view?usp=drivesdk) |
| normal.zip | 11710868 | `2cd481547129cdc4091c3e2119489f1bccd750effd5f4dab7d04797225ac4598` | [文件](https://drive.google.com/file/d/1VluFXwsPIknVIJCK_hEA3CnZcwB_Dkl2/view?usp=drivesdk) |
| package-manifest073.json | 2565132 | `3d9a6c4d5e1ab24daa26f686d0d782c010e59c9f5b40749c0b80c0579d6ac714` | [文件](https://drive.google.com/file/d/1Dev2OA4kEWX5jKvEfwNzomxE6eT1ola9/view?usp=drivesdk) |
| source-map073.json | 93889 | `dcffdf16c51f10eef0e1be84e7302d4f2545ca5048faec3dd8ebf08378391bd5` | [文件](https://drive.google.com/file/d/16UHFeKMOMQDn_SgpZ3h2w0qNeTpwjpYs/view?usp=drivesdk) |
| actual-app-manifest073.json | 217262 | `83b5714a36bbceac21ceda9ee8a368e6a0a7e1037107358d912cb19bba9ea7a1` | [文件](https://drive.google.com/file/d/1Fcb6aA-w4zRPHztPA3OoY2oyjzKSX-yH/view?usp=drivesdk) |
| drive073-original-seven-verified.json | 6077 | `78bc96c0e171312b818c1fce392790eefbef1474a643a2ab651cbbfcfabdeb05` | [文件](https://drive.google.com/file/d/1lG3O7aXoAWoK5ajFeiuHMOKGlIT83TXC/view?usp=drivesdk) |
| README-073.md | 4158 | `18054bc69e36162ccd497e28d89835000e686ab76d36bc65648a09da861d3392` | [文件](https://drive.google.com/file/d/1_wCM4r0CuqnVU-f3drfc-BWnEcPcfUEi/view?usp=drivesdk) |

四ZIP总108908059B、1478成员：source1049（1008files/41dirs）、macOS319（298/21）、evidence77（62/15）、normal33（28/5）。各低于100MiB；成员集/EOF/CRC/全字节SHA/Unix type+mode/执行flag已核。本次没有symlink；ACL/xattrs/resource fork和签名公证不由ZIP验证表示。

[封包manifest](Evidence/073/Delivery/package-manifest073.json)SHA3d9a6c4d5e1ab24daa26f686d0d782c010e59c9f5b40749c0b80c0579d6ac714；[独立本地封包审计](Evidence/073/Delivery/independent073-package-audit.md)25319项检查、0失败，另有2410项只读核对和实际helper --verify exit0，均不增加Unity1611测试计数。fresh Git blob/source ZIP/freeze/source-map的732SHA相同；证据62file与源码快照Git副本/61原件相同；normal28file完整保留。

[最终九文件收据](Evidence/073/Delivery/drive073-final-nine-verified.json)保存实际Drive ID/title/parent/权限/全字节bytesSHA；它验证七原件111784342B，以及上传的七文件收据6077B和README4158B。收据本身不递归声称已核自身上传。[包外交付复制索引](Evidence/073/Delivery/delivery-copy-index073.json)记录原件SHA。原包、SourceSnapshot和此前审计均封存不改。

## 游戏通过范围与未完成项

三条普通I、standard双AI，无注资或强制干预；Ottoman44/45自然終局，Dutch42在2400秒cap未决胜。实际市场交易11/15/0、assembled14/14/21，最后300秒均有实际PvP伤害。10049项一致性核对通过，P1两Ott以70C市场收入补足并实际研究Ledger、收到金币运输；P0未完成Ledger，Dutch市场未触发。不同窗口不认证六文明平衡、全部路线或因果性能改善。

本GUID Native1只核正常300F/300W/100C开局、无效岸边放置不扣费、Market137在陆地100W付款并完成、三1600×1000F9和实际进程exit0。后续public Sky坐标click/drag返回noWindowsAvailable，原生市场模态/买卖/100-500未完成，原报告零MarketTrade；当前GUID原生cold/replay未验。InputSystem/Canvas自动化与OS像素分开。普通Gather动态路线安全、约1.8s模拟慢步/长期原生FPS、双物理Mac、完整商业内容、Release签名/其他平台仍待工作。

## Git审查与历史身份

[既有私有draft PR1](https://github.com/freezeng123456/unity-mimic-aoe/pull/1)是审查入口；分支codex/land-rts-completion，main、公开发行和共享范围不在本次交付。源码ZIP以冻结SourceSnapshot为准，包外交付记录的最末Git提交以PR当前head和外部GitHub最终读回收据为准，避免包哈希或提交自引用。

[072交付](Delivery-072.md)及其11远端文件、SourceSnapshot8490/runtimebc127/718输入/GUID3288、16普通局仅9/16路线和五原ZIP保持原字节/旧范围。[072完成快照](Completion-Status-072.md)、[README快照](Archive/README-072.md)、[路线快照](AoE3-Gap-Roadmap-072.md)、[限制快照](Archive/Known-Issues-072-final.md)逐字保存，原Known-Issues-072和所有Evidence/072未改。
