using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public partial class World
    {
        private readonly List<EntityState> pendingEvacuations=new List<EntityState>();
        private float nextEvacuation;
        private bool PositionOccupied(Vector2 point,float radius,HashSet<int> ignored=null,bool reserveMoves=false,bool waterborne=false)
        {
            foreach(var other in Entities)
            {
                if(!other.Alive||other.garrisonedIn!=0||other.def.waterborne!=waterborne||other.def.kind==EntityKind.Building||ignored!=null&&ignored.Contains(other.id))continue;
                float clearance=radius+other.def.radius+.15f;
                if((other.position-point).sqrMagnitude<clearance*clearance)return true;
                if(reserveMoves)foreach(var order in other.orders)
                    if(order.kind==OrderKind.Move&&(order.point-point).sqrMagnitude<clearance*clearance)return true;
            }
            return false;
        }
        private void AssignAutomaticRally(List<EntityState> group,Vector2 destination)
        {
            if(group.Count==0)return;
            // Production is normally homogeneous; grouping also keeps a future
            // mixed reward from forcing one movement domain into the other.
            for(int domain=0;domain<2;domain++)
            {
                var matching=group.FindAll(unit=>unit.def.waterborne==(domain==1));
                if(matching.Count>0)AssignAutomaticRallyDomain(matching,destination);
            }
        }
        private void AssignAutomaticRallyDomain(List<EntityState> group,Vector2 destination)
        {
            float radius=0,spacing=1.8f;var ignored=new HashSet<int>();
            bool waterborne=group[0].def.waterborne;var navigation=NavigationFor(group[0]);
            foreach(var unit in group){ignored.Add(unit.id);radius=Mathf.Max(radius,unit.def.radius);spacing=Mathf.Max(spacing,unit.def.radius*4+.3f);}
            // This runs once at arrival/release, never per unit per rendered frame.
            // Existing units and their queued endpoints reserve occupied landing slots.
            var slots=new List<Vector2>();
            foreach(var p in navigation.FormationSlots(destination,Mathf.Min(1024,group.Count+Entities.Count*4),spacing))
            {if(navigation.IsWalkable(p,radius)&&!PositionOccupied(p,radius,ignored,true,waterborne))slots.Add(p);if(slots.Count==group.Count)break;}
            foreach(var unit in group)
            {
                unit.orders.Clear();unit.path.Clear();unit.pathIndex=0;unit.fireAt=-1;unit.attackTarget=0;unit.hold=false;
                int best=-1;float distance=float.MaxValue;
                for(int i=0;i<slots.Count;i++){float d=(slots[i]-unit.position).sqrMagnitude;if(d<distance){distance=d;best=i;}}
                if(best<0){unit.status="Rally area blocked";continue;}
                var goal=slots[best];slots.RemoveAt(best);unit.orders.Enqueue(new UnitOrder(OrderKind.Move,goal));unit.status="Move";
                Emit("RallyAssigned",unit.owner,unit.id,"Automatic rally destination",0,0,goal);
            }
        }
        private bool ReleaseGarrison(EntityState building,bool destroyed)
        {
            var group=new List<EntityState>();float radius=0;
            foreach(int id in building.occupants)
            {var unit=Get(id);if(unit==null||!unit.Alive||unit.garrisonedIn!=building.id)continue;group.Add(unit);radius=Mathf.Max(radius,unit.def.radius);}
            if(group.Count==0){building.occupants.Clear();return true;}
            bool enough=FindSpawnPositions(building,group.Count,radius,out var positions);
            if(!enough&&!destroyed){building.status="Garrison exit blocked";return false;}
            if(!enough)
            {
                // The footprint is already unblocked by Remove. Use its free cells
                // and local surroundings before waiting; never stack the remainder.
                float spacing=radius*2+.35f;
                foreach(var p in Navigation.FormationSlots(building.position,group.Count+Entities.Count*4,spacing))
                {
                    if((p-building.position).sqrMagnitude>144||PositionOccupied(p,radius))continue;
                    bool overlaps=positions.Exists(q=>(p-q).sqrMagnitude<spacing*spacing);
                    if(!overlaps)positions.Add(p);if(positions.Count>=group.Count)break;
                }
            }
            var released=new List<EntityState>();
            for(int i=0;i<group.Count&&i<positions.Count;i++)
            {
                var unit=group[i];unit.garrisonedIn=0;unit.position=unit.previousPosition=positions[i];
                unit.orders.Clear();unit.path.Clear();unit.pathIndex=0;unit.fireAt=-1;unit.attackTarget=0;unit.status="Idle";
                building.occupants.Remove(unit.id);released.Add(unit);
            }
            if(!destroyed)AssignProducerRally(building,released);
            if(released.Count>0)Emit("Ungarrison",building.owner,building.id,destroyed?"Evacuated destroyed building":"Released to rally",released.Count);
            return building.occupants.Count==0;
        }
        private void EvacuateDestroyedBuilding(EntityState building)
        {if(!ReleaseGarrison(building,true)){pendingEvacuations.Add(building);Emit("EvacuationWaiting",building.owner,building.id,"Waiting for safe space",building.occupants.Count);}}
        private void TickEvacuations()
        {
            if(pendingEvacuations.Count==0||Time<nextEvacuation)return;nextEvacuation=Time+1;
            for(int i=pendingEvacuations.Count-1;i>=0;i--)if(ReleaseGarrison(pendingEvacuations[i],true))pendingEvacuations.RemoveAt(i);
        }
    }
}
