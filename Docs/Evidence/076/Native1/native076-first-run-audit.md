# Native 076 first-run audit

- Status: `first_native_medical_attempt_failed_preserved`.
- First match only: `dc5835f86f1640608f0d325a3dc12ac7`; British (`coast`) vs Ottoman, winding42, standard AI.
- App0.19.0; buildGUID `a86c515f3ab040649ea505e507193ff3`; PID78563; sourceFP `dabf34d26490cece872959764b8b9e515749584b180f3d7899f38d79b69a6f8f`.
- Original captures, reports, event JSONL and replay untouched. Second match excluded.

## Acceptance bounds

- **Closed:** real150W Church foundation and completion; real800F/90s AgeII progression; naturally injured Pathfinder8; original failed attempt retained.
- **Open:** paid Priest birth, positive native healing, Stop/resume, medical save/cold restore and native replay playback.
- Human population reached0 and Church remained. Actual report is `restart_exit`, winner-1, not a natural completed match.

## Exact event chain (zero-based indexes)

| Item | Event index | World seconds | Actual result |
|---|---:|---:|---|
| Church payment |1876|142.89952087402344|150W;300W→150W|
| Church spawn |1877|142.89952087402344|Entity142 at(13,31)|
| Church complete |2012|155.9003143310547|13.000793457s observed multi-builder construction|
| AgeII payment |5216|356.81256103515625|800F;1205.2977294921875→405.2977294921875|
| AgeII attained |7042|446.9180603027344|90.1054992676s after queue start|
| Pathfinder death |10594|583.7045288085938|First medical patient lost|
| Town death |13245|674.5823364257812|Human capacity subsequently0|
| Last settler death |13621|687.2792358398438|Human population0|

Closed event count: **14269**, matching report and saved replay event sequence. P0 training events: **0**. All healing events: **0**. Report spend: **800F/150W/0C**.

## Original paired F9 index

| Role | Original JSON | UTC | World | JSON SHA256 | PNG SHA256 |
|---|---|---|---:|---|---|
|opening_default_state|`Frontier-20261009-212141-928-f16323-2dd37934726c436481f8de376a201d25.json`|2026-10-09T21:21:41.8436400Z|13.400014877319336|`a34a6cdc5d862937c20b5b9a19e4a0cc9e9f9aa07851ee75ecbeefa2382eaf91`|`30fbffcd8bb5864056b7ff41e7799fa01d74b8f58dcd1a730b2a2269cb1e7c42`|
|placement_rejected_wallet_intact|`Frontier-20261009-212338-488-f23224-59727d4665304769bc6915002d423e59.json`|2026-10-09T21:23:38.4045050Z|129.8987274169922|`4f299ee2dd13e85a2d78a912a50c49af8d4405269a604eb3b0418e171e767095`|`7fe7575e8bb5fd453866d853abaf7879e9014a68b0cac7044f1f1f21b88da8e7`|
|paid_church_completed_and_wounded_patient|`Frontier-20261009-212411-018-f25157-a20189c447ee4f2b8b54426b04d439a4.json`|2026-10-09T21:24:10.9297110Z|162.4007110595703|`a9b8db3b53c77a9864310ce7d2dd227821ed76757ad9f926c06f2ef2cd8426dc`|`dc454b9ee4abccc7c5d44f98f31e47166df54a0dc9fae643cd5ae2191d3b39ab`|
|wounded_patient_move|`Frontier-20261009-212606-956-f32075-b7d8dbac006e4308acfca5f2fdb7bd77.json`|2026-10-09T21:26:06.8756860Z|278.40777587890625|`42d506b631df5cb6f111f568288598912e2ed4e5361da4812aa1caabfb7361ea`|`0945452ba8d556cb3f0385dcde0a065cdd1c94fa587fb8cf1e182deb63ae6ef2`|
|age_ii_paid_queue|`Frontier-20261009-212725-451-f36759-bc2892ab7bff4d8eb37abcc54f0341be.json`|2026-10-09T21:27:25.3658540Z|356.9125671386719|`0ff6eb0769eaa48d43c71823f84e9fb5eb686498ec3e60ae05110c363e156109`|`66fcb9453c75f8cf37795bfc047b2f3a965a4c787946d5d839cc3162c2fa48db`|
|wounded_patient_hold_age_queue|`Frontier-20261009-212814-197-f39664-32f107c425bf4283bcaba2cd81ce1d23.json`|2026-10-09T21:28:14.1114020Z|405.61553955078125|`17aa035fbea112803566cd99a27cdfb9c4c88a636137bfceffd385c610a6007a`|`e21d0291cd7da3fee350e0dea615ef4c63eb4f3488ae08e0d410414371b27e0e`|
|medical_attempt_failed_no_population|`Frontier-20261009-213313-079-f57115-c18e5a16b2b84d88a0ce622bd9863f9d.json`|2026-10-09T21:33:12.9957450Z|704.4750366210938|`29f74102cbecee43cadf08aecd0c25c81dd5670b06e2b680f412d2ac772ed731`|`0aaa1115b16fe34f67214c34c7c4cd0de6748a2334f548d2df9423085bac8764`|

All7 JSON/PNG pairs: hash and1920×1080 IHDR dimensions agree with JSON; app/build/match/HUD identities and world times agree. Full paths and byte sizes in adjacent JSON.

## Closed originals

- `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate1/Native1/british/Data/Reports/20261009-213400-604-dc5835f86f1640608f0d325a3dc12ac7-restart_exit.json` — 5441 bytes; SHA256 `15d5e29f66f1f30fcc366852669931b574ac10110f6c83db521f9cb86235f605`.
- `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate1/Native1/british/Data/Reports/20261009-213400-604-dc5835f86f1640608f0d325a3dc12ac7-restart_exit-events.jsonl` — 10841822 bytes; SHA256 `72b2be56abd0c75b7b95bda187b2c64a4719c8a5e5c5481048644f05e87fe72f`.
- `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate1/Native1/british/Data/Replays/replay-643a4291beba48baaf4c107877608200.replay.json` — 994701 bytes; SHA256 `cb0d163af7f5614e5e8e044e709d73836fd8d916024d3fa91007ed20a335faf9`.
- Replay decoded payload SHA256 `08b1e6217f3f0b6442def35bc487aa647c82a41e4207a48f11eed1b67f2d8478` validates envelope;25anchors/11inputrecords/14269events. `incomplete=false`, finishReason=`restart_exit`; native playback not observed.

## Continuing log boundary

`/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate1/Native1/british/player.log` first **25510 bytes / 263 lines**, SHA256 `5b8c390c20753add0eef46bf5f8cd142d25fff631a6aca5dfb646076c5f17596`. Range ends at first-match `FRONTIER_REPORT` line; subsequent second-match log data is excluded.

First-run manual pause: line234, world704.575000, frame57117. SAVE REPLAY action line242, saved-file log line244. RESTART action line262, first-run report line263.

## Compact delivery index recommendation

Use five rows: opening(F9#1); Church paid+complete(event1876–2012,F9#3); AgeII paid+queue+complete(event5216/7042,F9#5); wounded patient394/450+Hold(F9#6); failed attempt(F9#7+restart_exit report). Keep all7 original pairs in package. Link report/event/replay SHA manifest; keep medical native status pending until a separate run proves it.
