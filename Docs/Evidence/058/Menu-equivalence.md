# 058：菜单修正与模拟证据的精确对应

最终版本完整通过 **527/527** 测试（EditMode 284、PlayMode 243）。初次矩阵使用的是修正前源码；两者指纹不同，本说明不把旧矩阵冒充最终指纹上的重新运行。

| 证据 | 源码指纹 | 结果 |
| --- | --- | --- |
| Tests-Initial 与原 36 场矩阵 | `febbf21f9a29749df55445ba48fc0855179f169af51a9b68689212daef0ddf1d` | 初次测试 526/526；矩阵 35/36 自然结束，保留 1 场超时 |
| Tests 与最终菜单修正 | `f49e6c08142d2b9513fe6e878a6e3df58ae3f802d1fbafd2078ba82e41b60577` | 最终测试 527/527 |

[自动核验证据](menu-equivalence.json) 对两轮完整测试源清单逐项比较，每轮均为 **385 个文件**：

- 仅两个确切路径变化：`Assets/Frontier/Presentation/FrontierHud.cs`、`Assets/Frontier/Tests/PlayMode/ClassicCivilizationUiTests.cs`。
- 其余 **383 个文件逐字节相同**，没有新增或删除路径；其中包含 Simulation 目录全部 **52 个源码及 meta 条目**、全部 AI 和 `Assets/Resources/Rules.json`。没有忽略整个目录来消除差异。
- 原有 EditMode 284 项、PlayMode 242 项测试身份均保留；只新增一项六文明简介完整渲染及按钮间距回归。
- 两个变更文件均从初次提交 `79d88e290cad42ecd88082cd5e55e8b613f56ae2` 与最终提交 `4d2f24afecfd9155bff4661837fbf0e407878d38` 读取原始 Git blob，核对各自测试清单 SHA-256；完整 unified diff 保存在 JSON。
- 原矩阵运行前、运行后两份完整清单均与 Tests-Initial 逐项一致，矩阵记录的测试凭据 SHA-256 也与归档原件一致。矩阵仍绑定初次指纹及原 **35/36** 结果。

实际 HUD 差异为简介区域高度从 80 调整到 104、增加 14–17 字号适配与换行，并将四个下方按钮下移 24；描述内容和按钮回调保持原样。新增回归按实际像素缩放生成未裁切全文，检查六文明文字均落在简介区域内并位于卡组按钮上方。

因此，原规则／模拟／AI 矩阵可作为未改变部分的行为证据，最终完整测试和新原生客户端验收覆盖菜单修正。这里没有宣称在最终全局指纹上重跑过原 36 场矩阵。高地镜像独立延时复核详见 [Matrix-analysis.md](Matrix-analysis.md)，仍与原始超时记录分开。

验证脚本为 [verify_menu_equivalence.py](verify_menu_equivalence.py)。复算时指定新的输出路径以保留现有证据：

```sh
python3 Docs/Evidence/058/verify_menu_equivalence.py \
  --final-commit 4d2f24afecfd9155bff4661837fbf0e407878d38 \
  --output /tmp/frontier-menu-equivalence.json
```

脚本还验证两轮测试 XML 的哈希、完整通过状态，以及当前工作区完整源码是否与最终测试清单相同。构建期间首次检查遇到临时清单差异时已拒绝生成；Unity 构建退出、源码复核一致后才生成本次有效证据。
