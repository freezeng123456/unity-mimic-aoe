using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Frontier.Tests
{
    public sealed class SimulationDispatchTests
    {
        [UnityTest] public IEnumerator PausedBenchmarkDoesNotSampleOrFinishAndResumesItsActiveClock()
        {
            var root=new GameObject("Benchmark pause measurement test");
            try
            {
                var game=root.AddComponent<GameController>();yield return null;game.enabled=false;
                game.BenchmarkMode=true;game.SelectedMap=MapTopology.Winding;game.StartMatch();
                var fixture=root.AddComponent<RuntimeBenchmark>();fixture.Initialize(game,10);fixture.enabled=false;
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                void Set(string name,object value)=>typeof(RuntimeBenchmark).GetField(name,flags).SetValue(fixture,value);
                T Get<T>(string name)=>(T)typeof(RuntimeBenchmark).GetField(name,flags).GetValue(fixture);
                void Update(){typeof(RuntimeBenchmark).GetMethod("Update",flags).Invoke(fixture,null);typeof(RuntimeBenchmark).GetMethod("LateUpdate",flags).Invoke(fixture,null);}
                Set("elapsedActive",24.9f);Set("lastFrameAt",Time.unscaledTime-1);
                game.TogglePause();Update();
                Assert.AreEqual(24.9f,Get<float>("elapsedActive"));Assert.IsFalse(Get<bool>("saved"),"Paused time must not complete the run");
                Assert.AreEqual(0,Get<int>("samples"));Assert.AreEqual(1,Get<int>("pausedFrames"));
                Assert.IsFalse(game.World.Entities.Any(e=>e.Alive&&e.def.kind==EntityKind.Unit&&e.orders.Count>0),"Paused benchmark must not issue new movement orders");
                game.TogglePause();Set("lastFrameAt",Time.unscaledTime-.03f);Update();
                Assert.That(Get<float>("elapsedActive"),Is.InRange(24.92f,24.95f));Assert.AreEqual(1,Get<int>("samples"));
                Assert.IsFalse(Get<bool>("saved"));Assert.Greater(Get<float>("pausedSeconds"),.99f);
                Assert.IsTrue(game.World.Entities.Any(e=>e.Alive&&e.def.kind==EntityKind.Unit&&e.orders.Count>0));
            }
            finally{Object.Destroy(root);}
            yield return null;
        }
        [UnityTest] public IEnumerator BenchmarkReturnToMenuRestoresTheOpponentForNormalPlay()
        {
            var root=new GameObject("Benchmark session lifecycle test");
            try
            {
                var game=root.AddComponent<GameController>();yield return null;game.enabled=false;
                game.BenchmarkMode=true;game.StartMatch();game.Simulation.Step();
                Assert.IsFalse(game.World.Events.Any(e=>e.kind=="Payment"),"The benchmark fixture intentionally disables AI");
                var fixture=root.AddComponent<RuntimeBenchmark>();fixture.Initialize(game,10);
                game.ReturnToMenu();yield return null;Assert.IsTrue(fixture==null,"The benchmark must not issue orders or sample a later match");
                game.StartMatch();game.Simulation.Step();
                Assert.IsTrue(game.World.Events.Any(e=>e.kind=="Payment"&&e.player==1),"The normal opponent must resume economic decisions after leaving a benchmark");
                Assert.IsFalse(game.World.Events.Any(e=>e.kind=="Payment"&&e.player==0),"Restart must leave the human player under player control");
                game.BenchmarkMode=true;game.StartMatch();fixture=root.AddComponent<RuntimeBenchmark>();fixture.Initialize(game,10);
                game.StartMatch();game.Simulation.Step();yield return null;
                Assert.IsTrue(fixture==null);Assert.IsTrue(game.World.Events.Any(e=>e.kind=="Payment"&&e.player==1),"Direct menu restart must also restore the opponent");
            }
            finally{Object.Destroy(root);}
            yield return null;
        }
        [UnityTest] public IEnumerator NativeControllerUsesTheSameSessionAsDirectSimulationAndResetsIt()
        {
            var root=new GameObject("Native scheduler boundary test");
            try
            {
                var game=root.AddComponent<GameController>();yield return null;game.Autoplay=true;game.PlayerCivilization="coast";game.OpponentCivilization="river";game.AgeLimit=5;game.StartMatch();
                // Disable the MonoBehaviour update so this test controls only frame delivery.
                game.enabled=false;var session=game.Simulation;Assert.AreSame(game.World,session.World);
                var reference=new World(game.Rules,game.MatchSeed,game.PlayerCivilization,game.OpponentCivilization,game.AgeLimit);
                for(int player=0;player<2;player++){reference.Players[player].deck.Clear();reference.Players[player].deck.AddRange(game.World.Players[player].deck);}
                var direct=new MatchSimulation(reference,0,1);
                for(int i=0;i<120;i++)session.AdvanceFrame(.25f);for(int i=0;i<session.StepCount;i++)direct.Step();
                CollectionAssert.AreEqual(reference.Events.Select(JsonUtility.ToJson).ToArray(),game.World.Events.Select(JsonUtility.ToJson).ToArray());
                game.TogglePause();int count=session.StepCount;session.AdvanceFrame(5);Assert.AreEqual(count,session.StepCount);
                game.ReturnToMenu();Assert.IsNull(game.Simulation);game.StartMatch();Assert.AreNotSame(session,game.Simulation);Assert.AreEqual(0,game.Simulation.StepCount);
            }
            finally{Object.Destroy(root);}
            yield return null;
        }
    }
}
