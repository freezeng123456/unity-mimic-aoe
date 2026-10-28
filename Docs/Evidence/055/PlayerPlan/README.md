# 普通玩家路线预算（0.2.31，静态规划）

依据当前冻结 Rules/DeckLibrary/World/Economy/一般 AI 策略；357 源文件逐字节与 [055 全量测试来源](../Tests/source-evidence.json) 一致。真实卡组、来源 SHA、计算假设见 [source-data.json](source-data.json)。本文是下轮普通 UI、1x、Easy、可暂停规划的行动参考，不是已经完成的玩家证据，也不保证获胜；不增加原 A–D 验收门槛。Coast 可设上限 III，River 路线需上限 V。菜单 Economy 预设才对应以下25卡，自定义卡组需另外核对。

开局先让6工采**己方食物箱**，收完立即转可见近鹿；留1–2工完成住房/收木箱后重新分配。初始6工、1探索者（0人口）。Coast 账面300F/300W/100C，另300F/300W实物箱；River 账面100F/300W/400C，另同样实物箱。收齐才分别是600F/600W/100C与400F/600W/400C，River不能按Coast600F起步计算。正常采集无交货站消耗，训练无持续食物维护费。

| 项目 | 真实费用 F/W/C | 基础时间与条件 |
|---|---:|---|
| Coast / River 工人 | 100/0/0 或 0/0/100 | TC单工20s；River上限50，Coast99 |
| Coast Manor / River House | 0/135/0 或 0/100/0 | 25s，+10人口；Coast完工实际赠1工 |
| II → III → IV → V | 800/0/0；1200/0/1000；2000/0/1200；4000/0/4000 | TC队列90/110/130/160s；既有训练会延迟开研 |
| Barracks / Stable / Foundry | 0/200/0；0/200/0；0/300/0 | 45/45/55s，时代II/II/III |
| 5 Pike / 5 Musketeer / 5 River Carabineer | 300/200/0；375/0/125；200/0/350 | 同批22/26/28s；Carabineer时代III |
| 2 Cannon（两次SINGLE×1） | 0/200/500 | 普通GUI同一Foundry串行45s+45s≈90s，时代III；每炮4人口 |
| River Bank | 350/350/0 | II，70s；完工2.5C/s；基础上限4 |
| Farm / Market | 0/300/0；0/100/0 | II/ I，50/30s；每农场有效8工 |
| Foraging Tools / Crop Rotation | 0/75/0；0/200/150 | I/III，30/50s；Crop需Foraging |
| Trade Post + Cargo Ledgers | 合计0/400/150 | 可见空socket上建200W/60s，再II研究200W150C/45s |

建筑表为1工无行走等待；2/4工施工速度分别×1.5/×2.5。普通GUI只切TRAIN:SINGLE×1或BATCH×5（Shift也选完整批量），没有×2按钮。表中5Pike/5Musk/5Carabineer是各一次BATCH×5，耗时22/26/28s；若用5次SINGLE则是110/130/140s。同一Foundry要造2Cannon，须两次SINGLE，各100W250C/45s，串行约90s；不能把源码API可接count2的45s当作普通GUI路线。BATCH×5炮虽同批45s，却需500W1250C/20人口，非本预算的2炮。同一建筑的批次/科技串行。TC重建500W/100s，失去住房不能忽略人口上限。时代奖励任选经济/军事，**付完费、时代完成、实际到达**才可用：II经济200F，III200W，IV600W400C，V1000F1000W1000C，均为需采实物箱；不能拿V奖励预付V。

## Coast I–III：先稳住采集，再保TC完成III

| 阶段 | 顺序与预算取舍 |
|---|---|
| I | 食物箱→近鹿；首Manor后给新工安排采集，保持TC正常付费出工至约10–12。SHIP有票时优先New Settlers（2工）或当下缺口资源；付800F进II前少排TC工，避免90s开研延迟。 |
| II | 先Barracks、住房、真正成形的一批5Pike+5Musk（675F200W125C），有票可用3Pike/3Musk减轻补损。向24工扩张；10工驻TC用于防守，其余采集，不能把驻工收入继续计入。余钱目标1200F1000C，币箱/币矿需实际收获。 |
| III | TC完成110s后建Foundry，用两次SINGLE分别支付100W250C，同一Foundry45s+45s约90s出齐2Cannon（合200W500C）；Cannon Support可另补1炮，不能代替付费训练证明。前排步兵保护炮与维修工，炮单独编组，对可见敌建筑部署/攻击；继续补有损兵力而不是让Idle部队追到敌家。 |

