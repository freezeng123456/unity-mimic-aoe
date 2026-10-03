# River normal-player attempt007 — strict C passed

C7 naturally ended in **Victory at2528.415771 simulation seconds (42m8s)**, River mirror/Easy/V cap/Military25, seed314159, normal1x, autoplay false. Canonical strict verification passes **17/17 checks and19/19 coverage**, with no gaps. All evidence belongs to match `cd717dda89684d87b979629d3fbdf1a9`, version0.2.31, GUID `128a731d440e4b7a94abfcc74b88fa97`, source fingerprint `1b45f97f3d90c9b76388419492a83c5622ccb2cf3a365030044403ccd309c1c7`. [Summary](summary.json), [invocation](audit-invocation.json), [CLI](verify-player-cli.txt), [verification](verification.json). Earlier C1–C6 failures remain unchanged; no cross-match coverage union.

## Natural ending and preservation deviation

The [match_finished report](20261003-113916-806-cd717dda89684d87b979629d3fbdf1a9-match_finished.json) and [raw events](20261003-113916-806-cd717dda89684d87b979629d3fbdf1a9-match_finished-events.jsonl) contain116,739 records. MatchEnded line116720 says all opposing units/buildings eliminated; winner0, no Resign, final living units/buildings[50,0]. Player reachedV versus opponentIV, kills/losses300/115,14 shipments each. [Menu-exit report](20261003-113921-407-cd717dda89684d87b979629d3fbdf1a9-menu_exit.json) and [events](20261003-113921-407-cd717dda89684d87b979629d3fbdf1a9-menu_exit-events.jsonl) have byte-identical events; only reason and managedBytes differ. [Pair audit](report-pair-audit.json), [natural-end/age audit](natural-end-and-age-audit.json).

**No separate Player-ended.log and no terminal native F9 were saved.** The final observation's trailing Escape occurred after the Victory panel appeared and returned directly to menu. Frozen log lines20663/20676/20677 show match_finished→Escape→menu_exit; F9 came later at20691/20692. This is explicitly preserved in [deviation](end-capture-deviation.json) and [operator handoff](GUI-HANDOFF.json). No ended snapshot was reconstructed and no F9 was fabricated.

The actual [Sky Victory JPEG](<Frontier Reckoning Screenshot 2026-10-03 at 7.39.21 PM.jpeg>) is251,062B, SHA `10ec18c0de85f2ae2d281f8f0b1bada353b12183a7d2be194ff9689e8df28aca`, original2560×1504. It visibly shows VICTORY42m8s, V versusIV,300/115,14 shipments; independently viewed by scale_run and root. It is **not native F9**, has no invented NativeCapture metadata, and the generic elimination-or-resignation caption alone is not used to prove no Resign. [Visual/provenance audit](visual-and-sky-provenance.json), [root visual review](root-independent-visual.json).

The original complete [frozen menu log](native-input-full-Player-menu.log) is1,996,408B, SHA `56f13d4eaec6caf58aa14580cc2299cf7b2b9e884510f5b4251eb4788fe788ab`. It retains preceding shared-process history. The [C7 excerpt](native-input-c7-menu-through-exit.log) is clearly labelled original lines14960–20703, not an ended snapshot. [Log audit](log-preservation-audit.json). All11 files in the operator handoff were hash-verified; its two preceding C6 menu files remain in C6, excluded from C7's24 F9 pairs. [Handoff audit](handoff-preservation-audit.json). Operator reports a later get_app_state reopened an empty menu, no match started, then a second public QUIT and process check; this later operation is not fabricated into the pre-QUIT frozen log.

## Same-match progression, economy and technology

|Age|Paid(s)|Completed(s)|Original payment/completion lines|
|---|---:|---:|---|
|2|316.910126|407.015625|6366/8994|
|3|739.966370|850.039490|22338/26730|
|4|1263.438599|1393.506836|44591/51487|
|5|1576.162231|1736.123169|61000/69743|

Paid completions:13 Settlers,41 Musketeers,25 Pikemen,20 Field Cannons,11 River Carabineers,10 Hussars; buildings5 houses,1 Trading Post,1 Barracks,4 banks,1 Foundry,1 Stable,3 Farms. Two treasures,14 arrived shipments, bank income13937.5C, six XP deliveries totalling240XP and30 resource deliveries (150F/1750W) are linked to original payments/events. Cargo Ledgers completed557.510925s. Resource mode changed toFood565.109070s andWood611.897644s.

Veteran Musketeer paid948.415466s, applied/completed988.505676s. The independent [technology lineage/visual audit](technology-visual-audit.json) links the **same old Musk221** before/after to frozen UI context, payment, Spawn/Death and pixels:860.536926s HP200/200 ATK24→990.505188s HP240/240 ATK29. New paid Musk290 was born1104.277466s after technology and is single-selected at1111.775635s, HP240/240 ATK29. The1110.575928s capture has context0 and is not substituted as a new-unit attribute proof. F9 JSON contains no selectedEntity field. See [independent technology notes](technology-visual-audit.md).

