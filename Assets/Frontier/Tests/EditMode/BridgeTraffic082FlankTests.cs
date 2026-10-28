using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Declared combat-free geometry fixture. The actual public Move must walk
    // over the south bridge and finish at an exact legal flank, with a cold twin.
    public sealed class BridgeTraffic082FlankTests
    {
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        [Serializable] sealed class Receipt
        {
            public string sourceFingerprint,status,error;
            public string scope="Declared physical navigation fixture using public Move and actual cold continuation; not400-unit throughput, paid production, native rendering orFPS";
            public int direction,flank,staticWaypoints,trafficWaypoints,steps;
            public Vector2 start,goal,final;
            public float elapsed,peakStep,peakHeightStep;
            public bool worldQueryUnchanged,coldEqual,crossedSouth,arrived;
        }
        static string State(World world)=>JsonUtility.ToJson(world.CaptureState());
        static List<Vector2> Query(World world,EntityState actor,Vector2 goal)
        {
            var find=typeof(World).GetMethod("FindMovementPath",Flags);Assert.NotNull(find);
            return (List<Vector2>)find.Invoke(world,new object[]{actor,goal,null,null});
        }
        [TestCase(-1,-1)] [TestCase(-1,1)] [TestCase(1,-1)] [TestCase(1,1)]
        public void PublicMoveCrossesTheBridgeThenReachesItsExactDryFlankAfterColdRestore(int direction,int flank)
        {
            var world=new World(Catalog.Load(),314159,"coast","coast",5,MapTopology.River);
            // Keep both live Towns so this real Tick fixture never manufactures a
            // victory by removing the opposing player. No controller is installed.
            foreach(var entity in world.Entities.Where(e=>e.def.id!="town").ToArray())world.Remove(entity);
            Vector2 start=new Vector2(64-direction*20,40),goal=new Vector2(64+direction*14,40+flank*8);
            var actor=world.Spawn("hussar",0,start);var span=world.Layout.Bridges[0];
            var row=new Receipt{sourceFingerprint=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT"),status="running",direction=direction,flank=flank,start=start,goal=goal};
            try
            {
                Assert.AreEqual(.65f,actor.def.radius,.00001f);Assert.IsTrue(world.Navigation.IsWalkable(start,actor.def.radius));
                Assert.IsTrue(world.Navigation.IsWalkable(goal,actor.def.radius));Assert.IsFalse(world.IsWater(goal));
                Assert.IsFalse(span.Approach(goal),"The target is a legal dry flank outside the actual ramp footprint");
                var ordinary=world.Navigation.FindPath(start,goal,actor.def.radius);row.staticWaypoints=ordinary.Count;
                Assert.GreaterOrEqual(ordinary.Count,2);Assert.Less(Vector2.Distance(ordinary.Last(),goal),.0001f);
                string queryBefore=State(world);var traffic=Query(world,actor,goal);row.trafficWaypoints=traffic.Count;
                row.worldQueryUnchanged=queryBefore==State(world);Assert.IsTrue(row.worldQueryUnchanged,"Planning cannot change serialized World");
                Assert.GreaterOrEqual(traffic.Count,2,"A physically reachable exact flank cannot be rejected by a bridge's expanded guidance box");
                Assert.Less(Vector2.Distance(traffic[0],start),.0001f);Assert.Less(Vector2.Distance(traffic.Last(),goal),.0001f);
                for(int leg=1;leg<traffic.Count;leg++)Assert.IsTrue(world.Navigation.ClearLine(traffic[leg-1],traffic[leg],actor.def.radius));
                var command=new GameCommand{player=0,kind=CommandKind.Order,order=OrderKind.Move,actors=new[]{actor.id},point=goal};
                Assert.IsTrue(world.Issue(command).accepted);Assert.AreEqual(goal,actor.orders.Peek().point);
                var cold=World.Restore(world.Rules,SnapshotCodec.Clone(world.CaptureState()));row.coldEqual=State(world)==State(cold);Assert.IsTrue(row.coldEqual);
                int losses=world.Players[0].losses;float hp=actor.hp;
                for(int i=0;i<2400&&(actor.orders.Count>0||Vector2.Distance(actor.position,goal)>=.051f);i++)
                {
                    Vector2 previous=actor.position;world.Tick(.1f);cold.Tick(.1f);row.steps++;row.elapsed+=.1f;
                    row.coldEqual=State(world)==State(cold);Assert.IsTrue(row.coldEqual,"Actual orders, bodies, paths and events must remain identical after cold restore");
                    Assert.IsTrue(actor.Alive);Assert.AreEqual(hp,actor.hp);Assert.AreEqual(losses,world.Players[0].losses);Assert.AreEqual(-1,world.Winner);
                    Assert.IsTrue(world.Navigation.IsWalkable(actor.position,actor.def.radius));Assert.IsTrue(world.Navigation.ClearLine(previous,actor.position,actor.def.radius));
                    float step=Vector2.Distance(previous,actor.position),height=Mathf.Abs(world.SurfaceHeight(actor.position)-world.SurfaceHeight(previous));
                    row.peakStep=Mathf.Max(row.peakStep,step);row.peakHeightStep=Mathf.Max(row.peakHeightStep,height);
                    Assert.LessOrEqual(step,world.Speed(actor)*.1f+.0001f,"No teleport or increased movement speed");
                    Assert.LessOrEqual(height,step*.4f+.0002f,"Original bridge slope bound");
                    if(world.IsWater(actor.position)){Assert.IsTrue(world.IsBridge(actor.position));row.crossedSouth|=span.Contains(actor.position);}
                    if(actor.orders.Count>0)Assert.AreEqual(goal,actor.orders.Peek().point,"Keep the exact public endpoint throughout the walk");
                    // Re-create the twin during the actual south-deck crossing,
                    // exercising saved paths rather than only an initial query.
                    if(row.crossedSouth&&i%13==0)cold=World.Restore(world.Rules,SnapshotCodec.Clone(world.CaptureState()));
                }
                row.final=actor.position;row.arrived=actor.orders.Count==0&&Vector2.Distance(actor.position,goal)<.051f;
                Assert.IsTrue(row.crossedSouth,"The actual south bridge must carry the route to this nearby flank");
                Assert.IsTrue(row.arrived,"The original exact dry flank must be reached within240sim seconds");
                row.status="passed";
            }
            catch(Exception e){row.status="failed";row.error=e.ToString();throw;}
            finally
            {
                row.final=actor.position;
                string output=Environment.GetEnvironmentVariable("FRONTIER082_EVIDENCE");
                if(string.IsNullOrEmpty(output))output=Path.Combine(Directory.GetCurrentDirectory(),"Builds","BridgeTraffic082");
                Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"bridge-flank-direction"+direction+"-side"+flank+".json"),JsonUtility.ToJson(row,true));
            }
        }
    }
}
