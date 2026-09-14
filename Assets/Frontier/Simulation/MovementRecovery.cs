using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public partial class World
    {
        private struct RecoveryDisc {public Vector2 position;public float radiusSquared,startSquared;}
        private readonly List<RecoveryDisc> recoveryDiscs=new List<RecoveryDisc>(64);
        private Vector2 recoveryStart;
        private Func<Vector2,bool> recoveryFilter;
        // Only a stalled actor pays for a route around nearby stationary allies.
        // This query does not mutate the shared building/navigation grid.
        private bool TryAllyRecovery(EntityState unit,Vector2 goal)
        {
            recoveryDiscs.Clear();recoveryStart=unit.position;
            foreach(var other in Nearby(unit.position,16))
            {
                if(!StationaryAlly(unit,other))continue;
                float radius=unit.def.radius+other.def.radius+.1f;
                recoveryDiscs.Add(new RecoveryDisc{position=other.position,radiusSquared=radius*radius,startSquared=(other.position-unit.position).sqrMagnitude});
            }
            if(recoveryDiscs.Count==0)return false;
            if(recoveryFilter==null)recoveryFilter=RecoveryPointFree;
            var route=Navigation.FindPath(unit.position,goal,unit.def.radius,recoveryFilter);
            if(route.Count<2||(route[route.Count-1]-goal).sqrMagnitude>.0001f)return false;
            unit.path.Clear();unit.path.AddRange(route);unit.pathIndex=1;unit.pathGoal=goal;
            unit.pathVersion=Navigation.Version;unit.pathRetryAt=Time+1.2f;unit.pathAvoidsAllies=true;
            return true;
        }
        private bool RecoveryPointFree(Vector2 point)
        {
            foreach(var disc in recoveryDiscs)
            {
                float distance=(point-disc.position).sqrMagnitude;
                if(distance>=disc.radiusSquared)continue;
                // Preserve the actual start and permit moving out of a pre-existing
                // contact. Never snap or deepen that contact to manufacture a path.
                if(disc.startSquared<disc.radiusSquared&&distance+.00001f>=disc.startSquared&&Vector2.Dot(point-recoveryStart,disc.position-recoveryStart)<=0)continue;
                return false;
            }
            return true;
        }
        private bool AllySegmentFree(EntityState unit,Vector2 start,Vector2 end)
        {
            var segment=end-start;float lengthSquared=segment.sqrMagnitude;if(lengthSquared<.0001f)return true;
            foreach(var other in Nearby((start+end)*.5f,segment.magnitude*.5f+3.5f))
            {
                if(!StationaryAlly(unit,other))continue;
                float radius=unit.def.radius+other.def.radius+.1f;
                float along=Vector2.Dot(other.position-start,segment)/lengthSquared;
                if((other.position-start).sqrMagnitude<radius*radius&&along<=0)continue;
                var closest=start+segment*Mathf.Clamp01(along);
                if((closest-other.position).sqrMagnitude<radius*radius)return false;
            }
            return true;
        }
    }
}
