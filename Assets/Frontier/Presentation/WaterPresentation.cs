using UnityEngine;
using UnityEngine.Rendering;
using Surface = Frontier.ArchitecturalMaterials.Surface;

namespace Frontier
{
    // Water art is a read-only view of the river map. It has no physics colliders,
    // pathfinding blockers, gathering authority or independently moving vessels.
    public static class WaterPresentation
    {
        public const float WaterLevel = MapTopology.WaterHeight;
        public const float BridgeDeckHeight = MapTopology.BridgeHeight;
        public const float MaximumBoatHeight = 2.35f;
        static Material water;
        static Material Wood => ArchitecturalMaterials.Get(Surface.Wood, new Color(.42f,.275f,.135f));
        static Material PaleWood => ArchitecturalMaterials.Get(Surface.Wood, new Color(.63f,.47f,.28f));
        static Material DarkWood => ArchitecturalMaterials.Get(Surface.Wood, new Color(.24f,.16f,.095f));
        static Material Iron => ArchitecturalMaterials.Get(Surface.Metal, new Color(.19f,.23f,.23f));
        static Material Rope => ArchitecturalMaterials.Get(Surface.Cloth, new Color(.67f,.60f,.42f));
        static Material Canvas => ArchitecturalMaterials.Get(Surface.Cloth, new Color(.84f,.80f,.66f));
        static Material Stone => ArchitecturalMaterials.Get(Surface.Stone, new Color(.45f,.43f,.35f));

