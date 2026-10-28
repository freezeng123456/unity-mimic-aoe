# 074 projection guard: independent static review

No blocking issue found in the settled guard and regression test. This supplements the preserved initial audit; the earlier Full1 result belongs to runtime 4344edacffad01bfc8d49ff90cb8361534f7702d and fingerprint b72d96ee8ef5018ae7c2543c552117bb3e9370f525fcc2d97470c1ce9aeacdb9, and does not validate the changed source.

AiGatherRoutes074.CanGather computes the ordinary World.WorkPoint once when the existing edge distance exceeds 1.35, constructs a separate arrival probe and requires World.EdgeDistance(arrival, source) <= 1.5 before ordinary route admission. Economy uses the same inclusive extraction threshold. Nearby workers, eligibility, cached path/detour checks and synchronous decision-local caching remain unchanged. The probe is not registered in World and does not replace the live worker position, command authority, movement or save data.

The added integration test demonstrates a safe reachable projection with real manual movement but zero credited income; it then verifies helper/continuity/workforce rejection and restored real income after clearing the declared obstacle. This tests observable behavior instead of copying the new predicate.

No Unity, build, test runner, observer, commit, upload or repository edit was performed for this review. Focused3 and the corrected source's full regression must close independently. Admission does not guarantee continuous safety between reviews, balance or performance. Exact reviewed file hashes are in the companion JSON.
