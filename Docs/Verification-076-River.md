# 076 C2医护地形联动验证

2026-10-10。[预先合同](Content-076-River-Contract.md)。C1的1795测试、原生和交付仍见[原验证](Verification-076.md)，不重标为C2。原始C2运行输出位于 `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate2/`。

## River1：保留首次实际失败

HEAD `d917cab0e4da15a5fa090136e464db791f45bcae`，776输入FP `d73591041dae31d39bbbae9565b86167f183704a6abc4997173464224570eb1f`，运行前后源指纹一致。实际8例中6过、2失败，exit2，XML耗时137.484秒；原始 `River1/EditMode.xml`、log、runner receipt保留。

两座整桥跨越／磁盘冷读、两侧栏阻断／绕行、patient及healer真实Town出入共6例通过。两付费船例在建立医疗关系和真实Embark之前，第457行普通Move预泊断言失败，均偏离请求泊点6.36396122米；尚未覆盖后续医疗船载链。

只读核验：`World.IssueOrders` 对单船Move也调用水域FormationSlots；船半径1.05、间距4.5，槽位用1.71米清障半径筛选，而真实泊点只需船身1.05米合法。普通Move的实际槽位不承诺等于请求泊点。修正仅限夹具：绑定并严格检查普通Move的实际目标，另记录请求／槽位／实际泊点；真实Embark后仍须首次步行、12秒内入船、船到达真实resolved berth（.25米生产门槛）、所有医疗／容器／冷读／真实航程断言。未修改生产逻辑。新轮运行和新指纹另记，River1不重标通过。

## River2：真实乘客半径揭示第二处夹具假设

HEAD `2d9889b7b87c8a6c37cdb57fbebe58f1e93be66f`，FP `5d1e5ca1fb2abc421d9b6e2baec5866ddddce56a9f6511f57b12e6b058c59270`，源指纹稳定；实际仍6/8，exit2，XML138.810秒，原件保留。新增日志确认普通Move请求泊点 `(46.57,30.96)`、实际槽位及船位置 `(51.07,26.46)`，与4.5米对角槽偏移一致。普通Move目标修正已通过；两失败前进到第469行：预勘测使用.9米陆地半径，真实乘客半径所得shore相差1米，故把勘测请求点当实际入口仍是夹具错误。

下一修正仅将入船前的公共 `TryTransportShore` 查询使用该真实乘客半径（与生产入口同样下限.4），严格比对实际Embark shore和resolved berth均小于.001米；短路程和首Tick步行断言也绑定该实际shore。医疗、真实船程和冷读门槛不变；未改生产逻辑，尚未宣称船载链通过。

## River3：8/8实际通过

HEAD `9b7a5a8`，776输入FP `e35a3b72c2a7b33371eb2c9b34a6c1838bc758b66dd73aacc1e707eeffe71a91`，运行前后一致；8过、0失败／跳过／不确定，exit0，XML250.638秒。原始 `River3/EditMode.xml`、log及receipt保留，独立审计另存Diagnostics。

- 两整桥例均真实跨桥后完成383条正数治疗；途中真实磁盘schema5检查点分别step30／38，warm/cold全世界／会话／digest逐Tick一致。
- 两侧栏例先静止30Tick无治疗，显式绕桥口后分别t6.70／6.50首次正数Heal，未删细线遮挡或身体扫掠门槛。
- patient137／healer134真实Town步行入驻分别t.9／1.6，相关active／queued Heal清除、2名外部医护无关完整队列保留；原步冷读，真实退出后需fresh Heal。
- 付费Dock、真实训练的ship141两例均真实Embark（step1263／t126.298645），patient76.5HP／healer31.0000019HP仍受伤，人口7。真实入口 `(43,33)` 与泊点 `(46.66,31.40)` 严格绑定，不再混用预勘测 `(42,33)` 或普通Move槽位。各在入船该步真实磁盘冷读（25481／25377B），连续50Tick船载同步且无治疗；实际航行到另一岸 `(60.92,101.78)`、卸船、fresh Heal正数恢复，warm/cold一直相同。

仅覆盖受控AgeII／5000资源／初始医生与伤势／无AI、原始地形和定义的机制链，不等于原生鼠标、默认经济、普通AI、完整胜局、FPS或平衡。3秒owned Editor sample是诊断附件，不作FPS验收。随后已实际完成下列完整Edit＋Play及同源码新app；原1795及C1包保持原身份。


## C2 Full1：1803/1803实际通过

运行源码HEAD `09e07a40fb0008cdb69719336a674cb228129244`，776输入FP `e35a3b72c2a7b33371eb2c9b34a6c1838bc758b66dd73aacc1e707eeffe71a91`，前后稳定且changedSources为空。实际exit0；1355 EditMode＋448 PlayMode＝1803，失败／跳过／不确定均0。XML分别859.3194742／232.5266393秒，runner wall1110.1157615秒；这些是Editor测试耗时，不是FPS。

原始报告目录 `/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project/Builds/Tests/run-JENFOStU/`；闭合runner原件位于上述Candidate2的`Full1/`。独立审计观察时重算所有776输入，当时live／runner／source-evidence三份完全一致；逐fullname比较C1：原1795项全部保留并通过，Edit恰新增8项，Play无增减。旧兼容项是本次完整运行内的子集，不另加到1803。

