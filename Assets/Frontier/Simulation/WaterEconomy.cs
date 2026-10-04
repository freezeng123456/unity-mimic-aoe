using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public partial class World
    {
        // Gathering authority belongs to the actor, not just to the resource's
        // Food/Wood/Coin category. A fish is never a land settler's food source.
        public bool CanGather(EntityState actor,EntityState target)=>GatheringDomainAllowed(actor,target)&&IsVisible(actor.owner,target)&&(target.remaining>0||Sustainable(target));

        private bool GatheringDomainAllowed(EntityState actor,EntityState target)
        {
            if(actor==null||!actor.Alive||actor.owner<0||actor.garrisonedIn!=0||target==null||!target.Alive||target.def.resource==ResourceKind.None||
                (target.def.kind!=EntityKind.Resource&&target.owner!=actor.owner))return false;
            if(actor.def.fishingBoat)return actor.def.waterborne&&target.def.id=="fish"&&target.def.waterborne&&IsWater(actor.position)&&IsWater(target.position);
            return actor.Worker&&!actor.def.waterborne&&!target.def.waterborne;
        }

        public Vector2 DockWaterDirection(Vector2 point)
        {
            if(MapId!="river"||IsWater(point)||IsBridgeApproach(point))return Vector2.zero;
            var definition=Rules.Entity("dock");if(definition==null)return Vector2.zero;
            var directions=new[]{Vector2.right,Vector2.left,Vector2.up,Vector2.down};
            foreach(var direction in directions)
            {
                float half=direction.x==0?definition.depth*.5f:definition.width*.5f;
                Vector2 front=point+direction*(half+1),side=new Vector2(-direction.y,direction.x);
                if(IsWater(front)&&!IsBridge(front)&&IsWater(front+side*1.2f)&&!IsBridge(front+side*1.2f)&&IsWater(front-side*1.2f)&&!IsBridge(front-side*1.2f))return direction;
            }
            return Vector2.zero;
        }

        public string DockPlacementReason(Vector2 point)
        {
            if(MapId!="river")return "River docks require a river map";
            if(DockWaterDirection(point)==Vector2.zero)return "Place the dock on a clear riverbank, facing open water";
            return "";
        }

        // The existing overload remains the land/reward spawn contract. Ships
        // use only their producer's water-facing side and their own clearance.
        public bool FindSpawnPositions(EntityState receiver,int count,EntityDef unit,out List<Vector2> positions)
        {
            if(!unit.waterborne)return FindSpawnPositions(receiver,count,unit.radius,out positions);
            positions=new List<Vector2>();
            if(receiver==null||receiver.def.id!="dock"||MapId!="river")return false;
            Vector2 direction=DockWaterDirection(receiver.position);if(direction==Vector2.zero)return false;
            Vector2 side=new Vector2(-direction.y,direction.x);
            float half=direction.x==0?receiver.def.depth*.5f:receiver.def.width*.5f;
            var navigation=NavigationFor(unit);
            for(int ring=0;ring<5&&positions.Count<count;ring++)for(int slot=0;slot<9&&positions.Count<count;slot++)
            {
                int offset=slot==0?0:(slot+1)/2*(slot%2==1?1:-1);
                Vector2 point=receiver.position+direction*(half+2+ring*1.25f)+side*(offset*(unit.radius*2+.3f));
                if(!IsWater(point)||!navigation.IsWalkable(point,unit.radius))continue;
                bool occupied=false;
                foreach(var entity in Entities)
                    if(entity.Alive&&entity.garrisonedIn==0&&entity.def.waterborne&&entity.def.kind!=EntityKind.Building&&(entity.position-point).sqrMagnitude<Mathf.Pow(unit.radius+entity.def.radius+.15f,2)){occupied=true;break;}
                foreach(var other in positions)if((other-point).sqrMagnitude<Mathf.Pow(unit.radius*2+.3f,2)){occupied=true;break;}
                if(!occupied)positions.Add(point);
            }
            return positions.Count==count;
        }
    }
}
