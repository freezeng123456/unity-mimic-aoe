using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Declared single-unit bridge fixture. Every position below is reached by
    // public Move and actual Tick; only a distant cell invalidates the route.
    public sealed class BridgeTransit082Tests
    {
        [Serializable] sealed class Receipt
        {
            public string label,status,error,sourceFingerprint;
            public string scope="Public physical Move, outgoing-ramp replan, cold continuation and order reset; not400-unit throughput, paid/native/FPS acceptance";
            public Vector2 checkpoint,final;
            public int checkpointIndex,checkpointDirection,replannedVersion,steps;
            public float elapsed,peakStep,peakHeightStep;
            public bool coldEqual=true,queryUnchanged,arrived,clearedAfterExit,sawReverseDeck;
        }
        static readonly Vector2 Forward=new Vector2(84,40),Back=new Vector2(44,40),Remote=new Vector2(8,118);
        static string State(World w)=>JsonUtility.ToJson(w.CaptureState());
        static World Cold(World w)=>World.Restore(w.Rules,SnapshotCodec.Clone(w.CaptureState()));
        static World Arena(out EntityState actor,Vector2? start=null)
        {
            var w=new World(Catalog.Load(),314159,"coast","coast",5,MapTopology.River);
            foreach(var e in w.Entities.Where(e=>e.def.id!="town").ToArray())w.Remove(e);
            actor=w.Spawn("hussar",0,start??Back);Assert.IsTrue(w.Navigation.IsWalkable(actor.position,actor.def.radius));return w;
        }
        static void Move(World w,EntityState actor,Vector2 goal)
        {
            var r=w.Issue(new GameCommand{player=0,kind=CommandKind.Order,order=OrderKind.Move,actors=new[]{actor.id},point=goal});
            Assert.IsTrue(r.accepted,r.reason);Assert.AreEqual(goal,actor.orders.Peek().point);
        }
        static void Step(World w,World cold,EntityState actor,Receipt row,int losses,float hp)
        {
            Vector2 before=actor.position;w.Tick(.1f);cold.Tick(.1f);row.steps++;row.elapsed+=.1f;
            row.coldEqual&=State(w)==State(cold);Assert.IsTrue(row.coldEqual,"Actual state, path, orders and events must match after a cold restore");
            Assert.IsTrue(actor.Alive);Assert.AreEqual(hp,actor.hp);Assert.AreEqual(losses,w.Players[0].losses);Assert.AreEqual(-1,w.Winner);
            Assert.IsTrue(w.Navigation.IsWalkable(actor.position,actor.def.radius));Assert.IsTrue(w.Navigation.ClearLine(before,actor.position,actor.def.radius));
            float distance=Vector2.Distance(before,actor.position),height=Mathf.Abs(w.SurfaceHeight(actor.position)-w.SurfaceHeight(before));
            row.peakStep=Mathf.Max(row.peakStep,distance);row.peakHeightStep=Mathf.Max(row.peakHeightStep,height);
            Assert.LessOrEqual(distance,w.Speed(actor)*.1f+.0001f);Assert.LessOrEqual(height,distance*.4f+.0002f);
            if(w.IsWater(actor.position))Assert.IsTrue(w.IsBridge(actor.position));
        }
        static void ReachOutgoingRamp(World w,EntityState actor,Receipt row)
        {
            Move(w,actor,Forward);var cold=Cold(w);int losses=w.Players[0].losses;float hp=actor.hp;var span=w.Layout.Bridges[0];
            for(int i=0;i<900;i++)
            {
                Step(w,cold,actor,row,losses,hp);
                Vector2 local=span.Local(actor.position);
                if(span.Approach(actor.position)&&local.x>span.HalfLength+.1f)
                    Assert.LessOrEqual(local.y,-actor.def.radius-.1f+.0002f,"Through movement stays on its own half of the outgoing ramp");
                if(actor.position.x>73&&actor.position.x<77.25f&&span.Approach(actor.position)&&!span.Contains(actor.position))
                {
                    row.checkpoint=actor.position;row.checkpointIndex=actor.orders.Peek().bridgeTransitIndex;
                    row.checkpointDirection=actor.orders.Peek().bridgeTransitDirection;return;
                }
            }
            Assert.Fail("The unit must actually reach the outgoing ramp before the forced replan");
        }
        static void Save(Receipt row)
        {
            string output=Environment.GetEnvironmentVariable("FRONTIER082_EVIDENCE");if(string.IsNullOrEmpty(output))output=Path.Combine(Directory.GetCurrentDirectory(),"Builds","BridgeTraffic082");
            Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"bridge-transit-"+row.label+".json"),JsonUtility.ToJson(row,true));
        }
        [Test] public void OutgoingRampReplanPreservesThroughDirectionAndExactColdContinuation()
        {
            var row=new Receipt{label="outgoing-replan",status="running",sourceFingerprint=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT")};
            try
            {
                var w=Arena(out var actor);ReachOutgoingRamp(w,actor,row);var span=w.Layout.Bridges[0];
                Assert.IsTrue(w.Navigation.IsWalkable(Remote,0));int oldVersion=w.Navigation.Version;
                w.Navigation.SetBlocked(Remote,0,true);Assert.Greater(w.Navigation.Version,oldVersion);row.replannedVersion=w.Navigation.Version;
                Assert.AreNotEqual(w.Navigation.Version,actor.pathVersion,"The next actual Tick must rebuild the old path");
                string beforeQuery=State(w);var find=typeof(World).GetMethod("FindMovementPath",BindingFlags.Instance|BindingFlags.NonPublic);
                var query=(List<Vector2>)find.Invoke(w,new object[]{actor,Forward,null,null});Assert.GreaterOrEqual(query.Count,2);
                row.queryUnchanged=beforeQuery==State(w);Assert.IsTrue(row.queryUnchanged,"A query reads transit context without committing it");
                var cold=Cold(w);int losses=w.Players[0].losses;float hp=actor.hp;
                for(int i=0;i<900&&actor.orders.Count>0;i++)
                {
                    Step(w,cold,actor,row,losses,hp);Vector2 local=span.Local(actor.position);
                    if(span.Approach(actor.position))
                    {
                        Assert.LessOrEqual(local.y,-actor.def.radius-.1f+.0002f,"A shortened outgoing-ramp path must not cut back into the opposing half");
                        Assert.AreEqual(1,actor.orders.Peek().bridgeTransitIndex);Assert.AreEqual(1,actor.orders.Peek().bridgeTransitDirection);
                    }
                    else if(actor.orders.Count>0)
                    {
                        Assert.AreEqual(0,actor.orders.Peek().bridgeTransitIndex);Assert.AreEqual(0,actor.orders.Peek().bridgeTransitDirection);row.clearedAfterExit=true;
                    }
                    if(i==0)Assert.AreEqual(w.Navigation.Version,actor.pathVersion,"The real route was rebuilt after invalidation");
                }
                row.final=actor.position;row.arrived=actor.orders.Count==0&&Vector2.Distance(actor.position,Forward)<.051f;
                Assert.IsTrue(row.arrived);Assert.IsTrue(row.clearedAfterExit);Assert.AreEqual(1,row.checkpointIndex);Assert.AreEqual(1,row.checkpointDirection);row.status="passed";
            }
            catch(Exception e){row.status="failed";row.error=e.ToString();throw;}
            finally{Save(row);}
        }
        [Test] public void StopAndANewReverseMoveStartFreshTransitInsteadOfReusingTheOutgoingDirection()
        {
            var row=new Receipt{label="stop-reverse",status="running",sourceFingerprint=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT")};
            try
            {
                var w=Arena(out var actor);ReachOutgoingRamp(w,actor,row);var old=actor.orders.Peek();
                Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,order=OrderKind.Stop,actors=new[]{actor.id}}).accepted);Assert.IsEmpty(actor.orders);
                Move(w,actor,Back);var current=actor.orders.Peek();Assert.AreNotSame(old,current);
                Assert.AreEqual(0,current.bridgeTransitIndex);Assert.AreEqual(0,current.bridgeTransitDirection);
                var cold=Cold(w);int losses=w.Players[0].losses;float hp=actor.hp;var span=w.Layout.Bridges[0];
                for(int i=0;i<900&&actor.orders.Count>0;i++)
                {
                    Step(w,cold,actor,row,losses,hp);
                    if(i==0){Assert.AreEqual(1,current.bridgeTransitIndex);Assert.AreEqual(-1,current.bridgeTransitDirection);}
                    if(w.IsWater(actor.position))
                    {
                        Assert.IsTrue(span.Contains(actor.position));Assert.LessOrEqual(-span.Local(actor.position).y,-actor.def.radius-.1f+.0002f,"The new westbound order uses its own right half");row.sawReverseDeck=true;
                    }
                    if(!span.Approach(actor.position)&&actor.orders.Count>0)
                    {Assert.AreEqual(0,current.bridgeTransitIndex);Assert.AreEqual(0,current.bridgeTransitDirection);row.clearedAfterExit=true;}
                }
                row.final=actor.position;row.arrived=actor.orders.Count==0&&Vector2.Distance(actor.position,Back)<.051f;
                Assert.IsTrue(row.arrived);Assert.IsTrue(row.sawReverseDeck);Assert.IsTrue(row.clearedAfterExit);row.status="passed";
            }
            catch(Exception e){row.status="failed";row.error=e.ToString();throw;}
            finally{Save(row);}
        }
        [TestCase(-1,0)] [TestCase(0,1)] [TestCase(1,0)] [TestCase(1,2)] [TestCase(3,1)]
        public void ColdRestoreRejectsContradictoryOrUnknownBridgeTransit(int index,int direction)
        {
            var w=Arena(out var actor,new Vector2(74,39));Move(w,actor,Forward);
            var valid=SnapshotCodec.Clone(w.CaptureState());var order=valid.entities.Single(e=>e.state.id==actor.id).orders[0];
            order.bridgeTransitIndex=1;order.bridgeTransitDirection=1;Assert.DoesNotThrow(()=>World.Restore(w.Rules,valid),"A valid active land transit is accepted");
            var malformed=SnapshotCodec.Clone(valid);order=malformed.entities.Single(e=>e.state.id==actor.id).orders[0];order.bridgeTransitIndex=index;order.bridgeTransitDirection=direction;
            Assert.Throws<ArgumentException>(()=>World.Restore(w.Rules,malformed));
        }
    }
}
