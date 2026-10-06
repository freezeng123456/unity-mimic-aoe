# 072 winding-ottoman-44 bounded investigation

Status: partial live-prefix diagnosis only. Matrix/release acceptance remains pending. This task only read existing bounded data and frozen code.

**Concrete finding:** Coin gathering ceased before the first Town loss. Later actual enemy fire killed the recovering workforce. The initial Coin interruption cause is not proved.

| World time | Workers / soldiers | Coin wallet | Gathered Coin | Age plan | Town | Market |
| --- | --- | --- | --- | --- | --- | --- |
| 961.812 | 30 / 4 | 511.101 | 2776.626 | 3 / saving=True | 1 complete | 158 complete |
| 991.905 | 31 / 3 | 371.140 | 2841.780 | 3 / saving=False | 1 complete | 158 complete |
| 1021.997 | 32 / 1 | 346.140 | 2841.780 | 3 / saving=False | 1 complete | 158 complete |
| 1052.090 | 32 / 1 | 141.140 | 2841.780 | 3 / saving=False | 1 complete | 158 complete |
| 1082.183 | 33 / 1 | 36.140 | 2841.780 | 3 / saving=False | 1 complete | 158 complete |
| 1112.276 | 34 / 0 | 36.140 | 2841.780 | 3 / saving=False | 1 complete | 158 complete |
| 1142.368 | 35 / 0 | 11.140 | 2841.780 | 3 / saving=False | 1 complete | 158 complete |
| 1172.461 | 35 / 1 | 11.140 | 2841.780 | 3 / saving=False | 1 complete | 158 complete |
| 1202.553 | 36 / 0 | 11.140 | 2841.780 | 3 / saving=False | 1 complete | 158 complete |
| 1322.924 | 39 / 0 | 11.140 | 2841.780 | 3 / saving=True | 1 complete | 158 complete |
| 1383.109 | 41 / 0 | 11.140 | 2841.780 | 3 / saving=False | 1 complete | 158 complete |
| 1413.202 | 41 / 0 | 11.140 | 2841.780 | 3 / saving=False | 1 complete | 158 complete |
| 1443.295 | 42 / 0 | 11.140 | 2841.780 | 0 / saving=False | 357 foundation | 158 complete |
| 1473.387 | 42 / 0 | 11.140 | 2841.780 | 0 / saving=False | 357 foundation | 158 complete |
| 1533.573 | 42 / 0 | 11.140 | 2841.780 | 0 / saving=False | 357 complete | 158 complete |
| 1563.665 | 42 / 0 | 11.140 | 2841.780 | 0 / saving=False | 357 complete | 368 foundation |
| 1593.758 | 43 / 0 | 11.140 | 2841.780 | 0 / saving=False | 357 complete | 368 complete |
| 1653.943 | 45 / 0 | 11.140 | 2841.780 | 0 / saving=False | 357 complete | 368 complete |
| 1684.036 | 45 / 0 | 11.140 | 2841.780 | 0 / saving=False | 357 complete | 368 complete |
| 1744.221 | 45 / 0 | 11.140 | 2841.780 | 0 / saving=False | 357 complete | 368 complete |
| 1774.314 | 45 / 0 | 11.140 | 2841.780 | 0 / saving=False | 357 complete | 368 complete |
| 1804.406 | 45 / 0 | 11.140 | 2841.780 | 0 / saving=False | 357 complete | 368 complete |
| 1834.499 | 45 / 0 | 11.140 | 2841.780 | 0 / saving=False | 357 complete | absent |
| 1864.592 | 38 / 0 | 11.140 | 2841.780 | 0 / saving=False | 405 foundation | 407 foundation |
| 1894.684 | 14 / 0 | 11.140 | 2841.780 | 0 / saving=False | 405 foundation | 407 foundation |
| 1924.777 | 0 / 0 | 11.140 | 2841.780 | 0 / saving=False | 405 foundation | 407 foundation |
| 1982.763 | 0 / 0 | 11.140 | 2841.780 | 0 / saving=False | absent | absent |

## Observations

- Sampled P0 Coin gathering reaches 2841.77978515625 by world 991.9048461914062 and stays at that exact value through all subsequent complete samples including 1982.762939453125.
- At world 961.8121948242188 gathered Coin is 2776.626220703125. The exact moment of cessation is only bounded between that sample and the plateau sample; no mine depletion event was read at that early time.
- At world 1142.3681640625 a paid Janissary 100F/25C train job is present and wallet is 11.1400146484375C. All subsequent samples retain 2930 spent Coin, zero passive Coin and the same wallet. Military queues are empty from world 1202.553466796875 onward in sampled Barracks/Stable/Foundry buildings.
- The original Town1 and Market158 are complete at world 1413.2020263671875. Town1 is absent with unfinished replacement357 at 1443.294677734375; replacement357 is complete by 1533.5726318359375. Complete replacement Market368 is present by 1593.7579345703125. Coin never resumes through those rebuilds.
- P0 agePlan was already targetAge3 before the shortage: saving=true at 961.812 then saving=false targetAge3 from 991.905 through 1413.202; after Town loss targetAge resets to0. At the questioned1804.406 sample it is targetAge0/saving=false, so an active age savings reserve is not shown there.
- Sampled home recovery and later actual enemy attack coexist: workers45 at1804.406 and1834.499,38 at1864.592,14 at1894.684,0 at1924.777. Town357 and Market368 disappear and replacements405/407 remain unfinished.
- The bounded live event window1878.4884033203125 through1924.5771484375 contains actual enemy-caused P0 Settler Death events,250 Move commands,3 Stop commands,2 Build commands and2 AIConstructionResumed events. Its CoinCrate391 Death is not a mine-depletion observation.
- Market is currently an economic technology building. No SurplusExchange or exchange implementation literal occurs in any frozen Simulation .cs read. Market technology rules provide upgrades and river-only bank research, not resource conversion.
- No P0 trade_post is present in any of the67 sampled building snapshots; complete Estate/Bank are also absent. This is a sampled observation, not proof that no short-lived trade_post ever existed between samples.

