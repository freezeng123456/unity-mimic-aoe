# 076原生医护验收 · 英国曲河42

2026-10-10。当前新构建的原生付费医护、Stop／手动恢复、真实进程重启载入及两段录像连续播放已分别执行；这不是完整人工比赛、原生性能或全部医疗地形的验收。总门槛见[076验证](Verification-076.md)、[分层矩阵](Acceptance-Matrix-076.md)。

## 身份与原件边界

- 冻结源码 `b1a9b6a977a60b63cd3e9f9632179c1787f3d00d`，774输入FP `dabf34d26490cece872959764b8b9e515749584b180f3d7899f38d79b69a6f8f`，与Focused4／Full1／Build1／NormalObserved1一致。
- App版本 `0.19.0`，新GUID `a86c515f3ab040649ea505e507193ff3`。原PID `78563`，真正冷启动PID `97963`；同可执行文件SHA256 `c2e121c781dc29e7b5551a5631c493899bebb4c37f7279edb0e30cec60d0266b`。
- 原生窗口为 British（`coast`）对 Ottoman、Winding seed42、standard AI；默认AgeI／钱包／单位，由真实GUI采集／建造／升级／训练。没有途中资源、单位、伤势或保存文件编辑。
- 外部原件根：`/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate1/Native1/british/`。F9的PNG／JSON原件在其 `Data/Captures/`，报告、事件、存档、录像分别在 `Data/Reports/`、`Data/Saves/`、`Data/Replays/`。原文件名、SHA及帧身份保留。
- 审计四组原件根：`/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate1/Diagnostics/`，名称分别为 `native076-first-run-audit`、`native076-paid-medical-audit`、`native076-cold-audit`、`native076-playback-audit` 的JSON／Markdown。四组审计、全部30组F9配对与生命周期receipt已复制入repo，[copy-index](Evidence/076/Native1/copy-index076.json) 的78个索引对象共41731871B，逐个bytes／SHA256复核通过；索引文件自身另计。原始完整日志／报告／存档／录像仍在外部原件根，不混称已全部复制入repo。当前封包／上传尚未发生，不把证据拷贝当作已发布。
- [原生闭合receipt](Evidence/076/Native1/native076-closure.json)记录三个相关PID均在真实GUI QUIT后已不存在；未观测OS exit code，**不填写exit0**。

## 首次尝试失败保留，不计自然败局

首局matchId `dc5835f86f1640608f0d325a3dc12ac7`：实际支付150W Church并完工、支付800F并完成90s AgeII；受伤Pathfinder8后来死亡，Town和最后settler随后死亡，人类人口为0。该局没有P0付费训练、没有任何Heal。操作者从暂停菜单实际Restart，报告为 `restart_exit`、winner=-1，**不是自然结束的人类败局**。

原14269条事件、7组F9原件、报告及录像段 `643a4291beba48baaf4c107877608200` 均保留；该失败不被第二次成功覆盖。首段失败录像仅有保存／解析证据，本轮不宣称其原生播放通过。详见[first-run审计](Evidence/076/Native1/native076-first-run-audit.md)。

## 第二局真实付费与医疗链

第二局matchId `ccc7b9fd6eb44cc3817dce2432df64af`，仍由PID78563正常GUI Restart开始。

|步骤|实际world秒／零起始event index|证据范围|
|---|---|---|
|Church付费与完成|19.600039／423 → 32.600075／526|300→150W；6名实际builder，13s多工人建造|
|真实目标攻击与受伤|AttackGuardian122：82.099319／974，prepared1009，Fire1019；受伤1020…3336|Pathfinder8由18次guardian×8HP及7次敌方Pathfinder×14HP伤害，450→208HP；不是HP注入|
|800F升级|222.704391／3402 → AgeII312.809875／4978|正常90s升级；选择 **LocalDefence `coast_militia_2`**，+5%训练、实际Pikemen149／150；不是首局的Homestead选择|
|Priest付款／队列／出生|328.510834／5236、5237 → 357.112579／5763、5765|100→0C，预约1人口，Priest153出生后used9／reserved0；base30s在+5%训练下实历28.601746s|
|自动治疗|358.712677…367.313202／5794…6037|87条实际正数1HP、10HP/s收据：Pathfinder208→295HP|
|Stop|367.313202／6038|医生AutoOFF、Idle、无target／order；之后13.800842世界秒无Heal|
|手动Heal恢复|381.114044／6311，正数6312／6315|Priest153→patient8，Auto仍OFF；295→296→297HP|
|真实保存|381.314056|暂停、world schema5、选择153；patient297／450且Hold，医生active Heal／work／target8、auto=false|

这一段合计89条正数1HP医疗收据。末张原F9位于381.214050、frame82755，仅有296HP；保存比它晚一个模拟Tick，297HP，二者不是同帧。

