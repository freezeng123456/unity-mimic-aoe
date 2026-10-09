# 076 ordinary-observation analysis

Raw input: `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate1/NormalObserved1/Observed`

| Case | Player | Civ | Paid queues | Births | Doctor deaths | Positive receipts | Actual healed HP | Patients | Full loops |
|---|---:|---|---:|---:|---:|---:|---:|---:|---:|
| frontier-ottoman-mirror42 | 0 | ottoman | 1 | 1 | 0 | 1550 | 1550.0 | 11 | 7 |
| frontier-ottoman-mirror42 | 1 | ottoman | 1 | 1 | 1 | 1342 | 1342.000015258789 | 15 | 4 |
| winding-ottoman-british44 | 0 | ottoman | 1 | 1 | 0 | 1852 | 2652.7999572753906 | 13 | 9 |
| winding-ottoman-british44 | 1 | coast | 2 | 2 | 0 | 1504 | 2487.2000002861023 | 14 | 14 |

## frontier-ottoman-mirror42

Status: `natural_terminal`; winner: `0` (cap is not victory).

Integrity: `{"eventLines": 87266, "manifestEventCount": 87266, "finalSnapshotEventCount": 87266, "countsAgree": true, "nondecreasingEventTimes": true, "openingAgeOne": true, "comparison1500Present": true, "comparisonMayBeAbsentForEarlyNaturalTerminal": false}`

Timing (not native FPS): `{"wallSeconds": 60.0008134, "stepSeconds": 56.23741899999977, "observerSeconds": 3.7616544, "maxStepMs": 2102.3537, "steps": 17689, "seconds": 1768.615234375, "meanStepMs": 3.179231104075966, "nativeFps": "not_measured"}`

### Player 0

Production: `paid_training_and_birth_observed`.

Exact return reasons: `{"Reached 85% health; ordinary army may rejoin": 9}`.

First wave: `{"eventIndex": 11018, "eventLine": 11019, "time": 468.01934814453125, "kind": "AIArmyPhase", "player": 0, "entity": 0, "target": 0, "amount": 6.0, "message": "Owned soldiers actually assembled for the first wave"}`.

Final recorded economy: `{"age": 4, "resources": {"food": 219.3345184326172, "wood": 278.436767578125, "coin": 68.21599578857422}, "gathered": {"food": 13763.125, "wood": 6837.03515625, "coin": 7545.7109375}, "spent": {"food": 15175.0, "wood": 7225.0, "coin": 9655.0}, "refunded": {"food": 0.0, "wood": 0.0, "coin": 0.0}, "passiveIncome": {"food": 663.9634399414062, "wood": 0.0, "coin": 1168.746826171875}, "tradeIncome": {"food": 675.0, "wood": 375.0, "coin": 200.0}, "marketIncome": {"food": 0.0, "wood": 0.0, "coin": 707.0}, "marketSold": {"food": 1400.0, "wood": 0.0, "coin": 0.0}, "marketBought": {"food": 0.0, "wood": 0.0, "coin": 0.0}, "usedPopulation": 95, "reservedPopulation": 2, "populationCap": 110, "kills": 141, "losses": 64, "trained": 82}`.

Sampled loose demand candidates: 11; exact eligibility windows: unknown.
- Full loop patient 167: Retreat event 12787 @ 510.02191162109375; first positive Heal event 12973 @ 515.7211303710938; 85% Return event 13538 @ 528.0181274414062; Fire event 16305 @ 592.5023803710938.
- Full loop patient 225: Retreat event 24856 @ 782.3560180664062; first positive Heal event 25606 @ 798.8519897460938; 85% Return event 26219 @ 808.7495727539062; Fire event 27512 @ 829.8444213867188.
- Full loop patient 260: Retreat event 37974 @ 1029.4957275390625; first positive Heal event 38400 @ 1036.7939453125; 85% Return event 38877 @ 1043.8922119140625; Fire event 48462 @ 1204.4530029296875.
- Full loop patient 267: Retreat event 38102 @ 1031.8951416015625; first positive Heal event 38133 @ 1032.4949951171875; 85% Return event 39393 @ 1051.0904541015625; Fire event 48502 @ 1204.8529052734375.
- Full loop patient 324: Retreat event 74270 @ 1598.1568603515625; first positive Heal event 76631 @ 1627.0498046875; 85% Return event 78612 @ 1648.5445556640625; Fire event 82288 @ 1695.233154296875.
- Full loop patient 347: Retreat event 74272 @ 1598.1568603515625; first positive Heal event 76479 @ 1625.7501220703125; 85% Return event 79583 @ 1660.5416259765625; Fire event 82291 @ 1695.233154296875.
- Full loop patient 379: Retreat event 78614 @ 1648.5445556640625; first positive Heal event 79810 @ 1663.44091796875; 85% Return event 82417 @ 1696.5328369140625; Fire event 85141 @ 1735.623291015625.

