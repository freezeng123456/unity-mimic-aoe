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
            Button(parent,"RECORDING LIBRARY",new Rect(100,525,208,52),ShowReplayLibrary);
            Button(parent,"LAN CO-OP",new Rect(322,525,208,52),ShowLanOptions);
            Button(parent,"CLASSIC CIVILIZATIONS",new Rect(100,620,874,40),ShowClassicCivilizations,"四国正常开局发展练习与独立受控战术遭遇。");
        }
        public void ShowClassicCivilizations()
        {
            if(game.IsLan){game.Notify("Leave LAN play before opening civilization practice");return;}
            var box=Modal("CLASSIC CIVILIZATIONS",720);
            Label(box,"四国正常开局发展与基础使用练习\n从时代 I 经营起步，完成付费生产、研究和指定攻击。",new Rect(25,76,820,55),18,muted);
            var routes=new[]{ClassicCivilizationRoutes084.British,ClassicCivilizationRoutes084.Dutch,ClassicCivilizationRoutes084.French,ClassicCivilizationRoutes084.Ottoman};
            var titles=new[]{"英国：庄园与长弓","荷兰：银行与轻骑兵","法国：猎民与胸甲骑兵","奥斯曼：自动招工与禁卫 / Abus 发展"};
            var descriptions=new[]{"庄园招工、长弓付费生产、老兵升级与指定攻击。","钱币村民、银行实际收入、Ruyter 生产升级与指定攻击。","新募猎民采集、胸甲骑兵生产升级与指定攻击。","自动招工、清真寺升级、禁卫军与 Abus 生产及分别指定攻击。"};
            for(int i=0;i<routes.Length;i++)
            {
                var d=GuidedPlayCatalog.Find(routes[i]);bool unlocked=game.Progress.IsUnlocked(d.Id);
                string status=game.Progress.IsCompleted(d.Id)?"[已完成] ":unlocked?"":"[未解锁] ";
                Button(box,status+titles[i]+"\n"+descriptions[i],new Rect(25,144+i*120,815,108),()=>game.StartGuided(d.Id),d.StartingSetupDescription,unlocked);
            }
            Button(box,"战术遭遇 / 受控兵力",new Rect(25,648,535,48),ShowClassicTactics084,"独立受控时代与原始兵力，不替代正常发展路线。");
            Button(box,"BACK / 返回主菜单",new Rect(590,648,250,48),()=>{CloseModal();ShowMenu();});
        }
        public void ShowClassicTactics084()
        {
            if(game.IsLan){game.Notify("Leave LAN play before opening tactical encounters");return;}
            var box=Modal("四国战术遭遇 / 受控兵力",720);
            Label(box,"独立原创演习：固定时代、原始部队、两波敌军。\n真实移动与战斗验收；可保存、读档、失败重试。",new Rect(25,76,820,55),18,muted);
            int i=0;foreach(var d in ClassicCivilizationTactics084.Definitions())
            {
                string status=game.Progress.IsCompleted(d.Id)?"[已完成] ":"";
                Button(box,status+d.Title+"\n"+d.Description,new Rect(25,144+i*120,815,108),()=>game.StartGuided(d.Id),d.StartingSetupDescription);i++;
            }
            Button(box,"发展练习 / 正常时代 I 开局",new Rect(25,648,535,48),ShowClassicCivilizations);
            Button(box,"BACK / 返回主菜单",new Rect(590,648,250,48),()=>{CloseModal();ShowMenu();});
        }
        public void ShowGuidedLibrary(bool campaign,int page=0)
        {
            if(game.IsLan){game.Notify("Leave LAN play before opening guided missions");return;}
            var items=new System.Collections.Generic.List<GuidedPlayDefinition>();
            foreach(var d in GuidedPlayCatalog.Definitions)if(!ClassicCivilizationTactics084.Contains(d.Id)&&(d.Kind==GuidedPlayKind.Campaign)==campaign)items.Add(d);
            int pages=Mathf.Max(1,Mathf.CeilToInt(items.Count/4f));page=Mathf.Clamp(page,0,pages-1);
            var box=Modal(campaign?"ORIGINAL RIVER CAMPAIGN":"GUIDED LESSONS",720);
            Label(box,campaign?items.Count+" original missions in two expeditions. Each expedition unlocks its following chapters.":"七项操作课程与四国付费发展路线。新路线从真实时代 I 经营起步。",new Rect(25,76,820,55),18,muted);
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
            Label(box,b.ToString(),new Rect(25,188,820,350),s.Checklist.Count>9?15:19,gold);
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
            if(game.IsLan){ShowLanLobby();return;}
            var box=Modal("LOCAL PLAYERS & TEAMS",660);
            Button(box,game.SkirmishModeLabel,new Rect(25,82,815,54),()=>{game.CycleSkirmishMode();ShowRoster();});
            Label(box,"You control your local slot. Other slots use the selected AI difficulty. Allies share sight; economies and unit control remain separate.",new Rect(25,148,815,70),18,muted);
            var roster=game.RosterCivilizations();var teams=game.RosterTeams();
            for(int i=0;i<roster.Length;i++)
            {
                int slot=i;string role=i==game.LocalPlayerSlot?"YOU":teams[i]==teams[game.LocalPlayerSlot]?"ALLY AI":"ENEMY AI";
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