        public static Material WaterMaterial()
        {
            if(water!=null)return water;
            const int size=256;var pixels=new Color32[size*size];var normalPixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float sx=x*Mathf.PI*2/size,sy=y*Mathf.PI*2/size;
                // Periodic harmonics keep seams invisible across the river tiles.
                float ripple=Mathf.Sin(sy*7+Mathf.Sin(sx*3)*.45f)*.55f+Mathf.Sin(sy*13-sx*2)*.25f+Mathf.Sin(sx*5+sy*3)*.2f;
                pixels[x+y*size]=Color.Lerp(new Color(.09f,.25f,.29f),new Color(.24f,.43f,.43f),ripple*.5f+.5f);
                float dx=Mathf.Cos(sx*5+sy*3)*.12f-Mathf.Cos(sy*13-sx*2)*.08f;
                float dy=Mathf.Cos(sy*7+Mathf.Sin(sx*3)*.45f)*.25f+Mathf.Cos(sy*13-sx*2)*.12f;
                Vector3 n=new Vector3(dx,dy,1).normalized;normalPixels[x+y*size]=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,1);
            }
            var albedo=new Texture2D(size,size,TextureFormat.RGBA32,true,false){name="River current and ripples",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};
            var normals=new Texture2D(size,size,TextureFormat.RGBA32,true,true){name="River fine ripple normals",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};
            albedo.SetPixels32(pixels);albedo.Apply(true,true);normals.SetPixels32(normalPixels);normals.Apply(true,true);
            water=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Frontier / river water",color=Color.white,enableInstancing=true};
            water.SetTexture("_BaseMap",albedo);water.SetTexture("_BumpMap",normals);water.EnableKeyword("_NORMALMAP");
            water.SetFloat("_BumpScale",.42f);water.SetFloat("_Smoothness",.72f);water.SetFloat("_Metallic",.12f);return water;
        }

        public static Transform BuildTerrain(Transform parent,World world)
        {
            if(world==null||world.MapId!=MapTopology.River)return null;
            var root=new GameObject("River, banks and two raised crossings").transform;root.SetParent(parent,false);
            var channel=new ArchitecturalGeometry();
            channel.Quad(new Vector3(MapTopology.RiverMinX,WaterLevel,0),new Vector3(MapTopology.RiverMinX,WaterLevel,128),new Vector3(MapTopology.RiverMaxX,WaterLevel,128),new Vector3(MapTopology.RiverMaxX,WaterLevel,0),WaterMaterial(),.125f);
            var surface=channel.Create(root,"Continuous navigable river");surface.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            var banks=new ArchitecturalGeometry();var edge=ArchitecturalMaterials.Get(Surface.Earth,new Color(.53f,.47f,.33f));
            foreach(float x in new[]{MapTopology.RiverMinX,MapTopology.RiverMaxX})
            {
                float sign=x==MapTopology.RiverMinX?-1:1;
                banks.Quad(new Vector3(x,.028f,0),new Vector3(x,.028f,128),new Vector3(x+sign*.48f,.035f,128),new Vector3(x+sign*.48f,.035f,0),edge);
                // The west strip uses the reverse winding because it lies left of x.
                banks.Quad(new Vector3(x+sign*.48f,.035f,0),new Vector3(x+sign*.48f,.035f,128),new Vector3(x,.028f,128),new Vector3(x,.028f,0),edge);
            }
            banks.Create(root,"Damp bank edge");
            BuildBridge(root,MapTopology.BridgeSouth);BuildBridge(root,MapTopology.BridgeNorth);return root;
        }

        static void BuildBridge(Transform root,float z)
        {
            var bridge=new GameObject("Raised crossing at "+z).transform;bridge.SetParent(root,false);
            var g=new ArchitecturalGeometry();
            float rampWest=MapTopology.RiverMinX-MapTopology.BridgeRampLength,rampEast=MapTopology.RiverMaxX+MapTopology.BridgeRampLength;
            float railZ=MapTopology.BridgeHalfWidth-.15f;
            // Bridge pillars sit on the bank, leaving the full central channel
            // unobstructed. Both ramps follow World.SurfaceHeight, not stairs.
            foreach(float x in new[]{MapTopology.RiverMinX-.5f,MapTopology.RiverMaxX+.5f})foreach(float side in new[]{-1f,1f})
            {
                g.Box(new Vector3(x,1.42f,z+side*(MapTopology.BridgeHalfWidth-.3f)),new Vector3(.80f,2.84f,.58f),Stone);
                g.Box(new Vector3(x,2.83f,z+side*(MapTopology.BridgeHalfWidth-.3f)),new Vector3(1.00f,.18f,.78f),Stone);
            }
            int plankCount=Mathf.RoundToInt((rampEast-rampWest)*2);
            for(int i=0;i<plankCount;i++)
            {
                float x=rampWest+.25f+i*.5f,level=BridgeHeight(x);
                float slope=x<MapTopology.RiverMinX?BridgeDeckHeight/MapTopology.BridgeRampLength:x>MapTopology.RiverMaxX?-BridgeDeckHeight/MapTopology.BridgeRampLength:0;var rotation=Quaternion.Euler(0,0,Mathf.Atan(slope)*Mathf.Rad2Deg);
                g.Box(new Vector3(x,level,z)-rotation*Vector3.up*.105f,new Vector3(.48f*Mathf.Sqrt(1+slope*slope),.21f,MapTopology.BridgeHalfWidth*2-.25f),i%5==0?PaleWood:Wood,rotation);
            }
            foreach(float side in new[]{-1f,1f})
            {
                float edgeZ=z+side*railZ;
                g.Beam(new Vector3(rampWest,-.16f,edgeZ),new Vector3(MapTopology.RiverMinX,BridgeDeckHeight-.16f,edgeZ),.28f,.27f,DarkWood);
                g.Beam(new Vector3(MapTopology.RiverMinX,BridgeDeckHeight-.16f,edgeZ),new Vector3(MapTopology.RiverMaxX,BridgeDeckHeight-.16f,edgeZ),.28f,.27f,DarkWood);
                g.Beam(new Vector3(MapTopology.RiverMaxX,BridgeDeckHeight-.16f,edgeZ),new Vector3(rampEast,-.16f,edgeZ),.28f,.27f,DarkWood);
                int posts=Mathf.CeilToInt((rampEast-rampWest)/2);
                for(int i=0;i<=posts;i++)
                {
                    float x=Mathf.Lerp(rampWest,rampEast,i/(float)posts),level=BridgeHeight(x);
                    g.Box(new Vector3(x,level+.42f,edgeZ),new Vector3(.13f,.88f,.13f),DarkWood);
                    if(i==posts)continue;
                    float nx=Mathf.Lerp(rampWest,rampEast,(i+1)/(float)posts),nlevel=BridgeHeight(nx);
                    g.Beam(new Vector3(x,level+.81f,edgeZ),new Vector3(nx,nlevel+.81f,edgeZ),.10f,.12f,PaleWood);
                    g.Beam(new Vector3(x,level+.36f,edgeZ),new Vector3(nx,nlevel+.36f,edgeZ),.075f,.09f,Wood);
                }
            }
            g.Create(bridge,"Batched timber crossing and masonry abutments");
        }
        static float BridgeHeight(float x)=>MapTopology.GroundHeight(new Vector2(x,MapTopology.BridgeSouth));

        // local +X is the water-facing side of a dock. WorldView owns rotation.
        // Ships point along local +Z, matching normal world-unit facing.
        public static bool TryBuildEntity(Transform root,EntityState entity,Color team)
        {
            if(root==null||entity?.def==null)return false;
            switch(entity.def.id)
            {
                case "dock":BuildDock(root,team);return true;
                case "fishing_boat":BuildBoat(root,team,false);return true;
                case "gunboat":BuildBoat(root,team,true);return true;
                case "fish":BuildFish(root);return true;
                default:return false;
            }
        }

        static void BuildDock(Transform root,Color team)
        {
            var foundation=new ArchitecturalGeometry();var walls=new ArchitecturalGeometry();var roof=new ArchitecturalGeometry();var detail=new ArchitecturalGeometry();
            // All boards, the warehouse and crane fit in the 4 x 4 footprint.
            for(int i=0;i<16;i++)foundation.Box(new Vector3(-1.815f+i*.242f,.16f,0),new Vector3(.228f,.18f,3.84f),i%4==0?PaleWood:Wood);
            foreach(float x in new[]{-1.78f,1.78f})foreach(float z in new[]{-1.75f,1.75f})
            {foundation.Cylinder(new Vector3(x,.12f,z),.11f,.45f,DarkWood,12);detail.Cylinder(new Vector3(x,.52f,z),.085f,.65f,DarkWood,12);detail.Cylinder(new Vector3(x,.69f,z),.11f,.15f,Rope,12);}
            walls.Box(new Vector3(-.95f,1.0f,0),new Vector3(1.50f,1.56f,2.54f),Wood);
            walls.Box(new Vector3(-.16f,.88f,-.35f),new Vector3(.055f,1.21f,.84f),DarkWood);
            for(int i=0;i<5;i++)walls.Box(new Vector3(-.123f,.88f,-.67f+i*.158f),new Vector3(.031f,1.14f,.145f),PaleWood);
            foreach(float z in new[]{-.83f,.92f})
            {walls.Box(new Vector3(-.168f,1.22f,z),new Vector3(.06f,.49f,.43f),DarkWood);walls.Box(new Vector3(-.12f,1.22f,z),new Vector3(.024f,.035f,.44f),PaleWood);walls.Box(new Vector3(-.12f,1.22f,z),new Vector3(.024f,.49f,.035f),PaleWood);}
            foreach(float z in new[]{-1.3f,1.3f})foreach(float x in new[]{-1.73f,-.17f})walls.Box(new Vector3(x,1.0f,z),new Vector3(.13f,1.61f,.13f),PaleWood);
            roof.Quad(new Vector3(-1.86f,1.78f,-1.46f),new Vector3(-1.86f,1.78f,1.46f),new Vector3(-.95f,2.31f,1.46f),new Vector3(-.95f,2.31f,-1.46f),Canvas);
            roof.Quad(new Vector3(-.95f,2.31f,-1.46f),new Vector3(-.95f,2.31f,1.46f),new Vector3(-.045f,1.78f,1.46f),new Vector3(-.045f,1.78f,-1.46f),Canvas);
            roof.Triangle(new Vector3(-1.86f,1.78f,-1.46f),new Vector3(-.95f,2.31f,-1.46f),new Vector3(-.045f,1.78f,-1.46f),Wood);
            roof.Triangle(new Vector3(-.045f,1.78f,1.46f),new Vector3(-.95f,2.31f,1.46f),new Vector3(-1.86f,1.78f,1.46f),Wood);
            var banner=ArchitecturalMaterials.Get(Surface.Cloth,team);
            detail.Box(new Vector3(-.11f,1.55f,.02f),new Vector3(.06f,.21f,1.17f),banner);
            detail.Cylinder(new Vector3(.65f,1.30f,-1.05f),.11f,2.16f,DarkWood,12);
            detail.Beam(new Vector3(.65f,2.27f,-1.05f),new Vector3(1.76f,2.03f,-1.05f),.14f,.16f,Wood);
            detail.Beam(new Vector3(.65f,1.4f,-1.05f),new Vector3(1.67f,2.03f,-1.05f),.075f,.075f,PaleWood);
            detail.Beam(new Vector3(1.69f,2.03f,-1.05f),new Vector3(1.69f,.82f,-1.05f),.025f,.025f,Rope);
            detail.Cylinder(new Vector3(1.69f,.78f,-1.05f),.065f,.15f,Iron,10);
            Barrel(detail,new Vector3(.37f,.25f,.97f),.23f,.60f);Barrel(detail,new Vector3(.90f,.25f,1.02f),.22f,.52f);
            detail.Box(new Vector3(.61f,.43f,.45f),new Vector3(.55f,.36f,.44f),Wood);
            for(int i=0;i<5;i++)detail.Beam(new Vector3(.20f,.62f,.17f+i*.1f),new Vector3(1.12f,.31f,.17f+i*.1f),.02f,.02f,Rope);
            foundation.Create(root,"Dock piles and plank platform");var wallRoot=walls.Create(root,"Dock warehouse walls");var roofRoot=roof.Create(root,"Dock pitched canvas roof");var details=detail.Create(root,"Dock crane rope cargo and team sign");
            var architecture=root.gameObject.AddComponent<ArchitectureModel>();architecture.Civilization="dock";architecture.Age=1;
            architecture.Walls=wallRoot;architecture.Roofs=roofRoot;architecture.Details=details;
            architecture.LocalBounds=new Bounds(new Vector3(0,1.20f,0),new Vector3(3.88f,2.6f,3.88f));
            architecture.SetConstruction(1);
        }

        static void BuildBoat(Transform root,Color team,bool gunboat)
        {
            var rig=root.gameObject.AddComponent<UnitRig>();
            var hull=new ArchitecturalGeometry();var cargo=new ArchitecturalGeometry();
            float width=gunboat?.76f:.63f,length=gunboat?1.92f:1.64f;
            Hull(hull,width,length,team);
            var cloth=ArchitecturalMaterials.Get(Surface.Cloth,team);
            if(gunboat)
            {
                cargo.Box(new Vector3(0,.55f,-.62f),new Vector3(.96f,.34f,.84f),Wood);
                cargo.Box(new Vector3(0,.74f,-.62f),new Vector3(1.04f,.07f,.95f),PaleWood);
                cargo.Box(new Vector3(0,.57f,.75f),new Vector3(.72f,.25f,.57f),DarkWood);
                foreach(float x in new[]{-.39f,.39f})cargo.Cylinder(new Vector3(x,.58f,.71f),.13f,.08f,Iron,12,Quaternion.Euler(0,0,90));
                var barrel=new ArchitecturalGeometry();
                // True open muzzle: outer rings return along the bore.
                barrel.Lathe(Vector3.zero,new[]{new Vector2(.17f,-.48f),new Vector2(.19f,-.34f),new Vector2(.145f,.40f),new Vector2(.18f,.46f),new Vector2(.18f,.55f),new Vector2(.115f,.55f),new Vector2(.105f,.36f),new Vector2(.10f,-.25f)},Iron,18);
                var pivot=new GameObject("Gunboat deck cannon recoil").transform;pivot.SetParent(root,false);pivot.localPosition=new Vector3(0,.83f,.94f);pivot.localRotation=Quaternion.Euler(90,0,0);
                barrel.Create(pivot,"Gunboat open bore iron cannon");rig.Barrel=pivot;rig.ArtilleryRecoil=.12f;rig.ArtilleryTravelTilt=0;
                cargo.Box(new Vector3(-.38f,.66f,-.67f),new Vector3(.11f,.22f,.18f),Iron);
                Barrel(cargo,new Vector3(.36f,.41f,-1.19f),.17f,.39f);
            }
            else
            {
                // Low open canopy and furled mast fit underneath both bridges.
                foreach(float x in new[]{-.43f,.43f})foreach(float z in new[]{-.76f,.15f})cargo.Cylinder(new Vector3(x,.88f,z),.038f,1.10f,DarkWood,8);
                cargo.Quad(new Vector3(-.52f,1.43f,-.90f),new Vector3(-.52f,1.43f,.29f),new Vector3(.52f,1.49f,.29f),new Vector3(.52f,1.49f,-.90f),Canvas);
                cargo.Quad(new Vector3(.52f,1.49f,-.90f),new Vector3(.52f,1.49f,.29f),new Vector3(-.52f,1.43f,.29f),new Vector3(-.52f,1.43f,-.90f),Canvas);
                Barrel(cargo,new Vector3(-.24f,.39f,.69f),.18f,.43f);
                cargo.Box(new Vector3(.19f,.50f,-.52f),new Vector3(.38f,.20f,.57f),PaleWood);
                for(int i=0;i<7;i++)cargo.Beam(new Vector3(-.43f,.74f,.37f+i*.065f),new Vector3(.42f,.39f,.37f+i*.065f),.015f,.015f,Rope);
                for(int i=0;i<7;i++)cargo.Beam(new Vector3(-.43f+i*.14f,.74f-i*.058f,.37f),new Vector3(-.43f+i*.14f,.74f-i*.058f,.76f),.015f,.015f,Rope);
                // The stowed oars remain inside the collision-width envelope.
                foreach(float side in new[]{-1f,1f})
                {cargo.Beam(new Vector3(side*.56f,.58f,-1.01f),new Vector3(side*.56f,.58f,.81f),.035f,.035f,PaleWood);cargo.Box(new Vector3(side*.56f,.58f,-1.15f),new Vector3(.12f,.035f,.34f),Wood);}
            }
            cargo.Cylinder(new Vector3(.23f,1.14f,-1.11f),.025f,1.58f,DarkWood,10);
            cargo.Quad(new Vector3(.23f,1.94f,-1.11f),new Vector3(.23f,1.64f,-1.11f),new Vector3(.65f,1.67f,-1.11f),new Vector3(.72f,1.89f,-1.11f),cloth);
            cargo.Quad(new Vector3(.72f,1.89f,-1.11f),new Vector3(.65f,1.67f,-1.11f),new Vector3(.23f,1.64f,-1.11f),new Vector3(.23f,1.94f,-1.11f),cloth);
            hull.Create(root,gunboat?"Gunboat planked hull and deck":"Fishing launch planked hull and deck");cargo.Create(root,gunboat?"Gunboat carriage lockers flag and ammunition":"Fishing launch canopy net barrels and oars");
            rig.CaptureInitialPose();
        }

        static void Hull(ArchitecturalGeometry g,float width,float length,Color team)
        {
            const int segments=16;Vector3 P(int i,float y,float scale)
            {
                float a=i*Mathf.PI*2/segments;float z=Mathf.Cos(a)*length;
                // Pointed bow and broad stern read as a vessel from strategy zoom.
                float taper=Mathf.Cos(a)>0?.76f:1;return new Vector3(Mathf.Sin(a)*width*taper*scale,y,z*scale);
            }
            var paint=ArchitecturalMaterials.Get(Surface.Wood,Color.Lerp(team,new Color(.32f,.24f,.14f),.35f));
            for(int ring=0;ring<4;ring++)for(int i=0;i<segments;i++)
            {
                float lower=-.12f+ring*.125f,upper=lower+.119f,lo=.58f+ring*.105f,hi=lo+.105f;
                g.Quad(P(i+1,lower,lo),P(i+1,upper,hi),P(i,upper,hi),P(i,lower,lo),ring==3?paint:ring%2==0?Wood:DarkWood);
            }
            for(int i=0;i<segments;i++)
            {
                Vector3 a=P(i,.41f,1),b=P(i+1,.41f,1);g.Beam(a,b,.065f,.075f,PaleWood);
                g.Triangle(new Vector3(0,.31f,0),P(i,.31f,.90f),P(i+1,.31f,.90f),PaleWood);
            }
            for(int row=-5;row<=5;row++)
            {
                float z=row*length/6;float span=Mathf.Sqrt(Mathf.Max(0,1-z*z/(length*length)))*width*.84f;
                g.Box(new Vector3(0,.33f,z),new Vector3(span*2,.032f,.024f),DarkWood);
            }
            g.Box(new Vector3(0,.50f,-length*.68f),new Vector3(width*1.40f,.08f,.19f),Wood);
            g.Beam(new Vector3(0,.30f,-length*.86f),new Vector3(0,.63f,-length*.58f),.035f,.035f,Iron);
        }

        static void Barrel(ArchitecturalGeometry g,Vector3 bottom,float radius,float height)
        {
            g.Lathe(bottom,new[]{new Vector2(radius*.83f,0),new Vector2(radius,height*.2f),new Vector2(radius,height*.78f),new Vector2(radius*.83f,height)},Wood,12);
            foreach(float y in new[]{height*.14f,height*.84f})g.Cylinder(bottom+Vector3.up*y,radius*1.02f,.035f,Iron,12);
            g.Cylinder(bottom+Vector3.up*height,radius*.84f,.024f,PaleWood,12);
        }

        static void BuildFish(Transform root)
        {
            var bodies=new ArchitecturalGeometry();var ripples=new ArchitecturalGeometry();
            var silver=ArchitecturalMaterials.Get(Surface.Metal,new Color(.72f,.77f,.62f));
            var fins=ArchitecturalMaterials.Get(Surface.Cloth,new Color(.24f,.39f,.39f));
            var foam=ArchitecturalMaterials.Get(Surface.Cloth,new Color(.56f,.72f,.67f));
            for(int i=0;i<5;i++)
            {
                float x=(i%3-1)*.43f,z=(i/3-.5f)*.69f;float direction=i%2==0?1:-1;
                Vector3 P(float px,float py,float pz)=>new Vector3(x+px,py,z+pz*direction);
                Vector3 nose=P(0,.12f,.29f),tail=P(0,.075f,-.24f),left=P(-.105f,.075f,0),right=P(.105f,.075f,0),back=P(0,.185f,0);
                void Face(Vector3 a,Vector3 b,Vector3 c,Material mat) { if(direction>0)bodies.Triangle(a,c,b,mat);else bodies.Triangle(a,b,c,mat); }
                Face(nose,left,back,silver);Face(nose,back,right,silver);Face(tail,back,left,silver);Face(tail,right,back,silver);
                Face(tail,P(-.14f,.075f,-.41f),P(.14f,.075f,-.41f),fins);
                bodies.Triangle(back,P(0,.27f,-.08f),tail,fins);bodies.Triangle(tail,P(0,.27f,-.08f),back,fins);
            }
            for(int ring=0;ring<3;ring++)for(int segment=0;segment<28;segment++)
            {
                // Short interrupted rings distinguish a gatherable fish shoal
                // from the fine regular ripples already on the river surface.
                if((segment+ring*3)%9<2)continue;
                float a=segment*Mathf.PI*2/28,b=(segment+1)*Mathf.PI*2/28;
                float rx=.90f+ring*.22f,rz=.71f+ring*.16f;
                Vector3 P(float t,float d)=>new Vector3(Mathf.Cos(t)*(rx+d),.045f,Mathf.Sin(t)*(rz+d));
                ripples.Quad(P(a,0),P(b,0),P(b,.028f),P(a,.028f),foam);
            }
            bodies.Create(root,"Five visible shoal fish and fins");ripples.Create(root,"Broken shoal surface ripple rings");
        }
    }
}
