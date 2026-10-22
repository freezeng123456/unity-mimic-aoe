using System.Collections.Generic;
using UnityEngine;
using Surface=Frontier.ArchitecturalMaterials.Surface;

namespace Frontier
{
    // A read-only dressing layer. Ground/water meshes and every map query stay
    // authoritative; small scenery is hidden by the existing fog/building gate.
    public static partial class RiverLandscapePresentation
    {
        const int MaximumBankClusters084=12;
        const float BankClusterRadius084=1.7f;

        struct BankSite084
        {
            public Vector2 point,tangent;
            public uint priority;
        }

        static uint LandscapeHash084(uint value)
        {
            value^=value>>16;value=unchecked(value*0x7feb352du);
            value^=value>>15;value=unchecked(value*0x846ca68bu);
            return value^(value>>16);
        }

        static uint LandscapeSeed084(string map,int seed)
        {
            uint value=unchecked((uint)seed)^0x845ec37bu;
            foreach(char letter in map)value=unchecked((value^letter)*16777619u);
            return LandscapeHash084(value);
        }

        static float LandscapeUnit084(uint value)=>(LandscapeHash084(value)&0x00ffffffu)/16777216f;

        static void ApplyGroundDetail084(MeshRenderer renderer,string map,int seed)
        {
            uint key=LandscapeSeed084(map,seed);
            var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
            // Keep the shared topology material cache: a match seed is a local
            // renderer value, never a new material or a mutation of other maps.
            block.SetVector("_DetailOrigin",new Vector4(LandscapeUnit084(key)*47,LandscapeUnit084(key+1)*47,0,0));
            if(map==MapTopology.Estuary)
            {
                block.SetColor("_TurfTint",new Color(.84f,.98f,.85f));
                block.SetColor("_MudColor",new Color(.43f,.44f,.355f));
                block.SetColor("_SandColor",new Color(.63f,.62f,.49f));
            }
            renderer.SetPropertyBlock(block);
        }

        static bool BankSiteClear084(World world,Vector2 point)
        {
            var layout=world.Layout;
            if(point.x<3||point.x>125||point.y<3||point.y>125
                ||NearHarbor(layout,point,8)||NearEntity(world,point,5.5f)||NearTradeRoute(layout,point,4))return false;
            // Leave the full six-metre bridge approach guide and the cluster's
            // own radius clear. This is visual clearance, not a new path rule.
            foreach(var span in layout.Bridges)
            {
                var local=span.Local(point);
                if(Mathf.Abs(local.x)<span.HalfLength+span.RampLength+6+BankClusterRadius084
                    &&Mathf.Abs(local.y)<span.HalfWidth+6+BankClusterRadius084)return false;
            }
            // Sample dry support around the small cluster, including bridge-side
            // barriers. Props cannot suggest that a lane or a berth is closed.
            for(int sample=0;sample<9;sample++)
            {
                float angle=(sample-1)*Mathf.PI/4;
                var p=sample==0?point:point+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*BankClusterRadius084;
                if(p.x<1||p.x>127||p.y<1||p.y>127||layout.SignedBankDistance(p)<.35f
                    ||layout.IsWater(p)||layout.IsBridgeApproach(p)||layout.IsBridgeSideBarrier(p))return false;
            }
            return true;
        }

