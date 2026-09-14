# 034 原始证据索引

最终客户端：0.2.13，Build GUID `ae1dace836c84d79b94d291667478592`。这些材料支持本轮限定范围，不构成完整游戏验收。

- `ResetFocus-baseline-final.xml` 是旧运行时同十例的 7/10 结果；`ResetFocus-green.xml` 是修复后 10/10。对应冻结测试源码保留在本目录，旧运行时两个脚本以 `-before.cs` 命名。它们位于 Docs 中，不参与 Unity 编译。
- `Isolation.xml` 为筛选集 4/4，`EditMode.xml` 为全量 110/110，`PlayMode.xml` 为全量 129/129。早期夹具错误没有算入权威红绿结果。
- `build-receipt.json`、`build-input-hashes.json` 与 `Build.log` 记录最终构建、125 项运行时/场景/依赖输入及关键二进制哈希。日志中的本机路径仅为取证记录，工程脚本不依赖它们。
- `native-cli-quit` 是内部可执行文件启动后最小化恢复失败的局部样本；`native-finder-quit` 是 Finder `.app` 启动后恢复失败的样本。日志分别为 `NativePlayer.log`、`NativeFinder.log`。没有将任一失败改称通过。
- `native-normal-finished` 是正常 `.app` 入口、非 autoplay、1 倍速、Coast 镜像 III 上限的自然败局，803.750793 秒，26,481 事件。`NativeNormal.log` 截至这局结束；`NativeNormal-final.log` 还包括之后重开和退出。
- `native-normal-restart` 是结算页 PLAY AGAIN 后的新局，63.399605 秒、944 事件，之后暂停、返回菜单并退出；它不是第二场完整对局。
- `audit-native-034.py` 从旁边原始 JSON/JSONL 和日志重算 `native-normal-audit-generated.json`，包括实际计数、输入文件 SHA-256 和统计边界。在本目录运行 `python3 audit-native-034.py` 可复算。该脚本不操纵游戏、不创建资源，也不是游戏运行依赖。

PNG 来自实际客户端 F9 新生成的 2560×1440 图像；JPEG 来自桌面抓图。`normal-F9-attempt.jpeg` 与 `normal-native-capture.png` 保存了桌面发白、客户端截图正常的边界，不能相互替代。`normal-resumed`、`normal-second-resume`、`normal-after-continuation` 及后续图结合原生日志核对三次暂停/恢复。

`normal-barracks-placement-result`、`normal-rebuild-after-raid` 和 `normal-safe-barracks-placement` 保留建造尝试与被毁重建过程；文件名不保证尝试成功，应以原始事件确定。人口显示疑点的四帧是 `normal-save-barracks-repair`、`normal-release-recover-economy`、`normal-food-repair-recovery`、`normal-redirect-hunters`。`normal-match-end-check` 是实际败北页；`normal-restart-*` 和 `normal-main-menu-after-restart` 是随后清理检查。

未包含临时探针 app、错误夹具测试副本或混入旧局数据的手工审计草稿。已有较早版本证据仍保留在各自编号目录，不能当作本 GUID 的当前证据。更完整的说明见 [Native-Verification-034.md](../../Native-Verification-034.md)。
