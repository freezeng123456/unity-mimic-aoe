# 037 选择双击静态审计

## 观察与源码事实

`RtsInput.ProcessPointer` treats a non-drag left release as a selection click. The double-selection branch is:

```csharp
if (entity != null && entity.owner == 0 &&
    lastType == entity.def.id && edge.time - lastClickTime < .3)
{
    // select every visible/on-screen friendly entity of this definition
}
else Select(entity, edge.shift);
lastType = entity?.def.id;
lastClickTime = edge.time;
```

The predicate compares only the current entity definition ID and the time gap. It does not compare the current screen position with the previous click, and it does not require the same entity ID. Two fast clicks on different on-screen villagers of the same definition therefore enter the “select all same type” branch even when they are about 100 screen pixels apart. With fourteen villagers sharing `def.id == "villager"`, this can select all fourteen; a later right-click resource order then legitimately assigns the same gather/rally behavior to the whole selected set. This is consistent with the reported food stoppage, although the copied 0.2.15 NativePlayer log has no input trace proving the click coordinates or the exact selected count, so the log alone cannot prove this was the trigger.

`lastClickTime` and `lastType` are updated only in the non-drag left-release path. A right-click is handled by `ProcessRight` and does not reset them. A UI click is consumed by `Hud.ProcessUiPointer` before `ProcessPointer`, so it does not update them either. A drag takes the `Dragging` branch, but the common tail still assigns `lastType` from the release pick (often null) and updates `lastClickTime`; this can suppress a subsequent same-type double selection by time/type coincidence, without providing a useful spatial reset. `ResetSelection`, `ClearTransientInput`, and restart/menu paths reset the fields, but ordinary right-click and ordinary UI transitions do not.

The `DiscreteInputBuffer` preserves distinct left/right/key edges in FIFO order and records pointer positions per event. Nothing in that buffer adds native double-click distance semantics. The UI dispatcher separately tracks press/release capture; it does not supply a double-click decision to `RtsInput`.

## Relation to the native observation

The reported sequence—rapid clicks on separated villagers followed by a right-click on a mine, with all fourteen eventually routed to that mine—matches the static failure mode: same definition plus a sub-0.3-second gap is sufficient to expand selection, and right-click then issues gather orders to every selected worker. The current `native-Player.log` is not an input-trace log, so it should not be used to claim the exact 100-pixel distance, click interval, or fourteen-selection transition was directly recorded. It remains a plausible mechanism supported by the code and the visual/player outcome.

This is independent of T/V/M/B/G/ESC single-key routing and independent of Ctrl-number group input. Those paths use `EdgeKind.Key`; the suspected defect is in left-release selection classification.

## Smallest repair that preserves existing behavior

Keep the existing same-definition double-click feature, but add a screen-distance gate using the previous left-click position, for example `Vector2.Distance(pointer,lastClickPosition) <= doubleClickMaxDistance` alongside the current type/time checks. A small threshold in screen pixels should be chosen from the UI/input scale and frozen in a focused PlayMode test; it must be large enough for two clicks on the same visible unit and small enough to reject the observed separated clicks. Store the position whenever a world left click is classified, and clear the double-click candidate on right-click, UI-consumed clicks, drag completion, focus/reset, selection reset, and modal transitions. Preserve `lastType`/time semantics for same-unit/same-area double selection.

The minimum verification should use real InputSystem down/up pairs: two nearby clicks on one worker type must still select same-type units; two fast clicks separated by roughly 100 screen pixels must select only the clicked units; a subsequent right-click must order only the resulting selection. Add checks that UI clicks, drags, and right-clicks cannot leave a stale double-click candidate. Do not infer success from the absence of a console error or from a later gather event alone.