### Player 1

Production: `paid_training_and_birth_observed`.

Exact return reasons: `{"Reached 85% health; ordinary army may rejoin": 4}`.

First wave: `{"eventIndex": 11015, "eventLine": 11016, "time": 468.01934814453125, "kind": "AIArmyPhase", "player": 1, "entity": 0, "target": 0, "amount": 6.0, "message": "Owned soldiers actually assembled for the first wave"}`.

Final recorded economy: `{"age": 4, "resources": {"food": 100.76904296875, "wood": 134.571533203125, "coin": 266.97698974609375}, "gathered": {"food": 13598.6982421875, "wood": 6160.02001953125, "coin": 5710.46826171875}, "spent": {"food": 13785.0, "wood": 6325.0, "coin": 6805.0}, "refunded": {"food": 0.0, "wood": 0.0, "coin": 0.0}, "passiveIncome": {"food": 0.0, "wood": 0.0, "coin": 0.0}, "tradeIncome": {"food": 0.0, "wood": 0.0, "coin": 0.0}, "marketIncome": {"food": 0.0, "wood": 0.0, "coin": 1262.0}, "marketSold": {"food": 4100.0, "wood": 0.0, "coin": 0.0}, "marketBought": {"food": 0.0, "wood": 0.0, "coin": 0.0}, "usedPopulation": 0, "reservedPopulation": 0, "populationCap": 0, "kills": 62, "losses": 141, "trained": 62}`.

Sampled loose demand candidates: 6; exact eligibility windows: unknown.
- Full loop patient 186: Retreat event 14952 @ 561.6099243164062; first positive Heal event 15413 @ 574.7067260742188; 85% Return event 16460 @ 595.2017211914062; Fire event 17940 @ 627.8937377929688.
- Full loop patient 263: Retreat event 35405 @ 986.3062133789062; first positive Heal event 35752 @ 992.5046997070312; 85% Return event 36961 @ 1010.3003540039062; Fire event 39079 @ 1046.8914794921875.
- Full loop patient 331: Retreat event 51539 @ 1264.6383056640625; first positive Heal event 51904 @ 1271.9365234375; 85% Return event 52749 @ 1283.8336181640625; Fire event 71159 @ 1557.3668212890625.
- Full loop patient 336: Retreat event 52226 @ 1276.6353759765625; first positive Heal event 52751 @ 1283.93359375; 85% Return event 54866 @ 1312.6265869140625; Fire event 71083 @ 1556.5670166015625.

## winding-ottoman-british44

Status: `observation_cap`; winner: `-1` (cap is not victory).

Integrity: `{"eventLines": 175321, "manifestEventCount": 175321, "finalSnapshotEventCount": 175321, "countsAgree": true, "nondecreasingEventTimes": true, "openingAgeOne": true, "comparison1500Present": true, "comparisonMayBeAbsentForEarlyNaturalTerminal": false}`

Timing (not native FPS): `{"wallSeconds": 278.8273427, "stepSeconds": 271.89580390000026, "observerSeconds": 6.9299382, "maxStepMs": 2199.3166, "steps": 24001, "seconds": 2400.090576171875, "meanStepMs": 11.32851980750803, "nativeFps": "not_measured"}`

### Player 0

Production: `paid_training_and_birth_observed`.

Exact return reasons: `{"Reached 85% health; ordinary army may rejoin": 9, "Recovery member lost": 1}`.

