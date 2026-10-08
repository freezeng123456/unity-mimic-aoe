using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public partial class World
    {
        const float ScoutReviewSeconds075=1.2f;
        public bool CanScout(EntityState actor)=>actor!=null&&actor.Alive&&actor.Complete&&actor.owner>=0&&
            IsPlayerActive(actor.owner)&&actor.garrisonedIn==0&&actor.def.kind==EntityKind.Unit&&!actor.def.waterborne&&
            (actor.Scout||(actor.def.tags&Tags.Explorer)!=0);

        bool HasOtherSurvivingUnit075(EntityState actor)
        {
            foreach(var other in Entities)if(other.id!=actor.id&&other.owner==actor.owner&&other.Alive&&other.def.kind==EntityKind.Unit)return true;
            return false;
        }
        public string StealthReason(int player,EntityState actor)
        {
            if(actor==null||actor.owner!=player||!CanScout(actor)||!actor.Scout||!actor.def.canStealth)return "Select your active stealth scout";
            if(actor.stealth)return "";
            if(!HasOtherSurvivingUnit075(actor))return "Your last surviving unit cannot hide";
            if(Time<actor.stealthReadyAt)return "Stealth is recovering";
            var order=actor.orders.Count>0?actor.orders.Peek():null;
            if(actor.fireAt>=0||order!=null&&(order.kind==OrderKind.Attack||order.kind==OrderKind.AttackMove||order.kind==OrderKind.Build||order.kind==OrderKind.Claim))
                return "Finish the attack or construction before hiding";
            return "";
        }
        CommandResult ToggleStealth075(GameCommand command)
        {
            int changed=0;string reason="Select a stealth scout";
            var seen=new HashSet<int>();foreach(int id in command.actors)
            {
                if(!seen.Add(id))continue;
                var actor=Get(id);reason=StealthReason(command.player,actor);if(reason!="")continue;
                if(actor.stealth)BreakStealth075(actor);
                else {actor.stealth=true;actor.attackTarget=0;actor.fireAt=-1;}
                Emit("Stealth",actor.owner,actor.id,actor.stealth?"Scout concealed":"Scout visible",actor.stealth?1:0);changed++;
            }
            return changed>0?CommandResult.Ok("Scout stealth changed"):CommandResult.Fail(reason);
        }
        void BreakStealth075(EntityState actor)
        {
            if(!actor.stealth)return;actor.stealth=false;actor.stealthReadyAt=Mathf.Max(actor.stealthReadyAt,Time+3);
        }
        void MaintainStealth075()
        {
            foreach(var actor in Entities)if(actor.stealth&&(!CanScout(actor)||!actor.Scout||!actor.def.canStealth||!HasOtherSurvivingUnit075(actor)))BreakStealth075(actor);
        }
        bool CanObserveStealth075(int player,EntityState target)
        {
            if(!target.stealth||target.owner==player||AreAllies(player,target.owner)||Time<target.revealedUntil)return true;
            foreach(var detector in Entities)
            {
                if(!detector.Alive||!detector.Complete||detector.garrisonedIn!=0||detector.owner<0||!IsPlayerActive(detector.owner)||
                    !AreAllies(player,detector.owner)||detector.def.detectionRange<=0||
                    detector.def.kind!=EntityKind.Unit&&detector.def.kind!=EntityKind.Building||Fog.At(player,detector.position)!=VisibilityState.Visible)continue;
                float range=Mathf.Min(8,detector.def.detectionRange);
                if((detector.position-target.position).sqrMagnitude<=range*range)return true;
            }
            return false;
        }
        // Neutral guardians use their own real vision, not a nonexistent player fog.
        bool Observes075(EntityState observer,EntityState target)
        {
            if(!Observes(observer,target))return false;
            return observer.owner>=0||!target.stealth||Time<target.revealedUntil||
                observer.def.detectionRange>0&&(observer.position-target.position).sqrMagnitude<=Mathf.Pow(Mathf.Min(8,observer.def.detectionRange),2);
        }
        void RevealHitScout075(EntityState target,float damage)
        {
            if(target.stealth&&damage>0)target.revealedUntil=Mathf.Max(target.revealedUntil,Time+6);
        }

        readonly struct ScoutThreat075
        {
            public readonly Vector2 point;public readonly float reach;
            public ScoutThreat075(EntityState entity){point=entity.position;reach=Mathf.Max(8,entity.def.range+4);}
        }
        List<ScoutThreat075> VisibleScoutThreats075(EntityState actor)
        {
            var threats=new List<ScoutThreat075>();
            foreach(var enemy in Entities)if(enemy.Alive&&enemy.Complete&&enemy.garrisonedIn==0&&(enemy.owner<0||IsPlayerActive(enemy.owner))&&
                enemy.def.kind!=EntityKind.Resource&&IsHostileTarget(actor.owner,enemy)&&IsVisible(actor.owner,enemy)&&HasFirepower(enemy))
                threats.Add(new ScoutThreat075(enemy));
            return threats;
        }
        static float ScoutClearance075(Vector2 point,List<ScoutThreat075> threats)
        {
            float clearance=float.MaxValue;
            foreach(var threat in threats)clearance=Mathf.Min(clearance,Vector2.Distance(point,threat.point)-threat.reach);
            return clearance;
        }
        static bool ScoutSegmentSafe075(Vector2 from,Vector2 to,List<ScoutThreat075> threats)
        {
            Vector2 delta=to-from;float length=delta.sqrMagnitude;
            foreach(var threat in threats)
            {
                float t=length>0?Mathf.Clamp01(Vector2.Dot(threat.point-from,delta)/length):0;
                if((threat.point-(from+delta*t)).sqrMagnitude<threat.reach*threat.reach)return false;
            }
            return true;
        }
        bool ScoutRouteSafe075(EntityState actor,Vector2 goal,List<ScoutThreat075> threats)
        {
            if(!Navigation.IsWalkable(goal,actor.def.radius)||ScoutClearance075(actor.position,threats)<0||ScoutClearance075(goal,threats)<0)return false;
            if(Navigation.ClearLine(actor.position,goal,actor.def.radius))return ScoutSegmentSafe075(actor.position,goal,threats);
            var path=Navigation.FindPath(actor.position,goal,actor.def.radius);
            if(path.Count<2||(path[0]-actor.position).sqrMagnitude>.001f||(path[path.Count-1]-goal).sqrMagnitude>.001f)return false;
            for(int i=1;i<path.Count;i++)if(!ScoutSegmentSafe075(path[i-1],path[i],threats))return false;
            return true;
        }
        bool CurrentScoutRouteSafe075(EntityState actor,UnitOrder order,List<ScoutThreat075> threats)
        {
            if(!ScoutRouteSafe075(actor,order.resolvedPoint,threats))return false;
            Vector2 previous=actor.position;
            for(int i=actor.pathIndex;i<actor.path.Count;i++)
            {if(!ScoutSegmentSafe075(previous,actor.path[i],threats))return false;previous=actor.path[i];}
            return actor.detourUntil<=Time||ScoutRouteSafe075(actor,actor.detour,threats);
        }
        bool TryScoutRetreat075(EntityState actor,List<ScoutThreat075> threats,out Vector2 goal)
        {
            goal=actor.position;float current=ScoutClearance075(actor.position,threats),best=current+.5f;
            // An emergency escape must be a genuinely clear local leg. It may
            // begin inside weapon reach; every threat distance must not shrink.
            for(int ring=1;ring<=2;ring++)for(int direction=0;direction<16;direction++)
            {
                float angle=direction*Mathf.PI/8;var point=actor.position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(ring*7);
                if(Fog.At(actor.owner,point)==VisibilityState.Unexplored||!Navigation.IsWalkable(point,actor.def.radius)||!Navigation.ClearLine(actor.position,point,actor.def.radius))continue;
                bool away=true;foreach(var threat in threats)if(Vector2.Dot(actor.position-threat.point,point-actor.position)<-.001f){away=false;break;}
                float score=ScoutClearance075(point,threats);if(away&&score>best){best=score;goal=point;}
            }
            return (goal-actor.position).sqrMagnitude>.01f;
        }
        readonly struct ScoutCandidate075
        {
            public readonly Vector2 point;public readonly int index;public readonly float score;
            public ScoutCandidate075(Vector2 point,int index,float score){this.point=point;this.index=index;this.score=score;}
        }
        bool TryScoutFrontier075(EntityState actor,UnitOrder order,List<ScoutThreat075> threats,out Vector2 goal)
        {
            goal=actor.position;var candidates=new List<ScoutCandidate075>();int cursor=(int)order.elapsed;
            // Only the owner's explored boundary is consulted. Hidden resource
            // positions, enemy positions and treasure truth never choose a leg.
            for(int i=0;i<1024;i++)
            {
                int index=(cursor+i)%1024;var point=new Vector2(2+(index%32)*4,2+(index/32)*4);
                if(Fog.At(actor.owner,point)!=VisibilityState.Unexplored||!Navigation.IsWalkable(point,actor.def.radius)||
                    Layout.RequiresTransport&&Layout.LandRegion(actor.position)!=Layout.LandRegion(point))continue;
                bool frontier=false;
                for(int d=0;d<4;d++)
                {var near=point+(d==0?Vector2.right:d==1?Vector2.left:d==2?Vector2.up:Vector2.down)*4;if(Fog.At(actor.owner,near)!=VisibilityState.Unexplored){frontier=true;break;}}
                if(frontier&&ScoutClearance075(point,threats)>=0)candidates.Add(new ScoutCandidate075(point,index,(point-actor.position).sqrMagnitude+i*.0001f));
            }
            candidates.Sort((a,b)=>a.score.CompareTo(b.score));
            // Path attempts occur only at a leg/review boundary, never per tick.
            for(int i=0;i<candidates.Count&&i<32;i++)if(ScoutRouteSafe075(actor,candidates[i].point,threats))
            {goal=candidates[i].point;order.elapsed=(candidates[i].index+1)%1024;return true;}
            order.elapsed=(cursor+97)%1024;return false;
        }
        void SetScoutLeg075(EntityState actor,UnitOrder order,Vector2 goal)
        {
            order.resolvedPoint=goal;order.hasResolvedPoint=true;order.destinationVersion=Navigation.Version;
            InvalidateCommandPath(actor);
        }
        void TickScoutOrder075(EntityState actor,UnitOrder order,float dt)
        {
            if(!CanScout(actor)){actor.orders.Dequeue();actor.path.Clear();return;}
            actor.attackTarget=0;actor.fireAt=-1;
            if(!order.hasResolvedPoint&&actor.orders.Count>1){actor.orders.Dequeue();actor.path.Clear();actor.status="Queued command ready";return;}
            if(order.hasResolvedPoint&&(actor.position-order.resolvedPoint).sqrMagnitude<=.15f*.15f)
            {
                order.hasResolvedPoint=false;actor.path.Clear();
                // A queued command owns the unit after the current scout leg.
                if(actor.orders.Count>1){actor.orders.Dequeue();actor.status="Scout leg complete";return;}
                order.destinationRetryAt=Time;
            }
            if(Time>=order.destinationRetryAt||order.hasResolvedPoint&&order.destinationVersion!=Navigation.Version)
            {
                order.destinationRetryAt=Time+ScoutReviewSeconds075;var threats=VisibleScoutThreats075(actor);
                if(ScoutClearance075(actor.position,threats)<0)
                {
                    if(TryScoutRetreat075(actor,threats,out var retreat))SetScoutLeg075(actor,order,retreat);
                    else {order.hasResolvedPoint=false;actor.path.Clear();actor.status="Scout escape blocked";return;}
                }
                else if(!order.hasResolvedPoint||!CurrentScoutRouteSafe075(actor,order,threats))
                {
                    order.hasResolvedPoint=false;actor.path.Clear();
                    if(TryScoutFrontier075(actor,order,threats,out var frontier))SetScoutLeg075(actor,order,frontier);
                    else {actor.status="No safe reachable scout frontier";return;}
                }
            }
            if(!order.hasResolvedPoint){actor.status="Waiting for scout frontier";return;}
            MoveTowardsTarget(actor,order.resolvedPoint,dt,.04f);actor.status="Scouting";
        }
    }
}
