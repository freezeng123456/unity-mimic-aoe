using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class BridgeActual45Replay082Tests
    {
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static string FixtureRoot=>Path.Combine(Application.dataPath,"Frontier","Tests","EditMode","Fixtures");
        const string WorldName="tributaries-crowd45-world.json",RulesName="tributaries-crowd45-rules.json";
        [Serializable] sealed class Row
        {
            public int tick,actor,waypoints,pathIndex,firstInvalidLeg=-1;public string kind;public bool queryAtRecordedPlanningOrigin;public Vector2 current,planningOrigin,goal;public float originShift;
            public bool navigationClear=true;public Vector2 invalidStart,invalidEnd;public Vector2[] path;public UnitOrder[] orders;
            public bool planningOriginChecked,planningOriginNavigationClear;public int planningOriginFirstInvalidLeg=-1;
            public Vector2 planningOriginInvalidStart,planningOriginInvalidEnd;public string validity;
        }
        [Serializable] sealed class Receipt
        {
            public string sourceFingerprint,fixtureWorldSha256,fixtureRulesSha256;public int ticks,changedRoutes,fineQueries;public bool worldQueriesUnchanged=true;public Row[] rows;
            public string scope="Unmodified actual45 World/rules restored; historical pure queries or up to50 actual ticks; no path injection, queue injection,400-arrival or throughput claim";
        }
        static string Hash(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        static World Load(out Receipt receipt)
        {
            string root=Environment.GetEnvironmentVariable("FRONTIER082_FIXTURES45");if(string.IsNullOrEmpty(root))root=FixtureRoot;
            string world=Path.Combine(root,WorldName),rules=Path.Combine(root,RulesName);
            Assert.AreEqual("858345a364967e98369b1f4cebd063c0f2d5870b837e1ebe60e7105b3c6fc850",Hash(world));
            Assert.AreEqual("b08413261c770b536bcc25f39187f237b5d83e753c93a55b4cfd6cc8b3ad0ba4",Hash(rules));
            receipt=new Receipt{sourceFingerprint=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT"),fixtureWorldSha256=Hash(world),fixtureRulesSha256=Hash(rules)};
            return World.Restore(JsonUtility.FromJson<RuleCatalog>(File.ReadAllText(rules)),JsonUtility.FromJson<WorldSnapshot>(File.ReadAllText(world)));
        }
        static Row Inspect(World w,EntityState actor,int tick,bool atPlanningOrigin=false)
        {
            // A query-only proxy recovers the actual saved planning origin for
            // historical geometry. It is never inserted in World or assigned a
            // path. Copy the actual post-Tick orders so saved transit context is
            // preserved in this query; this does not reconstruct pre-Tick state.
            var orders=actor.orders.Select(order=>SnapshotCodec.Clone(order)).ToArray();
            var probe=atPlanningOrigin?new EntityState{id=actor.id,position=actor.path[0],def=actor.def}:actor;
            if(atPlanningOrigin)foreach(var order in orders)probe.orders.Enqueue(order);
            int first=atPlanningOrigin?1:actor.pathIndex;
            var row=new Row{tick=tick,actor=actor.id,kind=actor.def.id,queryAtRecordedPlanningOrigin=atPlanningOrigin,current=probe.position,planningOrigin=actor.path[0],originShift=Vector2.Distance(probe.position,actor.path[0]),goal=actor.pathGoal,pathIndex=first,waypoints=actor.path.Count,path=actor.path.ToArray(),orders=orders};
            typeof(World).GetMethod("PrepareBridgeTraffic",Flags).Invoke(w,new object[]{probe,actor.pathGoal,actor.path});
            var line=typeof(World).GetMethod("MovementLineFree",Flags);Vector2 start=probe.position;
            // Executed path prefixes are historical. Only the current leg and
            // future suffix are required by the actor's next actual movement.
            for(int i=first;i<actor.path.Count;i++)
            {
                Vector2 end=actor.path[i];bool navigation=(actor.def.waterborne?w.WaterNavigation:w.Navigation).ClearLine(start,end,actor.def.radius);
                bool free=(bool)line.Invoke(w,new object[]{probe,start,end});
                if(!navigation)row.navigationClear=false;
                if(!free&&row.firstInvalidLeg<0){row.firstInvalidLeg=i;row.invalidStart=start;row.invalidEnd=end;}
                start=end;
            }
            return row;
        }
        static void AttachPlanningOrigin(Row current,Row origin)
        {
            current.planningOriginChecked=true;current.planningOriginNavigationClear=origin.navigationClear;
            current.planningOriginFirstInvalidLeg=origin.firstInvalidLeg;current.planningOriginInvalidStart=origin.invalidStart;current.planningOriginInvalidEnd=origin.invalidEnd;
            current.validity=current.firstInvalidLeg>=0?(origin.firstInvalidLeg>=0?"invalid-at-current-and-planning-origin":"current-only-invalid"):
                (origin.firstInvalidLeg>=0?"historical-prefix-only-invalid":"valid-at-both-origins");
        }
        [TestCase(262)] [TestCase(263)] [TestCase(305)]
        public void Actual45HistoricalPendingSuffixIsDiagnosedWithoutChangingTheWorld(int id)
        {
            var w=Load(out var receipt);var actor=w.Get(id);Assert.NotNull(actor);Assert.Greater(actor.path.Count,1);Assert.Less(actor.pathIndex,actor.path.Count);
            string before=JsonUtility.ToJson(w.CaptureState());var row=Inspect(w,actor,-1);var originRow=Inspect(w,actor,-1,true);AttachPlanningOrigin(row,originRow);receipt.rows=new[]{row,originRow};receipt.worldQueriesUnchanged=before==JsonUtility.ToJson(w.CaptureState());
            Write("bridge-actual45-historical-"+id,receipt);Assert.IsTrue(receipt.worldQueriesUnchanged);
            Assert.IsTrue(row.navigationClear,"The original static body sweep must remain valid");Assert.IsTrue(originRow.navigationClear);Assert.GreaterOrEqual(originRow.firstInvalidLeg,0,"This actual recorded route was invalid even at its original planning origin");Assert.GreaterOrEqual(row.firstInvalidLeg,0,"This historical actual path is expected to contain a pending lane violation; this is diagnostic evidence, not a producer regression pass");
        }
        [Test] public void PathsRegeneratedByActual45TicksAreValidAtTheirPlanningOriginAndActualStepsStayLegal()
        {
            var w=Load(out var receipt);var rows=new List<Row>();var old=w.Entities.ToDictionary(e=>e.id,e=>e.path.ToArray());
            int initialFine=(int)typeof(World).GetField("fineRecoveryAttempts",Flags).GetValue(w);bool bad=false;
            for(int tick=0;tick<50&&!bad;tick++)
            {
                int previousFine=(int)typeof(World).GetField("fineRecoveryAttempts",Flags).GetValue(w);w.Tick(.1f);receipt.ticks=tick+1;
                int afterFine=(int)typeof(World).GetField("fineRecoveryAttempts",Flags).GetValue(w);Assert.LessOrEqual(afterFine-previousFine,1,"Actual replay must preserve the single expensive fine-query budget");
                foreach(var actor in w.Entities.Where(e=>e.Alive&&e.def.kind==EntityKind.Unit&&e.garrisonedIn==0))
                {
                    var navigation=w.NavigationFor(actor);float distance=Vector2.Distance(actor.previousPosition,actor.position);
                    Assert.IsTrue(navigation.IsWalkable(actor.position,actor.def.radius));
                    Assert.IsTrue(navigation.ClearLine(actor.previousPosition,actor.position,actor.def.radius),"The actual movement step must remain legal");
                    Assert.LessOrEqual(distance,w.Speed(actor)*.1f+.0001f);
                    Assert.LessOrEqual(Mathf.Abs(w.SurfaceHeight(actor.position,actor.def.waterborne)-w.SurfaceHeight(actor.previousPosition,actor.def.waterborne)),distance*.4f+.0002f);
                }
                string before=JsonUtility.ToJson(w.CaptureState());
                foreach(var actor in w.Entities)
                {
                    bool changed=!old.TryGetValue(actor.id,out var prior)||!prior.SequenceEqual(actor.path);old[actor.id]=actor.path.ToArray();
                    if(!changed||!actor.Alive||actor.def.kind!=EntityKind.Unit||actor.path.Count<2||actor.pathIndex>=actor.path.Count)continue;
                    receipt.changedRoutes++;var row=Inspect(w,actor,tick);var originRow=Inspect(w,actor,tick,true);AttachPlanningOrigin(row,originRow);
                    // Steering can legitimately occlude the old next corner;
                    // MoveCore checks routeOccluded on the following Tick. Keep
                    // that current-origin diagnosis, but validate the producer
                    // at its saved origin and the actual movement independently.
                    rows.Add(row);if(originRow.firstInvalidLeg>=0)bad=true;
                }
                receipt.worldQueriesUnchanged&=before==JsonUtility.ToJson(w.CaptureState());Assert.IsTrue(receipt.worldQueriesUnchanged,"Post-Tick route inspections must be pure");
            }
            receipt.fineQueries=(int)typeof(World).GetField("fineRecoveryAttempts",Flags).GetValue(w)-initialFine;receipt.rows=rows.ToArray();Write("bridge-actual45-regenerated",receipt);
            Assert.Greater(receipt.changedRoutes,0,"A replay without any actual regenerated route does not validate the producer");
            Assert.IsFalse(bad,"An actual Tick regenerated a route invalid at its saved planning origin; post-steering corner occlusion is recorded separately");
        }
        static void Write(string name,Receipt receipt)
        {
            string output=Environment.GetEnvironmentVariable("FRONTIER082_EVIDENCE");if(string.IsNullOrEmpty(output))output=Path.Combine(Directory.GetCurrentDirectory(),"Builds","BridgeTraffic082");Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output,name+".json"),JsonUtility.ToJson(receipt,true));
        }
    }
}
