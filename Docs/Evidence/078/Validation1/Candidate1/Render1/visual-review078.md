# 078 Render1 — original paired image review

Status: **accepted-bounded-shoreline-visual-improvement-and-evidence-integrity**

Current source: `05dc944960c2724e08f2d6eeb02df282710d099ae36cf35bb7923d8918166217`, 788 inputs; version 0.21.0; reference HEAD `0a5788e1ef22eea27b62b29d309684b303856f29`. Actual PlayMode XML: **1/1 passed**, 21.252065 s; runner exit 0 and source-stable. Zero Editor build GUID is not a native build identity.

## Evidence checks

- All 12 original PNG byte SHA, 1920×1080 RGBA8 IHDR, CRC/decode and decoded RGBA SHA matched. Unity RGBA hashes use bottom-up rows; decoded comparison reversed rows only in memory. No image output was edited or re-encoded.
- All four pair files equal the run receipt, with identical camera/world/time/scene identity within each triple; source inventory equals the runner before map and current 788-input working tree.
- Runtime asserted exact candidate mesh restoration and scene/material/world invariance; independently checked receipt consistency and the hashing source mechanism, not an externally recreated Unity scene.
- This reviewer authored the fixture. This is a separate post-run original-byte and visual audit, not an independently authored fixture or second Unity execution.

| View | Zoom | Coarse/current pixels >3 | Repeat pixels >3 | Repeat RGB mean | Repeat exact RGBA |
|---|---:|---:|---:|---:|---|
| 01-east-bend-default26 | 26 | 52333 | 0 | 8.03755144033e-07 | False |
| 02-east-bend-close16 | 16 | 82874 | 0 | 0 | True |
| 03-harbor-pad-default26 | 26 | 37519 | 0 | 0 | True |
| 04-bridge-default26 | 26 | 43368 | 0 | 0 | True |

## Direct original-image findings

### 01-east-bend-default26

At ordinary 26 zoom, the coarse ground shows repeated saw-tooth/triangular shoreline bites along the upper-right outer bank below the tree line. Fine current makes that contact edge visibly more continuous and keeps the broad warm inner-bank sediment lobe. No new visible terrain hole, disconnected shore band or scenery displacement in this view. Remaining finite polyline/antialias and water-foam modulation are not a contour-perfect claim.

### 02-east-bend-close16

At close 16 zoom, the same coarse outer-tree-line staircase is easier to identify and is substantially reduced in fine current. The outer bank remains narrower/darker than the inner sand lobe. Visible bridge pier and reed/gravel bases retain apparent ground contact; no new seam or detached base seen. Some gentle polygonal silhouette/foam texture remains; no zero-defect whole-map claim.

### 03-harbor-pad-default26

The harbor context is an EMPTY reserved pad near the opening town, not a built or paid Dock. Fine current smooths the downstream shore contact without moving the town, bridge, units or tree clumps. Bridge pier feet and reeds show no newly visible detachment. Dark clear background beyond the map bounds appears in both controls and is not accepted as an in-map hole or gameplay evidence.

### 04-bridge-default26

Both bridge approaches, deck, visible masonry piers, river contact and bank reeds remain coherent in the same camera. Fine current reduces coarse serration on surrounding shores. No newly visible approach seam, floating pier foot, broken terrain patch or reed-base drift was seen at this view. This does not verify movement across the bridge or paid construction.

## Bounded verdict

Accept the shown Winding shoreline visual improvement at these four controlled views, contingent on separate actual-clearance/mesh contracts and broader regression. Not all geometric/visual defects on all map positions or seeds.

No new visible seam, hole, floating bridge foot or reed-base drift was seen in the reviewed four camera contexts. The remaining finite polyline/antialias and water-foam modulation are not a claim of exact contour-perfect geometry. A still frame does not prove absence of temporal z-fighting. The harbor is an EMPTY pad, not a paid Dock. Native movement, bridge crossing, ship/harbor construction, FPS and other maps remain separate gates.

Ground cost: 16,641 → 66,049 vertices; 32,768 → 131,072 triangles. No performance-neutrality claim. 34 original input files / 30,287,604 bytes were rehashed unchanged before/after audit.

Original eight inspected coarse/current paths are listed in the JSON. Full twelve-PNG byte/decoded hashes, four repeat controls, source/XML/runner identity, and exact original input manifest are retained there.
