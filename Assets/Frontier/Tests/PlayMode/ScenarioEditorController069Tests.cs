using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Frontier.Tests
{
    // Exercise the public controller routes, including the real fixed-step
    // simulation and completion Update. Every durable path belongs to this fixture.
    public sealed class ScenarioEditorController069Tests
    {
        const BindingFlags PrivateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
        GameObject root;GameController game;string directory;

        [UnitySetUp] public IEnumerator SetUp()
        {
            directory=Path.Combine(Path.GetTempPath(),"Frontier-scenario-controller069-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            root=new GameObject("Isolated scenario editor controller");game=root.AddComponent<GameController>();
            SetField("dataDirectoryOverride",directory);
            SetField("saveDirectoryOverride",Path.Combine(directory,"Saves"));
            SetField("reportDirectoryOverride",Path.Combine(directory,"Reports"));
            SetField("replayDirectoryOverride",Path.Combine(directory,"Replays"));
            // Allow the normal Start/menu initialization once. Subsequent steps
            // are explicit, so rendering speed cannot decide trigger timing.
            yield return null;game.enabled=false;
            Assert.IsTrue(game.InMenu);Assert.IsNull(game.World);
            Assert.IsFalse(game.Settings.Persistent,"Editor initialization must not read personal preferences");
            Assert.IsTrue(game.Progress.MemoryOnly,"Editor initialization must not read personal learning progress");
            SetProperty(game,"Progress",AccountProgressStore.Load(Path.Combine(directory,"Account")));
            SetProperty(game.Settings,"Path",Path.Combine(directory,"Preferences","player-settings.json"));
            var preferences=game.Settings.Current;preferences.musicEnabled=false;preferences.muted=true;
            Assert.IsTrue(game.Settings.TryUpdate(preferences,out string reason),reason);
            Assert.IsTrue(game.Settings.Persistent);Assert.IsFalse(game.Progress.MemoryOnly);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if(game!=null)
            {
                game.enabled=false;
                if(game.World!=null)game.ReturnToMenu();
                Invoke("FinishReplayFileWrites");Invoke("PollReplayOperations");
                Assert.IsFalse(game.ReplaySaving,"Owned replay writes must finish before removing the fixture");
            }
            if(root!=null)UnityEngine.Object.Destroy(root);yield return null;
            if(directory!=null&&Directory.Exists(directory))Directory.Delete(directory,true);
        }
        void SetField(string name,object value)
        {var field=typeof(GameController).GetField(name,PrivateInstance);Assert.IsNotNull(field,name);field.SetValue(game,value);}
        static void SetProperty(object target,string name,object value)
        {var property=target.GetType().GetProperty(name);Assert.IsNotNull(property,name);var setter=property.GetSetMethod(true);Assert.IsNotNull(setter,name);setter.Invoke(target,new[]{value});}
        void Invoke(string name)
        {var method=typeof(GameController).GetMethod(name,PrivateInstance);Assert.IsNotNull(method,name);method.Invoke(game,null);}
        static void Steps(MatchSimulation simulation,int count)
        {for(int i=0;i<count;i++)Assert.IsTrue(simulation.Step(),"Simulation stopped at step "+simulation.StepCount);}
        ScenarioDocument Draft(bool finish=false)
        {
            var document=new ScenarioDocument{id="controller-scenario",name="Controller scenario",rulesHash=SnapshotCodec.RulesHash(game.Rules),
                starts=new[]{
                    new ScenarioStart{player=0,team=0,civilization="france",age=2,initialResources=new Cost(321,432,543),
                        town=new Vector2(20,20),explorer=new Vector2(27,24),workers=new[]{new Vector2(25,17)}},
                    new ScenarioStart{player=1,team=1,civilization="river",initialResources=new Cost(300,300,100),
                        town=new Vector2(108,108),explorer=new Vector2(101,104),workers=new[]{new Vector2(103,111)}}},
                objectives=new[]{new ScenarioObjective{id="bonus",title="Receive the authored reward",required=false}},
                triggers=new[]{new ScenarioTrigger{id="reward",conditions=new[]{new ScenarioCondition{time=.2f}},
                    actions=new[]{new ScenarioAction{kind="grant_resource",resource=ResourceKind.Coin,amount=73},
                        new ScenarioAction{kind="complete_objective",objectiveId="bonus"}}}}};
            if(finish)document.triggers=document.triggers.Concat(new[]{new ScenarioTrigger{id="finish",conditions=new[]{new ScenarioCondition{time=.7f}},
                actions=new[]{new ScenarioAction{kind="win",player=0,message="Controller playtest completed"}}}}).ToArray();
            Assert.AreEqual("",ScenarioCompiler.Validate(document,game.Rules,2));return document;
        }
        byte[] SeedAccountSentinel()
        {
            using(var lesson=GuidedPlaySession.Start(game.Rules,"lesson_selection","editor-account-sentinel"))
            {
                int[] actors=lesson.SuggestedActors.ToArray();lesson.ObserveSelection(actors);
                var result=lesson.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=actors,order=OrderKind.Move,point=lesson.ObjectivePoint});
                Assert.IsTrue(result.accepted,result.reason);
                int guard=0;while(lesson.Status==GuidedPlayStatus.Running&&guard++<1000)Steps(lesson.Simulation,1);
                Assert.AreEqual(GuidedPlayStatus.Completed,lesson.Status,lesson.Reason);
                Assert.IsTrue(game.Progress.RecordCompletion(lesson),game.Progress.LastError);
            }
            Assert.AreEqual(1,game.Progress.CompletionCount("lesson_selection"));
            return File.ReadAllBytes(game.Progress.FilePath);
        }
        void AccountUnchanged(byte[] bytes)
        {
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(game.Progress.FilePath),"Authored play must not change campaign learning progress");
            Assert.AreEqual(1,game.Progress.CompletionCount("lesson_selection"));
            Assert.AreEqual(1,AccountProgressStore.Load(Path.Combine(directory,"Account")).CompletionCount("lesson_selection"));
        }
        IEnumerator ReadyUi()
        {Canvas.ForceUpdateCanvases();yield return null;Canvas.ForceUpdateCanvases();}
        static Vector2 RectScreenPoint(RectTransform rect,float x=.5f,float y=.5f)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            return RectTransformUtility.WorldToScreenPoint(null,Vector3.Lerp(Vector3.Lerp(corners[0],corners[3],x),Vector3.Lerp(corners[1],corners[2],x),y));
        }
        RectTransform MapCanvas()=>root.GetComponentsInChildren<ScenarioEditorInput>().Single(i=>i.isActiveAndEnabled).transform as RectTransform;
        Button UiButton(string title,string parent=null)=>root.GetComponentsInChildren<Button>().Single(b=>b.isActiveAndEnabled&&
            b.name.TrimStart('▸',' ')==title&&(parent==null||b.transform.parent.name==parent));
        InputField UiField(string title)=>root.GetComponentsInChildren<InputField>().Single(f=>f.isActiveAndEnabled&&f.name==title);
        IEnumerator ClickUiButton(string title,string parent=null)
        {
            yield return ReadyUi();var button=UiButton(title,parent);Assert.IsTrue(button.interactable,title);
            Vector2 point=RectScreenPoint((RectTransform)button.transform);
            Assert.IsTrue(game.Hud.ProcessUiPointer(point,true),"The UGUI dispatcher did not capture "+title);
            Assert.IsTrue(game.Hud.ProcessUiPointer(point,false),"The UGUI dispatcher did not release "+title);
            yield return ReadyUi();
        }
        IEnumerator EndEditUiField(string title,string value)
        {
            yield return ReadyUi();var input=UiField(title);var rect=(RectTransform)input.transform;
            var scroll=input.GetComponentInParent<ScrollRect>();
            if(scroll!=null&&rect.IsChildOf(scroll.content))
            {
                var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.content,rect);
                float range=Mathf.Max(0,scroll.content.rect.height-scroll.viewport.rect.height);
                float offset=Mathf.Clamp(-scroll.viewport.rect.height*.5f-bounds.center.y,0,range);
                scroll.StopMovement();scroll.verticalNormalizedPosition=range==0?1:1-offset/range;
            }
            yield return ReadyUi();Vector2 point=RectScreenPoint(rect);
            if(scroll!=null)Assert.IsTrue(RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport,point),title+" must be inside the visible inspector");
            Assert.IsTrue(game.Hud.ProcessUiPointer(point,true),"The UGUI dispatcher did not capture field "+title);
            Assert.IsTrue(game.Hud.ProcessUiPointer(point,false),"The UGUI dispatcher did not release field "+title);
            yield return null;Assert.IsTrue(input.isFocused,title+" must enter the real InputField editing lifecycle");
            input.text=value;input.DeactivateInputField();EventSystem.current.SetSelectedGameObject(null);
            yield return ReadyUi();
        }
        IEnumerator ClickUiMap(Vector2 mapPoint)
        {
            yield return ReadyUi();var map=MapCanvas();Assert.AreEqual("Editable map canvas",map.name);
            Vector2 point=RectScreenPoint(map,mapPoint.x/128,mapPoint.y/128);
            Assert.IsTrue(game.Hud.ProcessUiPointer(point,true));Assert.IsTrue(game.Hud.ProcessUiPointer(point,false));yield return ReadyUi();
        }
        IEnumerator DragUiMap(Vector2[] path,string committedHash)
        {
            yield return ReadyUi();var map=MapCanvas();Vector2 point=RectScreenPoint(map,path[0].x/128,path[0].y/128);
            Assert.IsTrue(game.Hud.ProcessUiPointer(point,true));
            Assert.AreEqual(committedHash,ScenarioCodec.ContentHash(game.ScenarioEditor.Document),"Pointer down must only update the stroke preview");
            var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,pressPosition=point,position=point,pointerDrag=map.gameObject};
            for(int i=1;i<path.Length;i++)
            {
                Vector2 next=RectScreenPoint(map,path[i].x/128,path[i].y/128);pointer.delta=next-pointer.position;pointer.position=next;
                Assert.IsTrue(ExecuteEvents.Execute(map.gameObject,pointer,ExecuteEvents.dragHandler),"The visible canvas must own the real drag handler");
                Assert.AreEqual(committedHash,ScenarioCodec.ContentHash(game.ScenarioEditor.Document),"Drag previews must not create intermediate undo transactions");
            }
            Assert.IsTrue(game.Hud.ProcessUiPointer(pointer.position,false));yield return ReadyUi();
        }

        [UnityTest] public IEnumerator UndoPlaytestRewardAndCompletedUpdateReturnTheSameDraftAndHistoryWithoutLearningWrites()
        {
            byte[] account=SeedAccountSentinel(),preferences=File.ReadAllBytes(game.Settings.Path);
            var source=Draft(true);Assert.IsTrue(game.OpenScenarioDocument(source),game.ScenarioEditorMessage);
            var editor=game.ScenarioEditor;string originalHash=ScenarioCodec.ContentHash(editor.Document);
            Assert.IsTrue(game.ApplyScenarioEdit(d=>{d.heightsCm[60+60*129]=25;d.surfaces[72+75*128]=2;d.name="One terrain stroke";}));
            string strokeHash=ScenarioCodec.ContentHash(editor.Document);
            Assert.IsTrue(game.ApplyScenarioEdit(d=>d.name="Unfinished title"));Assert.IsTrue(editor.Undo());
            Assert.AreEqual(strokeHash,ScenarioCodec.ContentHash(editor.Document));Assert.IsTrue(editor.CanUndo);Assert.IsTrue(editor.CanRedo);

            Assert.IsTrue(game.PlaytestScenario(),game.ScenarioEditorMessage);
            var world=game.World;Assert.IsTrue(world.HasScenario);Assert.IsTrue(game.IsScenarioPlaytest);Assert.IsFalse(game.ScenarioEditorOpen);
            Assert.AreEqual(strokeHash,world.ScenarioHash);Assert.IsNull(game.Guided);
            Assert.AreEqual(2,world.Players[0].age);Assert.AreEqual(543,world.Players[0].resources.coin);
            source.name="Caller mutation";var exposed=world.ScenarioDocument;exposed.name="Getter mutation";exposed.heightsCm[60+60*129]=9000;
            Assert.AreEqual("One terrain stroke",world.ScenarioDocument.name);Assert.AreEqual(25,world.ScenarioDocument.heightsCm[60+60*129]);
            Assert.IsFalse(game.ApplyScenarioEdit(d=>d.name="Edit while playing"));
            var worker=world.ScenarioEntity(ScenarioCodec.StartWorkerId(0,0));Vector2 before=worker.position;
            var command=game.Command(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Move,point=new Vector2(38,17)});
            Assert.IsTrue(command.accepted,command.reason);Steps(game.Simulation,3);
            Assert.Greater(Vector2.Distance(before,worker.position),.1f,"A real order must modify the playtest world");
            Assert.AreEqual(616,world.Players[0].resources.coin,.001f);Assert.AreEqual(1,world.ScenarioState.triggerStates.Single(t=>t.id=="reward").fires);
            Assert.AreEqual("completed",world.ScenarioObjectiveStates.Single(o=>o.id=="bonus").status);
            Assert.AreEqual(1,world.Events.Count(e=>e.kind=="ScenarioResourceGranted"));
            Assert.AreEqual(strokeHash,ScenarioCodec.ContentHash(editor.Document));
            int guard=0;while(!world.IsMatchEnded&&guard++<20)Steps(game.Simulation,1);
            Assert.IsTrue(world.IsMatchEnded);Assert.AreEqual(0,world.Winner);
            // The real result/completion branch must also leave account progress alone.
            game.enabled=true;yield return null;yield return null;game.enabled=false;
            Assert.IsTrue(game.Hud.ResultVisible);Assert.IsFalse(game.SaveLearningProgress());AccountUnchanged(account);
            var resultPanel=UiButton("RETURN TO EDITOR","VICTORY").transform.parent;
            var resultTexts=resultPanel.GetComponentsInChildren<Text>().Select(t=>t.text).ToArray();
            Assert.IsTrue(resultTexts.Any(t=>t.StartsWith(world.ScenarioState.reason+" · ")),"The visible result must explain the authored victory");
            Assert.IsFalse(resultTexts.Any(t=>t.Contains("Elimination or resignation")),"Authored completion must not be labelled as an elimination victory");
            CollectionAssert.AreEqual(preferences,File.ReadAllBytes(game.Settings.Path));

            game.ReturnToMenu();Assert.IsTrue(game.InMenu);Assert.IsNull(game.World);Assert.IsNull(game.Simulation);
            Assert.AreSame(editor,game.ScenarioEditor);Assert.IsTrue(game.ScenarioEditorOpen);Assert.IsFalse(game.IsScenarioPlaytest);
            Assert.AreEqual(strokeHash,ScenarioCodec.ContentHash(editor.Document));Assert.IsTrue(editor.CanUndo);Assert.IsTrue(editor.CanRedo);
            Assert.IsTrue(editor.Redo());Assert.AreEqual("Unfinished title",editor.Document.name);
            Assert.IsTrue(editor.Undo());Assert.AreEqual(strokeHash,ScenarioCodec.ContentHash(editor.Document));
            Assert.IsTrue(editor.Undo());Assert.AreEqual(originalHash,ScenarioCodec.ContentHash(editor.Document),"One stroke must remain exactly one undo transaction");
            AccountUnchanged(account);yield return null;
        }

        [UnityTest] public IEnumerator SelectedDocumentAndStartMatchUseAnOwnedFrozenFourPlayerRoster()
        {
            var document=Draft();document.starts=document.starts.Concat(new[]{
                new ScenarioStart{player=2,team=0,civilization="coast",town=new Vector2(20,108),explorer=new Vector2(27,104),workers=new[]{new Vector2(25,111)}},
                new ScenarioStart{player=3,team=1,civilization="ottoman",town=new Vector2(108,20),explorer=new Vector2(101,24),workers=new[]{new Vector2(103,17)}}}).ToArray();
            string hash=ScenarioCodec.ContentHash(document);Assert.IsTrue(game.SelectAuthoredScenario(document),game.Notice);
            document.starts[0].initialResources.coin=999999;document.starts[2].team=3;document.name="Changed caller";
            var getter=game.SelectedScenario;getter.heightsCm[0]=9999;getter.starts[0].civilization="ottoman";
            Assert.AreEqual(hash,ScenarioCodec.ContentHash(game.SelectedScenario));game.StartMatch();
            var world=game.World;Assert.IsNotNull(world);Assert.IsTrue(world.HasScenario);Assert.IsFalse(game.InMenu);Assert.IsFalse(game.IsScenarioPlaytest);
            Assert.AreEqual(hash,world.ScenarioHash);Assert.AreEqual(4,world.Players.Length);Assert.AreEqual(MapTopology.Scenario,world.MapId);
            CollectionAssert.AreEqual(new[]{"france","river","coast","ottoman"},world.Players.Select(p=>p.civilization));
            Assert.IsTrue(world.AreAllies(0,2));Assert.IsTrue(world.AreAllies(1,3));Assert.IsTrue(world.AreEnemies(0,1));
            Assert.AreEqual(543,world.Players[0].resources.coin);Assert.AreEqual(2,world.Players[0].age);
            Assert.IsTrue(game.SelectAuthoredScenario(null));Assert.IsNull(game.SelectedScenario);
            Assert.AreEqual(hash,world.ScenarioHash);Assert.AreSame(world,game.World,"Changing next-match selection cannot replace the active world");
            Steps(game.Simulation,3);Assert.AreEqual(616,world.Players[0].resources.coin,.001f);
            game.ReturnToMenu();Assert.IsFalse(game.ScenarioEditorOpen);yield return null;
        }

        [UnityTest] public IEnumerator InvalidImportAndSelectionPreserveTheRunningWorldAndExistingDraft()
        {
            var document=Draft();Assert.IsTrue(game.OpenScenarioDocument(document));
            Assert.IsTrue(game.ApplyScenarioEdit(d=>d.name="Retained draft"));var editor=game.ScenarioEditor;
            string draftHash=ScenarioCodec.ContentHash(editor.Document);game.CloseScenarioEditor();
            Assert.IsTrue(game.SelectAuthoredScenario(document));game.StartMatch();Steps(game.Simulation,3);
            var world=game.World;var simulation=game.Simulation;string state=JsonUtility.ToJson(world.CaptureState()),selectedHash=ScenarioCodec.ContentHash(game.SelectedScenario);
            string badFile=Path.Combine(directory,"invalid.scenario.json");File.WriteAllText(badFile,"{\"schema\":1,\"payload\":");byte[] badBytes=File.ReadAllBytes(badFile);
            Assert.IsFalse(game.OpenScenarioFile(badFile));Assert.AreSame(world,game.World);Assert.AreSame(simulation,game.Simulation);Assert.IsFalse(game.InMenu);
            Assert.AreSame(editor,game.ScenarioEditor);Assert.AreEqual(draftHash,ScenarioCodec.ContentHash(editor.Document));Assert.IsTrue(editor.CanUndo);
            CollectionAssert.AreEqual(badBytes,File.ReadAllBytes(badFile),"An import failure must preserve its input file");
            var blocked=ScenarioCodec.Clone(document);blocked.surfaces[20+20*128]=3;
            Assert.AreEqual("",ScenarioCodec.Validate(blocked,game.Rules));Assert.IsFalse(game.SelectAuthoredScenario(blocked));
            Assert.AreEqual(selectedHash,ScenarioCodec.ContentHash(game.SelectedScenario));Assert.AreSame(world,game.World);
            Assert.AreEqual(state,JsonUtility.ToJson(world.CaptureState()));
            Assert.IsFalse(game.OpenScenarioDocument(document),"Editing cannot replace an active match");Assert.AreSame(editor,game.ScenarioEditor);
            game.ReturnToMenu();Assert.IsTrue(game.OpenScenarioEditor());Assert.AreSame(editor,game.ScenarioEditor);
            Assert.AreEqual(draftHash,ScenarioCodec.ContentHash(editor.Document));yield return null;
        }

        [UnityTest] public IEnumerator MenuImportAndInvalidMutationKeepTheDraftUndoRedoAndLibraryUntouched()
        {
            Assert.IsTrue(game.OpenScenarioDocument(Draft()));var editor=game.ScenarioEditor;
            Assert.IsTrue(game.ApplyScenarioEdit(d=>d.description="Committed description"));
            Assert.IsTrue(game.ApplyScenarioEdit(d=>d.name="Redo witness"));Assert.IsTrue(editor.Undo());
            string hash=ScenarioCodec.ContentHash(editor.Document);
            string badFile=Path.Combine(directory,"bad-identity.scenario.json");var envelope=JsonUtility.FromJson<ScenarioEnvelope>(ScenarioCodec.Encode(editor.Document,game.Rules));
            envelope.sha256=new string('0',64);File.WriteAllText(badFile,JsonUtility.ToJson(envelope));
            Assert.IsFalse(game.OpenScenarioFile(badFile));Assert.IsTrue(game.ScenarioEditorOpen);Assert.IsNull(game.World);
            Assert.AreSame(editor,game.ScenarioEditor);Assert.AreEqual(hash,ScenarioCodec.ContentHash(editor.Document));Assert.IsTrue(editor.CanUndo);Assert.IsTrue(editor.CanRedo);
            Assert.IsFalse(game.ApplyScenarioEdit(d=>{d.name="Would replace draft";d.resources=new[]{new ScenarioPlacement{id="bad",definition="unknown"}};}));
            Assert.AreEqual(hash,ScenarioCodec.ContentHash(editor.Document));Assert.IsTrue(editor.CanUndo);Assert.IsTrue(editor.CanRedo);
            Assert.IsFalse(Directory.Exists(game.ScenarioDirectory),"Importing and editing must not create or overwrite library files");
            Assert.IsTrue(editor.Redo());Assert.AreEqual("Redo witness",editor.Document.name);yield return null;
        }

        [UnityTest] public IEnumerator InvalidGeographyAndOpeningAgeRejectPlaytestBeforeReplacingEditorState()
        {
            Assert.IsTrue(game.OpenScenarioDocument(Draft()));var editor=game.ScenarioEditor;
            Assert.IsTrue(game.ApplyScenarioEdit(d=>d.surfaces[20+20*128]=3));string blockedHash=ScenarioCodec.ContentHash(editor.Document);
            Assert.IsNotEmpty(game.ValidateScenarioDraft());Assert.IsFalse(game.PlaytestScenario());
            Assert.IsTrue(game.InMenu);Assert.IsTrue(game.ScenarioEditorOpen);Assert.IsFalse(game.IsScenarioPlaytest);
            Assert.IsNull(game.World);Assert.IsNull(game.Simulation);Assert.IsNull(game.SelectedScenario);
            Assert.AreSame(editor,game.ScenarioEditor);Assert.AreEqual(blockedHash,ScenarioCodec.ContentHash(editor.Document));Assert.IsTrue(editor.CanUndo);
            Assert.IsTrue(editor.Undo());string validHash=ScenarioCodec.ContentHash(editor.Document);Assert.IsTrue(editor.CanRedo);
            game.AgeLimit=1;StringAssert.Contains("era limit",game.ValidateScenarioDraft());
            Assert.IsFalse(game.PlaytestScenario(),"The authored age 2 opening exceeds this match's age cap");
            Assert.IsTrue(game.ScenarioEditorOpen);Assert.IsNull(game.World);Assert.IsNull(game.SelectedScenario);
            Assert.AreEqual(validHash,ScenarioCodec.ContentHash(editor.Document));Assert.IsTrue(editor.CanRedo);
            game.AgeLimit=5;Assert.IsEmpty(game.ValidateScenarioDraft());Assert.IsTrue(game.PlaytestScenario(),game.ScenarioEditorMessage);Assert.AreEqual(validHash,game.World.ScenarioHash);
            game.ReturnToMenu();Assert.AreSame(editor,game.ScenarioEditor);Assert.IsTrue(editor.CanRedo);yield return null;
        }

        [UnityTest] public IEnumerator SaveAndExportRoundtripTheCurrentDraftAndFailedSaveKeepsIdentityAndHistory()
        {
            Assert.IsTrue(game.OpenScenarioDocument(Draft()));var editor=game.ScenarioEditor;
            Assert.IsTrue(game.ApplyScenarioEdit(d=>{d.description="Library witness";d.heightsCm[64+64*129]=20;}));
            Assert.IsTrue(game.SaveScenarioDraft("saved-controller","Saved controller"),game.ScenarioEditorMessage);
            string hash=ScenarioCodec.ContentHash(editor.Document);var row=game.Scenarios.List().Single();
            Assert.AreEqual("saved-controller",row.id);Assert.IsEmpty(row.reason);Assert.AreEqual(hash,row.contentHash);
            byte[] bytes=File.ReadAllBytes(row.path);string export=Path.Combine(directory,"Exports","review.scenario.json");
            Assert.IsTrue(game.ExportScenarioDraft(export),game.ScenarioEditorMessage);
            Assert.AreEqual(hash,ScenarioCodec.ContentHash(game.Scenarios.ReadFile(export)));
            Assert.IsFalse(game.SaveScenarioDraft("../escape","Invalid replacement"));Assert.AreSame(editor,game.ScenarioEditor);
            Assert.AreEqual(hash,ScenarioCodec.ContentHash(editor.Document));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(row.path));
            Assert.AreEqual(1,game.Scenarios.List().Length);Assert.IsTrue(editor.CanUndo);
            Assert.IsTrue(editor.Undo());Assert.AreEqual("controller-scenario",editor.Document.id,"Saving a new identity remains an undoable editor transaction");
            Assert.IsTrue(editor.Redo());Assert.AreEqual(hash,ScenarioCodec.ContentHash(editor.Document));
            Assert.IsTrue(game.OpenScenarioFile(export),game.ScenarioEditorMessage);Assert.AreNotSame(editor,game.ScenarioEditor);
            Assert.AreEqual(hash,ScenarioCodec.ContentHash(game.ScenarioEditor.Document));Assert.IsFalse(game.ScenarioEditor.CanUndo);
            Assert.AreEqual(1,game.Scenarios.List().Length,"Loading an export must not import another library entry");yield return null;
        }

        [UnityTest] public IEnumerator ControllerCheckpointRestoresEmbeddedScenarioAndFiredRewardAfterDeletingItsLibrarySource()
        {
            byte[] account=SeedAccountSentinel(),preferences=File.ReadAllBytes(game.Settings.Path);
            var document=Draft();string hash=ScenarioCodec.ContentHash(document),source=game.Scenarios.Write(document);
            Assert.IsTrue(game.SelectAuthoredScenario(game.Scenarios.Read(document.id)));game.StartMatch();Steps(game.Simulation,3);
            Assert.AreEqual(1,game.World.ScenarioState.triggerStates.Single(t=>t.id=="reward").fires);
            game.TogglePause();var oldWorld=game.World;string state=JsonUtility.ToJson(oldWorld.CaptureState()),matchId=game.MatchId;
            int step=game.Simulation.StepCount;Assert.IsTrue(game.SaveMatch(1),game.Notice);
            game.ReturnToMenu();File.Delete(source);Assert.IsTrue(game.SelectAuthoredScenario(null));
            Assert.IsTrue(game.LoadMatch(1),game.Notice);Assert.AreNotSame(oldWorld,game.World);
            Assert.IsTrue(game.World.HasScenario);Assert.AreEqual(hash,game.World.ScenarioHash);Assert.AreEqual(hash,ScenarioCodec.ContentHash(game.SelectedScenario));
            Assert.AreEqual(state,JsonUtility.ToJson(game.World.CaptureState()));Assert.AreEqual(step,game.Simulation.StepCount);Assert.AreEqual(matchId,game.MatchId);
            Assert.IsTrue(game.IsPaused);Assert.IsFalse(game.IsScenarioPlaytest);Assert.IsFalse(game.ScenarioEditorOpen);Assert.IsNull(game.Guided);
            Assert.IsFalse(File.Exists(source));Assert.IsEmpty(game.Scenarios.List());
            game.TogglePause();Steps(game.Simulation,3);
            Assert.AreEqual(616,game.World.Players[0].resources.coin,.001f);Assert.AreEqual(1,game.World.ScenarioState.triggerStates.Single(t=>t.id=="reward").fires);
            Assert.AreEqual(1,game.World.Events.Count(e=>e.kind=="ScenarioResourceGranted"),"Restoring a fired reward cannot grant it again");
            Assert.IsFalse(game.SaveLearningProgress());AccountUnchanged(account);CollectionAssert.AreEqual(preferences,File.ReadAllBytes(game.Settings.Path));
            game.ReturnToMenu();Assert.IsFalse(game.ScenarioEditorOpen);AccountUnchanged(account);yield return null;
        }

        [UnityTest] public IEnumerator UguiBrushPressDragReleaseCommitsOneStrokeAndToolbarUndoRedoRestoresItsExactHash()
        {
            Assert.IsTrue(game.OpenScenarioDocument(Draft()));var editor=game.ScenarioEditor;yield return ReadyUi();
            Assert.IsTrue(game.Hud.ScenarioCanvasVisible);Assert.IsNotNull(MapCanvas().GetComponent<RawImage>().texture);
            string before=ScenarioCodec.ContentHash(editor.Document);
            yield return EndEditUiField("RADIUS (m)","3");yield return EndEditUiField("HEIGHT STEP (cm)","75");
            Assert.AreEqual(before,ScenarioCodec.ContentHash(editor.Document));Assert.IsFalse(editor.CanUndo,"Changing brush settings does not edit map data");
            yield return DragUiMap(new[]{new Vector2(60,60),new Vector2(62,61),new Vector2(72,66)},before);
            var changed=editor.Document;string after=ScenarioCodec.ContentHash(changed);Assert.AreNotEqual(before,after);
            Assert.AreEqual(75,changed.heightsCm[60+60*129],"Overlapping brush samples must not raise the same vertex twice in one stroke");
            Assert.AreEqual(75,changed.heightsCm[72+66*129]);Assert.AreEqual(0,changed.heightsCm[90+90*129]);
            Assert.IsTrue(editor.CanUndo);Assert.IsFalse(editor.CanRedo);Assert.IsNull(game.World);
            yield return ClickUiButton("UNDO");Assert.AreEqual(before,ScenarioCodec.ContentHash(editor.Document));Assert.IsFalse(editor.CanUndo);Assert.IsTrue(editor.CanRedo);
            yield return ClickUiButton("REDO");Assert.AreEqual(after,ScenarioCodec.ContentHash(editor.Document));Assert.IsTrue(editor.CanUndo);Assert.IsFalse(editor.CanRedo);
        }

        [UnityTest] public IEnumerator UguiRiverHandleClickOnlySelectsAndDraggingMovesExactlyOneControlWithUndoRedo()
        {
            var document=Draft();document.rivers=new[]{new ScenarioRiver{id="free-bend",smooth=false,
                points=new[]{new Vector2(64,-8),new Vector2(70,50),new Vector2(60,100),new Vector2(64,136)},halfWidths=new[]{5f,7,6,5}}};
            Assert.IsTrue(game.OpenScenarioDocument(document));var editor=game.ScenarioEditor;string before=ScenarioCodec.ContentHash(editor.Document);
            yield return ClickUiButton("RIVERS");yield return ClickUiMap(new Vector2(71,51));
            Assert.AreEqual(before,ScenarioCodec.ContentHash(editor.Document),"Clicking beside a handle selects it without moving it to the pointer");
            Assert.AreEqual(new Vector2(70,50),editor.Document.rivers[0].points[1]);Assert.IsFalse(editor.CanUndo);
            Assert.AreEqual("1",UiField("CONTROL INDEX").text);Assert.AreEqual("70",UiField("CONTROL X").text);Assert.AreEqual("50",UiField("CONTROL Z").text);
            yield return DragUiMap(new[]{new Vector2(71,51),new Vector2(75,54),new Vector2(78,55)},before);
            var river=editor.Document.rivers[0];string after=ScenarioCodec.ContentHash(editor.Document);
            Assert.That(Vector2.Distance(new Vector2(78,55),river.points[1]),Is.LessThan(.001f));
            Assert.AreEqual(new Vector2(64,-8),river.points[0]);Assert.AreEqual(new Vector2(60,100),river.points[2]);CollectionAssert.AreEqual(new[]{5f,7,6,5},river.halfWidths);
            yield return ClickUiButton("UNDO");Assert.AreEqual(before,ScenarioCodec.ContentHash(editor.Document));Assert.IsFalse(editor.CanUndo);
            yield return ClickUiButton("REDO");Assert.AreEqual(after,ScenarioCodec.ContentHash(editor.Document));
        }

        [UnityTest] public IEnumerator UguiObjectPlacementAndInspectorEndEditValidateOwnerAndResourceAmount()
        {
            Assert.IsTrue(game.OpenScenarioDocument(Draft()));var editor=game.ScenarioEditor;
            yield return ClickUiButton("OBJECTS & RESOURCES");yield return ClickUiButton("POINTER: select");yield return ClickUiButton("place","POINTER");
            yield return EndEditUiField("NEW OBJECT OWNER","1");
            yield return EndEditUiField("CATALOG ID","musketeer");yield return ClickUiMap(new Vector2(44,40));
            var soldier=editor.Document.objects.Single();Assert.AreEqual("musketeer",soldier.definition);Assert.AreEqual(1,soldier.owner);
            Assert.That(Vector2.Distance(new Vector2(44,40),soldier.position),Is.LessThan(.001f));string soldierId=soldier.id;
            yield return EndEditUiField("OWNER","0");Assert.AreEqual(0,editor.Document.objects.Single().owner);Assert.AreEqual(soldierId,editor.Document.objects.Single().id);
            string ownerHash=ScenarioCodec.ContentHash(editor.Document);bool undo=editor.CanUndo,redo=editor.CanRedo;
            yield return EndEditUiField("OWNER","2");Assert.AreEqual(ownerHash,ScenarioCodec.ContentHash(editor.Document));Assert.AreEqual(undo,editor.CanUndo);Assert.AreEqual(redo,editor.CanRedo);
            StringAssert.Contains("whole number",game.ScenarioEditorMessage);
            yield return EndEditUiField("CATALOG ID","tree");yield return ClickUiMap(new Vector2(50,50));
            var resource=editor.Document.resources.Single();Assert.AreEqual("tree",resource.definition);Assert.AreEqual(-1,resource.owner);Assert.AreEqual(-1,resource.remaining);
            string resourceId=resource.id;yield return EndEditUiField("AMOUNT (-1 default)","777.5");
            Assert.AreEqual(777.5f,editor.Document.resources.Single().remaining);Assert.AreEqual(resourceId,editor.Document.resources.Single().id);
            string amountHash=ScenarioCodec.ContentHash(editor.Document);
            yield return EndEditUiField("AMOUNT (-1 default)","NaN");Assert.AreEqual(amountHash,ScenarioCodec.ContentHash(editor.Document));StringAssert.Contains("number",game.ScenarioEditorMessage);
            yield return EndEditUiField("AMOUNT (-1 default)","-0.5");Assert.AreEqual(amountHash,ScenarioCodec.ContentHash(editor.Document),"The DTO rule only accepts -1 or a nonnegative resource amount");
            Assert.IsNull(game.World);yield return ClickUiButton("UNDO");Assert.AreEqual(-1,editor.Document.resources.Single().remaining);
            Assert.AreEqual(0,editor.Document.objects.Single().owner);yield return ClickUiButton("REDO");Assert.AreEqual(amountHash,ScenarioCodec.ContentHash(editor.Document));
        }

        [UnityTest] public IEnumerator UguiInvalidImportPreservesDraftHistoryAndClosingItsModalReturnsToTheCanvas()
        {
            Assert.IsTrue(game.OpenScenarioDocument(Draft()));var editor=game.ScenarioEditor;
            yield return EndEditUiField("TITLE","Imported draft witness");yield return EndEditUiField("DESCRIPTION","Redo witness");
            yield return ClickUiButton("UNDO");string hash=ScenarioCodec.ContentHash(editor.Document);Assert.IsTrue(editor.CanUndo);Assert.IsTrue(editor.CanRedo);
            string badFile=Path.Combine(directory,"invalid-ui-import.scenario.json");var envelope=JsonUtility.FromJson<ScenarioEnvelope>(ScenarioCodec.Encode(editor.Document,game.Rules));
            envelope.sha256=new string('0',64);File.WriteAllText(badFile,JsonUtility.ToJson(envelope));byte[] bytes=File.ReadAllBytes(badFile);
            yield return ClickUiButton("IMPORT");Assert.IsTrue(game.Hud.ModalVisible);
            yield return EndEditUiField("LOCAL FILE PATH",badFile);yield return ClickUiButton("IMPORT","IMPORT SCENARIO");
            Assert.IsTrue(game.Hud.ModalVisible,"A failed import must keep the path/error available for correction");Assert.IsTrue(game.ScenarioEditorOpen);
            Assert.AreSame(editor,game.ScenarioEditor);Assert.AreEqual(hash,ScenarioCodec.ContentHash(editor.Document));Assert.IsTrue(editor.CanUndo);Assert.IsTrue(editor.CanRedo);
            Assert.IsNull(game.World);CollectionAssert.AreEqual(bytes,File.ReadAllBytes(badFile));Assert.IsFalse(Directory.Exists(game.ScenarioDirectory));
            yield return ClickUiButton("X","IMPORT SCENARIO");Assert.IsFalse(game.Hud.ModalVisible);Assert.IsTrue(game.Hud.ScenarioCanvasVisible);
            Assert.AreEqual("Editable map canvas",MapCanvas().name);Assert.AreEqual(hash,ScenarioCodec.ContentHash(editor.Document));
            yield return ClickUiButton("REDO");Assert.AreEqual("Redo witness",editor.Document.description);
        }

        [UnityTest] public IEnumerator ReplacingDraftWritesPortableRecoveryAndPreviousDraftRestoresTheOriginalSessionHistory()
        {
            var document=Draft();document.id="kept-draft";Assert.IsTrue(game.OpenScenarioDocument(document));
            var original=game.ScenarioEditor;string initial=ScenarioCodec.ContentHash(original.Document);
            Assert.IsTrue(game.ApplyScenarioEdit(d=>d.description="Retained description"));
            Assert.IsTrue(game.ApplyScenarioEdit(d=>d.name="Retained redo title"));string redoHash=ScenarioCodec.ContentHash(original.Document);
            Assert.IsTrue(original.Undo());string keptHash=ScenarioCodec.ContentHash(original.Document);Assert.IsTrue(original.CanUndo);Assert.IsTrue(original.CanRedo);
            var replacement=Draft();replacement.id="replacement-draft";replacement.name="Replacement draft";string replacementHash=ScenarioCodec.ContentHash(replacement);
            Assert.IsTrue(game.OpenScenarioDocument(replacement),game.ScenarioEditorMessage);var second=game.ScenarioEditor;
            Assert.AreNotSame(original,second);Assert.AreEqual(1,game.PreviousScenarioDraftCount);Assert.IsFalse(second.CanUndo);
            var firstFiles=Directory.GetFiles(game.ScenarioRecoveryDirectory,"*.scenario.json");Assert.AreEqual(1,firstFiles.Length);
            Assert.AreEqual(keptHash,ScenarioCodec.ContentHash(game.Scenarios.ReadFile(firstFiles[0])),"Recovery must be a complete independently decodable document");
            Assert.IsTrue(original.CanUndo);Assert.IsTrue(original.CanRedo);Assert.AreEqual(keptHash,ScenarioCodec.ContentHash(original.Document));
            Assert.IsTrue(game.ApplyScenarioEdit(d=>d.description="Replacement undo witness"));string secondHash=ScenarioCodec.ContentHash(second.Document);

            yield return ClickUiButton("PREV DRAFT");Assert.AreSame(original,game.ScenarioEditor);Assert.IsTrue(game.ScenarioEditorOpen);Assert.IsTrue(game.InMenu);
            Assert.AreEqual(keptHash,ScenarioCodec.ContentHash(original.Document));Assert.IsTrue(original.CanUndo);Assert.IsTrue(original.CanRedo);
            var recoveryHashes=Directory.GetFiles(game.ScenarioRecoveryDirectory,"*.scenario.json").Select(p=>ScenarioCodec.ContentHash(game.Scenarios.ReadFile(p))).ToArray();
            CollectionAssert.AreEquivalent(new[]{keptHash,secondHash},recoveryHashes);
            yield return ClickUiButton("REDO");Assert.AreEqual(redoHash,ScenarioCodec.ContentHash(original.Document));
            yield return ClickUiButton("UNDO");Assert.AreEqual(keptHash,ScenarioCodec.ContentHash(original.Document));
            yield return ClickUiButton("UNDO");Assert.AreEqual(initial,ScenarioCodec.ContentHash(original.Document));Assert.IsFalse(original.CanUndo);
            Assert.AreEqual(keptHash,ScenarioCodec.ContentHash(game.Scenarios.ReadFile(firstFiles[0])),"Editing the recovered session cannot mutate the already written backup");

            yield return ClickUiButton("PREV DRAFT");Assert.AreSame(second,game.ScenarioEditor);Assert.AreEqual(secondHash,ScenarioCodec.ContentHash(second.Document));
            yield return ClickUiButton("UNDO");Assert.AreEqual(replacementHash,ScenarioCodec.ContentHash(second.Document));
            yield return ClickUiButton("REDO");Assert.AreEqual(secondHash,ScenarioCodec.ContentHash(second.Document));
            Assert.AreEqual(1,game.PreviousScenarioDraftCount);Assert.IsEmpty(game.Scenarios.List());Assert.IsNull(game.World);
        }

        [UnityTest] public IEnumerator BlockedRecoveryDirectoryRejectsNewOpenAndImportWithoutChangingDraftLibraryOrAccount()
        {
            byte[] account=SeedAccountSentinel(),preferences=File.ReadAllBytes(game.Settings.Path);
            var document=Draft();string library=game.Scenarios.Write(document);byte[] libraryBytes=File.ReadAllBytes(library);
            Assert.IsTrue(game.SelectAuthoredScenario(document));string selection=ScenarioCodec.ContentHash(game.SelectedScenario);
            Assert.IsTrue(game.OpenScenarioDocument(document));var original=game.ScenarioEditor;
            Assert.IsTrue(game.ApplyScenarioEdit(d=>d.description="Protected draft"));
            Assert.IsTrue(game.ApplyScenarioEdit(d=>d.name="Protected redo"));string redoHash=ScenarioCodec.ContentHash(original.Document);
            Assert.IsTrue(original.Undo());string hash=ScenarioCodec.ContentHash(original.Document);
            Assert.IsTrue(original.CanUndo);Assert.IsTrue(original.CanRedo);Assert.AreEqual(0,game.PreviousScenarioDraftCount);
            var replacement=Draft();replacement.id="replacement-import";string import=Path.Combine(directory,"Imports","replacement.scenario.json");
            game.Scenarios.ExportFile(import,replacement);byte[] importBytes=File.ReadAllBytes(import);
            Assert.IsFalse(Directory.Exists(game.ScenarioRecoveryDirectory));File.WriteAllText(game.ScenarioRecoveryDirectory,"Recovery directory blocker");
            byte[] blocker=File.ReadAllBytes(game.ScenarioRecoveryDirectory);

            Assert.IsFalse(game.OpenScenarioEditor(MapTopology.Winding),"New template creation must not discard a draft when its recovery copy cannot be written");
            Assert.AreSame(original,game.ScenarioEditor);Assert.AreEqual(hash,ScenarioCodec.ContentHash(original.Document));
            Assert.IsFalse(game.OpenScenarioDocument(replacement),"Opening a supplied document also requires preserving the earlier draft first");
            Assert.AreSame(original,game.ScenarioEditor);Assert.AreEqual(hash,ScenarioCodec.ContentHash(original.Document));
            Assert.IsFalse(game.OpenScenarioFile(import),"A valid imported document cannot bypass failed draft preservation");
            Assert.AreSame(original,game.ScenarioEditor);Assert.AreEqual(hash,ScenarioCodec.ContentHash(original.Document));
            Assert.IsTrue(original.CanUndo);Assert.IsTrue(original.CanRedo);Assert.AreEqual(0,game.PreviousScenarioDraftCount);
            Assert.IsTrue(game.ScenarioEditorOpen);Assert.IsTrue(game.InMenu);Assert.IsNull(game.World);Assert.IsNull(game.Simulation);
            Assert.AreEqual(selection,ScenarioCodec.ContentHash(game.SelectedScenario));
            CollectionAssert.AreEqual(blocker,File.ReadAllBytes(game.ScenarioRecoveryDirectory));
            CollectionAssert.AreEqual(importBytes,File.ReadAllBytes(import));CollectionAssert.AreEqual(libraryBytes,File.ReadAllBytes(library));
            Assert.AreEqual(1,game.Scenarios.List().Length);AccountUnchanged(account);CollectionAssert.AreEqual(preferences,File.ReadAllBytes(game.Settings.Path));
            Assert.IsTrue(original.Redo());Assert.AreEqual(redoHash,ScenarioCodec.ContentHash(original.Document));
            Assert.IsTrue(original.Undo());Assert.AreEqual(hash,ScenarioCodec.ContentHash(original.Document));yield return null;
        }
        static Rect MainMenuScreenBounds069(RectTransform rect)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            var points=corners.Select(p=>RectTransformUtility.WorldToScreenPoint(null,p)).ToArray();
            return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
        }
        [UnityTest] public IEnumerator MainMenuButtonsStayDisjointAndRealClicksReachLoadAndWorkshopAtBothUiScales()
        {
            foreach(float scale in new[]{1f,1.25f})
            {
                var settings=game.Settings.Current;settings.uiScale=scale;Assert.IsTrue(game.Settings.TryUpdate(settings,out string reason),reason);
                game.Hud.ShowMenu();yield return ReadyUi();
                var menu=(RectTransform)UiButton("LOAD SAVED MATCH","Main menu").transform.parent;
                var buttons=menu.GetComponentsInChildren<Button>().Where(b=>b.isActiveAndEnabled).ToArray();
                Assert.AreEqual(9,buttons.Count(b=>b.transform.parent==menu),"All nine main menu entries must remain present");
                var bounds=buttons.Select(b=>MainMenuScreenBounds069((RectTransform)b.transform)).ToArray();
                for(int i=0;i<buttons.Length;i++)
                {
                    Assert.Greater(bounds[i].width,1,buttons[i].name);Assert.Greater(bounds[i].height,1,buttons[i].name);
                    Assert.GreaterOrEqual(bounds[i].xMin,-1,buttons[i].name);Assert.GreaterOrEqual(bounds[i].yMin,-1,buttons[i].name);
                    Assert.LessOrEqual(bounds[i].xMax,Screen.width+1,buttons[i].name);Assert.LessOrEqual(bounds[i].yMax,Screen.height+1,buttons[i].name);
                    var label=MainMenuScreenBounds069(buttons[i].GetComponentInChildren<Text>().rectTransform);
                    Assert.GreaterOrEqual(label.xMin,bounds[i].xMin-.1f,buttons[i].name);Assert.LessOrEqual(label.xMax,bounds[i].xMax+.1f,buttons[i].name);
                    Assert.GreaterOrEqual(label.yMin,bounds[i].yMin-.1f,buttons[i].name);Assert.LessOrEqual(label.yMax,bounds[i].yMax+.1f,buttons[i].name);
                    for(int j=i+1;j<buttons.Length;j++)Assert.IsFalse(bounds[i].Overlaps(bounds[j]),buttons[i].name+" overlaps "+buttons[j].name+" at UI scale "+scale);
                }
                var load=MainMenuScreenBounds069((RectTransform)UiButton("LOAD SAVED MATCH","Main menu").transform);
                var workshop=MainMenuScreenBounds069((RectTransform)UiButton("SCENARIO WORKSHOP","Main menu").transform);
                Assert.AreEqual(load.width,workshop.width,.1f);Assert.AreEqual(load.yMin,workshop.yMin,.1f);Assert.Less(load.xMax,workshop.xMin);
                yield return ClickUiButton("LOAD SAVED MATCH","Main menu");
                Assert.IsTrue(game.Hud.ModalVisible);Assert.IsFalse(game.ScenarioEditorOpen);Assert.IsNull(game.World);
                Assert.IsNotNull(UiButton("BACK","LOAD MATCH"),"The load hitbox must open the actual checkpoint picker");
                yield return ClickUiButton("BACK","LOAD MATCH");
                yield return ClickUiButton("SCENARIO WORKSHOP","Main menu");
                Assert.IsTrue(game.ScenarioEditorOpen);Assert.IsTrue(game.Hud.ScenarioCanvasVisible);Assert.IsFalse(game.Hud.ModalVisible);Assert.IsNull(game.World);
                yield return ClickUiButton("CLOSE","Scenario workshop");Assert.IsFalse(game.ScenarioEditorOpen);yield return ReadyUi();
            }
        }
        [UnityTest] public IEnumerator SingleFactionDraftRemainsEditableAndSavedWhileReadyAndPlaytestRejectIt()
        {
            Assert.IsTrue(game.OpenScenarioDocument(Draft()));var editor=game.ScenarioEditor;
            Assert.IsTrue(game.ApplyScenarioEdit(d=>d.starts[1].team=0));string hash=ScenarioCodec.ContentHash(editor.Document);
            Assert.IsTrue(editor.CanUndo);StringAssert.Contains("at least two factions",game.ValidateScenarioDraft());
            Assert.IsFalse(game.PlaytestScenario());Assert.IsNull(game.World);Assert.IsNull(game.Simulation);Assert.IsNull(game.SelectedScenario);
            Assert.IsTrue(game.ScenarioEditorOpen);Assert.AreSame(editor,game.ScenarioEditor);Assert.AreEqual(hash,ScenarioCodec.ContentHash(editor.Document));
            Assert.IsTrue(game.SaveScenarioDraft(editor.Document.id,editor.Document.name));
            Assert.AreEqual(hash,ScenarioCodec.ContentHash(game.Scenarios.Read(editor.Document.id)),"An unfinished team assignment is still a durable editor draft");
            Assert.IsTrue(game.ApplyScenarioEdit(d=>d.starts[1].team=1));Assert.IsEmpty(game.ValidateScenarioDraft());
            Assert.IsTrue(game.PlaytestScenario());Assert.IsTrue(game.World.AreEnemies(0,1));game.ReturnToMenu();Assert.AreSame(editor,game.ScenarioEditor);yield return null;
        }
    }
}
