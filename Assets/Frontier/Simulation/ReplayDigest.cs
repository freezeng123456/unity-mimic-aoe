using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Frontier
{
    internal sealed class ReplayEventChain:IDisposable
    {
        readonly SHA256 sha=SHA256.Create();
        public string Hash {get;private set;}=SnapshotCodec.Hash("frontier-replay-events-v1");
        public int Count {get;private set;}
        public ReplayEventChain(){}
        public ReplayEventChain(int count,string hash){Count=count;Hash=hash;}
        public string Append(SimEvent value)
        {
            byte[] bytes=sha.ComputeHash(Encoding.UTF8.GetBytes(Hash+"\n"+JsonUtility.ToJson(value)));
            Hash=BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();Count++;return Hash;
        }
        public void Dispose()=>sha.Dispose();
    }
    internal sealed class ReplayInputChain:IDisposable
    {
        readonly SHA256 sha=SHA256.Create();
        public string Hash {get;private set;}=SnapshotCodec.Hash("frontier-replay-inputs-v1");
        public ReplayInputChain(){}
        public ReplayInputChain(string hash){Hash=hash;}
        public void Append(ReplayRecord record)
        {Hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Hash+"\n"+JsonUtility.ToJson(record)))).Replace("-","").ToLowerInvariant();}
        public void Dispose()=>sha.Dispose();
    }
    public static class ReplayDigest
    {
        [Serializable] sealed class Projection
        {public WorldSnapshot world;public MatchSimulationSnapshot simulation;public bool hasGuided;public GuidedPlaySnapshot guided;public string eventHash;}
        internal static MatchSimulationSnapshot SimulationState(MatchSimulation simulation)
        {
            var state=simulation.CaptureState();state.accumulator=0;return state;
        }
        // Accumulated render time and performance timings are presentation data.
        // Ordered technology/AI memory arrays remain intact: their iteration can
        // influence simulation decisions and floating-point arithmetic.
        internal static string FromState(WorldSnapshot world,MatchSimulationSnapshot simulation,bool hasGuided,GuidedPlaySnapshot guided,string eventHash)
        {
            return SnapshotCodec.Hash(JsonUtility.ToJson(new Projection{world=world,simulation=simulation,hasGuided=hasGuided,guided=hasGuided?guided:new GuidedPlaySnapshot(),eventHash=eventHash}));
        }
        public static string Compute(World world,MatchSimulation simulation,GuidedPlaySession guided=null)
        {
            if(world==null||simulation==null||!ReferenceEquals(world,simulation.World))throw new ArgumentException("Missing replay state");
            using(var chain=new ReplayEventChain())
            {
                foreach(var e in world.Events)chain.Append(ReplayEventCopy.Copy(e));
                return FromState(world.CaptureState(false),SimulationState(simulation),guided!=null,guided?.CaptureState(),chain.Hash);
            }
        }
    }
}
