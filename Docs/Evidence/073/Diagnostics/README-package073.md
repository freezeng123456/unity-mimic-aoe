# 073 local packaging helper (prepared, not executed)

`package073.py` is outside the project repository. Preparing it does not create a delivery, commit source, upload files, or declare normal/native acceptance. It uses Python standard libraries and local read-only Git commands. It does not start Unity, launch the app, use account/network tools, or extract archives.

## Declared source profile

Profile: `all-tracked-except-Docs-Evidence-073-v1`.

Read every tracked blob at the exact `--source-snapshot-commit`, except every descendant of `Docs/Evidence/`. Include all other tracked paths: root files, the complete `Assets/`, `Packages/`, `ProjectSettings/`, `Tools/` runtime inputs, all current and historical text documentation outside `Docs/Evidence/`, licenses and `SourceAssets/`. No historical 072 selected-path/screenshot list is reused. This is a complete declared source selection, not a full Git tree/history archive.

At runtime commit `519d7d34a0be85f75ee19e53458c636d1ba0b3e4`, the profile selects **1,001 tracked files / 23,920,071 bytes (22.812 MiB before compression)** and excludes **4,192 tracked paths / 3,408,302,218 bytes (3,250.410 MiB)**. Root's final documentation commit can change these counts. Every actual selected/excluded path, Git tree mode, blob ID and blob byte count is recorded; selected blobs also carry SHA-256. The full NUL-delimited Git tree metadata SHA binds the exhaustive partition. Selected files are read from committed blobs, never the working tree, `HEAD` aliases, `git archive` export attributes or an unstaged docs edit.

Preparation validation used AST parsing, CLI help and read-only profile planning; checked actual frozen source maps, the 1,611-case full XML regression, actual app version/GUID/executable permission and closed normal runner receipt; and hashed complete app/normal/native lstat trees. No ZIP writer or `package()` call was executed. A streaming level-6 raw-deflate calculation (without creating any archive) estimated **72.362 MiB app payload**, **11.163 MiB normal payload**, and **4.101 MiB Native1 payload** before ZIP headers. These are sizing estimates for the current originals, not verified final ZIP sizes or a choice to package the whole Native1 tree. ZIP write/readback remains unexecuted until authorized packaging.

The source snapshot's entire 732-file runtime map must match the pinned runtime commit, the frozen source map and complete full-test evidence exactly. Expected source fingerprint is `9b92812a922adb69a5ae501925417d8eba8e24b836f496274ebc8ec8679ba03d`. Snapshot and runtime commits can differ only with identical runtime inputs. Each source ZIP member's SHA/bytes and file/executable/link mode must match the corresponding selected Git blob; synthesized source directories have mode 0755.

All four required ZIPs must be strictly less than **100 MiB (104,857,600 bytes)**. The source size above gives ample margin. The exact app, evidence, normal and optional gallery compressed sizes are checked after creation; none is claimed in advance. Oversize output stops packaging and leaves the partial delivery for diagnosis; the tool does not split or silently omit evidence.

## Required actual inputs

Root must choose the final full lowercase source snapshot commit after canonical documentation is closed and committed. There is deliberately no final snapshot default. The repository, original tests, actual app, selected evidence and normal trees remain available and immutable for verification.

Default pinned locations:

- Repo: `/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project`.
- Source freeze: `Candidate1/Diagnostics/source-freeze073-before-full.json`.
- Full regression: repo `Builds/Tests/run-9q8yTJKS`, 1,192 EditMode + 419 PlayMode = **1,611 passing cases**. Both complete XML roots, nested suites, distinct test-case fullnames, source-evidence XML hashes and source map are checked. Logs are retained as original inputs.
- Actual build: `Candidate1/Build1/Frontier Reckoning.app`; actual closed build receipt `Candidate1/Build1/runner-receipt073.json`. Expected version `0.16.0`, actual `boot.config` build GUID `5f347ad9ec2f494984b0b49c4f896c31` and executable permission are checked against the build receipt.

The caller must supply:

- `--evidence-directory`: the exact complete root-selected/staged 073 evidence tree. It must contain byte/SHA-identical copies of the source freeze, full-test source evidence, both XMLs and both logs, closed build receipt and supplied closed normal/native receipt. The helper accepts any faithful subfolder layout by matching exact bytes/SHA. App executable/Info.plist/boot.config bytes are in `macOS.zip`; normal originals are in `normal.zip`.
- `--normal-directory`: the complete actual normal runner tree, including `matrix/manifest.json` and all original case/log/receipt files. Do not substitute a summary tree for the runner originals.
- `--normal-receipt`: an actual finalized source-bound receipt located within that normal tree. It needs a real `finishedUtc` (or `finishedAtUtc`), a closed status, matching fingerprint fields and, if it contains a process PID, an actual exit code. Source stability must be true when present. The helper preserves status and matrix case count verbatim; it does not turn two natural terminals plus one observation cap into all wins, all routes passed or balance acceptance.
- `--output-directory`: a new, nonexistent delivery directory. Its parent must already exist. It must be outside the repo and every input tree. Existing files/directories are never overwritten or deleted.

