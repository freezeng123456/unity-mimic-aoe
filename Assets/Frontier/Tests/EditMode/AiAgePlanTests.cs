using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public class AiAgePlanTests
    {
        World world; EntityState town;
        [SetUp] public void SetUp()
        {
            world = new World(JsonUtility.FromJson<RuleCatalog>(JsonUtility.ToJson(Catalog.Load())));
            town = world.Entities.Find(e => e.owner == 0 && e.def.id == "town");
            world.Players[0].resources = new Cost(0, 0, 0);
        }
        [Test] public void SmallSoldierLossKeepsAnExistingAgePlan()
        {
            world.Players[0].age = 2; world.Players[0].resources = new Cost(1000, 1000, 1000);
            var plan = new AiAgePlan(world, 0); plan.Tick(town, 20, 8, 0, false);
            Assert.AreEqual(3, plan.TargetAge); Assert.IsTrue(plan.Saving);
            plan.Tick(town, 20, 7, 0, false);
            Assert.AreEqual(3, plan.TargetAge); Assert.IsTrue(plan.Saving);
        }
        [Test] public void VisibleRaidersPauseSavingAndResumeOnlyAfterTwentySeconds()
        {
            var plan = new AiAgePlan(world, 0); plan.Tick(town, 12, 8, 0, false);
            Assert.AreEqual(2, plan.TargetAge);
            plan.Tick(town, 12, 3, 1, false); Assert.IsFalse(plan.Saving);
            for (int i = 0; i < 199; i++) { world.Tick(.1f); plan.Tick(town, 12, 3, 0, false); }
            Assert.IsFalse(plan.Saving);
            world.Tick(.2f); plan.Tick(town, 12, 3, 0, false);
            Assert.IsTrue(plan.Saving);
        }
        [Test] public void QueuedOrCompletedResearchClearsAgePlan()
        {
            var plan = new AiAgePlan(world, 0); plan.Tick(town, 12, 0, 0, false); Assert.IsTrue(plan.Saving);
            plan.Tick(town, 12, 0, 0, true); Assert.AreEqual(0, plan.TargetAge);
        }
        [Test] public void StalledSavingsExitAfterOneHundredFiftySimulationSeconds()
        {
            var plan = new AiAgePlan(world, 0); plan.Tick(town, 12, 0, 0, false); Assert.IsTrue(plan.Saving);
            for (int i = 0; i < 1501; i++) { world.Tick(.1f); plan.Tick(town, 12, 0, 0, false); }
            Assert.AreEqual(0, plan.TargetAge); Assert.IsFalse(plan.Saving);
        }
        [Test] public void PauseFreezesPlanState()
        {
            var plan = new AiAgePlan(world, 0); plan.Tick(town, 12, 0, 0, false); int age = plan.TargetAge;
            world.Paused = true; for (int i = 0; i < 100; i++) { world.Tick(1); plan.Tick(town, 12, 0, 0, false); }
            Assert.AreEqual(age, plan.TargetAge); Assert.IsTrue(plan.Saving);
        }

        [Test] public void IntegratedAIProtectsAgeFundsAfterOneSoldierLoss()
        {
            var w=new World(Catalog.Load(),42,"river","coast",5);var p=w.Players[0];p.age=2;
            var barracks=w.Spawn("barracks",0,new Vector2(38,37));var stable=w.Spawn("stable",0,new Vector2(45,37));
            w.Spawn("bank",0,new Vector2(38,46));w.Spawn("market",0,new Vector2(45,46));
            for(int i=0;i<18;i++)w.Spawn("villager",0,new Vector2(24+i%6,20+i/6));
            var troops=Enumerable.Range(0,8).Select(i=>w.Spawn("musketeer",0,new Vector2(31+i,34))).ToArray();
            p.resources=new Cost(500,0,300);p.populationCap=200;
            var ai=new SkirmishAI(w,0);ai.Tick(.1f);
            Assert.IsTrue(w.Events.Any(e=>e.player==0&&e.kind=="AIStrategy"&&e.message=="Saving for age 3"));
            int cursor=w.Events.Count;var spent=p.spent;w.Remove(troops[7],1);
            for(int i=0;i<20;i++){ai.Tick(.1f);w.Tick(.1f);}
            Assert.IsFalse(w.Events.Skip(cursor).Any(e=>e.kind=="Payment"&&(e.entity==barracks.id||e.entity==stable.id)),"Losing one soldier must not release the existing age reserve to military queues");
            Assert.AreEqual(spent.food,p.spent.food);Assert.AreEqual(spent.coin,p.spent.coin);
        }
    }
}
