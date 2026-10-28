using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class AiIndustrialSaveTests
    {
        [Test] public void AiShipsTwoRealFactoriesProducesItsHeavyArtilleryAndColdRestoresTheSameDecisions()
        {
            // Controlled age IV, two earned-XP inputs and six regular guards isolate
            // the industrial decision. No wagon, factory, gun, resources or capacity are gifted.
            // The opponent is not automated; this is not an unaided opening-to-industry match.
            var world=new World(Catalog.Load(),6407,"france","coast",4);
            var player=world.Players[0];player.age=4;player.resources=new Cost();
            player.deck.Clear();player.deck.Add("factory_wagon");player.deck.Add("robber_barons");
            for(int i=0;i<6;i++)world.Spawn("musketeer",0,new Vector2(30+i*1.6f,38));
            for(int i=0;i<2;i++)world.AwardXp(0,world.Rules.initialXp+world.Rules.xpIncrease*player.xpLevels-player.xp,0,"Controlled AI industry fixture XP");
            var simulation=new MatchSimulation(world,0);
            Until(simulation,()=>world.Entities.Count(e=>e.Alive&&e.owner==0&&e.def.id=="factory"&&e.Complete)==2,180,
                "Both earned shipments must deliver, deploy and finish through the AI's public commands");
            var factories=world.Entities.Where(e=>e.Alive&&e.owner==0&&e.def.id=="factory").OrderBy(e=>e.id).ToArray();
            Assert.AreEqual(2,player.sentCards.Count(card=>card=="factory_wagon"||card=="robber_barons"));
            Assert.AreEqual(2,world.Events.Count(e=>e.kind=="FactoryFoundationStarted"&&e.player==0));
            Assert.IsFalse(world.Entities.Any(e=>e.Alive&&e.owner==0&&e.def.id=="factory_wagon"));
            Until(simulation,()=>factories[1].factoryMode=="artillery"&&factories[1].factoryElapsed>=5,90,
                "The second factory must select artillery after a real regular guard force is available");
            Assert.AreNotEqual("artillery",factories[0].factoryMode,"The first factory retains resource production");
            Assert.IsTrue(world.Events.Any(e=>e.kind=="FactoryModeChanged"&&e.entity==factories[1].id&&e.message=="artillery"));
            Assert.IsTrue(world.Events.Any(e=>e.kind=="RallySet"&&e.entity==factories[1].id));
            Assert.IsTrue(world.Events.Any(e=>e.kind=="FactoryIncome"&&e.entity==factories[0].id));
            Assert.IsTrue(world.Events.Any(e=>e.kind=="FactoryModeChanged"&&e.entity==factories[0].id&&e.message=="wood"),
                "Real housing/infrastructure shortages must be able to change the default food mode");

            var savedWorld=JsonUtility.FromJson<WorldSnapshot>(JsonUtility.ToJson(world.CaptureState()));
            var savedSimulation=JsonUtility.FromJson<MatchSimulationSnapshot>(JsonUtility.ToJson(simulation.CaptureState()));
            var restored=MatchSimulation.Restore(World.Restore(world.Rules,savedWorld),savedSimulation);
            var loadedFactory=restored.World.Get(factories[1].id);bool produced=false;
            for(int i=0;i<1800&&!produced;i++)
            {
                float progress=factories[1].factoryElapsed;bool researching=factories[1].queue.Count>0;
                Assert.IsTrue(simulation.Step());Assert.IsTrue(restored.Step());
                Assert.AreEqual("artillery",factories[1].factoryMode,"Resource fluctuations cannot discard a committed gun");
                Assert.AreEqual(factories[1].factoryMode,loadedFactory.factoryMode);
                Assert.AreEqual(factories[1].factoryElapsed,loadedFactory.factoryElapsed);
                produced=world.Events.Any(e=>e.kind=="FactoryArtilleryProduced"&&e.entity==factories[1].id);
                if(!produced)
                {
                    Assert.GreaterOrEqual(factories[1].factoryElapsed,progress);
                    if(researching&&factories[1].queue.Count>0)Assert.AreEqual(progress,factories[1].factoryElapsed,"Research pauses the committed gun");
                }
            }
            Assert.IsTrue(produced,"A real gun must leave the factory within the bounded production/research interval");
            var output=world.Events.Last(e=>e.kind=="FactoryArtilleryProduced"&&e.entity==factories[1].id);
            Assert.AreEqual("heavy_cannon",world.Get(output.target).def.id);
            Assert.AreEqual(7,world.Get(output.target).def.population);
            Assert.IsTrue(restored.World.Get(output.target).Alive);
            Assert.AreEqual(JsonUtility.ToJson(world.CaptureState()),JsonUtility.ToJson(restored.World.CaptureState()),"Cold recovery must preserve actual entities, payments, population, modes and production events");
            Assert.AreEqual(JsonUtility.ToJson(simulation.CaptureState()),JsonUtility.ToJson(restored.CaptureState()));
            TestContext.WriteLine("Controlled age-IV AI industry: two real factories; first heavy cannon at "+world.Time+"s; population "+player.usedPopulation+" + reserved "+player.reservedPopulation+" / "+player.populationCap);
        }

        private static void Until(MatchSimulation simulation,Func<bool> complete,float seconds,string message)
        {
            int count=Mathf.CeilToInt(seconds/MatchSimulation.StepSeconds);
            for(int i=0;i<count&&!complete();i++)if(!simulation.Step())break;
            if(!complete())foreach(var e in simulation.World.Entities.Where(e=>e.Alive&&e.owner==0&&(e.def.id=="factory"||e.def.id=="factory_wagon")))
                TestContext.WriteLine(e.id+" "+e.def.id+" position="+e.position+" complete="+e.Complete+" mode="+e.factoryMode+" progress="+e.factoryElapsed+" status="+e.status);
            Assert.IsTrue(complete(),message);
        }

        [TestCase("coast")]
        [TestCase("ottoman")]
        public void AiRequestsItsRealFactoryShipmentThenBuildsAndFinishesTheFactory(string civilization)
        {
            // An Industrial-age start supplies one ordinary shipment entitlement,
            // not a spawned wagon, a free building or accelerated construction.
            var world=new World(Catalog.Load(),6407,civilization,"coast",5);
            var player=world.Players[0];player.age=4;player.resources=new Cost();player.shipmentTokens=1;
            player.deck.Clear();player.deck.Add("factory_wagon");
            var simulation=new MatchSimulation(world,0);simulation.Step();
            Assert.AreEqual(0,player.shipmentTokens);Assert.IsTrue(player.shipments.Any(job=>job.card=="factory_wagon"));
            EntityState factory=null;
            for(int i=0;i<1000&&factory==null;i++)
            {
                simulation.Step();factory=world.Entities.FirstOrDefault(e=>e.Alive&&e.owner==0&&e.def.id=="factory");
            }
            Assert.IsNotNull(factory,"A delivered wagon must be consumed through a legal Build command");
            Assert.IsFalse(factory.Complete);Assert.IsFalse(world.Entities.Any(e=>e.Alive&&e.owner==0&&e.def.id=="factory_wagon"));
            Assert.IsTrue(world.Events.Any(e=>e.kind=="FactoryFoundationStarted"&&e.entity==factory.id));
            Assert.IsFalse(world.Events.Any(e=>e.kind=="FactoryIncome"&&e.entity==factory.id),"The foundation must finish real construction before earning income");
            var savedWorld=JsonUtility.FromJson<WorldSnapshot>(JsonUtility.ToJson(world.CaptureState()));
            var savedSimulation=JsonUtility.FromJson<MatchSimulationSnapshot>(JsonUtility.ToJson(simulation.CaptureState()));
            var restored=MatchSimulation.Restore(World.Restore(world.Rules,savedWorld),savedSimulation);
            for(int i=0;i<850;i++){simulation.Step();restored.Step();}
            Assert.IsTrue(factory.Complete);Assert.IsTrue(restored.World.Get(factory.id).Complete);
            Assert.IsTrue(world.Events.Any(e=>e.kind=="FactoryIncome"&&e.entity==factory.id));
            Assert.AreEqual(JsonUtility.ToJson(world.CaptureState()),JsonUtility.ToJson(restored.World.CaptureState()),"An AI factory foundation and later income must survive a cold restore");
            Assert.AreEqual(JsonUtility.ToJson(simulation.CaptureState()),JsonUtility.ToJson(restored.CaptureState()));
        }

        [Test] public void AiDoesNotDiscardAWagonWhenNoSafeVisibleFactorySiteExists()
        {
            // All own vision is occupied. This adversarial placement fixture must
            // not give the AI permission to build in hidden terrain or consume a wagon.
            var world=new World(Catalog.Load(),6407,"coast","coast",5);world.Players[0].age=4;world.Players[0].resources=new Cost();
            var wagon=world.Spawn("factory_wagon",0,new Vector2(24,28));
            foreach(var point in Enumerable.Range(0,128).SelectMany(x=>Enumerable.Range(0,128).Select(y=>new Vector2(x,y))))
                if(world.Fog.At(0,point)==VisibilityState.Visible)world.Navigation.SetTerrainBlocked(point,.1f,true);
            new SkirmishAI(world,0).Tick(.1f);
            Assert.IsTrue(wagon.Alive);Assert.IsFalse(world.Entities.Any(e=>e.Alive&&e.owner==0&&e.def.id=="factory"));
            Assert.IsFalse(world.Events.Any(e=>e.kind=="FactoryFoundationStarted"));
        }
    }
}
