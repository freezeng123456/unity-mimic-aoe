using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class AiCivilizationTests
    {
        [TestCase("coast","homestead","militia")]
        [TestCase("river","banking","contracts")]
        [TestCase("wayfarer","safe","armed")]
        [TestCase("highland","civil","fortress")]
        public void AiChoosesAnAuthoredCivilizationBranch(string civ,string peaceful,string threatened)
        {
            var world=new World(Catalog.Load(),42,civ,"coast",5);var plan=new AiAgePlan(world,0);
            string peacefulId=plan.ChooseBranch(false),threatenedId=plan.ChooseBranch(true);
            Assert.AreEqual(civ+"_"+peaceful+"_2",peacefulId);
            Assert.AreEqual(civ+"_"+threatened+"_2",threatenedId);
            Assert.IsTrue(world.AgeChoices(0,2).Any(c=>c.id==peacefulId));
            Assert.IsTrue(world.AgeChoices(0,2).Any(c=>c.id==threatenedId));
        }
        [Test] public void CaravanReceivesRouteAndNeverArmyAttackMove()
        {
            var w=Opening("wayfarer");var station=w.Spawn("waystation",0,new Vector2(46,28));
            var cart=w.Spawn("caravan",0,new Vector2(31,31));w.Players[0].resources=new Cost(300,0,0);
            new SkirmishAI(w,0).Tick(.1f);
            Assert.IsFalse(cart.Military);Assert.AreEqual(station.id,cart.routeStation);Assert.Greater(cart.routeLeg,0);
            Assert.IsFalse(cart.orders.Any(o=>o.kind==OrderKind.AttackMove||o.kind==OrderKind.Attack));
            Assert.IsTrue(w.Events.Any(e=>e.kind=="CaravanRoute"&&e.entity==cart.id));
            Assert.IsFalse(w.Events.Any(e=>e.kind=="Command"&&e.entity==cart.id&&e.message.Contains("Attack")));
        }
        [Test] public void CaravanProductionUsesPaidQueueInsteadOfSpawning()
        {
            var w=Opening("wayfarer");var station=w.Spawn("waystation",0,new Vector2(46,28));
            var own=Own(w);var workers=own.Where(e=>e.Worker).ToList();var p=w.Players[0];p.resources=new Cost(300,300,300);
            Cost before=p.resources;var cost=w.UnitCost(0,w.Rules.Entity("caravan"));
            new AiCivilizationEconomy(w,0).Tick(own,workers,new List<EntityState>(),new Cost(),false,false);
            Assert.AreEqual(1,station.queue.Count);Assert.AreEqual("caravan",station.queue[0].item);
            Assert.AreEqual(before.wood-cost.wood,p.resources.wood,.001f);Assert.AreEqual(before.coin-cost.coin,p.resources.coin,.001f);
            Assert.IsTrue(w.Events.Any(e=>e.kind=="Payment"&&e.entity==station.id));
            Assert.IsFalse(w.Entities.Any(e=>e.owner==0&&e.def.id=="caravan"));
        }
        [Test] public void AgeSavingsParkEmptyCaravansButNeverDiscardLoadedCargo()
        {
            var w=Opening("wayfarer");var station=w.Spawn("waystation",0,new Vector2(46,28));
            var empty=w.Spawn("caravan",0,new Vector2(31,31));var loaded=w.Spawn("caravan",0,new Vector2(34,31));
            foreach(var cart in new[]{empty,loaded})Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.TradeRoute,actors=new[]{cart.id},target=station.id}).accepted);
            // The fixture represents cargo already paid by an earlier simulation
            // step; the AI must leave this in-transit shipment untouched.
            loaded.routeLeg=2;loaded.cargoPaid=World.CaravanLoadCost;
            int cursor=w.Events.Count;
            new AiCivilizationEconomy(w,0).Tick(Own(w),Own(w).Where(e=>e.Worker).ToList(),new List<EntityState>(),new Cost(),true,false);
            Assert.AreEqual(0,empty.routeLeg);Assert.AreEqual(2,loaded.routeLeg);Assert.AreEqual(60,loaded.cargoPaid.food);
            Assert.IsFalse(w.Events.Skip(cursor).Any(e=>e.kind=="CaravanCargoLost"));
        }
        [TestCase(0)]
        [TestCase(1500)]
        public void ContinuousAiReviewsKeepTrainingWorkersDespiteAutomaticCargoLoading(float startingCoin)
        {
            var w=Opening("wayfarer");
            foreach(var resource in w.Entities.Where(e=>e.def.id.StartsWith("crate_")).ToArray())w.Remove(resource);
            var station=w.Spawn("waystation",0,new Vector2(46,28));w.Spawn("barracks",0,new Vector2(37,37));
            var carts=new[]{w.Spawn("caravan",0,new Vector2(30,29)),w.Spawn("caravan",0,new Vector2(30,31))};
            var p=w.Players[0];p.resources=new Cost(0,500,startingCoin);
            foreach(var cart in carts)Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.TradeRoute,actors=new[]{cart.id},target=station.id}).accepted);
            var town=Own(w).First(e=>e.def.id=="town");var ai=new SkirmishAI(w,0);
            // No replenishment, forced age/production progress or manual worker
            // orders after the fixture. The regular think cadence and world
            // ticks must let real gathering fund and finish paid settlers.
            for(int i=0;i<2400&&w.Winner<0;i++){ai.Tick(.1f);w.Tick(.1f);}
            int trained=w.Events.Count(e=>e.player==0&&e.kind=="Experience"&&e.entity==town.id&&e.message=="Unit trained");
            Assert.GreaterOrEqual(trained,2,"60F automatic cargo loads must not starve 100F settler queues indefinitely");
            Assert.GreaterOrEqual(w.Events.Count(e=>e.player==0&&e.kind=="Payment"&&e.entity==town.id&&e.message.StartsWith("Settler ×")),trained);
            Assert.IsFalse(w.Events.Any(e=>e.kind=="CaravanCargoLost"&&e.player==0),"Budget pauses may stop only empty carts");
            if(startingCoin>=1500)Assert.IsFalse(w.Events.Any(e=>e.kind=="CaravanLoaded"&&e.player==0&&e.time<30),"An existing coin surplus cannot justify consuming the initial food recovery");
        }
        [Test] public void LowFoodBudgetLetsActuallyPaidCargoArriveBeforeParking()
        {
            var w=Opening("wayfarer");var station=w.Spawn("waystation",0,new Vector2(46,28));
            var cart=w.Spawn("caravan",0,new Vector2(29,28));w.Players[0].resources=new Cost(60,0,1500);
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.TradeRoute,actors=new[]{cart.id},target=station.id}).accepted);
            for(int i=0;i<100&&cart.routeLeg!=2;i++)w.Tick(.1f);
            Assert.AreEqual(2,cart.routeLeg);Assert.AreEqual(60,w.Players[0].spent.food,.001f);Assert.AreEqual(0,w.Players[0].resources.food,.001f);
            var economy=new AiCivilizationEconomy(w,0);float review=0;
            for(int i=0;i<600;i++)
            {
                if(w.Time>=review){review=w.Time+1.2f;var own=Own(w);economy.Tick(own,own.Where(e=>e.Worker).ToList(),new List<EntityState>(),new Cost(100,0,0),false,false);}
                w.Tick(.1f);
            }
            Assert.AreEqual(1,w.Events.Count(e=>e.kind=="CaravanDelivered"&&e.entity==cart.id));
            Assert.AreEqual(1,w.Events.Count(e=>e.kind=="CaravanLoaded"&&e.entity==cart.id));
            Assert.IsFalse(w.Events.Any(e=>e.kind=="CaravanCargoLost"&&e.entity==cart.id));Assert.AreEqual(0,cart.routeLeg);
            Assert.AreEqual(60,w.Players[0].spent.food,.001f);
        }
        [Test] public void AssignedArtisansRemainManufacturingDuringWorkforceAndConstructionReview()
        {
            var w=Opening("highland");var workshop=w.Spawn("workshop",0,new Vector2(36,29));
            var worker=Own(w).First(e=>e.Worker);var workers=new List<EntityState>{worker};
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Manufacture,target=workshop.id}).accepted);
            new AiWorkforce(w,0).Assign(workers,new Cost(500,500,500),new List<EntityState>());
            var foundation=w.Spawn("house",0,new Vector2(34,36),false);
            new AiConstruction(w,0).ResumeOne(Own(w),workers,new List<EntityState>());
            Assert.AreEqual(OrderKind.Manufacture,worker.orders.Peek().kind);Assert.AreEqual(workshop.id,worker.orders.Peek().target);
            Assert.IsFalse(AiCivilizationEconomy.CanReassign(worker));
            Assert.IsFalse(w.Events.Any(e=>e.kind=="AIConstructionResumed"&&e.target==foundation.id));
        }
        [Test] public void UnstaffedWorkshopRecruitsBoundedArtisansWithoutResourceGrant()
        {
            var w=Opening("highland");var workshop=w.Spawn("workshop",0,new Vector2(45,40));
            var own=Own(w);var workers=own.Where(e=>e.Worker).ToList();w.Players[0].resources=new Cost();
            var economy=new AiCivilizationEconomy(w,0);economy.Tick(own,workers,new List<EntityState>(),new Cost(),false,false);
            int assigned=workers.Count(e=>e.orders.Count>0&&e.orders.Peek().kind==OrderKind.Manufacture);
            Assert.AreEqual(1,assigned);Assert.AreEqual(0,w.AssignedArtisans(workshop),"Travelling assignees cannot count as active production workers");
            for(int i=0;i<5;i++)economy.Tick(own,workers,new List<EntityState>(),new Cost(),false,false);
            Assert.AreEqual(assigned,workers.Count(e=>e.orders.Count>0&&e.orders.Peek().kind==OrderKind.Manufacture));
            Assert.AreEqual(0,w.Players[0].resources.food);Assert.AreEqual(0,w.Players[0].resources.wood);Assert.AreEqual(0,w.Players[0].resources.coin);
        }
        [Test] public void FieldEngineerKeepsPaidRepairOrderAcrossArmyReviews()
        {
            var w=Opening("highland");var engineer=w.Spawn("field_engineer",0,new Vector2(37,38));
            var cannon=w.Spawn("cannon",0,new Vector2(40,38));cannon.hp=w.MaxHp(cannon)*.5f;
            var ai=new AiCivilizationEconomy(w,0);var army=new List<EntityState>{engineer,cannon};
            Assert.IsTrue(ai.Support(engineer,army,new List<EntityState>()));
            Assert.AreEqual(OrderKind.Repair,engineer.orders.Peek().kind);Assert.AreEqual(cannon.id,engineer.orders.Peek().target);
            int cursor=w.Events.Count;Assert.IsTrue(ai.Support(engineer,army,new List<EntityState>()));
            Assert.IsFalse(w.Events.Skip(cursor).Any(e=>e.kind=="Command"),"Do not restart an active repair order each review");
        }
        static World Opening(string civ)
        {var w=new World(Catalog.Load(),42,civ,"coast",5);w.Players[0].age=2;w.Players[0].populationCap=200;return w;}
        static List<EntityState> Own(World w)=>w.Entities.Where(e=>e.Alive&&e.owner==0).ToList();
    }
}
