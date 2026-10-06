# Closed 072 Dutch long-game analysis

This is a read-only analysis of the completed 072 observer run. No Unity process was launched, no game source was changed, and no 072 original was rewritten. The JSON receipt binds the actual originals by size, SHA-256 and file identity and checks them again after analysis. All source statements refer to runtime commit `bc127e02638caaeefd2310a1a3eff858f4f4cb2f`, rather than later 073 edits in the shared working tree.

## What the existing timing does and does not show

`traders_bend070-river-42` took **3303.1905432 seconds** for the whole observer case and ended at simulation time **3600.061279296875**, without a winner. The stopwatch in `Assets/Frontier/Editor/NormalRoutes072.cs` starts before world creation and stops after setup, all Steps, 30-second sample serialization/event writing, result extraction and final world/controller serialization. The samples do not contain wall-clock timestamps. This is not an isolated simulation CPU measurement or a native frame-rate result; it cannot locate the slow interval retrospectively.

The Dutch case contains **192512 events**, **123630757 event JSONL bytes**, and **298707931 final-world bytes**. The French seed43 case takes **557.8640185 seconds**, with **148833 events** and **232099389 final-world bytes**. The exact values and ratios are in the receipt. Dutch wall time is about **5.9 times** French while serialized event/world bytes are about **1.3 times**. Larger history can contribute to serialization and allocation cost, but file size alone does not explain that difference or prove its cause. The Ottoman winding seed44 case is also slow (**955.807363 seconds for 1982.762939453125 simulated seconds**), despite a much smaller ledger. A single Dutch-specific event-size explanation is therefore weak.

The continuity plan's coin forecast scans backward only until `world.Time - 60` for each eligible Trade Post. It does **not** scan all historical events on every fixed step. A recent-history window can still cost time at repeated plan reviews, but changing it before measuring would be speculative.

## A confirmed military scheduling failure

The Dutch samples around **2405, 2705, 3006, 3306 and 3600 seconds** show both AI controllers with **`hasStage=false` and `wave=[]`**. Both are in Muster in the late samples; player0 is briefly in Defend near2405. At the declared cap player0 owns **72 land soldiers**, player1 owns **70**, and each side owns **151 surviving entities**, while **219 of the 302 owned survivors are Idle**. Neither side has an unfinished entity at the final snapshot. The300-second ledger window from3300 to3600 has **zero `AttackPrepared`, `Fire` and `Damage` events**.

`AiArmyPlan072.SelectStage` only tries25 candidate points around the Town-to-map-center direction, within12 cells of that desired point. Each candidate requires current visibility, safety, physical reachability and a completely free **7 by7** area. When every candidate fails, `hasStage` becomes false. `Review` still responds to an immediate visible raid, but otherwise returns at `if(!stageReady)return` before it assembles or dispatches any cohort. The samples prove this no-stage state occurs in the real game. They do not prove which predicate rejected every candidate, nor that this branch consumed most CPU time.

This is a separate actionable gameplay issue: dense building placement or narrow river approaches can permanently prevent normal army deployment. A later fix should search a wider reachable visible region, accept a valid formation footprint of suitable size, or maintain a bounded alternative staging policy. It must retain actual arrival, threat clearance, protected forces and legal movement commands. It should be verified against the frozen Dutch final/2400 states, rather than weakening muster to a soldier count.

## Performance candidates to measure, in order

