using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // A synchronous decision-local admission check, not a new movement policy.
    // Ordinary Gather still uses World.WorkPoint and unfiltered navigation. A
    // safe filtered alternative is therefore insufficient to authorize it.
    // Movement, steering, new vision and depletion retargeting between AI
    // reviews remain ordinary simulation behavior and are checked next review.
    public sealed class AiGatherRoutes074
    {
        readonly World world;readonly int player;
        readonly struct Threat
        {
            public readonly Vector2 Position;public readonly float ReachSquared;
            public Threat(EntityState entity)
            {Position=entity.position;float reach=Mathf.Max(12,entity.def.range+4);ReachSquared=reach*reach;}
        }
        readonly Threat[] threats;
        readonly Dictionary<long,bool> gatherRoutes=new Dictionary<long,bool>();
        public AiGatherRoutes074(World world,int player,List<EntityState> visible)
        {
            this.world=world;this.player=player;var current=new List<Threat>();
            foreach(var entity in visible)if(AiResourceContinuity072.EconomicThreat(world,player,entity))current.Add(new Threat(entity));
            threats=current.ToArray();
        }
        public bool PointSafe(Vector2 point)
        {
            foreach(var threat in threats)if((threat.Position-point).sqrMagnitude<threat.ReachSquared)return false;
            return true;
        }
        public bool SegmentSafe(Vector2 from,Vector2 to)
        {
            Vector2 delta=to-from;float length=delta.sqrMagnitude;
            foreach(var threat in threats)
            {
                float t=length>0?Mathf.Clamp01(Vector2.Dot(threat.Position-from,delta)/length):0;
                if((threat.Position-(from+delta*t)).sqrMagnitude<threat.ReachSquared)return false;
            }
            return true;
        }
        public bool CanGather(EntityState worker,EntityState source)
        {
            if(worker==null||source==null||worker.owner!=player||!AiCivilizationEconomy.CanReassign(worker)||worker.def.waterborne||
                !world.GatherableResource(player,source)||!world.CanGather(worker,source))return false;
            long key=((long)worker.id<<32)|(uint)source.id;
            if(gatherRoutes.TryGetValue(key,out bool cached))return cached;
            bool safe=PointSafe(worker.position)&&PointSafe(source.position);
            // Movement stops at 1.35, while gathering itself admits 1.5. Use the
            // former so a nearby worker never bypasses a movement check.
            if(safe&&world.EdgeDistance(worker,source)>1.35f)
            {
                var goal=world.WorkPoint(source,worker);
                // Projection may find a walkable point beyond actual extraction
                // range. Reuse Gather's geometry with a separate arrival probe;
                // no live actor position or manual command behavior changes.
                var arrival=new EntityState{def=worker.def,position=goal};
                safe=world.EdgeDistance(arrival,source)<=1.5f&&OrdinaryPathSafe(worker,goal);
                // A valid cached path may survive the movement retarget threshold
                // or ally recovery. Check its remaining legs as well, rather than
                // proving only a fresh route which the current order might not use.
                var order=worker.orders.Count>0?worker.orders.Peek():null;
                if(safe&&order!=null&&order.kind==OrderKind.Gather&&order.target==source.id)
                {
                    Vector2 previous=worker.position;
                    for(int i=worker.pathIndex;i<worker.path.Count&&safe;i++)
                    {safe=SegmentSafe(previous,worker.path[i]);previous=worker.path[i];}
                    if(safe&&worker.detourUntil>world.Time)safe=OrdinaryPathSafe(worker,worker.detour);
                }
            }
            gatherRoutes[key]=safe;return safe;
        }
        public bool OrdinaryPathSafe(EntityState worker,Vector2 goal)
        {
            var navigation=world.Navigation;
            if(!PointSafe(worker.position)||!PointSafe(goal)||!navigation.IsWalkable(worker.position,worker.def.radius)||
                !navigation.IsWalkable(goal,worker.def.radius))return false;
            if(navigation.ClearLine(worker.position,goal,worker.def.radius))return SegmentSafe(worker.position,goal);
            var path=navigation.FindPath(worker.position,goal,worker.def.radius);
            if(path.Count<2||(path[0]-worker.position).sqrMagnitude>=.01f||(path[path.Count-1]-goal).sqrMagnitude>=.01f)return false;
            for(int i=1;i<path.Count;i++)if(!SegmentSafe(path[i-1],path[i]))return false;
            return true;
        }
    }
}
