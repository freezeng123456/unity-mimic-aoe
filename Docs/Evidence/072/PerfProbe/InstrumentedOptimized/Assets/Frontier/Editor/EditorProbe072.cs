using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Frontier.Editor
{
    // Read-only world probes on an isolated project copy. No Tick or Issue.
    public static class EditorProbe072
    {
        [Serializable] public sealed class Row
        {
            public string phase,operation,startedUtc,finishedUtc,inputSha256,worldBeforeSha256,worldAfterSha256,error;
            public int player,workers,own,visibleThreats,selectedWorker;public float worldTime,resultAmount;
            public bool resultAvailable,worldUnchanged; public Vector2 home,point;
            public double wallMs;public long safeCalls,segmentSafeCalls,economicThreatCalls,pathNodePops,pathClearLineCalls;
            public PerfProbeDiagnostics072.Metric[] metrics;
        }
        [Serializable] public sealed class Receipt
        {
            public string schema="frontier-isolated-perf-probe072-1",scope="Instrumented isolated project; restore/query only, not a gameplay/performance acceptance",status="running";
            public string inputFile,inputSha256,startedUtc,finishedUtc,rulesHash,error;
            public bool noWorldTicks=true,noCommands=true,worldStateUnchanged=true;
            public string budgetScope="Continuity default obligations delegate; resource geometry/query exact, full AI political budget is not reconstructed";
            public string timingScope="Inclusive nested Stopwatch wall timings plus hot-predicate counters; instrumentation overhead present; do not add nested times or compare directly to release frame FPS";
            public float worldTime;public int entities; public Row[] rows;
        }
        public static void Run()
        {
            string input=Environment.GetEnvironmentVariable("FRONTIER_PERF_SNAPSHOT");
            string output=Environment.GetEnvironmentVariable("FRONTIER_PERF_OUTPUT");
            if(string.IsNullOrEmpty(input)||string.IsNullOrEmpty(output))throw new ArgumentException("Set FRONTIER_PERF_SNAPSHOT and a new FRONTIER_PERF_OUTPUT directory");
            if(Directory.Exists(output))throw new IOException("Probe output must be a new directory; preserve prior diagnostics");
            Directory.CreateDirectory(output);
            var receipt=new Receipt{inputFile=Path.GetFullPath(input),startedUtc=DateTime.UtcNow.ToString("o")};
            var rows=new List<Row>();
            using(var stream=new StreamWriter(Path.Combine(output,"operations.jsonl"),false,new UTF8Encoding(false)))
            {
                try
                {
                    string json=File.ReadAllText(input);
                    using(var hash=SHA256.Create())receipt.inputSha256=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(input))).Replace("-","").ToLowerInvariant();
                    var rules=Catalog.Load();receipt.rulesHash=SnapshotCodec.RulesHash(rules);
                    var saved=JsonUtility.FromJson<WorldSnapshot>(json);var world=World.Restore(rules,saved);
                    receipt.worldTime=world.Time;receipt.entities=world.Entities.Count;
                    for(int player=0;player<world.Players.Length;player++)
                    {
                        int slot=player;var own=new List<EntityState>();var workers=new List<EntityState>();var visible=new List<EntityState>();
                        foreach(var e in world.Entities)if(e.Alive)
                        {
                            if(e.owner==slot){own.Add(e);if(e.Worker&&!e.def.waterborne)workers.Add(e);}
                            else if(AiResourceContinuity072.EconomicThreat(world,slot,e))visible.Add(e);
                        }
                        var helper=new AiResourceContinuity072(world,slot);
                        var army=new AiArmyPlan072(world,slot,AiDifficultyProfile.From(rules,"standard"));
                        Vector2 home=default;
                        Execute("Home",()=>{home=army.Home;},slot,world,own,workers,visible,stream,rows,receipt,r=>r.home=home);
                        helper.BeginDecision();
                        try
                        {
                            Execute("ContinuityEvaluateScopedCold",()=>{helper.Evaluate();},slot,world,own,workers,visible,stream,rows,receipt);
                            Execute("ContinuityEvaluateScopedWarm",()=>{helper.Evaluate();},slot,world,own,workers,visible,stream,rows,receipt);
                        }
                        finally{helper.EndDecision();}
                        bool threatened=visible.Count>0;
                        foreach(var kind in new[]{ResourceKind.Wood,ResourceKind.Coin})
                        {
                            float amount=0;bool available=false;
                            Execute("SourceAmountDirect-"+kind,()=>
                            {
                                var method=typeof(AiResourceContinuity072).GetMethod("SourceAmount",BindingFlags.Instance|BindingFlags.NonPublic);
                                if(method==null)throw new MissingMethodException("SourceAmount source changed");
                                object[] arguments={kind,workers,visible,threatened,false,kind==ResourceKind.Coin};
                                amount=(float)method.Invoke(helper,arguments);available=(bool)arguments[4];
                            },slot,world,own,workers,visible,stream,rows,receipt,r=>{r.resultAvailable=available;r.resultAmount=float.IsInfinity(amount)?-1:amount;});
                        }
                        // An existing unpaid-complete foundation is checked exactly.
                        var foundation=own.Find(e=>e.def.kind==EntityKind.Building&&!e.Complete);
                        if(foundation!=null)
                        {
                            EntityState selected=null;bool allowed=false;
                            Execute("ConstructionExisting-"+foundation.def.id,()=>{allowed=AiResourceContinuity072.TryConstructionWorker(world,slot,foundation.def,foundation.position,workers,visible,out selected,foundation.id);},slot,world,own,workers,visible,stream,rows,receipt,r=>{r.point=foundation.position;r.resultAvailable=allowed;r.selectedWorker=selected?.id??0;});
                        }
                        // Only a CanBuild-legal, affordable, visible new site. Do not give resources.
                        EntityDef definition=null;Vector2 candidate=default;
                        foreach(string id in new[]{"house","farm","estate"})
                        {
                            var value=rules.Entity(id);if(value==null)continue;
                            for(int ring=0;ring<4&&definition==null;ring++)for(int n=0;n<20;n++)
                            {
                                float angle=n*Mathf.PI*2/20;var point=home+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(9+ring*5);point=new Vector2(Mathf.Round(point.x),Mathf.Round(point.y));
                                if(world.CanBuild(slot,id,point)!="")continue;
                                definition=value;candidate=point;break;
                            }
                            if(definition!=null)break;
                        }
                        if(definition!=null)
                        {
                            EntityState selected=null;bool allowed=false;
                            Execute("ConstructionNew-"+definition.id,()=>{allowed=AiResourceContinuity072.TryConstructionWorker(world,slot,definition,candidate,workers,visible,out selected);},slot,world,own,workers,visible,stream,rows,receipt,r=>{r.point=candidate;r.resultAvailable=allowed;r.selectedWorker=selected?.id??0;});
                        }
                        else stream.WriteLine(JsonUtility.ToJson(new Row{phase="skipped",operation="ConstructionNew-NoLegalAffordableVisibleSite",player=slot,worldTime=world.Time}));
                        stream.Flush();
                    }
                    receipt.status=receipt.worldStateUnchanged?"queries_completed_world_unchanged":"query_mutation_detected";
                    if(!receipt.worldStateUnchanged)throw new InvalidOperationException("Read-only probe changed serialized world state");
                }
                catch(Exception ex)
                {
                    receipt.status="failed";receipt.error=ex.ToString();
                    throw;
                }
                finally
                {
                    PerfProbeDiagnostics072.Enabled=false;receipt.finishedUtc=DateTime.UtcNow.ToString("o");receipt.rows=rows.ToArray();
                    File.WriteAllText(Path.Combine(output,"receipt.json"),JsonUtility.ToJson(receipt,true),new UTF8Encoding(false));
                }
            }
        }
        static void Execute(string name,Action action,int player,World world,List<EntityState> own,List<EntityState> workers,List<EntityState> visible,StreamWriter stream,List<Row> rows,Receipt receipt,Action<Row> collect=null)
        {
            var row=new Row{operation=name,player=player,worldTime=world.Time,workers=workers.Count,own=own.Count,visibleThreats=visible.Count,inputSha256=receipt.inputSha256,startedUtc=DateTime.UtcNow.ToString("o"),phase="begin"};
            row.worldBeforeSha256=SnapshotCodec.Hash(JsonUtility.ToJson(world.CaptureState()));
            stream.WriteLine(JsonUtility.ToJson(row));stream.Flush();
            PerfProbeDiagnostics072.Reset();PerfProbeDiagnostics072.Enabled=true;var watch=Stopwatch.StartNew();
            try{action();collect?.Invoke(row);}
            catch(Exception ex){row.error=ex.ToString();throw;}
            finally
            {
                watch.Stop();PerfProbeDiagnostics072.Enabled=false;
                row.wallMs=watch.Elapsed.TotalMilliseconds;row.finishedUtc=DateTime.UtcNow.ToString("o");row.phase=row.error==null?"end":"failed";
                row.safeCalls=PerfProbeDiagnostics072.SafeCalls;row.segmentSafeCalls=PerfProbeDiagnostics072.SegmentSafeCalls;row.economicThreatCalls=PerfProbeDiagnostics072.EconomicThreatCalls;
                row.pathNodePops=PerfProbeDiagnostics072.PathNodePops;row.pathClearLineCalls=PerfProbeDiagnostics072.PathClearLineCalls;row.metrics=PerfProbeDiagnostics072.Read();
                row.worldAfterSha256=SnapshotCodec.Hash(JsonUtility.ToJson(world.CaptureState()));row.worldUnchanged=row.worldBeforeSha256==row.worldAfterSha256;
                receipt.worldStateUnchanged&=row.worldUnchanged;rows.Add(row);stream.WriteLine(JsonUtility.ToJson(row));stream.Flush();
            }
        }
    }
}
