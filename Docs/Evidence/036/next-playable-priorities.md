# Next playable priorities (iteration 036)

This list is based on `outputs/Clients-0.2.11/Docs/Full-Scope-Audit.md` and the current Evidence/035–036 material. It treats a complete player action sequence as the exit criterion; counters, configuration, screenshots, and automated match completion are supporting evidence only.

## 1. Complete a normal Coast player siege path

**Player behavior:** start a normal match, scout, sustain food/wood/coin, reach at least age III, send a military shipment, produce a mixed army including paid or shipped artillery, move it through the map, deploy it, damage an enemy building, and finish by natural elimination or a verified victory.

**Unaccepted evidence:** prior normal player matches reached only age II or ended without player-caused building damage; the audit explicitly keeps “complete player III/artillery siege” open. Automated AI matches with cannon events do not prove player control.

**Minimum closure evidence:** one fresh native run with `autoplay=false`, versioned build identifier, event audit showing `AgeAdvanced` to III, artillery `Spawn`/production and deployment, building `Damage` from the player, and natural `MatchEnded`; retain a short input/action log and result report.

**Close when:** the run is reproducible from a normal menu start and the action chain is visually checked, including no UI click being swallowed by the current HUD/input timing issue. Do not close from a longer run that never demonstrates artillery damage.

## 2. Repeat the same acceptance path for River Valley

**Player behavior:** select River Valley in the native setup, verify the coin-cost villager and bank economy through actual payments/income, then perform the same scout→economy→age→shipment→mixed army→siege→natural result sequence.

**Unaccepted evidence:** the audit records River configuration, automatic tests, and historical automatic matches, but explicitly says the River native full match remains open. Configuration fields and bank limits are not a playable civilization proof.

**Minimum closure evidence:** a fresh native `autoplay=false` report whose setup records `river`; event evidence for coin villager payment, completed bank income, at least one age transition beyond the opening, shipment arrival, and a player-issued military/building damage sequence. Record whether the civilization wins or loses; either is acceptable if the route is complete and real economy remains operative.

**Close when:** the complete action chain has been manually observed once with no hidden economy or automatic spawn assistance. If the player collapses, preserve the failure as a balance result and rerun after the smallest targeted fix; do not relabel an AI win as River player acceptance.

## 3. Finish input/focus reliability before claiming either run as representative

**Player behavior:** during both runs, issue a ground command immediately after closing/refreshing HUD panels, use selection and formation controls, pause on window focus loss, resume intentionally, and confirm that the next world click reaches the selected units rather than a stale UI capture.

**Unaccepted evidence:** the audit still marks the complete native hotkey matrix, mixed selection/Shift behavior, mini-map commands and long uninterrupted input as open. Current root work identifies a HUD 220 ms ground-click swallow and focus-loss attribution as active issues; existing PlayMode tests do not prove physical keyboard/focus behavior.

**Minimum closure evidence:** a fresh build run with timestamped input trace and screen capture for panel close→ground click, focus loss→paused state, resume, formation save/recall, and a mini-map or long-range command. Record accepted command events and the visible result, including any failure.

**Close when:** the same controls work in the build used for the Coast and River runs, across at least one focus-loss cycle, and the trace distinguishes UI capture from world command acceptance. Automated input tests remain supporting evidence only.
