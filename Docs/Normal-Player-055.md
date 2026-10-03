# 正常玩家原生对局 — 0.2.31

本轮独立macOS arm64客户端，Unity6000.6.3f1，build GUID `128a731d440e4b7a94abfcc74b88fa97`，源指纹`1b45f97f3d90c9b76388419492a83c5622ccb2cf3a365030044403ccd309c1c7`。玩家通过实际游戏窗口的鼠标/键盘操作，普通初始库存与真实资源箱，1倍速、autoplay=false、benchmark=false。没有免费资源、规则费用变更或削弱AI。

coast镜像前四局使用Easy、时代上限III、合法25张经济预设；第五至第九局使用合法25张军备预设。第十局回到经济25卡，已完成正常I–III、付费炮出生、炮击敌建筑与自然结算，17项严格检查全部通过，B完成。河谷C1/C2已自然战败并完整归档：C1为River镜像/Easy/V上限/经济25卡，1486.584106秒、六项覆盖缺口；C2为River对Coast/Easy/V上限/军备25卡，1081.682983秒、十项覆盖缺口。C3为River镜像/Easy/V上限/军备25卡，1781.212158秒自然败局，实际达到V，唯一缺口为炮击敌建筑。C4为River对Coast/Easy/V上限/军备25卡，994.504211秒自然败局；严格16/17检查、16/19覆盖，付费炮击建筑与Cargo Ledgers成立，仅缺Age IV、Age V、特殊兵种。C5为River镜像/Easy/V上限/军备25卡，1661.541382秒自然败局，严格16/17检查、18/19覆盖，仅缺 `ageV`。六局均正常1x/无autoplay，不跨局拼接成C通过；[C6](Evidence/055/NormalPlayer/river-attempt-006/README.md)已1367.213257秒自然败局，严格16/17检查、17/19覆盖，仅缺 `ageIV`、`ageV`；C7正由river_strategy操作GUI，尚未判定；最终0.2.32尚未启动。菜单配置图和F9状态按同版本/GUID绑定；先前失败与中断保持原结果。

## 已归档尝试

