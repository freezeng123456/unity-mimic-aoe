# River normal-player attempt005 — failed strict C, preserved

C5 ended naturally in **Defeat at1661.541382 simulation seconds (27m41s)**. River mirror, Easy, seed314159, V cap, Military25/25, normal1x and autoplay false. Canonical strict C returns **16/17 checks and18/19 coverage**; the only gap is **`ageV`**. PlayerV was neither paid nor completed. This remains a failed C attempt; other matches are not combined with it. See [strict summary](summary.json), [invocation and exit1](audit-invocation.json), [CLI](verify-player-cli.txt) and [archive verification](verification.json).

## Identity, raw records and preservation

- Match `d69b5316feda422f8a27d23545e13c5b`, version0.2.31, GUID`128a731d440e4b7a94abfcc74b88fa97`, source fingerprint`1b45f97f3d90c9b76388419492a83c5622ccb2cf3a365030044403ccd309c1c7`. [Frozen build audit](frozen-build-audit.json) verified all357 current source hashes and three .31 runtime hashes before any .32 change. Finalization preserves that result without re-reading current source or app.
- [Natural-end report](20261003-081035-834-d69b5316feda422f8a27d23545e13c5b-match_finished.json) / [events](20261003-081035-834-d69b5316feda422f8a27d23545e13c5b-match_finished-events.jsonl) contain79,417 records. MatchEnded is original line79380; noResign exists, living units/buildings reconstruct to[0,95]. PlayerIV versus opponentV, kills/losses98/96, shipments9 versus12. [Natural-end/age audit](natural-end-and-age-audit.json).
- [Menu-exit report](20261003-081203-427-d69b5316feda422f8a27d23545e13c5b-menu_exit.json) / [events](20261003-081203-427-d69b5316feda422f8a27d23545e13c5b-menu_exit-events.jsonl) have identical raw events; report differences are only reason and managedBytes. [Pair audit](report-pair-audit.json).
- Frozen [ended log](native-input-full-Player-ended.log):1,141,452B, SHA`7b65d7aeed3316d20516540518aaa0d26ab99c2be483d8c0a94c12f75e71cded`. Frozen [menu log](native-input-full-Player-menu.log):1,146,985B, SHA`72baf7f599209dd49578596d0f0988da6be8fcd86d3da617d6908e74498a0cc0`. Ended bytes are an exact menu-log prefix. Complete snapshots retain earlier process history; [C5 excerpt](native-input-c5-menu-through-exit.log) is full-log lines7652–11916, from the opponent-menu click through final menu F9. [Log audit](log-preservation-audit.json).

No later live shared log, report, capture or GUI was read. The [preliminary checkpoint](preliminary-archive-receipt.json) retains its original subset hashes; the [final receipt](archive-receipt.json) covers every final file except itself.

## Paid cannon damages enemy Foundry

Foundry235 paid100W250C at836.842712s (Payment27038, queue27039). Field Cannon261 actually spawned at881.931702s (29161, completion29162). Enemy Foundry239 is player1/Building, spawned812.448669s (25914) at(96,111). Attack command34219 at973.209412s targets239.

Raw positive Siege damage: **line34455 at978.008240s:130**, **line34716 at983.106995s:130**; total260. Cannon261 later died1104.877319s. [Independent cannon audit](cannon-building-audit.json) links payment/birth, target identity, command and both hits, and agrees with canonical evidence lines.

The972.209656s F9 selects enemy Foundry239 and shows1760/1760HP. At981.107483s, F9 selects Cannon261, showsReloading/Orders1, and the Foundry bar is reduced; this image follows the first hit and precedes the second. Exact two-hit damage is taken from raw Damage, not the health-bar pixels or Attack toast.

## Same old Musketeer before/after and new upgraded Musketeer

Veteran Musketeer was paid796.252625s (25116), applied/completed836.342834s (27016/27017). Old paid Musketeer173 was born338.111420s (7361), died918.922668s (31038), and remained alive for both captures:

| Capture time | Frozen UI context line | Entity/count | Actual pixel reading |
|---:|---:|---|---|
|825.245544s|9488|173 /1|HP200/200, ATK24|
|838.142395s|9577|173 /1|HP240/240, ATK29|
|874.833435s|9745|256 /1|HP240/240, ATK29|

New Musketeer256 was paid after technology at840.941711s (27241), born866.935364s (28456), and died931.919495s (31795). Its874.833435s single-unit display therefore belongs to a newly paid and born upgraded unit. Frozen rules give200HP/24attack and +20% →240HP/28.8attack, shown as29. The old173 pair uses the same entity ID, unlike C3's different pre/post IDs.

