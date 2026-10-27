using UnityEngine;
using UnityEngine.Rendering;
using Surface = Frontier.ArchitecturalMaterials.Surface;

namespace Frontier
{
    // Water art is a read-only view of the river map. It has no physics colliders,
    // pathfinding blockers, gathering authority or independently moving vessels.
    public static partial class WaterPresentation
    {
        public const float WaterLevel = MapTopology.WaterHeight;
        public const float BridgeDeckHeight = MapTopology.BridgeHeight;
        public const float MaximumBoatHeight = 2.35f;
        static Material water;
        static Material wake;
        static Material Wood => ArchitecturalMaterials.Get(Surface.Wood, new Color(.42f,.275f,.135f));
        static Material PaleWood => ArchitecturalMaterials.Get(Surface.Wood, new Color(.63f,.47f,.28f));
        static Material DarkWood => ArchitecturalMaterials.Get(Surface.Wood, new Color(.24f,.16f,.095f));
        static Material Iron => ArchitecturalMaterials.Get(Surface.Metal, new Color(.19f,.23f,.23f));
        static Material Rope => ArchitecturalMaterials.Get(Surface.Cloth, new Color(.67f,.60f,.42f));
        static Material Canvas => ArchitecturalMaterials.Get(Surface.Cloth, new Color(.84f,.80f,.66f));
        static Material Stone => ArchitecturalMaterials.Get(Surface.Stone, new Color(.45f,.43f,.35f));

        // Called once by the view. Pausing the authoritative world also pauses
        // the current; animation never changes water navigation or vessel speed.
        public static void Tick(World world)
        {
            if(world==null||!world.Layout.HasWater)return;
            var material=WaterMaterial();material.SetFloat("_FlowTime",world.Time);
            material.SetFloat("_RiverTheme",world.MapId==MapTopology.Estuary?1:world.MapId==MapTopology.Tropical?.45f:0);
        }

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
            var riverShader=Resources.Load<Shader>("RiverFlow");
            water=new Material(riverShader!=null?riverShader:Shader.Find("Universal Render Pipeline/Lit")){name="Frontier / river water",color=Color.white,enableInstancing=true};
            water.SetTexture("_BaseMap",albedo);water.SetTexture("_BumpMap",normals);water.EnableKeyword("_NORMALMAP");
            water.SetFloat("_BumpScale",.42f);water.SetFloat("_Smoothness",.72f);water.SetFloat("_Metallic",.12f);return water;
        }

