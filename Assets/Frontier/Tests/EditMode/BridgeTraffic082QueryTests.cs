using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class BridgeTraffic082QueryTests
    {
        [Serializable] private sealed class Row
        {public string map,error;public int bridge,direction,waypoints;public float radius;public double milliseconds;public bool worldUnchanged,coldPathEqual;public Vector2 start,goal;}
        [Serializable] private sealed class Receipt {public Row[] rows;}
        private static Vector2 Portal(World world,BridgeSpan span,int side,int direction,bool entering)
        {
            float radius=world.Rules.Entity("great_bombard").radius,end=span.HalfLength+span.RampLength;
            Vector2 desired=span.Point(side*(end+1),-direction*span.HalfWidth*.5f),best=Vector2.zero;float score=float.MaxValue;
            // Establish a genuinely dry fixture endpoint before asking the
            // production planner. Curved rivers need not extend straight on land.
            for(int across=1;across<=14;across++)for(int along=-16;along<=16;along++)
            {
                Vector2 p=span.Point(side*(end+across*.5f),along*.5f);
                if(entering&&along*.5f*direction>-radius-.1f)continue;
                if(!world.Navigation.IsWalkable(p,radius))continue;
                float cost=(p-desired).sqrMagnitude;
                if(cost<score){score=cost;best=p;}
            }
            Assert.Less(score,float.MaxValue,"No actual dry bridge portal; fixture cannot test clearance");return best;
        }
        [TestCase("river")] [TestCase(MapTopology.Winding)] [TestCase(MapTopology.Tropical)]
        [TestCase(MapTopology.Tributaries)] [TestCase(MapTopology.Estuary)]
        public void EveryBridgeKeepsItsActualGreatBombardClearanceInBothTrafficDirections(string map)
        {
            var world=new World(Catalog.Load(),314159,"coast","coast",5,map);
            foreach(var entity in world.Entities.ToArray())world.Remove(entity);
            var rows=new List<Row>();var errors=new List<string>();
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var find=typeof(World).GetMethod("FindMovementPath",flags);var line=typeof(World).GetMethod("MovementLineFree",flags);
            Assert.NotNull(find);Assert.NotNull(line);
            for(int i=0;i<world.Layout.Bridges.Length;i++)foreach(int direction in new[]{-1,1})
            {
                var span=world.Layout.Bridges[i];EntityState actor=null;
                var row=new Row{map=map,bridge=i,direction=direction,radius=world.Rules.Entity("great_bombard").radius};rows.Add(row);
                try
                {
                    Vector2 start=Portal(world,span,-direction,direction,true);
                    Vector2 goal=Portal(world,span,direction,direction,false);row.start=start;row.goal=goal;
                    actor=world.Spawn("great_bombard",0,start);
                    Assert.IsTrue(world.Navigation.IsWalkable(start,actor.def.radius),"Exact bridge approach start");
                    Assert.IsTrue(world.Navigation.IsWalkable(goal,actor.def.radius),"Exact bridge approach goal");
                    string before=JsonUtility.ToJson(world.CaptureState());var snapshot=world.CaptureState();var watch=Stopwatch.StartNew();
                    var path=(List<Vector2>)find.Invoke(world,new object[]{actor,goal,null,null});watch.Stop();row.milliseconds=watch.Elapsed.TotalMilliseconds;row.waypoints=path.Count;
                    row.worldUnchanged=before==JsonUtility.ToJson(world.CaptureState());Assert.IsTrue(row.worldUnchanged,"Query cannot change serialized World");
                    Assert.GreaterOrEqual(path.Count,2);Assert.Less(Vector2.Distance(path[0],start),.0001f);Assert.Less(Vector2.Distance(path[path.Count-1],goal),.0001f);
                    bool usedRequestedDeck=false;
                    for(int leg=1;leg<path.Count;leg++)
                    {
                        Assert.IsTrue(world.Navigation.ClearLine(path[leg-1],path[leg],actor.def.radius),"Authoritative actual navigation mask");
                        Assert.IsTrue((bool)line.Invoke(world,new object[]{actor,path[leg-1],path[leg]}),"Visible skipping must preserve lane filter");
                        int pieces=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(path[leg-1],path[leg])*4));
                        for(int n=0;n<=pieces;n++)
                        {
                            Vector2 p=Vector2.Lerp(path[leg-1],path[leg],(float)n/pieces);if(!span.Contains(p))continue;usedRequestedDeck=true;
                            Assert.LessOrEqual(span.Local(p).y*direction,-actor.def.radius-.1f+.00002f,"Both planned body envelopes separated at median");
                        }
                    }
                    Assert.IsTrue(usedRequestedDeck,"The queried bridge itself must remain usable");
                    var cold=World.Restore(world.Rules,snapshot);var coldPath=(List<Vector2>)find.Invoke(cold,new object[]{cold.Get(actor.id),goal,null,null});
                    row.coldPathEqual=path.SequenceEqual(coldPath);Assert.IsTrue(row.coldPathEqual,"Static query remains identical after cold restore");
                }
                catch(Exception e){row.error=e.ToString();errors.Add(map+" bridge"+i+" direction"+direction+": "+e.Message);}
                if(actor!=null)world.Remove(actor);
            }
            string output=Environment.GetEnvironmentVariable("FRONTIER082_EVIDENCE");if(string.IsNullOrEmpty(output))output=Path.Combine(Directory.GetCurrentDirectory(),"Builds","BridgeTraffic082");Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output,"bridge-traffic-query-"+map+".json"),JsonUtility.ToJson(new Receipt{rows=rows.ToArray()},true));
            Assert.IsEmpty(errors,string.Join("\n",errors));
        }
    }
}
