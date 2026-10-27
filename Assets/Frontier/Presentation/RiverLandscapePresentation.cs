using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Surface = Frontier.ArchitecturalMaterials.Surface;

namespace Frontier
{
    // All visible shore heights and bridge transforms come from the same layout
    // used by navigation. These meshes contain no colliders or game authority.
    public static partial class RiverLandscapePresentation
    {
        static Material Wood=>ArchitecturalMaterials.Get(Surface.Wood,new Color(.40f,.275f,.15f));
        static Material PaleWood=>ArchitecturalMaterials.Get(Surface.Wood,new Color(.63f,.47f,.28f));
        static Material DarkWood=>ArchitecturalMaterials.Get(Surface.Wood,new Color(.23f,.16f,.10f));
        static Material Stone=>ArchitecturalMaterials.Get(Surface.Stone,new Color(.46f,.45f,.39f));
        static readonly Dictionary<string,Material> groundMaterials=new Dictionary<string,Material>();

        static Material GroundMaterial(string map)
        {
            bool tropical=map==MapTopology.Tropical,winding=map==MapTopology.Winding;
            // Winding owns its sediment palette; never tint other temperate
            // rivers through the shared material cache. Protected pads retain
            // their old height/layer profiles, not their former sand tint.
            string key=tropical?"tropical":winding?"winding":"temperate";
            if(groundMaterials.TryGetValue(key,out var cached)&&cached!=null)return cached;
            var shader=Resources.Load<Shader>("RiverGround");if(shader==null)return LandscapePresentation.GroundMaterial();
            cached=new Material(shader){name="Frontier / continuous "+key+" riverbank layers"};
            var meadow=LandscapePresentation.GroundMaterial();var soil=ArchitecturalMaterials.Get(Surface.Earth,Color.white);
            cached.SetTexture("_GrassMap",meadow.GetTexture("_BaseMap"));cached.SetTexture("_GrassNormals",meadow.GetTexture("_BumpMap"));
            cached.SetTexture("_SoilMap",soil.GetTexture("_BaseMap"));cached.SetTexture("_SoilNormals",soil.GetTexture("_BumpMap"));
            cached.SetColor("_TurfTint",tropical?new Color(.79f,1.06f,.76f):new Color(.82f,.98f,.77f));
            cached.SetColor("_MudColor",tropical?new Color(.43f,.335f,.22f):new Color(.42f,.385f,.285f));
            cached.SetColor("_SandColor",tropical?new Color(.69f,.57f,.36f):winding?new Color(.72f,.63f,.43f):new Color(.65f,.59f,.42f));
            LandscapeArt085.ApplyRiver(cached);
            groundMaterials[key]=cached;return cached;
        }

        public static Transform Build(Transform parent,World world,Material water)
        {
            var root=new GameObject("Sculpted "+world.MapId+" river valley").transform;root.SetParent(parent,false);
            BuildGround(root,world.Layout,world.Seed);
            int index=0;
            if(world.Layout.Channels.Length>1)ConfluenceWaterPresentation.Build(root,world.Layout,water);
            else foreach(var channel in world.Layout.Channels)BuildChannel(root,channel,water,index++,world.Layout.WaterHeight);
            if(world.Layout.HasWater)BuildBanks(root,world);
            if(world.MapId==MapTopology.Tropical)BuildTropicalPalms(root,world);
            BuildConfluenceLandmark082(root,world);
            if(world.Layout.HasWater)BuildBankDetail084(root,world);
            index=0;foreach(var bridge in world.Layout.Bridges)BuildBridge(root,bridge,index++);
            return root;
        }

        static Transform MeshObject(Transform parent,string name,Mesh mesh,Material[] materials)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=materials;
            go.AddComponent<ProceduralMeshLifetime>().OwnedMesh=mesh;
            return go.transform;
        }

