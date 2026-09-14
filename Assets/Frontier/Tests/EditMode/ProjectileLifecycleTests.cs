using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public class ProjectileLifecycleTests
    {
        World world;
        EntityState attacker, target;

        [SetUp] public void SetUp()
        {
            var rules = JsonUtility.FromJson<RuleCatalog>(JsonUtility.ToJson(Catalog.Load()));
            var gun = rules.Entity("musketeer");
            gun.damage = 10; gun.meleeDamage = 0; gun.range = 20; gun.period = 2; gun.windup = .2f; gun.projectileSpeed = 10;
            var victim = rules.Entity("pike"); victim.damage = 0; victim.hp = 500; victim.rangedResist = 0;
            world = new World(rules);
            foreach(var guardian in world.Entities.Where(World.Guardian).ToArray())world.Remove(guardian);
            attacker = world.Spawn("musketeer", 0, new Vector2(40, 40));
            target = world.Spawn("pike", 1, new Vector2(45, 40));
            world.Fog.Tick(world.Entities);
        }

        void Steps(int count) { for (int i = 0; i < count; i++) world.Tick(.1f); }
        GameCommand Attack() => new GameCommand { player = 0, kind = CommandKind.Order, actors = new[] { attacker.id }, order = OrderKind.Attack, target = target.id, point = target.position };

        [Test] public void TargetEnteringFogAfterFireStillReceivesTheInFlightImpact()
        {
            Assert.IsTrue(world.Issue(Attack()).accepted);
            Steps(3); // fire has happened, but the projectile is still travelling
            Assert.IsTrue(world.Projectiles.Any(p => p.target == target.id));
            target.position = new Vector2(100, 100);
            world.Fog.Tick(world.Entities);
            Assert.IsFalse(world.IsVisible(0, target));
            float before = target.hp;
            Steps(30);
            Assert.Less(target.hp, before, "An already fired projectile must resolve against its recorded target even after vision is lost");
            Assert.AreEqual(1, world.Events.Count(e => e.kind == "Impact" && e.target == target.id));
        }

        [Test] public void ReplacingPreparingAttackWithMoveDoesNotResetTheReloadDeadline()
        {
            Assert.IsTrue(world.Issue(Attack()).accepted);
            Steps(1);
            float deadline = attacker.attackReadyAt;
            Assert.Greater(attacker.fireAt, world.Time);
            Assert.IsTrue(world.Issue(new GameCommand { player = 0, kind = CommandKind.Order, actors = new[] { attacker.id }, order = OrderKind.Move, point = new Vector2(42, 42) }).accepted);
            Assert.AreEqual(-1, attacker.fireAt, .001f, "Replacing a prepared shot must cancel the pending fire");
            Assert.AreEqual(deadline, attacker.attackReadyAt, .001f, "Cancelling preparation must not refund or reset the weapon cooldown");
            Assert.IsTrue(world.Issue(Attack()).accepted);
            while(world.Time+.1f<deadline)world.Tick(.1f);
            Assert.AreEqual(0, world.Events.Count(e => e.kind == "Fire" && e.entity == attacker.id));
            Steps(6);
            Assert.IsTrue(world.Events.Any(e => e.kind == "Fire" && e.entity == attacker.id),"Attack resumes after the preserved cooldown");
        }

        [Test] public void CannonSplashUsesSiegeDamageOnBuildingsAndHalfBaseDamageOnNearbyUnits()
        {
            var rules = JsonUtility.FromJson<RuleCatalog>(JsonUtility.ToJson(Catalog.Load()));
            var cannonDef = rules.Entity("cannon"); cannonDef.deploy = false; cannonDef.windup = .1f; cannonDef.period = 10; cannonDef.projectileSpeed = 100; cannonDef.range = 30; cannonDef.minRange = 0;
            world = new World(rules);
            foreach(var guardian in world.Entities.Where(World.Guardian).ToArray())world.Remove(guardian);
            attacker = world.Spawn("cannon", 0, new Vector2(40, 40));
            var building = world.Spawn("house", 1, new Vector2(45, 40));
            var unit = world.Spawn("pike", 1, new Vector2(46, 40));
            building.hp = 1000; unit.hp = 1000;
            world.Fog.Tick(world.Entities);
            Assert.IsTrue(world.Issue(new GameCommand { player = 0, kind = CommandKind.Order, actors = new[] { attacker.id }, order = OrderKind.Attack, target = building.id, point = building.position }).accepted);
            Steps(8);
            var buildingDamage = world.Events.Single(e => e.kind == "Damage" && e.target == building.id);
            var unitDamage = world.Events.Single(e => e.kind == "Damage" && e.target == unit.id);
            Assert.AreEqual(130, buildingDamage.amount, .01f, "Primary building hit uses cannon siege damage");
            // The cannon's splash starts at half base damage, then applies its
            // Infantry ×2 target bonus to the pike: 70 × .5 × 2 = 70.
            Assert.AreEqual(70, unitDamage.amount, .01f, "Secondary infantry hit applies half base damage before the cannon's Infantry ×2 bonus");
            Assert.AreEqual(DamageKind.Siege.ToString(), buildingDamage.message);
            Assert.AreEqual(DamageKind.Siege.ToString(), unitDamage.message);
        }
    }
}
