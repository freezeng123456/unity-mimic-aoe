# 075 原生操作记录

2026-10-09，实际运行Build1原生macOS arm64客户端0.18.0，GUID `67594b51e2dd40f593ec8e956f6f4960`；运行源码51bd61f、758输入FP a3e1e3518a23d000f6ce1e80bef628f72bd55c631fcb9ddcd6e9f2be80066594。使用公开Sky SDK点击实际窗口，F9由客户端生成PNG/JSON。没有使用场景赠兵、注资、autoplay或benchmark。

## 荷兰普通开局

荷兰人类P0、英国AI P1，Winding River Valley/seed42、时代I。原启动receipt误传winding_valley，实际在菜单改选Winding River Valley后开局；录像opening、F9和quit均为真实mapId=winding，保留原启动receipt。

普通钱包100F/300W/400C，六工人和探险家。选Pathfinder点击Envoy，实际支付50F并进入15秒队列。slot1在time24.300056保存，队列elapsed0.1/15、原支付50F；没有冷加载该槽。

实际ENVOY RALLY地面命令被接受，旗点为世界(36.7592583,30.7205200)。随后探险家执行AttackMove并与守卫交战，生产继续；Envoy136在39.199974秒从移动生产者新位置附近出生，实际取得RallyAssigned并到达旗点。使节被选中后点击AUTO SCOUT，后两张F9均为Scouting/Scout，位置从(36.3292,40.6862)变到(28.8050,18.4893)。实际保存录像并通过应用菜单正常Quit，quit报告time92.0991669、1277事件。

slot2点击尝试没有生成文件，不能记为保存成功。未原生执行使节贸易站施工、冷恢复或录像播放；这些机制的自动化覆盖分别保留。没有本局完整人工胜负或FPS验收。

## 法国普通开局和冷恢复

法国人类P0、英国AI P1，winding/seed42、时代I。普通钱包300F/300W/100C，六猎民和探险家。按T选城镇中心，实际点击Native Scout，22.100048秒支付50F/25W，队列与钱包变为250F/275W/100C；slot1在22.600050秒保留elapsed0.5/18、已付50F/25W。40.099960秒Native Scout136实际训练完成。

选中实际新兵，点击AUTO SCOUT和ENTER STEALTH，F9显示Scouting、Orders1、Stealth ACTIVE及真实移动。slot2在82.999306秒保存，scout136的stealth=true、首命令Scout16，带真实resolvedPoint/path。实际SAVE REPLAY显示Recording saved，随后应用菜单Quit。

新进程Cold1复用相同Data，实际点击LOAD SAVED MATCH→slot2，界面显示Loaded slot2、Scouting与Stealth ACTIVE。恢复后继续移动，F9再次记录ACTIVE；实际点击LEAVE STEALTH后显示OFF并再次F9，随后应用菜单Quit。冷录制anchor0的World与slot2逐值一致（仅events是录像投影），Simulation只将accumulator归零，符合ReplayDigest正规化。slot2原始SHA与冷启动receipt中的before值一致，未覆盖原槽。

原始与冷续录片段的event/input前缀hash已重新校验；保存录制段不等于原生播放通过。未冷恢复生产中的slot1、未验证双物理Mac、自然完整人工比赛或FPS。

## 证据和限定

原始PID荷兰69195、法国69715、法国Cold1 69832均已消失；日志有OnApplicationQuit/cleanup、真实quit报告和finishReason=quit录制片段。没有额外机器exitCode receipt，不编造原生exit0。原始F9的build GUID、PNG SHA和状态，与实际应用及构建回执绑定。

仓库保存Native1全部53个原始文件的逐字节副本，包括15对F9截图/JSON、存档/录像、启动receipt、原生日志与报告；两国只读审查另列。证据包保留这些完整原件，普通AI全量World/Simulation/JSONL原件另在normal包。[证据索引](Evidence/075/README.md)、[验证记录](Verification-075.md)、[交付](Delivery-075.md)。原生操作与普通AI观察曾同机运行，计时不作为隔离性能结果。
