using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class WaterEconomy063Tests
    {
        private static readonly string[] WaterMaps={"river","winding","tropical","tributaries","estuary"};

        private static World Prepared(string map,int player=0)
        {
            var world=new World(Catalog.Load(),314159,"coast","coast",5,map);
            world.Players[player].age=2;world.Players[player].resources=new Cost(5000,5000,5000);
            return world;
        }
        private static Vector2 LegalDockSite(World world,int player)
        {
            foreach(var point in world.Layout.DockSites[player])
            {
                world.Fog.Reveal(player,point,18);
                if(world.CanBuild(player,"dock",point)=="")return point;
            }
            Assert.Fail($"No usable authored harbor for player {player} on {world.MapId}: "+
                string.Join("; ",world.Layout.DockSites[player].Select(p=>p+" "+world.CanBuild(player,"dock",p))));
            return Vector2.zero;
        }
        private static CommandResult Command(World world,int player,CommandKind kind,EntityState actor,string item="",Vector2 point=default(Vector2))=>
            world.Issue(new GameCommand{player=player,kind=kind,actors=new[]{actor.id},item=item,point=point});

        [TestCaseSource(nameof(WaterMaps))]
        public void BothBanksPhysicallyConstructPaidRotatedHarborsAndLaunchIntoWater(string map)
        {
            for(int player=0;player<2;player++)
            {
                var world=Prepared(map,player);Vector2 point=LegalDockSite(world,player),direction=world.DockWaterDirection(point);
                var worker=world.Spawn("villager",player,point-direction*3.3f);float wood=world.Players[player].resources.wood;
                Assert.IsTrue(world.Navigation.IsWalkable(worker.position,worker.def.radius),map+" land construction contact");
                var build=Command(world,player,CommandKind.Build,worker,"dock",point);Assert.IsTrue(build.accepted,build.reason);
                var dock=world.Entities.Single(e=>e.Alive&&e.owner==player&&e.def.id=="dock");
                Assert.AreEqual(wood-200,world.Players[player].resources.wood,.001f);
                for(int i=0;i<1000&&!dock.Complete;i++){world.Tick(.1f);Assert.IsFalse(world.IsWater(worker.position),"The builder must remain on land");}
                Assert.IsTrue(dock.Complete,$"Harbor must complete on {map} player {player}; worker {worker.status}, at {worker.position}");
                Assert.IsTrue(world.Events.Any(e=>e.kind=="BuildingComplete"&&e.entity==dock.id));
                Assert.IsTrue(Command(world,player,CommandKind.Train,dock,"gunboat").accepted);
                Assert.AreEqual(wood-350,world.Players[player].resources.wood,.001f);
                int used=world.Players[player].usedPopulation;Assert.AreEqual(3,world.Players[player].reservedPopulation);
                world.TickEconomy(100);
                var boat=world.Entities.Single(e=>e.Alive&&e.owner==player&&e.def.id=="gunboat");
                Assert.Greater(Vector2.Dot(boat.position-point,direction),World.DockQuayLength+boat.def.radius);
                Assert.IsTrue(world.IsWater(boat.position));Assert.IsTrue(world.WaterNavigation.IsWalkable(boat.position,boat.def.radius));
                Assert.IsFalse(world.BuildingFootprintContains(dock.def,point,boat.position,boat.def.radius));
                Assert.AreEqual(used+3,world.Players[player].usedPopulation);Assert.AreEqual(0,world.Players[player].reservedPopulation);
            }
        }

        [TestCaseSource(nameof(WaterMaps))]
        public void HarborQuayBlocksShipsAndRefundRemovalPreservesBankTerrain(string map)
        {
            var world=Prepared(map);Vector2 point=LegalDockSite(world,0),direction=world.DockWaterDirection(point);
            var cells=world.BuildingFootprintCells(world.Rules.Entity("dock"),point).ToArray();
            var waterCell=cells.First(p=>world.IsWater(p));var landCell=cells.First(p=>!world.IsWater(p));
            Assert.IsTrue(world.WaterNavigation.IsWalkable(waterCell,0));Assert.IsFalse(world.Navigation.IsWalkable(waterCell,0));
            Assert.IsTrue(world.Navigation.IsWalkable(landCell,0));Assert.IsFalse(world.WaterNavigation.IsWalkable(landCell,0));
            var worker=world.Spawn("villager",0,point-direction*3.3f);float before=world.Players[0].resources.wood;
            Assert.IsTrue(Command(world,0,CommandKind.Build,worker,"dock",point).accepted);
            var dock=world.Entities.Single(e=>e.Alive&&e.owner==0&&e.def.id=="dock");
            foreach(var cell in cells)
            {Assert.IsFalse(world.Navigation.IsWalkable(cell,0));Assert.IsFalse(world.WaterNavigation.IsWalkable(cell,0));}
            Assert.IsTrue(Command(world,0,CommandKind.CancelBuilding,dock).accepted);
            Assert.AreEqual(before-50,world.Players[0].resources.wood,.001f);
            Assert.IsTrue(world.WaterNavigation.IsWalkable(waterCell,0));Assert.IsFalse(world.Navigation.IsWalkable(waterCell,0));
            Assert.IsTrue(world.Navigation.IsWalkable(landCell,0));Assert.IsFalse(world.WaterNavigation.IsWalkable(landCell,0));
        }

        [TestCaseSource(nameof(WaterMaps))]
        public void BlockedCurvedWaterExitRetainsPaidQueueAndPopulationUntilReopened(string map)
        {
            var world=Prepared(map);var point=LegalDockSite(world,0);var dock=world.Spawn("dock",0,point);
            Assert.IsTrue(Command(world,0,CommandKind.Train,dock,"fishing_boat").accepted);float paid=world.Players[0].resources.wood;
            // Every authored water cell is sealed through the obstacle layer;
            // reopening it must not remove the independent coast terrain.
            var water=new List<Vector2>();for(int y=1;y<127;y++)for(int x=1;x<127;x++)
            {var cell=new Vector2(x,y);if(world.IsWater(cell)){water.Add(cell);world.WaterNavigation.SetBlocked(cell,0,true);}}
            world.TickEconomy(100);Assert.AreEqual(1,dock.queue.Count);Assert.AreEqual(1,world.Players[0].reservedPopulation);
            Assert.AreEqual("Production exit blocked",dock.status);Assert.AreEqual(paid,world.Players[0].resources.wood);
            foreach(var cell in water)world.WaterNavigation.SetBlocked(cell,0,false);
            foreach(var cell in world.BuildingFootprintCells(dock.def,dock.position))world.WaterNavigation.SetBlocked(cell,0,true);
            world.TickEconomy(.1f);Assert.IsEmpty(dock.queue);Assert.AreEqual(0,world.Players[0].reservedPopulation);
            var boat=world.Entities.Single(e=>e.Alive&&e.owner==0&&e.def.fishingBoat);
            Assert.IsTrue(world.WaterNavigation.IsWalkable(boat.position,boat.def.radius));Assert.IsTrue(world.IsWater(boat.position));
            Assert.AreEqual(paid,world.Players[0].resources.wood);
        }

        [Test] public void WindingHarborFootprintAndPickingUseTheBankNormalRatherThanAnAxisAlignedBox()
        {
            var world=Prepared("winding");Vector2 point=Vector2.zero,direction=Vector2.zero;bool found=false;
            // AI harbors favor nearby sites and can coincide with a vertical
            // tangent at a bend. Deliberately survey an oblique legal bank to
            // exercise the human-placement and rotated-footprint contract.
            for(int z=12;z<=116&&!found;z++)for(int x=12;x<=116&&!found;x++)
            {
                Vector2 candidate=new Vector2(x,z),normal=world.DockWaterDirection(candidate);
                if(Mathf.Abs(normal.y)<.25f)continue;
                world.Fog.Reveal(0,candidate,18);
                if(world.CanBuild(0,"dock",candidate)!="")continue;
                point=candidate;direction=normal;found=true;
            }
            Assert.IsTrue(found,"The winding map must offer a legal harbor on a visibly slanted bank");
            Assert.Greater(Mathf.Abs(direction.y),.08f,"The tested harbor must have a visibly oblique bank");
            var definition=world.Rules.Entity("dock");var corners=world.BuildingFootprintCorners(definition,point);
            Assert.AreEqual(8,corners.Length);Assert.IsTrue(world.BuildingFootprintContains(definition,point,point+direction*4));
            Assert.IsFalse(world.BuildingFootprintContains(definition,point,point-direction*4));
            var worker=world.Spawn("villager",0,point-direction*3.3f);float wood=world.Players[0].resources.wood;
            var result=Command(world,0,CommandKind.Build,worker,"dock",point);Assert.IsTrue(result.accepted,result.reason);
            var dock=world.Entities.Single(e=>e.Alive&&e.owner==0&&e.def.id=="dock");
            Assert.AreEqual(wood-200,world.Players[0].resources.wood,.001f);
            Assert.AreEqual(point,dock.position,"Integer human placement must preserve the surveyed bank orientation");
            Assert.Less(world.EdgeDistance(worker,dock),1.5f,"Land construction distance must use the rotated warehouse");
            world.TickEconomy(1);Assert.Greater(dock.construction,0);Assert.IsFalse(world.IsWater(worker.position));
            var farPierContact=new EntityState{def=worker.def,position=point+direction*5.5f};
            Assert.Greater(world.DockLandEdgeDistance(farPierContact,dock),2,"The water pier is not a land construction contact");
        }

        [Test] public void HarborFoundationCannotTrapABoatJustOutsideThePierMeshInItsNavigationEnvelope()
        {
            var world=Prepared("river");Vector2 point=LegalDockSite(world,0),direction=world.DockWaterDirection(point);
            var boat=world.Spawn("fishing_boat",0,point+direction*6.05f);
            Assert.IsFalse(world.BuildingFootprintContains(world.Rules.Entity("dock"),point,boat.position,boat.def.radius),"The boat clears the visible pier");
            Assert.IsTrue(world.WaterNavigation.IsWalkable(boat.position,boat.def.radius));
            var worker=world.Spawn("villager",0,point-direction*3.3f);float wood=world.Players[0].resources.wood;
            Assert.IsFalse(Command(world,0,CommandKind.Build,worker,"dock",point).accepted);
            Assert.AreEqual(wood,world.Players[0].resources.wood);Assert.IsTrue(world.WaterNavigation.IsWalkable(boat.position,boat.def.radius));
            Assert.IsFalse(world.Entities.Any(e=>e.Alive&&e.owner==0&&e.def.id=="dock"));
        }

        [Test] public void ThreatenedFishingChangesToKnownSafeFishAndDoesNotKeepCreditingTheDangerousWorksite()
        {
            var world=Prepared("winding");foreach(var fish in world.Entities.Where(e=>e.def.id=="fish").ToArray())world.Remove(fish);
            Vector2 first=world.Layout.PatrolPoints[0],safe=world.Layout.PatrolPoints[1];
            var boat=world.Spawn("fishing_boat",0,first);var exposed=world.Spawn("fish",-1,first);var safeFish=world.Spawn("fish",-1,safe);
            var enemy=world.Spawn("gunboat",1,world.WaterNavigation.NearestWalkable(first+(safe-first).normalized*5,.9f));
            world.Fog.Reveal(0,first,20);world.Fog.Reveal(0,safe,20);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{boat.id},order=OrderKind.Gather,target=exposed.id}).accepted);
            world.TickEconomy(1);float food=world.Players[0].resources.food;
            new AiWaterEconomy(world,0).Tick(new List<EntityState>{boat},new List<EntityState>(),new List<EntityState>{enemy},new Cost(),false,true);
            Assert.AreEqual(OrderKind.Gather,boat.orders.Peek().kind);Assert.AreEqual(safeFish.id,boat.orders.Peek().target);
            world.TickEconomy(1);Assert.AreEqual(food,world.Players[0].resources.food,"A switched target cannot credit its previous nearby fishing site");
            Assert.AreEqual(WorkKind.Gather,boat.work.kind,"The last recorded work remains an audit record rather than a new credit");
        }

        [Test] public void NavalAiRetargetsTheExactVisibleEnemyWhenTheOldTargetDies()
        {
            var world=Prepared("winding");Vector2 point=world.Layout.PatrolPoints[0];
            var boat=world.Spawn("gunboat",0,point);var first=world.Spawn("gunboat",1,world.WaterNavigation.NearestWalkable(point+Vector2.up*5,.9f));
            var second=world.Spawn("gunboat",1,world.WaterNavigation.NearestWalkable(point+Vector2.down*8,.9f));
            world.Fog.Reveal(0,point,25);var ai=new AiWaterEconomy(world,0);var own=new List<EntityState>{boat};var enemies=new List<EntityState>{first,second};
            ai.Tick(own,new List<EntityState>(),enemies,new Cost(),false,true);Assert.AreEqual(first.id,boat.orders.Peek().target);
            world.Remove(first,0);world.Tick(5.1f);ai.Tick(own,new List<EntityState>(),enemies,new Cost(),false,true);
            Assert.AreEqual(OrderKind.Attack,boat.orders.Peek().kind);Assert.AreEqual(second.id,boat.orders.Peek().target);
        }

        [Test] public void LandRangedArmyFiresAtAVisibleHarborPierWithoutAdvancingTowardItsWarehouse()
        {
            var world=Prepared("river");Vector2 dockPoint=new Vector2(73,32),armyPoint=new Vector2(59,40);
            world.Players[1].age=2;world.Players[1].resources=new Cost(5000,5000,5000);world.Fog.Reveal(1,dockPoint,18);
            Assert.AreEqual("",world.CanBuild(1,"dock",dockPoint),"The test harbor must be legally placeable beside the crossing");
            var dock=world.Spawn("dock",1,dockPoint);var army=world.Spawn("musketeer",0,armyPoint);world.Fog.Tick(world.Entities);
            Assert.IsTrue(world.Navigation.IsWalkable(armyPoint,army.def.radius));Assert.IsTrue(world.IsBridge(armyPoint));
            Assert.IsTrue(world.IsVisible(0,dock));
            Assert.Greater(world.DockLandEdgeDistance(army,dock),army.def.range,"Warehouse alone must be beyond weapon range");
            Assert.Less(world.EdgeDistance(army,dock),army.def.range,"The selected pier is part of the military target");
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{army.id},order=OrderKind.Attack,target=dock.id}).accepted);
            float health=dock.hp;for(int tick=0;tick<40;tick++)world.Tick(.1f);
            Assert.AreEqual(armyPoint,army.position,"An in-range harbor pier must not trigger movement toward the warehouse");
            Assert.Less(dock.hp,health);Assert.IsTrue(world.Events.Any(e=>e.kind=="Fire"&&e.entity==army.id&&e.target==dock.id));
            Assert.IsTrue(world.Events.Any(e=>e.kind=="Damage"&&e.entity==army.id&&e.target==dock.id));
        }

        [Test] public void FishingAiCancelsKnownDangerousGatherBeforeTheBoatEntersEnemyRangeWhenNoSafeFishRemain()
        {
            foreach(bool blockRetreat in new[]{false,true})
            {
                var world=Prepared("river");foreach(var fish in world.Entities.Where(e=>e.def.id=="fish").ToArray())world.Remove(fish);
                var target=world.Spawn("fish",-1,new Vector2(64,55));var boat=world.Spawn("fishing_boat",0,new Vector2(64,80));
                var enemy=world.Spawn("gunboat",1,target.position);var enemies=new List<EntityState>{enemy};
                // A physical allied scout reports the fish and enemy. The AI
                // itself gains neither additional sight nor hidden targets.
                world.Spawn("explorer",0,new Vector2(54,55));
                if(blockRetreat)
                    foreach(var point in world.Layout.PatrolPoints)
                    {
                        enemies.Add(world.Spawn("gunboat",1,point));
                        world.Spawn("explorer",0,new Vector2(point.y<64?54:73,point.y));
                    }
                world.Fog.Tick(world.Entities);Assert.IsTrue(world.IsVisible(0,target));Assert.IsTrue(enemies.All(e=>world.IsVisible(0,e)));
                Assert.Greater(Vector2.Distance(boat.position,enemy.position),enemy.def.range+4,"The boat has not yet entered the known enemy's fire zone");
                Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{boat.id},order=OrderKind.Gather,target=target.id}).accepted);
                float food=world.Players[0].resources.food;
                new AiWaterEconomy(world,0).Tick(new List<EntityState>{boat},new List<EntityState>(),enemies,new Cost(),true,true);
                if(blockRetreat)Assert.IsEmpty(boat.orders,"Without any safe retreat point the dangerous fishing approach must stop");
                else
                {Assert.AreEqual(OrderKind.Move,boat.orders.Peek().kind);Assert.Greater(boat.orders.Peek().point.y,boat.position.y,"Retreat must sail away from the threatened fish");}
                world.Tick(.2f);Assert.GreaterOrEqual(boat.position.y,80,"The old gather must not keep steering into the known firing zone");
                Assert.AreEqual(food,world.Players[0].resources.food,"A cancelled fishing approach cannot credit food");
            }
        }

        [TestCase("winding","coast")]
        [TestCase("tropical","coast")]
        [TestCase("tributaries","coast")]
        [TestCase("estuary","coast")]
        [TestCase("winding","river")]
        [TestCase("winding","france")]
        [TestCase("winding","ottoman")]
        [TestCase("winding","wayfarer")]
        [TestCase("winding","highland")]
        public void PeacefulOpeningAiPaysForSurveyConstructionAndFishingOnEveryNewMap(string map,string civilization)
        {
            // Both original openings remain intact. The opponent has no AI;
            // public Hold orders keep our paid land soldiers home so defeating
            // it cannot end this economy-only measurement before fishing.
            // Workers, explorers, boats, accounts and age progression stay normal.
            var world=new World(Catalog.Load(),314159,civilization,"coast",2,map);var ai=new SkirmishAI(world,0);
            bool gathered=false;int cursor=world.Events.Count;
            for(int i=0;i<14000&&!gathered&&world.Winner<0;i++)
            {
                ai.Tick(.1f);
                var soldiers=world.Entities.Where(e=>e.Alive&&e.owner==0&&e.Military&&!e.def.waterborne&&e.garrisonedIn==0&&(e.def.tags&Tags.Explorer)==0&&(!e.hold||e.orders.Count>0)).Select(e=>e.id).ToArray();
                if(soldiers.Length>0)
                    Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=soldiers,order=OrderKind.Hold}).accepted);
                world.Tick(.1f);
                for(;cursor<world.Events.Count;cursor++)
                {var ev=world.Events[cursor];if(ev.kind=="Gather"&&ev.player==0&&world.Get(ev.entity)?.def.fishingBoat==true&&ev.amount>0)gathered=true;}
            }
            string diagnostic=$"{map}/{civilization}: t={world.Time:0.0}, age={world.Players[0].age}, resources={world.Players[0].resources}, winner={world.Winner}; "+
                string.Join("; ",world.Entities.Where(e=>e.Alive&&e.owner==0&&(e.def.id=="dock"||e.def.fishingBoat)).Select(e=>$"{e.def.id} at {e.position}: {e.status}"));
            Assert.IsTrue(gathered,"Peaceful AI economy must reach paid physical fishing within 1,400 simulated seconds. "+diagnostic);
            var payment=world.Events.First(e=>e.kind=="Payment"&&e.player==0&&e.message.StartsWith("Foundation River Dock: "));
            Assert.IsTrue(payment.hasEconomy);Assert.AreEqual(-200,payment.economy.actualDelta.wood,.001f);
            Assert.IsTrue(world.Events.Any(e=>e.kind=="BuildingComplete"&&e.player==0&&world.Get(e.entity)?.def.id=="dock"));
            var boatPayment=world.Events.First(e=>e.kind=="Payment"&&e.player==0&&e.message.StartsWith("Fishing Boat ×"));
            Assert.IsTrue(boatPayment.hasEconomy);Assert.AreEqual(-70,boatPayment.economy.actualDelta.wood,.001f);
            Assert.AreEqual(2,world.Players[0].age,"The economy chain must complete within the original era-II ceiling");
            var fishing=world.Events.First(e=>e.kind=="Gather"&&e.player==0&&world.Get(e.entity)?.def.fishingBoat==true&&e.amount>0);
            var boat=world.Get(fishing.entity);var fish=world.Get(fishing.target);
            Assert.IsNotNull(fish);Assert.AreEqual("fish",fish.def.id);Assert.IsTrue(world.IsWater(boat.position));
            Assert.AreEqual(OrderKind.Gather,boat.orders.Peek().kind);Assert.AreEqual(fish.id,boat.orders.Peek().target);
            // After the original opening has reached fishing, isolate one normal
            // work tick through public Stop orders. Fish depletion must become
            // actual food in both the resource balance and the gathered ledger.
            foreach(var other in world.Entities.Where(e=>e.Alive&&e.owner==0&&e.id!=boat.id&&e.def.kind==EntityKind.Unit&&e.orders.Count>0&&e.orders.Peek().kind==OrderKind.Gather).ToArray())
                Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{other.id},order=OrderKind.Stop}).accepted);
            float fishBefore=fish.remaining,foodBefore=world.Players[0].resources.food,gatheredBefore=world.Players[0].gathered.food;
            world.TickEconomy(.1f);
            float credited=world.Players[0].gathered.food-gatheredBefore;
            Assert.Greater(credited,0,"A physical fishing work tick must credit the food ledger. "+diagnostic);
            Assert.AreEqual(fishBefore-fish.remaining,credited,.01f);
            Assert.AreEqual(credited,world.Players[0].resources.food-foodBefore,.01f);
            TestContext.WriteLine(diagnostic);
        }
    }
}
