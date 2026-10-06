# 0.16.0 验证记录 · 073 限定验收

2026-10-07。当前runtime提交 `519d7d34a0be85f75ee19e53458c636d1ba0b3e4`；732运行输入（Assets、Packages、ProjectSettings、Tools）FP `9b92812a922adb69a5ae501925417d8eba8e24b836f496274ebc8ec8679ba03d`。正式完整回归与Mac构建已完成，三条普通I观察已实际关闭，当前GUID原生仅市场建造/付款/完成；原生兑换交互未完成，073交付仍待闭合。本页已记录正式测试、构建、三局观察与原生限定范围；私有封包/远端交付另见交付记录。[内容](Content-073.md)、[状态](Completion-Status.md)、[限制](Known-Issues.md)、[交付记录](Delivery-073.md)。

## 正式完整回归与构建

`Builds/Tests/run-9q8yTJKS`实际 **1192/1192 EditMode、419/419 PlayMode，共1611/1611 Passed**，Failed/Skipped/Inconclusive均0，每个test-case实际Passed。NUnit时长分别742.5196442s和225.8294612s；计时属于各测试输出，不能当原生性能。`source-evidence.json`的原XML SHA/计数与真实文件一致，732输入map与before-full冻结/current/runtime Git blob相同；文档编辑不改变运行输入。

| 正式原件 | SHA256 |
|---|---|
| EditMode.xml | `5f2f9ba07d2fa7b46d56a76aff4990a54aee3fdd8e3667670ccff27f06abd779` |
| PlayMode.xml | `cef0de7d19ac414bc2e5e648ac0eedb65aaae3d9145df97f48eff381b2269ffc` |
| source-evidence.json | `8b5a74f795570b317d710d9c640c03dc5aab1fd16184c256de22e190436b2a14` |

独立部分报告位于 `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.16.0/Candidate1/Diagnostics/independent073-audit.json` 与同名md，已经核完整回归。072旧1158Edit/409Play fullname全部保留，新增26Market+8StageEdit及10MarketUIPlay；名称保留不声称旧断言字节完全未改。Full/Build已独立核验；普通三case另由closure073-normal.json/md记录实际审计，保留检查器schema修正历程。

Build1通过 `Frontier.Editor.BuildTools.BuildMac` 实际构建：Unity6000.6.3f1、macOS arm64 Development/Mono、version0.16.0，actual exit0、`FRONTIER_BUILD Succeeded`；before/after732输入FP相同，sourceStable=true。客户端为 `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.16.0/Candidate1/Build1/Frontier Reckoning.app`，boot.config GUID `5f347ad9ec2f494984b0b49c4f896c31`。原件为该Build1目录的 `build.log` 与 `runner-receipt073.json`，状态built_same_frozen_source；产出客户端不推定原生验收或私有交付完成。

## 新受控行为的通过范围

市场Edit26项覆盖逐lot总价/五笔等价、非法/不足/权限/暂停/淘汰原子拒绝、价格floor/ceiling与spread反套利、多Market/重建同玩家价格及对手独立价格、精度饱和、自定义价差限制、只读报价、真实cold磁盘、非法saved pressure、录像cold完整前向及双向seek、LAN重发仅一次与full/delta。AI覆盖真实余Food换Coin、Town/食物义务、无Market/无余粮拒绝、集成正常Command与cold同世界、市场桥梁/紧急骑兵不能侵占未付Ledger150C。

Stage Edit8项覆盖被旧中心堵住后扩展stage的AgeII/III/IV真实6/10/14人抵达并派出、窄可达廊的5×5紧凑中心、可见但断连时等待及延期搜索、expanded/blocked两种cold后继续同World、非法cursor拒绝与旧缺省cursor从0开始。65位置×两占地共130候选；最多6个非直线FindPath仅属于SelectStage，旧stage重验计入，AtStage另计。没有把真实成军改成兵数门槛，没有认证全部动态港区或整个Review寻路预算。