**F9 JSON has no selectedEntity field.** Entity associations use the preceding original `FRONTIER_UI context` line, its frame/world time and raw Spawn/Death/payment lineage; selected-unit HP/ATK are read separately from actual PNGs. [Visual and technology audit](visual-and-technology-audit.json) contains all payloads, checks and nine explicit visual readings. This demonstrates the C5 technology behavior, while strict C still fails on ageV.

## Paid progression and the remaining gap

|Age|Paid(s)|Completed(s)|Payment/completion raw line|
|---|---:|---:|---:|
| 2 | 102.099014 | 192.202530 | 2052 / 3956 |
| 3 | 667.384094 | 777.457214 | 19932 / 24277 |
| 4 | 1023.397156 | 1153.465454 | 36765 / 44724 |

Paid births:22 Settlers,20 Musketeers,10 Pikemen,5 Field Cannons,5 River Carabineers and5 Hussars. Paid buildings:3 houses,1 barracks,1Trading Post,4banks,1Foundry,1Stable and2Farms. Five paid Carabineers are visible in the1080.583252s F9; IV is visible at1155.265015s. NoV screenshot is asserted because V was not reached.

Cargo Ledgers was paid366.013123s, completed411.115875s. Trade switched toFood at537.215881s; seven XP deliveries total330XP,21 Food deliveries total1575F. Four paid banks generated7418C; two treasures were claimed. Final player resources were2924.944824F/122.467712W/2696.176270C. These are ended-game facts, not a cause-of-defeat claim. [Key raw events](key-events-raw.json) retain exact original line numbers.

|Strict coverage|Result|
|---|---|
| `ageII` | PASS |
| `ageIII` | PASS |
| `ageIV` | PASS |
| `ageV` | **FAIL** |
| `paidWorkers` | PASS |
| `paidMilitary` | PASS |
| `paidCannon` | PASS |
| `cannonEnemyBuildingDamage` | PASS |
| `gathered` | PASS |
| `shipmentArrived` | PASS |
| `paidBank` | PASS |
| `bankIncome` | PASS |
| `specialUnit` | PASS |
| `paidTechnology` | PASS |
| `treasure` | PASS |
| `paidTradePost` | PASS |
| `tradeXP` | PASS |
| `resourceMode` | PASS |
| `resourceTradeDelivery` | PASS |

## All17 F9 pairs

Two menus and15 in-match PNG/JSON pairs are retained at2560×1440. Hashes, dimensions, version/GUID, match/request/HUD identities and times pass. Only the nine images listed in the visual audit were visually interpreted; the remainder are preserved without invented descriptions. In-menu matchId/requestMatchId are empty; hidden prior HUD values are not assigned to a new match.

