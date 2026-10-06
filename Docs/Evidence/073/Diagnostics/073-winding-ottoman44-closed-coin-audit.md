# 073 closed Ottoman coin-income audit

This is a read-only audit of the completed 072 winding-ottoman-44 case, not a new game or a counterfactual pass. The full original event ledger was streamed. Opening and early-terminal world files and all player samples were read. The 156 MB final world was not needed. Five original file hashes/stats were unchanged after the audit. Runtime source was read from frozen commit `bc127e02638caaeefd2310a1a3eff858f4f4cb2f`, independently of any new edits.

The first enduring Coin stop is a raid-induced retreat from a still-live central mine, after both local mines were depleted. It is not the first Town loss and is not whole-map depletion at 991 seconds.

| World seconds | Original observation |
|---|---|
| 687.679 | P0 local mine 16 depleted. |
| 936.418 | P0 local mine 37 depleted; workers have already been sent to central mine 103. |
| 975.509 | All eight current mine-103 workers receive Garrison orders to Town 1. |
| 975.809 / 976.009 / 978.008 | Visible P1 Abus 254/222 and Janissary 253 acquire retreating miner 234. |
| 985.906–988.006 | All eight named miners enter Town 1; they survive immediate retreat. |
| 991.905 | 1789.740 F / 935.497 W / 371.140 C; gathered Coin 2841.779785 stays flat through terminal. |
| 992.305–997.104 | Workers ungarrison and receive Wood/Food orders. |
| 1009.201 | Last delayed flush of an earlier Coin credit. This event does not indicate mining at 1009 seconds. |
| 1052.290–1057.089 | Five workers are sent to mine 104. They return to garrison after 1.2–2.4 seconds and never gather from it on these attempts. |
| 1052.090 / 1142.368 | Wallet Coin falls to 141.140 / 11.140, beneath Ledger and then every ordinary Age II army price. |
| 1233.146 / 1251.442 | P1 depletes central mines 104 / 103. |
| 1982.763 | P0 loses; all mines are exhausted. |

The event references in the JSON include original line numbers and byte offsets. Gather events use delayed buffered emission: the last seven post-retreat events are tiny credits flushed from workers already back near Town. Do not use their emission times to infer actual income.

## Source mechanism

`AiWorkforce` screens source positions for current visible danger, then scores Euclidean distance and demand. It does not check the whole worker-to-source path or per-worker `CanGather`/reachability before issuing the ordinary Gather order. `AiResourceContinuity072` has stricter safe route checks. The five attempted approaches to mine 104 are consistent with that mismatch: the endpoint is accepted, but the worker enters hostile ground and is ordered back to shelter. The exact intermediate A* paths and first threat predicate at 975.509 were not logged and are not claimed.

At Age II Ottoman cannot use the Age III Estate or Dutch Bank. A Coin-only shortage with excess Wood also does not enable `TradeBootstrap`/`LedgerBootstrap`, because those conditions currently begin with `NeedsWood`. Regular route investment additionally stops under attack or age saving. At 991.905 there is enough to pay the full 400 W / 150 C Post + Ledger startup; waiting until 1052 loses that opportunity. A reserve must protect both genuinely unpaid steps before army expenditure drains Coin.

## Supported repair direction

Keep the correct mining withdrawal. Unify safe transit checks for ordinary workers and continuity, and carry the checked movement path into execution or decline unsafe assignments. Extend payable Coin-only trade recovery while retaining Town and real political obligations. Add an actual player Market buy/sell command using Food/Wood surplus, atomic cost checks and deterministic state; AI should invoke that same command without free resources.

Meaningful regression cases should cover a still-live contested mine, a safe endpoint with an unsafe approach, and funded/unfunded Age II Ottoman Coin-only startup. A fresh predeclared natural match is still required after implementation; this audit establishes mechanism, not new balance or a win.
