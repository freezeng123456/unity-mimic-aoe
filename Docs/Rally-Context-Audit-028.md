# Rally and Context Command Audit 028

状态：只读审计，未修改运行时代码，未启动 Unity。本文区分当前工程已确认的行为、项目主动简化和仍未实现的目标；不把未核实的《帝国时代 III：决定版》行为写成事实。

## 当前工程已确认的行为

`RtsInput.ContextOrder` first separates selected buildings and mobile units. A selected building receives a `CommandKind.Rally` containing only `point`; selected mobile units receive one `CommandKind.Order`. The target entity is used to choose the order kind for mobile units:

- a visible resource or owned resource-like entity selects `OrderKind.Gather`;
- a treasure selects `Claim` when the actor is an explorer;
- an enemy or guardian selects `Attack`;
- an unfinished friendly building selects `Build` for workers;
- a damaged friendly building selects `Repair` for workers;
- a completed friendly garrison building selects `Garrison`;
- otherwise the fallback is `Move`.

`World.Issue` rejects a `Gather` order unless the actor is a worker and the target is a valid resource. Consequently, a non-worker explorer right-clicking a resource can produce a rejected `Gather` command rather than a movement or exploration order. This is current project behavior, confirmed by the implementation and the reported native trace; it is not evidence of the reference game's exact behavior.

Production jobs store one `rally` `Vector2` on the producer. `Economy.TickProduction` spawns the completed group, then calls automatic formation-slot assignment around that rally point. The resource or other entity that may have motivated the rally is not stored in `ProductionJob` or in `EntityState.rally`. A town center right-click on a resource therefore stores a location only. Newly trained workers can walk toward that point, but there is no resource target identity to turn the arrival into a `Gather` order; if no later command assigns a resource, they may arrive idle. This is a confirmed implementation limitation.

The README currently documents “select a production building and right-click the map” as the project rally contract. The current implementation does not document or implement an entity-aware production rally. The simulation API separately documents real worker gathering, finite depletion, and population/production validation.

## Reference status and project scope

The public reference material in this repository supports studying the broad RTS concepts of worker gathering, production rally points, contextual orders, and shipment/settler behavior. It does not provide a source-backed rule for the exact non-worker right-click fallback in this project, nor does the current evidence establish that a production building's rally command should bind future workers to a particular resource entity. Those semantics must be selected as a project contract and labelled as a deliberate implementation choice until verified against a suitable public source.

The original user requirement asks for right-click context actions and sensible production集结. It does not require silently assigning a resource to every future worker. The least surprising project contract is therefore:

1. A rally command may be either a location rally or an entity-aware resource rally.
2. An entity-aware resource rally is only created when the player explicitly right-clicks a valid visible resource while a compatible production building is selected.
3. The rally stores the resource entity id and a fallback point. If that resource is depleted, removed, hidden from the player, or no longer valid, the worker falls back to the stored point and an explicit `Resource depleted`/`Rally target unavailable` state; it does not acquire hidden information.
4. A non-worker contextual right-click on a resource falls back to `Move` toward the resource's work point, or `Claim`/`Attack` when the target is a treasure/guardian and the actor supports that action. It must never emit a worker-only `Gather` order that is expected to succeed.
5. Worker right-click on a resource remains a direct `Gather` order with the resource entity as target. This preserves actual resource accounting and depletion handling.

This contract is a proposed project behavior, not a claim about the commercial reference game's exact implementation.

## Minimal coherent interface proposal

Keep player intent separate from the eventual worker order. Extend the command model with a small target descriptor rather than encoding a resource by position:

```text
RallyIntent {
    mode: Location | ResourceEntity;
    point: Vector2;          // fallback/formation point
    targetEntity: int;       // 0 for Location
}
```

`CommandKind.Rally` can carry this descriptor. `EntityState` should retain the selected rally mode, point, and target id; `ProductionJob` should snapshot the same intent when the job is paid. On completion, the producer passes the snapshot to a single resolver:

```text
ResolveProductionDestination(producer, job, spawnedUnit)
```

The resolver validates ownership/visibility/liveness/resource type and then either creates a worker `Gather(targetEntity)` order, creates a normal formation move to the fallback point, or leaves the unit idle with a visible reason. It must not call `Gather` for military units. Existing automatic formation-slot assignment should still distribute multiple units around the resolved point; each worker can receive the same resource target while using a distinct work position through the existing `WorkPoint` logic.

For `ContextOrder`, classify the actor set before selecting the order. If the selected mobile set contains workers, a valid resource target can remain `Gather`. If it contains no workers, issue `Move` to the resource work point and show a notification such as `Non-workers cannot gather; moving to resource`. For a mixed worker/non-worker selection, split the command into worker `Gather` and non-worker `Move` orders, or reject with an explicit mixed-selection reason; do not send one invalid order to all actors.

The implementation should preserve the existing command result and event trail. A rejected or downgraded contextual command must record its reason. The resolver must clear or replace a previous resource rally when the player right-clicks ordinary terrain, so a stale depleted resource cannot silently reappear in a later production batch.

## Meaningful regressions

The following tests would cover the contract without asserting an unverified commercial-game rule:

1. **Non-worker resource fallback.** Select an explorer, right-click a visible resource, and assert one accepted `Move` order to the resource work point, target `0`, with no rejected worker-only `Gather` command.
2. **Worker direct gather remains entity-aware.** Select a worker, right-click a resource, advance simulation, and assert resource credit events name the selected resource entity; deplete/remove it and assert the worker transitions to the documented fallback/reassignment state.
3. **Resource-aware production rally.** Select a town center, issue an explicit resource rally, train a villager, advance until completion, and assert the new worker has a `Gather` order targeting that exact resource id and a non-overlapping work position. Then issue a plain terrain rally and assert the next villager receives only a move/formation destination.
4. **Invalidated rally does not use hidden state.** Set a resource rally, remove or make the resource unavailable before production completes, and assert the spawned worker does not gather it, does not discover a replacement resource outside visibility, and records the fallback reason.
5. **Mixed selection is deterministic.** Select one worker and one military unit, right-click a resource, and assert either two documented orders (worker `Gather`, military `Move`) or one explicit rejection; assert no military `Gather` and no unlogged partial command.

Until these changes exist, the correct audit label is: direct worker gathering implemented; location-only production rally implemented; resource-entity production rally not implemented; non-worker contextual fallback currently unsafe because `ContextOrder` can request worker-only `Gather` before `World.Issue` rejects it.
