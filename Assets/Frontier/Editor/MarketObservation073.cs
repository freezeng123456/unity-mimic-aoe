using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using UnityEngine;

namespace Frontier.Editor
{
    // Three predeclared ordinary openings. No gifts, forced orders or route guarantees.
    public static class MarketObservation073
    {
        const float Limit=2400,Comparison=1500,Interval=30;
        [Serializable] sealed class Run
        {
            public string schema="normal-market073-v1",startedUtc,finishedUtc,status,sourceFingerprint,rulesHash,version;
            public float limit=Limit,comparison=Comparison,sampleInterval=Interval;
            public Case[] cases;
        }
        [Serializable] sealed class Case
        {
            public string id,map,civilization,status,startedUtc,finishedUtc;
            public int seed,winner=-1,steps,marketTrades,stageDepartures;
            public float time;public bool comparisonReached;
            public double wallSeconds,stepSeconds,observerSeconds,finalWriteSeconds,maxStepMs;
            public int stepsOver100ms,stepsOver500ms;
        }
        [Serializable] sealed class Sample
        {
            public float time;public int player,age,workers,soldiers,towns,markets,posts;
            public Cost wallet,gathered,spent,marketIncome,marketSold,marketBought;
            public int foodPressure,woodPressure,gatherOrders,gatherRoutesRejectedAtSample;
            public AiArmyPlan072Snapshot army;
            public double intervalStepSeconds,intervalObserverSeconds,maxStepMs;
        }
        public static void RunNormal()
            =>RunDeclaredThreeCases("normal-market073-v1");
        public static void RunDeclaredThreeCases(string schema)
        {
            string directory=Environment.GetEnvironmentVariable("FRONTIER_REPORT_PATH");
            if(string.IsNullOrWhiteSpace(directory)||Directory.Exists(directory)&&Directory.EnumerateFileSystemEntries(directory).Any())throw new IOException("Use a fresh observation directory");
            Directory.CreateDirectory(directory);var rules=Catalog.Load();
            var run=new Run{schema=schema,startedUtc=DateTime.UtcNow.ToString("o"),status="running",version=Application.version,rulesHash=SnapshotCodec.RulesHash(rules),
                sourceFingerprint=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT")??"",cases=new[]{
                    new Case{id="winding-ottoman-44",map=MapTopology.Winding,civilization="ottoman",seed=44},
                    new Case{id="winding-ottoman-45",map=MapTopology.Winding,civilization="ottoman",seed=45},
                    new Case{id="river_bend-river-42",map=RiverContent070.Skirmish,civilization="river",seed=42}}};
            void Save()=>File.WriteAllText(Path.Combine(directory,"manifest.json"),JsonUtility.ToJson(run,true));
            Save();
            foreach(var c in run.cases)
            {
                c.startedUtc=DateTime.UtcNow.ToString("o");c.status="running";Save();string output=Path.Combine(directory,c.id);Directory.CreateDirectory(output);
                var wall=Stopwatch.StartNew();World world;
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
                {File.WriteAllText(Path.Combine(output,name+"-world.json"),JsonUtility.ToJson(world.CaptureState(false)));File.WriteAllText(Path.Combine(output,name+"-simulation.json"),JsonUtility.ToJson(simulation.CaptureState()));}
                State("opening");int cursor=0;float next=0;double lastStep=0,lastObserver=0,intervalMax=0;var timer=new Stopwatch();
                using(var events=new StreamWriter(Path.Combine(output,"events.jsonl")))
                using(var samples=new StreamWriter(Path.Combine(output,"samples.jsonl")))
                {
                    void Observe()
                    {
                        timer.Restart();var state=simulation.CaptureState();
                        foreach(var p in world.Players)
                        {
                            var own=world.Entities.Where(e=>e.Alive&&e.owner==p.id).ToArray();
                            var threats=world.Entities.Where(e=>AiResourceContinuity072.EconomicThreat(world,p.id,e)).ToList();
                            var routes=new AiGatherRoutes074(world,p.id,threats);
                            var gathering=own.Where(e=>e.Worker&&!e.def.waterborne&&e.garrisonedIn==0&&e.orders.Count>0&&e.orders.Peek().kind==OrderKind.Gather).ToArray();
                            var sample=new Sample{time=world.Time,player=p.id,age=p.age,workers=own.Count(e=>e.Worker),
                                soldiers=own.Count(e=>e.Military&&!e.def.waterborne&&(e.def.tags&Tags.Explorer)==0),towns=own.Count(e=>e.def.id=="town"),
                                markets=own.Count(e=>e.def.id=="market"&&e.Complete),posts=own.Count(e=>e.def.id=="trade_post"&&e.Complete),
                                wallet=p.resources,gathered=p.gathered,spent=p.spent,marketIncome=p.marketIncome,marketSold=p.marketSold,marketBought=p.marketBought,
                                foodPressure=p.marketFoodPressure,woodPressure=p.marketWoodPressure,army=state.controllers.First(a=>a.player==p.id).armyPlan,
                                gatherOrders=gathering.Length,gatherRoutesRejectedAtSample=gathering.Count(e=>!routes.CanGather(e,world.Get(e.orders.Peek().target))),
                                intervalStepSeconds=c.stepSeconds-lastStep,intervalObserverSeconds=c.observerSeconds-lastObserver,maxStepMs=intervalMax};
                            samples.WriteLine(JsonUtility.ToJson(sample));
                        }
                        while(cursor<world.Events.Count)events.WriteLine(JsonUtility.ToJson(world.Events[cursor++]));
                        samples.Flush();events.Flush();c.observerSeconds+=timer.Elapsed.TotalSeconds;lastStep=c.stepSeconds;lastObserver=c.observerSeconds;intervalMax=0;
                    }
                    while(world.Time<Limit&&!world.IsMatchEnded)
                    {
                        timer.Restart();if(!simulation.Step())throw new InvalidOperationException("Observer stopped at a nonterminal boundary");
                        double elapsed=timer.Elapsed.TotalSeconds;c.stepSeconds+=elapsed;c.steps++;double ms=elapsed*1000;
                        c.maxStepMs=Math.Max(c.maxStepMs,ms);intervalMax=Math.Max(intervalMax,ms);if(ms>100)c.stepsOver100ms++;if(ms>500)c.stepsOver500ms++;
                        if(!c.comparisonReached&&world.Time>=Comparison){timer.Restart();State("comparison");c.observerSeconds+=timer.Elapsed.TotalSeconds;c.comparisonReached=true;}
                        if(world.Time>=next){next=world.Time+Interval;Observe();}
                    }
                    world.FlushEconomyLog();Observe();
                }
                c.time=world.Time;c.winner=world.Winner;c.marketTrades=world.Events.Count(e=>e.kind=="MarketTrade");
                c.stageDepartures=world.Events.Count(e=>e.kind=="AIArmyPhase"&&e.message.Contains("assembled"));
                timer.Restart();State("final");c.finalWriteSeconds=timer.Elapsed.TotalSeconds;c.wallSeconds=wall.Elapsed.TotalSeconds;
                c.status=world.IsMatchEnded?"natural_terminal":"observation_cap";c.finishedUtc=DateTime.UtcNow.ToString("o");Save();
            }
            run.finishedUtc=DateTime.UtcNow.ToString("o");run.status="observed_three_declared_cases";Save();
        }
    }
}
