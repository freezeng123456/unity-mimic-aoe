using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // Decisions only: all spending, movement and healing go through World.Issue.
    // Doctors do not enter army cohorts, and a small persistent recovery cohort
    // gives actual wounded soldiers time to arrive, wait, heal and rejoin.
    public sealed class AiMedicalSupport076
    {
        readonly World world;readonly int player;readonly AiResourceContinuity072 continuity;
        readonly List<AiMedicalRecovery076Snapshot> recovering=new List<AiMedicalRecovery076Snapshot>();
        readonly List<AiMedicalRetry076Snapshot> cooldowns=new List<AiMedicalRetry076Snapshot>();
        float reviewAt;bool hasStation;Vector2 station;
        const float RecoverySeconds=90,ReviewSeconds=1.2f,RetrySeconds=30;
        const int RecoveryLimit=2;
        public AiMedicalSupport076(World world,int player,AiResourceContinuity072 continuity=null)
        {this.world=world??throw new ArgumentNullException(nameof(world));this.player=player;this.continuity=continuity??new AiResourceContinuity072(world,player);}
        public int[] RecoveryMembers { get {var ids=new int[recovering.Count];for(int i=0;i<ids.Length;i++)ids[i]=recovering[i].id;return ids;} }
        public bool IsRecovering(int id)=>recovering.Exists(r=>r.id==id);
        public Vector2 Station=>station;
        public bool HasStation=>hasStation;
        public string UnitId=>world.Players[player].civilization=="ottoman"?"imam":Classic?"priest":"";
        bool Classic=>world.Players[player].civilization=="coast"||world.Players[player].civilization=="river"||world.Players[player].civilization=="france";
        public string Building=>UnitId=="imam"?"mosque":UnitId=="priest"?"church":"";
        bool PatientRole(EntityState e)=>e!=null&&e.Alive&&e.Complete&&e.owner==player&&e.Military&&!e.def.waterborne&&
            e.def.canReceiveHealing&&(e.def.tags&Tags.Explorer)==0;
        bool Patient(EntityState e)=>PatientRole(e)&&e.garrisonedIn==0;
        bool CoolingDown(int id)=>cooldowns.Exists(r=>r.id==id&&world.Time<r.retryAt);
        void CleanCooldowns()=>cooldowns.RemoveAll(r=>r.retryAt<=world.Time||!PatientRole(world.Get(r.id)));
        int DoctorCount()=>UnitId==""?0:world.UnitCountIncludingProduction(player,UnitId);
        float Deficit(List<EntityState> own)
        {float missing=0;foreach(var e in own)if(Patient(e))missing+=Mathf.Max(0,world.MaxHp(e)-e.hp);return missing;}
        public bool HasInvestmentDemand(List<EntityState> own,int workers,int soldiers)
        {return UnitId!=""&&world.Players[player].age>=2&&workers>=12&&soldiers>=6&&Deficit(own)>=100&&DoctorCount()==0;}
        public bool NeedsBuilding(List<EntityState> own,int workers,int soldiers)=>HasInvestmentDemand(own,workers,soldiers)&&
            !own.Exists(e=>e.Alive&&e.def.id==Building);
        public bool YieldNoncriticalMosqueResearch(List<EntityState> own,int workers,int soldiers)=>Building=="mosque"&&
            HasInvestmentDemand(own,workers,soldiers)&&workers<world.WorkerLimit(player)-4;
        public Cost Forecast(List<EntityState> own,int workers,int soldiers)
        {
            if(!HasInvestmentDemand(own,workers,soldiers))return new Cost();
            return !own.Exists(e=>e.Alive&&e.def.id==Building)?world.EntityCost(player,world.Rules.Entity(Building)):
                world.UnitCost(player,world.Rules.Entity(UnitId));
        }
        public void TryInvest(List<EntityState> own,int workers,int soldiers,Cost reserve,bool allowed)
        {
            if(!allowed||world.Paused||world.IsMatchEnded||!world.IsPlayerActive(player)||UnitId==""||world.Players[player].age<2||workers<12||soldiers<6)return;
            float missing=Deficit(own);int count=DoctorCount();int wanted=missing>=250&&soldiers>=20?2:missing>=100?1:0;
            if(wanted==0)return;
            var producer=own.Find(e=>e.Alive&&e.Complete&&e.owner==player&&e.garrisonedIn==0&&e.def.id==Building&&e.queue.Count==0);
            if(producer==null)return;
            if(count<wanted)
            {
                var definition=world.Rules.Entity(UnitId);var cost=world.UnitCost(player,definition);
                if(world.ProductionReason(player,producer,definition.id)==""&&CanSpend(cost,reserve))
                    Send(new GameCommand{kind=CommandKind.Train,actors=new[]{producer.id},item=definition.id});
                return;
            }
            if(count==0||world.Players[player].age<3||missing<250)return;
            var tech=world.Rules.Tech(Building=="mosque"?"mission_fervor":"mission_fervor_church");
            if(tech!=null&&world.ProductionReason(player,producer,tech.id,true)==""&&CanSpend(tech.cost,reserve))
                Send(new GameCommand{kind=CommandKind.Research,actors=new[]{producer.id},item=tech.id});
        }
        bool CanSpend(Cost cost,Cost reserve)=>world.CanAfford(player,cost)&&
            AiResourceContinuity072.CanSpend(world,player,cost,continuity.EnsureProtectedReserve(reserve));
        public AiMedicalSupport076Snapshot CaptureState()
        {
            var copy=new AiMedicalRecovery076Snapshot[recovering.Count];
            for(int i=0;i<copy.Length;i++){var r=recovering[i];copy[i]=new AiMedicalRecovery076Snapshot{id=r.id,deadline=r.deadline,point=r.point,waiting=r.waiting};}
            // A pending save contains only live, unexpired owned cooldowns,
            // even when a death occurred between scheduled AI reviews.
            var retries=new List<AiMedicalRetry076Snapshot>();
            foreach(var r in cooldowns)if(r.retryAt>world.Time&&PatientRole(world.Get(r.id)))
                retries.Add(new AiMedicalRetry076Snapshot{id=r.id,retryAt=r.retryAt});
            retries.Sort((a,b)=>a.id.CompareTo(b.id));
            return new AiMedicalSupport076Snapshot{reviewAt=reviewAt,hasStation=hasStation,station=station,recovering=copy,cooldowns=retries.ToArray()};
        }
        public void RestoreState(AiMedicalSupport076Snapshot state)
        {
            AiSaveValidation.Require(state!=null&&state.schema==1&&AiSaveValidation.Clock(state.reviewAt)&&state.reviewAt<=world.Time+ReviewSeconds+.11f&&
                InMap(state.station)&&state.recovering!=null&&state.recovering.Length<=RecoveryLimit&&
                state.cooldowns!=null&&state.cooldowns.Length<=world.Rules.populationLimit,"medical support state");
            var seen=new HashSet<int>();
            foreach(var r in state.recovering)
            {
                var e=r==null?null:world.Get(r.id);
                // Dead records can exist until the next bounded review, just as
                // army cohorts may retain a newly killed member at a save boundary.
                AiSaveValidation.Require(r!=null&&r.id>0&&seen.Add(r.id)&&e!=null&&e.owner==player&&e.Military&&
                    e.def.canReceiveHealing&&!e.def.waterborne&&(e.def.tags&Tags.Explorer)==0&&
                    AiSaveValidation.Clock(r.deadline)&&r.deadline<=world.Time+RecoverySeconds+.11f&&InMap(r.point),"medical recovery member");
            }
            foreach(var r in state.cooldowns)
                AiSaveValidation.Require(r!=null&&r.id>0&&seen.Add(r.id)&&PatientRole(world.Get(r.id))&&
                    AiSaveValidation.Clock(r.retryAt)&&r.retryAt>world.Time&&r.retryAt<=world.Time+RetrySeconds+.11f,"medical retry cooldown");
            reviewAt=state.reviewAt;hasStation=state.hasStation;station=state.station;recovering.Clear();cooldowns.Clear();
            foreach(var r in state.recovering)recovering.Add(new AiMedicalRecovery076Snapshot{id=r.id,deadline=r.deadline,point=r.point,waiting=r.waiting});
            foreach(var r in state.cooldowns)cooldowns.Add(new AiMedicalRetry076Snapshot{id=r.id,retryAt=r.retryAt});
        }
        bool InMap(Vector2 p)=>SnapshotCodec.Finite(p)&&p.x>=0&&p.y>=0&&p.x<=world.Navigation.Width&&p.y<=world.Navigation.Height;
        bool Known(Vector2 point)=>world.Fog.At(player,point)!=VisibilityState.Unexplored;
        bool Route(EntityState unit,Vector2 goal,List<EntityState> visible)
        {
            if(!Known(goal)||!world.Navigation.IsWalkable(unit.position,unit.def.radius)||!world.Navigation.IsWalkable(goal,unit.def.radius))return false;
            var routes=new AiGatherRoutes074(world,player,visible);
            return routes.OrdinaryPathSafe(unit,goal);
        }
        bool RetreatRoute(EntityState unit,Vector2 goal,List<EntityState> visible)
        {
            var nav=world.Navigation;
            if(!Known(goal)||!nav.IsWalkable(unit.position,unit.def.radius)||!nav.IsWalkable(goal,unit.def.radius)||
                !AiResourceContinuity072.Safe(world,player,goal,visible))return false;
            List<Vector2> path;
            if(nav.ClearLine(unit.position,goal,unit.def.radius))path=new List<Vector2>{unit.position,goal};
            else path=nav.FindPath(unit.position,goal,unit.def.radius);
            if(path.Count<2||(path[0]-unit.position).sqrMagnitude>.01f||(path[path.Count-1]-goal).sqrMagnitude>.01f)return false;
            foreach(var threat in visible)
            {
                if(!AiResourceContinuity072.EconomicThreat(world,player,threat))continue;
                float reach=Mathf.Max(12,threat.def.range+4);bool leaving=(unit.position-threat.position).sqrMagnitude<reach*reach;
                for(int i=1;i<path.Count;i++)
                {
                    if(!Known(path[i]))return false;
                    var from=path[i-1];var delta=path[i]-from;float length=delta.sqrMagnitude;
                    float t=length>0?Mathf.Clamp01(Vector2.Dot(threat.position-from,delta)/length):0;
                    float minimum=(threat.position-(from+delta*t)).sqrMagnitude;
                    if(leaving)
                    {
                        // Every actual leg must increase distance until outside;
                        // a merely safe final point does not authorize a dip in.
                        if(minimum+.01f<(from-threat.position).sqrMagnitude)return false;
                        if((path[i]-threat.position).sqrMagnitude>=reach*reach)leaving=false;
                    }
                    else if(minimum<reach*reach)return false;
                }
            }
            return true;
        }
        bool SelectStation(List<EntityState> own,List<EntityState> doctors,List<EntityState> visible,Vector2 home)
        {
            if(doctors.Count==0){hasStation=false;return false;}
            if(hasStation&&Known(station)&&AiResourceContinuity072.Safe(world,player,station,visible)&&
                doctors.Exists(e=>RetreatRoute(e,station,visible)))return true;
            hasStation=false;
            // Stable bounded rear-of-home search: never pick a hidden shore or
            // claim that a safe endpoint proves an unsafe ordinary route safe.
            Vector2 away=(home-new Vector2(world.Navigation.Width*.5f,world.Navigation.Height*.5f)).normalized;
            if(away.sqrMagnitude<.01f)away=Vector2.down;
            for(int ring=0;ring<3;ring++)for(int n=0;n<12;n++)
            {
                float angle=n*Mathf.PI/6;Vector2 center=home+away*8;
                Vector2 point=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(3+ring*3);
                point=new Vector2(Mathf.Round(point.x),Mathf.Round(point.y));
                if(!Known(point)||!AiResourceContinuity072.Safe(world,player,point,visible)||!world.Navigation.IsWalkable(point,.6f))continue;
                if(!doctors.Exists(e=>RetreatRoute(e,point,visible)))continue;
                station=point;hasStation=true;return true;
            }
            return false;
        }
        void Release(int index,string reason,bool retry=false)
        {
            int id=recovering[index].id;recovering.RemoveAt(index);
            if(retry&&PatientRole(world.Get(id)))
            {
                cooldowns.RemoveAll(r=>r.id==id);
                cooldowns.Add(new AiMedicalRetry076Snapshot{id=id,retryAt=world.Time+RetrySeconds});
            }
            // End only this known recovery assignment. A single stale Heal
            // would otherwise follow the returning soldier towards the front;
            // queued or unrelated intent still belongs to its original owner.
            foreach(var doctor in world.Entities)
                if(doctor.Alive&&doctor.Complete&&doctor.owner==player&&doctor.def.healer&&doctor.garrisonedIn==0&&
                    doctor.orders.Count==1&&doctor.orders.Peek().kind==OrderKind.Heal&&doctor.orders.Peek().target==id)
                    Send(new GameCommand{kind=CommandKind.Order,actors=new[]{doctor.id},order=OrderKind.Hold});
            world.Emit("AIMedicalReturn",player,id,reason,0,0,world.Get(id)?.position??station);
            // No forced attack: the ordinary army planner decides the next
            // real formation command after this owned unit leaves recovery.
        }
        bool ExpectedOrder(EntityState patient,AiMedicalRecovery076Snapshot r)=>r.waiting?
            patient.hold&&patient.orders.Count==0:patient.orders.Count==0||patient.orders.Count==1&&patient.orders.Peek().kind==OrderKind.Move&&
                (patient.orders.Peek().point-r.point).sqrMagnitude<.01f;
        public void ReviewSupport(List<EntityState> own,List<EntityState> army,List<EntityState> visible,Vector2 home)
        {
            if(world.Paused||world.IsMatchEnded||!world.IsPlayerActive(player)||world.Time<reviewAt)return;
            reviewAt=world.Time+ReviewSeconds;CleanCooldowns();
            var doctors=own.FindAll(e=>e.Alive&&e.Complete&&e.owner==player&&e.def.healer&&e.garrisonedIn==0);
            doctors.Sort((a,b)=>a.id.CompareTo(b.id));
            bool ready=SelectStation(own,doctors,visible,home);
            for(int i=recovering.Count-1;i>=0;i--)
            {
                var r=recovering[i];var e=world.Get(r.id);
                if(!Patient(e)||e.hp>=world.MaxHp(e)*.85f||world.Time>=r.deadline)
                {
                    bool restored=Patient(e)&&e.hp>=world.MaxHp(e)*.85f;
                    Release(i,e==null||!e.Alive?"Recovery member lost":restored?"Reached 85% health; ordinary army may rejoin":"Bounded recovery window expired",!restored);
                    continue;
                }
                if(!ExpectedOrder(e,r)){Release(i,"A different order overrides medical recovery",true);continue;}
                if(!ready||!AiResourceContinuity072.Safe(world,player,r.point,visible))
                {Release(i,"Medical station no longer has observed safe access",true);continue;}
                if((r.point-station).sqrMagnitude>6*6)
                {
                    if(!Route(e,station,visible)){Release(i,"New station has no observed safe ordinary route",true);continue;}
                    r.point=station;r.waiting=false;
                    if(Send(new GameCommand{kind=CommandKind.Order,actors=new[]{e.id},order=OrderKind.Move,point=r.point}).accepted&&e.orders.Count>0)
                        r.point=e.orders.Peek().point;
                }
                if(Vector2.Distance(e.position,r.point)<=1.6f)
                {
                    if(!e.hold&&Send(new GameCommand{kind=CommandKind.Order,actors=new[]{e.id},order=OrderKind.Hold}).accepted)r.waiting=true;
                }
                else if(e.orders.Count==0&&Route(e,r.point,visible))
                    Send(new GameCommand{kind=CommandKind.Order,actors=new[]{e.id},order=OrderKind.Move,point=r.point});
            }
            if(!ready)return;
            int available=0;foreach(var e in army)if(Patient(e)&&!IsRecovering(e.id))available++;
            var candidates=army.FindAll(e=>Patient(e)&&!IsRecovering(e.id)&&!CoolingDown(e.id)&&e.hp<world.MaxHp(e)*.45f&&e.fireAt<0&&e.healingBlockedUntil<=world.Time&&
                (e.orders.Count==0||e.orders.Count==1&&(e.orders.Peek().kind==OrderKind.AttackMove)));
            // Health deficit and real paid value, then stable ID. Never look at
            // fog-hidden enemy positions or alter HP to create a medical demand.
            candidates.Sort((a,b)=>
            {float av=world.MaxHp(a)-a.hp+(a.def.cost.food+a.def.cost.wood+a.def.cost.coin)*.25f;
             float bv=world.MaxHp(b)-b.hp+(b.def.cost.food+b.def.cost.wood+b.def.cost.coin)*.25f;int value=bv.CompareTo(av);return value!=0?value:a.id.CompareTo(b.id);});
            foreach(var e in candidates)
            {
                if(recovering.Count>=RecoveryLimit||available<=4)break;
                // Spread arrival points within the doctor's true healing range.
                var point=station+Vector2.right*(recovering.Count==0?-1.5f:1.5f);
                if(!RetreatRoute(e,point,visible))continue;
                if(!Send(new GameCommand{kind=CommandKind.Order,actors=new[]{e.id},order=OrderKind.Move,point=point}).accepted)continue;
                var admittedPoint=e.orders.Count>0?e.orders.Peek().point:point;
                recovering.Add(new AiMedicalRecovery076Snapshot{id=e.id,deadline=world.Time+RecoverySeconds,point=admittedPoint});available--;
                world.Emit("AIMedicalRetreat",player,e.id,"A real wounded soldier leaves the ordinary cohort for bounded medical recovery",e.hp,0,point);
            }
            var taken=new HashSet<int>();
            foreach(var doctor in doctors)
            {
                // Explicit attack, queued human-style orders and transport are
                // not overwritten by support review. Medical commands remain
                // ordinary authority commands, not private movement writes.
                if(doctor.orders.Count>1||doctor.orders.Count==1&&doctor.orders.Peek().kind!=OrderKind.Move&&doctor.orders.Peek().kind!=OrderKind.Heal)continue;
                if(!AiResourceContinuity072.Safe(world,player,doctor.position,visible))
                {
                    // A retreat may leave an existing visible threat envelope,
                    // but never approach its weapon or enter another envelope.
                    if(RetreatRoute(doctor,station,visible))Send(new GameCommand{kind=CommandKind.Order,actors=new[]{doctor.id},order=OrderKind.Move,point=station});
                    continue;
                }
                if(Vector2.Distance(doctor.position,station)>3)
                {
                    if(Route(doctor,station,visible)&&(doctor.orders.Count==0||(doctor.orders.Peek().point-station).sqrMagnitude>.01f))
                        Send(new GameCommand{kind=CommandKind.Order,actors=new[]{doctor.id},order=OrderKind.Move,point=station});
                    continue;
                }
                EntityState target=null;
                foreach(var r in recovering)
                {
                    var candidate=world.Get(r.id);
                    if(!r.waiting||!Patient(candidate)||taken.Contains(candidate.id)||world.HealingTargetReason(player,doctor,candidate)!="")continue;
                    if(!Route(doctor,candidate.position,visible))continue;target=candidate;break;
                }
                if(target!=null)
                {
                    taken.Add(target.id);
                    if(doctor.orders.Count==0||doctor.orders.Peek().kind!=OrderKind.Heal||doctor.orders.Peek().target!=target.id)
                        Send(new GameCommand{kind=CommandKind.Order,actors=new[]{doctor.id},order=OrderKind.Heal,target=target.id});
                }
                else if(doctor.orders.Count==0&&!doctor.autoHeal)
                    Send(new GameCommand{kind=CommandKind.ToggleAutoHeal,actors=new[]{doctor.id}});
            }
        }
        CommandResult Send(GameCommand c)
        {c.player=player;var result=world.Issue(c);if(result.accepted)continuity.InvalidateSourceSurvey();return result;}
    }
}