First wave: `{"eventIndex": 13743, "eventLine": 13744, "time": 475.21978759765625, "kind": "AIArmyPhase", "player": 0, "entity": 0, "target": 0, "amount": 6.0, "message": "Owned soldiers actually assembled for the first wave"}`.

Final recorded economy: `{"age": 4, "resources": {"food": 633.37548828125, "wood": 4131.90673828125, "coin": 352.4051208496094}, "gathered": {"food": 19833.15625, "wood": 19325.40625, "coin": 12808.5869140625}, "spent": {"food": 20875.0, "wood": 16007.853515625, "coin": 17871.869140625}, "refunded": {"food": 0.0, "wood": 0.0, "coin": 0.0}, "passiveIncome": {"food": 491.8078308105469, "wood": 0.0, "coin": 3566.6845703125}, "tradeIncome": {"food": 900.0, "wood": 500.0, "coin": 800.0}, "marketIncome": {"food": 0.0, "wood": 0.0, "coin": 982.0}, "marketSold": {"food": 2700.0, "wood": 0.0, "coin": 0.0}, "marketBought": {"food": 0.0, "wood": 0.0, "coin": 0.0}, "usedPopulation": 72, "reservedPopulation": 6, "populationCap": 80, "kills": 206, "losses": 187, "trained": 162}`.

Sampled loose demand candidates: 26; exact eligibility windows: unknown.
- Full loop patient 267: Retreat event 41535 @ 998.3032836914062; first positive Heal event 42590 @ 1016.3988647460938; 85% Return event 43568 @ 1029.4957275390625; Fire event 44064 @ 1035.59423828125.
- Full loop patient 420: Retreat event 95507 @ 1703.7310791015625; first positive Heal event 96345 @ 1713.228759765625; 85% Return event 99325 @ 1737.3228759765625; Fire event 100790 @ 1750.3197021484375.
- Full loop patient 420: Retreat event 102411 @ 1766.1158447265625; first positive Heal event 103375 @ 1775.9134521484375; 85% Return event 106715 @ 1806.9058837890625; Fire event 107782 @ 1819.5028076171875.
- Full loop patient 420: Retreat event 109921 @ 1842.8970947265625; first positive Heal event 110013 @ 1844.0968017578125; 85% Return event 111659 @ 1859.6929931640625; Fire event 114554 @ 1888.8858642578125.
- Full loop patient 444: Retreat event 111661 @ 1859.6929931640625; first positive Heal event 113047 @ 1874.189453125; 85% Return event 113555 @ 1878.8883056640625; Fire event 114770 @ 1890.6854248046875.
- Full loop patient 491: Retreat event 113557 @ 1878.8883056640625; first positive Heal event 116453 @ 1903.38232421875; 85% Return event 117618 @ 1912.4801025390625; Fire event 123530 @ 1964.367431640625.
- Full loop patient 444: Retreat event 123340 @ 1962.8677978515625; first positive Heal event 124932 @ 1977.564208984375; 85% Return event 125697 @ 1984.4625244140625; Fire event 127013 @ 1996.8594970703125.
- Full loop patient 589: Retreat event 136836 @ 2091.289306640625; first positive Heal event 138785 @ 2110.407958984375; 85% Return event 140169 @ 2122.519775390625; Fire event 141765 @ 2137.033935546875.
- Full loop patient 588: Retreat event 140558 @ 2126.123291015625; first positive Heal event 142405 @ 2142.038818359375; 85% Return event 144542 @ 2157.353759765625; Fire event 150773 @ 2208.103271484375.

### Player 1

Production: `paid_training_and_birth_observed`.

Exact return reasons: `{"Reached 85% health; ordinary army may rejoin": 14, "Recovery member lost": 1}`.

First wave: `{"eventIndex": 8515, "eventLine": 8516, "time": 352.81231689453125, "kind": "AIArmyPhase", "player": 1, "entity": 0, "target": 0, "amount": 6.0, "message": "Owned soldiers actually assembled for the first wave"}`.

