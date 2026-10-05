# AoE III comparison and content roadmap

Comparison snapshot: 2026-10-05. Project baseline: 0.6.0, runtime commit `4f6f0e81e478c905f9229dd4b4cae948e062e79a`, delivered docs commit `2ce9bd0`. These are feature comparisons; published AoE rules and project balance values are separate. No commercial assets are imported.

The four completed river tasks remain completed: winding valley refinement, geography/gameplay integration, harbor/ship silhouettes, tropical/confluence/estuary expansion. The broader comparison is a new expansion objective. 732 baseline tests, controlled images and native launches do not establish content parity or acceptable performance.

## Reference layers

- Classic foundations: economic gathering/building, counter units, era advancement, Home City decks, civilization differences, trade/treasures, naval transport, single-player and multiplayer.
- Definitive Edition product baseline: campaign/tutorial/challenges, modern UI/accessibility, map/scenario editor, saved games/replays, cooperation and spectator tools.
- Later expansions: Royal Houses, Diplomacy/Tycoon, and 2026 Baltic Powers. New DLC features are recorded separately rather than presented as requirements of the 2005 original.

Official 2026 [Baltic Powers release](https://www.ageofempires.com/news/the-baltic-powers-available-now-for-age-of-empires-iii-definitive-edition/) and [Update 19.18309](https://www.ageofempires.com/news/age-of-empires-iii-definitive-edition-update-19-18309/) establish that comparisons limited to the older 16/22-civilization roster are outdated. Full current civilization count has not yet been verified here. Exact current AoE numerical costs/bonuses require individual primary evidence; unmatched values remain project rules P.

## Gap register

| Area | Verified 0.6.0 state | Required next behavior and acceptance |
|---|---|---|
| Classic civilizations | British/Dutch/French/Ottoman economy, core units, era upgrades; two original civilizations | Factories, distinct heavy artillery, late economic/military systems, fuller politicians/cards/revolution; each needs legal economy and combat tests plus complete native matches |
| Industrial economy | No factory or factory wagon | Two industrial shipment cards, delivered wagons, safe construction, resource/artillery modes, technologies; limits, blocked exits, population, lost factory and saved progress verified |
| Save/load | Reports and events only | Three local checkpoints, cold restoration of world/AI/fog/queues/orders/projectiles/era/deck and fixed-step remainder; branch-equivalent continuation, atomic overwrite, rule/version/corruption rejection |
| Performance | Native 200/400+12 boats P95 66.50/75.34 ms | Remove observed repeated allocations and presentation writes; repeat identical native fixture, then 20–40-minute normal war/water matches and Release-device profiling; no 60 FPS claim before measurement |
| Maps | Six fixed 128×128 maps, seed does not generate terrain | Original random templates, spawn/resource/shore fairness; 30 seeds per template, path and resource checks, full bilateral matches |
| Strategic objectives | Elimination/resignation | Capture harbor, defend bridge, escort caravan, with visible progress/failure and saved objective state |
| Map editor | None | Terrain/river/spawn/objective/trigger editing and durable import/export; restart-and-run roundtrip, malformed map rejection |
| Neutral factions | Trade sites/caravan XP/resources, guarded treasure and claims | At least three original allies with troops/technologies, map competition and distinct costs/population; prevent repeated rewards |
| Naval roster | Fishing boat/gunboat, docks, fishing, targeted attacks, bridge passage | Distinct transport/light/heavy/bombardment roles, technologies/cards/repair; mixed fleet harbor/bridge stress |
| Naval transport/AI | No boarding, landing or sea invasion | Capacity, loading/cancel/unload, death/blocked shore rules; island AI builds and crosses sea, defends and scouts without deadlock |
| Players/teams | Two-player skirmish, three AI profiles | At least four slots for teams/FFA, ally targeting and team victory, measured difficulty differences |
| Tutorial/scenarios/campaign | None | 5–8 guided lessons, three original scenarios and linked campaign progression; clear objectives/retry/save |
| Network/co-op | None; fixed step is not network synchronization | LAN two-machine command ordering and state hashes, latency/loss/reconnect checks, then four-player room and cooperation |
| Replay/spectator | Event diagnostics only | Initial state plus step-indexed commands/rule fingerprint/checkpoints; replay hash equivalence, speed/pause/view permissions |
| Art/animation | Original procedural models, six civilization details, build stages, curved landscape | Era/civilization silhouettes, walking/attack/work/death/destruction, LOD/shadow budgets measured during large fights |
| Audio | Original synthetic activity/events/environment/music | Spatial effects, unit acknowledgement voices, music/SFX/voice buses with persistent sliders; position and alert audibility checked |
| Settings/accessibility | Fixed hotkeys, frame selection/minimap/tooltips; audio toggles | Persistent remapping/conflict checks, UI scale and map preview, friend/enemy pattern cues, subtitles; Retina/1920 layout/input verification |
| Release platforms | Mac ARM Development Mono | Release build on target devices, long-session memory/GC/long frames, actual other-platform startup/input evidence before claiming support |
| DLC layer | None | Royal Houses, economic/diplomatic/regicide/independence variants, fish traps and capture structures, each with its own rules and acceptance |

## Implementation order

1. Industrial factories and heavy artillery; true saves and immediate presentation/input allocation fixes. Implemented and delivered in 0.7.0 with 796 passing tests, native normal cold restoration, four controlled industrial views, normal-rule AI result and 200/400 pressure receipts. See the unchanged [0.7 completion snapshot](Completion-Status-064.md) for that version's scope and remaining long-session/Release limits.
2. Map generation, neutral allies and map objectives; transport/fleet/repair and naval AI. Implemented in the 0.8.0 candidate: three generated templates, three original allies and objectives, transport/frigate/monitor roles, physical boarding/landing and paid harbor repair. After bounded save compression, the complete Unity run passed 958/958 (618 EditMode + 340 PlayMode), with current-source verification: runtime `c4365e31dbea9dd7e5d5c253b661ee2d3a25879a`, 531 inputs, fingerprint `d7bbe1e4f0e52210bf64b4787864449a438be87fb14952173cdd4d60d0563eb6`; rebuilt GUID `5fbcf668506b4814bc34d27b3902a237`. New writes retain all events in schema 2 gzip-base64, with fixed 64 MiB stored / 256 MiB raw limits; same-version/rules schema 1 remains readable, without 0.7 migration. The earlier GUID ac217 controlled native operations and its 1706-second failed save are historical. The new-GUID normal session reached 2106.203857 simulated seconds and 129,656 events (85,654,935 raw / 8,462,877 stored bytes); actual process exit, new-process GUI load and paused resave retained the complete file byte for byte. It continued another 116.11328125 seconds to 2222.317138671875 seconds and 140,982 events, then saved again; [independent archive](Evidence/065/Native/Normal/README.md) verifies file equality and exact prefixes. [Controlled cargo cold restoration](Evidence/065/Native/Fleet/README.md), same two soldiers' island landing and group recall also passed. New scene acceptance is limited to service-panel rendering, active bridge progress and monitor Move/Hold; native recruitment, bridge victory and combat repair remain historical. Normal reports are deliberate quits, not a natural-result match or foreground 1x FPS result. Packaging/cloud status comes from [outer delivery receipts](Delivery-065.md) after source/document freeze; no result is prefilled inside the source ZIP.
3. Four-player teams, guided challenges/original scenarios and durable settings/accessibility.
4. Deterministic command recording/replay and LAN cooperation; expand civilization/era art and audio alongside validated mechanics.
5. Further civilizations, DE/DLC modes and release distribution after their underlying systems pass their own tests and native checks.

These are batches with independent evidence, not a statement that one batch closes the entire comparison. Deployment/merge/public-sharing boundaries remain unchanged.

## Primary references and support

- [AoE III DE product](https://www.ageofempires.com/games/aoeiiide): campaigns, Art of War, Historical Battles, multiplayer, graphics/audio.
- [Release-day changelog](https://www.ageofempires.com/news/aoe3de-baseline-update/): maps, navy/Monitor roles, factory artillery, hotkeys and civilian economy changes.
- [Inside overview](https://www.ageofempires.com/news/aoe3de-inside-overview/): treasure and exploration rewards.
- [Mediterranean release](https://www.ageofempires.com/news/available-now-knights-of-the-mediterranean-new-to-aoe-iii-de/): Royal Houses and Diplomacy/Tycoon.
- [Historical maps](https://www.ageofempires.com/news/historical-maps-and-more-come-to-knights-of-the-mediterranean/): map generator and objectives.
- [Update 13.58326](https://www.ageofempires.com/news/age-of-empires-iii-de-update-13-58326/): later unit/naval/map changes.
- [Scenario editor interface](https://support.ageofempires.com/hc/en-us/articles/8476566769556-Scenario-Editor-Interface): terrain, water, trade routes, players, objectives and triggers.
- [Co-op release](https://www.ageofempires.com/news/aoe-iii-de-co-op-is-here/): cooperative historical scenarios and narration.
- [Save/replay troubleshooting](https://support.ageofempires.com/hc/en-us/articles/360052783111-How-to-find-information-for-troubleshooting-with-Support): native saved-game/replay artifacts.
- [Accessibility](https://www.ageofempires.com/age-iii-de-accessibility/): settings and accessible presentation.
- [2026 Baltic FAQ](https://www.ageofempires.com/news/faq-the-baltic-powers/): new expansion and free-update feature scope.
