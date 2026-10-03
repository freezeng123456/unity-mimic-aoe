# River normal-player attempt006 — failed strict C, preserved

C6 naturally ended in **Defeat at 1367.213257 simulation seconds (22m47s)**. River mirror, Easy, seed314159, V cap, Military25/25, normal1x, autoplay false. Canonical strict C returns **16/17 checks and 17/19 coverage**; only **`ageIV` and `ageV`** are missing. IV was paid but did not complete; V was not paid. Other matches are not combined into acceptance. See [summary](summary.json), [invocation](audit-invocation.json), [CLI exit1](verify-player-cli.txt) and [archive verification](verification.json).

## Identity and original records

Match `7558b19fc9984578a313082b633c6630`, version0.2.31, GUID `128a731d440e4b7a94abfcc74b88fa97`, source fingerprint `1b45f97f3d90c9b76388419492a83c5622ccb2cf3a365030044403ccd309c1c7`. All357 source files and all3 runtime hashes match the frozen .31 build. Root independently reproduced the same strict result and source/runtime match; ignored independent-review files are not required by this self-contained archive.

- [Natural-end report](20261003-092107-193-7558b19fc9984578a313082b633c6630-match_finished.json) and [events](20261003-092107-193-7558b19fc9984578a313082b633c6630-match_finished-events.jsonl): 60,226 events; MatchEnded line60193, no Resign, living units/buildings [0,67]. PlayerIII versus opponentIV, kills/losses65/84, ships8 versus10.
- [Menu-exit report](20261003-092155-226-7558b19fc9984578a313082b633c6630-menu_exit.json) and [events](20261003-092155-226-7558b19fc9984578a313082b633c6630-menu_exit-events.jsonl) have byte-identical events. Only reason and managedBytes differ between reports; [pair audit](report-pair-audit.json).
- Frozen [ended log](native-input-full-Player-ended.log):1,436,529B, SHA `7fc13556c97f8bd16bf4ae44dba11c9aee2d1c98f62a6ac39d948a41ac9ac3e4`; [menu log](native-input-full-Player-menu.log):1,442,069B, SHA `322608e5ec91560eb9c9a8719b33d81274ebce7de41bbc2cdf487289ffa40d3d`. Ended bytes are an exact menu-log prefix. [C6 excerpt](native-input-c6-menu-through-exit.log) preserves original full-log lines11917–14959; both full shared-process logs retain preceding history. [Log audit](log-preservation-audit.json).

No C7 runtime log, report, capture metadata or GUI was read. The [preliminary receipt](preliminary-archive-receipt.json) is unchanged; the [final receipt](archive-receipt.json) covers every final file except itself.

## Frozen-audit tool correction, original failure retained

The initial helper copy accidentally replaced lowercase `c5` inside its hardcoded fingerprint while changing C5 labels to C6. The actual recomputed source fingerprint, all357 hashes and all3 runtime hashes already matched the true .31 baseline; only the erroneous constant comparison failed. [Initial failed audit](frozen-build-audit.json) and [original helper](archive-ended-c6.py) are unchanged. The comparison literal was corrected in [corrected helper](archive-ended-c6-corrected.py); a separate full357+3 reread passed in [corrected frozen audit](frozen-build-audit-corrected.json), which retains the initial audit SHA and exact explanation. This was an archive-tool error, not a game/source discrepancy. No source or runtime bytes changed.

## Paid cannon hits enemy Farm

Foundry237 paid500W1250C for five cannons at778.956848s (Payment27155). Cannon256 spawned824.045837s (30421); enemy Farm266 spawned852.438904s (31567), owner1/Building, at(96,111).

Three positive Siege Damage records target that Farm: **35232 at947.515686s**, **35466 at952.614441s**, **35716 at957.713196s**, each130, total**390**. The explicit Attack command35548 at954.114075s follows the first two hits. Last Fire35700 at957.413269s precedes cannon Death35712 at957.613220s; its projectile hits after the cannon dies. [Cannon audit](cannon-building-audit.json) retains paid lineage, both entity identities, all Fire/Damage, the command and death.

