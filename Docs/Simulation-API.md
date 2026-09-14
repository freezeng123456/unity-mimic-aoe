# Simulation API

`World` owns `Rules`, `Players`, `Entities`, `Navigation`, `Fog`, `Events`, `Projectiles`, `Seed`, `MaxAge`, `Winner`, `Time` and `Paused`. `Issue(GameCommand)` is the shared mutation boundary. `Tick(float)` advances fixed simulation time and does nothing while paused or after a winner; the controller normally submits 0.1-second steps.

`World.IsVisible(player, EntityState)` is the fog boundary. `Visibility` retains Unexplored, Explored and Visible states. `Spawn` is bootstrap/engine authority, not an AI permission. `Get(id)` resolves entities; dead entities remain addressable but fail `Alive` and visibility checks.

Economy validates civilization, age, costs, producer, queue, population, footprint and vision. It handles construction, repair, training, research, ages, cancellation refunds and serialized shipments. Gathering credits finite resources until depleted. Used and reserved population are tracked separately. Shipments wait on missing receivers, capacity or blocked landing positions.

Navigation supplies dynamic blockers and A*. Combat uses delayed projectiles and resolves resistance at impact. `Remove` updates blockers, queues, population and kill/loss counters. Victory requires no living non-resource entity; the first town center is not an instant victory condition.

`StartMatch` creates a fresh seeded World and resets presentation state. `ReturnToMenu` clears World and AI references; restart creates a new simulation. `TogglePause` pauses the simulation and releases the cursor. Reports and event logs are written under Unity persistent data.

`VerificationRunner.RunDuel` is automated QA using two ordinary AI clients and an injected world factory. It proves automated rule/lifecycle linkage only, not manual controls, UI, audio, visual QA, build success, performance, or deterministic replay.
