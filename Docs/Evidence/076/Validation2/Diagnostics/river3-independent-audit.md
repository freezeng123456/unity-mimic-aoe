# Independent River3 evidence audit — 0.19.0 Candidate2

**PASS: actual closed run 8 / 8 passed, exit 0.** This audit re-reads originals and independently rehashes source; it does not launch Unity.

- Tested commit: `9b7a5a8f89e96f37bdd5ba74c7da50ae648e2774`.
- Observed later commit: `09e07a40fb0008cdb69719336a674cb228129244`; only three `Docs/` paths changed since test commit, working tree clean at observation.
- Stable source: `e35a3b72c2a7b33371eb2c9b34a6c1838bc758b66dd73aacc1e707eeffe71a91` — all **776** input hashes independently match the closed receipt; only the new test `.cs` / `.meta` differ from Candidate1 runtime inputs.
- XML: **13459 bytes**, SHA256 `94ad4d87f0d06041f5da2eca6d6ed898fd11cf446de2541df4e3c34b8d7af623`.
- Original receipt: **101341 bytes**, SHA256 `c26412d9e4c0d0e1a7fd1a0a6d632a888a479d6dc528db2980b34255c883045c`.
- XML duration **250.638374s**; runner wall **260.351581s**. These are Editor diagnostics, not FPS.
- Audit JSON: **30860 bytes**, SHA256 `08906998267d1ea91121fa8bdb364e863ad2a1000ee2a2964ae327f011b63001`.

| Executed case | Result | Seconds |
|---|---|---|
| `ActualGarrisonCleansOnlyContainerRelatedHealingAndColdExitNeedsFreshIntent(False)` | Passed | 14.849375 |
| `ActualGarrisonCleansOnlyContainerRelatedHealingAndColdExitNeedsFreshIntent(True)` | Passed | 14.461861 |
| `ExplicitHealPhysicallyCrossesWindingBridgeAndSurvivesColdCheckpoint(0)` | Passed | 47.902993 |
| `ExplicitHealPhysicallyCrossesWindingBridgeAndSurvivesColdCheckpoint(1)` | Passed | 48.430009 |
| `PaidWindingTransportCleansHealingAndColdDisembarkRestoresEligibility(False)` | Passed | 61.969691 |
| `PaidWindingTransportCleansHealingAndColdDisembarkRestoresEligibility(True)` | Passed | 62.332810 |
| `WindingRailingBlocksNearStationaryCareUntilOrdinaryDetour(0)` | Passed | 0.324213 |
| `WindingRailingBlocksNearStationaryCareUntilOrdinaryDetour(1)` | Passed | 0.314391 |

## Confirmed executed coverage

