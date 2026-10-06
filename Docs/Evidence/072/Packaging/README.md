# 072 封装工具与严格原生 schema 审计归档

本目录仅逐字复制9份 outside Diagnostics 原件，源前后与副本bytes/SHA/mode一致，见[来源与复制索引](packaging-originals-copy-index072.json)。它没有生成最终seal、实际交付ZIP、执行矩阵、上传、提交或push。运行来源仍是bc127、04698/718，GUID3288。

[新工具](package072-final1.py) SHA `8144e8f5bb75c88e6f77dfedb15fb7f52a7384a27fe4f5845e5655da67b53f4c`。独立[审计JSON](072-package-native-parser-final1-review.json)和[说明](072-package-native-parser-final1-review.md)确认：实际最终journal的9个声明范围、5个exit0进程、116原件、40对F9和选定像素记录通过严格schema检查；原journal不改成工具预期的成功字段。只对validated relativePath原件作精确path/bytes/SHA投影，旧hash reader保持原样；partial像素仍为partial事实，H Verified=false短段600→1111仍保留，不扩为整段录像、全像素、FPS或LAN认证。

| 对照与检查 | 原件 | 范围 |
|---|---|---|
| 原prepared工具及27合成自测 | [工具](package072-prepared.py) · [原回执](package072-tool-selftest.json) | 原字节保留；供新parser对照 |
| derived工具同27项合成自测重跑 | [回执](package072-final1-selftest.json) | 与旧27同款，不另算54种fixture |
| 新45项logical schema fixtures及真实journal检查 | [独立审查实现](review-package072-native-parser-final1.py) · [JSON](072-package-native-parser-final1-review.json) | 包括缺失/重复scope、false/pending、错PID/audit/plan、unknown像素/capture、原树遗漏和不稳定原件 |
| 原非native部分保持 | [审计JSON](072-package-native-parser-final1-review.json) | 38个原函数/类AST相同；source/fulltests/build前置部分与完整matrix gate原字节不变 |
| 固定独立矩阵检查器 | [independent-normal072.py](independent-normal072.py) | SHA `8579ec7a2bfdda5987c830a29bebfd94c1c0c17fc58b04af93f710c63661858c` 未变 |

原27与新增45属于封装工具的合成/逻辑fixtures，不能并入正式1567项Unity回归。审计回执时点完整16局矩阵仍running，derived工具实际拒绝该未完成runner；通过native prerequisite不让矩阵或其它门槛绕过。当前整体完成状态见[Completion-Status](../../../Completion-Status.md)。

[package072-usage.md](package072-usage.md)标为 **historicalpreparednotes**：它写于早期预备阶段，原文关于“没有完整最终测试/原生验收”以及旧journal schema期待值属于当时状态，均保持原字节，不作为现在的完成状态或新工具使用说明。这里保存的是可审阅原件副本，正式封装负责人使用原 outside 工具和实际冻结输入。

## final2：完整矩阵引用 schema 的严格适配

上文首次归档的9份原件、final1工具与审计时点作为历史记录保留。本次仅新增6份 outside 原件副本及[复制索引](package072-final2-originals-copy-index.json)，逐字bytes/SHA/mode匹配，源前后稳定。运行矩阵已自然闭合：16局证据一致，9条请求路线达成、7局在路线达成前自然结束；6次革命均完成重建与平民经济恢复，3次帝国路线完成。只有5局达到2400秒共同观察点，11局使用更早自然终局观察点。独立验收器结论为 Consistent / observed_routes_incomplete / Not assessed，不能写为16条路线全部成功或平衡性通过。

[final2工具](package072-final2.py) SHA `87d9670e315770da1fc5ca9d194a0d34a0c9c1011aec3c5f30af4b0d50b4db76` 从final1原字节派生，只替换两处将 filesUnchangedDuringAudit 误读为布尔值的门槛，并新增严格引用校验函数。固定验收器实际返回844条引用，均精确核对path/bytes/SHA/6项filesystem identity、普通文件类型、无重复、完整命名空间、sealed输入及718份冻结源码SHA、复读稳定性；布尔true也被拒绝。固定验收器SHA、复读subprocess参数与全部语义报告比较key不变。

[独立AST与schema审查](072-package-matrix-parser-final2-review.json) · [说明](072-package-matrix-parser-final2-review.md) · [审查实现](review-package072-matrix-parser-final2.py) · [精确diff](package072-final1-to-final2.diff) 确认38个其他既有函数/类AST相同，逆适配可逐AST还原两个修改函数；source、full1567、build、native限定范围、inventory和ZIP门槛均保留。真实844引用正例与36项新增logical fixtures通过，包括引用缺失/重复、错误字节或哈希、inode/ctime变化、错误路径/模式、布尔值替代、错误sealed引用、覆盖缺口及稳定性失败。

[final2合成自测回执](package072-final2-selftest.json) 重跑的是原27项，不能与Unity1567项相加，也不能将final1/native45与本次matrix36误算为游戏回归。旧prepared/final1工具、旧审查与旧自测都保留原字节。本目录保存可审阅副本；正式封装仍使用outside Diagnostics中的final2原件、真实冻结输入和另行审核的最终seal。此次归档没有生成seal、交付ZIP、上传、提交或push。
