using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Frontier
{
    public sealed partial class GameController
    {
        GameSessionRole lanRole=GameSessionRole.Single;
        public GameSessionRole SessionRole=>IsWatching?GameSessionRole.Replay:lanRole;
        public bool IsLan=>lanRole==GameSessionRole.LanHost||lanRole==GameSessionRole.LanClient;
        public bool IsLanHost=>lanRole==GameSessionRole.LanHost;
        public bool IsLanClient=>lanRole==GameSessionRole.LanClient;
        public int LocalPlayerSlot {get;private set;}
        public bool LocalMenuPaused {get;private set;}
        public LanLobbyState LanLobby {get;private set;}
        public int LanPort=>lanServer?.Port??lanJoinPort;
        public long LanStateRevision {get;private set;}
        public int LanCompletedStep=>IsLanClient?lanReplica?.CompletedStep??0:Simulation?.StepCount??0;
        public long LanEventCursor=>IsLanClient?lanReplica?.EventCursor??0:World?.Events.Count??0;
        public string LanProjectedDigest=>!IsLan||World==null?"":LanProjection.Digest(World.CaptureState(false),IsLanHost?lanQueues.Capture(World):lanReplica.QueueSnapshot);
        public int LanPendingCount=>lanRequests.Count;
        public double LanRttMilliseconds {get;private set;}
        public string LanConnectionText {get;private set;}="";
        public bool IsLanConnected=>IsLanHost?lanRemote?.connection?.Connected==true:IsLanClient&&lanClient?.Connected==true&&lanWelcomeReceived&&(InMenu||!lanAwaitingFull&&lanReplica!=null&&!lanReplica.NeedsFullState);
        public bool CanSubmitOrders=>World!=null&&!InMenu&&!IsWatching&&!LocalMenuPaused&&!World.Paused&&!World.IsMatchEnded&&World.IsPlayerActive(LocalPlayerSlot)&&(!IsLanClient||IsLanConnected)&&
            (Guided==null||Guided.Status==GuidedPlayStatus.Running);
        LanTcpServer lanServer;
        LanConnection lanClient;
        Task<LanConnection> lanConnectTask;
        CancellationTokenSource lanConnectCancellation;
        readonly List<LanPeer> lanCandidates=new List<LanPeer>();
        LanPeer lanRemote;
        sealed class LanPeer
        {
            public LanConnection connection;
            public string id="",token="";
            public int generation;
            public double heard,disconnectedAt=-1;
            public bool retired,closing;
        }
        string lanPeerId="",lanResumeToken="",lanMatchId="",lanEpoch="",lanJoinAddress="";
        int lanGeneration,lanJoinPort,lanLifetime;
        long lanNextRequest,lanConfirmedSequence,lanPingNonce;
        double lanLastHeard,lanLastPing,lanPingSent,lanNextConnect,lanDisconnectedAt=-1,lanLastPublish;
        bool lanWelcomeReceived,lanAwaitingFull,lanForceFull,lanDirty;
        LanAuthority lanAuthority;
        LanReplica lanReplica;
        LanFullState lanPublished;
        LanQueueTracker lanQueues=new LanQueueTracker();
        public long LanQueueId(int entity,int index)=>IsLanHost?lanQueues.QueueId(World,entity,index):lanReplica?.Queues.QueueId(entity,index)??0;
        public int LanQueuePosition(int entity,long id)
        {
            var e=World?.Get(entity);if(e==null||id<=0)return -1;
            for(int i=0;i<e.queue.Count;i++)if(LanQueueId(entity,i)==id)return i;
            return -1;
        }
        readonly Dictionary<long,CommandRequest> lanRequests=new Dictionary<long,CommandRequest>();
        readonly Dictionary<long,LanCommandRequest> lanUnconfirmed=new Dictionary<long,LanCommandRequest>();
        readonly List<LanCommandAck> lanAwaitingState=new List<LanCommandAck>();
        public event Action<LanCommandAck> LanCommandResolved;
        static double LanNow=>Time.realtimeSinceStartupAsDouble;
        static string ResumeToken()
        {var bytes=new byte[32];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(bytes);return Convert.ToBase64String(bytes);}
        public void HostLan(int port=47621)
        {
            var authored=SelectedScenario;
            if(authored!=null)
            {string invalid=ScenarioCompiler.Validate(authored,Rules,4);if(invalid!=""){Notify("Cooperative scenarios require four authored player slots: "+invalid);return;}}
            LanTcpServer server;
            try{server=new LanTcpServer(port);}
            catch(Exception error){Notify("Could not host: "+error.Message);return;}
            ReturnToMenu();lanServer=server;lanRole=GameSessionRole.LanHost;LocalPlayerSlot=0;ObserverPlayer=0;
            lanPeerId=Guid.NewGuid().ToString("N");lanGeneration=1;lanMatchId=Guid.NewGuid().ToString("N");lanEpoch=Guid.NewGuid().ToString("N");
            SkirmishMode="teams";var civs=RosterCivilizations();
            LanLobby=new LanLobbyState{mapId=SelectedMap,seed=MatchSeed,difficulty=OpponentDifficulty,maxAge=AgeLimit,objective=SelectedObjective,slots=new LanLobbySlot[4]};
            for(int i=0;i<4;i++)LanLobby.slots[i]=new LanLobbySlot{player=i,human=i==0||i==2,civilization=civs[i],ready=i==1||i==3,deck=DeckLibrary.Preset(Rules,civs[i],i==0?DeckPreset:"military").ToArray(),peerId=i==0?lanPeerId:""};
            ApplyLanScenarioConfiguration(authored);
            LanConnectionText="Hosting · port "+server.Port+" · waiting for your ally";lanLastPing=LanNow;Hud.ShowLanLobby();
        }
        public void JoinLan(string address,int port=47621)
        {
            if(string.IsNullOrWhiteSpace(address)||address.Length>255||port<1||port>65535){Notify("Enter a host address and valid port");return;}
            ReturnToMenu();lanRole=GameSessionRole.LanClient;LocalPlayerSlot=2;ObserverPlayer=2;lanJoinAddress=address.Trim();lanJoinPort=port;
            LanConnectionText="Connecting to "+lanJoinAddress+":"+port;lanDisconnectedAt=LanNow;BeginLanConnect();Hud.ShowLanLobby();
        }
        void BeginLanConnect()
        {
            if(lanConnectTask!=null)return;
            lanConnectCancellation?.Dispose();lanConnectCancellation=new CancellationTokenSource();
            lanConnectTask=LanConnection.ConnectAsync(lanJoinAddress,lanJoinPort,5000,lanConnectCancellation.Token);lanNextConnect=LanNow+1;
        }
        public void SetLanReady(bool ready)
        {
            if(!IsLan||LanLobby?.started==true)return;
            string reason=DeckLibrary.Validate(Rules,PlayerCivilization,SelectedDeck);
            if(ready&&reason!=""){Notify(reason);return;}
            if(IsLanHost)
            {
                var selected=SelectedScenario;string invalid=selected==null?"":ScenarioCompiler.Validate(selected,Rules,4);
                if(invalid!=""){Notify("Cooperative scenarios require four authored player slots: "+invalid);return;}
                ApplyLanScenarioConfiguration(selected);
            }
            var state=new LanReady{ready=ready,civilization=PlayerCivilization,deck=SelectedDeck.ToArray(),scenarioHash=LanLobby.scenarioHash};
            if(IsLanHost){InstallLanReady(LanLobby.slots[0],state);LanLobby.difficulty=OpponentDifficulty;LanLobby.maxAge=AgeLimit;LanLobby.objective=LanLobby.hasScenarioDocument?"elimination":SelectedObjective;BroadcastLanLobby();}
            else if(lanWelcomeReceived&&lanClient?.Connected==true)SendLan(lanClient,"ready",state);
        }
        void InstallLanReady(LanLobbySlot slot,LanReady state)
        {
            if(state==null||state.deck==null||state.deck.Length>Rules.deckCapacity||Rules.Civ(state.civilization)==null)throw new ArgumentException("Invalid ally setup");
            string reason=DeckLibrary.Validate(Rules,state.civilization,state.deck);
            if(reason!="")throw new ArgumentException(reason);
            if(state.ready&&state.scenarioHash!=LanLobby.scenarioHash)throw new ArgumentException("Ready belongs to a different scenario; review the map and ready again");
            slot.ready=state.ready;slot.civilization=state.civilization;slot.deck=(string[])state.deck.Clone();slot.scenarioHash=state.ready?state.scenarioHash:"";
        }
        public void NotifyLanScenarioChanged()
        {
            if(!IsLanHost||LanLobby==null||LanLobby.started)return;
            foreach(var slot in LanLobby.slots)if(slot.human){slot.ready=false;slot.scenarioHash="";}
            var selected=SelectedScenario;string invalid=selected==null?"":ScenarioCompiler.Validate(selected,Rules,4);
            if(invalid!=""){Notify("Choose a scenario with four legal player slots for cooperation: "+invalid);BroadcastLanLobby();return;}
            ApplyLanScenarioConfiguration(selected);BroadcastLanLobby();
        }
        void ApplyLanScenarioConfiguration(ScenarioDocument authored)
        {
            string hash=authored==null?"":ScenarioCodec.ContentHash(authored);
            if(LanLobby.hasScenarioDocument!=(authored!=null)||LanLobby.scenarioHash!=hash)
                foreach(var slot in LanLobby.slots)if(slot.human){slot.ready=false;slot.scenarioHash="";}
            LanLobby.hasScenarioDocument=authored!=null;LanLobby.scenarioDocument=authored==null?null:ScenarioCodec.Clone(authored);LanLobby.scenarioHash=hash;
            LanLobby.mapId=authored==null?SelectedMap:MapTopology.Scenario;LanLobby.seed=authored==null?MatchSeed:authored.seed;
            LanLobby.objective=authored==null?SelectedObjective:"elimination";
        }
        void ValidateLanLobbyScenario(LanLobbyState lobby)
        {
            if(lobby?.slots?.Length!=4)throw new ArgumentException("Invalid cooperative player slots");
            if(lobby.hasScenarioDocument)
            {
                string invalid=ScenarioCompiler.Validate(lobby.scenarioDocument,Rules,4);
                if(invalid!=""||lobby.mapId!=MapTopology.Scenario||lobby.scenarioHash!=ScenarioCodec.ContentHash(lobby.scenarioDocument)||lobby.seed!=lobby.scenarioDocument.seed)
                    throw new ArgumentException("Invalid cooperative scenario: "+invalid);
            }
            else if(!MapTopology.IsValid(lobby.mapId)||!string.IsNullOrEmpty(lobby.scenarioHash))throw new ArgumentException("Invalid cooperative map identity");
        }
        void InstallReceivedLanLobby(LanLobbyState lobby)
        {
            ValidateLanLobbyScenario(lobby);
            if(World!=null&&lobby.started&&(World.HasScenario!=lobby.hasScenarioDocument||World.ScenarioHash!=lobby.scenarioHash))throw new ArgumentException("Host changed the running scenario");
            LanLobby=SnapshotCodec.Clone(lobby);selectedScenario069=lobby.hasScenarioDocument?ScenarioCodec.Clone(lobby.scenarioDocument):null;
            SelectedMap=lobby.mapId;MatchSeed=lobby.seed;
        }
        void RefreshLanLobbyConnectionText()
        {
            if(LanLobby==null||!IsLanConnected)return;
            if(LanLobby.started)
            {if(IsLanClient&&(World==null||lanAwaitingFull))LanConnectionText="Synchronizing with host";return;}
            if(LanLobby.slots?.Length!=4)return;
            int partner=IsLanHost?2:0;bool localReady=LanLobby.slots[LocalPlayerSlot]?.ready==true,partnerReady=LanLobby.slots[partner]?.ready==true;
            LanConnectionText=localReady&&partnerReady
                ?IsLanHost?"Both players ready · start co-op":"Both players ready · waiting for host to start"
                :localReady?"Ready · waiting for P"+(partner+1)
                :partnerReady?"P"+(partner+1)+" ready · choose your civilization and ready up"
                :"Connected · choose your civilization and ready up";
        }
        void BroadcastLanLobby(){RefreshLanLobbyConnectionText();if(lanRemote?.connection?.Connected==true)SendLan(lanRemote.connection,"lobby",LanLobby);Hud.RefreshLanLobbyState();}
        public void StartLanMatch()
        {
            if(!IsLanHost||LanLobby==null||LanLobby.started)return;
            if(lanRemote?.connection?.Connected!=true||!LanLobby.slots[0].ready||!LanLobby.slots[2].ready){Notify("Both players must be ready");return;}
            if(Rules.Difficulty(LanLobby.difficulty)==null||LanLobby.maxAge<1||LanLobby.maxAge>Rules.ages.Length){Notify("Invalid map, difficulty or age cap");return;}
            try
            {
                ValidateLanLobbyScenario(LanLobby);
                if((SelectedScenario==null?"":ScenarioCodec.ContentHash(SelectedScenario))!=LanLobby.scenarioHash)throw new ArgumentException("Scenario changed; review the map and ready again");
                var civs=new string[4];for(int i=0;i<4;i++){civs[i]=LanLobby.slots[i].civilization;InstallLanReady(LanLobby.slots[i],new LanReady{ready=LanLobby.slots[i].ready,civilization=civs[i],deck=LanLobby.slots[i].deck,scenarioHash=LanLobby.slots[i].human?LanLobby.slots[i].scenarioHash:LanLobby.scenarioHash});}
                var next=LanLobby.hasScenarioDocument?new World(Rules,LanLobby.scenarioDocument,civs,new[]{0,1,0,1},LanLobby.maxAge):new World(Rules,LanLobby.seed,civs,new[]{0,1,0,1},LanLobby.maxAge,LanLobby.mapId);
                var objective=next.ConfigureMapObjective(LanLobby.objective);if(!objective.accepted)throw new ArgumentException(objective.reason);
                for(int i=0;i<4;i++){next.Players[i].deck.Clear();next.Players[i].deck.AddRange(LanLobby.slots[i].deck);}
                Guided?.Dispose();Guided=null;ReleasePlayback();ClearBenchmark();World=next;Simulation=new MatchSimulation(next,new[]{1,3},LanLobby.difficulty);MatchId=lanMatchId;
                LanLobby=SnapshotCodec.Clone(LanLobby);LanLobby.started=true;Autoplay=false;SimulationSpeed=1;InMenu=false;LocalMenuPaused=false;PausedForFocusLoss=false;
                BeginRecording("lan_host");ObserverPlayer=LocalPlayerSlot;
                lanAuthority=new LanAuthority(World,lanMatchId,lanEpoch,c=>Recorder!=null?Recorder.Issue(c):World.Issue(c),(paused,reason)=>{if(Recorder!=null)Recorder.Pause(paused,reason);else World.Paused=paused;},(c,id)=>
                {
                    try{var cancel=lanQueues.ResolveCancel(World,c,id);return Recorder!=null?Recorder.Issue(cancel):World.Issue(cancel);}
                    catch(InvalidOperationException error){return CommandResult.Fail(error.Message);}
                });
                lanAuthority.BindPeer(lanPeerId,0,lanGeneration);lanAuthority.BindPeer(lanRemote.id,2,lanRemote.generation);
                Simulation.BeforeStep+=DrainLanAuthority;
                ResetLanPresentation(true);lanForceFull=true;lanDirty=true;LanConnectionText="Cooperating with P3";
                SendLan(lanRemote.connection,"lobby",LanLobby);PublishLan();
            }
            catch(Exception error){Notify("Could not start cooperation: "+error.Message);}
        }
        void ResetLanPresentation(bool newWorld)
        {
            Settings.Captions.Clear();Audio.ResetMatch();audioCursor=World?.Events.Count??0;
            Controls.CancelTargetMode();Controls.ClearTransientInput();Hud.InvalidateActions();
            if(newWorld){View.ResetWorld();Controls.ResetSelection();Controls.Focus(World.OpeningTownPosition(LocalPlayerSlot));}
            if(newWorld)Hud.ShowGame();Audio.PauseAll(IsPaused);
        }
        public CommandRequest SubmitCommand(GameCommand command,Action<CommandResult> resolved=null,long expectedQueueId=0)
        {
            var handle=new CommandRequest{Callback=resolved};
            if(!IsLan){handle.Resolve(Command(command));return handle;}
            if(InMenu||World==null||IsWatching){handle.Resolve(CommandResult.Fail("No active cooperative match"));return handle;}
            if(IsLanClient&&!IsLanConnected){handle.Resolve(CommandResult.Fail("Waiting for the host connection and state"));return handle;}
            if(command==null||command.player!=LocalPlayerSlot){handle.Resolve(CommandResult.Fail("Orders must use your player slot"));return handle;}
            if(lanRequests.Count>=LanProtocol.MaxPeerPending){handle.Resolve(CommandResult.Fail("Too many orders waiting for the host"));return handle;}
            var owned=ReplayRecorder.CopyCommand(command);owned.actors??=Array.Empty<int>();owned.item??="";owned.choice??="";
            if(owned.kind==CommandKind.CancelQueue&&expectedQueueId<=0&&owned.actors.Length==1)expectedQueueId=LanQueueId(owned.actors[0],owned.index);
            return QueueLanRequest(handle,LanOperation.Command,owned,expectedQueueId);
        }
        CommandRequest QueueLanRequest(CommandRequest handle,LanOperation operation,GameCommand command,long expectedQueueId=0)
        {
            if(lanRequests.Count>=LanProtocol.MaxPeerPending){handle.Resolve(CommandResult.Fail("Too many orders waiting for the host"));return handle;}
            handle.Sequence=++lanNextRequest;lanRequests.Add(handle.Sequence,handle);
            var request=new LanCommandRequest{peerId=lanPeerId,matchId=lanMatchId,epoch=lanEpoch,generation=lanGeneration,sequence=handle.Sequence,operation=operation,command=command??new GameCommand{player=LocalPlayerSlot},expectedQueueId=expectedQueueId};
            lanUnconfirmed.Add(handle.Sequence,request);
            if(IsLanHost)ReceiveLanAck(lanAuthority.Submit(request));else SendLan(lanClient,"command",request);
            if(!handle.IsTerminal)Notify("Order pending host confirmation");return handle;
        }
        public void SetLanSharedPause(bool pause)
        {
            if(!IsLan||World==null||InMenu||IsLanClient&&!IsLanConnected)return;
            QueueLanRequest(new CommandRequest(),pause?LanOperation.Pause:LanOperation.Resume,null);
        }
        void ReceiveLanAck(LanCommandAck ack)
        {
            if(ack==null||ack.matchId!=lanMatchId||ack.epoch!=lanEpoch||ack.peerId!=lanPeerId||ack.generation!=lanGeneration||!lanRequests.ContainsKey(ack.sequence))return;
            if(ack.terminal&&!lanAwaitingState.Exists(a=>a.sequence==ack.sequence))lanAwaitingState.Add(ack);
            ResolveLanAcks();
        }
        void ResolveLanAcks()
        {
            if(IsLanClient&&(!lanWelcomeReceived||lanAwaitingFull||lanClient?.Connected!=true))return;
            for(int i=0;i<lanAwaitingState.Count;)
            {
                var ack=lanAwaitingState[i];if(ack.stateRevision>LanStateRevision){i++;continue;}
                lanAwaitingState.RemoveAt(i);
                if(!lanRequests.TryGetValue(ack.sequence,out var request))continue;
                lanRequests.Remove(ack.sequence);lanUnconfirmed.TryGetValue(ack.sequence,out var sent);lanUnconfirmed.Remove(ack.sequence);lanConfirmedSequence=Math.Max(lanConfirmedSequence,ack.sequence);
                if(ack.accepted&&sent?.operation==LanOperation.Command)View.Pulse(sent.command.point,sent.command.target,sent.command.order==OrderKind.Attack||sent.command.order==OrderKind.AttackMove);
                Notify(ack.reason);request.Resolve(new CommandResult{accepted=ack.accepted,reason=ack.reason});LanCommandResolved?.Invoke(ack);
                Debug.Log("FRONTIER_LAN_RESULT peer="+ack.peerId+" sequence="+ack.sequence+" accepted="+ack.accepted+" step="+ack.appliedStep+" ordinal="+ack.ordinal+" revision="+ack.stateRevision);
            }
        }
        public void CloseLocalMenu()
        {LocalMenuPaused=false;PausedForFocusLoss=false;Controls.SuppressWorldInput();Audio.PauseAll(IsPaused);Hud.ShowPause(false);}
        void SetLanLocalMenu(bool paused,bool focusLoss=false)
        {
            LocalMenuPaused=paused;PausedForFocusLoss=paused&&focusLoss;
            if(focusLoss)Controls.ClearTransientInput();else Controls.SuppressWorldInput();
            if(paused)Controls.CancelTargetMode();Audio.PauseAll(IsPaused);Hud.ShowPause(paused);
        }
        public void LeaveLan(){ReturnToMenu();}
        void CloseLanNetwork()
        {
            lanLifetime++;lanConnectCancellation?.Cancel();lanConnectCancellation?.Dispose();lanConnectCancellation=null;
            if(lanConnectTask!=null){var abandoned=lanConnectTask;abandoned.ContinueWith(t=>{if(t.Status==TaskStatus.RanToCompletion)t.Result.Dispose();else if(t.IsFaulted){var observed=t.Exception;}},TaskScheduler.Default);lanConnectTask=null;}
            bool leaveSent=IsLanClient&&lanClient?.Connected==true&&SendLan(lanClient,"leave",new LanReady());
            if(leaveSent)lanClient.CloseAfterFlush("Local ally left");else lanClient?.Dispose();lanClient=null;lanRemote?.connection?.Dispose();lanRemote=null;
            foreach(var peer in lanCandidates)peer.connection.Dispose();lanCandidates.Clear();lanServer?.Dispose();lanServer=null;
            var pending=new List<CommandRequest>(lanRequests.Values);lanRequests.Clear();lanUnconfirmed.Clear();lanAwaitingState.Clear();
            lanRole=GameSessionRole.Single;LocalPlayerSlot=0;ObserverPlayer=0;LocalMenuPaused=false;LanLobby=null;LanStateRevision=0;LanConnectionText="";
            lanPeerId="";lanResumeToken="";lanMatchId="";lanEpoch="";lanGeneration=0;lanNextRequest=0;lanConfirmedSequence=0;lanPublished=null;lanAuthority=null;lanReplica=null;lanQueues=new LanQueueTracker();lanWelcomeReceived=false;lanAwaitingFull=false;lanDisconnectedAt=-1;lanForceFull=false;lanDirty=false;
            foreach(var item in pending)item.Resolve(CommandResult.Fail("Cooperative session closed"));
        }
        bool SendLan<T>(LanConnection connection,string kind,T payload,long nonce=0,long revision=0,string error="")
        {
            if(connection==null||!connection.Connected)return false;
            try{return connection.Send(LanWire.Encode(kind,payload,lanMatchId,lanEpoch,nonce,revision,error));}
            catch(Exception problem){connection.Dispose();Notify("Connection stopped: "+problem.Message);return false;}
        }
        public void PumpLan()
        {
            if(!IsLan)return;
            if(IsLanHost)PumpLanHost();else PumpLanClient();
            DrainLanAuthority();PublishLan();ResolveLanAcks();
        }
        void DrainLanAuthority()
        {
            if(IsLanHost&&lanAuthority!=null)
            {
                var results=lanAuthority.Drain(Simulation.StepCount,LanStateRevision+1);
                foreach(var ack in results)
                {
                    lanDirty=true;
                    Debug.Log("FRONTIER_LAN_APPLY peer="+ack.peerId+" sequence="+ack.sequence+" hostSequence="+ack.hostSequence+" accepted="+ack.accepted+" protocolRejected="+ack.protocolRejected+" step="+ack.appliedStep+" ordinal="+ack.ordinal+" revision="+ack.stateRevision);
                    if(ack.peerId==lanPeerId)ReceiveLanAck(ack);
                    else if(lanRemote?.connection?.Connected==true&&ack.peerId==lanRemote.id)SendLan(lanRemote.connection,"ack",ack);
                }
            }
        }
        void PumpLanHost()
        {
            while(lanServer.TryAccept(out var connection))lanCandidates.Add(new LanPeer{connection=connection,heard=LanNow});
            foreach(var peer in lanCandidates.ToArray())
            {
                if(!peer.connection.Connected||LanNow-peer.heard>LanProtocol.TimeoutSeconds){peer.connection.Dispose();lanCandidates.Remove(peer);continue;}
                if(!peer.connection.TryReceive(out var bytes))continue;
                try
                {
                    var packet=LanWire.Decode(bytes);if(packet.kind!="hello")throw new ArgumentException("Join handshake required");
                    var hello=LanWire.Body<LanHello>(packet);ReplayValidation.ValidateIdentity(Rules,hello.identity,CurrentReplayIdentity());
                    if(lanRemote!=null)
                    {
                        if(lanRemote.retired||hello.peerId!=lanRemote.id||hello.resumeToken!=lanRemote.token||hello.matchId!=lanMatchId||hello.epoch!=lanEpoch)
                            throw new ArgumentException("This cooperative room already has an ally");
                        lanRemote.connection?.Dispose();peer.id=lanRemote.id;peer.token=lanRemote.token;peer.generation=lanRemote.generation+1;
                    }
                    else{peer.id=Guid.NewGuid().ToString("N");peer.token=ResumeToken();peer.generation=1;}
                    lanRemote=peer;lanCandidates.Remove(peer);peer.heard=LanNow;LanLobby.slots[2].peerId=peer.id;
                    lanAuthority?.BindPeer(peer.id,2,peer.generation);
                    SendLan(peer.connection,"welcome",new LanWelcome{peerId=peer.id,resumeToken=peer.token,player=2,generation=peer.generation,matchId=lanMatchId,epoch=lanEpoch,identity=CurrentReplayIdentity(),lobby=LanLobby});
                    if(LanLobby.started){lanForceFull=true;LanConnectionText="Cooperating with P3";}else BroadcastLanLobby();
                }
                catch(Exception error){SendLan(peer.connection,"error",new LanReady(),error:error.Message);peer.connection.CloseAfterFlush("Handshake rejected");lanCandidates.Remove(peer);}
            }
            if(lanRemote==null)return;
            int messages=0;
            while(messages++<LanProtocol.MaxMessagesPerPump&&!lanRemote.closing&&lanRemote.connection?.TryReceive(out var bytes)==true)
            {
                try
                {
                    var packet=LanWire.Decode(bytes);if(packet.matchId!=lanMatchId||packet.epoch!=lanEpoch)throw new ArgumentException("Expired cooperative session");
                    lanRemote.heard=LanNow;
                    if(packet.kind=="ping")SendLan(lanRemote.connection,"pong",new LanReady(),packet.nonce);
                    else if(packet.kind=="pong"){if(packet.nonce==lanPingNonce)LanRttMilliseconds=(LanNow-lanPingSent)*1000;}
                    else if(packet.kind=="ready")
                    {if(LanLobby.started)throw new ArgumentException("The match roster is frozen");InstallLanReady(LanLobby.slots[2],LanWire.Body<LanReady>(packet));BroadcastLanLobby();}
                    else if(packet.kind=="command")
                    {
                        if(lanAuthority==null)throw new ArgumentException("Match has not started");
                        if(System.Text.Encoding.UTF8.GetByteCount(packet.payload)>LanProtocol.MaxCommandBytes)throw new ArgumentException("LAN command byte limit exceeded");
                        var request=LanWire.Body<LanCommandRequest>(packet);
                        if(request.peerId!=lanRemote.id||request.generation!=lanRemote.generation)throw new ArgumentException("Connection does not own this player slot");
                        SendLan(lanRemote.connection,"ack",lanAuthority.Submit(request));
                    }
                    else if(packet.kind=="resync")lanForceFull=true;
                    else if(packet.kind=="leave"){lanRemote.closing=true;lanRemote.connection.Dispose();lanRemote.disconnectedAt=LanNow-LanProtocol.ReconnectSeconds;}
                    else throw new ArgumentException("Unsupported client message");
                }
                catch(Exception error){SendLan(lanRemote.connection,"error",new LanReady(),error:error.Message);lanRemote.closing=true;lanRemote.connection.CloseAfterFlush("Protocol rejected");break;}
            }
            if(lanRemote.connection.Connected&&LanNow-lanRemote.heard>LanProtocol.TimeoutSeconds)lanRemote.connection.Dispose();
            if(!lanRemote.connection.Connected)
            {
                if(lanRemote.disconnectedAt<0)lanRemote.disconnectedAt=LanNow;
                LanConnectionText=lanRemote.retired?"Ally left the cooperative session":"Ally disconnected · waiting to reconnect";
                if(!lanRemote.retired&&LanNow-lanRemote.disconnectedAt>=LanProtocol.ReconnectSeconds)
                {
                    lanRemote.retired=true;LanLobby.slots[2].ready=false;
                    if(lanAuthority!=null){if(World.Paused)lanAuthority.SubmitSystem(LanOperation.Resume);lanAuthority.SubmitSystem(LanOperation.Command,new GameCommand{player=2,kind=CommandKind.Resign});lanDirty=true;}
                    LanConnectionText="Ally left the cooperative session";
                }
            }
            else if(LanNow-lanLastPing>=LanProtocol.HeartbeatSeconds)
            {lanLastPing=LanNow;lanPingSent=LanNow;lanPingNonce++;SendLan(lanRemote.connection,"ping",new LanReady(),lanPingNonce);}
        }
        void PumpLanClient()
        {
            if(lanConnectTask?.IsCompleted==true)
            {
                var task=lanConnectTask;lanConnectTask=null;
                if(task.IsFaulted||task.IsCanceled){LanConnectionText="Host unavailable · reconnecting";var ignored=task.Exception;}
                else
                {
                    lanClient=task.Result;lanLastHeard=LanNow;lanWelcomeReceived=false;
                    SendLan(lanClient,"hello",new LanHello{identity=CurrentReplayIdentity(),peerId=lanPeerId,resumeToken=lanResumeToken,matchId=lanMatchId,epoch=lanEpoch,lastConfirmedSequence=lanConfirmedSequence,stateRevision=LanStateRevision});
                }
            }
            int messages=0;
            while(messages++<LanProtocol.MaxMessagesPerPump&&lanClient?.TryReceive(out var bytes)==true)
            {
                try
                {
                    var packet=LanWire.Decode(bytes);lanLastHeard=LanNow;
                    if(packet.kind=="welcome")
                    {
                        var welcome=LanWire.Body<LanWelcome>(packet);ReplayValidation.ValidateIdentity(Rules,welcome.identity,CurrentReplayIdentity());
                        if(welcome.player!=2||welcome.generation<1||string.IsNullOrEmpty(welcome.peerId)||string.IsNullOrEmpty(welcome.resumeToken)||welcome.resumeToken.Length>128||string.IsNullOrEmpty(welcome.matchId)||string.IsNullOrEmpty(welcome.epoch)||welcome.lobby?.slots?.Length!=4)
                            throw new ArgumentException("Invalid cooperative welcome");
                        if(lanPeerId!=""&&(lanPeerId!=welcome.peerId||lanMatchId!=welcome.matchId||lanEpoch!=welcome.epoch))throw new ArgumentException("Host changed the cooperative session");
                        lanPeerId=welcome.peerId;lanResumeToken=welcome.resumeToken;lanGeneration=welcome.generation;lanMatchId=welcome.matchId;lanEpoch=welcome.epoch;
                        InstallReceivedLanLobby(welcome.lobby);lanWelcomeReceived=true;lanAwaitingFull=LanLobby.started;lanDisconnectedAt=-1;LanConnectionText=LanLobby.started?"Synchronizing with host":"Connected · choose your civilization and ready up";RefreshLanLobbyConnectionText();
                        var retries=new List<LanCommandRequest>(lanUnconfirmed.Values);retries.Sort((a,b)=>a.sequence.CompareTo(b.sequence));
                        foreach(var request in retries){request.generation=lanGeneration;SendLan(lanClient,"command",request);}if(InMenu)Hud.ShowLanLobby();
                        continue;
                    }
                    if(packet.kind=="error")throw new ArgumentException(packet.error);
                    if(!lanWelcomeReceived||packet.matchId!=lanMatchId||packet.epoch!=lanEpoch)throw new ArgumentException("Expired host state");
                    if(packet.kind=="ping")SendLan(lanClient,"pong",new LanReady(),packet.nonce);
                    else if(packet.kind=="pong"){if(packet.nonce==lanPingNonce)LanRttMilliseconds=(LanNow-lanPingSent)*1000;}
                    else if(packet.kind=="lobby"){InstallReceivedLanLobby(LanWire.Body<LanLobbyState>(packet));RefreshLanLobbyConnectionText();if(InMenu)Hud.RefreshLanLobbyState();}
                    else if(packet.kind=="full")
                    {
                        var state=LanWire.Body<LanFullState>(packet);if(state.matchId!=lanMatchId||state.epoch!=lanEpoch)throw new ArgumentException("Full state changed session");
                        if(state.world==null||state.world.hasScenarioDocument!=LanLobby.hasScenarioDocument||state.world.scenarioHash!=LanLobby.scenarioHash)throw new ArgumentException("Full state changed the cooperative scenario");
                        if(lanReplica!=null&&!lanReplica.NeedsFullState&&!lanAwaitingFull&&state.revision==lanReplica.Revision)continue;
                        var oldWorld=World;lanReplica??=new LanReplica(Rules);lanReplica.Install(state);lanAwaitingFull=false;World=lanReplica.World;Simulation=null;Guided=null;Recorder?.Dispose();Recorder=null;
                        MatchId=lanMatchId;ObserverPlayer=LocalPlayerSlot;InMenu=false;LanStateRevision=lanReplica.Revision;if(oldWorld==null){LocalMenuPaused=false;PausedForFocusLoss=false;}
                        ResetLanPresentation(!ReferenceEquals(oldWorld,World));LanConnectionText="Cooperating as P3";ResolveLanAcks();
                    }
                    else if(packet.kind=="delta")
                    {
                        if(lanReplica==null||lanAwaitingFull){SendLan(lanClient,"resync",new LanReady());continue;}
                        var delta=LanWire.Body<LanStateDelta>(packet);int before=World.Events.Count;
                        if(lanReplica.Apply(delta))
                        {
                            audioCursor=Math.Max(0,audioCursor-Math.Max(0,before+delta.events.Length-World.Events.Count));LanStateRevision=lanReplica.Revision;Audio.PauseAll(IsPaused);ResolveLanAcks();
                        }
                        else if(lanReplica.NeedsFullState){LanConnectionText="Resynchronizing with host";lanAwaitingFull=true;Controls.CancelTargetMode();SendLan(lanClient,"resync",new LanReady());}
                    }
                    else if(packet.kind=="ack")ReceiveLanAck(LanWire.Body<LanCommandAck>(packet));
                    else throw new ArgumentException("Unsupported host message");
                }
                catch(Exception error){LanConnectionText="Connection error: "+error.Message;Notify(LanConnectionText);lanClient.Dispose();lanWelcomeReceived=false;lanNextConnect=LanNow+1;}
            }
            if(lanClient?.Connected==true&&LanNow-lanLastHeard>LanProtocol.TimeoutSeconds)lanClient.Dispose();
            if(lanClient?.Connected!=true)
            {
                lanWelcomeReceived=false;lanAwaitingFull=World!=null;if(lanDisconnectedAt<0){lanDisconnectedAt=LanNow;LanConnectionText="Host disconnected · reconnecting";}Controls.CancelTargetMode();Audio.PauseAll(true);
                if(LanNow-lanDisconnectedAt<LanProtocol.ReconnectSeconds){if(lanConnectTask==null&&LanNow>=lanNextConnect)BeginLanConnect();}
                else{LanConnectionText="Host connection lost · leave this session to start another game";lanConnectCancellation?.Cancel();}
            }
            else if(lanWelcomeReceived&&LanNow-lanLastPing>=LanProtocol.HeartbeatSeconds)
            {lanLastPing=LanNow;lanPingSent=LanNow;lanPingNonce++;SendLan(lanClient,"ping",new LanReady(),lanPingNonce);}
        }
        void PublishLan()
        {
            if(!IsLanHost||World==null||Simulation==null||LanLobby?.started!=true)return;
            if(!lanForceFull&&!lanDirty&&LanNow-lanLastPublish<.2)return;
            try
            {
                var next=LanProjection.Full(World,lanMatchId,lanEpoch,LanStateRevision+1,Simulation.StepCount,lanQueues.Capture(World));
                if(lanRemote?.connection?.Connected==true)
                {
                    if(lanPublished==null||lanForceFull)SendLan(lanRemote.connection,"full",next);
                    else
                    {
                        try{SendLan(lanRemote.connection,"delta",LanProjection.Delta(lanPublished,next,World.Events));}
                        catch(InvalidOperationException){SendLan(lanRemote.connection,"full",next);}
                    }
                }
                lanPublished=next;LanStateRevision=next.revision;lanLastPublish=LanNow;lanDirty=false;lanForceFull=false;ResolveLanAcks();
            }
            catch(Exception error){LanConnectionText="State synchronization failed: "+error.Message;Notify(LanConnectionText);lanRemote?.connection?.Dispose();}
        }
    }
}
