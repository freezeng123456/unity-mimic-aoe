# River normal-player attempt 004 — failed strict C, preserved

C4 ended naturally in **Defeat at 994.504211 simulation seconds (16m 34s)**. The frozen `0.2.31` macOS player ran River League versus Coast Compact, Easy, seed 314159, age cap V, Military 25/25, autoplay false and simulation speed 1. The player reached III; the opponent reached IV. Player kills/losses were 29/48 and shipments sent were six.

Canonical strict C returns **16/17 checks and 16/19 coverage items passed**. Only **`ageIV`, `ageV`, `specialUnit`** are missing. **Paid Field Cannon → enemy building damage is confirmed**, as is paid Cargo Ledgers. C4 remains a failed attempt; its successful cannon path is not combined with another match's V/Carabineer path. See [strict summary](summary.json), [exact invocation/exit 1](audit-invocation.json) and [archive verification](verification.json). Integrity PASS does not turn strict C into PASS.

## Identity and preservation

- Match `b4250e2ab6664083ad919de284cc0afb`, version `0.2.31`, GUID `128a731d440e4b7a94abfcc74b88fa97`; source fingerprint `1b45f97f3d90c9b76388419492a83c5622ccb2cf3a365030044403ccd309c1c7`. [Frozen build audit](frozen-build-audit.json) checks all 357 source files and three compiled runtime hashes against the preserved .31 receipts.
- [Natural-end report](20261003-063507-915-b4250e2ab6664083ad919de284cc0afb-match_finished.json) and [raw events](20261003-063507-915-b4250e2ab6664083ad919de284cc0afb-match_finished-events.jsonl) contain 36,218 events. MatchEnded line 36184 at 994.504211 s states all opposing units and buildings eliminated. No Resign exists; reconstructed final living units/buildings are `[0, 77]`. This establishes natural defeat despite the overlay's generic elimination-or-resignation wording.
- [Menu-exit report](20261003-063556-035-b4250e2ab6664083ad919de284cc0afb-menu_exit.json) and [events](20261003-063556-035-b4250e2ab6664083ad919de284cc0afb-menu_exit-events.jsonl) retain byte-identical event data; report differences are only `reason` and `managedBytes`. See [pair audit](report-pair-audit.json).
- [Full ended log](native-input-full-Player-ended.log): 732,582 bytes, SHA256 `763036d6b69eb8285651c303ec9bfb85864d782255e98d5e75c9e797cda17672`.
- [Full menu log](native-input-full-Player-menu.log): 738,112 bytes, SHA256 `a3de7b8f1370ced87bc93bb604ab1af5aeeab8c1f9168b00d7ba804c8f79e7ee`. The ended log is its exact byte prefix. These immutable shared-process snapshots include C2/C3 history; the [exact C4 excerpt](native-input-c4-menu-through-exit.log) covers full-log lines 5270–7651, starting with the River-to-Coast opponent menu click. [Log preservation audit](log-preservation-audit.json) records boundaries and byte identities.
- The helper's timestamped duplicate of the menu snapshot and its preliminary receipt are retained as the unchanged initial preservation checkpoint. [Final receipt](archive-receipt.json) covers every final archive file except itself.

No later live process log or match report was read. All gameplay interpretation below uses this ended C4's copied files.

## Paid cannon and actual building damage

Foundry entity 224 paid for one Field Cannon at **684.479919 s**, raw Payment line 21579 and queue line 21580: **100 Wood / 250 Coin**. Cannon entity **238** spawned at **729.568909 s**, line 23415, followed by paid-training completion line 23416. Enemy entity **156** is a player-1 Manor (`house`, Building kind 1), spawned at 242.505600 s, line 4962.

Raw Command line 25968 at **783.055847 s** is `Attack ×1`, entity 238, target 156. Positive building damage follows:

| Raw line | Time (s) | Source → target | Siege damage |
|---:|---:|---|---:|
| 26852 | 800.651550 | 238 → 156 | 130 |
| 27080 | 805.750305 | 238 → 156 | 130 |
| 27284 | 810.849060 | 238 → 156 | 130 |
| 27489 | 815.947815 | 238 → 156 | 130 |
| 27693 | 821.046570 | 238 → 156 | 130 |
| 27933 | 826.145325 | 238 → 156 | 130 |

Total recorded Siege damage is **780**. The cannon died at 826.745178 s after those six hits. [Independent cannon audit](cannon-building-audit.json) preserves complete payloads, identities and paid lineage; its result agrees with the canonical evidence.

