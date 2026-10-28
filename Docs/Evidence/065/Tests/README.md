# 0.8 最终完整自动化测试 — run-Wu2tGZSt

本目录归档 **958/958 Passed**：EditMode 618/618，PlayMode 340/340；failed、skipped、inconclusive 均为 0。数字、UTC 时间和 duration 直接读取本次 NUnit XML。这轮在原 952 项基础上加入 6 个受限压缩存档回归，并通过完整 Unity Test Runner；不沿用临时 Mono 替身的结果作为正式证明。本归档确认自动化测试及当前源码一致性，新客户端和真实长对局存档冷恢复仍须独立证据。

- 原始运行目录：`Builds/Tests/run-Wu2tGZSt`。
- 运行源码提交：`c4365e31dbea9dd7e5d5c253b661ee2d3a25879a`；归档时 `Assets`、`Packages`、`ProjectSettings`、`Tools` 的已跟踪文件与该提交无差异。
- 源码指纹：`d7bbe1e4f0e52210bf64b4787864449a438be87fb14952173cdd4d60d0563eb6`，531 个输入文件逐项匹配。
- 原始源码回执 UTC 时间：`2026-10-05T00:28:48.110552+00:00`。
- 本次归档 UTC 时间：`2026-10-05T00:33:08.759905+00:00`。
- 执行入口：`Tools/test.sh`；归档未启动 Unity 或重新运行测试。

| 模式 | 通过/总数 | 失败 | XML duration | XML UTC 起止 |
| --- | --- | --- | --- | --- |
| EditMode | 618/618 | 0 | 159.6913743 s | `2026-10-05 00:23:32Z` → `2026-10-05 00:26:12Z` |
| PlayMode | 340/340 | 0 | 140.7207245 s | `2026-10-05 00:26:25Z` → `2026-10-05 00:28:45Z` |

## 压缩存档验收范围

新写入使用 schema 2、`gzip-base64`；原始 UTF-8 payload 的 SHA-256、gzip CRC32/ISIZE 和真实单成员结束位置均校验。文件仍限 64 MiB，解压原文限 256 MiB，严格 UTF-8 和 canonical Base64；旧 schema 1 继续可读。新增用例覆盖完整 World/Simulation、实际登船乘员、控制组和事件的冷恢复继续；损坏头/尾、保持正确 footer 的 body 截断/垃圾、拼接成员、哈希、UTF-8、大小边界和失败写入保留旧槽位。66 MiB 重复事件文本是受控 codec 大小回归，不能冒称自然对局或原生 94,739 事件样本的验收。

## 正常开局运输与付款边界

两例为 `generated_strait`、Coast 对 Coast、Standard，只有玩家 0 的 AI 开启，对手实体保留。初始 I 时代 300F/300W/100C，时代 II 上限，1400 模拟秒截止，无资源、时代、XP、HP 或单位生成授予。实际部队步行会合登船、跨水航行并在无陆路相通的对岸分散卸载。

| 地图 seed | 结果 | 测试墙钟 duration | 实际登陆模拟时间 | 登陆船 ID | 已卸载人数 | 独立训练付款 | 细图尝试/失败 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 314159 | Passed | 1.170085 s | 506.422 s | 171 | 12 | 200 木 / 75 币 | 0/0 |
| 271828 | Passed | 1.309427 s | 504.022 s | 171 | 12 | 200 木 / 75 币 | 3/3 |

**付款与登陆是两项独立断言。** 测试观察运输船训练正常扣除 200 木 / 75 币，也观察真实登陆，但没有把训练队列产出实体 ID 与登陆船 ID 绑定；合法船运卡可能提供登陆船。不能称这艘登陆船的付费生产身份已证明。受控生命周期用例另行验证付费船产出与登卸载机制，不冒称正常双 AI 海战。

第二 seed 实际记录 3 次细图尝试与 3 次失败。NUnit duration 是测试墙钟用时，登陆时间是固定步长模拟时间，均不能换算成原生 FPS 或正常播放速度。

## 当前源码复核

归档后执行以下只读校验，exit code **0**。完整输出见 [verify-current-source.txt](verify-current-source.txt)，不替代新客户端及原生验收。

```sh
python3 -B Tools/verify-delivery.py --reports-dir Docs/Evidence/065/Tests --require-current-source
```

## 文件与哈希

XML 和 source-evidence 与原运行目录逐字节一致。Unity 日志只复制脱敏后的 `.sanitized.log`，原日志保留在本地 Builds。

| 归档文件 | 字节 | SHA-256 |
| --- | ---: | --- |
| [EditMode.xml](EditMode.xml) | 500368 | `52690f77490580e1c537bf43b2d3fe887b792a3462def917532ef5ed7c70bb31` |
| [PlayMode.xml](PlayMode.xml) | 308961 | `81b71bca87eeb0797cd11ae8b49c29b67747764aceaff556caa303cb28d91031` |
| [source-evidence.json](source-evidence.json) | 68093 | `37a0addd44931057f60113923dc488434346e686e1091d3a1072d14807bad6ce` |
| [EditMode.sanitized.log](EditMode.sanitized.log) | 75667 | `a69ea20ea07cb957eb37f57ec12dd5ec4cfd1c2fe6bc9c1e0fecfcf845b3c4fa` |
| [PlayMode.sanitized.log](PlayMode.sanitized.log) | 518952 | `be41e06205889490810904cd517c21dcea3332168556a908d54649a07d42cab4` |
| [verify-current-source.txt](verify-current-source.txt) | 681 | `761e0a12d7a8c312ad4225979af7917629369d8e4f5b2d2d87664e08c79df5a6` |

## 日志脱敏

移除许可证、授权、凭据模式命中的完整行与完整 PEM 私钥块，其余原字节和换行保留。[hash-verification.json](hash-verification.json) 记录每条移除行的一基原始行号和类别，不保存被移除内容；重扫上述模式均为 0。

| 模式 | 原始字节 | 原始行数 | 移除行数 | 保留行数 | 本地原始日志 SHA-256 |
| --- | ---: | ---: | ---: | ---: | --- |
| EditMode | 76939 | 680 | 16 | 664 | `4c6fe3e43d7198b7d14cc7b3d7c94871c7bb9afb31024f4a73295232d82033e2` |
| PlayMode | 520348 | 3422 | 18 | 3404 | `9ae54e4f56aa4835f10265eb5f865d8b756de3eb3ba6f026987b4621dd0ea71d` |

原 952 项归档逐字节保留在外部 `FinalCandidate/Diagnostics/Tests-before-save-compression-fix`，最初枚举歧义导致的编译失败保留在 `FinalCandidate/Diagnostics/Compile-failure-save-compression-qqupofV6`（无测试执行）。旧 GUID `ac217fb5b02c4077acce8f43dae02aab` 的截图、842 秒旧槽位和 1706 秒失败存档尝试均属于历史证据，不能冒称本轮新原生存档成功。回执不包含自身哈希，最终分包和上传回执独立生成。
