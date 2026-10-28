# D规模与长性能取证计划（冻结0.2.31）

只准备CLI/归档，不运行Unity或客户端，不操作当前玩家局。B/C由native_player继续独占；其结束后再执行下列D操作，保持**单原生客户端、无并行Editor**。当前357源文件及3项compiled runtime与055原始测试/构建receipt完全匹配：版本0.2.31，GUID `128a731d440e4b7a94abfcc74b88fa97`，源码FP `1b45f97f3d90c9b76388419492a83c5622ccb2cf3a365030044403ccd309c1c7`。此处是准备结果，不表示规模验收已经完成。

## 参数和生命周期

| 样本 | CLI参数 | RuntimeBenchmark暖机/保存 | F8计划与归档门槛 |
|---|---|---|---|
| 50 | `-frontierBenchmark 50 -frontierBenchmarkSeconds 25` | 初始/结束目标各50；总墙钟25s，前5s不入规模均值 | 约6s开始F8，25s自动暂停后F8停止；短诊断，不冒充300s |
| 100 | 同上，数量100 | 同上，目标100 | 同上，独立场次 |
| 200 | 同上，数量200 | 同上，目标200 | 同上，独立场次 |
| 400 | `-frontierBenchmark 400 -frontierBenchmarkSeconds 360` | 目标400；总墙钟360s，前10s不入规模均值 | 约10–12s开始F8，约325–330s先F8停止；**单窗口重算有效≥300s**，再等360s保存 |
| 正常玩家长窗口 | 普通菜单/正常开局，不带benchmark/autoplay | 按实际正常局；不能替代400夹具 | 单F8窗口有效≥300s，不能拼接多个短窗口；另列验收 |

`RuntimeBenchmark.cs:17`把时长夹到25..600，>25时暖机10s，否则5s。数量直接使用CLI整数，指定50/100/200/400，不追加autoplay或auto-speed。夹具清掉所有初始存活非资源实体（包括TC/工人/守卫），交替生成Pike/Musketeer/Hussar/Dragoon/Cannon，双方各一半；运行HP提高到1,000,000，无AI，约1s后开始、以后每7s向两目标交替发AttackMove。这是有移动/混战的压力诊断，**不是正常资源对局或自然终局证据**。

规模统计取`Time.unscaledDeltaTime`与每帧模拟/导航/战斗/经济读数，保存时只保留聚合，不存其完整逐帧数组；规模报告的均值/分位数因此不能独立从规模报告重算。`NativePerformanceCapture`的F8才提供独立逐帧重算；两套均值的起止/开销不同，不要求相等。

`RuntimeBenchmark.Save`写持久目录`Benchmarks/units-N.json`，随后**直接暂停World并请求F9截图**；它不写match report，不退出，也不因暂停自动结束F8。F8只统计当前/前帧均focused且unpaused、无失焦回调/暂停过渡的区间；实际自动暂停可由paused状态识别。400档若忘记先停F8，后面暂停帧仍保留但不贡献时长，解析器必须如实判定300s是否满足。F8自身最多600墙钟秒/72000帧，不能以600墙钟或基准360参数替代有效秒数。

## 精确运行顺序（由root后续执行）

1. 等B/C自然结束并归档，正常退出该客户端；确认Editor也已退出。固定本次窗口分辨率，保留实际报告值；不把2560×1440启动请求当作实际渲染分辨率。
2. 每次仅执行下面**一条**launch命令。不要shell循环连续`open`，否则会并行多客户端。50→100→200各一次短诊断，400一次360s诊断；失败另用attempt-002保留，不覆盖。
3. 客户端前台，在暖机后按F8开始；短档等待自动保存/暂停再F8结束。400按上述时间先结束F8，继续保持前台让夹具运行至保存。若失焦/暂停，原始帧保留，300s不足就保留失败另跑。
4. 等到规模JSON、F8JSON、自动F9的JSON/PNG全部完成（F9 JSON是完成凭据），再正常退出应用或Return To Menu后退出，获得同matchId的`quit`或`menu_exit`最终report/events。夹具无需Resign，不把此退出叫自然结算。
5. 客户端完全退出后指定确切文件归档；立即完成本档归档再重跑相同数量，因为`units-N.json`会被下一次同档覆盖。不得选其他GUID/当前运行局报告凑数。

```bash
FR_PROJECT='/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project'
FR_APP="$FR_PROJECT/Builds/macOS/Frontier Reckoning.app"
FR_DATA='/Users/Apple_501/Library/Application Support/Frontier Workshop/Frontier Reckoning'
mkdir -p "$FR_PROJECT/Builds/Scale055"

# 以下四条每次只执行一条，完成→退出→归档后才能执行下一条。
open -n "$FR_APP" --args -frontierBenchmark 50 -frontierBenchmarkSeconds 25 -screen-fullscreen 0 -screen-width 2560 -screen-height 1440 -logFile "$FR_PROJECT/Builds/Scale055/units-050-attempt-001.log"
open -n "$FR_APP" --args -frontierBenchmark 100 -frontierBenchmarkSeconds 25 -screen-fullscreen 0 -screen-width 2560 -screen-height 1440 -logFile "$FR_PROJECT/Builds/Scale055/units-100-attempt-001.log"
open -n "$FR_APP" --args -frontierBenchmark 200 -frontierBenchmarkSeconds 25 -screen-fullscreen 0 -screen-width 2560 -screen-height 1440 -logFile "$FR_PROJECT/Builds/Scale055/units-200-attempt-001.log"
open -n "$FR_APP" --args -frontierBenchmark 400 -frontierBenchmarkSeconds 360 -screen-fullscreen 0 -screen-width 2560 -screen-height 1440 -logFile "$FR_PROJECT/Builds/Scale055/units-400-attempt-001.log"
```

