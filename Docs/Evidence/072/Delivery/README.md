# 072 最终私有交付 · 选定原件

运行bc127、04698/718、GUID3288；SourceSnapshot8490。22份实际原件共2,901,154B逐字复制，来源绝对路径、源前后与副本bytes/SHA/mode/lstat身份见[复制索引](delivery-originals-copy-index072.json)。本目录不复制ZIP、80MB分片或巨型原生/矩阵材料，不改外部冻结树；完整当前交付见[Delivery072](../../../Delivery-072.md)。

## 实际交付收据

[Drive读回](drive072-final-delivery-readback.json)状态uploaded_owner_only_all_remote_bytes_sha256_verified：本版私有目录精确11文件458,466,879B，均shared=false、仅owner，父目录/名称/大小与实际期望一致；11远端文件通过认证原文件引用全部下载并逐响应字节计算SHA，与本地SHA和大小一致。它包含4个完整ZIP、3个Normal原字节分片、包清单、分片清单、合并脚本和README。209,058,503B整NormalZIP不作为单个远端文件存在。

- [包清单](package-manifest072.json)与[seal](package072-seal-final1.json)：本地5ZIP456,136,823B/2465members及精确source profile、Git保留/省略对象清单。
- [源码冻结](source-freeze072-final1.json)：SourceSnapshot8490和全部718运行SHA，运行输入仍与1567正式测试/构建/矩阵一致。
- [app](app072-final1-inventory.json)、[evidence](evidence072-final1-inventory.json)、[gallery](gallery072-final1-inventory.json)、[normal](normal072-final1-inventory.json)：四个实际冻结树inventory；[暂存原始收据](evidence072-final1-final-staging.json)保持129选定副本/完整Native的来源。
- [五包独立JSON](072-final-five-archives-independent-review.json) · [MD](072-final-five-archives-independent-review.md)：2465成员CRC/SHA/bytes/type/mode/path、四inventory与Gitblob对照；不是新的游戏测试。
- [证据暂存独立JSON](072-final-evidence-inventory-independent-review.json) · [MD](072-final-evidence-inventory-independent-review.md)：完整305成员/129选定副本/118Native来源和模式。
- [正常矩阵树独立JSON](normal072-final1-inventory-independent-review.json) · [MD](normal072-final1-inventory-independent-review.md)：144成员全部原件，16closed但9reached，不扩大为全部路线或平衡通过。

## 分片、真实合并与限制

[413原始拒绝](normal072-drive-413-original.json)记录连接器单文件104,857,600B上限及actionInvoked=false，不是用户审批拒绝。[分片清单](normal072-transport-manifest.json)将完整NormalZIP按连续原字节拆成80,000,000+80,000,000+49,058,503B；[脚本](assemble-normal072.py)与[原始下载说明](README-072-下载与对战记录合并.md)保持实际已上传字节。

[独立传输JSON](072-normal-transport-independent-review.json) · [MD](072-normal-transport-independent-review.md)37checks/0fail，逐字读完并比较209,058,503B三片与原ZIP，拼接SHA036fde...相等。原件前后身份/mode稳定；静态脚本只证明普通无优化Python调用的校验与xb/os.link防覆盖。[实际合并审查](normal072-transport-assembly-review.json)另记缺失/损坏拒绝、正常重组全ZIP CRC+SHA及已有输出保护4例。静态审查与实际运行收据不互相冒充。

原封包与SourceSnapshot8490不变；本目录及最终当前交付页面是后续追加，不写回已封Evidence/Gallery/raw树。历史原件的当时pending/uploaded=false/releaseAcceptanceGranted=false保持原字节：这些限定其当时检查作用，当前远端完成由新的Drive读回证明。包/传输检查不加入正式1567Unity测试，也不改变9原生声明范围、9/16路线结果、平衡Not assessed、帧率/签名/网络边界。最终Git提交/推送由root另行完成，本索引不声称已推送。