Single paid Carabineer293 is shown at1133.170410s HP145/145 ATK15 RANGE12 POP1; its paid lineage and context are in the visual audit. Native1396.306152s/1740.522095s F9s show AGE4/AGE5 and TC3500/3500. Six non-technology native PNGs and the real Sky victory image were inspected here; the technology reviewer records its own actual image readings.

## Paid cannon building damage

Foundry242 paid500W1250C for five cannons at942.416931s (line30339); cannon261 spawned987.505920s (32142). Attack line34475 at1034.694458s targets enemy Trading Post179. Damage line34670 at1039.293335s is130 Siege from that paid cannon to the enemy Building. Original F9 at1033.094849s shows HP1648/1760 and at1042.192627s HP1518/1760; both frozen UI contexts select179. The source/target/payment/Fire/Damage chain establishes attribution, not a toast alone.

Across the full same match, paid cannons have172 positive enemy-building Damage events with logged total44,200. The canonical list has192 all-source cannon hits, including20 non-paid-source hits; these are separated, and damage sums are not capped net HP removal. [Cannon audit](cannon-building-audit.json). Veteran/Guard/Imperial Field Cannon upgrades completed1677.237549/1735.623291/1840.697632s and are retained in the raw lineage.

The auxiliary extractor's first comparison incorrectly compared172 paid-only lines with the canonical192 all-source list. Both original and corrected extraction/code are retained; same-scope comparison reconciles all192 lines and has no reference issues. [Scope correction](extraction-scope-correction.json), [corrected extraction](ended-event-extraction-corrected.json). This affected no canonical strict result or raw gameplay record.

## Freeze and independent review

[Frozen audit](frozen-build-audit.json) passed all357 source-file hashes, combined fingerprint and three actual runtime hashes before any .32 change. Root independently reread357+3 at11:44:27Z and reran strict on the explicit original report; [independent frozen output](root-independent-frozen-build.json), [independent strict output](root-independent-summary.json), [22-field comparison](root-independent-strict-comparison.json), [copy comparison](root-independent-comparison.json). The outputs are copied unchanged, retaining original path provenance. This archive remains .31 gameplay evidence; it does not claim a later version's tests, runtime or menu smoke.

The [preliminary receipt](preliminary-archive-receipt.json), original [copy audit](source-copy-audit.json), actual raw report/events/F9 and corrected archive helper stay unchanged. Final [receipt](archive-receipt.json) covers every final regular file except itself. Original capture/log bytes are not normalized. No source/core055/canonical/056 file or GUI was changed by finalization.

## All native F9 pairs

24 original PNG/JSON pairs:2 menus and22 in-match captures; all SHA/dimension/version/GUID/match/request/HUD checks passed. There is no terminal native F9. In-menu match IDs are blank; hidden prior HUD IDs are not a new-match identity claim.

