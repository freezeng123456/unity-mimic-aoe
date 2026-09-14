# 033 test notes

Added `AttackReleaseTests` with four bounded EditMode cases using a cloned `RuleCatalog`, real `World.Issue`, and `World.Tick(MatchSimulation.StepSeconds)`. The current fixture uses two original-definition cannons, holds the target cannon in place, and gives it extra runtime HP only to prevent incidental combat death; it does not alter cannon deployment, minimum range, windup, or period.

- target entering the cannon minimum range during preparation cancels the shot and preserves `attackReadyAt`, then fires only after re-entry and the original deadline;
- exact minimum edge distance remains legal;
- an already emitted projectile still resolves after the target enters the dead-angle region;
- target death before firing cancels the shot without creating a projectile or resetting the deadline.

The root run recorded the same-source comparison as 3/4 on the old runtime and 4/4 after the five-line runtime change (receipt SHA `4523ba6791a57cbd5006b5c4bb2c329627b8a68b5b2a4a35414503a31078fc74`). The first case polls until real deployment and attack preparation, checks no Fire before the preserved deadline, and waits through the complete windup after re-entry. Existing `GarrisonFirepowerTests.LeavingBeforeFireCancelsShotWithoutResettingCooldown` covers cancellation on garrison removal; the 033 file itself does not separately parameterize pre-fire loss of vision or garrison state. No Unity run was performed in this review.
