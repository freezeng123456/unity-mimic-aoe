# 当前范围与限制 · 0.16.0 市场兑换与集结候选

2026-10-07。当前runtime为 `519d7d34a0be85f75ee19e53458c636d1ba0b3e4`，732输入FP `9b92812a922adb69a5ae501925417d8eba8e24b836f496274ebc8ec8679ba03d`。正式1192Edit+419Play=1611项全部Passed，0Failed/Skipped/Inconclusive；Build1实际exit0/Succeeded/sourceStable，GUID `5f347ad9ec2f494984b0b49c4f896c31`。三条普通I观察已实际关闭（runner exit0、源前后FP相同）：Ottoman44/45自然终局，Dutch42到2400秒cap；原生市场建造/扣费/完成已核，原生兑换点击因窗口定位错误未完成。073四包本地完整性已核，九文件私有Drive全字节SHA/owner-only读回通过；最末Git审查head见既有draft PR。[状态](Completion-Status.md)、[内容](Content-073.md)、[验证记录](Verification-073.md)、[交付记录](Delivery-073.md)。

## 市场兑换与经济边界

- 本项目规则P，每lot100资源，HUD提供100/500；整笔按逐lot变价总费用执行。初始买130C/卖70C，每次中间价变3，限定50–200；默认买价80–230C、卖价20–170C。Food/Wood各自计价、每玩家独立，同玩家多个Market及重建共用价格；没有共享全局商业市场或官方数值认证。
- 每笔必须选一个活着、己方、完工Market，并持有真实支付资源。不足余额、非法参数、暂停/淘汰/终局或浮点精度饱和原子拒绝；不会部分支付、部分售货或重置价格。配置限制变价小于两倍价差，受控套利/边界测试通过不等于商业市场或竞技经济平衡认证。
- Payment与MarketTrade及marketIncome/marketSold/marketBought分开记录真实支出、到账和买/卖货物，不能当作Gather、Passive或实车Trade收入。保存、录像前向/双向seek、LAN full/delta与重复请求、防陈旧报价/HUD指针刷新和写盘报告已通过自动化；当前GUID实际原生同范围尚待观察。
- AI只卖真实余Food，不卖Town恢复Wood；保留实际义务及至少400F防守储备，每次最多五lot。要有自己的完工Market与余粮，库存不足、设施毁损或需求仍无法满足时允许断档；市场不是无限币源或免费注资，连续卖粮会压低价格。
- Coin-only需求可申请真实Post200W+Ledger200W/150C启动，短期市场收入不释放未付Ledger预算，紧急骑兵同样保护已批准的150C；仍须施工、研究和实车配送。筹够现金、付款、设施完工、收到正运输收入及恢复战争能力是不同结果，三条新普通局尚未闭合，不能声称已普遍恢复。

## 普通采集与危险路线

- 072闭合Ottoman44原事件证明两本地矿先耗尽，8矿工从仍有资源的中央矿遇袭撤回；五次尝试另一矿又撤退且未采币。991.905累计Coin2841.779785后持平，迟到Gather事件为旧缓冲credit，并非继续在矿上采集；首Town丢失不是最初停币原因。精确中间A*路径/首次威胁谓词未记录，不能补写为已观察的轨迹。
- 普通AiWorkforce按当前可见目标点危险与欧氏距离分配Gather；实际WorkPoint/普通导航路线仍可能经过敌区，不能把continuity的filtered安全候选传入了实际运动。本批没有实现逐帧安全约束、动态危险绕行、护送或普遍采集恢复。
- 072可见Guardian独立经济避险、Build真实未来地基/WorkPoint与静态普通路线预检继续有效；后续武器、单位避让、fine recovery和detour可改变轨迹，静态预检不保证后续安全。隐藏/死亡/无武器守卫与敌国memory仍按旧合同处理。
- SourceSurvey单次review复用、可见危险圈快照和每radius弱连通负预检保留；true仍走原A*，Nav.Version或同Version restoreGeneration变化使旧负结论回退。受控连通测试与合法静态施工不认证全地图/全动态安全。[072验证](Verification-072.md)、[原限制](Known-Issues-072.md)。

## 集结、寻路与性能

- 073将SelectStage扩至65位置×7×7/5×5两占地，保存延期候选；每次最多6次非直线FindPath包含旧stage重验。ClearLine直达不消耗此预算，AtStage真实抵达判断、其他AI/运动寻路另计；不能宣称整个Army Review或固定Step最多6次寻路。
- 6/10/14实际抵达、4人补员和可用守卫保持；近墙/跨河长路不按距离或数量直接算到齐。没有合法可见、安全、空闲、可达stage时仍Muster，扩大候选不保证所有港区、动态障碍或地图都能发兵。自动化覆盖扩展中心、窄廊紧凑中心、断连等待、延期搜索/cold后同世界；普通三case军事结果仍待收口。
- 072 Dutch42闭合样本确有hasStage=false/wave空、大量陆军持续Muster，末300秒无AttackPrepared/Fire/Damage。未记录每候选拒绝谓词，不能把65位置修复说成已证明原全部原因，也不能从调度故障推出CPU主热点。
- Dutch42总wall3303.190543s包含建局、Step、采样/事件写盘及最终保存，旧样本无wall时间；文件体积不能单独解释主耗时。073新observer已实际关闭，Step分别385.481716/319.038606/123.777137s，observer0.623286/0.547908/0.592703s，最大单步约1.8s；与Native1共享主机负载，不是隔离基准。2400cap与1500比较点不同于0723600cap/2400比较点，不能直接把总wall比例写成性能改善或071/072因果对照。
- 旧NativeA同锚点250步Editor诊断结果字节一致且热点下降，旧072D的155.623s F8为声明短窗；均不是073GUI帧率或整个游戏加速倍数。当前GUID长期帧时间、GC/内存、正常/加速倍率和隔离性能未验收，不能承诺60FPS、所有卡顿消失或零泄漏。

