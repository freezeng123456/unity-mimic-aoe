# 0.3.1 最终客户端菜单布局回查：零对局

最终客户端实际 GUID **`c1e7fe583e4f4e288cfa4fcd9885a543`**，版本 `0.3.1`、bundle build `0`，Unity `6000.6.3f1`，原生 1600×1000。身份见 [最终 build receipt](../build-receipt.json)。本目录仅归档该新 GUID 的 **1 组 F9 JSON/PNG 和 1 份 `menu.log`**；报告、事件和唯一对局数均为 **0**。

初次 GUID `11d4fafce22a42ebb37dac73f0ef89fb` 的两局 16× 法奥双向自动对战另存 [Native-Initial](../Native-Initial/README.md)，它们属于初次构建，不能计为本目录新 GUID 的原生对局。初构建普通 1× 开局因鼠标 CUA `noWindowsAvailable` 未完成，最终菜单回查没有补成普通开局或鼠标点击验收。

## 实际菜单修复画面

[最终奥斯曼菜单 F9](Captures/Frontier-20261003-174836-645-f1157-74b87feade5f48068bb09b97cdcd3384.png) 的对应元数据时间为 `2026-10-03T17:48:36.6038560Z`，尺寸为 1600×1000，`inMenu=true`、`matchId` 与 `requestMatchId` 均为空，`worldTime=0`。已独立查看该保存 PNG：地图及 seed 行完整，奥斯曼简介完整显示为三行；说明文字结束后保留空隙，PRESET / EDIT DECK 已下移，文本没有覆盖按钮。修复前实际溢出画面保留在初构建目录，便于对照。

此次仅确认奥斯曼菜单在实际上述尺寸下的布局修复。主执行代理通过 CLI 选定奥斯曼，未开启 autoplay、未 StartMatch；F9 后 Cmd+Q，随后通过 shell 确认该最终 app 进程数量为 0。这些操作与退出事实来自主执行代理记录，归档脚本只读取已完成的原始文件。

[menu-equivalence.json](../menu-equivalence.json) 独立比较初次与最终源码，记录只有 `FrontierHud.cs` 和对应 PlayMode UI 回归测试变化，其余规则、模拟与 AI 来源逐字节相同；它证明该显示改动的范围，不能把初构建执行的矩阵或原生局重新标记为最终 GUID 实际运行。六文明文字完整性与按钮间距的自动回归另由最终测试材料支持，本目录没有六文明逐项原生点击截图。

## 文件与原文核验

采用工具的显式 `--menu-only`，严格拒绝匹配 GUID 的对局报告或含 live match 身份的截图；以精确 `menu.log` 筛选本阶段日志，没有混入初次两份 autoplay 日志。原始截图 JSON、PNG 和冻结日志原字节保留，PNG SHA 与元数据、图像尺寸均需一致。`summary.json` 重算菜单零对局范围；`manifest.json` 保存所有实际文件字节数与 SHA，`manifest.sha256` 校验清单自身。

独立复核及原始源字节比较：

```sh
python3 Docs/Evidence/058/Native/archive_native.py verify \
  --source "$HOME/Library/Application Support/Frontier Workshop/Frontier Reckoning" \
  --log-source Builds/Native-0.3.1-menu-fit
```

`menu.log` 仍保留退出时 6 条 `may have been prematurely finalized` 和 1 条 `MemoryLeaks` 诊断，不宣称诊断已解决、无泄漏或无警告。此次完成的是新客户端菜单布局回查，不是普通 1× 对局、手动 I–V、鼠标点击、最终 GUID 的自动整局、统计平衡或正常速度性能验收。

通用工具包含 9 项临时合成数据回归，检查严格 JSON、原文/gzip/PNG、生命周期、资源实体分类、唯一对局计数、菜单零对局与错误 live 身份拒绝、失败恢复时不同字节拒绝。合成测试资料没有作为本目录原生证据保留。
