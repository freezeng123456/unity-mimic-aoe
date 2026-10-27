# Neutral allies and terrain art 089

Each of the three original neutral settlements now offers two paid unit choices and two paid technologies after a treaty. River Guild adds a pike and a health drill, Highland Covenant adds a rifle and an attack drill, and Coastal League adds a marine and a health drill. These are original project factions, not replicas of historical nations. The two new pike-type units presently share much of their role; distinct tactical mechanics and balance are further work. Hats and equipment differentiate the three factions while retaining team colours.

The settlement menu pages services, updates availability when conditions change, and does not reveal an opponent's active service countdown. AI uses the existing paid, vision- and resource-gated commands to mix available allies and research matching upgrades; full competitive AI play is not validated. New service definitions are checked for duplicate or invalid entries at world initialization.

Roads have a soft shoulder that retracts at water and bridge structures. Unexplored fog now fully hides ground and roads, including at cell edges; the visual transition is softened on the known side only. The road texture is still visually uniform and caravan motion has not been visually validated in this iteration.

## Save compatibility

The additional catalog entries change rules identity. A narrowly scoped explicit copy operation accepts only normal 0.25.0 saves with the verified previous catalog identity. It checks the old and new world and AI states and compares the complete saved session after readback, then creates a new empty slot without replacing the original. The UI displays errors directly. These tests use synthetic prior-rule saves; no real prior 0.25.0 player checkpoint has yet been tested. Local 0.7.0 saves are outside this migration and were not modified. Authored scenarios, guided sessions and replays require their original rules/build and are not migrated. Integrity checks do not authenticate a save's origin or prove identical behavior across different builds.

## Evidence and scope

`Docs/Evidence/089` records 50/50 focused EditMode and 10/10 focused PlayMode cases with the same source fingerprint, including paid services, queue/cold restore, AI decisions, invalid definitions, map baselines, safe copy and UI, four river render studies, a controlled unit atlas, and road/fog image comparisons. Earlier failing runs 113, 114, 115, 124 and 126 are preserved outside the repository; their causes and repairs were checked rather than discarded. These are bounded Editor tests, not full regression, a full paid match, native input or a performance/release acceptance. The new fog shader adds texture samples; its frame-time cost is unmeasured.

Original Editor images: [neutral unit atlas](https://drive.google.com/file/d/148OgJVdweHuwB2J2Jt_iK-bIF5sbe1q7/view) and [winding river and road](https://drive.google.com/file/d/1-0wxYPrwqSdet97a3ondteRt0u6LoGMs/view). The atlas is a controlled model study, not a battle screenshot.
