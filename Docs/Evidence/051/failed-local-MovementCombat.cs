using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public partial class World
    {
        private void TickMovementCombat(float dt)
        {
            LastNavigationMs=0;int count=Entities.Count;
            for(int i=0;i<count;i++)
            {
                var e=Entities[i];if(!e.Alive||!e.Complete||e.garrisonedIn!=0||e.def.kind==EntityKind.Resource)continue;
                if(Guardian(e)&&Vector2.Distance(e.position,e.origin)>Rules.landmarks.guardLeash)
                {e.attackTarget=0;e.fireAt=-1;e.orders.Clear();MoveTowardsTarget(e,e.origin,dt);continue;}
                if(e.fireAt>=0)
                {
                    if(Time>=e.fireAt){Fire(e);e.fireAt=-1;}
                    else {e.status="Preparing attack";continue;}
                }
                UnitOrder order=e.orders.Count>0?e.orders.Peek():null;
                if(order!=null&&(order.kind==OrderKind.Gather||order.kind==OrderKind.Build||order.kind==OrderKind.Repair||order.kind==OrderKind.Garrison||order.kind==OrderKind.Claim))
                {
                    var target=Get(order.target);if(target==null||!target.Alive){if(order.kind!=OrderKind.Gather){e.orders.Dequeue();e.status="Target lost";}continue;}
                    if(order.kind==OrderKind.Garrison&&EdgeDistance(e,target)<=1.5f)
                    {
                        if(target.occupants.Count<target.def.garrison){e.garrisonedIn=target.id;e.orders.Clear();e.path.Clear();target.occupants.Add(e.id);e.status="Garrisoned";Emit("Garrison",e.owner,e.id,"Entered "+target.def.name,0,target.id);}
                        else {e.status="Garrison full";e.orders.Dequeue();}continue;
                    }
                    if(EdgeDistance(e,target)>1.35f)MoveTowardsTarget(e,WorkPoint(target,e),dt);
                    else {e.path.Clear();e.facing=(target.position-e.position).normalized;}
                    continue;
                }
                if(order!=null&&order.kind==OrderKind.Move)
                {
                    if(!ResolveMoveDestination(e,order,out var destination)){e.path.Clear();e.status="Destination occupied; waiting";continue;}
                    MoveTowardsTarget(e,destination,dt);
                    if((e.position-destination).sqrMagnitude<.3f*.3f){e.orders.Dequeue();e.path.Clear();e.status="Idle";}
                    continue;
                }
                EntityState enemy=null;
                if(order!=null&&order.kind==OrderKind.Attack)
                {
                    enemy=Get(order.target);
                    if(enemy==null||!enemy.Alive||!Observes(e,enemy)){e.orders.Dequeue();e.attackTarget=0;e.status="Target lost";continue;}
                }
                else
                {
                    var remembered=Get(e.attackTarget);
                    if(remembered!=null&&remembered.Alive&&remembered.owner!=e.owner&&Observes(e,remembered)&&EdgeDistance(e,remembered)<e.def.vision)enemy=remembered;
                    if(enemy==null&&Time>=e.scanAt&&HasFirepower(e))
                    {e.scanAt=Time+.55f+(e.id%5)*.025f;enemy=FindEnemy(e);e.attackTarget=enemy?.id??0;}
                }
                if(enemy!=null&&HasFirepower(e))
                {
                    float distance=EdgeDistance(e,enemy);bool melee=(e.forceMelee||distance<1.5f)&&e.def.meleeDamage>0;
                    float range=melee?1.4f:e.def.range;
                    if(distance>=e.def.minRange&&distance<=range+.1f)
                    {
                        e.path.Clear();e.facing=(enemy.position-e.position).normalized;
                        if(e.def.deploy&&!e.isDeployed)
                        {
                            if(e.deployUntil<=0)e.deployUntil=Time+2;
                            if(Time<e.deployUntil){e.status="Deploying cannon";continue;}e.isDeployed=true;e.deployUntil=0;
                        }
                        TryAttack(e,enemy,melee);continue;
                    }
                    if(e.def.kind==EntityKind.Unit&&!e.hold&&(!e.Worker||order!=null&&order.kind==OrderKind.Attack))
                    {
                        Vector2 goal=WorkPoint(enemy,e);
                        if(distance<e.def.minRange)goal=e.position+(e.position-enemy.position).normalized*(e.def.minRange+2);
                        else if(range>2)goal=enemy.position+(e.position-enemy.position).normalized*(range*.8f+enemy.def.radius);
                        MoveTowardsTarget(e,goal,dt);continue;
                    }
                }
                if(order!=null&&order.kind==OrderKind.AttackMove)
                {
                    if(!ResolveMoveDestination(e,order,out var destination)){e.path.Clear();e.status="Destination occupied; waiting";continue;}
                    MoveTowardsTarget(e,destination,dt);
                    if((e.position-destination).sqrMagnitude<1.5f){e.orders.Dequeue();e.path.Clear();}
                }
                else if(e.orders.Count==0)e.status=e.hold?"Holding":Time<e.attackReadyAt?"Reloading":"Idle";
            }
            
            for(int i=Projectiles.Count-1;i>=0;i--)
            {
                var p=Projectiles[i];if(Time<p.hitAt)continue;var target=Get(p.target);
                if(target!=null&&target.Alive&&target.garrisonedIn==0)
                {
                    var impact=target.position;ApplyDamage(target,p.damage,p.kind,p.owner,p.source);
                    if(p.splash>0)
                    {
                        var victims=new List<EntityState>();foreach(var candidate in Nearby(impact,p.splash))if(candidate.id!=target.id&&(candidate.owner>=0||Guardian(candidate))&&candidate.owner!=p.owner&&candidate.def.kind!=EntityKind.Resource)victims.Add(candidate);
                        foreach(var other in victims)
                        {
                            var shooter=Get(p.source);DamageKind splashKind=p.baseKind;
                            float raw=p.baseDamage;if(other.def.kind==EntityKind.Building&&p.siegeDamage>0){raw=p.siegeDamage;splashKind=DamageKind.Siege;}
                            float bonus=1;if(shooter!=null)foreach(var b in shooter.def.bonuses)if((b.tags&other.def.tags)!=0)bonus=Mathf.Max(bonus,b.multiplier);
                            ApplyDamage(other,raw*bonus*.5f,splashKind,p.owner,p.source);
                        }
                    }
                    Emit("Impact",p.owner,p.source,"Projectile impact",p.damage,p.target,impact);
                }
                Projectiles.RemoveAt(i);
            }
        }
        private EntityState FindEnemy(EntityState e)
        {
            EntityState result=null;float best=float.MaxValue;
            float vision=e.Worker?2:e.hold||e.def.kind==EntityKind.Building?e.def.range+5:e.def.vision;
            foreach(var target in Nearby(e.position,vision))
            {
                if((target.owner<0&&!Guardian(target))||target.owner==e.owner||target.def.kind==EntityKind.Resource||!Observes(e,target))continue;
                if(Guardian(e)&&(target.def.kind!=EntityKind.Unit||Vector2.Distance(target.position,e.origin)>Rules.landmarks.guardLeash))continue;
                if(e.def.id=="mortar"&&target.def.kind!=EntityKind.Building)continue;
                float distance=EdgeDistance(e,target);
                if(Guardian(target)&&distance>4&&(e.orders.Count==0||e.orders.Peek().kind!=OrderKind.AttackMove))continue;
                if(e.hold&&distance>e.def.range)continue;
                float score=distance+(target.def.kind==EntityKind.Building?8:0);if(score<best){best=score;result=target;}
            }
            return result;
        }
        private void MoveTowardsTarget(EntityState e,Vector2 goal,float dt)
        {
            long start=System.Diagnostics.Stopwatch.GetTimestamp();MoveCore(e,goal,dt);LastNavigationMs+=(float)((System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency);
        }
        private void MoveCore(EntityState e,Vector2 goal,float dt)
        {
            if(e.def.kind!=EntityKind.Unit)return;
            e.isDeployed=false;e.deployUntil=0;
            Vector2 requestedGoal=goal;
            if((e.progressGoal-requestedGoal).sqrMagnitude>4){e.progressGoal=requestedGoal;e.progressDistance=float.MaxValue;e.progressAt=Time;e.detourUntil=0;}
            if(e.detourUntil>Time&&(e.position-e.detour).sqrMagnitude>1)goal=e.detour;
            else
            {
                e.detourUntil=0;float remaining=(requestedGoal-e.position).magnitude;
                // Measure progress along the planned route. Euclidean distance may grow
                // while a unit correctly approaches a gate around a wall.
                if(e.pathIndex<e.path.Count&&(e.pathGoal-requestedGoal).sqrMagnitude<4)
                {
                    remaining=Vector2.Distance(e.position,e.path[e.pathIndex]);
                    for(int n=e.pathIndex+1;n<e.path.Count;n++)remaining+=Vector2.Distance(e.path[n-1],e.path[n]);
                }
                else {e.progressDistance=float.MaxValue;e.progressAt=Time;}
                if(remaining<e.progressDistance-.2f){e.progressDistance=remaining;e.progressAt=Time;}
                if(remaining>1.2f&&Time-e.progressAt>2)
                {
                    if(TryLocalRecovery(e,requestedGoal,out var chosen))
                    {e.detour=chosen;e.detourUntil=Time+6;goal=chosen;e.pathVersion=-1;}
                    e.progressAt=Time;e.progressDistance=float.MaxValue;
                }
            }
            if((e.position-goal).sqrMagnitude<.2f*.2f){e.path.Clear();return;}
            bool routeOccluded=e.pathIndex<e.path.Count&&!Navigation.ClearLine(e.position,e.path[e.pathIndex],e.def.radius);
            if(e.pathVersion!=Navigation.Version||(e.pathGoal-goal).sqrMagnitude>4||routeOccluded||e.pathIndex>=e.path.Count&&Time>=e.pathRetryAt)
            {
                e.path.Clear();e.path.AddRange(Navigation.FindPath(e.position,goal,e.def.radius));e.pathIndex=e.path.Count>1?1:0;
                e.pathGoal=goal;e.pathVersion=Navigation.Version;e.pathRetryAt=Time+1.2f;
                // A new route has a different remaining length. Comparing it to
                // the earlier straight-line baseline falsely triggers recovery.
                e.progressDistance=float.MaxValue;e.progressAt=Time;
            }
            if(e.pathIndex>=e.path.Count){e.status="Route blocked; retrying";return;}
            Vector2 waypoint=e.path[e.pathIndex],to=waypoint-e.position;
            // Avoidance can move an actor past a corner without touching its exact
            // waypoint. Take the next visible leg instead of orbiting an occupied corner.
            while(e.pathIndex<e.path.Count-1&&Navigation.ClearLine(e.position,e.path[e.pathIndex+1],e.def.radius)){e.pathIndex++;waypoint=e.path[e.pathIndex];to=waypoint-e.position;}
            Vector2 velocity=ChooseVelocity(e,to,goal,dt);float maxStep=Speed(e)*dt;
            Vector2 move=Vector2.ClampMagnitude(velocity*dt,maxStep),candidate=e.position+move;
            bool walked=false;
            if(Navigation.IsWalkable(candidate,e.def.radius)){e.position=candidate;walked=true;}
            else
            {
                Vector2 xOnly=e.position+new Vector2(move.x,0),zOnly=e.position+new Vector2(0,move.y);
                if(Navigation.IsWalkable(xOnly,e.def.radius)&&(xOnly-e.position).sqrMagnitude>.0001f){e.position=xOnly;walked=true;}
                else if(Navigation.IsWalkable(zOnly,e.def.radius)){e.position=zOnly;walked=true;}
            }
            if(walked&&(e.position-e.previousPosition).sqrMagnitude>.0001f){e.facing=(e.position-e.previousPosition).normalized;e.stuckFor=0;e.status="Moving";}
            else
            {
                e.stuckFor+=dt;if(e.stuckFor>1.5f){e.pathVersion=-1;e.pathRetryAt=Time;e.stuckFor=0;}
            }
        }
        private readonly List<EntityState> movementNeighbors=new List<EntityState>(24);
        private static readonly float[] steeringAngles={0,30,-30,60,-60,90,-90,120,-120,180};
        private Vector2 ChooseVelocity(EntityState e,Vector2 to,Vector2 goal,float dt)
        {
            float speed=Mathf.Min(Speed(e),to.magnitude/Mathf.Max(.001f,dt));Vector2 forward=to.normalized;
            movementNeighbors.Clear();foreach(var other in Nearby(e.position,3.5f))if(other.id!=e.id&&other.def.kind==EntityKind.Unit&&other.garrisonedIn==0)movementNeighbors.Add(other);
            if(movementNeighbors.Count==0)return forward*speed;
            Vector2 best=Vector2.zero;float bestScore=float.MaxValue;float distance=to.magnitude;Vector2 steeringGoal=e.position+to;
            foreach(float angle in steeringAngles)
            {
                float radians=angle*Mathf.Deg2Rad;Vector2 direction=new Vector2(forward.x*Mathf.Cos(radians)-forward.y*Mathf.Sin(radians),forward.x*Mathf.Sin(radians)+forward.y*Mathf.Cos(radians));
                Vector2 velocity=direction*speed,step=e.position+velocity*dt;
                if(!Navigation.IsWalkable(step,e.def.radius))continue;
                // Do not score a projection beyond a nearby corner: doing so made
                // units orbit the waypoint instead of completing the safe turn.
                Vector2 predicted=e.position+velocity*Mathf.Min(.45f,distance/Mathf.Max(.001f,speed));
                float score=(steeringGoal-predicted).magnitude-distance+Mathf.Abs(angle)*.0008f;
                foreach(var other in movementNeighbors)
                {
                    float clearance=e.def.radius+other.def.radius+.09f;
                    float overlap=Mathf.Max(0,clearance-(predicted-other.position).magnitude);
                    float immediate=Mathf.Max(0,clearance-(step-other.position).magnitude);
                    score+=overlap*overlap*18+immediate*immediate*8;
                }
                // Stable tie break avoids two symmetric alternatives switching every tick.
                score+=(angle<0?(e.id%2==0?.0001f:0):(e.id%2!=0?.0001f:0));
                if(score<bestScore){bestScore=score;best=velocity;}
            }
            return best;
        }
        private bool HasFirepower(EntityState e)=>e.def.damage>0&&(e.def.garrisonAttackLimit<=0||GarrisonFirepowerCount(e)>0);
        private void TryAttack(EntityState e,EntityState target,bool melee)
        {
            if(Time<e.attackReadyAt){e.status="Reloading";return;}
            e.shotMelee=melee;e.attackTarget=target.id;e.fireAt=Time+Mathf.Max(.1f,e.def.windup);
            e.attackReadyAt=Time+Mathf.Max(e.def.period,e.def.windup+.1f);e.status="Preparing attack";
            Emit("AttackPrepared",e.owner,e.id,"Attack prepared",0,target.id);
        }
        private void Fire(EntityState e)
        {
            var target=Get(e.attackTarget);if(target==null||!target.Alive||!Observes(e,target)||target.garrisonedIn!=0||!HasFirepower(e))return;
            bool melee=e.shotMelee&&e.def.meleeDamage>0;float range=melee?1.5f:e.def.range;
            float distance=EdgeDistance(e,target);
            // A target can cross the inner boundary while the weapon is preparing.
            // Abort only this pending shot; the already committed reload stays spent.
            if(distance<e.def.minRange){Emit("AttackCancelled",e.owner,e.id,"Target inside minimum range",0,target.id);return;}
            if(distance>range+1)return;
            DamageKind kind=melee?DamageKind.Melee:e.def.damageKind;
            float factor=Attack(e)/Mathf.Max(1,e.forceMelee?e.def.meleeDamage:e.def.damage);
            float damage=(melee?e.def.meleeDamage:e.def.damage)*factor;float baseDamage=damage;DamageKind baseKind=kind;
            if(target.def.kind==EntityKind.Building&&e.def.siegeDamage>0){damage=e.def.siegeDamage*(e.def.damage>0?Attack(e)/Mathf.Max(1,e.forceMelee?e.def.meleeDamage:e.def.damage):1);kind=DamageKind.Siege;}
            float multiplier=1;foreach(var bonus in e.def.bonuses)if((bonus.tags&target.def.tags)!=0)multiplier=Mathf.Max(multiplier,bonus.multiplier);
            damage*=multiplier;
            if(melee||e.def.projectileSpeed<=0)ApplyDamage(target,damage,kind,e.owner,e.id);
            else Projectiles.Add(new ProjectileState{source=e.id,target=target.id,owner=e.owner,start=e.position,end=target.position,firedAt=Time,hitAt=Time+Mathf.Max(.08f,Vector2.Distance(e.position,target.position)/e.def.projectileSpeed),damage=damage,baseDamage=baseDamage,siegeDamage=e.def.siegeDamage*factor,baseKind=baseKind,splash=e.def.splash,kind=kind});
            Emit("Fire",e.owner,e.id,kind.ToString(),damage,target.id);e.status="Reloading";
        }
        public void ApplyDamage(EntityState target,float raw,DamageKind kind,int owner,int source=0)
        {
            if(target==null||!target.Alive||target.garrisonedIn!=0)return;
            float resistance=kind==DamageKind.Melee?target.def.meleeResist:kind==DamageKind.Ranged?target.def.rangedResist:target.def.siegeResist;
            float damage=Mathf.Max(0,raw)*(1-Mathf.Clamp01(resistance));
            Emit("Damage",owner,source,kind.ToString(),damage,target.id,target.position);
            if(damage>=target.hp){Remove(target,owner);return;}target.hp-=damage;
            if(target.Worker&&target.orders.Count>0&&target.orders.Peek().kind==OrderKind.Gather)
            {
                EntityState shelter=null;float best=22*22;foreach(var building in Entities)if(building.Alive&&building.Complete&&building.owner==target.owner&&building.def.garrison>building.occupants.Count){float distance=(building.position-target.position).sqrMagnitude;if(distance<best){best=distance;shelter=building;}}
                if(shelter!=null){target.orders.Clear();target.path.Clear();target.orders.Enqueue(new UnitOrder(OrderKind.Garrison,shelter.position,shelter.id));}
            }
        }
    }
}
