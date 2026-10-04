using UnityEngine;

namespace Frontier
{
    public partial class World
    {
        // New authored banks can intersect the old expansion-resource coordinates.
        // Resolve opening placement once; Spawn itself remains an explicit contract
        // for rewards, production, tests and gameplay, and never silently teleports.
        private Vector2 MapLandPosition(string definition,Vector2 desired)
        {
            if(MapId==MapTopology.Frontier||MapId==MapTopology.River)return desired;
            var d=Rules.Entity(definition);
            if(d==null||d.waterborne)return desired;
            float width=d.kind==EntityKind.Building?d.width:Mathf.Max(1,d.radius*2);
            float depth=d.kind==EntityKind.Building?d.depth:Mathf.Max(1,d.radius*2);
            bool Legal(Vector2 p)
            {
                if(p.x-width*.5f<2||p.y-depth*.5f<2||p.x+width*.5f>126||p.y+depth*.5f>126)return false;
                if(Layout.BridgeFootprintOverlaps(p,width+1,depth+1))return false;
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
                    var p=desired+new Vector2(x,y);float distance=x*x+y*y;
                    if(distance<best&&Legal(p)){closest=p;best=distance;}
                }
                if(best<float.MaxValue&&ring*ring>best)return closest;
            }
            throw new System.InvalidOperationException("No safe opening placement for "+definition+" on "+MapId+" at "+desired);
        }
    }
}