Coast24工示例：6初始+4Manor赠工+New Settlers2+Settler Reserves3+**9付费工**。年龄II/III、4Manor、Barracks、Foundry、付费5Pike5Musk2Cannon合计 **3575F1440W1625C**，不扣开局/船运资源、无损兵/修理/市场。免费工尚未出生，少1工就另付100F、占20s TC队列。此示例仅核预算；扩到34工应再加住房/补工费用。经济25没有6PikeIII、5MuskIII或6LongbowIII，不能在HUD寻找这些卡。

## River I–V：先两银行维持工人，再四银行支付高时代

| 阶段 | 顺序与预算取舍 |
|---|---|
| I–II | 6工食品箱后转鹿；住房与收木箱并行，River Coin250C有票再用。早期TC工花币，不花粮。付800F进II后Barracks、先1–2银行并正常出工；Settler Reserves3实际到达省300C/60s。银行不能过早吃掉全部食品：每座350F350W。 |
| II–III | Paid5Pike+5Musk守家，目标24工；Economy25在II只有Mounted Guard2Hussar，**没有Coast的3Pike/3Musk**。两银行合5C/s恰好支付持续100C/20s出工，不能又把这300C/min算成时代盈余；补矿/币船运，接近1200F1000C时暂停新工付款留给III。 |
| III | 继续到34工、四银行，Foundry两次SINGLE各100W250C/45s，串行约90s出齐2炮；Stable可用一次BATCH×5在28s训练5Carabineer（200F350C）。卡组确有Carabineer Reserves6、Pikeman Reserves6、Musketeer Reserves5、Cannon Support1；按实际票数优先防守缺口/粮币缺口，不能假定全到。 |
| IV | 留2000F1200C进IV；Ledger Dispatch卡真实存在，到达后银行+25%。Bank Charter仅加银行上限1，**不会免费建第5银行**；四银行已足以持续收入，无需急买第五。 |
| V | 留4000F4000C进V，防守保持军队/修理/粮源。普通付费Veteran→Guard→Imperial Musketeer研究分别100F100C/40s、300F300C/55s、600F600C/70s，合1000F1000C；此链是合法科技示例，非要求全科技。军队稳定后再用炮对可见建筑主动攻城并正常终局。 |

River34工示例：6初始+Settler Reserves3+**25付费工（2500C/500s TC）**。五时代费用8000F6200C、四银行1400F1400W、六住房600W、Barracks/Stable/Foundry、付费5Pike5Musk5Carabineer2Cannon、上述一条火枪升级链合 **11275F3100W10675C**。加两Farm、Market、Foraging、Crop为 **11275F4075W10825C**；再加Trade Post/Cargo为 **11275F4475W10975C**。未计补损、修理、银行第5座、额外炮/奖励住房，也未预扣船运/经济奖励。TC付费25工500s与年龄490s串行的16:30仅是**TC自身队列下界**，不代表全套军队/科技在该时刻完成。Foundry的2次SINGLE另需串行90s，可在III之后与TC/其他建筑队列并行，不能将其写成同批45s，也不能机械把90s加在TC下界之后当成整局完成时间。实际资金/建造/防守/出生等待会更晚。

## 持续收入与有限食物

地图生成每侧近端5鹿×1400+2浆果×1000=**9000F**，第二鹿群另7000F（须探索且实际安全）；近矿1200C、同侧较远矿1200C，不能把未知/受威胁资源当随手可采。River年龄+四银行已9400F，恰用完开局400F+近端9000F，还没支付军队/升级/补损。II200F奖励、Food Convoy II600/III1000/IV1400可缓冲，但先实际到达再采，不能预信用。远鹿不安全或近鹿将尽，就在已可见、合法空地提前完成Farm。

| 劳力配置（10工驻TC另列，均为无行走/拥堵/受袭理想值） | 持续产出每分钟 |
|---|---|
| Coast24：10驻+8鹿粮+3木+3矿 | 403.2F /90W /108C |
| Coast34：10驻+12鹿粮+8木+4矿 | 604.8F /240W /144C |
| River24：10驻+10鹿粮+4木、2Bank | 504F /120W /300C（连续出工可用净币0） |
| River34：10驻+16鹿粮+8木、4Bank | 806.4F /240W /600C；Ledger后750C |
| River34改16农场粮（两Farm各8工），其余同上 | 480F，Foraging后528F，另加Crop后**624F**；木/银行币不变 |