- **Both original Winding bridge crossings and positive healing:** Initial physical route must visit the specified original span; actual doctor enters that bridge before any Heal; disk cold midpoint retains the actual Heal approach; warm/cold steps compare full World/Simulation JSON and replay digest; positive healing occurs only beyond the full far ramp; patient reaches full HP and Heal order cleans up. Outputs report 383 healing receipts for each span.
- **Native bridge side railing occlusion and legal detour:** Full-body legal points are close enough to heal but original railing blocks the medical clear line; 30 steps leave positions/HP unchanged with no automatic Heal; public explicit Heal physically takes the ordinary ramp mouth detour before a positive receipt and opened clear line.
- **Actual building entry by patient and healer; unrelated queues preserved:** Before entry there is an actual positive active Heal and a genuine non-head queued relation. Public Garrison walks into original Town within 12 seconds. Own and related active/queued Heal links clear; unrelated Heal/Shift Move order data remains byte-equal JSON, still-wounded discriminator remains, and entry tick has no entrant care. Actual disk cold restore, 50 paired contained steps, atomic invalid Heal rejection, public Ungarrison/rally, no stale care, and fresh positive explicit care after physical doctor return pass.
- **Paid physical dock/ship production, actual passenger-radius shore and entry:** Actual builder surveys/builds a paid Dock; exact construction and training costs, queue reservation and produced ship/Payment/UnitTrained are checked. Ordinary Move reaches its actual formation/resolved goal within .1m, not a raw preliminary berth. Independent public TryTransportShore max(.4, passenger radius) prediction must equal real Embark shore/berth within .001m; first step physically walks and does not board instantly; actual boarding occurs within 12 seconds and ship is within .251m of the real resolved berth (production threshold .25m). Entry clears only related medical intent.
- **Real cargo cold continuation, sailing, body-safe exit and fresh care:** Actual disk cold save while aboard; 50 paired cargo steps keep HP, identity, population=7, one double-sided occupancy relation and passenger position/previousPosition equal to the host, with no entrant healing. Invalid cargo Heal is atomic. Public Disembark sails >10m toward an opposite landing >20m away, emits one actual unload, retains population, releases a full-body clear land unit outside bridge/ramp with .15m body padding, leaves no stale Heal, then a fresh explicit command yields positive care after physical doctor movement.
- **Whole-fixture movement/medical invariants:** Original Catalog hash stays unchanged each step; after initial Seal only public commands and actual MatchSimulation.Step alter gameplay. Each step checks live body navigation domain, swept segment clearance, speed budget, bridge side height bound and peaceful no-combat premise. Every actual Heal receipt checks positive clamped delta/rate, live medical eligibility, stationary endpoints, quiet clocks, range and native clear line.

## Actual diagnostics retained verbatim

### `ActualGarrisonCleansOnlyContainerRelatedHealingAndColdExitNeedsFreshIntent(False)`

```text
Garrison: id=137; host=1; step=9; t=0.9000001; hp=68.5; expectedQueues=2
Disk checkpoint: step=9; t=0.9000001; schema=5; bytes=22269; selected=137
```

### `ActualGarrisonCleansOnlyContainerRelatedHealingAndColdExitNeedsFreshIntent(True)`

```text
Garrison: id=134; host=1; step=16; t=1.60000026; hp=31.0000019; expectedQueues=2
Disk checkpoint: step=16; t=1.60000026; schema=5; bytes=22577; selected=134
```

### `ExplicitHealPhysicallyCrossesWindingBridgeAndSurvivesColdCheckpoint(0)`

```text
Disk checkpoint: step=30; t=2.99999928; schema=5; bytes=21801; selected=134
Bridge 0: center=(57.54, 39.00); start=(45.79, 53.14); patient=(82.05, 18.33); actualDoctor=(72.92, 27.10); heals=383
```

### `ExplicitHealPhysicallyCrossesWindingBridgeAndSurvivesColdCheckpoint(1)`

```text
Disk checkpoint: step=38; t=3.79999852; schema=5; bytes=22005; selected=134
Bridge 1: center=(61.81, 89.00); start=(48.61, 71.45); patient=(82.56, 114.69); actualDoctor=(74.79, 104.80); heals=383
```

### `PaidWindingTransportCleansHealingAndColdDisembarkRestoresEligibility(False)`

```text
Ordinary sail: requestedBerth=(46.57, 30.96); formationSlot=(51.07, 26.46); finalGoal=(51.07, 26.46); actualShip=(51.07, 26.46)
Real Embark: requestedShore=(42.00, 33.00); actualShore=(43.00, 33.00); actualBerth=(46.66, 31.40); preEntryShip=(51.07, 26.46)
Embarked: id=137; host=141; step=1263; t=126.298645; hp=76.5; expectedQueues=2
Disk checkpoint: step=1263; t=126.298645; schema=5; bytes=25481; selected=137
Paid transport: enteringHealer=False; ship=141; shore=(42.00, 33.00); landing=(60.92, 101.78); entrant=137; population=7; cold step=1808
```

### `PaidWindingTransportCleansHealingAndColdDisembarkRestoresEligibility(True)`

