using System;
using UnityEngine;

namespace Frontier
{
    // Authored grid cells are centred on integers, with half-integer outer
    // edges. Rendering and interaction consume these same physical bounds.
    public static class MapTopology
    {
        public const string Frontier="frontier",River="river";
        public const float RiverMinX=57.5f,RiverMaxX=70.5f,WaterHeight=.02f;
        public const float BridgeSouth=40,BridgeNorth=88,BridgeHalfWidth=4.5f;
        public const float BridgeHeight=3.2f,BridgeRampLength=8;
        public static bool IsWater(Vector2 point)=>point.x>=RiverMinX&&point.x<=RiverMaxX&&point.y>=0&&point.y<=128;
        public static bool InBridgeBand(Vector2 point)=>Mathf.Abs(point.y-BridgeSouth)<=BridgeHalfWidth||Mathf.Abs(point.y-BridgeNorth)<=BridgeHalfWidth;
        public static bool IsBridge(Vector2 point)=>IsWater(point)&&InBridgeBand(point);
        public static bool IsBridgeApproach(Vector2 point)=>InBridgeBand(point)&&point.x>=RiverMinX-BridgeRampLength&&point.x<=RiverMaxX+BridgeRampLength;
        public static bool IsBridgeSideBarrier(Vector2 point)
        {
            if(point.x<RiverMinX-BridgeRampLength||point.x>RiverMaxX+BridgeRampLength)return false;
            float south=Mathf.Abs(point.y-BridgeSouth),north=Mathf.Abs(point.y-BridgeNorth);
            // One terrain-cell row immediately outside each railing closes the
            // ramp sides. Actors approach the zero-height mouths at either end.
            return south>=BridgeHalfWidth&&south<=BridgeHalfWidth+1||north>=BridgeHalfWidth&&north<=BridgeHalfWidth+1;
        }
        public static float GroundHeight(Vector2 point)
        {
            if(!InBridgeBand(point))return 0;
            if(point.x<RiverMinX)return BridgeHeight*Mathf.Clamp01((point.x-(RiverMinX-BridgeRampLength))/BridgeRampLength);
            if(point.x>RiverMaxX)return BridgeHeight*Mathf.Clamp01(((RiverMaxX+BridgeRampLength)-point.x)/BridgeRampLength);
            return BridgeHeight;
        }
    }

    public partial class World
    {
        public readonly string MapId;
        public readonly GridNavigation WaterNavigation;
        public readonly Vector2[] TradeRoute;
        public GridNavigation NavigationFor(EntityDef definition)=>definition!=null&&definition.waterborne?WaterNavigation:Navigation;
        public GridNavigation NavigationFor(EntityState entity)=>NavigationFor(entity?.def);
        public bool IsWater(Vector2 point)=>MapId==MapTopology.River&&MapTopology.IsWater(point);
        public bool IsBridge(Vector2 point)=>MapId==MapTopology.River&&MapTopology.IsBridge(point);
        public bool IsBridgeApproach(Vector2 point)=>MapId==MapTopology.River&&MapTopology.IsBridgeApproach(point);
        public bool BridgeFootprintOverlaps(Vector2 center,float width,float depth)
        {
            if(MapId!=MapTopology.River||center.x+width*.5f<MapTopology.RiverMinX-MapTopology.BridgeRampLength||center.x-width*.5f>MapTopology.RiverMaxX+MapTopology.BridgeRampLength)return false;
            return Mathf.Abs(center.y-MapTopology.BridgeSouth)<=MapTopology.BridgeHalfWidth+depth*.5f||Mathf.Abs(center.y-MapTopology.BridgeNorth)<=MapTopology.BridgeHalfWidth+depth*.5f;
        }
        public float SurfaceHeight(Vector2 point,bool waterborne=false)=>MapId==MapTopology.River?(waterborne?MapTopology.WaterHeight:MapTopology.GroundHeight(point)):0;
        private void InitializeNavigationTerrain()
        {
            Navigation.ConfigureTerrain(point=>IsWater(point)&&!IsBridge(point)||MapId==MapTopology.River&&MapTopology.IsBridgeSideBarrier(point));
            WaterNavigation.ConfigureTerrain(point=>!IsWater(point));
        }
        private Vector2[] MapTradeRoute()
        {
            if(MapId==MapTopology.River)return new[]{new Vector2(18,76),new Vector2(44,76),new Vector2(47,88),new Vector2(81,88),new Vector2(81,64),new Vector2(84,52),new Vector2(110,52)};
            return Rules.landmarks?.route==null?Array.Empty<Vector2>():(Vector2[])Rules.landmarks.route.Clone();
        }
        private Vector2 MapSocketPosition(TradeSocketDef socket)=>MapId==MapTopology.River&&socket.waypoint==2?new Vector2(81,67):socket.position;
        private int MapSocketWaypoint(TradeSocketDef socket)=>MapId==MapTopology.River?(socket.waypoint==2?4:socket.waypoint==3?5:socket.waypoint):socket.waypoint;
        public bool CanAttackTarget(EntityState actor,EntityState target)
        {
            if(actor==null||target==null||!actor.Alive||!target.Alive||target.def.kind==EntityKind.Resource||actor.def.civilian||actor.def.damage<=0)return false;
            if(actor.def.waterborne==target.def.waterborne)return true;
            // Weapons cannot make a melee contact between the bridge/shore and
            // water channels. A musket close to a boat keeps its ranged shot.
            return !actor.forceMelee&&actor.def.range>2&&actor.def.damageKind!=DamageKind.Melee;
        }
        private bool UsesMelee(EntityState actor,EntityState target,float distance)=>actor.def.waterborne==target.def.waterborne&&(actor.forceMelee||distance<1.5f)&&actor.def.meleeDamage>0;
    }
}
