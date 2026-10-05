# 066 包外交付回执归档

此目录逐字节复制最终输出的回执、上传输入、辅助脚本及客户端说明。具体原始路径、大小和SHA见[archive-receipt.json](archive-receipt.json)；本次只归档，没有执行脚本、构建、重新打包、上传或Git操作。

- [release-manifest.json](release-manifest.json)：三ZIP本地字节/SHA、runtime c974、源码快照e754、输入指纹与构建GUID。
- [package-contents-verification.json](package-contents-verification.json)：最终8部分全部Passed；435753字节，SHA `9b34a9d5a77090bac072eaef47b88c705ea30cc2437ea815e87fff1ad0d7dae9`。
- [cloud-upload-readback.json](cloud-upload-readback.json)：六份载荷的上传ID和逐项元数据核验。
- [cloud-delivery-final.json](cloud-delivery-final.json)：第七份回执上传及元数据核验；远端内容SHA/下载字节一致性未验证。
- [upload-inputs.json](upload-inputs.json)、[源码包清单](Frontier-Reckoning-0.9.0-source.manifest.json)：冻结输入；三ZIP没有在上传后重建。
- [README-zh.md](README-zh.md)：客户端指南原文，也是六载荷之一。
- [打包辅助](package-release-candidate.py)、[验包辅助](verify-package-contents.py)、[原生证据汇集辅助](assemble-native-evidence.py)：原脚本快照，仅保存未执行。
- [推送前GitHub观察](github-before-final-push.json)：私有仓库及保留的draft PR；不是最终推送或PR更新完成证明。
- [首轮失败验包](PackagingAttempt1/package-contents-verification.json)：clientReceiptBinaryFileAndBytesMatch、gallery appIdentityAndTestEvidenceAvailable失败；[首轮源码清单](PackagingAttempt1/Frontier-Reckoning-0.9.0-source.manifest.json)保留。旧失败ZIP原件仍在输出PackagingAttempt1，未替换成成功回执或宣称为云端最终载荷。

本目录是e754源码ZIP冻结后新增的外层文档，不在原源码ZIP中。六载荷为三ZIP、release-manifest、验包JSON和客户端指南；第七份是cloud-upload-readback。cloud-delivery-final及本目录其他文件不是新增云端对象。下载入口、最终包身份和当前GitHub边界见[Delivery-066](../../../Delivery-066.md)。
