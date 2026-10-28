# Frontier Reckoning 0.13.0

Mac Apple silicon 开发客户端。先解压整个包，再打开 Frontier Reckoning.app；应用目录内文件和执行权限需要保留。当前版本是Development/Mono构建，未完成发行签名公证。

主菜单 MAP, SEED & OBJECTIVES → RIVER EXPEDITIONS：选择Traders’ Bend开始正常经济对局；或进入双桥侦察、渡湾护送、守港回撤。远征首章直接开放，其后按真实完成记录解锁。旧七课与原三章战役保留；EDIT COPY进入工作坊编辑副本，试玩副本不获得解锁。

拖框选兵，右键可见敌军/建筑指定攻击，右键地面移动；A后点击为攻击移动，J驻守。切换近战/远程影响火枪兵对骑兵的克制。Minimap左键聚焦、右键移动；暂停不会推进战斗。

选军队，BOARD后点运输舰，等待实际登船；选舰，UNLOAD后点开放陆地岸线。护送需要指定载兵舰连同至少四名指定兵进入水域；卸载后仍须实际走入岛内。护航建议炮艇先截敌艇。

守港需要至少四名指定后卫连续40秒在标记内，敌人进入会清零计时；保住港口。近战应对骑兵，远程集火步兵，战后收拢并驻守；完成后可走桥或海运回营。本次原生验证走西渠护航和桥路撤退，东渠/海撤仍需独立试玩。

本包Examples含原生导出的Traders’ Bend副本，Recordings含此构建的真实守港WIN录制文件。从主菜单 RECORDING LIBRARY 读取同构建录像，录像和个人存档/进度分开。库会严格检查嵌入场景与状态，多个记录时本次出现分钟级读取等待。

本批967 EditMode+397 PlayMode全部1364通过，三章原生通关，侦察途中检查点在新进程恢复，守港录像从0完整前向到1243步验证。完整当前限制和原件见源码包Docs/Verification-070.md、Docs/Known-Issues.md。没有宣称AoE III整体复刻完成、长局性能/双物理Mac或跨构建文件兼容。


## Frozen packages

| File | Bytes | SHA256 |
|---|---:|---|
| Frontier-Reckoning-0.13.0-source.zip | 33648339 | 05c33877082f8b598520300732a0c8df34c06e060c679265d62ab72b1f295a2e |
| Frontier-Reckoning-0.13.0-macOS-arm64.zip | 76067591 | f65eb3545630fb246f017802313459ab87aec12d772a285273908a4889c7872f |
| Frontier-Reckoning-0.13.0-native-gallery.zip | 14290591 | 24f8569da1a3cb12055c92429c2e137cf3b8aa119942f45e6e2660f5fccca78a |

Source snapshot: 8040f716c0f79a44b5876d3583a1ad1a8c05a90e

Runtime: 8b6598d0939262571d0a595ba1499e053f00e1b7

682 inputs, fingerprint ab5ae20e5ff0de9d5e3b2fdd9346ed9f158cb4082a05933e4d6b074cd7333047

Independent ZIP acceptance: Passed, 1491 members. Actual Native audit: 75/75 passed.

Private release folder: https://drive.google.com/drive/folders/13SGjI5BVUbaT3pRwjOSOn1gIMcEaKQ7J

Draft PR: https://github.com/freezeng123456/unity-mimic-aoe/pull/1

Remote bytes were not downloaded for independent SHA256 recomputation; metadata verification is recorded separately.
