using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class WaterEconomy061Tests
    {
        private World world;
        private readonly Vector2 dockPoint=new Vector2(55,28);
        private PlayerState Player=>world.Players[0];
        [SetUp] public void SetUp()
        {
            world=new World(Catalog.Load(),314159,"coast","coast",5,"river");
            Player.age=2;Player.resources=new Cost(5000,5000,5000);
            world.Fog.Reveal(0,new Vector2(64,28),35);
        }
        private EntityState Dock(bool complete=true)=>world.Spawn("dock",0,dockPoint,complete);
        private EntityState Boat()=>world.Spawn("fishing_boat",0,new Vector2(64,24));
        private EntityState Fish(Vector2? point=null)
        {
            var fish=world.Spawn("fish",-1,point??new Vector2(64,26));world.Fog.Reveal(0,fish.position,20);return fish;
        }
        private CommandResult Train(EntityState dock,string unit="fishing_boat")=>world.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{dock.id},item=unit});
        private CommandResult Gather(EntityState actor,EntityState resource)=>world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{actor.id},order=OrderKind.Gather,target=resource.id});

        [Test] public void SharedNavalRulesKeepFishingCivilianAndGunboatArmed()
        {
            var rules=world.Rules;
            Assert.AreEqual(2048,(int)Tags.Naval);
            foreach(var civ in rules.civs)
            {
                Player.civilization=civ.id;
                foreach(string id in new[]{"dock","fishing_boat","gunboat"})Assert.IsTrue(world.EntityAvailable(0,rules.Entity(id)),civ.id+" "+id);
            }
            var fishing=rules.Entity("fishing_boat");Assert.IsTrue(fishing.civilian&&fishing.fishingBoat&&fishing.waterborne);Assert.AreEqual(Tags.Naval,fishing.tags);Assert.AreEqual(70,fishing.cost.wood);
            var gun=rules.Entity("gunboat");Assert.IsTrue(gun.waterborne&&!gun.civilian&&!gun.deploy);Assert.AreEqual(3,gun.population);Assert.AreEqual(40,gun.siegeDamage);
            var fish=rules.Entity("fish");Assert.AreEqual(2500,fish.amount);Assert.AreEqual(.8f,fish.gatherRate);
        }
        [Test] public void DockNeedsAgeTwoRiverLandBankAndClearWaterFront()
        {
            Assert.AreEqual("",world.CanBuild(0,"dock",dockPoint));
            Assert.AreNotEqual("",world.CanBuild(0,"dock",new Vector2(40,28)),"Inland dock");
            Assert.AreNotEqual("",world.CanBuild(0,"dock",new Vector2(64,28)),"Dock on water");
            Assert.AreNotEqual("",world.CanBuild(0,"dock",new Vector2(55,40)),"Dock at bridge");
            Player.age=1;Assert.AreNotEqual("",world.CanBuild(0,"dock",dockPoint));
            var land=new World(world.Rules);land.Players[0].age=2;land.Players[0].resources=new Cost(5000,5000,5000);land.Fog.Reveal(0,dockPoint,30);
            Assert.AreNotEqual("",land.CanBuild(0,"dock",dockPoint));
            var artificialDock=land.Spawn("dock",0,dockPoint);Assert.IsFalse(land.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{artificialDock.id},item="fishing_boat"}).accepted);
        }
        [Test] public void BothBanksFaceTheRiverAndRequireCurrentVision()
        {
            Assert.AreEqual(Vector2.right,world.DockWaterDirection(dockPoint));Assert.AreEqual(Vector2.left,world.DockWaterDirection(new Vector2(73,28)));
            world.Fog.Tick(world.Entities);Assert.AreNotEqual("",world.CanBuild(0,"dock",new Vector2(73,20)));
        }
        [Test] public void PaidDockFoundationCancellationRefundsOnlyConfiguredFraction()
        {
            var worker=world.Spawn("villager",0,new Vector2(50,28));float before=Player.resources.wood;
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{worker.id},item="dock",point=dockPoint}).accepted);
            var dock=world.Entities.Single(e=>e.Alive&&e.owner==0&&e.def.id=="dock");
            Assert.AreEqual(before-200,Player.resources.wood);Assert.AreEqual(0,dock.construction);Assert.AreEqual(OrderKind.Build,worker.orders.Peek().kind);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.CancelBuilding,actors=new[]{dock.id}}).accepted);
            Assert.AreEqual(before-50,Player.resources.wood);Assert.IsFalse(dock.Alive);
        }
        [Test] public void ShipQueuePaysReservesPopulationAndRefundsOnCancellation()
        {
            var dock=Dock();int used=Player.usedPopulation;var resources=Player.resources;
            Assert.IsTrue(Train(dock,"gunboat").accepted);Assert.AreEqual(3,Player.reservedPopulation);Assert.AreEqual(used,Player.usedPopulation);
            Assert.AreEqual(resources.wood-150,Player.resources.wood);Assert.AreEqual(resources.coin-100,Player.resources.coin);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.CancelQueue,actors=new[]{dock.id},index=0}).accepted);
            Assert.AreEqual(0,Player.reservedPopulation);Assert.AreEqual(resources.wood,Player.resources.wood);Assert.AreEqual(resources.coin,Player.resources.coin);
        }
        [Test] public void ShipTrainingRejectsPopulationShortageAndUnfinishedDocksWithoutPayment()
        {
            var dock=Dock(false);var resources=Player.resources;
            Assert.IsFalse(Train(dock).accepted);dock.construction=1;
            Player.populationCap=Player.usedPopulation;Assert.IsFalse(Train(dock).accepted);
            Assert.AreEqual(resources.wood,Player.resources.wood);Assert.AreEqual(0,Player.reservedPopulation);Assert.AreEqual(0,dock.queue.Count);
        }
        [Test] public void PaidProductionUsesWaterExitAndReleasesReservationOnlyOnSpawn()
        {
            var dock=Dock();int used=Player.usedPopulation;
            Assert.IsTrue(Train(dock).accepted);world.TickEconomy(100);
            var boat=world.Entities.Single(e=>e.owner==0&&e.def.id=="fishing_boat");
            Assert.IsTrue(world.IsWater(boat.position));Assert.IsTrue(world.NavigationFor(boat).IsWalkable(boat.position,boat.def.radius));
            Assert.Greater(boat.position.x,dock.position.x);Assert.AreEqual(used+1,Player.usedPopulation);Assert.AreEqual(0,Player.reservedPopulation);Assert.IsEmpty(dock.queue);
        }
        [Test] public void BlockedWaterExitKeepsPaidJobAndReservationUntilItOpens()
        {
            var dock=Dock();var boatDef=world.Rules.Entity("fishing_boat");var navigation=world.NavigationFor(boatDef);
            Assert.IsTrue(Train(dock).accepted);float paidWood=Player.resources.wood;int used=Player.usedPopulation;
            navigation.SetBlocked(new Vector2(64,28),20,true);world.TickEconomy(100);
            Assert.AreEqual(1,dock.queue.Count);Assert.AreEqual("Production exit blocked",dock.status);Assert.AreEqual(1,Player.reservedPopulation);Assert.AreEqual(used,Player.usedPopulation);
            Assert.IsFalse(world.Entities.Any(e=>e.owner==0&&e.def.id=="fishing_boat"));Assert.AreEqual(paidWood,Player.resources.wood);
            navigation.SetBlocked(new Vector2(64,28),20,false);world.TickEconomy(.1f);
            Assert.IsEmpty(dock.queue);Assert.AreEqual(0,Player.reservedPopulation);Assert.AreEqual(used+1,Player.usedPopulation);Assert.AreEqual(paidWood,Player.resources.wood);
        }
        [Test] public void FishingAuthorityRejectsLandWorkersGunboatsAndLandResources()
        {
            var boat=Boat();var fish=Fish();var worker=world.Spawn("villager",0,new Vector2(56,26));var gun=world.Spawn("gunboat",0,new Vector2(66,26));var berry=world.Spawn("berry",-1,new Vector2(55,24));
            Assert.IsTrue(Gather(boat,fish).accepted);Assert.IsFalse(Gather(worker,fish).accepted);Assert.IsFalse(Gather(gun,fish).accepted);Assert.IsFalse(Gather(boat,berry).accepted);
            Assert.AreEqual(fish.id,boat.orders.Peek().target,"A rejected land gather must preserve the accepted fishing order");
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{boat.id},item="dock",point=dockPoint}).accepted);
        }
        [Test] public void FishingCreditsFoodAndDepletesFiniteFishAtTheConfiguredRate()
        {
            var boat=Boat();var fish=Fish();float before=Player.resources.food,remaining=fish.remaining;
            Assert.IsTrue(Gather(boat,fish).accepted);world.TickEconomy(2);
            Assert.AreEqual(before+1.6f,Player.resources.food,.001f);Assert.AreEqual(remaining-1.6f,fish.remaining,.001f);Assert.AreEqual(1.6f,Player.gathered.food,.001f);
            Assert.IsTrue(boat.IsWorkingAt(world.Time));Assert.AreEqual(WorkKind.Gather,boat.work.kind);
            world.FlushEconomyLog();Assert.IsTrue(world.Events.Any(e=>e.kind=="Gather"&&e.entity==boat.id&&e.target==fish.id));
        }
        [Test] public void DepletedFishNeverRedirectsTheBoatToLandFood()
        {
            foreach(var fish in world.Entities.Where(e=>e.def.id=="fish").ToArray())world.Remove(fish);
            var boat=Boat();var target=Fish();var berry=world.Spawn("berry",-1,new Vector2(56,26));
            Assert.IsTrue(Gather(boat,target).accepted);world.Remove(target);world.TickEconomy(.1f);
            Assert.IsEmpty(boat.orders);Assert.IsFalse(world.CanGather(boat,berry));
        }
        [Test] public void FishingRallyStartsPaidBoatsOnExactFishAndRejectsCrossDomainTargets()
        {
            var dock=Dock();var fish=Fish(new Vector2(64,30));var berry=world.Spawn("berry",-1,new Vector2(53,33));
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Rally,actors=new[]{dock.id},target=fish.id}).accepted);
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Rally,actors=new[]{dock.id},target=berry.id}).accepted);
            var town=world.Entities.First(e=>e.owner==0&&e.def.id=="town");
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Rally,actors=new[]{town.id},target=fish.id}).accepted);
            Assert.IsTrue(Train(dock).accepted);world.TickEconomy(100);
            var boat=world.Entities.Single(e=>e.owner==0&&e.def.fishingBoat);Assert.AreEqual(OrderKind.Gather,boat.orders.Peek().kind);Assert.AreEqual(fish.id,boat.orders.Peek().target);
        }
        [Test] public void OrdinaryShipRallyKeepsDestinationAndTravelInWater()
        {
            var dock=Dock();Vector2 rally=new Vector2(64,48);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Rally,actors=new[]{dock.id},point=rally}).accepted);
            Assert.IsTrue(Train(dock,"gunboat").accepted);world.TickEconomy(100);
            var boat=world.Entities.Single(e=>e.owner==0&&e.def.id=="gunboat");
            Assert.AreEqual(OrderKind.Move,boat.orders.Peek().kind);Vector2 goal=boat.orders.Peek().point;
            Assert.IsTrue(world.IsWater(goal));Assert.IsTrue(world.NavigationFor(boat).IsWalkable(goal,boat.def.radius));
            Assert.Less(Vector2.Distance(goal,rally),4,"A ship rally must not be projected onto a bank by land formation slots");
            for(int i=0;i<300&&boat.orders.Count>0;i++){world.Tick(.1f);Assert.IsTrue(world.IsWater(boat.position));}
            Assert.Less(Vector2.Distance(boat.position,goal),1,"A completed paid ship must physically sail to its water rally");
        }
        [Test] public void NormalRiverAiEconomyPaysForCompletedDockAndFishingIncome()
        {
            // Normal seed, workers, accounts and technology. Age-II cap is a
            // public match option; the opponent is idle to isolate peaceful
            // economic reachability from combat outcome. No grants or spawns.
            var normal=new World(Catalog.Load(),314159,"coast","coast",2,"river");
            var ai=new SkirmishAI(normal,0);bool gathered=false;int cursor=normal.Events.Count;
            for(int i=0;i<12000&&!gathered&&normal.Winner<0;i++)
            {
                ai.Tick(.1f);normal.Tick(.1f);
                for(;cursor<normal.Events.Count;cursor++)
                {
                    var ev=normal.Events[cursor];
                    if(ev.kind=="Gather"&&ev.player==0&&normal.Get(ev.entity)?.def.fishingBoat==true&&ev.amount>0)gathered=true;
                }
            }
            var own=normal.Entities.Where(e=>e.Alive&&e.owner==0).ToArray();var player=normal.Players[0];
            string state=$"t={normal.Time:0.0}, age={player.age}, workers={own.Count(e=>e.Worker)}, land army={own.Count(e=>e.Military&&!e.def.waterborne)}, funds={player.resources}, docks={own.Count(e=>e.def.id=="dock")}, fishers={own.Count(e=>e.def.fishingBoat)}, winner={normal.Winner}";
            Assert.IsTrue(gathered,"Normal river AI must reach actual fishing income within 1,200 simulated seconds. "+state);
            var dockPayment=normal.Events.FirstOrDefault(e=>e.player==0&&e.kind=="Payment"&&e.message.StartsWith("Foundation River Dock: "));
            Assert.IsNotNull(dockPayment,"The AI must buy its dock");Assert.IsTrue(dockPayment.hasEconomy);
            Assert.AreEqual(-200,dockPayment.economy.requestedDelta.wood,.001f);Assert.AreEqual(-200,dockPayment.economy.actualDelta.wood,.001f);
            Assert.AreEqual(0,dockPayment.economy.actualDelta.food,.001f);Assert.AreEqual(0,dockPayment.economy.actualDelta.coin,.001f);
            Assert.AreEqual(dockPayment.economy.balanceBefore.wood-200,dockPayment.economy.balanceAfter.wood,.001f);
            Assert.IsTrue(normal.Events.Any(e=>e.player==0&&e.kind=="BuildingComplete"&&normal.Get(e.entity)?.def.id=="dock"),"The worker must finish construction");
            Assert.IsTrue(normal.Events.Any(e=>e.player==0&&e.kind=="Payment"&&e.message.StartsWith("Fishing Boat ×")),"The AI must pay for the vessel");
            Assert.Greater(player.spent.wood,270);Assert.Greater(player.gathered.food,0);
            var firstFishing=normal.Events.First(e=>e.player==0&&e.kind=="Gather"&&normal.Get(e.entity)?.def.fishingBoat==true&&e.amount>0);
            TestContext.WriteLine("Normal river economy acceptance: "+state);
            TestContext.WriteLine($"First paid dock at {dockPayment.time:0.0}s, first fishing credit at {firstFishing.time:0.0}s: {firstFishing.amount:0.000} food, boat {firstFishing.entity}, fish {firstFishing.target}.");
        }
        [Test] public void LandSettlersCanRepairTheDockWithAnActualResourceCharge()
        {
            var dock=Dock();dock.hp-=100;var worker=world.Spawn("villager",0,new Vector2(51.5f,28));var before=Player.resources;float hp=dock.hp;
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Repair,target=dock.id}).accepted);
            world.TickEconomy(1);Assert.Greater(dock.hp,hp);Assert.Less(Player.resources.wood,before.wood);
        }
        [Test] public void NavalAiCannotBuildOrRecruitWithoutMoneyAndPaysNormalCosts()
        {
            var worker=world.Spawn("villager",0,new Vector2(50,28));var ai=new AiWaterEconomy(world,0);
            var workers=new List<EntityState>{worker};var own=new List<EntityState>{worker};var enemies=new List<EntityState>();
            Player.resources=new Cost();ai.Tick(own,workers,enemies,new Cost(),false,false);
            Assert.IsFalse(world.Entities.Any(e=>e.owner==0&&e.def.id=="dock"));Assert.AreEqual(0,Player.resources.wood);
            Player.resources=new Cost(0,200,0);ai.Tick(own,workers,enemies,new Cost(),false,false);
            var dock=world.Entities.Single(e=>e.owner==0&&e.def.id=="dock");Assert.AreEqual(0,Player.resources.wood);Assert.IsFalse(dock.Complete);
            dock.construction=1;own.Add(dock);Fish(new Vector2(64,30));ai.Tick(own,workers,enemies,new Cost(),false,false);Assert.IsEmpty(dock.queue);
            Player.resources=new Cost(0,70,0);ai.Tick(own,workers,enemies,new Cost(),false,false);
            Assert.AreEqual(1,dock.queue.Count);Assert.AreEqual("fishing_boat",dock.queue[0].item);Assert.AreEqual(0,Player.resources.wood);Assert.AreEqual(1,Player.reservedPopulation);
        }
        [Test] public void NavalAiDoesNotUseHiddenFishForRecruitmentOrOrders()
        {
            var dock=Dock();var boat=Boat();var fish=Fish(new Vector2(64,110));world.Fog.Tick(world.Entities);
            foreach(var other in world.Entities.Where(e=>e.def.id=="fish"&&e!=fish).ToArray())world.Remove(other);
            Assert.IsFalse(world.IsVisible(0,fish));var ai=new AiWaterEconomy(world,0);
            ai.Tick(new List<EntityState>{dock,boat},new List<EntityState>(),new List<EntityState>(),new Cost(),false,false);
            Assert.IsFalse(dock.queue.Any(job=>job.item=="fishing_boat"));Assert.IsEmpty(boat.orders);
        }
        [Test] public void ExhaustedFishDoNotPreventPaidGunboatReplacement()
        {
            var dock=Dock();
            foreach(var fish in world.Entities.Where(e=>e.def.id=="fish").ToArray()){fish.remaining=0;world.Remove(fish);}
            Player.resources=new Cost(0,150,100);var ai=new AiWaterEconomy(world,0);
            ai.Tick(new List<EntityState>{dock},new List<EntityState>(),new List<EntityState>(),new Cost(),false,false);
            Assert.AreEqual(1,dock.queue.Count);Assert.AreEqual("gunboat",dock.queue[0].item);
            Assert.AreEqual(0,Player.resources.wood);Assert.AreEqual(0,Player.resources.coin);Assert.AreEqual(3,Player.reservedPopulation);
            Assert.IsFalse(world.Entities.Any(e=>e.owner==0&&e.def.id=="gunboat"),"A paid queue must complete before the vessel appears");
        }
        [Test] public void InvalidFishingOrderCannotCreditFoodEvenIfInsertedByAStaleQueue()
        {
            var boat=Boat();var berry=world.Spawn("berry",-1,new Vector2(56,24));float before=Player.resources.food;
            boat.orders.Enqueue(new UnitOrder(OrderKind.Gather,berry.position,berry.id));world.TickEconomy(100);
            Assert.AreEqual(before,Player.resources.food);Assert.IsEmpty(boat.orders);
        }
    }
}