1. **Construction/source navigation.** `TryConstructionWorker` combines a prospective post-foundation ordinary route check with another filtered route, up to6 candidates and8 approach points. Its safety predicates repeatedly inspect current threats. The source-survey path has the072 query-local threat/connectivity optimization; construction does not use that context. Measure the query counts, A* node pops and inclusive query time before optimizing.
2. **Dense ally movement recovery.** The final Dutch state has12 actors marked Route blocked; retrying and one Waiting for passage. `MovementRecovery.TryAllyRecovery` can search a512 by512 fine grid. One reserved fine-recovery slot bounds requests per fixed step, but does not bound the duration of an individual failed search. Measure coarse/fine query time and peak node expansion separately.
3. **Home and no-stage army reviews.** `Home` scans rescue workers/facilities against all world entities even when a Town already exists, then selects that Town anyway. A Town-first return could preserve results and avoid unused work, but must be tested for exact town-priority and no-town fallback semantics. Repeated failed stage selection is also a candidate; profile it without assuming the confirmed gameplay stall is the timing cause.
4. **Repair reserve surveys.** `StopRecoveryRepairs` runs before the think timer on every fixed step; if an active repair exists it performs a fresh continuity survey. The final Dutch state contains no repair order, so a final-state continuation would not measure an earlier repair burden. Use a real saved earlier branch with repairs for that claim.
5. **Observer I/O and history serialization.** Record durations of observer sampling, event flush and final World JSON export separately from Step CPU. Do not add overlapping inclusive method timers as exclusive CPU time.

These are evidence-guided hypotheses. None is certified as the principal cause of the3303-second wall duration by this read-only analysis.

## Minimal isolated profile protocol

Keep the original072 probe project and outputs immutable:

`/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.15.0/Candidate1/Diagnostics/PerfProbe072/project`

Prepare a genuinely new073 isolated project at:

`/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.16.0/Candidate1/Diagnostics/PerfProbe073/project`

Start from runtime commit `bc127e02638caaeefd2310a1a3eff858f4f4cb2f`, copy compatible instrumentation from the existing probe as explicitly recorded additions, and bind every source SHA. The existing `EditorProbe072.cs` and `PerfProbeDiagnostics072.cs` are useful templates. Launch only this independent project when no primary Unity test/build is running. Unity binary:

`/Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents/MacOS/Unity`

**Quick static query first:** use the exact4.1MB closed original:

`/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.15.0/Candidate1/NormalRoutesFinal1/matrix/traders_bend070-river-42/comparison-2400-world.json`

The existing entrypoint `Frontier.Editor.EditorProbe072.Run` accepts `FRONTIER_PERF_SNAPSHOT` and an exclusive new `FRONTIER_PERF_OUTPUT`. Record current owned workers/threats, `Home`, continuity `Evaluate`, each construction-site candidate and source survey. Measure no-tick query time, path counters, component graph builds, endpoint work and state equality before/after each operation. The comparison snapshot intentionally omits historical events and has **no exact2400 controller snapshot**; the nearest30-second sample is at2405, so it must not be treated as a matching simulation restore. Political obligation delegates and trade history must be labeled absent in a static-query profile. Do not manufacture them from the nearest sample.

**Exact controller continuation second:** prepare a separate entrypoint that restores the two actual matching closed originals:

`.../traders_bend070-river-42/final-world.json`

`.../traders_bend070-river-42/final-simulation.json`

These are exact state and controller inputs at3600.061279296875. The full World contains the actual192512-event history. Validate rules hash, world/controller time and step, compute the starting digest, then run at most250 fixed steps in the isolated project. Flush a begin row before each Step and an end row after it, so the durable label identifies a slow query. Instrument Step total, AI decision, construction, source survey, stage selection, movement coarse/fine recovery, and recent-event window scans; keep setup/deserialization, output JSON and runtime profiling durations separate. This is a diagnostic continuation beyond the old observer cap, not recorded original gameplay or a verified replay. Do not use the existing native-anchor `EditorStepProbe072.Run` directly: it requires a `ReplayAnchor` and exact recorded event prefix and will reject these normal World/controller files.

If a candidate patch is made, run baseline and patched copies from the same exact inputs and compare all final World/controller bytes or canonical digest plus all emitted event rows. Profile the original source, not a partially changed073 runtime, to explain the old072 observation. After a measured improvement, retain appropriate tests and verify fresh native performance with its own version/build identity. None of these future profile actions was executed in this analysis.

## Reproduce this read-only result

Run `python3 analyze-dutch42-performance073.py` from a new output directory containing a copy of the script. It refuses to overwrite its receipt. It streams four closed ledgers and reads the exact entities prefix of each large World rather than parsing their historical event arrays twice. The receipt includes four case/result comparisons,300-second event bins, selected exact controller samples, final entity counts/statuses, immutable-source blob hashes and original byte checks.
