using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Frontier.Tests
{
    // Real controller lifecycles over an isolated TCP loopback connection.
    // No test here certifies a native two-physical-Mac LAN session.
    public sealed class LanLifecycle068Tests
    {
        const BindingFlags PrivateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
        const int RemotePlayer=2;
        GameObject hostRoot,clientRoot;
        GameController host,client;
        string directory;
        Mouse mouse;Keyboard keyboard;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;

        [UnitySetUp] public IEnumerator SetUp()
        {
            directory=Path.Combine(Path.GetTempPath(),"Frontier-LAN-lifecycle-"+Guid.NewGuid().ToString("N"));
            Assert.IsFalse(Directory.Exists(directory));Directory.CreateDirectory(directory);
            background=InputSystem.settings.backgroundBehavior;editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            host=CreatePeer("host",out hostRoot,false);client=CreatePeer("client",out clientRoot,true);
            mouse=InputSystem.AddDevice<Mouse>();keyboard=InputSystem.AddDevice<Keyboard>();
            yield return Ready();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            foreach(var peer in new[]{client,host})if(peer!=null)
            {peer.enabled=false;peer.LeaveLan();DrainWrites(peer);}
            if(clientRoot!=null)UnityEngine.Object.Destroy(clientRoot);
            if(hostRoot!=null)UnityEngine.Object.Destroy(hostRoot);
            yield return null;
            if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);
            if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
            if(directory!=null&&Directory.Exists(directory))Directory.Delete(directory,true);
        }
        GameController CreatePeer(string name,out GameObject root,bool visible)
        {
            Assert.IsTrue(Application.isEditor||Application.isBatchMode,"Never instantiate a controller where Awake could load personal storage");
            root=new GameObject("Isolated LAN lifecycle "+name);var peer=root.AddComponent<GameController>();peer.enabled=false;
            Assert.IsFalse(peer.Settings.Persistent,"Awake must not load personal preferences");Assert.IsTrue(peer.Progress.MemoryOnly,"Awake must not load personal account progress");
            string owned=Path.Combine(directory,name);
            SetPrivate(peer,"saveDirectoryOverride",Path.Combine(owned,"Saves"));SetPrivate(peer,"reportDirectoryOverride",Path.Combine(owned,"Reports"));SetPrivate(peer,"replayDirectoryOverride",Path.Combine(owned,"Replays"));
            SetProperty(peer,"Progress",AccountProgressStore.Load(Path.Combine(owned,"Account")));
            SetProperty(peer.Settings,"Path",Path.Combine(owned,"Preferences","player-settings.json"));
            var preferences=peer.Settings.Current;preferences.muted=true;preferences.musicEnabled=false;preferences.environmentEnabled=false;
            Assert.IsTrue(peer.Settings.TryUpdate(preferences,out string reason),reason);
            Assert.IsTrue(peer.SaveDirectory.StartsWith(owned+Path.DirectorySeparatorChar,StringComparison.Ordinal));
            Assert.IsTrue(peer.ReplayDirectory.StartsWith(owned+Path.DirectorySeparatorChar,StringComparison.Ordinal));
            Assert.IsTrue(peer.Progress.FilePath.StartsWith(owned+Path.DirectorySeparatorChar,StringComparison.Ordinal));
            foreach(var camera in root.GetComponentsInChildren<Camera>())camera.enabled=false;
            var cameraRoot=new GameObject("Owned lifecycle "+name+" camera");cameraRoot.transform.SetParent(root.transform,false);
            var ownedCamera=cameraRoot.AddComponent<Camera>();ownedCamera.enabled=visible;ownedCamera.orthographic=true;ownedCamera.orthographicSize=26;ownedCamera.nearClipPlane=.2f;ownedCamera.farClipPlane=260;
            SetProperty(peer,"GameCamera",ownedCamera);peer.Controls.RestoreCamera(new Vector2(25,25),26);
            // Lifecycle tests issue no device commands. Both streams stay
            // disabled, while the client HUD may consume authoritative state.
            peer.Controls.enabled=false;peer.View.enabled=false;peer.Ambience.enabled=false;peer.Audio.PauseAll(true);
            if(!visible)foreach(var canvas in root.GetComponentsInChildren<Canvas>())canvas.gameObject.SetActive(false);
            return peer;
        }
        static T Private<T>(object target,string name)
        {var field=target.GetType().GetField(name,PrivateInstance);Assert.IsNotNull(field,name);return (T)field.GetValue(target);}
        static void SetPrivate(object target,string name,object value)
        {var field=target.GetType().GetField(name,PrivateInstance);Assert.IsNotNull(field,name);field.SetValue(target,value);}
        static void SetProperty(object target,string name,object value)
        {var setter=target.GetType().GetProperty(name)?.GetSetMethod(true);Assert.IsNotNull(setter,name);setter.Invoke(target,new[]{value});}
        static void Invoke(object target,string name)
        {var method=target.GetType().GetMethod(name,PrivateInstance);Assert.IsNotNull(method,name);method.Invoke(target,null);}
        static void DrainWrites(GameController peer){Invoke(peer,"FinishReplayFileWrites");Invoke(peer,"PollReplayOperations");Assert.IsFalse(peer.ReplaySaving);}
        IEnumerator Ready(){yield return null;Canvas.ForceUpdateCanvases();yield return null;}
        IEnumerator PumpUntil(Func<bool> condition,string reason,bool pumpHost=true,bool pumpClient=true)
        {
            float deadline=Time.realtimeSinceStartup+12;
            while(!condition()&&Time.realtimeSinceStartup<deadline)
            {if(pumpHost)host.PumpLan();if(pumpClient)client.PumpLan();yield return null;}
            Assert.IsTrue(condition(),reason+"; host: "+host.LanConnectionText+"; client: "+client.LanConnectionText);yield return Ready();
        }
        IEnumerator StartRemoteMatch()
        {
            host.SkirmishMode="teams";host.SelectedMap=MapTopology.Frontier;host.SelectedObjective="elimination";host.OpponentDifficulty="easy";
            client.PlayerCivilization="france";client.DeckPreset="economy";client.ResetDeckPreset();
            host.HostLan(0);Assert.Greater(host.LanPort,0);client.JoinLan("127.0.0.1",host.LanPort);
            yield return PumpUntil(()=>client.IsLanClient&&client.IsLanConnected&&client.LocalPlayerSlot==RemotePlayer&&host.LanLobby?.slots?.Length==4&&!string.IsNullOrEmpty(host.LanLobby.slots[RemotePlayer].peerId),"Join did not bind the remote slot");
            host.SetLanReady(true);client.SetLanReady(true);
            yield return PumpUntil(()=>host.LanLobby.slots[0].ready&&host.LanLobby.slots[RemotePlayer].ready,"Both players did not become ready");
            host.StartLanMatch();yield return PumpUntil(()=>client.World!=null&&!client.InMenu&&client.IsLanConnected,"Start full state did not reach the client");
            Assert.IsTrue(host.IsLanHost);Assert.AreEqual(0,host.LocalPlayerSlot);Assert.AreEqual(RemotePlayer,client.LocalPlayerSlot);Assert.IsNull(client.Simulation);
            CollectionAssert.AreEquivalent(new[]{1,3},host.Simulation.CaptureState().controllers.Select(c=>c.player));
            // Controlled fixture funds still require every real order to pass
            // World.Issue and its normal payment/production rules.
            host.World.Players[RemotePlayer].resources=new Cost(3000,3000,3000);
            yield return PumpUntil(()=>client.World.Players[RemotePlayer].resources.food==3000&&client.LanStateRevision==host.LanStateRevision,"Opening funds and revision did not synchronize");
        }
        EntityState Town(GameController peer)=>peer.World.Entities.Single(e=>e.Alive&&e.owner==RemotePlayer&&e.def.id=="town");
        GameCommand Train()=>new GameCommand{player=RemotePlayer,kind=CommandKind.Train,actors=new[]{Town(client).id},item=client.World.WorkerDefinition(RemotePlayer).id,count=1};
        static string State(World world,bool events=true)=>JsonUtility.ToJson(world.CaptureState(events));
        static Cost Difference(Cost after,Cost before)=>new Cost(after.food-before.food,after.wood-before.wood,after.coin-before.coin);
        static void EqualCost(Cost expected,Cost actual,string reason)
        {Assert.AreEqual(expected.food,actual.food,.001f,reason+" food");Assert.AreEqual(expected.wood,actual.wood,.001f,reason+" wood");Assert.AreEqual(expected.coin,actual.coin,.001f,reason+" coin");}
        LanConnection HostConnection()
        {
            var remote=Private<object>(host,"lanRemote");Assert.IsNotNull(remote);
            var field=remote.GetType().GetField("connection",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);Assert.IsNotNull(field);
            return (LanConnection)field.GetValue(remote);
        }
        Dictionary<string,byte[]> OwnedFiles()=>Directory.GetFiles(directory,"*",SearchOption.AllDirectories).ToDictionary(p=>p.Substring(directory.Length+1),File.ReadAllBytes);
        void UnchangedFiles(Dictionary<string,byte[]> before)
        {
            var after=OwnedFiles();CollectionAssert.AreEquivalent(before.Keys,after.Keys,"Client guard changed the owned file set");
            foreach(var file in before)CollectionAssert.AreEqual(file.Value,after[file.Key],"Client guard changed "+file.Key);
        }

        [UnityTest] public IEnumerator ClientActualUpdateAcrossFramesCannotTickOrRunASecondSimulation()
        {
            yield return StartRemoteMatch();var hostWorld=host.World;var clientWorld=client.World;
            string hostState=State(hostWorld),clientState=State(clientWorld);int step=host.Simulation.StepCount;float time=clientWorld.Time;
            Assert.IsFalse(host.enabled);Assert.IsFalse(client.enabled);
            // Invoke the production Update route across real frames without
            // enabling Start, which would consume the test runner's CLI flags.
            for(int frame=0;frame<12;frame++){Invoke(client,"Update");yield return null;}
            Assert.AreSame(clientWorld,client.World);Assert.IsNull(client.Simulation);Assert.IsNull(client.Recorder);
            Assert.AreEqual(time,client.World.Time,.000001f);Assert.AreEqual(clientState,State(client.World),"The replica advanced without a host state update");
            Assert.AreSame(hostWorld,host.World);Assert.AreEqual(step,host.Simulation.StepCount);Assert.AreEqual(hostState,State(host.World));
        }

        [UnityTest] public IEnumerator ClientLocalMenuAndFocusLossNeverPauseTheAuthoritativeWorld()
        {
            yield return StartRemoteMatch();int step=host.Simulation.StepCount;
            client.TogglePause();Assert.IsTrue(client.LocalMenuPaused);Assert.IsFalse(client.CanSubmitOrders);
            for(int frame=0;frame<4;frame++){Invoke(client,"Update");yield return null;Assert.IsFalse(host.World.Paused);Assert.IsFalse(client.World.Paused);}
            Assert.IsTrue(host.Simulation.Step(),"A remote local menu stopped the host scheduler");Assert.AreEqual(step+1,host.Simulation.StepCount);
            client.CloseLocalMenu();Assert.IsFalse(client.LocalMenuPaused);Assert.IsTrue(client.CanSubmitOrders);
            client.PauseForFocusLoss();Assert.IsTrue(client.LocalMenuPaused);Assert.IsTrue(client.PausedForFocusLoss);
            Assert.IsFalse(host.World.Paused);Assert.IsFalse(client.World.Paused);Assert.IsTrue(host.Simulation.Step());
            client.CloseLocalMenu();yield return Ready();Assert.IsFalse(client.LocalMenuPaused);
        }

        [UnityTest] public IEnumerator WrongDeltaBaselineKeepsTheRenderedWorldUntilAnActualResyncFullRepairsIt()
        {
            yield return StartRemoteMatch();var oldWorld=client.World;string before=State(oldWorld);long revision=client.LanStateRevision;
            var previous=SnapshotCodec.Clone(Private<LanFullState>(host,"lanPublished"));Assert.AreEqual(revision,previous.revision);
            float food=host.World.Players[RemotePlayer].resources.food;host.World.Players[RemotePlayer].resources.food+=123;
            var next=LanProjection.Full(host.World,host.MatchId,previous.epoch,previous.revision+1,host.Simulation.StepCount,previous.queues);
            var bad=LanProjection.Delta(previous,next,host.World.Events);bad.baseRevision=previous.revision+99;
            Assert.IsTrue(HostConnection().Send(LanWire.Encode("delta",bad,host.MatchId,previous.epoch)),"The malformed baseline must traverse the real host connection");
            yield return PumpUntil(()=>Private<LanReplica>(client,"lanReplica").NeedsFullState,"Client did not request a replacement baseline",false,true);
            Assert.AreSame(oldWorld,client.World);Assert.AreEqual(before,State(client.World),"A rejected delta partially changed the rendered world");Assert.AreEqual(revision,client.LanStateRevision);
            Assert.IsFalse(client.IsLanConnected);Assert.IsFalse(client.CanSubmitOrders);StringAssert.Contains("baseline",Private<LanReplica>(client,"lanReplica").Error);
            yield return PumpUntil(()=>client.IsLanConnected&&!Private<LanReplica>(client,"lanReplica").NeedsFullState&&client.World.Players[RemotePlayer].resources.food==food+123,"The host did not answer the wire resync request with a valid full state");
            Assert.AreSame(oldWorld,client.World,"A same-session full state repairs the existing replica");Assert.Greater(client.LanStateRevision,revision);
            Assert.AreEqual(State(host.World,false),State(client.World,false),"Repaired projection differs from the host");Assert.IsNull(client.Simulation);
        }

        [UnityTest] public IEnumerator ClientSoloAndReplayEntrypointsCannotMutateTheSessionOrOwnedFiles()
        {
            // Real valid solo artifacts distinguish a LAN guard from ordinary
            // missing-file or incompatible-file rejection.
            client.StartMatch();Assert.IsTrue(client.SaveMatch(1));Assert.IsTrue(client.SaveReplayNow(),client.ReplayFileError);string replay=client.LastReplayFile;
            yield return StartRemoteMatch();DrainWrites(client);DrainWrites(host);
            Assert.IsTrue(File.Exists(replay));Assert.IsTrue(File.Exists(MatchSaveStore.SlotPath(client.SaveDirectory,1)));
            var world=client.World;var scheduler=host.Simulation;string before=State(world),hostBefore=State(host.World),match=client.MatchId;var files=OwnedFiles();
            var direct=client.Command(Train());Assert.IsFalse(direct.accepted);StringAssert.Contains("confirmation",direct.reason);
            Assert.IsFalse(client.SaveMatch(2));Assert.IsFalse(client.LoadMatch(1));Assert.IsFalse(client.SaveReplayNow());client.SaveReplayAsync();
            client.OpenReplayAsync(replay);Assert.IsFalse(client.ReplayOpening);Assert.IsFalse(client.LoadReplayNow(replay));
            Assert.IsFalse(client.StartGuided("lesson_selection"));Assert.IsFalse(client.SaveLearningProgress());client.StartMatch();client.RetryCurrentMatch();client.SaveReport("quit");
            for(int frame=0;frame<3;frame++){Invoke(client,"Update");yield return null;}
            Assert.IsTrue(client.IsLanClient);Assert.IsTrue(client.IsLanConnected);Assert.IsFalse(client.IsWatching);Assert.IsFalse(client.InMenu);
            Assert.AreSame(world,client.World);Assert.AreEqual(match,client.MatchId);Assert.AreEqual(before,State(client.World));Assert.AreEqual(hostBefore,State(host.World));Assert.AreSame(scheduler,host.Simulation);
            Assert.IsNull(client.Simulation);Assert.IsNull(client.Recorder);Assert.IsNull(client.Guided);Assert.IsFalse(client.ReplayOpening);Assert.IsFalse(client.ReplaySaving);Assert.AreEqual(0,client.LanPendingCount);UnchangedFiles(files);
        }

        [UnityTest] public IEnumerator ReconnectResendsAnUnconfirmedTrainingSequenceWithoutPayingTwice()
        {
            yield return StartRemoteMatch();var hostWorld=host.World;var scheduler=host.Simulation;var clientWorld=client.World;
            string peer=Private<string>(client,"lanPeerId"),epoch=Private<string>(client,"lanEpoch"),match=client.MatchId;int generation=Private<int>(client,"lanGeneration");
            var oldConnection=Private<LanConnection>(client,"lanClient");long connectionId=oldConnection.ConnectionId;
            var command=Train();var cost=host.World.UnitCost(RemotePlayer,host.Rules.Entity(command.item));var spent=host.World.Players[RemotePlayer].spent;
            int payments=host.World.Events.Count(e=>e.kind=="Payment"&&e.player==RemotePlayer),callbacks=0;
            var request=client.SubmitCommand(command,result=>{callbacks++;Assert.IsTrue(result.accepted,result.reason);});
            Assert.AreEqual(CommandRequestStatus.Pending,request.Status);Assert.Greater(request.Sequence,0);
            // The host consumes and pays, but the client never pumps its ACK.
            // Explicit disposal drops those unread receipts before reconnect.
            yield return PumpUntil(()=>Town(host).queue.Count(q=>q.kind=="Train"&&q.item==command.item)==1,"The host did not consume the initial wire order",true,false);
            Assert.IsFalse(request.IsTerminal);Assert.AreEqual(0,callbacks);Assert.AreEqual(1,client.LanPendingCount);
            EqualCost(cost,Difference(host.World.Players[RemotePlayer].spent,spent),"Initial host payment");oldConnection.Dispose();Assert.IsFalse(oldConnection.Connected);
            yield return PumpUntil(()=>request.IsTerminal&&client.IsLanConnected&&Private<int>(client,"lanGeneration")>generation,"The original pending sequence did not resolve after reconnect");
            Assert.AreEqual(CommandRequestStatus.Applied,request.Status);Assert.AreEqual(1,callbacks);Assert.AreEqual(0,client.LanPendingCount);
            Assert.AreEqual(peer,Private<string>(client,"lanPeerId"));Assert.AreEqual(epoch,Private<string>(client,"lanEpoch"));Assert.AreEqual(match,client.MatchId);
            Assert.AreNotEqual(connectionId,Private<LanConnection>(client,"lanClient").ConnectionId);Assert.IsTrue(oldConnection.WorkersTerminated);
            Assert.AreSame(hostWorld,host.World);Assert.AreSame(scheduler,host.Simulation);Assert.AreSame(clientWorld,client.World);Assert.IsNull(client.Simulation);
            Assert.AreEqual(1,Town(host).queue.Count(q=>q.kind=="Train"&&q.item==command.item));Assert.AreEqual(1,Town(client).queue.Count(q=>q.kind=="Train"&&q.item==command.item));
            EqualCost(cost,Difference(host.World.Players[RemotePlayer].spent,spent),"Reconnect duplicate payment");Assert.AreEqual(payments+1,host.World.Events.Count(e=>e.kind=="Payment"&&e.player==RemotePlayer));
            EqualCost(host.World.Players[RemotePlayer].resources,client.World.Players[RemotePlayer].resources,"Reconnected replica economy");
            var records=host.Recorder.Capture().records.Where(r=>r.kind==ReplayOperation.Command&&r.command.player==RemotePlayer&&r.command.kind==CommandKind.Train).ToArray();
            Assert.AreEqual(1,records.Length,"Retransmission must not record or execute the order a second time");Assert.IsTrue(records[0].accepted);
        }

        [UnityTest] public IEnumerator DisconnectedHostCanResumeTheSharedPauseThroughItsActualMenu()
        {
            yield return StartRemoteMatch();host.SetLanSharedPause(true);
            yield return PumpUntil(()=>host.World.Paused&&client.World.Paused&&host.LanPendingCount==0&&client.LanPendingCount==0,"Shared pause did not reach both peers before disconnect");
            var connection=Private<LanConnection>(client,"lanClient");connection.Dispose();Assert.IsFalse(connection.Connected);
            // Keep the client controller disabled and never pump it again:
            // the host must offer Resume without waiting for a reconnection.
            host.PumpLan();yield return PumpUntil(()=>!host.IsLanConnected,"Host did not observe the actual peer disconnect",true,false);
            Assert.IsTrue(host.World.Paused);Assert.IsFalse(host.CanSubmitOrders);Assert.AreEqual(0,host.LanPendingCount);
            int step=host.Simulation.StepCount;float time=host.World.Time;
            foreach(var canvas in hostRoot.GetComponentsInChildren<Canvas>(true))canvas.gameObject.SetActive(true);
            host.TogglePause();yield return Ready();Assert.IsTrue(host.LocalMenuPaused);
            var resume=hostRoot.GetComponentsInChildren<Button>().Single(b=>b.isActiveAndEnabled&&b.name=="PAUSE MATCH");
            Assert.AreEqual("RESUME MATCH",resume.GetComponentInChildren<Text>().text);
            Assert.IsTrue(resume.interactable,"The authoritative host must be able to resume even when its partner is disconnected");
            resume.onClick.Invoke();Assert.AreEqual(1,host.LanPendingCount,"The actual menu must queue a host control request");
            yield return PumpUntil(()=>!host.World.Paused&&host.LanPendingCount==0,"Host Resume was stranded behind shared pause or the absent connection",true,false);
            Assert.IsFalse(host.IsLanConnected);Assert.IsFalse(connection.Connected);Assert.IsTrue(client.World.Paused,"The disconnected client was deliberately not pumped");
            Assert.AreEqual(step,host.Simulation.StepCount);Assert.AreEqual(time,host.World.Time,.000001f,"Resuming the control plane must not advance simulation time");
            Assert.IsTrue(host.LocalMenuPaused);Assert.IsNull(client.Simulation);
        }
    }
}
