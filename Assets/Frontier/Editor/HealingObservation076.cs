using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Frontier.Editor
{
    // Ordinary paid AI play. The observer never issues orders or changes economy.
    public static class HealingObservation076
    {
        [Serializable] sealed class Run
        {
            public string schema="healing076-v1",version,rulesHash,sourceFingerprint,startedUtc,finishedUtc,status;
            public float cap=2400,sampleInterval=30;public Case[] cases;
        }
        [Serializable] sealed class Case
        {
            public string id,civilization,opponent,map,status;public int seed,winner=-1,steps,events;
            public float seconds;public double wallSeconds,stepSeconds,observerSeconds,maxStepMs;
        }
        [Serializable] sealed class UnitHealth
        {
            public int id,patient;public string definition,status,order;
            public float hp,maxHp,blockedUntil;public bool autoHeal;
            public Vector2 position;
        }
        [Serializable] sealed class Sample
        {
            public float time,missingMilitaryHp,totalHealedHp;
            public int player,age,workers,soldiers,healers,queuedHealers,healingNow,healReceipts,retreats,returns;
            public Cost wallet,gathered,spent;public UnitHealth[] units;
        }
        public static void RunNormal()
        {
            string directory=Environment.GetEnvironmentVariable("FRONTIER_REPORT_PATH");
            if(string.IsNullOrWhiteSpace(directory)||Directory.Exists(directory)&&Directory.EnumerateFileSystemEntries(directory).Any())throw new IOException("Use a fresh observation directory");
            Directory.CreateDirectory(directory);var rules=Catalog.Load();
            var run=new Run{version=Application.version,rulesHash=SnapshotCodec.RulesHash(rules),sourceFingerprint=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT")??"",
                startedUtc=DateTime.UtcNow.ToString("o"),status="running",cases=new[]{
                    new Case{id="frontier-ottoman-mirror42",civilization="ottoman",opponent="ottoman",map=MapTopology.Frontier,seed=42},
                    new Case{id="winding-ottoman-british44",civilization="ottoman",opponent="coast",map=MapTopology.Winding,seed=44}}};
            void Save()=>File.WriteAllText(Path.Combine(directory,"manifest.json"),JsonUtility.ToJson(run,true));
            Save();
            foreach(var c in run.cases)
            {
                string output=Path.Combine(directory,c.id);Directory.CreateDirectory(output);c.status="running";Save();
                var world=new World(rules,c.seed,c.civilization,c.opponent,5,c.map);
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
                            var own=world.Entities.Where(e=>e.Alive&&e.owner==p.id).ToArray();var doctors=own.Where(e=>e.Healer).ToArray();
                            var army=own.Where(e=>e.Military&&!e.def.waterborne&&(e.def.tags&Tags.Explorer)==0).ToArray();
                            var healed=world.Events.Where(e=>e.player==p.id&&e.hasHealing).ToArray();
                            var sample=new Sample{time=world.Time,player=p.id,age=p.age,workers=own.Count(e=>e.Worker),soldiers=army.Length,healers=doctors.Length,
                                queuedHealers=own.Sum(e=>e.queue.Where(j=>j.kind=="Train"&&rules.Entity(j.item)?.healer==true).Sum(j=>j.count)),
                                healingNow=doctors.Count(e=>e.work.kind==WorkKind.Heal&&world.Time-e.work.time<=.2f),healReceipts=healed.Length,
                                totalHealedHp=healed.Sum(e=>e.healing.actual),missingMilitaryHp=army.Sum(e=>Mathf.Max(0,world.MaxHp(e)-e.hp)),
                                retreats=world.Events.Count(e=>e.player==p.id&&e.kind=="AIMedicalRetreat"),returns=world.Events.Count(e=>e.player==p.id&&e.kind=="AIMedicalReturn"),
                                wallet=p.resources,gathered=p.gathered,spent=p.spent,units=army.Concat(doctors).Select(e=>new UnitHealth{id=e.id,definition=e.def.id,status=e.status,
                                    order=e.orders.Count>0?e.orders.Peek().kind.ToString():"",hp=e.hp,maxHp=world.MaxHp(e),position=e.position,patient=e.healingTarget,
                                    blockedUntil=e.healingBlockedUntil,autoHeal=e.autoHeal}).ToArray()};
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
                UnityEngine.Debug.Log("HEALING076 "+c.id+" "+c.status+" time="+c.seconds+" events="+c.events);
            }
            run.status="closed_two_declared_ordinary_cases";run.finishedUtc=DateTime.UtcNow.ToString("o");Save();
        }
    }
}
