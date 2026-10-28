using System;
using System.Linq;
using UnityEngine;

namespace Frontier
{
    // New encounter-only observers. Revision-1 snapshots retain their exact
    // shape; accepted commands, positions and actual combat events are retained
    // by the existing World and scenario checkpoint, not a second combat model.
    public sealed partial class GuidedPlaySession
    {
        bool IsTactic084=>ClassicCivilizationTactics084.Contains(Definition.Id);
        static ScenarioDocument BuildGuidedDocument084(RuleCatalog rules,GuidedPlayDefinition d)
            =>ClassicCivilizationTactics084.Contains(d.Id)?ClassicCivilizationTactics084.Build(rules,d.Id):RiverContent070.Build(rules,d.AuthoredScenarioId);
        static bool InDeployment084(ScenarioDocument d,Vector2 point)
        {
            var r=Array.Find(d.regions,x=>x.id=="deployment");
            return r!=null&&Mathf.Abs(point.x-r.center.x)<=r.size.x*.5f&&Mathf.Abs(point.y-r.center.y)<=r.size.y*.5f;
        }
        Vector2 TacticObjectivePoint084=>Array.Find(authoredDocument.regions,r=>r.id=="deployment").center;
        int TacticSuggestedTarget084=>World.ScenarioEntities("wave-one").Concat(World.ScenarioEntities("wave-two"))
            .FirstOrDefault(id=>World.IsVisible(0,World.Get(id)));
        int[] TacticSuggestedActors084=>state.soldiers.Where(id=>World.Get(id)?.Alive==true&&World.Get(id).garrisonedIn==0).ToArray();
        static void ConfigureTactic084(World w,GuidedPlayDefinition d,GuidedPlaySnapshot s)
        {
            s.soldiers=w.ScenarioEntities("tactic-army");
            s.movePoint=Array.Find(w.ScenarioDocument.regions,r=>r.id=="deployment").center;
            s.boardingPoint=s.landingPoint=s.buildPoint=s.barracksPoint=s.contactPoint=s.movePoint;
            w.Fog.Tick(w.Entities);w.Emit("GuidedScenarioSetup",0,s.scout,d.StartingSetupDescription);
        }
        static bool OwnArmy084(World w,GuidedPlaySnapshot s,int id,string unit="")
            =>s.soldiers.Contains(id)&&w.Get(id)?.owner==0&&(unit==""||w.Get(id).def.id==unit);
        static bool Enemy084(World w,int id)
            =>w.ScenarioEntities("wave-one").Contains(id)||w.ScenarioEntities("wave-two").Contains(id);
        static bool HumanFire084(GuidedPlaySnapshot s,GuidedPlayCommandProof[] proof,SimEvent shot,int index)
            =>proof.Any(p=>p.eventIndex<index&&p.command.kind==CommandKind.Order&&p.command.actors.Contains(shot.entity)&&
                (p.command.order==OrderKind.Attack&&p.command.target==shot.target||p.command.order==OrderKind.AttackMove||p.command.order==OrderKind.Hold));
        // Geometry is recorded immediately next to the real Fire event, after
        // its live visibility/range checks. A setup unit or prepared/cancelled
        // shot cannot satisfy this predicate; actual target damage is required.
        static bool Shot084(World w,GuidedPlaySnapshot s,GuidedPlayCommandProof[] proof,int i,int end,string unit,string damage,Tags targetTags=Tags.None,float beyond=-1)
        {
            if(i<0||i+1>=end)return false;var g=w.Events[i];var f=w.Events[i+1];
            if(g.kind!="TacticShot084"||g.player!=0||!OwnArmy084(w,s,g.entity,unit)||!Enemy084(w,g.target)||g.message!=damage||
                f.kind!="Fire"||f.entity!=g.entity||f.target!=g.target||f.player!=0||f.time!=g.time||f.message!=damage||f.amount<=0||
                !SnapshotCodec.Finite(g.amount)||!SnapshotCodec.Finite(f.amount)||g.amount<=beyond||g.amount<0||!HumanFire084(s,proof,g,i))return false;
            var attacker=w.Get(g.entity);var target=w.Get(g.target);
            if(target==null||targetTags!=Tags.None&&(target.def.tags&targetTags)==0)return false;
            float range=damage=="Melee"?1.5f:attacker.def.range;
            if(g.amount>range+1.001f)return false;
            // Direct/melee damage precedes Fire; a projectile impact follows.
            // The interval includes that projectile's physically possible flight.
            float travel=damage=="Melee"||attacker.def.projectileSpeed<=0?0:Mathf.Max(.08f,(g.amount+attacker.def.radius+target.def.radius)/attacker.def.projectileSpeed);
            return w.Events.Take(end).Any(e=>e.kind=="Damage"&&e.player==0&&e.entity==g.entity&&e.target==g.target&&e.message==damage&&SnapshotCodec.Finite(e.amount)&&e.amount>0&&
                e.time+.0001f>=g.time&&e.time<=g.time+travel+.201f);
        }
        static bool Trait084(World w,GuidedPlayDefinition d,GuidedPlaySnapshot s,GuidedPlayCommandProof[] proof,int end)
        {
            end=Math.Min(end,w.Events.Count);
            if(d.Id==ClassicCivilizationTactics084.British)
            {
                bool ranged=Enumerable.Range(0,Math.Max(0,end-1)).Any(i=>Shot084(w,s,proof,i,end,"longbow","Ranged",beyond:w.Rules.Entity("longbow").vision));
                bool frontline=w.Events.Take(end).Any(e=>e.kind=="Damage"&&e.player==1&&SnapshotCodec.Finite(e.amount)&&e.amount>0&&Enemy084(w,e.entity)&&OwnArmy084(w,s,e.target,"pike")&&
                    proof.Any(p=>p.eventIndex<w.Events.IndexOf(e)&&p.command.kind==CommandKind.Order&&p.command.actors.Contains(e.target)));
                return ranged&&frontline;
            }
            if(d.Id==ClassicCivilizationTactics084.Dutch)
                return w.Events.Take(end).Any(e=>e.kind=="TacticManeuver084"&&e.player==0&&e.message==s.attemptId&&ValidateManeuver084(w,s,proof,e,end));
            if(d.Id==ClassicCivilizationTactics084.French)
            {
                for(int i=0;i+1<end;i++)
                {
                    if(!Shot084(w,s,proof,i,end,"cuirassier","Melee"))continue;var shot=w.Events[i];
                    var area=w.Events.Take(i).LastOrDefault(e=>e.time==shot.time&&e.kind=="MeleeAreaHit"&&e.entity==shot.entity&&e.target==shot.target&&e.player==0&&SnapshotCodec.Finite(e.amount)&&e.amount>=1);
                    if(area==null)continue;
                    if(w.Events.Take(i).Where(e=>e.time==shot.time&&e.kind=="Damage"&&e.entity==shot.entity&&e.player==0&&e.message=="Melee"&&SnapshotCodec.Finite(e.amount)&&e.amount>0&&Enemy084(w,e.target))
                        .Select(e=>e.target).Distinct().Count()>=2)return true;
                }
                return false;
            }
            bool jan=false,abus=false;
            for(int i=0;i+1<end;i++)
            {
                if(Shot084(w,s,proof,i,end,"janissary","Melee",Tags.Cavalry)&&proof.Any(p=>p.eventIndex<i&&p.command.kind==CommandKind.ToggleMode&&p.command.actors.Contains(w.Events[i].entity)))jan=true;
                if(Shot084(w,s,proof,i,end,"abus","Ranged",Tags.HeavyInfantry))abus=true;
            }
            return jan&&abus;
        }
        static bool ValidateManeuver084(World w,GuidedPlaySnapshot s,GuidedPlayCommandProof[] proofs,SimEvent arrival,int end)
        {
            if(arrival.kind!="TacticManeuver084"||arrival.player!=0||arrival.message!=s.attemptId||arrival.amount!=1||!OwnArmy084(w,s,arrival.entity,"carabineer")||arrival.target<0||arrival.target>=end)return false;
            var p=proofs.FirstOrDefault(x=>x.eventIndex==arrival.target);if(p==null)return false;var c=p.command;
            if(c.kind!=CommandKind.Order||c.order!=OrderKind.Move||c.append||c.actors.Length!=1||c.actors[0]!=arrival.entity||
                p.eventIndex<=s.startEventIndex||p.eventIndex>=w.Events.IndexOf(arrival))return false;
            var origin=w.Events[p.eventIndex-1];
            if(origin.kind!="Command"||origin.entity!=arrival.entity||origin.player!=0||origin.message!="Move ×1"||origin.amount!=1||
                origin.time!=w.Events[p.eventIndex].time||arrival.time<=origin.time||Vector2.Distance(origin.position,arrival.position)<8||
                Vector2.Distance(c.point,arrival.position)>2)return false;
            // This rider must already have delivered real anti-cavalry ranged
            // damage before accepting this move. Moving before the hit fails.
            for(int i=0;i+1<p.eventIndex-1;i++)
                if(w.Events[i].entity==arrival.entity&&Shot084(w,s,proofs,i,p.eventIndex-1,"carabineer","Ranged",Tags.HeavyCavalry))return true;
            return false;
        }
        static int Deployments084(World w,GuidedPlayDefinition d,GuidedPlaySnapshot s,GuidedPlayCommandProof[] proof,int end)
        {
            var document=w.ScenarioDocument;
            return w.Events.Take(end).Where(e=>e.kind=="TacticDeployment084"&&e.amount==1&&e.player==0&&e.message==s.attemptId&&OwnArmy084(w,s,e.entity)&&
                InDeployment084(document,e.position)&&Vector2.Distance(w.Get(e.entity).origin,e.position)>=3&&
                proof.Any(p=>p.eventIndex<w.Events.IndexOf(e)&&p.command.kind==CommandKind.Order&&p.command.actors.Contains(e.entity)&&
                    (p.command.order==OrderKind.Move||p.command.order==OrderKind.AttackMove)&&InDeployment084(document,p.command.point)))
                .Select(e=>e.entity).Distinct().Count();
        }
        static bool Cleared084(World w,int end=int.MaxValue)
        {
            var one=w.ScenarioEntities("wave-one");var two=w.ScenarioEntities("wave-two");
            return one.Length==4&&two.Length==ClassicCivilizationTactics084.Wave2Count(w.ScenarioDocument.id)&&one.Concat(two).All(id=>w.Get(id)?.Alive==false)&&
                one.Concat(two).All(id=>w.Events.Any(e=>e.kind=="Death"&&e.entity==id&&e.target==0&&w.Events.IndexOf(e)<end))&&
                new[]{ClassicCivilizationTactics084.Wave1Goal,ClassicCivilizationTactics084.Wave2Goal}.All(id=>
                    w.ScenarioObjectiveStates.Any(o=>o.status=="completed"&&o.id==id)&&
                    w.Events.Take(end).Any(e=>e.kind=="ScenarioObjectiveCompleted"&&e.message.StartsWith(id+": ",StringComparison.Ordinal)))&&
                new[]{ClassicCivilizationTactics084.Wave1Trigger,ClassicCivilizationTactics084.Wave2Trigger}.All(id=>
                    w.ScenarioState.triggerStates.Any(t=>t.id==id&&t.fires==1&&t.lastError=="")&&
                    w.Events.Take(end).Any(e=>e.kind=="ScenarioTriggerFired"&&e.message.StartsWith(id+" · execution ",StringComparison.Ordinal)))&&
                w.Events.Take(end).Any(e=>e.kind=="ScenarioEnded"&&e.player==0);
        }
        float TacticProgress084(GuidedPlayStage stage)
        {
            var p=commands.ToArray();int end=World.Events.Count;
            if(stage.Id=="deploy")return Math.Min(stage.Required,Deployments084(World,Definition,state,p,end));
            if(stage.Id=="trait")return Trait084(World,Definition,state,p,end)?1:0;
            return Cleared084(World)&&World.Winner==0&&World.ScenarioState.scenarioVictory?1:0;
        }
        void ObserveTacticMovement084()
        {
            foreach(int id in state.soldiers)
            {
                var e=World.Get(id);if(e?.Alive!=true||e.garrisonedIn!=0)continue;
                if(state.stageIndex==0&&InDeployment084(authoredDocument,e.position)&&Vector2.Distance(e.origin,e.position)>=3&&
                    !World.Events.Any(x=>x.kind=="TacticDeployment084"&&x.entity==id&&x.message==state.attemptId)&&
                    commands.Any(p=>p.command.kind==CommandKind.Order&&p.command.actors.Contains(id)&&(p.command.order==OrderKind.Move||p.command.order==OrderKind.AttackMove)&&InDeployment084(authoredDocument,p.command.point)))
                    World.Emit("TacticDeployment084",0,id,state.attemptId,1,0,e.position);
                if(Definition.Id!=ClassicCivilizationTactics084.Dutch||e.def.id!="carabineer")continue;
                var p=commands.LastOrDefault(x=>x.command.kind==CommandKind.Order&&x.command.order==OrderKind.Move&&x.command.actors.Length==1&&x.command.actors[0]==id);
                if(p==null||World.Events.Any(x=>x.kind=="TacticManeuver084"&&x.entity==id&&x.target==p.eventIndex))continue;
                var candidate=new SimEvent{kind="TacticManeuver084",entity=id,player=0,target=p.eventIndex,time=World.Time,position=e.position,message=state.attemptId};
                // Validate against a non-mutating candidate, including its future
                // index; the arrival proof is emitted only after actual motion.
                if(ValidatePendingManeuver084(candidate,p))World.Emit(candidate.kind,0,id,state.attemptId,1,p.eventIndex,e.position);
            }
        }
        bool ValidatePendingManeuver084(SimEvent a,GuidedPlayCommandProof p)
        {
            var c=p.command;if(c.append||p.eventIndex<=state.startEventIndex||p.eventIndex>=World.Events.Count)return false;
            var origin=World.Events[p.eventIndex-1];if(origin.kind!="Command"||origin.entity!=a.entity||origin.message!="Move ×1"||origin.player!=0||origin.amount!=1||
                origin.time>=a.time||Vector2.Distance(origin.position,a.position)<8||Vector2.Distance(c.point,a.position)>2)return false;
            for(int i=0;i+1<p.eventIndex-1;i++)if(World.Events[i].entity==a.entity&&Shot084(World,state,commands.ToArray(),i,p.eventIndex-1,"carabineer","Ranged",Tags.HeavyCavalry))return true;
            return false;
        }
        static bool Survivors084(World w,GuidedPlayDefinition d,GuidedPlaySnapshot s)
        {
            var alive=s.soldiers.Select(w.Get).Where(e=>e?.Alive==true).ToArray();
            if(alive.Length<ClassicCivilizationTactics084.MinimumSurvivors(d.Id))return false;
            if(d.Id==ClassicCivilizationTactics084.British)return alive.Count(e=>e.def.id=="longbow")>=2&&alive.Any(e=>e.def.id=="pike");
            if(d.Id==ClassicCivilizationTactics084.Ottoman)return alive.Any(e=>e.def.id=="janissary")&&alive.Any(e=>e.def.id=="abus");
            return true;
        }
        void TickTactic084()
        {
            if(World.Get(state.town)?.Alive!=true||World.Players[0].resigned||World.Players[0].eliminated||!Survivors084(World,Definition,state))
            {Finish(GuidedPlayStatus.Failed,"营地或指定军队损失过大；重试并调整站位与目标。");return;}
            if(World.Time>=Definition.TimeLimit||World.IsMatchEnded&&World.Winner!=0||World.Events.Any(e=>e.kind=="ScenarioTriggerFailed"))
            {Finish(GuidedPlayStatus.Failed,"演习超时或敌军脚本未能正常执行；请重试。");return;}
            ObserveTacticMovement084();
            while(Stage!=null)
            {
                float progress=TacticProgress084(Stage);if(Mathf.Abs(checklist[state.stageIndex].Progress-progress)>.0001f)state.revision++;
                checklist[state.stageIndex].Progress=progress;if(progress+.0001f<Stage.Required)break;
                int index=World.Events.Count;World.Emit("GuidedStageCompleted",0,0,StageMarker(state,Stage.Id));stageEvents.Add(index);state.stageIndex++;state.revision++;
            }
            if(Stage==null){Finish(GuidedPlayStatus.Completed,"两波敌军已击退，战术操作与实际伤害均已验证。");return;}
            if(World.IsMatchEnded){Finish(GuidedPlayStatus.Failed,"敌军已清除，但战术操作目标尚未完成；请重试。");return;}
            RefreshChecklist();
        }
        static void ValidateTacticProgress084(World w,MatchSimulation simulation,GuidedPlayDefinition d,GuidedPlaySnapshot s)
        {
            Require(simulation.CaptureState().controllers.Length==0,"tactics has no economic AI");
            Require(s.town==w.ScenarioEntity("start-0-town").id&&s.scout==w.ScenarioEntity("start-0-explorer").id&&s.workers.Length==0&&
                s.soldiers.SequenceEqual(w.ScenarioEntities("tactic-army"))&&s.soldiers.Length==ClassicCivilizationTactics084.ArmyCount(d.Id)&&
                s.transport==0&&s.firstTarget==0&&s.secondTarget==0,"tactics frozen roles");
            Require(s.movePoint==Array.Find(w.ScenarioDocument.regions,r=>r.id=="deployment").center,"tactics public marker");
            for(int i=0;i<s.stageIndex;i++)
            {
                int end=s.stageEvents[i];bool valid=i==0?Deployments084(w,d,s,s.commands,end)>=d.Stages[0].Required:
                    i==1?Trait084(w,d,s,s.commands,end):Cleared084(w,end)&&w.ScenarioState.scenarioVictory&&w.Winner==0;
                Require(valid,"tactics actual stage proof "+d.Stages[i].Id);
            }
            if(s.status==GuidedPlayStatus.Completed)Require(Survivors084(w,d,s)&&Cleared084(w)&&w.Winner==0&&w.ScenarioState.scenarioVictory,"tactics victory and surviving roles");
            else if(s.status==GuidedPlayStatus.Running)Require(!w.IsMatchEnded&&Survivors084(w,d,s)&&w.Time<d.TimeLimit,"tactics running boundary");
        }
    }
    public partial class World
    {
        // This event records an already computed live firing distance. It never
        // changes targeting, vision, projectiles, damage or any legacy event.
        void ObserveTacticShot084(EntityState attacker,EntityState target,DamageKind kind,float distance)
        {
            if(scenarioDocument!=null&&ClassicCivilizationTactics084.Contains(scenarioDocument.id))
                Emit("TacticShot084",attacker.owner,attacker.id,kind.ToString(),distance,target.id,attacker.position);
        }
    }
}
