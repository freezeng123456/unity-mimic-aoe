# Native2 independent bounded acceptance — 078

Status: `accepted_bounded_camera_capture_menu_exit_quit`. Audited 2026-10-10T03:53:03.580616+00:00.

## Exact binding

- Source 0.21.0 / `1914ee7d73d362b7f48833232e68ce8ff9ccca19`, all **788** current input SHA256 values independently equal Full1, Build1 and the actual source-evidence map; fingerprint `05dc944960c2724e08f2d6eeb02df282710d099ae36cf35bb7923d8918166217`.
- Actual Full1 closed/pass **1372 Edit + 463 Play = 1835/1835**, every XML test-case checked; Build1 closed/pass. Original receipts SHA256 `6f5ef09ff6a3d945ff31c85091a3574e4d8e68a1eb3a0889f189ed50c0abe0a8` and `45b654887bf578c301596378ca152ea780dd7825cd7f3f08c17dcb3a7ab0409e`.
- Actual app GUID `6ec8f11cbef44f19986815fea801d80c`; all **298 files / 191066720 bytes**, modes and empty symlink list independently equal manifest `78b333edba30f39cc6bdbca05fcc04300e3b2107d9b08c32de8f1bb259a320ea`. Plist, boot GUID, arm64 executable and read-only strict deep codesign pass.
- Actual second-session helper `8eeb6f238c846d7b86106962f28598199edec09b85a21a683de48b9fbbe6a2e2`, fresh launch PID **40786**, match `93f5be26e5ec4ca5b26c4f8c67d5db5c`. Original launch receipt `12316113a6907973388710c201104dcd5c798734c9ec5e547409cac2b5fb2136` remains `launched_not_accepted` / GUI `not_performed`: separate audit does not rewrite launch history.

## All original cameras retained

Every one of **9 PNG/JSON pairs** checked against SHA256, PNG IHDR/chunk CRC/full Pillow decode, application/match identity, coherent request/capture/HUD frame and world time; all 9 original PNGs and all 3 UI JPEGs directly visually inspected. Five intended contexts are unpaused and focused.

| Context | Frame | World time | Camera focus | Size | Paused |
|---|---:|---:|---|---:|---|
| unrevealed_edge_pan_extra | 1425 | 8.899997711 | (24.000000, 121.000000) | 26 | False |
| focus_lost_paused_extra | 3470 | 25.400060654 | (36.027153, 121.000000) | 26 | True |
| initial_town_26 | 4881 | 25.500061035 | (24.000000, 28.000000) | 26 | False |
| unrevealed_camera_only_extra_before_real_Move | 6621 | 54.899734497 | (86.983429, 68.478806) | 26 | False |
| real_Move_transition_extra | 12453 | 153.500167847 | (78.732948, 68.478806) | 26 | False |
| naturally_revealed_bend_26 | 14008 | 179.801773071 | (78.732948, 68.478806) | 26 | False |
| same_focus_bend_16 | 20185 | 283.808105469 | (78.732948, 68.478806) | 16 | False |
| bridge_context_26 | 21086 | 299.009033203 | (57.517483, 39.602211) | 26 | False |
| EMPTY_harbor_pad_context_26 | 22075 | 315.810058594 | (43.963181, 25.458563) | 26 | False |

The two unrevealed early cameras, focus-loss pause, and real Move transition remain preserved. Earlier `button: right` GUI calls emitted only Left traces; the actual single Right down/up and accepted Explorer 8 Move record are retained, not substituted with fabricated input. Five Equals edges change26→16; five Minus edges restore16→26. The accepted command is ordinary scout movement for natural visibility, not a directed attack or army validation. Explorer automatically engages nearby enemies: HP, position, combat state and world time evolve, so these are not fixed-state A/B or Hold captures.

## Visual verdict

Revealed outer bend reads continuous at26 and16 without an obvious coarse triangular bite in the visible bank. Bridge deck/rails and exposed feet remain coherent; no obvious newly opened ground/water seam or reed displacement is visible in these bounded contexts. Native fog has grid-shaped stair-steps and obscures portions of shore, distinct from bank geometry; finite edge aliasing/stylized water and vegetation remain. Harbor image is an **EMPTY harbor-pad context**, not paid Dock construction or a produced ship. The normal-game images are sequential different-world-time evidence, not the controlled Render1 ground-only pair. No whole-map or perfect-contour guarantee follows.

## Actual closure and replay integrity

Actual `RETURN TO MENU` action at world328.211/frame23857 precedes menu_exit report and replay save, then actual visible `QUIT` at world0/frame24539. Three original UI images corroborate normal start menu, manual pause and returned main menu. Current read-only ps at 2026-10-10T03:53:03.587973+00:00 has PID40786 absent and **all owned Unity/native process rows empty**. Detached OS exit status remains unknown/null; no exit0 or post-Quit GUI query is asserted.

Actual menu_exit t**328.2108154296875**, winner−1, **3608** events; speed1/autoplayfalse. Strict Base64/gzip CRC decode yields **11445621 bytes**, SHA256 `b0adc029f7f91ce892f0533255b886a87e42e96c11c21dc06052161196f8b749` equal envelope. All global events equal report JSONL, all 12 anchor cursor bounds checked, and final anchor time/winner/selected player fields agree with report. Five recording records include one accepted P0 Explorer Move; no targeted attack command. Stored hashes were not playback-recomputed and `Verified` remains false. Frame telemetry is retained but not an FPS test. Shutdown diagnostics retain 6 premature-thread-finalization warnings and a MemoryLeaks diagnostic.

## Scope and preservation

All **27 original files / 13996926 bytes** are indexed in JSON with exact relative path/size/SHA256; complete original tree unchanged before/after this audit. Native1 remains a separate partial opening-only/idle-defeat session, not substituted for Native2.

Accepted only: ordinary camera F9 coverage, actual menu_exit and visible Quit/closed-process lifecycle with exact source/app/full/build binding. Not accepted here: army maneuver or selected-target attack, paid Dock/ships, Move-Hold, FPS/resource neutrality, Verified playback, full manual victory/balance, whole-map flawlessness or completion of all four river-content parts.

Only these two external audit files written; no repo/source/original mutation or Unity/GUI/Git/cloud action.
