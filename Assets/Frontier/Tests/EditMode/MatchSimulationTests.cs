using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Frontier.Tests
{
    public sealed class MatchSimulationTests
    {
        static World NewWorld()=>new World(Catalog.Load(),314159,"coast","river",5);
        [Test] public void AIPlayerSlotsDispatchInNativeOrderRegardlessOfRegistrationOrder()
        {
            var a=NewWorld();var b=NewWorld();new MatchSimulation(a,0,1).Step();new MatchSimulation(b,1,0,1).Step();
            var payments=a.Events.Where(e=>e.kind=="Payment").ToArray();Assert.Greater(payments.Length,1);Assert.AreEqual(1,payments[0].player);
            CollectionAssert.AreEqual(a.Events.Select(JsonUtility.ToJson).ToArray(),b.Events.Select(JsonUtility.ToJson).ToArray());
        }
        [Test] public void VariableRenderBatchesProduceTheSameActualAIAndWorldEvents()
        {
            var direct=new MatchSimulation(NewWorld(),0,1);var frames=new MatchSimulation(NewWorld(),1,0);float[] chunks={.025f,.075f,.2f,.05f};int n=0;
            while(frames.StepCount<1800&&n<7200&&!frames.World.Paused&&!frames.World.IsMatchEnded)frames.AdvanceFrame(chunks[n++%chunks.Length]);
            for(int i=0;i<frames.StepCount;i++)Assert.IsTrue(direct.Step(),"Direct simulation must reach every completed render-batched step");
            Assert.AreEqual(direct.World.Time,frames.World.Time);
            CollectionAssert.AreEqual(direct.World.Events.Select(JsonUtility.ToJson).ToArray(),frames.World.Events.Select(JsonUtility.ToJson).ToArray());
            Assert.AreEqual(direct.World.Winner,frames.World.Winner);Assert.AreEqual(direct.World.IsMatchEnded,frames.World.IsMatchEnded);
            Assert.GreaterOrEqual(frames.StepCount,1800,"Render batches stopped before the declared horizon: frames="+n+", winner="+frames.World.Winner+", paused="+frames.World.Paused);
        }
        [Test] public void PauseAndTerminalStateDoNotAdvanceOrAccumulateAIDecisions()
        {
            var sim=new MatchSimulation(NewWorld(),0,1);sim.AdvanceFrame(.05f);sim.World.Paused=true;int events=sim.World.Events.Count;
            Assert.IsFalse(sim.Step());Assert.AreEqual(0,sim.AdvanceFrame(30));Assert.AreEqual(events,sim.World.Events.Count);
            sim.World.Paused=false;Assert.AreEqual(1,sim.AdvanceFrame(.05f));sim.World.Winner=1;events=sim.World.Events.Count;
            Assert.IsFalse(sim.Step());Assert.AreEqual(0,sim.AdvanceFrame(20));Assert.AreEqual(events,sim.World.Events.Count);
        }
        [Test] public void CatchupIsBoundedAndHumanSlotGetsNoUnrequestedAI()
        {
            var sim=new MatchSimulation(NewWorld(),1);Assert.AreEqual(MatchSimulation.MaxStepsPerFrame,sim.AdvanceFrame(30));Assert.IsTrue(sim.CatchingUp);
            Assert.IsFalse(sim.World.Events.Any(e=>e.player==0&&(e.kind=="Payment"||e.kind=="Command")),"A human-controlled slot must never receive an AI controller");
        }
    }
}