| 对局 | 正常操作/真实账本 | 实际结局与验收 |
|---|---|---|
| `9f0d7b0c338243b4a3c57107db5e1019` | 自身II、真实采集建造、付费工人、1次船运、修理与驻军；5击杀/16损失 | 534.2166模拟秒自然失败，自身II；缺III、付费军队/炮和炮击建筑，不通过B。见[第一次审计](Evidence/055/NormalPlayer/coast-attempt-001/summary.json) |
| `0d281a3fa942437fabb6798fe2acf664` | 自身II、付费Manor/Barracks、5名付费Pike；赠工不当付费工人，无船运/炮 | 662.7852模拟秒自然失败，自身II/AI III，败方单位和建筑全部0；缺III/付费工人/炮/船运/建筑炮击，不通过B。见[第二次审计](Evidence/055/NormalPlayer/coast-attempt-002/summary.json) |
| `62dbc87afc3d4296a932fd41997856aa` | 2名付费村民、5Pike+5Musk、2Manor+Barracks、3次船运到达；780.0566秒真实支付III升级费用 | 791.6537秒城镇中心被毁，升级未完成；810.9490秒自然失败，自身II/AI III。仅缺实际III、付费炮和炮击建筑，不通过B。见[第三次审计](Evidence/055/NormalPlayer/coast-attempt-003/summary.json) |
| `fd3381d7dfee44cfb12332568c767c10` | 实际完成III、真实付费炮兵、4次船运、27击杀/42损失；炮兵732.5682秒出生、743.7654秒被击杀 | 863.5362模拟秒自然失败，双方III。严格账本与自然结算检查通过，仅缺炮兵命中敌建筑，不通过B。见[第四次审计](Evidence/055/NormalPlayer/coast-attempt-004/summary.json) |
| `145add218156473c9a2872545115abe3` | 军备25卡、实际III、12付费村民、15Pike+15Musk、2门付费炮与另1门船运炮；6次船运、55击杀/82损失 | 1073.2850模拟秒自然失败，双方III，败方存活单位/建筑0；三炮仅伤害敌单位，敌建筑炮击0，仅缺cannonEnemyBuildingDamage，不通过B。见[第五次审计](Evidence/055/NormalPlayer/coast-attempt-005/summary.json) |
| `d57aee12243c4599a9343986d5b2c1ab` | 独立军备25卡菜单/开局/两进展F9；最后同局画面664.8847秒为II、III已排队；旧日志最后暂停705.3748秒后失焦 | 环境恢复时旧进程已不在，sky重新启动客户端回菜单；无同局match_finished/menu_exit/quit或完整events，不推断退出原因/自然战败/III完成。中断未验收，见[第六次归档](Evidence/055/NormalPlayer/coast-attempt-006/summary.json) |
| `781c4090a2474a91964105c79eab9e27` | 实际III、6付费工、10Pike+5Musk、1付费炮、2Manor/Barracks/Foundry及4船运；27击杀/39损失 | 835.6430秒自然失败，双方III，末局存活单位/建筑[0,68]，无Resign；严格仅缺炮击敌建筑，不通过B。见[第七次审计](Evidence/055/NormalPlayer/coast-attempt-007/summary.json) |
| `6cf241946653492ea782b2f968b5e229` | 己方Command/Gather均0；3次付费村民排队，仅1名实际出生、另2名未完成 | 259.6066秒menu_exit，非自然结算；首次实际暂停在223.6044秒。中断未验收，见[第八次归档](Evidence/055/NormalPlayer/coast-attempt-008/README.md) |
| `7f161abd4f994a1aaf5be762ba4a23f5` | 实际III、16付费工、5Pike+5Longbow、5船运；100W250C炮已付费但Foundry在39.09秒后被毁，未满45秒生产 | 974.5091秒自然败局；缺付费炮出生及敌建筑炮击，B9未通过。见[第九次审计](Evidence/055/NormalPlayer/coast-attempt-009/summary.json) |
| `f65f503a74b9415e89d0a875e9b0ce2d` | 经济25卡、实际III、3付费工、9Musk+5Pike、2付费炮、4船运；炮186六次命中敌Stable156，共780伤害 | **B通过**：17项检查全部true，958.6130秒自然败局、败方全灭、无Resign。见[第十次审计](Evidence/055/NormalPlayer/coast-attempt-010/summary.json) |
| C1 `1498f3a84f374c0498900c25a6177872` | River镜像/Easy/V/经济25，实际III、付费银行/Carabineer/科技/炮击及两宝藏领取 | 1486.584106秒自然败局；16/17严格检查、六项必需覆盖缺口，不通过C。见[C1归档](Evidence/055/NormalPlayer/river-attempt-001/README.md) |
| C2 `0b725a5116d2415f855f4443c4023e0e` | River对Coast/Easy/V/军备25，实际III、16付费工/15Musk/5Pike/两银行、75W与100XP宝藏；IV付款后TC毁 | 1081.682983秒自然败局；16/17严格检查、十项必需覆盖缺口。船运炮不计付费生产，不通过C。见[C2归档](Evidence/055/NormalPlayer/river-attempt-002/README.md) |
| C3 `8fb300efbc1a4169a953d9dab2b7caad` | River镜像/Easy/V/军备25，实际I–V、银行/特色兵/科技/贸易/宝藏、8付费炮 | 1781.212158秒自然败局；16/17检查、18/19覆盖，仅缺炮击敌建筑。见[C3归档](Evidence/055/NormalPlayer/river-attempt-003/README.md) |
| C4 `b4250e2ab6664083ad919de284cc0afb` | River对Coast/Easy/V/军备25，实际III、付费炮对敌Manor六次各130伤害、Cargo Ledgers与贸易 | 994.504211秒自然败局；16/17检查、16/19覆盖，仅缺IV/V/特殊兵种。见[C4归档](Evidence/055/NormalPlayer/river-attempt-004/README.md) |
| C5 `d69b5316feda422f8a27d23545e13c5b` | River镜像/Easy/V/军备25，实际IV、同一老Musk及新生兵科技属性、付费炮对敌Foundry两次各130伤害 | 1661.541382秒自然败局；16/17检查、18/19覆盖，仅缺ageV。见[C5归档](Evidence/055/NormalPlayer/river-attempt-005/README.md) |
| C6 `7558b19fc9984578a313082b633c6630` | River镜像/Easy/V/军备25，实际III、同一老Musk162及新生263科技属性、付费炮256对敌Farm266三次各130伤害；IV已付费但未完成 | 1367.213257秒自然败局；16/17检查、17/19覆盖，仅缺ageIV/ageV。见[C6归档](Evidence/055/NormalPlayer/river-attempt-006/README.md) |

