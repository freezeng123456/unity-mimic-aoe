# River normal-player attempt 003 — failed strict C, preserved

C3 ended naturally in **Defeat at 1781.212158 simulation seconds (29m 41s)**. The frozen `0.2.31` macOS player ran River League versus River League, Easy, seed 314159, age cap V, Military 25/25, autoplay false and simulation speed 1. Both players reached V. The player recorded 95 kills, 106 losses and 10 shipments. This is a failed attempt, not completion of C.

The canonical strict verifier returns **16/17 checks and 18/19 coverage items passed**. Its sole coverage gap is **`cannonEnemyBuildingDamage`**. Eight paid Field Cannons completed training, but all 153 player Field Cannon damage events hit enemy units; none hit an enemy building. See [strict summary](summary.json), [exact invocation and exit code](audit-invocation.json), [independent cannon scan](cannon-damage-audit.json) and [archive verification](verification.json). The latter's integrity PASS does not change strict C's FAIL.

## Identity and raw preservation

- Match: `8fb300efbc1a4169a953d9dab2b7caad`; version `0.2.31`; GUID `128a731d440e4b7a94abfcc74b88fa97`.
- Source fingerprint: `1b45f97f3d90c9b76388419492a83c5622ccb2cf3a365030044403ccd309c1c7`. [Frozen build audit](frozen-build-audit.json) confirms all 357 source hashes and all three compiled runtime hashes against the copied 055 receipts. No runtime was launched for archiving.
- [Natural end report](20261003-054153-740-8fb300efbc1a4169a953d9dab2b7caad-match_finished.json) and [raw events](20261003-054153-740-8fb300efbc1a4169a953d9dab2b7caad-match_finished-events.jsonl): 90,759 events. MatchEnded line 90721 says all opposing units and buildings eliminated; no Resign; final player/opponent living units and buildings `[0, 107]`. This disambiguates the generic defeat overlay wording.
- [Menu exit report](20261003-054457-232-8fb300efbc1a4169a953d9dab2b7caad-menu_exit.json) and [menu raw events](20261003-054457-232-8fb300efbc1a4169a953d9dab2b7caad-menu_exit-events.jsonl) retain the same match and byte-identical event stream. Report differences are only `reason` and `managedBytes`; see [pair audit](report-pair-audit.json).
- Immediately after natural defeat, the shared C2/C3 process log was frozen as [Player-ended](native-input-full-Player-ended.log): 503,819 bytes, SHA256 `8ea281b865a572b9979449a197b1ac5405433ac5d4903055de40d85bafa2975a`.
- After MAIN MENU and its F9, before another match, [Player-menu](native-input-full-Player-menu.log) was frozen: 509,349 bytes, SHA256 `9f2fcbcc794a28166ec91c6516c67de525940e78ba6cd596279dd8de95f66320`. The ended log is its exact byte prefix. Both include earlier C2 history; the [C3 excerpt](native-input-c3-menu-through-exit.log) is precisely full-log lines 1570–5269. [Log audit](log-preservation-audit.json) records the boundaries.
- The initial helper also retained a timestamped duplicate of the menu snapshot. `preliminary-archive-receipt.json` is the historical initial preservation checkpoint. [Final archive receipt](archive-receipt.json) inventories all final files except itself.

All reads after preservation used these snapshots and explicit C3 archive files. No later match reports, live logs, or GUI were used.

## Paid progress and coverage

| Age reached | Paid at (s) | Completed at (s) | Raw payment / completion line |
|---|---:|---:|---:|
| 2 | 103.998985 | 194.102646 | 2097 / 4018 |
| 3 | 569.408020 | 679.481140 | 17969 / 22279 |
| 4 | 1061.987793 | 1192.056030 | 41570 / 49444 |
| 5 | 1478.486084 | 1638.447021 | 66894 / 79532 |

| Technology | Paid at (s) | Completed at (s) | Raw payment / completion line |
|---|---:|---:|---:|
| cargo_ledgers | 245.505783 | 290.608521 | 5074 / 6316 |
| veteran_musketeer | 726.169739 | 766.259949 | 24526 / 26464 |
| veteran_cannon | 1186.057495 | 1226.147705 | 49016 / 51383 |

Paid completed units: 19 Settlers, 15 Musketeers, 20 Pikemen, eight Field Cannons and ten Carabineers. Paid completed buildings include four Counting Houses and one Trading Post. The post completed at 219.804214 s, delivered three XP payments of 40, switched to Food at 295.008789 s and delivered nine payments of 75 Food (675 total). Player bank income total was 9852.25 Coin. Two treasures were claimed. The full payment→completion lineage is in [summary](summary.json); [key events](key-events-raw.json) preserves selected exact raw payloads with original line numbers.

| Strict coverage | Result |
|---|---|
| `ageII` | PASS |
| `ageIII` | PASS |
| `ageIV` | PASS |
| `ageV` | PASS |
| `paidWorkers` | PASS |
| `paidMilitary` | PASS |
| `paidCannon` | PASS |
| `cannonEnemyBuildingDamage` | **FAIL** |
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

