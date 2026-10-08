# 075 scouting content contract — declared before validation

Baseline: 7d9910e8b05464faff755813d76c83bb7f07c522, 0.17.0. Prior 074 package/evidence remain immutable. New candidate is 0.18.0 Candidate1.

Implement two original national scout units with actual capabilities: Dutch Envoy trains through owned Explorer, weak fist melee and high vision; may build only a normal paid trading post. French Native Scout trains through town center, uses bow and stealth. No automatic opening units, healer, copied commercial assets, revolution transformation, or commercial-parity claim. Existing authored openings remain explicit. Prices/stats/caps/detection/cooldowns below are project rules P.

Envoy: 120HP, speed5, vision28, melee4/range1.3, 50food/15s, 0population, age1–5 cap3/5/8/13/18. Native Scout:100HP,speed5,vision28,bow7/melee3/range12,50food+25wood/18s,1population,cap4. Queued training counts toward cap and population; only real payments/completion/refunds apply. Movement of unit producer, garrison pause, blocked exit and producer death must preserve queue authority.

Auto Scout is an authoritative persistent Scout order, eligible for new scouts and existing Explorer; seeks reachable unexplored fog using real navigation and only visible threats. Stop/Hold/manual replacement must work. Shift-appended next order takes over after current exploration leg. Scouts remain attackable and are excluded from army recruitment/cohort thresholds, without gaining worker or treasure-capture rights. AI must avoid repeated scout spam and exercise actual paid training/construction.

Stealth: Native Scout only; speed multiplier0.6, own/allied terrain revelation remains ordinary. Enemy target/render visibility uses world authority plus detection8 on new scouts/Explorer/town/tower, plus neutral guardians within their own vision. Explicit attack/attack-move/build/claim breaks stealth with3s cooldown; damage reveals6s, in-flight shots remain valid; last surviving owned unit cannot remain hidden indefinitely. Save/LAN/replay must retain real state/commands. No persistent spoof of fog or view-only hiding.

Visuals: independent Envoy tall hat/longcoat/document satchel with fists; Native Scout lowheadwrap/shortmantle/bow/quiver. Actual bow action and no musket flash. Default-zoom picking/mixed selection, training controls, Auto Scout/stealth/build controls require UI/input checks.

Validation gates: focused behavioral EditMode and PlayMode first, fix demonstrated failures, then fresh whole-suite source-bound run. Fresh Mac build and real observation follow only after source freeze. Native screenshots + metadata bind to actual build, and only performed interactions count. New owner-only Drive candidate requires whole-byte/SHA readback and existing private draft PR update; no merge/main/public/ACL changes. Incomplete results remain explicitly pending.

Primary mechanism references: official 14.43676 (Dutch Explorer trains Envoys; Native Scout stealth/retraining and armed bonuses) and19.18309 (Auto Scout; Envoy trading posts/build limits; last surviving stealth and AI scout count fixes):
https://www.ageofempires.com/news/age-of-empires-iii-definitive-edition-update-14-43676/
https://www.ageofempires.com/news/age-of-empires-iii-definitive-edition-update-19-18309/