        static void BuildGround(Transform root,MapLayout layout,int visualSeed)
        {
            // The 1m triangle planes can occlude the narrow outer Winding shore
            // even when BankHeight at the water edge is submerged. Match this
            // map's .5m authority sampling without changing its height queries,
            // water strip, colliders or navigation. Other/scenario maps retain
            // their existing geometry and cost; aligned 1m vertices are kept.
            int count=layout.Id==MapTopology.Winding&&!layout.HasScenario?257:129;
            float cell=128f/(count-1);
            var vertices=new Vector3[count*count];var uv=new Vector2[vertices.Length];var layers=new Color[vertices.Length];
            var triangles=new List<int>();
            var authored=layout.HasScenario?new[]{new List<int>(),new List<int>(),new List<int>(),new List<int>()}:null;
            for(int z=0;z<count;z++)for(int x=0;x<count;x++)
            {
                int i=x+z*count;var p=new Vector2(x*cell,z*cell);
                // The bank query excludes bridges, leaving a real channel under
                // the bridge deck instead of raising the riverbed into its roof.
                vertices[i]=new Vector3(p.x,layout.BankHeight(p),p.y);uv[i]=p/128;
                // Shared map-owned bend profile: inner sediment lobes and outer
                // cut-banks agree with picking/heights, not a second shore mask.
                layers[i]=layout.BankLayers(p);
            }
            for(int z=0;z<count-1;z++)for(int x=0;x<count-1;x++)
            {
                int a=x+z*count,b=a+count,c=b+1,d=a+1;
                if(authored!=null)authored[layout.SurfaceKind(new Vector2((x+.5f)*cell,(z+.5f)*cell))].AddRange(new[]{a,b,c,a,c,d});
                else if(ConfluenceContent082.Applies(layout))
                {
                    // At a curved bank, a fixed diagonal can cross a submerged
                    // query with a raised 1m plane. Choose the diagonal whose
                    // midpoint best matches the same physical height authority.
                    // No special failed coordinate, extra geometry or bed offset.
                    float middle=layout.BankHeight(new Vector2(x+.5f,z+.5f));
                    float ac=Mathf.Abs((vertices[a].y+vertices[c].y)*.5f-middle);
                    float bd=Mathf.Abs((vertices[b].y+vertices[d].y)*.5f-middle);
                    triangles.AddRange(bd+.000001f<ac?new[]{a,b,d,b,c,d}:new[]{a,b,c,a,c,d});
                }
                else triangles.AddRange(new[]{a,b,c,a,c,d});
            }
            var mesh=new Mesh{name="River valley ground with submerged bed",indexFormat=IndexFormat.UInt32};
            mesh.vertices=vertices;mesh.uv=uv;mesh.colors=layers;
            if(authored!=null){mesh.subMeshCount=4;for(int i=0;i<4;i++)mesh.SetTriangles(authored[i],i);}
            else mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            var materials=authored==null?new[]{GroundMaterial(layout.Id)}:new[]{LandscapeArt085.GroundMaterial(),
                ArchitecturalMaterials.Get(Surface.Earth,new Color(.62f,.555f,.37f)),
                ArchitecturalMaterials.Get(Surface.Earth,new Color(.39f,.335f,.225f)),
                ArchitecturalMaterials.Get(Surface.Stone,new Color(.42f,.45f,.43f))};
            var ground=MeshObject(root,"Continuous sculpted land and river bed",mesh,materials);
            if(authored==null)ApplyGroundDetail084(ground.GetComponent<MeshRenderer>(),layout.Id,visualSeed);
        }

