using UnityEngine;
using Surface=Frontier.ArchitecturalMaterials.Surface;

namespace Frontier
{
    public static partial class RiverLandscapePresentation
    {
        static void BuildConfluenceLandmark082(Transform root,World world)
        {
            if(!ConfluenceContent082.Applies(world.Layout))return;
            var p=ConfluenceContent082.Landmark;
            if(world.IsWater(p)||world.IsBridgeApproach(p)||world.Layout.IsBridgeSideBarrier(p)
                ||NearHarbor(world.Layout,p,7)||NearEntity(world,p,6)||NearTradeRoute(world.Layout,p,4))return;
            var gravel=ArchitecturalMaterials.Get(Surface.Stone,new Color(.51f,.50f,.44f));
            var driftwood=ArchitecturalMaterials.Get(Surface.Wood,new Color(.47f,.43f,.34f));
            var g=new ArchitecturalGeometry();
            for(int i=0;i<4;i++)
            {
                float x=-.78f+i*.48f,z=(i%2)*.3f-.35f;
                g.Box(new Vector3(x,.12f,z),new Vector3(.64f,.24f,.51f),gravel,Quaternion.Euler(3,i*29,-4));
                g.Box(new Vector3(x+.04f,.25f,z+.03f),new Vector3(.39f,.12f,.34f),gravel,Quaternion.Euler(-3,i*29+9,2));
            }
            g.Beam(new Vector3(-.85f,.20f,.65f),new Vector3(.81f,.28f,.38f),.13f,.16f,driftwood);
            g.Beam(new Vector3(.25f,.24f,.48f),new Vector3(.39f,.48f,.83f),.07f,.06f,driftwood);
            var landmark=g.Create(root,"Confluence082 northern gravel and driftwood landmark");
            landmark.localPosition=new Vector3(p.x,world.Layout.BankHeight(p),p.y);
            var renderer=landmark.GetComponent<MeshRenderer>();var lod=landmark.gameObject.AddComponent<LODGroup>();
            lod.SetLODs(new[]{new LOD(.035f,new[]{renderer})});lod.RecalculateBounds();
            var visibility=root.gameObject.AddComponent<RiverSceneryVisibility>();visibility.Initialize(world);visibility.Add(renderer,p);
        }
    }
}
