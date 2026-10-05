# 0.11.0 交付与核验（068）

源码、macOS arm64客户端与当前原生图集的三个ZIP已冻结，逐成员检查和独立验包通过；Google Drive七个对象已上传并读回名称、字节数、父目录、shared=false及仅所有者权限。GitHub私有开发分支与原draft PR交付正在收尾，后续读回另记；没有合并或公开发布。

下载入口：[原私有项目中的0.11交付目录](https://drive.google.com/drive/folders/1K_xqcyeJAc87F6DJpmDb0CNflM0PtDcC)。下列SHA和bytes属于本地冻结ZIP，没有远端内容SHA或下载字节对照证明。

| 包 | 本地bytes | 本地SHA-256 | 成员 |
| --- | --- | --- | --- |
| [源码](https://drive.google.com/file/d/13LHXYAAGSMKvmn8kkSTjVQy4pjeb3gpo/view?usp=drivesdk) | 37164717 | `8bf03b113fa54b67d21a17d84d25ab0eadb23a83f26b2806992c1979f21f3d31` | 1018 |
| [macOS arm64客户端](https://drive.google.com/file/d/1tEQVj07h-DlXFBl4zf4YaUQDI5CggOvq/view?usp=drivesdk) | 75839160 | `47cc14a7c78a475435dbb38c4193e87e8dfd3de3de2df2fabd757b3da1f2d23e` | 322 |
| [当前原生图集](https://drive.google.com/file/d/1oZ7C_zxp3tEYd3KC49yt-PtydFGoLi1H/view?usp=drivesdk) | 13855483 | `a0abf060bcbe1f4f9ee398764c593f207771f4302f377a4c67dc4a5f300bf11d` | 86 |

## 构建和源码身份

- 源码包快照提交：`89061bc0a433fd4e94f98518200de0928cc91f7f`。
- 运行提交：`dca073708b671c60a1fe9ff243a67e4ace3fe157`。
- 630输入FP：`62240c2373c773b5aaf807c72815acab2e76577504f3a0569218702864730ebe`。
- 最终原生GUID：`fa26339934914cd2813df9763de6bba7`，Unity6000.6.3f1 / macOS arm64 Development / Mono。

[068完成快照](Completion-Status-068.md)保留源码ZIP冻结前的验收状态；本页与[Delivery回执](Evidence/068/Delivery/README.md)是冻结后的包外资料。三个已上传ZIP不再重建，之后的文档提交不会改变其SHA。[随包说明](Evidence/068/Delivery/README-zh.md)和[两人操作](Player-Guide-068.md)提供启动、加入、准备、共享暂停、断线和退出方法。

## 验收范围

[完整测试](Evidence/068/Tests/README.md)为1,185/1,185：817 EditMode＋368 PlayMode；全部旧1,097项及新增88项LAN用例实际通过，失败/跳过/未定为零。源码和构建冻结绑定同一630输入；本地测试不充当两台实体Mac的结果。

[当前原生证据](Evidence/068/Native/README.md)包含同一台Mac上两个独立进程经真实loopback TCP的22个操作阶段、24张UI图及16组PNG/JSON pairs。正常资源下两端训练付款、取消第二个相同作业并退款/保留第一个、移动、本地菜单/共享暂停、owned客户端中断18秒、房主在队友离线时恢复、重连收敛、离开和两个进程正常退出0均有实际操作记录。保存的menu_exit段的20个事件前缀、9个输入和9个状态哈希，以及4442条退出事件账本已独立核对；它不是原手动SAVE前缀，也没有进行该段原生冷播放或自然整局胜负。

两个物理Mac、真实网卡/防火墙、当前LAN的原生指定攻击/船运、自然合作终局、冷回放及长窗帧时间/内存/RTT仍待验证。ACK丢失后去重付款由真实TCP自动化覆盖，没有冒称这次原生故障注入。可信LAN没有公网匹配、NAT/中继、主机迁移、竞技敌情过滤、防作弊或跨构建/平台互通保证。

## 打包和独立核验

[清单](Evidence/068/Delivery/release-manifest.json)绑定1018源码、322客户端和86图集成员，总计1426。源码选集逐项匹配冻结Git blob、类型与执行位；每个ZIP成员完整读取CRC/SHA/bytes/POSIX权限、类型、路径。本次包内symlink实际为零；链接处理由临时夹具覆盖。客户端完整app及arm64/GUID绑定构建身份，没有个人存档/设置/账号数据。图集仅含当前Candidate3 Native，旧诊断只存在源包并标清历史身份。

[最终独立回执](Evidence/068/Delivery/independent-zip-verification-final.json)11项Passed，4652bytes，SHA `a63483f83ca7713f2933550f1e656274120899f1758eca02dcb4021f6a0cd50c`。第一次规则把合法Native/Build身份文件误判为缓存，保留[首次Failed回执](Evidence/068/Delivery/independent-zip-verification.json)，修正仅影响包外验证器；最终回执关联旧/新工具身份及纠正原因，三个ZIP和清单字节未变。源码ZIP内原验证工具快照保留冻结字节，修订工具在包外[final helper](Evidence/068/Delivery/Tools/independent-zip-verify-068-final.py)。原始归档日志的空白和警告保留；代码及说明的空白检查通过。

## 云盘读回

[六个载荷回执](Evidence/068/Delivery/cloud-upload-readback.json)记录三ZIP、清单、随包指南、独立验包JSON；各自本地上传前后bytes/SHA完全一致，云端名称、大小、目录和owner-only私有权限读回通过。[外层回执](Evidence/068/Delivery/cloud-delivery-final.json)记录第七个云盘对象——六载荷回执——的上传与读回，本地前后哈希也一致。外层文件仅在本地/仓库保留，不计为第八个云盘对象。没有共享权限修改，回执明确未下载比较远端内容SHA。

项目继续保留私有仓库与[原draft PR #1](https://github.com/freezeng123456/unity-mimic-aoe/pull/1)，不合并main或公开发布。整个AoE III差距目标仍在进行，[差距路线](AoE3-Gap-Roadmap-064.md)保留后续内容与验收工作。
