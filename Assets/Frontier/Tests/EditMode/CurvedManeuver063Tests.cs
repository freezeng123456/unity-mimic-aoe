using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class CurvedManeuver063Tests
    {
        [TestCase(MapTopology.Winding,200)] [TestCase(MapTopology.Winding,400)]
        [TestCase(MapTopology.Tropical,200)] [TestCase(MapTopology.Tropical,400)]
        [TestCase(MapTopology.Tributaries,200)] [TestCase(MapTopology.Tributaries,400)]
        [TestCase(MapTopology.Estuary,200)] [TestCase(MapTopology.Estuary,400)]
        public void MixedArmiesCrossBothWaysAsTwelveShipsPassUnderTheCurvedMapBridges(string map,int count)
        {
            // Freeze combat for a movement acceptance scenario. No free speed,
            // teleports, path injections or topology changes are used.
            var rules=JsonUtility.FromJson<RuleCatalog>(JsonUtility.ToJson(Catalog.Load()));
            foreach(var definition in rules.entities){definition.damage=0;definition.meleeDamage=0;definition.siegeDamage=0;}
            var world=new World(rules,314159,"coast","coast",5,map);
            foreach(var entity in world.Entities.Where(e=>e.def.id!="town").ToArray())world.Remove(entity);
            var kinds=new[]{"pike","musketeer","hussar","dragoon","cannon"};
            var actors=new List<EntityState>();var goals=new Dictionary<int,Vector2>();
            var visitedBridge=new HashSet<int>();
            for(int side=0;side<2;side++)
            {
                var group=new List<EntityState>();var center=new Vector2(side==0?27:101,64);
                var starts=world.Navigation.FormationSlots(center,count/2,2.8f);Assert.AreEqual(count/2,starts.Count,$"{map}: army formation capacity");
                for(int i=0;i<starts.Count;i++)
                {
                    var actor=world.Spawn(kinds[i%kinds.Length],0,starts[i]);
                    Assert.IsTrue(world.Navigation.IsWalkable(actor.position,actor.def.radius),$"{map} initial army clearance {actor.id}");group.Add(actor);actors.Add(actor);
                }
                Move(group,new Vector2(side==0?101:27,64));
            }
            for(int side=0;side<2;side++)
            {
                var group=new List<EntityState>();var starts=world.WaterNavigation.FormationSlots(world.Layout.PatrolPoints[side],6,3.9f);
                Assert.AreEqual(6,starts.Count,$"{map}: ship formation capacity");
                for(int i=0;i<starts.Count;i++)
                {
                    var actor=world.Spawn(i%2==0?"fishing_boat":"gunboat",0,starts[i]);
                    Assert.IsTrue(world.WaterNavigation.IsWalkable(actor.position,actor.def.radius),$"{map} initial ship clearance {actor.id}");group.Add(actor);actors.Add(actor);
                }
                Move(group,world.Layout.PatrolPoints[1-side]);
            }
            var tickMs=new List<double>(2400);var watch=new Stopwatch();
            float peakPlanarStep=0,peakHeightStep=0;int stepsTaken=0;
            for(int step=0;step<2400;step++)
            {
                watch.Restart();world.Tick(.1f);watch.Stop();tickMs.Add(watch.Elapsed.TotalMilliseconds);stepsTaken++;
                bool arrived=true;
                foreach(var actor in actors)
                {
                    float distance=Vector2.Distance(actor.position,actor.previousPosition);peakPlanarStep=Mathf.Max(peakPlanarStep,distance);
                    float heightStep=Mathf.Abs(world.SurfaceHeight(actor.position,actor.def.waterborne)-world.SurfaceHeight(actor.previousPosition,actor.def.waterborne));peakHeightStep=Mathf.Max(peakHeightStep,heightStep);
                    if(!world.NavigationFor(actor).IsWalkable(actor.position,actor.def.radius))Fail(actor,step,"left its navigation domain");
                    if(!world.NavigationFor(actor).ClearLine(actor.previousPosition,actor.position,actor.def.radius))Fail(actor,step,"swept through a blocked navigation cell");
                    if(distance>world.Speed(actor)*.1f+.0001f)Fail(actor,step,$"exceeded movement budget by {distance-world.Speed(actor)*.1f:F5}");
                    if(heightStep>distance*.4f+.0002f)Fail(actor,step,$"jumped a bridge edge: distance={distance:F5} height={heightStep:F5}");
                    if(actor.def.waterborne)
                    {
                        if(!world.IsWater(actor.position))Fail(actor,step,"ship reached land");
                        if(world.IsBridge(actor.position))visitedBridge.Add(actor.id);
                    }
                    else if(world.IsWater(actor.position))
                    {
                        if(!world.IsBridge(actor.position))Fail(actor,step,"land army entered open water");
                        visitedBridge.Add(actor.id);
                    }
                    if(Vector2.Distance(actor.position,goals[actor.id])>=1)arrived=false;
                }
                if(arrived)break;
            }
            var missed=actors.Where(actor=>Vector2.Distance(actor.position,goals[actor.id])>=1).ToArray();
            int armyArrived=actors.Count(actor=>!actor.def.waterborne&&Vector2.Distance(actor.position,goals[actor.id])<1);
            int shipsArrived=actors.Count(actor=>actor.def.waterborne&&Vector2.Distance(actor.position,goals[actor.id])<1);
            tickMs.Sort();double p95=Percentile(.95),p99=Percentile(.99),peak=tickMs[tickMs.Count-1];
            TestContext.WriteLine($"Curved maneuver: map={map}; land={count}; landArrived={armyArrived}; ships=12; shipsArrived={shipsArrived}; crossed={visitedBridge.Count}; simSeconds={stepsTaken*.1f:F1}; peakPlanarStep={peakPlanarStep:F5}; peakHeightStep={peakHeightStep:F5}; peakTickMs={peak:F4}; tickP95Ms={p95:F4}; tickP99Ms={p99:F4}");
            WriteFineRecoveryDiagnostics(world);
            foreach(var actor in missed)
            {
                SaveRecoverySnapshot(world,actor,goals[actor.id]);
                var diagnosticFlags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                bool recovered=(bool)typeof(World).GetMethod("TryAllyRecovery",diagnosticFlags).Invoke(world,new object[]{actor,goals[actor.id]});
                var filter=(Func<Vector2,bool>)typeof(World).GetField("recoveryFilter",diagnosticFlags).GetValue(world);
                TestContext.WriteLine($"Failure-only recovery probe: accepted={recovered}; startFilter={filter?.Invoke(actor.position)}; goalFilter={filter?.Invoke(goals[actor.id])}; goalStatic={world.NavigationFor(actor).IsWalkable(goals[actor.id],actor.def.radius)}");
                string Exact(Vector2 point)=>$"({point.x:R},{point.y:R})";
                TestContext.WriteLine($"Missed actor {actor.id}/{actor.def.id}: position={Exact(actor.position)}; goal={Exact(goals[actor.id])}; pathGoal={Exact(actor.pathGoal)}; path={actor.pathIndex}/{actor.path.Count}; avoidsAllies={actor.pathAvoidsAllies}; progressAt={actor.progressAt:R}; recoveryAt={actor.recoveryProgressAt:R}; recoveryAnchor={Exact(actor.recoveryProgressPosition)}; detour={Exact(actor.detour)} until={actor.detourUntil:R}; retry={actor.pathRetryAt:R}");
                foreach(var waypoint in actor.path.Skip(Math.Max(0,actor.pathIndex-1)))TestContext.WriteLine($"  waypoint {Exact(waypoint)} staticClear={world.NavigationFor(actor).ClearLine(actor.position,waypoint,actor.def.radius)}");
                foreach(var other in world.Entities.Where(e=>e.id!=actor.id&&e.Alive&&e.def.kind==EntityKind.Unit&&e.def.waterborne==actor.def.waterborne&&Vector2.Distance(e.position,actor.position)<5))
                    TestContext.WriteLine($"  neighbor {other.id}/{other.def.id}: position={Exact(other.position)}; radius={other.def.radius:R}; status={other.status}; orders={other.orders.Count}; hold={other.hold}; bodyDistance={Vector2.Distance(actor.position,other.position):R}");
            }
            Assert.AreEqual(0,missed.Length,$"{map}: {missed.Length}/{actors.Count} actors did not arrive within 240 simulation seconds: "+
                string.Join("; ",missed.Take(12).Select(actor=>$"{actor.id}/{actor.def.id} {actor.position} -> {goals[actor.id]} {actor.status}")));
            Assert.AreEqual(actors.Count,visitedBridge.Count,$"{map}: every army crosses a bridge and every ship passes beneath one");

            void Move(List<EntityState> group,Vector2 destination)
            {
                var result=world.Issue(new GameCommand{player=0,kind=CommandKind.Order,order=OrderKind.Move,actors=group.Select(actor=>actor.id).ToArray(),point=destination});
                Assert.IsTrue(result.accepted,$"{map}: normal move command failed: {result.reason}");
                foreach(var actor in group){Assert.IsNotEmpty(actor.orders,$"{map}: actor {actor.id} has no move order");goals[actor.id]=actor.orders.Peek().point;}
            }
            void Fail(EntityState actor,int step,string reason)=>Assert.Fail($"{map}: {actor.id}/{actor.def.id} {reason} at step {step}: {actor.previousPosition} -> {actor.position}");
            double Percentile(double probability)=>tickMs[Math.Min(tickMs.Count-1,Math.Max(0,(int)Math.Ceiling(probability*tickMs.Count)-1))];
        }
        [Test] public void RecordedEstuaryFormationAllowsCannonToLeaveThroughABodyClearCorridor()
        {
            string path=System.IO.Path.Combine(Application.dataPath,"Frontier","Tests","EditMode","Fixtures","estuary-formation-recovery420.json");
            var snapshot=JsonUtility.FromJson<RecoverySnapshot>(System.IO.File.ReadAllText(path));
            Assert.AreEqual(314159,snapshot.seed);Assert.AreEqual(MapTopology.Estuary,snapshot.mapId);Assert.AreEqual(414,snapshot.entities.Length);
            var world=RestoreRecoverySnapshot(snapshot);var actor=world.Get(snapshot.actorId);
            var parked=snapshot.entities.Where(e=>e.stationary).ToDictionary(e=>e.id,e=>e.position);
            // The command uses the recorded full formation and blocked cells.
            // Clearing the obsolete detour is an ordinary Stop then Move command.
            Assert.IsTrue(world.Issue(new GameCommand{player=actor.owner,kind=CommandKind.Order,order=OrderKind.Stop,actors=new[]{actor.id}}).accepted);
            Assert.IsTrue(world.Issue(new GameCommand{player=actor.owner,kind=CommandKind.Order,order=OrderKind.Move,actors=new[]{actor.id},point=snapshot.goal}).accepted);
            float elapsed=0;
            for(int step=0;step<2400&&actor.orders.Count>0;step++)
            {
                world.Tick(.1f);elapsed+=.1f;
                Assert.IsTrue(world.Navigation.ClearLine(actor.previousPosition,actor.position,actor.def.radius));
                float distance=Vector2.Distance(actor.previousPosition,actor.position);
                Assert.LessOrEqual(distance,world.Speed(actor)*.1f+.0001f);
                Assert.LessOrEqual(Mathf.Abs(world.SurfaceHeight(actor.position)-world.SurfaceHeight(actor.previousPosition)),distance*.4f+.0002f);
                foreach(var pair in parked)
                {
                    var other=world.Get(pair.Key);Assert.AreEqual(pair.Value,other.position,"Recovery must not move parked allies");
                    Vector2 delta=actor.position-actor.previousPosition;float t=delta.sqrMagnitude>.000001f?Mathf.Clamp01(Vector2.Dot(other.position-actor.previousPosition,delta)/delta.sqrMagnitude):0;
                    Assert.GreaterOrEqual(Vector2.Distance(actor.previousPosition+delta*t,other.position),actor.def.radius+other.def.radius,
                        $"Recovery crossed an ally body at step {step}, time {world.Time:R}, elapsed {elapsed:R}; actor {actor.id}/{actor.def.id} " +
                        $"previous=({actor.previousPosition.x:R},{actor.previousPosition.y:R}) current=({actor.position.x:R},{actor.position.y:R}) radius={actor.def.radius:R}; " +
                        $"other {other.id}/{other.def.id} position=({other.position.x:R},{other.position.y:R}) radius={other.def.radius:R}; " +
                        $"t={t:R}, avoidsAllies={actor.pathAvoidsAllies}, path={actor.pathIndex}/{actor.path.Count}, " +
                        $"pathGoal=({actor.pathGoal.x:R},{actor.pathGoal.y:R}), progressAt={actor.progressAt:R}, recoveryProgressAt={actor.recoveryProgressAt:R}");
                }
            }
            Assert.IsEmpty(actor.orders);Assert.Less(Vector2.Distance(actor.position,snapshot.goal),1);
            TestContext.WriteLine($"Recorded estuary recovery: {snapshot.entities.Length} entities; arrived after {elapsed:F1} simulation seconds");
            WriteFineRecoveryDiagnostics(world);
        }
        private static void WriteFineRecoveryDiagnostics(World world)
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            object Field(string name)=>typeof(World).GetField(name,flags).GetValue(world);
            TestContext.WriteLine($"Fine recovery: coarseAttempts={Field("allyRecoveryCoarseAttempts")}; attempts={Field("fineRecoveryAttempts")}; failures={Field("fineRecoveryFailures")}; deferred={Field("fineRecoveryDeferrals")}; peakQueryMs={Field("fineRecoveryPeakMs")}");
        }
        private static World RestoreRecoverySnapshot(RecoverySnapshot snapshot)
        {
            var world=new World(JsonUtility.FromJson<RuleCatalog>(snapshot.ruleJson),snapshot.seed,"coast","coast",5,snapshot.mapId);
            foreach(var entity in world.Entities.Where(e=>e.Alive).ToArray())world.Remove(entity);world.Entities.Clear();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            ((Dictionary<int,EntityState>)typeof(World).GetField("byId",flags).GetValue(world)).Clear();
            foreach(var source in snapshot.entities)
            {
                typeof(World).GetField("nextId",flags).SetValue(world,source.id);
                var entity=world.Spawn(source.definition,source.owner,source.position);entity.def.radius=source.radius;
                entity.hp=source.hp;entity.construction=source.construction;entity.garrisonedIn=source.garrisonedIn;entity.previousPosition=source.previousPosition;entity.origin=source.origin;entity.facing=source.facing;entity.status=source.status;entity.hold=source.hold;
                entity.pathGoal=source.pathGoal;entity.progressGoal=source.progressGoal;entity.detour=source.detour;entity.recoveryProgressPosition=source.recoveryProgressPosition;entity.pathAvoidsAllies=source.pathAvoidsAllies;entity.pathIndex=source.pathIndex;entity.pathVersion=source.pathVersion;
                entity.pathRetryAt=source.pathRetryAt;entity.progressAt=source.progressAt;entity.progressDistance=source.progressDistance;entity.detourUntil=source.detourUntil;entity.recoveryProgressAt=source.recoveryProgressAt;entity.path.AddRange(source.path);
                foreach(var order in source.orders)entity.orders.Enqueue(new UnitOrder(order.kind,order.point,order.target){resolvedPoint=order.resolvedPoint,hasResolvedPoint=order.hasResolvedPoint});
            }
            var placed=(bool[])typeof(GridNavigation).GetField("blocked",flags).GetValue(world.Navigation);Array.Clear(placed,0,placed.Length);
            world.Navigation.ConfigureTerrain(point=>snapshot.blockedCells[Mathf.RoundToInt(point.x)+Mathf.RoundToInt(point.y)*snapshot.width]);world.Time=snapshot.time;
            typeof(World).GetMethod("RebuildSpatial",flags).Invoke(world,null);return world;
        }
        [Serializable] private sealed class RecoverySnapshot
        {
            public string mapId,ruleJson;public int actorId,seed,width,height;public float time;public Vector2 goal;
            public bool[] blockedCells;public SnapshotEntity[] entities;
        }
        [Serializable] private sealed class SnapshotEntity
        {
            public int id,owner,garrisonedIn;public string definition,status;public float radius,hp,construction;
            public Vector2 position,previousPosition,origin,facing,pathGoal,progressGoal,detour,recoveryProgressPosition;
            public bool hold,stationary,pathAvoidsAllies;public int pathIndex,pathVersion;public float pathRetryAt,progressAt,progressDistance,detourUntil,recoveryProgressAt;
            public Vector2[] path;public SnapshotOrder[] orders;
        }
        [Serializable] private sealed class SnapshotOrder
        {public OrderKind kind;public Vector2 point,resolvedPoint;public bool hasResolvedPoint;public int target;}
        private static void SaveRecoverySnapshot(World world,EntityState actor,Vector2 goal)
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var navigation=world.NavigationFor(actor);
            var placed=(bool[])typeof(GridNavigation).GetField("blocked",flags).GetValue(navigation);
            var terrain=(bool[])typeof(GridNavigation).GetField("terrainBlocked",flags).GetValue(navigation);
            var cells=new bool[placed.Length];for(int i=0;i<cells.Length;i++)cells[i]=placed[i]||terrain[i];
            var stationary=typeof(World).GetMethod("StationaryAlly",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            var snapshot=new RecoverySnapshot{mapId=world.MapId,seed=world.Seed,time=world.Time,actorId=actor.id,goal=goal,width=navigation.Width,height=navigation.Height,blockedCells=cells,ruleJson=JsonUtility.ToJson(world.Rules),
                entities=world.Entities.Where(e=>e.Alive).Select(e=>new SnapshotEntity{
                    id=e.id,owner=e.owner,definition=e.def.id,status=e.status,radius=e.def.radius,hp=e.hp,construction=e.construction,garrisonedIn=e.garrisonedIn,
                    position=e.position,previousPosition=e.previousPosition,origin=e.origin,facing=e.facing,hold=e.hold,stationary=(bool)stationary.Invoke(null,new object[]{actor,e}),
                    pathGoal=e.pathGoal,progressGoal=e.progressGoal,detour=e.detour,recoveryProgressPosition=e.recoveryProgressPosition,pathAvoidsAllies=e.pathAvoidsAllies,pathIndex=e.pathIndex,pathVersion=e.pathVersion,
                    pathRetryAt=e.pathRetryAt,progressAt=e.progressAt,progressDistance=e.progressDistance,detourUntil=e.detourUntil,recoveryProgressAt=e.recoveryProgressAt,path=e.path.ToArray(),
                    orders=e.orders.Select(o=>new SnapshotOrder{kind=o.kind,point=o.point,resolvedPoint=o.resolvedPoint,hasResolvedPoint=o.hasResolvedPoint,target=o.target}).ToArray()
                }).ToArray()};
            string directory=System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(),"Builds","Curves063");System.IO.Directory.CreateDirectory(directory);
            string path=System.IO.Path.Combine(directory,$"recovery-{world.MapId}-{actor.id}.json");System.IO.File.WriteAllText(path,JsonUtility.ToJson(snapshot,true));
            TestContext.WriteLine("Failure recovery snapshot: "+path);
        }
    }
}
