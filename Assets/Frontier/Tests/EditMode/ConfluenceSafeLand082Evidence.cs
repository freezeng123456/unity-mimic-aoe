using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Strict evidence helper called inside the self-contained normal paid test.
    // Query-only planning selects public waypoints around unchanged threats.
    // Actual gameplay uses ordinary independent Move commands and original nav.
    // No spawn/grant, enemy orders, fog/HP/nav mutation, or timing comparison.
    public static class ConfluenceSafeLand082Evidence
    {
        static readonly Vector2 North=new Vector2(50,70);
        const float Dt=.1f,Budget=180,BodyGuard=.15f;
        [Serializable] sealed class Body
        {public int id,orders,container;public string definition,status,order;public Vector2 position,intent,resolved;public bool hasResolved;public float hp,distance;public int[] decks;}
        [Serializable] sealed class Frame {public float time;public Body[] actors;}
        [Serializable] sealed class Issued
        {public float time;public int actor;public bool accepted;public string reason;public Vector2 requested,assigned,resolved;public bool hasResolved;}
        [Serializable] sealed class Arrival
        {public float time;public int actor;public Vector2 requested,assigned,resolved,actual;}
        [Serializable] sealed class Plan
        {public int actor;public float planningRadius,length;public Vector2 start,finish;public Vector2[] waypoints;}
        [Serializable] sealed class Threat
        {public int id,initialAttackTarget,finalAttackTarget;public string definition;public Vector2 initial,final;public float initialHp,finalHp,vision,radius,minObservedCenter=999,minObservedEdge=999;public string initialOrders,finalOrders;}
        [Serializable] sealed class Result
        {
            public int schema=3;
            public string name="two-original-actors-public-physical-boundary-route",scope,snapshotPath,snapshotSha256,sourceFingerprint,rulesHashBefore,rulesHashAfter,landMaskHashBefore,landMaskHashAfter,failure,finalWorldHash;
            public bool completed,snapshotUnchanged;
            public float startTime,elapsed,elapsedToFailure;
            public int lossesBefore,lossesAfter;
            public Cost fundsBefore,fundsAfter,spentBefore,spentAfter;
            public Body[] initial,final;public Plan[] plans;public Threat[] threats;public SimEvent[] events;
            public List<Issued> commands=new List<Issued>();public List<Arrival> arrivals=new List<Arrival>();public List<Frame> frames=new List<Frame>();
        }
        sealed class Active
        {public int id,index;public List<Vector2> points;public UnitOrder order;public bool finished;}
        sealed class Diagnostic
        {
            public readonly World W;public readonly Result R;
            readonly int[] ids;
            readonly Dictionary<int,float> distance;
            readonly Dictionary<int,HashSet<int>> decks;
            readonly EntityState[] threats;readonly int cursor;int ticks;
            public Diagnostic(string json,RuleCatalog rules,string digest,int[] actors)
            {
                ids=actors;distance=ids.ToDictionary(x=>x,x=>0f);decks=ids.ToDictionary(x=>x,x=>new HashSet<int>());
                W=World.Restore(rules,JsonUtility.FromJson<WorldSnapshot>(json));
                R=new Result{snapshotPath="in-memory-normal-paid-world",snapshotSha256=digest,sourceFingerprint=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT"),startTime=W.Time,lossesBefore=W.Players[0].losses,fundsBefore=W.Players[0].resources,spentBefore=W.Players[0].spent,rulesHashBefore=SnapshotCodec.RulesHash(W.Rules),landMaskHashBefore=SnapshotCodec.Hash(JsonUtility.ToJson(W.Navigation.CaptureState())),scope="Strict180s land arrival from the actual normal paid chain after exact distinct southern Move completions. Original worker and paid cannon only; concurrent public waypoints, no escort. Query-only full scenario threat positions choose a tactical route; not an AI/fog benchmark. Intermediate production endpoint adjustments are retained; actual Move completion, final exact independent goals, both bridge decks, original movement limits, no body penetration and no losses are mandatory. No ferry speed-saving claim."};
                foreach(int id in ids)Require(W.Get(id)!=null&&W.Get(id).Alive&&W.Get(id).owner==0&&W.Get(id).garrisonedIn==0,"Missing original actor "+id);
                threats=W.Entities.Where(e=>e.Alive&&e.garrisonedIn==0&&e.def.kind==EntityKind.Unit&&(e.owner==1||World.Guardian(e))&&e.def.damage>0).ToArray();
                R.threats=threats.Select(e=>new Threat{id=e.id,definition=e.def.id,initial=e.position,initialHp=e.hp,vision=e.def.vision,radius=e.def.radius,initialAttackTarget=e.attackTarget,initialOrders=Orders(e)}).ToArray();
                Require(W.Get(59).attackTarget==0,"Original opponent59 already remembers a target at this input snapshot");
                R.initial=Bodies();cursor=W.Events.Count;SaveFrame();
            }
            static string Orders(EntityState e)=>string.Join(";",e.orders.Select(o=>o.kind+":"+o.target+":"+o.point));
            static void Require(bool condition,string reason){if(!condition)throw new InvalidOperationException(reason);}
            Body Describe(int id)
            {
                var e=W.Get(id);var o=e.orders.Count>0?e.orders.Peek():null;
                return new Body{id=id,definition=e.def.id,position=e.position,hp=e.hp,orders=e.orders.Count,container=e.garrisonedIn,status=e.status,order=o?.kind.ToString()??"",intent=o?.point??Vector2.zero,resolved=o?.resolvedPoint??Vector2.zero,hasResolved=o?.hasResolvedPoint??false,distance=distance[id],decks=decks[id].OrderBy(x=>x).ToArray()};
            }
            Body[] Bodies()=>ids.Select(Describe).ToArray();
            void SaveFrame()=>R.frames.Add(new Frame{time=W.Time,actors=Bodies()});
            // FindEnemy's Nearby query acquires by CENTER distance <= vision.
            // Retention later uses EdgeDistance < vision. With initial59 target0,
            // no acquisition is expected outside the center circle. We also
            // reserve both actual body radii plus ordinary .15m parking clearance:
            // cannon margin .4+.75+.15=1.30; worker .4+.4+.15=.95.
            // This is a query-only margin, not a gameplay sight/HP/rule edit.
            bool Quiet(Vector2 p,float movingRadius)=>threats.All(e=>Vector2.Distance(p,e.position)>e.def.vision+e.def.radius+movingRadius+BodyGuard);
            Plan MakePlan(int id)
            {
                var e=W.Get(id);float radius=Mathf.Max(1.8f,e.def.radius*4+.3f)*.38f;
                var route=new List<Vector2>();var start=e.position;var finish=North+new Vector2(W.Get(id).Worker?1.5f:-1.5f,0);
                // IsWalkable supplies the real radius-aware map boundary. There
                // is deliberately no historical8..119 point restriction here.
                Func<Vector2,bool> legal=p=>W.Navigation.IsWalkable(p,radius)&&Quiet(p,e.def.radius);
                foreach(var target in new[]{new Vector2(70,25),finish})
                {
                    var leg=W.Navigation.FindPath(start,target,radius,legal);
                    Require(leg.Count>=2&&Vector2.Distance(leg.Last(),target)<.01f,"No legal threat-avoiding route for original actor "+id+" to "+target);
                    route.AddRange(leg.Skip(1));start=target;
                }
                float length=0;start=e.position;
                foreach(var p in route){Require(legal(p),"Planned waypoint violates actual physical boundary or threat margin");length+=Vector2.Distance(start,p);start=p;}
                return new Plan{actor=id,planningRadius=radius,start=e.position,finish=finish,waypoints=route.ToArray(),length=length};
            }
            void Issue(Active a)
            {
                var requested=a.points[a.index];var result=W.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{a.id},order=OrderKind.Move,point=requested});
                var e=W.Get(a.id);a.order=e.orders.Count>0?e.orders.Peek():null;
                R.commands.Add(new Issued{time=W.Time,actor=a.id,accepted=result.accepted,reason=result.reason,requested=requested,assigned=a.order?.point??Vector2.zero,resolved=a.order?.resolvedPoint??Vector2.zero,hasResolved=a.order?.hasResolvedPoint??false});
                Require(result.accepted&&a.order!=null,"Public Move rejected or missing: "+a.id+" "+result.reason);
                Require(Vector2.Distance(a.order.point,requested)<.01f,"Public formation changed an already physically valid requested waypoint: "+a.id+" requested="+requested+" assigned="+a.order.point);
            }
            void Step()
            {
                Require(W.Time-R.startTime<Budget-.00001f,"Fixed180s whole-case budget exhausted");
                var before=ids.ToDictionary(id=>id,id=>W.Get(id).position);float dt=Mathf.Min(Dt,Budget-(W.Time-R.startTime));W.Tick(dt);ticks++;
                foreach(int id in ids)
                {
                    var e=W.Get(id);float moved=Vector2.Distance(before[id],e.position);distance[id]+=moved;
                    Require(e.Alive&&e.garrisonedIn==0,"Original paid actor lost/contained: "+id);
                    Require(W.Navigation.IsWalkable(e.position,e.def.radius)&&(!W.IsWater(e.position)||W.IsBridge(e.position)),"Land body outside valid navigation: "+id);
                    Require(W.Navigation.ClearLine(before[id],e.position,e.def.radius),"Actor crossed blocked traveled segment: "+id);
                    Require(moved<=W.Speed(e)*dt+.0001f,"Actor exceeded ordinary speed: "+id);
                    Require(Mathf.Abs(W.SurfaceHeight(before[id])-W.SurfaceHeight(e.position))<=moved*.4f+.0002f,"Actor exceeded ordinary slope limit: "+id);
                    foreach(var t in R.threats)
                    {var threat=W.Get(t.id);float center=Vector2.Distance(e.position,threat.position);t.minObservedCenter=Mathf.Min(t.minObservedCenter,center);t.minObservedEdge=Mathf.Min(t.minObservedEdge,center-e.def.radius-threat.def.radius);}
                    int samples=Mathf.Max(1,Mathf.CeilToInt(moved/.25f));
                    for(int s=0;s<=samples;s++)for(int b=0;b<W.Layout.Bridges.Length;b++)if(W.Layout.Bridges[b].Contains(Vector2.Lerp(before[id],e.position,(float)s/samples)))decks[id].Add(b);
                }
                var first=W.Get(ids[0]);var second=W.Get(ids[1]);
                Require(Vector2.Distance(first.position,second.position)>=first.def.radius+second.def.radius-.001f,"Original allied bodies overlap");
                Require(W.Players[0].losses==R.lossesBefore,"Additional original-player loss");Require(!W.IsMatchEnded,"Match ended before complete arrival");
                if(ticks%10==0)SaveFrame();
            }
            public void Execute()
            {
                // Plan both from the same restored world before either moves.
                R.plans=ids.Select(MakePlan).ToArray();
                var active=R.plans.Select(p=>new Active{id=p.actor,points=p.waypoints.ToList()}).ToArray();
                foreach(var a in active)Issue(a);
                while(active.Any(a=>!a.finished))
                {
                    Step();
                    foreach(var a in active.Where(a=>!a.finished))
                    {
                        var e=W.Get(a.id);if(e.orders.Count>0)continue;
                        var goal=a.order.hasResolvedPoint?a.order.resolvedPoint:a.order.point;
                        R.arrivals.Add(new Arrival{time=W.Time,actor=a.id,requested=a.points[a.index],assigned=a.order.point,resolved=goal,actual=e.position});
                        Require(Vector2.Distance(e.position,goal)<.051f,"Public Move dequeued before exact actual endpoint: "+a.id);
                        // A moving ally can legitimately reserve an intermediate
                        // waypoint. Require actual completion of the resolved
                        // public endpoint, and retain exact final destinations.
                        Require(Vector2.Distance(goal,a.points[a.index])<=8.001f,"Intermediate endpoint exceeded production local adjustment range: "+a.id);
                        if(a.index==a.points.Count-1)Require(Vector2.Distance(goal,a.points[a.index])<.01f,"Final independent destination must remain exact: "+a.id);
                        a.index++;if(a.index<a.points.Count)Issue(a);
                        else
                        {
                            a.finished=true;var result=W.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{a.id},order=OrderKind.Hold});
                            R.commands.Add(new Issued{time=W.Time,actor=a.id,accepted=result.accepted,reason="Hold only after actual final Move completion: "+result.reason});
                            Require(result.accepted,"Completed actor could not hold final independent slot");
                        }
                    }
                }
                foreach(int id in ids)
                {
                    var e=W.Get(id);Require(e.Alive&&e.orders.Count==0&&e.garrisonedIn==0&&Vector2.Distance(e.position,North)<6.01f,"Original actor did not actually arrive north: "+id);
                    Require(decks[id].Contains(0)&&decks[id].Contains(1),"Original actor did not actually traverse both main bridge decks: "+id);
                }
                Require(W.Players[0].losses==R.lossesBefore,"Full arrival requires zero additional player losses");
            }
            public void Finish(Exception failure)
            {
                R.elapsed=W.Time-R.startTime;R.lossesAfter=W.Players[0].losses;R.fundsAfter=W.Players[0].resources;R.spentAfter=W.Players[0].spent;
                R.rulesHashAfter=SnapshotCodec.RulesHash(W.Rules);R.landMaskHashAfter=SnapshotCodec.Hash(JsonUtility.ToJson(W.Navigation.CaptureState()));
                if(failure==null&&(R.rulesHashBefore!=R.rulesHashAfter||R.landMaskHashBefore!=R.landMaskHashAfter))failure=new InvalidOperationException("Rules or exact navigation mask changed during diagnostic");
                R.completed=failure==null;R.failure=failure?.ToString()??"";R.elapsedToFailure=R.completed?0:R.elapsed;R.final=Bodies();R.events=W.Events.Skip(cursor).ToArray();SaveFrame();
                foreach(var t in R.threats){var e=W.Get(t.id);t.final=e.position;t.finalHp=e.hp;t.finalAttackTarget=e.attackTarget;t.finalOrders=Orders(e);}
                R.finalWorldHash=SnapshotCodec.Hash(JsonUtility.ToJson(W.CaptureState()));
            }
        }
        public static void Verify(World original,int worker,int cannon)
        {
            string json=JsonUtility.ToJson(original.CaptureState()),hash=SnapshotCodec.Hash(json);
            var diagnostic=new Diagnostic(json,original.Rules,hash,new[]{cannon,worker});Exception failure=null;
            try{diagnostic.Execute();}catch(Exception ex){failure=ex;}diagnostic.Finish(failure);
            diagnostic.R.snapshotUnchanged=JsonUtility.ToJson(original.CaptureState())==json;
            string output=Environment.GetEnvironmentVariable("FRONTIER082_OUTPUT");
            if(string.IsNullOrWhiteSpace(output))output=Path.Combine(Path.GetTempPath(),"FrontierPaidSafeLand082-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(output);
            string report=Path.Combine(output,"paid-safe-land082.json");File.WriteAllText(report,JsonUtility.ToJson(diagnostic.R,true));
            TestContext.WriteLine("Confluence082 strict original two-actor land arrival="+diagnostic.R.completed+" elapsed="+diagnostic.R.elapsed.ToString("R")+" failure="+diagnostic.R.failure+" report="+report+"; no ferry/speed saving claim");
            Assert.IsTrue(diagnostic.R.snapshotUnchanged,"Land branch changed the normal paid World");
            Assert.IsTrue(diagnostic.R.completed,"Both original paid actors must actually arrive without loss and traverse both decks; inspect retained evidence");
        }
    }
}
