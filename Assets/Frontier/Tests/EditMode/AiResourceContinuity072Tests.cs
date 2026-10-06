using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class AiResourceContinuity072Tests
    {
        static List<EntityState> Workers(World w)=>w.Entities.Where(e=>e.Alive&&e.owner==0&&e.Worker&&!e.def.waterborne).ToList();
        static List<EntityState> Own(World w)=>w.Entities.Where(e=>e.Alive&&e.owner==0).ToList();
        static List<EntityState> Enemies(World w)=>w.Entities.Where(e=>e.Alive&&w.AreEnemies(0,e.owner)&&w.IsVisible(0,e)).ToList();
        static void RemoveResourceFixture(World w)
        {
            // One-time controlled depleted-map input, never called during play.
            foreach(var e in w.Entities.Where(e=>e.def.resource==ResourceKind.Wood||e.def.resource==ResourceKind.Coin).ToArray())w.Remove(e);
            foreach(var e in w.Entities.Where(e=>e.owner==0&&(e.def.tags&Tags.Explorer)!=0).ToArray())w.Remove(e);
            // A valid, already-used one-card opening deck suppresses external
            // supplies without violating the actual cold-save contract.
            w.Players[0].deck.Clear();w.Players[0].deck.Add("field_tools");w.Players[0].sentCards.Add("field_tools");w.Players[0].shipmentTokens=0;
        }
        static void Until(MatchSimulation sim,Func<bool> predicate,float seconds,string why)
        {
            for(int i=0;i<Mathf.CeilToInt(seconds/.1f)&&!predicate();i++)if(!sim.Step())break;
            if(!predicate())
            {
                foreach(var e in Own(sim.World))TestContext.WriteLine(e.id+" "+e.def.id+" "+e.position+" "+e.status+" complete="+e.Complete);
                foreach(var e in sim.World.Events.Where(e=>e.player==0&&(e.kind=="Payment"||e.kind=="TradeDelivery"||e.kind=="RevolutionCompleted")))TestContext.WriteLine(e.time+" "+e.kind+" "+e.message);
            }
            Assert.IsTrue(predicate(),why+"; time="+sim.World.Time+" wallet="+sim.World.Players[0].resources);
        }
        static MatchSimulation Cold(MatchSimulation sim)=>MatchSimulation.Restore(
            World.Restore(sim.World.Rules,JsonUtility.FromJson<WorldSnapshot>(JsonUtility.ToJson(sim.World.CaptureState()))),
            JsonUtility.FromJson<MatchSimulationSnapshot>(JsonUtility.ToJson(sim.CaptureState())));
        static void Equal(MatchSimulation a,MatchSimulation b)
        {
            Assert.AreEqual(JsonUtility.ToJson(a.World.CaptureState()),JsonUtility.ToJson(b.World.CaptureState()),"Full cold world, actual payments, modes, paths and events");
            Assert.AreEqual(JsonUtility.ToJson(a.CaptureState()),JsonUtility.ToJson(b.CaptureState()),"Full cold controller and scheduler state");
        }

        [Test] public void EnemyDestroyedTownUsesRealFactoryWoodToPayExactly500AndColdRestoresThroughConstructionAndGather()
        {
            // Controlled Industrial economy with an existing completed factory.
            // Town destruction, subsequent income, recovery payment and worker
            // gathering all use ordinary simulation authority, with no live gifts.
            var w=new World(Catalog.Load(),7201,"coast","coast",4);var p=w.Players[0];p.age=4;p.resources=new Cost(0,450,0);
            RemoveResourceFixture(w);var factory=w.Spawn("factory",0,new Vector2(17,35));
            var oldTown=Own(w).Single(e=>e.def.id=="town");oldTown.hp=1;
            var attacker=w.Spawn("cannon",1,new Vector2(40,28));w.Fog.Reveal(1,oldTown.position,25);
            var attack=w.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{attacker.id},order=OrderKind.Attack,target=oldTown.id});Assert.IsTrue(attack.accepted,attack.reason);
            for(int i=0;i<120&&oldTown.Alive;i++)w.Tick(.1f);
            Assert.IsFalse(oldTown.Alive);Assert.IsTrue(w.Events.Any(e=>e.kind=="Damage"&&e.entity==attacker.id&&e.target==oldTown.id));
            Assert.IsTrue(w.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{attacker.id},order=OrderKind.Move,point=new Vector2(100,28)}).accepted);
            for(int i=0;i<300&&attacker.position.x<80;i++)w.Tick(.1f);
            Assert.Greater(attacker.position.x,75,"The enemy leaves a genuine safe recovery window");
            int first=w.Events.Count;float before=p.spent.wood;var sim=new MatchSimulation(w,0);sim.Step();
            Assert.AreEqual("wood",factory.factoryMode,"A real shortage selects normal factory wood production");
            Assert.AreEqual(before,p.spent.wood,.001f,"Non-core building, training and research cannot consume the existing 450W");
            var restored=Cold(sim);Equal(sim,restored);
            for(int i=0;i<1800&&!Own(w).Any(e=>e.def.id=="town"&&e.Complete);i++){Assert.IsTrue(sim.Step());Assert.IsTrue(restored.Step());}
            var town=Own(w).SingleOrDefault(e=>e.def.id=="town");Assert.IsNotNull(town);Assert.IsTrue(town.Complete,"The paid 500W foundation finishes at normal builder speed");
            var payment=w.Events.Skip(first).First(e=>e.player==0&&e.kind=="Payment"&&e.economy.requestedDelta.wood<0);
            Assert.IsTrue(payment.message.StartsWith("Foundation "+town.def.name));Assert.AreEqual(-500,payment.economy.requestedDelta.wood,.001f);
            Assert.AreEqual(1,w.Events.Skip(first).Count(e=>e.player==0&&e.kind=="Payment"&&e.message.StartsWith("Foundation "+town.def.name)),"Recovery pays once, without a duplicated 500W reserve");
            Assert.IsTrue(w.Events.Skip(first).Any(e=>e.kind=="FactoryIncome"&&e.entity==factory.id&&e.message=="wood"&&e.amount>0));
            for(int i=0;i<300;i++){sim.Step();restored.Step();}w.FlushEconomyLog();restored.World.FlushEconomyLog();Equal(sim,restored);
            Assert.IsTrue(w.Events.Any(e=>e.time>payment.time&&e.kind=="Gather"&&e.player==0&&w.Get(e.entity)?.Worker==true&&e.amount>0),"Surviving workers resume real gathering after the recovery payment");
            TestContext.WriteLine("Controlled recovery: 500W paid at "+payment.time+"s; completed "+w.Time+"s; factory wood "+p.passiveIncome.wood);
        }

        [Test] public void PaidTradePostAndLedgerBootstrapEarnRealWoodWithoutTownOrHiddenForestThenRecoverTown()
        {
            // Age-II, 1000F + 400W + 150C is the declared input. It cannot yet buy a
            // 500W Town; an actually affordable trade startup is the exception.
            var w=new World(Catalog.Load(),7202,"coast","coast",2);var p=w.Players[0];p.age=2;p.resources=new Cost(1000,400,150);RemoveResourceFixture(w);
            w.Remove(Own(w).Single(e=>e.def.id=="town"));
            var socket=w.Get(w.TradeSockets[0].entity);var workers=Workers(w);
            var barracks=w.Spawn("barracks",0,socket.position+new Vector2(9,10));
            for(int i=0;i<workers.Count;i++){workers[i].position=socket.position+new Vector2(-5-i%3,2+i/3);workers[i].previousPosition=workers[i].position;}
            w.Fog.Tick(w.Entities);w.Fog.Reveal(0,socket.position,20);
            Assert.AreEqual("",w.CanBuild(0,"trade_post",socket.position));
            var startup=new AiResourceContinuity072(w,0).Evaluate();Assert.IsTrue(startup.TradeBootstrap);Assert.AreEqual(150,startup.Reserve.coin,.001f);
            var sim=new MatchSimulation(w,0);
            Until(sim,()=>Own(w).Any(e=>e.def.id=="trade_post"),5,"The fully payable approved Post starts normally");
            for(int i=0;i<200;i++)sim.Step();
            Assert.IsFalse(Own(w).Single(e=>e.def.id=="trade_post").Complete);
            Assert.AreEqual(150,p.resources.coin,.001f,"An existing Barracks and ample food cannot spend the unpaid ledger fee while the Post is being built");Assert.IsEmpty(barracks.queue);
            Until(sim,()=>p.technologies.Contains("cargo_ledgers"),180,"AI must pay 200W Post and 200W/150C Ledger and finish both ordinary jobs");
            var post=Own(w).Single(e=>e.def.id=="trade_post");Assert.IsTrue(post.Complete);
            Assert.AreEqual(400,p.spent.wood,.001f);Assert.AreEqual(150,p.spent.coin,.001f);
            Assert.AreEqual(2,w.Events.Count(e=>e.kind=="Payment"&&e.player==0&&e.economy.requestedDelta.wood<0));
            var restored=Cold(sim);
            for(int i=0;i<9000&&!Own(w).Any(e=>e.def.id=="town"&&e.Complete);i++){Assert.IsTrue(sim.Step());Assert.IsTrue(restored.Step());}
            Assert.IsTrue(Own(w).Any(e=>e.def.id=="town"&&e.Complete),"Repeated genuine caravan visits eventually finance a normal 500W recovery");
            var deliveries=w.Events.Where(e=>e.kind=="TradeDelivery"&&e.entity==post.id&&e.message=="Wood").ToArray();Assert.IsNotEmpty(deliveries);
            Assert.IsTrue(deliveries.All(e=>Mathf.Abs(e.amount-62.5f)<.01f));Assert.AreEqual(deliveries.Sum(e=>e.amount),p.tradeIncome.wood,.01f);
            Assert.AreEqual(0,p.gathered.wood,.001f);Assert.AreEqual(0,p.passiveIncome.wood,.001f,"Trade income is its own actual ledger");
            Assert.IsFalse(Own(w).Any(e=>e.def.id=="factory"||e.def.id=="factory_wagon"),"No Industrial building may be conjured in age II");Equal(sim,restored);
        }

        [Test] public void VisibleNearDepletionReservesPayableStartupAndHiddenWoodDoesNotChangeThePlan()
        {
            var w=new World(Catalog.Load(),7203,"coast","coast",3);var p=w.Players[0];p.age=2;p.resources=new Cost(0,300,150);RemoveResourceFixture(w);
            var tree=w.Spawn("tree",-1,new Vector2(35,22));tree.remaining=8;w.Fog.Reveal(0,tree.position,12);
            var helper=new AiResourceContinuity072(w,0);var plan=helper.Evaluate();Assert.AreEqual(8,plan.VisibleWoodRemaining,.001f);Assert.IsTrue(plan.NearWoodDepletion);Assert.IsTrue(plan.NeedsWood);
            Assert.IsFalse(plan.TradeBootstrap,"300W must not sink 200W into a 400W startup with no way to finish");Assert.AreEqual(400,plan.Reserve.wood,.001f);
            var hidden=w.Spawn("tree",-1,new Vector2(112,12));hidden.remaining=100000;
            Assert.IsFalse(w.IsVisible(0,hidden));var same=helper.Evaluate();Assert.AreEqual(plan.VisibleWoodRemaining,same.VisibleWoodRemaining);Assert.AreEqual(plan.Reserve.wood,same.Reserve.wood);Assert.AreEqual(plan.NeedsWood,same.NeedsWood);
            new SkirmishAI(w,0).Tick(.1f);Assert.IsFalse(w.Events.Any(e=>e.kind=="Payment"&&e.player==0&&e.economy.requestedDelta.wood<0),"Optional expenses cannot consume the prepaid startup horizon");
        }

        [Test] public void CompletedBankIncomeCoversCurrentCoinNeedButQueuedFactoryIncomeCannotBePromised()
        {
            var w=new World(Catalog.Load(),7204,"river","coast",3);var p=w.Players[0];p.age=3;p.resources=new Cost(0,0,0);RemoveResourceFixture(w);
            w.Spawn("barracks",0,new Vector2(36,28));var bank=w.Spawn("bank",0,new Vector2(28,40));w.Spawn("bank",0,new Vector2(38,44));w.Spawn("bank",0,new Vector2(45,51));
            var plan=new AiResourceContinuity072(w,0).Evaluate();Assert.Greater(plan.CoinForecast60,0);Assert.IsFalse(plan.NeedsCoin,"An operating Bank pays the two currently affordable recruitment rounds");Assert.AreNotEqual("estate",plan.CoreBuilding);
            for(int i=0;i<600;i++)w.Tick(.1f);w.FlushEconomyLog();Assert.Greater(p.passiveIncome.coin,0);Assert.IsTrue(w.Events.Any(e=>e.kind=="BankIncome"&&e.entity==bank.id));
            var f=new World(Catalog.Load(),7205,"coast","coast",4);f.Players[0].age=4;f.Players[0].resources=new Cost(0,0,0);RemoveResourceFixture(f);
            f.Spawn("foundry",0,new Vector2(36,28));var factory=f.Spawn("factory",0,new Vector2(28,40));
            Assert.IsTrue(f.Issue(new GameCommand{player=0,kind=CommandKind.FactoryMode,actors=new[]{factory.id},item="coin"}).accepted);
            Assert.Greater(new AiResourceContinuity072(f,0).Evaluate().CoinForecast60,0);
            // Declared pending research input exercises actual production pause.
            factory.queue.Add(new ProductionJob{kind="Research",item="factory_steam_power",duration=45,paid=new Cost()});
            var paused=new AiResourceContinuity072(f,0).Evaluate();Assert.AreEqual(0,paused.CoinForecast60,.001f);Assert.IsTrue(paused.NeedsCoin);Assert.AreEqual("estate",paused.CoreBuilding);
        }

        [TestCase("tower")]
        [TestCase("mortar")]
        public void VisibleWeaponsRejectConstructionWhileTheSameHiddenWeaponDoesNotLeak(string definition)
        {
            var w=new World(Catalog.Load(),7206,"coast","coast",3);var point=new Vector2(42,32);var enemy=w.Spawn(definition,1,new Vector2(58,32));
            // The point and builder are visible; the weapon is initially hidden.
            w.Fog.Tick(w.Entities);Assert.IsFalse(w.IsVisible(0,enemy));
            var all=new List<EntityState>{enemy};Assert.IsTrue(AiResourceContinuity072.Safe(w,0,point,all));
            Assert.IsTrue(AiResourceContinuity072.TryConstructionWorker(w,0,w.Rules.Entity("town"),point,Workers(w),all,out var builder),"Hidden tower state cannot alter the builder decision");Assert.IsNotNull(builder);
            w.Fog.Reveal(0,enemy.position,2);Assert.IsTrue(w.IsVisible(0,enemy));Assert.IsFalse(AiResourceContinuity072.Safe(w,0,point,all));
            Assert.IsFalse(AiResourceContinuity072.TryConstructionWorker(w,0,w.Rules.Entity("town"),point,Workers(w),all,out _));
        }

        [Test] public void PaidFoundationRejectsSealedBuilderAndUsesReachableSurvivorWithoutSecondPayment()
        {
            var w=new World(Catalog.Load(),7207,"coast","coast",3);w.Players[0].age=2;w.Players[0].resources=new Cost(0,300,0);var workers=Workers(w);var first=workers[0];
            var point=new Vector2(46,28);w.Fog.Reveal(0,point,25);Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{first.id},item="farm",point=point}).accepted);
            var farm=Own(w).Single(e=>e.def.id=="farm");
            for(int y=0;y<128;y++)w.Navigation.SetTerrainBlocked(new Vector2(39,y),.5f,true);
            Assert.IsFalse(AiResourceContinuity072.TryConstructionWorker(w,0,farm.def,farm.position,workers,new List<EntityState>(),out _,farm.id),"A visible paid site across a sealed wall has no eligible reachable builder");
            var survivor=workers[1];survivor.position=new Vector2(50,28);survivor.previousPosition=survivor.position;w.Fog.Tick(w.Entities);
            int payments=w.Events.Count(e=>e.kind=="Payment");new AiConstruction(w,0).ResumeOne(Own(w),workers,new List<EntityState>());
            Assert.AreEqual(0,first.orders.Count,"The unreachable single Build claim is released");Assert.AreEqual(OrderKind.Build,survivor.orders.Peek().kind);Assert.AreEqual(farm.id,survivor.orders.Peek().target);
            Assert.AreEqual(payments,w.Events.Count(e=>e.kind=="Payment"));
            for(int i=0;i<600&&!farm.Complete;i++)w.Tick(.1f);Assert.IsTrue(farm.Complete);Assert.AreEqual(300,w.Players[0].spent.wood,.001f);
        }

        [Test] public void NewRepairBetweenDecisionReviewsIsStoppedAtNextFixedBoundaryWithoutChargingRecoveryWood()
        {
            var w=new World(Catalog.Load(),7208,"highland","coast",3);w.Players[0].age=3;w.Players[0].resources=new Cost(0,100,100);RemoveResourceFixture(w);w.Remove(Own(w).Single(e=>e.def.id=="town"));
            var gun=w.Spawn("cannon",0,new Vector2(30,35));gun.hp=w.MaxHp(gun)-100;var engineer=w.Spawn("field_engineer",0,new Vector2(30,33));
            var ai=new SkirmishAI(w,0);ai.Tick(.1f);int payments=w.Events.Count(e=>e.kind=="Payment");
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{engineer.id},order=OrderKind.Repair,target=gun.id}).accepted);
            float wood=w.Players[0].resources.wood;float hp=gun.hp;ai.Tick(.1f);w.Tick(.1f);
            Assert.AreEqual(wood,w.Players[0].resources.wood,.001f);Assert.AreEqual(hp,gun.hp,.001f);Assert.AreEqual(payments,w.Events.Count(e=>e.kind=="Payment"));
            Assert.IsFalse(engineer.orders.Any(o=>o.kind==OrderKind.Repair));
            w.Paused=true;var state=JsonUtility.ToJson(w.CaptureState());ai.Tick(20);w.Tick(20);Assert.AreEqual(state,JsonUtility.ToJson(w.CaptureState()),"Pause does not run a recovery decision or paid repair");
        }

        [Test] public void ConcurrentContinuousRepairsShareTheActualTradeStartupSurplusAndCannotConsumeItsReserve()
        {
            var w=new World(Catalog.Load(),7209,"coast","coast",3);var p=w.Players[0];p.age=2;p.resources=new Cost(0,400.6f,200);RemoveResourceFixture(w);
            var gun=w.Spawn("cannon",0,new Vector2(30,35));gun.hp=w.MaxHp(gun)-100;
            var a=w.Spawn("field_engineer",0,new Vector2(30,33));var b=w.Spawn("field_engineer",0,new Vector2(31,33));
            foreach(var engineer in new[]{a,b})Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{engineer.id},order=OrderKind.Repair,target=gun.id}).accepted);
            var helper=new AiResourceContinuity072(w,0);Assert.AreEqual(400,helper.Evaluate().Reserve.wood,.001f);
            helper.StopRecoveryRepairs(w.Entities);Assert.AreEqual(1,new[]{a,b}.Count(e=>e.orders.Any(o=>o.kind==OrderKind.Repair)),"Two repairers cannot each claim the same 0.6W surplus");
            for(int i=0;i<20;i++){helper.StopRecoveryRepairs(w.Entities);w.Tick(.1f);}
            Assert.GreaterOrEqual(p.resources.wood,400);Assert.Greater(gun.hp,w.MaxHp(gun)-100,"The actually affordable repair portion is paid normally");
            Assert.IsFalse(new[]{a,b}.Any(e=>e.orders.Any(o=>o.kind==OrderKind.Repair)));Assert.IsTrue(w.Events.Any(e=>e.kind=="Payment"&&e.economy.hasRepair));
        }

        [TestCase(800)]
        [TestCase(1300)]
        public void UnpaidPoliticalGoalAllowsNecessaryPaidCoinIncomeAndOnlyActuallyFundedWoodStartup(int initialWood)
        {
            var w=new World(Catalog.Load(),7210,"france","coast",5);var p=w.Players[0];p.age=4;p.resources=new Cost(4000,initialWood,0);RemoveResourceFixture(w);
            // This paid budget/startup fixture excludes camp combat. Actual
            // guardian avoidance retains its separate, unweakened fixtures.
            foreach(var guardian in w.Entities.Where(World.Guardian).ToArray())w.Remove(guardian);
            w.Spawn("farm",0,new Vector2(15,40));w.Spawn("barracks",0,new Vector2(35,28));
            foreach(var point in new[]{new Vector2(17,18),new Vector2(33,18),new Vector2(43,18),new Vector2(15,30)})w.Spawn("house",0,point);
            int count=Workers(w).Count;for(int i=count;i<30;i++)w.Spawn("coureur",0,new Vector2(29+i%5,32+i/5));
            for(int i=0;i<8;i++)w.Spawn("musketeer",0,new Vector2(30+i%4,44+i/4));
            // A declared own watchtower maintains ordinary current vision of one
            // real route socket while the paid Estate earns its first 150C.
            // An AI infantryman cannot serve as a fixture observer: its normal
            // formation controller legitimately moves it back to its muster.
            var socket=w.Get(w.TradeSockets[0].entity);w.Spawn("tower",0,socket.position+new Vector2(7,0));w.Fog.Tick(w.Entities);
            var political=new AiRevolution071(w,0,AiDifficultyProfile.From(w.Rules,"standard"));Assert.Greater(political.Reserve.wood,0);Assert.Greater(political.Reserve.coin,0);
            var sim=new MatchSimulation(w,0);
            Until(sim,()=>w.Events.Any(e=>e.kind=="Gather"&&e.player==0&&w.Get(e.target)?.def.id=="estate"&&e.amount>0),180,"An unpaid political reserve cannot prohibit the real 500W coin source on which it depends");
            Assert.IsTrue(w.Events.Any(e=>e.kind=="Payment"&&e.player==0&&e.message.StartsWith("Foundation "+w.Rules.Entity("estate").name)&&Mathf.Abs(e.economy.requestedDelta.wood+500)<.001f));
            if(initialWood==800)
            {
                Assert.IsFalse(p.technologies.Contains("cargo_ledgers"));Assert.AreEqual(300,p.resources.wood,.01f,"An 800W fixture has no fabricated ninth hundred wood for Estate+Post+Ledger");
                Assert.IsFalse(w.Events.Any(e=>e.kind=="TradeDelivery"&&e.player==0&&e.message=="Wood"));return;
            }
            Until(sim,()=>p.technologies.Contains("cargo_ledgers")&&w.Events.Any(e=>e.kind=="TradeDelivery"&&e.player==0&&e.message=="Wood"),300,"The funded fixture must pay Post+Ledger normally and receive actual wood, while the political goal is still unpaid");
            Assert.IsTrue(w.Events.Any(e=>e.kind=="Payment"&&e.player==0&&e.message.StartsWith(w.Rules.Tech("cargo_ledgers").name)&&Mathf.Abs(e.economy.requestedDelta.wood+200)<.001f));
            Assert.Greater(p.tradeIncome.wood,0);Assert.AreEqual(0,p.passiveIncome.wood);Assert.AreEqual(0,p.gathered.wood);
        }

        [Test] public void WorkforceAbandonsVisibleTowerCoveredWoodAndUsesShelterWithoutReadingAHiddenTower()
        {
            var w=new World(Catalog.Load(),7211,"coast","coast",3);RemoveResourceFixture(w);
            // Remove the opening food crate once: opportunistic nearby crate
            // collection is an existing valid priority, not a tower leak.
            foreach(var crate in w.Entities.Where(e=>e.def.id=="crate_food").ToArray())w.Remove(crate);
            var worker=Workers(w)[0];var tree=w.Spawn("tree",-1,new Vector2(36,28));
            var tower=w.Spawn("tower",1,new Vector2(55,28));w.Fog.Tick(w.Entities);Assert.IsFalse(w.IsVisible(0,tower));
            var workforce=new AiWorkforce(w,0);var workers=new List<EntityState>{worker};var enemy=new List<EntityState>{tower};
            workforce.Assign(workers,new Cost(0,1000,0),enemy);Assert.IsTrue(workforce.HasSafeSource(ResourceKind.Wood));Assert.AreEqual(tree.id,worker.orders.Peek().target,"The hidden tower cannot alter a legal gather decision");
            w.Fog.Reveal(0,tower.position,2);workforce.Assign(workers,new Cost(0,1000,0),enemy);
            Assert.IsFalse(workforce.HasSafeSource(ResourceKind.Wood));Assert.IsFalse(worker.orders.Any(o=>o.kind==OrderKind.Gather&&o.target==tree.id));
            // With no remaining safe resource, a worker inside the visible
            // envelope uses an ordinary garrison command at the real Town.
            foreach(var resource in w.Entities.Where(e=>e.def.resource!=ResourceKind.None).ToArray())w.Remove(resource);
            worker.position=new Vector2(36,28);worker.previousPosition=worker.position;worker.orders.Clear();workforce.Assign(workers,new Cost(0,1000,0),enemy);
            Assert.AreEqual(OrderKind.Garrison,worker.orders.Peek().kind);Assert.AreEqual(Own(w).Single(e=>e.def.id=="town").id,worker.orders.Peek().target);
        }

        [Test] public void HiddenGuardianCannotExcludeVisibleWoodThenRevealedGuardianUsesRealShelterAndColdContinuesWithoutEnemyMemory()
        {
            var w=new World(Catalog.Load(),7214,"coast","coast",3);RemoveResourceFixture(w);
            foreach(var guard in w.Entities.Where(World.Guardian).ToArray())w.Remove(guard);
            // This source-selection fixture excludes opportunistic opening
            // crates explicitly; their ordinary priority is tested elsewhere.
            foreach(var crate in w.Entities.Where(e=>e.def.id.StartsWith("crate_")).ToArray())w.Remove(crate);
            var worker=Workers(w)[0];worker.position=new Vector2(36,28);worker.previousPosition=worker.position;
            var tree=w.Spawn("tree",-1,new Vector2(44,28));var guardian=w.Spawn("guardian",-1,new Vector2(55,28));
            w.Fog.Tick(w.Entities);Assert.IsTrue(w.IsVisible(0,tree));Assert.IsFalse(w.IsVisible(0,guardian));
            var threats=new List<EntityState>{guardian};var oneWorker=new List<EntityState>{worker};var workforce=new AiWorkforce(w,0);
            var continuity=new AiResourceContinuity072(w,0);
            Assert.AreEqual(tree.remaining,continuity.Evaluate().VisibleWoodRemaining,.001f,"A hidden neutral unit cannot affect the full source survey");
            workforce.Assign(oneWorker,new Cost(0,1000,0),threats);
            Assert.AreEqual(OrderKind.Gather,worker.orders.Peek().kind);Assert.AreEqual(tree.id,worker.orders.Peek().target);
            w.Fog.Reveal(0,guardian.position,2);Assert.IsTrue(w.IsVisible(0,guardian));Assert.IsFalse(w.AreEnemies(0,guardian.owner));
            Assert.IsTrue(AiResourceContinuity072.EconomicThreat(w,0,guardian));
            Assert.AreEqual(0,continuity.Evaluate().VisibleWoodRemaining,.001f,"The full source survey must include a currently visible armed neutral guardian");
            workforce.Assign(oneWorker,new Cost(0,1000,0),threats);
            Assert.IsFalse(workforce.HasSafeSource(ResourceKind.Wood));Assert.IsFalse(worker.orders.Any(o=>o.kind==OrderKind.Gather&&o.target==tree.id));

            // Physically move into the known envelope through a normal command,
            // then let the civilian safety planner issue the real retreat.
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Move,point=new Vector2(46,31)}).accepted);
            for(int i=0;i<160&&worker.orders.Count>0&&worker.Alive;i++)w.Tick(.1f);
            Assert.IsTrue(worker.Alive);Assert.AreEqual(0,worker.orders.Count);Assert.IsTrue(w.IsVisible(0,guardian));
            Assert.IsFalse(AiResourceContinuity072.Safe(w,0,worker.position,threats));
            int before=w.Events.Count;workforce.Assign(oneWorker,new Cost(0,1000,0),threats);
            Assert.AreEqual(OrderKind.Garrison,worker.orders.Peek().kind);var shelter=w.Get(worker.orders.Peek().target);
            Assert.AreEqual(0,shelter.owner);Assert.IsTrue(shelter.Complete);Assert.Greater(shelter.def.garrison,shelter.occupants.Count);
            Assert.IsTrue(w.Events.Skip(before).Any(e=>e.kind=="Command"&&e.entity==worker.id&&e.target==shelter.id&&e.message.StartsWith("Garrison ×")));
            var sim=new MatchSimulation(w,0);Assert.IsTrue(sim.Step());
            Assert.IsFalse(sim.CaptureState().controllers[0].memory.Any(e=>e.id==guardian.id),"A neutral economic threat must never enter enemy-faction memory");
            var restored=Cold(sim);Equal(sim,restored);
            for(int i=0;i<900;i++){Assert.IsTrue(sim.Step());Assert.IsTrue(restored.Step());}
            Assert.IsTrue(worker.Alive);Assert.IsTrue(w.Events.Any(e=>e.kind=="Garrison"&&e.entity==worker.id&&e.target==shelter.id));
            Assert.IsFalse(w.Events.Any(e=>e.kind=="Death"&&e.entity==worker.id));Equal(sim,restored);
            Assert.IsFalse(sim.CaptureState().controllers[0].memory.Any(e=>e.id==guardian.id));
        }

        [Test] public void VisibleGuardianBlocksResourceAndBuilderCorridorDespiteSafeEndpointsAndPreservesExactSegmentBoundaries()
        {
            var w=new World(Catalog.Load(),7215,"coast","coast",3);RemoveResourceFixture(w);
            foreach(var guard in w.Entities.Where(World.Guardian).ToArray())w.Remove(guard);
            var worker=Workers(w)[0];var guardian=w.Spawn("guardian",-1,new Vector2(42,28));var tree=w.Spawn("tree",-1,new Vector2(60,28));
            w.Fog.Reveal(0,new Vector2(45,28),45);var threats=new List<EntityState>{guardian};
            // A declared navigation barrier leaves a real five-cell crossing.
            // Ordinary navigation can cross it; the guardian covers that gap.
            for(int y=0;y<128;y++)if(y<26||y>30)w.Navigation.SetBlocked(new Vector2(42,y),.45f,true);
            Assert.IsTrue(AiResourceContinuity072.Safe(w,0,worker.position,threats));Assert.IsTrue(AiResourceContinuity072.Safe(w,0,tree.position,threats));
            Assert.IsFalse(AiResourceContinuity072.SegmentSafe(w,0,worker.position,tree.position,threats));
            var ordinary=w.Navigation.FindPath(worker.position,new Vector2(56,28),worker.def.radius);
            Assert.GreaterOrEqual(ordinary.Count,2);Assert.Less((ordinary[ordinary.Count-1]-new Vector2(56,28)).sqrMagnitude,.01f);
            var surveyed=new AiResourceContinuity072(w,0).Evaluate(Own(w),new List<EntityState>{worker},threats);
            Assert.AreEqual(0,surveyed.VisibleWoodRemaining,.001f,"A safe endpoint is insufficient when every physical crossing enters a visible guardian envelope");
            var house=w.Rules.Entity("house");var point=new Vector2(60,28);
            Assert.IsFalse(AiResourceContinuity072.TryConstructionWorker(w,0,house,point,new List<EntityState>{worker},threats,out _));
            Assert.IsTrue(AiResourceContinuity072.TryConstructionWorker(w,0,house,point,new List<EntityState>{worker},new List<EntityState>(),out var selected));Assert.AreEqual(worker.id,selected.id);
            Assert.IsTrue(AiResourceContinuity072.SegmentSafe(w,0,new Vector2(20,40),new Vector2(64,40),threats),"Exact radius equality remains safe, matching Safe's strict interior test");
            Assert.IsFalse(AiResourceContinuity072.SegmentSafe(w,0,new Vector2(20,39.99f),new Vector2(64,39.99f),threats));
        }

        [Test] public void ActualOrdinaryBuildRouteRejectsASafeFilteredAlternativeWithoutMutatingPreviewAndKeepsLegalNearEdgeConstruction()
        {
            var w=new World(Catalog.Load(),7216,"coast","coast",3);RemoveResourceFixture(w);
            foreach(var guard in w.Entities.Where(World.Guardian).ToArray())w.Remove(guard);
            w.Players[0].resources=new Cost(1000,1000,1000);var worker=Workers(w)[0];var one=new List<EntityState>{worker};
            var guardian=w.Spawn("guardian",-1,new Vector2(42,37));w.Fog.Tick(w.Entities);Assert.IsTrue(w.IsVisible(0,guardian));
            var threats=new List<EntityState>{guardian};var house=w.Rules.Entity("house");var point=new Vector2(60,28);
            // Declare current site vision for this legal construction fixture;
            // the genuine CanBuild authority below must still accept it.
            w.Fog.Reveal(0,point,4);
            Assert.AreEqual("",w.CanBuild(0,house.id,point));Assert.IsTrue(AiResourceContinuity072.Safe(w,0,worker.position,threats));Assert.IsTrue(AiResourceContinuity072.Safe(w,0,point,threats));
            var state=w.Navigation.CaptureState();var preview=new GridNavigation(state.width,state.height);preview.RestoreState(state);
            foreach(var cell in w.BuildingFootprintCells(house,point))preview.SetBlocked(cell,0,true);
            var delta=worker.position-point;float reach=Mathf.Max(house.width,house.depth)*.5f+worker.def.radius+.85f;
            var goal=preview.NearestWalkable(point+delta.normalized*reach,worker.def.radius);
            Assert.IsTrue(preview.ClearLine(worker.position,goal,worker.def.radius));Assert.IsFalse(AiResourceContinuity072.SegmentSafe(w,0,worker.position,goal,threats));
            var alternative=preview.FindPath(worker.position,goal,worker.def.radius,p=>AiResourceContinuity072.Safe(w,0,p,threats),
                (a,b)=>AiResourceContinuity072.SegmentSafe(w,0,a,b,threats));
            Assert.GreaterOrEqual(alternative.Count,2);Assert.Less((alternative[alternative.Count-1]-goal).sqrMagnitude,.01f,"There really is a reachable safe alternate route, which ordinary Build does not command");
            string navigation=JsonUtility.ToJson(w.Navigation.CaptureState());int events=w.Events.Count;var wallet=w.Players[0].resources;var spent=w.Players[0].spent;
            Assert.IsFalse(AiResourceContinuity072.TryConstructionWorker(w,0,house,point,one,threats,out _));
            Assert.AreEqual(navigation,JsonUtility.ToJson(w.Navigation.CaptureState()));Assert.AreEqual(events,w.Events.Count);
            Assert.AreEqual(JsonUtility.ToJson(wallet),JsonUtility.ToJson(w.Players[0].resources));Assert.AreEqual(JsonUtility.ToJson(spent),JsonUtility.ToJson(w.Players[0].spent));

            // The human authority still accepts this visible legal foundation;
            // its actual movement demonstrates why the AI rejects the choice.
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{worker.id},item=house.id,point=point}).accepted);
            var foundation=Own(w).Single(e=>e.def.id==house.id);Assert.Less((w.WorkPoint(foundation,worker)-goal).sqrMagnitude,.0001f);
            float paid=w.Players[0].spent.wood;int paymentCount=w.Events.Count(e=>e.kind=="Payment"&&e.player==0);
            Assert.IsFalse(AiResourceContinuity072.TryConstructionWorker(w,0,house,point,one,threats,out _,foundation.id));
            Assert.AreEqual(paid,w.Players[0].spent.wood);Assert.AreEqual(paymentCount,w.Events.Count(e=>e.kind=="Payment"&&e.player==0));
            w.Tick(.1f);Assert.GreaterOrEqual(worker.path.Count,2);
            Assert.IsTrue(Enumerable.Range(1,worker.path.Count-1).Any(i=>!AiResourceContinuity072.SegmentSafe(w,0,worker.path[i-1],worker.path[i],threats)));
            w.Remove(guardian);Assert.IsTrue(AiResourceContinuity072.TryConstructionWorker(w,0,house,point,one,threats,out _,foundation.id));

            var near=new World(Catalog.Load(),7217,"coast","coast",3);RemoveResourceFixture(near);
            foreach(var guard in near.Entities.Where(World.Guardian).ToArray())near.Remove(guard);
            near.Players[0].resources=new Cost(1000,1000,1000);var builder=Workers(near)[0];builder.position=new Vector2(44.8f,28);builder.previousPosition=builder.position;
            var distant=near.Spawn("guardian",-1,new Vector2(44.8f,45));near.Fog.Tick(near.Entities);near.Fog.Reveal(0,distant.position,1);Assert.IsTrue(near.IsVisible(0,distant));
            var nearbyPoint=new Vector2(48,28);Assert.AreEqual("",near.CanBuild(0,"house",nearbyPoint));
            Assert.IsTrue(AiResourceContinuity072.TryConstructionWorker(near,0,near.Rules.Entity("house"),nearbyPoint,new List<EntityState>{builder},new List<EntityState>{distant},out var selected));Assert.AreEqual(builder.id,selected.id);
            var start=builder.position;float wood=near.Players[0].spent.wood;
            Assert.IsTrue(near.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{builder.id},item="house",point=nearbyPoint}).accepted);
            var close=Own(near).Single(e=>e.def.id=="house");Assert.LessOrEqual(near.EdgeDistance(builder,close),1.35f);float progress=close.construction;
            near.Tick(.1f);Assert.Greater(close.construction,progress);Assert.Less((builder.position-start).sqrMagnitude,.0001f);
            Assert.AreEqual(near.EntityCost(0,close.def).wood,near.Players[0].spent.wood-wood,.001f,"Legal near-edge construction still pays normally and makes actual progress");
        }

        [Test] public void DecisionSurveyRecomputesBudgetAndAcceptedBuildStopEligibilityWithoutLeakingSubsetOrNextDecision()
        {
            var w=new World(Catalog.Load(),7213,"coast","coast",3);var p=w.Players[0];p.age=2;p.resources=new Cost(1000,1000,150);RemoveResourceFixture(w);
            var worker=Workers(w)[0];foreach(var other in Workers(w).Where(e=>e.id!=worker.id).ToArray())w.Remove(other);
            var tree=w.Spawn("tree",-1,new Vector2(35,22));tree.remaining=8;w.Fog.Reveal(0,tree.position,15);
            Cost obligations=new Cost();var helper=new AiResourceContinuity072(w,0,()=>obligations);
            helper.BeginDecision();
            try
            {
                var initial=helper.Evaluate();Assert.AreEqual(8,initial.VisibleWoodRemaining,.001f);Assert.IsFalse(initial.NeedsWood);
                int unchanged=w.Events.Count;obligations=new Cost(0,2000,0);
                var saving=helper.Evaluate();Assert.AreEqual(unchanged,w.Events.Count);Assert.IsTrue(saving.NeedsWood,"Fresh obligations must change the new Plan even when the source survey is reusable");Assert.AreEqual(400,saving.Reserve.wood,.001f);
                saving.Reserve=new Cost();Assert.AreEqual(400,helper.Evaluate().Reserve.wood,.001f,"Consumer adjustments must not mutate a cached Plan");
                var point=new Vector2(46,28);w.Fog.Reveal(0,point,25);
                var build=w.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{worker.id},item="farm",point=point});Assert.IsTrue(build.accepted,build.reason);
                Assert.AreEqual(700,p.resources.wood,.001f,"The source eligibility change follows a real paid 300W Foundation");
                Assert.AreEqual(0,helper.Evaluate().VisibleWoodRemaining,.001f,"The only worker's actual Build order removes its extraction eligibility during the same decision");
                Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Stop}).accepted);
                Assert.AreEqual(8,helper.Evaluate().VisibleWoodRemaining,.001f,"The accepted normal Stop restores eligibility through the event guard");
                Assert.AreEqual(0,helper.Evaluate(Own(w),new List<EntityState>(),Enemies(w)).VisibleWoodRemaining,.001f,"A caller's explicit worker subset is always independently surveyed");
                obligations=new Cost();Assert.IsFalse(helper.Evaluate().NeedsWood,"Fresh affordable wallet/obligations and the full-world survey survive a subset call");
            }
            finally{helper.EndDecision();}
            tree.remaining=2;Assert.AreEqual(2,helper.Evaluate().VisibleWoodRemaining,.001f,"A completed decision cannot retain source data into a direct component review");
        }

        [Test] public void AffordableBankDoesNotConsumeTheOnlyCompleteWoodStartupAndSpeculativeWorkerReserveCannotLockLedger()
        {
            // Both individual expenses are currently affordable, but 400W is
            // not enough for Bank350 plus the complete Post+Ledger400 startup.
            var w=new World(Catalog.Load(),7212,"river","coast",2);var p=w.Players[0];p.age=2;p.resources=new Cost(350,400,150);RemoveResourceFixture(w);
            // Isolate startup payment and income from unrelated camp combat.
            // The real NPC avoidance/revolution fixture retains its guardians.
            foreach(var guardian in w.Entities.Where(World.Guardian).ToArray())w.Remove(guardian);
            var socket=w.Get(w.TradeSockets[0].entity);w.Spawn("tower",0,socket.position+new Vector2(7,0));w.Fog.Tick(w.Entities);
            var plan=new AiResourceContinuity072(w,0).Evaluate();Assert.AreEqual("bank",plan.CoreBuilding);Assert.IsTrue(plan.TradeBootstrap);Assert.IsTrue(plan.CoreDeferredForWood);
            var sim=new MatchSimulation(w,0);
            Until(sim,()=>Own(w).Any(e=>e.def.id=="trade_post"),5,"The competing-bank exact-wallet fixture starts its actually payable Post first");
            var afterPost=new AiResourceContinuity072(w,0).Evaluate();Assert.IsTrue(afterPost.LedgerBootstrap);Assert.IsTrue(afterPost.CoreDeferredForWood,"The paid Post retains only its unpaid Ledger fee in the combination, rather than allowing the other core to consume it");
            Until(sim,()=>p.technologies.Contains("cargo_ledgers")&&w.Events.Any(e=>e.kind=="TradeDelivery"&&e.player==0&&e.message=="Wood"),260,
                "An approved complete startup must pay and produce, rather than lose its wood to the individually affordable Bank or save forever for another hypothetical worker");
            var payments=w.Events.Where(e=>e.kind=="Payment"&&e.player==0).ToArray();
            Assert.IsTrue(payments[0].message.StartsWith("Foundation "+w.Rules.Entity("trade_post").name));Assert.AreEqual(-200,payments[0].economy.requestedDelta.wood,.001f);
            Assert.IsTrue(payments[1].message.StartsWith(w.Rules.Tech("cargo_ledgers").name));Assert.AreEqual(-200,payments[1].economy.requestedDelta.wood,.001f);Assert.AreEqual(-150,payments[1].economy.requestedDelta.coin,.001f);
            Until(sim,()=>Own(w).Any(e=>e.def.id=="bank"&&e.Complete)&&p.passiveIncome.coin>0,600,"Only real recurring trade wood may finance the deferred Bank; the paid completed Bank then earns coin normally");
            Assert.IsTrue(w.Events.Any(e=>e.kind=="Payment"&&e.player==0&&e.message.StartsWith("Foundation "+w.Rules.Entity("bank").name)));
            Assert.Greater(p.tradeIncome.wood,0);Assert.Greater(p.passiveIncome.coin,0);Assert.AreEqual(0,p.gathered.wood,.001f);
        }
    }
}
