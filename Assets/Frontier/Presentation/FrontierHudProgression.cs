using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier
{
    public sealed partial class FrontierHud
    {
        Text guideLabel,captionLabel;
        float nextGuideRefresh;
        int guideRevision=-1,captionRevision=-1;
        void AddExpansionMenu(Transform parent)
        {
            Button(parent,"GUIDED LESSONS",new Rect(100,464,208,52),()=>ShowGuidedLibrary(false));
            Button(parent,"RIVER CAMPAIGN",new Rect(322,464,208,52),()=>ShowGuidedLibrary(true));
            Button(parent,"SETTINGS",new Rect(544,464,208,52),ShowSettings);
            Button(parent,game.SkirmishModeLabel+" / ROSTER",new Rect(766,464,208,52),ShowRoster);
        }
        public void ShowGuidedLibrary(bool campaign,int page=0)
        {
            var items=new System.Collections.Generic.List<GuidedPlayDefinition>();
            foreach(var d in GuidedPlayCatalog.Definitions)if((d.Kind==GuidedPlayKind.Campaign)==campaign)items.Add(d);
            int pages=Mathf.Max(1,Mathf.CeilToInt(items.Count/4f));page=Mathf.Clamp(page,0,pages-1);
            var box=Modal(campaign?"ORIGINAL RIVER CAMPAIGN":"GUIDED LESSONS",720);
            Label(box,campaign?"Three linked original missions. Complete each chapter to unlock the next.":"Seven practical lessons. Objectives require your real orders and their results.",new Rect(25,76,820,55),18,muted);
            for(int i=0;i<4&&page*4+i<items.Count;i++)
            {
                var d=items[page*4+i];bool unlocked=game.Progress.IsUnlocked(d.Id);
                var label=(game.Progress.IsCompleted(d.Id)?"[COMPLETED] ":unlocked?"":"[LOCKED] ")+d.Title+"\n"+d.Description;
                if(!unlocked)label+="\nRequires: "+GuidedPlayCatalog.Find(d.UnlockAfterId)?.Title;
                Button(box,label,new Rect(25,144+i*120,815,108),()=>game.StartGuided(d.Id),d.StartingSetupDescription,unlocked);
            }
            Button(box,"PREVIOUS",new Rect(25,648,200,48),()=>ShowGuidedLibrary(campaign,page-1),"",page>0);
            Button(box,"NEXT "+(page+1)+"/"+pages,new Rect(245,648,200,48),()=>ShowGuidedLibrary(campaign,page+1),"",page+1<pages);
            Button(box,"MENU",new Rect(645,648,195,48),()=>{CloseModal();ShowMenu();});
        }
        void AddGuidedHud()
        {
            guideRevision=-1;captionRevision=-1;nextGuideRefresh=0;
            if(game.Guided!=null)
            {
                var pane=Panel(gamePanel,"Guided objective",new Rect(18,205,710,116),new Color(.055f,.105f,.115f,.87f));pane.GetComponent<Image>().raycastTarget=false;
                guideLabel=Label(pane,"",new Rect(12,10,685,96),18,gold);guideLabel.font=cjkFont??font;
                Button(gamePanel,"MISSION BRIEFING",new Rect(22,330,240,42),ShowGuidedBriefing);
                Button(gamePanel,"FIND OBJECTIVE",new Rect(274,330,240,42),()=>game.Controls.Focus(game.Guided.ObjectivePoint));
            }
            else guideLabel=null;
            captionLabel=Label(gamePanel,"",new Rect(222,350,1000,55),18,ink);captionLabel.font=cjkFont??font;captionLabel.raycastTarget=false;
            var rt=captionLabel.rectTransform;rt.anchorMin=rt.anchorMax=Vector2.zero;rt.pivot=Vector2.zero;rt.anchoredPosition=new Vector2(222,350);
        }
        void RefreshProgressionHud()
        {
            if(captionLabel!=null&&captionRevision!=game.Settings.Captions.Revision)
            {captionRevision=game.Settings.Captions.Revision;captionLabel.text=game.Settings.Captions.Text;}
            if(guideLabel==null||game.Guided==null||Time.unscaledTime<nextGuideRefresh)return;
            nextGuideRefresh=Time.unscaledTime+.25f;var s=game.Guided;
            if(s.Revision==guideRevision)return;guideRevision=s.Revision;
            var b=new StringBuilder();b.Append(s.Definition.Title).Append(" · ").Append(s.Status).Append('\n');
            if(s.Stage!=null)b.Append(s.Stage.Title).Append(": ").Append(s.Stage.Instruction);
            else b.Append(s.Reason);
            guideLabel.text=b.ToString();
        }
        public void ShowGuidedBriefing()
        {
            var s=game.Guided;if(s==null)return;var box=Modal(s.Definition.Title,720);
            Label(box,s.StartingSetupDescription,new Rect(25,77,820,100),19,muted);
            var b=new StringBuilder();foreach(var item in s.Checklist)b.Append(item.Complete?"[DONE] ":item.Current?"> ":"  ").Append(item.Text).Append("  ").Append(item.Progress.ToString("0")).Append('/').Append(item.Required.ToString("0")).Append('\n');
            Label(box,b.ToString(),new Rect(25,188,820,350),19,gold);
            Label(box,"Use your normal unit and building controls. Save/Load preserves this attempt and checklist. Retry creates a new attempt on the same seed.",new Rect(25,550,815,60),18,muted);
            Button(box,"LOCATE",new Rect(25,640,250,52),()=>{CloseModal();game.Controls.Focus(s.ObjectivePoint);});
            Button(box,"CONTINUE",new Rect(590,640,250,52),CloseModal);
        }
        public void ShowGuidedResult()
        {
            var s=game.Guided;if(s==null)return;ResultVisible=true;
            var box=Modal(s.Status==GuidedPlayStatus.Completed?"CHALLENGE COMPLETED":"CHALLENGE FAILED",640);
            Label(box,s.Definition.Title+"\n"+s.Reason,new Rect(25,90,820,130),23,gold);
            Label(box,s.StartingSetupDescription+"\nThis result belongs to this authored challenge and its stated starting conditions.",new Rect(25,245,820,100),18,muted);
            bool next=s.Status==GuidedPlayStatus.Completed&&!string.IsNullOrEmpty(s.NextId)&&game.Progress.IsUnlocked(s.NextId);
            Button(box,"RETRY",new Rect(25,385,397,60),()=>game.StartGuided(s.Definition.Id));
            Button(box,"NEXT",new Rect(443,385,397,60),()=>game.StartGuided(s.NextId),"",next);
            Button(box,"SAVE PROGRESS",new Rect(25,466,397,60),()=>{game.SaveLearningProgress();ShowGuidedResult();},game.Progress.LastError,s.Status==GuidedPlayStatus.Completed);
            Button(box,"MAIN MENU",new Rect(443,466,397,60),()=>game.ReturnToMenu());
            Label(box,string.IsNullOrEmpty(game.Progress.LastError)?"Completion progress is saved separately from match checkpoints.":game.Progress.LastError,new Rect(25,558,815,58),17,muted);
        }
        public void ShowRoster()
        {
            var box=Modal("LOCAL PLAYERS & TEAMS",660);
            Button(box,game.SkirmishModeLabel,new Rect(25,82,815,54),()=>{game.CycleSkirmishMode();ShowRoster();});
            Label(box,"You control slot 1. Other slots use the selected AI difficulty. Allies share sight; economies and unit control remain separate.",new Rect(25,148,815,70),18,muted);
            var roster=game.RosterCivilizations();var teams=game.RosterTeams();
            for(int i=0;i<roster.Length;i++)
            {
                int slot=i;string role=i==0?"YOU":teams[i]==teams[0]?"ALLY AI":"ENEMY AI";
                Button(box,"SLOT "+(i+1)+" · TEAM "+(teams[i]+1)+" · "+role+"\n"+(game.Rules.Civ(roster[i])?.name??"CHOOSE CIVILIZATION"),new Rect(25,233+i*78,815,66),()=>ShowRosterCivilizations(slot,0));
            }
            Button(box,"DONE",new Rect(25,575,815,55),()=>{CloseModal();ShowMenu();});
        }
        void ShowRosterCivilizations(int slot,int page)
        {
            var civs=game.Rules.civs;int pages=Mathf.CeilToInt(civs.Length/4f);page=Mathf.Clamp(page,0,pages-1);
            var box=Modal("CIVILIZATION FOR SLOT "+(slot+1),650);
            for(int i=0;i<4&&page*4+i<civs.Length;i++)
            {
                var d=civs[page*4+i];Button(box,d.name+"\n"+d.description,new Rect(25+(i%2)*414,100+(i/2)*208,400,190),()=>{game.SetRosterCivilization(slot,d.id);ShowRoster();});
            }
            Button(box,"PREVIOUS",new Rect(25,548,240,55),()=>ShowRosterCivilizations(slot,page-1),"",page>0);
            Button(box,"NEXT",new Rect(280,548,240,55),()=>ShowRosterCivilizations(slot,page+1),"",page+1<pages);
            Button(box,"BACK",new Rect(600,548,240,55),ShowRoster);
        }
        void AddRosterResult(Transform box,World w)
        {
            Label(box,"PLAYER / TEAM       AGE     KILLS / LOSSES     SHIPMENTS",new Rect(25,176,820,45),18,gold);
            for(int i=0;i<w.Players.Length;i++)
            {
                var p=w.Players[i];Label(box,"P"+(i+1)+" / "+(p.team+1)+" · "+game.Rules.Civ(p.civilization).name+"    "+p.age+"    "+p.kills+" / "+p.losses+"    "+p.shipsSent+(p.resigned?" · RESIGNED":p.eliminated?" · ELIMINATED":""),new Rect(25,231+i*60,820,52),19);
            }
        }
    }
}
