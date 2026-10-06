# 0.15.0 私有交付 · 072

2026-10-06。源码快照、macOS arm64客户端、实际原图、完整当前原生与验证证据、16局正常开局原始材料已上传到[原私有Google Drive项目的072目录](https://drive.google.com/drive/folders/1hdezEOqw10bCPAUZ6YIG0btx8Rbw-0I5)。目录恰有11文件，共 **458,466,879B**；本版目录和全部文件均shared=false、仅owner权限，父目录和名称/精确大小均重新核对，没有扩大共享。

实际读回在2026-10-06T14:41:44.457510Z完成：[原始收据](Evidence/072/Delivery/drive072-final-delivery-readback.json)状态为 **uploaded_owner_only_all_remote_bytes_sha256_verified**。Codex root通过连接器给出的认证原文件引用，真正下载11文件并对每个响应的全部字节计算SHA256；11个字节数和SHA均与本地一致。此结论包含实际远端内容核验，不只依据元数据；收据没有保留临时签名下载URL。

## 下载入口

源码、客户端、验证证据与画廊各为完整ZIP：

| 文件 | 精确字节 | ZIP成员 |
|---|---:|---:|
| [macOS arm64客户端](https://drive.google.com/file/d/1V5mbSw99Mu-yjbKjSvOsqkFnzPz-qrb3/view?usp=drivesdk) | 75,954,915 | 319 |
| [当前源码快照](https://drive.google.com/file/d/1nsvMhRAcxXb99GiEQkWEviSWvkanc6ai/view?usp=drivesdk) | 82,614,658 | 1673 |
| [完整原生及当前验证证据](https://drive.google.com/file/d/1pqWca_wVjdQ4o4sbT8gmBVqI4E75uYJb/view?usp=drivesdk) | 74,596,163 | 305 |
| [10张实际原图画廊](https://drive.google.com/file/d/1pDI1r8nnQcx5EHiKpsRNZl8P3Wm3BoMW/view?usp=drivesdk) | 13,912,584 | 24 |
| [完整包成员/省略清单](https://drive.google.com/file/d/1KCD5RUEiCHDmvZfdBez6gH8bodMXiJvJ/view?usp=drivesdk) | 2,326,061 | JSON |

完整自然对战ZIP为209,058,503B，超过连接器单文件104,857,600B上限；整包上传在action调用前被413拒绝，真实[拒绝原件](Evidence/072/Delivery/normal072-drive-413-original.json)保留。它以三个连续原字节分片交付，完整ZIP没有作为单个远端文件上传：

| 文件 | 精确字节 |
|---|---:|
| [正常局分片001](https://drive.google.com/file/d/1cW_inf5_TvigoivYeeloXEZzt5HuuSSP/view?usp=drivesdk) | 80,000,000 |
| [正常局分片002](https://drive.google.com/file/d/1VDRQvdHBZjBTzpHOPJx3gYRok6vmPr1w/view?usp=drivesdk) | 80,000,000 |
| [正常局分片003](https://drive.google.com/file/d/13DQAnZkraR5PbDvXb7RDLnYP0r0IXyAh/view?usp=drivesdk) | 49,058,503 |
| [分片哈希清单](https://drive.google.com/file/d/1OFiVNogvKoY3ET7muNVQ2S_UWRc62eRf/view?usp=drivesdk) | 1,187 |
| [合并脚本](https://drive.google.com/file/d/1aOJ7j67vwE-d1bCgCmWC6TnvvWf3i88i/view?usp=drivesdk) | 1,657 |
| [下载/合并说明](https://drive.google.com/file/d/124FlGgbW5iFJJ4e5mCoAFWZUvosjqLMS/view?usp=drivesdk) | 1,151 |

下载三个part、分片清单和脚本到同一目录，以普通、无优化的Python3执行：

```sh
python3 assemble-normal072.py
```

脚本核三个分片与合并结果SHA、完整ZIP CRC，生成 `normal-routes-originals072.zip`；保留已经存在的输出文件。实际[合并检查](Evidence/072/Delivery/normal072-transport-assembly-review.json)通过缺失分片、损坏分片、正常合并和已有输出保护四例；正常合并实际读完ZIP CRC，字节/SHA与原件相等。独立[传输审计](Evidence/072/Delivery/072-normal-transport-independent-review.md)37checks/0fail，逐字比较三片与原ZIP连续区间，前后原件身份/模式稳定；静态脚本审查仅限定普通无优化Python调用，不扩大到-O环境。

原ZIP及合并结果SHA256：`036fde3891836a12f25a98f8af1bf3c0fa5974711580ffed72864b82d3c9c33a`。它含144成员、4,247,404,839B未压缩文件，完整保留16case的样本、事件、开局/比较/最终世界、原日志和来源收据。三片远端下载SHA分别与本地原片一致，和本地逐字拼接/实际合并共同证明这份原ZIP的传输身份。

## 本地封装与源码范围

本地五ZIP共 **456,136,823B、2465成员**，2212文件+253目录；正常局ZIP在本地一直保持完整。实际封包状态passed_local_archive_integrity，[独立全包审查](Evidence/072/Delivery/072-final-five-archives-independent-review.md)逐成员读到EOF并核CRC/SHA/bytes/type/mode/path、四份冻结目录清单和Git对象身份。上传后五个原ZIP的大小/SHA不变。此核验不包括macOS ACL/xattrs/resource fork、签名或公证。

| 本地原ZIP | SHA256 |
|---|---|
| source-current-072.zip | `4a3160b4809ab8635de50169aadde89f305e6d97c7368c3b4b7ce38679713ca3` |
| client072.zip | `6597e426a7f0e10bf24f876fb7ffad0c4e1a7d9a7c2b5c18c3bc6c51764a2b42` |
| evidence072.zip | `e6f2276d7f27bb984015f93a1564238b02d14c2ce32fef9cf40002245d6d9e92` |
| gallery072.zip | `574da3ea2846928b7f3e67731cf4331b241a0d7ecb1dcd8af2099fcba2638b0e` |
| normal-routes-originals072.zip | `036fde3891836a12f25a98f8af1bf3c0fa5974711580ffed72864b82d3c9c33a` |

源码来自明确快照 `8490fc7b153ddd7c3f566a59f9e916f0134f610b`，profile为current-source-workset-with-evidence-070-071-072；保留1518个Git跟踪文件和全部718运行输入。3635个旧批次Evidence路径逐项列明省略，070/071/072及其它保留目录按精确清单归档；**该ZIP不是完整Git历史证据树**。1518保留对象+3635省略对象完整划分同一源树。源码全树元数据SHA为 `598f7e035756abd88cd65152918030ef18c39786e4b91f5d5af47e647ef2ec4f`。

运行提交仍为 `bc127e02638caaeefd2310a1a3eff858f4f4cb2f`，718输入FP `04698dad49278a42db8a13a2ab997870bfb008c52f95313130026da5a7f35f62`；客户端GUID `3288d9fe24e844748a1183f756874fbf`，Unity6000.6.3f1、macOS arm64 Development/Mono。原生保存/录像继续严格版本/构建身份，不承诺跨GUID迁移。

验证证据ZIP248文件/57目录，含129个已提交选定副本、118个Native原始文件以及说明；其中journal固定116原件269,005,909B的完整目录与新增最终日志均保留。[暂存目录审查](Evidence/072/Delivery/072-final-evidence-inventory-independent-review.md)核精确命名空间/源副本/原件身份，没有重写历史running或pending原文。画廊10张PNG保留完整原像素与相应元数据：8张E/D、1张G港区、1张F新Settler；选图均由Codex在声明范围内实际查看，不将选定像素观察扩大为全部原生画面的验收，也不声称人类用户审阅全部图片。

## 游戏验收边界与文档快照

正式1158Edit+409Play=**1567/1567 Passed**，0Failed/Skipped/Inconclusive。本GUID5个实际进程exit0、9个声明原生范围闭合；H只实际1×前向step600→1111、Verified=false，不认证整段5490录像。16正常I局已关闭并独立Consistent，原状态observed_routes_incomplete、9条路线达到（革命6/8、升V3/8），7条未达；15自然终局+1cap，仅5局到2400边界。正常Town15笔500W恢复付款仅5笔完工；Dutch42约55分3秒wall、曲河Ottoman44Age2缺币/初始原因未证实，以及原生MemoryLeaks、长期帧率、普通Gather安全、双物理Mac/LAN、完整商业国别制度/卡池与声画质量差距都保留。上传和包完整性不追加平衡/性能/发行验收。[验证](Verification-072.md)、[操作](Player-Guide-072.md)、[限制](Known-Issues-072.md)、[下一步](AoE3-Gap-Roadmap.md)。

此页和最终交付收据在源码快照之后单独追加，已上传SourceSnapshot8490内的pending阶段页面保持原样，不重打包或形成自身哈希循环。此前071与其它版本的包/文档保持原身份。[交付选定原件与复制索引](Evidence/072/Delivery/README.md)逐字保存本次来源。既有[私有draft PR1](https://github.com/freezeng123456/unity-mimic-aoe/pull/1)是审查入口；本页尚不声明最终交付文档提交/推送结果，也不修改main或公开发行范围。