        static void BuildChannel(Transform root,RiverChannel channel,Material water,int index,float waterHeight)
        {
            // Five rows provide a depth gradient and visible shallows along both
            // banks. Arc-length UVs carry moving ripples around every bend.
            const int rows=5;int samples=channel.Centers.Length;
            var vertices=new Vector3[samples*rows];var uv=new Vector2[vertices.Length];var colors=new Color[vertices.Length];
            var triangles=new List<int>();float arc=0;
            for(int i=0;i<samples;i++)
            {
                var c=channel.Centers[i];if(i>0)arc+=Vector2.Distance(c,channel.Centers[i-1]);
                var tangent=(channel.Centers[Mathf.Min(samples-1,i+1)]-channel.Centers[Mathf.Max(0,i-1)]).normalized;
                var across=new Vector2(tangent.y,-tangent.x);
                for(int row=0;row<rows;row++)
                {
                    float fraction=row/(float)(rows-1),offset=(fraction*2-1)*channel.HalfWidths[i];
                    var p=c+across*offset;int v=i*rows+row;
                    vertices[v]=new Vector3(Mathf.Clamp(p.x,0,128),waterHeight,Mathf.Clamp(p.y,0,128));
                    uv[v]=new Vector2(fraction,arc/12);float depth=1-Mathf.Abs(fraction*2-1);
                    colors[v]=new Color(Mathf.Sqrt(depth),0,0,1);
                }
            }
            for(int i=0;i<samples-1;i++)for(int row=0;row<rows-1;row++)
            {
                int a=i*rows+row,b=a+rows,c=b+1,d=a+1;
                // Authored channels extend outside the map so the border is
                // natural. Collapse that extension without zero-area triangles.
                if(Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]).sqrMagnitude>.0000001f)triangles.AddRange(new[]{a,b,c});
                if(Vector3.Cross(vertices[c]-vertices[a],vertices[d]-vertices[a]).sqrMagnitude>.0000001f)triangles.AddRange(new[]{a,c,d});
            }
            var mesh=new Mesh{name="River channel "+index+" arc length current"};mesh.vertices=vertices;mesh.uv=uv;mesh.colors=colors;mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            var surface=MeshObject(root,index==0?"Continuous navigable river":"Navigable tributary "+index,mesh,new[]{water});
            surface.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }

        static void BuildBanks(Transform root,World world)
        {
            var g=new ArchitecturalGeometry();var reed=ArchitecturalMaterials.Get(Surface.Cloth,new Color(.37f,.41f,.205f));
            var stalk=ArchitecturalMaterials.Get(Surface.Wood,new Color(.47f,.40f,.22f));
            var pebble=ArchitecturalMaterials.Get(Surface.Stone,new Color(.55f,.535f,.43f));
            int sample=0;
            foreach(var channel in world.Layout.Channels)
            {
                for(int i=3;i<channel.Centers.Length-3;i+=7)
                {
                    var c=channel.Centers[i];var tangent=(channel.Centers[i+1]-channel.Centers[i-1]).normalized;var normal=new Vector2(tangent.y,-tangent.x);
                    foreach(int side in new[]{-1,1})
                    {
                        // Group sizes vary deterministically. No Unity Random
                        // state is touched, and future docks remain game-owned.
                        var p=c+normal*side*(channel.HalfWidths[i]+1.25f);
                        if(p.x<2||p.x>126||p.y<2||p.y>126||world.Layout.IsBridgeApproach(p)||NearEntity(world,p,3.5f)||NearHarbor(world.Layout,p,5.5f))continue;
                        int clump=3+(sample++%4);
                        for(int n=0;n<clump;n++)
                        {
                            float angle=n*2.39996f+sample*.71f;
                            var q=p+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(.15f+n*.10f);
                            float y=world.Layout.BankHeight(q),h=.35f+(n%3)*.13f;
                            if(world.MapId==MapTopology.Estuary&&q.y>82)h*=.58f;
                            var foot=new Vector3(q.x,y,q.y);
                            g.Beam(foot,foot+new Vector3(.04f,h,.025f),.023f,.023f,stalk);
                            for(int leaf=0;leaf<2;leaf++)
                            {
                                float sign=leaf==0?-1:1;var middle=foot+Vector3.up*h*.40f;
                                var tip=foot+new Vector3(sign*.16f,h*.82f,.06f);
                                g.Triangle(middle+Vector3.right*.027f,tip,middle-Vector3.right*.027f,reed);
                                g.Triangle(middle-Vector3.right*.027f,tip,middle+Vector3.right*.027f,reed);
                            }
                        }
                        if(sample%3!=0)continue;
                        var stonePoint=c+normal*side*(channel.HalfWidths[i]+3.2f);
                        if(world.Layout.IsBridgeApproach(stonePoint)||NearEntity(world,stonePoint,3.5f)||NearHarbor(world.Layout,stonePoint,5.5f))continue;
                        for(int n=0;n<3;n++)
                        {
                            var q=stonePoint+new Vector2(n*.35f,(n%2)*.32f);
                            g.Box(new Vector3(q.x,world.Layout.BankHeight(q)+.08f,q.y),new Vector3(.33f,.17f,.25f),pebble,Quaternion.Euler(5,n*31,12));
                        }
                    }
                }
            }
            var vegetation=g.Create(root,"Clustered bank reeds and gravel");
            var lod=vegetation.gameObject.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.035f,new[]{vegetation.GetComponent<MeshRenderer>()})});lod.RecalculateBounds();
        }

        static bool NearEntity(World world,Vector2 p,float radius)
        {
            foreach(var entity in world.Entities)
                if((entity.position-p).sqrMagnitude<radius*radius)return true;
            return false;
        }

        static bool NearHarbor(MapLayout layout,Vector2 p,float radius)
        {
            foreach(var bank in layout.DockSites)foreach(var site in bank)
                if((site-p).sqrMagnitude<radius*radius)return true;
            return false;
        }

        static bool NearTradeRoute(MapLayout layout,Vector2 point,float margin)
        {
            for(int i=0;i<layout.TradeRoute.Length-1;i++)
            {
                Vector2 a=layout.TradeRoute[i],delta=layout.TradeRoute[i+1]-a;
                float t=Mathf.Clamp01(Vector2.Dot(point-a,delta)/Mathf.Max(.001f,delta.sqrMagnitude));
                if((point-a-delta*t).sqrMagnitude<margin*margin)return true;
            }
            return false;
        }

        static void BuildTropicalPalms(Transform root,World world)
        {
            var visibility=root.gameObject.AddComponent<RiverSceneryVisibility>();visibility.Initialize(world);
            var bark=ArchitecturalMaterials.Get(Surface.Wood,new Color(.46f,.345f,.22f));
            var leaves=ArchitecturalMaterials.Get(Surface.Cloth,new Color(.21f,.40f,.155f));
            var sunLeaves=ArchitecturalMaterials.Get(Surface.Cloth,new Color(.37f,.50f,.205f));
            var channel=world.Layout.Channels[0];int count=BuildTropicalBayLandmarks081(root,world,visibility,bark,leaves,sunLeaves);
            // At most eight modest batches, not per-leaf GameObjects. Their map
            // sites are deterministic and give both banks a distinct silhouette.
            for(int i=16;i<channel.Centers.Length-16&&count<8;i+=23)
            {
                Vector2 c=channel.Centers[i],tangent=(channel.Centers[i+1]-channel.Centers[i-1]).normalized,normal=new Vector2(tangent.y,-tangent.x);
                foreach(int side in new[]{-1,1})
                {
                    Vector2 p=c+normal*side*(channel.HalfWidths[i]+6);
                    if(count>=8||p.x<5||p.x>123||p.y<5||p.y>123||world.Layout.IsBridgeApproach(p)||world.Layout.IsBridgeSideBarrier(p)||NearHarbor(world.Layout,p,7)||NearEntity(world,p,6)||NearTradeRoute(world.Layout,p,4))continue;
                    var g=new ArchitecturalGeometry();
                    Palm(g,Vector3.zero,2.75f,.2f,count*.73f,bark,leaves,sunLeaves);
                    Palm(g,new Vector3(.94f,-.02f,.64f),2.1f,-.18f,count*.73f+2,bark,leaves,sunLeaves);
                    // Low broad leaves under the palm make the understory read
                    // as humid woodland without adding a third tree silhouette.
                    for(int leaf=0;leaf<7;leaf++)
                    {
                        float angle=leaf*Mathf.PI*2/7;var direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                        Vector3 start=new Vector3(-.55f,.13f,.25f),tip=start+direction*.58f+Vector3.up*.32f,cross=Vector3.Cross(Vector3.up,direction)*.16f;
                        g.Triangle(start,tip+cross,tip,leaf%2==0?leaves:sunLeaves);g.Triangle(start,tip,tip-cross,leaves);
                        g.Triangle(tip,start,tip+cross,leaves);g.Triangle(tip-cross,tip,start,leaves);
                    }
                    var cluster=g.Create(root,"Tropical palm and broadleaf cluster "+count++);
                    cluster.localPosition=new Vector3(p.x,world.Layout.BankHeight(p),p.y);
                    var renderer=cluster.GetComponent<MeshRenderer>();
                    var lod=cluster.gameObject.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.035f,new[]{renderer})});lod.RecalculateBounds();
                    visibility.Add(renderer,p);
                }
            }
            visibility.Refresh(true);
        }

        static void Palm(ArchitecturalGeometry g,Vector3 basePoint,float height,float lean,float phase,Material bark,Material leaves,Material lightLeaves)
        {
            Vector3 Top(float t)=>basePoint+new Vector3(lean*t*t,t*height,Mathf.Sin(phase)*t*t*.16f);
            for(int ring=0;ring<5;ring++)
            {
                Vector3 a=Top(ring/5f),b=Top((ring+1)/5f);float radius=.11f-ring*.01f;
                g.Lathe((a+b)*.5f,new[]{new Vector2(radius,-(b-a).magnitude*.5f),new Vector2(radius*.94f,(b-a).magnitude*.5f)},bark,8,Quaternion.FromToRotation(Vector3.up,b-a));
            }
            Vector3 crown=Top(1);
            for(int frond=0;frond<9;frond++)
            {
                float angle=phase+frond*Mathf.PI*2/9;Vector3 direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle)),cross=Vector3.Cross(Vector3.up,direction);
                Vector3 F(float t)=>crown+direction*t*1.35f+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.33f-t*t*.45f);
                for(int part=0;part<3;part++)
                {
                    float t=part/3f,next=(part+1)/3f;
                    float width=.19f*Mathf.Sin(t*Mathf.PI)+.06f,endWidth=next>=1?0:.19f*Mathf.Sin(next*Mathf.PI)+.06f;
                    Vector3 a=F(t)-cross*width,b=F(next)-cross*endWidth,c=F(next)+cross*endWidth,d=F(t)+cross*width;
                    Material material=(frond+part)%3==0?lightLeaves:leaves;
                    g.Quad(a,b,c,d,material);g.Quad(d,c,b,a,material);
                }
            }
        }

        static void BuildBridge(Transform root,BridgeSpan span,int index)
        {
            var bridge=new GameObject("Raised crossing "+index+" on sculpted banks").transform;bridge.SetParent(root,false);
            bridge.localPosition=new Vector3(span.Center.x,0,span.Center.y);
            bridge.localRotation=Quaternion.Euler(0,-Mathf.Atan2(span.Direction.y,span.Direction.x)*Mathf.Rad2Deg,0);
            var g=new ArchitecturalGeometry();float deck=span.HalfLength,end=deck+span.RampLength,rail=span.HalfWidth-.2f;
            float Height(float x)=>span.GroundHeight(span.Point(x,0));
            foreach(float x in new[]{-deck-.35f,deck+.35f})foreach(int side in new[]{-1,1})
            {
                float footing=x<0?span.WestHeight:span.EastHeight;
                bool elevated=span.WestHeight!=0||span.EastHeight!=0;
                g.Box(new Vector3(x,elevated?(span.Height+footing)*.5f:span.Height*.45f,side*rail),new Vector3(.80f,elevated?Mathf.Max(.1f,span.Height-footing):span.Height*.9f,.65f),Stone);
                g.Box(new Vector3(x,span.Height-.28f,side*rail),new Vector3(1.15f,.2f,.85f),Stone);
            }
            int planks=Mathf.CeilToInt(end*4);
            for(int i=0;i<planks;i++)
            {
                float x=Mathf.Lerp(-end,end,(i+.5f)/planks),w=end*2/planks;
                float slope=x < -deck?(span.Height-span.WestHeight)/span.RampLength:x>deck?(span.EastHeight-span.Height)/span.RampLength:0;
                var rotation=Quaternion.Euler(0,0,Mathf.Atan(slope)*Mathf.Rad2Deg);
                g.Box(new Vector3(x,Height(x),0)-rotation*Vector3.up*.105f,new Vector3((w-.02f)*Mathf.Sqrt(1+slope*slope),.21f,span.HalfWidth*2-.22f),i%5==0?PaleWood:Wood,rotation);
            }
            foreach(int side in new[]{-1,1})
            {
                float z=side*rail;var start=new Vector3(-end,span.WestHeight-.2f,z);var a=new Vector3(-deck,span.Height-.2f,z);
                var b=new Vector3(deck,span.Height-.2f,z);var finish=new Vector3(end,span.EastHeight-.2f,z);
                g.Beam(start,a,.22f,.24f,DarkWood);g.Beam(a,b,.22f,.24f,DarkWood);g.Beam(b,finish,.22f,.24f,DarkWood);
                int posts=Mathf.CeilToInt(end);
                for(int i=0;i<=posts;i++)
                {
                    float x=Mathf.Lerp(-end,end,i/(float)posts),y=Height(x);
                    g.Box(new Vector3(x,y+.43f,z),new Vector3(.13f,.86f,.13f),DarkWood);
                    if(i==posts)continue;
                    float nx=Mathf.Lerp(-end,end,(i+1)/(float)posts),ny=Height(nx);
                    g.Beam(new Vector3(x,y+.8f,z),new Vector3(nx,ny+.8f,z),.095f,.105f,PaleWood);
                    // One crossing has crossed side rails; the other remains
                    // open timber. Both keep an identical clear deck envelope.
                    if(index%2==1)
                    {g.Beam(new Vector3(x,y+.21f,z),new Vector3(nx,ny+.72f,z),.055f,.055f,Wood);g.Beam(new Vector3(x,y+.72f,z),new Vector3(nx,ny+.21f,z),.055f,.055f,Wood);}
                    else g.Beam(new Vector3(x,y+.35f,z),new Vector3(nx,ny+.35f,z),.07f,.08f,Wood);
                }
            }
            g.Create(bridge,"Batched valley crossing with clear water channel");
        }
    }
}