        static void BuildBankDetail084(Transform root,World world)
        {
            if(world.Layout.HasScenario)return;
            uint seed=LandscapeSeed084(world.MapId,world.Seed);
            var landmarks=new List<Bounds>();
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                if(renderer.name.StartsWith("Tropical")||renderer.name.StartsWith("Confluence082"))
                {
                    var source=renderer.localBounds;var bounds=new Bounds(root.InverseTransformPoint(renderer.transform.TransformPoint(source.center)),Vector3.zero);
                    for(int corner=0;corner<8;corner++)
                    {
                        var p=source.center+Vector3.Scale(source.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                        bounds.Encapsulate(root.InverseTransformPoint(renderer.transform.TransformPoint(p)));
                    }
                    landmarks.Add(bounds);
                }
            var candidates=new List<BankSite084>();int channelIndex=0;
            foreach(var channel in world.Layout.Channels)
            {
                for(int sample=5;sample<channel.Centers.Length-5;sample+=5)
                {
                    Vector2 tangent=(channel.Centers[sample+1]-channel.Centers[sample-1]).normalized;
                    var normal=new Vector2(tangent.y,-tangent.x);
                    foreach(int side in new[]{-1,1})
                    {
                        uint key=LandscapeHash084(seed^unchecked((uint)(sample*53+channelIndex*977+(side+1)*139)));
                        float shoulder=3.1f+LandscapeUnit084(key+1)*1.6f;
                        var point=channel.Centers[sample]+normal*side*(channel.HalfWidths[sample]+shoulder)
                            +tangent*(LandscapeUnit084(key+2)-.5f)*2.1f;
                        if(!BankSiteClear084(world,point))continue;
                        bool nearLandmark=false;
                        foreach(var bounds in landmarks)
                        {
                            float dx=Mathf.Max(0,Mathf.Max(bounds.min.x-point.x,point.x-bounds.max.x));
                            float dz=Mathf.Max(0,Mathf.Max(bounds.min.z-point.y,point.y-bounds.max.z));
                            if(dx*dx+dz*dz<4){nearLandmark=true;break;}
                        }
                        if(!nearLandmark)candidates.Add(new BankSite084{point=point,tangent=tangent,priority=key});
                    }
                }
                channelIndex++;
            }
            // Stable hash ranking gives varied, reproducible clusters; spacing
            // keeps the fixed budget spread along the valley instead of using
            // the first twelve samples of one bank. No Unity Random is touched.
            candidates.Sort((a,b)=>a.priority!=b.priority?a.priority.CompareTo(b.priority)
                :a.point.x!=b.point.x?a.point.x.CompareTo(b.point.x):a.point.y.CompareTo(b.point.y));
            var chosen=new List<BankSite084>();
            foreach(var candidate in candidates)
            {
                bool close=false;foreach(var site in chosen)if((site.point-candidate.point).sqrMagnitude<64){close=true;break;}
                if(close)continue;chosen.Add(candidate);if(chosen.Count==MaximumBankClusters084)break;
            }
            if(chosen.Count==0)return;
            var dressing=new GameObject("Seeded riverbank detail 084").transform;dressing.SetParent(root,false);
            var visibility=dressing.gameObject.AddComponent<RiverSceneryVisibility>();visibility.Initialize(world);
            bool tropical=world.MapId==MapTopology.Tropical,estuary=world.MapId==MapTopology.Estuary;
            var leaf=ArchitecturalMaterials.Get(Surface.Cloth,tropical?new Color(.16f,.35f,.14f):new Color(.20f,.34f,.17f));
            var lightLeaf=ArchitecturalMaterials.Get(Surface.Cloth,tropical?new Color(.36f,.50f,.22f):new Color(.39f,.48f,.25f));
            var wood=ArchitecturalMaterials.Get(Surface.Wood,new Color(.30f,.245f,.16f));
            var gravel=ArchitecturalMaterials.Get(Surface.Stone,estuary?new Color(.55f,.57f,.49f):new Color(.43f,.47f,.40f));
            for(int index=0;index<chosen.Count;index++)
            {
                var site=chosen[index];uint key=site.priority;var g=new ArchitecturalGeometry();
                Vector2 across=new Vector2(site.tangent.y,-site.tangent.x);float baseHeight=world.Layout.BankHeight(site.point);
                Vector3 Foot(Vector2 offset)
                {
                    var p=site.point+site.tangent*offset.x+across*offset.y;
                    return new Vector3(p.x-site.point.x,world.Layout.BankHeight(p)-baseHeight,p.y-site.point.y);
                }
                // A compact fan, rather than isolated hairline stalks. Each
                // blade is double-sided geometry within one shared batch.
                for(int stem=0;stem<11;stem++)
                {
                    float a=stem*2.39996f+LandscapeUnit084(key+3)*6.28f;
                    float spread=.18f+Mathf.Sqrt(stem/10f)*.70f;
                    Vector3 foot=Foot(new Vector2(Mathf.Cos(a)*spread,Mathf.Sin(a)*spread*.54f));
                    float height=(.60f+LandscapeUnit084(key+unchecked((uint)stem)+5)*.45f)*(estuary?.80f:1);
                    var tip=foot+new Vector3(Mathf.Cos(a)*.15f,height,Mathf.Sin(a)*.15f);
                    g.Beam(foot,tip,.035f,.037f,wood);
                    for(int blade=0;blade<3;blade++)
                    {
                        float angle=a+blade*2.1f;var direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                        Vector3 start=Vector3.Lerp(foot,tip,.25f+blade*.13f),end=start+direction*(tropical?.52f:.44f)+Vector3.up*(height*.34f);
                        var cross=Vector3.Cross(Vector3.up,direction)*(tropical?.15f:.13f);
                        g.Triangle(start-cross,end,start+cross,blade==1?lightLeaf:leaf);
                        g.Triangle(start+cross,end,start-cross,blade==1?lightLeaf:leaf);
                    }
                }
                for(int stone=0;stone<4;stone++)
                {
                    float x=stone==0?-.92f:stone==1?-.48f:stone==2?.28f:.78f;
                    float z=stone==0?.48f:stone==1?.82f:stone==2?.80f:.46f;
                    Vector3 foot=Foot(new Vector2(x,z));float scale=.80f+LandscapeUnit084(key+unchecked((uint)stone)+29)*.45f;
                    g.Lathe(foot,new[]{new Vector2(0,.01f),new Vector2(.28f*scale,.01f),new Vector2(.36f*scale,.14f*scale),
                        new Vector2(.20f*scale,.32f*scale),new Vector2(0,.36f*scale)},gravel,5,Quaternion.Euler(0,stone*37,0));
                }
                if(index%3==0)
                {
                    var a=Foot(new Vector2(-.88f,-.66f))+Vector3.up*.09f;
                    var b=Foot(new Vector2(.82f,-.47f))+Vector3.up*.10f;
                    g.Beam(a,b,.14f,.16f,wood);
                    g.Beam(Vector3.Lerp(a,b,.62f),Foot(new Vector2(.44f,-.95f))+Vector3.up*.24f,.065f,.07f,wood);
                }
                var cluster=g.Create(dressing,"Bank grass gravel and driftwood 084 "+index);
                cluster.localPosition=new Vector3(site.point.x,baseHeight,site.point.y);
                var renderer=cluster.GetComponent<MeshRenderer>();
                // Low grasses must survive the normal zoom26 view. The batch
                // budget is fixed; cull only once the whole cluster is tiny.
                var lod=cluster.gameObject.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.008f,new[]{renderer})});lod.RecalculateBounds();
                visibility.Add(renderer,site.point);
            }
            visibility.Refresh(true);
        }
    }
}
