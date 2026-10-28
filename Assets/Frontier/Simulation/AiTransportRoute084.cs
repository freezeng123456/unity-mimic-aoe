using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public partial class World
    {
        // One complete native route query. The existing bridge planner has its
        // own finite stabilization bound (2 * authored bridges + 1). These
        // queries neither issue an order nor edit any live actor or transit.
        public bool TryMovementRoute084(EntityState actor,Vector2 start,Vector2 goal,out float distance,bool preserveBridgeTransit=false)
        {
            distance=0;
            if(!TransportRouteActor084(actor,start,goal))return false;
            var savedRules=new List<BridgeTrafficRule>(bridgeTrafficRules);
            int savedActor=bridgeTrafficActor;Vector2 savedOrigin=bridgeTrafficOrigin;float savedRadius=bridgeTrafficRadius;
            try
            {
                var proxy=TransportRouteProxy084(actor,start,goal,preserveBridgeTransit);
                var navigation=NavigationFor(proxy);
                // FindPath projects invalid endpoints. A complete chain may
                // never obtain reachability by accepting such a projection.
                if(!navigation.IsWalkable(start,proxy.def.radius)||!navigation.IsWalkable(goal,proxy.def.radius))return false;
                var route=FindMovementPath(proxy,goal);
                if(route.Count==0||(route.Count==1&&(goal-start).sqrMagnitude>.000001f)||(route[0]-start).sqrMagnitude>.000001f||(route[route.Count-1]-goal).sqrMagnitude>.000001f)return false;
                float length=0;
                for(int i=1;i<route.Count;i++)
                {
                    if(!MovementLineFree(proxy,route[i-1],route[i]))return false;
                    length+=Vector2.Distance(route[i-1],route[i]);
                }
                distance=length;return SnapshotCodec.Finite(length);
            }
            finally
            {
                bridgeTrafficRules.Clear();bridgeTrafficRules.AddRange(savedRules);
                bridgeTrafficActor=savedActor;bridgeTrafficOrigin=savedOrigin;bridgeTrafficRadius=savedRadius;
            }
        }

        // A swept, exact connector for nearby cohort/landing slots. This is a
        // line test, not another path search, and applies the same bridge lanes.
        public bool MovementConnector084(EntityState actor,Vector2 start,Vector2 goal,bool preserveBridgeTransit=false)
        {
            if(!TransportRouteActor084(actor,start,goal))return false;
            var savedRules=new List<BridgeTrafficRule>(bridgeTrafficRules);
            int savedActor=bridgeTrafficActor;Vector2 savedOrigin=bridgeTrafficOrigin;float savedRadius=bridgeTrafficRadius;
            try
            {
                var proxy=TransportRouteProxy084(actor,start,goal,preserveBridgeTransit);
                PrepareBridgeTraffic(proxy,goal,new List<Vector2>{start,goal});
                return MovementLineFree(proxy,start,goal);
            }
            finally
            {
                bridgeTrafficRules.Clear();bridgeTrafficRules.AddRange(savedRules);
                bridgeTrafficActor=savedActor;bridgeTrafficOrigin=savedOrigin;bridgeTrafficRadius=savedRadius;
            }
        }

        private static bool TransportRouteActor084(EntityState actor,Vector2 start,Vector2 goal)=>
            actor!=null&&actor.def!=null&&actor.Alive&&actor.def.radius>0&&SnapshotCodec.Finite(actor.def.radius)&&
            SnapshotCodec.Finite(start)&&SnapshotCodec.Finite(goal);

        private static EntityState TransportRouteProxy084(EntityState actor,Vector2 start,Vector2 goal,bool preserveBridgeTransit)
        {
            var proxy=new EntityState{id=actor.id,owner=actor.owner,def=actor.def,position=start,previousPosition=start,hp=actor.hp,construction=actor.construction};
            if(preserveBridgeTransit&&(start-actor.position).sqrMagnitude<=.000001f&&actor.orders.Count>0)
            {
                var current=actor.orders.Peek();
                proxy.orders.Enqueue(new UnitOrder(OrderKind.Move,goal)
                {bridgeTransitIndex=current.bridgeTransitIndex,bridgeTransitDirection=current.bridgeTransitDirection});
            }
            return proxy;
        }

        // Ordinary movement endpoint for an unchanged public order intent.
        // This mirrors ResolveMoveDestination's geometry and score without
        // emitting DestinationAdjusted, mutating an order or probing paths.
        // The caller proves the selected exact endpoint in its bounded native
        // route budget; if that first candidate is unreachable this survey
        // conservatively defers/rejects instead of searching more endpoints.
        public bool TryMoveEndpoint084(EntityState actor,Vector2 start,Vector2 requested,out Vector2 goal,bool reuseCurrentOrder=false)
        {
            goal=Vector2.zero;if(!TransportRouteActor084(actor,start,requested))return false;
            var proxy=TransportRouteProxy084(actor,start,requested,false);var navigation=NavigationFor(proxy);
            if(reuseCurrentOrder&&actor.orders.Count>0)
            {
                var current=actor.orders.Peek();
                if((current.kind==OrderKind.Move||current.kind==OrderKind.AttackMove)&&(current.point-requested).sqrMagnitude<.0001f&&
                    current.hasResolvedPoint&&current.destinationVersion==navigation.Version&&DestinationAvailable(proxy,current.resolvedPoint))
                {goal=current.resolvedPoint;return true;}
            }
            if(DestinationAvailable(proxy,requested)){goal=requested;return true;}
            var savedSpaces=new List<DestinationSpace>(destinationSpaces);
            try
            {
                GatherDestinationSpaces(proxy,requested);var candidates=new List<DestinationCandidate>();
                for(int y=-16;y<=16;y++)for(int x=-16;x<=16;x++)
                {
                    var offset=new Vector2(x*.5f,y*.5f);if(offset.sqrMagnitude>DestinationSearchRadius*DestinationSearchRadius)continue;
                    var point=requested+offset;if(!navigation.IsWalkable(point,proxy.def.radius)||!DestinationSpaceFree(proxy,point))continue;
                    candidates.Add(new DestinationCandidate{point=point,score=offset.sqrMagnitude+Vector2.Distance(point,start)*.001f});
                }
                if(candidates.Count==0)return false;candidates.Sort(CompareDestinationCandidates);goal=candidates[0].point;return true;
            }
            finally{destinationSpaces.Clear();destinationSpaces.AddRange(savedSpaces);}
        }

        // Geometry only, bounded by the same 25 x 25 neighborhood and stable
        // scoring as the public transport command. No route search is hidden
        // here. The caller must prove each complete land/water leg separately.
        // Current physical occupancy is provisional; public commands recheck it.
        public bool TryTransportShoreGeometry084(EntityState ship,Vector2 requested,float landRadius,out Vector2 land,out Vector2 water)
        {
            land=water=Vector2.zero;
            if(ship==null||ship.def==null||!ship.Alive||!ship.def.waterborne||!SnapshotCodec.Finite(requested)||
                landRadius<=0||!SnapshotCodec.Finite(landRadius))return false;
            var choices=new List<ShoreChoice>();var ignoredShip=new HashSet<int>{ship.id};
            foreach(var e in Entities)if(e.owner!=ship.owner&&e.def.kind==EntityKind.Unit&&!IsVisible(ship.owner,e))ignoredShip.Add(e.id);
            for(int y=-12;y<=12;y++)for(int x=-12;x<=12;x++)
            {
                if(x*x+y*y>144)continue;Vector2 candidate=requested+new Vector2(x,y);
                if(IsWater(candidate)||IsBridge(candidate)||IsBridgeApproach(candidate)||!Navigation.IsWalkable(candidate,landRadius))continue;
                Vector2 direction=Layout.WaterDirection(candidate);if(direction.sqrMagnitude<.5f)continue;
                float maximum=ship.def.radius+landRadius+3.5f;
                for(float offset=1;offset<=maximum;offset+=.5f)
                {
                    Vector2 berth=candidate+direction*offset;
                    if(!IsWater(berth)||IsBridge(berth)||IsBridgeApproach(berth)||!WaterNavigation.IsWalkable(berth,ship.def.radius)||
                        PositionOccupied(berth,ship.def.radius,ignoredShip,false,true))continue;
                    choices.Add(new ShoreChoice{land=candidate,water=berth,score=(candidate-requested).sqrMagnitude+offset*.15f});break;
                }
            }
            if(choices.Count==0)return false;
            choices.Sort((a,b)=>{int result=a.score.CompareTo(b.score);if(result!=0)return result;result=a.land.x.CompareTo(b.land.x);return result!=0?result:a.land.y.CompareTo(b.land.y);});
            land=choices[0].land;water=choices[0].water;return true;
        }

        // Candidate body slots follow the actual release ordering and geometry,
        // with the future berth supplied explicitly. Fog is intentionally not a
        // precondition: the sailing ship must reveal the actual landing before
        // TryUnloadTransport can release anyone. Occupancy may change in flight.
        // Results align with cargo input order and confer no slot reservation.
        public bool TryTransportLandingSlots084(EntityState ship,Vector2 shore,Vector2 berth,IReadOnlyList<EntityState> cargo,out Vector2[] points)
        {
            points=Array.Empty<Vector2>();
            if(ship==null||ship.def==null||!ship.Alive||!ship.def.transportShip||cargo==null||cargo.Count==0||
                !SnapshotCodec.Finite(shore)||!SnapshotCodec.Finite(berth)||!IsWater(berth)||IsBridge(berth)||IsBridgeApproach(berth)||
                !WaterNavigation.IsWalkable(berth,ship.def.radius))return false;
            var ordered=new List<int>(cargo.Count);var ignored=new HashSet<int>();
            for(int i=0;i<cargo.Count;i++)
            {
                var unit=cargo[i];
                if(unit==null||unit.def==null||!unit.Alive||unit.owner!=ship.owner||unit.def.kind!=EntityKind.Unit||unit.def.waterborne||!ignored.Add(unit.id))return false;
                ordered.Add(i);
            }
            foreach(var e in Entities)if(e.owner!=ship.owner&&e.def.kind==EntityKind.Unit&&!IsVisible(ship.owner,e))ignored.Add(e.id);
            ordered.Sort((a,b)=>cargo[a].def.radius==cargo[b].def.radius?cargo[a].id.CompareTo(cargo[b].id):cargo[b].def.radius.CompareTo(cargo[a].def.radius));
            var selected=new Vector2[cargo.Count];var placed=new List<int>(cargo.Count);
            foreach(int index in ordered)
            {
                var unit=cargo[index];bool found=false;
                foreach(var candidate in Navigation.FormationSlots(shore,256,unit.def.radius*2+.35f))
                {
                    if((candidate-shore).sqrMagnitude>36||Vector2.Distance(candidate,berth)>ship.def.radius+unit.def.radius+6||
                        IsWater(candidate)||IsBridge(candidate)||IsBridgeApproach(candidate)||!Navigation.IsWalkable(candidate,unit.def.radius)||
                        PositionOccupied(candidate,unit.def.radius,ignored,true)||!Navigation.ClearLine(shore,candidate,unit.def.radius))continue;
                    bool overlap=false;
                    foreach(int previous in placed)
                        if((candidate-selected[previous]).sqrMagnitude<Mathf.Pow(unit.def.radius+cargo[previous].def.radius+.15f,2)){overlap=true;break;}
                    if(overlap)continue;selected[index]=candidate;placed.Add(index);found=true;break;
                }
                if(!found)return false;
            }
            points=selected;return true;
        }
    }
}
