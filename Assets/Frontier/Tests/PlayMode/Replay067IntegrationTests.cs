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
    // Exercise the real controller routes and Update loop. The fixture owns
    // every durable path, including account, preferences, reports and replays.
    public sealed class Replay067IntegrationTests
    {
        const BindingFlags PrivateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
        GameObject root;GameController game;string directory;
        [UnitySetUp] public IEnumerator SetUp()
        {
            directory=Path.Combine(Path.GetTempPath(),"Frontier-replay-integration-"+Guid.NewGuid().ToString("N"));
            Assert.IsFalse(Directory.Exists(directory));Directory.CreateDirectory(directory);
            root=new GameObject("Isolated replay integration");game=root.AddComponent<GameController>();yield return null;game.enabled=false;
            Assert.IsFalse(game.Settings.Persistent,"Editor initialization must not load personal preferences");
            Assert.IsTrue(game.Progress.MemoryOnly,"Editor initialization must not load personal account progress");
            SetField("saveDirectoryOverride",Path.Combine(directory,"Saves"));
            SetField("reportDirectoryOverride",Path.Combine(directory,"Reports"));
            SetField("replayDirectoryOverride",Path.Combine(directory,"Replays"));
            SetProperty(game,"Progress",AccountProgressStore.Load(Path.Combine(directory,"Account")));
            SetProperty(game.Settings,"Path",Path.Combine(directory,"Preferences","player-settings.json"));
            var preferences=game.Settings.Current;preferences.musicEnabled=false;
            Assert.IsTrue(game.Settings.TryUpdate(preferences,out string reason),reason);
            Assert.IsTrue(game.Settings.Persistent);Assert.IsTrue(File.Exists(game.Settings.Path));Assert.IsFalse(game.Progress.MemoryOnly);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if(game!=null){game.enabled=false;DrainWrites();}
            if(root!=null)UnityEngine.Object.Destroy(root);yield return null;
            if(directory!=null&&Directory.Exists(directory))Directory.Delete(directory,true);
        }
        void SetField(string name,object value)
        {var field=typeof(GameController).GetField(name,PrivateInstance);Assert.IsNotNull(field,name);field.SetValue(game,value);}
        static void SetProperty(object target,string name,object value)
        {var property=target.GetType().GetProperty(name);Assert.IsNotNull(property,name);var setter=property.GetSetMethod(true);Assert.IsNotNull(setter,name);setter.Invoke(target,new[]{value});}
        void Invoke(string method)
        {var info=typeof(GameController).GetMethod(method,PrivateInstance);Assert.IsNotNull(info,method);info.Invoke(game,null);}
        void DrainWrites(){Invoke("FinishReplayFileWrites");Invoke("PollReplayOperations");Assert.IsFalse(game.ReplaySaving);}
        Dictionary<string,byte[]> OwnedFiles()=>Directory.GetFiles(directory,"*",SearchOption.AllDirectories)
            .ToDictionary(path=>path.Substring(directory.Length+1),File.ReadAllBytes);
        void Unchanged(Dictionary<string,byte[]> before)
        {
            var after=OwnedFiles();CollectionAssert.AreEquivalent(before.Keys,after.Keys,"Viewing must not create or delete owned durable files");
            foreach(var file in before)CollectionAssert.AreEqual(file.Value,after[file.Key],"Viewing changed "+file.Key);
        }
        static void Steps(MatchSimulation simulation,int count)
        {for(int i=0;i<count;i++)Assert.IsTrue(simulation.Step(),"Actual simulation stopped at "+simulation.StepCount);}
        sealed class Expected
        {
            readonly string world,simulation,guided,digest;readonly int step;
            public Expected(MatchSimulation scheduler,GuidedPlaySession lesson=null)
            {
                step=scheduler.StepCount;world=JsonUtility.ToJson(scheduler.World.CaptureState());
                var state=scheduler.CaptureState();state.accumulator=0;simulation=JsonUtility.ToJson(state);
                guided=lesson==null?null:JsonUtility.ToJson(lesson.CaptureState());digest=ReplayDigest.Compute(scheduler.World,scheduler,lesson);
            }
            public void Check(GameController controller)
            {
                Assert.IsTrue(controller.IsWatching);Assert.IsEmpty(controller.Playback.Error);Assert.AreEqual(step,controller.Simulation.StepCount);
                Assert.AreEqual(world,JsonUtility.ToJson(controller.World.CaptureState()),"Controller must expose the reconstructed world, including events and economy");
                var state=controller.Simulation.CaptureState();state.accumulator=0;Assert.AreEqual(simulation,JsonUtility.ToJson(state),"AI state differs");
                Assert.AreEqual(guided,controller.Guided==null?null:JsonUtility.ToJson(controller.Guided.CaptureState()));
                Assert.AreEqual(digest,ReplayDigest.Compute(controller.World,controller.Simulation,controller.Guided));
                Assert.AreSame(controller.Playback.World,controller.World);Assert.AreSame(controller.Playback.Simulation,controller.Simulation);
            }
        }
        IEnumerator Seek(int step)
        {
            game.BeginReplaySeek(step);Assert.IsTrue(game.ReplayPaused);game.enabled=true;int frames=0;
            while(game.Playback.IsSeeking&&frames++<100)yield return null;
            game.enabled=false;Assert.IsFalse(game.Playback.IsSeeking,"Controller's incremental seek did not finish");
            Assert.IsEmpty(game.Playback.Error);Assert.AreEqual(step,game.Playback.CurrentStep);
        }
        static void CompleteSelection(GuidedPlaySession lesson,ReplayRecorder recorder=null)
        {
            int[] scout=lesson.SuggestedActors.ToArray();
            if(recorder==null)lesson.ObserveSelection(scout);else recorder.ObserveSelection(scout);
            var command=new GameCommand{player=0,kind=CommandKind.Order,actors=scout,order=OrderKind.Move,point=lesson.ObjectivePoint};
            var result=recorder==null?lesson.Issue(command):recorder.Issue(command);Assert.IsTrue(result.accepted,result.reason);
            int guard=0;while(lesson.Status==GuidedPlayStatus.Running&&guard++<1000)Steps(lesson.Simulation,1);
            Assert.AreEqual(GuidedPlayStatus.Completed,lesson.Status,lesson.Reason);Assert.IsTrue(lesson.World.Paused);
        }

        [UnityTest] public IEnumerator CompletedLessonPlaybackCannotWriteAccountSlotsPreferencesOrReportsOrIssueOrders()
        {
            // A genuine prior completion provides a byte-level account sentinel.
            // Replaying a different successful attempt would increment it if the
            // live controller's completion-writing branch were reached.
            using(var prior=GuidedPlaySession.Start(game.Rules,"lesson_selection","account-sentinel"))
            {CompleteSelection(prior);Assert.IsTrue(game.Progress.RecordCompletion(prior),game.Progress.LastError);}
            Assert.AreEqual(1,game.Progress.CompletionCount("lesson_selection"));Assert.IsTrue(File.Exists(game.Progress.FilePath));
            game.StartMatch();Assert.IsTrue(game.SaveMatch(1));Assert.IsTrue(game.SaveMatch(2));
            string path=ReplayStore.PathFor(game.ReplayDirectory,"completed-guided");Expected expected;
            using(var lesson=GuidedPlaySession.Start(game.Rules,"lesson_selection","watched-new-attempt"))
            using(var recorder=new ReplayRecorder(lesson.Simulation,game.CurrentReplayIdentity(),lesson,"completed-guided",10))
            {CompleteSelection(lesson,recorder);expected=new Expected(lesson.Simulation,lesson);ReplayStore.Write(path,recorder.Finish("guided_completed"),game.Rules,game.CurrentReplayIdentity());}
            Assert.IsTrue(game.LoadReplayNow(path),game.ReplayFileError);DrainWrites();Assert.IsNull(game.Recorder);
            var files=OwnedFiles();game.SetReplaySpeed(4);game.ToggleReplayPlayback();Assert.IsFalse(game.ReplayPaused);game.enabled=true;
            float deadline=Time.realtimeSinceStartup+20;
            while(!game.Playback.Verified&&game.Playback.Error==""&&Time.realtimeSinceStartup<deadline)yield return null;
            game.enabled=false;Assert.IsTrue(game.Playback.Verified,game.Playback.Error);Assert.IsTrue(game.ReplayPaused);expected.Check(game);
            Assert.AreEqual(GuidedPlayStatus.Completed,game.Guided.Status);Assert.AreEqual("watched-new-attempt",game.Guided.AttemptId);
            string world=JsonUtility.ToJson(game.World.CaptureState());string simulation=JsonUtility.ToJson(game.Simulation.CaptureState());
            int town=game.World.Entities.First(e=>e.Alive&&e.owner==0&&e.def.id=="town").id;
            var rejected=game.Command(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{town},item=game.World.WorkerDefinition(0).id,count=1});
            Assert.IsFalse(rejected.accepted);StringAssert.Contains("Viewing a recording",rejected.reason);
            Assert.IsFalse(game.SaveMatch(1));Assert.IsFalse(game.SaveMatch(3));Assert.IsFalse(game.SaveLearningProgress());Assert.IsFalse(game.SaveReplayNow());
            game.Resign();game.SaveReport("quit");
            Assert.AreEqual(world,JsonUtility.ToJson(game.World.CaptureState()));Assert.AreEqual(simulation,JsonUtility.ToJson(game.Simulation.CaptureState()));
            Assert.IsFalse(game.World.Players[0].resigned);Assert.AreEqual(1,game.Progress.CompletionCount("lesson_selection"));
            Assert.AreEqual(1,AccountProgressStore.Load(Path.Combine(directory,"Account")).CompletionCount("lesson_selection"));
            Assert.IsFalse(File.Exists(MatchSaveStore.SlotPath(game.SaveDirectory,3)));Unchanged(files);
        }

        [UnityTest] public IEnumerator SavedFourSlotRecordingColdOpensAndControllerSeekAndObserverChangesPreserveExactSimulation()
        {
            game.SkirmishMode="teams";game.SelectedMap="generated_river";game.MatchSeed=314159;game.OpponentDifficulty="easy";game.StartMatch();
            Assert.AreEqual(4,game.World.Players.Length);int town=game.World.Entities.First(e=>e.Alive&&e.owner==0&&e.def.id=="town").id;
            var paid=game.Command(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{town},item=game.World.WorkerDefinition(0).id,count=1});Assert.IsTrue(paid.accepted,paid.reason);
            var expected=new Dictionary<int,Expected>();
            for(int step=1;step<=160;step++){Steps(game.Simulation,1);if(step==30||step==80||step==160)expected.Add(step,new Expected(game.Simulation));}
            Assert.Greater(game.World.Players[0].spent.food,0);foreach(int player in new[]{1,2,3})Assert.IsTrue(game.World.Events.Any(e=>e.kind=="Payment"&&e.player==player));
            Assert.IsTrue(game.SaveReplayNow(),game.ReplayFileError);Assert.IsTrue(File.Exists(game.LastReplayFile));
            // Preserve the manual export. Closing the live segment is allowed
            // to extend its original recording with the final report flush.
            string frozen=Path.Combine(game.ReplayDirectory,"cold-manual.replay.json");File.Copy(game.LastReplayFile,frozen);byte[] exported=File.ReadAllBytes(frozen);
            var previousWorld=game.World;var previousSimulation=game.Simulation;
            Assert.IsTrue(game.LoadReplayNow(frozen),game.ReplayFileError);DrainWrites();Assert.AreNotSame(previousWorld,game.World);Assert.AreNotSame(previousSimulation,game.Simulation);
            Assert.IsTrue(game.IsWatching);Assert.IsTrue(game.ReplayPaused);Assert.IsNull(game.Recorder);CollectionAssert.AreEqual(new[]{0,1,0,1},game.World.Players.Select(p=>p.team));
            string before=ReplayDigest.Compute(game.World,game.Simulation);string world=JsonUtility.ToJson(game.World.CaptureState());
            for(int player=0;player<4;player++)
            {
                game.SetObserverPlayer(player);Assert.AreEqual(player,game.ObserverPlayer);Assert.IsEmpty(game.Controls.Selected);
                yield return null; // Let the real HUD and WorldView consume the changed observer.
                Assert.AreEqual(before,ReplayDigest.Compute(game.World,game.Simulation));Assert.AreEqual(world,JsonUtility.ToJson(game.World.CaptureState()));
            }
            foreach(float speed in new[]{.5f,1f,2f,4f}){game.SetReplaySpeed(speed);Assert.AreEqual(speed,game.ReplaySpeed);Assert.AreEqual(before,ReplayDigest.Compute(game.World,game.Simulation));}
            var blocked=game.Command(new GameCommand{player=0,kind=CommandKind.Resign});Assert.IsFalse(blocked.accepted);StringAssert.Contains("Viewing a recording",blocked.reason);
            Assert.AreEqual(before,ReplayDigest.Compute(game.World,game.Simulation));
            foreach(int step in new[]{80,160,30,160}){yield return Seek(step);expected[step].Check(game);Assert.AreEqual(3,game.ObserverPlayer);}
            Assert.IsTrue(game.Playback.Verified);CollectionAssert.AreEqual(exported,File.ReadAllBytes(frozen));
        }

        [UnityTest] public IEnumerator InitialSemanticMismatchIsRejectedBeforeReportingOrReplacingTheRunningMatch()
        {
            game.StartMatch();int town=game.World.Entities.First(e=>e.Alive&&e.owner==0&&e.def.id=="town").id;
            Assert.IsTrue(game.Command(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{town},item=game.World.WorkerDefinition(0).id,count=1}).accepted);
            Steps(game.Simulation,10);var document=game.Recorder.Capture("controlled_initial_mismatch");Assert.AreEqual(0,document.records[0].completedStep);
            document.records[0].accepted=false;
            // Keep all file and advertised-input digests valid, so rejection
            // must come from actually executing the first recorded command.
            string hash=SnapshotCodec.Hash("frontier-replay-inputs-v1");int input=0;
            foreach(var anchor in document.anchors)
            {while(input<anchor.recordIndex)hash=SnapshotCodec.Hash(hash+"\n"+JsonUtility.ToJson(document.records[input++]));anchor.inputHash=hash;}
            string path=Path.Combine(game.ReplayDirectory,"initial-mismatch.replay.json");ReplayStore.Write(path,document,game.Rules,game.CurrentReplayIdentity());
            var world=game.World;var simulation=game.Simulation;var recorder=game.Recorder;string id=game.MatchId;
            string snapshot=JsonUtility.ToJson(world.CaptureState());var files=OwnedFiles();
            Assert.IsFalse(game.LoadReplayNow(path));StringAssert.Contains("command outcome at step 0 ordinal 0",game.ReplayFileError);
            Assert.AreSame(world,game.World);Assert.AreSame(simulation,game.Simulation);Assert.AreSame(recorder,game.Recorder);Assert.AreEqual(id,game.MatchId);
            Assert.IsFalse(game.IsWatching);Assert.IsFalse(game.InMenu);Assert.AreEqual(snapshot,JsonUtility.ToJson(game.World.CaptureState()));
            Assert.IsFalse(game.ReplaySaving);Unchanged(files);yield return null;
        }

        [UnityTest] public IEnumerator ReportIoFailureDoesNotBlockValidLoadOrRecordingAndQuitDrainsTheRecoveredSegment()
        {
            game.StartMatch();int town=game.World.Entities.First(e=>e.Alive&&e.owner==0&&e.def.id=="town").id;
            var train=new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{town},item=game.World.WorkerDefinition(0).id,count=1};
            Assert.IsTrue(game.Command(train).accepted);Steps(game.Simulation,35);Assert.IsTrue(game.SaveMatch(1));
            string slot=MatchSaveStore.SlotPath(game.SaveDirectory,1);byte[] saved=File.ReadAllBytes(slot),preferences=File.ReadAllBytes(game.Settings.Path);
            string blocked=Path.Combine(directory,"ReportPathBlockedByAnOwnedFile");File.WriteAllText(blocked,"owned report blocker");byte[] blocker=File.ReadAllBytes(blocked);
            SetField("reportDirectoryOverride",blocked);var previous=game.World;var previousRecorder=game.Recorder;
            LogAssert.Expect(LogType.Warning,new System.Text.RegularExpressions.Regex("^FRONTIER_REPORT_FAILED "));
            Assert.IsTrue(game.LoadMatch(1),"A diagnostic report failure must not prevent a validated checkpoint from loading");
            Assert.AreNotSame(previous,game.World);Assert.IsNotNull(game.Recorder);Assert.AreNotSame(previousRecorder,game.Recorder);
            Assert.IsTrue(game.Recorder.IsRecording);Assert.AreEqual(35,game.Simulation.StepCount);Assert.IsTrue(game.IsPaused);
            game.TogglePause();Assert.IsFalse(game.IsPaused);float food=game.World.Players[0].resources.food;
            var result=game.Command(train);Assert.IsTrue(result.accepted,result.reason);Assert.Less(game.World.Players[0].resources.food,food,"The resumed production order must actually pay");
            Steps(game.Simulation,250);Assert.IsTrue(game.World.Events.Any(e=>e.kind=="UnitTrained"&&e.player==0));
            Assert.IsTrue(game.Recorder.Capture().records.Any(r=>r.kind==ReplayOperation.Command&&r.accepted&&r.command.kind==CommandKind.Train));
            LogAssert.Expect(LogType.Warning,new System.Text.RegularExpressions.Regex("^FRONTIER_REPORT_FAILED "));
            // Invoke the real quit callback without terminating the test player:
            // its finally block must wait even though Reports cannot be written.
            Invoke("OnApplicationQuit");Invoke("PollReplayOperations");
            Assert.IsNull(game.Recorder);Assert.IsFalse(game.ReplaySaving);Assert.IsEmpty(game.ReplayFileError);Assert.IsTrue(File.Exists(game.LastReplayFile));
            var document=ReplayStore.Read(game.LastReplayFile,game.Rules,game.CurrentReplayIdentity());
            Assert.AreEqual("loaded_checkpoint",document.startKind);Assert.AreEqual("quit",document.finishReason);Assert.AreEqual(35,document.anchors[0].completedStep);
            Assert.AreEqual(285,document.anchors.Last().completedStep);Assert.IsFalse(document.incomplete);
            Assert.AreEqual(1,document.records.Count(r=>r.kind==ReplayOperation.Command&&r.accepted&&r.command.kind==CommandKind.Train));
            string expected=ReplayDigest.Compute(game.World,game.Simulation);string world=JsonUtility.ToJson(game.World.CaptureState());
            using(var replay=new ReplaySession(game.Rules,document,game.CurrentReplayIdentity()))
            {
                while(replay.CurrentStep<replay.EndStep&&replay.Error=="")Assert.Greater(replay.Advance(25),0);
                Assert.IsEmpty(replay.Error);Assert.IsTrue(replay.Verified);Assert.AreEqual(expected,ReplayDigest.Compute(replay.World,replay.Simulation));
                Assert.AreEqual(world,JsonUtility.ToJson(replay.World.CaptureState()));
            }
            CollectionAssert.AreEqual(saved,File.ReadAllBytes(slot));CollectionAssert.AreEqual(preferences,File.ReadAllBytes(game.Settings.Path));
            CollectionAssert.AreEqual(blocker,File.ReadAllBytes(blocked));Assert.IsFalse(File.Exists(game.Progress.FilePath));yield return null;
        }
    }
}
