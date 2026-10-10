# 077 Bank1 and mutation independent audit

**PASS: Bank1 actual 17 EditMode + 2 PlayMode pass. Two deliberate single-case negative controls fail with exit 2 and are restored.** No Unity re-execution.

- Historical patched source: **782 inputs**, FP `fdd60bfa136f944cee07e75b157bf8143a7ca63da11171d517feafb57a56716d`; receipt and verification inventories independently hash-identical.
- Bank1 base Git HEAD `09e07a40fb0008cdb69719336a674cb228129244` is not a committed 077 source tree; exact inventory plus reviewed nine-path patch binds the run.
- Live source observed: **784 inputs**, FP `3fcc6a6d9a31e8a49e0d5c34f711d9c3d771c4f413d4f3642a8391ee49daac4a`; same as Bank1: **false**. Differences: `['Assets/InitTestScenee502fa07-1efd-4906-9380-78a5290462b7.unity', 'Assets/InitTestScenee502fa07-1efd-4906-9380-78a5290462b7.unity.meta']`.
- Audit JSON: **33734B**, SHA256 `96d96be3186fe00fec19dcbd0294af5876e21a0734daafbc89b0c7452040613b`.

The two observed live extras are only `InitTestScene*.unity` and `.meta` while a separate compatibility run is active. All historical782 input hashes remain unchanged. They are classified as transient Editor-test-scene candidates, not integrated render-proposal source; the audit did not wait for or operate cleanup.

## Actual positive-run observations

- Dense Winding probes: 58,478; peak sampled grade0.3542763 under existing .4 physical slope contract.
- Unchanged original control/fish/start/terrain hashes, protected bridge/port/town pads, bed/water/inland heights and seven other historical layouts pass.
- Restored World consumer physically moves 212 steps, including40 changed-bank steps; warm/cold state agrees using the same cached layout.
- Actual generated mesh:16,641 vertices;436 meaningfully changed sand vertices. Literal west-inner sand mean0.7177923 (5 points), east-outer0.07755104 (4 points).

| Negative control | Exact observed failure | Restored source |
|---|---|---|
| reversed-side / EditMode | west InnerBend0, expected>0.75; RiverBankAsymmetry077Tests.cs:202 | Original SHA and full FP agree |
| disconnected-mesh-colors / PlayMode | vertex47 sand0.0067351460456848145 versus expected0.024863243103027344 ±1e-6; RiverBankMesh077Tests.cs:36 | Original SHA and full FP agree |

Both mutated byte hashes and complete altered inventories were independently reconstructed from retained original source plus the exact mutation formula. XML/fullname/checksum/messages match each original receipt. The two failed controls are not added to the19 passing tests.

## Limits

- PASS is an independent evidence/hash/provenance audit, not another Unity execution. Only Bank1 actual 17 EditMode + 2 PlayMode cases are unmodified-source passes.
- The two exit-2 single-case failures are deliberate negative controls that establish sensitivity to reversed inside/outside query sign and disconnected actual mesh colors. They remain failed originals, not passing production cases.
- Bank1 ran a patched working tree: gitHeadBefore names the pre-existing base commit, not a committed 077 tree. Its exact 782 input hashes, integration patch receipt and source-evidence fingerprint are the tested identity.
- Mutation altered-source fingerprints are independently reconstructed from the Bank1 inventory plus exact retained original/replacement bytes; the audit does not execute the mutator. Restoration receipts and affected live file hashes agree with the original tested source.
- Later live-source additions/changes, if any, are listed separately; they do not retrospectively inherit Bank1. No clean-working-tree or latest-HEAD acceptance is inferred.
- Dense slope scanning used zero-radius legal points; actual restored World/MatchSimulation consumer movement separately checked actual Explorer body/swept segments and the same cached MapLayout. This is not independent fresh map reconstruction or native disk reload.
- The two PlayMode cases inspect real generated sharedMesh vertices/colors/heights and independent literal west/east contrast; they are not rendered PNG, native visual quality, FPS or user playtest acceptance.
- Only Winding receives the new asymmetry; the other seven river layout instances establish historical height/layer preservation, not new morphology on those maps.
- Compatibility1 is a separate run and was not audited or relabeled here. No full regression, new app build, cloud upload, broader river lifecycle or whole-goal completion is asserted.
- Only the fresh external Diagnostics JSON/Markdown are written. No source/repo edits, Git commands, Unity execution/process intervention or cloud operations were performed.

## Original hashes

| Artifact | Bytes | SHA256 |
|---|---:|---|
| `Bank1/EditMode.xml` | 16759 | `588cb5dca81830046bc8f02d7b1fa066df3211c5c6191c33662fd131f09419a8` |
| `Bank1/PlayMode.xml` | 4295 | `9ad53fcb2c42ab566cfa239a99760dd993d3be323314f2f3d3b17950144584ff` |
| `Bank1/runner-receipt077.json` | 103455 | `5576cf398fdd532f19173437dc9d888b4b97b19a1286d9efa2f9070c5a273bf3` |
| `Bank1/source-evidence.json` | 100684 | `2e45cc0e32037d66b57b439ff78a5b96a88a7b8f5a29297afb791ea3af2b9779` |
| `disconnected-mesh-colors/PlayMode.xml` | 4540 | `b1409e2e8c4c3c8fc64607142cb88f52a79e690660835871f36b520e21b92447` |
| `disconnected-mesh-colors/original.txt` | 19833 | `e1d6260436a3ee45a926f0139aaa98af3c0e25cacdc26ec2ed474f49cd7b2786` |
| `Mutations1/mutation-receipt077.json` | 5059 | `0cc65ca9a852748e10fbe1bbf16d3595c46486d9ea0756475eaaf76ad24d7834` |
| `reversed-side/EditMode.xml` | 4623 | `419d4c0502e8c981795896a7d6803c4bca87ed8820e07562c4ee883733ba7667` |
| `reversed-side/original.txt` | 6205 | `23e2b47d6fa4572535dfc008b42309e4f1231d7cf8f9c78352738b48026f466f` |
