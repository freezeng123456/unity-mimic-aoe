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
            if(!Layout.HasWater||IsWater(point)||IsBridgeApproach(point))return Vector2.zero;
            var definition=Rules.Entity("dock");if(definition==null)return Vector2.zero;
            var direction=Layout.WaterDirection(point);if(direction.sqrMagnitude<.5f)return Vector2.zero;
            direction.Normalize();Vector2 side=new Vector2(-direction.y,direction.x);
            // The quay really projects into the channel. Its far edge must
            // have open water across its width, on any orientation of bank.
            Vector2 front=point+direction*DockQuayLength;
            for(int i=-1;i<=1;i++)
            {var sample=front+side*(i*DockQuayHalfWidth);if(!IsWater(sample)||IsBridgeApproach(sample))return Vector2.zero;}
            return direction;
        }

        public const float DockQuayLength=5,DockQuayHalfWidth=1.5f;

        // Footprints and rendering use local +X towards the water. Even a
        // rejected preview retains its nearest-bank orientation; legality is
        // decided separately, rather than changing its shape to face +X.
        private Vector2 BuildingDirection(EntityDef definition,Vector2 point)
        {
            if(definition.id!="dock"||!Layout.HasWater)return Vector2.right;
            Vector2 direction=Layout.WaterDirection(point);
            return direction.sqrMagnitude>.5f?direction.normalized:Vector2.right;
        }

        public Vector2[] BuildingFootprintCorners(EntityDef definition,Vector2 point)
        {
            Vector2 direction=BuildingDirection(definition,point),side=new Vector2(-direction.y,direction.x);
            float halfX=definition.width*.5f,halfY=definition.depth*.5f;
            var corners=new List<Vector2>{point-direction*halfX-side*halfY,point+direction*halfX-side*halfY,point+direction*halfX+side*halfY,point-direction*halfX+side*halfY};
            if(definition.id=="dock")corners.AddRange(new[]{point+direction*halfX-side*DockQuayHalfWidth,point+direction*DockQuayLength-side*DockQuayHalfWidth,point+direction*DockQuayLength+side*DockQuayHalfWidth,point+direction*halfX+side*DockQuayHalfWidth});
            return corners.ToArray();
        }

        public bool BuildingFootprintContains(EntityDef definition,Vector2 center,Vector2 point,float margin=0)
        {return BuildingFootprintDistance(definition,center,point)<=margin+.0001f;}

        public float BuildingFootprintDistance(EntityDef definition,Vector2 center,Vector2 point)
        {
            Vector2 direction=BuildingDirection(definition,center),side=new Vector2(-direction.y,direction.x),delta=point-center;
            Vector2 local=new Vector2(Vector2.Dot(delta,direction),Vector2.Dot(delta,side));
            float body=RectangleDistance(local,-definition.width*.5f,definition.width*.5f,definition.depth*.5f);
            return definition.id=="dock"?Mathf.Min(body,RectangleDistance(local,definition.width*.5f,DockQuayLength,DockQuayHalfWidth)):body;
        }

        public float DockLandEdgeDistance(EntityState actor,EntityState dock)
        {
            Vector2 direction=BuildingDirection(dock.def,dock.position),side=new Vector2(-direction.y,direction.x),delta=actor.position-dock.position;
            return Mathf.Max(0,RectangleDistance(new Vector2(Vector2.Dot(delta,direction),Vector2.Dot(delta,side)),-dock.def.width*.5f,dock.def.width*.5f,dock.def.depth*.5f)-actor.def.radius);
        }

        private static float RectangleDistance(Vector2 local,float minX,float maxX,float halfY)
        {return new Vector2(Mathf.Max(minX-local.x,0,local.x-maxX),Mathf.Max(0,Mathf.Abs(local.y)-halfY)).magnitude;}

        // Include every integer cell whose square intersects the physical
        // rectangle. This closes diagonal gaps between a rotated pier's piles.
        public IEnumerable<Vector2> BuildingFootprintCells(EntityDef definition,Vector2 point)
        {
            var seen=new HashSet<Vector2Int>();Vector2 direction=BuildingDirection(definition,point);
            foreach(var cell in RectangleCells(point,direction,definition.width,definition.depth))
                if(seen.Add(Vector2Int.RoundToInt(cell)))yield return cell;
            if(definition.id!="dock")yield break;
            float half=definition.width*.5f;
            foreach(var cell in RectangleCells(point+direction*((half+DockQuayLength)*.5f),direction,DockQuayLength-half,DockQuayHalfWidth*2))
                if(seen.Add(Vector2Int.RoundToInt(cell)))yield return cell;
        }

        private static IEnumerable<Vector2> RectangleCells(Vector2 center,Vector2 direction,float width,float depth)
        {
            Vector2 side=new Vector2(-direction.y,direction.x);
            float hx=width*.5f,hy=depth*.5f;
            float worldX=Mathf.Abs(direction.x)*hx+Mathf.Abs(side.x)*hy,worldY=Mathf.Abs(direction.y)*hx+Mathf.Abs(side.y)*hy;
            float cellProjection=(Mathf.Abs(direction.x)+Mathf.Abs(direction.y))*.5f;
            for(int y=Mathf.CeilToInt(center.y-worldY-.5f+.0001f);y<=Mathf.FloorToInt(center.y+worldY+.5f-.0001f);y++)
                for(int x=Mathf.CeilToInt(center.x-worldX-.5f+.0001f);x<=Mathf.FloorToInt(center.x+worldX+.5f-.0001f);x++)
                {
                    var cell=new Vector2(x,y);var delta=cell-center;
                    if(Mathf.Abs(Vector2.Dot(delta,direction))<hx+cellProjection-.0001f&&Mathf.Abs(Vector2.Dot(delta,side))<hy+cellProjection-.0001f)yield return cell;
                }
        }

        public bool BuildingFootprintsOverlap(EntityDef first,Vector2 firstPoint,EntityDef second,Vector2 secondPoint,float margin=0)
        {
            foreach(var a in FootprintRectangles(first,firstPoint))foreach(var b in FootprintRectangles(second,secondPoint))
            {
                Vector2 delta=b.center-a.center;
                bool separate=false;
                foreach(var axis in new[]{a.direction,a.side,b.direction,b.side})
                {
                    float extent=ProjectedHalfExtent(a,axis)+ProjectedHalfExtent(b,axis)+margin;
                    if(Mathf.Abs(Vector2.Dot(delta,axis))>=extent){separate=true;break;}
                }
                if(!separate)return true;
            }
            return false;
        }

        private struct FootprintRectangle
        {public Vector2 center,direction,side;public float halfX,halfY;}
        private IEnumerable<FootprintRectangle> FootprintRectangles(EntityDef definition,Vector2 point)
        {
            var direction=BuildingDirection(definition,point);var side=new Vector2(-direction.y,direction.x);
            yield return new FootprintRectangle{center=point,direction=direction,side=side,halfX=definition.width*.5f,halfY=definition.depth*.5f};
            if(definition.id=="dock")yield return new FootprintRectangle{center=point+direction*((definition.width*.5f+DockQuayLength)*.5f),direction=direction,side=side,halfX=(DockQuayLength-definition.width*.5f)*.5f,halfY=DockQuayHalfWidth};
        }
        private static float ProjectedHalfExtent(FootprintRectangle rectangle,Vector2 axis)=>Mathf.Abs(Vector2.Dot(rectangle.direction,axis))*rectangle.halfX+Mathf.Abs(Vector2.Dot(rectangle.side,axis))*rectangle.halfY;

        private string DockFootprintReason(Vector2 point)
        {
            var definition=Rules.Entity("dock");Vector2 direction=BuildingDirection(definition,point);
            foreach(var cell in RectangleCells(point,direction,definition.width,definition.depth))
                if(!Navigation.IsWalkable(cell,0)||IsWater(cell)||IsBridgeApproach(cell))return "Dock warehouse needs a clear land footprint";
            foreach(var cell in BuildingFootprintCells(definition,point))
            {
                if(IsBridgeApproach(cell))return "Keep bridge decks and approaches clear";
                if(!(IsWater(cell)?WaterNavigation:Navigation).IsWalkable(cell,0))return "Dock quay is blocked or outside the map";
            }
            return "";
        }

        public string DockPlacementReason(Vector2 point)
        {
            if(!Layout.HasWater)return "Docks require a water map";
            if(DockWaterDirection(point)==Vector2.zero)return "Place the dock on a clear riverbank, facing open water";
            return "";
        }

        // The existing overload remains the land/reward spawn contract. Ships
        // use only their producer's water-facing side and their own clearance.
        public bool FindSpawnPositions(EntityState receiver,int count,EntityDef unit,out List<Vector2> positions)
        {
            if(!unit.waterborne)return FindSpawnPositions(receiver,count,unit.radius,out positions);
            positions=new List<Vector2>();
            if(receiver==null||receiver.def.id!="dock"||!Layout.HasWater)return false;
            Vector2 direction=DockWaterDirection(receiver.position);if(direction==Vector2.zero)return false;
            Vector2 side=new Vector2(-direction.y,direction.x);
            var navigation=NavigationFor(unit);
            for(int ring=0;ring<5&&positions.Count<count;ring++)for(int slot=0;slot<9&&positions.Count<count;slot++)
            {
                int offset=slot==0?0:(slot+1)/2*(slot%2==1?1:-1);
                Vector2 point=receiver.position+direction*(DockQuayLength+unit.radius+.5f+ring*1.25f)+side*(offset*(unit.radius*2+.3f));
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
