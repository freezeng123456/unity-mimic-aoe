using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public sealed class MapResourceAnchor
    {
        public readonly string Definition;
        public readonly Vector2 Position;
        public readonly bool Owned;
        public MapResourceAnchor(string definition,Vector2 position,bool owned=false){Definition=definition;Position=position;Owned=owned;}
    }
    public sealed class MapStartLayout
    {
        public readonly Vector2 Town,Explorer;
        public readonly int Player;
        public readonly MapResourceAnchor[] Resources;
        public MapStartLayout(int player,Vector2 town,int resources=0,Vector2? explorer=null)
        {Player=player;Town=town;Explorer=explorer??town+new Vector2(7,4)*(player==0?1:-1);Resources=new MapResourceAnchor[resources];}
        public Vector2 WorkerPoint(int index)=>Town+new Vector2(5+index%3*1.3f,-3+index/3*1.4f)*(Player==0?1:-1);
    }

    // Versioned, local deterministic generation. No Unity/global random state,
    // unordered hash iteration or fixed-layout selection enters the terrain.
    public static class MapGeneration
    {
        public const int Version=1;
        private const int CacheLimit=12;
        private static readonly Dictionary<string,MapLayout> cache=new Dictionary<string,MapLayout>();
        private static readonly Queue<string> cacheOrder=new Queue<string>();
        private sealed class RandomSequence
        {
            private uint state;
            public RandomSequence(int seed,uint salt){state=unchecked((uint)seed)^salt;for(int i=0;i<4;i++)Next();}
            private uint Next()
            {
                // Full-state mixing also supports the seed whose salted state
                // is zero; no fallback seed aliases another generated world.
                state=unchecked(state+0x9e3779b9u);uint value=state;
                value=unchecked((value^(value>>16))*0x85ebca6bu);
                value=unchecked((value^(value>>13))*0xc2b2ae35u);return value^(value>>16);
            }
            public float Range(float a,float b)=>a+(b-a)*((Next()>>8)*(1f/16777216));
            public int Range(int a,int b)=>a+(int)(Next()%(uint)(b-a));
        }
        public static MapLayout Create(string id,int seed)
        {
            if(!MapTopology.IsGenerated(id))throw new ArgumentException("Not a generated map: "+id);
            string key=id+":"+seed.ToString(System.Globalization.CultureInfo.InvariantCulture);
            lock(cache)
            {
                if(cache.TryGetValue(key,out var known))return known;
                var layout=Generate(id,seed);cache.Add(key,layout);cacheOrder.Enqueue(key);
                while(cacheOrder.Count>CacheLimit)cache.Remove(cacheOrder.Dequeue());return layout;
            }
        }
        internal static MapStartLayout[] AuthoredStarts()=>new[]{new MapStartLayout(0,new Vector2(24,28)),new MapStartLayout(1,new Vector2(104,100))};
        internal static Vector2 Flip(Vector2 p)=>new Vector2(128-p.x,128-p.y);
        internal static Vector2[] Mirror(Vector2[] points){var result=new Vector2[points.Length];for(int i=0;i<points.Length;i++)result[i]=Flip(points[i]);return result;}
        private static RiverChannel Opposite(RiverChannel channel)
        {
            var points=new Vector2[channel.Centers.Length];var widths=new float[points.Length];
            for(int i=0;i<points.Length;i++){points[i]=Flip(channel.Centers[points.Length-1-i]);widths[i]=channel.HalfWidths[points.Length-1-i];}
            return new RiverChannel(points,widths);
        }
        private static RiverChannel Meander(RandomSequence random,float center,float amplitude,float width)
        {
            float second=random.Range(-3f,3f),slant=random.Range(-2f,2f),widen=random.Range(.3f,1.6f);
            var points=new Vector2[145];var widths=new float[145];
            // Odd center displacement / even width: a 180-degree paired opening
            // retains equal coastline lengths, route opportunities and resource amounts.
            for(int i=0;i<points.Length;i++)
            {
                float y=i-8,a=(y-64)*Mathf.PI/64;
                points[i]=new Vector2(center+amplitude*Mathf.Sin(a)+second*Mathf.Sin(2*a)+slant*(y-64)/64,y);
                widths[i]=width+widen*(.5f+.5f*Mathf.Cos(2*a));
            }
            return new RiverChannel(points,widths);
        }
        private static RiverChannel Connector(RiverChannel left,RiverChannel right,float y,float width,float bend)
        {
            var a=MapLayout.CenterAt(left,y);var b=MapLayout.CenterAt(right,y);int count=Mathf.CeilToInt(Vector2.Distance(a,b))+1;
            var points=new Vector2[count];var widths=new float[count];
            for(int i=0;i<count;i++){float t=(float)i/(count-1);points[i]=Vector2.Lerp(a,b,t)+Vector2.up*(Mathf.Sin(t*Mathf.PI)*bend);widths[i]=width;}
            return new RiverChannel(points,widths);
        }
        private static MapLayout Generate(string id,int seed)
        {
            bool strait=id==MapTopology.GeneratedStrait,delta=id==MapTopology.GeneratedDelta;
            var random=new RandomSequence(seed,strait?0xa341316cu:delta?0xc8013ea4u:0xad90777du);
            Vector2 town=new Vector2(random.Range(20,strait?24:27),random.Range(25,32));
            var starts=new[]{new MapStartLayout(0,town,43),new MapStartLayout(1,Flip(town),43)};
            var channels=new List<RiverChannel>();var bridges=new List<BridgeSpan>();
            RiverChannel main;
            if(strait)
            {
                main=Meander(random,43,random.Range(2.5f,4.5f),random.Range(7.5f,8.5f));var right=Opposite(main);
                channels.Add(main);channels.Add(right);
                var north=Connector(main,right,8,random.Range(5.5f,7f),random.Range(-1.2f,1.2f));channels.Add(north);channels.Add(Opposite(north));
            }
            else
            {
                main=Meander(random,64,random.Range(7f,14f),random.Range(5.5f,7f));channels.Add(main);
                float south=random.Range(26f,31f);bridges.Add(MapLayout.BridgeAt(main,south));bridges.Add(MapLayout.BridgeAt(main,128-south));
                if(delta)
                {
                    float reach=random.Range(26f,31f),spread=random.Range(14f,18f),branchWidth=random.Range(4.5f,5.3f);
                    var controls=new[]{MapLayout.CenterAt(main,46),new Vector2(64-reach,64-spread*.5f),new Vector2(64-reach-random.Range(0f,3f),64),new Vector2(64-reach,64+spread*.5f),MapLayout.CenterAt(main,82)};
                    var branch=MapLayout.Sample(controls,new[]{branchWidth,branchWidth,branchWidth+.5f,branchWidth,branchWidth});channels.Add(branch);channels.Add(Opposite(branch));
                }
            }
            var fish=new List<Vector2>();
            foreach(float y in new[]{17f,51,77,111})
            {fish.Add(MapLayout.CenterAt(main,y));if(strait)fish.Add(MapLayout.CenterAt(channels[1],y));}
            if(delta){fish.Add(MapLayout.CenterAt(channels[1],64));fish.Add(MapLayout.CenterAt(channels[2],64));}
            Vector2[] trade,neutral,objectives;
            if(strait)
            {
                trade=new Vector2[9];for(int i=0;i<trade.Length;i++){float y=24+i*10;trade[i]=(MapLayout.CenterAt(channels[0],y)+MapLayout.CenterAt(channels[1],y))*.5f;}
                neutral=new[]{trade[1],trade[4],trade[7]};
                objectives=new[]{trade[2],trade[6],trade[3],trade[5],trade[1]+new Vector2(6,0),trade[7]-new Vector2(6,0)};
            }
            else
            {
                var bridge=bridges[0];var north=bridges[1];float mouth=bridge.HalfLength+bridge.RampLength+4;
                trade=new[]{new Vector2(20,64),new Vector2(20,16),new Vector2(30,16),bridge.Point(-mouth,0),bridge.Point(-bridge.HalfLength,0),bridge.Center,bridge.Point(bridge.HalfLength,0),bridge.Point(mouth,0),new Vector2(108,16),new Vector2(108,64),new Vector2(108,112),new Vector2(98,112),north.Point(mouth,0),north.Point(north.HalfLength,0),north.Center,north.Point(-north.HalfLength,0),north.Point(-mouth,0),new Vector2(20,112),new Vector2(20,64)};
                neutral=new[]{new Vector2(16,64),new Vector2(112,64),new Vector2(16,16),new Vector2(112,112)};
                objectives=new[]{bridges[0].Point(-mouth-4,0),bridges[1].Point(mouth+4,0),new Vector2(16,48),new Vector2(112,80),new Vector2(16,80),new Vector2(112,48)};
            }
            var patrol=strait?new[]{MapLayout.CenterAt(main,24),MapLayout.CenterAt(channels[1],104)}:new[]{MapLayout.CenterAt(main,18),MapLayout.CenterAt(main,110)};
            var layout=new MapLayout(id,channels.ToArray(),bridges.ToArray(),fish.ToArray(),patrol,trade,seed,starts,neutral,objectives,strait);
            ResolveOpeningExplorer(layout);BuildResources(layout,random);ResolveCampSites(layout);return layout;
        }
        private static void ResolveCampSites(MapLayout layout)
        {
            // A bridge's outer mouth can lie beside the opening worker cluster.
            // Treasure camps need their own safe land choice, including guard
            // offsets and the ordinary routes to both harbor opportunities.
            var navigation=new GridNavigation();navigation.ConfigureTerrain(p=>layout.IsWater(p)&&!layout.IsBridge(p)||layout.IsBridgeSideBarrierCell(p));
            foreach(var start in layout.Starts)navigation.SetBlocked(start.Town,3,true);
            var corridors=new List<List<Vector2>>();
            for(int player=0;player<layout.Starts.Length;player++)
                foreach(var shore in Join(layout.DockSites[player],layout.LandingSites[player]))
                {
                    var end=navigation.NearestWalkable(shore,.4f);var path=navigation.FindPath(layout.Starts[player].WorkerPoint(0),end,.4f);
                    if(path.Count>0)corridors.Add(path);
                }
            var chosen=new List<Vector2>();
            bool Safe(Vector2 point)
            {
                if(!navigation.IsAreaFree(point,7,7)||layout.BridgeFootprintOverlaps(point,8,8))return false;
                foreach(var start in layout.Starts)
                {
                    if(Vector2.Distance(point,start.Town)<22||Vector2.Distance(point,start.Explorer)<20)return false;
                    for(int worker=0;worker<9;worker++)if(Vector2.Distance(point,start.WorkerPoint(worker))<20)return false;
                    foreach(var resource in start.Resources)if(Vector2.Distance(point,resource.Position)<6)return false;
                }
                foreach(var site in layout.NeutralSites)if(Vector2.Distance(point,site)<16)return false;
                foreach(var site in layout.IslandLandingSites)if(Vector2.Distance(point,site)<16)return false;
                foreach(var path in corridors)for(int i=1;i<path.Count;i++)
                {
                    Vector2 delta=path[i]-path[i-1];float t=Mathf.Clamp01(Vector2.Dot(point-path[i-1],delta)/Mathf.Max(.000001f,delta.sqrMagnitude));
                    if(Vector2.Distance(point,path[i-1]+delta*t)<16)return false;
                }
                foreach(var other in chosen)if(Vector2.Distance(point,other)<16)return false;
                return true;
            }
            for(int pair=0;pair<layout.ObjectiveSites.Length;pair+=2)
            {
                Vector2 desired=layout.ObjectiveSites[pair],bestPoint=Vector2.zero;float best=float.MaxValue;
                for(int y=7;y<=121;y++)for(int x=7;x<=121;x++)
                {
                    var point=new Vector2(x,y);float score=(point-desired).sqrMagnitude;
                    if(score>=best||!Safe(point)||!Safe(Flip(point))||Vector2.Distance(point,Flip(point))<16)continue;
                    best=score;bestPoint=point;
                }
                if(best==float.MaxValue)throw new InvalidOperationException("Generated map has no safe paired camp: "+layout.Id+" / "+layout.Seed+" / "+pair);
                layout.ObjectiveSites[pair]=bestPoint;chosen.Add(bestPoint);
                if(pair+1<layout.ObjectiveSites.Length){layout.ObjectiveSites[pair+1]=Flip(bestPoint);chosen.Add(Flip(bestPoint));}
            }
        }
        private static IEnumerable<Vector2> Join(Vector2[] first,Vector2[] second)
        {foreach(var point in first)yield return point;foreach(var point in second)yield return point;}
        private static void ResolveOpeningExplorer(MapLayout layout)
        {
            var navigation=new GridNavigation();navigation.ConfigureTerrain(p=>layout.IsWater(p)&&!layout.IsBridge(p)||layout.IsBridgeSideBarrierCell(p));
            foreach(var start in layout.Starts)navigation.SetBlocked(start.Town,3,true);
            var home=layout.Starts[0];Vector2 chosen=home.Explorer;float best=float.MaxValue;
            for(int ring=0;ring<=16;ring++)
            {
                for(int y=-ring;y<=ring;y++)for(int x=-ring;x<=ring;x++)
                {
                    if(Mathf.Max(Mathf.Abs(x),Mathf.Abs(y))!=ring)continue;float distance=x*x+y*y;if(distance>=best)continue;
                    var p=home.Explorer+new Vector2(x,y);
                    if(!navigation.IsWalkable(p,1.05f)||!layout.InStartingRegion(0,p)||layout.BridgeFootprintOverlaps(p,3,3)||ProtectedShore(layout,p,5.05f))continue;
                    bool worker=false;for(int i=0;i<9;i++)if(Vector2.Distance(p,home.WorkerPoint(i))<1.6f){worker=true;break;}if(worker)continue;
                    chosen=p;best=distance;
                }
                if(best<float.MaxValue&&ring*ring>best)break;
            }
            if(best==float.MaxValue)throw new InvalidOperationException("Generated map has no safe explorer opening: "+layout.Id+" / "+layout.Seed);
            layout.Starts[0]=new MapStartLayout(0,home.Town,43,chosen);layout.Starts[1]=new MapStartLayout(1,Flip(home.Town),43,Flip(chosen));
        }
        private static void BuildResources(MapLayout layout,RandomSequence random)
        {
            var anchors=new List<MapResourceAnchor>();var start=layout.Starts[0];
            float jitterX=random.Range(-1f,1f),jitterY=random.Range(-1f,1f);
            void Add(string definition,Vector2 offset,bool owned=false)
            {
                Vector2 desired=start.Town+offset+new Vector2(jitterX,jitterY);float radius=definition=="mine"?1.5f:1f;
                Vector2 chosen=Vector2.zero;float best=float.MaxValue;
                for(int ring=0;ring<=28;ring++)
                {
                    for(int y=-ring;y<=ring;y++)for(int x=-ring;x<=ring;x++)
                    {
                        if(Mathf.Max(Mathf.Abs(x),Mathf.Abs(y))!=ring)continue;
                        var p=desired+new Vector2(x,y);float distance=x*x+y*y;if(distance>=best||p.x<3+radius||p.y<3+radius||p.x>125-radius||p.y>125-radius)continue;
                        if(layout.SignedBankDistance(p)<radius+1||layout.IsBridgeApproach(p)||layout.BridgeFootprintOverlaps(p,radius*2+1,radius*2+1)||!layout.InStartingRegion(0,p)||ProtectedShore(layout,p,radius+4))continue;
                        if(Vector2.Distance(p,start.Explorer)<radius+.7f)continue;
                        bool landmark=false;foreach(var site in layout.NeutralSites)if(Vector2.Distance(p,site)<radius+4){landmark=true;break;}if(landmark)continue;
                        Vector2 q=p-start.Town;float townGap=new Vector2(Mathf.Max(0,Mathf.Abs(q.x)-4),Mathf.Max(0,Mathf.Abs(q.y)-4)).magnitude;if(townGap<radius+.5f)continue;
                        bool occupied=false;foreach(var other in anchors)
                        {float otherRadius=other.Definition=="mine"?1.5f:1f;if(Vector2.Distance(p,other.Position)<radius+otherRadius+.3f){occupied=true;break;}}
                        if(occupied)continue;best=distance;chosen=p;
                    }
                    if(best<float.MaxValue&&ring*ring>best)break;
                }
                if(best==float.MaxValue)throw new InvalidOperationException("Generated opening has no resource site: "+layout.Id+" / "+layout.Seed+" / "+definition);
                anchors.Add(new MapResourceAnchor(definition,chosen,owned));
            }
            Add("berry",new Vector2(8,-7));Add("berry",new Vector2(11,-7));
            for(int i=0;i<5;i++)Add("deer",new Vector2(-8+i*1.8f,9+i%2*1.8f));
            Add("mine",new Vector2(-10,-5));
            for(int i=0;i<18;i++)Add("tree",new Vector2(-14+i%6*2.2f,-12-i/6*2.2f));
            Add("crate_food",new Vector2(5,1),true);Add("crate_wood",new Vector2(5,3),true);
            Add("mine",new Vector2(22,7));
            for(int i=0;i<9;i++)Add("tree",new Vector2(17+i%3*2.1f,19+i/3*2.1f));
            for(int i=0;i<5;i++)Add("deer",new Vector2(9+i*2,25));
            for(int i=0;i<anchors.Count;i++)
            {layout.Starts[0].Resources[i]=anchors[i];layout.Starts[1].Resources[i]=new MapResourceAnchor(anchors[i].Definition,Flip(anchors[i].Position),anchors[i].Owned);}
        }
        internal static bool ProtectedShore(MapLayout layout,Vector2 point,float radius)
        {
            foreach(var sites in layout.DockSites)foreach(var site in sites)if(Vector2.Distance(point,site)<radius+2)return true;
            foreach(var sites in layout.LandingSites)foreach(var site in sites)if(Vector2.Distance(point,site)<radius)return true;
            foreach(var site in layout.NeutralSites)if(Vector2.Distance(point,site)<radius)return true;return false;
        }
        internal static Vector2[][] Landings(MapLayout layout)
        {
            var points=new List<Vector2>();float home=layout.Starts[0].Town.y;
            foreach(float target in new[]{home+14,128-home-14})
            {
                Vector2 chosen=Vector2.zero;float best=float.MaxValue;
                for(int y=12;y<=116;y++)for(int x=12;x<=116;x++)
                {
                    var p=new Vector2(x,y);float distance=layout.SignedBankDistance(p);if(distance<2.8f||distance>4.2f||layout.WaterDirection(p).x<.25f||!layout.InStartingRegion(0,p)||layout.BridgeFootprintOverlaps(p,8,8))continue;
                    bool busy=false;foreach(var site in layout.DockSites[0])if(Vector2.Distance(p,site)<9){busy=true;break;}if(busy)continue;
                    if(layout.SignedBankDistance(p+layout.WaterDirection(p)*7)>-2.2f)continue;
                    float score=Mathf.Abs(y-target)*2+Mathf.Abs(distance-3.3f);if(score<best){best=score;chosen=p;}
                }
                if(best==float.MaxValue)throw new InvalidOperationException("Generated map has no landing beach: "+layout.Id+" / "+layout.Seed);points.Add(chosen);
            }
            return new[]{points.ToArray(),Mirror(points.ToArray())};
        }
        internal static Vector2[] IslandLandings(MapLayout layout)
        {
            var points=new List<Vector2>();int region=layout.LandRegion(layout.TradeRoute[4]);
            foreach(int side in new[]{-1,1})
            {
                Vector2 chosen=Vector2.zero;float best=float.MaxValue;
                for(int y=45;y<=83;y++)for(int x=48;x<=80;x++)
                {
                    var p=new Vector2(x,y);float distance=layout.SignedBankDistance(p);if(distance<2.8f||distance>4.2f||layout.LandRegion(p)!=region||layout.WaterDirection(p).x*side<.25f)continue;
                    float score=Mathf.Abs(y-64)+Mathf.Abs(distance-3.3f);if(score<best){best=score;chosen=p;}
                }
                if(best<float.MaxValue)points.Add(chosen);
            }
            return points.ToArray();
        }

        internal static void BuildShoreCache(RiverChannel[] channels,float[] shore,Vector2[] center,int resolution,float cell)
        {
            var width=new float[shore.Length];var exact=new bool[shore.Length];for(int i=0;i<shore.Length;i++)shore[i]=float.PositiveInfinity;
            // Rasterize exact near-bank distances only where each segment can
            // affect water/harbors/heights. Far-land propagation is presentation
            // distance only; it never replaces an exact near-bank classification.
            foreach(var channel in channels)for(int n=0;n<channel.Centers.Length-1;n++)
            {
                Vector2 a=channel.Centers[n],b=channel.Centers[n+1],delta=b-a;float expand=Mathf.Max(channel.HalfWidths[n],channel.HalfWidths[n+1])+8;
                int x0=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x,b.x)-expand)/cell),0,resolution-1),x1=Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.x,b.x)+expand)/cell),0,resolution-1);
                int y0=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.y,b.y)-expand)/cell),0,resolution-1),y1=Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.y,b.y)+expand)/cell),0,resolution-1);
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                {
                    Vector2 p=new Vector2(x*cell,y*cell);float t=Mathf.Clamp01(Vector2.Dot(p-a,delta)/Mathf.Max(.000001f,delta.sqrMagnitude));
                    Vector2 c=a+delta*t;float w=Mathf.Lerp(channel.HalfWidths[n],channel.HalfWidths[n+1],t),distance=Vector2.Distance(p,c)-w;int i=x+y*resolution;
                    if(distance<shore[i]){shore[i]=distance;center[i]=c;width[i]=w;}exact[i]=true;
                }
            }
            for(int pass=0;pass<2;pass++)for(int reversed=0;reversed<2;reversed++)
                for(int row=0;row<resolution;row++)for(int column=0;column<resolution;column++)
                {
                    int x=reversed==0?column:resolution-1-column,y=reversed==0?row:resolution-1-row,i=x+y*resolution;if(exact[i])continue;
                    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                    {
                        int nx=x+dx,ny=y+dy;if(nx<0||ny<0||nx>=resolution||ny>=resolution)continue;int j=nx+ny*resolution;
                        if(float.IsPositiveInfinity(shore[j]))continue;float candidate=Vector2.Distance(new Vector2(x*cell,y*cell),center[j])-width[j];
                        if(candidate<shore[i]){shore[i]=candidate;center[i]=center[j];width[i]=width[j];}
                    }
                }
        }
    }
    public sealed partial class MapLayout
    {
        private int[] landRegions;
        public int LandRegion(Vector2 point)
        {
            if(landRegions==null)
            {
                var open=new bool[128*128];landRegions=new int[open.Length];
                for(int y=0;y<128;y++)for(int x=0;x<128;x++){var p=new Vector2(x,y);open[x+y*128]=(!IsWater(p)||IsBridge(p))&&!IsBridgeSideBarrierCell(p);}
                var pending=new Queue<int>();int label=0;
                for(int i=0;i<open.Length;i++)if(open[i]&&landRegions[i]==0)
                {
                    label++;landRegions[i]=label;pending.Enqueue(i);
                    while(pending.Count>0)
                    {
                        int current=pending.Dequeue(),x=current%128,y=current/128;
                        void Add(int next){if(next>=0&&open[next]&&landRegions[next]==0){landRegions[next]=label;pending.Enqueue(next);}}
                        Add(x>0?current-1:-1);Add(x<127?current+1:-1);Add(y>0?current-128:-1);Add(y<127?current+128:-1);
                    }
                }
            }
            int px=Mathf.Clamp(Mathf.RoundToInt(point.x),0,127),py=Mathf.Clamp(Mathf.RoundToInt(point.y),0,127);return landRegions[px+py*128];
        }
        public bool InStartingRegion(int player,Vector2 point)=>player>=0&&player<Starts.Length&&LandRegion(point)!=0&&LandRegion(point)==LandRegion(Starts[player].Town);
    }
}
