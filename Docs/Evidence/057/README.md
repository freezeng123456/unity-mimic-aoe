# 057 — 0.3.0 四文明首批内容证据

本目录保存新文明内容、AI 调度修复、480 项自动回归、独立 macOS 构建及原生客户端观察。它是新的内容版本，不能通过 056 的菜单等价证明继承旧版完整玩法、音频或性能验收。

## 当前版本身份

| 字段 | 值 |
| --- | --- |
| 应用版本／内部构建号 | `0.3.0`／`0` |
| 核心源码提交 | `d7bc04b93b256a13d4bdfa7c9633a010a19428df` |
| 本次源码指纹 | `538daf84e551da74bfc1be2cb9ee6d8726016fa1ec440c90363aa06ed7af0b2f` |
| Unity build GUID | `e6f13b0328df478cb19cb1bc3989dc5b` |
| Unity | `6000.6.3f1`，revision `45d8eee7de74` |
| 客户端 | macOS arm64，Development／Mono；299 文件，173,180,719 字节 |

完整客户端运行库 SHA 与构建信息见 [build-receipt.json](build-receipt.json)。最终归档提交可晚于核心源码提交，以打包时外部 manifest 的实际 HEAD 为准。

## 证据入口

| 文件或目录 | 内容与使用范围 |
| --- | --- |
| [Tests/source-evidence.json](Tests/source-evidence.json) | 当前 373 个源码文件指纹，以及全量 254 EditMode＋226 PlayMode 的报告 SHA |
| [Tests/EditMode.xml](Tests/EditMode.xml)、[Tests/PlayMode.xml](Tests/PlayMode.xml) | 最终 480 项全部通过，无失败、跳过或不确定项；原始日志在同目录 |
| [Matrix/matrix.json](Matrix/matrix.json) | 最终源码下四文明有序组合 16 局，前后指纹相同，16／16 自然结束 |
| [AI-Comparison.md](AI-Comparison.md)、[ai-comparison.json](ai-comparison.json) | 修复前后交易耗粮与生产恢复对照；逐局事件核验、实际付费兵种和机制覆盖 |
| [Matrix/compressed-events.json](Matrix/compressed-events.json) | 原始事件的无损压缩映射；核对原始事件 SHA 时先解压 `.jsonl.gz` |
| [Build.log](Build.log)、[build-receipt.json](build-receipt.json) | 直接 BuildMac 的构建日志、源码／测试绑定、实际版本、GUID 和运行库身份 |
| [Native/README.md](Native/README.md) | 新文明双向 16x autoplay 原生自然结算，以及单独记录的普通 1x 手动开局范围、截图、事件和日志 |
| [client-bundle-manifest.json](client-bundle-manifest.json) | 以实际生成文件为准：本地 app 与客户端 ZIP 的文件集合、bytes、SHA、Unix mode、目录及 GUID／版本核验 |
| [build_client_bundle_manifest.py](build_client_bundle_manifest.py) | 057 manifest 生成器；复用已保存的通用库存与归档校验，不构建、不伪造客户端 |

## 矩阵实际验证到什么

最终矩阵使用固定地图、seed `314159`、Standard、V 时代上限，经济与军事预设对阵。16 局分别有唯一 MatchEnded、零 Resign、败方没有存活单位或建筑。每局原始日志与结果保留，结论不通过跨局拼接伪装成某一局的完整覆盖。

机制累计观察包括 469 次付费装货／交货、14 次预制件制造完成及 12 次使用；工程兵有 738 条按模拟步记录的实际修理付款，累计恢复 1323 生命。243 次架盾命令代表使用能力，不代表 243 次有效挡伤。

五种新兵有真实付费训练完成；**AI 付费猎炮排队与完成是 0，18 次猎炮出生来自奖励**。付费猎炮生产和工人约束由机制测试覆盖，矩阵的奖励单位不能代替这项普通 AI 覆盖。山岭没有在本次矩阵到达 V；时代上限、科技定义齐全与实际晋级覆盖分开报告。

这不是统计平衡、多地图测试、每文明完整人工 I–V 验收，也不是本版音频或性能证明。原生两场完整局明确使用 **16x autoplay**；普通开局按其实际动作与持续时间单独记录，不能改称完整人工对局。

## 修复前记录保留

- [First-Test-Run](First-Test-Run)：首次 EditMode 249 项通过；PlayMode 226 项中 5 项失败。4 项旧船运 UI 用例依赖旧卡组槽位，另 1 项制造用例遗漏 Standard Jigs 前置；保留原报告，在明确合法卡组和通过真实研究完成前置后复测，未删去原有禁止误发和付款断言。
- [Pilot-Before-Ai-Fix](Pilot-Before-Ai-Fix/matrix.json)：最初两局诊断；与修复前矩阵对应两局日志一致，不重复计算为独立样本。
- [Matrix-Before-Ai-Fix](Matrix-Before-Ai-Fix/matrix.json)：修复前 16 局矩阵，展示远驿贸易货本压制村民／军队生产的路径。
- [Tests-Before-Ai-Fix](Tests-Before-Ai-Fix/source-evidence.json)：旧源码指纹的测试，不能替代最终 Tests。

修复只调整远驿 AI 预算与空车启停；含远驿的七个组合重新表现出不同生产路径，其余九个组合事件逐字节一致。最终结论只使用 `538daf…0b2f` 指纹对应的 Tests、Matrix 和新客户端身份，具体差异见对照文档。

## 本批交付范围

已实现两个新文明、六种新战斗兵、特色经济机制、48 个晋级选项、扩展卡池、UI、AI 和配套外观。新增地图、中立势力、任务场景及后续两个文明未实现；存档、联网、海战、其他平台和跨机器签名／公证没有验收。详见[内容说明](../../Content-Expansion-057.md)及[已知限制](../../Known-Issues.md)。

[055 打包器](../055/package_delivery.py)使用 `--evidence-set 057 --playtest-guide Docs/Playtest-057.md` 绑定最终干净 HEAD，核对所有源码 Git blob、本次源码 SHA 和客户端 manifest。仓库外的最终 `manifest.json` 才记录实际生成包的大小、SHA 和提交；索引本身不宣称尚未生成的文件已经核验。