## Technology pixels and entity associations

The pre-upgrade single Musketeer at 665.684509 / 699.176331 s displays HP 200/200 and ATK 24. The old group at 771.558655 s displays HP 240/240 and ATK 29; at 798.652039 s it displays 170/240 and ATK 29. The new single Musketeer at 805.550354 s displays HP 240/240 and ATK 29. Veteran Musketeer completed at 766.259949 s; frozen rules give +20% HP and attack (200→240, 24→28.8 before display rounding).

F9 JSON has **no selected entity ID**. Separate original input contexts associate the pre-upgrade single unit with entity 172 (paid birth 299.009033 s), the old group's first unit with 158 (paid birth 251.206131 s), and the later new single unit with 255 (paid birth 797.952209 s). Birth/death chronology confirms each was alive at its capture. These are different units; this is not a same-unit before/after claim. [Visual and technology audit](visual-and-technology-audit.json) records the source log lines, raw lineage, visual readings and their provenance. The older group and the newly trained unit independently show the upgraded stats.

## Control review and limits

At 1509.378540 s, raw Command line 69115 records `Move ×3`, target 0, for the attempted enemy-cannon click. The later commands at 1530.373413 / 1538.571411 s target entity 333 and explicitly record `Attack ×1` / `Attack ×4`. The earlier movement is not attack evidence. The operator described a cannon/tree overlap click; raw events establish the accepted commands, not a source-code defect.

Only the nine images listed in the visual audit were visually interpreted; all 20 PNG/JSON pairs were byte/hash/dimension/identity/time checked. Menu matchId/requestMatchId are empty; their hidden HUD identity may retain the previous match. Natural end, paid advancement and coverage decisions use the authoritative raw events. This archive makes no fixed-FPS, large-battle or C-pass claim.

## Complete F9 inventory

There are two menu captures and 18 in-match captures, including technology before/after, Age IV, Age V and natural defeat. Original files are preserved unchanged at 2560×1440. Lines refer to the full Player-menu log.

