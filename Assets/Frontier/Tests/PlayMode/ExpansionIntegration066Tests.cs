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
    // Controller/UI integration only. All durable writes belong to this fixture;
    // these InputSystem events do not certify operating-system native input.
    public sealed class ExpansionIntegration066Tests : NativeInputFixture
    {
        string directory;
        const BindingFlags PrivateInstance=BindingFlags.Instance|BindingFlags.NonPublic;

        [UnitySetUp] public IEnumerator IsolateIntegrationStorage()
        {
            Assert.IsFalse(game.Settings.Persistent,"Editor setup must never load the player's durable settings");
            Assert.IsTrue(game.Progress.MemoryOnly,"Editor setup must never load the player's campaign progress");
            directory=Path.Combine(Path.GetTempPath(),"Frontier-expansion-integration-"+Guid.NewGuid().ToString("N"));
            Assert.IsFalse(Directory.Exists(directory));Directory.CreateDirectory(directory);
            SetPrivate("saveDirectoryOverride",Path.Combine(directory,"Saves"));
            SetPrivate("reportDirectoryOverride",Path.Combine(directory,"Reports"));
            // Use a real, private preferences file so a match load is checked
            // against both the live service and a fresh disk reader.
            // Keep the exact service captured by DiscreteInputBuffer and audio;
            // replacing it after Awake would leave those subscriptions stale.
            string preferences=Path.Combine(directory,"Preferences","player-settings.json");
            typeof(PlayerSettingsService).GetProperty("Path").GetSetMethod(true).Invoke(game.Settings,new object[]{preferences});
            Assert.IsTrue(game.Settings.Persistent);Assert.AreEqual(preferences,game.Settings.Path);
            yield return Ready();
        }
        [UnityTearDown] public IEnumerator RemoveOnlyOwnedIntegrationStorage()
        {
            // NativeInputFixture destroys the session afterwards. Its OnDestroy
            // releases subscriptions and does not create a match report.
            if(game!=null)game.enabled=false;
            if(directory!=null&&Directory.Exists(directory))Directory.Delete(directory,true);
            yield return null;
        }
        void SetPrivate(string name,object value)
        {var field=typeof(GameController).GetField(name,PrivateInstance);Assert.IsNotNull(field,name+" is required for storage isolation");field.SetValue(game,value);}
        Button ExactButton(string title)=>session.GetComponentsInChildren<Button>().Single(b=>b.isActiveAndEnabled&&b.name==title);
        Text[] VisibleTexts()=>session.GetComponentsInChildren<Text>().Where(t=>t.isActiveAndEnabled).ToArray();
        static Rect ScreenBounds(RectTransform rect)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            var points=corners.Select(p=>RectTransformUtility.WorldToScreenPoint(null,p)).ToArray();
            return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
        }
        static void OnScreen(Rect bounds,string name)
        {
            Assert.Greater(bounds.width,1,name);Assert.Greater(bounds.height,1,name);
            Assert.GreaterOrEqual(bounds.xMin,-1,name);Assert.GreaterOrEqual(bounds.yMin,-1,name);
            Assert.LessOrEqual(bounds.xMax,Screen.width+1,name);Assert.LessOrEqual(bounds.yMax,Screen.height+1,name);
        }
        IEnumerator Drag(Vector2 from,Vector2 to)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState{position=from}.WithButton(MouseButton.Left));yield return Ready();
            InputSystem.QueueStateEvent(mouse,new MouseState{position=Vector2.Lerp(from,to,.45f)}.WithButton(MouseButton.Left));yield return Ready();
            InputSystem.QueueStateEvent(mouse,new MouseState{position=to}.WithButton(MouseButton.Left));yield return Ready();
            InputSystem.QueueStateEvent(mouse,new MouseState{position=to});yield return Ready();
        }
        void AssertAllResultRows(string title)
        {
            var text=VisibleTexts();Assert.IsTrue(text.Any(t=>t.text==title),"The result must use the local player's actual team outcome");
            for(int i=0;i<4;i++)
            {
                var p=game.World.Players[i];string prefix="P"+(i+1)+" / "+(p.team+1)+" · ";
                var row=text.Single(t=>t.text.StartsWith(prefix));StringAssert.Contains(game.Rules.Civ(p.civilization).name,row.text);
                OnScreen(ScreenBounds(row.rectTransform),prefix);
            }
        }
        IEnumerator FourSlotRoundTrip(string mode)
        {
            game.ReturnToMenu();game.Hud.ShowRoster();yield return Ready();
            yield return Click(ButtonCenter(ExactButton("1 VS 1 DUEL")));
            if(mode=="ffa")yield return Click(ButtonCenter(ExactButton("2 VS 2 TEAMS")));
            Assert.AreEqual(mode,game.SkirmishMode);
            for(int slot=1;slot<=4;slot++)Assert.IsNotNull(FindButton("SLOT "+slot+" ·"));
            yield return Click(ButtonCenter(FindButton("DONE")));
            game.SelectedMap=MapTopology.Frontier;game.SelectedObjective="elimination";
            game.PlayerCivilization="coast";game.OpponentCivilization="river";
            game.SetRosterCivilization(2,"france");game.SetRosterCivilization(3,"highland");game.StartMatch();yield return Ready();
            var expectedTeams=mode=="teams"?new[]{0,1,0,1}:new[]{0,1,2,3};
            CollectionAssert.AreEqual(expectedTeams,game.World.Players.Select(p=>p.team).ToArray());
            CollectionAssert.AreEqual(new[]{3,2,1},game.Simulation.CaptureState().controllers.Select(c=>c.player).ToArray());
            for(int step=0;step<35;step++)Assert.IsTrue(game.Simulation.Step());
            foreach(int player in new[]{1,2,3})Assert.IsTrue(game.World.Events.Any(e=>e.kind=="Payment"&&e.player==player),"AI slot "+player+" must really purchase from its own economy");
            Assert.IsFalse(game.World.Events.Any(e=>e.kind=="Payment"&&e.player==0),"The human slot must not acquire an AI controller");
            game.TogglePause();Assert.IsTrue(game.SaveMatch(1));var saved=MatchSaveStore.Read(MatchSaveStore.SlotPath(game.SaveDirectory,1),Application.version);
            Assert.AreEqual(4,saved.world.players.Length);string id=game.MatchId;var previous=game.World;
            game.ReturnToMenu();game.SkirmishMode="duel";Assert.IsTrue(game.LoadMatch(1));yield return Ready();
            Assert.AreNotSame(previous,game.World);Assert.AreEqual(id,game.MatchId);Assert.AreEqual(mode,game.SkirmishMode);
            Assert.IsNull(game.Guided,"An ordinary skirmish checkpoint must not create a default lesson session");
            Assert.AreEqual(JsonUtility.ToJson(saved.world),JsonUtility.ToJson(game.World.CaptureState()),"All four economies, queues, fog and teams must survive the controller's cold reconstruction");
            CollectionAssert.AreEqual(expectedTeams,game.World.Players.Select(p=>p.team).ToArray());
            Assert.AreEqual(JsonUtility.ToJson(saved.simulation),JsonUtility.ToJson(game.Simulation.CaptureState()));
            game.TogglePause();Assert.IsTrue(game.Simulation.Step());
            if(mode=="teams")
            {
                // Slot 1 can be eliminated while its teammate wins. A result
                // implemented as Winner == 0 would incorrectly show defeat.
                Assert.IsTrue(game.Command(new GameCommand{player=0,kind=CommandKind.Resign}).accepted);
                Assert.IsFalse(game.World.IsMatchEnded);Assert.IsTrue(game.Command(new GameCommand{player=1,kind=CommandKind.Resign}).accepted);
                Assert.IsFalse(game.World.IsMatchEnded);Assert.IsTrue(game.Command(new GameCommand{player=3,kind=CommandKind.Resign}).accepted);
                Assert.AreEqual(2,game.World.Winner);Assert.IsTrue(game.World.IsPlayerWinner(0));Assert.IsTrue(game.World.IsPlayerWinner(2));
            }
            else
            {
                for(int player=1;player<4;player++)
                {Assert.IsTrue(game.Command(new GameCommand{player=player,kind=CommandKind.Resign}).accepted);Assert.AreEqual(player==3,game.World.IsMatchEnded);}
                Assert.IsTrue(game.World.IsPlayerWinner(0));Assert.IsFalse(game.World.IsPlayerWinner(2));
            }
            game.Hud.ShowResult();yield return Ready();AssertAllResultRows("VICTORY");
            Assert.IsFalse(game.Simulation.Step(),"A completed match cannot dispatch another AI step");
        }
        [UnityTest] public IEnumerator TeamRosterColdLoadRetainsFourEconomiesAndShowsVictoryForTheSurvivingAlly()
        {yield return FourSlotRoundTrip("teams");}
        [UnityTest] public IEnumerator FreeForAllMenuColdLoadAndFinalResultRetainAllFourIndependentSlots()
        {yield return FourSlotRoundTrip("ffa");}

        void AssertRenderedChinese()
        {
            var text=VisibleTexts().Where(t=>t.text.Any(c=>c>='\u2e80')).ToArray();Assert.IsNotEmpty(text,"The actual lesson UI must contain its Chinese catalog text");
            foreach(var label in text)
            {
                Assert.IsNotNull(label.font,label.text);label.font.RequestCharactersInTexture(label.text,label.fontSize,label.fontStyle);
                foreach(char c in label.text.Where(c=>c>='\u2e80').Distinct())Assert.IsTrue(label.font.HasCharacter(c),"Missing lesson glyph '"+c+"' in "+label.font.name);
                Assert.Greater(label.cachedTextGenerator.characterCountVisible,0,"The active text must generate visible glyphs");
            }
        }
        [UnityTest] public IEnumerator LessonAndCampaignPagesUseAFontContainingTheirRealChineseInstructions()
        {
            game.ReturnToMenu();
            foreach(bool campaign in new[]{false,true})
            {
                int pages=Mathf.CeilToInt(GuidedPlayCatalog.Definitions.Count(d=>d.Kind==(campaign?GuidedPlayKind.Campaign:GuidedPlayKind.Lesson))/4f);
                for(int page=0;page<pages;page++)
                {game.Hud.ShowGuidedLibrary(campaign,page);yield return Ready();AssertRenderedChinese();}
            }
            game.Hud.ShowGuidedLibrary(false);yield return Ready();var definition=GuidedPlayCatalog.Find("lesson_selection");
            yield return Click(ButtonCenter(FindButton(definition.Title)));Assert.AreEqual(definition.Id,game.Guided.Definition.Id);
            yield return new WaitForSecondsRealtime(.3f);yield return Ready();AssertRenderedChinese();
            yield return Click(ButtonCenter(FindButton("MISSION BRIEFING")));AssertRenderedChinese();
            foreach(var item in game.Guided.Checklist)Assert.IsTrue(VisibleTexts().Any(t=>t.text.Contains(item.Text)),"Briefing must show the real checklist item: "+item.Text);
        }
        [UnityTest] public IEnumerator GuidedSelectionProgressSurvivesControllerLoadAndPauseRestartCreatesANewLessonAttempt()
        {
            Assert.IsTrue(game.StartGuided("lesson_selection"));yield return Ready();
            string attempt=game.Guided.AttemptId;var scout=game.World.Get(game.Guided.SuggestedActors.Single());
            game.Controls.Focus(scout.position);
            // StartGuided intentionally suppresses world edges for .22 seconds.
            yield return new WaitForSecondsRealtime(.25f);yield return Ready();
            var point=ScreenPoint(scout.position,game.World.SurfaceHeight(scout.position)+1);AssertWorldPoint(point);
            game.enabled=true;yield return Click(point);game.enabled=false;
            Assert.AreEqual(scout.id,game.Controls.FirstSelected?.id);Assert.IsTrue(game.Guided.Checklist[0].Complete,"Actual pointer selection must reach GameController's guided observer");
            Assert.AreEqual(GuidedPlayGoal.Move,game.Guided.Stage.Goal);
            game.TogglePause();Assert.IsTrue(game.SaveMatch(1));var saved=MatchSaveStore.Read(MatchSaveStore.SlotPath(game.SaveDirectory,1),Application.version);
            game.ReturnToMenu();Assert.IsTrue(game.LoadMatch(1));yield return Ready();
            Assert.AreEqual(attempt,game.Guided.AttemptId);Assert.AreEqual(JsonUtility.ToJson(saved.guided),JsonUtility.ToJson(game.Guided.CaptureState()));
            Assert.IsTrue(game.Guided.Checklist[0].Complete);Assert.IsTrue(game.IsPaused);
            yield return Click(ButtonCenter(FindButton("RESTART")));yield return Ready();
            Assert.AreEqual("lesson_selection",game.Guided.Definition.Id);Assert.AreNotEqual(attempt,game.Guided.AttemptId);
            Assert.AreEqual(GuidedPlayGoal.Selection,game.Guided.Stage.Goal);Assert.IsFalse(game.Guided.Checklist[0].Complete);
            Assert.IsEmpty(game.Simulation.CaptureState().controllers,"Retry must not silently start an ordinary AI skirmish");
        }
        [UnityTest] public IEnumerator LoadingAnOlderMatchCannotOverwriteNewerDurablePlayerSettings()
        {
            var old=game.Settings.Current;old.muted=true;old.musicEnabled=false;old.environmentEnabled=false;
            Assert.IsTrue(game.Settings.TryUpdate(old,out var reason),reason);game.TogglePause();Assert.IsTrue(game.SaveMatch(1));
            var checkpoint=MatchSaveStore.Read(MatchSaveStore.SlotPath(game.SaveDirectory,1),Application.version);
            Assert.IsTrue(checkpoint.muted);Assert.IsFalse(checkpoint.music);Assert.IsFalse(checkpoint.environmentSound);
            var current=game.Settings.Current;current.muted=false;current.musicEnabled=true;current.environmentEnabled=true;
            current.masterVolume=.43f;current.sfxVolume=.61f;current.uiScale=1.25f;current.friendEnemyPatterns=true;current.highContrastMinimap=true;
            Assert.IsTrue(game.Settings.TryUpdate(current,out reason),reason);Assert.IsTrue(game.Settings.TryRebind(PlayerAction.AttackMove,Key.Q,out reason),reason);
            string expected=JsonUtility.ToJson(game.Settings.Current);byte[] disk=File.ReadAllBytes(game.Settings.Path);var service=game.Settings;
            Assert.IsTrue(game.LoadMatch(1));yield return Ready();Assert.AreSame(service,game.Settings);
            Assert.AreEqual(expected,JsonUtility.ToJson(game.Options));CollectionAssert.AreEqual(disk,File.ReadAllBytes(game.Settings.Path));
            Assert.AreEqual(expected,JsonUtility.ToJson(PlayerSettingsService.Load(Path.Combine(directory,"Preferences")).Current));
            Assert.IsFalse(game.Muted);Assert.IsTrue(game.MusicEnabled);Assert.IsTrue(game.EnvironmentSoundEnabled);
            game.TogglePause();yield return new WaitForSecondsRealtime(.25f);var worker=game.World.Entities.First(e=>e.Alive&&e.owner==0&&e.Worker);game.Controls.Select(worker);
            yield return Press(Key.A);Assert.IsFalse(game.Controls.AttackMode);yield return Press(Key.Q);Assert.IsTrue(game.Controls.AttackMode,"The current binding must remain live after loading an older checkpoint");
        }
        [UnityTest] public IEnumerator EnlargedUiSliderAndScrolledActionsRemainReachableThroughRealPointerGestures()
        {
            game.TogglePause();game.Hud.ShowSettings();yield return Ready();yield return Click(ButtonCenter(FindButton("ACCESSIBILITY")));
            var slider=session.GetComponentsInChildren<Slider>().Single(s=>s.name=="UI scale slider");var track=ScreenBounds((RectTransform)slider.transform);
            OnScreen(track,slider.name);float before=slider.value;
            yield return Drag(new Vector2(track.center.x,track.center.y),new Vector2(track.xMax-1,track.center.y));
            Assert.Greater(slider.value,before+.2f,"Dragging the actual UGUI handle must change the settings draft");
            float scale=slider.value;yield return Click(ButtonCenter(FindButton("SAVE SETTINGS")));Assert.AreEqual(scale,game.Options.uiScale,.001f);
            yield return Ready();foreach(var button in session.GetComponentsInChildren<Button>().Where(b=>b.isActiveAndEnabled))OnScreen(ScreenBounds((RectTransform)button.transform),button.name);
            yield return Click(ButtonCenter(FindButton("RESUME")));yield return new WaitForSecondsRealtime(.25f);
            var worker=game.World.Entities.First(e=>e.Alive&&e.owner==0&&e.Worker);game.Controls.Select(worker);yield return Ready();
            var scroll=session.GetComponentsInChildren<ScrollRect>().Single(s=>s.name=="Context action viewport");
            var viewport=ScreenBounds(scroll.viewport);OnScreen(viewport,scroll.name);var actions=scroll.content.GetComponentsInChildren<Button>();
            Assert.Greater(scroll.content.rect.height,scroll.viewport.rect.height,"Enlarged worker actions must have actual overflow to exercise scrolling");
            foreach(var button in actions)
            {
                var bounds=ScreenBounds((RectTransform)button.transform);
                Assert.GreaterOrEqual(bounds.xMin,viewport.xMin-1,button.name);Assert.LessOrEqual(bounds.xMax,viewport.xMax+1,button.name);
            }
            for(int i=0;i<actions.Length;i++)for(int j=i+1;j<actions.Length;j++)
                Assert.IsFalse(ScreenBounds((RectTransform)actions[i].transform).Overlaps(ScreenBounds((RectTransform)actions[j].transform)),"Distinct action hitboxes must not overlap");
            float top=scroll.verticalNormalizedPosition;var first=FindButton("ATTACK MOVE");var from=ButtonCenter(first);
            yield return Drag(from,from+Vector2.up*130);Assert.Less(scroll.verticalNormalizedPosition,top-.1f,"Pointer drag must scroll the real masked action area");
            var board=FindButton("BOARD SHIP");var boardBounds=ScreenBounds((RectTransform)board.transform);
            Assert.GreaterOrEqual(boardBounds.yMin,viewport.yMin-1);Assert.LessOrEqual(boardBounds.yMax,viewport.yMax+1);
            yield return Click(ButtonCenter(board));Assert.IsTrue(game.Controls.EmbarkMode,"A formerly clipped action must remain clickable after scrolling");
        }
    }
}
