# 0.2.32 menu-only release equivalence

This directory prepares a strictly bounded release check. No 0.2.32 source edit, Unity run, build, or native acceptance has occurred as part of preparing the tool. Controlled tests below are tests of the verifier, not native proof.

The frozen 0.2.31 baseline is commit `50d303b76519a3a9b448c27cd2cdeb22779a1e5d`, source fingerprint `1b45f97f3d90c9b76388419492a83c5622ccb2cf3a365030044403ccd309c1c7`, native GUID `128a731d440e4b7a94abfcc74b88fa97`. All 357 source hashes in [055 source evidence](../055/Tests/source-evidence.json) match that commit. Source coverage is exactly `Assets`, `Packages`, `ProjectSettings`, and `Tools`, using the same hidden-file and `__pycache__` exclusions as the existing evidence tool; untracked ordinary files are included in the current inventory.

Only the following exact UTF-8 byte replacements are approved. Each old target must occur exactly once. C# `\n` below is a literal backslash and `n`, not a physical source line break.

| File | Old bytes | New bytes |
| --- | --- | --- |
| `Assets/Frontier/Presentation/FrontierHud.cs` | `"FIVE-ERA LAND GAME · DEVELOPMENT"` | `"FIVE-ERA LAND GAME"` |
| Same HUD file | `"Two civilizations · Custom decks · Original project rules\nTrade, treasures and final audiovisual quality are still in development.\nIndependent project. Not affiliated with Age of Empires or its publishers."` | `"Two civilizations · Custom decks · Original project rules\nBanks · Trade routes · Guarded treasures · Original visuals and audio\nIndependent project. Not affiliated with Age of Empires or its publishers."` |
| `ProjectSettings/ProjectSettings.asset` | `  bundleVersion: 0.2.31` plus its existing newline | `  bundleVersion: 0.2.32` plus the same newline |

The old `BuildTools.cs` has no version literal. It must remain byte-for-byte unchanged. There are exactly two changed files and 355 unchanged files. The checker rejects extra HUD logic, changed UI geometry, whitespace drift, resource/AI/rule changes, changed build scripts, any additional or removed source file, or any other unapproved byte change. It has no option to widen this allow list.

Root applies these replacements only after the 0.2.31 B/C/D evidence is complete. Root then runs the existing full test command on the frozen new source, archives its fresh `EditMode.xml`, `PlayMode.xml`, logs and `source-evidence.json` under `056/Tests`, builds the new client, and writes a separate `056/build-receipt.json`. The new receipt must contain the new source fingerprint, version 0.2.32, nonzero new native GUID, and actual `compiledRuntimeSha256` entries for `Contents/Info.plist`, `Contents/MacOS/Frontier Reckoning`, and `Contents/Resources/Data/Managed/Frontier.Runtime.dll`.

The verifier checks all 357 old hashes against the old Git commit, the exact new bytes, the entire current source inventory against the new test evidence, and both XML hashes. The old source-evidence document SHA is fixed, so its XML hashes, original timestamps and test identities cannot silently change. Its old receipt snapshot is likewise bound to its original SHA and the archived bundle-file hashes; later verification prose may change in the separate current 055 receipt while its build identity/runtime stays the same. The verifier requires the same 214 EditMode and 210 PlayMode unique test identities, all individually Passed without skipped/inconclusive tests. New XML must have different bytes and newer run timestamps than the old XML. These recorded timestamps and source evidence document root's fresh run; the checker does not independently observe how Unity was launched.

It reads the actual new app's plist and `boot.config` to bind version and GUID to the separate receipt, verifies every compiled-file hash listed in that receipt, and rejects reuse of the old runtime DLL. Its GUID must differ from the old native GUID. Old 055 JSON records retain their recorded version/GUID, including the separately preserved 0.2.30 attempts. Source and HEAD are re-read at the end to reject drift during the audit.

Run controlled verifier tests before release:

```sh
python3 -m unittest discover -s Docs/Evidence/056 -p 'test_verify_release_equivalence.py' -v
```

After root's fresh tests and build, run from the project root:

```sh
python3 Docs/Evidence/056/verify_release_equivalence.py \
  --app 'Builds/macOS/Frontier Reckoning.app' \
  --output Docs/Evidence/056/release-equivalence.json
```

The command exits nonzero and saves a failed result if any required new evidence is absent or any check fails. Output is restricted to a `release-equivalence*.json` result directly in the new evidence directory; unsafe output paths are rejected without writing, including failure cases. It never modifies source, receipts, or old evidence. Root separately archives the new menu/start/exit smoke and the distribution ZIP manifest. Exact source continuity lets the release refer to prior 0.2.31 gameplay/feature observations; it does not turn their GUIDs into 0.2.32 observations, establish new native gameplay/performance/audio acceptance, or prove the new menu layout was rendered correctly. The release description should say “0.2.31 acceptance evidence retained; 0.2.32 menu/version-only delta separately verified,” and link both sets of receipts.
