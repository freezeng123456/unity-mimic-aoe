using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class NeutralAllyAi090Tests
    {
        static readonly MethodInfo Review=typeof(SkirmishAI).GetMethod("UseNeutralSettlement",BindingFlags.NonPublic|BindingFlags.Instance);
        // Controlled fixture starts with an already-owned post to isolate this AI
        // decision. Every service still travels through the actual paid command.
        static World Fixture(int index,out NeutralAllianceState state,out NeutralAllyDef def)
        {
            var rules=SnapshotCodec.Clone(Catalog.Load());var world=new World(rules,maxAge:5);
            foreach(var e in world.Entities.Where(x=>x.def.kind==EntityKind.Unit).ToArray())world.Remove(e);
            world.Players[0].age=5;world.Players[0].resources=new Cost(5000,5000,5000);
            state=world.NeutralAllies[index];def=world.NeutralDefinition(state.id);var site=world.Get(state.site);
            var post=world.Spawn("trade_post",0,site.position);
            world.Spawn("villager",0,world.Navigation.NearestWalkable(site.position+new Vector2(-5,0),.4f));
            state.owner=0;state.post=post.id;world.Fog.Tick(world.Entities);
            foreach(string id in World.NeutralOptions(def,CommandKind.NeutralRecruit))rules.Entity(id).time=.2f;
            foreach(string id in World.NeutralOptions(def,CommandKind.NeutralResearch))rules.Tech(id).time=.2f;
            return world;
        }
        static void Decide(World world,Cost reserve)
        {
            Assert.IsNotNull(Review);var own=world.Entities.Where(e=>e.Alive&&e.owner==0).ToList();
            Review.Invoke(new SkirmishAI(world,0),new object[]{own,reserve});
        }
        static void Complete(World world){for(int i=0;i<5;i++)world.Tick(.1f);}
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void PaidAiMixesBothAlliedUnitsAndResearchesBothApplicableUpgrades(int index)
        {
            var world=Fixture(index,out var state,out var def);
            string[] expected={def.unit,def.additionalUnits[0],def.unit,def.tech,def.additionalUnits[0],def.additionalTechs[0],def.unit};
            string[] expectedKinds={"Train","Train","Train","Research","Train","Research","Train"};
            for(int i=0;i<expected.Length;i++)
            {
                var before=world.Players[0].resources;int payments=world.Events.Count(e=>e.kind=="Payment"&&e.entity==state.site);
                Decide(world,new Cost());Assert.AreEqual(1,state.jobs.Count,"Decision "+i);
                Assert.AreEqual(expected[i],state.jobs[0].item,"Decision "+i);Assert.AreEqual(expectedKinds[i],state.jobs[0].kind);
                Assert.AreEqual(payments+1,world.Events.Count(e=>e.kind=="Payment"&&e.entity==state.site));
                Assert.IsTrue(world.Players[0].resources.food<before.food||world.Players[0].resources.wood<before.wood||world.Players[0].resources.coin<before.coin,"Actual economy pays each service");
                Complete(world);Assert.IsEmpty(state.jobs,"One paid job completes before the next review");
            }
            Assert.AreEqual(3,world.Entities.Count(e=>e.Alive&&e.owner==0&&e.def.id==def.unit));
            Assert.AreEqual(2,world.Entities.Count(e=>e.Alive&&e.owner==0&&e.def.id==def.additionalUnits[0]));
            Assert.IsTrue(world.Players[0].technologies.Contains(def.tech));Assert.IsTrue(world.Players[0].technologies.Contains(def.additionalTechs[0]));
            int totalPayments=world.Events.Count(e=>e.kind=="Payment"&&e.entity==state.site);Decide(world,new Cost());
            Assert.AreEqual(totalPayments,world.Events.Count(e=>e.kind=="Payment"&&e.entity==state.site),"Five allied units enforce the original bounded investment");
        }
        [Test] public void AiCannotBypassVisionAgeResourceReserveOrContest()
        {
            var world=Fixture(0,out var state,out var def);var site=world.Get(state.site);
            void NoService(){Assert.IsEmpty(state.jobs);Assert.IsFalse(world.Events.Any(e=>e.kind=="Payment"&&e.entity==state.site));}
            world.Players[0].age=1;Decide(world,new Cost());NoService();
            world.Players[0].age=5;world.Fog.RestoreState(new int[32768]);Decide(world,new Cost());NoService();
            world.Fog.Tick(world.Entities);Decide(world,new Cost(5000,5000,5000));NoService();
            var enemy=world.Spawn("musketeer",1,site.position+new Vector2(4,0));world.Fog.Tick(world.Entities);Decide(world,new Cost());NoService();
            world.Remove(enemy);world.Fog.Tick(world.Entities);Decide(world,new Cost());
            Assert.AreEqual(def.unit,state.jobs.Single().item);Assert.AreEqual("Train",state.jobs[0].kind);
        }
        [Test] public void ColdWorldRestoreMakesTheSameNextChoiceAndPayment()
        {
            var world=Fixture(1,out var state,out var def);Decide(world,new Cost());Complete(world);
            var cold=World.Restore(world.Rules,SnapshotCodec.Clone(world.CaptureState()));
            Decide(world,new Cost());Decide(cold,new Cost());
            Assert.AreEqual(def.additionalUnits[0],state.jobs.Single().item);
            Assert.AreEqual(JsonUtility.ToJson(world.CaptureState()),JsonUtility.ToJson(cold.CaptureState()));
        }
    }
}
