using UnityEngine;

namespace Frontier
{
    public partial class World
    {
        public Vector2 OpeningTownPosition(int player)
        {
            if(player<0||player>=Layout.Starts.Length)throw new System.ArgumentOutOfRangeException(nameof(player));
            return Layout.Starts[player].Town;
        }
        private void InitializeGeneratedMap()
        {
            // Seeded anchors describe geography, while the normal placement
            // contract checks actual civilization sizes and existing actors.
            for(int player=0;player<Players.Length;player++)
            {
                var start=Layout.Starts[player];
                Spawn("town",player,MapLandPosition("town",start.Town,player));
                string worker=WorkerDefinition(player).id;
                for(int i=0;i<Rules.Civ(Players[player].civilization).startingWorkers;i++)
                    Spawn(worker,player,MapLandPosition(worker,start.WorkerPoint(i),player));
                Spawn("explorer",player,MapLandPosition("explorer",start.Explorer,player));
                foreach(var anchor in start.Resources)
                    Spawn(anchor.Definition,anchor.Owned?player:-1,MapLandPosition(anchor.Definition,anchor.Position,player));
            }
            foreach(var point in Layout.FishPoints)Spawn("fish",-1,point);
            if(MapId==MapTopology.Frontier)for(int y=42;y<=86;y+=4)
            {
                if(y>=58&&y<=70)continue;
                var obstacle=new Vector3(64,y,1.8f);TerrainObstacles.Add(obstacle);Navigation.SetBlocked(new Vector2(obstacle.x,obstacle.y),obstacle.z,true);
            }
        }
        // New authored banks can intersect the old expansion-resource coordinates.
        // Resolve opening placement once; Spawn itself remains an explicit contract
        // for rewards, production, tests and gameplay, and never silently teleports.
        private Vector2 MapLandPosition(string definition,Vector2 desired,int homePlayer=-1)
        {
            if(Players.Length==2&&(MapId==MapTopology.Frontier||MapId==MapTopology.River))return desired;
            var d=Rules.Entity(definition);
            if(d==null||d.waterborne)return desired;
            float width=d.kind==EntityKind.Building?d.width:Mathf.Max(1,d.radius*2);
            float depth=d.kind==EntityKind.Building?d.depth:Mathf.Max(1,d.radius*2);
            bool Legal(Vector2 p)
            {
                if(p.x-width*.5f<2||p.y-depth*.5f<2||p.x+width*.5f>126||p.y+depth*.5f>126)return false;
                if(Layout.BridgeFootprintOverlaps(p,width+1,depth+1))return false;
                if((Layout.Generated||Players.Length>2)&&homePlayer>=0&&!Layout.InStartingRegion(homePlayer,p))return false;
                if(Players.Length>2&&homePlayer>=0&&!Layout.InOpeningTerritory(homePlayer,p))return false;
                if((Layout.Generated||Players.Length>2)&&d.id!="town"&&MapGeneration.ProtectedShore(Layout,p,d.radius+4))return false;
                if(!Navigation.IsAreaFree(p,width,depth))return false;
                foreach(var v in new[]{Vector2.zero,new Vector2(-width*.5f,-depth*.5f),new Vector2(width*.5f,-depth*.5f),new Vector2(-width*.5f,depth*.5f),new Vector2(width*.5f,depth*.5f)})
                    if(IsWater(p+v))return false;
                // Moving several authored resources away from the same curved
                // bank must not project them into a shared legal land point.
                foreach(var other in Entities)
                {
                    if(!other.Alive||other.def.waterborne)continue;
                    if(other.def.kind==EntityKind.Building)
                    {if(BuildingFootprintDistance(other.def,other.position,p)<d.radius+.12f)return false;}
                    else if(d.kind==EntityKind.Building)
                    {
                        var delta=other.position-p;
                        var distance=new Vector2(Mathf.Max(0,Mathf.Abs(delta.x)-width*.5f),Mathf.Max(0,Mathf.Abs(delta.y)-depth*.5f)).magnitude;
                        if(distance<other.def.radius+.12f)return false;
                    }
                    else if(Vector2.Distance(p,other.position)<d.radius+other.def.radius+.12f)return false;
                }
                return true;
            }
            if(Legal(desired))return desired;
            Vector2 closest=desired;float best=float.MaxValue;
            for(int ring=1;ring<=48;ring++)
            {
                for(int y=-ring;y<=ring;y++)for(int x=-ring;x<=ring;x++)
                {
                    if(Mathf.Max(Mathf.Abs(x),Mathf.Abs(y))!=ring)continue;
                    var offset=new Vector2(x,y)*(Layout.Generated&&homePlayer==1?-1:1);
                    var p=desired+offset;float distance=x*x+y*y;
                    if(distance<best&&Legal(p)){closest=p;best=distance;}
                }
                if(best<float.MaxValue&&ring*ring>best)return closest;
            }
            throw new System.InvalidOperationException("No safe opening placement for "+definition+" on "+MapId+" at "+desired);
        }
    }
}
