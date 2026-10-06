using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class AiRevolution071Tests
    {
        // These prepared age-IV arenas isolate paid political orders and recovery.
        // A durable town rule keeps a successful army from ending the fixture
        // before the 90s+60s queues can be observed. Initial holdings, housing and
        // units are explicit fixture inputs, never claimed as an unaided opening.
        static World IndustrialArena(string civilization,int seed,Cost resources,bool scarce=false,bool fullPopulation=false,bool siegeFixture=false)
        {
            var rules=JsonUtility.FromJson<RuleCatalog>(JsonUtility.ToJson(Catalog.Load()));
            rules.Entity("town").hp=200000;
            if(fullPopulation)rules.populationLimit=46;
            if(scarce)foreach(var definition in rules.entities)
            {
                if(definition.kind==EntityKind.Unit)definition.speed=0;
                definition.damage=0;definition.meleeDamage=0;definition.siegeDamage=0;
                definition.gatherRate=0;
            }
            if(siegeFixture)
            {
                // Declared accelerated single-target siege fixture. The public
                // attack/projectile/damage path must destroy the real houses.
                var siege=rules.Entity("cannon");siege.damage=1;siege.siegeDamage=5000;
                siege.range=90;siege.vision=100;siege.splash=0;siege.period=.2f;
                siege.windup=.1f;siege.projectileSpeed=1000;siege.deploy=false;siege.speed=0;
            }
            var world=new World(rules,seed,civilization,"coast",5);
            var p=world.Players[0];p.age=4;
            for(int i=0;i<12;i++)world.Spawn("house",0,new Vector2(8+i%6*6,6+i/6*6));
            var worker=world.WorkerDefinition(0);
            while(world.Entities.Count(e=>e.Alive&&e.owner==0&&e.Worker&&!e.def.waterborne)<38)
            {
                int i=world.Entities.Count(e=>e.Alive&&e.owner==0&&e.Worker&&!e.def.waterborne);
                world.Spawn(worker.id,0,new Vector2(30+i%6*1.25f,30+i/6*1.25f));
            }
            string guard=civilization=="river"?"skirmisher":civilization=="ottoman"?"janissary":"musketeer";
            for(int i=0;i<8;i++)world.Spawn(guard,0,new Vector2(17+i*1.3f,40));
            world.Spawn("barracks",0,new Vector2(38,37));world.Spawn("stable",0,new Vector2(45,37));
            world.Spawn("market",0,new Vector2(45,46));world.Spawn("foundry",0,new Vector2(38,46));
            if(!scarce&&world.CountingHouseLimit(0)>0)world.Spawn("bank",0,new Vector2(52,37));
            if(civilization=="ottoman")world.Spawn("mosque",0,new Vector2(52,46));
            p.resources=resources;p.shipmentTokens=0;
            if(scarce)foreach(var crate in world.Entities.Where(e=>e.Alive&&e.owner==0&&e.def.id.StartsWith("crate_")).ToArray())world.Remove(crate);
            if(siegeFixture)
            {
                world.Players[1].age=4; // Declared legal enemy cannon opening.
                var siege=world.Spawn("cannon",1,new Vector2(75,10));
                Assert.IsTrue(world.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{siege.id},order=OrderKind.Move,point=new Vector2(124,16)}).accepted);
            }
            return world;
        }
        static EntityState Town(World world)=>world.Entities.First(e=>e.Alive&&e.owner==0&&e.def.id=="town");
        static Cost PoliticalBudget(string civilization)
        {
            var world=new World(Catalog.Load(),42,civilization,"coast",5);
            return world.RevolutionDefinition(0).cost+world.Rules.Tech(World.CivilianRebuildingTechnology).cost+
                (world.AutoRecruitSeconds(0)>0?new Cost():world.UnitCost(0,world.WorkerDefinition(0)));
        }
        static void CostEquals(Cost expected,Cost actual)
        {Assert.AreEqual(expected.food,actual.food,.001f);Assert.AreEqual(expected.wood,actual.wood,.001f);Assert.AreEqual(expected.coin,actual.coin,.001f);}
        static void Until(MatchSimulation simulation,Func<bool> complete,float seconds,string message)
        {
            for(int i=0;i<Mathf.CeilToInt(seconds/MatchSimulation.StepSeconds)&&!complete();i++)if(!simulation.Step())break;
            Assert.IsTrue(complete(),message+"; time="+simulation.World.Time+"; phase="+simulation.World.Players[0].politicalPhase+"; resources="+simulation.World.Players[0].resources);
        }
        static MatchSimulation Cold(MatchSimulation simulation)
        {
            var saved=JsonUtility.FromJson<WorldSnapshot>(JsonUtility.ToJson(simulation.World.CaptureState()));
            var control=JsonUtility.FromJson<MatchSimulationSnapshot>(JsonUtility.ToJson(simulation.CaptureState()));
            return MatchSimulation.Restore(World.Restore(simulation.World.Rules,saved),control);
        }
        static void TickUntil(World world,Func<bool> complete,float seconds,string message)
        {
            for(int i=0;i<Mathf.CeilToInt(seconds/MatchSimulation.StepSeconds)&&!complete()&&!world.IsMatchEnded;i++)world.Tick(MatchSimulation.StepSeconds);
            Assert.IsTrue(complete(),message+"; time="+world.Time+"; phase="+world.Players[0].politicalPhase);
        }
        static World AfterEnemyHousingLoss(string civilization,bool visibleTownThreat=false)
        {
            // Prepared age-IV recovery fixture. Political decisions are actual
            // human commands; the recovery AI starts only after actual combat.
            var world=IndustrialArena(civilization,42,PoliticalBudget(civilization),true,false,true);
            var town=Town(world);var p=world.Players[0];
            if(visibleTownThreat)world.Spawn("musketeer",1,town.position+new Vector2(11,0));
            var weakest=world.Entities.First(e=>e.Alive&&e.owner==0&&e.Worker&&!e.def.waterborne);
            weakest.hp=world.MaxHp(weakest)*.5f; // Declared pre-command injury.
            Assert.AreEqual(46,p.usedPopulation);Assert.AreEqual(140,p.populationCap);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Revolution,actors=new[]{town.id}}).accepted);
            TickUntil(world,()=>p.politicalPhase==PoliticalPhase.Revolutionary,95,"Real revolution must complete");
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Research,actors=new[]{town.id},item=World.CivilianRebuildingTechnology}).accepted);
            TickUntil(world,()=>p.politicalPhase==PoliticalPhase.CivilianRebuilt,65,"Real paid civilian research must complete");
            var siege=world.Entities.Single(e=>e.Alive&&e.owner==1&&e.def.id=="cannon");
            Assert.Greater(Vector2.Distance(siege.position,town.position),34);
            float siegeAt=world.Time;int lost=0;
            foreach(var house in world.Entities.Where(e=>e.Alive&&e.owner==0&&e.def.id=="house").Take(11).ToArray())
            {
                Assert.IsTrue(world.IsVisible(1,house),"The attacker must actually see its target");
                Assert.IsTrue(world.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{siege.id},order=OrderKind.Attack,target=house.id}).accepted);
                TickUntil(world,()=>!house.Alive,3,"Actual enemy fire must destroy the house");lost++;
                Assert.IsTrue(world.Events.Any(e=>e.kind=="Damage"&&e.player==1&&e.entity==siege.id&&e.target==house.id&&e.amount>0));
                Assert.IsTrue(world.Events.Any(e=>e.kind=="Death"&&e.entity==house.id&&e.player==0&&e.target==1));
                Assert.AreEqual(140-lost*10,p.populationCap,"Each real house death must remove its actual capacity");
            }
            Assert.IsTrue(world.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{siege.id},order=OrderKind.Move,point=new Vector2(124,16)}).accepted);
            Assert.Less(world.Time-siegeAt,50,"Housing must be lost before a55s Ottoman automatic birth can mask the shortage");
            Assert.AreEqual(46,p.usedPopulation);Assert.AreEqual(30,p.populationCap);Assert.AreEqual(0,p.reservedPopulation);
            Assert.IsFalse(world.Entities.Any(e=>e.Alive&&e.owner==0&&e.Worker&&!e.def.waterborne));
            Assert.AreEqual(0,town.queue.Count);
            CostEquals(new AiRevolution071(world,0,AiDifficultyProfile.From(world.Rules,"standard")).StarterWorkerCost,p.resources);
            return world;
        }

        [TestCase("coast",42,true)] [TestCase("coast",43,false)]
        [TestCase("river",42,true)] [TestCase("river",43,false)]
        [TestCase("france",42,true)] [TestCase("france",43,false)]
        [TestCase("ottoman",42,true)] [TestCase("ottoman",43,false)]
        public void NormalAgeOneOpeningSelectsBothStableRoutesWithoutAnEarlyPoliticalOrder(string civilization,int seed,bool revolution)
        {
            var world=new World(Catalog.Load(),seed,civilization,"coast",5);
            var plan=new AiRevolution071(world,0,AiDifficultyProfile.From(world.Rules,"standard"));
            Assert.AreEqual(revolution,plan.PrefersRevolution);Assert.IsTrue(plan.AllowsAgeAdvance);
            var simulation=new MatchSimulation(world,0);
            for(int i=0;i<100;i++)Assert.IsTrue(simulation.Step());
            Assert.Less(world.Players[0].age,4);
            Assert.IsFalse(world.Events.Any(e=>e.player==0&&(e.kind=="RevolutionQueued"||e.kind=="RevolutionCompleted")));
            Assert.IsFalse(Town(world).queue.Any(j=>j.kind=="Age"&&j.item=="5"));
            Assert.IsTrue(world.Events.Any(e=>e.player==0&&(e.kind=="Command"||e.kind=="ProductionQueued")),"The AI must actually issue ordinary opening orders");
            TestContext.WriteLine("Unmodified age-I opening only: seed="+seed+", civ="+civilization+", selected="+(revolution?"revolution":"Empire V")+"; not a completed late-game route");
        }

        [TestCase("coast")] [TestCase("river")] [TestCase("france")] [TestCase("ottoman")]
        public void RevolutionSavingsProtectBothTheRealCommitmentAndCivilianRecovery(string civilization)
        {
            var budget=PoliticalBudget(civilization);budget.food-=1;
            var world=IndustrialArena(civilization,42,budget);var town=Town(world);
            var ai=new SkirmishAI(world,0);var spent=world.Players[0].spent;
            ai.Tick(.1f);
            Assert.IsFalse(town.queue.Any(j=>j.kind=="Revolution"||j.kind=="Age"));
            Assert.IsFalse(world.Events.Any(e=>e.player==0&&e.kind=="RevolutionQueued"));
            Assert.AreEqual(0,ai.CaptureState().agePlan.targetAge,"Saving for revolution must not reserve or issue age V");
            CostEquals(spent,world.Players[0].spent);
            CostEquals(PoliticalBudget(civilization),new AiRevolution071(world,0,ai.Difficulty).Reserve);
        }

        [TestCase("coast")] [TestCase("river")] [TestCase("france")] [TestCase("ottoman")]
        public void PreparedIndustrialAiPaysRevolutionRebuildsCiviliansAndColdRestoresTheSameOrders(string civilization)
        {
            var world=IndustrialArena(civilization,42,PoliticalBudget(civilization));
            var simulation=new MatchSimulation(world,0);var town=Town(world);var p=world.Players[0];
            int[] originalWorkers=world.Entities.Where(e=>e.Alive&&e.owner==0&&e.Worker&&!e.def.waterborne).Select(e=>e.id).ToArray();
            Assert.IsTrue(simulation.Step());
            var job=town.queue.Single(j=>j.kind=="Revolution");
            CostEquals(world.RevolutionDefinition(0).cost,job.paid);
            Assert.AreEqual(world.RevolutionDefinition(0).time,job.duration);
            Assert.IsTrue(world.WorkerSourcesClosed(0));
            Assert.GreaterOrEqual(p.resources.food,400);Assert.GreaterOrEqual(p.resources.coin,400);
            Assert.AreEqual(1,world.Events.Count(e=>e.player==0&&e.kind=="RevolutionQueued"));
            for(int i=0;i<50;i++)Assert.IsTrue(simulation.Step());
            var restored=Cold(simulation);
            for(int i=0;i<2200&&p.politicalPhase!=PoliticalPhase.CivilianRebuilt;i++)
            {Assert.IsTrue(simulation.Step());Assert.IsTrue(restored.Step());}
            Assert.AreEqual(PoliticalPhase.CivilianRebuilt,p.politicalPhase);
            Assert.AreEqual(4,p.age);Assert.IsTrue(p.technologies.Contains(World.CivilianRebuildingTechnology));
            Assert.GreaterOrEqual(p.revolutionAt,world.RevolutionDefinition(0).time-.11f);
            Assert.GreaterOrEqual(p.civilianRebuiltAt-p.revolutionAt,world.Rules.Tech(World.CivilianRebuildingTechnology).time-.11f);
            Assert.IsFalse(world.WorkerSourcesClosed(0));Assert.IsTrue(world.CivilianWorkersEnabled(0));
            foreach(int id in originalWorkers)Assert.IsTrue(world.Get(id).def.revolutionary,"Original worker IDs remain soldiers after civilian rebuilding");
            Assert.AreEqual(1,world.Events.Count(e=>e.player==0&&e.kind=="RevolutionCompleted"));
            Assert.AreEqual(1,world.Events.Count(e=>e.player==0&&e.kind=="CivilianRebuilt"));
            Assert.IsFalse(world.Events.Any(e=>e.player==0&&e.kind=="Payment"&&e.message.Contains("Advance to age 5")));
            Assert.IsFalse(town.queue.Any(j=>j.kind=="Age"));
            Assert.AreEqual(JsonUtility.ToJson(world.CaptureState()),JsonUtility.ToJson(restored.World.CaptureState()));
            Assert.AreEqual(JsonUtility.ToJson(simulation.CaptureState()),JsonUtility.ToJson(restored.CaptureState()));
            Assert.IsTrue(world.Events.Any(e=>e.player==0&&e.kind=="Payment"&&e.message.Contains(world.Rules.Tech(World.CivilianRebuildingTechnology).name)),"Recovery is a real paid Research command");
            Until(simulation,()=>world.Entities.Any(e=>e.Alive&&e.owner==0&&e.Worker&&!e.def.waterborne),90,"Restored ordinary settler sources must produce real civilians");
            Assert.AreEqual(4,p.age);
            TestContext.WriteLine("Prepared age-IV political fixture: "+civilization+", Revolution "+p.revolutionAt+"s, CivilianRebuilt "+p.civilianRebuiltAt+"s; real cost and queue time; not a normal-start completion claim");
        }

        [TestCase("coast")] [TestCase("river")] [TestCase("france")] [TestCase("ottoman")]
        public void PreparedEmpireRoutePaysAndCompletesAgeVWithoutRevolution(string civilization)
        {
            var rules=Catalog.Load();var world=IndustrialArena(civilization,43,rules.Age(5).cost);
            var simulation=new MatchSimulation(world,0);Assert.IsTrue(simulation.Step());
            var job=Town(world).queue.Single(j=>j.kind=="Age");Assert.AreEqual("5",job.item);CostEquals(rules.Age(5).cost,job.paid);
            Assert.IsTrue(world.Events.Any(e=>e.player==0&&e.kind=="Payment"&&e.message.StartsWith("Advance to age 5")));
            var restored=Cold(simulation);
            for(int i=0;i<Mathf.CeilToInt((job.duration+5)/MatchSimulation.StepSeconds)&&world.Players[0].age<5;i++)
            {Assert.IsTrue(simulation.Step());Assert.IsTrue(restored.Step());}
            Assert.AreEqual(5,world.Players[0].age);Assert.AreEqual(PoliticalPhase.Empire,world.Players[0].politicalPhase);
            Assert.IsFalse(world.Events.Any(e=>e.player==0&&e.kind=="RevolutionQueued"));
            Assert.AreEqual(JsonUtility.ToJson(world.CaptureState()),JsonUtility.ToJson(restored.World.CaptureState()));
            Assert.AreEqual(JsonUtility.ToJson(simulation.CaptureState()),JsonUtility.ToJson(restored.CaptureState()));
        }

        [TestCase("coast")] [TestCase("river")] [TestCase("france")] [TestCase("ottoman")]
        public void ScarceInitialBudgetPaysTheFirstRealCivilianWithoutCratesCombatXpOrPassiveIncome(string civilization)
        {
            // Declared prepared-IV scarcity arena: all actors stay stationary and
            // damage and gather rates are zero; own crates are removed before orders.
            // No bank/factory or extra token exists. Nothing is injected later.
            var world=IndustrialArena(civilization,42,PoliticalBudget(civilization),true);
            var p=world.Players[0];var simulation=new MatchSimulation(world,0);var town=Town(world);
            var plan=new AiRevolution071(world,0,simulation.Difficulty);
            Assert.IsTrue(simulation.Step());
            Assert.IsTrue(town.queue.Any(j=>j.kind=="Revolution"));
            CostEquals(world.Rules.Tech(World.CivilianRebuildingTechnology).cost+plan.StarterWorkerCost,plan.Reserve);
            Until(simulation,()=>town.queue.Any(j=>j.kind=="Research"&&j.item==World.CivilianRebuildingTechnology),110,"Real revolution must finish and pay recovery");
            CostEquals(plan.StarterWorkerCost,plan.Reserve);
            CostEquals(plan.StarterWorkerCost,p.resources);
            var restored=Cold(simulation);
            for(int i=0;i<1300&&!world.Entities.Any(e=>e.Alive&&e.owner==0&&e.Worker&&!e.def.waterborne);i++)
            {Assert.IsTrue(simulation.Step());Assert.IsTrue(restored.Step());}
            Assert.AreEqual(PoliticalPhase.CivilianRebuilt,p.politicalPhase);
            Assert.IsTrue(world.Entities.Any(e=>e.Alive&&e.owner==0&&e.Worker&&!e.def.waterborne),"First civilian must be produced by a paid or automatic town source");
            CostEquals(new Cost(),plan.Reserve);CostEquals(new Cost(),p.gathered);CostEquals(new Cost(),p.passiveIncome);
            Assert.AreEqual(0,p.shipmentTokens);Assert.AreEqual(0,p.shipsSent);Assert.AreEqual(0,p.kills);
            Assert.IsFalse(world.Events.Any(e=>e.player==0&&e.kind=="ShipmentSent"));
            if(world.AutoRecruitSeconds(0)<=0)
                Assert.IsTrue(world.Events.Any(e=>e.player==0&&e.kind=="ProductionQueued"&&e.message.StartsWith(world.WorkerDefinition(0).id+" ×")),"The reserved first-worker cost must be spent by a real Train order");
            Assert.AreEqual(JsonUtility.ToJson(world.CaptureState()),JsonUtility.ToJson(restored.World.CaptureState()));
            Assert.AreEqual(JsonUtility.ToJson(simulation.CaptureState()),JsonUtility.ToJson(restored.CaptureState()));
        }

        [TestCase("coast")] [TestCase("river")] [TestCase("france")] [TestCase("ottoman")]
        public void FullPopulationScarcityDisbandsExactlyOneOwnSoldierAndProducesTheFundedCivilian(string civilization)
        {
            // The declared limit, houses, 38 workers and eight guards exactly
            // fill 46 slots before the first command; the explorer costs zero. All
            // funding is initial; no combat, extraction, crates or bank can help.
            var world=IndustrialArena(civilization,42,PoliticalBudget(civilization),true,true);
            var p=world.Players[0];var town=Town(world);var simulation=new MatchSimulation(world,0);
            var plan=new AiRevolution071(world,0,simulation.Difficulty);
            var weakest=world.Entities.First(e=>e.Alive&&e.owner==0&&e.Worker&&!e.def.waterborne);
            weakest.hp=world.MaxHp(weakest)*.5f; // Declared injury, before any order.
            Assert.AreEqual(46,p.usedPopulation);Assert.AreEqual(46,p.populationCap);
            Assert.IsTrue(simulation.Step());Assert.IsTrue(town.queue.Any(j=>j.kind=="Revolution"));
            Until(simulation,()=>town.queue.Any(j=>j.kind=="Research"&&j.item==World.CivilianRebuildingTechnology),110,"Full-cap revolution must pay the real rebuilding queue");
            CostEquals(plan.StarterWorkerCost,p.resources);CostEquals(plan.StarterWorkerCost,plan.Reserve);
            Assert.IsFalse(world.Events.Any(e=>e.player==0&&e.kind=="UnitDisbanded"),"No soldier is dismissed before civilians are enabled");
            var restored=Cold(simulation);
            for(int i=0;i<1500&&!world.Entities.Any(e=>e.Alive&&e.owner==0&&e.Worker&&!e.def.waterborne);i++)
            {Assert.IsTrue(simulation.Step());Assert.IsTrue(restored.Step());}
            Assert.AreEqual(PoliticalPhase.CivilianRebuilt,p.politicalPhase);
            Assert.IsTrue(world.Entities.Any(e=>e.Alive&&e.owner==0&&e.Worker&&!e.def.waterborne),"A real paid or automatic civilian must occupy the freed seat");
            var disband=world.Events.Single(e=>e.player==0&&e.kind=="UnitDisbanded");
            Assert.AreEqual(weakest.id,disband.entity);Assert.IsFalse(weakest.Alive);
            Assert.AreEqual(46,p.usedPopulation);Assert.AreEqual(0,p.reservedPopulation);
            CostEquals(new Cost(),p.resources);CostEquals(new Cost(),p.gathered);CostEquals(new Cost(),p.passiveIncome);CostEquals(new Cost(),p.refunded);
            CostEquals(PoliticalBudget(civilization),p.spent);CostEquals(new Cost(),plan.Reserve);
            Assert.AreEqual(0,p.kills);Assert.AreEqual(0,p.shipmentTokens);Assert.AreEqual(0,p.shipsSent);
            if(world.AutoRecruitSeconds(0)<=0)
                Assert.IsTrue(world.Events.Any(e=>e.player==0&&e.kind=="ProductionQueued"&&e.message.StartsWith(world.WorkerDefinition(0).id+" ×")),"The first civilian must use the public paid Train command");
            else Assert.IsTrue(world.Events.Any(e=>e.player==0&&e.kind=="AutomaticWorkerProduced"));
            Assert.IsFalse(plan.TryFreeCivilianSeat(town,new System.Collections.Generic.List<EntityState>()),"Once a worker exists no second population seat is sacrificed");
            Assert.AreEqual(JsonUtility.ToJson(world.CaptureState()),JsonUtility.ToJson(restored.World.CaptureState()));
            Assert.AreEqual(JsonUtility.ToJson(simulation.CaptureState()),JsonUtility.ToJson(restored.CaptureState()));
        }

        [TestCase("coast")] [TestCase("river")] [TestCase("france")] [TestCase("ottoman")]
        public void RealEnemyHousingLossRecoversOvercapacityOnePublicDisbandPerDecisionAndColdRestores(string civilization)
        {
            var world=AfterEnemyHousingLoss(civilization);var p=world.Players[0];var town=Town(world);
            var candidates=world.Entities.Where(e=>e.Alive&&e.owner==0&&e.def.revolutionary).ToDictionary(e=>e.id);
            int weakest=candidates.Values.OrderBy(e=>e.hp).ThenBy(e=>e.id).First().id;
            var simulation=new MatchSimulation(world,0);var plan=new AiRevolution071(world,0,simulation.Difficulty);
            Until(simulation,()=>world.Events.Count(e=>e.player==0&&e.kind=="UnitDisbanded")==3,10,"AI must release several seats while still overcapacity");
            Assert.AreEqual(43,p.usedPopulation);Assert.AreEqual(30,p.populationCap);
            CostEquals(plan.StarterWorkerCost,p.resources);CostEquals(plan.StarterWorkerCost,plan.Reserve);
            var restored=Cold(simulation);
            for(int i=0;i<1300&&!world.Entities.Any(e=>e.Alive&&e.owner==0&&e.Worker&&!e.def.waterborne);i++)
            {
                int beforeEvents=world.Events.Count,beforePopulation=p.usedPopulation;
                Assert.IsTrue(simulation.Step());Assert.IsTrue(restored.Step());
                var dismissed=world.Events.Skip(beforeEvents).Where(e=>e.player==0&&e.kind=="UnitDisbanded").ToArray();
                Assert.LessOrEqual(dismissed.Length,1,"One decision can dismiss only one real actor");
                foreach(var e in dismissed)
                {
                    Assert.IsTrue(candidates.ContainsKey(e.entity));Assert.IsFalse(candidates[e.entity].Alive);
                    Assert.AreEqual(0,candidates[e.entity].garrisonedIn);Assert.AreEqual(1,candidates[e.entity].def.population);
                    var removal=world.Events.Skip(beforeEvents).Single(d=>d.player==0&&d.kind=="Death"&&d.entity==e.entity);
                    Assert.IsTrue(removal.hasPopulation);Assert.AreEqual(beforePopulation-1,removal.population.used);Assert.AreEqual(30,removal.population.capacity);
                    Assert.AreEqual(-1,removal.target,"Dismissal has no enemy killer or combat bounty");
                    Assert.IsFalse(e.hasEconomy,"Disband must never credit or refund currency");
                }
            }
            var disbands=world.Events.Where(e=>e.player==0&&e.kind=="UnitDisbanded").ToArray();
            Assert.AreEqual(17,disbands.Length,"46 used plus one real civilian require17 legitimate releases to fit capacity30");
            Assert.AreEqual(weakest,disbands[0].entity);Assert.AreEqual(17,disbands.Select(e=>e.entity).Distinct().Count());
            Assert.IsTrue(world.Entities.Any(e=>e.Alive&&e.owner==0&&e.Worker&&!e.def.waterborne));
            Assert.AreEqual(30,p.usedPopulation);Assert.AreEqual(30,p.populationCap);Assert.AreEqual(0,p.reservedPopulation);
            CostEquals(PoliticalBudget(civilization),p.spent);CostEquals(new Cost(),p.resources);CostEquals(new Cost(),p.refunded);
            CostEquals(new Cost(),p.gathered);CostEquals(new Cost(),p.passiveIncome);CostEquals(new Cost(),plan.Reserve);
            Assert.AreEqual(0,p.kills);Assert.AreEqual(0,p.shipmentTokens);Assert.AreEqual(0,p.shipsSent);
            Assert.IsFalse(world.Events.Any(e=>e.player==0&&e.kind=="Experience"&&e.message=="Combat bounty"));
            Assert.AreEqual(1,world.Events.Count(e=>e.player==0&&e.kind=="RevolutionCompleted"));
            Assert.AreEqual(1,world.Events.Count(e=>e.player==0&&e.kind=="CivilianRebuilt"));
            Assert.IsFalse(world.Events.Any(e=>e.player==0&&e.kind=="Payment"&&e.message.Contains("Advance to age 5")));
            if(world.AutoRecruitSeconds(0)<=0)
            {
                Assert.IsTrue(world.Events.Any(e=>e.player==0&&e.kind=="ProductionQueued"&&e.message.StartsWith(world.WorkerDefinition(0).id+" ×")));
                Assert.IsTrue(world.Events.Any(e=>e.player==0&&e.kind=="UnitTrained"&&e.message==world.WorkerDefinition(0).id));
            }
            else Assert.IsTrue(world.Events.Any(e=>e.player==0&&e.kind=="AutomaticWorkerProduced"));
            Assert.IsFalse(plan.TryFreeCivilianSeat(town,new System.Collections.Generic.List<EntityState>()));
            Assert.AreEqual(JsonUtility.ToJson(world.CaptureState()),JsonUtility.ToJson(restored.World.CaptureState()));
            Assert.AreEqual(JsonUtility.ToJson(simulation.CaptureState()),JsonUtility.ToJson(restored.CaptureState()));
            TestContext.WriteLine("Prepared IV recovery after actual enemy house destruction: "+civilization+";17 public dismissals and real civilian output, not a normal-start route claim");
        }

        [Test] public void AVisibleTownThreatPreventsOvercapacityDisbandAndPreservesFundedCivilianBudget()
        {
            var world=AfterEnemyHousingLoss("coast",true);var p=world.Players[0];var town=Town(world);
            Assert.IsTrue(world.Entities.Any(e=>e.Alive&&e.owner==1&&e.def.id=="musketeer"&&world.IsVisible(0,e)&&Vector2.Distance(e.position,town.position)<34));
            var simulation=new MatchSimulation(world,0);
            for(int i=0;i<120;i++)Assert.IsTrue(simulation.Step());
            Assert.IsFalse(world.Events.Any(e=>e.player==0&&e.kind=="UnitDisbanded"));
            Assert.AreEqual(46,p.usedPopulation);Assert.AreEqual(30,p.populationCap);Assert.AreEqual(0,p.reservedPopulation);
            CostEquals(new AiRevolution071(world,0,simulation.Difficulty).StarterWorkerCost,p.resources);
            Assert.IsFalse(town.queue.Any(j=>j.kind=="Train"));
        }

        [Test] public void ARealQueuedRevolutionClearsAnOldAgePlanImmediately()
        {
            var world=IndustrialArena("coast",43,PoliticalBudget("coast"));var plan=new AiAgePlan(world,0);var town=Town(world);
            plan.Tick(town,38,8,0,false);Assert.AreEqual(5,plan.TargetAge);Assert.IsTrue(plan.Saving);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Revolution,actors=new[]{town.id}}).accepted);
            plan.Tick(town,38,8,0,false);Assert.AreEqual(0,plan.TargetAge);Assert.IsFalse(plan.Saving);CostEquals(new Cost(),plan.Reserve);
        }

        [Test] public void HiddenEnemyForcesDoNotAlterTheOwnReserveButVisibleRaidersReleaseItForDefense()
        {
            var world=IndustrialArena("coast",42,PoliticalBudget("coast"));var plan=new AiRevolution071(world,0,AiDifficultyProfile.From(world.Rules,"standard"));
            CostEquals(PoliticalBudget("coast"),plan.Reserve);
            for(int i=0;i<3;i++)
            {var hidden=world.Spawn("musketeer",1,new Vector2(110+i,100));Assert.IsFalse(world.IsVisible(0,hidden));}
            CostEquals(PoliticalBudget("coast"),plan.Reserve);
            for(int i=0;i<3;i++)
            {var raider=world.Spawn("musketeer",1,Town(world).position+new Vector2(10+i,0));Assert.IsTrue(world.IsVisible(0,raider));}
            CostEquals(new Cost(),plan.Reserve);Assert.IsFalse(plan.TryCommit(Town(world)));
        }

        [Test] public void AiShipsRealRecoveryCratesCollectsOnlyOwnSuppliesAndKeepsGatherOrdersDuringArmyReviews()
        {
            // A human-authored revolution and one explicit ordinary shipment token
            // isolate recovery from a missing-coin position. No crate, income,
            // free technology or AI-era advancement is injected after setup.
            var budget=PoliticalBudget("coast");budget.coin-=400;
            var world=IndustrialArena("coast",42,budget);var p=world.Players[0];var town=Town(world);
            p.shipmentTokens=1;
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Revolution,actors=new[]{town.id}}).accepted);
            for(int i=0;i<901&&p.politicalPhase==PoliticalPhase.Empire;i++)world.Tick(.1f);
            Assert.AreEqual(PoliticalPhase.Revolutionary,p.politicalPhase);
            var foreign=world.Spawn("crate_coin",1,town.position+new Vector2(6,4));
            var soldier=world.Entities.First(e=>e.Alive&&e.owner==0&&e.def.revolutionary);
            Assert.IsFalse(world.CanGather(soldier,foreign));
            var simulation=new MatchSimulation(world,0);Assert.IsTrue(simulation.Step());
            Assert.IsTrue(p.shipments.Any(s=>s.card=="rev_coast_engineering"),"Coin deficit selects the real engineering shipment from the active revolutionary list");
            Until(simulation,()=>world.Entities.Any(e=>e.Alive&&e.owner==0&&e.def.id=="crate_coin"),60,"Shipment must land an actual own coin crate");
            Until(simulation,()=>world.Entities.Any(e=>e.Alive&&e.owner==0&&e.def.revolutionary&&e.orders.Count>0&&e.orders.Peek().kind==OrderKind.Gather&&world.Get(e.orders.Peek().target)?.def.id=="crate_coin"),30,"Revolutionaries must gather their delivered coin");
            var collector=world.Entities.First(e=>e.Alive&&e.owner==0&&e.def.revolutionary&&e.orders.Count>0&&e.orders.Peek().kind==OrderKind.Gather&&world.Get(e.orders.Peek().target)?.def.id=="crate_coin");
            int coin=collector.orders.Peek().target;float at=world.Time;
            for(int i=0;i<90;i++)Assert.IsTrue(simulation.Step());
            Assert.Greater(world.Time-at,AiDifficultyProfile.From(world.Rules,"standard").ArmySeconds);
            Assert.IsTrue(collector.orders.Count>0&&collector.orders.Peek().kind==OrderKind.Gather&&collector.orders.Peek().target==coin,"Scheduled army review must preserve the active supply collection");
            Assert.IsFalse(world.Events.Any(e=>e.player==0&&e.kind=="Command"&&e.message.StartsWith("Gather")&&e.target==foreign.id));
            Assert.IsTrue(world.Events.Any(e=>e.player==0&&e.kind=="Gather"&&e.entity==collector.id&&e.target==coin&&e.amount>0),"Delivered coin must become real gathered income");
            var restored=Cold(simulation);
            for(int i=0;i<2000&&p.politicalPhase!=PoliticalPhase.CivilianRebuilt;i++)
            {Assert.IsTrue(simulation.Step());Assert.IsTrue(restored.Step());}
            Assert.AreEqual(PoliticalPhase.CivilianRebuilt,p.politicalPhase);
            Assert.AreEqual(JsonUtility.ToJson(world.CaptureState()),JsonUtility.ToJson(restored.World.CaptureState()));
            Assert.AreEqual(JsonUtility.ToJson(simulation.CaptureState()),JsonUtility.ToJson(restored.CaptureState()));
        }
    }
}
