# 0.2.2 有界只读审查：AI 恢复与视听生命周期

## 最严重结论

`AiWorkforce.Assign` 发现当前采集目标危险后，并不保证村民会立即离开。它会把危险目标从 `resources` 排除；随后若没有同类安全资源，`best` 保持 null，当前村民订单不会被清除或改派。结果是村民可以继续执行危险采集订单，直到被杀死或其他系统打断。该点属于可定位的 AI 策略缺口；若测试中村民仍持续收到 Gather 结算，则还需检查 MovementCombat 的受伤避难规则是否被触发。

## AI 劳动力与集结恢复

`AiWorkforce` 只接收存活、非驻扎且当前订单为空或为 Gather 的 worker；Build/Repair/Garrison 等订单会被保留。每 12 秒最多改派约四分之一的可用 worker，空闲者优先。安全资源按当前可见性和可持续剩余量筛选，并以可见敌方单位的射程加 4（最低 12）判定危险。这个筛选没有提供安全替代资源时的撤离分支，形成上面的风险。

银行预估使用“已完工银行数 × 当前 CountingHouseRate × 60”从目标缺口中扣除 Coin。它是整分钟的潜在收入，而非按照银行剩余寿命、当前时间片或建筑是否会在该分钟内被摧毁计算；因此可能使 AI 低估 Coin 需求而减少采集。反过来，如果银行已存在但实际未持续产出，这个预估会造成资源饿死或延迟军队/时代行动。需要用实际 BankIncome 和 Payment 对照验证，当前代码本身没有把预估记入账本。

`SkirmishAI` 在发出 `Ungarrison` 后，`World.Issue` 会解除 occupant 并让单位移动到建筑 rally 点；随后 `AiWorkforce.Assign` 才可能在下一个 AI 思考周期把空闲 worker 改派到安全资源。被摧毁建筑的 occupants 由 `World.Remove` 释放并移到建筑右侧，但没有恢复此前 Gather 订单的机制。这个路径是明确的策略缺口，不能当作已完成的自动复工。

## 战斗反馈与音频

`CombatFeedback.Bind` 会先 `Dispose` 当前 active/pool effect，再把 `eventCursor` 设置到新 World 的当前事件尾部；因此通过 Bind 重开时不会重放旧事件。`Dispose` 也销毁生成的反馈对象和材质。若重开流程只替换或重置 `GameController.World` 而没有再次调用 Bind，代码中没有其他 World 身份变化检测，旧引用会继续消费旧事件；需要检查实际重开入口是否总是 Bind。

CombatFeedback 只消费 Fire、Impact、Death，按可见性和 `visibilityMask` 过滤，并在 active effect 达到容量 24 时丢弃新效果。它会在暂停时停止读取新事件，但已有效果仍在 Tick 开头按 `world.Time` 和迷雾清理；暂停期间若视觉层继续 Tick，时间不推进，效果不会提前过期。这个行为与暂停保留当前画面的预期一致，但缺少真实暂停/恢复场景证据。

`EventAudio` 初始化 12 个 channel，前 10 个用于战斗，后 2 个用于通知；分类节流和通道替换限制声音数量。`ResetMatch` 会停止声音、清空节流表并归零计数，`DisposeAudio` 还销毁合成 clip。可是 `OnEvent` 将战斗可见性和摄像机距离固定使用玩家 0，当前 1V1 本地玩家场景可接受；若观察者或本地阵营变化而没有重新初始化，这会泄露/丢失其他观察者应听到的事件。另一个边界是事件过期只检查 `world.Time - ev.time > .4f`，重开后若旧 World/事件被错误复用，必须依靠 ResetMatch/重新 Initialize 清理，单靠事件时间不足以隔离世代。

## 建议的验证场景

1. **危险资源无替代源。** 让 worker 正在采集，敌方远程单位进入资源点危险半径，地图上没有同类安全资源；记录 12 秒以上的 Gather、Damage、Garrison 和死亡事件。若仍有 Gather，确认上述 `best == null` 缺口；若应避难却未驻扎，再检查 `ApplyDamage` 的 22 单位 shelter 搜索。

2. **银行预估与真实收入。** 让 AI 拥有银行后在一个 60 秒周期中摧毁银行，比较 `BankIncome`、Coin Gather、Payment 和时代/军队队列。重点观察银行预估是否让 Coin 采集短缺，或银行实际被毁后 AI 仍延迟恢复采集。

3. **重开与视听世代。** 在有 Fire/Impact/Notice 效果的对局中暂停、结束并立即重新开始，确认旧 CombatFeedback effect、`eventCursor`、EventAudio `PlayedCount`、channel 播放状态和节流表都清零；同时在新对局首个事件中确认没有旧事件被重放。该场景若失败，优先判为重开入口漏调 Bind/ResetMatch 的生命周期问题。

当前没有运行 Unity，也没有把这些静态缺口称为已验证运行时 bug。统一指令接口、自动对局或基准通过不能替代上述安全资源和重开生命周期证据。


## 主 agent 处置与复核

“危险资源无替代源仍继续采集”已补充 SeekShelter 分支，新增真实进入驻扎且没有继续采集收入的测试通过。其余关于银行未来寿命的条目属于预测不确定性：每次分配都重新统计 Alive 且 Complete 的银行，未读取未来敌军或预支资源。被毁后是否足够快恢复钱币应继续用正常对局评估。

重开入口已逐一核对：StartMatch/ReturnToMenu 调用 Audio.ResetMatch，WorldView.ResetWorld 调用 CombatFeedback.Bind。11项PlayMode包含重开清空、暂停冻结、隐藏时刻事件不补播及弹道中段遮挡。当前玩家固定为0的本地1V1是明确支持范围；观战/玩家身份切换尚未实现。这些复核并不宣称听感、画面和玩家完整整局验收。
