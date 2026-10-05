using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Frontier
{
    public sealed partial class GameController : MonoBehaviour
    {
        public World World { get; private set; }
        public RuleCatalog Rules { get; private set; }
        public WorldView View { get; private set; }
        public RtsInput Controls { get; private set; }
        public FrontierHud Hud { get; private set; }
        public NativeCapture Captures { get; private set; }
        public string MatchId { get; private set; } = "";
        public EventAudio Audio { get; private set; }
        public AmbientAudio Ambience { get; private set; }
        public bool EnvironmentSoundEnabled {get=>Options?.environmentEnabled??true;set=>ChangePreference(s=>s.environmentEnabled=value);}
        public bool MusicEnabled {get=>Options?.musicEnabled??true;set=>ChangePreference(s=>s.musicEnabled=value);}
        public Camera GameCamera { get; private set; }
        public bool InMenu { get; private set; } = true;
        public bool IsPaused => IsWatching ? ReplayPaused : LocalMenuPaused || World != null && World.Paused;
        public bool PausedForFocusLoss { get; private set; }
        public string Notice = "";
        public string DeckPreset = "economy";
        public string PlayerCivilization="coast",OpponentCivilization="river";
        public int AgeLimit=5,MatchSeed=314159;
        public string OpponentDifficulty="standard";
        public string SelectedMap=MapTopology.Frontier;
        public string SelectedObjective="elimination";
        public string MapName=>selectedScenario069?.name??MapTopology.Name(SelectedMap);
        public void CycleMap()
        {
            SelectAuthoredScenario(null);
            int current=Array.IndexOf(MapTopology.All,SelectedMap);
            SelectedMap=MapTopology.All[(current+1)%MapTopology.All.Length];
        }
        public string DifficultyReason => AiDifficultyProfile.InvalidReason(Rules.Difficulty(OpponentDifficulty));
        public void CycleDifficulty()
        {
            var options=Rules.aiDifficulties;if(options==null||options.Length==0)return;
            int current=Array.FindIndex(options,d=>d!=null&&d.id==OpponentDifficulty);
            OpponentDifficulty=options[(current+1)%options.Length]?.id??"";
        }
        private readonly Dictionary<string,List<string>> configuredDecks=new Dictionary<string,List<string>>();
        public List<string> SelectedDeck
        {
            get{if(!configuredDecks.TryGetValue(PlayerCivilization,out var deck)){deck=DeckLibrary.Preset(Rules,PlayerCivilization,DeckPreset);configuredDecks[PlayerCivilization]=deck;}return deck;}
        }
        public void ResetDeckPreset(){configuredDecks[PlayerCivilization]=DeckLibrary.Preset(Rules,PlayerCivilization,DeckPreset);}
        public void ToggleDeckCard(string id)
        {
            var deck=SelectedDeck;if(deck.Contains(id))deck.Remove(id);else if(deck.Count<Rules.deckCapacity)deck.Add(id);else Notify("Deck full: remove a card first");
        }
        public bool Autoplay;
        private bool muted;
        public bool Muted
        {
            get=>Options?.muted??muted;
            set{if(Settings==null){muted=value;Audio?.SetMuted(value);}else ChangePreference(s=>s.muted=value);}
        }
        public float SimulationSpeed=1;
        public bool BenchmarkMode;
        public float LastSimulationFrameMs;
        public MatchSimulation Simulation { get; private set; }
        private string reportDirectoryOverride=null;
        private string saveDirectoryOverride=null;
        private string dataDirectoryOverride;
        public string CaptureDirectory=>Path.Combine(dataDirectoryOverride??Application.persistentDataPath,"Captures");
        public string SaveDirectory=>saveDirectoryOverride??Path.Combine(Application.persistentDataPath,"Saves");
        public string SaveSlotLabel(int slot)
        {
            string path=MatchSaveStore.SlotPath(SaveDirectory,slot);if(!File.Exists(path))return "SLOT "+slot+" · EMPTY";
            try{var save=MatchSaveStore.Read(path);if(save.appVersion!=Application.version)return "SLOT "+slot+" · REQUIRES GAME "+save.appVersion;return "SLOT "+slot+" · "+MapTopology.Name(save.world.mapId)+" · "+((int)save.world.time/60)+":"+((int)save.world.time%60).ToString("00");}
            catch(Exception){return "SLOT "+slot+" · UNREADABLE";}
        }
        public bool SaveMatch(int slot)
        {
            if(IsLan){Notify("Cooperative matches are recorded by the host; checkpoints are available in solo games");return false;}
            if(IsWatching){Notify("Viewing a recording does not create match checkpoints");return false;}
            if(World==null||Simulation==null||BenchmarkMode){Notify("Start a normal match before saving");return false;}
            try
            {
                MatchSaveStore.Write(MatchSaveStore.SlotPath(SaveDirectory,slot),new SavedMatch{matchId=MatchId,appVersion=Application.version,
                    world=World.CaptureState(),simulation=Simulation.CaptureState(),cameraPosition=GameCamera.transform.position,cameraSize=GameCamera.orthographicSize,
                    cameraFocus=Controls.CameraFocus,selection=Controls.Selected.ToArray(),groups=Controls.CaptureGroups(),
                    muted=Muted,environmentSound=EnvironmentSoundEnabled,music=MusicEnabled,autoplay=Autoplay,simulationSpeed=SimulationSpeed,
                    guided=Guided?.CaptureState()});
                Notify("Saved to slot "+slot);Debug.Log("FRONTIER_SAVE slot="+slot+" match="+MatchId+" time="+World.Time);return true;
            }
            catch(Exception error){Notify("Save failed: "+error.Message);Debug.LogWarning("FRONTIER_SAVE_FAILED "+error.Message);return false;}
        }
        public bool LoadMatch(int slot)
        {
            if(IsLan){Notify("Leave the cooperative session before loading a checkpoint");return false;}
            try
            {
                var saved=MatchSaveStore.Read(MatchSaveStore.SlotPath(SaveDirectory,slot),Application.version);
                if(saved.guided!=null&&saved.guided.attemptId!=saved.matchId)throw new InvalidDataException("Challenge attempt does not match this checkpoint");
                var restored=Frontier.World.Restore(Rules,saved.world);var simulation=MatchSimulation.Restore(restored,saved.simulation);
                var guided=saved.guided==null?null:GuidedPlaySession.Restore(restored,simulation,saved.guided);
                // Validate and reconstruct before replacing the current game.
                if(World!=null)SaveReport("load_exit");Guided?.Dispose();ReleasePlayback();Guided=guided;progressRecordedAttempt="";Settings.Captions.Clear();ClearBenchmark();Audio.ResetMatch();World=restored;Simulation=simulation;MatchId=saved.matchId;
                PlayerCivilization=World.Players[0].civilization;OpponentCivilization=World.Players[1].civilization;AgeLimit=World.MaxAge;
                SelectedMap=World.MapId;SelectedObjective=World.MapObjective.mode;MatchSeed=World.Seed;OpponentDifficulty=Simulation.Difficulty.Id;
                configuredDecks[PlayerCivilization]=new List<string>(World.Players[0].deck);
                RestoreRosterConfiguration();SyncScenarioSelectionFromWorld();Autoplay=Guided==null&&saved.autoplay;SimulationSpeed=saved.simulationSpeed;
                InMenu=false;PausedForFocusLoss=false;audioCursor=World.Events.Count;frameTimes.Clear();nextTelemetry=World.Time+60;LastSimulationFrameMs=0;
                View.ResetWorld();Controls.ResetSelection();Controls.RestoreCamera(saved.cameraFocus,saved.cameraSize);Controls.RestoreSelection(saved.selection);Controls.RestoreGroups(saved.groups);
                Controls.ClearTransientInput();Hud.ShowGame();World.Paused=true;Audio.PauseAll(true);Hud.ShowPause(true);
                BeginRecording("loaded_checkpoint");
                Notify("Loaded slot "+slot+" · resume when ready");Debug.Log("FRONTIER_LOAD slot="+slot+" match="+MatchId+" time="+World.Time);return true;
            }
            catch(Exception error){Notify("Load failed: "+error.Message);Debug.LogWarning("FRONTIER_LOAD_FAILED "+error.Message);return false;}
        }
        private float noticeUntil;
        private int audioCursor;
        private float nextTelemetry;
        private readonly List<float> frameTimes=new List<float>(36000);
        private string lastReport;
        public static readonly string[] AgeNames={"", "I · FRONTIER", "II · COMMERCE", "III · FORTRESS", "IV · INDUSTRY", "V · SOVEREIGNTY"};

        private void Awake()
        {
            Application.targetFrameRate=60;
            QualitySettings.vSyncCount=0;
            Rules=Catalog.Load();
            InitializeExpansionPreferences();
            GameCamera=Camera.main;
            if(GameCamera==null) {var go=new GameObject("Strategy Camera");GameCamera=go.AddComponent<Camera>();go.tag="MainCamera";go.AddComponent<AudioListener>();go.transform.SetParent(transform,false);}
            GameCamera.orthographic=true; GameCamera.orthographicSize=26;
            GameCamera.nearClipPlane=.2f; GameCamera.farClipPlane=260;
            GameCamera.clearFlags=CameraClearFlags.SolidColor;GameCamera.backgroundColor=new Color(.055f,.095f,.12f);
            View=gameObject.AddComponent<WorldView>();View.Initialize(this);
            Controls=gameObject.AddComponent<RtsInput>();Controls.Initialize(this);
            Hud=gameObject.AddComponent<FrontierHud>();Hud.Initialize(this);
            Captures=gameObject.AddComponent<NativeCapture>();Captures.Initialize(this);
            Audio=gameObject.AddComponent<EventAudio>();Audio.Initialize(this);
            var ambienceRoot=new GameObject("Original ambience");ambienceRoot.transform.SetParent(transform,false);
            Ambience=ambienceRoot.AddComponent<AmbientAudio>();Ambience.Initialize(this);
            var audioCapture=GameCamera.GetComponent<NativeAudioCapture>()??GameCamera.gameObject.AddComponent<NativeAudioCapture>();
            audioCapture.Initialize(this);
            gameObject.AddComponent<NativePerformanceCapture>().Initialize(this);
            Controls.Focus(new Vector2(25,25));
        }
        private void Start()
        {
            var args=Environment.GetCommandLineArgs();
            string Arg(string key){int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:null;}
            if(MapTopology.IsValid(Arg("-frontierMap")))SelectedMap=Arg("-frontierMap");
            if(Rules.Civ(Arg("-frontierPlayerCiv"))!=null)PlayerCivilization=Arg("-frontierPlayerCiv");
            if(Rules.Civ(Arg("-frontierOpponentCiv"))!=null)OpponentCivilization=Arg("-frontierOpponentCiv");
            if(Rules.Civ(Arg("-frontierAllyCiv"))!=null)AllyCivilization=Arg("-frontierAllyCiv");
            if(Rules.Civ(Arg("-frontierThirdCiv"))!=null)ThirdCivilization=Arg("-frontierThirdCiv");
            if(int.TryParse(Arg("-frontierSeed"),out int seed))MatchSeed=seed;
            if(Arg("-frontierObjective")!=null)SelectedObjective=Arg("-frontierObjective");
            if(ValidSkirmishMode(Arg("-frontierFormat")))SkirmishMode=Arg("-frontierFormat");
            if(int.TryParse(Arg("-frontierMaxAge"),out int ages))AgeLimit=Mathf.Clamp(ages,1,Rules.ages.Length);
            if(Arg("-frontierReplayDirectory")!=null)replayDirectoryOverride=Path.GetFullPath(Arg("-frontierReplayDirectory"));
            if(Arg("-frontierReportDirectory")!=null)reportDirectoryOverride=Path.GetFullPath(Arg("-frontierReportDirectory"));
            if(Arg("-frontierScenarioPath")!=null)
            {try{SelectAuthoredScenario(Scenarios.ReadFile(Arg("-frontierScenarioPath")));}catch(Exception error){Notify("Scenario could not load: "+error.Message);}}
            if(Array.IndexOf(args,"-frontierScenarioEditor")>=0){Hud.ShowMenu();if(selectedScenario069!=null)OpenScenarioDocument(selectedScenario069);else OpenScenarioEditor();}
            else if(int.TryParse(Arg("-frontierLanHost"),out int lanPort)){HostLan(lanPort);}
            else if(Arg("-frontierLanJoin")!=null){JoinLan(Arg("-frontierLanJoin"),int.TryParse(Arg("-frontierLanPort"),out int joinPort)?joinPort:47621);}
            else if(Arg("-frontierReplay")!=null){Hud.ShowMenu();OpenReplayAsync(Path.GetFullPath(Arg("-frontierReplay")));}
            else if(Array.IndexOf(args,"-frontierAutoplay")>=0) {Autoplay=true;int index=Array.IndexOf(args,"-frontierAutoSpeed");if(index>=0&&index+1<args.Length&&float.TryParse(args[index+1],out float speed))SimulationSpeed=Mathf.Clamp(speed,1,16);StartMatch();}
            else if(GuidedPlayCatalog.Find(Arg("-frontierLesson"))!=null)StartGuided(Arg("-frontierLesson"));
            else Hud.ShowMenu();
            int benchmarkIndex=Array.IndexOf(args,"-frontierBenchmark");
            if(benchmarkIndex>=0&&benchmarkIndex+1<args.Length&&int.TryParse(args[benchmarkIndex+1],out int count))
            {
                float duration=25;
                if(float.TryParse(Arg("-frontierBenchmarkSeconds"),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float seconds)&&!float.IsNaN(seconds)&&!float.IsInfinity(seconds))duration=seconds;
                BenchmarkMode=true;if(!MapTopology.IsValid(Arg("-frontierMap")))SelectedMap=MapTopology.Frontier;StartMatch();gameObject.AddComponent<RuntimeBenchmark>().Initialize(this,count,duration);
            }
        }
        public void StartMatch()
        {
            if(IsLan){Notify("Use the cooperative lobby to start this match");return;}
            if(IsScenarioPlaytest&&ScenarioEditor!=null){StartAuthoredScenario(ScenarioEditor.Document,true);return;}
            if(selectedScenario069!=null){StartAuthoredScenario(selectedScenario069,false);return;}
            IsScenarioPlaytest=false;ScenarioEditorOpen=false;
            string deckError=DeckLibrary.Validate(Rules,PlayerCivilization,SelectedDeck);
            if(deckError!=""){Notify(deckError);Hud.ShowMenu();return;}
            if(DifficultyReason!=""){Notify(DifficultyReason);Hud.ShowMenu();return;}
            if(RosterReason!=""){Notify(RosterReason);Hud.ShowMenu();return;}
            // A menu restart ends an already running diagnostic fixture. The CLI
            // creates its fixture after StartMatch, so its initial mode is retained.
            if(GetComponent<RuntimeBenchmark>()!=null)ClearBenchmark();
            if(World!=null)SaveReport("restart_exit");Guided?.Dispose();ReleasePlayback();Guided=null;progressRecordedAttempt="";Settings.Captions.Clear();
            PausedForFocusLoss=false;audioCursor=0;frameTimes.Clear();nextTelemetry=0;Audio.ResetMatch();
            World=new World(Rules,MatchSeed,RosterCivilizations(),RosterTeams(),AgeLimit,SelectedMap);
            var objective=World.ConfigureMapObjective(SelectedObjective);
            if(!objective.accepted){Notify(objective.reason);World=null;InMenu=true;Hud.ShowMenu();return;}
            MatchId=Guid.NewGuid().ToString("N");
            for(int player=0;player<World.Players.Length;player++)SetDeck(player,player==0?DeckPreset:"military");
            Simulation=new MatchSimulation(World,AutomatedSlots(World.Players.Length),OpponentDifficulty);
            InMenu=false;Notice="Explore, gather, build. Earn XP and choose a shipment with H.";noticeUntil=12;
            View.ResetWorld();Controls.ResetSelection();Controls.Focus(World.OpeningTownPosition(0));
            Hud.ShowGame();
            BeginRecording(Autoplay?"autoplay":"normal");
        }
        private void SetDeck(int player,string preset)
        {
            var p=World.Players[player];var deck=player==0?SelectedDeck:DeckLibrary.Preset(Rules,p.civilization,preset);
            if(DeckLibrary.Validate(Rules,p.civilization,deck)!="")deck=DeckLibrary.Preset(Rules,p.civilization,preset);
            p.deck.Clear();p.deck.AddRange(deck);
        }
        public CommandResult Command(GameCommand c)
        {
            if(IsLan)return CommandResult.Fail("Cooperative orders require host confirmation");
            if(InMenu||World==null)return CommandResult.Fail("No active match");
            if(IsWatching)return CommandResult.Fail("Viewing a recording does not issue game orders");
            var r=Recorder!=null?Recorder.Issue(c):Guided==null?World.Issue(c):Guided.Issue(c);
            Notify(r.reason);if(r.accepted)View.Pulse(c.point,c.target,c.order==OrderKind.Attack||c.order==OrderKind.AttackMove);
            return r;
        }
        public void Notify(string text) {Notice=text;noticeUntil=Time.unscaledTime+6;Settings?.Captions.Hint(text);}
        private void OnApplicationFocus(bool focused)
        {
            // Batch verification has no foreground player. Interactive autoplay
            // and benchmark exclusions are handled by the same session policy.
            if(!focused&&!Application.isBatchMode)PauseForFocusLoss();
        }
        public void PauseForFocusLoss()
        {
            if(IsLan){if(!InMenu&&World!=null&&!LocalMenuPaused)SetLanLocalMenu(true,true);return;}
            if(IsWatching){if(!ReplayPaused){ReplayPaused=true;Audio.PauseAll(true);Controls.ClearTransientInput();}return;}
            if(InMenu||World==null||World.IsMatchEnded||Guided!=null&&Guided.Status!=GuidedPlayStatus.Running||World.Paused||Autoplay||BenchmarkMode)return;
            SetPaused(true,"focus_lost");
        }
        public void TogglePause()
        {
            if(InMenu||World==null)return;
            if(IsLan){SetLanLocalMenu(!LocalMenuPaused);return;}
            if(IsWatching){ToggleReplayPlayback();Hud.ShowPause(ReplayPaused);return;}
            if(Guided!=null&&Guided.Status!=GuidedPlayStatus.Running){Hud.ShowGuidedResult();return;}
            if(World.IsMatchEnded){Hud.ShowResult();return;}
            SetPaused(!World.Paused,World.Paused?"manual_resume":"manual_pause");
        }
        private void SetPaused(bool paused,string reason)
        {
            if(Recorder!=null)Recorder.Pause(paused,reason);
            else {World.Paused=paused;World.Emit("PauseChanged",0,0,reason,paused?1:0);}
            PausedForFocusLoss=paused&&reason=="focus_lost";
            // Focus loss invalidates the old device stream. An explicit pause
            // button/ESC transition retains later fresh keyboard edges in FIFO.
            if(reason=="focus_lost")Controls.ClearTransientInput();else Controls.SuppressWorldInput();
            if(paused)Controls.CancelTargetMode();
            Audio.PauseAll(paused);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,null,
                "FRONTIER_PAUSE paused={0} reason={1} world={2:F6} realtime={3:F6} frame={4}",
                paused,reason,World.Time,Time.realtimeSinceStartupAsDouble,Time.frameCount);
            Hud.ShowPause(paused);
        }
        public void ReturnToMenu()
        {
            bool reopenEditor=RestoreScenarioEditorAfterPlaytest();
            if(World!=null)SaveReport("menu_exit");
            Controls.CancelTargetMode();Controls.ClearTransientInput();Hud.InvalidateActions();
            Guided?.Dispose();Guided=null;ReleasePlayback();Recorder?.Dispose();Recorder=null;Settings.Captions.Clear();ClearBenchmark();Audio.ResetMatch();World=null;Simulation=null;InMenu=true;Autoplay=false;SimulationSpeed=1;LastSimulationFrameMs=0;
            MatchId="";
            CloseLanNetwork();
            PausedForFocusLoss=false;Controls.ResetSelection();View.ResetWorld();if(reopenEditor)Hud.ShowScenarioEditor();else Hud.ShowMenu();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        private void ClearBenchmark()
        {
            BenchmarkMode=false;
            foreach(var fixture in GetComponents<RuntimeBenchmark>()){fixture.enabled=false;Destroy(fixture);}
        }
        public void Resign()
        {
            if(IsWatching||InMenu||World==null||World.IsMatchEnded)return;
            if(IsLan){if(World.Paused)SetLanSharedPause(false);SubmitCommand(new GameCommand{player=LocalPlayerSlot,kind=CommandKind.Resign});return;}
            if(IsPaused)SetPaused(false,"resign");Command(new GameCommand{player=LocalPlayerSlot,kind=CommandKind.Resign});
        }
        private void Update()
        {
            Settings.Captions.Tick(Time.unscaledTime);
            PollReplayOperations();
            PumpLan();
            if(InMenu||World==null)return;
            if(IsWatching)AdvanceReplayFrame();else ObserveGuidedSelection();
            if(!IsWatching&&!IsLanClient&&!World.Paused&&!World.IsMatchEnded)
            {
                Simulation.AdvanceFrame(Time.unscaledDeltaTime*(Autoplay?SimulationSpeed:1));
                LastSimulationFrameMs=Simulation.LastFrameMs;
                if(Simulation.CatchingUp)Notify("Simulation is catching up — rendering load is high.");
                frameTimes.Add(Time.unscaledDeltaTime*1000);
            }
            PublishLan();
            while(audioCursor<World.Events.Count)
            {
                var ev=World.Events[audioCursor++];Settings.Captions.OnEvent(World,ObserverPlayer,ev);Audio.OnEvent(ev);
                if(ev.player==ObserverPlayer && (ev.kind=="ShipmentArrived"||ev.kind=="AgeAdvanced"||ev.kind=="ResourceDepleted"||ev.kind=="TreasureClaimed"))Notify(ev.message);
            }
            Audio.TickActivity();
            if(!IsWatching&&Guided!=null)
            {
                if(World.IsMatchEnded&&Guided.Status==GuidedPlayStatus.Running)Guided.Tick();
                if(Guided.Status!=GuidedPlayStatus.Running&&!Hud.ResultVisible){SaveLearningProgress();SaveReport("guided_finished");Hud.ShowGuidedResult();}
            }
            else if(!IsWatching&&World.IsMatchEnded&&!Hud.ResultVisible){SaveReport("match_finished");Hud.ShowResult();}
            if(Time.unscaledTime>noticeUntil)Notice="";
            if(Autoplay&&World.Time>=nextTelemetry)
            {nextTelemetry=World.Time+60;Debug.Log($"FRONTIER_PROGRESS t={World.Time:0} entities={World.Entities.Count} age={World.Players[0].age}/{World.Players[1].age} winner={World.Winner}");}
        }
        public void SaveReport(string reason)
        {
            if(World==null||IsWatching||IsLanClient)return;
            if(Recorder!=null)Recorder.FlushEconomy();else World.FlushEconomyLog();
            if(EndsRecording(reason))CloseRecording(reason);
            try{WriteReport(reason);}
            catch(Exception error){Notify("Match report could not be written: "+error.Message);Debug.LogWarning("FRONTIER_REPORT_FAILED "+error.Message);}
        }
        private void WriteReport(string reason)
        {
            string dir=(reportDirectoryOverride??Path.Combine(Application.persistentDataPath,"Reports"));Directory.CreateDirectory(dir);
            string stem=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+MatchId+"-"+reason;
            lastReport=Path.Combine(dir,stem+".json");
            var samples=new List<float>(frameTimes);samples.Sort();
            float mean=0;foreach(float f in samples)mean+=f;if(samples.Count>0)mean/=samples.Count;
            var report=new MatchReport{mapId=World.MapId,aiDifficulty=Simulation?.Difficulty.Id,aiDecisionSeconds=Simulation?.Difficulty.DecisionSeconds??0,aiArmySeconds=Simulation?.Difficulty.ArmySeconds??0,matchId=MatchId,eventSchemaVersion=SimEvent.CurrentSchemaVersion,appVersion=Application.version,buildGuid=Application.buildGUID,maxAge=World.MaxAge,version=Application.unityVersion,platform=Application.platform.ToString(),device=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,resolution=Screen.width+"x"+Screen.height,reason=reason,seed=World.Seed,time=World.Time,winner=World.Winner,autoplay=Autoplay,simulationSpeed=Autoplay?SimulationSpeed:1,meanFrameMs=mean,p95FrameMs=samples.Count>0?samples[(int)((samples.Count-1)*.95f)]:0,managedBytes=GC.GetTotalMemory(false),eventCount=World.Events.Count,players=new PlayerReport[World.Players.Length],guided=Guided?.CaptureState()};
            for(int i=0;i<World.Players.Length;i++){var p=World.Players[i];report.players[i]=new PlayerReport{player=i,team=p.team,eliminated=p.eliminated,resigned=p.resigned,civilization=p.civilization,age=p.age,gathered=p.gathered,spent=p.spent,kills=p.kills,losses=p.losses,shipments=p.shipsSent,population=p.usedPopulation,passiveIncome=p.passiveIncome,tradeIncome=p.tradeIncome,treasures=p.treasuresClaimed,guardians=p.guardiansDefeated,resources=p.resources,refunded=p.refunded,ageHistory=p.ageHistory.ToArray()};}
            File.WriteAllText(lastReport,JsonUtility.ToJson(report,true));
            using(var writer=new StreamWriter(Path.Combine(dir,stem+"-events.jsonl")))foreach(var ev in World.Events)writer.WriteLine(JsonUtility.ToJson(ev));
            Debug.Log("FRONTIER_REPORT "+lastReport);
        }
        public void Capture()
        {
            Captures.Request();
        }
        private void OnApplicationQuit(){try{if(!PersistScenarioDraft())Debug.LogWarning("FRONTIER_SCENARIO_DRAFT_SAVE_FAILED");SaveReport("quit");}catch(Exception error){Debug.LogWarning("FRONTIER_REPORT_FAILED "+error.Message);}finally{FinishReplayFileWrites();CloseLanNetwork();}}
        [Serializable]private class MatchReport {public string mapId,aiDifficulty;public float aiDecisionSeconds,aiArmySeconds;public int eventSchemaVersion;public string matchId,appVersion,buildGuid,version,platform,device,gpu,resolution,reason;public int seed,winner,eventCount,maxAge;public float time,meanFrameMs,p95FrameMs,simulationSpeed;public bool autoplay;public long managedBytes;public PlayerReport[] players;public GuidedPlaySnapshot guided;}
        [Serializable]private class PlayerReport {public string civilization;public int player,team,age,kills,losses,shipments,population,treasures,guardians;public bool eliminated,resigned;public Cost gathered,spent,passiveIncome,tradeIncome,resources,refunded;public string[] ageHistory;}
    }
}
