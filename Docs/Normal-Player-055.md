# 正常玩家原生对局 — 0.2.31

本轮独立macOS arm64客户端，Unity6000.6.3f1，build GUID `128a731d440e4b7a94abfcc74b88fa97`，源指纹`1b45f97f3d90c9b76388419492a83c5622ccb2cf3a365030044403ccd309c1c7`。玩家通过实际游戏窗口的鼠标/键盘操作，普通初始库存与真实资源箱，1倍速、autoplay=false、benchmark=false。没有免费资源、规则费用变更或削弱AI。

coast镜像前四局使用Easy、时代上限III、合法25张经济预设；第五至第八局使用合法25张军备预设，仍为Easy及III上限。第七局已自然败局并严格审核，仍缺敌建筑炮击；第八局在259.6066秒主动返回菜单中断，第九局正常重试进行中，不提前作验收结论。菜单配置图和F9状态按同版本/GUID绑定；重开的配置继承明确说明，不把其他对局的结算图替换过来。

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

首局菜单与结算有对应F9。第二局Play Again沿用首局配置，没有独立新setup或同局结算F9；原始match_finished与menu_exit各自完整JSON/JSONL保留。缺图如实保留，不因其他对局有图而补作本局证据。

第三局独立菜单、开局F9和同match自然Defeat F9已按原字节保存；付费升级请求不能代替AgeAdvanced。第四局Play Again继承第三局菜单配置，未取得独立结算F9；自然match_finished报告保留，结算之后的菜单截图不代替结算图。两次F8窗口均与本局报告/events完整配对，全部帧有焦点，但各自有效时长不足300秒。

第五局已自然结算并完整归档：157.2004秒付费II、247.3059秒完成；510.5219秒付费III、620.5955秒完成。两炮以SINGLE各支付100W250C，在同一Foundry串行45s训练，718.4716/763.5606秒实际出生；另894.9285秒Cannon Support到达的炮单列船运来源。三炮均有真实Fire/Damage，但目标全是敌单位，不能替代敌建筑伤害。严格coast-iii复核除cannonEnemyBuildingDamage外均满足，1073.2850秒自然败局，无Resign，仍不通过B。12组同局F9及独立军备菜单图、结算图、完整match_finished/menu_exit和输入片段见[第五局归档](Evidence/055/NormalPlayer/coast-attempt-005/README.md)。

本局[F8完整关联摘要](Evidence/055/Performance/normal-b5-window-001/summary.json)在2560×1440录得35,776帧，11,772有效帧、197.825666秒有效活动，全部帧有焦点；600.0019墙钟秒中暂停区间明确排除。身份/统计/实际实体计数通过，但未指定300秒门槛，不能当长窗口完成。每批UI操作恢复、操作并暂停，失败和规划暂停继续保留；river五时代仍按独立正常玩家标准验收。

第六局状态为`interrupted_no_natural_end_report`：NativeInput055旧日志末次`manual_pause world=705.374800`后记录失焦；根代理恢复时sky重新启动客户端进入主菜单，但现有归档不能独立证明旧进程为何结束。20文件共2,821,359字节、13份原生来源按字节封存、4组F9配对见[中断归档](Evidence/055/NormalPlayer/coast-attempt-006/README.md)。最后保存画面仍为II+III队列，缺完成事件；不计III完成、自然结算或B通过。本局F8只有258.709919有效秒，仍缺最终报告/Spawn-Death关联，不能计300秒长性能通过。

第七局已完整自然结算：197.2028秒付II、287.3083秒完成，482.2202秒付III、592.3024秒完成；628.4936秒付100W250C训练Cannon223、673.5826秒实际出生。最终严格审核无事件/付款异常、自然结算及败方全部淘汰均成立，但`cannonEnemyBuildingDamage`仍为false，835.6430秒自然战败不转作B通过。原始最终报告/events与菜单/进展配对按本局身份归档，不用别局结算截图替代。

B7两份F8均已与本局最终报告/Spawn-Death完整配对：[第一窗口](Evidence/055/Performance/normal-b7-window-001/summary.json)有效259.774107秒，全帧聚焦；[第二窗口](Evidence/055/Performance/normal-b7-window-002/summary.json)有效135.600574秒，allFramesFocused=false。身份/统计/实际实体计数审核通过，但两份均未指定300秒门槛且各自不足300秒，不拼接为长窗口。

B8开局F9明确paused=false，第一次实际暂停直到223.6044秒；全局己方Command/Gather为0，259.6066秒通过RETURN TO MENU中断，不能称自然战败。正常扣费的3个村民作业只有1个实际出生，2个仍在队列。B9已改操作等待间隔，现场观察到7名工人实际Gather、付费队列和TC资源集结点成功；这些过程观察不作完整B或长性能验收。

## 输入排查与证明边界

恢复/关闭面板后的[输入时序诊断](Evidence/055/InputTiming/README.md)绑定B8原始己方输入摘录及冻结RtsInput源码：四次世界指针动作距Escape恢复仅19.2/50.0/105.7/133.1毫秒，均在0.22秒屏障内，与实际Command/Gather为0相符。后续每次恢复或关闭面板后等待700毫秒再操作世界，并确认真实暂停/Orders/Gather状态。这解释B8的对应动作，不推广为全部旧失败或固定坐标偏移；没有修改游戏源码。

工具操作曾遇窗口失焦自动暂停、全屏状态下`noWindowsAvailable`以及状态需重新读取提示。标准Window菜单恢复窗口模式后工具坐标点击恢复；此时客户端实际渲染3456×1924。并未证明普通鼠标在全屏同样失败。原生窗口/暂停/焦点以具体F9和逐帧报告为准，不把全部截图写成默认2560×1440。

B4之后菜单又出现坐标操作`noWindowsAvailable`，AX窗口元素和键盘仍能用。同构建按窗口2560×1440重启并打开输入trace后，通过标准Window菜单进入全屏再返回窗口，第二次坐标点击取得真实Unity按下/释放/按钮动作与菜单变化。小段原始日志、准确启动参数及SHA见[窗口恢复记录](Evidence/055/InputWindowRecovery/README.md)；这是一例恢复观察，不是所有窗口/焦点问题永久修复的证明。

暂停用于查看状态和规划，恢复后才下令；没有在暂停时推经济或发免费兵。工具操作和模型思考的墙钟开销会影响1倍速比赛：前两局经济/防守操作明显过晚，失败仍完整保留。

`verify_player.py`独立审核逐笔请求/实际扣费/库存余额、生产最短时间与实际完成、单位及建筑身份、资源采集、经验/船运、时代、炮兵敌建筑伤害，以及自然消灭时败方所有存活单位/建筑。river追加银行收入、付费Carabineer/科技、宝藏和至少两种真实贸易收入。报告缺少卡组字段，菜单图与操作记录说明声明的DeckLibrary合法预设，不制造不存在的deck事件。

这些事件不单独证明每一种Shift手势、所有遮挡/出口组合或听觉品质；对应规则、输入自动化、视觉、音频和性能证据各自说明边界。封存0.2.30第三次尝试在`Evidence/055/NormalPlayer/attempt-003`，GUID不同，保持历史失败标签。