Final recorded economy: `{"age": 5, "resources": {"food": 929.028564453125, "wood": 7775.177734375, "coin": 389.51312255859375}, "gathered": {"food": 28215.6875, "wood": 22877.96875, "coin": 13789.775390625}, "spent": {"food": 28880.0, "wood": 15994.8017578125, "coin": 21232.22265625}, "refunded": {"food": 0.0, "wood": 0.0, "coin": 0.0}, "passiveIncome": {"food": 47.84996032714844, "wood": 0.0, "coin": 6192.5693359375}, "tradeIncome": {"food": 1275.0, "wood": 625.0, "coin": 1150.0}, "marketIncome": {"food": 0.0, "wood": 0.0, "coin": 375.0}, "marketSold": {"food": 600.0, "wood": 0.0, "coin": 0.0}, "marketBought": {"food": 0.0, "wood": 0.0, "coin": 0.0}, "usedPopulation": 129, "reservedPopulation": 9, "populationCap": 150, "kills": 185, "losses": 207, "trained": 254}`.

Sampled loose demand candidates: 39; exact eligibility windows: unknown.
- Full loop patient 176: Retreat event 42668 @ 1017.4985961914062; first positive Heal event 43270 @ 1026.2965087890625; 85% Return event 43960 @ 1034.2945556640625; Fire event 46767 @ 1070.78564453125.
- Full loop patient 276: Retreat event 46283 @ 1063.0875244140625; first positive Heal event 48112 @ 1086.78173828125; 85% Return event 49593 @ 1103.8775634765625; Fire event 51795 @ 1135.769775390625.
- Full loop patient 261: Retreat event 56913 @ 1202.2535400390625; first positive Heal event 57417 @ 1209.5517578125; 85% Return event 58047 @ 1216.6500244140625; Fire event 73269 @ 1434.896728515625.
- Full loop patient 295: Retreat event 56915 @ 1202.2535400390625; first positive Heal event 57457 @ 1210.0516357421875; 85% Return event 58046 @ 1216.6500244140625; Fire event 73278 @ 1434.9967041015625.
- Full loop patient 176: Retreat event 58049 @ 1216.6500244140625; first positive Heal event 58483 @ 1223.2484130859375; 85% Return event 59082 @ 1231.0465087890625; Fire event 73191 @ 1434.096923828125.
- Full loop patient 445: Retreat event 102863 @ 1770.9146728515625; first positive Heal event 103254 @ 1774.9136962890625; 85% Return event 105011 @ 1790.1099853515625; Fire event 107503 @ 1816.3035888671875.
- Full loop patient 392: Retreat event 104461 @ 1785.3111572265625; first positive Heal event 105452 @ 1794.009033203125; 85% Return event 105785 @ 1797.3082275390625; Fire event 114552 @ 1888.8858642578125.
- Full loop patient 423: Retreat event 113788 @ 1881.2877197265625; first positive Heal event 115655 @ 1897.78369140625; 85% Return event 117072 @ 1907.6812744140625; Fire event 123552 @ 1964.5673828125.
- Full loop patient 494: Retreat event 120374 @ 1938.8736572265625; first positive Heal event 121724 @ 1950.970703125; 85% Return event 123337 @ 1962.8677978515625; Fire event 128764 @ 2013.555419921875.
- Full loop patient 512: Retreat event 124177 @ 1970.0660400390625; first positive Heal event 126187 @ 1989.361328125; 85% Return event 126979 @ 1996.4595947265625; Fire event 129485 @ 2020.5537109375.
- Full loop patient 530: Retreat event 127219 @ 1998.8590087890625; first positive Heal event 129535 @ 2021.0535888671875; 85% Return event 130527 @ 2030.0513916015625; Fire event 133993 @ 2064.763427734375.
- Full loop patient 446: Retreat event 130529 @ 2030.0513916015625; first positive Heal event 134832 @ 2074.272705078125; 85% Return event 136029 @ 2084.082275390625; Fire event 144076 @ 2154.450927734375.
- Full loop patient 635: Retreat event 155787 @ 2248.642822265625; first positive Heal event 156467 @ 2254.548583984375; 85% Return event 158546 @ 2270.263916015625; Fire event 161974 @ 2296.089111328125.
- Full loop patient 446: Retreat event 160908 @ 2288.281494140625; first positive Heal event 165249 @ 2320.813232421875; 85% Return event 166576 @ 2330.322509765625; Fire event 174726 @ 2395.786376953125.

