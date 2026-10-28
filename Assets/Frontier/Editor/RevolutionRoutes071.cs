using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Frontier.Editor
{
    // Observes eight normal age-I games. Bootstrap uses the selected faction's
    // actual opening resources; thereafter only MatchSimulation's AI may act.
    // A timeout or early defeat is evidence, never a fabricated completed route.
    public static class RevolutionRoutes071
    {
        public static void Run()
        {
            string directory=Environment.GetEnvironmentVariable("FRONTIER_REPORT_PATH");
            if(string.IsNullOrEmpty(directory)||Directory.Exists(directory)&&Directory.EnumerateFileSystemEntries(directory).Any())throw new IOException("Use a new empty route evidence directory");
            Directory.CreateDirectory(directory);
            var manifest=new Manifest{startedUtc=DateTime.UtcNow.ToString("o"),appVersion=Application.version,unityVersion=Application.unityVersion,
                declaredSourceFingerprint=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT")??"",rulesHash=SnapshotCodec.RulesHash(Catalog.Load()),cases=new Case[8]};
            int index=0;var rules=Catalog.Load();
            foreach(string civ in new[]{"coast","river","france","ottoman"})foreach(int seed in new[]{42,43})
            {
                var item=new Case{id=civ+"-"+(seed==42?"revolution":"empire"),civilization=civ,seed=seed,requestedRoute=seed==42?"revolution":"empire"};manifest.cases[index++]=item;
            }
            Save();
            foreach(var item in manifest.cases)
            {
                string output=Path.Combine(directory,item.id);Directory.CreateDirectory(output);item.startedUtc=DateTime.UtcNow.ToString("o");item.status="running";Save();
                var watch=System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var doc=RiverContent070.Build(rules,RiverContent070.Skirmish);doc.seed=item.seed;
                    for(int player=0;player<2;player++){doc.starts[player].civilization=item.civilization;doc.starts[player].age=1;doc.starts[player].initialResources=rules.Civ(item.civilization).startingResources;}
                    File.WriteAllText(Path.Combine(output,"opening-scenario.json"),ScenarioCodec.Encode(doc,rules));
                    var world=new World(rules,doc,new[]{item.civilization,item.civilization},new[]{0,1},5);var simulation=new MatchSimulation(world,new[]{0,1},"standard");
                    item.startingResources=world.Players[0].resources;item.startingWorkers=world.Entities.Count(e=>e.Alive&&e.owner==0&&e.Worker);
                    using(var summaries=new StreamWriter(Path.Combine(output,"snapshots.jsonl")))
                    {
                        float next=0;
                        while(world.Time<2400&&!world.IsMatchEnded)
                        {
                            simulation.Step();
                            if(world.Players[0].politicalPhase==PoliticalPhase.CivilianRebuilt&&item.firstCivilianRecoveredAt<0&&
                                world.Entities.Any(e=>e.Alive&&e.owner==0&&e.Worker&&!e.def.waterborne))item.firstCivilianRecoveredAt=world.Time;
                            if(world.Time<next)continue;next=world.Time+30;
                            foreach(var p in world.Players)summaries.WriteLine(JsonUtility.ToJson(new Summary{time=world.Time,player=p.id,age=p.age,phase=p.politicalPhase,resources=p.resources,
                                spent=p.spent,gathered=p.gathered,passive=p.passiveIncome,population=p.usedPopulation,reserved=p.reservedPopulation,capacity=p.populationCap,
                                workers=world.Entities.Count(e=>e.Alive&&e.owner==p.id&&e.Worker),revolutionaries=world.Entities.Count(e=>e.Alive&&e.owner==p.id&&e.def.revolutionary)}));
                        }
                    }
                    world.FlushEconomyLog();using(var events=new StreamWriter(Path.Combine(output,"events.jsonl")))foreach(var e in world.Events)events.WriteLine(JsonUtility.ToJson(e));
                    var state=world.Players[0];item.seconds=world.Time;item.winner=world.Winner;item.age=state.age;item.phase=state.politicalPhase;item.spent=state.spent;item.income=state.gathered+state.passiveIncome;
                    item.converted=world.Events.Count(e=>e.player==0&&e.kind=="WorkerConverted");item.revolutionAt=state.revolutionAt;item.rebuiltAt=state.civilianRebuiltAt;
                    item.civilianSourcesRestored=state.politicalPhase==PoliticalPhase.CivilianRebuilt;
                    foreach(var ev in world.Events)
                    {
                        if(!item.civilianSourcesRestored||ev.player!=0||ev.kind!="Gather"||ev.time<=state.civilianRebuiltAt||ev.amount<=0)continue;
                        var worker=world.Get(ev.entity);var source=world.Get(ev.target);
                        if(worker==null||!worker.Worker||worker.def.waterborne||source==null||source.def.id.StartsWith("crate_",StringComparison.Ordinal))continue;
                        if(source.def.resource==ResourceKind.Food)item.naturalIncomeAfterRebuilding.food+=ev.amount;
                        else if(source.def.resource==ResourceKind.Wood)item.naturalIncomeAfterRebuilding.wood+=ev.amount;
                        else if(source.def.resource==ResourceKind.Coin)item.naturalIncomeAfterRebuilding.coin+=ev.amount;
                    }
                    item.civilianEconomyRecovered=item.civilianSourcesRestored&&item.firstCivilianRecoveredAt>=0&&
                        item.naturalIncomeAfterRebuilding.food+item.naturalIncomeAfterRebuilding.wood+item.naturalIncomeAfterRebuilding.coin>0;
                    item.routeReached=item.requestedRoute=="empire"?state.age==5:item.civilianEconomyRecovered;
                    item.naturalTerminal=world.IsMatchEnded;item.status=item.routeReached?"route_reached":
                        item.requestedRoute=="revolution"&&item.civilianSourcesRestored?"government_rebuilt_economy_not_recovered":
                        world.IsMatchEnded?"ended_before_route":"route_not_reached_at_limit";
                    File.WriteAllText(Path.Combine(output,"final-world.json"),JsonUtility.ToJson(world.CaptureState(),true));
                    File.WriteAllText(Path.Combine(output,"final-simulation.json"),JsonUtility.ToJson(simulation.CaptureState(),true));
                }
                catch(Exception error){item.status="exception";item.error=error.ToString();Debug.LogError(error);}
                finally{watch.Stop();item.wallSeconds=watch.Elapsed.TotalSeconds;item.finishedUtc=DateTime.UtcNow.ToString("o");File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(item,true));Save();}
            }
            manifest.finishedUtc=DateTime.UtcNow.ToString("o");manifest.status=manifest.cases.All(c=>c.routeReached)?"all_routes_reached":"observed_routes_incomplete";Save();
            void Save()=>File.WriteAllText(Path.Combine(directory,"manifest.json"),JsonUtility.ToJson(manifest,true));
        }
        [Serializable]sealed class Manifest
        {
            public string startedUtc,finishedUtc,status="running",appVersion,unityVersion,rulesHash,declaredSourceFingerprint;
            public string scope="Normal age-I headless observation, mirror factions, standard difficulty, 2400s cap. Revolution route requires actual civilian rebuilding, a recovered land worker and positive natural-resource income; native input, two physical Macs and balance certification are separate";
            public string sourceBinding="Fingerprint is supplied by the external runner; independent before/after source verification is required";
            public Case[] cases;
        }
        [Serializable]sealed class Case
        {public string id,civilization,requestedRoute,status="planned",startedUtc,finishedUtc,error="";public int seed,age,winner,converted,startingWorkers;public float seconds,revolutionAt,rebuiltAt,firstCivilianRecoveredAt=-1;public double wallSeconds;public bool routeReached,naturalTerminal,civilianSourcesRestored,civilianEconomyRecovered;public PoliticalPhase phase;public Cost startingResources,spent,income,naturalIncomeAfterRebuilding;}
        [Serializable]sealed class Summary
        {public float time;public int player,age,workers,revolutionaries,population,reserved,capacity;public PoliticalPhase phase;public Cost resources,spent,gathered,passive;}
    }
}
