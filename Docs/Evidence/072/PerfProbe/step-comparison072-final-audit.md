# 072 isolated replay optimization: final independent audit

The closed original and optimized runs both completed 250 normal fixed steps from the actual NativeA step-14700 anchor and the same full recorded history. The comparison passed 285 checks with zero failures; this seal audit passed 63 checks with zero failures. Final world JSON, controller JSON and every emitted event JSONL byte are identical, as are all 250 observable step transitions. Final digest: `59d3818268c962b1584cc4d9530168778ad45ca38604c60f7c5828aca9b4d75f`; event count 61,290 → 62,633 (1,343 actual new events).

Gross diagnostic step time fell from 565254.4585 ms to 3918.2488 ms, a measured 144.26× ratio. The maximum step fell from 72990.4258 ms to 276.0642 ms; steps above 500 ms fell from 11 to 0. Median step time was 1.69485 ms versus 1.62105 ms.

Filtered FindPath calls fell from 6,029 to 219; popped nodes from 118,093,662 to 317,334; EconomicThreat checks from 9,415,813,950 to 46,853. The optimized run built 46 query-local connectivity graphs. Positive preflight results still use original FindPath and endpoint checks. The measured native stall mechanism was repeated safety-filtered path failure, rather than a rendering claim.

The audit rehashed both instrumentation manifests, archived original instrumented source, current copied/main source seals, the unchanged step harness, original anchor/prefix and all ten raw run files. Every captured hash remained stable. Raw baseline and its historical preparation manifest remain unchanged.

Root reported the separate main full test `run-HACRyTBF` running concurrently with the optimized isolated probe. CPU contention and instrumentation affect timings; this audit did not measure concurrent CPU attribution. Nested inclusive times cannot be summed. This result does not certify release frame rate, final native GUI, complete normal-route matrix or final release acceptance; those remain pending.
