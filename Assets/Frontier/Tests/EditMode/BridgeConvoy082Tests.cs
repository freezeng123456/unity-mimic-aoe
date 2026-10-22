using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // New preferred-spacing contract, replacing the experimental greedy-gap
    // fixture only. Both actor moves, route warm-up and Stop/Hold are public.
    // No path, timer, order context, position correction or speed is injected.
    public sealed class BridgeConvoy082Tests
    {
        const float Preferred=.1f;
        [Serializable] sealed class Receipt
        {
            public string sourceFingerprint,status,error,beforeWorldSha256;
            public string scope="Two public-Move bridge actors, preferred+.1 spacing, zero wait and exact arrivals with cold/sweep/speed/slope; not original400 throughput or paid/native/FPS acceptance";
            public int direction,steps;public bool leaderCreatedFirst,coldEqual=true,arrived,crossedDeck,zeroFirstStep,firstFullPhysicalFree,sawPhysicalFreePreferredClip;
            public float initialGap,warmupTime,elapsed,firstBodyGapAtFollowerStep,firstFollowerAdvance,firstLateral,firstFullStep,firstGapAfterFollower,peakPhysicalStep;
            public Vector2 leaderGoal,followerGoal,leaderFinal,followerFinal;
        }
        static string State(World w)=>JsonUtility.ToJson(w.CaptureState());
        static float SegmentDistance(Vector2 a,Vector2 b,Vector2 p)
        {Vector2 d=b-a;float at=d.sqrMagnitude>1e-12f?Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude):0;return Vector2.Distance(a+d*at,p);}
        static void Order(World w,EntityState actor,OrderKind kind,Vector2 point=default)
        {
            var result=w.Issue(new GameCommand{player=0,kind=CommandKind.Order,order=kind,actors=new[]{actor.id},point=point});
            Assert.IsTrue(result.accepted,result.reason);if(kind==OrderKind.Move)Assert.AreEqual(point,actor.orders.Peek().point);
        }
        static void Step(World w,World cold,EntityState leader,EntityState follower,BridgeSpan span,Vector2 forward,float dt,Receipt row,int losses,bool checkPreferred)
        {
            var actors=new[]{leader,follower};var before=actors.ToDictionary(e=>e.id,e=>e.position);
            var order=w.Entities.Select((e,i)=>new{e.id,index=i}).ToDictionary(e=>e.id,e=>e.index);
            w.Tick(dt);cold.Tick(dt);if(!checkPreferred)row.warmupTime+=dt;else{row.steps++;row.elapsed+=dt;}
            row.coldEqual&=State(w)==State(cold);Assert.IsTrue(row.coldEqual,"Actual cold full-state continuation differs");
            Assert.AreEqual(losses,w.Players[0].losses);Assert.AreEqual(-1,w.Winner);
            foreach(var actor in actors)
            {
                Assert.IsTrue(actor.Alive&&actor.garrisonedIn==0);Assert.AreEqual(w.MaxHp(actor),actor.hp);
                Assert.IsTrue(w.Navigation.IsWalkable(actor.position,actor.def.radius));Assert.IsTrue(w.Navigation.ClearLine(before[actor.id],actor.position,actor.def.radius));
                float length=Vector2.Distance(before[actor.id],actor.position);row.peakPhysicalStep=Mathf.Max(row.peakPhysicalStep,length);
                Assert.LessOrEqual(length,w.Speed(actor)*dt+.0001f);
                Assert.LessOrEqual(Mathf.Abs(w.SurfaceHeight(actor.position)-w.SurfaceHeight(before[actor.id])),length*.4f+.0002f);
                if(w.IsWater(actor.position)){Assert.IsTrue(w.IsBridge(actor.position));row.crossedDeck=true;}
                foreach(var other in actors.Where(e=>e.id!=actor.id))
                {
                    Vector2 body=order[other.id]<order[actor.id]?other.position:before[other.id];
                    Assert.GreaterOrEqual(SegmentDistance(before[actor.id],actor.position,body),actor.def.radius+other.def.radius-.00001f,"Actual sequential movement swept through another body");
                }
            }
            if(checkPreferred&&leader.orders.Count>0&&span.Approach(before[follower.id])&&span.Approach(follower.position))
            {
                Vector2 body=order[leader.id]<order[follower.id]?leader.position:before[leader.id];
                if(span.Approach(body)&&Vector2.Dot(body-before[follower.id],forward)>0)
                {
                    float physical=leader.def.radius+follower.def.radius,beforeGap=Vector2.Distance(before[follower.id],body)-physical;
                    float afterGap=SegmentDistance(before[follower.id],follower.position,body)-physical;
                    float fullStep=w.Speed(follower)*dt;
                    if(beforeGap>=fullStep&&beforeGap-fullStep<Preferred-.00002f)
                    {
                        row.sawPhysicalFreePreferredClip=true;
                        Assert.Less(Vector2.Dot(follower.position-before[follower.id],forward),fullStep-.00001f,"A physically free full step must still preserve preferred following space");
                    }
                    if(beforeGap>=Preferred-.00002f)Assert.GreaterOrEqual(afterGap,Preferred-.00002f,"A following step must preserve the declared preferred body spacing");
                    else
                    {
                        Assert.GreaterOrEqual(afterGap,beforeGap-.00002f,"Existing preferred-spacing deficit must not be deepened");
                        Assert.LessOrEqual(Vector2.Distance(before[follower.id],follower.position),.00002f,"No usable preferred gap means a real zero wait, not lateral full-speed drift");
                    }
                }
            }
        }
        [TestCase(-1)] [TestCase(1)]
        public void ACloseSameFlowBridgeConvoyUsesItsRealForwardGapThenCompletesBothPublicMoves(int direction)
        {
            // Preserve both existing experimental case identities. The contract
            // now preserves preferred spacing instead of greedily eating80% of
            // the physical gap; old50/52/56 sources and failures remain archived.
            var failures=new List<string>();
            foreach(bool leaderCreatedFirst in new[]{false,true})
                try{Run(direction,leaderCreatedFirst);}catch(Exception error){failures.Add("leaderCreatedFirst="+leaderCreatedFirst+": "+error);}
            Assert.IsEmpty(failures,string.Join("\n",failures));
        }
        static void Run(int direction,bool leaderCreatedFirst)
        {
            const float gap=.02f;
            var w=new World(Catalog.Load(),314159,"coast","coast",5,MapTopology.River);
            foreach(var e in w.Entities.Where(e=>e.def.id!="town").ToArray())w.Remove(e);
            var span=w.Layout.Bridges[0];Vector2 forward=span.Direction*direction,side=new Vector2(-forward.y,forward.x);
            Vector2 leaderStart=span.Point(-direction*4,-direction*2),followerStart=leaderStart-forward*(w.Rules.Entity("great_bombard").radius+w.Rules.Entity("hussar").radius+gap);
            EntityState leader,follower;
            if(leaderCreatedFirst){leader=w.Spawn("great_bombard",0,leaderStart);follower=w.Spawn("hussar",0,followerStart);}
            else{follower=w.Spawn("hussar",0,followerStart);leader=w.Spawn("great_bombard",0,leaderStart);}
            var row=new Receipt{sourceFingerprint=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT"),status="running",direction=direction,leaderCreatedFirst=leaderCreatedFirst,initialGap=gap,leaderGoal=span.Point(direction*20,-direction*2),followerGoal=span.Point(direction*22,-direction*2)};
            try
            {
                Assert.IsTrue(span.Contains(leader.position)&&span.Contains(follower.position));
                foreach(var actor in new[]{leader,follower})Assert.IsTrue(w.Navigation.IsWalkable(actor.position,actor.def.radius));
                Order(w,follower,OrderKind.Hold);Order(w,leader,OrderKind.Move,row.leaderGoal);
                var cold=World.Restore(w.Rules,SnapshotCodec.Clone(w.CaptureState()));int losses=w.Players[0].losses;
                // Actual tiny public-Move Tick establishes the leader route even
                // when it is processed after the held follower. No route injection.
                Step(w,cold,leader,follower,span,forward,.001f,row,losses,false);Assert.IsNotEmpty(leader.path);
                Order(w,follower,OrderKind.Move,row.followerGoal);Order(cold,cold.Get(follower.id),OrderKind.Move,row.followerGoal);
                Assert.AreEqual(State(w),State(cold));row.beforeWorldSha256=SnapshotCodec.Hash(State(w));
                Vector2 leaderBefore=leader.position,followerBefore=follower.position;
                Step(w,cold,leader,follower,span,forward,.1f,row,losses,true);
                Vector2 body=leaderCreatedFirst?leader.position:leaderBefore;
                row.firstBodyGapAtFollowerStep=Vector2.Distance(body,followerBefore)-leader.def.radius-follower.def.radius;
                row.firstFullStep=w.Speed(follower)*.1f;row.firstFullPhysicalFree=row.firstBodyGapAtFollowerStep>=row.firstFullStep;
                row.firstFollowerAdvance=Vector2.Dot(follower.position-followerBefore,forward);row.firstLateral=Mathf.Abs(Vector2.Dot(follower.position-followerBefore,side));
                row.firstGapAfterFollower=Vector2.Distance(body,follower.position)-leader.def.radius-follower.def.radius;
                Assert.LessOrEqual(row.firstLateral,.0002f,"Same-flow bridge following does not require sideways drift");
                if(leaderCreatedFirst)
                {
                    Assert.Greater(row.firstFollowerAdvance,.01f,"Use a useful positive portion of the preferred-spacing gap");
                    Assert.That(row.firstGapAfterFollower,Is.InRange(Preferred-.00002f,Preferred+.03f),"Keep the declared preferred headway instead of consuming the full physical gap");
                }
                else
                {
                    row.zeroFirstStep=true;Assert.Less(row.firstBodyGapAtFollowerStep,Preferred);
                    Assert.LessOrEqual(Vector2.Distance(follower.position,followerBefore),.00002f,"A later-processed leader must leave the currently space-starved follower waiting physically");
                }
                // These actual short steps also exercise the case where a full
                // physical-body step would be free but would eat preferred space.
                // There is no extra scene, path or timer injection.
                for(int shortTick=0;shortTick<3;shortTick++)
                {
                    Vector2 prior=follower.position;Step(w,cold,leader,follower,span,forward,.01f,row,losses,true);
                    if(!leaderCreatedFirst&&shortTick==0)Assert.Greater(Vector2.Dot(follower.position-prior,forward),.001f,"The late leader's real movement must open the next Tick's gap");
                }
                Assert.IsTrue(row.sawPhysicalFreePreferredClip,"At least one actual short step must preserve preferred spacing even though its full physical sweep would be free");
                for(int tick=4;tick<2400&&(leader.orders.Count>0||follower.orders.Count>0);tick++)
                {
                    Step(w,cold,leader,follower,span,forward,.1f,row,losses,true);
                    if(span.Contains(follower.position)&&tick%17==0)cold=World.Restore(w.Rules,SnapshotCodec.Clone(w.CaptureState()));
                    if(leader.orders.Count>0)Assert.AreEqual(row.leaderGoal,leader.orders.Peek().point);if(follower.orders.Count>0)Assert.AreEqual(row.followerGoal,follower.orders.Peek().point);
                }
                row.arrived=leader.orders.Count==0&&follower.orders.Count==0&&Vector2.Distance(leader.position,row.leaderGoal)<.051f&&Vector2.Distance(follower.position,row.followerGoal)<.051f;
                Assert.IsTrue(row.crossedDeck);Assert.IsTrue(row.arrived,"Both exact public goals must finish within240sim seconds after the declared setup");row.status="passed";
            }
            catch(Exception error){row.status="failed";row.error=error.ToString();throw;}
            finally
            {
                row.leaderFinal=leader.position;row.followerFinal=follower.position;
                string output=Environment.GetEnvironmentVariable("FRONTIER082_EVIDENCE");if(string.IsNullOrEmpty(output))output=Path.Combine(Directory.GetCurrentDirectory(),"Builds","BridgeTraffic082");Directory.CreateDirectory(output);
                string name="bridge-headway-direction"+direction+"-leaderFirst"+leaderCreatedFirst;File.WriteAllText(Path.Combine(output,name+".json"),JsonUtility.ToJson(row,true));
                if(row.status!="passed")File.WriteAllText(Path.Combine(output,name+"-world.json"),State(w));
            }
        }
    }
}
