using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Frontier.Tests
{
    // Real controller/HUD/input routes over an ephemeral loopback TCP port.
    // This fixture does not certify native input or a two-physical-Mac LAN.
    public sealed class LanPresentation068Tests
    {
        const BindingFlags PrivateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
        const int RemotePlayer=2;
        GameObject hostRoot,clientRoot;
        GameController host,client;
        string directory;
        Mouse mouse;
        Keyboard keyboard;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;

        [UnitySetUp] public IEnumerator SetUp()
        {
            directory=Path.Combine(Path.GetTempPath(),"Frontier-LAN-presentation-"+Guid.NewGuid().ToString("N"));
            Assert.IsFalse(Directory.Exists(directory));Directory.CreateDirectory(directory);
            background=InputSystem.settings.backgroundBehavior;
            editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            host=CreatePeer("host",out hostRoot,false);
            client=CreatePeer("client",out clientRoot,true);
            mouse=InputSystem.AddDevice<Mouse>();keyboard=InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(Screen.width*.5f,Screen.height*.5f)});
            yield return Ready();
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            foreach(var peer in new[]{client,host})if(peer!=null)
            {
                peer.enabled=false;peer.LeaveLan();
                Invoke(peer,"FinishReplayFileWrites");Invoke(peer,"PollReplayOperations");
            }
            if(clientRoot!=null)UnityEngine.Object.Destroy(clientRoot);
            if(hostRoot!=null)UnityEngine.Object.Destroy(hostRoot);
            yield return null;
            if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);
            if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior=background;
            InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
            if(directory!=null&&Directory.Exists(directory))Directory.Delete(directory,true);
        }

        GameController CreatePeer(string name,out GameObject root,bool visible)
        {
            Assert.IsTrue(Application.isEditor||Application.isBatchMode,"Create peers only where Awake uses memory-only services; never load personal storage");
            root=new GameObject("Isolated LAN "+name);
            var peer=root.AddComponent<GameController>();peer.enabled=false;
            // Editor/batch Awake must start with memory-only services. Redirect
            // all durable paths before any match, report or replay can start.
            Assert.IsFalse(peer.Settings.Persistent,"Awake must not load personal preferences");
            Assert.IsTrue(peer.Progress.MemoryOnly,"Awake must not load personal account progress");
            string owned=Path.Combine(directory,name);
            SetField(peer,"saveDirectoryOverride",Path.Combine(owned,"Saves"));
            SetField(peer,"reportDirectoryOverride",Path.Combine(owned,"Reports"));
            SetField(peer,"replayDirectoryOverride",Path.Combine(owned,"Replays"));
            SetProperty(peer,"Progress",AccountProgressStore.Load(Path.Combine(owned,"Account")));
            SetProperty(peer.Settings,"Path",Path.Combine(owned,"Preferences","player-settings.json"));
            var preferences=peer.Settings.Current;preferences.muted=true;preferences.musicEnabled=false;preferences.environmentEnabled=false;
            Assert.IsTrue(peer.Settings.TryUpdate(preferences,out string reason),reason);
            Assert.IsTrue(peer.SaveDirectory.StartsWith(owned+Path.DirectorySeparatorChar,StringComparison.Ordinal));
            Assert.IsTrue(peer.ReplayDirectory.StartsWith(owned+Path.DirectorySeparatorChar,StringComparison.Ordinal));
            Assert.IsTrue(peer.Progress.FilePath.StartsWith(owned+Path.DirectorySeparatorChar,StringComparison.Ordinal));

            // Each controller owns its camera; the hidden host never consumes
            // this fixture's mouse stream or overlaps the client's HUD.
            foreach(var camera in root.GetComponentsInChildren<Camera>())camera.enabled=false;
            var cameraRoot=new GameObject("Owned "+name+" camera");cameraRoot.transform.SetParent(root.transform,false);
            var ownedCamera=cameraRoot.AddComponent<Camera>();ownedCamera.enabled=visible;
            ownedCamera.orthographic=true;ownedCamera.orthographicSize=26;ownedCamera.nearClipPlane=.2f;ownedCamera.farClipPlane=260;
            SetProperty(peer,"GameCamera",ownedCamera);peer.Controls.RestoreCamera(new Vector2(25,25),26);
            peer.View.enabled=false;peer.Controls.enabled=visible;
            if(!visible)foreach(var canvas in root.GetComponentsInChildren<Canvas>())canvas.gameObject.SetActive(false);
            peer.Ambience.enabled=false;peer.Audio.PauseAll(true);
            return peer;
        }

        static void SetField(object target,string name,object value)
        {var field=target.GetType().GetField(name,PrivateInstance);Assert.IsNotNull(field,name+" is required for storage isolation");field.SetValue(target,value);}
        static void SetProperty(object target,string name,object value)
        {var setter=target.GetType().GetProperty(name)?.GetSetMethod(true);Assert.IsNotNull(setter,name);setter.Invoke(target,new[]{value});}
        static void Invoke(object target,string name)
        {var method=target.GetType().GetMethod(name,PrivateInstance);Assert.IsNotNull(method,name);method.Invoke(target,null);}
        IEnumerator Ready(){yield return null;Canvas.ForceUpdateCanvases();yield return null;}
        IEnumerator PumpUntil(Func<bool> condition,string reason)
        {
            float deadline=Time.realtimeSinceStartup+12;
            while(!condition()&&Time.realtimeSinceStartup<deadline)
            {host.PumpLan();client.PumpLan();yield return null;}
            Assert.IsTrue(condition(),reason+"; host: "+host.LanConnectionText+"; client: "+client.LanConnectionText);
            yield return Ready();
        }
        IEnumerator StartRemoteMatch()
        {
            host.SkirmishMode="teams";host.SelectedMap=MapTopology.Frontier;host.SelectedObjective="elimination";
            host.OpponentDifficulty="easy";host.MatchSeed=314159;
            client.PlayerCivilization="france";client.DeckPreset="economy";client.ResetDeckPreset();
            host.HostLan(0);Assert.Greater(host.LanPort,0,"The fixture must obtain an isolated ephemeral port");
            client.JoinLan("127.0.0.1",host.LanPort);
            yield return PumpUntil(()=>client.IsLanClient&&client.IsLanConnected&&client.LocalPlayerSlot==RemotePlayer&&host.LanLobby!=null&&host.LanLobby.slots.Length==4&&!string.IsNullOrEmpty(host.LanLobby.slots[RemotePlayer].peerId),"HELLO did not bind the remote human slot");
            host.SetLanReady(true);client.SetLanReady(true);
            yield return PumpUntil(()=>host.LanLobby.slots[0].ready&&host.LanLobby.slots[RemotePlayer].ready,"Both human slots must be ready");
            host.StartLanMatch();
            yield return PumpUntil(()=>client.World!=null&&!client.InMenu&&client.IsLanConnected,"The authoritative start snapshot did not reach the client");
            Assert.IsTrue(host.IsLanHost);Assert.IsTrue(client.IsLanClient);Assert.AreEqual(0,host.LocalPlayerSlot);Assert.AreEqual(RemotePlayer,client.LocalPlayerSlot);
            Assert.AreEqual(4,host.World.Players.Length);Assert.AreEqual("france",host.World.Players[RemotePlayer].civilization);
            CollectionAssert.AreEquivalent(new[]{1,3},host.Simulation.CaptureState().controllers.Select(c=>c.player));
            Assert.IsNull(client.Simulation,"The LAN client must render host state without a second scheduler");
            Assert.IsTrue(client.CanSubmitOrders);Assert.AreEqual(RemotePlayer,client.ObserverPlayer);
            // Controlled fixture funds make the real commands legal; they do
            // not replace World.Issue or fabricate a successful outcome.
            host.World.Players[RemotePlayer].resources=new Cost(3000,3000,3000);
            host.World.Players[RemotePlayer].shipmentTokens=3;
            yield return PumpUntil(()=>client.World.Players[RemotePlayer].resources.food==3000&&client.World.Players[RemotePlayer].shipmentTokens==3,"Controlled opening funds did not synchronize");
            client.Controls.ClearTransientInput();yield return new WaitForSecondsRealtime(.25f);yield return Ready();
        }

        EntityState Town(GameController peer)=>peer.World.Entities.Single(e=>e.Alive&&e.owner==RemotePlayer&&e.def.id=="town");
        Button Button(string prefix)=>clientRoot.GetComponentsInChildren<Button>().Single(b=>b.isActiveAndEnabled&&b.name.StartsWith(prefix,StringComparison.Ordinal));
        bool VisibleText(string text)=>clientRoot.GetComponentsInChildren<Text>().Any(t=>t.isActiveAndEnabled&&t.text==text);
        bool VisibleTextPrefix(string text)=>clientRoot.GetComponentsInChildren<Text>().Any(t=>t.isActiveAndEnabled&&t.text.StartsWith(text,StringComparison.Ordinal));
        void Select(EntityState entity){client.Controls.Select(entity);Invoke(client.Hud,"EnsureActionContextCurrent");}
        static void EqualCost(Cost expected,Cost actual,string reason)
        {Assert.AreEqual(expected.food,actual.food,.001f,reason+" food");Assert.AreEqual(expected.wood,actual.wood,.001f,reason+" wood");Assert.AreEqual(expected.coin,actual.coin,.001f,reason+" coin");}
        static Cost Difference(Cost after,Cost before)=>new Cost(after.food-before.food,after.wood-before.wood,after.coin-before.coin);
        void PendingWithoutLocalPayment(Cost resources,Cost spent)
        {
            Assert.AreEqual(1,client.LanPendingCount,"A HUD click must await the actual host outcome");
            EqualCost(resources,client.World.Players[RemotePlayer].resources,"Pending request changed replica resources");
            EqualCost(spent,client.World.Players[RemotePlayer].spent,"Pending request charged the client locally");
        }
        void OneRecordedCommand(CommandKind kind,string item="")
        {
            Assert.IsNotNull(host.Recorder,"The host must record the authoritative external operation");
            var commands=host.Recorder.Capture().records.Where(r=>r.kind==ReplayOperation.Command&&r.command.player==RemotePlayer&&r.command.kind==kind&&(item==""||r.command.item==item)).ToArray();
            Assert.AreEqual(1,commands.Length,"The real HUD action must reach the recorder exactly once");Assert.IsTrue(commands[0].accepted,commands[0].reason);
        }

        [UnityTest] public IEnumerator RemoteTrainingHudWaitsForHostAndPaysOnceForSlotTwo()
        {
            yield return StartRemoteMatch();var town=Town(client);Select(town);yield return Ready();
            var unit=client.World.WorkerDefinition(RemotePlayer);var cost=host.World.UnitCost(RemotePlayer,unit);
            Assert.IsEmpty(client.World.ProductionReason(RemotePlayer,town,unit.id));
            var resources=client.World.Players[RemotePlayer].resources;var spent=client.World.Players[RemotePlayer].spent;
            var hostSpent=host.World.Players[RemotePlayer].spent;var otherSpent=host.World.Players[0].spent;
            int payments=host.World.Events.Count(e=>e.kind=="Payment"&&e.player==RemotePlayer);
            var train=Button(unit.name+"\n");Assert.IsTrue(train.interactable);train.onClick.Invoke();
            PendingWithoutLocalPayment(resources,spent);Assert.IsEmpty(Town(host).queue,"The host has not consumed the TCP request yet");
            yield return PumpUntil(()=>client.LanPendingCount==0&&Town(client).queue.Any(q=>q.kind=="Train"&&q.item==unit.id),"Remote training did not resolve and replicate");
            Assert.AreEqual(1,Town(host).queue.Count(q=>q.kind=="Train"&&q.item==unit.id));
            EqualCost(cost,Difference(host.World.Players[RemotePlayer].spent,hostSpent),"Host training payment");
            EqualCost(otherSpent,host.World.Players[0].spent,"Remote HUD charged host slot zero");
            Assert.AreEqual(payments+1,host.World.Events.Count(e=>e.kind=="Payment"&&e.player==RemotePlayer));
            EqualCost(host.World.Players[RemotePlayer].resources,client.World.Players[RemotePlayer].resources,"Resolved replica resources");OneRecordedCommand(CommandKind.Train,unit.id);
        }

        [UnityTest] public IEnumerator RemoteResearchAndHomeCityUseTheBoundHumanSlot()
        {
            yield return StartRemoteMatch();var hostTown=Town(host);
            var market=host.World.Spawn("market",RemotePlayer,hostTown.position+new Vector2(12,8));host.World.Fog.Reveal(RemotePlayer,market.position,15);
            yield return PumpUntil(()=>client.World.Get(market.id)!=null,"Controlled completed market did not synchronize");
            Select(client.World.Get(market.id));yield return Ready();Button("TECHNOLOGIES").onClick.Invoke();yield return Ready();
            var tech=client.Rules.techs.Where(t=>t.building=="market"&&(string.IsNullOrEmpty(t.civilization)||t.civilization==client.World.Players[RemotePlayer].civilization))
                .OrderBy(t=>t.age).ThenBy(t=>t.id,StringComparer.Ordinal).Take(8)
                .First(t=>client.World.ProductionReason(RemotePlayer,client.World.Get(market.id),t.id,true)=="");
            var resources=client.World.Players[RemotePlayer].resources;var spent=client.World.Players[RemotePlayer].spent;var hostSpent=host.World.Players[RemotePlayer].spent;
            Button(tech.name+" · AGE ").onClick.Invoke();PendingWithoutLocalPayment(resources,spent);
            client.Hud.ShowHomeCity();yield return Ready();Assert.IsTrue(VisibleTextPrefix("HOME CITY"));
            yield return PumpUntil(()=>client.LanPendingCount==0&&client.World.Get(market.id).queue.Any(q=>q.kind=="Research"&&q.item==tech.id),"Remote research did not resolve and replicate");
            Assert.IsTrue(VisibleTextPrefix("HOME CITY"),"A delayed research result must not reopen its old modal");
            EqualCost(tech.cost,Difference(host.World.Players[RemotePlayer].spent,hostSpent),"Host research payment");OneRecordedCommand(CommandKind.Research,tech.id);
            var player=client.World.Players[RemotePlayer];var card=player.deck.Take(8).Select(client.Rules.Card).First(c=>client.World.ShipmentReason(RemotePlayer,c.id)=="");
            int tokens=player.shipmentTokens,sent=player.shipsSent,hostSent=host.World.Players[0].shipsSent;
            resources=player.resources;spent=player.spent;
            Button(card.name.ToUpperInvariant()+"  ·  AGE ").onClick.Invoke();PendingWithoutLocalPayment(resources,spent);
            Assert.AreEqual(tokens,player.shipmentTokens,"Pending shipment must not consume a local token");
            yield return PumpUntil(()=>client.LanPendingCount==0&&client.World.Players[RemotePlayer].shipsSent==sent+1,"Remote Home City shipment did not resolve");
            Assert.AreEqual(tokens-1,host.World.Players[RemotePlayer].shipmentTokens);Assert.AreEqual(sent+1,host.World.Players[RemotePlayer].shipsSent);
            Assert.AreEqual(hostSent,host.World.Players[0].shipsSent,"Home City must not send the host's card");OneRecordedCommand(CommandKind.Ship,card.id);
        }

        [UnityTest] public IEnumerator LateAcceptedAgeResultDoesNotCloseANewerHomeCityModal()
        {
            yield return StartRemoteMatch();var town=Town(client);Select(town);client.Hud.ShowAgeChoices();yield return Ready();
            var choice=client.World.AgeChoices(RemotePlayer,2).First();var age=host.Rules.Age(2);
            var resources=client.World.Players[RemotePlayer].resources;var spent=client.World.Players[RemotePlayer].spent;var hostSpent=host.World.Players[RemotePlayer].spent;
            Assert.IsEmpty(client.World.AgeReason(RemotePlayer,town));Button(choice.name.ToUpperInvariant()+"\n").onClick.Invoke();PendingWithoutLocalPayment(resources,spent);
            Assert.IsTrue(VisibleText("ADVANCE TO AGE 2"),"Pending age advancement must keep its current modal open");
            client.Hud.ShowHomeCity();yield return Ready();Assert.IsTrue(VisibleTextPrefix("HOME CITY"));
            yield return PumpUntil(()=>client.LanPendingCount==0&&Town(client).queue.Any(q=>q.kind=="Age"&&q.choice==choice.id),"Actual host age outcome did not arrive");
            Assert.IsTrue(VisibleTextPrefix("HOME CITY"),"The old accepted age callback closed a newer modal");
            Assert.IsFalse(VisibleText("ADVANCE TO AGE 2"));EqualCost(age.cost,Difference(host.World.Players[RemotePlayer].spent,hostSpent),"Host age payment");OneRecordedCommand(CommandKind.AdvanceAge);
        }

        Vector2 LegalHousePoint()
        {
            var town=Town(client);
            for(int radius=8;radius<=24;radius+=2)for(int x=-radius;x<=radius;x+=2)for(int y=-radius;y<=radius;y+=2)
            {
                var point=client.World.BuildPoint(RemotePlayer,"house",town.position+new Vector2(x,y));
                if(client.World.CanBuild(RemotePlayer,"house",point)=="")return point;
            }
            Assert.Fail("The controlled opening must contain a legal visible house site");return Vector2.zero;
        }
        [UnityTest] public IEnumerator LateAcceptedConstructionDoesNotClearANewerTargetMode()
        {
            yield return StartRemoteMatch();var worker=client.World.Entities.First(e=>e.Alive&&e.owner==RemotePlayer&&e.Worker);Select(worker);
            var point=LegalHousePoint();client.Controls.Focus(point);client.Hud.ShowBuildMenu();yield return Ready();
            Button(client.Rules.Entity("house").name+"  ·  ").onClick.Invoke();yield return new WaitForSecondsRealtime(.25f);yield return Ready();
            Assert.AreEqual("house",client.Controls.BuildingToPlace);
            var screen=(Vector2)client.GameCamera.WorldToScreenPoint(new Vector3(point.x,0,point.y));
            Assert.That(screen.x,Is.InRange(1,Screen.width-1));Assert.That(screen.y,Is.InRange(1,Screen.height-1));Assert.IsFalse(client.Hud.BlocksWorldInput(screen));
            var resources=client.World.Players[RemotePlayer].resources;var spent=client.World.Players[RemotePlayer].spent;var hostSpent=host.World.Players[RemotePlayer].spent;
            int houses=host.World.Entities.Count(e=>e.Alive&&e.owner==RemotePlayer&&e.def.id=="house");
            InputSystem.QueueStateEvent(mouse,new MouseState{position=screen}.WithButton(MouseButton.Left));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=screen});yield return Ready();
            PendingWithoutLocalPayment(resources,spent);Assert.AreEqual("house",client.Controls.BuildingToPlace,"Pending placement must not imitate accepted placement");
            client.Controls.BeginBuild("farm");Assert.AreEqual("farm",client.Controls.BuildingToPlace);
            yield return PumpUntil(()=>client.LanPendingCount==0&&client.World.Entities.Count(e=>e.Alive&&e.owner==RemotePlayer&&e.def.id=="house")==houses+1,"Actual house construction did not resolve");
            Assert.AreEqual("farm",client.Controls.BuildingToPlace,"A delayed house result cleared the newer farm target mode");
            Assert.AreEqual(houses+1,host.World.Entities.Count(e=>e.Alive&&e.owner==RemotePlayer&&e.def.id=="house"));
            EqualCost(host.World.EntityCost(RemotePlayer,host.Rules.Entity("house")),Difference(host.World.Players[RemotePlayer].spent,hostSpent),"Host foundation payment");OneRecordedCommand(CommandKind.Build,"house");
        }

        [UnityTest] public IEnumerator LocalMenuDoesNotPauseTheHostAndSharedResumeStillDrainsWhilePaused()
        {
            yield return StartRemoteMatch();Select(Town(client));yield return Ready();
            var unit=client.World.WorkerDefinition(RemotePlayer);var staleTrainClick=Button(unit.name+"\n").onClick;
            client.TogglePause();yield return Ready();
            Assert.IsTrue(client.LocalMenuPaused);Assert.IsTrue(VisibleText("LAN MATCH MENU"));
            Assert.IsFalse(host.World.Paused);Assert.IsFalse(client.World.Paused,"A local menu must not change the shared pause state");
            int step=host.Simulation.StepCount;Assert.IsTrue(host.Simulation.Step(),"The authoritative match must continue while a client menu is open");
            Assert.AreEqual(step+1,host.Simulation.StepCount);Button("CONTINUE").onClick.Invoke();yield return Ready();
            Assert.IsFalse(client.LocalMenuPaused);Assert.IsTrue(client.CanSubmitOrders);

            host.SetLanSharedPause(true);
            yield return PumpUntil(()=>host.LanPendingCount==0&&host.World.Paused&&client.World.Paused,"Shared pause did not become authoritative");
            Assert.IsFalse(client.CanSubmitOrders);Assert.IsFalse(host.Simulation.Step());
            var spent=host.World.Players[RemotePlayer].spent;int pending=client.LanPendingCount;
            staleTrainClick.Invoke();Assert.AreEqual(pending,client.LanPendingCount,"An old HUD button must not enqueue an order while shared pause is active");
            client.Controls.BeginBuild("house");Assert.IsEmpty(client.Controls.BuildingToPlace,"Build targeting must refuse orders during shared pause");
            EqualCost(spent,host.World.Players[RemotePlayer].spent,"Paused HUD changed the human economy");
            step=host.Simulation.StepCount;float time=host.World.Time;
            client.TogglePause();yield return Ready();var resume=Button("PAUSE MATCH");
            Assert.AreEqual("RESUME MATCH",resume.GetComponentInChildren<Text>().text);
            Assert.IsTrue(resume.interactable,"Session controls must remain available when game orders are blocked");
            resume.onClick.Invoke();Assert.AreEqual(1,client.LanPendingCount,"Shared resume must await a real control receipt");
            yield return PumpUntil(()=>client.LanPendingCount==0&&!host.World.Paused&&!client.World.Paused,"Resume was stranded behind the stopped simulation");
            Assert.AreEqual(step,host.Simulation.StepCount,"Transport/control pumping must not advance simulation");Assert.AreEqual(time,host.World.Time,.000001f);
            Assert.IsTrue(client.LocalMenuPaused);Button("CONTINUE").onClick.Invoke();yield return Ready();
            Assert.IsFalse(client.LocalMenuPaused);Assert.IsTrue(client.CanSubmitOrders);
        }
    }
}
