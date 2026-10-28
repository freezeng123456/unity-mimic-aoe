# Independent Full1 077 audit — FAIL

- Evidence integrity: **PASS**; acceptance: **FAIL** (actual runner exit 2).
- Actual EditMode: **1372/1372 passed**; PlayMode: **451/452 passed**, one failed; zero skips/inconclusive.
- Exact original C2 **1803/1803 identities retained and passed**; actual new **21 = 20 passed + 1 failed**.
- Historical version **0.20.0**, runtime commit `43346c284b3c47963a30c728481ebe49f09c1d10`, **784** inputs, FP `b7783d5d2af85f67a26ba96f32591e9f20664e56cf3f11693d29095ab5ab1484`.
- Commit objects and runner before/after source inventories bind the historical tested source; Docs changes are outside that runtime inventory.
- Audit closure observed the later test-only fixture fix at `a74fc436dc08c50f9d23dc506df92f7ff2942d76`, FP `8c858fe82932c1c4909af27aea8dab71a183812902c937ee4931003f33840335`; only `Assets/Frontier/Tests/PlayMode/RiverBankMesh077Tests.cs` differs. This is distinct from failed Full1 historical source; no rerun result is inferred.
- Passing `source-evidence.json` is expected absent: failed PlayMode exits `test.sh` before record/verify.

## Exact failure

`Frontier.Tests.RiverBankMesh077Tests.WindingSedimentPaletteHasItsOwnMaterialAndDoesNotLeakIntoOtherRiverMaps`

```text
  Expected: RGBA(0.740, 0.670, 0.480, 1.000)
  But was:  RGBA(0.740, 0.670, 0.480, 1.000)
at Frontier.Tests.RiverBankMesh077Tests.WindingSedimentPaletteHasItsOwnMaterialAndDoesNotLeakIntoOtherRiverMaps () [0x0001b] in /Users/Apple_501/Documents/Codex/work/frontier-reckoning/project/Assets/Frontier/Tests/PlayMode/RiverBankMesh077Tests.cs:28
```

The first exact Color equality at line28 failed. Formatted values round to the same three decimals; no float-bit cause is asserted. Subsequent material-object isolation/other-map/final palette assertions in this case were not executed. No tolerance change or rerun was performed by this audit.

## Render study (bounded)

- Actual render test passed; **15 original 1920×1080 PNGs / 5 camera pairs** have byte SHA, sidecar/source-inventory/pair bindings verified.
- Actual source inventory matches the same 784-input FP and runtime commit. Recorded completion and exact ground restoration are true.
- Only recorded repeat-control and visible-signal thresholds were independently checked; **no full pixel-delta or RGBA hash recomputation**.
- Editor frozen Winding seed42 fixture with zero simulation steps; historical ground is reconstructed inside the new source, not an old-build capture. No native, paid gameplay, FPS or visual-quality acceptance.

| View | Changed pixels >3 | Repeat-current pixels >3 | Expected difference | Recorded gate |
|---|---:|---:|---|---|
| 01-bend-default26 | 121852 | 0 | True | PASS |
| 02-bend-close16 | 236870 | 0 | True | PASS |
| 03-winding-overview-default26 | 122168 | 0 | True | PASS |
| 04-protected-harbor-pad-default26 | 54201 | 0 | False | PASS |
| 05-protected-bridge-default26 | 93609 | 0 | False | PASS |

## New actual cases

