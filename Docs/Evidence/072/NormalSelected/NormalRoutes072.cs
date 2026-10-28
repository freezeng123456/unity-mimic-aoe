using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Frontier.Editor
{
    // A declared observation, not a forced route test. All costs, targeting and
    // outcomes after normal bootstrap are owned by the two scheduled AIs.
    public static class NormalRoutes072
    {
        const float Limit=3600,ComparisonTime=2400,SampleInterval=30;
        public static void Run()
        {
            string directory=Environment.GetEnvironmentVariable("FRONTIER_REPORT_PATH");
            if(string.IsNullOrEmpty(directory)||Directory.Exists(directory)&&Directory.EnumerateFileSystemEntries(directory).Any())
                throw new IOException("Use a new empty 072 observation directory");
            Directory.CreateDirectory(directory);var rules=Catalog.Load();
            var manifest=new Manifest{startedUtc=DateTime.UtcNow.ToString("o"),appVersion=Application.version,unityVersion=Application.unityVersion,
                rulesHash=SnapshotCodec.RulesHash(rules),declaredSourceFingerprint=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT")??"",
                cases=new Case[16]};int index=0;
            foreach(string map in new[]{RiverContent070.Skirmish,MapTopology.Winding})
            foreach(string civ in new[]{"coast","river","france","ottoman"})
            foreach(int seed in map==RiverContent070.Skirmish?new[]{42,43}:new[]{44,45})
                manifest.cases[index++]=new Case{id=map+"-"+civ+"-"+seed,map=map,civilization=civ,seed=seed,requestedRoute=seed%2==0?"revolution":"empire"};
            Save();
            foreach(var item in manifest.cases)
            {
                string output=Path.Combine(directory,item.id);Directory.CreateDirectory(output);
                item.startedUtc=DateTime.UtcNow.ToString("o");item.status="running";Save();
                var watch=System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    World world;
                    if(item.map==RiverContent070.Skirmish)
                    {
                        var doc=RiverContent070.Build(rules,RiverContent070.Skirmish);doc.seed=item.seed;
                        foreach(var start in doc.starts){start.civilization=item.civilization;start.age=1;start.initialResources=rules.Civ(item.civilization).startingResources;}
                        File.WriteAllText(Path.Combine(output,"opening-scenario.json"),ScenarioCodec.Encode(doc,rules));
                        world=new World(rules,doc,new[]{item.civilization,item.civilization},new[]{0,1},5);
                    }
                    else world=new World(rules,item.seed,item.civilization,item.civilization,5,item.map);
                    var simulation=new MatchSimulation(world,new[]{0,1},"standard");
                    File.WriteAllText(Path.Combine(output,"opening-world.json"),JsonUtility.ToJson(world.CaptureState(),true));
                    item.startingResources=world.Players[0].resources;item.startingWorkers=world.Entities.Count(e=>e.Alive&&e.owner==0&&e.Worker);
                    float next=0;bool comparisonWritten=false;int eventCursor=0;
                    using(var samples=new StreamWriter(Path.Combine(output,"samples.jsonl")))
                    using(var events=new StreamWriter(Path.Combine(output,"events.jsonl")))
                    {
                        while(world.Time<Limit&&!world.IsMatchEnded)
                        {
                            if(!simulation.Step())throw new InvalidOperationException("Normal observer stopped before a terminal state");
                            ObserveRoute(world,item);
                            if(!comparisonWritten&&world.Time>=ComparisonTime){WriteComparison(world,item,output);comparisonWritten=true;}
                            if(world.Time>=next)
                            {
                                next=world.Time+SampleInterval;
                                foreach(var player in world.Players)samples.WriteLine(JsonUtility.ToJson(Sample(world,simulation,player.id)));
                                while(eventCursor<world.Events.Count)events.WriteLine(JsonUtility.ToJson(world.Events[eventCursor++]));
                            }
                        }
                        world.FlushEconomyLog();while(eventCursor<world.Events.Count)events.WriteLine(JsonUtility.ToJson(world.Events[eventCursor++]));
                        foreach(var player in world.Players)samples.WriteLine(JsonUtility.ToJson(Sample(world,simulation,player.id)));
                    }
                    if(!comparisonWritten)WriteComparison(world,item,output);
                    var p=world.Players[0];item.seconds=world.Time;item.winner=world.Winner;item.age=p.age;item.phase=p.politicalPhase;
                    item.revolutionAt=p.revolutionAt;item.rebuiltAt=p.civilianRebuiltAt;item.spent=p.spent;item.income=p.gathered+p.passiveIncome;
                    item.civilianSourcesRestored=p.politicalPhase==PoliticalPhase.CivilianRebuilt;
                    foreach(var ev in world.Events)
                    {
                        if(!item.civilianSourcesRestored||ev.player!=0||ev.kind!="Gather"||ev.time<=p.civilianRebuiltAt||ev.amount<=0)continue;
                        var worker=world.Get(ev.entity);var source=world.Get(ev.target);
                        if(worker==null||!worker.Worker||worker.def.waterborne||source==null||source.def.id.StartsWith("crate_",StringComparison.Ordinal))continue;
                        if(source.def.resource==ResourceKind.Food)item.naturalIncomeAfterRebuilding.food+=ev.amount;
                        else if(source.def.resource==ResourceKind.Wood)item.naturalIncomeAfterRebuilding.wood+=ev.amount;
                        else if(source.def.resource==ResourceKind.Coin)item.naturalIncomeAfterRebuilding.coin+=ev.amount;
                    }
                    item.civilianEconomyRecovered=item.civilianSourcesRestored&&item.firstCivilianRecoveredAt>=0&&
                        item.naturalIncomeAfterRebuilding.food+item.naturalIncomeAfterRebuilding.wood+item.naturalIncomeAfterRebuilding.coin>0;
                    item.routeReached=item.requestedRoute=="empire"?p.age==5:item.civilianEconomyRecovered;
                    item.naturalTerminal=world.IsMatchEnded;item.status=item.routeReached?"route_reached":
                        item.civilianSourcesRestored?"government_rebuilt_economy_not_recovered":world.IsMatchEnded?"ended_before_route":"route_not_reached_at_declared_limit";
                    File.WriteAllText(Path.Combine(output,"final-world.json"),JsonUtility.ToJson(world.CaptureState(),true));
                    File.WriteAllText(Path.Combine(output,"final-simulation.json"),JsonUtility.ToJson(simulation.CaptureState(),true));
                }
                catch(Exception error){item.status="exception";item.error=error.ToString();Debug.LogError(error);}
                finally{watch.Stop();item.wallSeconds=watch.Elapsed.TotalSeconds;item.finishedUtc=DateTime.UtcNow.ToString("o");File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(item,true));Save();}
            }
            manifest.finishedUtc=DateTime.UtcNow.ToString("o");manifest.status=manifest.cases.Any(c=>c.status=="exception")?"observation_errors":
                manifest.cases.All(c=>c.routeReached)?"all_observed_routes_reached":"observed_routes_incomplete";Save();
            void Save()=>File.WriteAllText(Path.Combine(directory,"manifest.json"),JsonUtility.ToJson(manifest,true));
        }
        static void ObserveRoute(World world,Case item)
        {
            foreach(var town in world.Entities.Where(e=>e.Alive&&e.owner==0&&e.def.id=="town"))foreach(var job in town.queue)
            {
                if(job.kind=="Age"&&job.item=="5"&&item.empirePaidAt<0)item.empirePaidAt=world.Time;
                if(job.kind=="Revolution"&&item.revolutionPaidAt<0)item.revolutionPaidAt=world.Time;
                if(job.kind=="Research"&&job.item==World.CivilianRebuildingTechnology&&item.governmentPaidAt<0)item.governmentPaidAt=world.Time;
            }
            if(world.Players[0].age==5&&item.empireCompletedAt<0)item.empireCompletedAt=world.Time;
            if(world.Players[0].politicalPhase==PoliticalPhase.CivilianRebuilt&&item.firstCivilianRecoveredAt<0&&
                world.Entities.Any(e=>e.Alive&&e.owner==0&&e.Worker&&!e.def.waterborne))item.firstCivilianRecoveredAt=world.Time;
        }
        static Summary Sample(World world,MatchSimulation simulation,int player)
        {
            var p=world.Players[player];var own=world.Entities.Where(e=>e.Alive&&e.owner==player).ToArray();
            return new Summary{time=world.Time,player=player,age=p.age,phase=p.politicalPhase,resources=p.resources,
                spent=p.spent,gathered=p.gathered,passive=p.passiveIncome,population=p.usedPopulation,reserved=p.reservedPopulation,capacity=p.populationCap,
                workers=own.Count(e=>e.Worker),soldiers=own.Count(e=>e.Military&&!e.def.waterborne&&(e.def.tags&Tags.Explorer)==0),
                buildings=own.Where(e=>e.def.kind==EntityKind.Building).Select(e=>new Building{id=e.id,definition=e.def.id,position=e.position,complete=e.Complete,rally=e.rally,queue=e.queue.ToArray(),factoryMode=e.factoryMode}).ToArray(),
                army=own.Where(e=>e.Military&&!e.def.waterborne&&(e.def.tags&Tags.Explorer)==0).Select(e=>new Soldier{id=e.id,definition=e.def.id,position=e.position,hp=e.hp,
                    garrisonedIn=e.garrisonedIn,orders=e.orders.ToArray(),path=e.path.ToArray(),status=e.status}).ToArray(),
                simulation=simulation.CaptureState()};
        }
        static void WriteComparison(World world,Case item,string output)
        {
            item.comparisonAt=world.Time;item.ageAtComparison=world.Players[0].age;item.phaseAtComparison=world.Players[0].politicalPhase;
            item.comparisonReached=world.Time>=ComparisonTime;
            item.comparisonStatus=item.comparisonReached?"matched_horizon_reached":"natural_terminal_before_matched_horizon";
            // This projection keeps pending income and orders without duplicating
            // the historical ledger. Original events are streamed separately.
            File.WriteAllText(Path.Combine(output,item.comparisonReached?"comparison-2400-world.json":"early-terminal-world.json"),JsonUtility.ToJson(world.CaptureState(false),true));
        }
        [Serializable]sealed class Manifest
        {
            public string startedUtc,finishedUtc,status="planned",appVersion,unityVersion,rulesHash,declaredSourceFingerprint;
            public float declaredLimitSeconds=Limit,comparisonSeconds=ComparisonTime,sampleIntervalSeconds=SampleInterval;
            public string scope="16 normal age-I mirror games, standard difficulty. Traders' Bend seeds42/43 reproduce the eight 071 openings; Winding seeds44/45 add a second map. Stop at natural terminal or predeclared3600s. A2400 projection is recorded only when reached; earlier natural terminal has a separate projection and explicit status. This is new072 evidence. Positions, orders, paid queues and completed phases are recorded separately. No bootstrap top-ups, forced victory or runtime interventions.";
            public string sourceBinding="External before/after fingerprint verification required. Diagnostic samples include both AI states and are not inputs to AI decisions.";
            public Case[] cases;
        }
        [Serializable]sealed class Case
        {
            public string id,map,civilization,requestedRoute,status="planned",startedUtc,finishedUtc,error="",comparisonStatus="not_observed";
            public int seed,age,winner,startingWorkers,ageAtComparison;
            public float seconds,comparisonAt,revolutionAt,rebuiltAt,firstCivilianRecoveredAt=-1,empirePaidAt=-1,empireCompletedAt=-1,revolutionPaidAt=-1,governmentPaidAt=-1;
            public double wallSeconds;public bool routeReached,naturalTerminal,civilianSourcesRestored,civilianEconomyRecovered,comparisonReached;
            public PoliticalPhase phase,phaseAtComparison;public Cost startingResources,spent,income,naturalIncomeAfterRebuilding;
        }
        [Serializable]sealed class Summary
        {public float time;public int player,age,workers,soldiers,population,reserved,capacity;public PoliticalPhase phase;public Cost resources,spent,gathered,passive;public Soldier[] army;public Building[] buildings;public MatchSimulationSnapshot simulation;}
        [Serializable]sealed class Soldier
        {public int id,garrisonedIn;public string definition,status;public Vector2 position;public float hp;public UnitOrder[] orders;public Vector2[] path;}
        [Serializable]sealed class Building
        {public int id;public string definition,factoryMode;public Vector2 position,rally;public bool complete;public ProductionJob[] queue;}
    }
}
