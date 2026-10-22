using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Real coarse/fine graph queries over an actual Estuary mask. The local
    // chamber is an explicit geometry fixture; no planner path is injected.
    public sealed class BridgeFineConsistency082Tests
    {
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        [Serializable] sealed class Rule {public int bridge;public float direction;}
        [Serializable] sealed class Receipt
        {
            public string sourceFingerprint;public int fineAttempts,coarseAttempts,firstInvalidLeg=-1;
            public bool accepted,deferred,worldUnchanged;public Vector2 start,goal;public Vector2[] path;public Rule[] plannedRules,rebuiltRules;
            public string scope="Actual recovery graph query in explicit narrow chamber over Estuary; no Tick, path injection or400-arrival claim";
        }
        static Rule[] Rules(World w)
        {
            var result=new List<Rule>();foreach(var value in (IList)typeof(World).GetField("bridgeTrafficRules",Flags).GetValue(w))
            {var type=value.GetType();result.Add(new Rule{bridge=Array.IndexOf(w.Layout.Bridges,(BridgeSpan)type.GetField("span").GetValue(value)),direction=(float)type.GetField("direction").GetValue(value)});}return result.ToArray();
        }
        [Test] public void ActualFineRecoveryInAnEstuaryChamberMustNotReturnASelfOccludedRoute()
        {
            var w=new World(Catalog.Load(),314159,"coast","coast",5,MapTopology.Estuary);
            foreach(var e in w.Entities.Where(e=>e.def.id!="town").ToArray())w.Remove(e);
            for(int x=92;x<=103;x++){w.Navigation.SetBlocked(new Vector2(x,59),0,true);w.Navigation.SetBlocked(new Vector2(x,64),0,true);}
            for(int y=59;y<=64;y++)w.Navigation.SetBlocked(new Vector2(103,y),0,true);
            w.Spawn("hussar",0,new Vector2(94,60.08f));w.Spawn("hussar",0,new Vector2(94,62.42f));
            var actor=w.Spawn("musketeer",0,new Vector2(98.2f,61.2f));Vector2 goal=new Vector2(23.7f,60.7f);
            Assert.IsTrue(w.Navigation.IsWalkable(actor.position,actor.def.radius));Assert.IsTrue(w.Navigation.IsWalkable(goal,actor.def.radius));
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,order=OrderKind.Move,actors=new[]{actor.id},point=goal}).accepted);
            Assert.IsEmpty(actor.path);typeof(World).GetMethod("RebuildSpatial",Flags).Invoke(w,null);
            var receipt=new Receipt{sourceFingerprint=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT"),start=actor.position,goal=goal};
            receipt.accepted=(bool)typeof(World).GetMethod("TryAllyRecovery",Flags).Invoke(w,new object[]{actor,goal});
            receipt.fineAttempts=(int)typeof(World).GetField("fineRecoveryAttempts",Flags).GetValue(w);receipt.coarseAttempts=(int)typeof(World).GetField("allyRecoveryCoarseAttempts",Flags).GetValue(w);
            receipt.deferred=(bool)typeof(World).GetField("fineRecoveryDeferred",Flags).GetValue(w);receipt.path=actor.path.ToArray();receipt.plannedRules=Rules(w);
            string before=JsonUtility.ToJson(w.CaptureState());typeof(World).GetMethod("PrepareBridgeTraffic",Flags).Invoke(w,new object[]{actor,goal,actor.path});receipt.rebuiltRules=Rules(w);
            var line=typeof(World).GetMethod("MovementLineFree",Flags);
            for(int i=1;i<actor.path.Count;i++)if(!(bool)line.Invoke(w,new object[]{actor,actor.path[i-1],actor.path[i]})){receipt.firstInvalidLeg=i;break;}
            receipt.worldUnchanged=before==JsonUtility.ToJson(w.CaptureState());
            string output=Environment.GetEnvironmentVariable("FRONTIER082_EVIDENCE");if(string.IsNullOrEmpty(output))output=Path.Combine(Directory.GetCurrentDirectory(),"Builds","BridgeTraffic082");Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output,"bridge-fine-estuary-chamber.json"),JsonUtility.ToJson(receipt,true));
            Assert.AreEqual(1,receipt.fineAttempts,"The real parked-body passage must require exactly one quarter-meter query");
            Assert.IsTrue(receipt.accepted||receipt.deferred,"A valid narrow passage must return a valid route or explicitly continue its real recovery");
            Assert.AreEqual(-1,receipt.firstInvalidLeg,"The actual accepted fine route must survive its own rebuilt bridge rules");
            Assert.IsTrue(receipt.worldUnchanged,"Rule/line queries cannot mutate serialized World");
        }
    }
}
