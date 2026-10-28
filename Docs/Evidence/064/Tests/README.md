# 0.7.0 最终完整测试 — run-LOUCjHNW

本目录归档最终冻结输入的 **796/796 Passed**：EditMode 466/466，PlayMode 330/330，failed/skipped/inconclusive 均为 0。结果与耗时直接读取 NUnit XML；这份证据只确认自动化测试与当前源码一致性。原生构建、进程退出后冷启动载入、实测性能、ZIP 和云端交付仍须各自的独立回执。

- 原始运行目录：`Builds/Tests/run-LOUCjHNW`。
- 运行源码提交：`5cce74ccf7222133af9bad633923f452b100bfb2`；归档时 `Assets`、`Packages`、`ProjectSettings`、`Tools` 的已跟踪文件与此提交无差异，497 个当前输入文件与测试清单逐项一致。
- 源码指纹：`8342b85bf835fd48d5c4dfc8f612ed64770fc5b7d026d96c4f539caaa96d79b0`。
- 原始源码回执时间：`2026-10-04T19:55:34.512845+00:00`（UTC）。测试结束为香港时间 2026-10-05 03:55:32。
- 执行入口：`Tools/test.sh`；本次归档未启动 Unity 或重新执行测试。

| 模式 | 通过/总数 | 失败 | XML duration | XML UTC 起止 |
| --- | --- | --- | --- | --- |
| EditMode | 466/466 | 0 | 118.9930545 s | `2026-10-04 19:51:19Z` → `2026-10-04 19:53:18Z` |
| PlayMode | 330/330 | 0 | 125.2645935 s | `2026-10-04 19:53:27Z` → `2026-10-04 19:55:32Z` |

全套 XML 包含真实敌情中项删除后新增的冷恢复继续等价、收入事件顺序及工业阶段存读档回归。`ImplicitPursuitKeepsItsNullOrderRecoveryQueueAndColdContinuation` 也在本次 EditMode XML 中为 Passed；其正常隐式追击产生 FIFO 队列后冷恢复 21 Tick 的定向验证已进入本次最终完整回归。

## 当前源码复核

归档后执行以下只读校验，exit code **0**，输出保留在 [verify-current-source.txt](verify-current-source.txt)。该校验逐项核对当前源码清单和指纹，验证 XML 哈希及全量通过结果，并检查已有项目交付静态条件；不替代原生验收。

```sh
python3 -B Tools/verify-delivery.py --reports-dir Docs/Evidence/064/Tests --require-current-source
```

## 文件与哈希

XML 和 `source-evidence.json` 与原始运行目录逐字节一致。日志采用脱敏副本，名称明确带 `.sanitized`；其哈希对应脱敏后的文件。

| 归档文件 | 字节 | SHA-256 |
| --- | ---: | --- |
| [EditMode.xml](EditMode.xml) | 382,092 | `5b2b3220bcf6a7836207c4c1695403473d9cc14733d6e46b08df043c53de6060` |
| [PlayMode.xml](PlayMode.xml) | 299,193 | `ee496a832dce3d68e130b7c0ef875c32af39d93f4d03c9e3b00f965cd49662de` |
| [source-evidence.json](source-evidence.json) | 63,692 | `d21c660852280aa7262b33f668f8f6b4d8343dcf9144cd0143a0308c8bb23a37` |
| [EditMode.sanitized.log](EditMode.sanitized.log) | 70,896 | `f28dbd58089c38adbba1bf8ad3829ef63dbb7dec85aa17c7cd2d212819f3e9f5` |
| [PlayMode.sanitized.log](PlayMode.sanitized.log) | 487,539 | `23ee120a910b0c1efafa21c1dd1976c42186e28c5b4360014b09ecb74c11104c` |
| [verify-current-source.txt](verify-current-source.txt) | 681 | `830fb73e5e5adcbf4ccfea17d24cf90d88579c47fd4237185d117ddb0a1df5a1` |

## 日志脱敏与来源

移除完整 Licensing/许可证/授权相关行、凭据模式命中行及 PEM 私钥块，保留其他字节和原换行。原始日志只留在本地 `Builds`，没有复制到本目录。原始 SHA 仅用于本地溯源。机器可读的 [hash-verification.json](hash-verification.json) 保存规则、每条移除行的一基原始行号及分类，不保存被移除的内容；脱敏结果再次扫描上述模式为 0。

| 模式 | 原始字节 | 原始行数 | 移除行数 | 保留行数 | 本地原始日志 SHA-256 |
| --- | ---: | ---: | ---: | ---: | --- |
| EditMode | 72,281 | 625 | 18 | 607 | `cb45b4bb8177f7733709bb9b809adfb51a4966a1e6671485fd79786145006421` |
| PlayMode | 489,048 | 3214 | 20 | 3194 | `37f54e884aacd579ca11c11378c15aebf91aecc7547573f43a0caf6acf683a37` |

`hash-verification.json` 只列实际证据文件，不包含自身哈希，也不承担最终分包验证回执。后续原生、构建和交付结果应由各自回执绑定本源码指纹。