首局菜单与结算有对应F9。第二局Play Again沿用首局配置，没有独立新setup或同局结算F9；原始match_finished与menu_exit各自完整JSON/JSONL保留。缺图如实保留，不因其他对局有图而补作本局证据。

第三局独立菜单、开局F9和同match自然Defeat F9已按原字节保存；付费升级请求不能代替AgeAdvanced。第四局Play Again继承第三局菜单配置，未取得独立结算F9；自然match_finished报告保留，结算之后的菜单截图不代替结算图。两次F8窗口均与本局报告/events完整配对，全部帧有焦点，但各自有效时长不足300秒。

第五局已自然结算并完整归档：157.2004秒付费II、247.3059秒完成；510.5219秒付费III、620.5955秒完成。两炮以SINGLE各支付100W250C，在同一Foundry串行45s训练，718.4716/763.5606秒实际出生；另894.9285秒Cannon Support到达的炮单列船运来源。三炮均有真实Fire/Damage，但目标全是敌单位，不能替代敌建筑伤害。严格coast-iii复核除cannonEnemyBuildingDamage外均满足，1073.2850秒自然败局，无Resign，仍不通过B。12组同局F9及独立军备菜单图、结算图、完整match_finished/menu_exit和输入片段见[第五局归档](Evidence/055/NormalPlayer/coast-attempt-005/README.md)。

本局[F8完整关联摘要](Evidence/055/Performance/normal-b5-window-001/summary.json)在2560×1440录得35,776帧，11,772有效帧、197.825666秒有效活动，全部帧有焦点；600.0019墙钟秒中暂停区间明确排除。身份/统计/实际实体计数通过，但未指定300秒门槛，不能当长窗口完成。每批UI操作恢复、操作并暂停，失败和规划暂停继续保留；river五时代仍按独立正常玩家标准验收。

第六局状态为`interrupted_no_natural_end_report`：NativeInput055旧日志末次`manual_pause world=705.374800`后记录失焦；根代理恢复时sky重新启动客户端进入主菜单，但现有归档不能独立证明旧进程为何结束。20文件共2,821,359字节、13份原生来源按字节封存、4组F9配对见[中断归档](Evidence/055/NormalPlayer/coast-attempt-006/README.md)。最后保存画面仍为II+III队列，缺完成事件；不计III完成、自然结算或B通过。本局F8只有258.709919有效秒，仍缺最终报告/Spawn-Death关联，不能计300秒长性能通过。

第七局已完整自然结算：197.2028秒付II、287.3083秒完成，482.2202秒付III、592.3024秒完成；628.4936秒付100W250C训练Cannon223、673.5826秒实际出生。最终严格审核无事件/付款异常、自然结算及败方全部淘汰均成立，但`cannonEnemyBuildingDamage`仍为false，835.6430秒自然战败不转作B通过。原始最终报告/events与菜单/进展配对按本局身份归档，不用别局结算截图替代。

B7两份F8均已与本局最终报告/Spawn-Death完整配对：[第一窗口](Evidence/055/Performance/normal-b7-window-001/summary.json)有效259.774107秒，全帧聚焦；[第二窗口](Evidence/055/Performance/normal-b7-window-002/summary.json)有效135.600574秒，allFramesFocused=false。身份/统计/实际实体计数审核通过，但两份均未指定300秒门槛且各自不足300秒，不拼接为长窗口。

B8开局F9明确paused=false，第一次实际暂停直到223.6044秒；全局己方Command/Gather为0，259.6066秒通过RETURN TO MENU中断，不能称自然战败。正常扣费的3个村民作业只有1个实际出生，2个仍在队列。B9已改操作等待间隔，现场观察到7名工人实际Gather、付费队列和TC资源集结点成功；这些过程观察不作完整B或长性能验收。

B9 `7f161abd4f994a1aaf5be762ba4a23f5` 的[最终F8审计](Evidence/055/Performance/normal-b9-final-001/f8-audit.json)已完整配对：20,003帧全部有效/有焦点，单窗335.999332有效秒，明确300秒最低时长通过，335笔内存与最终事件前缀全部一致。原初审六文件保持原字节。B9对局本身仍失败，正常长性能通过不替代D规模。

