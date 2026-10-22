using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Query-only consistency over the original400 fixture's public formation
    // endpoints. This does not Tick, inject a path or establish arrival/throughput.
    public sealed class BridgePathConsistency082Tests
    {
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly int[] Samples={0,1,2,3,4,20,49,99,150,199};
        [Serializable] sealed class Rule {public int bridge;public float direction;}
        [Serializable] sealed class Row
        {
            public int actor,side,sample,waypoints,firstInvalidLeg=-1;public string kind,error;
            public Vector2 start,goal;public Vector2[] path;public Rule[] plannedRules,rebuiltRules;
            public bool actualNavigationClear,coldPathEqual;
        }
        [Serializable] sealed class Receipt
        {
            public string map,sourceFingerprint;public bool worldUnchanged;public Row[] rows;
            public string scope="Query-only self-consistency after deriving bridge rules from returned path; no physical movement,400-arrival orFPS claim";
        }
        static Rule[] Rules(World w)
        {
            var list=(IList)typeof(World).GetField("bridgeTrafficRules",Flags).GetValue(w);var rows=new List<Rule>();
            foreach(var entry in list)
            {
                var type=entry.GetType();var span=(BridgeSpan)type.GetField("span").GetValue(entry);
                rows.Add(new Rule{bridge=Array.IndexOf(w.Layout.Bridges,span),direction=(float)type.GetField("direction").GetValue(entry)});
            }
            return rows.ToArray();
        }
        [TestCase(MapTopology.River)] [TestCase(MapTopology.Winding)] [TestCase(MapTopology.Tropical)]
        [TestCase(MapTopology.Tributaries)] [TestCase(MapTopology.Estuary)]
        public void AReturnedOriginalFormationRouteRemainsValidWhenItsOwnBridgeRulesAreRebuilt(string map)
        {
            var w=new World(Catalog.Load(),314159,"coast","coast",5,map);
            foreach(var e in w.Entities.Where(e=>e.def.id!="town").ToArray())w.Remove(e);
            var selected=new List<Tuple<EntityState,int,int>>();string[] kinds={"pike","musketeer","hussar","dragoon","cannon"};
            for(int side=0;side<2;side++)
            {
                var starts=w.Navigation.FormationSlots(new Vector2(side==0?27:101,64),200,2.8f);Assert.AreEqual(200,starts.Count);
                var group=starts.Select((p,i)=>w.Spawn(kinds[i%5],0,p)).ToArray();
                foreach(var actor in group)Assert.IsTrue(w.Navigation.IsWalkable(actor.position,actor.def.radius));
                var result=w.Issue(new GameCommand{player=0,kind=CommandKind.Order,order=OrderKind.Move,actors=group.Select(e=>e.id).ToArray(),point=new Vector2(side==0?101:27,64)});
                Assert.IsTrue(result.accepted,result.reason);foreach(int index in Samples)selected.Add(Tuple.Create(group[index],side,index));
            }
            string before=JsonUtility.ToJson(w.CaptureState());var cold=World.Restore(w.Rules,SnapshotCodec.Clone(w.CaptureState()));
            var find=typeof(World).GetMethod("FindMovementPath",Flags);var prepare=typeof(World).GetMethod("PrepareBridgeTraffic",Flags);var line=typeof(World).GetMethod("MovementLineFree",Flags);
            Assert.NotNull(find);Assert.NotNull(prepare);Assert.NotNull(line);var rows=new List<Row>();var errors=new List<string>();
            foreach(var sample in selected)
            {
                var actor=sample.Item1;Vector2 goal=actor.orders.Peek().point;
                var row=new Row{actor=actor.id,kind=actor.def.id,side=sample.Item2,sample=sample.Item3,start=actor.position,goal=goal};rows.Add(row);
                try
                {
                    var path=(List<Vector2>)find.Invoke(w,new object[]{actor,goal,null,null});row.path=path.ToArray();row.waypoints=path.Count;row.plannedRules=Rules(w);
                    Assert.GreaterOrEqual(path.Count,2,"A public original fixture endpoint needs an actual route");
                    Assert.Less(Vector2.Distance(path[0],actor.position),.0001f);Assert.Less(Vector2.Distance(path.Last(),goal),.0001f);
                    prepare.Invoke(w,new object[]{actor,goal,path});row.rebuiltRules=Rules(w);row.actualNavigationClear=true;
                    for(int leg=1;leg<path.Count;leg++)
                    {
                        if(!w.Navigation.ClearLine(path[leg-1],path[leg],actor.def.radius)){row.actualNavigationClear=false;row.firstInvalidLeg=leg;Assert.Fail("Returned path violates actual navigation");}
                        if(!(bool)line.Invoke(w,new object[]{actor,path[leg-1],path[leg]})){row.firstInvalidLeg=leg;Assert.Fail("Returned path becomes occluded after its own rules are rebuilt");}
                    }
                    var coldPath=(List<Vector2>)find.Invoke(cold,new object[]{cold.Get(actor.id),goal,null,null});row.coldPathEqual=path.SequenceEqual(coldPath);Assert.IsTrue(row.coldPathEqual);
                }
                catch(Exception e){row.error=e.ToString();errors.Add(map+" actor"+actor.id+"/"+actor.def.id+" side"+sample.Item2+" sample"+sample.Item3+": "+e.Message);}
            }
            bool unchanged=before==JsonUtility.ToJson(w.CaptureState());
            string output=Environment.GetEnvironmentVariable("FRONTIER082_EVIDENCE");if(string.IsNullOrEmpty(output))output=Path.Combine(Directory.GetCurrentDirectory(),"Builds","BridgeTraffic082");Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output,"bridge-path-self-consistency-"+map+".json"),JsonUtility.ToJson(new Receipt{map=map,sourceFingerprint=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT"),worldUnchanged=unchanged,rows=rows.ToArray()},true));
            Assert.IsTrue(unchanged,"Queries cannot change serialized World");Assert.IsEmpty(errors,string.Join("\n",errors));
        }
    }
}
