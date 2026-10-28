using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // Tall, non-interactive scenery cannot rely on the ground fog plane. Its
    // visibility follows the same observer fog as resource models, and later
    // construction hides a clump instead of placing a warehouse inside a tree.
    public sealed class RiverSceneryVisibility:MonoBehaviour
    {
        World world;
        readonly List<MeshRenderer> renderers=new List<MeshRenderer>();
        readonly List<Vector2> centers=new List<Vector2>();
        readonly List<bool> occupied=new List<bool>();
        int landVersion=-1,waterVersion=-1;
        float refreshAt;
        public int ClusterCount=>renderers.Count;
        public void Initialize(World source){world=source;}
        public void Add(MeshRenderer renderer,Vector2 center)
        {renderers.Add(renderer);centers.Add(center);occupied.Add(false);renderer.enabled=false;}
        public void Refresh(bool forceOccupancy=false)
        {
            if(world==null)return;
            if(forceOccupancy||landVersion!=world.Navigation.Version||waterVersion!=world.WaterNavigation.Version)
            {
                landVersion=world.Navigation.Version;waterVersion=world.WaterNavigation.Version;
                for(int i=0;i<centers.Count;i++)
                {
                    occupied[i]=false;
                    foreach(var entity in world.Entities)
                        if(entity.Alive&&entity.def.kind==EntityKind.Building&&world.BuildingFootprintContains(entity.def,entity.position,centers[i],2.4f))
                        {occupied[i]=true;break;}
                }
            }
            for(int i=0;i<renderers.Count;i++)if(renderers[i]!=null)
                renderers[i].enabled=!occupied[i]&&world.Fog.At(0,centers[i])==VisibilityState.Visible;
        }
        void LateUpdate()
        {
            if(Time.unscaledTime<refreshAt)return;
            refreshAt=Time.unscaledTime+.25f;Refresh();
        }
    }
}
