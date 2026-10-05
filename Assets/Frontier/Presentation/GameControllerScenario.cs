using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public sealed partial class GameController
    {
        ScenarioDocument selectedScenario069;
        ScenarioStore scenarioStore069;
        readonly List<ScenarioEditorSession> previousScenarioDrafts069=new List<ScenarioEditorSession>();
        public int PreviousScenarioDraftCount=>previousScenarioDrafts069.Count;
        public string[] PreviousScenarioDraftNames=>previousScenarioDrafts069.ConvertAll(s=>s.Document.name).ToArray();
        string persistedEditorHash069="";
        public string LatestScenarioDraftPath=>Path.Combine(ScenarioRecoveryDirectory,"latest.scenario.json");
        public string ScenarioRecoveryDirectory=>Path.Combine(dataDirectoryOverride??Application.persistentDataPath,"ScenarioDrafts");
        public ScenarioDocument SelectedScenario => selectedScenario069==null?null:ScenarioCodec.Clone(selectedScenario069);
        public ScenarioEditorSession ScenarioEditor {get;private set;}
        public bool ScenarioEditorOpen {get;private set;}
        public bool IsScenarioPlaytest {get;private set;}
        public string ScenarioEditorMessage {get;private set;}="";
        public string ScenarioDirectory=>Path.Combine(dataDirectoryOverride??Application.persistentDataPath,"Scenarios");
        public ScenarioStore Scenarios=>scenarioStore069??(scenarioStore069=new ScenarioStore(ScenarioDirectory,Rules));

        public bool OpenScenarioEditor(string template=null)
        {
            if(IsLan){Notify("Leave the cooperative session before editing a scenario");return false;}
            if(!InMenu){Notify("Return to the menu before editing a scenario");return false;}
            try
            {
                if(ScenarioEditor==null||template!=null)
                {
                    string map=template??(MapTopology.IsValid(SelectedMap)&&SelectedMap!=MapTopology.Scenario?SelectedMap:MapTopology.Winding);
                    ScenarioDocument doc;
                    if(template==null&&selectedScenario069!=null)doc=SelectedScenario;
                    else if(template==null&&File.Exists(LatestScenarioDraftPath))doc=Scenarios.ReadFile(LatestScenarioDraftPath);
                    else doc=ScenarioEditorSession.FromMap(Rules,map,MatchSeed,SkirmishMode=="duel"?2:4);
                    var next=new ScenarioEditorSession(Rules,doc);KeepCurrentEditorDraft069();ScenarioEditor=next;
                }
                ScenarioEditorOpen=true;IsScenarioPlaytest=false;Controls.ClearTransientInput();Hud.ShowScenarioEditor();return true;
            }
            catch(Exception error){EditorMessage("Could not open editor: "+error.Message);return false;}
        }
        public bool OpenScenarioDocument(ScenarioDocument document)
        {
            if(IsLan||!InMenu){Notify("Return to the solo menu before editing");return false;}
            try
            {
                var next=new ScenarioEditorSession(Rules,document);
                KeepCurrentEditorDraft069();ScenarioEditor=next;ScenarioEditorOpen=true;IsScenarioPlaytest=false;
                EditorMessage("Opened "+next.Document.name);Controls.ClearTransientInput();Hud.ShowScenarioEditor();return true;
            }
            catch(Exception error){Debug.LogWarning("FRONTIER_EDITOR_OPEN_FAILED "+error);EditorMessage("Open failed: "+error.Message);return false;}
        }
        void KeepCurrentEditorDraft069()
        {
            if(ScenarioEditor==null)return;
            // A complete portable recovery copy succeeds before replacing a
            // draft. The four most recent sessions also retain their undo history.
            string path=Path.Combine(ScenarioRecoveryDirectory,"draft-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N")+".scenario.json");
            Scenarios.ExportFile(path,ScenarioEditor.Document);previousScenarioDrafts069.Add(ScenarioEditor);
            if(previousScenarioDrafts069.Count>4)previousScenarioDrafts069.RemoveAt(0);
        }
        public bool RestorePreviousScenarioDraft(int index=-1)
        {
            if(IsLan||!InMenu||previousScenarioDrafts069.Count==0)return false;
            if(index<0)index=previousScenarioDrafts069.Count-1;if(index>=previousScenarioDrafts069.Count)return false;
            var prior=previousScenarioDrafts069[index];
            try
            {
                if(ScenarioEditor!=null)
                {
                    string path=Path.Combine(ScenarioRecoveryDirectory,"draft-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N")+".scenario.json");
                    Scenarios.ExportFile(path,ScenarioEditor.Document);
                }
                previousScenarioDrafts069.RemoveAt(index);if(ScenarioEditor!=null)previousScenarioDrafts069.Add(ScenarioEditor);
                ScenarioEditor=prior;ScenarioEditorOpen=true;IsScenarioPlaytest=false;Controls.ClearTransientInput();Hud.ShowScenarioEditor();EditorMessage("Recovered previous draft and its undo history");return true;
            }
            catch(Exception error){EditorMessage("Recovery failed: "+error.Message);return false;}
        }
        public bool OpenScenarioFile(string path)
        {
            try{return OpenScenarioDocument(Scenarios.ReadFile(path));}
            catch(Exception error){EditorMessage("Import failed: "+error.Message);return false;}
        }
        public bool SelectAuthoredScenario(ScenarioDocument document)
        {
            try
            {
                if(document!=null){string reason=ScenarioCompiler.Validate(document,Rules,document.starts.Length);if(reason!="")throw new ArgumentException(reason);}
                selectedScenario069=document==null?null:ScenarioCodec.Clone(document);
                NotifyLanScenarioChanged();return true;
            }
            catch(Exception error){Notify("Cannot select scenario: "+error.Message);return false;}
        }
        void SyncScenarioSelectionFromWorld()
        {selectedScenario069=World?.HasScenario==true?World.ScenarioDocument:null;IsScenarioPlaytest=false;ScenarioEditorOpen=false;}
        public bool PersistScenarioDraft()
        {
            if(ScenarioEditor==null)return true;
            try
            {
                var draft=ScenarioEditor.Document;string hash=ScenarioCodec.ContentHash(draft);
                if(hash!=persistedEditorHash069||!File.Exists(LatestScenarioDraftPath))Scenarios.ExportFile(LatestScenarioDraftPath,draft);
                persistedEditorHash069=hash;return true;
            }
            catch(Exception error){EditorMessage("Draft could not be preserved: "+error.Message);return false;}
        }
        public void CloseScenarioEditor()
        {if(!PersistScenarioDraft())return;ScenarioEditorOpen=false;Controls.ClearTransientInput();Hud.ShowMenu();}
        public void EditorMessage(string message)
        {ScenarioEditorMessage=message;Notify(message);}
        public bool ApplyScenarioEdit(Action<ScenarioDocument> edit)
        {
            if(!ScenarioEditorOpen||ScenarioEditor==null)return false;
            bool ok=ScenarioEditor.Apply(edit,out string reason);EditorMessage(ok?"Draft updated":reason);return ok;
        }
        public bool SaveScenarioDraft(string id,string name)
        {
            if(ScenarioEditor==null)return false;
            try
            {
                var doc=ScenarioEditor.Document;doc.id=id.Trim();doc.name=name.Trim();
                string reason=ScenarioCodec.Validate(doc,Rules);if(reason!="")throw new ArgumentException(reason);
                string path=Scenarios.Write(doc);
                // Disk replacement succeeds before changing the in-memory identity.
                ScenarioEditor.Apply(d=>{d.id=doc.id;d.name=doc.name;},out _);
                EditorMessage("Saved "+doc.name+" · "+path);return true;
            }
            catch(Exception error){EditorMessage("Save failed: "+error.Message);return false;}
        }
        public bool ExportScenarioDraft(string path)
        {
            if(ScenarioEditor==null)return false;
            try{EditorMessage("Exported "+Scenarios.ExportFile(path,ScenarioEditor.Document));return true;}
            catch(Exception error){EditorMessage("Export failed: "+error.Message);return false;}
        }
        public string ValidateScenarioDraft()
        {
            if(ScenarioEditor==null)return "Open a draft first";
            var doc=ScenarioEditor.Document;string reason=ScenarioCompiler.Validate(doc,Rules,doc.starts.Length);
            if(reason=="")foreach(var start in doc.starts)if(start.age>AgeLimit){reason="Starting age exceeds this match era limit ("+AgeLimit+")";break;}
            EditorMessage(reason==""?"Ready to play · "+doc.starts.Length+" players · "+ScenarioCodec.ContentHash(doc).Substring(0,12):reason);return reason;
        }
        public bool PlaytestScenario()
        {
            if(ScenarioEditor==null||!ScenarioEditorOpen)return false;
            return StartAuthoredScenario(ScenarioEditor.Document,true);
        }
        bool StartAuthoredScenario(ScenarioDocument document,bool editorPlaytest)
        {
            if(IsLan){Notify("Start cooperative scenarios from the lobby");return false;}
            World next;MatchSimulation nextSimulation;
            try
            {
                var roster=new string[document.starts.Length];var teams=new int[roster.Length];
                for(int i=0;i<roster.Length;i++){roster[i]=document.starts[i].civilization;teams[i]=document.starts[i].team;}
                next=new World(Rules,ScenarioCodec.Clone(document),roster,teams,AgeLimit);
                var automated=new int[next.Players.Length-1];for(int i=1;i<next.Players.Length;i++)automated[i-1]=i;
                nextSimulation=new MatchSimulation(next,automated,OpponentDifficulty);
            }
            catch(Exception error){EditorMessage("Playtest could not start: "+error.Message);return false;}
            if(World!=null)SaveReport("restart_exit");
            Guided?.Dispose();Guided=null;ReleasePlayback();Recorder?.Dispose();Recorder=null;ClearBenchmark();
            Settings.Captions.Clear();Audio.ResetMatch();World=next;Simulation=nextSimulation;MatchId=Guid.NewGuid().ToString("N");
            selectedScenario069=next.ScenarioDocument;PlayerCivilization=next.Players[0].civilization;OpponentCivilization=next.Players[1].civilization;
            SelectedMap=next.MapId;SelectedObjective="elimination";MatchSeed=next.Seed;RestoreRosterConfiguration();
            for(int i=0;i<next.Players.Length;i++)SetDeck(i,i==0?DeckPreset:"military");
            IsScenarioPlaytest=editorPlaytest;ScenarioEditorOpen=false;Autoplay=false;SimulationSpeed=1;InMenu=false;
            PausedForFocusLoss=false;audioCursor=next.Events.Count;frameTimes.Clear();nextTelemetry=next.Time+60;LastSimulationFrameMs=0;
            Controls.ResetSelection();View.ResetWorld();Controls.Focus(next.OpeningTownPosition(0));Controls.ClearTransientInput();Hud.ShowGame();
            BeginRecording(editorPlaytest?"editor_playtest":"authored_scenario");
            Notify(document.name+" · "+(editorPlaytest?"PLAYTEST · Return to editor keeps the draft":"AUTHORED SCENARIO"));return true;
        }
        bool RestoreScenarioEditorAfterPlaytest()
        {
            bool reopen=IsScenarioPlaytest&&ScenarioEditor!=null;
            IsScenarioPlaytest=false;ScenarioEditorOpen=reopen;return reopen;
        }
    }
}
