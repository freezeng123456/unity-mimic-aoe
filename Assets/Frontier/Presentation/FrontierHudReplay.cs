using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier
{
    public sealed partial class FrontierHud
    {
        RectTransform replayPanel;
        Text replayStatus;
        Button replayPlay,replaySpeed,replayObserver;
        bool replayLibraryVisible;
        int replayPage,replayLibraryRevision=-1;

        void AddReplayHud()
        {
            replayPanel=null;replayStatus=null;
            if(!game.IsWatching)return;
            replayPanel=Panel(gamePanel,"Replay controls",new Rect(976,258,592,180),background);
            replayStatus=Label(replayPanel,"",new Rect(12,10,568,68),16,gold);
            replayPlay=Button(replayPanel,"PLAY",new Rect(12,82,180,42),()=>game.ToggleReplayPlayback());
            replaySpeed=Button(replayPanel,"SPEED 1×",new Rect(200,82,180,42),()=>game.CycleReplaySpeed());
            replayObserver=Button(replayPanel,"VIEW P1",new Rect(388,82,192,42),()=>game.SetObserverPlayer((game.ObserverPlayer+1)%game.World.Players.Length));
            Button(replayPanel,"−30s",new Rect(12,130,180,40),()=>game.MoveReplayTime(-30));
            Button(replayPanel,"+30s",new Rect(200,130,180,40),()=>game.MoveReplayTime(30));
            Button(replayPanel,"TIMELINE / MENU",new Rect(388,130,192,40),ShowReplayOptions);
            RefreshReplayHud();
        }
        void LayoutReplayHud(float width)
        {
            if(replayPanel==null)return;
            float w=width*.37f,step=(w-32)/3;
            Position(replayPanel,width*.61f,258,w,180);Position(replayStatus.rectTransform,12,10,w-24,68);
            int i=0;foreach(var button in replayPanel.GetComponentsInChildren<Button>())
            {Position((RectTransform)button.transform,12+(i%3)*(step+4),i<3?82:130,step,i<3?42:40);button.GetComponentInChildren<Text>().rectTransform.sizeDelta=new Vector2(step-20,30);i++;}
        }
        void RefreshReplayHud()
        {
            if(replayLibraryVisible&&replayLibraryRevision!=game.ReplayCatalogRevision)RenderReplayLibrary(replayPage);
            if(replayStatus==null||!game.IsWatching)return;
            var s=game.Playback;var d=game.WatchingDocument;
            string state=!string.IsNullOrEmpty(s.Error)?"STOPPED · "+s.Error:s.IsSeeking?"SEEKING "+ReplayTime(s.SeekTargetStep)+" · "+s.LastSeekSimulatedSteps+" steps":s.Verified?"RECORDED SEGMENT VERIFIED":game.ReplayPaused?"PAUSED":"PLAYING";
            replayStatus.text="REPLAY · "+ReplayTime(s.CurrentStep)+" / "+ReplayTime(s.EndStep)+" · P"+(game.ObserverPlayer+1)+"\n"+state+(d.incomplete?"\nINTERRUPTED RECORDING · "+d.incompleteReason:"");
            replayPlay.GetComponentInChildren<Text>().text=s.Verified?"REPLAY FROM START":game.ReplayPaused?"PLAY":"PAUSE";
            replayPlay.interactable=!s.IsSeeking&&string.IsNullOrEmpty(s.Error);
            replaySpeed.GetComponentInChildren<Text>().text="SPEED "+game.ReplaySpeed.ToString("0.#",CultureInfo.InvariantCulture)+"×";
            replayObserver.GetComponentInChildren<Text>().text="VIEW P"+(game.ObserverPlayer+1)+" · "+game.World.Players[game.ObserverPlayer].civilization;
        }
        static string ReplayTime(int step)
        {int seconds=Mathf.FloorToInt(step*MatchSimulation.StepSeconds);return (seconds/60).ToString("00")+":"+(seconds%60).ToString("00");}
        public void ShowReplayLibrary()
        {
            if(!game.InMenu&&!game.IsWatching&&!game.IsPaused)game.TogglePause();
            game.RequestReplayCatalog();RenderReplayLibrary(0);
        }
        void RenderReplayLibrary(int requestedPage)
        {
            var entries=game.ReplayEntries;int pages=Mathf.Max(1,Mathf.CeilToInt(entries.Count/4f));replayPage=Mathf.Clamp(requestedPage,0,pages-1);
            var box=Modal("RECORDING LIBRARY",720,ReplayLibraryBack);replayLibraryVisible=true;replayLibraryRevision=game.ReplayCatalogRevision;
            Label(box,game.ReplayCatalogLoading?"Reading recordings…":game.ReplayLibraryError!=""?"Library unavailable: "+game.ReplayLibraryError:entries.Count==0?"No recordings yet. Matches are recorded automatically. SAVE REPLAY exports the current segment.":"Select a recording to watch. Checkpoints and account progress remain separate.",new Rect(25,76,815,60),17,muted);
            for(int i=0;i<4&&replayPage*4+i<entries.Count;i++)
            {
                var e=entries[replayPage*4+i];string status=e.CanOpen?(e.Incomplete?"INTERRUPTED · "+e.IncompleteReason:e.MatchEnded?"MATCH ENDED":"RECORDED SEGMENT"):e.Status+" · "+e.Error;
                string title=e.MapId+" · "+e.PlayerCount+" players · "+ReplayTime(e.StartStep)+"–"+ReplayTime(e.EndStep)+"\n"+status+"\n"+e.FileName;
                Button(box,title,new Rect(25,145+i*112,815,102),()=>{game.OpenReplayAsync(e.FilePath);CloseModal();},e.Error,e.CanOpen&&!game.ReplayOpening);
            }
            Button(box,"PREVIOUS",new Rect(25,620,190,44),()=>RenderReplayLibrary(replayPage-1),"",replayPage>0);
            Button(box,"NEXT "+(replayPage+1)+"/"+pages,new Rect(225,620,190,44),()=>RenderReplayLibrary(replayPage+1),"",replayPage+1<pages);
            Button(box,"REFRESH",new Rect(435,620,190,44),()=>{game.RequestReplayCatalog();RenderReplayLibrary(replayPage);},"",!game.ReplayCatalogLoading);
            Button(box,"BACK",new Rect(645,620,195,44),ReplayLibraryBack);
            Label(box,"Only recordings made by this exact game build can be opened.",new Rect(25,675,815,30),15,muted);
        }
        void ReplayLibraryBack(){CloseModal();if(game.InMenu)ShowMenu();else if(game.IsWatching)ShowReplayOptions();else ShowPause(true);}
        public void ShowReplayOptions()
        {
            if(!game.IsWatching)return;
            if(!game.ReplayPaused)game.ToggleReplayPlayback();
            var s=game.Playback;var box=Modal("REPLAY TIMELINE",570,CloseModal);
            Label(box,"Recorded interval "+ReplayTime(s.StartStep)+"–"+ReplayTime(s.EndStep)+"\nViewing P"+(game.ObserverPlayer+1)+" · "+game.World.Players[game.ObserverPlayer].civilization+"\n"+(game.WatchingDocument.incomplete?"Interrupted: "+game.WatchingDocument.incompleteReason:game.WatchingDocument.finishReason),new Rect(25,78,815,90),18,muted);
            var target=Label(box,"JUMP TO "+ReplayTime(s.CurrentStep),new Rect(25,180,815,30),20,gold);
            var track=Panel(box,"Timeline",new Rect(25,230,815,36),panelColor);var slider=track.gameObject.AddComponent<Slider>();slider.minValue=s.StartStep;slider.maxValue=Mathf.Max(s.StartStep+1,s.EndStep);slider.wholeNumbers=true;slider.value=s.CurrentStep;
            // Slider drives child anchors; fixed dimensions belong to the
            // containing areas so stretch does not add another track's width/height.
            var fillArea=Rect(track,"Fill area",new Rect(11,12,793,12));
            var handleArea=Rect(track,"Handle area",new Rect(11,0,793,36));
            var fill=Panel(fillArea,"Track",new Rect(0,0,0,0),muted);fill.sizeDelta=Vector2.zero;fill.anchoredPosition=Vector2.zero;
            var handle=Panel(handleArea,"Handle",new Rect(0,0,22,0),gold);handle.pivot=new Vector2(.5f,.5f);handle.sizeDelta=new Vector2(22,0);handle.anchoredPosition=Vector2.zero;
            slider.fillRect=fill;slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<Image>();slider.interactable=s.EndStep>s.StartStep;
            slider.onValueChanged.AddListener(v=>target.text="JUMP TO "+ReplayTime((int)v));
            Button(box,"JUMP",new Rect(25,290,397,56),()=>{game.BeginReplaySeek((int)slider.value);CloseModal();},"Seeking runs in bounded steps while the camera remains responsive.",string.IsNullOrEmpty(s.Error));
            Button(box,"FROM START",new Rect(443,290,397,56),()=>{game.BeginReplaySeek(s.StartStep);CloseModal();});
            Button(box,"PLAY",new Rect(25,365,397,56),()=>{CloseModal();game.ToggleReplayPlayback();},"",!s.IsSeeking&&string.IsNullOrEmpty(s.Error));
            Button(box,"BACK TO VIEW",new Rect(443,365,397,56),CloseModal);
            Button(box,"RECORDING LIBRARY",new Rect(25,445,397,56),ShowReplayLibrary);
            Button(box,"MAIN MENU",new Rect(443,445,397,56),()=>game.ReturnToMenu());
        }
        public void ShowReplayOutcome()
        {
            if(!game.IsWatching)return;var box=Modal("REPLAY STOPPED",390);
            Label(box,game.Playback.Error,new Rect(25,82,815,135),18,gold);
            Button(box,"RECORDING LIBRARY",new Rect(25,255,397,65),ShowReplayLibrary);
            Button(box,"MAIN MENU",new Rect(443,255,397,65),()=>game.ReturnToMenu());
        }
        public void ShowReplayOpenError(string error)
        {
            var box=Modal("RECORDING COULD NOT OPEN",390,ReplayLibraryBack);
            Label(box,error,new Rect(25,82,815,135),18,gold);
            Button(box,"RECORDING LIBRARY",new Rect(25,255,397,65),ShowReplayLibrary);
            Button(box,"BACK",new Rect(443,255,397,65),ReplayLibraryBack);
        }
    }
}
