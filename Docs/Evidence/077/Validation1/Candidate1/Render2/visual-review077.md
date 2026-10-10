# Render077 — independent Render1 / Render2 visual review

## Bounded verdict

**Accept the isolated palette readability improvement in the inspected controlled views and keep this source frozen for planned regression.** Actual `view_image` inspection compared the Render1-current and Render2-current originals at default26 and close16; the three remaining Render2 current originals (overview, reserved harbor pad, bridge) were also viewed. The warm pale-ochre deposition shoulder now separates clearly from the olive turf even at default26. Close16 makes the inner-side width/color distinction obvious. A dark wet rim remains; no severe white clipping or global turf recoloring is apparent in these views.

**Outer/east water-contact polygonal notches remain**, most clearly beside the upper tree line. Brighter sediment is not a geometry repair. This is a controlled view/palette verdict, not native/paid gameplay, FPS, all-map quality, protected pixel preservation, or completion of the four-part river roadmap. No further source adjustment is requested during Full1; this reviewer did not inspect or declare a Full1 outcome.

## Identity and source comparison

- Render1: version 0.20.0, revision `3a6050a5d06661dcf3342d9b22b33b7d1e998bf4`, 784-source FP `e98ac815888d142300f9312c07116c2fd9f5d44321c100f9e899c49e7b2efb62`; inventory file SHA `4a9047d1615f56be6bfc7f21df45cb91652a69ba78be2bff8c17ab06be0b66a4`.
- Render2: version 0.20.0, revision `43346c284b3c47963a30c728481ebe49f09c1d10`, 784-source FP `b7783d5d2af85f67a26ba96f32591e9f20664e56cf3f11693d29095ab5ab1484`; inventory file SHA `6b6fa998fcb9e51d29d0f44ba7f497b31b4952d343af92d82484353803c85150`.
- Both fingerprints were independently recomputed from the exact compact ordinal-key source inventory. Same 784 paths: **782 unchanged, exactly 2 changed**. Both runner receipts bind these inventories before/after, stable source and exit 0; both XML files were SHA-checked and record **1 render test passed, 0 failed**. Render2 XML SHA `7951287ed4bd967e0f88a67115b61293cfa76be90a752e85e19b701801813383`.

The exact two source changes are:

1. `Assets/Frontier/Presentation/RiverLandscapePresentation.cs`: `e1d6260436a3ee45a926f0139aaa98af3c0e25cacdc26ec2ed474f49cd7b2786` → `076f657a1944e5c365cd8e42dccf2778803fc83f96dc09d381c3ae70d8bf62bc`. This is the only production delta: Winding gets its own ground-material/cache key and SandColor `(.74,.67,.48)` instead of shared temperate `(.62,.555,.37)`. Tropical color, other temperate color, turf, mud, water, geometry, bank queries and layer weights are unchanged.
2. `Assets/Frontier/Tests/PlayMode/RiverBankMesh077Tests.cs`: `737e8f59599dd753493661aca813e240f66c251f299a79cdb07c753e28a67212` → `eae756553ab40f0e89cd13209283575d7d96941dc0c69b3ec02a30a78651ef05`. Adds actual-material identity/color isolation assertions for Winding versus Tributaries, Estuary and Tropical. It does not alter the render-study code. **Render2 executes the single render study, not this added isolation test.** These two Render2 source hashes also match their recorded Git blobs. Commit 43346c2 additionally changes two Docs files outside the source-fingerprint inventory; “two changes” here means source changes, not total commit files.

## All five pairs / fifteen originals audited

All 15 Render2 PNG byte SHA values match both per-image JSON and pair JSON; all 15 Render1 PNGs were checked likewise. PNG headers are 1920×1080 RGBA8. Every new receipt matches the recorded source FP/inventory SHA/revision, Winding42, version, zero simulation steps/world time, 133 entities, actual camera focus/transform/zoom and URP. All five pairs report exact mesh restoration and unchanged world hash.

All five cross-run current receipts have the same world SHA `f0469e004504ab09ddaacbbbf76ea1ae0ad8b2796a379bd4110a68f35446df3c`, current ground-mesh SHA `ba50da44c5dfa029da5d3d434f92f9620cc17dc2e13ce7fd8752e64b6bde1d71`, historical ground-mesh SHA `e2e00adcd1adecfbf627556d8aff692d5d89c1d7e406f1e2d3bdf51f42018623`, and camera fields. Match IDs, frames/Unity time and source identities differ as expected; this is not a claim that every runtime byte is identical. The companion JSON contains every PNG/receipt hash and detailed assertions.

| Render2 view | Baseline/current pixels above RGB threshold 3 | Mean RGB delta | Repeat pixels above 3 | Repeat mean RGB delta |
|---|---:|---:|---:|---:|
| 01-bend-default26 | 121,853 | 0.649966 | 0 | 1.76826131337e-06 |
| 02-bend-close16 | 236,870 | 1.360441 | 0 | 0 |
| 03-winding-overview-default26 | 122,168 | 0.650982 | 0 | 0 |
| 04-protected-harbor-pad-default26 | 54,201 | 0.259224 | 0 | 0 |
| 05-protected-bridge-default26 | 93,609 | 0.496018 | 0 | 0 |

**Precision correction:** “repeat 0” means zero pixels **above threshold 3** for all five views. Default26 control/current PNGs have different SHA and a tiny reported mean delta `1.768261313372932e-06`; do not label that pair byte-identical or exact zero RGB. Other four repeat pairs have reported mean 0 and identical PNG SHA. Pixel metrics/raw RGBA hashes are receipt facts, not independently decoded or recomputed in this review; PNG byte hashes, header and receipt bindings were independently verified.

## Interpretation boundaries

- Render1-current versus Render2-current is the palette comparison. Within either run the reconstructed historical ground pair shares that run's new-build material, models, placements and water; it is not an old-build landscape capture.
- Winding protected pads/bridge-near sand pixels deliberately receive the new tint wherever their existing sand weights are nonzero. Protected height/layer profiles remain unchanged; old visual colors are not preserved. The harbor frame shows reserved shoreline context, **not a built dock**. The three context frames reveal no obvious catastrophic ground/bridge discontinuity, but are not detailed harbor-model acceptance.
- Preserve both sets of original images, tests, receipts and this review. Outer contact-line notches remain a bounded known visual issue. Further geometry polish, native evidence, broader river content and full regression results have their own acceptance boundaries.

Only these new external review JSON/Markdown files were written by this reviewer. No Unity/source/image/test/receipt mutation occurred.
