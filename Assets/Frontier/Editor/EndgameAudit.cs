using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Frontier.Editor
{
    // Explicitly staged endgames for observing the existing public AI/world path.
    // These fixture changes are never used by native normal-match initialization.
    public static class EndgameAudit
    {
        public static void Run()
        {
            string root=Environment.GetEnvironmentVariable("FRONTIER_REPORT_PATH")??"Builds/EndgameAudit";
            RunOne(Path.Combine(root,"southeast"),new Vector2(116,16));
            RunOne(Path.Combine(root,"northwest"),new Vector2(16,116));
        }
        private static void RunOne(string directory,Vector2 retreat)
        {
            Directory.CreateDirectory(directory);var w=new World(Catalog.Load(),4801,"coast","coast",3);
            var survivor=w.Entities.First(e=>e.owner==1&&e.Worker);
            foreach(var e in w.Entities.ToArray())
                if(e.def.kind==EntityKind.Resource||(e.def.tags&(Tags.Explorer|Tags.Guardian))!=0||e.owner==1&&e.id!=survivor.id)w.Remove(e);
            w.Players[0].age=2;w.Players[0].resources=new Cost();w.Players[1].resources=new Cost();
            if(!w.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{survivor.id},order=OrderKind.Move,point=retreat}).accepted)throw new InvalidOperationException("Retreat fixture rejected");
            for(int i=0;i<500;i++)w.Tick(.1f);
            if(Vector2.Distance(survivor.position,retreat)>2||w.IsVisible(0,survivor)||w.Winner>=0)throw new InvalidOperationException("Invalid staged endgame");
            var army=Enumerable.Range(0,6).Select(i=>w.Spawn("musketeer",0,new Vector2(31+i%3*2,35+i/3*2))).ToArray();
            var simulation=new MatchSimulation(w,0);float began=w.Time,next=began,seen=-1;
            using(var samples=new StreamWriter(Path.Combine(directory,"army-samples.jsonl")))
            {
                for(int i=0;i<9000&&w.Winner<0;i++)
                {
                    simulation.Step();bool visible=w.IsVisible(0,survivor);if(visible&&seen<0)seen=w.Time;
                    if(w.Time<next)continue;next=w.Time+5;
                    samples.WriteLine(JsonUtility.ToJson(new Sample{time=w.Time,survivorVisible=visible,army=army.Where(e=>e.Alive).Select(e=>e.position).ToArray()}));
                }
            }
            using(var log=new StreamWriter(Path.Combine(directory,"events.jsonl")))foreach(var e in w.Events)log.WriteLine(JsonUtility.ToJson(e));
            var damage=w.Events.Where(e=>e.kind=="Damage"&&e.player==0&&e.target==survivor.id).ToArray();
            var result=new Result{preparedAt=began,endedAt=w.Time,searchSeconds=w.Time-began,firstSeenAt=seen,firstDamageAt=damage.Length>0?damage[0].time:-1,winner=w.Winner,survivor=survivor.id,retreat=retreat,patrolPoints=w.Events.Count(e=>e.kind=="AISearchPoint"),deferredPoints=w.Events.Count(e=>e.kind=="AISearchDeferred"),damageEvents=damage.Length,events=w.Events.Count};
            File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(result,true));
            if(w.Winner!=0||survivor.Alive||seen<0||damage.Length==0||damage[0].time<seen)throw new InvalidOperationException("Search did not finish through observed real combat");
            Debug.Log($"ENDGAME_AUDIT {directory}: winner={w.Winner} search={result.searchSeconds} points={result.patrolPoints} firstSeen={seen}");
        }
        [Serializable]private sealed class Sample{public float time;public bool survivorVisible;public Vector2[] army;}
        [Serializable]private sealed class Result
        {
            public string scope="Staged endgame: opposing base/other units, resources, guardians and explorers removed; six musketeers provided as fixture. Normal movement, visibility, combat and victory; not a standard-opening match or native QA.";
            public int seed=4801,winner,survivor,patrolPoints,deferredPoints,damageEvents,events;
            public float preparedAt,endedAt,searchSeconds,firstSeenAt,firstDamageAt;
            public Vector2 retreat;
        }
    }
}
