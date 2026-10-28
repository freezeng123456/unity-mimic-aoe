using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    // Real controller -> compressed disk slot -> fresh controller. This check
    // covers checkpoint integration, not winning or replaying four encounters.
    public sealed class ClassicTactics084DiskSaveTests
    {
        readonly List<GameObject> controllers=new List<GameObject>();
        string directory;
        static readonly string[] Encounters={ClassicCivilizationTactics084.British,ClassicCivilizationTactics084.Dutch,
            ClassicCivilizationTactics084.French,ClassicCivilizationTactics084.Ottoman};
        const BindingFlags PrivateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
        [UnitySetUp] public IEnumerator OwnedStorage()
        {
            directory=Path.Combine(Path.GetTempPath(),"Frontier-tactics-disk084-"+Guid.NewGuid().ToString("N"));
            Assert.IsFalse(Directory.Exists(directory));Directory.CreateDirectory(directory);yield return null;
        }
        [UnityTearDown] public IEnumerator DestroyControllersBeforeRemovingOwnedStorage()
        {
            foreach(var root in controllers)if(root!=null)
            {var controller=root.GetComponent<GameController>();if(controller!=null)controller.enabled=false;UnityEngine.Object.Destroy(root);}
            // OnDestroy finishes any controller-owned replay writes before the
            // fixture removes its private save/report/replay directory.
            yield return null;controllers.Clear();
            if(TestContext.CurrentContext.Result.Outcome.Status==NUnit.Framework.Interfaces.TestStatus.Failed)
                TestContext.Error.WriteLine("Preserved actual disk-slot fixture: "+directory);
            else if(directory!=null&&Directory.Exists(directory))Directory.Delete(directory,true);
        }
        static void PrivatePath(GameController game,string field,string path)
        {
            var property=typeof(GameController).GetField(field,PrivateInstance);Assert.IsNotNull(property,field+" isolates only this fixture's storage");
            property.SetValue(game,path);
        }
        GameController Controller(string id,string role)
        {
            var root=new GameObject("Tactics disk slot / "+id+" / "+role);controllers.Add(root);
            var game=root.AddComponent<GameController>();game.enabled=false;game.Controls.enabled=false;
            // Editor initialization loads memory-only defaults before this
            // fixture installs its explicit private durable output paths.
            Assert.IsFalse(game.Settings.Persistent,"Do not read or write the player's durable preferences");
            Assert.IsTrue(game.Progress.MemoryOnly,"Do not read or write the player's completion records");
            string owned=Path.Combine(directory,id);PrivatePath(game,"dataDirectoryOverride",Path.Combine(owned,role,"Data"));
            PrivatePath(game,"saveDirectoryOverride",Path.Combine(owned,"Saves"));
            PrivatePath(game,"reportDirectoryOverride",Path.Combine(owned,role,"Reports"));
            PrivatePath(game,"replayDirectoryOverride",Path.Combine(owned,role,"Replays"));
            Assert.IsTrue(Path.GetFullPath(game.SaveDirectory).StartsWith(Path.GetFullPath(directory)+Path.DirectorySeparatorChar,StringComparison.Ordinal));
            return game;
        }
        static void SameCheckpoint(GameController loaded,SavedMatch disk,string worldJson,string simulationJson,string guidedJson,byte[] bytes,string path)
        {
            Assert.IsFalse(loaded.InMenu);Assert.IsTrue(loaded.IsPaused);Assert.IsTrue(loaded.World.Paused);
            Assert.IsNotNull(loaded.Guided);Assert.AreEqual(disk.matchId,loaded.MatchId);Assert.AreEqual(disk.guided.attemptId,loaded.Guided.AttemptId);
            Assert.AreEqual(disk.guided.definitionId,loaded.Guided.Definition.Id);Assert.AreEqual(GuidedPlayStatus.Running,loaded.Guided.Status);
            Assert.AreEqual(disk.guided.stageIndex,loaded.Guided.CaptureState().stageIndex);Assert.IsTrue(loaded.Guided.Checklist[0].Complete);
            Assert.AreEqual(worldJson,JsonUtility.ToJson(loaded.World.CaptureState()),"No unit, event, order, binding or pending wave can be regenerated or advanced by load");
            Assert.AreEqual(simulationJson,JsonUtility.ToJson(loaded.Simulation.CaptureState()),"Keep fixed-step and no-AI state");
            Assert.AreEqual(guidedJson,JsonUtility.ToJson(loaded.Guided.CaptureState()),"Keep real deployment, accepted commands and exact attempt witnesses");
            CollectionAssert.AreEqual(disk.world.entities.Select(e=>e.state.id),loaded.World.Entities.Select(e=>e.id));
            CollectionAssert.AreEqual(disk.guided.soldiers,loaded.Guided.CaptureState().soldiers);
            Assert.AreEqual(disk.world.events.Length,loaded.World.Events.Count);
            foreach(string kind in new[]{"Spawn","GuidedStarted","GuidedScenarioSetup","ScenarioTriggerFired"})
                Assert.AreEqual(disk.world.events.Count(e=>e.kind==kind),loaded.World.Events.Count(e=>e.kind==kind),kind+" must not repeat");
            Assert.AreEqual(4,loaded.World.ScenarioEntities(ClassicCivilizationTactics084.Wave1Group).Length);
            Assert.AreEqual(ClassicCivilizationTactics084.Wave2Count(disk.guided.definitionId),loaded.World.ScenarioEntities(ClassicCivilizationTactics084.Wave2Group).Length);
            Assert.IsTrue(loaded.World.ScenarioState.triggerStates.All(t=>t.fires==1&&t.lastError==""),"Both already-produced waves retain one successful trigger execution");
            Assert.IsEmpty(loaded.Simulation.CaptureState().controllers);Assert.IsFalse(loaded.Autoplay);
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path),"Loading cannot rewrite the disk slot");
        }
        [UnityTest] public IEnumerator FourTacticalDiskSlotsSurviveFreshControllerLoadPauseAndRetryWithoutRespawning()
        {
            foreach(string id in Encounters)
            {
                var source=Controller(id,"source");Assert.IsTrue(source.InMenu);Assert.IsNull(source.World);
                Assert.IsTrue(source.StartGuided(id),source.Notice);
                var army=source.Guided.CaptureState().soldiers;var origins=army.ToDictionary(actor=>actor,actor=>source.World.Get(actor).position);
                var command=new GameCommand{player=0,kind=CommandKind.Order,actors=army,order=OrderKind.Move,point=ClassicCivilizationTactics084.DeploymentPoint(id)};
                var accepted=source.Command(command);Assert.IsTrue(accepted.accepted,accepted.reason);
                for(int i=0;i<40;i++)Assert.IsTrue(source.Simulation.Step(),source.Guided.Reason);
                Assert.AreEqual(GuidedPlayStatus.Running,source.Guided.Status);Assert.AreEqual("trait",source.Guided.Stage.Id);
                Assert.IsTrue(source.Guided.Checklist[0].Complete,"The deployed stage must come from actual public movement and fixed steps");
                Assert.GreaterOrEqual(army.Count(actor=>Vector2.Distance(origins[actor],source.World.Get(actor).position)>=3),source.Guided.Definition.Stages[0].Required);
                Assert.IsTrue(source.Guided.CaptureState().commands.Any(p=>p.command.kind==CommandKind.Order&&p.command.order==OrderKind.Move&&p.command.actors.SequenceEqual(army)));
                // Withdraw through another public order before the first
                // attack-move reaches the field. This checks already-produced
                // wave bindings on disk without claiming encounter completion.
                var rear=source.World.Navigation.NearestWalkable(command.point+new Vector2(0,-30),army.Max(actor=>source.World.Get(actor).def.radius));
                Assert.GreaterOrEqual(Vector2.Distance(rear,command.point),28,"The quiet checkpoint must use genuine distant dry land");
                var withdraw=source.Command(new GameCommand{player=0,kind=CommandKind.Order,actors=army,order=OrderKind.Move,point=rear});Assert.IsTrue(withdraw.accepted,withdraw.reason);
                for(int i=0;i<160&&army.Any(actor=>Vector2.Distance(source.World.Get(actor).position,rear)>5);i++)
                    Assert.IsTrue(source.Simulation.Step(),source.Guided.Reason);
                Assert.IsTrue(army.All(actor=>source.World.Get(actor).Alive&&Vector2.Distance(source.World.Get(actor).position,rear)<=5),"All original soldiers must really withdraw");
                var hold=source.Command(new GameCommand{player=0,kind=CommandKind.Order,actors=army,order=OrderKind.Hold,point=rear});Assert.IsTrue(hold.accepted,hold.reason);
                for(int i=0;i<500&&source.World.Time<41;i++)Assert.IsTrue(source.Simulation.Step(),source.Guided.Reason);
                Assert.GreaterOrEqual(source.World.Time,41);Assert.AreEqual(GuidedPlayStatus.Running,source.Guided.Status);
                Assert.AreEqual("trait",source.Guided.Stage.Id);Assert.AreEqual(ClassicCivilizationTactics084.Wave2Count(id),source.World.ScenarioEntities(ClassicCivilizationTactics084.Wave2Group).Length);
                Assert.IsTrue(source.World.ScenarioState.triggerStates.All(t=>t.fires==1&&t.lastError==""));
                Assert.IsTrue(source.World.ScenarioEntities(ClassicCivilizationTactics084.Wave1Group).All(actor=>Vector2.Distance(source.World.Get(actor).origin,source.World.Get(actor).position)>10),"The first wave must actually execute its ordinary movement");
                // Save the normal pause-menu state through public APIs; no test
                // assigns a World flag or manufactures a serialized marker.
                Assert.IsFalse(source.IsPaused);source.TogglePause();Assert.IsTrue(source.IsPaused);
                string originalWorld=JsonUtility.ToJson(source.World.CaptureState());
                string originalSimulation=JsonUtility.ToJson(source.Simulation.CaptureState());
                string originalGuided=JsonUtility.ToJson(source.Guided.CaptureState());
                string attempt=source.Guided.AttemptId;var oldWorld=source.World;var oldSimulation=source.Simulation;var oldGuided=source.Guided;
                Assert.IsTrue(source.SaveMatch(1),source.Notice);
                string path=MatchSaveStore.SlotPath(source.SaveDirectory,1);Assert.IsTrue(File.Exists(path));
                byte[] bytes=File.ReadAllBytes(path);Assert.Greater(bytes.Length,100);
                var envelope=JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(path));Assert.IsNotNull(envelope);
                Assert.AreEqual(2,envelope.schema);Assert.AreEqual("gzip-base64",envelope.encoding);Assert.AreEqual(64,envelope.sha256.Length);
                Assert.IsNotEmpty(envelope.payloadBase64);var gzip=Convert.FromBase64String(envelope.payloadBase64);Assert.AreEqual(0x1f,gzip[0]);Assert.AreEqual(0x8b,gzip[1]);
                var disk=MatchSaveStore.Read(path,Application.version);Assert.IsTrue(disk.hasGuided);Assert.IsNotNull(disk.guided);
                Assert.AreEqual(id,disk.guided.definitionId);Assert.AreEqual(attempt,disk.matchId);Assert.AreEqual(attempt,disk.guided.attemptId);
                Assert.IsTrue(disk.world.paused);Assert.AreEqual(originalWorld,JsonUtility.ToJson(disk.world));
                Assert.AreEqual(originalSimulation,JsonUtility.ToJson(disk.simulation));Assert.AreEqual(originalGuided,JsonUtility.ToJson(disk.guided));
                Assert.IsEmpty(Directory.GetFiles(source.SaveDirectory,"*.tmp-*"));
                UnityEngine.Object.Destroy(source.gameObject);yield return null;
                Assert.IsTrue(source==null,"The source controller must actually be destroyed before cold load");
                var loaded=Controller(id,"loaded");Assert.IsTrue(loaded.InMenu);Assert.IsNull(loaded.World);Assert.IsNull(loaded.Guided);
                Assert.IsTrue(loaded.LoadMatch(1),loaded.Notice);
                Assert.AreNotSame(oldWorld,loaded.World);Assert.AreNotSame(oldSimulation,loaded.Simulation);Assert.AreNotSame(oldGuided,loaded.Guided);
                SameCheckpoint(loaded,disk,originalWorld,originalSimulation,originalGuided,bytes,path);
                yield return null;yield return null;
                SameCheckpoint(loaded,disk,originalWorld,originalSimulation,originalGuided,bytes,path);
                // A second real slot load must also adopt the same world, not
                // replay setup or produce a second copy of an enemy wave.
                var firstLoadedWorld=loaded.World;Assert.IsTrue(loaded.LoadMatch(1),loaded.Notice);Assert.AreNotSame(firstLoadedWorld,loaded.World);
                SameCheckpoint(loaded,disk,originalWorld,originalSimulation,originalGuided,bytes,path);
                loaded.TogglePause();Assert.IsFalse(loaded.IsPaused);Assert.IsTrue(loaded.Simulation.Step());
                Assert.Greater(loaded.World.Time,disk.world.time,"A loaded pause can resume the normal scheduler");
                var resign=loaded.Command(new GameCommand{player=0,kind=CommandKind.Resign});Assert.IsTrue(resign.accepted,resign.reason);
                Assert.AreEqual(GuidedPlayStatus.Failed,loaded.Guided.Status);loaded.RetryCurrentMatch();
                Assert.AreEqual(id,loaded.Guided.Definition.Id);Assert.AreNotEqual(attempt,loaded.Guided.AttemptId);
                Assert.AreEqual(GuidedPlayStatus.Running,loaded.Guided.Status);Assert.AreEqual(0,loaded.World.Time);
                Assert.AreEqual("deploy",loaded.Guided.Stage.Id);Assert.IsEmpty(loaded.Guided.CaptureState().commands);
                Assert.AreEqual(ClassicCivilizationTactics084.ArmyCount(id),loaded.Guided.CaptureState().soldiers.Length);
                Assert.AreEqual(4,loaded.World.ScenarioEntities(ClassicCivilizationTactics084.Wave1Group).Length);
                Assert.IsEmpty(loaded.World.ScenarioEntities(ClassicCivilizationTactics084.Wave2Group));
                Assert.IsTrue(loaded.World.ScenarioState.triggerStates.All(t=>t.fires==0));
                CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path),"Retry creates a fresh attempt without overwriting the prior disk checkpoint");
                TestContext.WriteLine("Real compressed disk slot -> destroyed source -> fresh controller -> identical paused state -> resume/resign/retry: "+id+" attempt="+attempt+" file="+path);
                UnityEngine.Object.Destroy(loaded.gameObject);yield return null;
            }
        }
    }
}