| Mode | Case | Result |
|---|---|---|
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.DenseActualWalkableBankStepsKeepTheExistingPointFourPhysicalSlopeContract` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.InsideBendSignWorksForBothTurnDirections(-1)` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.InsideBendSignWorksForBothTurnDirections(1)` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.LiteralEasternMeanderHasItsInnerBankWestAndOuterBankEastInTheStoredQuery` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.OriginalWindingControlsFishStartsAndTerrainHashesStayUnchanged` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.OtherRiverLayoutsRetainTheirHistoricalBankHeightsAndLayers("river")` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.OtherRiverLayoutsRetainTheirHistoricalBankHeightsAndLayers("tropical")` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.OtherRiverLayoutsRetainTheirHistoricalBankHeightsAndLayers("tributaries")` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.OtherRiverLayoutsRetainTheirHistoricalBankHeightsAndLayers("estuary")` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.OtherRiverLayoutsRetainTheirHistoricalBankHeightsAndLayers("generated_river")` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.OtherRiverLayoutsRetainTheirHistoricalBankHeightsAndLayers("generated_delta")` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.OtherRiverLayoutsRetainTheirHistoricalBankHeightsAndLayers("generated_strait")` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.ProtectedBridgeRampsHarborPadsAndTownPadsKeepTheirOldProfile` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.RiverBedWaterLevelAndBeyondThreeMeterInlandGroundRetainHistoricalHeights` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.SharedLayersAreNormalizedAndTwoSeparatedBendPairsHaveUsefulInnerOuterContrast` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.SharedLayoutRestoredWorldConsumerContinuesRealMovementAndMatchesWarmState` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.StraightOrDegenerateSamplesHaveNoInventedBend` | Passed |
| PlayMode | `Frontier.Tests.RiverBankMesh077Tests.ActualEasternMeanderMeshHasBroaderSandOnItsLiteralWestInnerBank` | Passed |
| PlayMode | `Frontier.Tests.RiverBankMesh077Tests.ActualGroundMeshColorsAndHeightsEqualTheSharedMapQueries` | Passed |
| PlayMode | `Frontier.Tests.RiverBankMesh077Tests.WindingSedimentPaletteHasItsOwnMaterialAndDoesNotLeakIntoOtherRiverMaps` | Failed |
| PlayMode | `Frontier.Tests.RiverBankRender077Tests.OrdinaryCameraPairsCompareReconstructedHistoricalGroundWithNewBankProfile` | Passed |

## Original hashes

48 originals were read and rehashed unchanged before closure; exact bytes/SHA/modes are in the JSON receipt.

| Original | Bytes | SHA256 |
|---|---:|---|
| `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.20.0/Candidate1/Full1/runner-receipt077.json` | 102657 | `6d0829ae5d1a6caaa615016d1853d37852dd24049fd360a8acefdc35cdbd26b1` |
| `/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project/Builds/Tests/run-La5Xze2I/EditMode.xml` | 1131811 | `9356ee0a2fbcf364f091292f7866557d1328c51300a30826699802951d9d0768` |
| `/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project/Builds/Tests/run-La5Xze2I/PlayMode.xml` | 606914 | `95453205b710588524f10273087f6ab17713c221ba922dd25ee47945c3d57c5b` |
| `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.20.0/Candidate1/Full1/Captures/source-inventory-bank077.json` | 146656 | `6b6fa998fcb9e51d29d0f44ba7f497b31b4952d343af92d82484353803c85150` |
| `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.20.0/Candidate1/Full1/Captures/study-run077.json` | 7960 | `2367305b8ee50bd7ca449936b1bc792afd12fd6a7e1ef45fda8f4ddad3aac767` |
# Independent Full1 077 audit — FAIL

- Evidence integrity: **PASS**; acceptance: **FAIL** (actual runner exit 2).
- Actual EditMode: **1372/1372 passed**; PlayMode: **451/452 passed**, one failed; zero skips/inconclusive.
- Exact original C2 **1803/1803 identities retained and passed**; actual new **21 = 20 passed + 1 failed**.
- Historical version **0.20.0**, runtime commit `43346c284b3c47963a30c728481ebe49f09c1d10`, **784** inputs, FP `b7783d5d2af85f67a26ba96f32591e9f20664e56cf3f11693d29095ab5ab1484`.
- Commit objects and runner before/after source inventories bind the historical tested source; Docs changes are outside that runtime inventory.
- Passing `source-evidence.json` is expected absent: failed PlayMode exits `test.sh` before record/verify.

## Exact failure

`Frontier.Tests.RiverBankMesh077Tests.WindingSedimentPaletteHasItsOwnMaterialAndDoesNotLeakIntoOtherRiverMaps`

```text
  Expected: RGBA(0.740, 0.670, 0.480, 1.000)
  But was:  RGBA(0.740, 0.670, 0.480, 1.000)
