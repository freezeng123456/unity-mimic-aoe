# 0.10.0 final test evidence

`run-q8grIRWM` completed with **1097 / 1097 Passed**: 740 EditMode and 357 PlayMode, zero failed, skipped or inconclusive cases. XML durations were 205.2043782 s and 164.4922186 s respectively.

- Runtime commit: `2668c47a426f2cea8d4bc7b309cdbfa71533cc9d`.
- Source fingerprint: `8180b02b55ed3957b94042d779a6d74bdda78cd46073cf70d93353c330363466`; all 595 source inputs independently matched the saved manifest and current files.
- Actual new native-layout regression: `Frontier.Tests.Replay067IntegrationTests.TimelineFillAndHandleStayInsideTheRenderedTrackAndDraggingStillSeeks`, Passed in 0.886252 s. It checks timeline fill/handle containment and actual pointer dragging still seeks.
- Replay groups: 10 behavioral EditMode, 20 storage EditMode and 5 controller/UI PlayMode tests, all Passed; these groups are included in the suite totals.

[EditMode.xml](EditMode.xml), [PlayMode.xml](PlayMode.xml) and [source-evidence.json](source-evidence.json) are copied byte for byte. [archive-receipt.json](archive-receipt.json) records original/archived byte counts and SHA-256 values. The two `.log` files replace only the values of anchored licensing `Machine Id` / `Serial number` fields with `[REDACTED_DEVICE_ID]`; exact changed line numbers and the regex are in the receipt. All other log bytes, including diagnostics and exit output, are retained. Raw logs remain local in the source run directory.

The prior 1096-test candidate and its different fingerprint remain in [Diagnostics/FrozenCandidate1/Tests](../Diagnostics/FrozenCandidate1/Tests/README.md). These automated results do not by themselves certify final native rendering, full replay playback, performance, packaging or upload.
