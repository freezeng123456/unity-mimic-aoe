# ProcessE selected save/replay independent raw audit

Status: selected raw save/replay verified with scope limits. This is not final native acceptance or release approval.

- Match `68c69480797e47e3850953863ce58aa9`, replay GUID `3288d9fe24e844748a1183f756874fbf`, app 0.15.0. The slot itself has no build GUID field; it is linked through the matching replay identity and full normalized final state.
- Both schema-2 gzip-base64 envelopes contain one complete stream and matching payload SHA. Original selected bytes match before and after audit. ProfileE remains open to future ColdF additions.
- Independently recomputed all 367 event-chain entries, 10 input-chain entries, and all 20 anchor state digests from original JSON substrings, preserving float serialization. All anchor and record prefix references match. No observer implementation was imported.
- Slot world and simulation equal the final replay anchor after the documented replay normalization: events become `[]`, accumulator becomes `0`. Slot events equal all 367 replay events. Final step 5490, time 549.0130004882812, paused, winner -1.
- Town 1 has one paid Research queue: `civilian_rebuilding`, 400 F / 0 W / 400 C, elapsed **0.10000000149011612 / 60 seconds**. Completion is pending (`civilianRebuiltAt=0`). Wallet is 100 F / 0 W / 0 C; spent is 1600 F / 800 W / 1600 C; refunded is 400 F / 0 W / 400 C.
- Player 0 is Revolutionary (`politicalPhase=1`, `rev_coast`, `revolutionAt=507.22174072265625`). Stable original worker IDs 3–8 are all alive owner-0 `revolutionary`, ungarrisoned, with HP 190 and no queued orders. Player 0 has no AI controller, autoplay is false, speed is 1.
- Player hotkey groups are empty. The distinct authored `control-force072` binding retains IDs 75–82. Authored target bindings are House 83 and Guardian 84.

This audit does not execute replay forward or establish cold-load equality, pixel visibility, host-network behavior, performance, a normal opening, moving-worker combat, all native acceptance, or release approval. Parent audits transaction/combat semantics separately. See the JSON for exact references, all anchor/record indices, queue and political state, and individual checks.

- `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.15.0/Candidate1/NativeObserved/FinalGuid072/ProfileE/Saves/slot-1.json`
  - Original SHA-256 `c46875d4883e887d330fee45f97cd34d6fe79487ef5fd469b4530fd4376db8f8`; payload SHA-256 `810f9a7d23117804330f9395deceee3496c75d286cae560373659c3f26c13bf6`.
- `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.15.0/Candidate1/NativeObserved/FinalGuid072/ProfileE/Replays/replay-2d5f4818ae50407cb7769a0971cc85ef.replay.json`
  - Original SHA-256 `dab9e98aa9223ff9b1f112476aac02e9b0d168a032f6aaa589787d93e7b84a0e`; payload SHA-256 `3b0447c54f34d3c700fe8bcff06fd8aa51e17dc5ad4006a25a3c4ba4267471d9`.

205 checks passed.