## Causal limits

- **initial_coin_source_failure / unresolved**: Mine depletion, no currently visible safe reachable Coin, unsafe mineral routes, or interrupted worker orders remain possible. Neither samples nor the bounded late event window exposes the initial worker Gather target or resource remaining at the time Coin gathering ceased.
- **age2_coin_recovery_gap / code_supported_mechanism_compatible_with_samples**: All sampled Ottoman regular Age2 choices Janissary/Abus/Hussar cost at least25/100/80 Coin. Estate requiresAge3 and Bank is river-only; continuity TradeBootstrap is conditioned on NeedsWood and ordinary InvestInRoute is disabled while underAttack/saving. A Coin-only shortfall with large Wood can therefore have no continuity-triggered early sustainable alternative. After a Town loss resets targetAge, starting a new Age3 plan also requires8 soldiers. This explains inability to recover under the observed conditions, without proving the initial loss of safe Coin.
- **town_loss_initial_cause / inconsistent_as_sole_initial_cause**: Coin gathering has already plateaued by991.905 while Town remains complete through1413.202. Subsequent Town losses and enemy attacks exacerbate recovery but cannot alone account for the earlier plateau.
- **active_queue_or_age_budget_deadlock_at1804 / not_supported_by_snapshot**: There is no paid production queue at1804 and agePlan.targetAge0/savingfalse. Actual wallet11.140 is below even a single Janissary25 Coin ordinary payment. Earlier targetAge3 paused savings may be part of defense behavior, but the questioned snapshot is not a reserved-budget stall.
- **unsafe_site_priority_and_worker_evasion / compatible_not_exclusive_cause**: Frozen source prioritizes town/core restoration, housing, barracks/special/market/stable/foundry and rejects unsafe builders/sites. Workforce skips workers with non-Gather orders and may Move away from threats. Late Move/build/Stop events and incomplete replacement foundations fit evacuation/reassignment during an observed lethal raid; no exact worker-to-source attribution can be made from these DTOs.

## Exact read bounds

- Samples: bytes 0..1347034; 134 complete JSON rows; bounded SHA256 8018ade3cecb2b3dc557769d2e7e8371530d82b53f6d15c1ec8f1e6211c17ad0.
- Events: bytes 59301888..61399040 (2MiB last-window bounds at13:47:31UTC); 1878.488..1924.577 world; bounded SHA256 28c1818cf42c6b04443a27d6f16ef9b64498c7861f75bd5ab93a58a36bcb9ae8.
- Both files were originally mutable at the live observation. Re-read was after natural closure; before/after stat stability does not substitute for a formal evidence freeze.
- The final-world.json156MB and full64MB events ledger were not read.
- Exact initial Coin-worker targets are absent from samples; no early depletion event was read.

## Frozen source references

- Assets/Frontier/Simulation/SkirmishAI.cs lines 94-150,207-227,364-427; SHA256 37b2cc8da6fc0c8c54d62feacb102888f0af0d9dcc5e3be660833a6827c46da1; matches frozen evidence=true.
- Assets/Frontier/Simulation/AiResourceContinuity072.cs lines 111-204,208-245; SHA256 2e42dea01cf9482027cd3a0473c8dd7ff675338f8302f6c26a9154985efd8e54; matches frozen evidence=true.
- Assets/Frontier/Simulation/AiWorkforce.cs lines 31-70,87-129; SHA256 fe2407f3c0fc5bd8ba3fb71f59e209297c23097431885ffc083007c8a23c2fc3; matches frozen evidence=true.
- Assets/Frontier/Simulation/AiAgePlan.cs lines 26-57; SHA256 0846b091a7003837759dc5d01e1a11216550db669faf762d1ecb7778a03576c5; matches frozen evidence=true.
- Assets/Frontier/Simulation/AiConstruction.cs lines 14-41; SHA256 16ff2beb45f8ec6e9faebe550e5ce3536d6c34ccd9f12258ff1aa205b8856b34; matches frozen evidence=true.
- Assets/Frontier/Simulation/AiCivilizationEconomy.cs lines 23-40; SHA256 6ca35e16ef31bcfc9d156625cf521aaf6812c4aac0992e9bac8dfa090edb639e; matches frozen evidence=true.
- Assets/Frontier/Simulation/Progression.cs lines 13-22; SHA256 35bbf41e74d00e7285fa1de02c66733e0b430e2c0dc5ce5a88b3e244a7387910; matches frozen evidence=true.
- Assets/Resources/Rules.json lines 736-780,871-915,1348-1394,2034-2097,3858-3877,8740-8770; SHA256 71f075b082da4df6a7a74a8bf4406f6df919133c08023d7652a3d640ec5a9d25; matches frozen evidence=true.

## Case closure observed after live read

Runner result: natural terminal at 1982.763s, Age2, winner1, ended_before_route; routeReached=False. This is a runner record, not a full closed-case audit.
Whole matrix snapshot: 16 closed, running=; final audit pending.

No source/Docs edits, GUI operation, new tests, new simulation, runtime intervention, budget modification, packaging or upload occurred. Original16-case runner now records16closed with status observed_routes_incomplete; independent final audit remains pending.

JSON SHA256: 850346da2ad21a6fd5b04f1bf9c6f506bf322a4d075a7994781eb5b731a12b41