B10的[完整归档](Evidence/055/NormalPlayer/coast-attempt-010/README.md)含33,001条事件、8组同局F9与保全输入日志。III在402.215332秒实际完成；付费炮186/236分别在508.121796/796.752502秒出生。炮186在577.206116秒收到对敌马厩156的Attack命令，580.505310–605.999084秒六次各130攻城伤害，全部付款/生产/目标身份已关联。目标F9在585.204163秒显示1850/1980，是首炮命中后、第二炮命中前；后续命中由原始事件证明，不倒填进该截图。

## 输入排查与证明边界

恢复/关闭面板后的[输入时序诊断](Evidence/055/InputTiming/README.md)绑定B8原始己方输入摘录及冻结RtsInput源码：四次世界指针动作距Escape恢复仅19.2/50.0/105.7/133.1毫秒，均在0.22秒屏障内，与实际Command/Gather为0相符。后续每次恢复或关闭面板后等待700毫秒再操作世界，并确认真实暂停/Orders/Gather状态。这解释B8的对应动作，不推广为全部旧失败或固定坐标偏移；没有修改游戏源码。

工具操作曾遇窗口失焦自动暂停、全屏状态下`noWindowsAvailable`以及状态需重新读取提示。标准Window菜单恢复窗口模式后工具坐标点击恢复；此时客户端实际渲染3456×1924。并未证明普通鼠标在全屏同样失败。原生窗口/暂停/焦点以具体F9和逐帧报告为准，不把全部截图写成默认2560×1440。

B4之后菜单又出现坐标操作`noWindowsAvailable`，AX窗口元素和键盘仍能用。同构建按窗口2560×1440重启并打开输入trace后，通过标准Window菜单进入全屏再返回窗口，第二次坐标点击取得真实Unity按下/释放/按钮动作与菜单变化。小段原始日志、准确启动参数及SHA见[窗口恢复记录](Evidence/055/InputWindowRecovery/README.md)；这是一例恢复观察，不是所有窗口/焦点问题永久修复的证明。

暂停用于查看状态和规划，恢复后才下令；没有在暂停时推经济或发免费兵。工具操作和模型思考的墙钟开销会影响1倍速比赛：前两局经济/防守操作明显过晚，失败仍完整保留。

`verify_player.py`独立审核逐笔请求/实际扣费/库存余额、生产最短时间与实际完成、单位及建筑身份、资源采集、经验/船运、时代、炮兵敌建筑伤害，以及自然消灭时败方所有存活单位/建筑。river追加银行收入、付费Carabineer/科技、宝藏和至少两种真实贸易收入。报告缺少卡组字段，菜单图与操作记录说明声明的DeckLibrary合法预设，不制造不存在的deck事件。

这些事件不单独证明每一种Shift手势、所有遮挡/出口组合或听觉品质；对应规则、输入自动化、视觉、音频和性能证据各自说明边界。封存0.2.30第三次尝试在`Evidence/055/NormalPlayer/attempt-003`，GUID不同，保持历史失败标签。

## River C1：保留已完成覆盖与缺口

[同局严格审计](Evidence/055/NormalPlayer/river-attempt-001/summary.json)对应 `1498f3a84f374c0498900c25a6177872`、原0.2.31/GUID，共53,782事件。17项检查中16项成立，`requiredPlayerCoverage`失败；缺口为`ageIV`、`ageV`、`paidTradePost`、`tradeXP`、`resourceMode`、`resourceTradeDelivery`。已成立的真实覆盖包括II/III、付费工/步兵/炮、炮击敌建筑、采集和船运、付费银行与被动币、Carabineer、付费科技及两次宝藏领取。自然败局与覆盖不足均保留，不能当作C通过。

末局己方累计采食约5069、木2474、币1090，银行产币3182.5；对手同期采食约13251、木7731、币6401。下一次操作参考将优先持续补工与保护采集，不能仅凭更早到III推断经济足够支撑IV/V。原始账本与画面/输入证据分别说明，不用后者补造缺失事件。

## River C2：自然败局与未完成的IV

