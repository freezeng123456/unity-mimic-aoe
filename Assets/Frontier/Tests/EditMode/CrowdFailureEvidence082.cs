using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Frontier.Tests
{
    // Failure evidence only. Never Tick, Issue, FindPath or call private recovery.
    // Capture once BEFORE the old per-actor failure probes can mutate paths.
    internal static class CrowdFailureEvidence082
    {
        [Serializable] private sealed class Neighbor
        { public int id;public string kind,status;public bool stationary;public float radius,distance,physicalGap;public Vector2 position; }
        [Serializable] private sealed class Actor
        {
            public int id,owner,pathIndex,pathCount,bridge;
            public string kind,status,order;
            public bool alive,waterborne,hold,pathAvoidsAllies,orderHasResolvedPoint,stationary;
            public float hp,radius,distanceToAssigned,progressAge,recoveryProgressAge,stuckFor,detourRemaining,pathRetryRemaining;
            public Vector2 position,previousPosition,assignedGoal,orderPoint,orderResolvedPoint,pathGoal,nextWaypoint,heading,bridgeLocal;
            public Neighbor[] nearestAllies;
        }
        [Serializable] private sealed class Bridge
        {
            public int index,landNear,waterNear,positiveFlow,negativeFlow,zeroFlow,stationaryLand,recoveryPaths,detours;
            public Vector2 center,direction;public float halfLength,halfWidth,rampLength,minPhysicalGap;
        }
        [Serializable] private sealed class Evidence
        {
            public int schema=1,seed,actorCount,missed,landMissed,waterMissed;
            public string label,mapId,worldSha256,rulesSha256,snapshotFile,rulesFile,createdUtc;
            public string scope="Unmodified simulation state captured before failure recovery probes; no acceptance changes";
            public float time;public bool worldUnchanged;
            public Actor[] actors;public Bridge[] bridges;
        }
        private static bool Ally(World world,EntityState a,EntityState b)=>
            a.id!=b.id&&b.Alive&&b.garrisonedIn==0&&b.def.kind==EntityKind.Unit&&
            a.def.waterborne==b.def.waterborne&&(a.owner==b.owner||world.AreAllies(a.owner,b.owner));
        private static bool Stationary(EntityState e)
        {
            if(e.hold||e.orders.Count==0)return true;
            var kind=e.orders.Peek().kind;
            return kind!=OrderKind.Move&&kind!=OrderKind.AttackMove&&e.status!="Moving";
        }
        private static Vector2 Next(EntityState e,Vector2 fallback)
        {
            if(e.pathIndex>=0&&e.pathIndex<e.path.Count)return e.path[e.pathIndex];
            if(e.orders.Count==0)return fallback;
            var order=e.orders.Peek();return order.hasResolvedPoint?order.resolvedPoint:order.point;
        }
        // Optional read-only checkpoints expose when a crowd first blocks.
        // The original movement budget and acceptance loop stay unchanged.
        internal static void Progress(World world,IReadOnlyList<EntityState> actors,IReadOnlyDictionary<int,Vector2> goals,string label,int step)
        {
            string progress=Environment.GetEnvironmentVariable("FRONTIER082_CROWD_PROGRESS");
            if(string.IsNullOrEmpty(progress)||step%300!=0)return;
            string evidence=Capture(world,actors,goals,label+"-step"+step.ToString("D4"));
            Directory.CreateDirectory(progress);
            int arrived=actors.Count(a=>!a.def.waterborne&&Vector2.Distance(a.position,goals[a.id])<1);
            File.AppendAllText(Path.Combine(progress,"progress.log"),DateTime.UtcNow.ToString("O")+" "+label+" "+world.MapId+" step="+step+" landArrived="+arrived+" evidence="+evidence+Environment.NewLine);
        }
        internal static string Capture(World world,IReadOnlyList<EntityState> actors,IReadOnlyDictionary<int,Vector2> goals,string label)
        {
            if(world==null||actors==null||goals==null)throw new ArgumentNullException();
            string original=JsonUtility.ToJson(world.CaptureState()),ruleJson=JsonUtility.ToJson(world.Rules);
            var rows=new List<Actor>(actors.Count);
            foreach(var e in actors)
            {
                if(!goals.TryGetValue(e.id,out Vector2 goal))throw new InvalidOperationException("Original assigned goal is missing for "+e.id);
                var order=e.orders.Count>0?e.orders.Peek():null;
                Vector2 next=Next(e,goal);int nearestBridge=-1;Vector2 local=Vector2.zero;float nearestDistance=float.MaxValue;
                for(int i=0;i<world.Layout.Bridges.Length;i++)
                {
                    var span=world.Layout.Bridges[i];Vector2 candidate=span.Local(e.position);
                    float outside=Mathf.Max(0,Mathf.Abs(candidate.x)-span.HalfLength-span.RampLength);
                    float side=Mathf.Max(0,Mathf.Abs(candidate.y)-span.HalfWidth);
                    float distance=outside*outside+side*side;
                    if(distance<nearestDistance){nearestDistance=distance;nearestBridge=i;local=candidate;}
                }
                var neighbors=world.Entities.Where(b=>Ally(world,e,b)).Select(b=>new Neighbor{
                    id=b.id,kind=b.def.id,status=b.status,stationary=Stationary(b),radius=b.def.radius,position=b.position,
                    distance=Vector2.Distance(e.position,b.position),physicalGap=Vector2.Distance(e.position,b.position)-e.def.radius-b.def.radius
                }).OrderBy(n=>n.physicalGap).ThenBy(n=>n.id).Take(8).ToArray();
                rows.Add(new Actor{id=e.id,owner=e.owner,kind=e.def.id,status=e.status,alive=e.Alive,hp=e.hp,radius=e.def.radius,
                    waterborne=e.def.waterborne,hold=e.hold,stationary=Stationary(e),position=e.position,previousPosition=e.previousPosition,
                    assignedGoal=goal,distanceToAssigned=Vector2.Distance(e.position,goal),
                    order=order==null?"":order.kind.ToString(),orderPoint=order==null?Vector2.zero:order.point,
                    orderHasResolvedPoint=order!=null&&order.hasResolvedPoint,orderResolvedPoint=order==null?Vector2.zero:order.resolvedPoint,
                    pathGoal=e.pathGoal,pathIndex=e.pathIndex,pathCount=e.path.Count,nextWaypoint=next,heading=(next-e.position).normalized,
                    pathAvoidsAllies=e.pathAvoidsAllies,progressAge=world.Time-e.progressAt,recoveryProgressAge=world.Time-e.recoveryProgressAt,
                    stuckFor=e.stuckFor,detourRemaining=Mathf.Max(0,e.detourUntil-world.Time),pathRetryRemaining=e.pathRetryAt-world.Time,
                    bridge=nearestBridge,bridgeLocal=local,nearestAllies=neighbors});
            }
            var bridges=new List<Bridge>();
            for(int i=0;i<world.Layout.Bridges.Length;i++)
            {
                var span=world.Layout.Bridges[i];var summary=new Bridge{index=i,center=span.Center,direction=span.Direction,
                    halfLength=span.HalfLength,halfWidth=span.HalfWidth,rampLength=span.RampLength,minPhysicalGap=float.MaxValue};
                foreach(var a in rows)
                {
                    Vector2 local=span.Local(a.position);
                    if(Mathf.Abs(local.x)>span.HalfLength+span.RampLength+12||Mathf.Abs(local.y)>span.HalfWidth+12)continue;
                    if(a.waterborne){summary.waterNear++;continue;}summary.landNear++;
                    float forward=Vector2.Dot(a.heading,span.Direction);
                    if(forward>.1f)summary.positiveFlow++;else if(forward<-.1f)summary.negativeFlow++;else summary.zeroFlow++;
                    if(a.stationary)summary.stationaryLand++;if(a.pathAvoidsAllies)summary.recoveryPaths++;if(a.detourRemaining>0)summary.detours++;
                    foreach(var n in a.nearestAllies)summary.minPhysicalGap=Mathf.Min(summary.minPhysicalGap,n.physicalGap);
                }
                if(summary.minPhysicalGap==float.MaxValue)summary.minPhysicalGap=0;bridges.Add(summary);
            }
            string directory=Environment.GetEnvironmentVariable("FRONTIER082_CROWD_EVIDENCE");
            if(string.IsNullOrEmpty(directory))directory=Path.Combine(Directory.GetCurrentDirectory(),"Builds","Crowd082");
            Directory.CreateDirectory(directory);
            string safe=new string((label??"crowd").Select(c=>char.IsLetterOrDigit(c)||c=='-'||c=='_'?c:'_').ToArray());
            string prefix=Path.Combine(directory,safe+"-"+world.MapId+"-n"+actors.Count);
            string snapshotPath=prefix+"-world.json",rulesPath=prefix+"-rules.json";
            string after=JsonUtility.ToJson(world.CaptureState());bool unchanged=original==after;
            var evidence=new Evidence{label=label,mapId=world.MapId,seed=world.Seed,time=world.Time,actorCount=actors.Count,
                missed=rows.Count(a=>a.distanceToAssigned>=1),landMissed=rows.Count(a=>!a.waterborne&&a.distanceToAssigned>=1),
                waterMissed=rows.Count(a=>a.waterborne&&a.distanceToAssigned>=1),worldSha256=SnapshotCodec.Hash(original),
                rulesSha256=SnapshotCodec.Hash(ruleJson),snapshotFile=snapshotPath,rulesFile=rulesPath,
                createdUtc=DateTime.UtcNow.ToString("O"),worldUnchanged=unchanged,actors=rows.ToArray(),bridges=bridges.ToArray()};
            File.WriteAllText(snapshotPath,original,new UTF8Encoding(false));File.WriteAllText(rulesPath,ruleJson,new UTF8Encoding(false));
            string evidencePath=prefix+"-evidence.json";File.WriteAllText(evidencePath,JsonUtility.ToJson(evidence,true),new UTF8Encoding(false));
            if(!unchanged)throw new InvalidOperationException("Crowd failure evidence unexpectedly changed serialized World; original retained at "+snapshotPath);
            return evidencePath;
        }
    }
}