|#|Simulation time(s)|Original pair|Full menu-log line|
|---:|---:|---|---:|
| 1 | menu | [Frontier-20261003-063821-899-f622021-076601c824544cdc9d3287e41505b653.png](Frontier-20261003-063821-899-f622021-076601c824544cdc9d3287e41505b653.png) · [JSON](Frontier-20261003-063821-899-f622021-076601c824544cdc9d3287e41505b653.json) | 7661 |
| 2 | 1.400000 | [Frontier-20261003-063847-334-f623528-2a9ecc2ec45343d295b84c11aa69441d.png](Frontier-20261003-063847-334-f623528-2a9ecc2ec45343d295b84c11aa69441d.png) · [JSON](Frontier-20261003-063847-334-f623528-2a9ecc2ec45343d295b84c11aa69441d.json) | 7723 |
| 3 | 321.810425 | [Frontier-20261003-065718-005-f689624-86d2ea870f32455d85b8fabe249918f3.png](Frontier-20261003-065718-005-f689624-86d2ea870f32455d85b8fabe249918f3.png) · [JSON](Frontier-20261003-065718-005-f689624-86d2ea870f32455d85b8fabe249918f3.json) | 8406 |
| 4 | 537.615784 | [Frontier-20261003-070406-903-f713946-c57b52613296462683bd9196a4c4582e.png](Frontier-20261003-070406-903-f713946-c57b52613296462683bd9196a4c4582e.png) · [JSON](Frontier-20261003-070406-903-f713946-c57b52613296462683bd9196a4c4582e.json) | 8937 |
| 5 | 733.068054 | [Frontier-20261003-071123-254-f739904-cb74faf9f3b34047a166740c67c60c17.png](Frontier-20261003-071123-254-f739904-cb74faf9f3b34047a166740c67c60c17.png) · [JSON](Frontier-20261003-071123-254-f739904-cb74faf9f3b34047a166740c67c60c17.json) | 9281 |
| 6 | 779.456726 | [Frontier-20261003-071450-093-f752156-b1ea8dbfaf2d4425929c75c94895a541.png](Frontier-20261003-071450-093-f752156-b1ea8dbfaf2d4425929c75c94895a541.png) · [JSON](Frontier-20261003-071450-093-f752156-b1ea8dbfaf2d4425929c75c94895a541.json) | 9305 |
| 7 | 825.245544 | [Frontier-20261003-071956-551-f770435-45d31c25b42b4068bc76c996d3897aef.png](Frontier-20261003-071956-551-f770435-45d31c25b42b4068bc76c996d3897aef.png) · [JSON](Frontier-20261003-071956-551-f770435-45d31c25b42b4068bc76c996d3897aef.json) | 9490 |
| 8 | 838.142395 | [Frontier-20261003-072026-626-f772222-1436100252ea4af0a7ac8639799845e5.png](Frontier-20261003-072026-626-f772222-1436100252ea4af0a7ac8639799845e5.png) · [JSON](Frontier-20261003-072026-626-f772222-1436100252ea4af0a7ac8639799845e5.json) | 9579 |
| 9 | 874.833435 | [Frontier-20261003-072219-823-f778960-899d4aa33414430985f0a13f97cb14c2.png](Frontier-20261003-072219-823-f778960-899d4aa33414430985f0a13f97cb14c2.png) · [JSON](Frontier-20261003-072219-823-f778960-899d4aa33414430985f0a13f97cb14c2.json) | 9747 |
| 10 | 890.929504 | [Frontier-20261003-072257-257-f781184-7b6547416dee41768fc83954f8d70cd6.png](Frontier-20261003-072257-257-f781184-7b6547416dee41768fc83954f8d70cd6.png) · [JSON](Frontier-20261003-072257-257-f781184-7b6547416dee41768fc83954f8d70cd6.json) | 9865 |
| 11 | 972.209656 | [Frontier-20261003-072710-648-f796233-b6bb66caddea4996ba090bbc1a67be58.png](Frontier-20261003-072710-648-f796233-b6bb66caddea4996ba090bbc1a67be58.png) · [JSON](Frontier-20261003-072710-648-f796233-b6bb66caddea4996ba090bbc1a67be58.json) | 10155 |
| 12 | 981.107483 | [Frontier-20261003-072733-398-f797580-dc426f73a2c04af68e1ca7833a09419a.png](Frontier-20261003-072733-398-f797580-dc426f73a2c04af68e1ca7833a09419a.png) · [JSON](Frontier-20261003-072733-398-f797580-dc426f73a2c04af68e1ca7833a09419a.json) | 10176 |
| 13 | 1024.197021 | [Frontier-20261003-072928-339-f804425-0c578c782a60460eb51f35c421a892e0.png](Frontier-20261003-072928-339-f804425-0c578c782a60460eb51f35c421a892e0.png) · [JSON](Frontier-20261003-072928-339-f804425-0c578c782a60460eb51f35c421a892e0.json) | 10309 |
| 14 | 1080.583252 | [Frontier-20261003-073316-867-f818025-e59bc6c162c343838046fa6fcf1c5c9b.png](Frontier-20261003-073316-867-f818025-e59bc6c162c343838046fa6fcf1c5c9b.png) · [JSON](Frontier-20261003-073316-867-f818025-e59bc6c162c343838046fa6fcf1c5c9b.json) | 10500 |
| 15 | 1155.265015 | [Frontier-20261003-073738-674-f833599-eccc2c95d19e4fa996f2e47f676a834a.png](Frontier-20261003-073738-674-f833599-eccc2c95d19e4fa996f2e47f676a834a.png) · [JSON](Frontier-20261003-073738-674-f833599-eccc2c95d19e4fa996f2e47f676a834a.json) | 10738 |
| 16 | 1661.541382 | [Frontier-20261003-081058-030-f952417-14326f7d2987435ca2faf6e58455ce24.png](Frontier-20261003-081058-030-f952417-14326f7d2987435ca2faf6e58455ce24.png) · [JSON](Frontier-20261003-081058-030-f952417-14326f7d2987435ca2faf6e58455ce24.json) | 11861 |
| 17 | menu | [Frontier-20261003-081204-423-f956325-caea6d1f49f94933abe8677439448873.png](Frontier-20261003-081204-423-f956325-caea6d1f49f94933abe8677439448873.png) · [JSON](Frontier-20261003-081204-423-f956325-caea6d1f49f94933abe8677439448873.json) | 11905 |

[Copy audit](source-copy-audit.json), [capture audit](capture-audit.json), [verification](verification.json) and [SHA receipt](archive-receipt.json) preserve the distinction between archive integrity and strict acceptance. No source/core055 receipt/056/canonical document or subsequent game was changed by this archival task.