```text
Ordinary sail: requestedBerth=(46.57, 30.96); formationSlot=(51.07, 26.46); finalGoal=(51.07, 26.46); actualShip=(51.07, 26.46)
Real Embark: requestedShore=(42.00, 33.00); actualShore=(43.00, 33.00); actualBerth=(46.66, 31.40); preEntryShip=(51.07, 26.46)
Embarked: id=134; host=141; step=1263; t=126.298645; hp=31.0000019; expectedQueues=2
Disk checkpoint: step=1263; t=126.298645; schema=5; bytes=25377; selected=134
Paid transport: enteringHealer=True; ship=141; shore=(42.00, 33.00); landing=(60.92, 101.78); entrant=134; population=7; cold step=1812
```

### `WindingRailingBlocksNearStationaryCareUntilOrdinaryDetour(0)`

```text
Railing 0: bodyPoints=(66.79, 35.91)/(69.37, 38.97); local=(9.07, 3.60)/(9.07, 7.60); firstHeal=6.699996; actualDoctor=(76.81, 33.11)
```

### `WindingRailingBlocksNearStationaryCareUntilOrdinaryDetour(1)`

```text
Railing 1: bodyPoints=(65.31, 99.06)/(61.81, 101.88); local=(10.02, 3.60)/(10.02, 8.10); firstHeal=6.499996; actualDoctor=(68.28, 108.88)
Saving results to: /Users/Apple_501/Library/Application Support/Frontier Workshop/Frontier Reckoning/TestResults.xml
```

## Original attempt history remains intact

- River1: exit 2, 6 / 8 passed; both transport cases failed at original line 457, raw berth versus actual ordinary Move slot, **6.36396122m**.
- River2: exit 2, 6 / 8 passed; both transport cases failed at original line 469, preliminary `.9m` shore versus actual `.4m` passenger shore, **1m**.
- River3 binds actual ordinary Move and passenger-radius naval destinations without relaxing real movement, short boarding, container cleanup, cargo, exit or fresh-care assertions. Earlier failures are not rewritten.

## Limits

- This is an independent read/hash/source-binding and executable-assertion audit of the closed eight-case Unity EditMode run, not a separate Unity gameplay re-execution.
- These controlled fixtures retain original Winding geography and Catalog but initialize age II, 5000/5000/5000 funds, assembled peaceful land units and 15-percent wounds; they are not a normal-resource opening or complete campaign.
- The new medical geography cases cover the two authored Winding bridges/railings only, not new medical lifecycle instances on Tropical, Tributaries, Estuary or generated maps.
- Disk save/read/cold World and MatchSimulation restore occurred inside tests. Temporary slot files are deleted by the test finally block; their actual size/schema diagnostics are retained in XML, not exported native save originals.
- Matching replay digests during paired cold continuation is not a newly executed standalone replay playback or native application process restart.
- No new native human playthrough, human win/loss result, rendered FPS or performance acceptance is established. Editor elapsed durations and the owned process sample are diagnostics only.
- Candidate1 runtime b1a9b6a, immutable packages, transport delivery, private Drive and GitHub receipts remain their own earlier evidence. This audit does not replace them or claim Candidate2 Full/Build/native/cloud/GitHub delivery results.
- The observed post-test documentation-only commit is recorded separately from the exact tested 9b7a5a8 commit. Future HEAD changes are outside this audit observation.
- The earlier River1/River2 6-pass/2-fail attempts are retained unchanged; their failed preliminary berth/shore assumptions are not relabeled as passing runs.
- No expansion of Ottoman native acceptance, complete four-part river content completion or whole project goal completion is inferred.

## Artifact boundary

Full exact original/source SHA descriptors, eight testcase outputs, six temporary cold checkpoint diagnostics, retained failure descriptors, source checks and owned process-sample descriptors are in the JSON.
Only fresh external Candidate2/Diagnostics audit JSON/Markdown were written. Production, repository files, old originals, Unity, Git, Drive and GitHub were not changed.
