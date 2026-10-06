using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Frontier.Tests
{
    public sealed class RiverContentController070Tests
    {
        const BindingFlags PrivateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
        GameObject root;GameController game;string directory;
        [UnitySetUp] public IEnumerator SetUp()
        {
            directory=Path.Combine(Path.GetTempPath(),"frontier-river-controller070-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            root=new GameObject("Isolated river content 070 controller");game=root.AddComponent<GameController>();
            SetField("dataDirectoryOverride",directory);SetField("saveDirectoryOverride",Path.Combine(directory,"Saves"));
            SetField("reportDirectoryOverride",Path.Combine(directory,"Reports"));SetField("replayDirectoryOverride",Path.Combine(directory,"Replays"));
            yield return null;game.enabled=false;Assert.IsTrue(game.InMenu);Assert.IsNull(game.World);
            SetProperty(game,"Progress",AccountProgressStore.Load(Path.Combine(directory,"Account")));
            var settings=game.Settings.Current;settings.muted=true;settings.musicEnabled=false;Assert.IsTrue(game.Settings.TryUpdate(settings,out string why),why);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if(game!=null)
            {
                game.enabled=false;if(game.World!=null)game.ReturnToMenu();
                Invoke("FinishReplayFileWrites");Invoke("PollReplayOperations");Assert.IsFalse(game.ReplaySaving);
            }
            if(root!=null)UnityEngine.Object.Destroy(root);yield return null;
            if(directory!=null&&Directory.Exists(directory))Directory.Delete(directory,true);
        }
        void SetField(string name,object value)
        {var field=typeof(GameController).GetField(name,PrivateInstance);Assert.IsNotNull(field,name);field.SetValue(game,value);}
        static void SetProperty(object target,string name,object value)
        {var property=target.GetType().GetProperty(name);Assert.IsNotNull(property,name);property.GetSetMethod(true).Invoke(target,new[]{value});}
        void Invoke(string name)
        {var method=typeof(GameController).GetMethod(name,PrivateInstance);Assert.IsNotNull(method,name);method.Invoke(game,null);}
        Button ButtonContaining(string text)
        {
            return root.GetComponentsInChildren<Button>().Single(b=>b.gameObject.activeInHierarchy&&
                b.GetComponentsInChildren<Text>().Any(t=>t.text.Split('\n')[0].Contains(text)));
        }
        static string Explain(GameController g)=>"stage="+g.Guided?.Stage?.Id+" status="+g.Guided?.Status+" t="+g.World?.Time+" reason="+g.Guided?.Reason;
        void Until(Func<bool> predicate,float seconds,string why)
        {
            for(int i=0;i<Mathf.CeilToInt(seconds*10)&&!predicate()&&game.Guided?.Status==GuidedPlayStatus.Running;i++)game.Simulation.Step();
            Assert.IsTrue(predicate(),why+" "+Explain(game));
        }
        void Order(int[] actors,OrderKind kind,Vector2 point,int target=0)
        {
            var live=actors.Where(id=>game.World.Get(id)?.Alive==true&&game.World.Get(id).garrisonedIn==0).ToArray();
            var result=game.Command(new GameCommand{player=0,kind=CommandKind.Order,actors=live,order=kind,point=point,target=target});Assert.IsTrue(result.accepted,result.reason+" "+Explain(game));
        }
        Vector2 Region(string id)=>game.World.ScenarioDocument.regions.Single(r=>r.id==id).center;
        void ScoutSurvey()
        {
            var state=game.Guided.CaptureState();Order(state.soldiers,OrderKind.AttackMove,Region("survey-zone"));Order(new[]{state.scout},OrderKind.Move,Region("survey-zone"));
            Until(()=>game.Guided.Stage?.Id=="blockade",180,"The actual controller commands must complete the survey");
        }
        void ScoutFinish()
        {
            var state=game.Guided.CaptureState();var target=game.World.Get(state.firstTarget);
            foreach(string id in new[]{"guard-a","guard-b"})
            {
                var guard=game.World.ScenarioEntity(id);Assert.IsNotNull(guard,id);if(!guard.Alive)continue;
                if(!game.World.IsVisible(0,guard))
                {
                    Order(state.soldiers,OrderKind.AttackMove,game.World.Navigation.NearestWalkable(guard.position+new Vector2(-8,-4),.4f));
                    Until(()=>game.World.IsVisible(0,guard),120,"Reveal the actual guard before the directed attack");
                }
                Order(state.soldiers,OrderKind.Attack,guard.position,guard.id);
                Until(()=>!guard.Alive,120,"The controller must clear named guards before concentrating on the market");
            }
            if(!game.World.IsVisible(0,target))
            {
                Order(state.soldiers,OrderKind.AttackMove,game.World.Navigation.NearestWalkable(target.position+new Vector2(-8,-4),.4f));
                Until(()=>game.World.IsVisible(0,target),120,"Reveal blockade normally before the named attack");
            }
            Order(state.soldiers,OrderKind.Attack,target.position,target.id);
            Until(()=>game.Guided.Status!=GuidedPlayStatus.Running,180,"Actual combat must finish the chapter");
            Assert.AreEqual(GuidedPlayStatus.Completed,game.Guided.Status,Explain(game));
        }
        [UnityTest] public IEnumerator RiverLibraryButtonsExposeTheNewIndependentArcAndLockedChapters()
        {
            game.Hud.ShowRiverContent070();yield return null;
            Assert.IsTrue(ButtonContaining("TRADERS' BEND").interactable);
            Assert.IsTrue(ButtonContaining(GuidedPlayCatalog.Find(RiverContent070.Scout).Title).interactable);
            Assert.IsFalse(ButtonContaining(GuidedPlayCatalog.Find(RiverContent070.Convoy).Title).interactable);
            Assert.IsFalse(ButtonContaining(GuidedPlayCatalog.Find(RiverContent070.Retreat).Title).interactable);
            Assert.AreEqual(4,root.GetComponentsInChildren<Button>().Count(b=>b.isActiveAndEnabled&&b.name=="EDIT COPY"));
            Assert.IsTrue(game.Progress.IsUnlocked(RiverContent070.Scout));Assert.IsFalse(game.Progress.IsUnlocked("campaign_bridge"));
            ButtonContaining(GuidedPlayCatalog.Find(RiverContent070.Scout).Title).onClick.Invoke();
            Assert.IsFalse(game.InMenu);Assert.IsNotNull(game.Guided);Assert.AreEqual(RiverContent070.Scout,game.Guided.Definition.Id);
            Assert.AreEqual("survey",game.Guided.Stage.Id);Assert.IsTrue(game.World.HasScenario);
            Assert.IsNull(game.ScenarioEditor,"Starting a campaign must not masquerade as workshop playtest");
        }
        [UnityTest] public IEnumerator StandardMapMenuSelectionStartsTheFrozenOrdinaryMapWithTheChosenCivilizations()
        {
            game.PlayerCivilization="coast";game.OpponentCivilization="river";game.OpponentDifficulty="easy";
            game.Hud.ShowRiverContent070();yield return null;ButtonContaining("TRADERS' BEND").onClick.Invoke();
            Assert.IsTrue(game.InMenu);Assert.IsNotNull(game.SelectedScenario);Assert.AreEqual(RiverContent070.Skirmish,game.SelectedScenario.id);
            string selected=ScenarioCodec.ContentHash(game.SelectedScenario);game.StartMatch();
            Assert.IsFalse(game.InMenu);Assert.IsNull(game.Guided);Assert.IsFalse(game.IsScenarioPlaytest);Assert.IsTrue(game.World.HasScenario);
            Assert.AreEqual(selected,game.World.ScenarioHash);CollectionAssert.AreEqual(new[]{"coast","river"},game.World.Players.Select(p=>p.civilization));
            Assert.AreEqual("elimination",game.World.MapObjective.mode);Assert.IsEmpty(game.World.ScenarioDocument.objectives);
            Assert.AreEqual(6,game.World.Entities.Count(e=>e.owner==0&&e.Worker));Assert.AreEqual(300,game.World.Players[0].resources.wood);
            Assert.IsFalse(game.World.Entities.Any(e=>e.owner>=0&&(e.def.waterborne||e.def.id=="dock")));
            Assert.AreEqual(2,game.World.Layout.Bridges.Length);Assert.AreEqual(4,game.World.TradeSockets.Count);
            Assert.IsFalse(game.SelectRiverContent(RiverContent070.Skirmish),"An active match cannot be replaced through a menu-only selection");
        }
        [UnityTest] public IEnumerator StandardAuthoredMapDoesNotSilentlyIgnoreACustomaryBridgeObjective()
        {
            Assert.IsTrue(game.SelectRiverContent(RiverContent070.Skirmish));game.SelectedObjective="defend_bridge";game.StartMatch();
            Assert.IsFalse(game.InMenu,game.Notice);Assert.IsTrue(game.World.HasScenario);Assert.IsEmpty(game.World.ScenarioDocument.objectives);
            Assert.AreEqual("defend_bridge",game.World.MapObjective.mode);Assert.AreEqual("defend_bridge",game.SelectedObjective);
            for(int i=0;i<10;i++)Assert.IsTrue(game.Simulation.Step());
            Assert.Greater(game.World.MapObjective.elapsed,0,"The selected standard objective must actually tick");
            yield return null;game.Hud.ShowGame();yield return null;
            Assert.IsTrue(root.GetComponentsInChildren<Text>().Any(t=>t.isActiveAndEnabled&&t.text.Contains("DEFEND THE BRIDGE")),"Real HUD must explain the active customary objective");
            game.ReturnToMenu();Assert.AreEqual("defend_bridge",game.SelectedObjective);game.StartMatch();
            Assert.AreEqual("defend_bridge",game.World.MapObjective.mode,"Menu restart must retain the selected mode");
        }
        [UnityTest] public IEnumerator AuthoredWorkshopCopyRetainsItsOwnGoalsAndCannotEarnChapterUnlocks()
        {
            Assert.IsTrue(game.OpenScenarioDocument(RiverContent070.Build(game.Rules,RiverContent070.Convoy)));
            var session=game.ScenarioEditor;string original=ScenarioCodec.ContentHash(session.Document);
            Assert.IsTrue(game.PlaytestScenario());Assert.IsTrue(game.IsScenarioPlaytest);Assert.IsNull(game.Guided);
            Assert.AreEqual("embark_group",game.World.ScenarioDocument.objectives.Single(o=>o.id=="embark").kind);
            Assert.AreEqual("elimination",game.World.MapObjective.mode);Assert.IsFalse(game.SaveLearningProgress());
            game.ReturnToMenu();Assert.IsTrue(game.ScenarioEditorOpen);Assert.AreSame(session,game.ScenarioEditor);
            Assert.AreEqual(original,ScenarioCodec.ContentHash(game.ScenarioEditor.Document));Assert.AreEqual(0,game.Progress.CompletedCount);
            Assert.IsFalse(game.Progress.IsUnlocked(RiverContent070.Convoy));yield return null;
        }
        [UnityTest] public IEnumerator ControllerColdSlotPreservesActualSurveyAndCompletesOnceThenUnlocksTheNextChapter()
        {
            Assert.IsTrue(game.StartGuided(RiverContent070.Scout));ScoutSurvey();game.World.Paused=true;
            Assert.IsTrue(game.SaveMatch(1),game.Notice);string attempt=game.Guided.AttemptId;string hash=game.World.ScenarioHash;
            var saved=MatchSaveStore.Read(MatchSaveStore.SlotPath(game.SaveDirectory,1),Application.version);string world=JsonUtility.ToJson(saved.world);
            string guided=JsonUtility.ToJson(saved.guided);Assert.AreEqual("blockade",game.Guided.Stage.Id);
            game.ReturnToMenu();Assert.IsTrue(game.LoadMatch(1),game.Notice);Assert.AreEqual(world,JsonUtility.ToJson(game.World.CaptureState()));
            Assert.AreEqual(guided,JsonUtility.ToJson(game.Guided.CaptureState()));Assert.AreEqual(attempt,game.Guided.AttemptId);Assert.AreEqual(hash,game.World.ScenarioHash);
            Assert.AreEqual(1,game.World.Events.Count(e=>e.kind=="GuidedScenarioSetup"));game.World.Paused=false;ScoutFinish();
            Assert.IsTrue(game.SaveLearningProgress(),game.Progress.LastError);Assert.IsTrue(game.SaveLearningProgress());Assert.AreEqual(1,game.Progress.CompletionCount(RiverContent070.Scout));
            Assert.IsTrue(game.Progress.IsUnlocked(RiverContent070.Convoy));Assert.IsFalse(game.Progress.IsUnlocked(RiverContent070.Retreat));
            Assert.IsFalse(game.Progress.IsUnlocked("campaign_bridge"));game.ReturnToMenu();game.Hud.ShowRiverContent070();yield return null;
            Assert.IsTrue(ButtonContaining(GuidedPlayCatalog.Find(RiverContent070.Convoy).Title).interactable);
            Assert.IsFalse(ButtonContaining(GuidedPlayCatalog.Find(RiverContent070.Retreat).Title).interactable);
            ButtonContaining(GuidedPlayCatalog.Find(RiverContent070.Convoy).Title).onClick.Invoke();
            Assert.AreEqual(RiverContent070.Convoy,game.Guided.Definition.Id);Assert.AreEqual("embark",game.Guided.Stage.Id);
            Assert.AreEqual(6,game.Guided.CaptureState().soldiers.Length);Assert.AreEqual(0,game.Guided.CaptureState().stageIndex);
        }
        [UnityTest] public IEnumerator LockedChaptersAndARejectedMapSelectionDoNotReplaceAPlayableWorld()
        {
            Assert.IsFalse(game.StartGuided(RiverContent070.Convoy));Assert.IsNull(game.World);Assert.IsTrue(game.InMenu);
            Assert.IsFalse(game.SelectRiverContent(RiverContent070.Scout));Assert.IsNull(game.SelectedScenario);
            Assert.IsTrue(game.SelectRiverContent(RiverContent070.Skirmish));game.StartMatch();var world=game.World;var simulation=game.Simulation;
            Assert.IsFalse(game.StartGuided(RiverContent070.Retreat));Assert.AreSame(world,game.World);Assert.AreSame(simulation,game.Simulation);
            Assert.IsNull(game.Guided);yield return null;
        }
    }
}