Market UI Play10项覆盖真正入口权限与支付/到账/变价、100/500切换保持按钮对象、余额不足/旧点击拒绝、另一Market改变报价后点击重核、毁损/观战/暂停/断网禁用及恢复、按住指针跨报价刷新/松开用当前价、实际报告写盘市场账本与Payment/MarketTrade核对。InputSystem/Canvas测试不等于当前GUID OS像素；原生范围单独待验。

FocusedFinal2实际63Edit+10Play全Passed；没有预记录source map/runner收据，不能单独扩为正式exact-source证明。FocusedMarket1实际20/21和Market2实际39/40均保留CoinOnlyShortageProtectsCompleteTradeStartupBeforeMilitaryConsumesLedgerFee失败，Market3随后42/42Passed。FocusedFinal1保留CS8773 C#9 inferred-delegate编译失败；后续FocusedFinal2与完整回归另行通过。旧072失败/中止/探针也保持原件，成功不回写失败。

## 两个闭合072样本的只读诊断

新诊断原件位于Candidate1/Diagnostics：`073-winding-ottoman44-closed-coin-audit.json/.md`、`073-dutch42-readonly-performance-audit.json/.md`；读取冻结072 runtime `bc127e02638caaeefd2310a1a3eff858f4f4cb2f` 与已闭合072原件，前后原件SHA/stat稳定，没有重新运行或重写072。

Ottoman44流式读取100055原事件，确认本地矿16/37耗尽后8矿工在975.509被下达回Town驻军，仍活的中央矿103附近有敌袭。991.905累计Coin2841.779785后持平；1052–1057五次矿104派遣在1.2–2.4秒内撤回且未采币。991时矿未全图耗尽，首Town损失更晚；迟到Gather条目是旧credit flush。原普通AiWorkforce仅筛目标点，continuity安全路线与实际Gather不一致；中间A*轨迹/首次威胁predicate未记录，不追加精确路径因果。073币桥针对义务/渠道，不代表已修普通路线或动态护送。

Dutch42模拟3600.061279s、whole-case wall3303.190543s，旧watch包含建局/Step/采样事件写盘/最终保存，稀疏样本无wall戳，不能定位主耗时。late hasStage=false/wave空和Muster，终点72/70陆军、219/302存活自有对象Idle，末300秒零AttackPrepared/Fire/Damage，是确定的调度失败。旧SelectStage仅25候选/7×7可能持续无stage；没有记录全部拒绝predicate，不能证明它是主要CPU热点。073扩展及observer分项计时须在新运行结果中另核，不宣称旧total wall原因或原生FPS改善。

## 普通I观察 · 实际关闭

真实预声明为Candidate1/Diagnostics/predeclared-observation073.json，observer SHA `963de12e6d39cfeeb6faa17c1ba532d03c752b226d722752780e2c1e5255a6a8`；普通I、standard、双AI、最高V，2400模拟秒cap、1500比较点、30秒采样。三个case：

| 地图 / 文明 / seed | 当前状态 | 待核结果 |
|---|---|---|
| winding / Ottoman / 44 | natural_terminal；2084.883057s、P0胜 | 11笔MarketTrade、14次assembled；1500比较点到达 |
| winding / Ottoman / 45 | natural_terminal；1754.918579s、P1胜 | 15笔MarketTrade、14次assembled；1500比较点到达 |
| river_bend / Dutch / 42（配置civilization=`river`） | observation_cap；2400.090576s、无winner | 0笔MarketTrade、21次assembled；1500比较点到达 |

运行目录 `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.16.0/Candidate1/NormalObserved1` 的开始收据记录2026-10-06T15:51:35.662825Z、真实runtime/FP与RunNormal，preparationRun=false、caseInterventions=false；真实关闭收据2026-10-06T16:05:33.404156Z、exit0、sourceStable=true，FP前后相同；manifest为observed_three_declared_cases。observer复制环境FP仍需外部真实前后源码证据绑定，不能仅用它自证身份。没有运行中注资/强制命令/干预。

逐笔事件与424个sample核对：两Ott共26次卖Food、零买入，P0分别1400F→707C和3000F→1042C，P1均200F→137C。两局P1在827.944885s从80.705048C卖100F后达到150.705048C，同tick支付200W/150C研究Cargo Ledgers，873.033875s完成，897.727844s实际收到50C。P0没有完成Ledger，不能称双方均完成币渠道；Dutch双方完成Ledger并收到真实商路资源，市场没有触发。