存档 `slot-1.json` 147801B，SHA256 `a654ba3b084a98003976d89907e0c1d58e2d58b7407662e1099ee7223d2bee05`；保存前缀6318条事件，原PID结束时报告6337条，额外19条为同时间的Gather flush，未再发生Heal。原报告 `menu_exit`／winner=-1／无P0损失，不是完整人工胜局。15组付费／医疗F9配对通过PNG SHA、1920×1080、request／capture／HUD同帧与新GUID核对。详见[paid-medical审计](Evidence/076/Native1/native076-paid-medical-audit.md)。

## 真正冷启动恢复与续疗

旧PID78563实际退出、新PID97963使用相同显式Data目录启动并LOAD原slot1；不是在原进程里只解析JSON。

|恢复阶段|实际world秒|patient8|Priest153|
|---|---:|---|---|
|LOAD后的暂停F9|381.314056|297／450|auto=false，原手动Heal／target8／work保持|
|Resume后的F9|391.914703|403／450|同一手动Heal继续，未重发请求|
|满血F9|400.315216|450／450|Idle、target0、无order、auto=false|

冷尾真实新增 **153条正数1HP Heal**，首6319@381.4140625、末6786@396.614990；没有过疗或满血后继续计正数。冷尾没有新的P0 Command／Payment／ProductionQueued／UnitTrained，因此不是补发Heal、重新付费或重新出生冒充恢复。

恢复的全部world entities与原schema5保存一致；其他世界字段一致的比较排除录像anchor省略的归档events。simulation accumulator按实际恢复路径由保存值0.0944029689归零，**不声称整个会话逐字节不变**。冷报告6888条 = 保存前缀6318 + 新增570；不得把原报告额外19条Gather与冷报告相拼累加。

3组冷恢复F9配对均核验，`replayWatching=false`。初次激活曾出现短暂noargs菜单PID97964，单独activation-race receipt保留且排除；正确PID97963的LOAD日志、保存SHA、暂停F9构成恢复证据。详见[cold审计](Evidence/076/Native1/native076-cold-audit.md)。

## 原生录像库连续播放

- 冷段 `adea3e8512e44f20acd4cefc3c420b1b`：从loaded checkpoint tick3813开始，**1x连续播放到tick4004／Verified**，最终world400.415222／patient450HP／doctor Idle。最终F9为 `Frontier-20261009-214458-815-f12305-5f4e33e77a534e9bb2de9661f8c97a7c.json/.png`。
- 第二局开局段 `90b7fc9666314d10a3d7013ac5da6ad3`：从tick0开始，**4x连续播放到tick3813／Verified**，最终world381.314056／patient297HP／doctor Healing。最后原F9为 `Frontier-20261009-214747-935-f22181-e3adf960be654f988434293c221cc852.json/.png`。
- 两次均在当前新GUID原生录像库实际打开／播放，没有seek；播放是录像消费而非新的普通比赛。不得将playback记录的医疗状态再次累加为live新增医疗。
- 两段最终F9均为watching=true、recording=false、verified=true、paused／worldPaused=true、incomplete=false、错误字段空，currentStep=endStep=final anchor step。最终worldtime／资源／人口、全部10个自方unit投影、2个生产建筑及所有player文明／team／状态，与原录像final anchor逐项一致；F9未输出完整隐藏世界，不称full-world dump。
- 5组回放F9的PNG SHA、1920×1080及同帧身份核验通过；所有seek=false，限定input日志未观察到Seek控制动作，最终PNG可见 `RECORDED SEGMENT VERIFIED` 与1x／4x。两段原文件／payload哈希、实际打开路径、播放前后F9与连续日志由[playback审计](Evidence/076/Native1/native076-playback-audit.md)单独列索引；cold审计本身只统计live续疗，边界不合并。

## 仍待独立验证

1. 原生手动目标这次已在射程内，未证明out-of-range手动接近；未证明受疗后原生返战Fire。
2. 原生Otto Imam默认开局，以及真实Winding两桥／侧栏、Garrison／Embark／Disembark与医疗联动尚未执行。曲河地图名本身不等于医生跨桥。
3. 完整AI医疗调度的磁盘MatchSave／录像、更广地图文明、医疗开关matched-seed对照与经济代价／平衡结论仍独立待验；普通两局的5次付费／34条整补回火是已观察事实。
4. 原生FPS／音画听感阈值／大军同屏、双物理Mac、完整人类自然胜负、跨版本迁移和Release公证未验。
5. second-run食物余额与ledger估算的浮点残差为 **-0.068359375F**，原件保留，未伪称账目严格零残差。
6. 当前076尚未封包／上传；既有075九文件owner-only交付保持原身份，不重命名为076，整体内容差距目标继续保持进行中。