        public static Transform BuildTerrain(Transform parent,World world)
        {
            if(world==null||!world.Layout.HasWater&&!world.Layout.HasScenario)return null;
            if(world.MapId!=MapTopology.River)return RiverLandscapePresentation.Build(parent,world,WaterMaterial());
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
                case "transport_ship":case "frigate":case "monitor":BuildFleetVessel(root,team,entity.def.id);return true;
                case "fish":BuildFish(root);return true;
                default:return false;
            }
        }

        static void BuildDock(Transform root,Color team)
        {
            var foundation=new ArchitecturalGeometry();var walls=new ArchitecturalGeometry();var roof=new ArchitecturalGeometry();var detail=new ArchitecturalGeometry();
            // The 4x4 shore warehouse and its real 3m water extension follow
            // WaterEconomy's dock footprint. +X faces the channel everywhere.
            for(int i=0;i<16;i++)foundation.Box(new Vector3(-1.815f+i*.242f,.16f,0),new Vector3(.228f,.18f,3.84f),i%4==0?PaleWood:Wood);
            foreach(float x in new[]{-1.78f,1.78f})foreach(float z in new[]{-1.75f,1.75f})
            {foundation.Cylinder(new Vector3(x,.12f,z),.11f,.45f,DarkWood,12);detail.Cylinder(new Vector3(x,.52f,z),.085f,.65f,DarkWood,12);detail.Cylinder(new Vector3(x,.69f,z),.11f,.15f,Rope,12);}
            // A hollow storehouse with a recessed, open doorway rather than a
            // painted door over a solid box. It remains one material batch.
            walls.Box(new Vector3(-1.68f,1.0f,0),new Vector3(.13f,1.56f,2.54f),Wood);
            foreach(float z in new[]{-1.21f,1.21f})walls.Box(new Vector3(-.95f,1.0f,z),new Vector3(1.50f,1.56f,.13f),Wood);
            walls.Box(new Vector3(-.20f,1.0f,-.97f),new Vector3(.12f,1.56f,.56f),Wood);
            walls.Box(new Vector3(-.20f,1.0f,.81f),new Vector3(.12f,1.56f,.91f),Wood);
            walls.Box(new Vector3(-.20f,1.59f,-.18f),new Vector3(.12f,.37f,.96f),Wood);
            foreach(float z in new[]{-.68f,.33f})walls.Box(new Vector3(-.12f,.88f,z),new Vector3(.13f,1.25f,.09f),PaleWood);
            walls.Box(new Vector3(.16f,.88f,-.77f),new Vector3(.065f,1.20f,.72f),DarkWood,Quaternion.Euler(0,-55,0));
            detail.Box(new Vector3(-1.22f,.43f,-.41f),new Vector3(.47f,.35f,.49f),PaleWood);
            Barrel(detail,new Vector3(-.97f,.26f,.67f),.22f,.50f);
            foreach(float z in new[]{-.83f,.92f})
            {walls.Box(new Vector3(-.168f,1.22f,z),new Vector3(.06f,.49f,.43f),DarkWood);walls.Box(new Vector3(-.12f,1.22f,z),new Vector3(.024f,.035f,.44f),PaleWood);walls.Box(new Vector3(-.12f,1.22f,z),new Vector3(.024f,.49f,.035f),PaleWood);}
            foreach(float z in new[]{-1.3f,1.3f})foreach(float x in new[]{-1.73f,-.17f})walls.Box(new Vector3(x,1.0f,z),new Vector3(.13f,1.61f,.13f),PaleWood);
            BuildDockRoof085(roof);
            var banner=ArchitecturalMaterials.Get(Surface.Cloth,team);
            detail.Box(new Vector3(-.11f,1.55f,.02f),new Vector3(.06f,.21f,1.17f),banner);
            // One clear diagonal hoist reaches over the occupied quay; it adds
            // no navigable berth or separate renderers beyond the detail batch.
            detail.Cylinder(new Vector3(.65f,1.55f,-1.05f),.14f,2.64f,DarkWood,12);
            detail.Beam(new Vector3(.65f,2.72f,-1.05f),new Vector3(3.35f,2.48f,-1.05f),.20f,.22f,Wood);
            detail.Beam(new Vector3(.65f,1.52f,-1.05f),new Vector3(2.95f,2.52f,-1.05f),.10f,.12f,PaleWood);
            detail.Beam(new Vector3(3.28f,2.49f,-1.05f),new Vector3(3.28f,.92f,-1.05f),.03f,.03f,Rope);
            detail.Cylinder(new Vector3(3.28f,.83f,-1.05f),.09f,.18f,Iron,10);
            Barrel(detail,new Vector3(.37f,.25f,.97f),.23f,.60f);Barrel(detail,new Vector3(.90f,.25f,1.02f),.22f,.52f);
            detail.Box(new Vector3(.61f,.43f,.45f),new Vector3(.55f,.36f,.44f),Wood);
            for(int i=0;i<5;i++)detail.Beam(new Vector3(.20f,.62f,.17f+i*.1f),new Vector3(1.12f,.31f,.17f+i*.1f),.02f,.02f,Rope);
            // Two finger piers flank a recessed berth. The footprint blocks the
            // complete 2..5m water rectangle, so no vessel passes through piles.
            foreach(float side in new[]{-1f,1f})
            {
                for(int i=0;i<12;i++)foundation.Box(new Vector3(2.125f+i*.25f,.16f,side*1.10f),new Vector3(.234f,.18f,.73f),i%4==0?PaleWood:Wood);
                foundation.Beam(new Vector3(1.8f,.02f,side*1.40f),new Vector3(4.95f,.02f,side*1.40f),.11f,.16f,DarkWood);
                foundation.Beam(new Vector3(1.8f,.02f,side*.80f),new Vector3(4.95f,.02f,side*.80f),.11f,.16f,DarkWood);
                foreach(float x in new[]{2.25f,3.5f,4.78f})
                {
                    foundation.Cylinder(new Vector3(x,-.40f,side*1.23f),.12f,1.30f,DarkWood,10);
                    detail.Cylinder(new Vector3(x,.33f,side*1.26f),.092f,.34f,DarkWood,10);
                    detail.Cylinder(new Vector3(x,.50f,side*1.26f),.12f,.06f,Iron,10);
                    detail.Beam(new Vector3(x-.15f,.50f,side*1.26f),new Vector3(x+.15f,.50f,side*1.26f),.06f,.06f,Iron);
                    // Rope coils are low profile and stay in the water footprint.
                    detail.Lathe(new Vector3(x,.27f,side*1.03f),new[]{new Vector2(.14f,0),new Vector2(.15f,.06f),new Vector2(.06f,.06f),new Vector2(.06f,0)},Rope,10);
                }
                for(int n=0;n<3;n++)detail.Box(new Vector3(2.60f+n*.82f,.07f,side*1.46f),new Vector3(.24f,.40f,.08f),DarkWood);
            }
            // Slipway lowers toward the water between both occupied fingers.
            float slipAngle=Mathf.Atan2(-.17f,2.55f)*Mathf.Rad2Deg;
            foundation.Box(new Vector3(3.56f,.135f,0),new Vector3(2.56f,.075f,1.33f),PaleWood,Quaternion.Euler(0,0,slipAngle));
            for(int i=0;i<10;i++)foundation.Box(new Vector3(2.43f+i*.24f,.23f-i*.016f,0),new Vector3(.03f,.035f,1.35f),DarkWood);
            RefineDockArchitecture085(foundation,walls);
            var platform=foundation.Create(root,"Dock piles and plank platform");var wallRoot=walls.Create(root,"Dock warehouse walls");var roofRoot=roof.Create(root,"Dock pitched canvas roof");var details=detail.Create(root,"Dock crane rope cargo and team sign");
            var architecture=root.gameObject.AddComponent<ArchitectureModel>();architecture.Civilization="dock";architecture.Age=1;
            architecture.Walls=wallRoot;architecture.Roofs=roofRoot;architecture.Details=details;
            // These four authored sections share identity transforms in dock
            // space. Real mesh bounds include both submerged piles and the hoist.
            var authoredBounds=platform.GetComponent<MeshFilter>().sharedMesh.bounds;
            foreach(var section in new[]{wallRoot,roofRoot,details})authoredBounds.Encapsulate(section.GetComponent<MeshFilter>().sharedMesh.bounds);
            architecture.LocalBounds=authoredBounds;
            architecture.SetConstruction(1);
        }

        static void BuildBoat(Transform root,Color team,bool gunboat)
        {
            var rig=root.gameObject.AddComponent<UnitRig>();
            var motion=root.gameObject.AddComponent<BoatPresentationMotion>();
            var hull=new ArchitecturalGeometry();var cargo=new ArchitecturalGeometry();
            float width=gunboat?.76f:.63f,length=gunboat?1.92f:1.64f;
            Hull(hull,width,length,team,gunboat?"gunboat":"fishing_boat");
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
                Crew(cargo,new Vector3(-.22f,.42f,-1.07f),team,true);
                Crew(cargo,new Vector3(-.23f,.43f,.22f),team,true);
            }
            else
            {
                // Low open canopy and furled mast fit underneath both bridges.
                foreach(float x in new[]{-.43f,.43f})foreach(float z in new[]{-.76f,.15f})cargo.Cylinder(new Vector3(x,.88f,z),.038f,1.10f,DarkWood,8);
                BuildFishingCanopy085(cargo);
                Barrel(cargo,new Vector3(-.24f,.39f,.69f),.18f,.43f);
                cargo.Box(new Vector3(.19f,.50f,-.52f),new Vector3(.38f,.20f,.57f),PaleWood);
                for(int i=0;i<7;i++)cargo.Beam(new Vector3(-.43f,.74f,.37f+i*.065f),new Vector3(.42f,.39f,.37f+i*.065f),.015f,.015f,Rope);
                for(int i=0;i<7;i++)cargo.Beam(new Vector3(-.43f+i*.14f,.74f-i*.058f,.37f),new Vector3(-.43f+i*.14f,.74f-i*.058f,.76f),.015f,.015f,Rope);
                // The stowed oars remain inside the collision-width envelope.
                foreach(float side in new[]{-1f,1f})
                {cargo.Beam(new Vector3(side*.56f,.58f,-1.01f),new Vector3(side*.56f,.58f,.81f),.035f,.035f,PaleWood);cargo.Box(new Vector3(side*.56f,.58f,-1.15f),new Vector3(.12f,.035f,.34f),Wood);}
                Crew(cargo,new Vector3(-.18f,.40f,-.61f),team,true);
                Crew(cargo,new Vector3(.16f,.35f,.63f),team,true);
                var netPivot=new GameObject("Fisher lifting net basket").transform;netPivot.SetParent(root,false);netPivot.localPosition=new Vector3(.05f,.56f,.88f);
                var net=new ArchitecturalGeometry();
                net.Lathe(Vector3.zero,new[]{new Vector2(.15f,-.06f),new Vector2(.23f,.16f),new Vector2(.23f,.20f),new Vector2(.17f,.20f)},Rope,10);
                for(int strand=0;strand<6;strand++)
                {float a=strand*Mathf.PI/3;net.Beam(new Vector3(Mathf.Cos(a)*.14f,-.04f,Mathf.Sin(a)*.14f),new Vector3(Mathf.Cos(a)*.23f,.19f,Mathf.Sin(a)*.23f),.016f,.016f,Rope);}
                net.Create(netPivot,"Open fishing basket and lifting lines");motion.FishingGear=netPivot;
            }
            cargo.Cylinder(new Vector3(.23f,1.14f,-1.11f),.025f,1.58f,DarkWood,10);
            cargo.Quad(new Vector3(.23f,1.94f,-1.11f),new Vector3(.23f,1.64f,-1.11f),new Vector3(.65f,1.67f,-1.11f),new Vector3(.72f,1.89f,-1.11f),cloth);
            cargo.Quad(new Vector3(.72f,1.89f,-1.11f),new Vector3(.65f,1.67f,-1.11f),new Vector3(.23f,1.64f,-1.11f),new Vector3(.23f,1.94f,-1.11f),cloth);
            hull.Create(root,gunboat?"Gunboat planked hull and deck":"Fishing launch planked hull and deck");cargo.Create(root,gunboat?"Gunboat carriage lockers flag and ammunition":"Fishing launch canopy net barrels and oars");
            var sternOar=new GameObject("Stern sculling oar pivot").transform;sternOar.SetParent(root,false);sternOar.localPosition=new Vector3(0,.58f,-length*.68f);
            var propulsion=new ArchitecturalGeometry();
            propulsion.Beam(new Vector3(0,.33f,.24f),new Vector3(0,-.42f,-.26f),.034f,.034f,PaleWood);
            propulsion.Box(new Vector3(0,-.39f,-.23f),new Vector3(.12f,.32f,.032f),Wood,Quaternion.Euler(36,0,0));
            propulsion.Create(sternOar,"Visible stern sculling propulsion");motion.SternOar=sternOar;
            var rudder=new GameObject("Stern rudder pivot").transform;rudder.SetParent(root,false);rudder.localPosition=new Vector3(0,.13f,-length*.91f);
            var rudderMesh=new ArchitecturalGeometry();rudderMesh.Box(new Vector3(0,-.10f,.09f),new Vector3(.045f,.39f,.32f),DarkWood);
            rudderMesh.Beam(new Vector3(0,.17f,0),new Vector3(0,.37f,.23f),.045f,.045f,PaleWood);rudderMesh.Create(rudder,"Stern rudder and tiller");motion.Rudder=rudder;
            BuildWake(root,motion,length);motion.HullLength=length;motion.Capture();
            rig.CaptureInitialPose();
        }

        static void Crew(ArchitecturalGeometry g,Vector3 seat,Color team,bool seated)
        {
            // Crew and pennant share a team-cloth batch; a separate near-identical
            // shirt tint would add a ninth material slot on fishing launches.
            var shirt=ArchitecturalMaterials.Get(Surface.Cloth,team);
            var skin=ArchitecturalMaterials.Get(Surface.Cloth,new Color(.65f,.46f,.30f));
            g.Lathe(seat,new[]{new Vector2(.11f,.18f),new Vector2(.15f,.36f),new Vector2(.13f,.52f),new Vector2(.08f,.57f)},shirt,8);
            g.Lathe(seat+Vector3.up*.59f,new[]{new Vector2(.06f,0),new Vector2(.10f,.05f),new Vector2(.10f,.18f),new Vector2(.06f,.22f),new Vector2(0,.23f)},skin,8);
            g.Cylinder(seat+Vector3.up*.82f,.13f,.07f,Canvas,10);
            foreach(float side in new[]{-1f,1f})
            {
                Vector3 shoulder=seat+new Vector3(side*.12f,.50f,.015f),elbow=seat+new Vector3(side*.17f,.30f,.12f),hand=seat+new Vector3(side*.13f,.32f,.29f);
                g.Beam(shoulder,elbow,.10f,.095f,shirt);g.Beam(elbow,hand,.065f,.065f,skin);
                Vector3 hip=seat+new Vector3(side*.075f,.19f,0),knee=seat+new Vector3(side*.075f,.12f,.24f),foot=seat+new Vector3(side*.075f,0,.28f);
                g.Beam(hip,knee,.105f,.095f,DarkWood);g.Beam(knee,foot,.085f,.085f,DarkWood);
                g.Box(foot+new Vector3(0,0,.055f),new Vector3(.095f,.06f,.19f),DarkWood);
            }
        }

        static void BuildWake(Transform root,BoatPresentationMotion motion,float length)
        {
            if(wake==null)
            {
                var shader=Resources.Load<Shader>("BoatWake");if(shader==null)return;
                wake=new Material(shader){name="Frontier / trailing boat foam",enableInstancing=true};
            }
            // Foam stays in the hull-width envelope. Its longer trail has no
            // solid geometry and is excluded from the entity's pick bounds.
            var g=new ArchitecturalGeometry();
            foreach(float side in new[]{-1f,1f})
            {
                Vector3 a=new Vector3(side*.13f,.026f,-length*.72f),b=new Vector3(side*.58f,.026f,-length-2.1f);
                g.Quad(a,b,b+new Vector3(side*.12f,0,0),a+new Vector3(side*.07f,0,0),wake,.5f);
            }
            var t=g.Create(root,"Non-solid trailing boat wake");
            var mesh=t.GetComponent<MeshFilter>().sharedMesh;
            mesh.uv=new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0),new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)};
            mesh.RecalculateTangents();motion.Wake=t.GetComponent<MeshRenderer>();motion.Wake.shadowCastingMode=ShadowCastingMode.Off;motion.Wake.enabled=false;
        }

        static void Hull(ArchitecturalGeometry g,float width,float length,Color team,string role=null)
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
                g.Quad(P(i+1,lower,lo),P(i+1,upper,hi),P(i,upper,hi),P(i,lower,lo),HullStrake085(role,ring,paint));
            }
            for(int i=0;i<segments;i++)
            {
                Vector3 a=P(i,.41f,1),b=P(i+1,.41f,1);g.Beam(a,b,.065f,.075f,PaleWood);
                // Visible inner gunwale makes the hull a thick shell rather than
                // paper-thin sides. The deck remains below the painted rim.
                g.Quad(P(i,.40f,1),P(i+1,.40f,1),P(i+1,.40f,.88f),P(i,.40f,.88f),Wood);
                g.Triangle(new Vector3(0,.31f,0),P(i,.31f,.90f),P(i+1,.31f,.90f),PaleWood);
            }
            for(int row=-5;row<=5;row++)
            {
                float z=row*length/6;float span=Mathf.Sqrt(Mathf.Max(0,1-z*z/(length*length)))*width*.84f;
                g.Box(new Vector3(0,.33f,z),new Vector3(span*2,.032f,.024f),DarkWood);
            }
            g.Box(new Vector3(0,.50f,-length*.68f),new Vector3(width*1.40f,.08f,.19f),Wood);
            g.Beam(new Vector3(0,.30f,-length*.86f),new Vector3(0,.63f,-length*.58f),.035f,.035f,Iron);
            g.Beam(new Vector3(0,-.14f,-length*.81f),new Vector3(0,-.14f,length*.84f),.09f,.08f,DarkWood);
            g.Beam(new Vector3(0,-.10f,length*.86f),new Vector3(0,.48f,length*.97f),.08f,.08f,PaleWood);
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