Optional: `--native-gallery-directory` preserves an exact selected gallery tree as `nativegallery.zip`. It requires `--native-receipt`, pointing to root's actual finalized source-bound native scope audit/receipt. The receipt must have a closed status, actual finish timestamp and the source fingerprint at top level (or matching before/after fields). A raw native process receipt which only says `closed` but has no source map is insufficient for this identity binding. Root can supply the actual bounded scope audit rather than editing the raw process receipt. Native process closure and gallery preservation do not imply that the unobserved exchange GUI worked; the actual limited market construction/payment/completion scope and `noWindowsAvailable` gap remain in the source documents/evidence.

All filesystem archives preserve the full selected lstat tree, including empty directories, file Unix modes and relative symlink text. The helper rejects dangling/escaping/cyclic symlinks, case/Unicode aliases, Git/cache directories, common credential/private-key filenames and special device/socket nodes. This filename rule is not a content-level secret audit; the caller selects suitable public delivery inputs. No files are silently skipped in a supplied tree. ZIP timestamps are normalized to 1980-01-01; ACLs, xattrs, resource forks and notarization are not encoded.

## Invocation after root closes the actual source/evidence

The following are examples with placeholders, **not commands executed during preparation**. Replace `ROOT_FINAL_SOURCE_COMMIT` with the root-selected exact 40/64-character commit. Never use that literal token as a receipt or guess a future commit.

Read-only source profile check (no ZIPs, no output directory):

```sh
python3 -B '/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.16.0/Candidate1/Diagnostics/package073.py' \
  --plan \
  --source-snapshot-commit ROOT_FINAL_SOURCE_COMMIT
```

Local packaging, after all actual inputs above are closed and staged:

```sh
python3 -B '/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.16.0/Candidate1/Diagnostics/package073.py' \
  --package \
  --source-snapshot-commit ROOT_FINAL_SOURCE_COMMIT \
  --evidence-directory '/absolute/root-selected/Evidence073' \
  --normal-directory '/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.16.0/Candidate1/NormalObserved1' \
  --normal-receipt '/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.16.0/Candidate1/NormalObserved1/runner-receipt073.json' \
  --output-directory '/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.16.0/Candidate1/DeliveryFinal1'
```

If using the optional gallery, additionally pass `--native-gallery-directory '/absolute/exact-selected-native-gallery' --native-receipt '/absolute/actual-closed-native-scope-audit073.json'`. Copy that actual audit into the selected evidence tree too. Defaults for repo/source freeze/tests/app/build receipt can be overridden with explicit paths.

Outputs after successful packaging:

- `source.zip`: selected committed source under `source/`.
- `macOS.zip`: original actual app under `Frontier Reckoning.app/`.
- `evidence.zip`: complete selected evidence under `Evidence073/`.
- `normal.zip`: complete actual runner originals under `Normal073/`.
- Optional `nativegallery.zip`: complete selected gallery under `NativeGallery073/`.
- `source-map073.json`: complete 732-file runtime map with snapshot/runtime identities.
- `actual-app-manifest073.json`: actual app identity and every member's SHA/bytes/type/Unix mode/link target.
- `package-manifest073.json`: exhaustive source selected/excluded partition, full input tree inventories, pinned input hashes, ZIP SHA/bytes, exact source/normal/native status and explicit scope limits. `local_archives_verified` means archive verification only.

Each writer's output is independently reopened. The verifier checks CRC, exact member set without duplicates, bytes/SHA for every member, Unix type/mode (including executable flags), directory flags and symlink text. It then rereads the entire frozen input trees and pinned originals, regenerates the Git blob map and rechecks each ZIP against these fresh records. No archive is extracted and no executable is run.

Independent verification can be rerun after packaging without rewriting any artifact; the original Git objects and absolute original input trees must still be available and unchanged:

```sh
python3 -B '/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.16.0/Candidate1/Diagnostics/package073.py' \
  --verify \
  --manifest '/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.16.0/Candidate1/DeliveryFinal1/package-manifest073.json' \
  --manifest-sha256 ACTUAL_REVIEWED_MANIFEST_SHA256
```

`--manifest-sha256` optionally binds the verification to an independently reviewed manifest hash. The verifier compares standalone source/app manifests too. Failure exits 2; no success manifest is written by a failed package operation. Preparing and statically checking this tool does not prove a future final package, normal/native acceptance, upload, signing, notarization or remote byte identity.
