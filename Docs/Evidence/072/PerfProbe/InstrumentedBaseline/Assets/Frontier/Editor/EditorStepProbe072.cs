using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Frontier.Editor
{
    // Isolated diagnostic continuation, never a new match or live bootstrap.
    public static class EditorStepProbe072
    {
        const int DeclaredSteps=250;
        [Serializable] public sealed class EventArray {public SimEvent[] events;}
        [Serializable] public sealed class StepRow
        {
            public string phase,utc,error;public int ordinal,stepBefore,stepAfter,eventsBefore,eventsAfter,entities;
            public float timeBefore,timeAfter,lastNavigationMs,lastCombatMs,lastEconomyMs;public bool stepped,matchEnded;
            public double wallMs;public long safeCalls,segmentSafeCalls,economicThreatCalls,pathNodePops,pathClearLineCalls;
            public PerfProbeDiagnostics072.Metric[] metrics;
        }
        [Serializable] public sealed class Receipt
        {
            public string schema="frontier-isolated-native-step-probe072-1",scope="Original NativeA anchor/full actual event prefix + exact restored controllers; fixed250-step continuation, instrumented Editor no rendering or future external replay inputs";
            public string status="running",startedUtc,finishedUtc,anchorFile,anchorSha256,prefixFile,prefixSha256,rulesHash,error;
            public string startingStateDigest,expectedStartingStateDigest,finalStateDigest;
            public int declaredSteps=DeclaredSteps,completedSteps,initialStep,finalStep,initialEvents,finalEvents;
            public float initialWorldTime,finalWorldTime;public bool startingStateDigestMatches,noLiveStateInjection=true,noForcedWinner=true,noNewOpening=true;
            public string timingScope="Per fixed-step inclusive nested method wall/counters; instrumentation and JSONLflush overhead, concurrentCPU possible; not releaseFPS or playback verification";
        }
        static string FileHash(string path)
        {using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        public static void Run()
        {
            string anchorPath=Environment.GetEnvironmentVariable("FRONTIER_PERF_ANCHOR");
            string prefixPath=Environment.GetEnvironmentVariable("FRONTIER_PERF_EVENTS_PREFIX");
            string output=Environment.GetEnvironmentVariable("FRONTIER_PERF_OUTPUT");
            if(string.IsNullOrEmpty(anchorPath)||string.IsNullOrEmpty(output))throw new ArgumentException("Set FRONTIER_PERF_ANCHOR and new FRONTIER_PERF_OUTPUT");
            if(string.IsNullOrEmpty(prefixPath))prefixPath=Path.Combine(Path.GetDirectoryName(anchorPath),"recorded-events-prefix.json");
            if(Directory.Exists(output))throw new IOException("Use new output directory, preserve previous query probes");
            Directory.CreateDirectory(output);var receipt=new Receipt{startedUtc=DateTime.UtcNow.ToString("o"),anchorFile=Path.GetFullPath(anchorPath),prefixFile=Path.GetFullPath(prefixPath)};
            World world=null;MatchSimulation simulation=null;
            using(var steps=new StreamWriter(Path.Combine(output,"steps.jsonl"),false,new UTF8Encoding(false)))
            {
                try
                {
                    receipt.anchorSha256=FileHash(anchorPath);receipt.prefixSha256=FileHash(prefixPath);
                    var anchor=JsonUtility.FromJson<ReplayAnchor>(File.ReadAllText(anchorPath));
                    string prefixJson=File.ReadAllText(prefixPath);
                    var prefix=JsonUtility.FromJson<EventArray>("{\"events\":"+prefixJson+"}").events;
                    if(anchor==null||anchor.world==null||anchor.simulation==null||anchor.hasGuided||prefix==null||prefix.Length!=anchor.eventCount||anchor.world.events==null||anchor.world.events.Length!=0)
                        throw new InvalidDataException("Require unguided eventless originalanchor + exact full recordedprefix");
                    var rules=Catalog.Load();receipt.rulesHash=SnapshotCodec.RulesHash(rules);
                    // Public Restore deep-clones this hydrated initialization DTO.
                    // The prefix is the recorded history, never manufactured events.
                    var hydrated=SnapshotCodec.Clone(anchor.world);hydrated.events=prefix;
                    world=World.Restore(rules,hydrated);simulation=MatchSimulation.Restore(world,anchor.simulation);
                    receipt.initialStep=simulation.StepCount;receipt.initialEvents=world.Events.Count;receipt.initialWorldTime=world.Time;
                    if(simulation.StepCount!=anchor.completedStep)throw new InvalidDataException("Controller/anchor step mismatch");
                    receipt.expectedStartingStateDigest=anchor.stateDigest;receipt.startingStateDigest=ReplayDigest.Compute(world,simulation);
                    receipt.startingStateDigestMatches=receipt.startingStateDigest==anchor.stateDigest;
                    if(!receipt.startingStateDigestMatches)throw new InvalidDataException("Hydrated bootstrap does not match original replay state digest");
                    steps.WriteLine(JsonUtility.ToJson(receipt));steps.Flush();
                    for(int ordinal=1;ordinal<=DeclaredSteps;ordinal++)
                    {
                        var row=new StepRow{phase="begin",ordinal=ordinal,utc=DateTime.UtcNow.ToString("o"),stepBefore=simulation.StepCount,timeBefore=world.Time,eventsBefore=world.Events.Count,entities=world.Entities.Count};
                        steps.WriteLine(JsonUtility.ToJson(row));steps.Flush();
                        PerfProbeDiagnostics072.Reset();PerfProbeDiagnostics072.Enabled=true;var timer=Stopwatch.StartNew();
                        try{row.stepped=simulation.Step();}
                        catch(Exception ex){row.error=ex.ToString();throw;}
                        finally
                        {
                            timer.Stop();PerfProbeDiagnostics072.Enabled=false;row.wallMs=timer.Elapsed.TotalMilliseconds;row.utc=DateTime.UtcNow.ToString("o");row.phase=row.error==null?"end":"failed";
                            row.stepAfter=simulation.StepCount;row.timeAfter=world.Time;row.eventsAfter=world.Events.Count;row.matchEnded=world.IsMatchEnded;
                            row.lastNavigationMs=world.LastNavigationMs;row.lastCombatMs=world.LastCombatMs;row.lastEconomyMs=world.LastEconomyMs;
                            row.safeCalls=PerfProbeDiagnostics072.SafeCalls;row.segmentSafeCalls=PerfProbeDiagnostics072.SegmentSafeCalls;row.economicThreatCalls=PerfProbeDiagnostics072.EconomicThreatCalls;
                            row.pathNodePops=PerfProbeDiagnostics072.PathNodePops;row.pathClearLineCalls=PerfProbeDiagnostics072.PathClearLineCalls;row.metrics=PerfProbeDiagnostics072.Read();
                            if(row.stepped)receipt.completedSteps++;
                            steps.WriteLine(JsonUtility.ToJson(row));steps.Flush();
                        }
                        if(!row.stepped)
                        {
                            if(world.IsMatchEnded){receipt.status="natural_terminal_before_declared_horizon";break;}
                            throw new InvalidOperationException("Simulation stopped before horizon without natural terminal");
                        }
                    }
                    if(receipt.completedSteps==DeclaredSteps)receipt.status="declared_continuation_completed";
                    receipt.finalStateDigest=ReplayDigest.Compute(world,simulation);
                }
                catch(Exception ex){receipt.status="failed";receipt.error=ex.ToString();throw;}
                finally
                {
                    PerfProbeDiagnostics072.Enabled=false;receipt.finishedUtc=DateTime.UtcNow.ToString("o");
                    if(world!=null)
                    {
                        receipt.finalWorldTime=world.Time;receipt.finalEvents=world.Events.Count;receipt.finalStep=simulation?.StepCount??0;
                        File.WriteAllText(Path.Combine(output,"final-world.json"),JsonUtility.ToJson(world.CaptureState(false)),new UTF8Encoding(false));
                        if(simulation!=null)File.WriteAllText(Path.Combine(output,"final-simulation.json"),JsonUtility.ToJson(simulation.CaptureState(),true),new UTF8Encoding(false));
                        using(var events=new StreamWriter(Path.Combine(output,"new-events.jsonl"),false,new UTF8Encoding(false)))
                            for(int i=receipt.initialEvents;i<world.Events.Count;i++)events.WriteLine(JsonUtility.ToJson(world.Events[i]));
                    }
                    File.WriteAllText(Path.Combine(output,"receipt.json"),JsonUtility.ToJson(receipt,true),new UTF8Encoding(false));
                }
            }
        }
    }
}
