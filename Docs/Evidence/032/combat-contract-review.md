# Artillery combat contract review (032)

Read-only review of the native player command → order → movement/deployment → preparation → fire → projectile impact path, plus existing tests. No balance/rules/source edits and no native launch.

## High-confidence conclusions

1. **No exact code defect was found that blocks a specified visible building attack.** `RtsInput` maps `A` + click on a visible enemy to `OrderKind.Attack` (`RtsInput.cs:177–182`), and `World.IssueOrders` accepts a visible enemy target (`World.cs:117–141`). `MovementCombat` then moves a non-worker attack-order unit toward the target, requires `EdgeDistance` within `[minRange, range+.1]`, waits two seconds for `deploy=true`, prepares after cooldown, converts a building hit to `siegeDamage`, and creates a projectile (`MovementCombat.cs:40–72, 223–243`). This is an end-to-end implemented path.

2. **The principal behavior gates are real and can explain missed structures without implying a bug:** target visibility is required both when issuing and retaining a direct Attack order; cannon minimum range is 4; movement clears deployment; and `AttackMove` has no fixed target and may select visible defending units before a building (`World.cs:123–128`; `MovementCombat.cs:44, 53–72, 105–119, 125–129). Fire-time target loss, fog, garrisoning, death, or range failure silently returns from `Fire` without a dedicated failure event (`MovementCombat.cs:230–234`). These are contract conditions, not evidence of a broken building branch.

3. **Existing tests cover the building damage seam, but not the full native player interaction seam.** `ProjectileLifecycleTests.PrimaryBuildingHitUsesCannonSiegeDamage` spawns a cannon and enemy house, issues an `Attack` command, and asserts a 130 Siege Damage event (`Assets/Frontier/Tests/EditMode/ProjectileLifecycleTests.cs:57–77`). Other tests cover projectile impact after fog entry, target death before impact, cooldown, garrison firepower, and animation timing (`ProjectileLifecycleTests.cs:28–77`, `GarrisonFirepowerTests.cs`, `UnitAnimationTests.cs:21–28`). There is no test found that starts at `RtsInput`/screen picking, verifies native `A` + building click, exercises cannon movement into range and the two-second deployment, or distinguishes direct Attack from AttackMove under defenders. Thus the pure simulation building-hit seam is covered, while the actual UI-to-combat seam remains unverified.

## Deployment, melee mode, and retargeting boundaries

Field Cannon has `meleeDamage=0`, so `forceMelee` cannot turn it into a melee attacker; the mode command only applies to units with melee damage and range >2 (`World.cs:104–105`). Deployment is explicitly exercised by the runtime path, but the existing building projectile test disables deployment (`cannonDef.deploy=false`) and sets minimum range to zero for deterministic setup (`ProjectileLifecycleTests.cs:61–69`). Therefore that test proves building siege conversion/projectile impact, not native deployment positioning.

Target re-selection is implemented in `FindEnemy`; buildings receive a +8 score penalty, so an attack-move cannon can prefer nearby units. Existing tests do not provide a dedicated assertion for this target preference or for a direct fixed building target surviving movement, visibility, deployment, and defender contact.

## Acceptance boundary

The source and test evidence support “a valid simulation Attack against a visible, in-range building can deal cannon siege damage.” They do not support “the native player can always reach and keep a building target,” “AttackMove prioritizes buildings,” or “a failed normal match proves a combat defect.” A decisive missing seam would be one native/player-input test or recorded run that selects a cannon, targets a currently visible building, keeps it stationary in the 4–20 range band through deployment and fire, and verifies target-specific Fire/Impact/Damage plus building HP decrease.

