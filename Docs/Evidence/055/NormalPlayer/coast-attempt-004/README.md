# 0.2.31 normal native player coast attempt B4: failed one gate

This archive is bound to macOS native build GUID `128a731d440e4b7a94abfcc74b88fa97`, version 0.2.31, match `fd3381d7dfee44cfb12332568c767c10`. Coast mirror, Easy, age cap III, seed 314159, normal 1x speed and autoplay false. The match ended naturally at **863.5362 simulation seconds**, with winner 1 and both players having actually completed age III. Event reconstruction leaves zero living units/buildings for the losing player; there is no Resign or diagnostic opening death.

`summary.json` and `menu-exit-summary.json` both fail only **cannonEnemyBuildingDamage**. All identity, opening, payment/completion, age duration, normal rules, legal shipment and natural-end checks pass. A paid cannon was trained, but neither it nor the shipment cannon damaged an enemy building. This attempt does not complete milestone B.

| Actual player result | Raw event proof (JSONL line) |
|---|---|
| Paid age II | 1455 payment at 81.1993s; 3309 completion at 171.3013s |
| Paid age III | 13075 payment of 1200 F / 1000 C at 476.7199s; 16958 completion at 586.8038s |
| Paid units | 2 Settlers, 12 Pikemen, 10 Musketeers, 1 Field Cannon; exact payment/queue/Spawn chains in summary |
| Paid buildings | 2 Manors, 1 Barracks, 1 Foundry |
| Paid cannon entity 237 | 20863 payment of 100 W / 250 C and 20864 queue at 687.4792s; 22836 Spawn and 22837 completion at 732.5682s |
| Paid cannon death | 23397 at 743.7654s, 11.1973s after Spawn |
| Natural ending | 29149 MatchEnded at 863.5362s: all opposing units and buildings eliminated |

The shipment cannon is a separate entity 210: arrived at 636.0917s and died at 650.1883s. It is not counted as the paid cannon. Entity 237 has an automatic RallyAssigned event but no representative player Command or Fire event. `player-command-audit.json` preserves **22 explicit trained military commands**, including Move, Hold and Attack for Pikemen and Musketeers. A multi-unit Command records the first selected entity and selection count, not a list of all selected entities or proof of the physical gesture.

The operator used PlayAgain with B3 settings unchanged. The paired pre-match menu image is therefore the genuine preceding B3 setup and is clearly marked inherited, not an independent B4 setup. The saved post-match menu image `Frontier-20261002-125329-683-f303916-06c8b6df937e48e3831e5c61db97c233` was independently read from disk: Coast mirror, Easy, era limit 3/5, seed 314159, Economy, 25/25. Deck configuration remains an external UI declaration because report, events and capture metadata have no deck fields.

The archived opening and progress F9 pairs have exact PNG hashes/dimensions and version/GUID/request/live/HUD identities. At 12:17 simulation time the saved F9 is after the paid cannon Spawn, but its pause overlay obscures the battlefield; no visual cannon identity is asserted. **No ending F9 was saved.** The operator returned to the menu after natural end; the post-match menu cannot substitute for a settlement image. `capture-audit.json` records this limitation explicitly.

Both F8 windows from this same match are separately fully associated with this report and Spawn/Death journal: [first](../../Performance/normal-b4-window-001/summary.json), [second](../../Performance/normal-b4-window-002/summary.json). They have 196.0985 and 191.8143 eligible active seconds respectively. Each passes identity/frame/count verification without a 300-second requirement; their sums do not establish one 300-second capture window.

`archive-receipt.json` freezes every source copy and all deliverable hashes. Scripts, Rules and DeckLibrary snapshots are preserved. Run the installed auditor from the project root (the deep snapshot is for provenance and does not preserve its relative module lookup):

```sh
python3 Docs/Evidence/055/verify_player.py \
  Docs/Evidence/055/NormalPlayer/coast-attempt-004/20261002-125310-048-fd3381d7dfee44cfb12332568c767c10-match_finished.json \
  --build-guid 128a731d440e4b7a94abfcc74b88fa97 --app-version 0.2.31 \
  --match-id fd3381d7dfee44cfb12332568c767c10 --scenario coast-iii \
  --deck-preset economy \
  --rules Docs/Evidence/055/NormalPlayer/coast-attempt-004/Rules.json \
  --menu-capture Docs/Evidence/055/NormalPlayer/coast-attempt-004/Frontier-20261002-114351-906-f93075-3f34c7fa33034e31be3ac463c19951fb.json \
  --output /tmp/frontier-b4-recheck.json
```

Expected exit code is 1, with only cannonEnemyBuildingDamage missing.
