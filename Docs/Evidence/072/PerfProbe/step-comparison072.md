# Independent isolated step comparison 072

Status: passed; 285 checks, 0 failures.

Both runs must actually complete 250 steps from the same original anchor and recorded history. Final world/controller JSON, all emitted event JSONL bytes, final state digest, and per-step observable transitions were independently compared. No raw files were changed.

Baseline gross step time: 565254.4585 ms; maximum 72990.4258 ms; 11 steps over 500 ms. Optimized gross: 3918.2488 ms; maximum 276.0642 ms; 0 steps over 500 ms. Measured gross ratio: 144.2620. Inclusive method timers overlap.

Original and optimized final state digest: `59d3818268c962b1584cc4d9530168778ad45ca38604c60f7c5828aca9b4d75f` / `59d3818268c962b1584cc4d9530168778ad45ca38604c60f7c5828aca9b4d75f`. Full event counts: 62633 / 62633.

This is isolated instrumented Editor evidence. Compile/cache state and concurrent CPU affect wall time; it does not certify release frame rate, full regressions, native GUI or all normal routes. See the JSON for every source input hash, check, actual metric and failed assertion.
