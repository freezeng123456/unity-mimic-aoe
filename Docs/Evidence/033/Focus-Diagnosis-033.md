# Focus diagnosis 033

## Evidence

The raw [FocusTrace.log](FocusTrace.log) is from the 0.2.11 native client. It identifies the first pointer sequence as `down p=(0.00, 0.00)` followed by an up event at the eventual Begin button position. The trace then records the actual second click on `BEGIN SKIRMISH` at approximately `(1960,544)`/`(1962,544)`, with the UI action accepted at simulation time `0.000`.

After the match began, the trace records a `T` keyboard edge and then a real UGUI click on `Settler 100F` at approximately `(1354,284)`. The corresponding quit event ledger contains a player-0 `Payment` of `100 F` at simulation time `88.199` and the quit report records player-0 food spending of `100`, consistent with one paid settler order.

The application was then minimized or otherwise lost focus. The trace records `FRONTIER_PAUSE paused=True reason=focus_lost` at simulation time `89.499210`, followed by `FRONTIER_FOCUS focused=False`; the event ledger records the same focus-loss transition as `PauseChanged` with `message=focus_lost`. The quit report is explicitly `appVersion: 0.2.11`, `reason: quit`, `winner: -1`, and `time: 89.49920654296875`.

The supplied return-window screenshots and trace contain no later Unity `focused=True` callback and no later Escape input edge. The trace ends with the process quitting after the focus-loss state. The supplied images do not reproduce a white screen; they show the restored window/menu sequence available in this evidence set.

## Conclusion

This evidence supports that, in the recorded 0.2.11 client run, the first click was a stale or misplaced `(0,0)` pointer edge, the second click began the match, a real settler action was paid, and minimizing caused a focus-loss pause at simulation time `89.499`. It does not establish that the window-return path delivered a Unity `focused=True` callback or an Escape edge, so it cannot establish an automatic resume or an Escape recovery in that run.

The evidence does not show a white-screen reproduction. It also cannot establish the duration of the wall-clock focus interval from simulation time or frame counts, cannot establish behavior of a 0.2.12 client, and cannot generalize from this single native run to all macOS focus/window-manager paths. The recorded client and report remain 0.2.11 evidence.