本代理没有执行这些launch命令，也不通过脚本替代F8 GUI操作。启动参数请求窗口模式与分辨率，具体窗口/焦点由root实际确认；源代码focus-loss策略对benchmark不自动暂停，所以窗口失焦会继续消耗360s，却不增加F8有效时间。

## 复用归档与校验

复用既有 [`../verify_performance.py`](../verify_performance.py)，新增薄封装 [`archive_scale_capture.py`](archive_scale_capture.py) 只处理**已完成真实原生输出**，不启动游戏、不造样本、不控制GUI。它核357源/compiled runtime与055receipt、非零原GUID、版本/场次/分辨率；原始Spawn/Death前缀及每笔memory必须保持请求N、双方各N/2、中立/建筑0；关联自动截图JSON/PNG SHA；再调用原F8解析器。400自动指定有效≥300s，短档只作短诊断。

按实际已完成文件替换以下三个placeholder（F8 JSON、最终report、自动F9 JSON），不要通配符任选最新文件：

```bash
python3 "$FR_PROJECT/Docs/Evidence/055/Performance/archive_scale_capture.py" \
  --units 400 --duration-seconds 360 \
  --benchmark "$FR_DATA/Benchmarks/units-400.json" \
  --performance "$FR_DATA/PerformanceCaptures/EXACT-F8.json" \
  --match-report "$FR_DATA/Reports/EXACT-quit.json" \
  --capture-metadata "$FR_DATA/Captures/EXACT-F9.json" \
  --client-log "$FR_PROJECT/Builds/Scale055/units-400-attempt-001.log" \
  --archive-dir "$FR_PROJECT/Docs/Evidence/055/Performance/scale-400-attempt-001"
```

短档分别替换units为50/100/200、duration为25及对应原始文件/日志/空归档目录。events默认与最终report同stem，支持`.jsonl.gz`；显式`--events`可指定正确配对。封装拒写055 Performance之外/非空旧目录；所有失败检查和原始失败证据应保留，不能删除重录成通过。只验证CLI `--help`/语法和既有解析器工具回归，**未运行真实规模归档**；最终成功必须来自后续真实输出。

直接复用原F8工具时400命令为：

```bash
python3 "$FR_PROJECT/Docs/Evidence/055/verify_performance.py" \
  "$FR_DATA/PerformanceCaptures/EXACT-F8.json" \
  --match-report "$FR_DATA/Reports/EXACT-quit.json" \
  --build-guid 128a731d440e4b7a94abfcc74b88fa97 --match-id EXACT-MATCH-ID \
  --app-version 0.2.31 --allow-diagnostic --min-unpaused-seconds 300 \
  --archive-dir "$FR_PROJECT/Docs/Evidence/055/Performance/scale-400-direct-attempt-001" \
  --output "$FR_PROJECT/Docs/Evidence/055/Performance/scale-400-direct-attempt-001/f8-audit.json"
```

`--allow-diagnostic`仅允许**benchmark=true、autoplay=false、报告与match均1x**；摘要仍`diagnostic=true / normalPlayerCapture=false`，这一项false保留，只对诊断验收排除它，其余身份/帧/内存/事件/时长检查全部要求通过。不允许autoplay16x。正常玩家300s使用同工具，**省去allow-diagnostic**，与本人的最终report/events配对、另放`normal-long-*`；不得拿400压力夹具代替玩家性能证据。

最小闭环原始文件：`units-N.json`、同局F8原始JSON、最终原生report、完整同stem events（或gzip）、自动F9 JSON/PNG，加解析工具、Rules快照和055源码/构建receipt；可保留单次已退出客户端log。归档含逐文件SHA与完整审计。不要只交汇总或复制旧0.2.30短矩阵。

单位数是实际存活实体数，不是population/pop reservation、摄像机内数量或已渲染无遮挡模型数。`viewportVisibleUnitCenters`仅表示当时允许迷雾可见且单位中心落在视口、z>0；解析器只验证0..alive范围，events不能独立复算相机/迷雾/遮挡。即使值400，也不证明全部400无遮挡、HUD外或完整外形同屏，不追加这些门槛。

来源定位：`Assets/Frontier/Presentation/RuntimeBenchmark.cs:15–66`、`NativePerformanceCapture.cs:54–185`、`GameController.cs:95–115/142/199–220`；本计划不修改冻结代码。性能报告须陈述实际mean/floor P95/P99、内存/GC与样本时长，不能以无报错推导固定帧率、平衡、跨设备或无限长稳定性。
