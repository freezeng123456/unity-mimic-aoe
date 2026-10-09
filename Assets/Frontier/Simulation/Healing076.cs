using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public partial class World
    {
        // Project rules P: medicine is a separate, unpaid, out-of-combat service.
        public const float HealingCombatDelay076=3,HealingReviewDelay076=.5f;
        public bool CanHeal(EntityState actor)=>actor!=null&&actor.Alive&&actor.Complete&&actor.owner>=0&&
            IsPlayerActive(actor.owner)&&actor.garrisonedIn==0&&actor.def.kind==EntityKind.Unit&&!actor.def.waterborne&&
            actor.Healer&&SnapshotCodec.Finite(actor.def.healRate)&&SnapshotCodec.Finite(actor.def.healRange)&&actor.def.healRate>0&&actor.def.healRange>0;

        string HealingCapabilityReason076(int player,EntityState actor,EntityState target,bool requireWound)
        {
            if(actor==null||actor.owner!=player||!CanHeal(actor))return "Select your active healer";
            if(target==null||!target.Alive||!target.Complete||target.garrisonedIn!=0)return "Choose a living available patient";
            if(target.id==actor.id)return "A healer needs another patient";
            if(target.owner<0||!IsPlayerActive(target.owner)||!AreAllies(player,target.owner))return "Choose your own or allied patient";
            if(target.def.kind!=EntityKind.Unit||target.def.waterborne||!target.def.canReceiveHealing)return "This unit does not receive healing";
            if(!IsVisible(player,target))return "Patient is not currently visible";
            if(requireWound&&target.hp>=MaxHp(target))return "Patient is at full health";
            return "";
        }
        public string HealingTargetReason(int player,EntityState actor,EntityState target)=>HealingCapabilityReason076(player,actor,target,true);
        public float HealRate(EntityState actor)
        {
            if(actor==null||!actor.Healer)return 0;
            float factor=1;var player=P(actor.owner);
            if(player!=null)foreach(var id in player.technologies){var tech=Rules.Tech(id);if(tech!=null&&Applies(tech,actor))factor+=tech.healing;}
            return Mathf.Max(0,actor.def.healRate*factor);
        }
        public float HealRange(EntityState actor)=>actor!=null&&actor.Healer?Mathf.Max(0,actor.def.healRange):0;
        float HealerTrainingSpeed076(int player,EntityDef definition)
        {
            float factor=1;var sample=new EntityState{def=definition};
            foreach(var id in Players[player].technologies){var tech=Rules.Tech(id);if(tech!=null&&Applies(tech,sample))factor+=tech.healerTrainSpeed;}
            return Mathf.Max(.01f,factor);
        }
        public string HealingPauseReason(EntityState actor,EntityState target)
        {
            string reason=HealingTargetReason(actor?.owner??-1,actor,target);if(reason!="")return reason;
            if(Time<actor.healingBlockedUntil||Time<target.healingBlockedUntil)return "Recovering from combat";
            if(actor.fireAt>=0||target.fireAt>=0)return "Attack preparation in progress";
            if((actor.position-actor.previousPosition).sqrMagnitude>.000001f)return "Healer is moving";
            if((target.position-target.previousPosition).sqrMagnitude>.000001f)return "Patient is moving";
            if(EdgeDistance(actor,target)>HealRange(actor)+.0001f)return "Patient is outside healing range";
            if(!HealingLine076(actor.position,target.position))return "Healing line is obstructed";
            return "";
        }
        bool HealingLine076(Vector2 source,Vector2 target)=>Navigation.ClearLine(source,target,.05f);
        void BlockHealing076(EntityState actor)
        {
            if(actor==null)return;
            actor.healingBlockedUntil=Mathf.Max(actor.healingBlockedUntil,Time+HealingCombatDelay076);
            if(actor.Healer)actor.work=default;
        }
        void ClearHealing076(EntityState actor)
        {
            if(actor==null)return;actor.healingTarget=0;actor.healingReviewAt=0;
            if(actor.work.kind==WorkKind.Heal)actor.work=default;
        }
        void ClearPatientHealingLinks076(EntityState patient)
        {
            if(patient==null||patient.def.kind!=EntityKind.Unit||!patient.def.canReceiveHealing)return;
            foreach(var actor in Entities)
            {
                if(!actor.Healer)continue;
                if(actor.healingTarget==patient.id)ClearHealing076(actor);
                bool removed=false;var orders=actor.orders.ToArray();
                foreach(var order in orders)if(order.kind==OrderKind.Heal&&order.target==patient.id){removed=true;break;}
                if(!removed)continue;
                actor.orders.Clear();foreach(var order in orders)if(order.kind!=OrderKind.Heal||order.target!=patient.id)actor.orders.Enqueue(order);
                InvalidateCommandPath(actor);actor.status="Patient unavailable";
            }
        }
        CommandResult ToggleAutoHeal076(GameCommand command)
        {
            int changed=0;var seen=new HashSet<int>();
            foreach(int id in command.actors)
            {
                if(!seen.Add(id))continue;var actor=Get(id);if(actor?.owner!=command.player||!CanHeal(actor))continue;
                actor.autoHeal=!actor.autoHeal;
                if(actor.orders.Count==0||actor.orders.Peek().kind!=OrderKind.Heal)ClearHealing076(actor);
                Emit("AutoHeal",actor.owner,actor.id,actor.autoHeal?"Automatic healing enabled":"Automatic healing disabled",actor.autoHeal?1:0);changed++;
            }
            return changed>0?CommandResult.Ok("Automatic healing changed"):CommandResult.Fail("Select your active healer");
        }
        CommandResult IssueHealOrders076(GameCommand command)
        {
            var target=Get(command.target);var actors=new List<EntityState>();var seen=new HashSet<int>();string reason="Select your active healer";
            foreach(int id in command.actors)
            {
                if(!seen.Add(id))continue;var actor=Get(id);reason=HealingTargetReason(command.player,actor,target);
                if(reason=="")actors.Add(actor);
            }
            if(actors.Count==0)return CommandResult.Fail(reason);
            foreach(var actor in actors)
            {
                if(!command.append)
                {
                    actor.orders.Clear();InvalidateCommandPath(actor);ClearHealing076(actor);actor.attackTarget=0;actor.fireAt=-1;
                }
                actor.hold=false;actor.orders.Enqueue(new UnitOrder(OrderKind.Heal,target.position,target.id));
                if(actor.orders.Count==1){actor.healingTarget=target.id;actor.status="Heal";}
            }
            var ev=Emit("Command",command.player,actors[0].id,"Heal ×"+actors.Count,actors.Count,target.id);
            for(int observer=0;observer<Players.Length;observer++)
                if(!IsVisible(observer,actors[0])||!IsVisible(observer,target))ev.visibilityMask&=~(1<<observer);
            return CommandResult.Ok("Heal order accepted");
        }
        void FinishHealOrder076(EntityState actor,UnitOrder order,string status)
        {
            if(actor.orders.Count>0&&ReferenceEquals(actor.orders.Peek(),order))actor.orders.Dequeue();
            ClearHealing076(actor);InvalidateCommandPath(actor);actor.status=status;
        }
        void TickHealApproach076(EntityState actor,UnitOrder order,float dt)
        {
            var patient=Get(order.target);
            if(HealingCapabilityReason076(actor.owner,actor,patient,false)!="")
            {FinishHealOrder076(actor,order,"Patient unavailable");return;}
            if(patient.hp>=MaxHp(patient)){FinishHealOrder076(actor,order,"Patient restored");return;}
            actor.attackTarget=0;actor.fireAt=-1;actor.healingTarget=patient.id;
            float range=HealRange(actor);
            if(EdgeDistance(actor,patient)<=range&&HealingLine076(actor.position,patient.position))
            {actor.path.Clear();actor.facing=(patient.position-actor.position).normalized;actor.status="Waiting to heal";return;}
            // Explicit healing approaches through the same real land navigation as Move.
            // Keep a legal approach until the patient moves or the retry clock expires.
            if(!order.hasResolvedPoint||(order.point-patient.position).sqrMagnitude>1||
                !Navigation.IsWalkable(order.resolvedPoint,actor.def.radius)||
                !HealingLine076(order.resolvedPoint,patient.position)||Time>=order.destinationRetryAt)
            {
                order.point=patient.position;order.hasResolvedPoint=TryHealingApproach076(actor,patient,out order.resolvedPoint);
                order.destinationRetryAt=Time+1.2f;
            }
            if(!order.hasResolvedPoint){actor.path.Clear();actor.status="Patient route blocked; waiting";return;}
            MoveTowardsTarget(actor,order.resolvedPoint,dt,.25f);
            if((actor.position-actor.previousPosition).sqrMagnitude<=.000001f)actor.status="Approaching patient";
        }
        bool TryHealingApproach076(EntityState actor,EntityState patient,out Vector2 chosen)
        {
            chosen=actor.position;float best=float.MaxValue;
            Vector2 away=(actor.position-patient.position).normalized;if(away.sqrMagnitude<.01f)away=Vector2.right;
            float radius=actor.def.radius+patient.def.radius+Mathf.Max(.3f,HealRange(actor)*.72f);
            for(int i=0;i<16;i++)
            {
                float angle=i*Mathf.PI/8,cos=Mathf.Cos(angle),sin=Mathf.Sin(angle);
                var direction=new Vector2(away.x*cos-away.y*sin,away.x*sin+away.y*cos);
                var candidate=Navigation.NearestWalkable(patient.position+direction*radius,actor.def.radius);
                if(!Navigation.IsWalkable(candidate,actor.def.radius)||Vector2.Distance(candidate,patient.position)>HealRange(actor)+actor.def.radius+patient.def.radius||
                    !HealingLine076(candidate,patient.position))continue;
                var path=Navigation.FindPath(actor.position,candidate,actor.def.radius);
                if(path.Count==0||(path[path.Count-1]-candidate).sqrMagnitude>.0001f||!Navigation.ClearLine(actor.position,path[0],actor.def.radius))continue;
                float length=Vector2.Distance(actor.position,path[0]);for(int leg=1;leg<path.Count;leg++)length+=Vector2.Distance(path[leg-1],path[leg]);
                if(length<best){best=length;chosen=candidate;}
            }
            return best<float.MaxValue;
        }
        EntityState AutoHealingPatient076(EntityState actor,HashSet<int> excluded=null)
        {
            var current=Get(actor.healingTarget);
            if(current!=null&&(excluded==null||!excluded.Contains(current.id))&&HealingPauseReason(actor,current)=="")return current;
            EntityState best=null;float deficit=-1,distance=float.MaxValue;
            foreach(var patient in Entities)
            {
                if(excluded!=null&&excluded.Contains(patient.id)||HealingPauseReason(actor,patient)!="")continue;
                float missing=1-patient.hp/MaxHp(patient),edge=EdgeDistance(actor,patient);
                if(missing>deficit+.00001f||Mathf.Abs(missing-deficit)<=.00001f&&(edge<distance-.00001f||Mathf.Abs(edge-distance)<=.00001f&&(best==null||patient.id<best.id)))
                {best=patient;deficit=missing;distance=edge;}
            }
            return best;
        }
        sealed class HealingRequest076
        {
            public EntityState actor,patient;public float rate,distance;public bool automatic;
        }
        void TickHealing076(float dt)
        {
            var requests=new List<HealingRequest076>();
            foreach(var actor in Entities)
            {
                if(!actor.Healer)continue;
                if(!CanHeal(actor)){ClearHealing076(actor);continue;}
                var order=actor.orders.Count>0?actor.orders.Peek():null;
                if(order!=null&&order.kind!=OrderKind.Heal){ClearHealing076(actor);continue;}
                EntityState patient;
                if(order!=null)
                {
                    patient=Get(order.target);
                    if(HealingCapabilityReason076(actor.owner,actor,patient,false)!=""){FinishHealOrder076(actor,order,"Patient unavailable");continue;}
                    if(patient.hp>=MaxHp(patient)){FinishHealOrder076(actor,order,"Patient restored");continue;}
                    actor.healingTarget=patient.id;
                }
                else
                {
                    if(!actor.autoHeal){ClearHealing076(actor);continue;}
                    patient=Get(actor.healingTarget);
                    if(Time>=actor.healingReviewAt||HealingTargetReason(actor.owner,actor,patient)!="")
                    {patient=AutoHealingPatient076(actor);actor.healingTarget=patient?.id??0;actor.healingReviewAt=Time+HealingReviewDelay076;}
                    if(patient==null)continue;
                }
                string pause=HealingPauseReason(actor,patient);
                if(pause!=""){actor.status=pause;continue;}
                float rate=HealRate(actor);if(rate<=0)continue;
                requests.Add(new HealingRequest076{actor=actor,patient=patient,rate=rate,distance=EdgeDistance(actor,patient),automatic=order==null});
            }
            // All requests are gathered after every projectile impact. Stable arbitration
            // prevents stacking and never depends on the Entities list iteration order.
            var settled=new HashSet<int>();
            while(requests.Count>0)
            {
                requests.Sort((a,b)=>
                {
                    int value=a.patient.id.CompareTo(b.patient.id);if(value!=0)return value;
                    value=b.rate.CompareTo(a.rate);if(value!=0)return value;
                    value=a.distance.CompareTo(b.distance);return value!=0?value:a.actor.id.CompareTo(b.actor.id);
                });
                var automaticLosers=new List<HealingRequest076>();
                foreach(var request in requests)
                {
                    if(settled.Contains(request.patient.id))
                    {
                        request.actor.status="Patient already receiving care";
                        if(request.automatic)automaticLosers.Add(request);
                        continue;
                    }
                    var patient=request.patient;var actor=request.actor;
                    if(HealingPauseReason(actor,patient)!="")continue;
                    float max=MaxHp(patient),before=patient.hp,delta=Mathf.Min(request.rate*dt,max-before);if(delta<=0)continue;
                    patient.hp=Mathf.Min(max,before+delta);float actual=patient.hp-before;if(actual<=0)continue;
                    settled.Add(patient.id);actor.facing=(patient.position-actor.position).normalized;actor.status="Healing";
                    actor.work=new WorkActivity{kind=WorkKind.Heal,target=patient.id,time=Time};
                    var receipt=new HealingReceipt{source=actor.id,target=patient.id,hpBefore=before,hpAfter=patient.hp,maxHp=max,actual=actual,rate=request.rate};
                    var ev=Emit("Heal",actor.owner,actor.id,"Patient restored",actual,patient.id,patient.position);ev.hasHealing=true;ev.healing=receipt;
                    // Seeing the healer alone must not disclose an allied concealed Scout.
                    for(int observer=0;observer<Players.Length;observer++)
                        if(!IsVisible(observer,actor)||!IsVisible(observer,patient))ev.visibilityMask&=~(1<<observer);
                    if(patient.hp>=max&&actor.orders.Count>0&&actor.orders.Peek().kind==OrderKind.Heal)
                    {actor.orders.Dequeue();actor.path.Clear();/* Retain this tick's actual work; clear on the next tick. */}
                }
                // Automatic support fills other patients, while an explicit Heal
                // remains fixed to its requested target. Reserve every first-round
                // winner before selecting fallbacks so list order cannot bias care.
                requests.Clear();
                foreach(var loser in automaticLosers)
                {
                    var patient=AutoHealingPatient076(loser.actor,settled);if(patient==null)continue;
                    loser.patient=patient;loser.distance=EdgeDistance(loser.actor,patient);loser.actor.healingTarget=patient.id;
                    loser.actor.healingReviewAt=Time+HealingReviewDelay076;requests.Add(loser);
                }
            }
        }

        static void ValidateHealingSnapshot076(RuleCatalog rules,WorldSnapshot snapshot)
        {
            void Require(bool valid,string why){if(!valid)throw new ArgumentException("Invalid saved match: "+why);}
            var entities=new Dictionary<int,EntitySnapshot>();foreach(var saved in snapshot.entities)entities.Add(saved.state.id,saved);
            foreach(var saved in snapshot.entities)
            {
                var actor=saved.state;var definition=rules.Entity(saved.definition);
                if(actor.healingTarget>0)
                {
                    Require(definition.healer&&actor.owner>=0&&actor.Alive&&actor.Complete&&actor.garrisonedIn==0&&!snapshot.players[actor.owner].state.eliminated&&!snapshot.players[actor.owner].state.resigned,"active healer state");
                    Require(entities.TryGetValue(actor.healingTarget,out var patientSaved),"healing reference");
                    var patient=patientSaved.state;var patientDef=rules.Entity(patientSaved.definition);
                    Require(patient.id!=actor.id&&patient.Alive&&patient.Complete&&patient.garrisonedIn==0&&patient.owner>=0&&patientDef.kind==EntityKind.Unit&&
                        !patientDef.waterborne&&patientDef.canReceiveHealing&&!snapshot.players[patient.owner].state.eliminated&&!snapshot.players[patient.owner].state.resigned&&snapshot.players[actor.owner].state.team==snapshot.players[patient.owner].state.team,"healing patient state");
                }
                foreach(var job in saved.queue)if(job.kind=="Train"&&rules.Entity(job.item)?.healer==true)
                {
                    var unit=rules.Entity(job.item);Require(actor.owner>=0,"healer producer owner");var owner=snapshot.players[actor.owner].state;
                    Require(actor.owner>=0&&definition.id==unit.producer&&job.count==1&&job.population==unit.population&&
                        (string.IsNullOrEmpty(unit.civilization)||owner.civilization==unit.civilization)&&
                        (unit.excludedCivilizations==null||Array.IndexOf(unit.excludedCivilizations,owner.civilization)<0)&&owner.age>=unit.age&&
                        Mathf.Abs(job.paid.food-unit.cost.food)<=.001f&&Mathf.Abs(job.paid.wood-unit.cost.wood)<=.001f&&Mathf.Abs(job.paid.coin-unit.cost.coin)<=.001f&&
                        Mathf.Abs(job.duration-unit.time)<=.001f,"healer production queue");
                }
            }
            for(int player=0;player<snapshot.players.Length;player++)foreach(var definition in rules.entities)
            {
                if(!definition.healer||definition.trainingLimit<=0)continue;
                int count=0;
                foreach(var saved in snapshot.entities)
                {
                    if(saved.state.owner!=player||!saved.state.Alive)continue;
                    if(saved.definition==definition.id)count++;
                    foreach(var job in saved.queue)if(job.kind=="Train"&&job.item==definition.id)count+=job.count;
                }
                Require(count<=definition.trainingLimit,"healer reserved production limit");
            }
            foreach(var ev in snapshot.events??Array.Empty<SimEvent>())Require(ev!=null&&ev.hasHealing==(ev.kind=="Heal"),"healing event presence");
            foreach(var ev in snapshot.events??Array.Empty<SimEvent>())if(ev.hasHealing)
            {
                var heal=ev.healing;
                Require(ev.kind=="Heal"&&heal!=null&&heal.source==ev.entity&&heal.target==ev.target&&entities.ContainsKey(heal.source)&&entities.ContainsKey(heal.target)&&
                    SnapshotCodec.Finite(heal.hpBefore)&&SnapshotCodec.Finite(heal.hpAfter)&&SnapshotCodec.Finite(heal.maxHp)&&SnapshotCodec.Finite(heal.actual)&&SnapshotCodec.Finite(heal.rate)&&
                    heal.hpBefore>0&&heal.hpAfter>heal.hpBefore&&heal.maxHp>=heal.hpAfter&&heal.actual>0&&heal.rate>0&&Mathf.Abs(heal.hpAfter-heal.hpBefore-heal.actual)<=.0001f&&Mathf.Abs(ev.amount-heal.actual)<=.0001f,"healing receipt");
            }
        }
    }
}
