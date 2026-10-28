using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Isolated lifecycle mechanics fixtures, not a normal paid economy run.
    // Cargo state is an explicit fixture input, matching existing container tests.
    // Both cases execute real World.Tick; no private movement method is called.
    public sealed class ContainerReleaseBody082Tests
    {
        static World Arena(string map)
        {
            var w=new World(Catalog.Load(),314159,"coast","coast",5,map);
            foreach(var e in w.Entities.Where(e=>e.def.id!="town").ToArray())w.Remove(e);
            foreach(var p in w.TerrainObstacles)
            {w.Navigation.SetBlocked(new Vector2(p.x,p.y),p.z,false);w.WaterNavigation.SetBlocked(new Vector2(p.x,p.y),p.z,false);}
            return w;
        }
        static World Cold(World w)=>World.Restore(w.Rules,SnapshotCodec.Clone(w.CaptureState()));
        static string State(World w)=>JsonUtility.ToJson(w.CaptureState());
        static void Order(World w,EntityState e,OrderKind kind,Vector2 point=default(Vector2),int target=0)
        {
            var result=w.Issue(new GameCommand{player=e.owner,kind=CommandKind.Order,actors=new[]{e.id},order=kind,point=point,target=target});
            Assert.IsTrue(result.accepted,result.reason);
        }
        static EntityState Cargo(World w,EntityState container)
        {
            var p=w.Spawn("villager",0,container.position);p.garrisonedIn=container.id;container.occupants.Add(p.id);return p;
        }
        static float SegmentDistance(Vector2 a,Vector2 b,Vector2 p)
        {var d=b-a;float t=d.sqrMagnitude>1e-12f?Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude):0;return Vector2.Distance(a+d*t,p);}
        static void BodyClear(World w,int moverId,int passengerId,float dt)
        {
            var mover=w.Get(moverId);var passenger=w.Get(passengerId);float radius=mover.def.radius+passenger.def.radius;
            Assert.IsTrue(mover.Alive&&passenger.Alive&&passenger.garrisonedIn==0);
            Assert.IsTrue(w.Navigation.IsWalkable(mover.position,mover.def.radius));
            Assert.IsTrue(w.Navigation.IsWalkable(passenger.position,passenger.def.radius));
            Assert.IsTrue(w.Navigation.ClearLine(mover.previousPosition,mover.position,mover.def.radius));
            Assert.LessOrEqual(Vector2.Distance(mover.previousPosition,mover.position),w.Speed(mover)*dt+.0001f);
            Assert.GreaterOrEqual(SegmentDistance(mover.previousPosition,mover.position,passenger.position),radius-.00001f,"The later actor's actual same-tick movement must see the newly released physical body");
            Assert.GreaterOrEqual(Vector2.Distance(mover.position,passenger.position),radius-.00001f);
        }
        static int Bucket(Vector2 p)=>Mathf.FloorToInt(p.x/8)+Mathf.FloorToInt(p.y/8)*32;
        static int[] NearbyIds(World w,Vector2 center,float radius)
        {
            var method=typeof(World).GetMethod("Nearby",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            return ((IEnumerable<EntityState>)method.Invoke(w,new object[]{center,radius})).Select(e=>e.id).ToArray();
        }
        static void FinishAndCompare(World w,World cold,int moverId,int passengerId,Vector2 goal,int losses)
        {
            bool crossed=false;
            for(int step=0;step<150&&w.Get(moverId).orders.Count>0;step++)
            {
                w.Tick(.1f);cold.Tick(.1f);BodyClear(w,moverId,passengerId,.1f);BodyClear(cold,moverId,passengerId,.1f);
                Assert.AreEqual(losses,w.Players[0].losses,"No additional loss after the explicitly tested release");
                Assert.AreEqual(State(w),State(cold),"Cold mirror must preserve actual collision/steering/events");
                var mover=w.Get(moverId);var p=w.Get(passengerId);
                if(!crossed&&Bucket(mover.previousPosition)!=Bucket(mover.position)&&Bucket(mover.position)==Bucket(p.position))
                {
                    // Passenger was created after mover, but was already in this
                    // bucket. Appending the lower-id mover would reverse the
                    // order compared with a fresh Restore/RebuildSpatial.
                    Assert.Less(mover.id,p.id);var immediate=Cold(w);
                    CollectionAssert.AreEqual(NearbyIds(w,p.position,3.5f),NearbyIds(immediate,p.position,3.5f),"Cross-bucket commit must preserve cold-rebuild neighbor order without another Tick");
                    cold=immediate;crossed=true;
                }
            }
            Assert.IsTrue(crossed,"Fixture must actually exercise a lower-id mover entering the higher-id released passenger's bucket");
            Assert.IsEmpty(w.Get(moverId).orders);Assert.Less(Vector2.Distance(w.Get(moverId).position,goal),.051f);
            Assert.AreEqual(State(w),State(cold));
        }
        [Test] public void ShipUnloadsBeforeLaterActorMovesAndColdOrderRemainsStable()
        {
            var w=Arena(MapTopology.River);var ship=w.Spawn("transport_ship",0,new Vector2(60,64));
            // The fixed river starts at x57.5; x54 legitimately projects to
            // x55 for this ship's berth reach. Use that measured exact shore
            // so this fixture isolates same-tick body registration.
            Assert.IsTrue(w.TryTransportShore(ship,new Vector2(55,64),.4f,out var shore,out var berth));
            Assert.AreEqual(new Vector2(55,64),shore);ship.position=ship.previousPosition=berth; // Prepared berthed fixture.
            var mover=w.Spawn("cannon",0,shore-Vector2.up*1.31f);var passenger=Cargo(w,ship);
            Assert.Less(ship.id,mover.id);Assert.Less(mover.id,passenger.id);
            var goal=shore+Vector2.up*3;Order(w,mover,OrderKind.Move,goal);w.Fog.Tick(w.Entities);
            var result=w.Issue(new GameCommand{player=0,kind=CommandKind.Disembark,actors=new[]{ship.id},point=shore});Assert.IsTrue(result.accepted,result.reason);
            Assert.AreEqual(berth,ship.orders.Peek().resolvedPoint);int cursor=w.Events.Count,losses=w.Players[0].losses;var cold=Cold(w);
            w.Tick(.1f);cold.Tick(.1f);
            Assert.AreEqual(1,w.Events.Skip(cursor).Count(e=>e.kind=="Disembarked"&&e.entity==passenger.id&&e.target==ship.id));
            Assert.AreEqual(shore,passenger.position,"The first requested landing slot must be the one ahead of the later mover");
            Assert.IsEmpty(ship.occupants);Assert.AreEqual(0,w.TransportLoad(ship));
            BodyClear(w,mover.id,passenger.id,.1f);BodyClear(cold,mover.id,passenger.id,.1f);
            Assert.AreEqual(losses,w.Players[0].losses);Assert.AreEqual(State(w),State(cold));
            FinishAndCompare(w,cold,mover.id,passenger.id,goal,losses);
        }
        [Test] public void EarlierAttackerDestroysGarrisonBeforeLaterActorMovesAndColdOrderRemainsStable()
        {
            var w=Arena(MapTopology.Frontier);var attacker=w.Spawn("explorer",1,new Vector2(46.5f,64));
            var host=w.Spawn("tower",0,new Vector2(50,64));host.hp=1; // Explicit one-shot destruction fixture, not paid-world evidence.
            Assert.IsTrue(w.FindSpawnPositions(host,1,.4f,out var exit));var releasedPoint=exit[0];
            Assert.AreEqual(new Vector2(53.5f,64),releasedPoint);
            var mover=w.Spawn("cannon",0,releasedPoint-Vector2.up*1.31f);var passenger=Cargo(w,host);
            Assert.Less(attacker.id,host.id);Assert.Less(host.id,mover.id);Assert.Less(mover.id,passenger.id);
            Order(w,mover,OrderKind.Hold);w.Fog.Tick(w.Entities);Order(w,attacker,OrderKind.Attack,target:host.id);
            for(int i=0;i<20&&attacker.fireAt<0;i++)w.Tick(.1f);
            Assert.GreaterOrEqual(attacker.fireAt,0,"The earlier attacker must prepare a real normal attack");
            while(w.Time+.1f<attacker.fireAt-.00001f)w.Tick(.1f);
            Assert.IsTrue(host.Alive&&passenger.garrisonedIn==host.id);
            Assert.IsTrue(w.FindSpawnPositions(host,1,.4f,out var actualExit));Assert.AreEqual(releasedPoint,actualExit[0]);
            var goal=releasedPoint+Vector2.up*3;Order(w,mover,OrderKind.Move,goal);
            int cursor=w.Events.Count,losses=w.Players[0].losses;var cold=Cold(w);
            w.Tick(.1f);cold.Tick(.1f);
            Assert.IsFalse(host.Alive);Assert.IsEmpty(host.occupants);Assert.AreEqual(releasedPoint,passenger.position);
            Assert.AreEqual(1,w.Events.Skip(cursor).Count(e=>e.kind=="Ungarrison"&&e.entity==host.id));
            Assert.AreEqual(losses+1,w.Players[0].losses,"Only the deliberately destroyed container is lost");
            BodyClear(w,mover.id,passenger.id,.1f);BodyClear(cold,mover.id,passenger.id,.1f);Assert.AreEqual(State(w),State(cold));
            // Stop further test combat after the specific destruction event;
            // this fixture checks release/index/collision, not an army battle.
            Order(w,attacker,OrderKind.Hold);Order(cold,cold.Get(attacker.id),OrderKind.Hold);
            FinishAndCompare(w,cold,mover.id,passenger.id,goal,losses+1);
        }
    }
}
