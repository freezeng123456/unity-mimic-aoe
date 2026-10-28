using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class AiWaterInvestment084Tests
    {
        // Funded harbors isolate policy decisions. Only NormalOpening below
        // proves the complete original-resource age/build/train/gather chain.
        static World Harbor(out EntityState dock,int age=2)
        {
            var w=new World(Catalog.Load(),314159,"coast","coast",5,MapTopology.River);
            w.Players[0].age=age;w.Players[0].resources=new Cost(5000,5000,5000);
            foreach(var e in w.Entities.Where(e=>e.def.id=="fish").ToArray())w.Remove(e);
            var point=new Vector2(55,28);w.Fog.Reveal(0,point,18);
            Assert.AreEqual("",w.CanBuild(0,"dock",point));dock=w.Spawn("dock",0,point);w.Fog.Tick(w.Entities);return w;
        }
        static List<EntityState> Own(World w)=>w.Entities.Where(e=>e.Alive&&e.owner==0).ToList();
        static void Review(World w,AiWaterEconomy ai,Cost reserve=default(Cost),bool saving=false,bool attacked=false)
        {
            var own=Own(w);var visible=w.Entities.Where(e=>w.AreEnemies(0,e.owner)&&w.IsVisible(0,e)).ToList();
            ai.Tick(own,own.Where(e=>e.Worker).ToList(),visible,reserve,saving,attacked);
        }
        static EntityState Fish(World w,Vector2 point,float amount=2500)
        {var fish=w.Spawn("fish",-1,point);fish.remaining=amount;w.Fog.Tick(w.Entities);return fish;}
        static void SamePlan(AiWaterInvestment084.Plan a,AiWaterInvestment084.Plan b)
        {
            Assert.AreEqual(a.dockId,b.dockId);Assert.AreEqual(a.survey,b.survey);Assert.AreEqual(a.buildDock,b.buildDock);Assert.AreEqual(a.dockPoint,b.dockPoint);
            Assert.AreEqual(a.nextShip,b.nextShip);Assert.AreEqual(a.fishingTarget,b.fishingTarget);Assert.AreEqual(a.reachableFish,b.reachableFish);
            Assert.AreEqual(a.foodDeficit,b.foodDeficit);Assert.AreEqual(a.fish?.id??0,b.fish?.id??0);Assert.AreEqual(a.cost,b.cost);
        }

        [TestCase(2)][TestCase(3)][TestCase(4)]
        public void EmptyVisibleRiverDoesNotAutomaticallyBuyTwoGunboatsAndLateFleet(int age)
        {
            var w=Harbor(out var dock,age);var ai=new AiWaterEconomy(w,0);int cursor=w.Events.Count;
            var plan=new AiWaterInvestment084(w,0).Evaluate(Own(w));
            Assert.AreEqual("",plan.nextShip);Assert.AreEqual(new Cost(),ai.Forecast(Own(w),false));
            for(int i=0;i<5;i++)Review(w,ai);
            Assert.IsEmpty(dock.queue);Assert.IsFalse(w.Events.Skip(cursor).Any(e=>e.player==0&&e.kind=="Payment"));
        }

        [Test] public void RepeatedDerivedPlansCannotMutateTheWorldOrCreateAnUnserializedReservation()
        {
            var w=Harbor(out _);Fish(w,new Vector2(64,25));var own=Own(w);var before=JsonUtility.ToJson(w.CaptureState());
            var policy=new AiWaterInvestment084(w,0);var first=policy.Evaluate(own);
            for(int i=0;i<4;i++)SamePlan(first,policy.Evaluate(own));
            Assert.AreEqual(before,JsonUtility.ToJson(w.CaptureState()));
            var cold=World.Restore(w.Rules,SnapshotCodec.Clone(w.CaptureState()));
            SamePlan(first,new AiWaterInvestment084(cold,0).Evaluate(Own(cold)));
        }

        [Test] public void FoodDeficitScalesFishingAndAlreadyPaidQueuesConsumeTheFiniteQuota()
        {
            var w=Harbor(out var dock);var fish=Fish(w,new Vector2(64,25));w.Spawn("barracks",0,new Vector2(38,37));
            var policy=new AiWaterInvestment084(w,0);var abundant=policy.Evaluate(Own(w));
            Assert.AreEqual(1,abundant.fishingTarget,"A rich food wallet needs only the first productive fishing vessel");
            w.Players[0].resources=new Cost(0,1000,1000);var hungry=policy.Evaluate(Own(w));
            Assert.Greater(hungry.foodDeficit,0);Assert.AreEqual(4,hungry.fishingTarget);Assert.AreEqual(fish.id,hungry.fish.id);
            for(int i=0;i<4;i++)Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{dock.id},item="fishing_boat"}).accepted);
            var queued=policy.Evaluate(Own(w));Assert.AreEqual(4,queued.fishingTarget);Assert.AreEqual("",queued.nextShip);
            Assert.AreEqual(4,w.Players[0].reservedPopulation);Assert.AreEqual(280,w.Players[0].spent.wood,.001f);
            fish.remaining=80;Assert.AreEqual(1,policy.Evaluate(Own(w)).fishingTarget,"One finite fish stock is not counted separately for each queued boat");
        }

        [Test] public void AVisibleFishAcrossASealedWaterChannelCannotFundABoat()
        {
            var w=Harbor(out var dock);var unreachable=Fish(w,new Vector2(64,41));
            Assert.IsTrue(w.IsVisible(0,unreachable));
            for(int x=0;x<128;x++)w.WaterNavigation.SetBlocked(new Vector2(x,35),0,true);
            var policy=new AiWaterInvestment084(w,0);var plan=policy.Evaluate(Own(w));
            Assert.IsNull(plan.fish);Assert.AreEqual(0,plan.fishingTarget);Assert.AreEqual("",plan.nextShip);
            Review(w,new AiWaterEconomy(w,0));Assert.IsEmpty(dock.queue);Assert.AreEqual(0,w.Players[0].spent.wood);
            w.Spawn("explorer",0,new Vector2(54,12));var reachable=Fish(w,new Vector2(64,12));
            Assert.IsTrue(w.IsVisible(0,reachable));Assert.Less(Vector2.Distance(unreachable.position,new Vector2(62.15f,28)),Vector2.Distance(reachable.position,new Vector2(62.15f,28)));
            var alternative=policy.Evaluate(Own(w));Assert.AreEqual(reachable.id,alternative.fish.id);Assert.AreEqual("fishing_boat",alternative.nextShip);
        }

        [Test] public void SeeingTheBankDoesNotCancelTheUnfinishedPhysicalSurveyBeforeFishCanBeSeen()
        {
            var w=new World(Catalog.Load(),314159,"coast","coast",2,MapTopology.River);w.Players[0].age=2;
            foreach(var e in w.Entities.Where(e=>e.def.id=="fish").ToArray())w.Remove(e);
            var worker=Own(w).First(e=>e.Worker);var shore=w.Layout.DockSites[0][0];
            var goal=w.Navigation.NearestWalkable(shore-w.DockWaterDirection(shore)*4,worker.def.radius);
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Move,point=goal}).accepted);
            for(int i=0;i<1200&&w.Fog.At(0,shore)!=VisibilityState.Visible;i++)w.Tick(.1f);
            Assert.AreEqual(VisibilityState.Visible,w.Fog.At(0,shore));Assert.Greater(Vector2.Distance(worker.position,goal),6);
            var plan=new AiWaterInvestment084(w,0).Evaluate(Own(w));
            Assert.IsTrue(plan.survey);Assert.AreEqual(shore,plan.dockPoint);Assert.IsFalse(plan.buildDock,"An empty surveyed river cannot justify paying for a dock");
            var copy=World.Restore(w.Rules,SnapshotCodec.Clone(w.CaptureState()));SamePlan(plan,new AiWaterInvestment084(copy,0).Evaluate(Own(copy)));
        }

        [Test] public void AnObservedWeaponOnTheSailingRouteCancelsGatherBeforeTheBoatEntersItsRange()
        {
            var w=Harbor(out _);var boat=w.Spawn("fishing_boat",0,new Vector2(64,20));
            w.Spawn("explorer",0,new Vector2(54,80));var fish=Fish(w,new Vector2(64,80));
            var enemy=w.Spawn("gunboat",1,new Vector2(64,50));w.Fog.Tick(w.Entities);
            Assert.IsTrue(w.IsVisible(0,fish));Assert.IsFalse(w.IsVisible(0,enemy));
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{boat.id},order=OrderKind.Gather,target=fish.id}).accepted);
            var policy=new AiWaterInvestment084(w,0);
            Assert.IsTrue(policy.FishingRouteSafe(boat,fish,new List<EntityState>{enemy}),"A hidden enemy cannot invalidate an otherwise legal public route");
            w.Spawn("explorer",0,new Vector2(54,50));w.Fog.Tick(w.Entities);Assert.IsTrue(w.IsVisible(0,enemy));
            Assert.Greater(Vector2.Distance(boat.position,enemy.position),enemy.def.range+4);
            Assert.IsFalse(policy.FishingRouteSafe(boat,fish,new List<EntityState>{enemy}),"Safe endpoints do not make an exposed intervening leg safe");
            Review(w,new AiWaterEconomy(w,0),saving:true,attacked:true);
            Assert.IsFalse(boat.orders.Count>0&&boat.orders.Peek().kind==OrderKind.Gather,"The old gather may not keep sailing through the newly observed gun");
        }

        [Test] public void HiddenFishDepletionAndHiddenFleetChangesDoNotAffectThePlanOrItsPayment()
        {
            var w=Harbor(out var dock);Fish(w,new Vector2(64,25));
            var hiddenFish=Fish(w,new Vector2(64,110));var enemy=w.Spawn("frigate",1,new Vector2(64,115));w.Fog.Tick(w.Entities);
            Assert.IsFalse(w.IsVisible(0,hiddenFish));Assert.IsFalse(w.IsVisible(0,enemy));
            var cold=World.Restore(w.Rules,SnapshotCodec.Clone(w.CaptureState()));
            cold.Get(hiddenFish.id).remaining=0;cold.Get(enemy.id).position=new Vector2(64,119);cold.Get(enemy.id).hp=1;
            SamePlan(new AiWaterInvestment084(w,0).Evaluate(Own(w)),new AiWaterInvestment084(cold,0).Evaluate(Own(cold)));
            var a=new AiWaterEconomy(w,0);var b=new AiWaterEconomy(cold,0);Review(w,a);Review(cold,b);
            Assert.AreEqual(w.Players[0].resources,cold.Players[0].resources);Assert.AreEqual(w.Players[0].spent,cold.Players[0].spent);
            Assert.AreEqual("fishing_boat",dock.queue.Single().item);Assert.AreEqual(dock.queue.Single().item,cold.Get(dock.id).queue.Single().item);
            Assert.AreEqual(dock.queue.Single().paid,cold.Get(dock.id).queue.Single().paid);Assert.AreEqual(dock.queue.Single().duration,cold.Get(dock.id).queue.Single().duration);
        }

        [TestCase(false)][TestCase(true)]
        public void WorkerEraAndMissingTownBudgetsRemainProtected(bool missingTown)
        {
            var w=Harbor(out var dock);Fish(w,new Vector2(64,25));
            var reserve=new Cost(150,100,75);
            if(missingTown)
            {
                w.Spawn("house",0,new Vector2(18,15));
                var town=Own(w).Single(e=>e.def.id=="town");w.Remove(town);reserve=worldTownCost(w);
            }
            w.Players[0].resources=new Cost(reserve.food,reserve.wood+69,reserve.coin);
            Assert.AreEqual("",w.ProductionReason(0,dock,"fishing_boat"),"The command must be affordable and housed before the protected reserve rejects spending");
            var ai=new AiWaterEconomy(w,0);int cursor=w.Events.Count;
            Review(w,ai,missingTown?new Cost():reserve);
            Assert.IsEmpty(dock.queue);Assert.IsFalse(w.Events.Skip(cursor).Any(e=>e.player==0&&e.kind=="Payment"));
            Assert.AreEqual(0,w.Players[0].spent.wood);
        }
        static Cost worldTownCost(World w)=>w.EntityCost(0,w.Rules.Entity("town"));

        [TestCase(true,false)][TestCase(false,true)][TestCase(true,true)]
        public void EraSavingAndActiveDefenseKeepTheirOriginalPurchaseGate(bool saving,bool attacked)
        {
            var w=Harbor(out var dock);Fish(w,new Vector2(64,25));var ai=new AiWaterEconomy(w,0);var before=w.Players[0].resources;
            Assert.AreEqual("fishing_boat",new AiWaterInvestment084(w,0).Evaluate(Own(w)).nextShip);
            Review(w,ai,saving:saving,attacked:attacked);Assert.IsEmpty(dock.queue);Assert.AreEqual(before,w.Players[0].resources);
            if(saving)Assert.AreEqual(new Cost(),ai.Forecast(Own(w),true));
        }

        [Test] public void VisibleNavalThreatHasARealPurposeAndPaidShipWhileHiddenThreatDoesNot()
        {
            var w=Harbor(out var dock);var enemy=w.Spawn("gunboat",1,new Vector2(64,42));w.Fog.Tick(w.Entities);
            Assert.IsTrue(w.IsVisible(0,enemy));var policy=new AiWaterInvestment084(w,0);var plan=policy.Evaluate(Own(w));
            Assert.AreEqual("gunboat",plan.nextShip);var ai=new AiWaterEconomy(w,0);Assert.AreEqual(plan.cost,ai.Forecast(Own(w),false));
            var before=w.Players[0].resources;Review(w,ai);Assert.AreEqual("gunboat",dock.queue.Single().item);
            Assert.AreEqual(before.wood-plan.cost.wood,w.Players[0].resources.wood,.001f);Assert.AreEqual(before.coin-plan.cost.coin,w.Players[0].resources.coin,.001f);
            Assert.IsTrue(w.Events.Any(e=>e.kind=="Payment"&&e.player==0&&e.entity==dock.id&&e.economy.actualDelta.wood==-150));
        }

        [Test] public void PaidQueueAndDerivedPolicyContinueIdenticallyAfterAColdCheckpoint()
        {
            var w=Harbor(out var dock);Fish(w,new Vector2(64,25));var first=new AiWaterEconomy(w,0);Review(w,first);
            Assert.AreEqual("fishing_boat",dock.queue.Single().item);
            var state=SnapshotCodec.Clone(first.CaptureState());var cold=World.Restore(w.Rules,SnapshotCodec.Clone(w.CaptureState()));
            var second=new AiWaterEconomy(cold,0);second.RestoreState(state);
            for(int i=0;i<400;i++)
            {
                if(i%12==0){Review(w,first);Review(cold,second);}
                w.Tick(.1f);cold.Tick(.1f);
            }
            Assert.IsTrue(w.Events.Any(e=>e.player==0&&e.kind=="Gather"&&w.Get(e.entity)?.def.fishingBoat==true&&e.amount>0),"Paid production must finish and physically earn food before comparing continuations");
            SamePlan(new AiWaterInvestment084(w,0).Evaluate(Own(w)),new AiWaterInvestment084(cold,0).Evaluate(Own(cold)));
            Assert.AreEqual(JsonUtility.ToJson(first.CaptureState()),JsonUtility.ToJson(second.CaptureState()));
            Assert.AreEqual(JsonUtility.ToJson(w.CaptureState()),JsonUtility.ToJson(cold.CaptureState()));
        }

        [Test] public void SameFishFreshSafeRouteReplacesTheActualOldUnsafeGatherPath()
        {
            var w=Harbor(out _);w.Spawn("explorer",0,new Vector2(75,80));
            var fish=Fish(w,new Vector2(68,80));var boat=w.Spawn("fishing_boat",0,new Vector2(59,38));
            // A physical test island has two routes. A real parked ally closes
            // the short left route, so ordinary Gather/recovery saves the right.
            // Removing a UNIT later does not change the navigation version.
            for(int z=40;z<=60;z++)for(int x=61;x<=66;x++)w.WaterNavigation.SetBlocked(new Vector2(x,z),0,true);
            var blocker=w.Spawn("gunboat",0,new Vector2(59,44));
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{blocker.id},order=OrderKind.Hold}).accepted);
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{boat.id},order=OrderKind.Gather,target=fish.id}).accepted);
            for(int i=0;i<600&&!(boat.pathAvoidsAllies&&boat.path.Any(point=>point.x>=67));i++)w.Tick(.1f);
            Assert.IsTrue(boat.pathAvoidsAllies&&boat.path.Any(point=>point.x>=67),"Require an actual public Gather/recovery route; never inject a path into the actor");
            int navigationVersion=w.WaterNavigation.Version;var previous=boat.orders.Peek();
            w.ApplyDamage(blocker,100000,DamageKind.Siege,1);Assert.IsFalse(blocker.Alive);
            w.Spawn("explorer",0,new Vector2(83,50));var enemy=w.Spawn("musketeer",1,new Vector2(81,50));w.Fog.Tick(w.Entities);
            Assert.AreEqual(navigationVersion,w.WaterNavigation.Version,"Unit death and the newly seen weapon must not force MoveCore to discard the saved route automatically");
            Assert.IsTrue(w.IsVisible(0,enemy));Assert.IsTrue(w.IsVisible(0,fish));
            var dangers=new List<EntityState>{enemy};var policy=new AiWaterInvestment084(w,0);
            Assert.IsTrue(policy.PointSafe(boat.position,dangers));
            Assert.AreSame(fish,policy.NearestFish(boat,dangers,out _,out _),"A fresh ordinary route to this same fish is now safe via the reopened left route");
            Assert.IsFalse(policy.FishingRouteSafe(boat,fish,dangers),"The retained right route still crosses the newly seen weapon coverage");
            Review(w,new AiWaterEconomy(w,0),saving:true,attacked:true);
            Assert.AreEqual(OrderKind.Gather,boat.orders.Peek().kind);Assert.AreEqual(fish.id,boat.orders.Peek().target);
            Assert.AreNotSame(previous,boat.orders.Peek(),"A fresh-route proof must replace the old order, even when the resource identity is unchanged");
            Assert.IsEmpty(boat.path);Assert.AreEqual(0,boat.detourUntil);Assert.IsFalse(boat.pathAvoidsAllies);
            Assert.IsTrue(policy.FishingRouteSafe(boat,fish,dangers));
        }

        [Test] public void FishingBoatAlreadyInsideAVisibleGunEscapesWithARealMoveAndColdContinuation()
        {
            var w=Harbor(out _);w.Spawn("explorer",0,new Vector2(54,80));var fish=Fish(w,new Vector2(64,80));
            var boat=w.Spawn("fishing_boat",0,new Vector2(64,52));var enemy=w.Spawn("gunboat",1,new Vector2(64,60));
            w.Spawn("explorer",0,new Vector2(54,60));w.Fog.Tick(w.Entities);
            Assert.IsTrue(w.IsVisible(0,enemy));Assert.Less(Vector2.Distance(boat.position,enemy.position),enemy.def.range);
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{boat.id},order=OrderKind.Gather,target=fish.id}).accepted);
            var first=new AiWaterEconomy(w,0);var saved=SnapshotCodec.Clone(w.CaptureState());
            var cold=World.Restore(w.Rules,saved);var second=new AiWaterEconomy(cold,0);second.RestoreState(SnapshotCodec.Clone(first.CaptureState()));
            var dangers=new List<EntityState>{enemy};var policy=new AiWaterInvestment084(w,0);
            Assert.IsFalse(policy.FishingRouteSafe(boat,fish,dangers),"New fishing remains prohibited from inside known coverage");
            Review(w,first,saving:true,attacked:true);Review(cold,second,saving:true,attacked:true);
            Assert.Greater(boat.orders.Count,0,"A safe outward escape must remain an actual order instead of being cleared by Stop");
            Assert.AreEqual(OrderKind.Move,boat.orders.Peek().kind,"A reachable outward retreat must not become Stop merely because its start is dangerous");
            Assert.IsTrue(policy.PointSafe(boat.orders.Peek().point,dangers));
            Assert.IsTrue(policy.EscapeWaterRouteSafe(boat,boat.orders.Peek().point,dangers));
            float before=Vector2.Distance(boat.position,enemy.position);var position=boat.position;
            w.Tick(.1f);cold.Tick(.1f);
            Assert.Greater(Vector2.Distance(boat.position,enemy.position),before+.01f,"Check actual outward movement, not only an accepted command");
            Assert.LessOrEqual(Vector2.Distance(boat.position,position),w.Speed(boat)*.1f+.0001f);
            Assert.AreEqual(JsonUtility.ToJson(w.CaptureState()),JsonUtility.ToJson(cold.CaptureState()));
            Assert.AreEqual(JsonUtility.ToJson(first.CaptureState()),JsonUtility.ToJson(second.CaptureState()));
        }

        [Test] public void NormalOpeningBridgeMapPaysForSurveyConstructionProductionAndActualFishing()
            =>NormalOpeningPaysForSurveyConstructionProductionAndActualFishing(MapTopology.Tributaries);

        [Test] public void NormalEstuaryOpeningPaysForSurveyConstructionProductionAndActualFishing()
            =>NormalOpeningPaysForSurveyConstructionProductionAndActualFishing(MapTopology.Estuary);

        static void NormalOpeningPaysForSurveyConstructionProductionAndActualFishing(string map)
        {
            var w=new World(Catalog.Load(),314159,"coast","coast",2,map);var ai=new SkirmishAI(w,0);
            Assert.AreEqual(new Cost(300,300,100),w.Players[0].resources);Assert.AreEqual(1,w.Players[0].age);Assert.IsFalse(w.Layout.RequiresTransport);
            int cursor=w.Events.Count;SimEvent gathered=null;
            for(int i=0;i<14000&&gathered==null&&w.Winner<0;i++)
            {
                ai.Tick(.1f);
                var soldiers=Own(w).Where(e=>e.Military&&!e.def.waterborne&&e.garrisonedIn==0&&(e.def.tags&Tags.Explorer)==0&&(!e.hold||e.orders.Count>0)).Select(e=>e.id).ToArray();
                if(soldiers.Length>0)Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=soldiers,order=OrderKind.Hold}).accepted);
                w.Tick(.1f);
                for(;cursor<w.Events.Count;cursor++)if(w.Events[cursor].kind=="Gather"&&w.Events[cursor].player==0&&w.Get(w.Events[cursor].entity)?.def.fishingBoat==true&&w.Events[cursor].amount>0)gathered=w.Events[cursor];
            }
            Assert.IsNotNull(gathered,$"The original-resource AI must fund real fishing: t={w.Time} age={w.Players[0].age} wallet={w.Players[0].resources}");
            Assert.IsTrue(w.Events.Any(e=>e.player==0&&e.kind=="Payment"&&e.message.StartsWith("Foundation River Dock: ")&&e.hasEconomy&&e.economy.actualDelta.wood==-200));
            Assert.IsTrue(w.Events.Any(e=>e.player==0&&e.kind=="BuildingComplete"&&w.Get(e.entity)?.def.id=="dock"));
            Assert.IsTrue(w.Events.Any(e=>e.player==0&&e.kind=="Payment"&&e.message.StartsWith("Fishing Boat ×")&&e.hasEconomy&&e.economy.actualDelta.wood==-70));
            var boat=w.Get(gathered.entity);var fish=w.Get(gathered.target);Assert.IsTrue(w.IsWater(boat.position));Assert.AreEqual("fish",fish.def.id);
            float fishBefore=fish.remaining,gatheredBefore=w.Players[0].gathered.food;
            foreach(var other in Own(w).Where(e=>e.id!=boat.id&&e.def.kind==EntityKind.Unit&&e.orders.Count>0&&e.orders.Peek().kind==OrderKind.Gather))
                Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{other.id},order=OrderKind.Stop}).accepted);
            w.TickEconomy(.1f);float income=w.Players[0].gathered.food-gatheredBefore;
            Assert.Greater(income,0);Assert.AreEqual(fishBefore-fish.remaining,income,.01f);
        }
    }
}
