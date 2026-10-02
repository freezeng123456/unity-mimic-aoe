# 054 玩家尝试 2 · II 与防御链成立，核心主动对局未完成

2026-10-02；matchId `0192b4b4cfb7470ea89f5bb1b8270a16`。实际客户端 `0.2.30`，buildGuid `21261224be4f40e692a3629fc4cc42fe`，Unity `6000.6.3f1`、`OSXPlayer`。coast 镜像、Easy、时代上限 III、固定地图/Seed 314159，`autoplay=false`、模拟速度 1；正常开局每方300F/300W/100C、1座TC、6名村民、1名探险家。

输入由操作者通过原生鼠标键盘完成；摘要检查正常开局与真实付款/生产/工作事件，不把操作工具的间隔当作连续熟练操作或平衡基准。原始事件18,166条，非零GUID与明确matchId排除了EditMode/PlayMode和Benchmark夹具。文件复制前后字节哈希相同；每项验收的 `actual` 和对应原始事件在 [summary.json](summary.json)。

## 实际结果与验收缺口

| 项目 | 实际结果 | 状态 |
|---|---|---|
| 采集 | 2,380条Gather；1167.943604F / 512.298157W / 94.679390C | 已发生 |
| 付费村民 | 6笔100F付款、6次入队、6名实际出生 | 已完成 |
| 建造 | Manor支付135W，81.199333秒完工；Watchtower支付200W，363.812988秒完工 | 已完成 |
| 时代II | 222.604385秒支付800F；312.709869秒完成 | 已完成 |
| 时代III | 无玩家III付款或完成事件，最终为II | 未完成 |
| 船运 | New Settlers与Settler Reserves，各发送1次、各实际到达1次 | 两次完整发送/到达 |
| 修理 | 895笔结构化实际扣款/回血；共115.070648W，逐笔回血合计1611.043701HP | 已发生 |
| 驻军与TC火力 | 1人进入岗楼、4人进入TC；TC准备/Fire/Damage/Impact各13条 | 已发生 |
| 新军事单位 | 时代II奖励Pikeman实体161，4次对敌Hussar近战伤害 | 奖励出生已发生 |
| 付费军事生产 | 无军事训练付款/入队/完成；未建军营或炮厂 | 未完成 |
| 炮兵与主动攻城 | Cannon出生0；显式军队攻击敌建筑0；军队/炮兵敌建筑Damage均0 | 未完成 |
| 暂停恢复 | 32.000084、115.698807、502.521454秒均有手动暂停/恢复事件 | 已发生 |
| 自然结算 | 631.792786秒MatchEnded，winner=1；零Resign，玩家存活单位建筑为0 | 自然败北路径已发生 |
| 返回菜单 | 同局menu_exit报告，模拟时间与胜负和结束报告一致 | 导出已发生；UI操作由操作者另行观察 |

总支出1400F / 450.070648W / 0C；最终余额67.831238F / 362.229034W / 194.676147C。原始汇总与浮点逐步累加存在微小残差，摘要保留该诊断，没有人为容差“全绿”结论。

本局证明经济、II、两次船运、岗楼、修理、驻军与自然失败的局部链路。**III、付费军队生产、炮兵主动命中敌建筑及完整主动对局仍未通过**。一名奖励Pikeman和防御建筑火力不能替代这些要求，也不能证明第二文明、五时代、长期性能或最终视听体验。

## 原始事件定位

下列行号均按 `20261002-092822-561-0192b4b4cfb7470ea89f5bb1b8270a16-match_finished-events.jsonl` 的1起算；时间为模拟秒。

- 第273行：13.600016秒Gather×6到浆果9，早期食物采集确已开始。
- 第569–572行：32.000084秒两笔100F村民付款与入队；第859/860、1198/1199行实际出生133、136及 `Unit trained`。第11232–11241行464.419128/464.519135秒另四笔付款/入队；实际出生177、178、183、186分别在第11916/11917、12581/12582、13317/13318、14346/14347行（484.420349、504.421570、524.419006、544.414124秒）。这些6名才计为付费训练，住房/船运/时代奖励不计。
- 第1180/1181/1327行：Manor135付款、地基、完工。第7261/7262/7578行：Watchtower170付款、地基、完工。
- 第3965行：800F时代II付款；第6084行：312.709869秒AgeAdvanced；第6085/6086行：奖励Pikeman161出生与Governor reward。不是军事训练出队。
- 第4548/5412行：New Settlers于252.606216秒发送、287.608337秒到达；第12009/13196行：Settler Reserves于487.120514秒发送、522.119568秒到达。村民180是第二次船运出生的Settler（第13193行），其18次Damage不计军事；Pikeman161只有4次Damage，目标为敌Hussar165。
- 第13115行：520.220032秒Repair×2到TC1；第13146行521.119812秒首次实际修理，HP1382→1383.800049、实际木材变化−0.128570557。895条均有 `economy.hasRepair=true`、正向HP变化及负向资源变化，最后两条为第15539/15540行565.808899秒。文本四舍五入为“0 W”不代表免费；1611.043701HP是逐笔回血和，不是受袭期间净HP增量。
- 第8034行377.313812秒村民3进入Watchtower170；本材料没有与此项对应的显式玩家Garrison命令，保留其自动驻军可能。第15541行565.808899秒显式Garrison×2；第15543/15544行565.908875秒村民177/178实际进入TC。第16714行另一Garrison×2；第16748/16761行595.901550/596.401428秒186/183进入TC。
- 第15546/15561/15567/15568行：TC1于566.008850秒准备、566.408752秒Fire、566.608704秒Damage与Impact，目标174。全局各13条，确认实际防守火力。
- 第17129/17130行：605.699158秒TC被毁并撤出4人；第18137行：631.792786秒最后Manor135被毁；第18143行：同刻完全消灭结算。TC被毁后仍运行26.093628秒，**605.699秒不是结束时间**。

