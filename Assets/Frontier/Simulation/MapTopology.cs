using System;
using UnityEngine;

namespace Frontier
{
    // Authored grid cells are centred on integers, with half-integer outer
    // edges. Rendering and interaction consume these same physical bounds.
    public static class MapTopology
    {
        public const string Frontier="frontier",River="river",Winding="winding",Tropical="tropical",Tributaries="tributaries",Estuary="estuary";
        public const string GeneratedRiver="generated_river",GeneratedDelta="generated_delta",GeneratedStrait="generated_strait";
        public const string Scenario="custom_scenario";
        public static readonly string[] Authored={Frontier,River,Winding,Tropical,Tributaries,Estuary};
        public static readonly string[] Generated={GeneratedRiver,GeneratedDelta,GeneratedStrait};
        public static readonly string[] All={Frontier,River,Winding,Tropical,Tributaries,Estuary,GeneratedRiver,GeneratedDelta,GeneratedStrait};
        public static bool IsGenerated(string id)=>id==GeneratedRiver||id==GeneratedDelta||id==GeneratedStrait;
        public static bool IsValid(string id)=>id==Frontier||IsRiverMap(id);
        public static bool IsRiverMap(string id)=>id==River||id==Winding||id==Tropical||id==Tributaries||id==Estuary||IsGenerated(id);
        public static string Name(string id)
        {
            switch(id)
            {
                case Frontier:return "Frontier Plains";case River:return "River Crossing";
                case Winding:return "Winding River Valley";case Tropical:return "Tropical River Bay";
                case Tributaries:return "Confluence Rivers";case Estuary:return "River Estuary";
                case GeneratedRiver:return "Seeded River Valley";case GeneratedDelta:return "Seeded Braided Delta";case GeneratedStrait:return "Seeded Island Straits";
                default:return id??"";
            }
        }
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
        public bool IsWater(Vector2 point)=>Layout.IsWater(point);
        public bool IsBridge(Vector2 point)=>Layout.IsBridge(point);
        public bool IsBridgeApproach(Vector2 point)=>Layout.IsBridgeApproach(point);
        public bool BridgeFootprintOverlaps(Vector2 center,float width,float depth)=>Layout.BridgeFootprintOverlaps(center,width,depth);
        public float SurfaceHeight(Vector2 point,bool waterborne=false)=>waterborne&&Layout.HasWater?Layout.WaterHeight:Layout.GroundHeight(point);
        private void InitializeNavigationTerrain()
        {
            Navigation.ConfigureTerrain(point=>IsWater(point)&&!IsBridge(point)||Layout.IsBridgeSideBarrierCell(point)||Layout.IsTerrainBlocked(point));
            WaterNavigation.ConfigureTerrain(point=>!IsWater(point));
        }
        private Vector2[] MapTradeRoute()
        {
            if(HasScenario)return (Vector2[])Layout.TradeRoute.Clone();
            if(Layout.HasWater)return (Vector2[])Layout.TradeRoute.Clone();
            return Rules.landmarks?.route==null?Array.Empty<Vector2>():(Vector2[])Rules.landmarks.route.Clone();
        }
        private Vector2 MapSocketPosition(TradeSocketDef socket)
        {
            if(Layout.Generated)
            {
                int index=Array.IndexOf(Rules.landmarks.sockets,socket);
                return Layout.NeutralSites[Mathf.Clamp(index,0,Layout.NeutralSites.Length-1)];
            }
            if(MapId==MapTopology.River)return socket.waypoint==2?new Vector2(81,67):socket.position;
            if(!Layout.HasWater)return socket.position;
            // Three sockets remain on bank stretches, beyond either ramp mouth.
            int waypoint=MapSocketWaypoint(socket);return TradeRoute[waypoint]+new Vector2(0,3);
        }
        private int MapSocketWaypoint(TradeSocketDef socket)
        {
            if(Layout.Generated)
            {
                Vector2 point=MapSocketPosition(socket);int closest=0;float distance=float.MaxValue;
                for(int i=0;i<TradeRoute.Length;i++){float candidate=(point-TradeRoute[i]).sqrMagnitude;if(candidate<distance){distance=candidate;closest=i;}}
                return closest;
            }
            return MapId==MapTopology.River?(socket.waypoint==2?4:socket.waypoint==3?5:socket.waypoint):Layout.HasWater?(socket.waypoint<=1?1:socket.waypoint==2?TradeRoute.Length-3:TradeRoute.Length-1):socket.waypoint;
        }
        public bool CanAttackTarget(EntityState actor,EntityState target)
        {
            if(actor==null||target==null||!actor.Alive||!target.Alive||target.def.kind==EntityKind.Resource||actor.def.civilian||actor.def.damage<=0||!IsHostileTarget(actor.owner,target))return false;
            if(actor.def.waterborne==target.def.waterborne)return true;
            // Weapons cannot make a melee contact between the bridge/shore and
            // water channels. A musket close to a boat keeps its ranged shot.
            return !actor.forceMelee&&actor.def.range>2&&actor.def.damageKind!=DamageKind.Melee;
        }
        private bool UsesMelee(EntityState actor,EntityState target,float distance)=>actor.def.waterborne==target.def.waterborne&&(actor.forceMelee||distance<1.5f)&&actor.def.meleeDamage>0;
    }
}
