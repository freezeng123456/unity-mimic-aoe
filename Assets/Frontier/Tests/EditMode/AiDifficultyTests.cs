using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class AiDifficultyTests
    {
        static RuleCatalog Rules()=>Catalog.Load();
        static World World(int seed=4501)=>new World(Rules(),seed,"coast","river",5);

        [Test]
        public void DifficultyProfilesValidateAndDoNotShareMutableState()
        {
            var rules=Rules();
            Assert.That(rules.aiDifficulties,Is.Not.Null);
            var easy=AiDifficultyProfile.From(rules,"easy");
            var hard=AiDifficultyProfile.From(rules,"hard");
            Assert.AreEqual("easy",easy.Id);Assert.AreEqual("hard",hard.Id);
            Assert.That(easy.DecisionSeconds,Is.GreaterThan(hard.DecisionSeconds));
            Assert.That(easy.ArmySeconds,Is.GreaterThan(hard.ArmySeconds));
            Assert.That(AiDifficultyProfile.InvalidReason(null),Is.Not.Empty);
            Assert.Throws<ArgumentException>(()=>AiDifficultyProfile.From(rules,"missing"));
            Assert.Throws<ArgumentException>(()=>new AiDifficultyProfile(new AiDifficultyDef{id="bad",name="Bad",decisionSeconds=float.NaN,armySeconds=1}));
            Assert.Throws<ArgumentException>(()=>new AiDifficultyProfile(new AiDifficultyDef{id="bad",name="Bad",decisionSeconds=float.PositiveInfinity,armySeconds=1}));
            Assert.Throws<ArgumentException>(()=>new AiDifficultyProfile(new AiDifficultyDef{id="bad",name="Bad",decisionSeconds=0,armySeconds=1}));
            Assert.Throws<ArgumentException>(()=>new AiDifficultyProfile(new AiDifficultyDef{id="bad",name="Bad",decisionSeconds=.1f,armySeconds=.099f}));
            var def=new AiDifficultyDef{id="copy",name="Copy",description="x",decisionSeconds=1,armySeconds=2};var profile=new AiDifficultyProfile(def);
            def.name="mutated";def.decisionSeconds=9;def.armySeconds=9;
            Assert.AreEqual("Copy",profile.Name);Assert.AreEqual(1,profile.DecisionSeconds);Assert.AreEqual(2,profile.ArmySeconds);
        }

        [TestCase("easy")][TestCase("standard")][TestCase("hard")]
        public void EachDifficultyPaysAndCompletesOneActualTrainingJob(string difficulty)
        {
            var w=World();var p=w.Players[0];
            var town=w.Entities.First(e=>e.owner==0&&e.def.id=="town");
            var before=p.resources;var initialVillagers=w.Entities.Count(e=>e.owner==0&&e.def.id=="villager");
            var sim=new MatchSimulation(w,new[]{0},difficulty);
            Assert.AreEqual(before.food,p.resources.food);Assert.AreEqual(before.wood,p.resources.wood);Assert.AreEqual(before.coin,p.resources.coin,"Choosing a profile grants no resources");
            Assert.IsTrue(sim.Step());
            var payment=w.Events.Single(e=>e.player==0&&e.entity==town.id&&e.kind=="Payment");
            Assert.AreEqual(-100,payment.economy.actualDelta.food);Assert.AreEqual(0,payment.economy.actualDelta.wood);Assert.AreEqual(0,payment.economy.actualDelta.coin);
            Assert.IsTrue(w.Events.Any(e=>e.player==0&&e.entity==town.id&&e.kind=="ProductionQueued"&&e.message.StartsWith("villager")));
            Assert.AreEqual(1,p.reservedPopulation);Assert.AreEqual(initialVillagers,p.usedPopulation);
            // Let that actual AI order complete without the AI requesting a second job.
            for(int i=0;i<210;i++)w.Tick(MatchSimulation.StepSeconds);
            Assert.AreEqual(initialVillagers+1,w.Entities.Count(e=>e.owner==0&&e.def.id=="villager"));
            Assert.AreEqual(1,p.trained);Assert.AreEqual(0,p.reservedPopulation);Assert.AreEqual(initialVillagers+1,p.usedPopulation);
        }

        [Test]
        public void DifficultyChangesObservedDecisionReactionTime()
        {
            float Observe(string id)
            {
                var w=World(4510);w.Players[0].resources=new Cost();
                int town=w.Entities.First(e=>e.owner==0&&e.def.id=="town").id;
                var sim=new MatchSimulation(w,new[]{0},id);sim.Step();var start=w.Events.Count;var baseline=w.Time;
                Assert.IsFalse(w.Events.Any(e=>e.player==0&&e.entity==town&&e.kind=="Payment"));
                w.Players[0].resources=new Cost(100,0,0);
                for(int i=1;i<=100;i++) { sim.Step(); if(w.Events.Skip(start).Any(e=>e.player==0&&e.entity==town&&e.kind=="Payment")) return w.Time-baseline; }
                return float.PositiveInfinity;
            }
            var easy=Observe("easy");var standard=Observe("standard");var hard=Observe("hard");
            Assert.IsTrue(!float.IsNaN(easy)&&!float.IsInfinity(easy)&&!float.IsNaN(standard)&&!float.IsInfinity(standard)&&!float.IsNaN(hard)&&!float.IsInfinity(hard));
            Assert.That(easy,Is.GreaterThan(standard));
            Assert.That(standard,Is.GreaterThan(hard));
            Assert.That(easy,Is.GreaterThan(hard));
        }

        [Test]
        public void HiddenEnemyCannotBecomeAnExactDifficultyTarget()
        {
            foreach(string id in new[]{"easy","standard","hard"})
            {
                var w=World(4511);var hidden=w.Spawn("musketeer",1,new Vector2(112,112));
                for(int i=0;i<6;i++)w.Spawn("musketeer",0,new Vector2(34+i,38));
                Assert.IsFalse(w.IsVisible(0,hidden));new MatchSimulation(w,new[]{0},id).Step();
                Assert.IsFalse(w.Events.Any(e=>e.target==hidden.id));
            }
        }

        [Test]
        public void PausedDifficultySimulationDoesNotAccumulateCommands()
        {
            var w=World();var sim=new MatchSimulation(w,new[]{0},"hard");sim.Step();w.Paused=true;
            var events=w.Events.Count;var time=w.Time;Assert.IsFalse(sim.Step());Assert.AreEqual(events,w.Events.Count);Assert.AreEqual(time,w.Time);
            w.Paused=false;Assert.IsTrue(sim.Step());
        }
    }
}
