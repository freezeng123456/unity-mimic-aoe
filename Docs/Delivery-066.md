# 0.9.0 交付与核验（066）

三个最终ZIP已通过本地内容核验，六份载荷和第七份上传回执均已上传；本次云端名称、大小、父目录、`shared=false`与仅所有者权限元数据读回通过。**远端SHA-256及下载后的字节一致性未验证；GitHub最终推送与PR更新仍由主任务随后完成。** 私有仓库和draft PR保留，没有公开发布或合并结论。

下载入口：[私有0.9交付目录](https://drive.google.com/drive/folders/1wzczmUUktYSDSn92KuwpUQ4dZkIK-h-3)。下列大小和SHA属于本地最终ZIP，不是远端内容校验结果。

| 包 | 本地bytes | 本地SHA-256 |
| --- | --- | --- |
| [源码](https://drive.google.com/file/d/1j8MgvvuvxjcyQUbe5glqD16xh29FXqdM/view) · `Frontier-Reckoning-0.9.0-source.zip` | 27361952 | `577029148b76bea60e5cd39925f45f51d63295f100a92764556665ca417e5cc0` |
| [macOS arm64客户端](https://drive.google.com/file/d/1AHgqXFTL27ynXpq54R6hLmECkSj_UINt/view) · `Frontier-Reckoning-0.9.0-macOS-arm64.zip` | 75783320 | `223dd1b8b1958a1f0eecafb028f0407438d904fb35f0bbbed2afa719c1cd8096` |
| [原生图集](https://drive.google.com/file/d/1pa4je-NJ8p_0CFaSH7iU5kCmXSB1bxf_/view) · `Frontier-Reckoning-0.9.0-gallery.zip` | 10855029 | `28f52a3e724a76f334d8c0cb2412e156be83db53b36f6f0c604d5a178c828576` |

## 源码、运行和包外文档的身份

- 源码ZIP的文档/源码快照提交：`e75407c78522374ed7e327a1ed1727212d27ffd3`。
- 运行代码提交：`c9747695d91fa203b05116a1fc0c15a8aac32de2`。
- 573个运行输入指纹：`66cf7cc57e080eb40cfdf98310ee26affd93d690745dde2b457e8fb3eb0ee1ee`。
- 原生构建GUID：`1969c7ee14f944879510f4ca0d0f241d`；macOS arm64、Unity6000.6.3f1、Development/Mono。

三ZIP在核验和云端上传后未重建，哈希保持不变。本页及[Delivery证据归档](Evidence/066/Delivery/README.md)是在e754快照后新增的包外文档，不在已经上传的源码ZIP内；后续文档提交不等于重新生成源码包。[Completion-Status-066](Completion-Status-066.md)保留分包前的本地验收快照，当前[完成状态](Completion-Status.md)另记交付进度。

## 本地核验与云端元数据

[release-manifest.json](Evidence/066/Delivery/release-manifest.json)绑定三ZIP哈希。[最终验包回执](Evidence/066/Delivery/package-contents-verification.json)为435753字节，SHA `9b34a9d5a77090bac072eaef47b88c705ea30cc2437ea815e87fff1ad0d7dae9`，八个部分全部Passed：testEvidence、source、client、gallery、sourceStillFrozen、sourceArchiveStillFrozen、clientArchiveStillFrozen、galleryArchiveStillFrozen。核验覆盖包内容、测试/源码指纹、客户端身份和图集20组F9；它不重新运行游戏或性能测试。

[cloud-upload-readback.json](Evidence/066/Delivery/cloud-upload-readback.json)记录六份载荷：三ZIP、release-manifest、验包JSON和README-zh。逐项本地字节/SHA重核、远端名称/大小/父目录/隐私元数据均通过。[cloud-delivery-final.json](Evidence/066/Delivery/cloud-delivery-final.json)记录第七份cloud-upload-readback回执的上传与同样的元数据核验。第七份回执为11344字节，SHA `15255d36d18fac0c498efc359adac2a2fc11d898848ebdf07c2bf85eb0f15b52`，远端ID `1aRkIE3t779q4qqO6VjIa_W63WvBLJCqd`。

当次七对象均`shared=false`、仅owner角色，没有改变共享权限。回执明确各载荷`remoteContentSha256=false`；本地SHA和远端同名同大小不能证明远端内容相同，也没有下载字节比较结果。cloud-delivery-final和归档辅助文件是后来保存的外层资料，不被算成第八个上传对象。

## 启动与已验证范围

下载客户端包并完整解压，启动`Frontier Reckoning.app`。适用Apple Silicon macOS；未签名公证、未验证Intel Mac。实际随包说明原文见[README-zh](Evidence/066/Delivery/README-zh.md)，完整操作见[玩家指南](Player-Guide-066.md)。客户端不附个人存档、测试偏好或完成记录；初次使用默认按键A，原生测试的Q改键不是交付默认值。

[测试归档](Evidence/066/Tests/README.md)为1062/1062（710 EditMode+352 PlayMode）；[原生回执](Evidence/066/Native/README.md)验证设置冷恢复、单课槽3冷恢复后真实移动完成31.2000828秒及账号徽记再次冷恢复、2v2曲河103.4989929秒冷恢复/投降后盟友继续、四国FFA开局。它不证明全10引导场景原生通关、四人完整终局、本版400单位性能或其他平台。Development退出警告保留，本版性能未采样。

## 失败记录与GitHub边界

[首轮验包失败](Evidence/066/Delivery/PackagingAttempt1/package-contents-verification.json)原样保留：客户端身份的文件/字节检查及图集应用身份/测试证据检查失败。首轮源码清单亦保留，旧失败ZIP原件仍在输出PackagingAttempt1；它们不是云端最终三包。只引用后来八部分全部Passed的最终回执，不抹去失败历史。

[推送前GitHub观察](Evidence/066/Delivery/github-before-final-push.json)记录私有仓库、[PR #1](https://github.com/freezeng123456/unity-mimic-aoe/pull/1)仍open/draft且未合并。本页冻结时最终推送和PR说明更新仍待主任务完成，不能把本地e754快照或Drive上传当成GitHub已更新。没有Git提交、推送、PR修改、构建、UI或重新打包由本次文档归档执行。
