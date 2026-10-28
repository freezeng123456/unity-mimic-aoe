# 074 package helper — preparation only

`package074.py` has been prepared and syntax-checked. No ZIP creation, Unity invocation, commit, upload or app launch has been performed by this preparation. Run creation only after the root closes the actual full regression, build and declared ordinary observation, selects its immutable evidence tree and commits the source snapshot.

Defaults pin the approved runtime commit `4344edacffad01bfc8d49ff90cb8361534f7702d`, version `0.17.0`, 740 source inputs and fingerprint `b72d96ee8ef5018ae7c2543c552117bb3e9370f525fcc2d97470c1ce9aeacdb9`. Actual full-test counts are derived from XML and the source-evidence map; no expected test count is treated as a passing result. The build GUID is read from the actual app boot.config and matched to the actual closed build receipt. Supply --build-guid to additionally bind the independently reviewed GUID.

These defaults identify the initial preparation. If the runtime changes, supply `--runtime-commit ACTUAL_TESTED_RUNTIME_COMMIT --source-fingerprint ACTUAL_FULL_SOURCE_FINGERPRINT --source-input-count ACTUAL_INPUT_COUNT` in **both** creation and offline-verification commands. The helper applies these values before all validations and records them in the new manifest; it never relabels an earlier test run as the corrected source. Use the new source's closed XML/source-evidence reports and actual build/observation receipts. No rerun total is accepted merely because it is expected.

Creation (replace the three ACTUAL values with closed evidence; the output directory must not exist):

```sh
python3 /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.17.0/Candidate1/Diagnostics/package074.py --package \
  --source-snapshot-commit ACTUAL_SOURCE_SNAPSHOT_COMMIT \
  --test-directory ACTUAL_TEST_DIRECTORY \
  --build-directory /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.17.0/Candidate1/Build1 \
  --build-guid ACTUAL_BUILD_GUID \
  --evidence-directory /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.17.0/Candidate1/EvidenceFinal1 \
  --normal-directory /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.17.0/Candidate1/NormalObserved1 \
  --output-directory /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.17.0/Candidate1/DeliveryFinal1
```

Optional arguments: --repo, --source-freeze, --test-receipt, --build-receipt, --normal-receipt, --expected-edit-count, --expected-play-count. Build and normal receipts default to `runner-receipt074.json` inside their supplied directory. Receipts must have an actual finish timestamp, exitCode 0, sourceStable true, matching runtimeCommit and matching source fingerprint fields. Ordinary observation additionally requires preparationRun false and caseInterventions false. Selected evidence must contain byte-identical copies of the supplied report/source-map/log/receipt/normal-manifest originals; app binaries belong in the app ZIP.

The source profile includes every tracked path except the entire Docs/Evidence directory and records the complete selected/excluded Git path, blob address, size, SHA and mode partition. Every Git blob, including excluded evidence, is streamed and hashed; this can take appreciable I/O. Runtime blobs must equal the full test map and the current working-tree map. Fresh before/after inventories detect source and filesystem changes. The exact complete app, evidence and normal directory trees include empty directories and relative symlink payloads. Unsupported nodes, unsafe aliases, escaping links, caches and credential-key paths stop creation.

Outputs are source.zip, macOS.zip, evidence.zip and normal.zip, plus package-manifest074.json, source-map074.json and actual-app-manifest074.json. Each closed ZIP must be strictly below 100 MiB. Fresh readback checks its real EOF, full member set, CRC, streamed bytes/SHA, Unix permissions/type and symlink target; selected source members are also checked against their Git blob addresses. Any failure stops with exit 2 or an uncaught nonzero exception and preserves the failed output for diagnosis. Start a new output path for a later attempt; do not overwrite or relabel it.

Offline verification reads only the existing manifest, two sidecars and four ZIPs, without Git, Unity, input-tree reads or writes:

```sh
python3 /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.17.0/Candidate1/Diagnostics/package074.py --verify \
  --manifest /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.17.0/Candidate1/DeliveryFinal1/package-manifest074.json \
  --manifest-sha256 ACTUAL_REVIEWED_MANIFEST_SHA256
```

The external manifest SHA prevents substituting a different self-consistent manifest. Offline verification proves delivered archive integrity; it does not freshly re-read excluded Git blobs or claim gameplay, native, balance, performance, release signing, installability or remote delivery. Timestamps are normalized; Unix mode/type/symlinks are preserved. ACLs, xattrs, resource forks, hard-link relationships, signing and notarization are not represented. Runtime and documentation/source snapshot commits remain separate; no document claims its own final commit hash or manifest hash.