## 原生F9配对画面

从客户端Captures目录选取本局全部4组原始PNG/JSON，2560×1440，未改像素。每组 `matchId=requestMatchId=hudMatchId` 与本局相同，构建/版本相同，请求/截图/HUD帧及模拟时间相同；PNG实际SHA等于sidecar的 `pngSha256`。完整来源、两文件哈希与校验布尔值见 [Captures/manifest.json](Captures/manifest.json)。

| 原生PNG | 模拟秒 / 暂停 | 实际画面与边界 |
|---|---|---|
| [早期经营](Captures/Frontier-20261002-091856-135-f37153-dc6d054a9c944b77bdb139cb0ddafcb2.png) | 115.598808 / false | AGE1、POP9+0/30、391F/165W/100C；画面7名选中Settler此刻Idle，与稍早Gather命令不相等，不能描述为全员正在采集 |
| [暂停与生产队列](Captures/Frontier-20261002-092539-624-f46115-bbcefe888ca74839bfb9e3e528efe875.png) | 502.521454 / true | MATCH PAUSED、AGE2、POP1+3/30、TC1940/3500、3名村民队列；实际后续出队由事件核对 |
| [修理命令](Captures/Frontier-20261002-092631-119-f47286-f13eb9708411485890e5aea9ebfcb693.png) | 520.220032 / false | Repair order accepted，2名Settler显示Repair；sidecarTC1428HP；此刻早于首次实际回血521.119812秒 |
| [驻军命令](Captures/Frontier-20261002-092716-717-f48260-c4ae167f57e9499b932a2fa7d6d7bafe.png) | 565.808899 / false | Garrison order accepted、TC1675/3500、队列已空；显示Sheltered0、UNGARRISON0；实际进入在下一模拟步565.908875秒，不能把命令帧的0判为失效 |

即时详情数字和动作按钮数字采用不同刷新节奏，操作者另有紧接G→T截图出现Sheltered2/按钮0的读审记录。按钮约1秒重建而详情每帧刷新，属于待后续优化/复核的显示一致性体验；本局13次TC实际开火成立，没有据此新增驻军代码故障结论。

## 文件身份与重算

原始客户端目录：`~/Library/Application Support/Frontier Workshop/Frontier Reckoning/Reports`。match_finished与menu_exit事件文件逐字节相同，后者不是另一局或第二次自然结算。

| 文件 | SHA-256 |
|---|---|
| `20261002-092822-561-0192b4b4cfb7470ea89f5bb1b8270a16-match_finished.json` | `dc3473a7562bb44212f22210a60208e3aa502938d7e8b7d35b8b5fd2bc591203` |
| `20261002-092822-561-0192b4b4cfb7470ea89f5bb1b8270a16-match_finished-events.jsonl` | `177b856bb53c42af747e39878bb111bc2bbe12b4fd8b951c412e3b8fa0f039f8` |
| `20261002-092827-567-0192b4b4cfb7470ea89f5bb1b8270a16-menu_exit.json` | `7b76855b69e5f66422d3394a7c08223f7c82010075be2a85a8a7ab6af7f18b76` |
| `20261002-092827-567-0192b4b4cfb7470ea89f5bb1b8270a16-menu_exit-events.jsonl` | `177b856bb53c42af747e39878bb111bc2bbe12b4fd8b951c412e3b8fa0f039f8` |

两局共用 [../summarize_player.py](../summarize_player.py)，依赖 [../../summarize_native.py](../../summarize_native.py)，按Rules.json映射实体/真实训练，并明确GUID、matchId、非autoplay、1x、正常开局。工具及所有本局归档文件的身份另见 [archive-receipt.json](archive-receipt.json)。在仓库根重算：

```sh
python3 Docs/Evidence/054/Player/summarize_player.py --match-id 0192b4b4cfb7470ea89f5bb1b8270a16 --build-guid 21261224be4f40e692a3629fc4cc42fe --reports-root Docs/Evidence/054/Player/attempt-2 --output Docs/Evidence/054/Player/attempt-2/summary.json
```

重算会更新摘要生成时间和哈希，须同步刷新receipt；不得覆盖原始报告/事件/配对截图。
