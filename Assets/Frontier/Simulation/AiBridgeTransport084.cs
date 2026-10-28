using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public enum AiBridgeReview084 { None, Deferred, NoPaidBoat, NoWave, RejectedRoute, RejectedValue, Committed }
    [Serializable] public sealed class AiBridgeCandidate084
    {
        public int schema=1,ship,navigationVersion,waterVersion,pair,cursor;
        public Vector2 destination,shipOrigin,boardingShore,boardingWater,landingShore,landingWater;
        public int[] members=Array.Empty<int>();
        public Vector2[] origins=Array.Empty<Vector2>(),landingSlots=Array.Empty<Vector2>(),directIntents=Array.Empty<Vector2>(),continuationIntents=Array.Empty<Vector2>();
        public float[] direct=Array.Empty<float>(),boarding=Array.Empty<float>(),continuation=Array.Empty<float>();
        public float shipApproach,sea;
    }
    [Serializable] public sealed class AiBridgeMission084
    {
        public int schema=1,ship,phase=1,attempts,originalCount;
        public Vector2 destination,boardingShore,boardingWater,landingShore,landingWater;
        public int[] members=Array.Empty<int>(),landed=Array.Empty<int>();
        public float startedAt,retryAt,directSeconds,transportSeconds;
        public bool invalidLanding;
    }
    // Optional bridge transport only spends an already available boat's turnaround.
    // It never creates an acquisition promise or substitutes a spawn for a paid queue.
    public sealed class AiBridgeTransport084
    {
        readonly World world;readonly int player;readonly AiArmyPlan072 army;readonly AiRevolution071 revolution;readonly AiMedicalSupport076 medical;
        public AiBridgeCandidate084 Candidate {get;private set;}
        public AiBridgeMission084 Mission {get;private set;}
        public AiBridgeReview084 LastReview {get;private set;}
        public float RetryAt {get;private set;}
        public int LastRouteQueries {get;private set;}
        public AiBridgeTransport084(World world,int player,AiArmyPlan072 army,AiRevolution071 revolution,AiMedicalSupport076 medical)
        {this.world=world;this.player=player;this.army=army;this.revolution=revolution;this.medical=medical;}
        public bool Owns(int id)
        {
            if(Mission==null||Array.IndexOf(Mission.members,id)<0)return false;
            var e=world.Get(id);return e!=null&&e.Alive&&e.owner==player&&e.Military&&!e.def.waterborne&&
                (e.garrisonedIn==Mission.ship||Pending(e,Mission.ship)||Array.IndexOf(Mission.landed,id)>=0&&(medical==null||!medical.IsRecovering(id)));
        }
        public void Restore(AiBridgeCandidate084 candidate,AiBridgeMission084 mission,AiBridgeReview084 review,float retry)
        {
            AiSaveValidation.Require(Enum.IsDefined(typeof(AiBridgeReview084),review)&&AiSaveValidation.Clock(retry),"bridge review");
            if(candidate!=null)
            {
                int n=candidate.members?.Length??0;
                AiSaveValidation.Require(candidate.schema==1&&candidate.ship>0&&candidate.navigationVersion>=0&&candidate.waterVersion>=0&&candidate.pair>=0&&candidate.pair<9&&
                    n>=4&&n<=6&&candidate.cursor>=0&&candidate.cursor<=n*3+2&&candidate.origins?.Length==n&&candidate.landingSlots?.Length==n&&candidate.directIntents?.Length==n&&candidate.continuationIntents?.Length==n&&
                    candidate.direct?.Length==n&&candidate.boarding?.Length==n&&candidate.continuation?.Length==n&&
                    Point(candidate.destination)&&Point(candidate.shipOrigin)&&Point(candidate.boardingShore)&&Point(candidate.boardingWater)&&Point(candidate.landingShore)&&Point(candidate.landingWater)&&
                    AiSaveValidation.Clock(candidate.shipApproach)&&AiSaveValidation.Clock(candidate.sea),"bridge candidate");
                var seen=new HashSet<int>();for(int i=0;i<n;i++)AiSaveValidation.Require(candidate.members[i]>0&&seen.Add(candidate.members[i])&&Point(candidate.origins[i])&&Point(candidate.landingSlots[i])&&Point(candidate.directIntents[i])&&Point(candidate.continuationIntents[i])&&
                    AiSaveValidation.Clock(candidate.direct[i])&&AiSaveValidation.Clock(candidate.boarding[i])&&AiSaveValidation.Clock(candidate.continuation[i]),"bridge candidate route");
            }
            if(mission!=null)
            {
                AiSaveValidation.Require(mission.schema==1&&mission.ship>0&&mission.phase>=1&&mission.phase<=3&&mission.attempts>=0&&mission.attempts<=6&&
                    mission.members!=null&&mission.members.Length>=4&&mission.members.Length<=6&&mission.originalCount==mission.members.Length&&mission.landed!=null&&
                    Point(mission.destination)&&Point(mission.boardingShore)&&Point(mission.boardingWater)&&Point(mission.landingShore)&&Point(mission.landingWater)&&
                    AiSaveValidation.Clock(mission.startedAt)&&mission.startedAt<=world.Time+.01f&&AiSaveValidation.Clock(mission.retryAt)&&
                    AiSaveValidation.Clock(mission.directSeconds)&&AiSaveValidation.Clock(mission.transportSeconds),"bridge mission");
                var seen=new HashSet<int>();foreach(int id in mission.members)AiSaveValidation.Require(id>0&&seen.Add(id),"bridge mission member");
                seen.Clear();foreach(int id in mission.landed)AiSaveValidation.Require(Array.IndexOf(mission.members,id)>=0&&seen.Add(id),"bridge landed member");
                AiSaveValidation.Require(candidate==null,"committed bridge candidate");
            }
            Candidate=candidate==null?null:SnapshotCodec.Clone(candidate);Mission=mission==null?null:SnapshotCodec.Clone(mission);LastReview=review;RetryAt=retry;
        }
        public void ValidateOwnership()
        {
            if(Mission==null)return;
            var ship=world.Get(Mission.ship);AiSaveValidation.Require(ship!=null&&ship.owner==player&&ship.def.transportShip,"bridge ship ownership");
            foreach(int id in Mission.landed)AiSaveValidation.Require(world.Events.Exists(ev=>ev.kind=="Disembarked"&&ev.entity==id&&ev.target==Mission.ship&&ev.time>=Mission.startedAt),"bridge actual landing provenance");
            foreach(int id in Mission.members)
            {
                var e=world.Get(id);AiSaveValidation.Require(e!=null&&e.Military&&!e.def.waterborne,"bridge original passenger");
                if(!Owns(id))continue;
                AiSaveValidation.Require(Array.IndexOf(army.WaveMembers,id)>=0&&Array.IndexOf(army.GuardMembers,id)<0&&(medical==null||!medical.IsRecovering(id))&&!e.def.revolutionary,"bridge and army ownership");
                AiSaveValidation.Require(e.garrisonedIn==Mission.ship||e.garrisonedIn==0,"bridge foreign cargo");
            }
        }
        bool Point(Vector2 p)=>SnapshotCodec.Finite(p)&&p.x>=0&&p.y>=0&&p.x<=world.Navigation.Width&&p.y<=world.Navigation.Height;
        EntityState Boat(List<EntityState> own)
        {
            EntityState best=null;foreach(var e in own)
            {
                if(world.NavalTransportReason(player,e)!=""||!e.Complete||e.occupants.Count>0||e.orders.Count>0||e.hp<world.MaxHp(e)*.75f)continue;
                if(!world.TryGetTransportCargoSummary(player,e,out var load)||load.IncomingUnits!=0)continue;
                if(best==null||e.id<best.id)best=e;
            }
            return best;
        }
        List<EntityState> Members(EntityState boat,List<EntityState> enemies)
        {
            var chosen=new List<EntityState>();if(army.Phase!=AiArmyPhase072.March)return chosen;
            int slots=0;var ids=army.WaveMembers;Array.Sort(ids);var guards=new HashSet<int>(army.GuardMembers);
            foreach(int id in ids)
            {
                var e=world.Get(id);if(e==null||!e.Alive||e.owner!=player||!e.Military||e.def.waterborne||!e.Complete||e.garrisonedIn!=0||e.hold||e.isDeployed||e.deployUntil>world.Time||
                    guards.Contains(id)||medical!=null&&medical.IsRecovering(id)||e.def.revolutionary||(e.def.tags&Tags.Explorer)!=0)continue;
                if(e.orders.Count>0&&(e.orders.Peek().kind==OrderKind.Embark||e.orders.Peek().kind==OrderKind.Repair||e.orders.Peek().kind==OrderKind.DockRepair))continue;
                int cost=world.TransportSlotCost(e);if(slots+cost>Math.Min(14,boat.def.transportCapacity))continue;
                chosen.Add(e);slots+=cost;if(chosen.Count==6)break;
            }
            return chosen;
        }
        List<Vector2> Shores(Vector2 near)
        {
            var points=new List<Vector2>();foreach(var group in world.Layout.LandingSites)foreach(var p in group)Add(p);
            foreach(var group in world.Layout.DockSites)foreach(var p in group)Add(p);
            points.Sort((a,b)=>{int n=(a-near).sqrMagnitude.CompareTo((b-near).sqrMagnitude);if(n!=0)return n;n=a.x.CompareTo(b.x);return n!=0?n:a.y.CompareTo(b.y);});
            if(points.Count>3)points.RemoveRange(3,points.Count-3);return points;
            void Add(Vector2 p){foreach(var old in points)if((old-p).sqrMagnitude<.01f)return;points.Add(p);}
        }
        bool Threatened(Vector2 point,List<EntityState> enemies)
        {
            foreach(var e in enemies)if(world.IsVisible(player,e)&&AiResourceContinuity072.EconomicThreat(world,player,e)&&e.Complete&&
                (e.def.damage>0||e.def.meleeDamage>0||e.def.siegeDamage>0)&&
                (e.position-point).sqrMagnitude<Mathf.Pow(Mathf.Max(10,e.def.range+6)+e.def.radius,2))return true;
            return false;
        }
        bool SameCandidate(EntityState boat,List<EntityState> group)
        {
            var c=Candidate;if(c==null||c.ship!=boat.id||c.navigationVersion!=world.Navigation.Version||c.waterVersion!=world.WaterNavigation.Version||
                (c.destination-army.Destination).sqrMagnitude>.01f||c.members.Length!=group.Count||!world.MovementConnector084(boat,boat.position,c.shipOrigin))return false;
            for(int i=0;i<group.Count;i++)
            {
                // The exact legal connector and its full added approach cost
                // already bound drift. A 10m reset prevents a normal moving
                // cohort from finishing its budgeted multi-review survey.
                if(c.members[i]!=group[i].id||!world.MovementConnector084(group[i],group[i].position,c.origins[i],true))return false;
                var order=group[i].orders.Count>0?group[i].orders.Peek():null;
                if(order!=null&&(order.kind==OrderKind.Move||order.kind==OrderKind.AttackMove)&&(order.point-c.destination).sqrMagnitude<=18*18&&
                    (order.point-c.directIntents[i]).sqrMagnitude>.0001f)return false;
            }
            return true;
        }
        bool BeginCandidate(EntityState boat,List<EntityState> group,List<EntityState> enemies,int pair)
        {
            var boarding=Shores(group[0].position);var landing=Shores(army.Destination);
            if(boarding.Count==0||landing.Count==0||pair>=boarding.Count*landing.Count)return false;
            float radius=.4f;foreach(var e in group)radius=Mathf.Max(radius,e.def.radius);
            var c=new AiBridgeCandidate084{ship=boat.id,pair=pair,navigationVersion=world.Navigation.Version,waterVersion=world.WaterNavigation.Version,
                destination=army.Destination,shipOrigin=boat.position,members=new int[group.Count],origins=new Vector2[group.Count],directIntents=new Vector2[group.Count],continuationIntents=new Vector2[group.Count],direct=new float[group.Count],boarding=new float[group.Count],continuation=new float[group.Count]};
            float spacing=1.8f;foreach(var member in group)spacing=Mathf.Max(spacing,member.def.radius*4+.3f);
            var formation=world.Navigation.FormationSlots(c.destination,group.Count,spacing);if(formation.Count!=group.Count)return false;
            for(int i=0;i<group.Count;i++)
            {
                c.members[i]=group[i].id;c.origins[i]=group[i].position;c.continuationIntents[i]=formation[i];
                var order=group[i].orders.Count>0?group[i].orders.Peek():null;
                c.directIntents[i]=order!=null&&(order.kind==OrderKind.AttackMove||order.kind==OrderKind.Move)&&(order.point-c.destination).sqrMagnitude<=18*18?order.point:formation[i];
            }
            if(!world.TryTransportShoreGeometry084(boat,boarding[pair/landing.Count],radius,out c.boardingShore,out c.boardingWater)||
                !world.TryTransportShoreGeometry084(boat,landing[pair%landing.Count],radius,out c.landingShore,out c.landingWater)||
                (c.boardingShore-c.landingShore).sqrMagnitude<144||Threatened(c.boardingShore,enemies)||Threatened(c.landingShore,enemies)||
                !world.TryTransportLandingSlots084(boat,c.landingShore,c.landingWater,group,out c.landingSlots))return false;
            Candidate=c;return true;
        }
        void Advance(EntityState boat,List<EntityState> group,List<EntityState> enemies,int after,AiBridgeReview084 exhausted)
        {Candidate=null;for(int pair=after+1;pair<9;pair++)if(BeginCandidate(boat,group,enemies,pair))break;LastReview=Candidate==null?exhausted:AiBridgeReview084.Deferred;RetryAt=world.Time+(Candidate==null?8:1);}
        void Reject(AiBridgeReview084 reason)
        {Candidate=null;LastReview=reason;RetryAt=world.Time+8;}
        public bool Tick(List<EntityState> own,List<EntityState> enemies,bool underAttack)
        {
            LastRouteQueries=0;
            if(Mission!=null)return Operate(enemies,underAttack);
            if(world.Time<RetryAt)return false;
            if(underAttack){Reject(AiBridgeReview084.NoWave);return false;}
            var boat=Boat(own);if(boat==null){Reject(AiBridgeReview084.NoPaidBoat);return false;}
            var group=Members(boat,enemies);if(group.Count<4){Reject(AiBridgeReview084.NoWave);return false;}
            if(!SameCandidate(boat,group))Candidate=null;
            int pair=Candidate?.pair??0;
            while(Candidate==null&&pair<9){if(BeginCandidate(boat,group,enemies,pair))break;pair++;}
            if(Candidate==null){Reject(AiBridgeReview084.RejectedRoute);return false;}
            var c=Candidate;int count=group.Count,total=count*3+2;
            while(c.cursor<total&&LastRouteQueries<6)
            {
                int i=c.cursor;EntityState actor;Vector2 start,goal;
                if(i<count){actor=group[i];start=c.origins[i];if(!world.TryMoveEndpoint084(actor,start,c.directIntents[i],out goal,true)){Advance(boat,group,enemies,c.pair,AiBridgeReview084.RejectedRoute);return false;}}
                else if(i<count*2){i-=count;actor=group[i];start=c.origins[i];goal=c.boardingShore;}
                else if(i<count*3){i-=count*2;actor=group[i];start=c.landingShore;if(!world.TryMoveEndpoint084(actor,start,c.continuationIntents[i],out goal)){Advance(boat,group,enemies,c.pair,AiBridgeReview084.RejectedRoute);return false;}}
                else {actor=boat;start=c.cursor==count*3?c.shipOrigin:c.boardingWater;goal=c.cursor==count*3?c.boardingWater:c.landingWater;}
                LastRouteQueries++;
                if(!world.TryMovementRoute084(actor,start,goal,out float length,c.cursor<count*2))
                {Advance(boat,group,enemies,c.pair,AiBridgeReview084.RejectedRoute);return false;}
                if(c.cursor<count)c.direct[i]=length;
                else if(c.cursor<count*2)c.boarding[i]=length;
                else if(c.cursor<count*3)c.continuation[i]=length;
                else if(c.cursor==count*3)c.shipApproach=length;else c.sea=length;
                c.cursor++;
            }
            if(c.cursor<total||LastRouteQueries>0){LastReview=AiBridgeReview084.Deferred;return false;}
            // Final commitment gets this review's exact native direct route from
            // every current body, rather than assuming a stale path is a lower
            // bound after bridge direction or a soldier's position has changed.
            for(int i=0;i<count;i++)
            {
                LastRouteQueries++;
                if(!world.TryMoveEndpoint084(group[i],group[i].position,c.directIntents[i],out var actualGoal,true)||!world.TryMovementRoute084(group[i],group[i].position,actualGoal,out c.direct[i],true))
                {Advance(boat,group,enemies,c.pair,AiBridgeReview084.RejectedRoute);return false;}
            }
            float direct=0,boardingTime=0,continuation=0;float shipSpeed=Mathf.Max(.1f,world.Speed(boat));
            for(int i=0;i<count;i++)
            {
                float speed=Mathf.Max(.1f,world.Speed(group[i])),connector=Vector2.Distance(group[i].position,c.origins[i]);
                // A validated legal connector bounds the current boarding
                // approach while the full survey spans several reviews.
                direct=Mathf.Max(direct,c.direct[i]/speed);
                boardingTime=Mathf.Max(boardingTime,(c.boarding[i]+connector)/speed);
                continuation=Mathf.Max(continuation,(c.continuation[i]+6)/speed);
            }
            float approach=(c.shipApproach+Vector2.Distance(boat.position,c.shipOrigin))/shipSpeed;
            // Normal serialized boarding, deployment recovery and local queues
            // receive a fixed conservative allowance rather than invented delay.
            float transport=Mathf.Max(boardingTime,approach)+c.sea/shipSpeed+continuation+12+count*2;
            if(direct-transport<20||transport>direct*.8f){Advance(boat,group,enemies,c.pair,AiBridgeReview084.RejectedValue);return false;}
            if(Threatened(c.boardingShore,enemies)||Threatened(c.landingShore,enemies)){Reject(AiBridgeReview084.RejectedRoute);return false;}
            var result=Send(new GameCommand{kind=CommandKind.Embark,actors=c.members,target=boat.id,point=c.boardingShore});
            if(!result.accepted){Reject(AiBridgeReview084.RejectedRoute);return false;}
            // Public authority must have retained the evaluated exact shore.
            var order=boat.orders.Count>0?boat.orders.Peek():null;
            if(order==null||order.kind!=OrderKind.TransportBerth||(order.point-c.boardingShore).sqrMagnitude>.01f||(order.resolvedPoint-c.boardingWater).sqrMagnitude>.01f)
            {Send(new GameCommand{kind=CommandKind.Order,actors=new[]{boat.id},order=OrderKind.Stop});Reject(AiBridgeReview084.RejectedRoute);return false;}
            Mission=new AiBridgeMission084{ship=boat.id,destination=c.destination,boardingShore=c.boardingShore,boardingWater=c.boardingWater,landingShore=c.landingShore,landingWater=c.landingWater,
                members=(int[])c.members.Clone(),originalCount=count,startedAt=world.Time,directSeconds=direct,transportSeconds=transport};
            Candidate=null;LastReview=AiBridgeReview084.Committed;
            world.Emit("AIBridgeTransportCommitted",player,boat.id,"Existing paid boat saves a complete public land route",direct,0,Mission.destination);return true;
        }
        bool Pending(EntityState e,int ship)=>e!=null&&e.Alive&&e.owner==player&&e.garrisonedIn==0&&e.orders.Count>0&&e.orders.Peek().kind==OrderKind.Embark&&e.orders.Peek().target==ship;
        void CancelPending(AiBridgeMission084 m)
        {foreach(int id in m.members){var e=world.Get(id);if(Pending(e,m.ship))Send(new GameCommand{kind=CommandKind.Order,actors=new[]{id},order=OrderKind.Stop});}}
        void End(bool completed)
        {
            var m=Mission;CancelPending(m);int survivors=0;var released=new List<int>();
            foreach(int id in m.landed)
            {
                var e=world.Get(id);if(e==null||!e.Alive||e.owner!=player||e.garrisonedIn!=0||medical!=null&&medical.IsRecovering(id))continue;survivors++;
                released.Add(id);
            }
            if(completed&&released.Count>0)
            {
                float spacing=1.8f;foreach(int id in released)spacing=Mathf.Max(spacing,world.Get(id).def.radius*4+.3f);
                var endpoints=world.Navigation.FormationSlots(m.destination,released.Count,spacing);
                if(endpoints.Count!=released.Count)completed=false;
                else for(int i=0;i<released.Count;i++)
                {
                    var passenger=world.Get(released[i]);LastRouteQueries++;
                    if(!world.TryMoveEndpoint084(passenger,passenger.position,endpoints[i],out var goal)||!world.TryMovementRoute084(passenger,passenger.position,goal,out _))
                    {completed=false;break;}
                }
            }
            if(released.Count>0)Send(new GameCommand{kind=CommandKind.Order,actors=released.ToArray(),order=OrderKind.AttackMove,point=completed?m.destination:army.Home});
            world.Emit(completed?"AIBridgeTransportCompleted":"AIBridgeTransportReleased",player,m.ship,completed?"Original wave actually landed and resumed its public objective":"Original wave transport ended or returned",survivors,m.originalCount,completed?m.landingShore:m.boardingShore);
            Mission=null;RetryAt=world.Time+15;
        }
        bool Operate(List<EntityState> enemies,bool underAttack)
        {
            var m=Mission;var ship=world.Get(m.ship);
            // Successful release needs actual per-passenger landing evidence;
            // zero cargo alone also describes sinking, loss or cancellation.
            var landed=new List<int>(m.landed);
            foreach(var ev in world.Events)if(ev.kind=="Disembarked"&&ev.target==m.ship&&ev.time>=m.startedAt&&Array.IndexOf(m.members,ev.entity)>=0)
            {
                if(!landed.Contains(ev.entity))landed.Add(ev.entity);var passenger=world.Get(ev.entity);
                // Native retries can resolve and unload a different shore in a
                // single world tick between AI reviews. Audit actual release
                // coordinates, not merely the last observed ship order.
                if(m.phase==2&&(passenger==null||(ev.position-m.landingShore).sqrMagnitude>36.0001f||
                    !world.Navigation.IsWalkable(ev.position,passenger.def.radius)||!world.Navigation.ClearLine(m.landingShore,ev.position,passenger.def.radius)))m.invalidLanding=true;
            }
            m.landed=landed.ToArray();
            if(world.NavalTransportReason(player,ship)!=""){End(false);return true;}
            bool anyAboard=false,anyPending=false;
            foreach(int id in m.members){var e=world.Get(id);if(e!=null&&e.Alive&&e.owner==player&&e.garrisonedIn==ship.id)anyAboard=true;if(Pending(e,m.ship))anyPending=true;}
            if(m.phase>1&&!anyAboard&&!anyPending){End(m.phase==2&&!m.invalidLanding&&m.landed.Length>0);return true;}
            bool danger=underAttack||ship.hp<world.MaxHp(ship)*.45f||Threatened(m.landingShore,enemies)||m.phase==1&&Threatened(m.boardingShore,enemies);
            if(danger&&m.phase!=3){m.phase=3;m.attempts=0;m.retryAt=0;CancelPending(m);if(ship.orders.Count>0)Send(new GameCommand{kind=CommandKind.Order,actors=new[]{ship.id},order=OrderKind.Stop});}
            if(m.phase==1)
            {
                bool exact=ship.orders.Count>0&&ship.orders.Peek().kind==OrderKind.TransportBerth&&(ship.orders.Peek().point-m.boardingShore).sqrMagnitude<.01f&&
                    (ship.orders.Peek().resolvedPoint-m.boardingWater).sqrMagnitude<.01f;
                if((anyPending||!anyAboard)&&world.Time-m.startedAt<60&&exact)return true;
                CancelPending(m);
                if(!anyAboard){End(false);return true;}
                m.phase=2;m.retryAt=0;
            }
            if(m.phase==3&&!anyAboard){End(false);return true;}
            // An observed attack at the original bank cannot authorize releasing
            // real passengers into that weapon. Keep actual cargo aboard and
            // retry the same saved shore when the visible danger is gone.
            if(m.phase==3&&Threatened(m.boardingShore,enemies))
            {
                if(ship.orders.Count>0&&ship.orders.Peek().kind==OrderKind.Disembark)
                    Send(new GameCommand{kind=CommandKind.Order,actors=new[]{ship.id},order=OrderKind.Stop});
                m.retryAt=Mathf.Max(m.retryAt,world.Time+3);return true;
            }
            Vector2 shore=m.phase==3?m.boardingShore:m.landingShore,water=m.phase==3?m.boardingWater:m.landingWater;
            if(ship.orders.Count>0&&ship.orders.Peek().kind==OrderKind.Disembark)
            {
                var order=ship.orders.Peek();bool exact=(order.point-shore).sqrMagnitude<.01f&&(order.resolvedPoint-water).sqrMagnitude<.01f;
                bool blocked=order.destinationCursor>=6&&(ship.status.StartsWith("Landing occupied",StringComparison.Ordinal)||ship.status.StartsWith("Shore blocked",StringComparison.Ordinal));
                if(exact&&!blocked)return true;
                Send(new GameCommand{kind=CommandKind.Order,actors=new[]{ship.id},order=OrderKind.Stop});m.attempts=Math.Min(6,m.attempts+1);m.retryAt=world.Time+3;
                if(m.phase==2){m.phase=3;m.attempts=0;}return true;
            }
            if(world.Time<m.retryAt)return true;
            if(m.attempts>=6){m.retryAt=world.Time+30;m.attempts=0;return true;}
            var result=Send(new GameCommand{kind=CommandKind.Disembark,actors=new[]{ship.id},point=shore});m.attempts++;
            if(!result.accepted){m.retryAt=world.Time+5;if(m.phase==2){m.phase=3;m.attempts=0;}return true;}
            var resolved=ship.orders.Peek();
            if((resolved.point-shore).sqrMagnitude>.01f||(resolved.resolvedPoint-water).sqrMagnitude>.01f)
            {Send(new GameCommand{kind=CommandKind.Order,actors=new[]{ship.id},order=OrderKind.Stop});m.retryAt=world.Time+5;if(m.phase==2){m.phase=3;m.attempts=0;}}
            return true;
        }
        CommandResult Send(GameCommand c){c.player=player;return world.Issue(c);}
    }
}
