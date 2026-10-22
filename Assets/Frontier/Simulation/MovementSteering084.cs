using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public partial class World
    {
        private struct SteeringObservation084
        {
            internal int id,owner;
            internal bool waterborne;
            internal SteeringNeighbor084 neighbor;
            internal BridgeSpan corridor;
            internal float corridorDirection,corridorAcross;
        }
        private readonly List<SteeringObservation084> steeringFrame084=new List<SteeringObservation084>(512);
        private readonly List<int>[] steeringBins084=new List<int>[16*16];
        private readonly List<SteeringNeighbor084> steeringNeighbors084=new List<SteeringNeighbor084>(48);
        private readonly LocalSteering084 localSteering084=new LocalSteering084();
        private float steeringFrameTime084,steeringMaxSpeed084,steeringMaxRadius084;
        private bool steeringFrameReady084;
        private EntityState steeringQueryUnit084;
        private float steeringQueryDt084;
        private Func<SteeringVector084,bool> steeringAdmissible084;
        private static SteeringVector084 SteeringVector(Vector2 value)=>new SteeringVector084(value.x,value.y);
        private static Vector2 MovementVector(SteeringVector084 value)=>new Vector2((float)value.x,(float)value.y);

        // All predictions in this Tick see one frame, before the first actor
        // moves. Rebuild from saved state every Tick; no velocity history, last
        // dt, random state or unsaved simulator affects a cold continuation.
        private void BeginSteeringFrame084(float dt)
        {
            steeringFrame084.Clear();foreach(var bin in steeringBins084)bin?.Clear();
            steeringMaxSpeed084=steeringMaxRadius084=0;
            foreach(var actor in Entities)
            {
                if(!actor.Alive||actor.garrisonedIn!=0||actor.def.kind!=EntityKind.Unit)continue;
                Vector2 intent=SteeringIntent084(actor);
                var observation=new SteeringObservation084{id=actor.id,owner=actor.owner,waterborne=actor.def.waterborne,
                    neighbor=new SteeringNeighbor084{id=actor.id,position=SteeringVector(actor.position),intent=SteeringVector(intent),
                        radius=actor.def.radius,moving=intent.sqrMagnitude>.0000001f}};
                if(intent.sqrMagnitude>.0000001f&&BridgeConvoyKey082(actor,intent.normalized,out var corridor,out float direction)&&SteeringThroughCorridor084(actor,corridor,direction))
                {observation.corridor=corridor;observation.corridorDirection=direction;observation.corridorAcross=corridor.Local(actor.position).x*direction;}
                int index=steeringFrame084.Count;steeringFrame084.Add(observation);
                int x=Mathf.Clamp(Mathf.FloorToInt(actor.position.x/8),0,15),y=Mathf.Clamp(Mathf.FloorToInt(actor.position.y/8),0,15),cell=x+y*16;
                if(steeringBins084[cell]==null)steeringBins084[cell]=new List<int>(8);steeringBins084[cell].Add(index);
                steeringMaxSpeed084=Mathf.Max(steeringMaxSpeed084,Speed(actor));steeringMaxRadius084=Mathf.Max(steeringMaxRadius084,actor.def.radius);
            }
            steeringFrameTime084=Time;steeringFrameReady084=true;
            if(steeringAdmissible084==null)steeringAdmissible084=SteeringStepAdmissible084;
        }
        private bool SteeringThroughCorridor084(EntityState actor,BridgeSpan span,float direction)
        {
            if(actor.orders.Count==0)return false;var order=actor.orders.Peek();
            if(order.kind!=OrderKind.Move&&order.kind!=OrderKind.AttackMove)return false;
            Vector2 goal=order.hasResolvedPoint?order.resolvedPoint:order.point;
            // Exact bridge destinations retain their original freedom, including
            // crossing its median. They do not promise a through-flow response.
            if(span.Approach(goal))return false;
            if(actor.path.Count>=2&&(actor.pathGoal-goal).sqrMagnitude<=4)
            {
                bool found=false;Vector2 first=Vector2.zero,last=Vector2.zero;
                for(int i=1;i<actor.path.Count;i++)
                {
                    if(!ClipBridgeTraffic(span.Local(actor.path[i-1]),span.Local(actor.path[i]),span.HalfLength+span.RampLength,span.HalfWidth,out Vector2 entry,out Vector2 exit))continue;
                    if(!found){first=entry;found=true;}last=exit;
                }
                bool median=found&&first.x*last.x<=0;
                bool deckExit=found&&span.Contains(actor.position)&&Mathf.Abs(last.x)>=span.HalfLength+span.RampLength-.0001f&&last.x*(last.x-first.x)>0;
                // Match Prepare precedence: a real reverse route wins over an
                // old transit. Never fall back after finding either direction.
                if(found&&Mathf.Abs(last.x-first.x)>.25f&&(median||deckExit))return Mathf.Sign(last.x-first.x)==direction;
            }
            int transit=order.bridgeTransitIndex-1;
            if(transit>=0&&transit<Layout.Bridges.Length&&ReferenceEquals(Layout.Bridges[transit],span)&&span.Approach(actor.position))
                return order.bridgeTransitDirection==direction;
            // Public Move clears its path before the next Tick. A fresh actor
            // born on the deck has no historical route yet; accept only a clear
            // straight exit that is already wholly in the correct travel half.
            // This is observed current-order intent, not an injected path.
            if(order.kind!=OrderKind.Move||actor.path.Count!=0||actor.pathIndex!=0||actor.pathAvoidsAllies||actor.detourUntil>Time||!span.Contains(actor.position))return false;
            Vector2 a=span.Local(actor.position),b=span.Local(goal);float end=span.HalfLength+span.RampLength;
            float sideLimit=-actor.def.radius-.1f+(float)BridgeTrafficTolerance082;
            if(b.x*direction<=end||a.y*direction>sideLimit||!NavigationFor(actor).ClearLine(actor.position,goal,actor.def.radius))return false;
            if(!ClipBridgeTraffic(a,b,end,span.HalfWidth,out _,out Vector2 lastExit))return false;
            return lastExit.x*direction>=end-.0001f&&lastExit.y*direction<=sideLimit;
        }
        private Vector2 SteeringIntent084(EntityState actor)
        {
            if(!actor.Complete||actor.hold||actor.shielded||Time<actor.shieldReadyAt||actor.fireAt>=0||actor.owner>=0&&!IsPlayerActive(actor.owner)||actor.orders.Count==0)return Vector2.zero;
            var order=actor.orders.Peek();
            // An idle worker, stopped artillery or a queued non-movement task
            // never promises half of an avoidance response. Explicit movement
            // is the bounded initial model; other pursuit is treated as static.
            if(order.kind!=OrderKind.Move&&order.kind!=OrderKind.AttackMove)return Vector2.zero;
            Vector2 goal=order.hasResolvedPoint?order.resolvedPoint:order.point;
            float travel=Speed(actor)*(float)LocalSteering084.Horizon;
            if(travel<=0||(goal-actor.position).sqrMagnitude<.0004f)return Vector2.zero;
            Vector2 point=actor.position;
            if((actor.pathGoal-goal).sqrMagnitude<=4&&actor.pathIndex<actor.path.Count)
            {
                for(int i=Mathf.Max(0,actor.pathIndex);i<actor.path.Count&&travel>0;i++)
                {
                    Vector2 segment=actor.path[i]-point;float length=segment.magnitude;
                    if(length<=travel){point=actor.path[i];travel-=length;}
                    else {point+=segment*(travel/length);travel=0;}
                }
            }
            else point=actor.position+Vector2.ClampMagnitude(goal-actor.position,travel);
            return (point-actor.position)/(float)LocalSteering084.Horizon;
        }
        private void GatherSteeringNeighbors084(EntityState actor,Vector2 preferred,double horizon)
        {
            steeringNeighbors084.Clear();
            float reach=(preferred.magnitude+steeringMaxSpeed084)*(float)LocalSteering084.Horizon+actor.def.radius+steeringMaxRadius084+.2f;
            int x0=Mathf.Clamp(Mathf.FloorToInt((actor.position.x-reach)/8),0,15),x1=Mathf.Clamp(Mathf.FloorToInt((actor.position.x+reach)/8),0,15);
            int y0=Mathf.Clamp(Mathf.FloorToInt((actor.position.y-reach)/8),0,15),y1=Mathf.Clamp(Mathf.FloorToInt((actor.position.y+reach)/8),0,15);
            var origin=SteeringVector(actor.position);
            bool hasCorridor=BridgeConvoyKey082(actor,preferred.normalized,out var corridor,out float direction);
            float across=hasCorridor?corridor.Local(actor.position).x*direction:0;
            bool guided=false;if(hasCorridor&&bridgeTrafficActor==actor.id)foreach(var rule in bridgeTrafficRules)
                if(ReferenceEquals(rule.span,corridor)&&rule.direction==direction){guided=true;break;}
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
            {
                var bin=steeringBins084[x+y*16];if(bin==null)continue;
                foreach(int index in bin)
                {
                    var observation=steeringFrame084[index];
                    if(observation.id==actor.id||observation.waterborne!=actor.def.waterborne)continue;
                    var neighbor=observation.neighbor;neighbor.distanceSquared=(neighbor.position-origin).LengthSquared;
                    // Retain soft avoidance of other sides from the original
                    // chooser, without relying on a hostile unit's cooperation.
                    if(!ShareMovementSide(actor.owner,observation.owner)){neighbor.intent=new SteeringVector084();neighbor.moving=false;}
                    double physical=actor.def.radius+neighbor.radius;
                    bool rearFollower=guided&&ShareMovementSide(actor.owner,observation.owner)&&neighbor.moving&&
                        ReferenceEquals(corridor,observation.corridor)&&observation.corridorDirection==direction&&
                        observation.corridorAcross<across-.0001f&&neighbor.distanceSquared>=physical*physical&&
                        SteeringVector084.Dot(SteeringVector(preferred.normalized),neighbor.intent.Normalized)>=.75;
                    neighbor=LocalSteering084.FollowingPrediction(neighbor,SteeringVector(preferred),rearFollower);
                    double near=actor.def.radius+neighbor.radius+.6;
                    neighbor.priority=neighbor.distanceSquared<=near*near?0:
                        LocalSteering084.Conflicts(origin,SteeringVector(preferred),actor.def.radius,neighbor,horizon)?1:2;
                    if(neighbor.distanceSquared<=reach*reach)steeringNeighbors084.Add(neighbor);
                }
            }
            // Near bodies and predicted threats precede ordinary proximity.
            // Soft scoring is explicitly bounded; actual hard sweeps remain
            // uncapped and can reject a body absent from these predictions.
            steeringNeighbors084.Sort(CompareSteeringNeighbors084);
            if(steeringNeighbors084.Count>LocalSteering084.MaxNeighbors)
                steeringNeighbors084.RemoveRange(LocalSteering084.MaxNeighbors,steeringNeighbors084.Count-LocalSteering084.MaxNeighbors);
        }
        private static int CompareSteeringNeighbors084(SteeringNeighbor084 a,SteeringNeighbor084 b)
        {int order=a.priority.CompareTo(b.priority);if(order!=0)return order;order=a.distanceSquared.CompareTo(b.distanceSquared);return order!=0?order:a.id.CompareTo(b.id);}
        private bool SteeringStepAdmissible084(SteeringVector084 velocity)
        {
            Vector2 end=steeringQueryUnit084.position+MovementVector(velocity)*steeringQueryDt084;
            return MovementLineFree(steeringQueryUnit084,steeringQueryUnit084.position,end)&&
                AllyMovementSegmentFree(steeringQueryUnit084,steeringQueryUnit084.position,end);
        }
        private bool SteeringCrossConflict084(EntityState actor,Vector2 velocity,Vector2 forward,double horizon)
        {
            var origin=SteeringVector(actor.position);var candidate=SteeringVector(velocity);
            foreach(var neighbor in steeringNeighbors084)
            {
                bool sameFlow=neighbor.moving&&neighbor.intent.LengthSquared>1e-12&&SteeringVector084.Dot(SteeringVector(forward),neighbor.intent.Normalized)>=.75;
                if(!sameFlow&&LocalSteering084.Conflicts(origin,candidate,actor.def.radius,neighbor,horizon))return true;
            }
            return false;
        }
        // The same canonical follower keeps its full straight step when every
        // real front-body budget already preserves the declared headway. This
        // is the un-clipped branch of the existing following policy, not an
        // assumed reciprocal response or an exemption from physical sweeps.
        private bool TryBridgeConvoyFullVelocity084(EntityState unit,Vector2 forward,float speed,float dt,out Vector2 velocity)
        {
            velocity=Vector2.zero;if(unit.def.waterborne||bridgeTrafficActor!=unit.id||dt<=0||speed<=0)return false;
            if(!BridgeConvoyKey082(unit,forward,out var span,out float direction)||!SteeringThroughCorridor084(unit,span,direction))return false;
            bool guided=false;foreach(var rule in bridgeTrafficRules)
                if(ReferenceEquals(rule.span,span)&&rule.direction==direction){guided=true;break;}
            if(!guided)return false;
            float maximum=speed*dt,originAcross=span.Local(unit.position).x*direction;bool following=false;
            foreach(var other in Nearby(unit.position,maximum+3.6f))
            {
                if(!PhysicalMovementAlly(unit,other)||StationaryAlly(unit,other))continue;
                Vector2 otherForward=BridgeConvoyForward082(other);if(Vector2.Dot(forward,otherForward)<.75f)continue;
                if(!BridgeConvoyKey082(other,otherForward,out var otherSpan,out float otherDirection)||
                    !ReferenceEquals(span,otherSpan)||otherDirection!=direction||
                    span.Local(other.position).x*direction<=originAcross+.0001f)continue;
                Vector2 relative=other.position-unit.position;float along=Vector2.Dot(relative,forward);if(along<=0)continue;
                float physical=unit.def.radius+other.def.radius;
                if(relative.sqrMagnitude<physical*physical)return false;
                float preferred=physical+BridgeConvoyPreferredHeadway082;
                float lateralSquared=Mathf.Max(0,relative.sqrMagnitude-along*along);
                if(lateralSquared>=preferred*preferred)continue;
                if(!SteeringThroughCorridor084(other,span,direction))return false;
                float contact=along-Mathf.Sqrt(preferred*preferred-lateralSquared);
                if(contact<maximum)return false;
                following=true;
            }
            if(!following)return false;
            Vector2 candidate=unit.position+forward*maximum;
            if(!MovementLineFree(unit,unit.position,candidate)||!AllyMovementSegmentFree(unit,unit.position,candidate))return false;
            velocity=forward*speed;return true;
        }
        private Vector2 ChooseSteeringVelocity084(EntityState actor,Vector2 to,Vector2 goal,float dt)
        {
            if(dt<=0||to.sqrMagnitude<1e-12f)return Vector2.zero;
            if(!InBridgeSteeringArea084(actor))return ChooseLegacyVelocity084(actor,to,goal,dt);
            if(!steeringFrameReady084||steeringFrameTime084!=Time)BeginSteeringFrame084(dt);
            float speed=Mathf.Min(Speed(actor),to.magnitude/Mathf.Max(.001f,dt));Vector2 forward=to.normalized,preferred=forward*speed;
            // A velocity is only preferred until this visible waypoint. Predicting
            // it through the next turn or endpoint invents future collisions with
            // bodies the saved route will go around on a later leg.
            double horizon=Math.Max(dt,Math.Min(LocalSteering084.Horizon,to.magnitude/Math.Max(.001f,speed)));
            GatherSteeringNeighbors084(actor,preferred,horizon);
            // Preserve the declared same-flow bridge headway when it is the
            // only interaction. A zero-follow velocity cannot override a
            // crossing/counterflow escape, including a threat to that wait.
            if(TryBridgeConvoyVelocity082(actor,forward,speed,dt,out var convoy))
            {
                if(!SteeringCrossConflict084(actor,preferred,forward,horizon)&&!SteeringCrossConflict084(actor,convoy,forward,horizon))return convoy;
            }
            else if(TryBridgeConvoyFullVelocity084(actor,forward,speed,dt,out convoy)&&
                !SteeringCrossConflict084(actor,preferred,forward,horizon)&&!SteeringCrossConflict084(actor,convoy,forward,horizon))return convoy;
            steeringQueryUnit084=actor;steeringQueryDt084=dt;
            return MovementVector(localSteering084.Choose(actor.id,SteeringVector(actor.position),SteeringVector(preferred),actor.def.radius,dt,steeringNeighbors084,steeringAdmissible084,horizon));
        }
        private bool InBridgeSteeringArea084(EntityState actor)
        {
            if(actor.def.waterborne)return false;
            foreach(var span in Layout.Bridges)
            {
                Vector2 local=span.Local(actor.position);
                if(Mathf.Abs(local.x)<=span.HalfLength+span.RampLength+6&&Mathf.Abs(local.y)<=span.HalfWidth+6)return true;
            }
            return false;
        }
        // Preserve the previously accepted non-bridge navigation behavior.
        private readonly List<EntityState> legacySteeringNeighbors084=new List<EntityState>(24);
        private static readonly float[] legacySteeringAngles084={0,30,-30,60,-60,90,-90,120,-120,180};
        private Vector2 ChooseLegacyVelocity084(EntityState e,Vector2 to,Vector2 goal,float dt)
        {
            var navigation=NavigationFor(e);
            float speed=Mathf.Min(Speed(e),to.magnitude/Mathf.Max(.001f,dt));Vector2 forward=to.normalized;
            // Follow an exact recovery corridor when it has an unoccupied next step.
            // Lateral steering must not bend that corridor back into a parked ally.
            if(TryBridgeConvoyVelocity082(e,forward,speed,dt,out var convoyVelocity))return convoyVelocity;
            Vector2 recoveryStep=e.position+forward*speed*dt;
            if(e.pathAvoidsAllies&&MovementLineFree(e,e.position,recoveryStep)&&AllyMovementSegmentFree(e,e.position,recoveryStep))return forward*speed;
            legacySteeringNeighbors084.Clear();foreach(var other in Nearby(e.position,3.5f))if(other.id!=e.id&&other.def.kind==EntityKind.Unit&&other.garrisonedIn==0&&other.def.waterborne==e.def.waterborne)legacySteeringNeighbors084.Add(other);
            if(legacySteeringNeighbors084.Count==0)return forward*speed;
            Vector2 best=Vector2.zero;float bestScore=float.MaxValue;float distance=to.magnitude;Vector2 steeringGoal=e.position+to;
            foreach(float angle in legacySteeringAngles084)
            {
                float radians=angle*Mathf.Deg2Rad;Vector2 direction=new Vector2(forward.x*Mathf.Cos(radians)-forward.y*Mathf.Sin(radians),forward.x*Mathf.Sin(radians)+forward.y*Mathf.Cos(radians));
                Vector2 velocity=direction*speed,step=e.position+velocity*dt;
                if(!MovementLineFree(e,e.position,step)||!AllyMovementSegmentFree(e,e.position,step))continue;
                // Do not score a projection beyond a nearby corner: doing so made
                // units orbit the waypoint instead of completing the safe turn.
                Vector2 predicted=e.position+velocity*Mathf.Min(.45f,distance/Mathf.Max(.001f,speed));
                float score=(steeringGoal-predicted).magnitude-distance+Mathf.Abs(angle)*.0008f;
                foreach(var other in legacySteeringNeighbors084)
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
    }
}
