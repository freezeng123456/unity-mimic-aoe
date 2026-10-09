# C2 Ottoman native audit — incomplete acceptance

**INCOMPLETE native acceptance. Evidence integrity: PASS.** Real default-economy opening/Mosque construction is retained; no medical completion is inferred.

- Match `3d8032957468484c9cfebd5d2340690c`; Build GUID `ed29410c2b27484abeac486c8f57b81b`; version `0.19.0`.
- Source `09e07a40fb0008cdb69719336a674cb228129244` / FP `e35a3b72c2a7b33371eb2c9b34a6c1838bc758b66dd73aacc1e707eeffe71a91`; actual 298 app files match manifest byte/SHA/mode values.
- Closed `menu_exit` report: **8608 events**, world **385.914337158s**, winner **-1**, player-zero age **1**, losses **1**.
- UI log records Return to Menu, report/replay saved, then Quit. Original PID55949 is absent at audit observation; original launch receipt remains `launched_not_accepted`.
- Audit JSON: **118332B**, SHA256 `e25d8867dfa85b796aac3ada408b8d66870bf7c9aa17fe70821375dba19c3444`.

## Exact event locators

| Event | Zero-based index / JSONL line | Entity / target | World time |
|---|---|---|---:|
| Payment: Foundation Mosque: 0 F / 100 W / 0 C | 561 / 562 | 2 / 0 | 38.199989319 |
| Spawn: Mosque | 562 / 563 | 136 / 0 | 38.199989319 |
| BuildingComplete: Mosque ready | 647 / 648 | 136 / 0 | 49.599815369 |
| Death: Pathfinder lost | 7459 / 7460 | 8 / 1 | 358.212646484 |

Event records have no numeric event-id field; these locators are explicit indices/lines, not fabricated event IDs.
- Payment is exactly **100W**, balance `300F/300W/100C → 300F/200W/100C`; P0 has only this one payment.
- Final replay anchor preserves Explorer8 with `dead=true`, `hp=0`; the final F9 at world385.914 has no live Explorer8. Enemy ranged Damage events immediately precede the actual Death.
- Global **Heal=0 / hasHealing=0**; P0 **ProductionQueued=0 / UnitTrained=0 / AgeAdvanced=0**; no Imam appears. Earlier automatic Fire is ordinary combat, not a post-heal return-fire chain.

## Preserved originals and checks

- All 22 native originals are byte/SHA indexed: eight paired PNG/sidecar captures, launch receipt, player/stdout logs, menu-exit report, complete event JSONL and recorded replay.
- All eight PNG hashes, same request/capture/HUD frame and world time, match IDs, app version and build GUID agree.
- Replay `c90d163335144c17bea4b9f9e84bdae8` gzip payload validates: **17020117 decoded bytes**, SHA256 `ad9547f5f88d063aff65a028247f5938a29e4fc8a628ac6ca2be46274ea636e9`; 14 anchors / 13 records / 8608 events equal the report JSONL.
- Replay `incomplete=false` concerns complete recording, not acceptance. No replay playback was run.
- Native log has zero Exception signatures; build has zero emitted exception/C# compilation-error signatures. Three broad Exception-word build hits are source-filename size listings, retained and classified in the JSON. Shutdown thread-finalization and MemoryLeaks diagnostics remain recorded, not discarded.

## Unexecuted native acceptance

- No player-zero age-II advance, paid military recruitment or Imam birth.
- No positive Heal receipt, paid medical care chain, out-of-range medical approach or post-heal return-fire chain.
- No native cold-save/load or replay playback validation in this Ottoman run.
- No natural complete human victory/defeat acceptance: winner remains -1 at menu_exit.
- No FPS/performance acceptance, four-map medical native coverage, two-physical-Mac LAN or whole-goal completion.

Only fresh external Diagnostics JSON/Markdown were created; original files, repository/source, GUI/Unity, Git and cloud state were not changed.
