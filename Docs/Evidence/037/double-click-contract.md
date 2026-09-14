# Double-click selection contract — iteration 037

Status: project UI contract (P), not a verified Age of Empires III rule. Written before runtime changes. The sealed 0.2.15 native match is preserved independently.

A completed single left click selects its picked object. Two consecutive completed left clicks can select all visible friendly entities of the same definition only when their event timestamps are within 0.3 seconds and their screen release positions are within 8 input pixels, matching the current drag slop. The picked object must be friendly, and the second timestamp must not precede the first. A distant second click remains a single selection even if both objects have the same definition. This preserves normal double-click selection without requiring a stationary entity identity.

A right-click command, a UI pointer gesture, a completed box selection, a minimap gesture, a keyboard world operation, programmatic selection/focus, a target-mode transition, a device pointer reset or a pause/focus/modal transition breaks the old double-click candidate. An empty-ground click does not establish a candidate. Shift retains the existing append behavior for a valid double click. The interaction does not alter gather, attack, movement, AI, resource or shipment rules.

Ranked hypotheses: (1) missing screen distance merges separate worker picks, (2) right commands retain the first pick and merge a later pick, (3) UI/box transitions retain stale click context. Frozen queued InputSystem tests will distinguish these cases on unchanged runtime before applying the fix.

Evidence limits: the ordinary 037 match had no detailed native input trace, so its worker misallocation is a correlated observation, not an exact replay. Ctrl/Command group saves and the desktop capture white rectangle remain separate unresolved issues. A passing queued-input regression does not prove hardware modifier delivery or a complete player siege match.
