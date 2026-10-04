using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // Shared authored geography. Water, navigation, picking, harbor placement
    // and presentation use one definition rather than independent river masks.
    public sealed class RiverChannel
    {
        public readonly Vector2[] Centers;
        public readonly float[] HalfWidths;
        public RiverChannel(Vector2[] centers,float[] halfWidths)
        {
            if(centers==null||halfWidths==null||centers.Length<2||centers.Length!=halfWidths.Length)throw new ArgumentException("A river requires matching center and width samples");
            Centers=(Vector2[])centers.Clone();HalfWidths=(float[])halfWidths.Clone();
        }
    }

    public sealed class BridgeSpan
    {
        public readonly Vector2 Center,Direction;
        public readonly float HalfLength,HalfWidth,RampLength,Height;
        public BridgeSpan(Vector2 center,Vector2 direction,float halfLength,float halfWidth=4.5f,float rampLength=9,float height=3.2f)
        {
            if(direction.sqrMagnitude<.99f||halfLength<=0||halfWidth<=1||rampLength<=0||height<=0)throw new ArgumentException("Invalid bridge span");
            Center=center;Direction=direction.normalized;HalfLength=halfLength;HalfWidth=halfWidth;RampLength=rampLength;Height=height;
        }
        public Vector2 Local(Vector2 point)
        {Vector2 delta=point-Center;return new Vector2(Vector2.Dot(delta,Direction),Vector2.Dot(delta,new Vector2(-Direction.y,Direction.x)));}
        public Vector2 Point(float across,float along)=>Center+Direction*across+new Vector2(-Direction.y,Direction.x)*along;
        public bool Contains(Vector2 point)
        {Vector2 local=Local(point);return Mathf.Abs(local.x)<=HalfLength&&Mathf.Abs(local.y)<=HalfWidth;}
        public bool Approach(Vector2 point)
        {Vector2 local=Local(point);return Mathf.Abs(local.x)<=HalfLength+RampLength&&Mathf.Abs(local.y)<=HalfWidth;}
        public bool SideBarrier(Vector2 point,float cellHalf=0)
        {
            Vector2 local=Local(point);float along=Mathf.Abs(local.y);
            float projection=cellHalf*(Mathf.Abs(Direction.x)+Mathf.Abs(Direction.y));
            // Terrain owns complete square cells. A slanted railing must also
            // block cells that intersect it when their centers sit on the deck;
            // otherwise actors can walk across the physical height discontinuity.
            return Mathf.Abs(local.x)<=HalfLength+RampLength+projection&&along>=HalfWidth-projection&&along<=HalfWidth+1.5f+projection;
        }
        public float GroundHeight(Vector2 point)
        {Vector2 local=Local(point);return Mathf.Abs(local.y)<=HalfWidth?Height*Mathf.Clamp01((HalfLength+RampLength-Mathf.Abs(local.x))/RampLength):0;}
        public bool FootprintOverlaps(Vector2 center,float width,float depth)
        {
            // Separating-axis test for an axis-aligned building and the full
            // rotated bridge rectangle (including ramp mouths).
            Vector2 perpendicular=new Vector2(-Direction.y,Direction.x),delta=center-Center;
            float x=width*.5f,y=depth*.5f,length=HalfLength+RampLength;
            if(Mathf.Abs(delta.x)>x+Mathf.Abs(Direction.x)*length+Mathf.Abs(perpendicular.x)*HalfWidth)return false;
            if(Mathf.Abs(delta.y)>y+Mathf.Abs(Direction.y)*length+Mathf.Abs(perpendicular.y)*HalfWidth)return false;
            if(Mathf.Abs(Vector2.Dot(delta,Direction))>length+Mathf.Abs(Direction.x)*x+Mathf.Abs(Direction.y)*y)return false;
            return Mathf.Abs(Vector2.Dot(delta,perpendicular))<=HalfWidth+Mathf.Abs(perpendicular.x)*x+Mathf.Abs(perpendicular.y)*y;
        }
    }

    public sealed partial class MapLayout
    {
        public readonly string Id;
        public readonly int Seed;
        public readonly bool Generated,RequiresTransport;
        public readonly MapStartLayout[] Starts;
        public readonly Vector2[] NeutralSites,ObjectiveSites,IslandLandingSites;
        public readonly Vector2[][] LandingSites;
        public readonly bool HasWater;
        public readonly float WaterHeight;
        public readonly RiverChannel[] Channels;
        public readonly BridgeSpan[] Bridges;
        public readonly Vector2[] FishPoints,PatrolPoints,TradeRoute;
        public readonly Vector2[][] DockSites;
        private const int Resolution=257;
        private const float Cell=.5f;
        private readonly float[] shore;
        private readonly Vector2[] nearestCenter;
        private static readonly Dictionary<string,MapLayout> layouts=new Dictionary<string,MapLayout>();
        internal MapLayout(string id,RiverChannel[] channels,BridgeSpan[] bridges,Vector2[] fish,Vector2[] patrol,Vector2[] trade,
            int seed=0,MapStartLayout[] starts=null,Vector2[] neutral=null,Vector2[] objectives=null,bool requiresTransport=false)
        {
            Seed=seed;Generated=MapTopology.IsGenerated(id);RequiresTransport=requiresTransport;
            Starts=starts??MapGeneration.AuthoredStarts();NeutralSites=neutral??Array.Empty<Vector2>();ObjectiveSites=objectives??Array.Empty<Vector2>();
            Id=id;HasWater=channels.Length>0;WaterHeight=id==MapTopology.Frontier?0:id==MapTopology.River?MapTopology.WaterHeight:-.4f;Channels=channels;Bridges=bridges;FishPoints=fish;PatrolPoints=patrol;TradeRoute=trade;
            if(HasWater&&id!=MapTopology.River)
            {
                shore=new float[Resolution*Resolution];nearestCenter=new Vector2[shore.Length];
                if(Generated)MapGeneration.BuildShoreCache(Channels,shore,nearestCenter,Resolution,Cell);
                else for(int z=0;z<Resolution;z++)for(int x=0;x<Resolution;x++)
                {
                    int index=x+z*Resolution;Vector2 point=new Vector2(x*Cell,z*Cell);float best=float.MaxValue;Vector2 center=Vector2.zero;
                    foreach(var channel in Channels)for(int i=0;i<channel.Centers.Length-1;i++)
                    {
                        Vector2 a=channel.Centers[i],delta=channel.Centers[i+1]-a;
                        float t=Mathf.Clamp01(Vector2.Dot(point-a,delta)/Mathf.Max(.000001f,delta.sqrMagnitude));
                        Vector2 candidate=a+delta*t;float distance=(point-candidate).magnitude-Mathf.Lerp(channel.HalfWidths[i],channel.HalfWidths[i+1],t);
                        if(distance<best){best=distance;center=candidate;}
                    }
                    shore[index]=best;nearestCenter[index]=center;
                }
            }
            DockSites=id==MapTopology.River?new[]{new[]{new Vector2(55,28),new Vector2(55,100)},new[]{new Vector2(73,100),new Vector2(73,28)}}:
                HasWater?new[]{BankSites(0),BankSites(1)}:new[]{Array.Empty<Vector2>(),Array.Empty<Vector2>()};
            if(Generated){DockSites[1]=MapGeneration.Mirror(DockSites[0]);LandingSites=MapGeneration.Landings(this);IslandLandingSites=RequiresTransport?MapGeneration.IslandLandings(this):Array.Empty<Vector2>();}
            else{LandingSites=new[]{Array.Empty<Vector2>(),Array.Empty<Vector2>()};IslandLandingSites=Array.Empty<Vector2>();}
        }
        public static MapLayout Create(string id)
        {
            if(!MapTopology.IsValid(id))throw new ArgumentException("Unknown map: "+id);
            if(MapTopology.IsGenerated(id))return Create(id,314159);
            lock(layouts){if(!layouts.TryGetValue(id,out var layout)){layout=Build(id);layouts.Add(id,layout);}return layout;}
        }
        public static MapLayout Create(string id,int seed)=>MapTopology.IsGenerated(id)?MapGeneration.Create(id,seed):Create(id);
        private float CachedShore(Vector2 point)
        {
            float px=Mathf.Clamp(point.x/Cell,0,Resolution-1),py=Mathf.Clamp(point.y/Cell,0,Resolution-1);
            int x=Mathf.Min(Resolution-2,Mathf.FloorToInt(px)),y=Mathf.Min(Resolution-2,Mathf.FloorToInt(py));float tx=px-x,ty=py-y;int i=x+y*Resolution;
            return Mathf.Lerp(Mathf.Lerp(shore[i],shore[i+1],tx),Mathf.Lerp(shore[i+Resolution],shore[i+Resolution+1],tx),ty);
        }
        public float SignedBankDistance(Vector2 point)
        {
            if(!HasWater)return 128;
            if(Id==MapTopology.River)return Mathf.Max(MapTopology.RiverMinX-point.x,point.x-MapTopology.RiverMaxX);
            return CachedShore(point);
        }
        public bool IsWater(Vector2 point)=>HasWater&&point.x>=0&&point.x<=128&&point.y>=0&&point.y<=128&&(Id==MapTopology.River?MapTopology.IsWater(point):CachedShore(point)<=0);
        public bool IsBridge(Vector2 point)
        {if(Id==MapTopology.River)return MapTopology.IsBridge(point);foreach(var span in Bridges)if(span.Contains(point))return true;return false;}
        public bool IsBridgeApproach(Vector2 point)
        {if(Id==MapTopology.River)return MapTopology.IsBridgeApproach(point);foreach(var span in Bridges)if(span.Approach(point))return true;return false;}
        public bool IsBridgeSideBarrier(Vector2 point)
        {if(Id==MapTopology.River)return MapTopology.IsBridgeSideBarrier(point);foreach(var span in Bridges)if(span.SideBarrier(point))return true;return false;}
        public bool IsBridgeSideBarrierCell(Vector2 point)
        {if(Id==MapTopology.River)return MapTopology.IsBridgeSideBarrier(point);foreach(var span in Bridges)if(span.SideBarrier(point,.5f))return true;return false;}
        public float BankHeight(Vector2 point)
        {
            if(!HasWater||Id==MapTopology.River)return 0;
            float distance=CachedShore(point);
            if(distance<=0)return Mathf.Lerp(WaterHeight-.02f,-.9f,Mathf.SmoothStep(0,1,-distance/4));
            return (WaterHeight-.02f)*(1-Mathf.SmoothStep(0,1,distance/3));
        }
        public float GroundHeight(Vector2 point)
        {
            if(Id==MapTopology.River)return MapTopology.GroundHeight(point);
            foreach(var span in Bridges)if(span.Approach(point))return span.GroundHeight(point);
            return BankHeight(point);
        }
        public bool BridgeFootprintOverlaps(Vector2 center,float width,float depth)
        {foreach(var span in Bridges)if(span.FootprintOverlaps(center,width,depth))return true;return false;}
        public Vector2 WaterDirection(Vector2 point)
        {
            if(!HasWater)return Vector2.zero;
            if(Id==MapTopology.River)return point.x<64?Vector2.right:Vector2.left;
            int x=Mathf.Clamp(Mathf.RoundToInt(point.x/Cell),0,Resolution-1),y=Mathf.Clamp(Mathf.RoundToInt(point.y/Cell),0,Resolution-1);
            return (nearestCenter[x+y*Resolution]-point).normalized;
        }
        private Vector2[] BankSites(int player)
        {
            var output=new List<Vector2>();float preferred=Generated?Starts[player].Town.y:player==0?25:103;
            // Pick integer building centers after testing every cell of the
            // same 4x4 footprint used by construction. Curved shores therefore
            // do not yield deceptively legal-looking, unbuildable AI targets.
            foreach(float target in new[]{preferred,128-preferred})
            {
                Vector2 chosen=Vector2.zero;float score=float.MaxValue;
                for(int z=12;z<=116;z++)for(int x=12;x<=116;x++)
                {
                    var point=new Vector2(x,z);float distance=SignedBankDistance(point);if(distance<2.6f||distance>4.4f||IsWater(point)||BridgeFootprintOverlaps(point,8,8))continue;
                    if(Generated&&!InStartingRegion(player,point))continue;
                    Vector2 direction=WaterDirection(point);if(player==0?direction.x<.25f:direction.x>-.25f)continue;
                    if(!HarborLandClear(point,direction))continue;
                    Vector2 front=point+direction*5,side=new Vector2(-direction.y,direction.x)*1.5f;
                    if(!IsWater(front)||!IsWater(front+side)||!IsWater(front-side)||IsBridgeApproach(front)||IsBridgeApproach(front+side)||IsBridgeApproach(front-side))continue;
                    Vector2 shipExit=point+direction*6.4f;
                    if(SignedBankDistance(shipExit)>-1.7f||IsBridgeApproach(shipExit))continue;
                    float candidate=Mathf.Abs(z-target)*2+Mathf.Abs(distance-3.1f)+Mathf.Abs(direction.y)*.3f;
                    if(candidate<score){score=candidate;chosen=point;}
                }
                if(score<float.MaxValue&&!output.Contains(chosen))output.Add(chosen);
            }
            return output.ToArray();
        }
        private bool HarborLandClear(Vector2 center,Vector2 direction)
        {
            Vector2 side=new Vector2(-direction.y,direction.x);float projection=(Mathf.Abs(direction.x)+Mathf.Abs(direction.y))*.5f;
            float extent=2*(Mathf.Abs(direction.x)+Mathf.Abs(direction.y));
            for(int y=Mathf.CeilToInt(center.y-extent-.5f+.0001f);y<=Mathf.FloorToInt(center.y+extent+.5f-.0001f);y++)
                for(int x=Mathf.CeilToInt(center.x-extent-.5f+.0001f);x<=Mathf.FloorToInt(center.x+extent+.5f-.0001f);x++)
                {
                    Vector2 point=new Vector2(x,y),delta=point-center;
                    if(Mathf.Abs(Vector2.Dot(delta,direction))>=2+projection-.0001f||Mathf.Abs(Vector2.Dot(delta,side))>=2+projection-.0001f)continue;
                    if(IsWater(point)||IsBridgeApproach(point)||IsBridgeSideBarrier(point))return false;
                    if(Generated)
                    {
                        if(IsBridgeSideBarrierCell(point))return false;
                        foreach(var start in Starts)
                            if(Mathf.Abs(point.x-start.Town.x)<=3&&Mathf.Abs(point.y-start.Town.y)<=3)return false;
                    }
                }
            if(Generated)
            {
                // The quay occupies complete cells too. Its far tip can clear
                // the mesh while an intervening rotated cell hits a railing.
                Vector2 quay=center+direction*3.5f;float quayExtent=1.5f*(Mathf.Abs(direction.x)+Mathf.Abs(direction.y));
                for(int y=Mathf.CeilToInt(quay.y-quayExtent-.5f+.0001f);y<=Mathf.FloorToInt(quay.y+quayExtent+.5f-.0001f);y++)
                    for(int x=Mathf.CeilToInt(quay.x-quayExtent-.5f+.0001f);x<=Mathf.FloorToInt(quay.x+quayExtent+.5f-.0001f);x++)
                    {
                        var p=new Vector2(x,y);var delta=p-quay;
                        if(Mathf.Abs(Vector2.Dot(delta,direction))>=1.5f+projection-.0001f||Mathf.Abs(Vector2.Dot(delta,side))>=1.5f+projection-.0001f)continue;
                        if(IsBridgeApproach(p)||!IsWater(p)&&IsBridgeSideBarrierCell(p))return false;
                    }
            }
            return true;
        }
        private static MapLayout Build(string id)
        {
            if(id==MapTopology.Frontier)return new MapLayout(id,Array.Empty<RiverChannel>(),Array.Empty<BridgeSpan>(),Array.Empty<Vector2>(),Array.Empty<Vector2>(),Array.Empty<Vector2>());
            if(id==MapTopology.River)return new MapLayout(id,new[]{new RiverChannel(new[]{new Vector2(64,0),new Vector2(64,128)},new[]{6.5f,6.5f})},
                new[]{new BridgeSpan(new Vector2(64,40),Vector2.right,6.5f,4.5f,8),new BridgeSpan(new Vector2(64,88),Vector2.right,6.5f,4.5f,8)},
                new[]{new Vector2(64,22),new Vector2(64,55),new Vector2(64,73),new Vector2(64,106)},new[]{new Vector2(64,28),new Vector2(64,100)},
                new[]{new Vector2(18,76),new Vector2(44,76),new Vector2(47,88),new Vector2(81,88),new Vector2(81,64),new Vector2(84,52),new Vector2(110,52)});
            Vector2[] control;float[] widths;RiverChannel[] channels;
            if(id==MapTopology.Tropical)
            {
                control=new[]{new Vector2(67,-8),new Vector2(59,12),new Vector2(51,27),new Vector2(57,42),new Vector2(75,60),new Vector2(72,77),new Vector2(55,94),new Vector2(53,111),new Vector2(61,136)};
                widths=new[]{6f,6,7,6,12,10,7,8,7};
            }
            else if(id==MapTopology.Estuary)
            {
                control=new[]{new Vector2(60,-8),new Vector2(52,15),new Vector2(58,31),new Vector2(69,47),new Vector2(73,64),new Vector2(63,79),new Vector2(56,99),new Vector2(60,117),new Vector2(63,153)};
                widths=new[]{6f,6,6.5f,7,8,10,14,19,37};
            }
            else
            {
                control=new[]{new Vector2(60,-8),new Vector2(56,12),new Vector2(51,25),new Vector2(56,37),new Vector2(68,50),new Vector2(78,64),new Vector2(76,76),new Vector2(63,88),new Vector2(49,100),new Vector2(51,113),new Vector2(58,136)};
                widths=new[]{6f,6,7,6,7,9,8,7,8,7,6};
            }
            var main=Sample(control,widths);
            if(id==MapTopology.Tributaries)
            {
                var tributary=Sample(new[]{new Vector2(30,136),new Vector2(25,115),new Vector2(26,96),new Vector2(32,82),new Vector2(37,70),new Vector2(52,61),new Vector2(68,60),new Vector2(77,64)},new[]{4f,4,4.5f,5,5,5,5,5});
                channels=new[]{main,tributary};
            }
            else channels=new[]{main};
            var south=BridgeAt(main,id==MapTopology.Estuary?34:id==MapTopology.Tributaries?31:39);var north=BridgeAt(main,id==MapTopology.Estuary?74:89);
            var bridges=new List<BridgeSpan>{south,north};
            if(id==MapTopology.Tributaries)bridges.Add(BridgeAt(channels[1],112));
            var fish=new List<Vector2>();foreach(float z in id==MapTopology.Estuary?new[]{20f,55,92,108}:new[]{20f,56,74,108})fish.Add(CenterAt(main,z));
            if(id==MapTopology.Tropical){fish.Add(CenterAt(main,63)+new Vector2(4,0));fish.Add(CenterAt(main,78)+new Vector2(-3,1));}
            if(id==MapTopology.Tributaries){fish.Add(CenterAt(channels[1],124));fish.Add(CenterAt(channels[1],93));}
            if(id==MapTopology.Estuary){fish.Add(CenterAt(main,113)+new Vector2(-8,0));fish.Add(CenterAt(main,114)+new Vector2(8,0));}
            // Approach the bridge along its axis. The bank bends do not let a
            // caravan enter the elevated deck through an unguarded ramp side.
            float mouth=north.HalfLength+north.RampLength+3;
            var crossing=id==MapTopology.Tributaries?south:north;mouth=crossing.HalfLength+crossing.RampLength+3;
            var trade=new[]{new Vector2(18,66),id==MapTopology.Tributaries?new Vector2(30,52):new Vector2(25,78),crossing.Point(-mouth,0),crossing.Point(-crossing.HalfLength,0),crossing.Center,crossing.Point(crossing.HalfLength,0),crossing.Point(mouth,0),new Vector2(108,72),new Vector2(111,53)};
            return new MapLayout(id,channels,bridges.ToArray(),fish.ToArray(),new[]{CenterAt(main,24),CenterAt(main,104)},trade);
        }
        internal static Vector2 CenterAt(RiverChannel channel,float z)
        {
            for(int i=0;i<channel.Centers.Length-1;i++)
            {
                Vector2 a=channel.Centers[i],b=channel.Centers[i+1];if(z>=Mathf.Min(a.y,b.y)&&z<=Mathf.Max(a.y,b.y))return Vector2.Lerp(a,b,Mathf.Abs(b.y-a.y)<.0001f?0:(z-a.y)/(b.y-a.y));
            }
            return channel.Centers[channel.Centers.Length/2];
        }
        internal static BridgeSpan BridgeAt(RiverChannel channel,float z)
        {
            Vector2 center=CenterAt(channel,z),tangent=(CenterAt(channel,z+1)-CenterAt(channel,z-1)).normalized;
            Vector2 across=new Vector2(tangent.y,-tangent.x);if(across.x<0)across=-across;
            float width=6;
            for(int i=0;i<channel.Centers.Length-1;i++)if(z>=Mathf.Min(channel.Centers[i].y,channel.Centers[i+1].y)&&z<=Mathf.Max(channel.Centers[i].y,channel.Centers[i+1].y))
            {float t=(z-channel.Centers[i].y)/(channel.Centers[i+1].y-channel.Centers[i].y);width=Mathf.Lerp(channel.HalfWidths[i],channel.HalfWidths[i+1],t);break;}
            // Two meters of abutment land avoid tiny dry wedges at the edge of
            // a curved bank. The deck still spans every water cell under it.
            return new BridgeSpan(center,across,width+2,4.8f,9,3.2f);
        }
        internal static RiverChannel Sample(Vector2[] points,float[] widths)
        {
            var output=new List<Vector2>();var halfWidths=new List<float>();
            for(int i=0;i<points.Length-1;i++)
            {
                Vector2 a=points[Mathf.Max(0,i-1)],b=points[i],c=points[i+1],d=points[Mathf.Min(points.Length-1,i+2)];
                int steps=Mathf.CeilToInt(Vector2.Distance(b,c)*2);
                for(int step=0;step<steps;step++)
                {
                    float t=(float)step/steps,t2=t*t,t3=t2*t;
                    output.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t2+(-a+3*b-3*c+d)*t3));
                    halfWidths.Add(Mathf.Lerp(widths[i],widths[i+1],Mathf.SmoothStep(0,1,t)));
                }
            }
            output.Add(points[points.Length-1]);halfWidths.Add(widths[widths.Length-1]);return new RiverChannel(output.ToArray(),halfWidths.ToArray());
        }
    }
}