|#|Simulation seconds|Original pair|Frozen log line|
|---:|---:|---|---:|
|1|menu|[PNG](Frontier-20261003-092247-728-f1208992-9c3c2c3d21c84c5cb3ac5ebefc7180f3.png) · [JSON](Frontier-20261003-092247-728-f1208992-9c3c2c3d21c84c5cb3ac5ebefc7180f3.json)|14961|
|2|0.900000|[PNG](Frontier-20261003-092249-827-f1209109-2711e46deee6478c890f8f99c19e99b3.png) · [JSON](Frontier-20261003-092249-827-f1209109-2711e46deee6478c890f8f99c19e99b3.json)|14983|
|3|60.399651|[PNG](Frontier-20261003-092500-200-f1216867-4ecb2ad2605646368c47539d026eff1f.png) · [JSON](Frontier-20261003-092500-200-f1216867-4ecb2ad2605646368c47539d026eff1f.json)|15146|
|4|412.315948|[PNG](Frontier-20261003-093323-632-f1246733-f863f23d52d4455f8aa74849ca1c246d.png) · [JSON](Frontier-20261003-093323-632-f1246733-f863f23d52d4455f8aa74849ca1c246d.json)|15458|
|5|550.612610|[PNG](Frontier-20261003-093856-040-f1266501-f6c15e7c83174db9a33addb8920fd153.png) · [JSON](Frontier-20261003-093856-040-f1266501-f6c15e7c83174db9a33addb8920fd153.json)|15897|
|6|565.109070|[PNG](Frontier-20261003-093931-017-f1268572-c54eda32425149b19a4fbf56ebc1fb6b.png) · [JSON](Frontier-20261003-093931-017-f1268572-c54eda32425149b19a4fbf56ebc1fb6b.json)|15990|
|7|603.099792|[PNG](Frontier-20261003-094102-041-f1273972-528ce38b8967478a8e845689e6f3fa59.png) · [JSON](Frontier-20261003-094102-041-f1273972-528ce38b8967478a8e845689e6f3fa59.json)|16067|
|8|610.497986|[PNG](Frontier-20261003-094502-312-f1288268-bf9e083f356a4cd0a946a7a3247ea044.png) · [JSON](Frontier-20261003-094502-312-f1288268-bf9e083f356a4cd0a946a7a3247ea044.json)|16149|
|9|717.871765|[PNG](Frontier-20261003-094931-140-f1304263-7fd6406347394344a7ec5ea5f6e0bc39.png) · [JSON](Frontier-20261003-094931-140-f1304263-7fd6406347394344a7ec5ea5f6e0bc39.json)|16515|
|10|850.139465|[PNG](Frontier-20261003-095844-715-f1337227-a01798e4fffb4712a9c83a9cf57899ed.png) · [JSON](Frontier-20261003-095844-715-f1337227-a01798e4fffb4712a9c83a9cf57899ed.json)|16786|
|11|860.536926|[PNG](Frontier-20261003-095939-831-f1340498-4ac303abbc2f4e55b464a56f278ff8fc.png) · [JSON](Frontier-20261003-095939-831-f1340498-4ac303abbc2f4e55b464a56f278ff8fc.json)|16920|
|12|990.505188|[PNG](Frontier-20261003-102401-827-f1381744-1002c8a44f534ac190b647f07e431f36.png) · [JSON](Frontier-20261003-102401-827-f1381744-1002c8a44f534ac190b647f07e431f36.json)|17182|
|13|998.303284|[PNG](Frontier-20261003-102529-588-f1386951-9af9abbb38e64f3d968e7fb5c3c8ffdb.png) · [JSON](Frontier-20261003-102529-588-f1386951-9af9abbb38e64f3d968e7fb5c3c8ffdb.json)|17311|
|14|1033.094849|[PNG](Frontier-20261003-102656-019-f1392085-9bd300707bee47b3af527290564adc22.png) · [JSON](Frontier-20261003-102656-019-f1392085-9bd300707bee47b3af527290564adc22.json)|17433|
|15|1042.192627|[PNG](Frontier-20261003-102719-777-f1393484-c98cd7bbfbd74212b49e9f79dde42a6f.png) · [JSON](Frontier-20261003-102719-777-f1393484-c98cd7bbfbd74212b49e9f79dde42a6f.json)|17472|
|16|1110.575928|[PNG](Frontier-20261003-103102-660-f1406760-8056abd2742a4dacb199575c2ef43f66.png) · [JSON](Frontier-20261003-103102-660-f1406760-8056abd2742a4dacb199575c2ef43f66.json)|17766|
|17|1111.775635|[PNG](Frontier-20261003-103135-038-f1408689-9ff2ad7a9d184361a6074a43216fc145.png) · [JSON](Frontier-20261003-103135-038-f1408689-9ff2ad7a9d184361a6074a43216fc145.json)|17799|
|18|1133.170410|[PNG](Frontier-20261003-103213-638-f1410990-1012e908e3ec41da995966746d2b45d0.png) · [JSON](Frontier-20261003-103213-638-f1410990-1012e908e3ec41da995966746d2b45d0.json)|17862|
|19|1264.138428|[PNG](Frontier-20261003-104159-328-f1445814-010d361dcc5e498f8539e9799d30dbbd.png) · [JSON](Frontier-20261003-104159-328-f1445814-010d361dcc5e498f8539e9799d30dbbd.json)|18115|
|20|1396.306152|[PNG](Frontier-20261003-104637-225-f1462365-aa74fca56bc54bda8fc152681a563278.png) · [JSON](Frontier-20261003-104637-225-f1462365-aa74fca56bc54bda8fc152681a563278.json)|18279|
|21|1576.762085|[PNG](Frontier-20261003-105257-715-f1484964-6bdb7231c0e14cf39814903cd8ceed9f.png) · [JSON](Frontier-20261003-105257-715-f1484964-6bdb7231c0e14cf39814903cd8ceed9f.json)|18517|
|22|1579.661377|[PNG](Frontier-20261003-105321-999-f1486408-4450dab8713d4de2bcc48e48036d7099.png) · [JSON](Frontier-20261003-105321-999-f1486408-4450dab8713d4de2bcc48e48036d7099.json)|18572|
|23|1740.522095|[PNG](Frontier-20261003-110237-451-f1519437-db38d67eff2143768a9b6c615ee75161.png) · [JSON](Frontier-20261003-110237-451-f1519437-db38d67eff2143768a9b6c615ee75161.json)|19040|
|24|menu|[PNG](Frontier-20261003-113933-192-f1651086-8ca5435fbef44691a75902ecf368f35b.png) · [JSON](Frontier-20261003-113933-192-f1651086-8ca5435fbef44691a75902ecf368f35b.json)|20692|

|Required coverage|Result|
|---|---|
|`ageII`|PASS|
|`ageIII`|PASS|
|`ageIV`|PASS|
|`ageV`|PASS|
|`paidWorkers`|PASS|
|`paidMilitary`|PASS|
|`paidCannon`|PASS|
|`cannonEnemyBuildingDamage`|PASS|
|`gathered`|PASS|
|`shipmentArrived`|PASS|
|`paidBank`|PASS|
|`bankIncome`|PASS|
|`specialUnit`|PASS|
|`paidTechnology`|PASS|
|`treasure`|PASS|
|`paidTradePost`|PASS|
|`tradeXP`|PASS|
|`resourceMode`|PASS|
|`resourceTradeDelivery`|PASS|