The957.813171s F9 shows no selected unit, an already reduced enemy Farm health bar and the Attack-accepted toast. Exact damage and projectile timing are established by raw events, not the toast/bar. The earlier operator impression that the cannon had died without a building hit was mistaken.

## Same old Musketeer and newly born upgraded Musketeer

Veteran Musketeer was paid725.070007s (23837), applied/completed765.160217s (25772/25773). Old paid Musketeer162 was born250.206070s (5130), died1146.167236s (45180), and was alive in both single-unit screenshots:

| Capture time | Full-log UI context | Entity/count | Actual pixels |
|---:|---:|---|---|
|574.306824s|13058|162 / 1|HP200/200, ATK24|
|773.458191s|13464|162 / 1|HP240/240, ATK29|
|846.140442s|13697|263 / 1|HP240/240, ATK29|

New paid Musketeer263 was paid806.150208s (29403), born832.143860s (30818), and died1132.770508s (44308). Payment and birth both follow technology completion. Frozen Rules give200HP/24ATK plus20%, resulting240HP/28.8ATK displayed29. [Technology/visual audit](visual-and-technology-audit.json) preserves all contexts, intervening trace and raw birth/death/payment chains. Group3 alone is not used as identity proof. **F9 JSON has no selectedEntity field**; ID comes from frozen UI context, pixels are inspected independently.

## Progression and missing ages

| Age | Paid(s) | Completed(s) | Original payment/completion lines |
|---|---:|---:|---|
| 2 | 100.899033 | 191.002457 | 1986 / 3944 |
| 3 | 606.299011 | 716.372131 | 18780 / 23366 |

IV was paid1190.856323s (47562), but TC1 died1292.431519s (56453), only101.575195s later, before the130-second upgrade completed. No IV completion or V payment/completion exists. [Natural-end/age audit](natural-end-and-age-audit.json).

Paid births:15 Settlers,15 Musketeers,10 Pikemen,6 Field Cannons,10 River Carabineers. Paid buildings:5 houses,1 trade post,1 barracks,3 banks,1 foundry,1 stable. Cannon255 was a shipment unit and is distinct from paid256–260/347. Single paid Carabineer274 is linked to the961.412292s F9 (HP145/145, ATK15, RANGE12); five cavalry are visible.

Cargo Ledgers completed308.909637s. Six XP deliveries total240XP; trade changed toFood323.910553s and five Food deliveries total375F. Three banks generated5409.25C; two treasures were claimed. Final resources384.422058F/1.103040W/1181.625366C. These are ended-game facts, not a general causal claim. [Key raw events](key-events-raw.json) keep one-based original line references.

## Bounded input/UX observations

The [input audit](input-ux-audit.json) preserves actual commands and the19:00–19:22 trace. First actor255 received Move x4 at1059.088501s (40473), Move x3 at1142.568115s (44965), and Move x3 at1152.465698s (45553), all target0. They must not be described as Attack. Actor positions sampled by the latter records move from(29.041500,38.136761) to(51.049656,48.226082); actor255 dies1153.965332s. Command.position is the actor position, not the clicked destination; the record names only the first actor/count.

After resume1153.465, A input is logged, followed by a target left-down120.882ms after resume and left-up171.482ms after resume. No player Command appears through1162s. The copied [input source](RtsInput.cs) has a0.22s pointer barrier while fresh keys are allowed immediately. This is consistent with the click being suppressed and A remaining pending, but the trace contains no direct AttackMode state field. The first ADVANCE AGE pointer click has no action record; the later Escape and second click precede an actual action and payment. The audit distinguishes these trace facts from mode-state inference. Static model/pick geometry does not establish the cause of every missed click. Existing InputProjection documentation is unchanged; no new A–D gate or broader .32 delta is introduced.

## Complete F9 inventory and coverage

All11 pairs are original2560×1440 PNG/JSON:2 menus and9 in-match captures. All hash/dimension/version/GUID/match/HUD checks pass. Seven images were visually interpreted; others are preserved without invented descriptions. No IV/V screenshot is asserted. In-menu match/request IDs are empty and hidden prior HUD values are not treated as a new match.

