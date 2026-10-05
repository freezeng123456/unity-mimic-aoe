using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Frontier
{
    public sealed partial class GameController
    {
        public ReplayRecorder Recorder {get;private set;}
        public ReplaySession Playback {get;private set;}
        public bool IsWatching=>Playback!=null;
        public bool ReplayPaused {get;private set;}=true;
        public float ReplaySpeed {get;private set;}=1;
        public int ObserverPlayer {get;private set;}
        public ReplayDocument WatchingDocument {get;private set;}
        public string ReplayFile {get;private set;}="";
        public string LastReplayFile {get;private set;}="";
        public string ReplayFileError {get;private set;}="";
        public string ReplayLibraryError {get;private set;}="";
        public IReadOnlyList<ReplayCatalogEntry> ReplayEntries {get;private set;}=Array.Empty<ReplayCatalogEntry>();
        public int ReplayCatalogRevision {get;private set;}
        public bool ReplayCatalogLoading=>replayCatalogTask!=null;
        public bool ReplayOpening=>replayOpenTask!=null;
        public bool ReplaySaving=>replayPendingWrites>0;
        public string ReplayDirectory=>replayDirectoryOverride??Path.Combine(Application.persistentDataPath,"Replays");
        string replayDirectoryOverride;
        Task<IReadOnlyList<ReplayCatalogEntry>> replayCatalogTask;
        Task<ReplayDocument> replayOpenTask;
        Task replayWriteTask;
        string replayOpenPath="",reportedReplayError="";
        int replayPendingWrites,replayEpoch,replayOpenEpoch;
        float replayFrameDebt;
        ReplayDocument lastRecording;
        readonly List<ReplayWriteNotice> replayWriteNotices=new List<ReplayWriteNotice>();
        sealed class ReplayWriteNotice {public Task task;public string path;}

        public ReplayIdentity CurrentReplayIdentity()
        {
            string build=Application.isEditor?"editor:"+typeof(World).Assembly.ManifestModule.ModuleVersionId.ToString("N"):Application.buildGUID;
            return ReplayIdentity.Create(Rules,Application.version,build,Application.unityVersion);
        }
        void BeginRecording(string startKind)
        {
            Recorder?.Dispose();Recorder=null;ObserverPlayer=LocalPlayerSlot;reportedReplayError="";
            if(IsLanClient||BenchmarkMode||World==null||Simulation==null)return;
            try{Recorder=new ReplayRecorder(Simulation,CurrentReplayIdentity(),Guided,Guid.NewGuid().ToString("N"),300,MatchId,startKind);}
            catch(Exception error){Notify("Recording could not start: "+error.Message);Debug.LogWarning("FRONTIER_REPLAY_RECORD_FAILED "+error.Message);}
        }
        void ReleasePlayback()
        {
            replayEpoch++;Playback?.Dispose();Playback=null;WatchingDocument=null;ReplayFile="";
            ReplayPaused=true;ReplaySpeed=1;replayFrameDebt=0;ObserverPlayer=0;reportedReplayError="";
        }
        public bool SaveReplayNow()
        {
            if(IsLanClient){Notify("The host owns the cooperative match recording");return false;}
            if(IsWatching){Notify("This recording is open for viewing");return false;}
            if(ReplaySaving){Notify("A recording is still being saved");return false;}
            try
            {
                var document=Recorder?.Capture("manual_save")??lastRecording;
                if(document==null){Notify("Start a match before saving a recording");return false;}
                string path=ReplayStore.PathFor(ReplayDirectory,document.segmentId);
                ReplayStore.Write(path,document,Rules,CurrentReplayIdentity());
                lastRecording=document;LastReplayFile=path;ReplayFileError="";
                Notify(document.incomplete?"Saved interrupted recording: "+document.incompleteReason:"Recording saved");
                Debug.Log("FRONTIER_REPLAY_SAVED file="+path+" segment="+document.segmentId+" original="+document.matchId);return true;
            }
            catch(Exception error){ReplayFileError=error.Message;Notify("Recording save failed: "+error.Message);Debug.LogWarning("FRONTIER_REPLAY_SAVE_FAILED "+error.Message);return false;}
        }
        public void SaveReplayAsync()
        {
            if(IsLanClient){Notify("The host owns the cooperative match recording");return;}
            if(IsWatching){Notify("This recording is open for viewing");return;}
            if(ReplaySaving){Notify("A recording is still being saved");return;}
            try
            {
                var document=Recorder?.Capture("manual_save")??lastRecording;
                if(document==null){Notify("Start a match before saving a recording");return;}
                lastRecording=document;QueueReplayWrite(document);Notify("Saving recording…");
            }
            catch(Exception error){ReplayFileError=error.Message;Notify("Recording save failed: "+error.Message);}
        }
        void CloseRecording(string reason)
        {
            if(Recorder==null)return;
            try
            {
                lastRecording=Recorder.Finish(reason);Recorder.Dispose();Recorder=null;
                // Batch tests preserve personal storage unless they explicitly
                // supply their own replay directory. Native sessions save here.
                if(Application.isBatchMode&&replayDirectoryOverride==null)return;
                QueueReplayWrite(lastRecording);
            }
            catch(Exception error){Recorder?.Dispose();Recorder=null;ReplayFileError=error.Message;Notify("Recording could not close: "+error.Message);Debug.LogWarning("FRONTIER_REPLAY_CLOSE_FAILED "+error.Message);}
        }
        void QueueReplayWrite(ReplayDocument document)
        {
            if(replayPendingWrites>=4)throw new IOException("Previous recordings are still saving; the last recording remains available in memory");
            string path=ReplayStore.PathFor(ReplayDirectory,document.segmentId);
            var identity=CurrentReplayIdentity();var rules=SnapshotCodec.Clone(Rules);var previous=replayWriteTask;
            replayPendingWrites++;ReplayFileError="";
            var task=Task.Run(()=>
            {
                if(previous!=null)try{previous.GetAwaiter().GetResult();}catch(Exception){}
                // document owns its immutable export data; no worker touches a
                // live World, controller, UI or UnityEngine.Object.
                ReplayStore.Write(path,document,rules,identity);
            });
            replayWriteTask=task;replayWriteNotices.Add(new ReplayWriteNotice{task=task,path=path});
        }
        public void RequestReplayCatalog()
        {
            if(replayCatalogTask!=null)return;
            ReplayLibraryError="";ReplayEntries=Array.Empty<ReplayCatalogEntry>();ReplayCatalogRevision++;
            string directory=ReplayDirectory;var rules=SnapshotCodec.Clone(Rules);var identity=CurrentReplayIdentity();
            replayCatalogTask=Task.Run(()=>ReplayStore.Catalog(directory,rules,identity));
        }
        public void OpenReplayAsync(string path)
        {
            if(IsLan){Notify("Leave the cooperative session before opening a recording");return;}
            if(ReplayOpening){Notify("A recording is already opening");return;}
            var rules=SnapshotCodec.Clone(Rules);var identity=CurrentReplayIdentity();
            replayOpenPath=path;replayOpenEpoch=replayEpoch;ReplayFileError="";
            replayOpenTask=Task.Run(()=>ReplayStore.Read(path,rules,identity));Notify("Opening recording…");
        }
        public bool LoadReplayNow(string path)
        {
            if(IsLan){Notify("Leave the cooperative session before opening a recording");return false;}
            try{return InstallReplay(ReplayStore.Read(path,Rules,CurrentReplayIdentity()),path);}
            catch(Exception error){ReplayFileError=error.Message;Notify("Recording could not open: "+error.Message);Debug.LogWarning("FRONTIER_REPLAY_OPEN_FAILED "+error.Message);return false;}
        }
        bool InstallReplay(ReplayDocument document,string path)
        {
            if(IsLan){Notify("Leave the cooperative session before opening a recording");return false;}
            ReplaySession next=null;
            try
            {
                next=new ReplaySession(Rules,document,CurrentReplayIdentity());
                if(!string.IsNullOrEmpty(next.Error))throw new InvalidDataException(next.Error);
                // Full file validation and initial reconstruction precede any
                // replacement of the current game or writing of its report.
                if(World!=null&&!IsWatching)SaveReport("replay_exit");
                Guided?.Dispose();Recorder?.Dispose();Recorder=null;ReleasePlayback();
                Playback=next;next=null;WatchingDocument=document;ReplayFile=path;ReplayPaused=true;
                World=Playback.World;Simulation=Playback.Simulation;Guided=Playback.Guided;MatchId=document.matchId;
                PlayerCivilization=World.Players[0].civilization;OpponentCivilization=World.Players[1].civilization;
                SelectedMap=World.MapId;MatchSeed=World.Seed;AgeLimit=World.MaxAge;SelectedObjective=World.MapObjective.mode;
                RestoreRosterConfiguration();Autoplay=false;SimulationSpeed=1;InMenu=false;PausedForFocusLoss=false;
                progressRecordedAttempt="";Settings.Captions.Clear();ClearBenchmark();Audio.ResetMatch();audioCursor=World.Events.Count;
                frameTimes.Clear();LastSimulationFrameMs=0;View.ResetWorld();Controls.ResetSelection();Controls.Focus(World.OpeningTownPosition(0));Controls.ClearTransientInput();
                Hud.ShowGame();Audio.PauseAll(true);ReplayFileError="";
                Notify(document.incomplete?"Viewing interrupted recording: "+document.incompleteReason:"Recording opened · press PLAY");
                Debug.Log("FRONTIER_REPLAY_OPEN file="+path+" segment="+document.segmentId+" original="+document.matchId+" start="+Playback.StartStep+" end="+Playback.EndStep);return true;
            }
            catch(Exception error){next?.Dispose();ReplayFileError=error.Message;Notify("Recording could not open: "+error.Message);Debug.LogWarning("FRONTIER_REPLAY_OPEN_FAILED "+error.Message);return false;}
        }
        public void ToggleReplayPlayback()
        {
            if(!IsWatching||Playback.IsSeeking||!string.IsNullOrEmpty(Playback.Error))return;
            if(Playback.Verified&&ReplayPaused)BeginReplaySeek(Playback.StartStep);
            ReplayPaused=!ReplayPaused;replayFrameDebt=0;Audio.PauseAll(ReplayPaused);Controls.ClearTransientInput();
        }
        public void SetReplaySpeed(float speed)
        {
            if(speed!=.5f&&speed!=1&&speed!=2&&speed!=4)throw new ArgumentOutOfRangeException(nameof(speed));
            ReplaySpeed=speed;replayFrameDebt=0;
        }
        public void CycleReplaySpeed(){float[] speeds={.5f,1,2,4};SetReplaySpeed(speeds[(Array.IndexOf(speeds,ReplaySpeed)+1)%speeds.Length]);}
        public void SetObserverPlayer(int player)
        {
            if(!IsWatching||player<0||player>=World.Players.Length)throw new ArgumentOutOfRangeException(nameof(player));
            ObserverPlayer=player;Controls.ResetSelection();Settings.Captions.Clear();audioCursor=World.Events.Count;
            Audio.ResetMatch();Audio.PauseAll(ReplayPaused);
            View.ResetWorld();Hud.InvalidateActions();Controls.Focus(World.OpeningTownPosition(player));
        }
        public void BeginReplaySeek(int step)
        {
            if(!IsWatching)return;
            step=Mathf.Clamp(step,Playback.StartStep,Playback.EndStep);ReplayPaused=true;replayFrameDebt=0;Audio.ResetMatch();Audio.PauseAll(true);
            Playback.BeginSeek(step);SynchronizeReplayWorld();Controls.ClearTransientInput();
            Debug.Log("FRONTIER_REPLAY_SEEK segment="+WatchingDocument.segmentId+" target="+step);
        }
        public void MoveReplayTime(int seconds){if(IsWatching)BeginReplaySeek(Playback.CurrentStep+Mathf.RoundToInt(seconds/MatchSimulation.StepSeconds));}
        void SynchronizeReplayWorld()
        {
            if(!IsWatching)return;
            if(!ReferenceEquals(World,Playback.World))
            {
                World=Playback.World;Simulation=Playback.Simulation;Guided=Playback.Guided;
                audioCursor=World.Events.Count;Settings.Captions.Clear();View.ResetWorld();Controls.ResetSelection();Hud.ShowGame();
            }
        }
        void AdvanceReplayFrame()
        {
            if(!IsWatching)return;
            if(Playback.IsSeeking){Playback.AdvanceSeek(20);SynchronizeReplayWorld();audioCursor=World.Events.Count;}
            else if(!ReplayPaused&&!Playback.Verified&&string.IsNullOrEmpty(Playback.Error))
            {
                replayFrameDebt=Mathf.Min(2,replayFrameDebt+Time.unscaledDeltaTime*ReplaySpeed);
                int count=Mathf.Min(MatchSimulation.MaxStepsPerFrame,(int)(replayFrameDebt/MatchSimulation.StepSeconds));
                if(count>0||Playback.CurrentStep==Playback.EndStep)
                {int before=Playback.CurrentStep;Playback.Advance(Mathf.Max(1,count));replayFrameDebt=Mathf.Max(0,replayFrameDebt-(Playback.CurrentStep-before)*MatchSimulation.StepSeconds);SynchronizeReplayWorld();}
            }
            if(!string.IsNullOrEmpty(Playback.Error)&&reportedReplayError!=Playback.Error)
            {reportedReplayError=Playback.Error;ReplayPaused=true;Audio.PauseAll(true);Notify("Replay stopped: "+Playback.Error);Hud.ShowReplayOutcome();}
            else if(Playback.Verified&&!ReplayPaused)
            {ReplayPaused=true;Audio.PauseAll(true);Notify(WatchingDocument.incomplete?"Recorded prefix verified · recording was interrupted":"Recorded segment verified");}
        }
        void PollReplayOperations()
        {
            for(int i=0;i<replayWriteNotices.Count;)
            {
                var item=replayWriteNotices[i];if(!item.task.IsCompleted){i++;continue;}
                replayWriteNotices.RemoveAt(i);replayPendingWrites--;
                if(item.task.IsFaulted){ReplayFileError=item.task.Exception.GetBaseException().Message;Notify("Recording save failed: "+ReplayFileError);Debug.LogWarning("FRONTIER_REPLAY_SAVE_FAILED "+ReplayFileError);}
                else {LastReplayFile=item.path;Notify("Recording saved");Debug.Log("FRONTIER_REPLAY_SAVED file="+item.path);}
            }
            if(replayCatalogTask?.IsCompleted==true)
            {
                if(replayCatalogTask.IsFaulted)ReplayLibraryError=replayCatalogTask.Exception.GetBaseException().Message;
                else ReplayEntries=replayCatalogTask.Result;
                replayCatalogTask=null;ReplayCatalogRevision++;
            }
            if(replayOpenTask?.IsCompleted==true)
            {
                var task=replayOpenTask;replayOpenTask=null;
                if(replayOpenEpoch==replayEpoch)
                {
                    if(task.IsFaulted){ReplayFileError=task.Exception.GetBaseException().Message;Notify("Recording could not open: "+ReplayFileError);Hud.ShowReplayOpenError(ReplayFileError);}
                    else if(!InstallReplay(task.Result,replayOpenPath))Hud.ShowReplayOpenError(ReplayFileError);
                }
            }
            if(Recorder!=null&&!string.IsNullOrEmpty(Recorder.Error)&&reportedReplayError!=Recorder.Error)
            {reportedReplayError=Recorder.Error;Notify("Recording interrupted: "+Recorder.Error);}
        }
        void FinishReplayFileWrites()
        {
            try{replayWriteTask?.GetAwaiter().GetResult();}
            catch(Exception error){Debug.LogWarning("FRONTIER_REPLAY_SAVE_FAILED "+error.Message);}
        }
        static bool EndsRecording(string reason)=>reason=="quit"||reason=="menu_exit"||reason=="restart_exit"||reason=="load_exit"||reason=="challenge_exit"||reason=="guided_finished"||reason=="match_finished"||reason=="replay_exit";
    }
}