The 784.655457 s F9 shows the single cannon still Moving; it is not yet hit proof. The 803.050964 s F9 shows Reloading and a partially reduced Manor health bar. The preceding native UI context (full log line 7247) selects entity 238, count 1; F9 JSON itself has no selectedEntity field. The exact six-hit conclusion comes from Damage events rather than GUI acceptance or estimated pixels. [Visual/feature audit](visual-and-feature-audit.json) separates these observations.

## Progress and remaining gaps

| Age reached | Paid at (s) | Completed at (s) | Payment / completion raw line |
|---|---:|---:|---:|
| 2 | 101.999016 | 192.102524 | 2085 / 4014 |
| 3 | 540.815002 | 650.888123 | 16241 / 20484 |

Player paid births were 14 Settlers, five Pikemen, eight Musketeers and one Field Cannon. Paid buildings include one house, bank, Trading Post, barracks, tower, Foundry and reconstructed Town Center. Initial TC entity 1 died at 801.351379 s; a new TC entity 249 was paid at 844.440857 s and completed at 867.335266 s. It did not produce a natural win or further age completion.

Cargo Ledgers was paid at **343.911774 s** (line 8489), applied/completed at **389.014526 s** (lines 10344/10345). It is the only paid player technology; **no Veteran Musketeer upgrade or before/after upgraded-unit evidence is claimed**. Trading Post 159 switched to Coin at 474.919769 s, directly visible in its F9. C4 had six XP deliveries totaling 270 XP, 18 Coin deliveries totaling 900 Coin, 570.5 passive bank Coin, and two claimed treasures. These preserve the paidTechnology, trade and bank coverage without implying IV/V or Carabineer completion. [Key raw payloads](key-events-raw.json) retain original one-based event lines.

| Strict coverage | Result |
|---|---|
| `ageII` | PASS |
| `ageIII` | PASS |
| `ageIV` | **FAIL** |
| `ageV` | **FAIL** |
| `paidWorkers` | PASS |
| `paidMilitary` | PASS |
| `paidCannon` | PASS |
| `cannonEnemyBuildingDamage` | PASS |
| `gathered` | PASS |
| `shipmentArrived` | PASS |
| `paidBank` | PASS |
| `bankIncome` | PASS |
| `specialUnit` | **FAIL** |
| `paidTechnology` | PASS |
| `treasure` | PASS |
| `paidTradePost` | PASS |
| `tradeXP` | PASS |
| `resourceMode` | PASS |
| `resourceTradeDelivery` | PASS |

## Complete F9 inventory

All **16** original PNG/JSON pairs are preserved: two menus and fourteen in-match captures. All PNG hashes, dimensions (2560×1440), build/match identities and world/request/HUD times were checked. Seven images listed in [visual/feature audit](visual-and-feature-audit.json) were visually interpreted. In-menu matchId/requestMatchId are empty; hidden HUD values may retain prior state and are not assigned to the menu's match identity.