| # | Simulation seconds | Original pair | Full menu-log line |
|---:|---:|---|---:|
| 1 | menu | [PNG](Frontier-20261003-081347-210-f962441-875032a5f2cd4487af47e9cf86321c9c.png) · [JSON](Frontier-20261003-081347-210-f962441-875032a5f2cd4487af47e9cf86321c9c.json) | 11918 |
| 2 | 0.900000 | [PNG](Frontier-20261003-081349-148-f962548-52c3fd757cd44e93b1b30db6e4583a7b.png) · [JSON](Frontier-20261003-081349-148-f962548-52c3fd757cd44e93b1b30db6e4583a7b.json) | 11942 |
| 3 | 101.699020 | [PNG](Frontier-20261003-081953-987-f984314-854dac5d404f4ac290fd367a33948f93.png) · [JSON](Frontier-20261003-081953-987-f984314-854dac5d404f4ac290fd367a33948f93.json) | 12196 |
| 4 | 324.010559 | [PNG](Frontier-20261003-083209-615-f1028130-0ab065e37a02408b840d47ab47da9e32.png) · [JSON](Frontier-20261003-083209-615-f1028130-0ab065e37a02408b840d47ab47da9e32.json) | 12717 |
| 5 | 574.306824 | [PNG](Frontier-20261003-083930-316-f1054362-e0f9197278dc4cc5b9b6d3005a43c6b0.png) · [JSON](Frontier-20261003-083930-316-f1054362-e0f9197278dc4cc5b9b6d3005a43c6b0.json) | 13083 |
| 6 | 773.458191 | [PNG](Frontier-20261003-084653-809-f1080710-649b324e79ad4bc3a6455e4fae91f27b.png) · [JSON](Frontier-20261003-084653-809-f1080710-649b324e79ad4bc3a6455e4fae91f27b.json) | 13466 |
| 7 | 846.140442 | [PNG](Frontier-20261003-085053-883-f1095018-9c2498f3f2c84dd0847a64a566495a4c.png) · [JSON](Frontier-20261003-085053-883-f1095018-9c2498f3f2c84dd0847a64a566495a4c.json) | 13699 |
| 8 | 957.813171 | [PNG](Frontier-20261003-090208-774-f1135245-bfb0533a45724c2f87af01333819f4cc.png) · [JSON](Frontier-20261003-090208-774-f1135245-bfb0533a45724c2f87af01333819f4cc.json) | 14018 |
| 9 | 961.412292 | [PNG](Frontier-20261003-090305-410-f1138623-2f1d0d806ff8457991e2e37b18d31720.png) · [JSON](Frontier-20261003-090305-410-f1138623-2f1d0d806ff8457991e2e37b18d31720.json) | 14057 |
| 10 | 1367.213257 | [PNG](Frontier-20261003-092137-398-f1204818-edf4d40722a940268add50a81b84d986.png) · [JSON](Frontier-20261003-092137-398-f1204818-edf4d40722a940268add50a81b84d986.json) | 14904 |
| 11 | menu | [PNG](Frontier-20261003-092156-140-f1205909-cd523f0373c94dd9831dcb4c4babd9cb.png) · [JSON](Frontier-20261003-092156-140-f1205909-cd523f0373c94dd9831dcb4c4babd9cb.json) | 14948 |

| Required coverage | Result |
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
| `specialUnit` | PASS |
| `paidTechnology` | PASS |
| `treasure` | PASS |
| `paidTradePost` | PASS |
| `tradeXP` | PASS |
| `resourceMode` | PASS |
| `resourceTradeDelivery` | PASS |

[Copy audit](source-copy-audit.json), [static-source copy audit](static-source-copy-audit.json), [capture audit](capture-audit.json), [verification](verification.json) and [SHA receipt](archive-receipt.json) distinguish complete preservation from strict gameplay failure. No source/core055 receipt/056/canonical file was edited by this archive task.
