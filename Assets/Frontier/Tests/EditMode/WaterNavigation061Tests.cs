using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class WaterNavigation061Tests
    {
        private World world;
        [SetUp] public void SetUp()
        {
            world=new World(Catalog.Load(),314159,"coast","coast",5,"river");
            foreach(var entity in world.Entities.Where(e=>e.def.id!="town").ToArray())world.Remove(entity);
        }
        private CommandResult Order(EntityState actor,OrderKind kind,Vector2 point=default,EntityState target=null)
        {
            if(target!=null)world.Fog.Reveal(actor.owner,target.position,8);
            return world.Issue(new GameCommand{player=actor.owner,kind=CommandKind.Order,actors=new[]{actor.id},order=kind,point=point,target=target?.id??0});
        }
        private void TickChecked(EntityState actor,int ticks)
        {
            for(int i=0;i<ticks;i++)
            {
                var previous=actor.position;float height=world.SurfaceHeight(previous,actor.def.waterborne);world.Tick(.1f);
                Assert.IsTrue(world.NavigationFor(actor).IsWalkable(actor.position,actor.def.radius),$"{actor.def.id} left its channel at {actor.position}");
                Assert.LessOrEqual(Vector2.Distance(previous,actor.position),world.Speed(actor)*.1f+.0001f,"No teleport or overspeed");
                Assert.LessOrEqual(Mathf.Abs(world.SurfaceHeight(actor.position,actor.def.waterborne)-height),Vector2.Distance(previous,actor.position)*.4f+.0002f,"No bridge-side height jump");
            }
        }
        [Test] public void DefaultMapStaysLandAndRiverAddsFourLegalFishWithoutMutatingSharedRules()
        {
            var rules=Catalog.Load();var route=rules.landmarks.route.ToArray();var socket=rules.landmarks.sockets[1].position;
            var frontier=new World(rules);var river=new World(rules,314159,"coast","coast",5,"river");
            Assert.AreEqual("frontier",frontier.MapId);Assert.IsFalse(frontier.IsWater(new Vector2(64,22)));Assert.IsEmpty(frontier.Entities.Where(e=>e.def.waterborne));
            Assert.IsNotEmpty(frontier.TerrainObstacles);Assert.IsEmpty(river.TerrainObstacles);
            Assert.AreEqual(4,river.Entities.Count(e=>e.def.id=="fish"));
            foreach(var fish in river.Entities.Where(e=>e.def.id=="fish")){Assert.IsTrue(river.IsWater(fish.position));Assert.IsFalse(river.IsBridge(fish.position));Assert.IsTrue(river.WaterNavigation.IsWalkable(fish.position,fish.def.radius));}
            CollectionAssert.AreEqual(route,rules.landmarks.route);Assert.AreEqual(socket,rules.landmarks.sockets[1].position);
            CollectionAssert.AreEqual(route,frontier.TradeRoute);Assert.AreNotEqual(socket,river.Get(river.TradeSockets[1].entity).position);
        }
        [Test] public void ExactBankEdgesAndBridgeHeightsMatchBothNavigationChannels()
        {
            Assert.AreEqual(57.5f,MapTopology.RiverMinX);Assert.AreEqual(70.5f,MapTopology.RiverMaxX);
            Assert.IsFalse(world.Navigation.IsWalkable(new Vector2(64,22),.4f));Assert.IsTrue(world.WaterNavigation.IsWalkable(new Vector2(64,22),.9f));
            Assert.IsTrue(world.Navigation.IsWalkable(new Vector2(64,40),.4f));Assert.IsTrue(world.WaterNavigation.IsWalkable(new Vector2(64,40),.9f));
            Assert.IsFalse(world.WaterNavigation.IsWalkable(new Vector2(57,22),.4f));Assert.IsFalse(world.Navigation.IsWalkable(new Vector2(58,22),.4f));
            Assert.AreEqual(3.2f,world.SurfaceHeight(new Vector2(64,40)));Assert.AreEqual(.02f,world.SurfaceHeight(new Vector2(64,40),true));
            Assert.AreEqual(1.6f,world.SurfaceHeight(new Vector2(53.5f,40)),.0001f);Assert.AreEqual(1.6f,world.SurfaceHeight(new Vector2(74.5f,88)),.0001f);
            Assert.AreEqual(0,world.SurfaceHeight(new Vector2(49.5f,40)));Assert.IsTrue(world.IsBridgeApproach(new Vector2(52,40)));
            Assert.IsTrue(world.BridgeFootprintOverlaps(new Vector2(48,40),4,4));Assert.IsFalse(world.BridgeFootprintOverlaps(new Vector2(55,28),4,4));
        }
        [Test] public void LandArmyCrossesByBridgeOnEveryTickInsteadOfWadingOrTeleporting()
        {
            var unit=world.Spawn("hussar",0,new Vector2(48,60));var goal=new Vector2(80,60);
            Assert.IsTrue(Order(unit,OrderKind.Move,goal).accepted);bool crossed=false;
            for(int i=0;i<900&&unit.orders.Count>0;i++)
            {
                TickChecked(unit,1);if(world.IsWater(unit.position)){crossed=true;Assert.IsTrue(world.IsBridge(unit.position));}
            }
            Assert.IsTrue(crossed);Assert.IsEmpty(unit.orders);Assert.Less(Vector2.Distance(unit.position,goal),.3f);
        }
        [Test] public void ShipsSailUnderBothBridgesAndNeverLeaveTheWater()
        {
            var boat=world.Spawn("gunboat",0,new Vector2(64,22));var goal=new Vector2(64,106);bool south=false,north=false;
            Assert.IsTrue(Order(boat,OrderKind.Move,goal).accepted);
            for(int i=0;i<600&&boat.orders.Count>0;i++)
            {
                TickChecked(boat,1);Assert.IsTrue(world.IsWater(boat.position));
                if(world.IsBridge(boat.position)){south|=boat.position.y<64;north|=boat.position.y>64;Assert.AreEqual(.02f,world.SurfaceHeight(boat.position,true));}
            }
            Assert.IsTrue(south&&north);Assert.IsEmpty(boat.orders);Assert.Less(Vector2.Distance(boat.position,goal),.3f);
        }
        [Test] public void BridgeOccupantsDoNotPushOrReserveTheBoatChannel()
        {
            var infantry=world.Spawn("pike",0,new Vector2(64,40));infantry.hold=true;
            var boat=world.Spawn("fishing_boat",0,new Vector2(64,32));var goal=new Vector2(64,48);float closest=float.MaxValue;
            Assert.IsTrue(Order(boat,OrderKind.Move,goal).accepted);
            for(int i=0;i<100&&boat.orders.Count>0;i++){TickChecked(boat,1);closest=Mathf.Min(closest,Vector2.Distance(boat.position,infantry.position));Assert.AreEqual(64,boat.position.x,.0001f);}
            Assert.Less(closest,.3f,"Ship passes beneath the stationary bridge actor");Assert.IsEmpty(boat.orders);Assert.AreEqual(new Vector2(64,40),infantry.position);
        }
        [Test] public void ReleasingDynamicBlocksAndDemolishingBuildingsCannotEraseWaterTerrain()
        {
            var water=new Vector2(64,60);var bank=new Vector2(55,60);
            Assert.IsEmpty(world.Navigation.FindPath(new Vector2(56,60),new Vector2(64,60),.4f).Where(p=>world.IsWater(p)&&!world.IsBridge(p)));
            world.Navigation.SetBlocked(water,2,true);world.Navigation.SetBlocked(water,2,false);
            world.WaterNavigation.SetBlocked(bank,2,true);world.WaterNavigation.SetBlocked(bank,2,false);
            var fixture=world.Spawn("house",0,water);world.Remove(fixture);
            Assert.IsFalse(world.Navigation.IsWalkable(water,.4f));Assert.IsFalse(world.WaterNavigation.IsWalkable(bank,.4f));Assert.IsTrue(world.WaterNavigation.IsWalkable(water,.9f));
        }
        [Test] public void TerrainChangesInvalidatePreviouslyCachedClearanceComponents()
        {
            var navigation=new GridNavigation(20,20);var from=new Vector2(4,10);var to=new Vector2(16,10);
            Assert.IsNotEmpty(navigation.FindPath(from,to));int before=navigation.Version;
            navigation.ConfigureTerrain(p=>p.x==10);Assert.Greater(navigation.Version,before);Assert.IsEmpty(navigation.FindPath(from,to));
            navigation.SetBlocked(new Vector2(10,10),3,true);navigation.SetBlocked(new Vector2(10,10),3,false);Assert.IsEmpty(navigation.FindPath(from,to));
            navigation.SetTerrainBlocked(new Vector2(10,10),1,false);Assert.IsNotEmpty(navigation.FindPath(from,to));
        }
        [Test] public void MixedLandAndWaterSelectionGetsIndependentLegalDestinations()
        {
            var infantry=world.Spawn("pike",0,new Vector2(50,26));var boat=world.Spawn("fishing_boat",0,new Vector2(64,26));
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{infantry.id,boat.id},order=OrderKind.Move,point=new Vector2(64,60)}).accepted);
            Assert.IsTrue(world.Navigation.IsWalkable(infantry.orders.Peek().point,infantry.def.radius));Assert.IsTrue(world.WaterNavigation.IsWalkable(boat.orders.Peek().point,boat.def.radius));
            for(int i=0;i<700&&(infantry.orders.Count>0||boat.orders.Count>0);i++)
            {world.Tick(.1f);Assert.IsTrue(world.Navigation.IsWalkable(infantry.position,infantry.def.radius));Assert.IsTrue(world.WaterNavigation.IsWalkable(boat.position,boat.def.radius));}
            Assert.IsEmpty(infantry.orders);Assert.IsEmpty(boat.orders);
        }
        [Test] public void FailedMixedMovePreservesEveryActorsExistingOrderAtomically()
        {
            var infantry=world.Spawn("pike",0,new Vector2(50,26));var boat=world.Spawn("fishing_boat",0,new Vector2(64,26));
            Assert.IsTrue(Order(infantry,OrderKind.Move,new Vector2(50,30)).accepted);Assert.IsTrue(Order(boat,OrderKind.Move,new Vector2(64,30)).accepted);
            var landOrder=infantry.orders.Peek();var boatOrder=boat.orders.Peek();world.WaterNavigation.ConfigureTerrain(_=>true);
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{infantry.id,boat.id},order=OrderKind.Move,point=new Vector2(64,60)}).accepted);
            Assert.AreSame(landOrder,infantry.orders.Peek());Assert.AreSame(boatOrder,boat.orders.Peek());
        }
        [Test] public void CrossChannelMeleeAndShipGarrisonAreRejectedWithoutDiscardingMove()
        {
            var pike=world.Spawn("pike",0,new Vector2(56,28));var enemy=world.Spawn("fishing_boat",1,new Vector2(60,28));
            Assert.IsTrue(Order(pike,OrderKind.Move,new Vector2(52,28)).accepted);var old=pike.orders.Peek();
            Assert.IsFalse(Order(pike,OrderKind.Attack,target:enemy).accepted);Assert.AreSame(old,pike.orders.Peek());
            var boat=world.Spawn("fishing_boat",0,new Vector2(64,28));var town=world.Entities.First(e=>e.owner==0&&e.def.id=="town");
            Assert.IsFalse(Order(boat,OrderKind.Garrison,target:town).accepted);Assert.AreEqual(0,boat.garrisonedIn);
        }
        [Test] public void ShoreMusketUsesRangedShotEvenBesideShipAndForceMeleeCannotAttackIt()
        {
            var musket=world.Spawn("musketeer",0,new Vector2(56.9f,28));var boat=world.Spawn("fishing_boat",1,new Vector2(58.3f,28));boat.hold=true;
            float hp=boat.hp;Assert.IsTrue(Order(musket,OrderKind.Attack,target:boat).accepted);TickChecked(musket,15);
            Assert.Less(boat.hp,hp);Assert.IsFalse(musket.shotMelee);Assert.IsTrue(world.Events.Any(e=>e.kind=="Fire"&&e.entity==musket.id&&e.target==boat.id));
            musket.forceMelee=true;Assert.IsFalse(Order(musket,OrderKind.Attack,target:boat).accepted);
        }
        [Test] public void GunboatBombardsBankBuildingThenRetargetsSpecificLandSoldier()
        {
            var gun=world.Spawn("gunboat",0,new Vector2(64,28));var dock=world.Spawn("dock",1,new Vector2(73,28));var soldier=world.Spawn("pike",1,new Vector2(73,33));soldier.hold=true;
            float hp=dock.hp;Assert.IsTrue(Order(gun,OrderKind.Attack,target:dock).accepted);TickChecked(gun,15);Assert.Less(dock.hp,hp);
            float soldierHp=soldier.hp;Assert.IsTrue(Order(gun,OrderKind.Attack,target:soldier).accepted);TickChecked(gun,50);
            Assert.Less(soldier.hp,soldierHp);Assert.IsTrue(world.Events.Any(e=>e.kind=="Fire"&&e.entity==gun.id&&e.target==soldier.id));Assert.IsTrue(world.IsWater(gun.position));
        }
        [Test] public void WatchtowerAutoFireDamagesEnemyBoatButMeleeDoesNotChaseIt()
        {
            var tower=world.Spawn("tower",0,new Vector2(53,28));var pike=world.Spawn("pike",0,new Vector2(56,35));var boat=world.Spawn("fishing_boat",1,new Vector2(64,28));boat.hold=true;
            var start=pike.position;float hp=boat.hp;world.Fog.Tick(world.Entities);for(int i=0;i<20;i++)world.Tick(.1f);
            Assert.Less(boat.hp,hp);Assert.AreEqual(start,pike.position);Assert.AreEqual(0,pike.attackTarget);
        }
        [Test] public void NeutralCaravanCrossesOnlyTheAuthoredBridgeAndTradeSocketsStayReachable()
        {
            bool crossed=false;for(int i=0;i<700;i++)
            {
                var previous=world.CaravanPosition;float height=world.SurfaceHeight(previous);world.Tick(.1f);
                Assert.IsTrue(world.Navigation.IsWalkable(world.CaravanPosition,.4f));
                Assert.LessOrEqual(Mathf.Abs(world.SurfaceHeight(world.CaravanPosition)-height),Vector2.Distance(previous,world.CaravanPosition)*.4f+.0002f,"Neutral cart must use the ramp mouth");
                if(world.IsWater(world.CaravanPosition)){crossed=true;Assert.IsTrue(world.IsBridge(world.CaravanPosition));}
            }
            Assert.IsTrue(crossed);
            var fresh=new World(world.Rules,314159,"coast","coast",5,"river");
            foreach(var site in fresh.TradeSockets)
            {
                var entity=fresh.Get(site.entity);Assert.IsFalse(fresh.IsWater(entity.position));
                foreach(var point in new[]{new Vector2(32,28),new Vector2(96,100)})Assert.IsNotEmpty(fresh.Navigation.FindPath(point,entity.position,.4f));
            }
        }
        [Test] public void BridgeRampSideBarriersCannotBeCrossedOrClearedByDynamicRelease()
        {
            foreach(float bridge in new[]{MapTopology.BridgeSouth,MapTopology.BridgeNorth})foreach(float x in new[]{52f,55f,73f,76f})
            {
                var side=new Vector2(x,bridge+5);Assert.IsFalse(world.Navigation.IsWalkable(side,.4f));
                world.Navigation.SetBlocked(side,.45f,true);world.Navigation.SetBlocked(side,.45f,false);Assert.IsFalse(world.Navigation.IsWalkable(side,.4f));
                var from=new Vector2(x,bridge+8);var to=new Vector2(x,bridge);Assert.IsFalse(world.Navigation.ClearLine(from,to,.4f));
                var route=world.Navigation.FindPath(from,to,.4f);Assert.Greater(route.Count,2);
                for(int n=1;n<route.Count;n++)
                {
                    int steps=Mathf.CeilToInt(Vector2.Distance(route[n-1],route[n])*10);var previous=route[n-1];
                    for(int step=1;step<=steps;step++)
                    {
                        var point=Vector2.Lerp(route[n-1],route[n],step/(float)steps);
                        Assert.IsTrue(world.Navigation.IsWalkable(point,.4f));
                        Assert.LessOrEqual(Mathf.Abs(world.SurfaceHeight(point)-world.SurfaceHeight(previous)),Vector2.Distance(point,previous)*.4f+.0002f);
                        previous=point;
                    }
                }
            }
        }
        [TestCase(200)] [TestCase(400)]
        public void MixedArmyPassesBothWaysWhileTwelveShipsUseTheUnderpass(int count)
        {
            var rules=JsonUtility.FromJson<RuleCatalog>(JsonUtility.ToJson(Catalog.Load()));
            foreach(var definition in rules.entities){definition.damage=0;definition.meleeDamage=0;definition.siegeDamage=0;}
            var arena=new World(rules,314159,"coast","coast",5,"river");
            foreach(var entity in arena.Entities.Where(e=>e.def.id!="town").ToArray())arena.Remove(entity);
            var types=new[]{"pike","musketeer","hussar","dragoon","cannon"};var actors=new List<EntityState>();var goals=new Dictionary<int,Vector2>();
            for(int side=0;side<2;side++)
            {
                var group=new List<EntityState>();var center=new Vector2(side==0?27:101,64);
                var starts=arena.Navigation.FormationSlots(center,count/2,2.8f);Assert.AreEqual(count/2,starts.Count);
                for(int i=0;i<starts.Count;i++){var actor=arena.Spawn(types[i%types.Length],0,starts[i]);group.Add(actor);actors.Add(actor);}
                Assert.IsTrue(arena.Issue(new GameCommand{player=0,kind=CommandKind.Order,order=OrderKind.Move,actors=group.Select(e=>e.id).ToArray(),point=new Vector2(side==0?101:27,64)}).accepted);
                foreach(var actor in group)goals[actor.id]=actor.orders.Peek().point;
            }
            for(int side=0;side<2;side++)
            {
                var group=new List<EntityState>();
                var starts=arena.WaterNavigation.FormationSlots(new Vector2(64,side==0?18:110),6,3.9f);Assert.AreEqual(6,starts.Count);
                for(int i=0;i<starts.Count;i++){var actor=arena.Spawn(i%2==0?"fishing_boat":"gunboat",0,starts[i]);group.Add(actor);actors.Add(actor);}
                Assert.IsTrue(arena.Issue(new GameCommand{player=0,kind=CommandKind.Order,order=OrderKind.Move,actors=group.Select(e=>e.id).ToArray(),point=new Vector2(64,side==0?110:18)}).accepted);
                foreach(var actor in group)goals[actor.id]=actor.orders.Peek().point;
            }
            float peakStep=0,peakHeightStep=0;double peakTickMs=0;int stepsTaken=0;var crossed=new HashSet<int>();
            var watch=new System.Diagnostics.Stopwatch();
            for(int step=0;step<2400;step++)
            {
                watch.Restart();arena.Tick(.1f);watch.Stop();peakTickMs=System.Math.Max(peakTickMs,watch.Elapsed.TotalMilliseconds);stepsTaken++;
                bool arrived=true;
                foreach(var actor in actors)
                {
                    float distance=Vector2.Distance(actor.position,actor.previousPosition);peakStep=Mathf.Max(peakStep,distance);
                    float heightStep=Mathf.Abs(arena.SurfaceHeight(actor.position,actor.def.waterborne)-arena.SurfaceHeight(actor.previousPosition,actor.def.waterborne));peakHeightStep=Mathf.Max(peakHeightStep,heightStep);
                    if(!arena.NavigationFor(actor).IsWalkable(actor.position,actor.def.radius))Assert.Fail($"{actor.id}/{actor.def.id} left its channel at step {step}: {actor.position}");
                    if(distance>arena.Speed(actor)*.1f+.0001f)Assert.Fail($"{actor.id} exceeded speed budget at step {step}");
                    if(heightStep>distance*.4f+.0002f)Assert.Fail($"{actor.id} jumped a bridge side at step {step}: {heightStep}");
                    if(actor.def.waterborne)
                    {if(!arena.IsWater(actor.position))Assert.Fail("Ship reached land");if(arena.IsBridge(actor.position))crossed.Add(actor.id);}
                    else if(arena.IsWater(actor.position))
                    {if(!arena.IsBridge(actor.position))Assert.Fail("Land unit entered open water");crossed.Add(actor.id);}
                    if(Vector2.Distance(actor.position,goals[actor.id])>=1)arrived=false;
                }
                if(arrived)break;
            }
            var missed=actors.Where(e=>Vector2.Distance(e.position,goals[e.id])>=1).ToList();
            int landArrived=actors.Count(e=>!e.def.waterborne&&Vector2.Distance(e.position,goals[e.id])<1),shipsArrived=actors.Count(e=>e.def.waterborne&&Vector2.Distance(e.position,goals[e.id])<1);
            TestContext.WriteLine($"River maneuver: land={count}; landArrived={landArrived}; ships=12; shipsArrived={shipsArrived}; crossed={crossed.Count}; simSeconds={stepsTaken*.1f:F1}; peakPlanarStep={peakStep:F5}; peakHeightStep={peakHeightStep:F5}; peakTickMs={peakTickMs:F4}");
            Assert.AreEqual(0,missed.Count,$"{missed.Count}/{actors.Count} did not arrive within 240 simulation seconds: "+string.Join("; ",missed.Take(12).Select(e=>$"{e.id} {e.def.id} {e.position} -> {goals[e.id]} {e.status}")));
            Assert.AreEqual(actors.Count,crossed.Count,"Every land unit crossed a bridge and every ship crossed below one");
        }
    }
}