[C2严格审计](Evidence/055/NormalPlayer/river-attempt-002/summary.json)对应 `0b725a5116d2415f855f4443c4023e0e`、原0.2.31/GUID、River对Coast、Easy、V上限、Military25。49,623条事件、唯一MatchEnded、无Resign，1081.682983秒自然败局；末局存活单位及建筑为[0,73]。17项检查中16项成立，仅requiredPlayerCoverage失败。

十缺口为 `paidCannon`、`cannonEnemyBuildingDamage`、`ageIV`、`ageV`、`specialUnit`、`paidTechnology`、`paidTradePost`、`tradeXP`、`resourceMode`、`resourceTradeDelivery`。已完成16付费工、15Musk、5Pike、两银行及两类宝藏奖励；IV付款998.003357秒、TC被毁1045.491821秒，相隔47.488464秒，未满130秒，不能当作IV完成。Cannon Support生成的炮288不计付费训练；Hussar Reserves只有发送记录，没有到达记录。

[完整归档](Evidence/055/NormalPlayer/river-attempt-002/README.md)共39文件、70,299,829字节，含两组最终report/events、6组F9及完整输入日志。离线独立核对所有文件SHA并重跑归档账本审计，仍为16/17及相同十缺口；receipt SHA为 `1013cf80503cc10254e62f61951aae36785d7cac16eb603cc1c63571bbe4ca97`。C1/C2均保持原始失败状态。

## River C3：完整I–V与单项攻城缺口

[C3完整归档](Evidence/055/NormalPlayer/river-attempt-003/README.md)对应 `8fb300efbc1a4169a953d9dab2b7caad`，River镜像/Easy/V/Military25、原0.2.31/GUID。1781.212158秒自然败局、90,759条事件、唯一MatchEnded、无Resign，终局双方时代V、败方全灭。严格16/17检查及18/19覆盖成立，仅缺 `cannonEnemyBuildingDamage`。

II/III/IV/V分别于194.102646/679.481140/1192.056030/1638.447021秒实际完成。19付费工、15Musk、20Pike、8炮、10Carabineer、4银行、付费贸易站、科技和宝藏均已成立；贸易站三次40XP交付后切Food并实际交付九次75F。独立扫描153次我方炮伤害全部命中敌单位，无敌建筑伤害。

73文件共147,020,138字节、20组F9、两份冻结日志、完整终局report/events已校验SHA；receipt SHA为 `8afbbbea487a0b28b09baff51f9ddaa93218ac1d987bc5058936daba34e2ee48`。科技截图旧兵群与新兵均显示240HP上限/ATK29；此前单兵为200HP/ATK24，但前后选中的旧兵ID不同，不写成同一单位配对。C3保留原始失败结论，后续C4结果另列如下，不跨局合并覆盖。

## River C4：真实建筑炮击与三项剩余缺口

[C4完整归档](Evidence/055/NormalPlayer/river-attempt-004/README.md)绑定 `b4250e2ab6664083ad919de284cc0afb`、原0.2.31/GUID，River对Coast/Easy/V/Military25，994.504211秒自然败局。36,218条事件、唯一MatchEnded、无Resign，最终玩家III/对手IV，存活单位及建筑为[0,77]。严格16/17检查、16/19覆盖，仅缺 `ageIV`、`ageV`、`specialUnit`。

Foundry224在684.479919秒付100W250C，炮238于729.568909秒真实出生；783.055847秒Attack明确指向敌Manor156，800.651550–826.145325秒六条Damage各130 Siege，总780，见[独立炮击核验](Evidence/055/NormalPlayer/river-attempt-004/cannon-building-audit.json)。F9在784.655457秒仍显示Moving、803.050964秒显示Reloading；六次命中以原始Damage为准，不用Attack接受提示代替。初始TC801.351379秒被毁，重建TC249于867.335266秒完成，未达IV/V。

Cargo Ledgers343.911774秒付费、389.014526秒完成，是本局唯一付费科技；不声称Veteran Musketeer升级。贸易站切Coin后18次交付共900C，另六次XP交付共270XP；银行产币570.5、两宝藏成立。65文件共59,908,543字节、16组F9、双冻结日志和两组终局报告/events完整保留；receipt SHA为 `cc28fc8e47999c072aeb04a0df04af0f4e8536ac70daebbaea547a14f8647e59`，完整性与文件SHA核验通过。C4失败状态保持；C5已1661.541382秒自然败局，严格16/17检查、18/19覆盖，仅缺 `ageV`；[C6](Evidence/055/NormalPlayer/river-attempt-006/README.md)已1367.213257秒自然败局，严格16/17检查、17/19覆盖，仅缺 `ageIV`、`ageV`；C7正由river_strategy操作GUI，尚未判定；最终0.2.32尚未启动。

