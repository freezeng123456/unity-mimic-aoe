using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class ScoutingThreat075Tests
    {
        // Declared worker/weapon arenas isolate threat admission and real combat.
        // No claim of paid production or a standard age-I match is made here.
        sealed class Arena
        {
            public readonly World World;
            public readonly EntityState Worker,Enemy;
            public readonly AiArmyPlan072 Army;
            readonly AiSearchPlan search;
            readonly AiWaterEconomy water;
            readonly AiRevolution071 revolution;
            readonly AiCivilizationEconomy civilization;
            public Arena(string enemy="native_scout",bool unarmed=false)
            {
                var rules=SnapshotCodec.Clone(Catalog.Load());
                if(unarmed){var definition=rules.Entity(enemy);definition.damage=definition.meleeDamage=definition.siegeDamage=0;}
                World=new World(rules,7509,"france",enemy=="envoy"?"river":"france",3);
                foreach(var entity in World.Entities.Where(e=>e.def.id!="town").ToArray())World.Remove(entity);
                Worker=World.Spawn("villager",0,new Vector2(40,26));
                Enemy=World.Spawn(enemy,1,new Vector2(50,26));
                World.Spawn("villager",1,new Vector2(90,98)); // permits legitimate stealth entry
                World.Fog.Tick(World.Entities);
                var profile=AiDifficultyProfile.From(rules,"standard");Army=new AiArmyPlan072(World,0,profile);
                search=new AiSearchPlan(World,0,World.OpeningTownPosition(1));water=new AiWaterEconomy(World,0);
                revolution=new AiRevolution071(World,0,profile);civilization=new AiCivilizationEconomy(World,0);
            }
            public List<EntityState> Own()=>World.Entities.Where(e=>e.Alive&&e.owner==0).ToList();
            public List<EntityState> Visible()=>World.Entities.Where(e=>e.Alive&&World.AreEnemies(0,e.owner)&&World.IsVisible(0,e)).ToList();
            public void Review()
            {
                var own=Own();
                // Intentionally include scouts in the supplied mobile list: the
                // planner's own admission must preserve the cohort distinction.
                Army.Review(own,own.Where(e=>e.def.kind==EntityKind.Unit&&!e.def.waterborne).ToList(),Visible(),
                    World.OpeningTownPosition(1),true,search,water,revolution,civilization);
            }
        }
        static void Accepted(CommandResult result)=>Assert.IsTrue(result.accepted,result.reason);
        static void Order(World world,EntityState actor,OrderKind order,int target=0)=>Accepted(world.Issue(
            new GameCommand{player=actor.owner,kind=CommandKind.Order,actors=new[]{actor.id},order=order,target=target}));
        static bool Damaged(World world,int source,int target)=>world.Events.Any(e=>e.kind=="Damage"&&e.entity==source&&e.target==target&&e.amount>0);

        [TestCase("native_scout")][TestCase("envoy")]
        public void ActualScoutWeaponDamageMakesGatheringAndConstructionUnsafe(string definition)
        {
            var a=new Arena(definition);var w=a.World;
            Assert.IsTrue(a.Enemy.Scout);Assert.IsFalse(a.Enemy.Military);Assert.IsTrue(w.IsVisible(0,a.Enemy));
            Order(w,a.Worker,OrderKind.Hold);Order(w,a.Enemy,OrderKind.Attack,a.Worker.id);float hp=a.Worker.hp;
            for(int i=0;i<150&&!Damaged(w,a.Enemy.id,a.Worker.id);i++)w.Tick(.1f);
            Assert.IsTrue(Damaged(w,a.Enemy.id,a.Worker.id));Assert.Less(a.Worker.hp,hp);
            Assert.IsTrue(w.Events.Any(e=>e.kind=="Fire"&&e.entity==a.Enemy.id&&e.target==a.Worker.id));
            Order(w,a.Worker,OrderKind.Stop);
            var threats=a.Visible();Assert.IsTrue(AiResourceContinuity072.EconomicThreat(w,0,a.Enemy));
            Assert.IsFalse(AiResourceContinuity072.Safe(w,0,a.Worker.position,threats));
            var source=w.Spawn("tree",-1,new Vector2(32,26));w.Fog.Tick(w.Entities);
            Assert.IsTrue(w.CanGather(a.Worker,source));Assert.IsTrue(w.GatherableResource(0,source));
            Assert.IsFalse(new AiGatherRoutes074(w,0,threats).CanGather(a.Worker,source));
            Assert.IsFalse(AiResourceContinuity072.TryConstructionWorker(w,0,w.Rules.Entity("house"),new Vector2(43,30),
                new List<EntityState>{a.Worker},threats,out _));
            var workforce=new AiWorkforce(w,0);workforce.Assign(new List<EntityState>{a.Worker},new Cost(0,1000,0),threats);
            Assert.IsFalse(workforce.HasSafeSource(ResourceKind.Wood));
            Assert.IsFalse(a.Worker.orders.Any(o=>o.kind==OrderKind.Gather&&o.target==source.id),"AI cannot send the victim through the observed scout's weapon envelope");
        }

        [TestCase("native_scout")][TestCase("envoy")]
        public void VisibleArmedScoutDispatchesActualDefendersWithoutEnlistingOwnedScouts(string definition)
        {
            var a=new Arena(definition);var w=a.World;
            var defender=w.Spawn("musketeer",0,new Vector2(34,26));
            var ownScout=w.Spawn("native_scout",0,new Vector2(20,48));Order(w,ownScout,OrderKind.Hold);
            Order(w,a.Worker,OrderKind.Hold);Order(w,a.Enemy,OrderKind.Attack,a.Worker.id);w.Fog.Tick(w.Entities);
            Assert.IsTrue(a.Army.HasImmediateThreat(a.Own(),a.Visible()));int first=w.Events.Count;
            a.Review();Assert.AreEqual(AiArmyPhase072.Defend,a.Army.Phase);
            Assert.AreEqual(OrderKind.AttackMove,defender.orders.Peek().kind);
            Assert.IsTrue(w.Events.Skip(first).Any(e=>e.kind=="Command"&&e.entity==defender.id&&e.message.StartsWith("AttackMove")));
            Assert.IsTrue(ownScout.hold);Assert.IsEmpty(ownScout.orders);
            Assert.IsFalse(a.Army.WaveMembers.Contains(ownScout.id)||a.Army.GuardMembers.Contains(ownScout.id));
            for(int i=0;i<300&&!Damaged(w,defender.id,a.Enemy.id);i++)w.Tick(.1f);
            Assert.IsTrue(Damaged(w,defender.id,a.Enemy.id),"The real accepted defense must fire and damage the raider");
            Assert.IsTrue(ownScout.hold);Assert.IsEmpty(ownScout.orders);
        }

        [Test] public void ConcealedScoutCannotTriggerSafetyOrDefenseUntilActuallyDetected()
        {
            var a=new Arena();var w=a.World;Order(w,a.Enemy,OrderKind.Hold);
            var source=w.Spawn("tree",-1,new Vector2(32,26));w.Fog.Tick(w.Entities);
            var supplied=new List<EntityState>{a.Enemy};
            Assert.IsTrue(w.IsVisible(0,a.Enemy));
            Accepted(w.Issue(new GameCommand{player=1,kind=CommandKind.ToggleStealth,actors=new[]{a.Enemy.id}}));
            Assert.AreEqual(VisibilityState.Visible,w.Fog.At(0,a.Enemy.position));Assert.IsFalse(w.IsVisible(0,a.Enemy));
            Assert.IsFalse(AiResourceContinuity072.EconomicThreat(w,0,a.Enemy));
            Assert.IsTrue(AiResourceContinuity072.Safe(w,0,a.Worker.position,supplied));
            Assert.IsTrue(new AiGatherRoutes074(w,0,supplied).CanGather(a.Worker,source));
            Assert.IsFalse(a.Army.HasImmediateThreat(a.Own(),supplied),"Even an accidentally supplied hidden entity must not become strategic knowledge");
            var detector=w.Spawn("envoy",0,new Vector2(43,29));Order(w,detector,OrderKind.Hold);w.Fog.Tick(w.Entities);
            Assert.Less(Vector2.Distance(detector.position,a.Enemy.position),8);Assert.IsTrue(a.Enemy.stealth);Assert.IsTrue(w.IsVisible(0,a.Enemy));
            Assert.IsTrue(AiResourceContinuity072.EconomicThreat(w,0,a.Enemy));
            Assert.IsFalse(new AiGatherRoutes074(w,0,supplied).CanGather(a.Worker,source));
            Assert.IsTrue(a.Army.HasImmediateThreat(a.Own(),supplied));
            w.Remove(detector);Assert.IsFalse(w.IsVisible(0,a.Enemy));
            Assert.IsFalse(a.Army.HasImmediateThreat(a.Own(),supplied));
        }

        [Test] public void WeaponlessScoutRemainsNonThreatAndOwnedScoutsCannotFillAFirstWave()
        {
            var unarmed=new Arena(unarmed:true);var u=unarmed.World;
            Assert.IsTrue(u.IsVisible(0,unarmed.Enemy));Assert.IsFalse(AiResourceContinuity072.EconomicThreat(u,0,unarmed.Enemy));
            Assert.IsFalse(unarmed.Army.HasImmediateThreat(unarmed.Own(),unarmed.Visible()));
            var a=new Arena();var w=a.World;w.Remove(a.Enemy);a.Review();Assert.IsTrue(a.Army.CaptureState().hasStage);
            var points=w.Navigation.FormationSlots(a.Army.RallyPoint,6,2.1f);Assert.AreEqual(6,points.Count);
            var members=points.Select((point,index)=>w.Spawn(index<3?"musketeer":"native_scout",0,point)).ToArray();
            foreach(var member in members)Order(w,member,OrderKind.Hold);
            while(w.Time<a.Army.CaptureState().reviewAt+.01f)w.Tick(.1f);
            w.Fog.Tick(w.Entities);a.Review();Assert.AreEqual(AiArmyPhase072.Muster,a.Army.Phase);
            Assert.IsEmpty(a.Army.WaveMembers);Assert.IsEmpty(a.Army.GuardMembers);
            Assert.IsFalse(w.Events.Any(e=>e.kind=="AIArmyPhase"&&e.message.Contains("first wave")),"Three support scouts must not turn three soldiers into a six-soldier departure");
            foreach(var scout in members.Where(e=>e.Scout)){Assert.IsTrue(scout.hold);Assert.IsEmpty(scout.orders);}
            var forged=a.Army.CaptureState();forged.phase=AiArmyPhase072.March;forged.initialWaveSize=3;
            forged.wave=members.Where(e=>e.Scout).Select(e=>e.id).ToArray();
            Assert.Throws<ArgumentException>(()=>a.Army.RestoreState(forged),"Cold AI state cannot smuggle scouts into a committed cohort");
        }
    }
}
