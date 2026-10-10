# Independent final Full2 077 audit — PASS

- Evidence integrity: **PASS**; actual runner exit **0**.
- **1372/1372 EditMode + 452/452 PlayMode = 1824/1824 passed**; zero failed/skipped/inconclusive.
- Exact original C2 **1803/1803 testcase fullname+mode identities retained and passed**; all actual **21 new cases passed**.
- Version **0.20.0**, runtime commit `a74fc436dc08c50f9d23dc506df92f7ff2942d76`, **784** source inputs, FP `8c858fe82932c1c4909af27aea8dab71a183812902c937ee4931003f33840335`.
- Runner/source-evidence/actual original XML SHA and current source map agree. Runtime Git commit objects independently match every source input; Docs are outside the runtime fingerprint.
- Separately running Build1 is not Full2 runtime evidence; no new build/app/native acceptance is claimed.

## Preserved failed Full1

- Original Full1 remains closed **exit2**, historical FP `b7783d5d2af85f67a26ba96f32591e9f20664e56cf3f11693d29095ab5ab1484`, runtime commit `43346c284b3c47963a30c728481ebe49f09c1d10`.
- Original initial exact Color comparison failure at RiverBankMesh077Tests.cs:28 remains intact; its passing source-evidence is absent as expected. Frozen independent Full1 audit stays **FAIL / integrity PASS**, not relabelled by this later run.

## Actual material isolation diagnostics

- Full2 actual test passed and reached initial Winding, tributaries, estuary, tropical and Winding-after-other-map checks. Source retains distinct-material identity assertions and final unchanged Winding palette.
- Per-RGBA tolerance is **1e-6**; full-precision outputs independently show tiny component differences. No exact internal roundtrip/half-shader cause is inferred.

| Actual label | Expected RGBA | Actual RGBA | Max absolute component delta |
|---|---|---|---:|
| Initial Winding | [0.74, 0.67, 0.48, 1.0] | [0.74, 0.67, 0.479999959, 1.0] | 4.1e-08 |
| tributaries | [0.62, 0.555, 0.37, 1.0] | [0.62, 0.555, 0.369999975, 1.0] | 2.5e-08 |
| estuary | [0.62, 0.555, 0.37, 1.0] | [0.62, 0.555, 0.369999975, 1.0] | 2.5e-08 |
| tropical | [0.66, 0.57, 0.38, 1.0] | [0.659999967, 0.569999933, 0.379999936, 1.0] | 6.7e-08 |
| Winding after other-map construction | [0.74, 0.67, 0.48, 1.0] | [0.74, 0.67, 0.479999959, 1.0] | 4.1e-08 |

## Actual controlled render study

- Actual render case and all 3 mesh cases passed; **15 original 1920×1080 PNGs / 5 camera pairs** byte SHA/IHDR/source+sidecar+pair bindings verified.
- Completion/mesh restoration and the 784-input source identity bind this actual Full2. Recorded repeat-current/signal threshold fields checked; **no full pixel/RGBA hash recomputation**.
- Frozen Editor Winding seed42 world with zero simulation steps; historical ground reconstructed in new source. No old-build/native/paid gameplay, FPS or visual approval.

| View | Changed pixels >3 | Repeat-current pixels >3 | Expected difference | Recorded gate |
|---|---:|---:|---|---|
| 01-bend-default26 | 121852 | 0 | True | PASS |
| 02-bend-close16 | 236870 | 0 | True | PASS |
| 03-winding-overview-default26 | 122168 | 0 | True | PASS |
| 04-protected-harbor-pad-default26 | 54201 | 0 | False | PASS |
| 05-protected-bridge-default26 | 93609 | 0 | False | PASS |

## All actual new cases

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
| PlayMode | `Frontier.Tests.RiverBankMesh077Tests.WindingSedimentPaletteHasItsOwnMaterialAndDoesNotLeakIntoOtherRiverMaps` | Passed |
| PlayMode | `Frontier.Tests.RiverBankRender077Tests.OrdinaryCameraPairsCompareReconstructedHistoricalGroundWithNewBankProfile` | Passed |

## Original SHA bindings

54 original files were read and rehashed unchanged before closure. Exact bytes/SHA/modes and all per-class counts are in the JSON receipt.

| Original | Bytes | SHA256 |
|---|---:|---|
| `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.20.0/Candidate1/Full2/runner-receipt077.json` | 102650 | `87d917ce3cde4aa4302d38152831dd938fc25658d091a61c310e551fd480154a` |
| `/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project/Builds/Tests/run-QOgQGatR/EditMode.xml` | 1131800 | `2218fadce0908b2b11711d233f738c34fa4026a80a933bd9338a60ca78285e05` |
| `/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project/Builds/Tests/run-QOgQGatR/PlayMode.xml` | 606635 | `e474222050046aa14c0641e4ff7a3e7e1f73c55e4b1e65ef228a7960f6ac34f3` |
| `/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project/Builds/Tests/run-QOgQGatR/source-evidence.json` | 100959 | `bad15f1f8159ccd81efc84484677ce4a6d52d656055e9ed9ba1cbed712c1bb5b` |
| `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.20.0/Candidate1/Full2/Captures/source-inventory-bank077.json` | 146656 | `a68a1cda8945fbc31087c230931816b13b900cc09eca997c3c0a9bb688302de7` |
| `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.20.0/Candidate1/Full2/Captures/study-run077.json` | 7960 | `9e2f232580b1598a724a1c29a62f3a8ef5a70d8df941bb1537f9b462516911ad` |
| `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.20.0/Candidate1/Diagnostics/full1-independent-audit077.json` | 69306 | `1872c714726da8d2256cd756c76839bc1e8ed6537ffe2ca2509f2d3b10159063` |
| `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.20.0/Candidate1/Diagnostics/full1-independent-audit077.md` | 13622 | `d7cf4efe11c9b0a0f4bcfbfe0b845709fd9e365839070c1801b8ac1b1d0fe0a7` |

Integrity errors: none.

