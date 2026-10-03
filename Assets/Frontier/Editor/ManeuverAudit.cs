using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Frontier.Editor
{
    // Reproducible synthetic navigation fixtures. These measure simulation work,
    // not rendering FPS, native mouse latency, paid production or game balance.
    public static class ManeuverAudit
    {
        static readonly string[] Kinds={"pike","musketeer","hussar","dragoon","cannon","carabineer","cuirassier","janissary","abus","spahi","halberdier"};
        const float Step=.1f,Limit=240,ArrivalRadius=1;
        public static void Run()
        {
            string directory=Environment.GetEnvironmentVariable("FRONTIER_REPORT_PATH");
            if(string.IsNullOrEmpty(directory))throw new ArgumentException("FRONTIER_REPORT_PATH must identify a fresh directory");
            if(Directory.Exists(directory)&&Directory.EnumerateFileSystemEntries(directory).Any())throw new IOException("Evidence directory is not empty: "+directory);
            Directory.CreateDirectory(directory);
            var report=new Report{unity=Application.unityVersion,version=Application.version,startedUtc=DateTime.UtcNow.ToString("o"),processor=SystemInfo.processorType};
            // Warm the runtime before collecting timing samples; retain every
            // subsequently measured tick, including replans and outliers.
            var warm=Arena(false);var warmUnits=Group(warm,12,new Vector2(28,64));Command(warm,warmUnits,new Vector2(99,64));
            for(int i=0;i<50;i++)warm.Tick(Step);
            var cases=new[]{
                new Spec("open-48",48),new Spec("turn-90-96",96,turn:new Vector2(50,102)),
                new Spec("reverse-96",96,turn:new Vector2(20,64)),
                new Spec("gate-100",100,gate:true),new Spec("gate-200",200,gate:true),new Spec("gate-400",400,gate:true),
                new Spec("counterflow-100",100,gate:true,counterflow:true),new Spec("blocked-then-open-48",48,gate:true,closed:true)
            };
            foreach(var spec in cases)
            {
                var item=Measure(spec,directory);report.cases.Add(item);
                File.WriteAllText(Path.Combine(directory,"report.json"),JsonUtility.ToJson(report,true));
                Debug.Log($"MANEUVER {item.id}: {item.arrived}/{item.count} arrived, last {item.arrivalMax:F2}s; tick p95 {item.tickMs.p95:F3}ms; {item.status}");
            }
            report.finishedUtc=DateTime.UtcNow.ToString("o");report.status=report.cases.All(c=>c.status=="passed")?"passed":"failed";
            File.WriteAllText(Path.Combine(directory,"report.json"),JsonUtility.ToJson(report,true));
            if(report.status!="passed")throw new InvalidOperationException("Maneuver acceptance failed; all cases retained in "+directory);
        }
        static World Arena(bool gate)
        {
            var rules=JsonUtility.FromJson<RuleCatalog>(JsonUtility.ToJson(Catalog.Load()));
            // Isolate locomotion from casualties and post-arrival acquisition.
            // Speeds, radii and the production pathfinder remain unchanged.
            foreach(var d in rules.entities){d.damage=0;d.meleeDamage=0;d.siegeDamage=0;}
            var w=new World(rules,314159,"coast","river",5);
            foreach(var e in w.Entities.ToArray())if(e.def.id!="town")w.Remove(e);
            foreach(var o in w.TerrainObstacles)w.Navigation.SetBlocked(new Vector2(o.x,o.y),o.z,false);
            if(gate)for(int y=0;y<128;y++)if(y<57||y>71)w.Navigation.SetBlocked(new Vector2(64,y),.45f,true);
            return w;
        }
        static List<EntityState> Group(World w,int count,Vector2 center)
        {
            var slots=w.Navigation.FormationSlots(center,count,2.8f);
            if(slots.Count!=count)throw new InvalidOperationException("Incomplete spawn formation");
            var units=slots.Select((p,i)=>w.Spawn(Kinds[i%Kinds.Length],0,p)).ToList();
            foreach(var e in units)if(!w.Navigation.IsWalkable(e.position,e.def.radius))throw new InvalidOperationException("Blocked fixture start");
            for(int i=0;i<units.Count;i++)for(int j=i+1;j<units.Count;j++)
                if(Vector2.Distance(units[i].position,units[j].position)<=units[i].def.radius+units[j].def.radius)throw new InvalidOperationException("Overlapping fixture start");
            return units;
        }
        static double Command(World w,List<EntityState> units,Vector2 target)
        {
            var command=new GameCommand{player=0,kind=CommandKind.Order,actors=units.Select(e=>e.id).ToArray(),order=OrderKind.Move,point=target};
            var watch=System.Diagnostics.Stopwatch.StartNew();var result=w.Issue(command);watch.Stop();
            if(!result.accepted)throw new InvalidOperationException(result.reason);
            if(units.Any(e=>e.orders.Count!=1||e.orders.Peek().kind!=OrderKind.Move))throw new InvalidOperationException("Not every actor accepted the move");
            return watch.Elapsed.TotalMilliseconds;
        }
        static Case Measure(Spec spec,string directory)
        {
            var w=Arena(spec.gate);
            if(spec.closed)for(int y=57;y<=71;y++)w.Navigation.SetBlocked(new Vector2(64,y),.45f,true);
            var units=Group(w,spec.counterflow?spec.count/2:spec.count,new Vector2(spec.turn.HasValue?40:28,64));
            var item=new Case{id=spec.id,count=spec.count,limitSeconds=Limit,gateWidth=spec.gate?15:0};
            item.commandMs.Add(Command(w,units,new Vector2(99,64)));
            if(spec.counterflow)
            {
                var right=Group(w,spec.count/2,new Vector2(99,64));item.commandMs.Add(Command(w,right,new Vector2(28,64)));units.AddRange(right);
            }
            if(spec.turn.HasValue)
            {
                for(int i=0;i<20;i++)w.Tick(Step);
                item.commandMs.Add(Command(w,units,spec.turn.Value));
            }
            float started=w.Time;
            var rows=units.Select(e=>new Unit{entity=e.id,kind=e.def.id,start=e.position,goal=e.orders.Peek().point}).ToArray();
            item.distinctGoals=rows.Select(e=>e.goal).Distinct().Count();
            item.allOrdersAccepted=units.All(e=>e.orders.Count==1&&e.orders.Peek().kind==OrderKind.Move);
            var ticks=new List<double>();var nav=new List<double>();var trace=new System.Text.StringBuilder("tick,simulation_seconds,tick_ms,navigation_ms,arrived\n");
            bool released=false;
            for(int tick=0;tick<Mathf.RoundToInt(Limit/Step);tick++)
            {
                if(spec.closed&&!released&&tick==100)
                {for(int y=57;y<=71;y++)w.Navigation.SetBlocked(new Vector2(64,y),.45f,false);released=true;item.gateOpenedAt=w.Time-started;}
                var before=units.Select(e=>e.position).ToArray();
                var watch=System.Diagnostics.Stopwatch.StartNew();w.Tick(Step);watch.Stop();ticks.Add(watch.Elapsed.TotalMilliseconds);nav.Add(w.LastNavigationMs);
                int arrived=0;
                for(int i=0;i<units.Count;i++)
                {
                    var e=units[i];var row=rows[i];var delta=e.position-before[i];float distance=delta.magnitude;
                    row.pathLength+=distance;row.final=e.position;row.finalDistance=Vector2.Distance(e.position,row.goal);
                    if(row.firstMotion<0&&Vector2.Distance(e.position,row.start)>.02f)row.firstMotion=w.Time-started;
                    if(row.firstProgress<0&&Vector2.Dot(e.position-row.start,(row.goal-row.start).normalized)>.02f)row.firstProgress=w.Time-started;
                    if(!w.Navigation.IsWalkable(e.position,e.def.radius)){row.obstacleViolations++;item.obstacleViolations++;}
                    if(distance>w.Speed(e)*Step+.002f){row.teleports++;item.teleports++;}
                    if(spec.closed&&!released&&e.position.x>=63) item.crossedClosedBarrier++;
                    if(row.finalDistance<ArrivalRadius){arrived++;if(row.firstArrival<0)row.firstArrival=w.Time-started;}
                }
                trace.Append(tick).Append(',').Append((w.Time-started).ToString("F4",CultureInfo.InvariantCulture)).Append(',')
                    .Append(watch.Elapsed.TotalMilliseconds.ToString("F5",CultureInfo.InvariantCulture)).Append(',')
                    .Append(w.LastNavigationMs.ToString("F5",CultureInfo.InvariantCulture)).Append(',').Append(arrived).Append('\n');
                item.arrived=arrived;item.simulatedSeconds=w.Time-started;
                if(arrived==units.Count)break;
            }
            item.units=rows;item.tickMs=Stats.Of(ticks);item.navigationMs=Stats.Of(nav);
            item.arrivalP50=Percentile(rows.Where(u=>u.firstArrival>=0).Select(u=>(double)u.firstArrival),.5);
            item.arrivalP95=Percentile(rows.Where(u=>u.firstArrival>=0).Select(u=>(double)u.firstArrival),.95);
            item.arrivalMax=rows.Max(u=>u.firstArrival);item.motionMax=rows.Max(u=>u.firstMotion);item.progressMax=rows.Max(u=>u.firstProgress);
            item.status=item.arrived==item.count&&item.allOrdersAccepted&&item.distinctGoals==item.count&&item.obstacleViolations==0&&item.teleports==0&&item.crossedClosedBarrier==0&&
                (spec.gate||rows.All(u=>u.firstProgress>=0&&u.firstProgress<=2.001f))?"passed":"failed";
            File.WriteAllText(Path.Combine(directory,spec.id+".json"),JsonUtility.ToJson(item,true));
            File.WriteAllText(Path.Combine(directory,spec.id+"-ticks.csv"),trace.ToString());return item;
        }
        static double Percentile(IEnumerable<double> values,double p)
        {var a=values.OrderBy(v=>v).ToArray();return a.Length==0?-1:a[Math.Max(0,(int)Math.Ceiling(a.Length*p)-1)];}
        sealed class Spec
        {
            public string id;public int count;public bool gate,counterflow,closed;public Vector2? turn;
            public Spec(string id,int count,bool gate=false,bool counterflow=false,bool closed=false,Vector2? turn=null)
            {this.id=id;this.count=count;this.gate=gate;this.counterflow=counterflow;this.closed=closed;this.turn=turn;}
        }
        [Serializable]sealed class Report
        {
            public string scope="Synthetic locomotion: 11 stock unit types, normal radii/speeds, all weapon damage disabled, no AI, no paid production. Timings exclude instrumentation and rendering. Not a native FPS/input-latency certification.";
            public string acceptance="Every unit within 1 world unit of its distinct assigned goal simultaneously by 240 simulated seconds; no obstacle penetration or speed-limit violation; every command replaces its queue. Open/turn cases: every unit makes progress toward its new goal within 2 seconds. Closed barrier: no crossing before release at 10 seconds. Failures retained.";
            public string unity,version,processor,startedUtc,finishedUtc,status="running";public int seed=314159;public float fixedStep=Step;
            public List<Case> cases=new List<Case>();
        }
        [Serializable]sealed class Case
        {
            public string id,status;public int count,arrived,distinctGoals,gateWidth,obstacleViolations,teleports,crossedClosedBarrier;public bool allOrdersAccepted;
            public float limitSeconds,simulatedSeconds,gateOpenedAt,arrivalMax,motionMax,progressMax;public double arrivalP50,arrivalP95;
            public List<double> commandMs=new List<double>();public Stats tickMs,navigationMs;public Unit[] units;
        }
        [Serializable]sealed class Unit
        {public int entity,obstacleViolations,teleports;public string kind;public Vector2 start,goal,final;public float firstMotion=-1,firstProgress=-1,firstArrival=-1,pathLength,finalDistance;}
        [Serializable]sealed class Stats
        {
            public int samples;public double p50,p95,max;
            public static Stats Of(List<double> x)=>new Stats{samples=x.Count,p50=Percentile(x,.5),p95=Percentile(x,.95),max=x.Count>0?x.Max():-1};
        }
    }
}
