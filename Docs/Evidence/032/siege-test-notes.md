# Siege input integration tests · verified 032

The four added tests use queued InputSystem events through the current input and scene layers. They do not simulate physical hardware or claim a native standalone manual match. Runtime, shared entity definitions and sealed client ZIP remain unchanged.

The movement fixture starts outside the cannon range, places a friendly villager near the building to provide genuine vision, and advances the existing MatchSimulation.Step() at StepSeconds = 0.1. Assertions require real movement, at least two seconds of deployment, preparation before Fire, Fire before Impact, Impact and Damage in the same tick, unchanged building HP at the first Fire, and actual subsequent HP loss. Explicit building targeting and ground AttackMove are separate tests.

First run: 3/4, because the fixture accidentally used undefined entity ID settler. Corrected to the shipped villager ID; second run: 4/4 (0.9655993 s). Full PlayMode: 115/115 (28.1915045 s). This was fixture repair, not a runtime fix. No EditMode rerun was needed for the test-only additions; 104/104 remains the prior 031 result on unchanged runtime.
