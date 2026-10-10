# Render077 Render1 — bounded visual review

## Identity and reviewed evidence

- Reviewer actually opened the four original PNGs with `view_image`: `Captures/01-bend-default26-{baseline-reconstructed-historical-ground,current-bank077}.png` and `Captures/02-bend-close16-{baseline-reconstructed-historical-ground,current-bank077}.png`; read both pair JSONs, the close-current receipt, and `study-run077.json`. The four PNG byte SHA256 values were independently read back and matched their pair receipts.
- This is actual GameCamera / WorldView / URP controlled rendering, 0.20.0, Winding seed 42, source fingerprint `e98ac815888d142300f9312c07116c2fd9f5d44321c100f9e899c49e7b2efb62`; inventory SHA256 `4a9047d1615f56be6bfc7f21df45cb91652a69ba78be2bff8c17ab06be0b66a4`. Receipts identify reference revision `3a6050a5d06661dcf3342d9b22b33b7d1e998bf4`.
- The baseline is reconstructed historical .42m/3m ground height and RGBA on this same new-build ground mesh. New-build materials, water, models and placements remain shared. It is not an old-build/native capture. World time and simulation steps are zero; fog is fixture-revealed. No gameplay, FPS, paid construction or full-product acceptance follows from this review.

| View | Historical PNG SHA256 | Current PNG SHA256 | Changed RGB pixels, threshold 3 | Mean absolute RGB delta | Repeat-current |
|---|---|---|---:|---:|---:|
| 01 default zoom 26 | `2777a7d05707c7fa8854e3c69f3aecf583248cbec365d9182cb1a319f59d3614` | `7b4969e66181f635fa2f556b7d7e3c525057406e91145b3d09f216a1311cf334` | 95,307 | .313427 | 0 pixels / 0 mean |
| 02 close zoom 16 | `06970ec00de38886baa860e70cbe3a51ac830e370d3b30934652c821b409d2d5` | `4010012dc801b19a48e903c70752cfd88047005f2efd6192b5291842b7acee21` | 192,975 | .669356 | 0 pixels / 0 mean |

Both pair receipts report exact mesh restoration and unchanged world hash. Pixel metrics establish a stable real difference, not a quality score or automatic approval.

## Practical verdict

1. **Real but visually restrained improvement.** At close16 the west/inner bend has a broader ochre/sediment shoulder; the east/outer bend retains a tighter darker rim. At default26 the same asymmetry is visible on direct comparison, but the dry sediment remains close in hue/value to surrounding olive turf. The bend still reads primarily as a uniform green-brown landscape around a turquoise river. The user-facing bank-identity improvement is therefore weaker than the numerical profile change suggests.
2. **A separate contact-line artifact remains.** The new east/outer bank, especially its upper curved section beside the tree line, has small polygonal notches at the water/land contact. These are clearly visible at close16 and reduced, not absent, at default26. This review identifies appearance only; it does not determine whether mesh sampling, slope/water intersection or another geometric contributor is responsible. A color change is not a notch repair.
3. No missing ground, catastrophic discontinuity, or changed object placement is apparent in these two reviewed view pairs. This is a narrow observation, not acceptance of every bank, bridge, harbor or map.

## Recommendation before the expensive full run

**Do one bounded Winding-only sediment-palette revision, then Render2 at the identical views.** Parent selected a modest trial `_SandColor = (.74,.67,.48)` rather than current temperate `(.62,.555,.37)`. Keep turf, wet mud, water, ground geometry, BankHeight, BankLayers and their authority/protection contracts unchanged. The proposed color has not been rendered or accepted in this review.

- `RiverLandscapePresentation.GroundMaterial` currently caches only `tropical` / `temperate`; give Winding its own material/cache key. A shared temperate tint edit would unintentionally affect other river maps. Add actual material-isolation assertions for the Winding trial and the original non-Winding temperate color.
- **Deliberate visual boundary:** the Winding palette also changes protected-pad/bridge-near sediment pixels wherever their existing sand weights are nonzero. Protected heights and layer weights stay unchanged; exact protected visual colors are not being preserved or claimed. Avoid expanding this slice into shader/alpha-mask plumbing merely to disguise that boundary.
- Preserve Render1 originals, receipts and tests. Render2's within-run reconstructed historical pair still shares its new material: it isolates ground geometry/layer differences, not the full old palette. Compare Render1-current against Render2-current at the same camera as the separate palette assessment.
- At default26, ask whether the west inner deposition band is readily distinguishable from adjacent turf without losing the dark wet rim or turning the whole bank into a bright beach. Check close16 for clipping/overbrightness and inspect the existing harbor/bridge frames for the deliberately changed tint. Do not weaken repeat-control, mesh restoration, world hash, or pixel-difference assertions.
- Keep the outer contact notches as a bounded known visual issue. Passing Render2 and the new isolation test can justify the full regression freeze for this palette slice; it does not close shoreline geometry polish or the four-part river roadmap.

No source, Unity, image or existing receipt was changed by this reviewer; only this external review document was written.
