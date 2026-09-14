using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Frontier.Editor
{
    // Read-only observation of two normal AI command clients. No balance overrides,
    // bonus income, fog reveals, invulnerability, or post-bootstrap spawning.
    public static class StrategyAudit
    {
        public static void RunDifficultyMatrix()
        {
            string originalRoot=Environment.GetEnvironmentVariable("FRONTIER_REPORT_PATH"),originalDifficulty=Environment.GetEnvironmentVariable("FRONTIER_AUDIT_DIFFICULTY");
            string root=originalRoot??"Builds/DifficultyMatrix";
            try
            {
                foreach(var definition in Catalog.Load().aiDifficulties)
                {Environment.SetEnvironmentVariable("FRONTIER_REPORT_PATH",Path.Combine(root,definition.id));Environment.SetEnvironmentVariable("FRONTIER_AUDIT_DIFFICULTY",definition.id);Run();}
            }
            finally{Environment.SetEnvironmentVariable("FRONTIER_REPORT_PATH",originalRoot);Environment.SetEnvironmentVariable("FRONTIER_AUDIT_DIFFICULTY",originalDifficulty);}
        }
        public static void RunMatrix()
        {
            string root=Environment.GetEnvironmentVariable("FRONTIER_REPORT_PATH")??"Builds/StrategyMatrix";
            string[] civ0={"river","coast","river","coast","river","coast"};
            string[] civ1={"coast","river","river","coast","coast","river"};
            for(int i=0;i<civ0.Length;i++)
            {
                Environment.SetEnvironmentVariable("FRONTIER_AUDIT_CIV0",civ0[i]);Environment.SetEnvironmentVariable("FRONTIER_AUDIT_CIV1",civ1[i]);
                Environment.SetEnvironmentVariable("FRONTIER_AUDIT_DECK0",i<4?"economy":"military");Environment.SetEnvironmentVariable("FRONTIER_AUDIT_DECK1",i<4?"military":"economy");
                Environment.SetEnvironmentVariable("FRONTIER_REPORT_PATH",Path.Combine(root,i+"-"+civ0[i]+"-"+civ1[i]));Run();
            }
        }
        public static void Run()
        {
            string value(string key,string fallback)=>Environment.GetEnvironmentVariable(key)??fallback;
            string civ0=value("FRONTIER_AUDIT_CIV0","river"),civ1=value("FRONTIER_AUDIT_CIV1","coast");
            int seed=int.Parse(value("FRONTIER_AUDIT_SEED","314159"));
            float limit=float.Parse(value("FRONTIER_AUDIT_LIMIT","2400"),System.Globalization.CultureInfo.InvariantCulture);
            string directory=value("FRONTIER_REPORT_PATH","Builds/StrategyAudit");Directory.CreateDirectory(directory);
            var world=new World(Catalog.Load(),seed,civ0,civ1,5);
            for(int player=0;player<2;player++){var p=world.Players[player];p.deck.Clear();p.deck.AddRange(DeckLibrary.Preset(world.Rules,p.civilization,value("FRONTIER_AUDIT_DECK"+player,player==0?"economy":"military")));}
            string difficulty=value("FRONTIER_AUDIT_DIFFICULTY","standard");
            var simulation=new MatchSimulation(world,new[]{0,1},difficulty);float next=0;
            using(var snapshots=new StreamWriter(Path.Combine(directory,"snapshots.jsonl")))
            {
                while(world.Time<limit&&world.Winner<0)
                {
                    simulation.Step();
                    if(world.Time>=next){next=world.Time+30;for(int player=0;player<2;player++)snapshots.WriteLine(JsonUtility.ToJson(Snapshot(world,player)));}
                }
                for(int player=0;player<2;player++)snapshots.WriteLine(JsonUtility.ToJson(Snapshot(world,player)));
            }
            world.FlushEconomyLog();using(var events=new StreamWriter(Path.Combine(directory,"events.jsonl")))foreach(var e in world.Events)events.WriteLine(JsonUtility.ToJson(e));
            File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(new Result{aiDifficulty=simulation.Difficulty.Id,aiDecisionSeconds=simulation.Difficulty.DecisionSeconds,aiArmySeconds=simulation.Difficulty.ArmySeconds,seed=seed,civilization0=civ0,civilization1=civ1,seconds=world.Time,winner=world.Winner,age0=world.Players[0].age,age1=world.Players[1].age,events=world.Events.Count},true));
            Debug.Log($"STRATEGY_AUDIT {civ0}/{civ1} t={world.Time} winner={world.Winner} age={world.Players[0].age}/{world.Players[1].age}");
        }
        private static EconomySnapshot Snapshot(World w,int player)
        {
            var p=w.Players[player];var own=w.Entities.Where(e=>e.Alive&&e.owner==player).ToArray();var workers=own.Where(e=>e.Worker).ToArray();
            var gathering=workers.Where(e=>e.garrisonedIn==0&&e.orders.Count>0&&e.orders.Peek().kind==OrderKind.Gather).ToArray();
            int on(ResourceKind kind)=>gathering.Count(e=>w.Get(e.orders.Peek().target)?.def.resource==kind);
            return new EconomySnapshot{time=w.Time,player=player,age=p.age,resources=p.resources,gathered=p.gathered,spent=p.spent,bank=p.passiveIncome,trade=p.tradeIncome,workers=workers.Length,sheltered=workers.Count(e=>e.garrisonedIn!=0),food=on(ResourceKind.Food),wood=on(ResourceKind.Wood),coin=on(ResourceKind.Coin),army=own.Count(e=>e.Military&&(e.def.tags&Tags.Explorer)==0),banks=own.Count(e=>e.def.id=="bank"&&e.Complete),shipments=p.shipsSent,population=p.usedPopulation,queued=string.Join(";",own.SelectMany(e=>e.queue).Select(j=>j.kind+":"+j.item+"x"+j.count))};
        }
        [Serializable]private class EconomySnapshot{public float time;public int player,age,workers,sheltered,food,wood,coin,army,banks,shipments,population;public string queued;public Cost resources,gathered,spent,bank,trade;}
        [Serializable]private class Result{public string aiDifficulty;public float aiDecisionSeconds,aiArmySeconds;public int seed,winner,age0,age1,events;public float seconds;public string civilization0,civilization1;public string scope="Headless simulation audit; not native player input, visuals, audio, or performance acceptance";}
    }
}
