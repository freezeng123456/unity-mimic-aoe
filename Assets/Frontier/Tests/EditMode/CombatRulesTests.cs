using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public class CombatRulesTests
    {
        World world;
        EntityState attacker,target;
        [SetUp] public void SetUp()
        {
            // Independent rule fixture: changing a test definition never mutates Catalog's cached assets.
            var rules=JsonUtility.FromJson<RuleCatalog>(JsonUtility.ToJson(Catalog.Load()));
            var gun=rules.Entity("musketeer");gun.damage=10;gun.meleeDamage=0;gun.range=20;gun.period=2;gun.windup=.2f;gun.projectileSpeed=10;
            gun.bonuses=new[]{new TargetBonus{tags=Tags.Infantry,multiplier=2},new TargetBonus{tags=Tags.HeavyInfantry,multiplier=3}};
            var victim=rules.Entity("pike");victim.damage=0;victim.hp=500;victim.tags=Tags.Infantry|Tags.HeavyInfantry;victim.rangedResist=.2f;
            world=new World(rules);
            // Damage-rule fixture excludes unrelated neutral camp attackers.
            foreach(var guard in world.Entities.Where(World.Guardian).ToArray())world.Remove(guard);
            attacker=world.Spawn("musketeer",0,new Vector2(40,40));target=world.Spawn("pike",1,new Vector2(45,40));
            world.Fog.Tick(world.Entities);
        }
        void Steps(int n){for(int i=0;i<n;i++)world.Tick(.1f);}
        GameCommand Attack()=>new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{attacker.id},order=OrderKind.Attack,target=target.id,point=target.position};
        [Test] public void ReissuedAttackCannotBypassCooldown()
        {
            Assert.IsTrue(world.Issue(Attack()).accepted);Steps(9);
            Assert.AreEqual(1,world.Events.Count(e=>e.kind=="Fire"&&e.entity==attacker.id));
            float deadline=attacker.attackReadyAt;
            for(int i=0;i<10;i++){Assert.IsTrue(world.Issue(Attack()).accepted);Steps(1);Assert.AreEqual(deadline,attacker.attackReadyAt);}
            Assert.AreEqual(1,world.Events.Count(e=>e.kind=="Fire"&&e.entity==attacker.id));
            Steps(7);Assert.AreEqual(2,world.Events.Count(e=>e.kind=="Fire"&&e.entity==attacker.id));
        }
        [Test] public void TargetKilledBeforeProjectileImpactIsNotDamagedAgain()
        {
            attacker.def.projectileSpeed=1;Assert.IsTrue(world.Issue(Attack()).accepted);Steps(5);
            Assert.IsTrue(world.Projectiles.Any(p=>p.target==target.id),"Test must actually have an in-flight projectile");
            world.Remove(target,0);Steps(60);
            Assert.AreEqual(0,world.Events.Count(e=>e.kind=="Damage"&&e.target==target.id));
            Assert.AreEqual(1,world.Events.Count(e=>e.kind=="Death"&&e.entity==target.id));
        }
        [Test] public void MatchingBonusesUseMaximumThenResistance()
        {
            Assert.IsTrue(world.Issue(Attack()).accepted);Steps(10);
            Assert.AreEqual(500-10*3*.8f,target.hp,.01f);
            Assert.AreEqual(24,world.Events.Single(e=>e.kind=="Damage"&&e.target==target.id).amount,.01f);
        }
        [Test] public void SplashRecomputesEachVictimsBonusAndResistance()
        {
            attacker.def.splash=3;
            var second=world.Spawn("villager",1,new Vector2(45,41));second.def.hp=500;second.hp=500;second.def.rangedResist=.4f;second.def.tags=Tags.Villager;
            Assert.IsTrue(world.Issue(Attack()).accepted);Steps(10);
            Assert.AreEqual(500-10*.5f*.6f,second.hp,.01f,"Secondary victim must not inherit primary target's 3x infantry multiplier");
        }
        [Test] public void LethalBuildingDamageReleasesPopulationAndNavigationExactlyOnce()
        {
            var house=world.Spawn("house",1,new Vector2(80,80));int cap=world.Players[1].populationCap;
            Assert.IsFalse(world.Navigation.IsWalkable(house.position,.4f));
            world.ApplyDamage(house,100000,DamageKind.Siege,0,attacker.id);
            Assert.IsTrue(world.Navigation.IsWalkable(house.position,.4f));Assert.AreEqual(cap-house.def.capacity,world.Players[1].populationCap);
            world.ApplyDamage(house,100000,DamageKind.Siege,0,attacker.id);
            Assert.AreEqual(1,world.Events.Count(e=>e.kind=="Death"&&e.entity==house.id));
        }
    }
}
