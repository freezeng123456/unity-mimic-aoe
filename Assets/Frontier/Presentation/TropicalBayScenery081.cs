using UnityEngine;
using Surface=Frontier.ArchitecturalMaterials.Surface;

namespace Frontier
{
    public static partial class RiverLandscapePresentation
    {
        // Seed two named landmark slots into the existing <=8 tropical palm
        // budget. Existing proceduralPalm() is reused, not cloned or replaced.
        static int BuildTropicalBayLandmarks081(Transform root,World world,RiverSceneryVisibility visibility,
            Material bark,Material leaves,Material sunLeaves)
        {
            if(!TropicalBayContent081.Applies(world.Layout))return 0;
            var rock=ArchitecturalMaterials.Get(Surface.Stone,new Color(.59f,.555f,.43f));
            int count=0;
            for(int bank=0;bank<TropicalBayContent081.RegionCount;bank++)
            {
                var region=TropicalBayContent081.Region(bank);var p=region.VegetationCenter;
                if(p.x<5||p.x>123||p.y<5||p.y>123||world.Layout.IsWater(p)
                    ||world.Layout.IsBridgeApproach(p)||world.Layout.IsBridgeSideBarrier(p)
                    ||NearHarbor(world.Layout,p,7)||NearEntity(world,p,6)||NearTradeRoute(world.Layout,p,4))continue;
                var direction=-world.Layout.WaterDirection(p);float yaw=-Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg;
                var g=new ArchitecturalGeometry();
                Palm(g,new Vector3(-.55f,0,-.25f),2.75f,.22f,bank*.73f,bark,leaves,sunLeaves);
                Palm(g,new Vector3(.66f,-.02f,.55f),2.1f,-.16f,bank*.73f+2,bark,leaves,sunLeaves);
                // Three low dry rock ledges, not impassable cliff-sized scenery.
                // Taller palms and these ledges share one fog/occupancy gate.
                for(int stone=0;stone<3;stone++)
                {
                    float x=-.84f+stone*.72f,z=-.76f+(stone%2)*.22f;
                    g.Box(new Vector3(x,.12f,z),new Vector3(.78f,.24f,.58f),rock,Quaternion.Euler(4,stone*23,-3));
                    g.Box(new Vector3(x+.08f,.275f,z+.04f),new Vector3(.47f,.13f,.40f),rock,Quaternion.Euler(-3,stone*23+7,4));
                }
                for(int leaf=0;leaf<5;leaf++)
                {
                    float angle=leaf*Mathf.PI*2/5;var forward=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                    var start=new Vector3(.2f,.1f,-.2f);var tip=start+forward*.6f+Vector3.up*.25f;
                    var side=Vector3.Cross(Vector3.up,forward)*.14f;
                    g.Quad(start,tip+side,tip,tip-side,leaf%2==0?leaves:sunLeaves);
                    g.Quad(tip-side,tip,tip+side,start,leaves);
                }
                var landmark=g.Create(root,"Tropical081 "+region.Id+" palm and low stone landmark");
                landmark.localPosition=new Vector3(p.x,world.Layout.BankHeight(p),p.y);
                landmark.localRotation=Quaternion.Euler(0,yaw,0);
                var renderer=landmark.GetComponent<MeshRenderer>();var lod=landmark.gameObject.AddComponent<LODGroup>();
                lod.SetLODs(new[]{new LOD(.035f,new[]{renderer})});lod.RecalculateBounds();visibility.Add(renderer,p);count++;
            }
            return count;
        }
    }
}