|完整运行内兼容类|实际通过|
|---|---:|
|NavalTransport065Tests|17|
|CurvedManeuver063Tests|9|
|WaterNavigation061Tests|17|
|MapLayout063Tests|28|
|NavalDeliveryTests|6|
|GarrisonExitTests|7|
|NavigationSweep063Tests|9|
|旧兼容合计|93|
|新Healing076RiverLifecycleTests|8|

- EditMode.xml SHA256 `85b25538f34eced2090124fa31e595f2d94b7340e823c23cf91c53ded65b6ad4`。
- PlayMode.xml SHA256 `602b205f8d22cc4fbc4ae6fe105d48c229778e34393d4e1bad35d3603460823a`。
- source-evidence.json SHA256 `fdc9eee3fb643c184918abeaef1652513a616358c4a79ec690c32321e014c987`；闭合runner receipt SHA256 `dee744ef3ce5d4e76dd213728caff9d4cc2ec715e26f903467afbe9523dab912`。
- 外部独立审计 `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate2/Diagnostics/full1-independent-audit.json` SHA256 `023349bc8df6fb69b1ba5d12c16de181a02676f6dbcfada3d3009215891a255e`，结论PASS；含107 Edit／79 Play逐类统计、新8项完整输出和旧93项身份。

## C2 Build1：新原生app的独立身份

同源码HEAD `09e07a40fb0008cdb69719336a674cb228129244`、同776输入FP `e35a3b72c2a7b33371eb2c9b34a6c1838bc758b66dd73aacc1e707eeffe71a91`，Build1实际exit0且前后源稳定。版本仍0.19.0；新GUID `ed29410c2b27484abeac486c8f57b81b`，与C1的 `a86c515f3ab040649ea505e507193ff3` 分开。

实际app在 `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate2/Build1/Frontier Reckoning.app`。298文件／191062652B，arm64；manifest记录codesign verification exit0。Native独立审计另对298文件全量复核bytes／SHA／mode一致，实际可执行文件SHA256 `a669de6955b94cc950d69759dc768fb8d8d31c8e07e1ba002b053b45b71d6cf4`。`actual-app-manifest076.json` SHA256 `4c8806bf0f5757e40fa82b28c8e22e064e9e593297003fd9f1514ec7eaf7c511`；Build runner receipt SHA256 `fe3698e9c6a6e748a33a60eca324cb0eec2b9e1ccb5d4dd715117402091dec79`。

## C2 Native1／Ottoman：真实开局，医疗验收不完整

原件目录 `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate2/Native1/ottoman/`。实际普通Ottoman／英国对手／Winding42，默认AgeI／300F＋300W＋100C，autoplay=false、1x。matchId `3d8032957468484c9cfebd5d2340690c`；8组schema7 PNG/JSON、report、replay与上述新GUID／0.19.0一致，同帧／HUD／PNG SHA全部核对。

真实100W Mosque136付款（event index561／JSONL line562／t38.199989）、spawn136（562／563）、普通工人施工完成（647／648／t49.599815）。采集与自动工人确实发生；P0唯一付款为该100W，无额外中途注资记录。分析间隔中普通AI进攻，Explorer8不是截图遗漏：Death index7459／line7460，t358.212646，message `Pathfinder lost`，player0／entity8／target1；此前有敌方实弹Damage。最新F9 world385.914337已无活Explorer8，replay最终anchor保留其dead=true／hp0，report记录P0 loss1。

**这局没有Imam出生、P0付费军事训练、时代升级或任何正数Heal，也没有医疗后返战Fire；全局Heal及hasHealing均0，P0 ProductionQueued／UnitTrained／AgeAdvanced均0。** 早先自动战斗Fire不计作医疗闭环。UI随后RETURN TO MENU并QUIT，PID55949已退出；report reason=menu_exit／winner=-1／8608事件，既不是人工完整胜局，也不是正式自然败局验收。原launch receipt的`launched_not_accepted`状态保持不动。

记录的replay段 `c90d163335144c17bea4b9f9e84bdae8` 有14 anchors／13 records／8608事件，gzip解码17,020,117B与payload SHA相符，事件与JSONL完全一致；`incomplete=false`仅指录像记录未截断，本轮没有播放到Verified、没有奥斯曼原生冷存档读回。8组截图及全部22原件保留，无重标。Native log无Exception命中；Build log三条Exception字样是源文件大小清单，非异常，已在审计中保留分类。退出的线程finalization／MemoryLeaks诊断仍保留，未声称日志完全无诊断。

外部审计 `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate2/Diagnostics/native1-ottoman-incomplete-audit076.json` SHA256 `e25d8867dfa85b796aac3ada408b8d66870bf7c9aa17fe70821375dba19c3444`：nativeAcceptance=INCOMPLETE，evidenceIntegrity=PASS。完整原生医疗、超射程接近／受疗后返战、默认经济Imam、FPS、完整人工胜负及双物理Mac仍独立待验。C1原包、云盘及GitHub交付保持原身份；本页不声称C2已新增封包／云盘交付。

本页C2的源码、Full1、Build1及Native1均为0.19.0历史验收身份；后续077／0.20.0开发源FP `fdd60bfa136f944cee07e75b157bf8143a7ca63da11171d517feafb57a56716d` 属另一轮，另行测试，本页1803不证明077通过。
