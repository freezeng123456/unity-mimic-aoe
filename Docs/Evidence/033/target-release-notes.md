# Target release coverage

`TargetReleaseVisibilityTests` adds two EditMode cases without changing the frozen four-case `AttackReleaseTests` file:

- a genuinely prepared cannon shot is cancelled after the target moves out of the player fog and `Fog.Tick` confirms it is no longer visible;
- a genuinely prepared shot is cancelled after the target enters an enemy town through an accepted `World.Issue` garrison command and fixed-step ticks confirm `garrisonedIn`.

Both cases assert no `Fire`, no projectile, and preservation of `attackReadyAt`. The target cannon uses the original definition and only receives extra runtime HP to avoid unrelated combat death. Unity was not run for this addition.