## 自动化、观察与未完成交付

- 正式1611项全Passed且原732输入绑定完整；独立完整审计保留072旧fullname，名称保留不表示断言字节从未改变。FocusedFinal2实际63Edit+10Play全Passed，但未预记source map/runner收据，正式exact-source依据另为run-9q8yTJKS。
- FocusedMarket1实际20/21、Market2实际39/40，各保留CoinOnlyShortageProtectsCompleteTradeStartupBeforeMilitaryConsumesLedgerFee失败；Market3后续42/42Passed。FocusedFinal1的CS8773 C#9 inferred-delegate编译失败log保留，后续FocusedFinal2和完整回归另记通过。072既有失败/终止/探针原件不回写。
- 预声明winding Ottoman44/45、river_bend Dutch42，普通I、standard、双AI，2400模拟秒cap、1500比较点、30秒采样，无运行注资/强制命令/干预。开始收据与正在增长的样本不能当作三case完成；自然终局/cap、真实支付/施工/配送/市场收入/集结/攻击须分别核。三例即使关闭也不等于新16局路线认证或六文明平衡。
- 本GUID实际完成市场建造和100W扣费，三1600×1000F9原图已查看；原生兑换模态/买卖/100-500点击因public Sky noWindowsAvailable未完成，保存/冷恢复/完整录像/长局尚未验收。自动化InputSystem/Canvas字形不是OS像素；0729原生范围/40对F9/五exit0及H600→1111、Verified=false属于GUID3288，不重标为073。
- 073四原ZIP已封存，本地完整性独立核验通过；云盘九文件111794577B实际全字节SHA/owner-only读回通过。证据封包和上传不会增加原生交互、动态安全、平衡或FPS范围；后续文档Git提交不重建原包。072SourceSnapshot8490、原包与11文件保持原身份，见[073交付](Delivery-073.md)和[Delivery072](Delivery-072.md)。

073三局与私有交付均已关闭其声明范围，原生exchange/current-GUID cold/replay仍未验收；失败/cap与旧版边界保持。

## 政治、保存与历史身份

- 四经典革命/平民重建、800F/800W/800C与90秒、400F/400C与60秒均为本项目P；志愿兵只能采己方箱，政府完成后的新工人才恢复普通来源。既有队列/在途运输、DISBAND无退款/击杀XP及真实容量合同保留，完整商业革命国家/政治家/卡池未实现。
- 保存与录像严格匹配应用/规则/buildIdentity；072→073原生跨版本迁移、跨GUID播放和云存档不承诺。新增价格/账本/延期搜索状态的本地校验不能认证完整历史付款、签名来源或竞技防作弊。
- 07216正常I局独立Consistent、observed_routes_incomplete，路线9/16达到、革命6/8/升V3/8，15自然终局+1cap；15笔500W Town恢复仅5笔完工，不能说守住预算就保证地基/工人生存或三资源恢复。071旧八条仅1/8完整恢复也不回写通过；旧GUID/UI/存档/录像/截图各保持原范围。[072快照](Completion-Status-072.md)、[071验证](Verification-071.md)。

## 河流、场景、合作与平台

- 七课、两条三章原创链与固定/生成图保留；070西渠/守港原生、东渠/海撤模拟属于旧身份。普通付费舰队、捕鱼/贸易、自然海战与广泛平衡仍需独立样本。
- 工作坊固定128×128米、最多64初始组、每组128唯一引用；没有动态/嵌套组、任意脚本/尺寸、商业场景导入、过场编辑或游戏内地形/桥破坏。[069合同](Scenario-069-Contract.md)。
- 可信LAN同包手动地址/权威host执行；历史同机双进程及073市场replica自动化不证明两台物理Mac、网卡/防火墙、自然合作整局和网络成本。没有公网匹配/NAT/中继、主机迁移、加密或竞技防作弊。
- 程序人物/船只、枢轴动画与合成反馈仍有商业视听差距。当前macOS Apple silicon Development/Mono，Release/签名公证、Windows/Linux/Intel Mac未验收。完整单位语音、骨骼/布料、装填/建筑破坏、长期声画/内存和LAN RTT/带宽均仍待补。

更早完整边界见[072原限制](Known-Issues-072.md)及[072最终canonical快照](Archive/Known-Issues-072-final.md)、[071限制](Known-Issues-071.md)、[070限制](Known-Issues-070.md)。旧证据和交付不重建、不扩大共享或验收范围。