| Case | 原Events / Samples | 最后300秒 Fire / 正PvP Damage | wall / Step / observer / finalWrite（s） |
|---|---:|---:|---|
| Ottoman44 | 110105 / 142 | 2941 / 2749 | 387.152684 / 385.481716 / 0.623286 / 0.030213 |
| Ottoman45 | 87150 / 120 | 2533 / 2280 | 319.653741 / 319.038606 / 0.547908 / 0.026374 |
| Dutch42 | 101471 / 162 | 317 / 330 | 125.206723 / 123.777137 / 0.592703 / 0.027065 |

所有1500 crossing实际为1500.080811s/15003 steps；Dutch通过原创scenario构造，snapshot mapId为custom_scenario，内嵌document.id为traders_bend070。每对World/Simulation均来自同一边界，计数/账本/报价与对应事件前缀核对。独立检查器初次误把Dutch snapshot mapId预期为manifest的作者地图ID，三identity检查失败；这属于检查器schema假设，原失败报告和后续修正只读核验分别保留，未改游戏或重新跑局。

独立[三局审计](Evidence/073/Diagnostics/closure073-normal.md)实际10049项检查、0失败；298726条事件/424采样。[JSON](Evidence/073/Diagnostics/closure073-normal.json)160455B、SHA a56416a210e94d746aaee0e7610b3d43479320fb4840737e362008dead7de516；MD SHA35d662e619da0866302f8da85f8470b0caa6928de503d679aa137ec3c55b3855。第一次检查器schema误判的3项失败另封存first JSON/MD，不挪作游戏失败，也不改原局。

最大单步分别1800.2972/1862.1063/1832.6452ms，>100ms步1043/899/123、>500ms步21/13/8。Native1与三局共享主机并行，不能称隔离性能基准或原生FPS改善。

三个结束也不能认证全部币恢复/路线、自然文明平衡或原生FPS。Step、采样/事件写盘、final serialization分别记录；窗口与072不同，不直接拿wall比例当因果改善。

## 当前GUID原生限定范围与原件入库

Native1真实人工正常开局，三1600×1000F9原图均已查看且metadata PNG SHA相等。岸边无效放置未扣费；Market137在陆地实际支付100W、建造完成（1000HP/construction1），最终wallet300F/200W/100C和spent100W与原Payment相同。通过AX应用菜单Quit，真实进程exit0。后续public Sky坐标click/drag反复noWindowsAvailable，故市场模态、原生买卖/100-500未完成，原报告零MarketTrade；本GUID冷恢复/录像也未验收。原日志、动作、F9和quit报告保留在Native1，Diagnostics/native073-scope-audit.json/md限定全部范围；actions.jsonl从工人选择开始，之前接口尝试保留在任务工具历史，不能称全程journal。072GUID3288的9范围/40对F9、五exit0以及H短段600→1111/Verified=false均保持原身份，不能挪作073。

Native1与普通观察在同一主机并行运行，所有计时均不能视为隔离原生FPS。
61份选定原件已逐字复制到[Evidence/073](Evidence/073/copy-index073.json)，含完整回归原XML/log/map、失败/成功focus、Build receipt/log、三局manifest/receipt、Native1完整保留树和全部限定审计；原件前后SHA稳定。完整28份普通局原件另交normal.zip。[总审计](Evidence/073/Diagnostics/independent073-audit.json)148691B、SHA4165a0754d5994f2dd27407b36efa0340c3733a379a99e3cc0c2f46ebb59c187，Full/Build/三case通过、nativeexchange=false，交付outside该审计。

073封包、Drive实际字节/SHA/owner-only与本轮最终Git推送另由[交付记录](Delivery-073.md)补齐。全部072Evidence、源码快照/五ZIP/远端11文件以及更早历史保持原字节和原身份；原生长局性能、普通Gather动态安全、双物理Mac、完整商业内容与签名Release仍待证据。
