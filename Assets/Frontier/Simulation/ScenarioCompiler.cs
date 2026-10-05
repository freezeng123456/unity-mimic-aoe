using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // Compilation is a transaction. Only a complete, owned and validated layout
    // enters the cache; changing an editor draft cannot change a running match.
    public static class ScenarioCompiler
    {
        public const int Version=1;
        static readonly Dictionary<string,MapLayout> cache=new Dictionary<string,MapLayout>();
        static readonly Queue<string> order=new Queue<string>();
        public static string Validate(ScenarioDocument document,RuleCatalog rules,int players,int[] teamsOverride=null)
        {
            try
            {
                Compile(document,rules,players);
                var teams=teamsOverride??Array.ConvertAll(document.starts,s=>s.team);
                if(teams.Length!=players)return "Scenario player teams must match the match roster";
                var factions=new HashSet<int>();foreach(int team in teams){if(team<0||team>3)return "Team must be between zero and three";factions.Add(team);}
                return factions.Count<2?"A match requires at least two factions":"";
            }
            catch(Exception error)when(error is ArgumentException||error is InvalidOperationException){return error.Message;}
        }
        public static MapLayout Compile(ScenarioDocument document,RuleCatalog rules,int players)
        {
            string reason=ScenarioCodec.Validate(document,rules);if(reason!="")throw new ArgumentException(reason);
            if(players<2||players>4||document.starts.Length!=players)throw new ArgumentException("Scenario player slots must match the match roster");
            var owned=ScenarioCodec.Clone(document);string key=ScenarioCodec.ContentHash(owned)+":"+players;
            lock(cache)
            {
                if(cache.TryGetValue(key,out var known))return known;
                var channels=new RiverChannel[owned.rivers.Length];
                for(int i=0;i<channels.Length;i++)
                {var r=owned.rivers[i];channels[i]=r.smooth?MapLayout.Sample(r.points,r.halfWidths):new RiverChannel(r.points,r.halfWidths);}
                var bridges=new BridgeSpan[owned.bridges.Length];
                for(int i=0;i<bridges.Length;i++)
                {
                    var b=owned.bridges[i];Vector2 direction=b.direction.normalized;
                    float reach=b.halfLength+b.rampLength;
                    bridges[i]=new BridgeSpan(b.center,direction,b.halfLength,b.halfWidth,b.rampLength,b.height,
                        Height(owned,b.center-direction*reach),Height(owned,b.center+direction*reach));
                }
                var starts=new MapStartLayout[players];
                for(int i=0;i<players;i++)starts[i]=new MapStartLayout(i,owned.starts[i].town,0,owned.starts[i].explorer);
                var fish=new List<Vector2>();foreach(var r in owned.resources)if(r.definition=="fish")fish.Add(r.position);
                var neutral=new Vector2[owned.tradeSockets.Length];for(int i=0;i<neutral.Length;i++)neutral[i]=owned.tradeSockets[i].position;
                var objectives=new Vector2[owned.treasures.Length];for(int i=0;i<objectives.Length;i++)objectives[i]=owned.treasures[i].position;
                var layout=new MapLayout(MapTopology.Scenario,channels,bridges,fish.ToArray(),Array.Empty<Vector2>(),owned.tradeRoute,
                    owned.seed,starts,neutral,objectives,scenario:owned);
                ValidateGeography(layout,owned,rules);
                cache.Add(key,layout);order.Enqueue(key);while(order.Count>12)cache.Remove(order.Dequeue());return layout;
            }
        }
        internal static float Height(ScenarioDocument document,Vector2 p)
        {
            float px=Mathf.Clamp(p.x,0,128),py=Mathf.Clamp(p.y,0,128);
            int x=Mathf.Min(127,Mathf.FloorToInt(px)),y=Mathf.Min(127,Mathf.FloorToInt(py)),i=x+y*129;
            return Mathf.Lerp(Mathf.Lerp(document.heightsCm[i],document.heightsCm[i+1],px-x),
                Mathf.Lerp(document.heightsCm[i+129],document.heightsCm[i+130],px-x),py-y)*.01f;
        }
        static void ValidateGeography(MapLayout layout,ScenarioDocument doc,RuleCatalog rules)
        {
            var land=new GridNavigation();var water=new GridNavigation();
            land.ConfigureTerrain(p=>layout.IsTerrainBlocked(p)||layout.IsWater(p)&&!layout.IsBridge(p)||layout.IsBridgeSideBarrierCell(p));
            water.ConfigureTerrain(p=>!layout.IsWater(p));
            var placed=new List<Tuple<EntityDef,Vector2,string>>();
            void Check(string definition,Vector2 p,string id)
            {
                var d=rules.Entity(definition);if(d==null)throw new ArgumentException("Unknown scenario object "+definition);
                if(d.id=="dock")
                {if(!layout.HasWater||layout.WaterDirection(p).sqrMagnitude<.9f||layout.SignedBankDistance(p)<1||layout.SignedBankDistance(p)>6||layout.FoundationHeightRange(p,d.width,d.depth,layout.WaterDirection(p))>.5f)throw new ArgumentException("Scenario dock needs a usable bank: "+id);}
                else if(d.waterborne)
                {if(!layout.IsWater(p)||!water.IsWalkable(p,d.radius))throw new ArgumentException("Scenario water object is outside a usable water channel: "+id);}
                else
                {
                    float width=d.kind==EntityKind.Building?d.width:Mathf.Max(.8f,d.radius*2),depth=d.kind==EntityKind.Building?d.depth:Mathf.Max(.8f,d.radius*2);
                    if(d.kind==EntityKind.Building?
                        !land.IsAreaFree(p,width,depth)||layout.BridgeFootprintOverlaps(p,width,depth)||layout.FoundationHeightRange(p,width,depth)>.25f:
                        !land.IsWalkable(p,d.radius))throw new ArgumentException("Scenario object has a blocked, steep or submerged placement: "+id);
                }
                foreach(var existing in placed)
                {
                    var e=existing.Item1;if(d.waterborne!=e.waterborne&&d.id!="dock"&&e.id!="dock")continue;
                    if(d.id=="trade_socket"||e.id=="trade_socket")continue;
                    if(d.kind!=EntityKind.Building&&e.kind!=EntityKind.Building)
                    {
                        // Resource radii describe selection/gathering extents.
                        // Live movement only separates Unit bodies; a woodland
                        // can contain clustered trees, deer and gathering units.
                        if(d.kind!=EntityKind.Unit||e.kind!=EntityKind.Unit)continue;
                        if((p-existing.Item2).sqrMagnitude<Mathf.Pow(d.radius+e.radius,2)-.00001f)
                            throw new ArgumentException("Overlapping scenario objects: "+id+" ["+d.id+","+d.kind+",r="+d.radius+",p="+p+"] / "+existing.Item3+" ["+e.id+","+e.kind+",r="+e.radius+",p="+existing.Item2+"]");
                        continue;
                    }
                    Vector2 delta=p-existing.Item2;
                    float width=(d.kind==EntityKind.Building?d.width*.5f:d.radius)+(e.kind==EntityKind.Building?e.width*.5f:e.radius);
                    float depth=(d.kind==EntityKind.Building?d.depth*.5f:d.radius)+(e.kind==EntityKind.Building?e.depth*.5f:e.radius);
                    if(Mathf.Abs(delta.x)<width+.08f&&Mathf.Abs(delta.y)<depth+.08f)throw new ArgumentException("Overlapping scenario objects: "+id+" ["+d.id+","+d.kind+",r="+d.radius+",p="+p+"] / "+existing.Item3+" ["+e.id+","+e.kind+",r="+e.radius+",p="+existing.Item2+"]");
                }
                placed.Add(Tuple.Create(d,p,id));
                if(d.kind==EntityKind.Building&&d.id!="dock")land.SetBlocked(p,Mathf.Max(d.width,d.depth)*.5f,true);
            }
            for(int i=0;i<doc.starts.Length;i++)
            {
                var s=doc.starts[i];Check("town",s.town,"start-"+i+"-town");
                Check("explorer",s.explorer,"start-"+i+"-explorer");
                string worker=rules.Civ(s.civilization)?.workerUnit??"villager";
                for(int j=0;j<s.workers.Length;j++)Check(worker,s.workers[j],"start-"+i+"-worker-"+j);
            }
            foreach(var item in doc.resources)Check(item.definition,item.position,item.id);
            foreach(var item in doc.objects)Check(item.definition,item.position,item.id);
            foreach(var socket in doc.tradeSockets)Check("trade_socket",socket.position,socket.id);
            foreach(var camp in doc.treasures)
            {
                Check(rules.landmarks.treasures[camp.definition].entity,camp.position,camp.id);
                for(int i=0;i<camp.guardPositions.Length;i++)Check("guardian",camp.guardPositions[i],camp.id+"-guard-"+i);
            }
            foreach(var point in doc.tradeRoute)
                if(!land.IsWalkable(point,.4f))throw new ArgumentException("Trade route has a blocked or submerged waypoint");
            for(int i=1;i<doc.tradeRoute.Length;i++)
            {
                var path=land.FindPath(doc.tradeRoute[i-1],doc.tradeRoute[i],.4f);
                if(path.Count==0||(path[path.Count-1]-doc.tradeRoute[i]).sqrMagnitude>.01f)throw new ArgumentException("Trade route has disconnected waypoints");
            }
            foreach(var bridge in layout.Bridges)
            {
                if(!layout.IsWater(bridge.Center))throw new ArgumentException("A scenario bridge must cross water");
                foreach(float sign in new[]{-1f,1f})
                {
                    var mouth=bridge.Point(sign*(bridge.HalfLength+bridge.RampLength),0);
                    if(layout.IsWater(mouth)||!land.IsWalkable(mouth,.4f))throw new ArgumentException("Scenario bridge ramp must reach clear land");
                }
                if(bridge.Height-layout.WaterHeight<WaterPresentation.MaximumBoatHeight+.25f)throw new ArgumentException("Scenario bridge does not leave enough room for ships");
            }
        }
    }
    public sealed partial class MapLayout
    {
        bool[] scenarioTerrainMask;
        public float BaseGroundHeight(Vector2 point)=>HasScenario?ScenarioCompiler.Height(scenarioDefinition,point):BankHeight(point);
        public int SurfaceKind(Vector2 point)=>HasScenario?scenarioDefinition.surfaces[Mathf.Clamp(Mathf.FloorToInt(point.x),0,127)+Mathf.Clamp(Mathf.FloorToInt(point.y),0,127)*128]:0;
        public float FoundationHeightRange(Vector2 p,float width,float depth)
        {
            float min=float.MaxValue,max=float.MinValue;
            if(HasScenario)
            {
                // A bilinear height field reaches its extrema at height-grid
                // vertices and at its intersections with the foundation edge.
                // Sampling a fixed number of points can miss a narrow edited
                // peak inside a large building's otherwise level footprint.
                float left=p.x-width*.5f,right=p.x+width*.5f,bottom=p.y-depth*.5f,top=p.y+depth*.5f;
                int firstX=Mathf.CeilToInt(left),lastX=Mathf.FloorToInt(right),firstY=Mathf.CeilToInt(bottom),lastY=Mathf.FloorToInt(top);
                for(int y=-1;y<=lastY-firstY+1;y++)for(int x=-1;x<=lastX-firstX+1;x++)
                {
                    float px=x<0?left:x>lastX-firstX?right:firstX+x,py=y<0?bottom:y>lastY-firstY?top:firstY+y;
                    float h=GroundHeight(new Vector2(px,py));min=Mathf.Min(min,h);max=Mathf.Max(max,h);
                }
                return max-min;
            }
            for(int y=0;y<=4;y++)for(int x=0;x<=4;x++)
            {float h=GroundHeight(p+new Vector2((x/4f-.5f)*width,(y/4f-.5f)*depth));min=Mathf.Min(min,h);max=Mathf.Max(max,h);}
            return max-min;
        }
        public float FoundationHeightRange(Vector2 p,float width,float depth,Vector2 direction)
        {
            if(!HasScenario||direction.sqrMagnitude<.0001f||Mathf.Abs(direction.y)<.0001f)return FoundationHeightRange(p,width,depth);
            direction.Normalize();var side=new Vector2(-direction.y,direction.x);
            float hx=width*.5f,hy=depth*.5f,min=float.MaxValue,max=float.MinValue;
            float Sample(Vector2 point){float h=GroundHeight(point);min=Mathf.Min(min,h);max=Mathf.Max(max,h);return h;}
            float ex=Mathf.Abs(direction.x)*hx+Mathf.Abs(side.x)*hy,ey=Mathf.Abs(direction.y)*hx+Mathf.Abs(side.y)*hy;
            // Include every height vertex in the actual rotated warehouse.
            for(int y=Mathf.CeilToInt(p.y-ey);y<=Mathf.FloorToInt(p.y+ey);y++)for(int x=Mathf.CeilToInt(p.x-ex);x<=Mathf.FloorToInt(p.x+ex);x++)
            {Vector2 point=new Vector2(x,y),delta=point-p;if(Mathf.Abs(Vector2.Dot(delta,direction))<=hx+.00001f&&Mathf.Abs(Vector2.Dot(delta,side))<=hy+.00001f)Sample(point);}
            var corners=new[]{p-direction*hx-side*hy,p+direction*hx-side*hy,p+direction*hx+side*hy,p-direction*hx+side*hy};
            var cuts=new List<float>();
            for(int edge=0;edge<4;edge++)
            {
                Vector2 a=corners[edge],b=corners[(edge+1)%4],delta=b-a;cuts.Clear();cuts.Add(0);cuts.Add(1);
                if(Mathf.Abs(delta.x)>.00001f)for(int x=Mathf.CeilToInt(Mathf.Min(a.x,b.x));x<=Mathf.FloorToInt(Mathf.Max(a.x,b.x));x++)cuts.Add(Mathf.Clamp01((x-a.x)/delta.x));
                if(Mathf.Abs(delta.y)>.00001f)for(int y=Mathf.CeilToInt(Mathf.Min(a.y,b.y));y<=Mathf.FloorToInt(Mathf.Max(a.y,b.y));y++)cuts.Add(Mathf.Clamp01((y-a.y)/delta.y));
                cuts.Sort();
                for(int i=1;i<cuts.Count;i++)
                {
                    float first=cuts[i-1],last=cuts[i];if(last-first<.000001f)continue;
                    Vector2 start=a+delta*first,end=a+delta*last;
                    float h0=Sample(start),h1=Sample(end),hm=Sample((start+end)*.5f);
                    // Bilinear terrain along a slanted edge is quadratic.
                    // Its stationary point can be between the grid crossings.
                    float qa=2*(h0+h1-2*hm),qb=h1-h0-qa;
                    if(Mathf.Abs(qa)>.000001f){float t=-qb/(2*qa);if(t>0&&t<1)Sample(Vector2.Lerp(start,end,t));}
                }
            }
            return max-min;
        }
        public bool IsTerrainBlocked(Vector2 p)
        {
            if(!HasScenario)return false;
            if(scenarioTerrainMask==null)
            {
                scenarioTerrainMask=new bool[128*128];
                for(int y=0;y<128;y++)for(int x=0;x<128;x++)
                {var cell=new Vector2(x,y);scenarioTerrainMask[x+y*128]=!IsBridgeApproach(cell)&&(SurfaceKind(cell)==3||FoundationHeightRange(cell,1,1)>.8f);}
            }
            return scenarioTerrainMask[Mathf.Clamp(Mathf.RoundToInt(p.x),0,127)+Mathf.Clamp(Mathf.RoundToInt(p.y),0,127)*128];
        }
        bool ScenarioRequiresTransport()
        {for(int i=1;i<Starts.Length;i++)if(LandRegion(Starts[i].Town)!=LandRegion(Starts[0].Town))return true;return false;}
        Vector2[][] ScenarioShoreSites(bool landing)
        {
            var output=new Vector2[Starts.Length][];
            for(int player=0;player<Starts.Length;player++)
            {
                var chosen=new List<Vector2>();
                if(HasWater)for(int alternative=0;alternative<2;alternative++)
                {
                    Vector2 best=Vector2.zero;float score=float.MaxValue;
                    for(int y=9;y<=119;y++)for(int x=9;x<=119;x++)
                    {
                        var p=new Vector2(x,y);float bank=SignedBankDistance(p);
                        if(bank<2.8f||bank>4.2f||!InStartingRegion(player,p)||BridgeFootprintOverlaps(p,10,10))continue;
                        bool occupied=false;foreach(var start in Starts)if((p-start.Town).sqrMagnitude<14*14){occupied=true;break;}
                        foreach(var prior in chosen)if((prior-p).sqrMagnitude<12*12){occupied=true;break;}
                        if(landing)foreach(var prior in DockSites[player])if((prior-p).sqrMagnitude<10*10){occupied=true;break;}
                        if(occupied)continue;
                        var direction=WaterDirection(p);if(direction.sqrMagnitude<.9f||FoundationHeightRange(p,4,4,direction)>.5f||!HarborLandClear(p,direction)||SignedBankDistance(p+direction*7)>-2.2f||IsTerrainBlocked(p))continue;
                        float candidate=(p-Starts[player].Town).sqrMagnitude;
                        if(candidate<score){score=candidate;best=p;}
                    }
                    if(score<float.MaxValue)chosen.Add(best);
                }
                output[player]=chosen.ToArray();
            }
            return output;
        }
    }
}