at Frontier.Tests.RiverBankMesh077Tests.WindingSedimentPaletteHasItsOwnMaterialAndDoesNotLeakIntoOtherRiverMaps () [0x0001b] in /Users/Apple_501/Documents/Codex/work/frontier-reckoning/project/Assets/Frontier/Tests/PlayMode/RiverBankMesh077Tests.cs:28
```

The first exact Color equality at line28 failed. Formatted values round to the same three decimals; no float-bit cause is asserted. Subsequent material-object isolation/other-map/final palette assertions in this case were not executed. No tolerance change or rerun was performed by this audit.

## Render study (bounded)

- Actual render test passed; **15 original 1920×1080 PNGs / 5 camera pairs** have byte SHA, sidecar/source-inventory/pair bindings verified.
- Actual source inventory matches the same 784-input FP and runtime commit. Recorded completion and exact ground restoration are true.
- Only recorded repeat-control and visible-signal thresholds were independently checked; **no full pixel-delta or RGBA hash recomputation**.
- Editor frozen Winding seed42 fixture with zero simulation steps; historical ground is reconstructed inside the new source, not an old-build capture. No native, paid gameplay, FPS or visual-quality acceptance.

| View | Changed pixels >3 | Repeat-current pixels >3 | Expected difference | Recorded gate |
|---|---:|---:|---|---|
| 01-bend-default26 | 121852 | 0 | True | PASS |
| 02-bend-close16 | 236870 | 0 | True | PASS |
| 03-winding-overview-default26 | 122168 | 0 | True | PASS |
| 04-protected-harbor-pad-default26 | 54201 | 0 | False | PASS |
| 05-protected-bridge-default26 | 93609 | 0 | False | PASS |

## New actual cases

| Mode | Case | Result |
|---|---|---|
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.DenseActualWalkableBankStepsKeepTheExistingPointFourPhysicalSlopeContract` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.InsideBendSignWorksForBothTurnDirections(-1)` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.InsideBendSignWorksForBothTurnDirections(1)` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.LiteralEasternMeanderHasItsInnerBankWestAndOuterBankEastInTheStoredQuery` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.OriginalWindingControlsFishStartsAndTerrainHashesStayUnchanged` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.OtherRiverLayoutsRetainTheirHistoricalBankHeightsAndLayers("river")` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.OtherRiverLayoutsRetainTheirHistoricalBankHeightsAndLayers("tropical")` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.OtherRiverLayoutsRetainTheirHistoricalBankHeightsAndLayers("tributaries")` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.OtherRiverLayoutsRetainTheirHistoricalBankHeightsAndLayers("estuary")` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.OtherRiverLayoutsRetainTheirHistoricalBankHeightsAndLayers("generated_river")` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.OtherRiverLayoutsRetainTheirHistoricalBankHeightsAndLayers("generated_delta")` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.OtherRiverLayoutsRetainTheirHistoricalBankHeightsAndLayers("generated_strait")` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.ProtectedBridgeRampsHarborPadsAndTownPadsKeepTheirOldProfile` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.RiverBedWaterLevelAndBeyondThreeMeterInlandGroundRetainHistoricalHeights` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.SharedLayersAreNormalizedAndTwoSeparatedBendPairsHaveUsefulInnerOuterContrast` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.SharedLayoutRestoredWorldConsumerContinuesRealMovementAndMatchesWarmState` | Passed |
| EditMode | `Frontier.Tests.RiverBankAsymmetry077Tests.StraightOrDegenerateSamplesHaveNoInventedBend` | Passed |
| PlayMode | `Frontier.Tests.RiverBankMesh077Tests.ActualEasternMeanderMeshHasBroaderSandOnItsLiteralWestInnerBank` | Passed |
| PlayMode | `Frontier.Tests.RiverBankMesh077Tests.ActualGroundMeshColorsAndHeightsEqualTheSharedMapQueries` | Passed |
| PlayMode | `Frontier.Tests.RiverBankMesh077Tests.WindingSedimentPaletteHasItsOwnMaterialAndDoesNotLeakIntoOtherRiverMaps` | Failed |
| PlayMode | `Frontier.Tests.RiverBankRender077Tests.OrdinaryCameraPairsCompareReconstructedHistoricalGroundWithNewBankProfile` | Passed |

## Original hashes

48 originals were read and rehashed unchanged before closure; exact bytes/SHA/modes are in the JSON receipt.

| Original | Bytes | SHA256 |
|---|---:|---|
| `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.20.0/Candidate1/Full1/runner-receipt077.json` | 102657 | `6d0829ae5d1a6caaa615016d1853d37852dd24049fd360a8acefdc35cdbd26b1` |
| `/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project/Builds/Tests/run-La5Xze2I/EditMode.xml` | 1131811 | `9356ee0a2fbcf364f091292f7866557d1328c51300a30826699802951d9d0768` |
| `/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project/Builds/Tests/run-La5Xze2I/PlayMode.xml` | 606914 | `95453205b710588524f10273087f6ab17713c221ba922dd25ee47945c3d57c5b` |
| `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.20.0/Candidate1/Full1/Captures/source-inventory-bank077.json` | 146656 | `6b6fa998fcb9e51d29d0f44ba7f497b31b4952d343af92d82484353803c85150` |
| `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.20.0/Candidate1/Full1/Captures/study-run077.json` | 7960 | `2367305b8ee50bd7ca449936b1bc792afd12fd6a7e1ef45fda8f4ddad3aac767` |

Integrity errors: none.

