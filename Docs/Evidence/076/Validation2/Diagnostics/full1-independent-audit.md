# Independent Full1 evidence audit — 0.19.0 Candidate2

**PASS: actual 1803 / 1803 passed; 1355 EditMode + 448 PlayMode; exit 0.** Independent parsing, hashes and source/case comparisons; no Unity re-execution.

- Exact tested/observed commit: `09e07a40fb0008cdb69719336a674cb228129244`; clean at audit observation.
- Source fingerprint: `e35a3b72c2a7b33371eb2c9b34a6c1838bc758b66dd73aacc1e707eeffe71a91`; all **776** live hashes equal both closed receipt and verification manifest.
- Original reports: `/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project/Builds/Tests/run-JENFOStU`.
- Closed original command PID: `40408`. The separately started Build1 is outside this audit.
- Runner wall: **1110.115761s**; Editor durations do not establish FPS.
- Audit JSON: **91833 bytes**, SHA256 `023349bc8df6fb69b1ba5d12c16de181a02676f6dbcfada3d3009215891a255e`.

| Actual original | Bytes | SHA256 |
|---|---:|---|
| `EditMode.xml` | 1117340 | `85b25538f34eced2090124fa31e595f2d94b7340e823c23cf91c53ded65b6ad4` |
| `PlayMode.xml` | 601829 | `602b205f8d22cc4fbc4ae6fe105d48c229778e34393d4e1bad35d3603460823a` |
| `source-evidence.json` | 99895 | `fdc9eee3fb643c184918abeaef1652513a616358c4a79ec690c32321e014c987` |
| `runner-receipt076.json` | 101580 | `dee744ef3ce5d4e76dd213728caff9d4cc2ec715e26f903467afbe9523dab912` |
| `stdout-0.log` | 873 | `2a1f9d1cac60db96c5f4bf58e1d97c64833e39d4bc36d8da35695db8e7207fb2` |

## Exact compatibility coverage

**All 1795 prior testcase identities still pass.** EditMode adds exactly the eight lifecycle identities; PlayMode has no added/removed identities. No baseline identities disappeared.

| Class | Actual passed | Sum of testcase seconds |
|---|---:|---:|
| `NavalTransport065Tests` | 17 | 6.223528 |
| `CurvedManeuver063Tests` | 9 | 49.458876 |
| `WaterNavigation061Tests` | 17 | 5.820050 |
| `MapLayout063Tests` | 28 | 0.387873 |
| `NavalDeliveryTests` | 6 | 0.333419 |
| `GarrisonExitTests` | 7 | 0.449336 |
| `NavigationSweep063Tests` | 9 | 0.113244 |
| **Old compatibility subtotal, inside full run** | **93** | **62.786326** |
| **New Healing076RiverLifecycleTests, inside full run** | **8** | **240.204188** |

The JSON includes all 107 EditMode and 79 PlayMode class statistics, the exact 93 compatibility identities and complete actual diagnostics for all eight new cases.

## New lifecycle results

| Executed lifecycle case | Result | Seconds |
|---|---|---:|
| `ActualGarrisonCleansOnlyContainerRelatedHealingAndColdExitNeedsFreshIntent(False)` | Passed | 13.410690 |
| `ActualGarrisonCleansOnlyContainerRelatedHealingAndColdExitNeedsFreshIntent(True)` | Passed | 13.997324 |
| `ExplicitHealPhysicallyCrossesWindingBridgeAndSurvivesColdCheckpoint(0)` | Passed | 46.081026 |
| `ExplicitHealPhysicallyCrossesWindingBridgeAndSurvivesColdCheckpoint(1)` | Passed | 46.765681 |
| `PaidWindingTransportCleansHealingAndColdDisembarkRestoresEligibility(False)` | Passed | 59.926718 |
| `PaidWindingTransportCleansHealingAndColdDisembarkRestoresEligibility(True)` | Passed | 59.406255 |
| `WindingRailingBlocksNearStationaryCareUntilOrdinaryDetour(0)` | Passed | 0.316231 |
| `WindingRailingBlocksNearStationaryCareUntilOrdinaryDetour(1)` | Passed | 0.300263 |

Actual new-test assertions cover both original Winding bridge crossings and railings, positive healing after legal movement, patient/healer Town entry cleanup, paid dock/transport production, actual passenger-radius shore/berth, physical short boarding, cold cargo continuation, body-safe opposite-shore exit and fresh post-exit explicit healing. Original Catalog stays unchanged; first-tick movement, body sweep/speed/height and related/unrelated queue assertions remain enforced.

## Evidence limits

- This independent audit parses and hashes completed Full1 originals and compares all source/case identities; it does not execute Unity again.
- The 1803 complete passing tests are 1355 EditMode plus 448 PlayMode. All 1795 Candidate1 baseline testcase identities remain and pass; exactly eight new Winding medical lifecycle identities are added.
- The old 93-case compatibility subtotal is seven exact EditMode classes, not another separate execution and not 93 additional cases beyond the full 1803.
- New medical crossing/container coverage remains controlled peaceful Winding fixtures with assembled wounded units, age II and funded resources; it is not a new ordinary-resource full human campaign.
- The two original Winding bridge spans/railings, real paid dock/transport, actual Town/container cleanup, temporary disk cold restore and fresh post-exit healing pass within this full run. New medical lifecycle coverage for Tropical/Tributaries/Estuary, new native Ottoman observations and human win/loss remain separate.
- Temporary test cold-save files are deleted by test cleanup. Paired World/MatchSimulation JSON and replay digest equality is not newly executed standalone native replay playback or native process restart.
- Editor test durations, including curved maneuver timing outputs, are not rendered FPS or native performance acceptance.
- The separate Build1 process started after closed Full1 is not treated as part of this test run. This audit makes no Build1, native, package, Drive, GitHub publication or whole-goal completion claim.
- Candidate1 original immutable packages, delivery and receipts remain preserved as earlier evidence; no old evidence was overwritten or relabeled.
- Only the two fresh external Candidate2/Diagnostics/full1-independent-audit files are written by this audit. Repository source, Git/cloud state, Unity processes and original reports are unchanged.

## Write boundary

Only fresh external audit JSON and Markdown were added. Full1 XML/log/receipt/verification evidence, production source, existing evidence, Unity, Git, cloud and later Build1 were not modified or operated.