| # | Simulation time (s) | Original pair | Log line |
|---:|---:|---|---:|
| 1 | menu | [Frontier-20261003-041929-566-f126866-5855988223674d9da68fd641bb5888ff.png](Frontier-20261003-041929-566-f126866-5855988223674d9da68fd641bb5888ff.png) · [JSON](Frontier-20261003-041929-566-f126866-5855988223674d9da68fd641bb5888ff.json) | 1579 |
| 2 | 1.800000 | [Frontier-20261003-041946-793-f127884-8b680bdd2027476fa173b491e4c910a3.png](Frontier-20261003-041946-793-f127884-8b680bdd2027476fa173b491e4c910a3.png) · [JSON](Frontier-20261003-041946-793-f127884-8b680bdd2027476fa173b491e4c910a3.json) | 1639 |
| 3 | 241.405533 | [Frontier-20261003-043019-829-f165576-4999e6447573404d8d1588caada2860c.png](Frontier-20261003-043019-829-f165576-4999e6447573404d8d1588caada2860c.png) · [JSON](Frontier-20261003-043019-829-f165576-4999e6447573404d8d1588caada2860c.json) | 2166 |
| 4 | 242.905624 | [Frontier-20261003-043044-708-f167051-9ddda87eb4824d68a824ef3f4c2c9336.png](Frontier-20261003-043044-708-f167051-9ddda87eb4824d68a824ef3f4c2c9336.png) · [JSON](Frontier-20261003-043044-708-f167051-9ddda87eb4824d68a824ef3f4c2c9336.json) | 2199 |
| 5 | 295.408813 | [Frontier-20261003-043755-152-f192644-e735f471062f4bbf85543926721201c2.png](Frontier-20261003-043755-152-f192644-e735f471062f4bbf85543926721201c2.png) · [JSON](Frontier-20261003-043755-152-f192644-e735f471062f4bbf85543926721201c2.json) | 2403 |
| 6 | 325.410645 | [Frontier-20261003-043928-549-f198210-85ef99beab664463938e30a525352563.png](Frontier-20261003-043928-549-f198210-85ef99beab664463938e30a525352563.png) · [JSON](Frontier-20261003-043928-549-f198210-85ef99beab664463938e30a525352563.json) | 2477 |
| 7 | 544.314148 | [Frontier-20261003-045031-067-f237615-2e23f8c0fcff44e5b97d3c8efc957ecb.png](Frontier-20261003-045031-067-f237615-2e23f8c0fcff44e5b97d3c8efc957ecb.png) · [JSON](Frontier-20261003-045031-067-f237615-2e23f8c0fcff44e5b97d3c8efc957ecb.json) | 2805 |
| 8 | 665.684509 | [Frontier-20261003-045413-969-f250891-855a4921db934c568404fd3b3bdcbbe9.png](Frontier-20261003-045413-969-f250891-855a4921db934c568404fd3b3bdcbbe9.png) · [JSON](Frontier-20261003-045413-969-f250891-855a4921db934c568404fd3b3bdcbbe9.json) | 3110 |
| 9 | 699.176331 | [Frontier-20261003-045447-441-f252875-dc5ac5f4c6194f6cba3d614d6672a344.png](Frontier-20261003-045447-441-f252875-dc5ac5f4c6194f6cba3d614d6672a344.png) · [JSON](Frontier-20261003-045447-441-f252875-dc5ac5f4c6194f6cba3d614d6672a344.json) | 3173 |
| 10 | 771.558655 | [Frontier-20261003-045627-307-f258807-991ab280e4134daba162cd61058f121d.png](Frontier-20261003-045627-307-f258807-991ab280e4134daba162cd61058f121d.png) · [JSON](Frontier-20261003-045627-307-f258807-991ab280e4134daba162cd61058f121d.json) | 3331 |
| 11 | 798.652039 | [Frontier-20261003-045749-153-f263666-c1be96f69436415590d1b41123f493dd.png](Frontier-20261003-045749-153-f263666-c1be96f69436415590d1b41123f493dd.png) · [JSON](Frontier-20261003-045749-153-f263666-c1be96f69436415590d1b41123f493dd.json) | 3413 |
| 12 | 805.550354 | [Frontier-20261003-045832-039-f266211-b9e22e80a29443dc8110ced3f66d7b07.png](Frontier-20261003-045832-039-f266211-b9e22e80a29443dc8110ced3f66d7b07.png) · [JSON](Frontier-20261003-045832-039-f266211-b9e22e80a29443dc8110ced3f66d7b07.json) | 3483 |
| 13 | 877.532776 | [Frontier-20261003-050123-311-f276396-e866ddbc569c4b93bc5498822f1771d9.png](Frontier-20261003-050123-311-f276396-e866ddbc569c4b93bc5498822f1771d9.png) · [JSON](Frontier-20261003-050123-311-f276396-e866ddbc569c4b93bc5498822f1771d9.json) | 3705 |
| 14 | 913.224060 | [Frontier-20261003-050307-690-f282617-deb6184afa734795b8d83c5ac0c169b4.png](Frontier-20261003-050307-690-f282617-deb6184afa734795b8d83c5ac0c169b4.png) · [JSON](Frontier-20261003-050307-690-f282617-deb6184afa734795b8d83c5ac0c169b4.json) | 3773 |
| 15 | 1062.787598 | [Frontier-20261003-050656-060-f296205-0eddbb0c9b2245c985bc9243747e8579.png](Frontier-20261003-050656-060-f296205-0eddbb0c9b2245c985bc9243747e8579.png) · [JSON](Frontier-20261003-050656-060-f296205-0eddbb0c9b2245c985bc9243747e8579.json) | 3935 |
| 16 | 1220.149170 | [Frontier-20261003-051213-608-f315085-50d856d4402d47f6b9f37ed7cd2a5270.png](Frontier-20261003-051213-608-f315085-50d856d4402d47f6b9f37ed7cd2a5270.png) · [JSON](Frontier-20261003-051213-608-f315085-50d856d4402d47f6b9f37ed7cd2a5270.json) | 4226 |
| 17 | 1479.285889 | [Frontier-20261003-052801-887-f371493-7253e844dbb24d24b705a3427a644a64.png](Frontier-20261003-052801-887-f371493-7253e844dbb24d24b705a3427a644a64.png) · [JSON](Frontier-20261003-052801-887-f371493-7253e844dbb24d24b705a3427a644a64.json) | 4557 |
| 18 | 1641.946167 | [Frontier-20261003-053534-612-f398380-508b363d32394c0585d35f5dd3f196c7.png](Frontier-20261003-053534-612-f398380-508b363d32394c0585d35f5dd3f196c7.png) · [JSON](Frontier-20261003-053534-612-f398380-508b363d32394c0585d35f5dd3f196c7.json) | 5101 |
| 19 | 1781.212158 | [Frontier-20261003-054203-696-f421350-8f52213dcd064181969955b2e32758a8.png](Frontier-20261003-054203-696-f421350-8f52213dcd064181969955b2e32758a8.png) · [JSON](Frontier-20261003-054203-696-f421350-8f52213dcd064181969955b2e32758a8.json) | 5214 |
| 20 | menu | [Frontier-20261003-054458-093-f431671-e4697b67d45f4d1ab7363d2b101c412c.png](Frontier-20261003-054458-093-f431671-e4697b67d45f4d1ab7363d2b101c412c.png) · [JSON](Frontier-20261003-054458-093-f431671-e4697b67d45f4d1ab7363d2b101c412c.json) | 5258 |

## Offline verification

The exact canonical invocation and exit 1 are retained in [audit-invocation.json](audit-invocation.json); [CLI output](verify-player-cli.txt) and [summary](summary.json) state the single expected failure. [Source copy audit](source-copy-audit.json), [capture audit](capture-audit.json), [frozen audit](frozen-build-audit.json), [pair audit](report-pair-audit.json) and [final receipt](archive-receipt.json) provide byte-level preservation checks. No source, core receipt, 056 evidence, GUI, commit or push was changed by this archival task.
