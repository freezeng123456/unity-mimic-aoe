#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Frontier
{
    // Isolated-copy instrumentation only. Counts never enter world decisions.
    public static class PerfProbeDiagnostics072
    {
        public static bool Enabled;
        public static long SafeCalls, SegmentSafeCalls, EconomicThreatCalls, PathNodePops, PathClearLineCalls;
        static readonly Dictionary<string,Metric> metrics=new Dictionary<string,Metric>();
        [Serializable] public sealed class Metric
        { public string method; public long calls; public double inclusiveMs,maxMs; }
        public struct Timer : IDisposable
        {
            Metric metric; long started;
            internal Timer(Metric value){metric=value;started=Stopwatch.GetTimestamp();}
            public void Dispose()
            {
                if(metric==null)return;
                double ms=(Stopwatch.GetTimestamp()-started)*1000.0/Stopwatch.Frequency;
                metric.inclusiveMs+=ms;metric.maxMs=Math.Max(metric.maxMs,ms);
            }
        }
        public static Timer Measure(string name)
        {
            if(!Enabled)return default;
            if(!metrics.TryGetValue(name,out var value)){value=new Metric{method=name};metrics.Add(name,value);}
            value.calls++;return new Timer(value);
        }
        public static void Reset()
        {metrics.Clear();SafeCalls=SegmentSafeCalls=EconomicThreatCalls=PathNodePops=PathClearLineCalls=0;}
        public static Metric[] Read()
        {
            var result=new List<Metric>();
            foreach(var value in metrics.Values)result.Add(new Metric{method=value.method,calls=value.calls,inclusiveMs=value.inclusiveMs,maxMs=value.maxMs});
            result.Sort((a,b)=>string.CompareOrdinal(a.method,b.method));return result.ToArray();
        }
    }
}
#endif
