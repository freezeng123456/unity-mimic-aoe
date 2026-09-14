# Native input events review — 0.2.15

Sources: `014316ba18e2400897a8be05e9aad6e4-match_finished.json` and its event JSONL, plus restart report `48322029c2674a2a968025ec33113d0c-menu_exit.json` and its event JSONL. This is an evidence review of a short manual input check, not a complete match acceptance.

## Reproducible findings

- The finished report is `appVersion=0.2.15`, build `28e06a3b43184bdeaa73fdf70f10d6c2`, seed `314159`, `autoplay=false`, simulation time `105.79896s`, `winner=1`, and `reason=match_finished`.
- There are **7 Payment events in total**, but player 0 has exactly **3**: two settler training payments at 11.0 and 51.5 seconds (100 food each), followed by 135 wood for `Foundation Manor` at 105.299 seconds. Player 1 has the other four settler training payments. There is **1 Refund**, belonging to player 0 at 52.600 seconds, cancelling a villager for 100 food. Therefore the player-0 statement is two training payments plus one building payment; the bilateral total is six training payments plus one building payment.
- New resource rally evidence for player 0 consists of exactly two `RallySet` events: 38.900 seconds targeting entity 12 and 53.200 seconds targeting entity 9, both with message `Resource rally set`. `RallyAssigned` also appears for both owners; those automatic events are not counted as additional player-0 manual rally commands.
- One `AttackMove` command is present for player 0 at 68.300 seconds. This is command acceptance evidence; it does not prove a successful engagement or building damage.
- The 135 wood building payment is explicit: `Foundation Manor` at 105.299 seconds, player 0, actual balance change −135 wood.
- Pause evidence is present: `PauseChanged manual_pause` and `PauseChanged manual_resume` at 68.900 seconds. At 105.799 seconds the player pauses and then resigns; the final `Resign` event is the actual end cause. The report’s `match_finished` reason therefore describes the saved report category, while the terminal result was **active resignation**, not natural elimination. The measured time is 105.799 seconds.
- The player had no collection income: player 0 report has `gathered.food=0`, `gathered.wood=0`, `gathered.coin=0`. The opponent collected resources. Player 0’s observed economy consisted of the starting balance, training/building payments and the one 100-food refund.
- The restart report is a separate `menu_exit` at 0.6 seconds with `winner=-1`, player 0 age I, population 6, and starting resources 300/300/100. It is evidence of a fresh menu exit/restart setup, not a continuation of the finished match.

## Boundary

This evidence supports real command/payment/refund/rally/AttackMove/pause/restart events in a native 0.2.15 session. It does not establish a full economic opening, age advancement, shipment use by player 0, mixed-army combat, artillery, siege, natural victory, or complete input coverage.
