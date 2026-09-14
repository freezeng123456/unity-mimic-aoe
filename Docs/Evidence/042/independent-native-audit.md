# 042 · 0.2.19 原生局部对局审计

证据 matchId 为 `8afe40fb71de4df283ae3abd272f95b5`，appVersion `0.2.19`，buildGuid `41a7c09fede248e6b47e9732be4e6cdd`，seed 314159，`autoplay=false`，simulationSpeed 1.0。报告 reason 为 `menu_exit`，world time 262.6068115 秒，`winner=-1`；这不是自然胜负。事件共 3098 条，玩家 0 为 coast Age I，玩家 1 为 river Age II。

玩家 0 最终支付 1 笔村民训练费用 100 F；对应 `Payment` 与 `ProductionQueued` 均在 180.501815 秒，实体 1（TC）。该村民在 200.503036 秒完成，产生 `Experience: Unit trained` 5 XP。玩家 0 其他 XP 来源为 Combat bounty：99.1990585 秒 20 XP、101.2990265 秒 8 XP、115.9988025 秒 8 XP；合计 XP 事件 41，训练 XP 5。报告最终资源为 200 F / 300 W / 100 C，spent 为 100 F，gathered 为 0。

玩家 0 的移动事件为 `Command Move ×1`，135.1990509 秒，实体 2，target 0，amount 1；事件的 `position` 是实体原点 `(29,25)`，不是下令目的地，不能从该字段推出移动终点。04 只证明相机从 TC 中心 `(24,28)` 向左滚动；08/09/10 使用相同相机焦点 `(74.6077,30.7624)`，画面显示所选村民从左侧基地走到画面中部原先黑暗区域；13 按 V 定位到村民后相机焦点为 `(74.4731,31.8250)`，可辅助证明路径，但不是事件直接记录的实体坐标。不能混淆镜头测试和实体移动测试。玩家 0 没有 AgeAdvance 请求或完成、没有 ShipmentSent/ShipmentArrived、没有建筑施工或被毁链条。报告 ageHistory 仅为 `I @ 0s`。

玩家 0 事件过滤结果包含 11 Spawn、13 AttackPrepared、13 Fire、13 Damage、4 Experience、1 Payment、1 ProductionQueued 和 1 条 PauseChanged（262.6068115 秒，manual_pause）；玩家 1 的 2495 条 Gather 等事件没有合并。当前事件文件中没有玩家 0 Repair 账本事件，不能据此声称发生修理支出或生命值恢复。

该次原生检查实际确认了 100F 训练支付、生产排队、训练完成、5 XP 入账、移动命令和暂停事件。未完成或未证实的链条包括 Age II/III、船运请求/到达、建筑施工/摧毁、修理、主动军队交战与自然结算。由于最终为手动暂停后 `menu_exit` 且 winner=-1，不能称完整对局验收。

补充：`training-click-without-selection` 是未选中 TC 时的无效点击，不能视为扣费；报告中的训练确认仅对应 180.501815 秒 Payment/ProductionQueued 和 200.503036 秒 Unit trained。