## Evidence boundaries

- Two declared ordinary cases are coverage evidence, not a balance comparison.
- An observation cap is not a victory; only a natural terminal has a winner.
- Thirty-second samples can miss short wounds, queue jobs, eligibility windows and deaths.
- missingMilitaryHp includes garrisoned/aboard land soldiers; AI medical Deficit excludes them. Its >=100 value does not prove medical eligibility.
- Sample units omit garrisonedIn, complete state, attack preparation and full queued orders. Do not infer precise admission eligibility from samples alone.
- Samples omit underAttack, age-saving and component protected reserves. A zero-purchase cause is unknown unless supported by full checkpoints/event evidence or a separately declared read-only diagnostic continuation.
- A healthy-return event is accepted only with the exact declared 85% reason. Timeout, unavailable, death, manual override and safety releases are separate outcomes.
- A full loop requires one patient episode: Retreat, positive HealingReceipt, exact 85% Return, then that patient's Fire before its next Retreat or Death.
- Heal-only, doctor birth-only and timeout-return-fire routes are not full medical loops. Worker/scout healing is not automatically military recovery.
- Money fields preserve recorded numeric values; no rounded wallet reconciliation or invented reserve balance is performed.
- Step timings describe this headless observer, not native rendering FPS. maxStepMs is a maximum, not a percentile.

## Zero-output inspection order

1. **Integrity and scope**: manifest version/rulesHash/sourceFingerprint; two expected case identities; opening Age I/default opening; final event counts; natural_terminal versus observation_cap. A missing, incomplete or mismatched artifact is an evidence gap, not AI zero output.
2. **No order versus paid queue versus birth**: doctor ProductionQueued, immediately preceding same-player/producer/time Payment transaction, UnitTrained target doctor ID, Spawn, Refund and producer/doctor Death. No queue: investment was not accepted. Paid queue but no birth: inspect queue progress, exit blockage, producer death or cancellation. Birth then death: not zero production.
3. **Actual demand preconditions**: age>=2, workers>=12, non-explorer land soldiers>=6, actual eligible available soldier missing HP>=100, live+queued doctor count; second doctor requires soldiers>=20 and missing HP>=250. Thirty-second missingMilitaryHp is only a loose candidate. Recompute eligible available roles from a full checkpoint; brief unsampled windows stay unknown.
4. **Investment permission gates**: same-decision !saveForAge and !underAttack, active/nonterminal/nonpaused player; AIStrategy/AgeAdvanced and full simulation agePlan provide partial evidence. Do not equate a large wallet with an open investment gate. Exact underAttack and decision-time gates are absent from samples.
5. **Producer and real production legality**: completed available mosque/church with empty queue, civilization/age eligibility, population used+reserved+1<=capacity, doctor cap, exact unit cost. Separate absent/unfinished/busy producer, population lock, unaffordable unit and production-exit delay. Opening/1500/final checkpoints are point evidence only.
6. **Budget and queue ordering**: ordinary military/research payments occur before medical investment; protected age/political/worker/bank/special/civilization/siege/resource-continuity reserves; mosque critical worker-limit versus optional speed research. Wallet minus displayed unit cost is not the protected-spending test. Missing decision-time reserve values remain unknown; never infer a precise reserve cause from aggregate spent.
7. **Birth but no positive healing**: doctor order/status/autoHeal/blockedUntil, actual positive receipt, available wounded patient, movement/combat lock/range/line; full checkpoint medical station and recovery cohort. Distinguish no wound, all patients fighting/moving/aboard, physician movement/busy orders, death and no safe reachable station. No sampled healingNow does not prove no healing between samples.
8. **Recovery and return to combat**: same-ID episode indexes/times, positive receipt HP delta, exact Return reason, first subsequent Fire before next Retreat/Death, timeout<=90s review allowance and retry cooldown state. Report successful loop, heal without return, successful return without fire, timeout/safety/manual/unavailable/death separately; no forced commands to manufacture coverage.

All event references use original zero-based eventIndex and one-based eventLine; time ties preserve file order. Per-case event-index.jsonl records every event. Raw files were read only.
