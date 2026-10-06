# 074 compatibility variant 2 — prepared, not executed

Use package074-2.py for the final runtime, not the preserved package074.py defaults. No packaging, Unity, project edit, historical-evidence mutation or upload was performed in preparing this variant. Full2 was still running at review; expected counts are not results.

The real run074.py schema records gitHeadBefore, complete sourceFilesBefore, computed before/after fingerprints, sourceStable, changedSources and actual closure fields. It never records a complete sourceFilesAfter map or app path. Variant 2 binds those actual fields: exact runner schema/kind/completed status, final runtime gitHeadBefore, complete before map equal the Git/full-test/current runtime map, matching mandatory before/after fingerprints, empty changedSources, exitCode 0 and real finishedUtc/sourceStable. It preserves the absence of the historical after map rather than fabricating it. Full current runtime maps and committed source maps are re-read before/after packaging independently; full test source-evidence records its complete map. A future recorded after map is checked if present.

The app comes from the CLI build directory and complete actual app tree. Actual Info.plist version, boot.config text/GUID and executable permissions must match receipt buildVersion, bootConfig and mainExecutableMode. A receipt app field, if present in a future receipt, must also match. Existing receipts are never amended.

Creation after Full2, build, normal observation and source snapshot all close (replace ACTUAL_SOURCE_SNAPSHOT_COMMIT and ACTUAL_BUILD_GUID; DeliveryFinal1 must not exist):

```sh
python3 /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.17.0/Candidate1/Diagnostics/package074-2.py --package \
  --repo /Users/Apple_501/Documents/Codex/work/frontier-reckoning/project \
  --runtime-commit ba0b57336c46d72738b71b59af923a149aff467d \
  --source-fingerprint 61d00af23e00d960dd040ef55bdfb0aa269b15745be1d1a2f0faec46377d6698 \
  --source-input-count 740 \
  --app-version 0.17.0 \
  --source-snapshot-commit ACTUAL_SOURCE_SNAPSHOT_COMMIT \
  --test-directory /Users/Apple_501/Documents/Codex/work/frontier-reckoning/project/Builds/Tests/run-xijamd2m \
  --test-receipt /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.17.0/Candidate1/Full2/runner-receipt074.json \
  --build-directory /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.17.0/Candidate1/Build1 \
  --build-receipt /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.17.0/Candidate1/Build1/runner-receipt074.json \
  --build-guid ACTUAL_BUILD_GUID \
  --normal-directory /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.17.0/Candidate1/NormalObserved1 \
  --normal-receipt /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.17.0/Candidate1/NormalObserved1/runner-receipt074.json \
  --evidence-directory /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.17.0/Candidate1/EvidenceFinal1 \
  --output-directory /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.17.0/Candidate1/DeliveryFinal1
```

Full2's actual wrapper-output.log identifies run-xijamd2m. Confirm its closed reportsDirectory before creation. Counts derive from closed XML and its source-evidence; after actual closure, --expected-edit-count and --expected-play-count can additionally pin independently reviewed counts. The predicted 1227 + 419 total is not assumed to have passed.

EvidenceFinal1 must include byte-identical copies of these nine supplied originals (subdirectory names are free; equality uses bytes and SHA):

- run-xijamd2m/source-evidence.json, EditMode.xml, PlayMode.xml, EditMode.log, PlayMode.log;
- Full2/runner-receipt074.json;
- Build1/runner-receipt074.json;
- NormalObserved1/runner-receipt074.json;
- NormalObserved1/matrix/manifest.json.

If --source-freeze is provided, copy that exact full-map JSON too. The declaration JSON is not automatically a compatible source-freeze document: --source-freeze requires files and sourceFingerprint. Recommended selected evidence also includes Full2/wrapper-output.log, build.log and its wrapper-output.log, normal.log and its wrapper-output.log, predeclared074.json, focused/red and initial results clearly labelled with their own source identity, validators, audit reports, and the actual helper/diff/usage. These recommended items are not separate helper prerequisites.

Actual app Info.plist/boot.config/executable are carried in the complete macOS app ZIP and do not need duplicate EvidenceFinal1 copies. The normal ZIP captures the entire unchanged NormalObserved1 root, including runner, logs, matrix and case data, not only matrix/. The source ZIP reads committed Git tree bytes, excludes all Docs/Evidence, records and hashes all selected/excluded blobs and preserves selected Git modes. Four fresh ZIPs must each be strictly below 100 MiB, with fresh exhaustive EOF/CRC/member-byte/SHA/mode/link readback and fresh unchanged input checks. No native, commercial release, signing, installability, performance, balance or remote-delivery assertion is inferred.

Offline read-only verification on the delivered files, with the same final identity overrides:

```sh
python3 /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.17.0/Candidate1/Diagnostics/package074-2.py --verify \
  --runtime-commit ba0b57336c46d72738b71b59af923a149aff467d \
  --source-fingerprint 61d00af23e00d960dd040ef55bdfb0aa269b15745be1d1a2f0faec46377d6698 \
  --source-input-count 740 \
  --app-version 0.17.0 \
  --build-guid ACTUAL_BUILD_GUID \
  --manifest /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.17.0/Candidate1/DeliveryFinal1/package-manifest074.json \
  --manifest-sha256 ACTUAL_REVIEWED_MANIFEST_SHA256
```

Verification reads only the existing manifest, source-map and actual-app-manifest sidecars, and four ZIPs; no original repo/input-tree reads, Git, Unity or writes occur. Source path/maps embedded in those documents are metadata, not paths to open. The external reviewed manifest SHA pins the scope of the recorded creation proof; offline integrity does not freshly verify historical excluded Git bytes or original receipts.

Static stop conditions: Full2/build/normal unclosed, wrong runtime or missing source maps/closure fields, missing evidence copies, old defaults used without overrides, a source snapshot equal to the runtime or containing changed runtime inputs, build GUID/version/mode/boot mismatch, current tree changes, unsafe filesystem members/links, generated/cache/key paths, existing or overlapping output path, and any archive reaching 100 MiB. Before/after inventories of all excluded historical Git blobs add appreciable I/O. Any failed output stays diagnostic; use a fresh output directory for a new attempt.
