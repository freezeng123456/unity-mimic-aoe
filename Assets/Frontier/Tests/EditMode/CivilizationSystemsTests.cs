using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public class CivilizationSystemsTests
    {
        private World world;
        private EntityState home;
        private void Setup(string civ)
        {
            var rules=JsonUtility.FromJson<RuleCatalog>(JsonUtility.ToJson(Catalog.Load()));
            world=new World(rules,civ0:civ,civ1:"coast",maxAge:5);
            foreach(var e in world.Entities.ToArray())world.Remove(e);
            home=world.Spawn("town",0,new Vector2(20,20));world.Spawn("town",1,new Vector2(106,106));
            world.Spawn("house",0,new Vector2(12,30));world.Players[0].age=3;
            world.Players[0].resources=new Cost(10000,10000,10000);world.Fog.Tick(world.Entities);
        }
        private void Steps(int n){for(int i=0;i<n;i++)world.Tick(.1f);}
        private EntityState Station()=>world.Spawn("waystation",0,new Vector2(42,20));
        private EntityState Cart()=>world.Spawn("caravan",0,new Vector2(25,20));
        private CommandResult Route(EntityState cart,EntityState station)=>world.Issue(new GameCommand{player=0,kind=CommandKind.TradeRoute,actors=new[]{cart.id},target=station.id});
        [Test] public void CaravanPaysBeforeLeavingAndEarnsOnlyAfterActualArrival()
        {
            Setup("wayfarer");var station=Station();var cart=Cart();var before=world.Players[0].resources;
            Assert.IsTrue(Route(cart,station).accepted);
            for(int i=0;i<100&&cart.routeLeg!=2;i++)Steps(1);
            Assert.AreEqual(2,cart.routeLeg);Assert.AreEqual(before.food-60,world.Players[0].resources.food,.01f);
            Assert.AreEqual(before.coin,world.Players[0].resources.coin,.01f);
            for(int i=0;i<1000&&!world.Events.Any(e=>e.kind=="CaravanDelivered");i++)Steps(1);
            var delivered=world.Events.Single(e=>e.kind=="CaravanDelivered");
            Assert.LessOrEqual(world.EdgeDistance(cart,station),1.5f);Assert.AreEqual(40+22*1.6f,delivered.amount,.01f);
            Assert.AreEqual(before.coin+delivered.amount,world.Players[0].resources.coin,.01f);Assert.IsTrue(delivered.hasEconomy);
        }
        [Test] public void CaravanCannotRepeatDeliveryWithoutReturningAndPayingAgain()
        {
            Setup("wayfarer");var station=Station();var cart=Cart();Assert.IsTrue(Route(cart,station).accepted);
            for(int i=0;i<2000&&world.Events.Count(e=>e.kind=="CaravanDelivered")<2;i++)Steps(1);
            Assert.AreEqual(2,world.Events.Count(e=>e.kind=="CaravanDelivered"));
            Assert.AreEqual(2,world.Events.Count(e=>e.kind=="CaravanLoaded"));
            var first=world.Events.First(e=>e.kind=="CaravanDelivered");var second=world.Events.Last(e=>e.kind=="CaravanDelivered");Assert.Greater(second.time-first.time,5);
        }
        [Test] public void InterceptedCargoIsLostWithoutRefundOrIncome()
        {
            Setup("wayfarer");var cart=Cart();Assert.IsTrue(Route(cart,Station()).accepted);
            for(int i=0;i<100&&cart.routeLeg!=2;i++)Steps(1);
            var balance=world.Players[0].resources;world.Remove(cart,1);Steps(100);
            Assert.AreEqual(balance,world.Players[0].resources);Assert.IsFalse(world.Events.Any(e=>e.kind=="CaravanDelivered"));
            Assert.AreEqual(1,world.Events.Count(e=>e.kind=="CaravanCargoLost"));
        }
        [Test] public void LostEndpointCancelsLoadedTripAndManualOrdersCannotCashOut()
        {
            Setup("wayfarer");var station=Station();var cart=Cart();Route(cart,station);
            for(int i=0;i<100&&cart.routeLeg!=2;i++)Steps(1);
            world.Remove(station);Steps(1);Assert.AreEqual(0,cart.routeLeg);Assert.AreEqual(1,world.Events.Count(e=>e.kind=="CaravanCargoLost"));
            Assert.AreEqual(0,world.Events.Count(e=>e.kind=="CaravanDelivered"));
        }
        [Test] public void RetargetingSameRouteDoesNotSpendOrDiscardCargo()
        {
            Setup("wayfarer");var station=Station();var cart=Cart();Route(cart,station);
            for(int i=0;i<100&&cart.routeLeg!=2;i++)Steps(1);
            var balance=world.Players[0].resources;
            for(int i=0;i<20;i++)Assert.IsTrue(Route(cart,station).accepted);
            Assert.AreEqual(balance,world.Players[0].resources);Assert.AreEqual(60,cart.cargoPaid.food);
            Assert.AreEqual(0,world.Events.Count(e=>e.kind=="CaravanCargoLost"));
        }
        [Test] public void ShortEnemyAndWrongCivilizationTradeRoutesAreRejected()
        {
            Setup("wayfarer");var cart=Cart();var shortStation=world.Spawn("waystation",0,new Vector2(29,20));
            Assert.IsFalse(Route(cart,shortStation).accepted);
            var enemyStation=world.Spawn("waystation",1,new Vector2(45,20));Assert.IsFalse(Route(cart,enemyStation).accepted);
            world.Players[0].civilization="coast";Assert.IsFalse(Route(cart,Station()).accepted);
        }
        [Test] public void TradeDistanceIsCappedAndPoorCaravansWaitWithoutDebt()
        {
            Setup("wayfarer");var cart=Cart();var far=world.Spawn("waystation",0,new Vector2(95,20));
            Assert.IsTrue(Route(cart,far).accepted);Assert.AreEqual(55,cart.routeDistance);
            world.Players[0].resources=new Cost();Steps(100);
            Assert.AreEqual(0,world.Players[0].resources.food);Assert.AreEqual(0,cart.cargoPaid.food);
            Assert.IsFalse(world.Events.Any(e=>e.kind=="CaravanLoaded"||e.kind=="CaravanDelivered"));
        }
        private EntityState Workshop()
        {
            var shop=world.Spawn("workshop",0,new Vector2(42,20));world.ApplyTechnology(0,"highland_standard_jigs");return shop;
        }
        private EntityState Artisan(EntityState shop)
        {
            var worker=world.Spawn("villager",0,new Vector2(shop.position.x+shop.def.width*.5f+1,shop.position.y));world.Fog.Tick(world.Entities);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Manufacture,target=shop.id}).accepted);return worker;
        }
        private CommandResult Prefab(EntityState shop)=>world.Issue(new GameCommand{player=0,kind=CommandKind.Manufacture,actors=new[]{shop.id},item="construction_prefab"});
        [Test] public void ManufacturingRequiresPaidOrderAndPresentWorkers()
        {
            Setup("highland");var shop=Workshop();var recipe=world.Rules.manufactureRecipes.Single();var before=world.Players[0].resources;
            Assert.IsTrue(Prefab(shop).accepted);Assert.AreEqual(before.wood-recipe.cost.wood,world.Players[0].resources.wood,.01f);
            Steps(100);Assert.AreEqual(0,shop.queue[0].elapsed);Assert.AreEqual(0,world.Players[0].prefabCharges);
            var worker=Artisan(shop);Steps(40);Assert.Greater(shop.queue[0].elapsed,0);float progress=shop.queue[0].elapsed;
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Stop}).accepted);
            Steps(30);Assert.AreEqual(progress,shop.queue[0].elapsed,.001f);
        }
        [Test] public void ManufacturingDoesNotAlsoGatherAndFinishesOnce()
        {
            Setup("highland");var shop=Workshop();var worker=Artisan(shop);Assert.IsTrue(Prefab(shop).accepted);
            var before=world.Players[0].resources;Steps(500);
            Assert.AreEqual(1,world.Players[0].prefabCharges);Assert.AreEqual(1,world.Events.Count(e=>e.kind=="ManufactureComplete"));
            Assert.AreEqual(before,world.Players[0].resources);Assert.IsFalse(world.Events.Any(e=>e.kind=="Gather"&&e.entity==worker.id));
        }
        [Test] public void ManufacturingCancellationRefundsPaidCostAndDestroyedWorkshopDoesNot()
        {
            Setup("highland");var shop=Workshop();var before=world.Players[0].resources;
            Assert.IsTrue(Prefab(shop).accepted);Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.CancelQueue,actors=new[]{shop.id},index=0}).accepted);
            Assert.AreEqual(before,world.Players[0].resources);Assert.IsTrue(Prefab(shop).accepted);
            var paidBalance=world.Players[0].resources;world.Remove(shop,1);Steps(400);
            Assert.AreEqual(paidBalance,world.Players[0].resources);Assert.AreEqual(0,world.Players[0].prefabCharges);
        }
        [Test] public void WorkshopArtilleryReservesPopulationButCannotSpawnWithoutLabor()
        {
            Setup("highland");var shop=Workshop();int initial=world.Players[0].reservedPopulation;
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{shop.id},item="culverin"}).accepted);
            Assert.AreEqual(initial+world.Rules.Entity("culverin").population,world.Players[0].reservedPopulation);
            Steps(600);Assert.IsFalse(world.Entities.Any(e=>e.Alive&&e.def.id=="culverin"));
            Artisan(shop);Steps(800);Assert.AreEqual(1,world.Entities.Count(e=>e.Alive&&e.def.id=="culverin"));Assert.AreEqual(initial,world.Players[0].reservedPopulation);
        }
        [Test] public void ManufacturingStockCapCountsQueuesAcrossWorkshops()
        {
            Setup("highland");var one=Workshop();var two=world.Spawn("workshop",0,new Vector2(42,40));world.Players[0].prefabCharges=6;
            Assert.IsTrue(Prefab(one).accepted);Assert.IsTrue(Prefab(two).accepted);Assert.IsFalse(Prefab(one).accepted);
        }
        [Test] public void PrefabPaysFullFoundationAndConsumesOnlyOneKit()
        {
            Setup("highland");world.Players[0].prefabCharges=2;var worker=world.Spawn("villager",0,new Vector2(30,35));world.Fog.Tick(world.Entities);
            var before=world.Players[0].resources;var cost=world.EntityCost(0,world.Rules.Entity("barracks"));
            var result=world.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{worker.id},item="barracks",point=new Vector2(30,42)});Assert.IsTrue(result.accepted,result.reason);
            var b=world.Entities.Last(e=>e.def.id=="barracks");Assert.AreEqual(.3f,b.construction,.0001f);Assert.IsFalse(b.Complete);
            Assert.AreEqual(before.wood-cost.wood,world.Players[0].resources.wood,.01f);Assert.AreEqual(1,world.Players[0].prefabCharges);
            world.Issue(new GameCommand{player=0,kind=CommandKind.CancelBuilding,actors=new[]{b.id}});Assert.AreEqual(1,world.Players[0].prefabCharges,"Consumed labor kit is not duplicated by cancellation");
        }
        [Test] public void CivilizationsCannotResearchOrChooseOtherCivilizationRoutes()
        {
            Setup("wayfarer");world.Players[0].age=1;
            var foreign=world.Rules.ageChoices.First(c=>c.civilization=="highland"&&c.age==2);
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.AdvanceAge,actors=new[]{home.id},choice=foreign.id}).accepted);
            var choice=world.AgeChoices(0,2).First();Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.AdvanceAge,actors=new[]{home.id},choice=choice.id}).accepted);
            Steps(1200);Assert.AreEqual(2,world.Players[0].age);Assert.IsTrue(world.Players[0].technologies.Contains(choice.tech));
            Assert.AreEqual(1,world.Events.Count(e=>e.kind=="AgeRouteChosen"));
        }
        [Test] public void ShieldProtectsOnlyAfterDeploymentAndNotAgainstSiege()
        {
            Setup("highland");var guard=world.Spawn("shield_guard",0,new Vector2(40,40));
            var toggle=new GameCommand{player=0,kind=CommandKind.ToggleShield,actors=new[]{guard.id}};Assert.IsTrue(world.Issue(toggle).accepted);
            Assert.IsFalse(world.Issue(toggle).accepted,"Cannot skip deployment time by toggling repeatedly");
            float hp=guard.hp;world.ApplyDamage(guard,20,DamageKind.Ranged,1);float first=hp-guard.hp;
            Steps(21);hp=guard.hp;world.ApplyDamage(guard,20,DamageKind.Ranged,1);Assert.AreEqual(first-7,hp-guard.hp,.01f);
            hp=guard.hp;world.ApplyDamage(guard,20,DamageKind.Siege,1);Assert.AreEqual(20*(1-guard.def.siegeResist),hp-guard.hp,.01f);
        }
        [Test] public void MovingShieldGuardLowersShieldAndWaitsBeforeMoving()
        {
            Setup("highland");var guard=world.Spawn("shield_guard",0,new Vector2(40,40));
            world.Issue(new GameCommand{player=0,kind=CommandKind.ToggleShield,actors=new[]{guard.id}});Steps(21);var start=guard.position;
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{guard.id},order=OrderKind.Move,point=new Vector2(48,40)}).accepted);
            Assert.IsFalse(guard.shielded);Steps(10);Assert.AreEqual(start,guard.position);Steps(30);Assert.Greater(Vector2.Distance(start,guard.position),1);
        }
        [Test] public void EngineerRepairSpendsResourcesAndRejectsEnemyArtillery()
        {
            Setup("highland");var gun=world.Spawn("cannon",0,new Vector2(40,40));var engineer=world.Spawn("field_engineer",0,new Vector2(42,40));gun.hp*=.5f;world.Fog.Tick(world.Entities);
            float hp=gun.hp;var before=world.Players[0].resources;
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{engineer.id},order=OrderKind.Repair,target=gun.id}).accepted);Steps(30);
            Assert.Greater(gun.hp,hp);Assert.Less(world.Players[0].resources.coin,before.coin);
            var enemy=world.Spawn("cannon",1,new Vector2(44,40));world.Fog.Tick(world.Entities);
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{engineer.id},order=OrderKind.Repair,target=enemy.id}).accepted);
        }
        [Test] public void ExplicitAttackLowersShieldBeforePursuingAnOutOfRangeEnemy()
        {
            Setup("highland");var guard=world.Spawn("shield_guard",0,new Vector2(40,40));
            var enemy=world.Spawn("villager",1,new Vector2(49,40));enemy.hold=true;world.Fog.Tick(world.Entities);
            world.Issue(new GameCommand{player=0,kind=CommandKind.ToggleShield,actors=new[]{guard.id}});Steps(21);var start=guard.position;
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{guard.id},order=OrderKind.Attack,target=enemy.id}).accepted);
            Steps(1);Assert.IsFalse(guard.shielded);Steps(10);Assert.AreEqual(start,guard.position);
            Steps(30);Assert.Greater(Vector2.Distance(start,guard.position),1);
        }
        [Test] public void EngineerStopsPayingWhenItsArtilleryTargetGarrisons()
        {
            Setup("highland");var gun=world.Spawn("cannon",0,new Vector2(24,20));
            var engineer=world.Spawn("field_engineer",0,new Vector2(25,21));gun.hp*=.5f;world.Fog.Tick(world.Entities);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{engineer.id},order=OrderKind.Repair,target=gun.id}).accepted);
            Steps(1);Assert.IsTrue(world.Events.Any(e=>e.kind=="Payment"&&e.entity==engineer.id));
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{gun.id},order=OrderKind.Garrison,target=home.id}).accepted);
            Steps(1);Assert.AreEqual(home.id,gun.garrisonedIn);Assert.IsEmpty(engineer.orders);
            float hp=gun.hp,coin=world.Players[0].resources.coin;Steps(30);
            Assert.AreEqual(hp,gun.hp);Assert.AreEqual(coin,world.Players[0].resources.coin);
        }
    }
}
