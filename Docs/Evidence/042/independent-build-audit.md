# 042 independent build audit

This is a read-only audit of the 0.2.19 build evidence. No Unity or GUI was started.

## Results

- `EditMode042.xml`: **145/145 passed**, 0 failed, 0 skipped, 0 inconclusive.
- `PlayMode042-final.xml`: **159/159 passed**, 0 failed, 0 skipped, 0 inconclusive.
- The earlier `PlayMode042.xml` is **158/158 passed**; the final report is the later 159-case run and is the applicable final count.
- `build-receipt.json` identifies version **0.2.19** and records a successful macOS build of 172,892,708 bytes.
- The 257 receipt input hashes were recomputed against the current `outputs/FrontierReckoning` tree: **257/257 present and matching; 0 missing; 0 mismatched**.
- `immediately-before-build-inputs.json` contains the same 257 entries and values as the receipt input map: **exact match**.
- The delivered executable at `outputs/Clients-0.2.19/Frontier Reckoning.app/Contents/MacOS/Frontier Reckoning` matches the receipt executable SHA-256 `0d8d00cc0fbf2b4c5852ac61bff2c582004f9e9cf20d0f9290014be668215527`.
- The delivered managed runtime at `outputs/Clients-0.2.19/Frontier Reckoning.app/Contents/Resources/Data/Managed/Frontier.Runtime.dll` matches the receipt runtime hash `6f34e6cbf657cd0988e301982689fdbf1c409601b2746888175fba93e6abc60f`.
- `outputs/Clients-0.2.19` therefore has the same executable and runtime identities recorded by the receipt; the receipt's `work/verification/042/client` path is the build staging path.
- `Info.plist` reports `CFBundleShortVersionString=0.2.19` and `CFBundleExecutable=Frontier Reckoning`. `CFBundleVersion=0` is the Unity bundle build number and is not the product version field used by the receipt.

No source/build identity inconsistency was found in the checked evidence. The build log contains `FRONTIER_BUILD Succeeded` for the same receipt app path.

## Scope note on camera changes

The 042 input set includes `CameraPanInput.cs`, its EditMode tests, the RtsInput integration, the HUD camera-edge protection and the capture camera metadata. The hash comparison confirms those files were the inputs used for the recorded build. The 145/145 and 159/159 results establish test-suite pass status, not a claim that every native focus/Retina/window configuration has been independently proven.

The audit is intentionally limited to build identity, version metadata, and the recorded XML results. It makes no independent claim about the internal coverage of `ExperienceBountyTests` or exact original-game XP parity.
