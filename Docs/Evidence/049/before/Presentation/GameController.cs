using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Frontier
{
    public sealed class GameController : MonoBehaviour
    {
        public World World { get; private set; }
        public RuleCatalog Rules { get; private set; }
        public WorldView View { get; private set; }
        public RtsInput Controls { get; private set; }
        public FrontierHud Hud { get; private set; }
        public NativeCapture Captures { get; private set; }
        public string MatchId { get; private set; } = "";
        public EventAudio Audio { get; private set; }
        public Camera GameCamera { get; private set; }
        public bool InMenu { get; private set; } = true;
        public bool IsPaused => World != null && World.Paused;
        public bool PausedForFocusLoss { get; private set; }
        public string Notice = "";
        public string DeckPreset = "economy";
        public string PlayerCivilization="coast",OpponentCivilization="river";
        public int AgeLimit=5,MatchSeed=314159;
        public string OpponentDifficulty="standard";
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
        public bool Muted;
        public float SimulationSpeed=1;
        public bool BenchmarkMode;
        public float LastSimulationFrameMs;
        public MatchSimulation Simulation { get; private set; }
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
            Controls.Focus(new Vector2(25,25));
        }
        private void Start()
        {
            var args=Environment.GetCommandLineArgs();
            string Arg(string key){int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:null;}
            if(Rules.Civ(Arg("-frontierPlayerCiv"))!=null)PlayerCivilization=Arg("-frontierPlayerCiv");
            if(Rules.Civ(Arg("-frontierOpponentCiv"))!=null)OpponentCivilization=Arg("-frontierOpponentCiv");
            if(int.TryParse(Arg("-frontierMaxAge"),out int ages))AgeLimit=Mathf.Clamp(ages,1,Rules.ages.Length);
            if(Array.IndexOf(args,"-frontierAutoplay")>=0) {Autoplay=true;int index=Array.IndexOf(args,"-frontierAutoSpeed");if(index>=0&&index+1<args.Length&&float.TryParse(args[index+1],out float speed))SimulationSpeed=Mathf.Clamp(speed,1,16);StartMatch();}
            else Hud.ShowMenu();
            int benchmarkIndex=Array.IndexOf(args,"-frontierBenchmark");
            if(benchmarkIndex>=0&&benchmarkIndex+1<args.Length&&int.TryParse(args[benchmarkIndex+1],out int count)){BenchmarkMode=true;StartMatch();gameObject.AddComponent<RuntimeBenchmark>().Initialize(this,count);}
        }
        public void StartMatch()
        {
            string deckError=DeckLibrary.Validate(Rules,PlayerCivilization,SelectedDeck);
            if(deckError!=""){Notify(deckError);Hud.ShowMenu();return;}
            if(DifficultyReason!=""){Notify(DifficultyReason);Hud.ShowMenu();return;}
            // A menu restart ends an already running diagnostic fixture. The CLI
            // creates its fixture after StartMatch, so its initial mode is retained.
            if(GetComponent<RuntimeBenchmark>()!=null)ClearBenchmark();
            PausedForFocusLoss=false;audioCursor=0;frameTimes.Clear();nextTelemetry=0;Audio.ResetMatch();
            World=new World(Rules,MatchSeed,PlayerCivilization,OpponentCivilization,AgeLimit);
            MatchId=Guid.NewGuid().ToString("N");
            SetDeck(0,DeckPreset); SetDeck(1,"military");
            Simulation=new MatchSimulation(World,BenchmarkMode?Array.Empty<int>():Autoplay?new[]{0,1}:new[]{1},OpponentDifficulty);
            InMenu=false;Notice="Explore, gather, build. Earn XP and choose a shipment with H.";noticeUntil=12;
            View.ResetWorld();Controls.ResetSelection();Controls.Focus(new Vector2(25,25));
            Hud.ShowGame();
        }
        private void SetDeck(int player,string preset)
        {
            var p=World.Players[player];var deck=player==0?SelectedDeck:DeckLibrary.Preset(Rules,p.civilization,preset);
            if(DeckLibrary.Validate(Rules,p.civilization,deck)!="")deck=DeckLibrary.Preset(Rules,p.civilization,preset);
            p.deck.Clear();p.deck.AddRange(deck);
        }
        public CommandResult Command(GameCommand c)
        {
            if(InMenu||World==null)return CommandResult.Fail("No active match");
            var r=World.Issue(c);
            Notify(r.reason);if(r.accepted)View.Pulse(c.point,c.target,c.order==OrderKind.Attack||c.order==OrderKind.AttackMove);
            return r;
        }
        public void Notify(string text) {Notice=text;noticeUntil=Time.unscaledTime+6;}
        private void OnApplicationFocus(bool focused)
        {
            // Batch verification has no foreground player. Interactive autoplay
            // and benchmark exclusions are handled by the same session policy.
            if(!focused&&!Application.isBatchMode)PauseForFocusLoss();
        }
        public void PauseForFocusLoss()
        {
            if(InMenu||World==null||World.Winner>=0||World.Paused||Autoplay||BenchmarkMode)return;
            SetPaused(true,"focus_lost");
        }
        public void TogglePause()
        {
            if(InMenu||World==null||World.Winner>=0)return;
            SetPaused(!World.Paused,World.Paused?"manual_resume":"manual_pause");
        }
        private void SetPaused(bool paused,string reason)
        {
            World.Paused=paused;PausedForFocusLoss=paused&&reason=="focus_lost";
            // Focus loss invalidates the old device stream. An explicit pause
            // button/ESC transition retains later fresh keyboard edges in FIFO.
            if(reason=="focus_lost")Controls.ClearTransientInput();else Controls.SuppressWorldInput();
            if(paused)Controls.CancelTargetMode();
            Audio.PauseAll(paused);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            World.Emit("PauseChanged",0,0,reason,paused?1:0);
            Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,null,
                "FRONTIER_PAUSE paused={0} reason={1} world={2:F6} realtime={3:F6} frame={4}",
                paused,reason,World.Time,Time.realtimeSinceStartupAsDouble,Time.frameCount);
            Hud.ShowPause(paused);
        }
        public void ReturnToMenu()
        {
            if(World!=null)SaveReport("menu_exit");
            ClearBenchmark();Audio.ResetMatch();World=null;Simulation=null;InMenu=true;Autoplay=false;SimulationSpeed=1;LastSimulationFrameMs=0;
            MatchId="";
            PausedForFocusLoss=false;Controls.ResetSelection();View.ResetWorld();Hud.ShowMenu();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        private void ClearBenchmark()
        {
            BenchmarkMode=false;
            foreach(var fixture in GetComponents<RuntimeBenchmark>()){fixture.enabled=false;Destroy(fixture);}
        }
        public void Resign() {if(InMenu||World==null||World.Winner>=0)return;if(IsPaused)SetPaused(false,"resign");Command(new GameCommand{player=0,kind=CommandKind.Resign});}
        private void Update()
        {
            if(InMenu||World==null)return;
            if(!World.Paused&&World.Winner<0)
            {
                Simulation.AdvanceFrame(Time.unscaledDeltaTime*(Autoplay?SimulationSpeed:1));
                LastSimulationFrameMs=Simulation.LastFrameMs;
                if(Simulation.CatchingUp)Notify("Simulation is catching up — rendering load is high.");
                frameTimes.Add(Time.unscaledDeltaTime*1000);
            }
            while(audioCursor<World.Events.Count)
            {
                var ev=World.Events[audioCursor++];Audio.OnEvent(ev);
                if(ev.player==0 && (ev.kind=="ShipmentArrived"||ev.kind=="AgeAdvanced"||ev.kind=="ResourceDepleted"||ev.kind=="TreasureClaimed"))Notify(ev.message);
            }
            if(World.Winner>=0&&!Hud.ResultVisible){SaveReport("match_finished");Hud.ShowResult();}
            if(Time.unscaledTime>noticeUntil)Notice="";
            if(Autoplay&&World.Time>=nextTelemetry)
            {nextTelemetry=World.Time+60;Debug.Log($"FRONTIER_PROGRESS t={World.Time:0} entities={World.Entities.Count} age={World.Players[0].age}/{World.Players[1].age} winner={World.Winner}");}
        }
        public void SaveReport(string reason)
        {
            if(World==null)return;
            World.FlushEconomyLog();
            string dir=Path.Combine(Application.persistentDataPath,"Reports");Directory.CreateDirectory(dir);
            string stem=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+MatchId+"-"+reason;
            lastReport=Path.Combine(dir,stem+".json");
            var samples=new List<float>(frameTimes);samples.Sort();
            float mean=0;foreach(float f in samples)mean+=f;if(samples.Count>0)mean/=samples.Count;
            var report=new MatchReport{aiDifficulty=Simulation?.Difficulty.Id,aiDecisionSeconds=Simulation?.Difficulty.DecisionSeconds??0,aiArmySeconds=Simulation?.Difficulty.ArmySeconds??0,matchId=MatchId,eventSchemaVersion=SimEvent.CurrentSchemaVersion,appVersion=Application.version,buildGuid=Application.buildGUID,maxAge=World.MaxAge,version=Application.unityVersion,platform=Application.platform.ToString(),device=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,resolution=Screen.width+"x"+Screen.height,reason=reason,seed=World.Seed,time=World.Time,winner=World.Winner,autoplay=Autoplay,simulationSpeed=Autoplay?SimulationSpeed:1,meanFrameMs=mean,p95FrameMs=samples.Count>0?samples[(int)((samples.Count-1)*.95f)]:0,managedBytes=GC.GetTotalMemory(false),eventCount=World.Events.Count,players=new PlayerReport[2]};
            for(int i=0;i<2;i++){var p=World.Players[i];report.players[i]=new PlayerReport{civilization=p.civilization,age=p.age,gathered=p.gathered,spent=p.spent,kills=p.kills,losses=p.losses,shipments=p.shipsSent,population=p.usedPopulation,passiveIncome=p.passiveIncome,tradeIncome=p.tradeIncome,treasures=p.treasuresClaimed,guardians=p.guardiansDefeated,resources=p.resources,refunded=p.refunded,ageHistory=p.ageHistory.ToArray()};}
            File.WriteAllText(lastReport,JsonUtility.ToJson(report,true));
            using(var writer=new StreamWriter(Path.Combine(dir,stem+"-events.jsonl")))foreach(var ev in World.Events)writer.WriteLine(JsonUtility.ToJson(ev));
            Debug.Log("FRONTIER_REPORT "+lastReport);
        }
        public void Capture()
        {
            Captures.Request();
        }
        private void OnApplicationQuit(){SaveReport("quit");}
        [Serializable]private class MatchReport {public string aiDifficulty;public float aiDecisionSeconds,aiArmySeconds;public int eventSchemaVersion;public string matchId,appVersion,buildGuid,version,platform,device,gpu,resolution,reason;public int seed,winner,eventCount,maxAge;public float time,meanFrameMs,p95FrameMs,simulationSpeed;public bool autoplay;public long managedBytes;public PlayerReport[] players;}
        [Serializable]private class PlayerReport {public string civilization;public int age,kills,losses,shipments,population,treasures,guardians;public Cost gathered,spent,passiveIncome,tradeIncome,resources,refunded;public string[] ageHistory;}
    }
}
