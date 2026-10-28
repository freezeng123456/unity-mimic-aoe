using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class MapObjectivesTests
    {
        static World Fixture(string map="river",bool shortClock=true)
        {
            var rules=SnapshotCodec.Clone(Catalog.Load());Assert.AreEqual(3,rules.mapObjectives.Length);
            foreach(var e in rules.entities){e.damage=0;e.meleeDamage=0;e.siegeDamage=0;}
            foreach(var d in rules.mapObjectives){d.holdSeconds=1;d.failureSeconds=.6f;d.deadline=shortClock?10:1200;}
            var world=new World(rules,maxAge:5,mapId:map);
            foreach(var e in world.Entities.Where(e=>e.def.kind==EntityKind.Unit).ToArray())world.Remove(e);
            return world;
        }
        static void Tick(World world,int steps){for(int i=0;i<steps;i++)world.Tick(.1f);}
        static EntityState Soldier(World world,int player,Vector2 point)
        {
            var unit=world.Spawn("musketeer",player,world.Navigation.NearestWalkable(point,.4f));
            Assert.IsTrue(world.Issue(new GameCommand{player=player,kind=CommandKind.Order,actors=new[]{unit.id},order=OrderKind.Hold}).accepted);return unit;
        }
        static void Configure(World world,string id)
        {var result=world.ConfigureMapObjective(id);Assert.IsTrue(result.accepted,result.reason);}

        [Test] public void EliminationRemainsDefaultAndAnUnsupportedMapDoesNotCreateAnObjective()
        {
            var world=Fixture("frontier");Assert.AreEqual("elimination",world.MapObjective.mode);
            int count=world.Entities.Count;Assert.IsFalse(world.ConfigureMapObjective("capture_harbor").accepted);Assert.AreEqual(count,world.Entities.Count);
            Assert.IsFalse(world.ConfigureMapObjective("defend_bridge").accepted);
            foreach(var e in world.Entities.Where(e=>e.Alive&&e.owner==1).ToArray())world.Remove(e);
            Tick(world,1);Assert.AreEqual(0,world.Winner);Assert.IsTrue(world.Events.Any(e=>e.kind=="MatchEnded"));
        }
        [TestCase(false)] [TestCase(true)] public void HarborNeedsUncontestedRealOccupancyAndTransfersTheActualDock(bool naval)
        {
            var world=Fixture();Configure(world,"capture_harbor");var state=world.MapObjective;
            var dock=world.Get(state.target);Assert.AreEqual("dock",dock.def.id);Assert.AreEqual(-1,dock.owner);
            Assert.IsFalse(world.ConfigureMapObjective("capture_harbor").accepted,"Configuration cannot duplicate the harbor");
            if(naval)
            {var boat=world.Rules.Entity("gunboat");world.Spawn(boat.id,0,world.WaterNavigation.NearestWalkable(state.center+world.DockWaterDirection(state.center)*7,boat.radius));}
            else Soldier(world,0,state.center+new Vector2(-5,0));
            var enemy=Soldier(world,1,state.center+new Vector2(0,5));
            Tick(world,4);Assert.IsTrue(state.contested);Assert.AreEqual(0,state.progress);Assert.AreEqual(-1,world.Winner);
            world.Remove(enemy);Tick(world,3);Assert.Greater(state.progress,0);
            var cold=World.Restore(world.Rules,SnapshotCodec.Clone(world.CaptureState()));Tick(world,10);Tick(cold,10);
            Assert.AreEqual(JsonUtility.ToJson(world.CaptureState()),JsonUtility.ToJson(cold.CaptureState()));
            Assert.AreEqual(0,world.Winner);Assert.AreEqual(0,dock.owner);Assert.AreEqual("won",state.status);
            Assert.AreEqual(1,world.Events.Count(e=>e.kind=="MapHarborCaptured"));Assert.AreEqual(1,world.Events.Count(e=>e.kind=="MatchEnded"));
        }
        [Test] public void DefenderMustHoldTheBridgeAndEnemyOccupationCanActuallyWin()
        {
            var world=Fixture();Configure(world,"defend_bridge");Tick(world,2);Assert.AreEqual(0,world.MapObjective.progress);
            Soldier(world,1,world.MapObjective.center);Tick(world,8);
            Assert.AreEqual(1,world.Winner);Assert.AreEqual("failed",world.MapObjective.status);Assert.AreEqual("Bridge overrun",world.MapObjective.reason);
            var won=Fixture();Configure(won,"defend_bridge");Soldier(won,0,won.MapObjective.center);Tick(won,12);
            Assert.AreEqual(0,won.Winner);Assert.AreEqual("Bridge defended",won.MapObjective.reason);
        }
        [Test] public void ObjectiveDeadlineAndDestroyedEscortAreRecordedFailures()
        {
            var world=Fixture();Configure(world,"defend_bridge");Tick(world,110);
            Assert.AreEqual(1,world.Winner);Assert.AreEqual("Objective deadline expired",world.MapObjective.reason);
            var escort=Fixture("frontier");Configure(escort,"escort_caravan");escort.Remove(escort.Get(escort.MapObjective.escort));Tick(escort,1);
            Assert.AreEqual(1,escort.Winner);Assert.AreEqual("Escort caravan destroyed",escort.MapObjective.reason);
        }
        [Test] public void RealEscortStopsWithoutProtectionAndColdRecoveryContinuesTheWholeRoute()
        {
            var world=Fixture("frontier",false);Configure(world,"escort_caravan");var state=world.MapObjective;var cart=world.Get(state.escort);Vector2 start=cart.position;
            Tick(world,10);Assert.Less(Vector2.Distance(start,cart.position),.05f);Assert.AreEqual(0,state.waypoint);
            var guard=Soldier(world,0,start+new Vector2(2,0));World cold=null;int guardId=guard.id,visited=0;
            // Repeated ordinary move orders accompany the real moving cart.
            // Neither the cart nor its route progress is teleported/injected.
            for(int step=0;step<11000&&world.Winner<0;step++)
            {
                if(step%5==0)
                {
                    Vector2 destination=cart.position;
                    if(state.waypoint<state.route.Length)destination+=Vector2.ClampMagnitude(state.route[state.waypoint]-cart.position,3);
                    var command=new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{guardId},order=OrderKind.Move,point=destination};
                    Assert.IsTrue(world.Issue(command).accepted);if(cold!=null)Assert.IsTrue(cold.Issue(command).accepted);
                }
                world.Tick(.1f);if(cold!=null)cold.Tick(.1f);
                if(state.waypoint>visited)
                {
                    Assert.AreEqual(visited+1,state.waypoint,"Escort must visit every route checkpoint in order");
                    Assert.LessOrEqual(Vector2.Distance(cart.position,state.route[visited]),3.001f,"Checkpoint needs actual nearby arrival");
                    Assert.IsTrue(world.Navigation.ClearLine(cart.position,state.route[visited],cart.def.radius),"No checkpoint credit through an obstacle");
                    visited=state.waypoint;
                }
                if(step==120){cold=World.Restore(world.Rules,SnapshotCodec.Clone(world.CaptureState()));Assert.Greater(Vector2.Distance(start,cart.position),2);}
            }
            Assert.NotNull(cold);Assert.AreEqual(0,world.Winner,world.MapObjective.reason+" at checkpoint "+state.waypoint+" cart "+cart.position+" guard "+guard.position+" status "+cart.status);
            Assert.AreEqual(state.route.Length,state.waypoint);Assert.AreEqual(state.route.Length,world.Events.Count(e=>e.kind=="MapEscortCheckpoint"));
            Assert.AreEqual(JsonUtility.ToJson(world.CaptureState()),JsonUtility.ToJson(cold.CaptureState()),"Actual escort movement/orders/events must continue identically");
        }
        [Test] public void InvalidObjectiveReferencesCannotEnterAColdRestoredMatch()
        {
            var world=Fixture("frontier");Configure(world,"escort_caravan");var saved=world.CaptureState();saved.mapObjective.escort=int.MaxValue;
            Assert.Throws<ArgumentException>(()=>World.Restore(world.Rules,saved));
            saved=world.CaptureState();saved.mapObjective.route[0]=new Vector2(float.NaN,3);
            Assert.Throws<ArgumentException>(()=>World.Restore(world.Rules,saved));
        }
        [Test] public void ObjectiveAiTargetsThePublicRouteUntilTheEnemyCartIsActuallyVisible()
        {
            var world=Fixture("frontier");Configure(world,"escort_caravan");var state=world.MapObjective;var cart=world.Get(state.escort);
            world.Fog.RestoreState(new int[32768]);Assert.IsFalse(world.IsVisible(1,cart));
            Assert.IsTrue(world.TryAiObjectiveDestination(1,out var destination));Assert.AreEqual(state.route[0],destination);
            Soldier(world,1,cart.position+new Vector2(5,0));world.Fog.Tick(world.Entities);Assert.IsTrue(world.IsVisible(1,cart));
            Assert.IsTrue(world.TryAiObjectiveDestination(1,out destination));Assert.AreEqual(cart.position,destination);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Resign}).accepted);Assert.AreEqual("failed",state.status);
            Assert.IsFalse(world.TryAiObjectiveDestination(1,out _));
        }
    }
}
