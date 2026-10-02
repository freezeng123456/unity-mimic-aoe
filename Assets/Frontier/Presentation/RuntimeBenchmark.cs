using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

namespace Frontier
{
    // Opt-in benchmark fixture. Never enabled by the skirmish menu or the normal AI.
    public sealed class RuntimeBenchmark : MonoBehaviour
    {
        private GameController game;private int count;private float started,nextOrder,duration=25,warmup=5;
        private long initialAllocated;private int gc0,gc1,gc2;private readonly List<float> frames=new List<float>();
        private float simTotal,navTotal,combatTotal,economyTotal;private int samples;private bool saved;
        private readonly List<int> units=new List<int>();
        public void Initialize(GameController owner,int population,float durationSeconds=25)
        {
            game=owner;count=population;duration=Mathf.Clamp(durationSeconds,25,600);warmup=duration>25?10:5;var w=game.World;
            foreach(var e in w.Entities)if(e.Alive&&e.def.kind!=EntityKind.Resource)w.Remove(e);
            string[] kinds={"pike","musketeer","hussar","dragoon","cannon"};
            for(int i=0;i<count;i++)
            {
                Vector2 p=new Vector2(35+i%20*1.8f,32+i/20*1.8f);p=w.Navigation.NearestWalkable(p,1);
                var e=w.Spawn(kinds[i%kinds.Length],i%2,p);e.hp=1000000;units.Add(e.id);
            }
            game.Controls.Focus(new Vector2(52,48));started=Time.unscaledTime;nextOrder=started+1;
            initialAllocated=GC.GetTotalMemory(false);gc0=GC.CollectionCount(0);gc1=GC.CollectionCount(1);gc2=GC.CollectionCount(2);
            Debug.Log("BENCHMARK_START units="+count+" rendered="+Screen.width+"x"+Screen.height);
        }
        private void Update()
        {
            if(game==null||saved)return;float elapsed=Time.unscaledTime-started;
            if(Time.unscaledTime>=nextOrder)
            {
                nextOrder=Time.unscaledTime+7;var w=game.World;
                for(int p=0;p<2;p++)
                {
                    var actors=units.FindAll(id=>{var e=w.Get(id);return e!=null&&e.Alive&&e.owner==p;});
                    var destination=elapsed%14<7?new Vector2(75,90):new Vector2(48,38);
                    w.Issue(new GameCommand{player=p,kind=CommandKind.Order,actors=actors.ToArray(),order=OrderKind.AttackMove,point=destination});
                }
            }
            if(elapsed>=warmup){frames.Add(Time.unscaledDeltaTime*1000);simTotal+=game.LastSimulationFrameMs;navTotal+=game.World.LastNavigationMs;combatTotal+=game.World.LastCombatMs;economyTotal+=game.World.LastEconomyMs;samples++;}
            if(elapsed>=duration)Save();
        }
        private void Save()
        {
            saved=true;frames.Sort();float mean=0;foreach(var x in frames)mean+=x;mean/=Mathf.Max(1,frames.Count);
            int living=0;foreach(int id in units)if(game.World.Get(id)?.Alive==true)living++;
            var report=new BenchmarkReport{appVersion=Application.version,buildGuid=Application.buildGUID,matchId=game.MatchId,focusedAtSave=Application.isFocused,initialActiveUnits=count,endingActiveUnits=living,seed=game.World.Seed,unity=Application.unityVersion,resolution=Screen.width+"x"+Screen.height,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,memoryMb=SystemInfo.systemMemorySize,build="Development / Mono / macOS arm64",durationSeconds=duration,warmupSeconds=warmup,samples=frames.Count,meanFrameMs=mean,p95Ms=Percentile(.95f),p99Ms=Percentile(.99f),managedBytes=GC.GetTotalMemory(false),managedGrowthBytes=GC.GetTotalMemory(false)-initialAllocated,unityAllocatedBytes=Profiler.GetTotalAllocatedMemoryLong(),gc0=GC.CollectionCount(0)-gc0,gc1=GC.CollectionCount(1)-gc1,gc2=GC.CollectionCount(2)-gc2,simulationPerRenderedFrameMs=simTotal/Mathf.Max(1,samples),navigationPerTickSampleMs=navTotal/Mathf.Max(1,samples),combatPerTickSampleMs=combatTotal/Mathf.Max(1,samples),economyPerTickSampleMs=economyTotal/Mathf.Max(1,samples)};
            string dir=Path.Combine(Application.persistentDataPath,"Benchmarks");Directory.CreateDirectory(dir);string file=Path.Combine(dir,"units-"+count+".json");File.WriteAllText(file,JsonUtility.ToJson(report,true));Debug.Log("BENCHMARK_SAVED "+file);game.World.Paused=true;game.Capture();
        }
        private float Percentile(float q)=>frames.Count==0?0:frames[Mathf.FloorToInt((frames.Count-1)*q)];
        [Serializable]private class BenchmarkReport
        {
            public int initialActiveUnits,endingActiveUnits,seed,memoryMb,samples,gc0,gc1,gc2;
            public string appVersion,buildGuid,matchId,unity,resolution,cpu,gpu,build;
            public bool focusedAtSave;
            public string fixture="Mixed combat and repeated group movement; elevated runtime HP keeps the requested active-unit count stable. Not a normal match. 60 fps cap, vSync off.";
            public float durationSeconds,warmupSeconds,meanFrameMs,p95Ms,p99Ms,simulationPerRenderedFrameMs,navigationPerTickSampleMs,combatPerTickSampleMs,economyPerTickSampleMs;
            public long managedBytes,managedGrowthBytes,unityAllocatedBytes;
        }
    }
}