鹿基础.84F/工/s、浆果.67、木.5、矿.6、Farm.5。Foraging仅食品+10%，Crop另+20%按加法合**×1.30**，不是+25%；Farm .50→.55→.65。每资源节点有效8工，16工挤同一Farm不产双份。34工+10驻+8木只剩16粮工；第三Farm可作备用/扩张，不能凭空同时计24粮工。两Farm需预留600W，先完成再转工，地基不能采。

2修理工每秒修36HP，未升级3500HP TC连续受损时约耗**154.29W/min**。6木工180W/min仅余25.71W/min，难以同时补Pike/住房/炮；须留现金木缓冲。上述配置若从采木抽2工修理，先扣2工产出；24工再抽2工只剩12采工，不能按14采工+2修理算。敌军远程超TC16射程时驻10工也不能自动解决，要用现存军队攻击实际可见的威胁。

## 守家与船运边界

保TC与正在采集的工人优先：步兵站TC前约6–10米，炮在后方另编组；用J/Hold守点，出现可见高威胁目标再指定攻击，击退即收回阵线。Idle/AttackMove可能接敌追击，不能下完10军就长期不看；规划/上下文等待先用正常暂停。TC前10名有效驻工提供基础90伤害/3s（命中/减伤另算）；TC容量50不等于50名火力，也不是把10军驻进TC。炮最小射程4、部署2s，近身需步兵先挡；炮点车架中心/框选确认HUD，再点已可见建筑，不用固定屏幕偏移补偿。

Easy只是AI反应/军令较慢，资源、视野、费用、战斗相同；AI按自身军队集结与观察作决策，不是固定时刻波次。24/34工是持续采集目标，不是必胜公式或新验收指标。

初始SHIP票0；首票需150XP、下票225、再300……；采集每单位0.1XP，建筑/训练/时代完成另奖XP，银行被动币不按采集奖XP。船运**串行35s**，单位可能因住房/落点等等待，实物箱仍需工人采（6单位/工/s，最多有效8工）。资源卡/军事卡都消耗票，不能把所有支持同时预算到账。卡组中的Stone Foundations（id estate）仅是建筑HP+10%科技，**不是Estate建筑**。Trade Post须正常socket建造，再Cargo研究才合法切Food/Wood/Coin；宝藏由探索者在可见、所有守卫消灭后正常领取。它们可补预算，不当作开局无条件收入。

以下仅列最易误认的军事卡；完整25张（含真实名称、时代、数量、重复性、资源/科技内容）在JSON：

| Coast Economy25军事卡 | River Economy25军事卡 |
|---|---|
| II Mounted Guard ×2Hussar | II Mounted Guard ×2Hussar |
| II Musketeer Company ×3Musketeer | III Cannon Support ×1Cannon |
| II Pike Company ×3Pike | III Dragoon Escort ×2Dragoon |
| III Cannon Support ×1Cannon | III River Carabineer Reserves ×6Carabineer |
| III Dragoon Escort ×2Dragoon | III Hussar Reserves ×4Hussar |
| III Hussar Reserves ×4Hussar | III Musketeer Reserves ×5Musketeer |
| — | III Pikeman Reserves ×6Pike |
| — | III Ranger Company ×3Skirmisher |

来源定位：[FrontierHud.cs:262/300](../../../../Assets/Frontier/Presentation/FrontierHud.cs#L262) GUI单造/整批数量，[DeckLibrary.cs:13](../../../../Assets/Frontier/Simulation/DeckLibrary.cs#L13) 预设排序，[GameController.cs:25/114](../../../../Assets/Frontier/Presentation/GameController.cs#L25) 默认/起局卡组，[World.cs:36](../../../../Assets/Frontier/Simulation/World.cs#L36) 固定生成，[Economy.cs:184/213/237/335/373](../../../../Assets/Frontier/Simulation/Economy.cs#L184) 完工/八工/修理/SHIP/叠加，[Progression.cs:7](../../../../Assets/Frontier/Simulation/Progression.cs#L7) 文明费用与银行，[SkirmishAI.cs:42/146](../../../../Assets/Frontier/Simulation/SkirmishAI.cs#L42) 一般目标/集结，[AiAgePlan.cs:18](../../../../Assets/Frontier/Simulation/AiAgePlan.cs#L18) 储备策略；数值本体为 [Rules.json](../../../../Assets/Resources/Rules.json)。只读取这些一般规则，不读取当前局隐藏敌军状态。
