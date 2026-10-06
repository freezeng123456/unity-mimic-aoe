using System;
using System.Linq;
using UnityEngine;

namespace Frontier
{
    public sealed partial class GuidedPlaySession
    {
        readonly ScenarioDocument authoredDocument;
        static string AuthoredArmyGroup(string id)=>id==RiverContent070.Scout?"field-force":id==RiverContent070.Convoy?"landing-force":"rear-guard";
        bool IsAuthored=>authoredDocument!=null;
        ScenarioObjective AuthoredObjective(GuidedPlayStage stage)=>stage==null?null:Array.Find(authoredDocument.objectives,o=>o.id==stage.Id);
        ScenarioObjectiveState AuthoredState(string id)=>Array.Find(World.ScenarioObjectiveStates,o=>o.id==id);
        Vector2 AuthoredObjectivePoint
        {
            get
            {
                var goal=AuthoredObjective(Stage);if(goal==null)return World.OpeningTownPosition(0);
                if(goal.kind=="embark_group")return World.ScenarioEntity(goal.targetId)?.position??state.boardingPoint;
                if(goal.regionId!="")return Array.Find(authoredDocument.regions,r=>r.id==goal.regionId).center;
                return World.ScenarioEntity(goal.objectId)?.position??World.OpeningTownPosition(0);
            }
        }
        int AuthoredSuggestedTarget
        {
            get
            {
                var goal=AuthoredObjective(Stage);if(goal==null)return 0;
                if(goal.kind=="embark_group")return World.ScenarioEntity(goal.targetId)?.id??0;
                return goal.kind=="destroy_object"?World.ScenarioEntity(goal.objectId)?.id??0:0;
            }
        }
        int[] AuthoredSuggestedActors
        {
            get
            {
                var goal=AuthoredObjective(Stage);if(goal==null)return Array.Empty<int>();
                if(goal.kind=="escort_group")return World.ScenarioEntities(goal.targetId).Where(id=>World.Get(id)?.Alive==true).ToArray();
                if(goal.kind=="destroy_object")return state.soldiers.Where(id=>World.Get(id)?.Alive==true).ToArray();
                return World.ScenarioEntities(goal.objectId).Where(id=>World.Get(id)?.Alive==true).ToArray();
            }
        }
        float AuthoredProgress(GuidedPlayStage stage)
        {
            var result=AuthoredState(stage.Id);
            return result?.status=="completed"?stage.Required:result?.status=="active"?Mathf.Min(stage.Required,result.progress):0;
        }
        void TickAuthored()
        {
            if(World.Get(state.town)?.Alive!=true||World.Players[0].resigned||World.Players[0].eliminated)
            {Finish(GuidedPlayStatus.Failed,"任务营地已经失守或己方退出。");return;}
            if(World.IsMatchEnded&&World.Winner!=0)
            {Finish(GuidedPlayStatus.Failed,World.ScenarioState.reason);return;}
            if(World.Time>=Definition.TimeLimit)
            {Finish(GuidedPlayStatus.Failed,"远征任务时限已到。");return;}
            // A restored boundary can contain more than one completed goal.
            // Adopt their real scenario results in order before handling victory.
            while(Stage!=null)
            {
                var result=AuthoredState(Stage.Id);
                if(result==null)throw new InvalidOperationException("Campaign objective is missing from its frozen document");
                if(result.status=="failed"){Finish(GuidedPlayStatus.Failed,result.reason);return;}
                float progress=AuthoredProgress(Stage);
                if(Mathf.Abs(checklist[state.stageIndex].Progress-progress)>.0001f)state.revision++;
                checklist[state.stageIndex].Progress=progress;
                if(result.status!="completed")break;
                int index=World.Events.Count;World.Emit("GuidedStageCompleted",0,0,StageMarker(state,Stage.Id));
                stageEvents.Add(index);state.stageIndex++;state.revision++;
            }
            if(state.stageIndex==Definition.Stages.Count)
            {Finish(GuidedPlayStatus.Completed,"远征目标全部完成；可保存进度并进入下一章。");return;}
            if(World.IsMatchEnded){Finish(GuidedPlayStatus.Failed,"战局结束时仍有远征目标未完成。");return;}
            RefreshChecklist();
        }
        static void ConfigureAuthored(World w,GuidedPlayDefinition d,GuidedPlaySnapshot s)
        {
            string group=AuthoredArmyGroup(d.Id);
            s.soldiers=w.ScenarioEntities(group);s.transport=w.ScenarioEntity("transport")?.id??0;
            string target=d.Id==RiverContent070.Scout?"road-market":d.Id==RiverContent070.Convoy?"island-blockade":"supply-cache";
            s.firstTarget=w.ScenarioEntity(target)?.id??0;
            var doc=w.ScenarioDocument;
            var first=Array.Find(doc.objectives,o=>o.id==d.Stages[0].Id);
            s.movePoint=first.regionId!=""?Array.Find(doc.regions,r=>r.id==first.regionId).center:w.Get(s.town).position;
            s.boardingPoint=s.soldiers.Length>0?w.Get(s.soldiers[0]).position:w.Get(s.scout).position;
            var landing=Array.Find(doc.regions,r=>r.id=="island"||r.id=="rear-camp");
            s.landingPoint=landing?.center??s.movePoint;
            w.Fog.Tick(w.Entities);
            w.Emit("GuidedScenarioSetup",0,s.scout,d.StartingSetupDescription);
        }
        static void ValidateAuthoredProgress(World world,GuidedPlayDefinition definition,GuidedPlaySnapshot saved)
        {
            Require(saved.town==world.ScenarioEntity("start-0-town").id&&saved.scout==world.ScenarioEntity("start-0-explorer").id,"authored primary roles");
            Require(saved.soldiers.SequenceEqual(world.ScenarioEntities(AuthoredArmyGroup(definition.Id))),"authored army roles");
            Require(saved.transport==(world.ScenarioEntity("transport")?.id??0),"authored transport role");
            var objectives=world.ScenarioObjectiveStates;var document=world.ScenarioDocument;
            var terminalEvents=new int[document.objectives.Length];
            for(int i=0;i<document.objectives.Length;i++)
            {
                var goal=document.objectives[i];var objective=Array.Find(objectives,o=>o.id==goal.id);
                bool completed=objective.status=="completed",terminal=completed||objective.status=="failed";
                terminalEvents[i]=-1;
                // These frozen chapters do not override goal results through
                // triggers. Their terminal states must retain the real result
                // event, before a restored session can generate Guided witnesses.
                for(int index=0;index<world.Events.Count;index++)
                {
                    var ev=world.Events[index];
                    if(ev.kind!="ScenarioObjectiveCompleted"&&ev.kind!="ScenarioObjectiveFailed"||
                        ev.message==null||!ev.message.StartsWith(goal.id+": ",StringComparison.Ordinal))continue;
                    Require(terminal&&terminalEvents[i]<0&&index>saved.startEventIndex&&
                        ev.player==document.victoryPlayer&&ev.kind==(completed?"ScenarioObjectiveCompleted":"ScenarioObjectiveFailed")&&
                        ev.message==goal.id+": "+objective.reason&&SnapshotCodec.Finite(ev.time)&&ev.time>=0&&ev.time<=world.Time+.001f,
                        "authored objective result witness "+goal.id);
                    terminalEvents[i]=index;
                }
                Require(!terminal||terminalEvents[i]>=0,"authored missing objective result witness "+goal.id);
                if(!terminal)Require(objective.reason=="","authored nonterminal objective reason "+goal.id);
                if(objective.status=="locked")Require(objective.progress==0&&objective.elapsed==0,"authored locked objective progress "+goal.id);
                if(goal.kind=="hold_region")
                    Require(objective.progress<=goal.holdSeconds+.001f&&objective.progress<=objective.elapsed+.001f,
                        "authored hold clock "+goal.id);
                if(completed)
                {
                    float threshold=goal.kind=="hold_region"?goal.holdSeconds:goal.kind=="destroy_object"?1:goal.amount;
                    Require(objective.progress+.0001f>=threshold,"authored completed objective progress "+goal.id);
                    Require(goal.deadline==0||objective.elapsed+.0001f<goal.deadline,"authored completed objective deadline "+goal.id);
                }
            }
            for(int i=0;i<document.objectives.Length;i++)
            {
                var goal=document.objectives[i];var objective=Array.Find(objectives,o=>o.id==goal.id);
                if(objective.status=="locked")continue;
                foreach(string prerequisite in goal.prerequisites)
                {
                    int prior=Array.FindIndex(document.objectives,o=>o.id==prerequisite);
                    Require(Array.Find(objectives,o=>o.id==prerequisite)?.status=="completed","authored objective prerequisite "+goal.id);
                    if(terminalEvents[i]>=0)Require(terminalEvents[prior]<terminalEvents[i]&&
                        world.Events[terminalEvents[prior]].time<=world.Events[terminalEvents[i]].time,
                        "authored objective result order "+goal.id);
                }
            }
            for(int i=0;i<saved.stageIndex;i++)
            {
                int goal=Array.FindIndex(document.objectives,o=>o.id==definition.Stages[i].Id);
                Require(objectives[goal].status=="completed"&&terminalEvents[goal]<saved.stageEvents[i]&&
                    world.Events[terminalEvents[goal]].time<=world.Events[saved.stageEvents[i]].time,"authored completed stage result");
            }
            if(saved.status==GuidedPlayStatus.Completed)
            {
                var result=world.ScenarioState;
                Require(world.Winner==0&&result.scenarioVictory&&result.winner==0,"authored campaign victory");
                foreach(var goal in document.objectives)if(goal.required)
                    Require(Array.Find(objectives,o=>o.id==goal.id)?.status=="completed","authored required objective result");
            }
            else if(saved.status==GuidedPlayStatus.Running)Require(!world.IsMatchEnded,"authored running result");
        }
    }
}
