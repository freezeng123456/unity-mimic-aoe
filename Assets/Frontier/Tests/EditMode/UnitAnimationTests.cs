using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public class UnitAnimationTests
    {
        World world; EntityState unit;
        [SetUp] public void SetUp()
        {
            world = new World(JsonUtility.FromJson<RuleCatalog>(JsonUtility.ToJson(Catalog.Load())));
            unit = world.Spawn("musketeer", 0, new Vector2(40, 40));
        }
        [Test] public void MovementRequiresDisplacementAndStatusAloneCannotInventWork()
        {
            unit.status = "Moving"; unit.previousPosition = unit.position - Vector2.right;
            Assert.AreEqual(UnitPoseState.Move, UnitAnimation.Evaluate(unit, 2).state);
            unit.status = "Gathering food"; unit.previousPosition = unit.position;
            Assert.AreEqual(UnitPoseState.Idle, UnitAnimation.Evaluate(unit, 2).state, "A status string on a musketeer is not real worker activity");
        }
        [Test] public void AttackPreparationFireAndReloadUseSimulationTimeAnchors()
        {
            unit.fireAt = 2.5f; unit.attackReadyAt = 4; unit.status = "Preparing attack";
            var preparing = UnitAnimation.Evaluate(unit, 2.5f-unit.def.windup*.5f);
            Assert.AreEqual(UnitPoseState.PreparingAttack, preparing.state); Assert.That(preparing.actionPhase, Is.InRange(0.49f, .51f));
            var fired = UnitAnimation.Evaluate(unit, 2.6f, 2.5f);
            Assert.AreEqual(UnitPoseState.Fire, fired.state); Assert.That(fired.actionPhase, Is.EqualTo(.1f / .22f).Within(.001f));
            // MovementCombat clears fireAt immediately after Fire; the reload
            // pose is therefore evaluated from the real post-shot state.
            unit.fireAt = -1; unit.status = "Reloading";
            var reloading = UnitAnimation.Evaluate(unit, 3.5f, 2.5f);
            Assert.AreEqual(UnitPoseState.Reloading, reloading.state); Assert.Greater(reloading.actionPhase, 0);
        }
        [Test] public void GatherOrderWithoutActualMovementDoesNotClaimMovementPose()
        {
            unit.status = "Moving"; unit.previousPosition = unit.position;
            unit.orders.Enqueue(new UnitOrder(OrderKind.Gather, unit.position, 0));
            var pose = UnitAnimation.Evaluate(unit, 1);
            Assert.IsFalse(pose.moving); Assert.AreEqual(UnitPoseState.Idle, pose.state, "A queued gather order is not evidence of work at a resource");
        }
        [Test] public void PausedRenderReadsAtSameTimeProduceStablePose()
        {
            unit.status = "Moving"; unit.previousPosition = unit.position - Vector2.up;
            var a = UnitAnimation.Evaluate(unit, 7.25f); var b = UnitAnimation.Evaluate(unit, 7.25f);
            Assert.AreEqual(a.state, b.state); Assert.AreEqual(a.locomotionPhase, b.locomotionPhase); Assert.AreEqual(a.actionPhase, b.actionPhase);
        }
        [TestCase("ally_highland_guard")]
        [TestCase("ally_river_pike")]
        [TestCase("ally_coastal_marine")]
        public void AlliedPikemenUsePolearmAttackPose(string id)
        {
            var ally=world.Spawn(id,0,new Vector2(40,40));ally.fireAt=2.2f;
            var pose=UnitAnimation.Evaluate(ally,2);
            Assert.IsTrue(pose.polearm);Assert.IsTrue(pose.melee);
            Assert.AreEqual(UnitPoseState.PreparingAttack,pose.state);
        }
        [Test] public void EvaluationDoesNotMutateSimulationFields()
        {
            unit.status = "Preparing attack"; unit.fireAt = 3; unit.attackReadyAt = 8; unit.attackTarget = 17; unit.hp = 91;
            var before = new { unit.status, unit.fireAt, unit.attackReadyAt, unit.attackTarget, unit.hp, unit.position };
            UnitAnimation.Evaluate(unit, 4, 3, 3);
            Assert.AreEqual(before.status, unit.status); Assert.AreEqual(before.fireAt, unit.fireAt); Assert.AreEqual(before.attackReadyAt, unit.attackReadyAt);
            Assert.AreEqual(before.attackTarget, unit.attackTarget); Assert.AreEqual(before.hp, unit.hp); Assert.AreEqual(before.position, unit.position);
        }
    }
}
