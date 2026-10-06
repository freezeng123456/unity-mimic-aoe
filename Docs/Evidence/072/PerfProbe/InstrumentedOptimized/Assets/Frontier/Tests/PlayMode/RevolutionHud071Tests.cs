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
    // NativeInputFixture's real InputSystem gestures, with durable paths set
    // before controller Start. Initial resources and age belong to an authored
    // fixture; no runtime units, clocks, payments or queue progress are injected.
    public sealed class RevolutionHud071Tests
    {
        const BindingFlags PrivateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
        GameObject session;GameController game;Mouse mouse;Keyboard keyboard;string directory;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;

        [UnitySetUp] public IEnumerator SetUp()
        {
            background=InputSystem.settings.backgroundBehavior;editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            mouse=InputSystem.AddDevice<Mouse>();keyboard=InputSystem.AddDevice<Keyboard>();
            directory=Path.Combine(Path.GetTempPath(),"frontier-revolution-hud071-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            session=new GameObject("Isolated revolution InputSystem regression");game=session.AddComponent<GameController>();
            SetField("dataDirectoryOverride",directory);SetField("saveDirectoryOverride",Path.Combine(directory,"Saves"));
            SetField("reportDirectoryOverride",Path.Combine(directory,"Reports"));SetField("replayDirectoryOverride",Path.Combine(directory,"Replays"));
            yield return null;game.enabled=false;
            Assert.IsTrue(game.InMenu);Assert.IsNull(game.World);Assert.IsFalse(game.Settings.Persistent);Assert.IsTrue(game.Progress.MemoryOnly);
            SetProperty(game,"Progress",AccountProgressStore.Load(Path.Combine(directory,"Account")));
            SetProperty(game.Settings,"Path",Path.Combine(directory,"Preferences","player-settings.json"));
            var preferences=game.Settings.Current;preferences.muted=true;preferences.musicEnabled=false;preferences.environmentEnabled=false;
            Assert.IsTrue(game.Settings.TryUpdate(preferences,out string reason),reason);
            Assert.IsTrue(game.OpenScenarioDocument(Document()),game.Notice);Assert.IsTrue(game.PlaytestScenario(),game.Notice);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(Screen.width*.5f,Screen.height*.5f)});
            yield return Ready();Assert.AreEqual(4,Player.age);Assert.AreEqual(5,game.World.MaxAge);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            try
            {
                if(game!=null)
                {
                    game.enabled=false;if(game.World!=null)game.ReturnToMenu();
                    InvokeController("FinishReplayFileWrites");InvokeController("PollReplayOperations");Assert.IsFalse(game.ReplaySaving);
                }
            }
            finally
            {
                if(session!=null)UnityEngine.Object.Destroy(session);
                if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);
                if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
            }
            yield return null;
            if(directory!=null&&Directory.Exists(directory))Directory.Delete(directory,true);
        }
        void SetField(string name,object value)=>typeof(GameController).GetField(name,PrivateInstance).SetValue(game,value);
        static void SetProperty(object target,string name,object value)=>target.GetType().GetProperty(name).GetSetMethod(true).Invoke(target,new[]{value});
        void InvokeController(string name)=>typeof(GameController).GetMethod(name,PrivateInstance).Invoke(game,null);
        void UpdateHud()=>typeof(FrontierHud).GetMethod("Update",PrivateInstance).Invoke(game.Hud,null);
        PlayerState Player=>game.World.Players[0];
        EntityState Town=>game.World.ScenarioEntity("start-0-town");
        RevolutionDef Rule=>game.World.RevolutionDefinition(0);
        ScenarioDocument Document()=>new ScenarioDocument{id="revolution-hud071",name="Revolution interface fixture",enableElimination=false,
            rulesHash=SnapshotCodec.RulesHash(game.Rules),starts=new[]{
                new ScenarioStart{player=0,team=0,civilization="coast",age=4,initialResources=new Cost(4000,4000,4000),
                    town=new Vector2(24,28),explorer=new Vector2(31,32),workers=new[]{new Vector2(40,25),new Vector2(44,25),new Vector2(48,25)}},
                new ScenarioStart{player=1,team=1,civilization="france",town=new Vector2(104,100),explorer=new Vector2(97,96),
                    initialResources=new Cost(),workers=Array.Empty<Vector2>()}},
            resources=new[]{new ScenarioPlacement{id="own-crate",definition="crate_food",owner=0,position=new Vector2(42,35),remaining=75},
                new ScenarioPlacement{id="natural-tree",definition="tree",position=new Vector2(48,35),remaining=200}}};
        IEnumerator Ready(){yield return null;Canvas.ForceUpdateCanvases();yield return null;}
        IEnumerator Click(Vector2 point,MouseButton button=MouseButton.Left)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(button));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return Ready();
        }
        IEnumerator Press(Key key)
        {InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Ready();}
        Button FindButton(string prefix,string parent=null)=>session.GetComponentsInChildren<Button>().Single(b=>b.isActiveAndEnabled&&
            b.name.StartsWith(prefix,StringComparison.Ordinal)&&(parent==null||b.transform.parent.name==parent));
        IEnumerator ClickButton(string prefix,string parent=null)
        {
            var button=FindButton(prefix,parent);Assert.IsTrue(button.interactable,prefix);
            var rect=(RectTransform)button.transform;yield return Click(RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)));
        }
        IEnumerator SelectUnit(EntityState entity)
        {
            // Closing a modal deliberately guards world gestures for 0.22 real
            // seconds. Two batch-mode frames can both fall inside that guard.
            // Wait normally; do not clear or bypass the production input barrier.
            game.Controls.Focus(entity.position);yield return new WaitForSecondsRealtime(.25f);yield return Ready();
            Vector2 point=game.GameCamera.WorldToScreenPoint(new Vector3(entity.position.x,game.World.SurfaceHeight(entity.position,entity.def.waterborne)+1,entity.position.y));AssertWorldPoint(point);
            yield return Click(point);CollectionAssert.AreEqual(new[]{entity.id},game.Controls.Selected);
        }
        void AssertWorldPoint(Vector2 point)
        {
            Assert.That(point.x,Is.InRange(1,Screen.width-1));Assert.That(point.y,Is.InRange(1,Screen.height-1));
            Assert.IsFalse(game.Hud.BlocksWorldInput(point),"Gesture must hit the unobstructed viewport");
        }
        IEnumerator ContextClick(EntityState entity)
        {
            game.Controls.Focus(entity.position);yield return new WaitForSecondsRealtime(.25f);yield return Ready();
            Vector2 point=game.GameCamera.WorldToScreenPoint(new Vector3(entity.position.x,game.World.SurfaceHeight(entity.position,entity.def.waterborne)+(entity.def.kind==EntityKind.Unit?1:0),entity.position.y));AssertWorldPoint(point);
            yield return Click(point,MouseButton.Right);
        }
        static void EqualCost(Cost expected,Cost actual)
        {Assert.AreEqual(expected.food,actual.food,.001f);Assert.AreEqual(expected.wood,actual.wood,.001f);Assert.AreEqual(expected.coin,actual.coin,.001f);}
        static Cost Subtract(Cost a,Cost b)=>new Cost(a.food-b.food,a.wood-b.wood,a.coin-b.coin);
        void Steps(int count){for(int i=0;i<count;i++)Assert.IsTrue(game.Simulation.Step(),"Simulation stopped at "+game.Simulation.StepCount);}
        void CompletePhase(PoliticalPhase phase,int maximumSteps)
        {
            int count=0;while(Player.politicalPhase!=phase&&count++<maximumSteps)Steps(1);
            Assert.AreEqual(phase,Player.politicalPhase,"Real production did not complete within its bounded duration");
        }
        IEnumerator WaitForReplayWrites()
        {
            // The controller Update is disabled to keep fixed simulation steps
            // under test control. Its normal asynchronous completion poll must
            // still run; waiting alone cannot retire completed write notices.
            float deadline=Time.realtimeSinceStartup+30;
            while(game.ReplaySaving&&Time.realtimeSinceStartup<deadline)
            {
                InvokeController("PollReplayOperations");
                if(game.ReplaySaving)yield return null;
            }
            Assert.IsFalse(game.ReplaySaving,"Previous cold-load segments did not finish saving within 30 real seconds");
            Assert.IsEmpty(game.ReplayFileError);
        }
        IEnumerator StartRevolution()
        {
            yield return Press(Key.T);Assert.AreEqual(Town.id,game.Controls.FirstSelected.id);
            yield return ClickButton("REVOLUTION","Context actions");yield return ClickButton("START REVOLUTION");
            Assert.AreEqual("Revolution",Town.queue.Single().kind);Assert.AreEqual(Rule.id,Town.queue.Single().item);
        }
        ReplayRecord[] Commands()=>game.Recorder.Capture().records.Where(r=>r.kind==ReplayOperation.Command).ToArray();
        Transform ActiveView(int id)=>session.GetComponentsInChildren<Transform>().Single(t=>t.gameObject.activeInHierarchy&&
            t.name=="Entity "+id&&t.parent!=null&&t.parent.name=="Simulation Entity Views");
        string[] VisibleCardNames()=>session.GetComponentsInChildren<Button>().Where(b=>b.isActiveAndEnabled&&b.name.Contains("  ·  AGE "))
            .Select(b=>b.name.Substring(0,b.name.IndexOf("  ·  AGE ",StringComparison.Ordinal))).ToArray();

        static void AssertGeneratedQueueGlyphsInsideTile(UnityEngine.UI.Text text, UnityEngine.UI.Button button)
        {
            Assert.IsTrue(text.isActiveAndEnabled, "Actual visible queue label must be active");
            Assert.IsNotNull(text.font, "Use the production font loaded by FrontierHud");
            Assert.IsTrue(text.font.dynamic, "This fixture must exercise the production dynamic-font metrics");
            var tile = (UnityEngine.RectTransform)button.transform;
            Assert.AreEqual(47, tile.rect.height, .001f, "The production queue tile remains 47 high");
            Assert.Greater(text.cachedTextGenerator.vertexCount, 0,
                "A nonempty text string is insufficient: truncation can generate no glyphs");
            // GetMesh returns the renderer's current mesh. Inspect it without taking ownership.
            var mesh = text.canvasRenderer.GetMesh();
            Assert.IsNotNull(mesh, "Actual CanvasRenderer must hold a mesh");
            {
                Assert.Greater(mesh.vertexCount, 0, "Actual CanvasRenderer must contain glyph geometry");
                Assert.Greater(mesh.bounds.size.x, .001f, "Reject a degenerate/sentinel-only result");
                Assert.Greater(mesh.bounds.size.y, .001f, "Reject invisible zero-height geometry");
                foreach (var vertex in mesh.vertices)
                {
                    var p = tile.InverseTransformPoint(text.rectTransform.TransformPoint(vertex));
                    Assert.That(p.x, Is.InRange(tile.rect.xMin - .25f, tile.rect.xMax + .25f),
                        "Rendered glyph must stay inside the actual queue tile horizontally");
                    Assert.That(p.y, Is.InRange(tile.rect.yMin - .25f, tile.rect.yMax + .25f),
                        "Rendered glyph must stay inside the 47-high queue tile vertically");
                }
                TestContext.WriteLine("Queue label='{0}' font={1} fontSize={2} lineSpacing={3} rect={4} preferredHeight={5} " +
                    "generatorVertices={6} rendererVertices={7} rendererBounds={8} pixelsPerUnit={9}",
                    text.text, text.font.name, text.fontSize, text.lineSpacing, text.rectTransform.rect,
                    text.preferredHeight, text.cachedTextGenerator.vertexCount, mesh.vertexCount, mesh.bounds, text.pixelsPerUnit);
            }
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator ActualPaidRevolutionCountdownGeneratesContainedGlyphsAndUpdatesBeforeRealCancellation()
        {
            Cost before = Player.resources;
            yield return StartRevolution();
            yield return Ready();
            UnityEngine.Canvas.ForceUpdateCanvases();
            yield return null;
            UnityEngine.Canvas.ForceUpdateCanvases();
            var job = Town.queue.Single();
            EqualCost(Rule.cost, job.paid);
            EqualCost(Subtract(before, Rule.cost), Player.resources);
            var button = FindButton(Rule.name, "Production queue");
            var clock = button.GetComponentsInChildren<UnityEngine.UI.Text>()
                .Single(t => t.isActiveAndEnabled && t.transform.parent == button.transform && t.text == "90s");
            Assert.AreEqual("90s", clock.text, "Actual normal queue state starts a 90-second revolution");
            // Three non-whitespace glyphs must actually exist, not merely be stored in text.text.
            Assert.GreaterOrEqual(clock.cachedTextGenerator.vertexCount, 12, "All three 90s glyphs must generate quads");
            AssertGeneratedQueueGlyphsInsideTile(clock, button);
            var title = button.GetComponentsInChildren<UnityEngine.UI.Text>()
                .Single(t => t.isActiveAndEnabled && t.transform.parent == button.transform && t.text == Rule.name);
            AssertGeneratedQueueGlyphsInsideTile(title, button);

            // Let the real fixed-step production timer advance; no elapsed field injection.
            Steps(17);
            // The production HUD refreshes queue tiles once per real second and
            // preserves an in-flight pointer gesture. Fixed simulation steps can
            // finish sooner; allow the normal refresh, without invalidating it.
            yield return new WaitForSecondsRealtime(1.05f);
            UpdateHud();
            yield return Ready();
            UnityEngine.Canvas.ForceUpdateCanvases();
            yield return null;
            UnityEngine.Canvas.ForceUpdateCanvases();
            Assert.AreSame(job, Town.queue.Single(), "UI rebuild must retain the paid queue's identity");
            string remaining = UnityEngine.Mathf.Max(0, job.duration - job.elapsed).ToString("0") + "s";
            Assert.AreNotEqual("90s", remaining, "The test must observe a genuinely changed countdown");
            button = FindButton(Rule.name, "Production queue");
            Assert.IsTrue(button.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t =>
                t.isActiveAndEnabled && t.transform.parent == button.transform && t.text == remaining),
                "The normal HUD refresh must display the actual advanced countdown: " + remaining);
            clock = button.GetComponentsInChildren<UnityEngine.UI.Text>()
                .Single(t => t.isActiveAndEnabled && t.transform.parent == button.transform && t.text == remaining);
            Assert.GreaterOrEqual(clock.cachedTextGenerator.vertexCount, 4 * remaining.Length,
                "Every non-whitespace countdown character needs a rendered quad after refresh");
            AssertGeneratedQueueGlyphsInsideTile(clock, button);
            yield return ClickButton(Rule.name, "Production queue");
            Assert.IsEmpty(Town.queue);
            EqualCost(before, Player.resources);
            EqualCost(Rule.cost, Player.refunded);
            Assert.AreEqual(1, Commands().Count(r => r.accepted && r.command.kind == CommandKind.CancelQueue),
                "The same actual queue button must still cancel the original paid job exactly once");
        }

        [UnityTest] public IEnumerator TownPreviewPaysOnceAndActualQueueClickRefundsTheWholeRevolution()
        {
            Cost before=Player.resources;yield return Press(Key.T);yield return ClickButton("REVOLUTION","Context actions");
            Assert.IsTrue(session.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("3 settlers currently eligible")&&t.text.Contains(Rule.cost.ToString())));
            Assert.IsEmpty(Town.queue);EqualCost(before,Player.resources);
            yield return ClickButton("START REVOLUTION");EqualCost(Subtract(before,Rule.cost),Player.resources);
            EqualCost(Rule.cost,Town.queue.Single().paid);Assert.IsTrue(game.World.WorkerSourcesClosed(0));
            Assert.AreEqual(1,game.World.Events.Count(e=>e.kind=="RevolutionQueued"&&e.player==0));
            Assert.AreEqual(1,Commands().Count(r=>r.accepted&&r.command.kind==CommandKind.Revolution));
            yield return ClickButton(Rule.name,"Production queue");
            Assert.IsEmpty(Town.queue);EqualCost(before,Player.resources);Assert.AreEqual(PoliticalPhase.Empire,Player.politicalPhase);
            Assert.IsFalse(game.World.WorkerSourcesClosed(0));EqualCost(Rule.cost,Player.refunded);
            Assert.AreEqual(1,Commands().Count(r=>r.accepted&&r.command.kind==CommandKind.CancelQueue));
            Steps(950);Assert.AreEqual(PoliticalPhase.Empire,Player.politicalPhase);
            Assert.IsFalse(game.World.Events.Any(e=>e.kind=="RevolutionCompleted"&&e.player==0));
        }

        [UnityTest] public IEnumerator OpenHomeCityRefreshesOnTheCompletionFrameAndSelectedStableIdGetsAnArmedViewAndReplay()
        {
            string[] opening=Player.deck.ToArray();var worker=game.World.ScenarioEntity("start-0-worker-0");int stableId=worker.id;
            yield return StartRevolution();yield return SelectUnit(worker);var civilianView=ActiveView(stableId);
            Assert.IsNotEmpty(civilianView.GetComponentsInChildren<WorkerEquipment>(true));
            yield return Press(Key.H);CollectionAssert.AreEquivalent(opening.Take(8).Select(id=>game.Rules.Card(id).name.ToUpperInvariant()),VisibleCardNames());
            // Production completes through the real scheduler, then the normal HUD
            // Update runs in the same Unity frame, without waiting for another second.
            int frame=Time.frameCount;CompletePhase(PoliticalPhase.Revolutionary,1000);UpdateHud();Assert.AreEqual(frame,Time.frameCount);
            CollectionAssert.AreEquivalent(Rule.cards.Select(id=>game.Rules.Card(id).name.ToUpperInvariant()),VisibleCardNames());
            CollectionAssert.AreEqual(opening,Player.deck);CollectionAssert.AreEqual(new[]{stableId},game.Controls.Selected);
            Assert.AreEqual(Rule.unit,game.World.Get(stableId).def.id);Assert.IsFalse(game.World.Get(stableId).Worker);
            yield return Press(Key.Escape);yield return Ready();var armedView=ActiveView(stableId);
            Assert.AreNotSame(civilianView,armedView,"Stable simulation IDs still need a new model after conversion");
            Assert.IsEmpty(armedView.GetComponentsInChildren<WorkerEquipment>(true));
            Assert.IsTrue(armedView.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Musket"));
            CollectionAssert.AreEqual(new[]{stableId},game.Controls.Selected);
            Assert.IsTrue(game.SaveReplayNow(),game.ReplayFileError);var document=ReplayStore.Read(game.LastReplayFile,game.Rules,game.CurrentReplayIdentity());
            Assert.IsFalse(document.incomplete);Assert.AreEqual(1,document.records.Count(r=>r.accepted&&r.kind==ReplayOperation.Command&&r.command.kind==CommandKind.Revolution));
            using(var playback=new ReplaySession(game.Rules,document,game.CurrentReplayIdentity()))
            {
                Assert.AreEqual(0,playback.CurrentStep);playback.Advance(2000);Assert.IsEmpty(playback.Error);Assert.IsTrue(playback.Verified);
                Assert.AreEqual(document.anchors.Last().completedStep,playback.CurrentStep);
                Assert.AreEqual(PoliticalPhase.Revolutionary,playback.World.Players[0].politicalPhase);Assert.AreEqual(Rule.unit,playback.World.Get(stableId).def.id);
                EqualCost(Player.resources,playback.World.Players[0].resources);
            }
        }

        IEnumerator ColdSlot(int slot)
        {
            yield return Press(Key.Escape);Assert.IsTrue(game.World.Paused);yield return ClickButton("SAVE MATCH");yield return ClickButton("SLOT "+slot+" ·");
            string path=MatchSaveStore.SlotPath(game.SaveDirectory,slot);Assert.IsTrue(File.Exists(path));byte[] savedBytes=File.ReadAllBytes(path);
            var saved=MatchSaveStore.Read(path,Application.version);var previous=game.World;
            yield return ClickButton("BACK");yield return ClickButton("LOAD MATCH");yield return ClickButton("SLOT "+slot+" ·");
            Assert.AreNotSame(previous,game.World);Assert.IsTrue(game.World.Paused);
            Assert.AreEqual(JsonUtility.ToJson(saved.world),JsonUtility.ToJson(game.World.CaptureState()),"Cold load must preserve every world and production field");
            Assert.AreEqual(JsonUtility.ToJson(saved.simulation),JsonUtility.ToJson(game.Simulation.CaptureState()),"Cold load must preserve actual scheduler and AI state");
            CollectionAssert.AreEqual(saved.selection,game.Controls.Selected);CollectionAssert.AreEqual(savedBytes,File.ReadAllBytes(path));
            yield return ClickButton("RESUME");Assert.IsFalse(game.World.Paused);
        }
        [UnityTest] public IEnumerator ControllerSlotButtonsColdRestoreBothPoliticalQueuesAndFinishCivilianRebuilding()
        {
            int[] workers=game.World.Entities.Where(e=>e.Alive&&e.owner==0&&e.Worker).Select(e=>e.id).ToArray();Cost before=Player.resources;
            yield return StartRevolution();Steps(73);float elapsed=Town.queue.Single().elapsed;Assert.Greater(elapsed,0);
            yield return ColdSlot(1);Assert.AreEqual(PoliticalPhase.Empire,Player.politicalPhase);Assert.AreEqual(elapsed,Town.queue.Single().elapsed);
            EqualCost(Rule.cost,Town.queue.Single().paid);EqualCost(Subtract(before,Rule.cost),Player.resources);
            CompletePhase(PoliticalPhase.Revolutionary,1000);yield return Ready();float revolutionAt=Player.revolutionAt;
            foreach(int id in workers)Assert.AreEqual(Rule.unit,game.World.Get(id).def.id);
            yield return Press(Key.T);yield return ClickButton("CIVILIAN GOVERNMENT","Context actions");
            var tech=game.Rules.Tech(World.CivilianRebuildingTechnology);yield return ClickButton("REBUILD CIVILIAN GOVERNMENT");
            EqualCost(Subtract(Subtract(before,Rule.cost),tech.cost),Player.resources);Assert.AreEqual(tech.id,Town.queue.Single().item);
            Assert.AreEqual(1,Commands().Count(r=>r.accepted&&r.command.kind==CommandKind.Research&&r.command.item==tech.id));
            Steps(113);elapsed=Town.queue.Single().elapsed;yield return ColdSlot(2);
            Assert.AreEqual(PoliticalPhase.Revolutionary,Player.politicalPhase);Assert.AreEqual(revolutionAt,Player.revolutionAt);
            Assert.AreEqual("Research",Town.queue.Single().kind);Assert.AreEqual(tech.id,Town.queue.Single().item);Assert.AreEqual(elapsed,Town.queue.Single().elapsed);
            EqualCost(tech.cost,Town.queue.Single().paid);CompletePhase(PoliticalPhase.CivilianRebuilt,700);yield return Ready();
            Assert.Contains(tech.id,Player.technologies.ToArray());Assert.IsFalse(game.World.WorkerSourcesClosed(0));
            foreach(int id in workers)Assert.AreEqual(Rule.unit,game.World.Get(id).def.id,"Rebuilding must not revert existing infantry");
            Assert.AreEqual(4,Player.age);Assert.That(game.World.AgeReason(0,Town),Is.Not.Empty);
            Assert.AreEqual(1,game.World.Events.Count(e=>e.kind=="RevolutionCompleted"&&e.player==0));Assert.AreEqual(1,game.World.Events.Count(e=>e.kind=="CivilianRebuilt"&&e.player==0));
            EqualCost(Rule.cost+tech.cost,Player.spent);
            var settler=game.Rules.Entity(game.Rules.Civ(Player.civilization).workerUnit);
            yield return ClickButton(settler.name,"Context actions");Assert.AreEqual("Train",Town.queue.Single().kind);Assert.AreEqual(settler.id,Town.queue.Single().item);
            int guard=0;while(Town.queue.Count>0&&guard++<500)Steps(1);Assert.IsEmpty(Town.queue);
            Assert.AreEqual(1,game.World.Entities.Count(e=>e.Alive&&e.owner==0&&e.Worker));
            foreach(int id in workers)Assert.AreEqual(Rule.unit,game.World.Get(id).def.id);
            yield return WaitForReplayWrites();
            Assert.IsTrue(game.SaveReplayNow(),game.ReplayFileError);
            var document=ReplayStore.Read(game.LastReplayFile,game.Rules,game.CurrentReplayIdentity());Assert.IsFalse(document.incomplete);
            Assert.AreEqual(1,document.records.Count(r=>r.accepted&&r.kind==ReplayOperation.Command&&r.command.kind==CommandKind.Train&&r.command.item==settler.id));
            using(var playback=new ReplaySession(game.Rules,document,game.CurrentReplayIdentity()))
            {
                playback.Advance(2000);Assert.IsEmpty(playback.Error);Assert.IsTrue(playback.Verified);
                Assert.AreEqual(PoliticalPhase.CivilianRebuilt,playback.World.Players[0].politicalPhase);EqualCost(Player.resources,playback.World.Players[0].resources);
            }
        }

        [UnityTest] public IEnumerator DisbandConfirmationKeepsOrDismissesOnlyTheSelectedInfantryWithoutRefundOrXpAndReplaysExactly()
        {
            int id=game.World.ScenarioEntity("start-0-worker-0").id;yield return StartRevolution();CompletePhase(PoliticalPhase.Revolutionary,1000);yield return Ready();
            var soldier=game.World.Get(id);yield return SelectUnit(soldier);Assert.AreEqual(1,soldier.def.population);
            // Save a group through the actual modifier gesture before dismissing
            // its member; recalling a now-empty group must remain safe.
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftCtrl,Key.Digit1));
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Ready();
            CollectionAssert.AreEqual(new[]{id},game.Controls.CaptureGroups().Single(g=>g.number==1).actors);
            int[] foreign=game.World.Entities.Where(e=>e.Alive&&e.owner==1).Select(e=>e.id).ToArray();
            int[] survivors=game.World.Entities.Where(e=>e.Alive&&e.owner==0&&e.id!=id).Select(e=>e.id).ToArray();
            string before=JsonUtility.ToJson(game.World.CaptureState());int commands=Commands().Length;
            yield return ClickButton("DISBAND","Context actions");Assert.IsTrue(soldier.Alive);yield return ClickButton("KEEP UNITS");
            Assert.AreEqual(before,JsonUtility.ToJson(game.World.CaptureState()),"Cancelling the preview must leave the complete world unchanged");
            Assert.AreEqual(commands,Commands().Length,"The preview must not submit a paid or destructive command");
            CollectionAssert.AreEqual(new[]{id},game.Controls.Selected);
            Cost resources=Player.resources,refunded=Player.refunded,spent=Player.spent;float xp=Player.xp;int population=Player.usedPopulation,kills=Player.kills;
            yield return ClickButton("DISBAND","Context actions");yield return ClickButton("DISBAND 1 UNITS");
            var disband=Commands().Last();Assert.IsTrue(disband.accepted,disband.reason);Assert.AreEqual(CommandKind.Disband,disband.command.kind);
            Assert.AreEqual(0,disband.command.player);CollectionAssert.AreEqual(new[]{id},disband.command.actors);
            Assert.IsFalse(game.World.Get(id).Alive);Assert.AreEqual(population-1,Player.usedPopulation);
            EqualCost(resources,Player.resources);EqualCost(refunded,Player.refunded);EqualCost(spent,Player.spent);Assert.AreEqual(xp,Player.xp);Assert.AreEqual(kills,Player.kills);
            foreach(int foreignId in foreign)Assert.IsTrue(game.World.Get(foreignId).Alive,"Disband cannot dismiss another player's object");
            foreach(int survivorId in survivors)Assert.IsTrue(game.World.Get(survivorId).Alive,"Unselected own units and buildings must survive");
            Assert.AreEqual(1,game.World.Events.Count(e=>e.kind=="UnitDisbanded"&&e.player==0&&e.entity==id));
            yield return Press(Key.Digit1);Assert.IsEmpty(game.Controls.Selected);Assert.IsNull(game.Controls.FirstSelected);yield return Ready();
            Assert.IsTrue(game.SaveReplayNow(),game.ReplayFileError);var document=ReplayStore.Read(game.LastReplayFile,game.Rules,game.CurrentReplayIdentity());
            Assert.IsFalse(document.incomplete);Assert.AreEqual(1,document.records.Count(r=>r.kind==ReplayOperation.Command&&r.accepted&&r.command.kind==CommandKind.Disband));
            using(var playback=new ReplaySession(game.Rules,document,game.CurrentReplayIdentity()))
            {
                Assert.AreEqual(0,playback.CurrentStep);playback.Advance(2000);Assert.IsEmpty(playback.Error);Assert.IsTrue(playback.Verified);
                Assert.AreEqual(document.anchors.Last().completedStep,playback.CurrentStep);Assert.IsFalse(playback.World.Get(id).Alive);
                Assert.AreEqual(Player.usedPopulation,playback.World.Players[0].usedPopulation);EqualCost(resources,playback.World.Players[0].resources);
                Assert.AreEqual(xp,playback.World.Players[0].xp);EqualCost(refunded,playback.World.Players[0].refunded);
                foreach(int foreignId in foreign)Assert.IsTrue(playback.World.Get(foreignId).Alive);
                foreach(int survivorId in survivors)Assert.IsTrue(playback.World.Get(survivorId).Alive);
            }
        }

        [UnityTest] public IEnumerator ConvertedInfantryActualContextClickCollectsOwnCrateAndCannotGatherNaturalTree()
        {
            int id=game.World.ScenarioEntity("start-0-worker-0").id;yield return StartRevolution();CompletePhase(PoliticalPhase.Revolutionary,1000);yield return Ready();
            var soldier=game.World.Get(id);var crate=game.World.ScenarioEntity("own-crate");var tree=game.World.ScenarioEntity("natural-tree");
            yield return SelectUnit(soldier);yield return ContextClick(crate);
            var gather=Commands().Last();Assert.IsTrue(gather.accepted,gather.reason);Assert.AreEqual(OrderKind.Gather,gather.command.order);
            Assert.AreEqual(crate.id,gather.command.target);CollectionAssert.AreEqual(new[]{id},gather.command.actors);
            float food=Player.resources.food,remaining=crate.remaining;int gatherSteps=0;
            while(Player.resources.food<=food&&gatherSteps++<150)Steps(1);
            Assert.Greater(Player.resources.food,food);Assert.Less(crate.remaining,remaining);Assert.Greater(crate.remaining,0,"Inspect actual gathering before the crate is depleted");
            Assert.IsTrue(soldier.IsWorkingAt(game.World.Time));Assert.AreEqual(WorkKind.Gather,soldier.work.kind);Assert.AreEqual(crate.id,soldier.work.target);
            var gatheringPose=UnitAnimation.Evaluate(soldier,game.World.Time,workTarget:crate);
            Assert.AreEqual(UnitPoseState.Gather,gatheringPose.state);Assert.AreEqual(WorkerTool.Basket,gatheringPose.tool);
            yield return Ready();var rig=ActiveView(id).GetComponentInChildren<UnitRig>(true);Assert.IsNotNull(rig);Assert.IsNull(rig.WorkerEquipment);
            Assert.IsNotNull(rig.SupplyCollectorWeapon);Assert.AreSame(rig.Weapon,rig.SupplyCollectorWeapon);
            Assert.IsFalse(rig.SupplyCollectorWeapon.gameObject.activeSelf,"Real supply collection must stow the musket");
            int applicationsBeforeStop=rig.PoseApplicationCount;
            yield return Press(Key.S);var stop=Commands().Last();Assert.IsTrue(stop.accepted,stop.reason);Assert.AreEqual(OrderKind.Stop,stop.command.order);
            CollectionAssert.AreEqual(new[]{id},stop.command.actors);Assert.IsFalse(soldier.IsWorkingAt(game.World.Time));
            Assert.AreNotEqual(UnitPoseState.Gather,UnitAnimation.Evaluate(soldier,game.World.Time,workTarget:crate).state);
            for(int frame=0;frame<8&&(rig.PoseApplicationCount<=applicationsBeforeStop||!rig.SupplyCollectorWeapon.gameObject.activeSelf);frame++)yield return null;
            Assert.Greater(rig.PoseApplicationCount,applicationsBeforeStop,"The normal WorldView presentation must apply the changed Stop pose");
            Assert.IsTrue(rig.SupplyCollectorWeapon.gameObject.activeSelf,"The actual Stop command must restore the musket");
            yield return ContextClick(tree);var move=Commands().Last();Assert.IsTrue(move.accepted,move.reason);
            Assert.AreEqual(OrderKind.Move,move.command.order);Assert.AreEqual(0,move.command.target);CollectionAssert.AreEqual(new[]{id},move.command.actors);
            float wood=Player.resources.wood;remaining=tree.remaining;Steps(100);Assert.AreEqual(wood,Player.resources.wood);Assert.AreEqual(remaining,tree.remaining);
            Assert.IsFalse(game.World.CanGather(soldier,tree));Assert.IsFalse(Commands().Any(r=>r.accepted&&r.command.order==OrderKind.Gather&&r.command.target==tree.id));
            Assert.IsTrue(game.SaveReplayNow(),game.ReplayFileError);var document=ReplayStore.Read(game.LastReplayFile,game.Rules,game.CurrentReplayIdentity());Assert.IsFalse(document.incomplete);
            using(var playback=new ReplaySession(game.Rules,document,game.CurrentReplayIdentity()))
            {
                Assert.AreEqual(0,playback.CurrentStep);playback.Advance(2000);Assert.IsEmpty(playback.Error);Assert.IsTrue(playback.Verified);
                EqualCost(Player.resources,playback.World.Players[0].resources);Assert.AreEqual(crate.remaining,playback.World.ScenarioEntity("own-crate").remaining);
                Assert.IsFalse(playback.World.Get(id).IsWorkingAt(playback.World.Time));
            }
        }
    }
}
