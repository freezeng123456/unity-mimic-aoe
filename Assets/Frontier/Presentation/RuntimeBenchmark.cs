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
        private GameController game;private int count;private float lastFrameAt,elapsedActive,nextOrder,duration=25,warmup=5,pausedSeconds,initialSimulationTime;private int pausedFrames;
        private long initialAllocated;private int gc0,gc1,gc2;private readonly List<float> frames=new List<float>();
        private float simTotal,navTotal,combatTotal,economyTotal,allRunMaxFrameMs;private int samples;private bool saved;
        private readonly List<int> units=new List<int>();private readonly List<int> ships=new List<int>();
        public void Initialize(GameController owner,int population,float durationSeconds=25)
        {
            game=owner;count=population;duration=Mathf.Clamp(durationSeconds,25,600);warmup=duration>25?10:5;var w=game.World;
            foreach(var e in w.Entities)if(e.Alive&&e.def.kind!=EntityKind.Resource)w.Remove(e);
            string[] kinds={"pike","musketeer","hussar","dragoon","cannon"};
            if(w.Layout.HasWater)
            {
                for(int side=0;side<2;side++)
                {
                    var starts=w.Navigation.FormationSlots(new Vector2(side==0?27:101,64),count/2,2.8f);
                    for(int i=0;i<starts.Count;i++){var e=w.Spawn(kinds[i%kinds.Length],side,starts[i]);e.hp=1000000;units.Add(e.id);}
                    var launches=w.WaterNavigation.FormationSlots(w.Layout.PatrolPoints[side],6,3.9f);
                    for(int i=0;i<launches.Count;i++){var e=w.Spawn(i%2==0?"fishing_boat":"gunboat",side,launches[i]);e.hp=1000000;ships.Add(e.id);}
                }
                game.Controls.Focus(w.Layout.Bridges[0].Center);
            }
            else
            {
                for(int i=0;i<count;i++)
                {
                    Vector2 p=new Vector2(35+i%20*1.8f,32+i/20*1.8f);p=w.Navigation.NearestWalkable(p,1);
                    var e=w.Spawn(kinds[i%kinds.Length],i%2,p);e.hp=1000000;units.Add(e.id);
                }
                game.Controls.Focus(new Vector2(52,48));
            }
            lastFrameAt=Time.unscaledTime;elapsedActive=0;nextOrder=1;initialSimulationTime=w.Time;
            initialAllocated=GC.GetTotalMemory(false);gc0=GC.CollectionCount(0);gc1=GC.CollectionCount(1);gc2=GC.CollectionCount(2);
            Debug.Log("BENCHMARK_START units="+count+" rendered="+Screen.width+"x"+Screen.height);
        }
        private void Update()
        {
            if(game==null||saved||game.World==null)return;
            float delta=Mathf.Max(0,Time.unscaledTime-lastFrameAt);lastFrameAt=Time.unscaledTime;
            // A paused fixture is not movement/combat load. Keep its samples and
            // duration out of the active run, and disclose the interruption.
            if(game.World.Paused){pausedSeconds+=delta;pausedFrames++;return;}
            elapsedActive+=delta;float elapsed=elapsedActive;allRunMaxFrameMs=Mathf.Max(allRunMaxFrameMs,Time.unscaledDeltaTime*1000);
            if(elapsed>=nextOrder)
            {
                var w=game.World;nextOrder=elapsed+(w.Layout.HasWater?35:7);
                for(int p=0;p<2;p++)
                {
                    var actors=units.FindAll(id=>{var e=w.Get(id);return e!=null&&e.Alive&&e.owner==p;});
                    var destination=w.Layout.HasWater?new Vector2((elapsed%70<35)==(p==0)?101:27,64):elapsed%14<7?new Vector2(75,90):new Vector2(48,38);
                    w.Issue(new GameCommand{player=p,kind=CommandKind.Order,actors=actors.ToArray(),order=OrderKind.AttackMove,point=destination});
                    if(w.Layout.HasWater)
                    {
                        var boats=ships.FindAll(id=>w.Get(id)?.Alive==true&&w.Get(id).owner==p);
                        var harbor=w.Layout.PatrolPoints[elapsed%70<35?1-p:p];
                        w.Issue(new GameCommand{player=p,kind=CommandKind.Order,actors=boats.ToArray(),order=OrderKind.AttackMove,point=harbor});
                    }
                }
            }
            if(elapsed>=warmup){frames.Add(Time.unscaledDeltaTime*1000);simTotal+=game.LastSimulationFrameMs;navTotal+=game.World.LastNavigationMs;combatTotal+=game.World.LastCombatMs;economyTotal+=game.World.LastEconomyMs;samples++;}
            if(elapsed>=duration)Save();
        }
        private void Save()
        {
            saved=true;frames.Sort();float mean=0;foreach(var x in frames)mean+=x;mean/=Mathf.Max(1,frames.Count);
            int living=0;foreach(int id in units)if(game.World.Get(id)?.Alive==true)living++;
            foreach(int id in ships)if(game.World.Get(id)?.Alive==true)living++;
            var report=new BenchmarkReport{mapId=game.World.MapId,initialLandUnits=units.Count,initialShips=ships.Count,visibleLandAtSave=CountVisible(units,false),visibleShipsAtSave=CountVisible(ships,false),landInCameraAtSave=CountVisible(units,true),shipsInCameraAtSave=CountVisible(ships,true),cameraOrthographicSize=game.GameCamera.orthographicSize,appVersion=Application.version,buildGuid=Application.buildGUID,matchId=game.MatchId,focusedAtSave=Application.isFocused,initialActiveUnits=units.Count+ships.Count,endingActiveUnits=living,seed=game.World.Seed,unity=Application.unityVersion,resolution=Screen.width+"x"+Screen.height,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,memoryMb=SystemInfo.systemMemorySize,build="Development / Mono / macOS arm64",durationSeconds=duration,activeSeconds=elapsedActive,pausedSeconds=pausedSeconds,pausedFrames=pausedFrames,simulationSeconds=game.World.Time-initialSimulationTime,warmupSeconds=warmup,samples=frames.Count,meanFrameMs=mean,p95Ms=Percentile(.95f),p99Ms=Percentile(.99f),maxMeasuredFrameMs=frames.Count==0?0:frames[frames.Count-1],maxIncludingWarmupFrameMs=allRunMaxFrameMs,managedBytes=GC.GetTotalMemory(false),managedGrowthBytes=GC.GetTotalMemory(false)-initialAllocated,unityAllocatedBytes=Profiler.GetTotalAllocatedMemoryLong(),gc0=GC.CollectionCount(0)-gc0,gc1=GC.CollectionCount(1)-gc1,gc2=GC.CollectionCount(2)-gc2,simulationPerRenderedFrameMs=simTotal/Mathf.Max(1,samples),navigationPerTickSampleMs=navTotal/Mathf.Max(1,samples),combatPerTickSampleMs=combatTotal/Mathf.Max(1,samples),economyPerTickSampleMs=economyTotal/Mathf.Max(1,samples)};
            string dir=Path.Combine(Application.persistentDataPath,"Benchmarks");Directory.CreateDirectory(dir);string file=Path.Combine(dir,(game.World.Layout.HasWater?"map-"+game.World.MapId+"-":"")+"units-"+count+".json");File.WriteAllText(file,JsonUtility.ToJson(report,true));Debug.Log("BENCHMARK_SAVED "+file);game.World.Paused=true;game.Capture();
        }
        private int CountVisible(List<int> ids,bool cameraOnly)
        {
            int visible=0;foreach(int id in ids)
            {
                var actor=game.World.Get(id);if(actor==null||!actor.Alive||!game.World.IsVisible(0,actor))continue;
                if(cameraOnly){var p=game.GameCamera.WorldToViewportPoint(new Vector3(actor.position.x,game.World.SurfaceHeight(actor.position,actor.def.waterborne)+1,actor.position.y));if(p.z<=0||p.x<0||p.x>1||p.y<0||p.y>1)continue;}
                visible++;
            }
            return visible;
        }
        private float Percentile(float q)=>frames.Count==0?0:frames[Mathf.FloorToInt((frames.Count-1)*q)];
        [Serializable]private class BenchmarkReport
        {
            public int visibleLandAtSave,visibleShipsAtSave,landInCameraAtSave,shipsInCameraAtSave;
            public int initialLandUnits,initialShips,initialActiveUnits,endingActiveUnits,seed,memoryMb,samples,gc0,gc1,gc2,pausedFrames;
            public string mapId,appVersion,buildGuid,matchId,unity,resolution,cpu,gpu,build;
            public bool focusedAtSave;
            public string fixture="Mixed combat and repeated group movement; elevated runtime HP keeps the requested active-unit count stable. Not a normal match. 60 fps cap, vSync off.";
            public float cameraOrthographicSize,durationSeconds,activeSeconds,pausedSeconds,simulationSeconds,warmupSeconds,meanFrameMs,p95Ms,p99Ms,maxMeasuredFrameMs,maxIncludingWarmupFrameMs,simulationPerRenderedFrameMs,navigationPerTickSampleMs,combatPerTickSampleMs,economyPerTickSampleMs;
            public long managedBytes,managedGrowthBytes,unityAllocatedBytes;
        }
    }
}
