# 0.13.0 私有交付 · 070

2026-10-06。三个发行ZIP、冻结清单、独立ZIP验收回执和操作说明已上传到[原私有Google云盘项目的0.13目录](https://drive.google.com/drive/folders/13SGjI5BVUbaT3pRwjOSOn1gIMcEaKQ7J)。新目录及六文件共七个对象的名称、字节数、父目录、shared=false及仅owner权限均由connector重新读回。

| 文件 | 精确字节 | 成员 |
|---|---:|---:|
| [macOS arm64客户端](https://drive.google.com/file/d/17-LTPZ4R8u2fz9gEOy-5PrEehPd8CYuZ/view?usp=drivesdk) | 76067591 | 323 |
| [源码快照](https://drive.google.com/file/d/1KoXGc2IGg-Qo0e5MGhIX1S5pRZOYKTRd/view?usp=drivesdk) | 33648339 | 1070 |
| [实际原生图集](https://drive.google.com/file/d/1bFQWnZ8O-AlkvIxKYgdLjQlWre3ZgDul/view?usp=drivesdk) | 14290591 | 98 |

三个包全部1491成员通过独立CRC、SHA256、路径、Unix模式/执行权限、符号链接、原始ZIP头和原件核对。图集包含27组关键原PNG/JSON，GUID与字节哈希一致；源码含本批完整测试、构建、关键原生账本，候选1保留单独诊断；客户端含操作说明、实际导出Traders' Bend副本和守港实际录像。只封装原.app，未封装Unity生成的备份目录。

运行提交为8b6598d0939262571d0a595ba1499e053f00e1b7，682输入指纹ab5ae20e5ff0de9d5e3b2fdd9346ed9f158cb4082a05933e4d6b074cd7333047；客户端GUID1e9cea464e46478d9193c121b4ce3534。源码ZIP快照为8040f716c0f79a44b5876d3583a1ad1a8c05a90e；两提交的Assets、Packages、ProjectSettings和Tools逐项相同。此后追加云端交付回执和说明的Git提交，不改发行包、客户端或运行输入，避免循环把包自身哈希写入源码包。

完整1364/1364回归通过，三章当前原生通关、侦察途中检查点新进程恢复和守港实际录像从0前向4×到1243步验证成功。[070验证](Verification-070.md)、[操作](Player-Guide-070.md)、[限制](Known-Issues.md)。原生审计75项通过，两个自有进程实际QUIT正常退出0；三次失败尝试仍保留。

冻结清单SHA256为592bf7fd08837a4639cec241d3835efed863dbcd9e56c0a0115b1fedb26205bd；实际ZIP验收回执SHA256为f9562f1a919c38264872c0f9bced2829ea7aa3435ca968121b6d0c3c0f7049dc。清单生成时的independentZipVerification=pending保留其时序，最终Passed由单独回执确认。本地三ZIP SHA在上传前后不变，未下载远程内容重新计算SHA；元数据读回不替代远程内容哈希证明。

交付原件见[Delivery证据](Evidence/070/Delivery/)。开发分支仍为codex/land-rts-completion，既有[PR1](https://github.com/freezeng123456/unity-mimic-aoe/pull/1)保持草稿；main和共享范围不改变。东渠、海撤、普通自然战争、两台物理Mac、长窗性能及发行签名公证仍待验收。
