using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public partial class World
    {
        private struct DestinationSpace { public Vector2 point;public float radius; }
        private struct DestinationCandidate { public Vector2 point;public float score; }
        private readonly List<DestinationSpace> destinationSpaces=new List<DestinationSpace>(64);
        private readonly List<DestinationCandidate> destinationCandidates=new List<DestinationCandidate>(512);
        private const float DestinationSearchRadius=8,DestinationClearance=.65f;
        // Order.point remains the original intent. Only a taken endpoint is
        // resolved locally; an intervening wall never completes the order.
        private bool ResolveMoveDestination(EntityState unit,UnitOrder order,out Vector2 goal)
        {
            goal=order.hasResolvedPoint?order.resolvedPoint:order.point;
            bool revised=order.destinationVersion!=Navigation.Version;
            if(order.hasResolvedPoint&&!revised&&DestinationAvailable(unit,goal))return true;
            if(DestinationAvailable(unit,order.point))
            {
                if(order.hasResolvedPoint)ResetDestinationPath(unit);
                order.hasResolvedPoint=false;order.destinationVersion=Navigation.Version;goal=order.point;return true;
            }
            if(!revised&&Time<order.destinationRetryAt)return false;
            if(revised)order.destinationCursor=0;
            order.destinationVersion=Navigation.Version;order.destinationRetryAt=Time+1.2f;
            GatherDestinationSpaces(unit,order.point);destinationCandidates.Clear();
            for(int y=-16;y<=16;y++)for(int x=-16;x<=16;x++)
            {
                var offset=new Vector2(x*.5f,y*.5f);if(offset.sqrMagnitude>DestinationSearchRadius*DestinationSearchRadius)continue;
                var point=order.point+offset;if(!Navigation.IsWalkable(point,unit.def.radius)||!DestinationSpaceFree(unit,point))continue;
                destinationCandidates.Add(new DestinationCandidate{point=point,score=offset.sqrMagnitude+Vector2.Distance(point,unit.position)*.001f});
            }
            destinationCandidates.Sort(CompareDestinationCandidates);
            if(!Navigation.IsWalkable(unit.position,unit.def.radius))return false;
            int count=destinationCandidates.Count;
            // Bound expensive route probes. If all closest candidates lie on the
            // wrong side of a wall, later retries continue through the candidates.
            for(int probe=0;probe<Mathf.Min(12,count);probe++)
            {
                int i=order.destinationCursor%count;order.destinationCursor=(i+1)%count;
                var candidate=destinationCandidates[i].point;var path=Navigation.FindPath(unit.position,candidate,unit.def.radius);
                if(path.Count<2||(path[path.Count-1]-candidate).sqrMagnitude>.0001f)continue;
                bool changed=!order.hasResolvedPoint||(order.resolvedPoint-candidate).sqrMagnitude>.0001f;
                order.resolvedPoint=candidate;order.hasResolvedPoint=true;goal=candidate;ResetDestinationPath(unit);
                if(changed)Emit("DestinationAdjusted",unit.owner,unit.id,order.kind+" destination "+order.point+" -> "+candidate,Vector2.Distance(order.point,candidate),0,candidate);
                return true;
            }
            return false;
        }
        private static readonly Vector2[] recoveryOffsets=RecoveryOffsets();
        private static Vector2[] RecoveryOffsets()
        {
            var offsets=new Vector2[26];offsets[0]=new Vector2(-7,4);offsets[1]=new Vector2(7,4);
            for(int i=0;i<24;i++){float angle=i*15*Mathf.Deg2Rad;offsets[i+2]=new Vector2(Mathf.Sin(angle),Mathf.Cos(angle))*8;}
            return offsets;
        }
        private bool TryLocalRecovery(EntityState unit,Vector2 requestedGoal,out Vector2 chosen)
        {
            Vector2 forward=(requestedGoal-unit.position).normalized,side=new Vector2(-forward.y,forward.x);
            float best=float.MaxValue;chosen=unit.position;
            foreach(var offset in recoveryOffsets)
            {
                var point=Navigation.NearestWalkable(unit.position+side*offset.x+forward*offset.y,unit.def.radius);
                if(!Navigation.IsWalkable(point,unit.def.radius)||!Navigation.ClearLine(unit.position,point,unit.def.radius)||!RecoverySegmentFree(unit,point))continue;
                float score=Vector2.Distance(point,requestedGoal);
                foreach(var other in Nearby(point,4))if(other.id!=unit.id&&other.def.kind==EntityKind.Unit)score+=8*Mathf.Max(0,4-Vector2.Distance(point,other.position));
                if(score<best){best=score;chosen=point;}
            }
            return best<float.MaxValue;
        }
        private bool RecoverySegmentFree(EntityState unit,Vector2 point)
        {
            var segment=point-unit.position;float lengthSquared=segment.sqrMagnitude;
            if(lengthSquared<.01f)return false;
            foreach(var other in Nearby((point+unit.position)*.5f,segment.magnitude*.5f+3.5f))
            {
                if(!StationaryAlly(unit,other))continue;
                float needed=unit.def.radius+other.def.radius+.12f;
                var relative=other.position-unit.position;
                float along=Vector2.Dot(relative,segment)/lengthSquared;
                // An existing overlap may be escaped, but not deepened.
                if(relative.sqrMagnitude<needed*needed&&along<=0)continue;
                var closest=unit.position+segment*Mathf.Clamp01(along);
                if((closest-other.position).sqrMagnitude<needed*needed)return false;
            }
            return true;
        }
        private static int CompareDestinationCandidates(DestinationCandidate a,DestinationCandidate b)
        {int order=a.score.CompareTo(b.score);if(order!=0)return order;order=a.point.x.CompareTo(b.point.x);return order!=0?order:a.point.y.CompareTo(b.point.y);}
        private static void ResetDestinationPath(EntityState unit)
        {unit.pathVersion=-1;unit.detourUntil=0;unit.progressDistance=float.MaxValue;unit.stuckFor=0;}
        private static bool StationaryAlly(EntityState unit,EntityState other)
        {
            if(other.id==unit.id||!other.Alive||other.owner!=unit.owner||other.garrisonedIn!=0||other.def.kind!=EntityKind.Unit)return false;
            if(other.orders.Count==0||other.hold)return true;
            var kind=other.orders.Peek().kind;
            return kind!=OrderKind.Move&&kind!=OrderKind.AttackMove&&other.status!="Moving";
        }
        private bool DestinationAvailable(EntityState unit,Vector2 point)
        {
            if(!Navigation.IsWalkable(point,unit.def.radius))return false;
            foreach(var other in Nearby(point,3.5f))if(StationaryAlly(unit,other))
            {float space=unit.def.radius+other.def.radius+.15f;if((other.position-point).sqrMagnitude<space*space)return false;}
            return true;
        }
        private void GatherDestinationSpaces(EntityState unit,Vector2 intent)
        {
            destinationSpaces.Clear();
            foreach(var other in Entities)
            {
                if(other.id==unit.id||!other.Alive||other.owner!=unit.owner||other.garrisonedIn!=0||other.def.kind!=EntityKind.Unit)continue;
                Vector2 point;
                if(other.orders.Count>0&&(other.orders.Peek().kind==OrderKind.Move||other.orders.Peek().kind==OrderKind.AttackMove))
                {var order=other.orders.Peek();point=order.hasResolvedPoint?order.resolvedPoint:order.point;}
                else if(StationaryAlly(unit,other))point=other.position;else continue;
                if((point-intent).sqrMagnitude>12*12||!Navigation.IsWalkable(point,other.def.radius))continue;
                destinationSpaces.Add(new DestinationSpace{point=point,radius=other.def.radius});
            }
        }
        private bool DestinationSpaceFree(EntityState unit,Vector2 point)
        {
            foreach(var space in destinationSpaces)
            {float needed=unit.def.radius+space.radius+DestinationClearance;if((point-space.point).sqrMagnitude<needed*needed)return false;}
            return true;
        }
    }
}