## River C5：科技与建筑炮击成立，仅缺V

[C5完整归档](Evidence/055/NormalPlayer/river-attempt-005/README.md)绑定 `d69b5316feda422f8a27d23545e13c5b`、原0.2.31/GUID。River镜像/Easy/V/Military25，1661.541382秒自然败局，唯一MatchEnded原行79380、无Resign，最终存活单位及建筑[0,95]；严格16/17检查、18/19覆盖仅缺 `ageV`，V未付款也未完成。

老Musk173在825.245544/838.142395秒两张单选图均由原始UI context关联，HP200/200、ATK24变为HP240/240、ATK29；科技836.342834秒完成，173在338.111420秒出生、918.922668秒死亡。新Musk256在科技后840.941711秒付款、866.935364秒出生，874.833435秒单选图显示HP240/240、ATK29。F9 JSON没有selectedEntity字段，ID来自冻结输入上下文及出生/死亡链。

付费炮261于881.931702秒出生，对敌Foundry239在978.008240/983.106995秒两次各130 Siege伤害，共260。IV于1153.465454秒完成；五名付费Carab、Cargo Ledgers、贸易与银行均同局成立。67文件共127,494,066字节、17组F9、双冻结日志和79,417条事件已保全；[receipt](Evidence/055/NormalPlayer/river-attempt-005/archive-receipt.json) SHA为 `299eacd347eb2e951df0804e0a143de562adda1d1dfff469b08cee383bc026d0`。C5失败保持，[C6](Evidence/055/NormalPlayer/river-attempt-006/README.md)已1367.213257秒自然败局，严格16/17检查、17/19覆盖，仅缺 `ageIV`、`ageV`；C7正由river_strategy操作GUI，尚未判定；最终0.2.32尚未启动，不跨局拼接覆盖。

## River C6：炮击与科技成立，IV/V未完成

[C6完整归档](Evidence/055/NormalPlayer/river-attempt-006/README.md)绑定 `7558b19fc9984578a313082b633c6630`、原0.2.31/GUID，River镜像/Easy/V/Military25、正常1x、无autoplay。1367.213257秒自然败局，60,226条事件、唯一MatchEnded原行60193、无Resign，终局玩家III/对手IV，存活单位及建筑[0,67]。严格16/17检查、17/19覆盖，仅缺 `ageIV`、`ageV`。IV在1190.856323秒付费，TC1在1292.431519秒死亡，间隔101.575195秒未达130秒完成时长；V未付费。

付费炮256在824.045837秒出生，对敌Farm266于947.515686/952.614441/957.713196秒造成三次各130 Siege伤害，共390；最后一发在957.413269秒发射、炮于957.613220秒死亡后命中。前两次命中早于954.114075秒的显式Attack命令，不能用接受提示替代原始伤害。老Musk162在574.306824/773.458191秒两张单选图由冻结UI context与出生/死亡链关联，HP200/200、ATK24变为HP240/240、ATK29；Veteran Musk765.160217秒完成。新Musk263在科技后806.150208秒付费、832.143860秒出生，846.140442秒图显示HP240/240、ATK29。F9 JSON没有selectedEntity字段，单位ID来自输入上下文。

63文件共94,778,838字节、11组F9和双冻结日志完整保留；[receipt](Evidence/055/NormalPlayer/river-attempt-006/archive-receipt.json) SHA为 `a09a6581238f86db4f55ef882e8a754e94b0f82daf616c046019d9ca387127ee`，全部62成员bytes/SHA已独立复核。初次归档helper误改硬编码指纹导致比较失败，原probe保留；[纠正审计](Evidence/055/NormalPlayer/river-attempt-006/frozen-build-audit-corrected.json)及root独立357源码/3项runtime全量核验均通过，并非运行源码变化。C6局部Move与恢复后A键输入事实见[输入审计](Evidence/055/NormalPlayer/river-attempt-006/input-ux-audit.json)，不把未记录的点击原因写成结论。C7由river_strategy操作GUI进行中，尚未判定；最终0.2.32尚未启动，不跨局拼接覆盖。
