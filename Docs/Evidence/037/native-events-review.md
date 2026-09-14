# 0.2.15 native mirror match review

Source: `36ea1d82568a47ccb8578a525d1d8994-match_finished.json` and its event JSONL. This is an evidence audit of one normal player session; it is not a claim that the full RTS acceptance gate passed.

## Outcome

The build is 0.2.15 (`28e06a3b43184bdeaa73fdf70f10d6c2`), `autoplay=false`, seed 314159, maxAge 5. The report ends at 969.910 seconds with `winner=1` and `reason=match_finished`. Player 0 reached age II only (`AgeAdvanced` at 422.72s); player 1 reached age II at 207.70s and age III at 580.51s. Player 0 paid for age III at 843.341125s (`1200 F / 1000 C`) but produced no completion event: its 110-second job would complete at about 953.341s, approximately 3.026s after the first town-center death at 950.315003s.

## Player 0 accounting

Player 0 gathered 3672.7126 food, 977.7536 wood and 1642.9630 coin, with zero passive and trade income. The report records 3850 food, 1277.7521 wood and 1250 coin spent, and no refunds. The event stream contains 4744 player-0 Payment events because repairs are logged repeatedly; the meaningful production/build/age payments include settler production, two Manor foundations, the 800-food age-II payment at 332.61s, and the later military production. Resources end at 123.5547 food, 0.0309 wood and 492.6580 coin, these aggregate floating-point fields do not alone establish exact event-ledger equivalence.

Player 0's shipment chain is complete for the three shipments sent: `ShipmentSent` at 271.01s, 484.62s and 670.78s, with corresponding arrivals at 306.01s, 519.62s and 705.77s. The event log includes spawned food and coin crates; this proves delivery, while it does not by itself prove that every delivered crate was collected. Player 0 had no refunds. Player 0 claimed two treasures and defeated one guardian; the report separates `treasures=2`, `guardians=1`, and `kills=27`.

## Production, losses and combat

Player 0 spawned settlers, musketeers and pikemen after age II, but no cannon, foundry or artillery event appears in the player-0 spawn sequence. The player also lost settlers, musketeers, pikemen, a barracks and finally the town center. The first player-0 town-center death is at 950.315s; the final player-0 Manor death is at 969.910s, when the match ended. Thus the first TC destruction did not immediately declare victory; natural elimination followed about 19.595 seconds later.

Player 0 produced 204 damage events, but **0** target an entity whose Spawn record identifies it as a building (coverage is limited to entity IDs with Spawn records and their event labels). The observed siege/building damage is attributed to player 1. Player 0's 27 reported kills include one guardian separately recorded in `guardians=1`; the remaining 26 are ordinary combat kills by the report accounting. No player-0 artillery siege path was demonstrated.

## Pause/focus and acceptance boundary

There are 66 `PauseChanged` events for player 0, representing 33 pause/resume pairs. Most are manual pairs; focus-loss markers occur at 774.958s and 851.039s, each followed by resume. There is no player-0 `Resign` event in this event file; the match ends through natural elimination after the town center and remaining Manors are destroyed. This proves repeated pause/focus handling occurred, but it does not prove every focus-loss transition preserved the intended input state.

The match demonstrates a real resource economy, age-II progression, three shipment sends/arrivals, treasure/guardian interaction, military production and natural post-TC elimination. It does not demonstrate a player-controlled age-III/IV/V path, artillery production/deployment, player damage to enemy buildings, or a player victory/complete siege route. The next normal player check must actually complete age III and the artillery siege route. In this session the third-age queue was destroyed before completion; no rejection event is inferred from elapsed time.
# Capture 与伤害映射复核（可重跑）

`capture-index.jsonl` 当前有 34 条记录。索引中的 `sourceJson`/`sourcePng` 是来源原名；本地证据文件按索引 `label` 保存为 `<label>.json` 与 `<label>.png`。按该映射复核后，34/34 个 sidecar 与 PNG 存在，34/34 的实际 PNG SHA-256 同时匹配索引 `sha256` 和 sidecar `pngSha256`，且 sidecar `pngFile == sourcePng`。sidecar 按版本分组为 0.2.15（19 条）和 0.2.16（15 条）；0.2.16 只作为独立 capture 证据，不能改写 0.2.15 对局结论。各条目的 build GUID 也已从 sidecar 读取。可重跑脚本为 `audit-native-037.py`，机器结果保存在 `native-evidence-check.json`。

对 0.2.15 match_finished-events.jsonl 的玩家 0 伤害事件逐条建立 Spawn entity ID 映射：204 条 Damage 中 204 条（100%）目标具有 Spawn 记录，197 条目标属于敌方已 Spawn 实体；其中 0 条映射到由 Spawn 标签识别的敌方建筑。当前映射覆盖的是事件中的 Spawn ID 与标签，因此不能据此声称完成了人工攻城验收；但“目标缺少 Spawn”不是本局伤害审计的遗漏来源（计数为 0）。
