using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class ClassicCivilizationAiTests
    {
        [TestCase("france","homesteads","cavalry")]
        [TestCase("ottoman","administration","janissaries")]
        public void ClassicAiUsesItsAuthoredPeaceAndDefenseRoutes(string civilization,string peaceful,string threatened)
        {
            var w=new World(Catalog.Load(),42,civilization,"coast",5);var plan=new AiAgePlan(w,0);
            Assert.AreEqual(civilization+"_"+peaceful+"_2",plan.ChooseBranch(false));
            Assert.AreEqual(civilization+"_"+threatened+"_2",plan.ChooseBranch(true));
        }

        [Test] public void FrenchAiContinuouslyFundsAndCompletesCoureursWithoutOrdinarySettlerQueues()
        {
            var w=new World(Catalog.Load(),42,"france","coast",5);var p=w.Players[0];p.age=2;
            foreach(var crate in w.Entities.Where(e=>e.def.id.StartsWith("crate_")).ToArray())w.Remove(crate);
            p.resources=new Cost(0,500,0);var town=Own(w,"town");var worker=w.WorkerDefinition(0);var ai=new SkirmishAI(w,0);
            // After this opening fixture only ordinary AI decisions, gathering,
            // paid production and simulation time may expand the workforce.
            Simulate(w,ai,240);
            int completed=w.Events.Count(e=>e.player==0&&e.kind=="Experience"&&e.entity==town.id&&e.message=="Unit trained");
            var queued=w.Events.Where(e=>e.player==0&&e.kind=="ProductionQueued"&&e.entity==town.id).ToArray();
            Assert.GreaterOrEqual(completed,2);
            Assert.IsTrue(queued.All(e=>e.message.StartsWith("coureur ×")));
            Assert.GreaterOrEqual(w.Events.Count(e=>e.player==0&&e.kind=="Payment"&&e.entity==town.id&&e.message.StartsWith(worker.name+" ×")),completed);
            Assert.IsFalse(w.Events.Any(e=>e.player==0&&e.kind=="AutomaticWorkerProduced"));
            Assert.IsTrue(w.Entities.Where(e=>e.Alive&&e.owner==0&&e.Worker).All(e=>e.def.id=="coureur"&&!e.Military));
        }

        [Test] public void OttomanArmySpendsFoodWithoutReservingAForbiddenManualSettler()
        {
            var w=new World(Catalog.Load(),42,"ottoman","coast",5);var p=w.Players[0];p.age=2;
            var barracks=w.Spawn("barracks",0,new Vector2(38,37));w.Spawn("mosque",0,new Vector2(46,37));w.Spawn("market",0,new Vector2(46,46));
            var cost=w.UnitCost(0,w.Rules.Entity("janissary"));p.resources=cost;
            new SkirmishAI(w,0).Tick(.1f);
            Assert.AreEqual(1,barracks.queue.Count);Assert.AreEqual("janissary",barracks.queue[0].item);
            Assert.AreEqual(cost.food,p.spent.food,.001f);Assert.AreEqual(cost.coin,p.spent.coin,.001f);
            Assert.IsEmpty(Own(w,"town").queue);
            Assert.IsFalse(w.Events.Any(e=>e.player==0&&e.kind=="ProductionQueued"&&e.message.StartsWith("villager ×")));
        }

        [Test] public void OttomanAiBuildsPaidMosqueAndUpgradesActualAutomaticRecruitment()
        {
            var w=new World(Catalog.Load(),42,"ottoman","coast",5);var p=w.Players[0];var ai=new SkirmishAI(w,0);
            float initialSeconds=w.AutoRecruitSeconds(0);
            Simulate(w,ai,300);
            var mosque=w.Entities.FirstOrDefault(e=>e.Alive&&e.owner==0&&e.def.id=="mosque");
            Assert.IsNotNull(mosque);Assert.IsTrue(mosque.Complete);
            Assert.IsTrue(w.Events.Any(e=>e.player==0&&e.kind=="Payment"&&w.Get(e.entity)?.Worker==true&&e.message.StartsWith("Foundation "+mosque.def.name+": ")));
            Assert.IsTrue(p.technologies.Contains("mosque_millet"));
            Assert.IsTrue(w.Events.Any(e=>e.player==0&&e.kind=="Payment"&&e.entity==mosque.id&&e.message.StartsWith(w.Rules.Tech("mosque_millet").name+": ")));
            Assert.Less(w.AutoRecruitSeconds(0),initialSeconds);
            var automatic=w.Events.Where(e=>e.player==0&&e.kind=="AutomaticWorkerProduced").ToArray();
            Assert.GreaterOrEqual(automatic.Length,3);
            Assert.IsTrue(automatic.All(e=>e.message=="villager"&&e.amount==1&&w.Get(e.target)?.Worker==true));
            Assert.IsFalse(w.Events.Any(e=>e.player==0&&e.kind=="ProductionQueued"&&e.message.StartsWith("villager ×")));
        }

        [Test] public void OttomanAiPaysForWorkerCapBeforeAFullWorkforceCanStall()
        {
            var w=new World(Catalog.Load(),42,"ottoman","coast",5);var p=w.Players[0];p.age=2;
            var mosque=w.Spawn("mosque",0,new Vector2(44,35));
            for(int i=0;i<3;i++)w.Spawn("house",0,new Vector2(18+i*6,15));
            int initialWorkers=w.Entities.Count(e=>e.Alive&&e.owner==0&&e.Worker);
            for(int i=initialWorkers;i<21;i++)w.Spawn("villager",0,new Vector2(25+(i%5)*1.3f,32+(i/5)*1.3f));
            p.resources=new Cost(300,700,200);float initialSeconds=w.AutoRecruitSeconds(0);
            Simulate(w,new SkirmishAI(w,0),180);
            Assert.IsTrue(p.technologies.Contains("mosque_galata"));Assert.Greater(w.WorkerLimit(0),25);
            Assert.Less(w.AutoRecruitSeconds(0),initialSeconds);
            Assert.IsTrue(w.Events.Any(e=>e.player==0&&e.kind=="Payment"&&e.entity==mosque.id&&e.message.StartsWith(w.Rules.Tech("mosque_galata").name+": ")));
            Assert.IsTrue(w.Events.Any(e=>e.player==0&&e.kind=="AutomaticWorkerProduced"));
            Assert.IsFalse(w.Events.Any(e=>e.player==0&&e.kind=="ProductionQueued"&&e.message.StartsWith("villager ×")));
        }

        [Test] public void OttomanAiUsesAgeTwoAbusAtItsEarlyFoundry()
        {
            var w=new World(Catalog.Load(),42,"ottoman","coast",5);var p=w.Players[0];p.age=2;
            w.Spawn("barracks",0,new Vector2(37,37));w.Spawn("mosque",0,new Vector2(45,37));
            var foundry=w.Spawn("foundry",0,new Vector2(45,46));p.resources=w.UnitCost(0,w.Rules.Entity("abus"));
            new SkirmishAI(w,0).Tick(.1f);
            Assert.AreEqual(2,w.UnitAge(0,foundry.def));Assert.AreEqual(1,foundry.queue.Count);Assert.AreEqual("abus",foundry.queue[0].item);
            Assert.IsTrue(w.Events.Any(e=>e.player==0&&e.kind=="Payment"&&e.entity==foundry.id));
        }

        [Test] public void DutchAiUsesItsEarlySkirmisherAgeOverride()
        {
            var w=new World(Catalog.Load(),42,"river","coast",5);var p=w.Players[0];p.age=2;
            var barracks=w.Spawn("barracks",0,new Vector2(38,37));w.Spawn("bank",0,new Vector2(46,37));w.Spawn("market",0,new Vector2(46,46));
            for(int i=0;i<4;i++)w.Spawn("house",0,new Vector2(15+i*6,15));
            int workers=w.Entities.Count(e=>e.Alive&&e.owner==0&&e.Worker);
            for(int i=workers;i<24;i++)w.Spawn("villager",0,new Vector2(25+(i%5)*1.3f,32+(i/5)*1.3f));
            p.resources=w.UnitCost(0,w.Rules.Entity("skirmisher"));new SkirmishAI(w,0).Tick(.1f);
            Assert.AreEqual(1,barracks.queue.Count);Assert.AreEqual("skirmisher",barracks.queue[0].item);
            Assert.IsTrue(w.Events.Any(e=>e.player==0&&e.kind=="Payment"&&e.entity==barracks.id));
        }

        static EntityState Own(World w,string id)=>w.Entities.First(e=>e.Alive&&e.owner==0&&e.def.id==id);
        static void Simulate(World w,SkirmishAI ai,int seconds)
        {for(int i=0;i<seconds*10&&w.Winner<0;i++){ai.Tick(.1f);w.Tick(.1f);}}
    }
}
