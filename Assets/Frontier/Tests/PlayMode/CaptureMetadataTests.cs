using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class CaptureMetadataTests : NativeInputFixture
    {
        private EntityState Town()=>game.World.Entities.First(e=>e.owner==0&&e.def.id=="town");
        private CaptureMetadata Read()=>CaptureMetadata.Read(game);
        private IEnumerator TrainSettler()
        {
            game.World.Players[0].resources=new Cost(1000,1000,1000);var town=Town();
            Assert.IsTrue(game.World.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{town.id},item="villager"}).accepted);yield return Ready();
        }

        [UnityTest] public IEnumerator SnapshotContainsPlayerTrainingPopulationAndHudFrame()
        {
            yield return TrainSettler();var factory=game.World.Spawn("factory",0,new Vector2(42,22));factory.factoryMode="artillery";factory.factoryElapsed=42;
            var m=JsonUtility.FromJson<CaptureMetadata>(JsonUtility.ToJson(Read()));
            Assert.IsTrue(m.hasPlayer);Assert.AreEqual(1,m.population.reserved);Assert.IsTrue(m.production.Any(p=>p.entity==Town().id&&p.queue.Any(j=>j.item=="villager")));
            var capturedFactory=m.production.Single(p=>p.entity==factory.id);Assert.AreEqual("artillery",capturedFactory.factoryMode);Assert.AreEqual(42,capturedFactory.factoryElapsed);
            factory.factoryElapsed=99;Assert.AreEqual(42,capturedFactory.factoryElapsed,"The industrial screenshot record must remain detached from later progress");
            Assert.AreEqual(game.Hud.ResourceSummary,m.hudResources);Assert.AreEqual(900,m.resources.food);StringAssert.Contains("POP  "+m.population.used+"+1/"+m.population.capacity,m.hudResources);Assert.GreaterOrEqual(m.hudUpdatedFrame,0);Assert.AreEqual(m.hudMatchId,m.matchId);Assert.GreaterOrEqual(m.captureFrame,m.hudUpdatedFrame);
        }

        [UnityTest] public IEnumerator CameraSnapshotRecordsViewAndPointerWithoutRevealingMap()
        {
            var destination=new Vector2(88,80);Assert.AreEqual(VisibilityState.Unexplored,game.World.Fog.At(0,destination));
            game.Controls.Focus(destination);yield return Ready();
            var m=JsonUtility.FromJson<CaptureMetadata>(JsonUtility.ToJson(Read()));
            Assert.AreEqual(destination,m.camera.focus);Assert.AreEqual(game.GameCamera.orthographicSize,m.camera.orthographicSize);
            Assert.AreEqual(mouse.position.ReadValue(),m.camera.pointer);Assert.AreEqual(Application.isFocused,m.camera.applicationFocused);
            Assert.AreEqual(VisibilityState.Unexplored,game.World.Fog.At(0,destination));
            game.Controls.Focus(new Vector2(24,28));Assert.AreEqual(destination,m.camera.focus);
        }

        [UnityTest] public IEnumerator SnapshotIsDetachedFromLaterWorldMutationAndJsonIsStable()
        {
            yield return TrainSettler();var before=Read();var json=JsonUtility.ToJson(before);Town().queue[0].elapsed=17;game.World.Players[0].resources.food=1;var after=Read();
            Assert.AreEqual(json,JsonUtility.ToJson(before));Assert.AreNotEqual(json,JsonUtility.ToJson(after));Assert.AreEqual(0,before.production.Single(p=>p.entity==Town().id).queue[0].elapsed,.001f);Assert.AreNotEqual(before.resources.food,after.resources.food);
        }

        [UnityTest] public IEnumerator SnapshotExcludesEnemyTownProduction()
        {
            var enemy=game.World.Entities.First(e=>e.owner==1&&e.def.id=="town");game.World.Players[1].resources=new Cost(1000,1000,1000);Assert.IsTrue(game.World.Issue(new GameCommand{player=1,kind=CommandKind.Train,actors=new[]{enemy.id},item="villager"}).accepted);var m=Read();yield return null;
            Assert.IsFalse(m.production.Any(p=>p.entity==enemy.id));Assert.IsFalse(m.production.SelectMany(p=>p.queue).Any(j=>j.item=="villager"));
        }

        [UnityTest] public IEnumerator MatchIdChangesOnRestartAndClearsOnMenu()
        {
            var first=game.MatchId;Assert.IsNotEmpty(first);game.StartMatch();yield return Ready();var second=game.MatchId;Assert.IsNotEmpty(second);Assert.AreNotEqual(first,second);game.ReturnToMenu();yield return Ready();var m=Read();Assert.IsEmpty(game.MatchId);Assert.IsFalse(m.hasPlayer);Assert.IsEmpty(m.matchId);
        }

        [UnityTest] public IEnumerator DisabledHudLeavesSnapshotHudTextAndSampleTimeStale()
        {
            yield return Ready();var before=Read();var text=before.hudResources;var frame=before.hudUpdatedFrame;var time=before.hudWorldTime;game.Hud.enabled=false;game.World.Players[0].resources.food+=77;game.World.Time+=3;game.World.Players[0].reservedPopulation+=2;yield return null;var after=Read();
            Assert.AreEqual(text,after.hudResources);Assert.AreEqual(frame,after.hudUpdatedFrame);Assert.AreEqual(time,after.hudWorldTime,.001f);Assert.AreNotEqual(before.resources.food,after.resources.food);Assert.AreEqual(before.population.reserved+2,after.population.reserved);
        }

        [UnityTest] public IEnumerator BatchCaptureRequestReportsUnavailableWithoutBusySuccess()
        {
            if(!Application.isBatchMode){Assert.Ignore("Capture unavailable branch is only meaningful in batch mode");yield break;}
            LogAssert.Expect(LogType.Warning,"FRONTIER_CAPTURE_UNAVAILABLE no interactive rendered frame");Assert.IsFalse(game.Captures.Request());Assert.IsFalse(game.Captures.Busy);yield return null;Assert.IsFalse(game.Captures.Busy);
        }
    }
}