| # | Simulation time (s) | Original pair | Full menu-log line |
|---:|---:|---|---:|
| 1 | menu | [Frontier-20261003-054706-655-f439321-74109ea7b5cb44edb8611676f1b7a998.png](Frontier-20261003-054706-655-f439321-74109ea7b5cb44edb8611676f1b7a998.png) · [JSON](Frontier-20261003-054706-655-f439321-74109ea7b5cb44edb8611676f1b7a998.json) | 5279 |
| 2 | 0.900000 | [Frontier-20261003-054720-934-f440168-9f3b8ff2f56a4e118002efbfb998afe9.png](Frontier-20261003-054720-934-f440168-9f3b8ff2f56a4e118002efbfb998afe9.png) · [JSON](Frontier-20261003-054720-934-f440168-9f3b8ff2f56a4e118002efbfb998afe9.json) | 5303 |
| 3 | 68.799522 | [Frontier-20261003-055030-248-f451386-0e497de687214d5b97597efc187543ba.png](Frontier-20261003-055030-248-f451386-0e497de687214d5b97597efc187543ba.png) · [JSON](Frontier-20261003-055030-248-f451386-0e497de687214d5b97597efc187543ba.json) | 5555 |
| 4 | 338.611450 | [Frontier-20261003-060201-114-f492463-64bba3f6386343178445aae94b4b4e4d.png](Frontier-20261003-060201-114-f492463-64bba3f6386343178445aae94b4b4e4d.png) · [JSON](Frontier-20261003-060201-114-f492463-64bba3f6386343178445aae94b4b4e4d.json) | 6066 |
| 5 | 474.919769 | [Frontier-20261003-061111-735-f525220-d969c479c3ce41289449a2df7b783008.png](Frontier-20261003-061111-735-f525220-d969c479c3ce41289449a2df7b783008.png) · [JSON](Frontier-20261003-061111-735-f525220-d969c479c3ce41289449a2df7b783008.json) | 6423 |
| 6 | 597.101257 | [Frontier-20261003-061538-311-f541053-272f6796f1ef4cd3b783c1f03dfeca36.png](Frontier-20261003-061538-311-f541053-272f6796f1ef4cd3b783c1f03dfeca36.png) · [JSON](Frontier-20261003-061538-311-f541053-272f6796f1ef4cd3b783c1f03dfeca36.json) | 6718 |
| 7 | 600.800354 | [Frontier-20261003-061621-999-f543640-7e9c1d9c84e14464b482ca066078e0ca.png](Frontier-20261003-061621-999-f543640-7e9c1d9c84e14464b482ca066078e0ca.png) · [JSON](Frontier-20261003-061621-999-f543640-7e9c1d9c84e14464b482ca066078e0ca.json) | 6782 |
| 8 | 653.587463 | [Frontier-20261003-061738-379-f548158-693c1f1d04a949b4935e34b93dae8b3a.png](Frontier-20261003-061738-379-f548158-693c1f1d04a949b4935e34b93dae8b3a.png) · [JSON](Frontier-20261003-061738-379-f548158-693c1f1d04a949b4935e34b93dae8b3a.json) | 6830 |
| 9 | 733.367981 | [Frontier-20261003-062145-201-f562830-9f1841867b1345419a6a88a3e18d88dd.png](Frontier-20261003-062145-201-f562830-9f1841867b1345419a6a88a3e18d88dd.png) · [JSON](Frontier-20261003-062145-201-f562830-9f1841867b1345419a6a88a3e18d88dd.json) | 7040 |
| 10 | 736.967102 | [Frontier-20261003-062223-229-f565087-3ed5dbd0a9d74d9caa732f1630c5a8c6.png](Frontier-20261003-062223-229-f565087-3ed5dbd0a9d74d9caa732f1630c5a8c6.png) · [JSON](Frontier-20261003-062223-229-f565087-3ed5dbd0a9d74d9caa732f1630c5a8c6.json) | 7081 |
| 11 | 740.866150 | [Frontier-20261003-062252-349-f566814-1b219783633c4935aab8e6a3da5b4477.png](Frontier-20261003-062252-349-f566814-1b219783633c4935aab8e6a3da5b4477.png) · [JSON](Frontier-20261003-062252-349-f566814-1b219783633c4935aab8e6a3da5b4477.json) | 7135 |
| 12 | 784.655457 | [Frontier-20261003-062538-114-f576670-b2037778d210430dbbeddfe5ea3760f2.png](Frontier-20261003-062538-114-f576670-b2037778d210430dbbeddfe5ea3760f2.png) · [JSON](Frontier-20261003-062538-114-f576670-b2037778d210430dbbeddfe5ea3760f2.json) | 7257 |
| 13 | 803.050964 | [Frontier-20261003-062556-519-f577747-8b93d16b6abf4075953200e2f452288e.png](Frontier-20261003-062556-519-f577747-8b93d16b6abf4075953200e2f452288e.png) · [JSON](Frontier-20261003-062556-519-f577747-8b93d16b6abf4075953200e2f452288e.json) | 7270 |
| 14 | 985.606384 | [Frontier-20261003-063445-232-f609155-716c95be6e6e465ca390ca7f873da957.png](Frontier-20261003-063445-232-f609155-716c95be6e6e465ca390ca7f873da957.png) · [JSON](Frontier-20261003-063445-232-f609155-716c95be6e6e465ca390ca7f873da957.json) | 7565 |
| 15 | 994.504211 | [Frontier-20261003-063525-645-f611543-8791e70cdc32413a9cfe2689766f3412.png](Frontier-20261003-063525-645-f611543-8791e70cdc32413a9cfe2689766f3412.png) · [JSON](Frontier-20261003-063525-645-f611543-8791e70cdc32413a9cfe2689766f3412.json) | 7596 |
| 16 | menu | [Frontier-20261003-063557-467-f613428-cc2051205d5d44c98bc47a9e0d889aa0.png](Frontier-20261003-063557-467-f613428-cc2051205d5d44c98bc47a9e0d889aa0.png) · [JSON](Frontier-20261003-063557-467-f613428-cc2051205d5d44c98bc47a9e0d889aa0.json) | 7640 |

The existing strict verifier was run once on the archived C4 with scenario `river-v`, deck preset `military`, its opening menu F9 and the frozen .31 version/GUID. [CLI output](verify-player-cli.txt), [copy audit](source-copy-audit.json), [capture audit](capture-audit.json), [verification](verification.json) and [final SHA receipt](archive-receipt.json) make the outcome reviewable. No source, core055 receipt, 056 evidence, other canonical document, GUI, commit or push was changed by this archival task.
