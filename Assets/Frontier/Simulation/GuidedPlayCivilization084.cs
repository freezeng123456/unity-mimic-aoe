using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Frontier
{
    public sealed partial class GuidedPlaySession
    {
        bool IsCivilizationRoute084=>ClassicCivilizationRoutes084.Contains(Definition.Id);
        int civilizationEvidenceEnd084=int.MaxValue;
        int EvidenceEnd084=>Math.Min(civilizationEvidenceEnd084,World.Events.Count);
        IEnumerable<int> EventIndices084()=>Enumerable.Range(state.startEventIndex+1,Math.Max(0,EvidenceEnd084-state.startEventIndex-1));
        Vector2 CivilizationObjectivePoint084
        {
            get
            {
                if(Stage?.Goal==GuidedPlayGoal.PaidDamage084)
                {
                    var target=World.Get(state.firstTarget);
                    // The map's opposing opening is a declared destination.
                    // A hidden enemy's live position is never a UI hint.
                    return World.IsVisible(0,target)?target.position:World.OpeningTownPosition(1);
                }
                if(Stage?.Goal==GuidedPlayGoal.PaidBuilding084)
                {
                    // A hint only. Normal CanBuild includes visibility, payment,
                    // occupancy and age; the ordinary construction UI pays.
                    if(World.CanAfford(0,World.EntityCost(0,World.Rules.Entity(Stage.Item))))
                    {
                        try{return BuildSite(World,Stage.Item,World.OpeningTownPosition(0)+new Vector2(13,6));}
                        catch(InvalidOperationException){} // Save-up / blocked hint returns home.
                    }
                }
                return World.OpeningTownPosition(0);
            }
        }
        int[] CivilizationSuggestedActors084
        {
            get
            {
                if(Stage==null)return Array.Empty<int>();
                if(Stage.Goal==GuidedPlayGoal.PaidDamage084)return Stage.Item.Split(',').SelectMany(PaidTrained084).Where(id=>World.Get(id)?.Alive==true).Distinct().ToArray();
                if(Stage.Goal==GuidedPlayGoal.PaidAge084||Stage.Goal==GuidedPlayGoal.CivilizationEconomy084&&Stage.Item=="automatic_worker")return new[]{state.town};
                if(Stage.Goal==GuidedPlayGoal.PaidTrain084||Stage.Goal==GuidedPlayGoal.PaidResearch084)
                {
                    string producer=Stage.Goal==GuidedPlayGoal.PaidTrain084?World.Rules.Entity(Stage.Item).producer:World.Rules.Tech(Stage.Item).building;
                    return World.Entities.Where(e=>e.Alive&&e.owner==0&&e.Complete&&e.def.id==producer).Select(e=>e.id).ToArray();
                }
                if(Stage.Item=="coureur_income")return PaidTrained084("coureur").Where(id=>World.Get(id)?.Alive==true).ToArray();
                return World.Entities.Where(e=>e.Alive&&e.owner==0&&e.Worker).Select(e=>e.id).ToArray();
            }
        }
        bool PaidCommand084(GuidedPlayCommandProof proof,CommandKind kind,string item,string paymentPrefix)
        {
            var c=proof.command;
            if(c.kind!=kind||c.item!=item||proof.eventIndex>=EvidenceEnd084)return false;
            int prior=state.startEventIndex;
            foreach(var other in commands)if(other.eventIndex<proof.eventIndex)prior=Math.Max(prior,other.eventIndex);
            for(int i=proof.eventIndex-1;i>prior;i--)
            {
                var e=World.Events[i];
                if(e.kind!="Payment"||e.player!=0||!e.hasEconomy||e.economy==null||!e.message.StartsWith(paymentPrefix,StringComparison.Ordinal)||!c.actors.Contains(e.entity))continue;
                var delta=e.economy.requestedDelta;
                if(delta.food<=0&&delta.wood<=0&&delta.coin<=0&&delta.food+delta.wood+delta.coin<0)return true;
            }
            return false;
        }
        int[] PaidBuildings084(string item)
        {
            var result=new List<int>();var d=World.Rules.Entity(item);
            foreach(var proof in commands)
            {
                if(!PaidCommand084(proof,CommandKind.Build,item,"Foundation "+d.name+":"))continue;
                int lower=state.startEventIndex;foreach(var prior in commands)if(prior.eventIndex<proof.eventIndex)lower=Math.Max(lower,prior.eventIndex);
                for(int i=lower+1;i<proof.eventIndex;i++)
                {
                    var ev=World.Events[i];var e=World.Get(ev.entity);
                    if(ev.kind!="Spawn"||ev.player!=0||e?.def.id!=item||(e.position-proof.command.point).sqrMagnitude>.01f)continue;
                    if(EventIndices084().Any(n=>n>proof.eventIndex&&World.Events[n].kind=="BuildingComplete"&&World.Events[n].player==0&&World.Events[n].entity==e.id))result.Add(e.id);
                }
            }
            return result.Distinct().ToArray();
        }
        int[] PaidTrained084(string item)
        {
            var d=World.Rules.Entity(item);var result=new List<int>();
            var paid=commands.Where(p=>PaidCommand084(p,CommandKind.Train,item,d.name+" ×")).ToArray();
            foreach(var producer in paid.Select(p=>p.command.actors[0]).Distinct())
            {
                int limit=paid.Where(p=>p.command.actors[0]==producer).Sum(p=>Mathf.Clamp(p.command.count,1,d.batch));
                int first=paid.Where(p=>p.command.actors[0]==producer).Min(p=>p.eventIndex);
                result.AddRange(EventIndices084().Where(i=>i>first).Select(i=>World.Events[i]).Where(e=>e.kind=="UnitTrained"&&e.player==0&&e.entity==producer&&e.message==item&&World.Get(e.target)?.def.id==item).Select(e=>e.target).Distinct().Take(limit));
            }
            return result.Distinct().ToArray();
        }
        bool PaidResearch084(string item)
        {
            var t=World.Rules.Tech(item);
            return World.Players[0].technologies.Contains(item)&&commands.Any(proof=>PaidCommand084(proof,CommandKind.Research,item,t.name+":")&&
                EventIndices084().Any(i=>i>proof.eventIndex&&World.Events[i].kind=="ResearchComplete"&&World.Events[i].player==0&&World.Events[i].entity==proof.command.actors[0]&&World.Events[i].message==t.name));
        }
        bool PaidAge084(int age)
        {
            return World.Players[0].age>=age&&commands.Any(proof=>proof.command.kind==CommandKind.AdvanceAge&&
                World.Rules.ageChoices.Any(c=>c.id==proof.command.choice&&c.civilization==Definition.PlayerCivilization&&c.age==age)&&
                PaidCommand084(proof,CommandKind.AdvanceAge,"","Advance to age "+age+":")&&
                EventIndices084().Any(i=>i>proof.eventIndex&&World.Events[i].kind=="AgeAdvanced"&&World.Events[i].player==0&&World.Events[i].entity==state.town&&World.Events[i].message=="Reached age "+age));
        }
        float CivilizationDamage084(string item)
        {
            var soldiers=new HashSet<int>(PaidTrained084(item));float damage=0;
            foreach(int i in EventIndices084())
            {
                var e=World.Events[i];if(e.kind!="Damage"||e.player!=0||e.target!=state.firstTarget||!soldiers.Contains(e.entity))continue;
                if(!commands.Any(p=>p.eventIndex<i&&p.command.kind==CommandKind.Order&&p.command.order==OrderKind.Attack&&p.command.target==state.firstTarget&&p.command.actors.Contains(e.entity)))continue;
                // Melee Damage is emitted before Fire in the same fixed tick;
                // ranged Fire precedes its later impact. Both require real combat.
                if(!EventIndices084().Any(n=>World.Events[n].kind=="Fire"&&World.Events[n].player==0&&World.Events[n].entity==e.entity&&World.Events[n].target==e.target&&World.Events[n].time<=e.time+.0001f))continue;
                damage+=e.amount;
            }
            return damage;
        }
        float CivilizationProgress084(GuidedPlayStage stage)
        {
            switch(stage.Goal)
            {
                case GuidedPlayGoal.PaidBuilding084:return PaidBuildings084(stage.Item).Length;
                case GuidedPlayGoal.PaidTrain084:return PaidTrained084(stage.Item).Length;
                case GuidedPlayGoal.PaidResearch084:return PaidResearch084(stage.Item)?1:0;
                case GuidedPlayGoal.PaidAge084:return PaidAge084((int)stage.Required)?stage.Required:0;
                case GuidedPlayGoal.PaidDamage084:return stage.Item.Split(',').Min(CivilizationDamage084);
                case GuidedPlayGoal.CivilizationEconomy084:
                    switch(stage.Item)
                    {
                        case "manor_worker":
                            var manors=new HashSet<int>(PaidBuildings084("house"));
                            return EventIndices084().Select(i=>World.Events[i]).Count(e=>e.kind=="ManorWorkerProduced"&&e.player==0&&manors.Contains(e.entity)&&World.Get(e.target)?.Worker==true);
                        case "bank_income":
                            var banks=new HashSet<int>(PaidBuildings084("bank"));
                            return EventIndices084().Select(i=>World.Events[i]).Where(e=>e.kind=="BankIncome"&&e.player==0&&banks.Contains(e.entity)).Sum(e=>e.amount);
                        case "coureur_income":
                            var workers=new HashSet<int>(PaidTrained084("coureur"));
                            return EventIndices084().Where(i=>World.Events[i].kind=="Gather"&&World.Events[i].player==0&&workers.Contains(World.Events[i].entity)&&commands.Any(p=>p.eventIndex<i&&p.command.kind==CommandKind.Order&&p.command.order==OrderKind.Gather&&p.command.actors.Contains(World.Events[i].entity))).Sum(i=>World.Events[i].amount);
                        case "automatic_worker":return EventIndices084().Select(i=>World.Events[i]).Count(e=>e.kind=="AutomaticWorkerProduced"&&e.player==0&&e.entity==state.town&&World.Get(e.target)?.Worker==true);
                    }
                    break;
            }
            return 0;
        }
        bool CivilizationFailure084()
        {
            if(Stage?.Goal!=GuidedPlayGoal.PaidDamage084)return false;
            if(Stage.Item.Split(',').Any(item=>!PaidTrained084(item).Any(id=>World.Get(id)?.Alive==true)))
            {Finish(GuidedPlayStatus.Failed,"当前实战所需的一类付费特色部队已全部损失；请重试并保护军队。");return true;}
            if(World.Get(state.firstTarget)?.Alive!=true&&CivilizationProgress084(Stage)+.0001f<Stage.Required)
            {Finish(GuidedPlayStatus.Failed,"原有目标在指定特色部队完成实战要求前已损失；请先侦察，再分别指定部队攻击。");return true;}
            return false;
        }
        static void ConfigureCivilization084(World w,GuidedPlayDefinition d,GuidedPlaySnapshot s)
        {
            s.firstTarget=w.Entities.First(e=>e.owner==1&&e.def.id=="explorer").id;
            s.movePoint=w.OpeningTownPosition(0);s.buildPoint=s.movePoint;s.barracksPoint=s.movePoint;
            w.Emit("GuidedScenarioSetup",0,s.scout,d.StartingSetupDescription);
        }
        static void ValidateCivilizationProgress084(World world,MatchSimulation simulation,GuidedPlayDefinition definition,GuidedPlaySnapshot saved)
        {
            Require(saved.firstTarget==world.Entities.First(e=>e.owner==1&&e.def.id=="explorer").id&&saved.secondTarget==0&&saved.transport==0&&saved.neutralSite==0&&saved.soldiers.Length==0,"civilization opening roles");
            Require(!world.Events.Any(e=>e.kind=="GuidedScenarioGrant"||e.kind=="GuidedScenarioEra"),"civilization normal opening");
            using(var observer=new GuidedPlaySession(world,simulation,definition,SnapshotCodec.Clone(saved)))
                for(int i=0;i<saved.stageIndex;i++)
                {
                    observer.civilizationEvidenceEnd084=saved.stageEvents[i];
                    Require(observer.CivilizationProgress084(definition.Stages[i])+.0001f>=definition.Stages[i].Required,"civilization actual completed stage "+definition.Stages[i].Id);
                }
        }
    }
}
