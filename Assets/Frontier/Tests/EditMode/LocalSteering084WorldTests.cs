using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Public-command integration checks. No saved path, queue, timer, speed,
    // radius or arrival threshold is injected. Kernel checks are a separate
    // standalone program and do not count as these actual World tests.
    public sealed class LocalSteering084WorldTests
    {
        [Serializable] private sealed class Receipt
        {
            public string sourceFingerprint,scene,status,error,beforeWorldSha256;
            public string scope="2-5 actual public-Move actors, unchanged radii/speeds/goals/240s, full-state cold continuation and sequential physical/Nav/slope checks; not original400 or native/FPS acceptance";
            public int ticks,fineQueries;public float elapsed,peakStep,minBodyGap=float.MaxValue;
            public bool coldEqual=true,arrived,stoppedUnchanged=true;
            public int[] actors;public Vector2[] starts,goals,finals;
        }
        private static string State(World world)=>JsonUtility.ToJson(world.CaptureState());
        private static float DistanceToSegment(Vector2 a,Vector2 b,Vector2 p)
        {var delta=b-a;float t=delta.sqrMagnitude>1e-12f?Mathf.Clamp01(Vector2.Dot(p-a,delta)/delta.sqrMagnitude):0;return Vector2.Distance(a+delta*t,p);}
        private static void Order(World world,EntityState actor,OrderKind kind,Vector2 goal=default)
        {
            var result=world.Issue(new GameCommand{player=0,kind=CommandKind.Order,order=kind,actors=new[]{actor.id},point=goal});
            Assert.IsTrue(result.accepted,result.reason);
            if(kind==OrderKind.Move)Assert.AreEqual(goal,actor.orders.Peek().point);
        }
        private static World Arena(string map)
        {
            var world=new World(Catalog.Load(),314159,"coast","coast",5,map);
            foreach(var actor in world.Entities.Where(e=>e.def.id!="town").ToArray())world.Remove(actor);
            // Open-ground cases isolate local movement on the real map. The
            // recorded62 case keeps the actual tributaries static geometry.
            if(map=="frontier")foreach(var obstacle in world.TerrainObstacles)
                world.Navigation.SetBlocked(new Vector2(obstacle.x,obstacle.y),obstacle.z,false);
            return world;
        }
        private static EntityState Spawn(World world,List<EntityState> actors,string kind,Vector2 point)
        {var actor=world.Spawn(kind,0,point);Assert.IsTrue(world.Navigation.IsWalkable(point,actor.def.radius),kind+" fixture origin not walkable");actors.Add(actor);return actor;}
        private static void Move(World world,Dictionary<int,Vector2> goals,EntityState actor,Vector2 goal)
        {Order(world,actor,OrderKind.Move,goal);goals.Add(actor.id,goal);}
        [Test] public void Actual62FullWorldReplayKeepsColdAndLetsBothFrontActorsLeaveTheirRecordedStall()
        {
            string root=Environment.GetEnvironmentVariable("FRONTIER084_FIXTURES62");
            if(string.IsNullOrEmpty(root))root=Path.Combine(Application.dataPath,"Frontier","Tests","EditMode","Fixtures");
            string worldFile=Path.Combine(root,"tributaries-headway62-world.json"),rulesFile=Path.Combine(root,"tributaries-headway62-rules.json");
            Assert.AreEqual("63361ed86a753d0752ec62543cb4bbca9662955062a4175f6c796d54a5ff1462",SnapshotCodec.Hash(File.ReadAllText(worldFile)));
            Assert.AreEqual("b08413261c770b536bcc25f39187f237b5d83e753c93a55b4cfd6cc8b3ad0ba4",SnapshotCodec.Hash(File.ReadAllText(rulesFile)));
            var rules=JsonUtility.FromJson<RuleCatalog>(File.ReadAllText(rulesFile));
            var world=World.Restore(rules,JsonUtility.FromJson<WorldSnapshot>(File.ReadAllText(worldFile)));
            var actors=new[]{world.Get(233),world.Get(459)};foreach(var actor in actors)Assert.NotNull(actor);
            var receipt=new Receipt{sourceFingerprint=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT"),scene="actual62-full-world-front-pair",status="running",
                scope="Unmodified full actual62 World/rules,50 real Tick continuation, focused actors233/459, full-state cold/Nav/body/speed/slope; no path injection and not original240s400-arrival acceptance",
                actors=actors.Select(e=>e.id).ToArray(),starts=actors.Select(e=>e.position).ToArray(),goals=actors.Select(e=>e.orders.Peek().point).ToArray(),beforeWorldSha256=SnapshotCodec.Hash(State(world))};
            try
            {
                var cold=World.Restore(rules,SnapshotCodec.Clone(world.CaptureState()));
                var fine=typeof(World).GetField("fineRecoveryAttempts",BindingFlags.Instance|BindingFlags.NonPublic);int initialFine=(int)fine.GetValue(world);
                for(int tick=0;tick<50;tick++)
                {
                    var bodies=world.Entities.Where(e=>e.Alive&&e.garrisonedIn==0&&e.def.kind==EntityKind.Unit&&e.owner==actors[0].owner&&!e.def.waterborne).ToArray();
                    var before=bodies.ToDictionary(e=>e.id,e=>e.position);
                    var order=world.Entities.Select((e,i)=>new{e.id,index=i}).ToDictionary(e=>e.id,e=>e.index);
                    int fineBefore=(int)fine.GetValue(world);world.Tick(.1f);cold.Tick(.1f);receipt.ticks++;receipt.elapsed+=.1f;
                    Assert.LessOrEqual((int)fine.GetValue(world)-fineBefore,1,"Single expensive fine-query budget changed");
                    receipt.coldEqual&=State(world)==State(cold);Assert.IsTrue(receipt.coldEqual,"Full restored actual62 state diverged");
                    foreach(var actor in actors)
                    {
                        Assert.IsTrue(actor.Alive);Assert.IsTrue(world.Navigation.IsWalkable(actor.position,actor.def.radius));
                        Assert.IsTrue(world.Navigation.ClearLine(before[actor.id],actor.position,actor.def.radius));
                        float length=Vector2.Distance(before[actor.id],actor.position);receipt.peakStep=Mathf.Max(receipt.peakStep,length);
                        Assert.LessOrEqual(length,world.Speed(actor)*.1f+.0001f);
                        Assert.LessOrEqual(Mathf.Abs(world.SurfaceHeight(actor.position)-world.SurfaceHeight(before[actor.id])),length*.4f+.0002f);
                        foreach(var other in bodies.Where(e=>e.id!=actor.id&&e.Alive))
                        {
                            Vector2 body=order[other.id]<order[actor.id]?other.position:before[other.id];
                            float gap=DistanceToSegment(before[actor.id],actor.position,body)-actor.def.radius-other.def.radius;
                            receipt.minBodyGap=Mathf.Min(receipt.minBodyGap,gap);Assert.GreaterOrEqual(gap,-.00001f,"Actual62 front actor crossed a sequential body");
                        }
                        Assert.IsNotEmpty(actor.orders);Assert.AreEqual(receipt.goals[Array.IndexOf(actors,actor)],actor.orders.Peek().point);
                    }
                    if(tick%7==0)cold=World.Restore(rules,SnapshotCodec.Clone(world.CaptureState()));
                }
                receipt.fineQueries=(int)fine.GetValue(world)-initialFine;
                for(int i=0;i<actors.Length;i++)Assert.Greater(Vector2.Distance(receipt.starts[i],actors[i].position),.1f,"Actual62 front actor"+actors[i].id+" still cannot leave its recorded stall; diagnose lane/coordination separately without weakening masks");
                receipt.status="passed";
            }
            catch(Exception error){receipt.status="failed";receipt.error=error.ToString();throw;}
            finally
            {
                receipt.finals=actors.Select(e=>e.position).ToArray();string output=Environment.GetEnvironmentVariable("FRONTIER084_EVIDENCE");
                if(string.IsNullOrEmpty(output))output=Environment.GetEnvironmentVariable("FRONTIER082_EVIDENCE");
                if(string.IsNullOrEmpty(output))output=Path.Combine(Directory.GetCurrentDirectory(),"Builds","Movement084");Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output,"local-steering084-actual62-full-world-front-pair.json"),JsonUtility.ToJson(receipt,true));
                if(receipt.status!="passed")File.WriteAllText(Path.Combine(output,"local-steering084-actual62-full-world-front-pair-world.json"),State(world));
            }
        }
        [TestCase("head-on")] [TestCase("merge-three")] [TestCase("follow-mixed")]
        [TestCase("stop-obstacle")] [TestCase("counterflow-five")] [TestCase("recorded62-front-pair")]
        public void LocalInteractionsCompleteTheirExactPublicGoalsWithColdAndHardSweeps(string scene)
        {
            var world=Arena(scene=="recorded62-front-pair"?"tributaries":"frontier");
            var actors=new List<EntityState>();var goals=new Dictionary<int,Vector2>();var stopped=new Dictionary<int,Vector2>();
            if(scene=="head-on")
            {
                Move(world,goals,Spawn(world,actors,"hussar",new Vector2(39,62)),new Vector2(85,62));
                Move(world,goals,Spawn(world,actors,"dragoon",new Vector2(85,62)),new Vector2(39,62));
            }
            else if(scene=="merge-three")
            {
                Move(world,goals,Spawn(world,actors,"hussar",new Vector2(40,56)),new Vector2(84,68));
                Move(world,goals,Spawn(world,actors,"cannon",new Vector2(40,68)),new Vector2(84,56));
                Move(world,goals,Spawn(world,actors,"dragoon",new Vector2(37,62)),new Vector2(88,62));
            }
            else if(scene=="follow-mixed")
            {
                Move(world,goals,Spawn(world,actors,"great_bombard",new Vector2(43,62)),new Vector2(84,62));
                Move(world,goals,Spawn(world,actors,"hussar",new Vector2(40,62)),new Vector2(81,62));
            }
            else if(scene=="stop-obstacle")
            {
                var obstacle=Spawn(world,actors,"great_bombard",new Vector2(62,62));Order(world,obstacle,OrderKind.Stop);stopped.Add(obstacle.id,obstacle.position);
                Move(world,goals,Spawn(world,actors,"hussar",new Vector2(42,62)),new Vector2(84,62));
            }
            else if(scene=="counterflow-five")
            {
                Move(world,goals,Spawn(world,actors,"hussar",new Vector2(42,60)),new Vector2(82,60));
                Move(world,goals,Spawn(world,actors,"musketeer",new Vector2(42,62)),new Vector2(82,62));
                Move(world,goals,Spawn(world,actors,"dragoon",new Vector2(42,64)),new Vector2(82,64));
                Move(world,goals,Spawn(world,actors,"dragoon",new Vector2(82,61)),new Vector2(42,61));
                Move(world,goals,Spawn(world,actors,"great_bombard",new Vector2(82,63)),new Vector2(42,63));
            }
            else
            {
                // Actual62's recorded two front positions and original public
                // goals, not shortened next-waypoint goals. Public planning may
                // choose a different route after the other398 units are absent.
                Move(world,goals,Spawn(world,actors,"hussar",new Vector2(65.38793182373047f,23.99160385131836f)),new Vector2(117.5f,73.9000015258789f));
                Move(world,goals,Spawn(world,actors,"dragoon",new Vector2(65.85425567626953f,22.724891662597656f)),new Vector2(23.700000762939453f,83.80000305175781f));
            }
            var receipt=new Receipt{sourceFingerprint=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT"),scene=scene,status="running",
                actors=actors.Select(e=>e.id).ToArray(),starts=actors.Select(e=>e.position).ToArray(),goals=actors.Select(e=>goals.TryGetValue(e.id,out var goal)?goal:e.position).ToArray()};
            try
            {
                foreach(var actor in actors)foreach(var other in actors.Where(e=>e.id>actor.id))
                    Assert.GreaterOrEqual(Vector2.Distance(actor.position,other.position),actor.def.radius+other.def.radius,"Fixture must not begin with a body overlap");
                receipt.beforeWorldSha256=SnapshotCodec.Hash(State(world));var cold=World.Restore(world.Rules,SnapshotCodec.Clone(world.CaptureState()));
                var fine=typeof(World).GetField("fineRecoveryAttempts",BindingFlags.Instance|BindingFlags.NonPublic);Assert.NotNull(fine);
                int initialFine=(int)fine.GetValue(world),losses=world.Players[0].losses;
                for(int tick=0;tick<2400;tick++)
                {
                    if(goals.All(pair=>world.Get(pair.Key).orders.Count==0&&Vector2.Distance(world.Get(pair.Key).position,pair.Value)<.051f))break;
                    var before=actors.ToDictionary(e=>e.id,e=>e.position);
                    var order=world.Entities.Select((e,i)=>new{e.id,index=i}).ToDictionary(e=>e.id,e=>e.index);
                    int fineBefore=(int)fine.GetValue(world);world.Tick(.1f);cold.Tick(.1f);receipt.ticks++;receipt.elapsed+=.1f;
                    Assert.LessOrEqual((int)fine.GetValue(world)-fineBefore,1,"Local steering must preserve the actual single fine-query budget");
                    receipt.coldEqual&=State(world)==State(cold);Assert.IsTrue(receipt.coldEqual,"Full authoritative state differs after a cold continuation");
                    Assert.AreEqual(losses,world.Players[0].losses);Assert.AreEqual(-1,world.Winner);
                    foreach(var actor in actors)
                    {
                        Assert.IsTrue(actor.Alive&&actor.garrisonedIn==0);Assert.AreEqual(world.MaxHp(actor),actor.hp);
                        Assert.IsTrue(world.Navigation.IsWalkable(actor.position,actor.def.radius));
                        Assert.IsTrue(world.Navigation.ClearLine(before[actor.id],actor.position,actor.def.radius),"Actual static sweep failed");
                        float length=Vector2.Distance(before[actor.id],actor.position);receipt.peakStep=Mathf.Max(receipt.peakStep,length);
                        Assert.LessOrEqual(length,world.Speed(actor)*.1f+.0001f);
                        Assert.LessOrEqual(Mathf.Abs(world.SurfaceHeight(actor.position)-world.SurfaceHeight(before[actor.id])),length*.4f+.0002f);
                        foreach(var other in actors.Where(e=>e.id!=actor.id))
                        {
                            Vector2 body=order[other.id]<order[actor.id]?other.position:before[other.id];
                            float gap=DistanceToSegment(before[actor.id],actor.position,body)-actor.def.radius-other.def.radius;
                            receipt.minBodyGap=Mathf.Min(receipt.minBodyGap,gap);Assert.GreaterOrEqual(gap,-.00001f,"Actual sequential sweep crossed another physical body");
                        }
                        if(stopped.TryGetValue(actor.id,out var fixedPosition))
                        {receipt.stoppedUnchanged&=actor.position==fixedPosition;Assert.IsTrue(receipt.stoppedUnchanged,"Avoidance must not push a Stop actor");}
                        if(goals.TryGetValue(actor.id,out var original)&&actor.orders.Count>0)
                        {Assert.AreEqual(OrderKind.Move,actor.orders.Peek().kind);Assert.AreEqual(original,actor.orders.Peek().point,"Public command goal changed");}
                    }
                    if(tick%13==0)cold=World.Restore(world.Rules,SnapshotCodec.Clone(world.CaptureState()));
                }
                receipt.fineQueries=(int)fine.GetValue(world)-initialFine;
                receipt.arrived=goals.All(pair=>world.Get(pair.Key).orders.Count==0&&Vector2.Distance(world.Get(pair.Key).position,pair.Value)<.051f);
                Assert.IsTrue(receipt.arrived,"Exact original public goals did not complete within240sim seconds");receipt.status="passed";
            }
            catch(Exception error){receipt.status="failed";receipt.error=error.ToString();throw;}
            finally
            {
                receipt.finals=actors.Select(e=>e.position).ToArray();
                string output=Environment.GetEnvironmentVariable("FRONTIER084_EVIDENCE");if(string.IsNullOrEmpty(output))output=Environment.GetEnvironmentVariable("FRONTIER082_EVIDENCE");
                if(string.IsNullOrEmpty(output))output=Path.Combine(Directory.GetCurrentDirectory(),"Builds","Movement084");Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output,"local-steering084-"+scene+".json"),JsonUtility.ToJson(receipt,true));
                if(receipt.status!="passed")File.WriteAllText(Path.Combine(output,"local-steering084-"+scene+"-world.json"),State(world));
            }
        }
    }
}
