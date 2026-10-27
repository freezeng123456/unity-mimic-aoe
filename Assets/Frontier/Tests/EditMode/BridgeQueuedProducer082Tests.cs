using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // This is a real queued-planner fixture, not a World.Tick replay. Existing
    // requests are served through production reservations without moving bodies,
    // advancing time, clearing the queue or injecting requests/paths.
    public sealed class BridgeQueuedProducer082Tests
    {
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static string FixtureRoot=>Path.Combine(Application.dataPath,"Frontier","Tests","EditMode","Fixtures");
        [Serializable] sealed class Rule {public int bridge;public float direction;}
        [Serializable] sealed class Row
        {
            public int round,actor,fineQueries,waypoints,pathIndex,firstInvalidFullLeg=-1,firstInvalidSuffixLeg=-1;
            public string kind,error;public bool accepted,deferred,navigationClear=true,worldQueriesUnchanged=true,positionsUnchanged,timeUnchanged;
            public Vector2 origin,goal,invalidFullStart,invalidFullEnd,invalidSuffixStart,invalidSuffixEnd;public Vector2[] path;public UnitOrder[] orders;
            public Rule[] plannedRules,rebuiltRules;public int[] queueBefore,queueAfter;
        }
        [Serializable] sealed class Receipt
        {
            public string sourceFingerprint,fixtureWorldSha256,fixtureRulesSha256;public float fixedTime;
            public int initialRequests,maxRounds,rounds,fineQueries,acceptedRoutes,acceptedFineRoutes;public bool positionsUnchanged=true,timeUnchanged=true,worldQueriesUnchanged=true;
            public Row[] rows;
            public string scope="Real queued planning fixture with only rules identity normalized for empty DTO additions: production BeginFineRecoveryTick/TryAllyRecovery(existing head,real pathGoal)/EndFineRecoveryTick reservation rounds; no World.Tick, movement, time advance, path/queue/request injection,400-arrival or throughput claim";
        }
        static string Hash(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        static int Attempts(World w)=>(int)typeof(World).GetField("fineRecoveryAttempts",Flags).GetValue(w);
        static Rule[] Rules(World w)
        {
            var rows=new List<Rule>();foreach(var value in (IList)typeof(World).GetField("bridgeTrafficRules",Flags).GetValue(w))
            {
                var type=value.GetType();var span=(BridgeSpan)type.GetField("span").GetValue(value);
                rows.Add(new Rule{bridge=Array.IndexOf(w.Layout.Bridges,span),direction=(float)type.GetField("direction").GetValue(value)});
            }
            return rows.ToArray();
        }
        [Test] public void Actual45QueuedFineProducerMustReturnASelfConsistentRouteBeforeAnyMovement()
        {
            string root=Environment.GetEnvironmentVariable("FRONTIER082_FIXTURES45");if(string.IsNullOrEmpty(root))root=FixtureRoot;
            string world=Path.Combine(root,"tributaries-crowd45-world.json"),rules=Path.Combine(root,"tributaries-crowd45-rules.json");
            string worldHash=Hash(world),rulesHash=Hash(rules);
            Assert.AreEqual("858345a364967e98369b1f4cebd063c0f2d5870b837e1ebe60e7105b3c6fc850",worldHash);
            Assert.AreEqual("b08413261c770b536bcc25f39187f237b5d83e753c93a55b4cfd6cc8b3ad0ba4",rulesHash);
            var w=MapBaselineNeutral088.RestoreHistoricalFixture(File.ReadAllText(rules),File.ReadAllText(world));
            var initial=w.CaptureState();var positions=w.Entities.ToDictionary(e=>e.id,e=>e.position);var rows=new List<Row>();var failures=new List<string>();
            var receipt=new Receipt{sourceFingerprint=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT"),fixtureWorldSha256=worldHash,fixtureRulesSha256=rulesHash,
                fixedTime=w.Time,initialRequests=initial.fineRecoveryQueue.Length,maxRounds=initial.fineRecoveryQueue.Length*2};
            Assert.Greater(receipt.initialRequests,0,"The original real failure fixture must contain pending requests");
            var begin=typeof(World).GetMethod("BeginFineRecoveryTick",Flags);var recover=typeof(World).GetMethod("TryAllyRecovery",Flags);var end=typeof(World).GetMethod("EndFineRecoveryTick",Flags);
            var prepare=typeof(World).GetMethod("PrepareBridgeTraffic",Flags);var line=typeof(World).GetMethod("MovementLineFree",Flags);
            int initialFine=Attempts(w);
            for(int round=0;round<receipt.maxRounds&&failures.Count==0;round++)
            {
                if(w.CaptureState().fineRecoveryQueue.Length==0)break;
                begin.Invoke(w,null);Row row=null;
                try
                {
                    // Begin may prune stale real requests. Use its actual first
                    // valid FIFO head rather than fabricating a reservation.
                    var queue=w.CaptureState().fineRecoveryQueue;if(queue.Length==0)continue;
                    var actor=w.Get(queue[0]);Assert.NotNull(actor);Vector2 goal=actor.pathGoal;
                    row=new Row{round=round,actor=actor.id,kind=actor.def.id,origin=actor.position,goal=goal,queueBefore=queue};rows.Add(row);
                    int beforeFine=Attempts(w);row.accepted=(bool)recover.Invoke(w,new object[]{actor,goal});row.fineQueries=Attempts(w)-beforeFine;
                    row.deferred=(bool)typeof(World).GetField("fineRecoveryDeferred",Flags).GetValue(w);row.plannedRules=Rules(w);
                    if(row.fineQueries>1)failures.Add("Round "+round+" secretly performed multiple fine queries");
                    if(row.accepted)
                    {
                        receipt.acceptedRoutes++;if(row.fineQueries>0)receipt.acceptedFineRoutes++;
                        row.path=actor.path.ToArray();row.orders=actor.orders.Select(order=>SnapshotCodec.Clone(order)).ToArray();row.pathIndex=actor.pathIndex;row.waypoints=actor.path.Count;
                        if(actor.path.Count<2)failures.Add("Accepted actor "+actor.id+" has no complete route");
                        else
                        {
                            if((actor.path[0]-row.origin).sqrMagnitude>.0001f||(actor.path[actor.path.Count-1]-goal).sqrMagnitude>.0001f)failures.Add("Accepted actor "+actor.id+" changed an exact route endpoint");
                            string beforeQuery=JsonUtility.ToJson(w.CaptureState());prepare.Invoke(w,new object[]{actor,goal,actor.path});row.rebuiltRules=Rules(w);
                            var nav=actor.def.waterborne?w.WaterNavigation:w.Navigation;
                            for(int leg=1;leg<actor.path.Count;leg++)
                            {
                                Vector2 a=actor.path[leg-1],b=actor.path[leg];if(!nav.ClearLine(a,b,actor.def.radius))row.navigationClear=false;
                                if(!(bool)line.Invoke(w,new object[]{actor,a,b})&&row.firstInvalidFullLeg<0){row.firstInvalidFullLeg=leg;row.invalidFullStart=a;row.invalidFullEnd=b;}
                            }
                            Vector2 start=actor.position;
                            for(int leg=actor.pathIndex;leg<actor.path.Count;leg++)
                            {
                                Vector2 target=actor.path[leg];if(!nav.ClearLine(start,target,actor.def.radius))row.navigationClear=false;
                                if(!(bool)line.Invoke(w,new object[]{actor,start,target})&&row.firstInvalidSuffixLeg<0){row.firstInvalidSuffixLeg=leg;row.invalidSuffixStart=start;row.invalidSuffixEnd=target;}
                                start=target;
                            }
                            row.worldQueriesUnchanged=beforeQuery==JsonUtility.ToJson(w.CaptureState());receipt.worldQueriesUnchanged&=row.worldQueriesUnchanged;
                            if(row.firstInvalidFullLeg>=0||row.firstInvalidSuffixLeg>=0)failures.Add("Unmoved real FIFO actor "+actor.id+" received a self-occluded fine route");
                            if(!row.worldQueriesUnchanged)failures.Add("Post-planner geometry queries mutated serialized World");
                        }
                    }
                }
                catch(Exception e){if(row!=null)row.error=e.ToString();failures.Add(e.ToString());}
                finally{end.Invoke(w,null);}
                receipt.rounds=round+1;
                if(row!=null)
                {
                    row.queueAfter=w.CaptureState().fineRecoveryQueue;row.timeUnchanged=w.Time==receipt.fixedTime;row.positionsUnchanged=w.Entities.All(e=>positions[e.id]==e.position);
                    receipt.timeUnchanged&=row.timeUnchanged;receipt.positionsUnchanged&=row.positionsUnchanged;
                    if(!row.timeUnchanged||!row.positionsUnchanged)failures.Add("Queued planning changed actual time or body positions");
                }
            }
            receipt.fineQueries=Attempts(w)-initialFine;receipt.rows=rows.ToArray();
            string output=Environment.GetEnvironmentVariable("FRONTIER082_EVIDENCE");if(string.IsNullOrEmpty(output))output=Path.Combine(Directory.GetCurrentDirectory(),"Builds","BridgeTraffic082");Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output,"bridge-actual45-queued-producer.json"),JsonUtility.ToJson(receipt,true));
            Assert.Greater(receipt.fineQueries,0,"A fixture with no actual fine query cannot validate its producer");
            Assert.Greater(receipt.acceptedFineRoutes,0,"At least one actual fine query must produce an accepted route");
            Assert.IsTrue(receipt.positionsUnchanged&&receipt.timeUnchanged&&receipt.worldQueriesUnchanged);
            Assert.IsEmpty(failures,string.Join("\n",failures));
        }
    }
}
