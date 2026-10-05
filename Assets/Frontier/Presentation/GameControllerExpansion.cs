using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public sealed partial class GameController
    {
        public PlayerSettingsService Settings {get;private set;}
        public PlayerSettings Options {get;private set;}
        public AccountProgressStore Progress {get;private set;}
        public GuidedPlaySession Guided {get;private set;}
        public string SkirmishMode="duel",AllyCivilization="coast",ThirdCivilization="france",FourthCivilization="ottoman";
        string progressRecordedAttempt="";
        int[] guidedSelection=Array.Empty<int>();
        public static bool ValidSkirmishMode(string mode)=>mode=="duel"||mode=="teams"||mode=="ffa";
        public string SkirmishModeLabel=>SkirmishMode=="teams"?"2 VS 2 TEAMS":SkirmishMode=="ffa"?"4 PLAYER FREE FOR ALL":"1 VS 1 DUEL";
        public void CycleSkirmishMode(){SkirmishMode=SkirmishMode=="duel"?"teams":SkirmishMode=="teams"?"ffa":"duel";}
        public string[] RosterCivilizations()=>SkirmishMode=="duel"?new[]{PlayerCivilization,OpponentCivilization}:
            SkirmishMode=="teams"?new[]{PlayerCivilization,OpponentCivilization,AllyCivilization,ThirdCivilization}:
            new[]{PlayerCivilization,OpponentCivilization,ThirdCivilization,FourthCivilization};
        public int[] RosterTeams()=>SkirmishMode=="duel"?new[]{0,1}:SkirmishMode=="teams"?new[]{0,1,0,1}:new[]{0,1,2,3};
        public void SetRosterCivilization(int slot,string id)
        {
            if(Rules.Civ(id)==null)throw new ArgumentException("Unknown civilization");
            if(slot==0)PlayerCivilization=id;else if(slot==1)OpponentCivilization=id;
            else if(slot==2){if(SkirmishMode=="teams")AllyCivilization=id;else ThirdCivilization=id;}
            else if(slot==3){if(SkirmishMode=="teams")ThirdCivilization=id;else FourthCivilization=id;}
            else throw new ArgumentOutOfRangeException(nameof(slot));
        }
        public string RosterReason
        {
            get
            {
                if(!ValidSkirmishMode(SkirmishMode))return "Choose duel, teams or free for all";
                foreach(string id in RosterCivilizations())if(Rules.Civ(id)==null)return "Unknown roster civilization";
                return "";
            }
        }
        int[] AutomatedSlots(int count)
        {
            if(BenchmarkMode)return Array.Empty<int>();
            int first=Autoplay?0:1;var slots=new int[count-first];for(int i=first;i<count;i++)slots[i-first]=i;return slots;
        }
        void RestoreRosterConfiguration()
        {
            if(World.Players.Length==2){SkirmishMode="duel";return;}
            SkirmishMode=World.Players.Length==4&&World.AreAllies(0,2)&&World.AreAllies(1,3)?"teams":"ffa";
            if(SkirmishMode=="teams"){AllyCivilization=World.Players[2].civilization;ThirdCivilization=World.Players[3].civilization;}
            else {ThirdCivilization=World.Players[2].civilization;if(World.Players.Length>3)FourthCivilization=World.Players[3].civilization;}
        }
        void InitializeExpansionPreferences()
        {
            Settings=PlayerSettingsService.Load();Options=Settings.Current;muted=Options.muted;
            Settings.Changed+=PreferencesChanged;Progress=AccountProgressStore.Load();
        }
        void PreferencesChanged()
        {
            Options=Settings.Current;muted=Options.muted;
            Hud?.ApplyPreferences();View?.ApplyPreferences();
        }
        void ChangePreference(Action<PlayerSettings> change)
        {
            if(Settings==null)return;
            var draft=Settings.Current;change(draft);
            if(!Settings.TryUpdate(draft,out string reason))Notify("Settings were not saved: "+reason);
        }
        public bool StartGuided(string id)
        {
            if(!Progress.IsUnlocked(id)){Notify("Complete the previous campaign chapter to unlock this mission");return false;}
            GuidedPlaySession next;
            try{next=GuidedPlaySession.Start(Rules,id);}
            catch(Exception error){Notify("Could not start challenge: "+error.Message);return false;}
            if(World!=null)SaveReport("challenge_exit");
            Guided?.Dispose();ReleasePlayback();Guided=next;progressRecordedAttempt="";guidedSelection=Array.Empty<int>();
            ClearBenchmark();Settings.Captions.Clear();Audio.ResetMatch();
            World=next.World;Simulation=next.Simulation;MatchId=next.AttemptId;
            PlayerCivilization=World.Players[0].civilization;OpponentCivilization=World.Players[1].civilization;
            SelectedMap=World.MapId;MatchSeed=World.Seed;AgeLimit=World.MaxAge;SelectedObjective=World.MapObjective.mode;
            Autoplay=false;SimulationSpeed=1;InMenu=false;PausedForFocusLoss=false;audioCursor=World.Events.Count;
            frameTimes.Clear();nextTelemetry=World.Time+60;LastSimulationFrameMs=0;
            View.ResetWorld();Controls.ResetSelection();Controls.Focus(World.OpeningTownPosition(0));Controls.ClearTransientInput();Hud.ShowGame();
            BeginRecording("guided_scenario");
            Notify(next.Definition.Title+" · "+next.StartingSetupDescription);return true;
        }
        public void RetryCurrentMatch(){if(Guided!=null)StartGuided(Guided.Definition.Id);else StartMatch();}
        public bool SaveLearningProgress()
        {
            if(IsWatching)return false;
            if(Guided==null||Guided.Status!=GuidedPlayStatus.Completed)return false;
            if(progressRecordedAttempt==Guided.AttemptId)return true;
            if(!Progress.RecordCompletion(Guided)){Notify("Campaign progress could not be saved: "+Progress.LastError);return false;}
            progressRecordedAttempt=Guided.AttemptId;return true;
        }
        void ObserveGuidedSelection()
        {
            if(IsWatching||Guided==null||Guided.Status!=GuidedPlayStatus.Running)return;
            var selected=Controls.Selected;bool same=selected.Count==guidedSelection.Length;
            if(same)for(int i=0;i<selected.Count;i++)if(selected[i]!=guidedSelection[i]){same=false;break;}
            if(same)return;
            guidedSelection=selected.ToArray();if(Recorder!=null)Recorder.ObserveSelection(guidedSelection);else Guided.ObserveSelection(guidedSelection);
        }
        private void OnDestroy(){FinishReplayFileWrites();Recorder?.Dispose();Playback?.Dispose();Guided?.Dispose();if(Settings!=null)Settings.Changed-=PreferencesChanged;}
    }
}
