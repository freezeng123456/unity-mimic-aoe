using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Frontier.Editor
{
    // Ordinary paid AI play. The observer never issues orders or changes economy.
    public static class ScoutingObservation075
    {
        [Serializable] sealed class Run
        {
            public string schema="scouting075-v1",version,rulesHash,sourceFingerprint,startedUtc,finishedUtc,status;
            public float cap=2400,sampleInterval=30;public Case[] cases;
        }
        [Serializable] sealed class Case
        {
            public string id,civilization,map,status;public int seed,winner=-1,steps,events;
            public float seconds;public double wallSeconds,stepSeconds,observerSeconds,maxStepMs;
        }
        [Serializable] sealed class Sample
        {
            public float time;public int player,age,workers,soldiers,scouts,queuedScouts,scoutOrders,stealthScouts,concealedScouts,exploredCells;
            public Cost wallet,gathered,spent;public int[] scoutIds;public string[] scoutStatus;
        }
        public static void RunNormal()
        {
            string directory=Environment.GetEnvironmentVariable("FRONTIER_REPORT_PATH");
            if(string.IsNullOrWhiteSpace(directory)||Directory.Exists(directory)&&Directory.EnumerateFileSystemEntries(directory).Any())throw new IOException("Use a fresh observation directory");
            Directory.CreateDirectory(directory);var rules=Catalog.Load();
            var run=new Run{version=Application.version,rulesHash=SnapshotCodec.RulesHash(rules),sourceFingerprint=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT")??"",
                startedUtc=DateTime.UtcNow.ToString("o"),status="running",cases=new[]{
                    new Case{id="river-bend-dutch42",civilization="river",map=RiverContent070.Skirmish,seed=42},
                    new Case{id="winding-french44",civilization="france",map=MapTopology.Winding,seed=44}}};
            void Save()=>File.WriteAllText(Path.Combine(directory,"manifest.json"),JsonUtility.ToJson(run,true));
            Save();
            foreach(var c in run.cases)
            {
                string output=Path.Combine(directory,c.id);Directory.CreateDirectory(output);c.status="running";Save();
                World world;
                if(c.map==RiverContent070.Skirmish)
                {
                    var doc=RiverContent070.Build(rules,c.map);doc.seed=c.seed;
                    foreach(var start in doc.starts){start.civilization=c.civilization;start.age=1;start.initialResources=rules.Civ(c.civilization).startingResources;}
                    File.WriteAllText(Path.Combine(output,"opening-scenario.json"),ScenarioCodec.Encode(doc,rules));
                    world=new World(rules,doc,new[]{c.civilization,c.civilization},new[]{0,1},5);
                }
                else world=new World(rules,c.seed,c.civilization,c.civilization,5,c.map);
                var simulation=new MatchSimulation(world,new[]{0,1},"standard");
                void State(string name)
                {
                    // Include the event history: AI coin forecasts consult actual
                    // trade deliveries, so an eventless state is not a restart.
                    File.WriteAllText(Path.Combine(output,name+"-world.json"),JsonUtility.ToJson(world.CaptureState()));
                    File.WriteAllText(Path.Combine(output,name+"-simulation.json"),JsonUtility.ToJson(simulation.CaptureState()));
                }
                State("opening");int cursor=0;float next=0;var wall=Stopwatch.StartNew();var timer=new Stopwatch();
                using(var events=new StreamWriter(Path.Combine(output,"events.jsonl")))
                using(var samples=new StreamWriter(Path.Combine(output,"samples.jsonl")))
                {
                    void Observe()
                    {
                        timer.Restart();
                        foreach(var p in world.Players)
                        {
                            var own=world.Entities.Where(e=>e.Alive&&e.owner==p.id).ToArray();var scouts=own.Where(e=>e.Scout).ToArray();
                            int explored=0;for(int y=0;y<128;y++)for(int x=0;x<128;x++)if(world.Fog.At(p.id,new Vector2(x,y))!=VisibilityState.Unexplored)explored++;
                            var sample=new Sample{time=world.Time,player=p.id,age=p.age,workers=own.Count(e=>e.Worker),
                                soldiers=own.Count(e=>e.Military&&!e.def.waterborne&&(e.def.tags&Tags.Explorer)==0),scouts=scouts.Length,
                                queuedScouts=own.Sum(e=>e.queue.Where(j=>j.kind=="Train"&&rules.Entity(j.item)?.scout==true).Sum(j=>j.count)),
                                scoutOrders=scouts.Count(e=>e.orders.Count>0&&e.orders.Peek().kind==OrderKind.Scout),stealthScouts=scouts.Count(e=>e.stealth),
                                concealedScouts=scouts.Count(e=>e.stealth&&!world.IsVisible(1-p.id,e)),exploredCells=explored,
                                wallet=p.resources,gathered=p.gathered,spent=p.spent,scoutIds=scouts.Select(e=>e.id).ToArray(),scoutStatus=scouts.Select(e=>e.status).ToArray()};
                            samples.WriteLine(JsonUtility.ToJson(sample));
                        }
                        while(cursor<world.Events.Count)events.WriteLine(JsonUtility.ToJson(world.Events[cursor++]));
                        samples.Flush();events.Flush();c.observerSeconds+=timer.Elapsed.TotalSeconds;
                    }
                    Observe();next=run.sampleInterval;bool comparison=false;
                    while(world.Time<run.cap&&!world.IsMatchEnded)
                    {
                        timer.Restart();bool advanced=simulation.Step();double elapsed=timer.Elapsed.TotalSeconds;
                        c.stepSeconds+=elapsed;c.maxStepMs=Math.Max(c.maxStepMs,elapsed*1000);
                        if(!advanced)throw new InvalidOperationException("Ordinary simulation stopped without a terminal state");
                        if(world.Time>=1500&&!comparison){timer.Restart();State("comparison1500");c.observerSeconds+=timer.Elapsed.TotalSeconds;comparison=true;}
                        if(world.Time>=next){Observe();next+=run.sampleInterval;}
                    }
                    world.FlushEconomyLog();Observe();timer.Restart();State("final");c.observerSeconds+=timer.Elapsed.TotalSeconds;
                }
                c.wallSeconds=wall.Elapsed.TotalSeconds;c.steps=simulation.StepCount;c.seconds=world.Time;c.winner=world.Winner;c.events=world.Events.Count;
                c.status=world.IsMatchEnded?"natural_terminal":"observation_cap";Save();
                UnityEngine.Debug.Log("SCOUTING075 "+c.id+" "+c.status+" time="+c.seconds+" events="+c.events);
            }
            run.status="closed_two_declared_ordinary_cases";run.finishedUtc=DateTime.UtcNow.ToString("o");Save();
        }
    }
}
