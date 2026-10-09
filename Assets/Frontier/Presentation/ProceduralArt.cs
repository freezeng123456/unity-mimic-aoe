using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier
{
    // Original development models, assembled from authored silhouettes; no third-party game assets.
    public static class ProceduralArt
    {
        private enum RoofForm { Gable,Hip,Flat,Shed }
        private static readonly Dictionary<Color,Material> Materials=new Dictionary<Color,Material>();
        public static readonly Color Blue=new Color(.13f,.42f,.64f), Red=new Color(.68f,.22f,.15f);
        public static Color OwnerColor(int owner)=>owner==0?Blue:owner==1?Red:owner==2?new Color(.16f,.61f,.41f):owner==3?new Color(.61f,.35f,.72f):new Color(.4f,.43f,.24f);
        public static Material Mat(Color color)
        {
            if(Materials.TryGetValue(color,out var m)&&m!=null)return m;
            m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=color;m.SetFloat("_Smoothness",.08f);Materials[color]=m;return m;
        }
        public static GameObject Part(Transform parent,string name,PrimitiveType type,Vector3 pos,Vector3 scale,Color color)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=Mat(color);UnityEngine.Object.Destroy(go.GetComponent<Collider>());return go;
        }
        public static GameObject MeshPart(Transform parent,string name,Vector3[] vertices,int[] triangles,Color color)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);var m=new Mesh{name=name};m.vertices=vertices;m.triangles=triangles;m.RecalculateNormals();go.AddComponent<MeshFilter>().sharedMesh=m;go.AddComponent<MeshRenderer>().sharedMaterial=Mat(color);go.AddComponent<ProceduralMeshLifetime>().OwnedMesh=m;return go;
        }
        public static void Roof(Transform t,float w,float d,float y,Color c)
        {
            MeshPart(t,"Gabled roof",new[]{new Vector3(-w,y,-d),new Vector3(w,y,-d),new Vector3(0,y+w*.7f,-d),new Vector3(-w,y,d),new Vector3(w,y,d),new Vector3(0,y+w*.7f,d)},new[]{0,2,1,3,4,5,0,3,5,0,5,2,1,2,5,1,5,4,0,1,4,0,4,3},c);
        }
        // Eight bevelled corners and separate face vertices give a low-poly,
        // flat-lit outline without the high vertex count of Unity capsules.
        private static Transform Section(Transform parent,string name,Vector3 position,Vector2 lower,Vector2 upper,float height,Color color)
        {
            Vector2[] outline={new Vector2(-.72f,-1),new Vector2(.72f,-1),new Vector2(1,-.65f),new Vector2(1,.65f),new Vector2(.72f,1),new Vector2(-.72f,1),new Vector2(-1,.65f),new Vector2(-1,-.65f)};
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<8;i++)
            {
                int j=(i+1)%8,n=vertices.Count;
                Vector3 Point(int k,Vector2 size,float y)=>new Vector3(outline[k].x*size.x*.5f,y,outline[k].y*size.y*.5f);
                vertices.AddRange(new[]{Point(i,lower,-height*.5f),Point(j,lower,-height*.5f),Point(j,upper,height*.5f),Point(i,upper,height*.5f)});
                triangles.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});
                n=vertices.Count;vertices.AddRange(new[]{Vector3.down*height*.5f,Point(i,lower,-height*.5f),Point(j,lower,-height*.5f)});triangles.AddRange(new[]{n,n+1,n+2});
                n=vertices.Count;vertices.AddRange(new[]{Vector3.up*height*.5f,Point(i,upper,height*.5f),Point(j,upper,height*.5f)});triangles.AddRange(new[]{n,n+2,n+1});
            }
            var part=MeshPart(parent,name,vertices.ToArray(),triangles.ToArray(),color).transform;part.localPosition=position;return part;
        }
        private static Transform Beam(Transform parent,string name,Vector3 from,Vector3 to,float width,float depth,Color color)
        {var part=Section(parent,name,(from+to)*.5f,new Vector2(width,depth),new Vector2(width*.85f,depth*.85f),(to-from).magnitude,color);part.localRotation=Quaternion.FromToRotation(Vector3.up,to-from);return part;}
        private static Transform Tube(Transform parent,string name,Vector3 position,float diameter,float length,float thickness,Color color)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6,b=(i+1)*Mathf.PI/6;float outer=diameter*.5f,inner=outer-thickness;
                Vector3 Point(float angle,float r,float y)=>new Vector3(Mathf.Cos(angle)*r,y,Mathf.Sin(angle)*r);
                void Quad(Vector3 p,Vector3 q,Vector3 r,Vector3 s)
                {int n=vertices.Count;vertices.AddRange(new[]{p,q,r,s});triangles.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});}
                Quad(Point(a,outer,-length*.5f),Point(b,outer,-length*.5f),Point(b,outer,length*.5f),Point(a,outer,length*.5f));
                Quad(Point(b,inner,-length*.5f),Point(a,inner,-length*.5f),Point(a,inner,length*.5f),Point(b,inner,length*.5f));
                Quad(Point(a,outer,length*.5f),Point(b,outer,length*.5f),Point(b,inner,length*.5f),Point(a,inner,length*.5f));
                Quad(Point(b,outer,-length*.5f),Point(a,outer,-length*.5f),Point(a,inner,-length*.5f),Point(b,inner,-length*.5f));
            }
            var part=MeshPart(parent,name,vertices.ToArray(),triangles.ToArray(),color).transform;part.localPosition=position;return part;
        }
        public static Transform BuildEntity(EntityState e,Transform parent,string civilization=null,int age=1)
        {
            var root=new GameObject(e.def.name).transform;root.SetParent(parent,false);
            Color team=OwnerColor(e.owner);
            if(WaterPresentation.TryBuildEntity(root,e,team))return root;
            if(e.def.kind==EntityKind.Resource){Resource(root,e);return root;}
            if(e.def.kind==EntityKind.Building){DetailedArchitecture.Build(root,e,team,civilization,age);return root;}
            var rig=root.gameObject.AddComponent<UnitRig>();
            if(e.Healer){MedicalFigure076(root,rig,e,team);DetailedUnits.BakeAuthored(root,rig);rig.CaptureInitialPose();return root;}
            if(e.def.id=="factory_wagon"){FactoryWagon(root,rig,team);DetailedUnits.BakeAuthored(root,rig);rig.CaptureInitialPose();return root;}
            if(e.def.id=="caravan"||e.def.id=="objective_cart"){CargoCaravan(root,rig,team);DetailedUnits.Refine(root,rig,e,team);rig.CaptureInitialPose();return root;}
            bool horse=(e.def.tags&Tags.Cavalry)!=0;
            bool gun=(e.def.tags&Tags.Artillery)!=0;
            Color wood=new Color(.24f,.15f,.085f), iron=new Color(.19f,.22f,.22f);
            if(gun)
            {
                if(e.def.id=="rocket"||e.def.id=="heavy_cannon"||e.def.id=="great_bombard")
                {IndustrialArtillery(root,rig,e.def.id,team);DetailedUnits.BakeAuthored(root,rig);rig.CaptureInitialPose();return root;}
                Artillery(root,rig,e.def.id=="mortar",team,e.def.id=="culverin");
                DetailedUnits.Refine(root,rig,e,team);rig.CaptureInitialPose();return root;
            }
            if(e.def.id=="musketeer")
            {AuthoredInfantry.Attach(root,rig,team);Equip(e,rig,false);DetailedUnits.Refine(root,rig,e,team);rig.CaptureInitialPose();return root;}
            if(e.def.id=="envoy"||e.def.id=="native_scout")
            {ScoutingFigure(root,rig,e,team);Equip(e,rig,false);DetailedUnits.Refine(root,rig,e,team);rig.CaptureInitialPose();return root;}
            rig.RiderSeatHeight=.95f;float riderY=horse?rig.RiderSeatHeight:0;
            if(horse)
            {
                Horse(root,rig,team);
            }
            Color coat=e.Worker?new Color(.69f,.57f,.36f):team;
            if(e.def.revolutionary)coat=new Color(.42f,.38f,.30f);
            if(e.def.civilization=="wayfarer")coat=new Color(.21f,.23f,.40f);
            else if(e.def.civilization=="highland")coat=new Color(.37f,.39f,.33f);
            else if(e.def.id=="coureur")coat=new Color(.60f,.52f,.35f);
            else if(e.def.id=="cuirassier")coat=new Color(.20f,.30f,.46f);
            else if(e.def.id=="janissary")coat=new Color(.76f,.71f,.56f);
            else if(e.def.id=="abus")coat=new Color(.46f,.25f,.19f);
            else if(e.def.id=="spahi")coat=new Color(.45f,.22f,.19f);
            if(e.def.id=="ally_river_guard")coat=new Color(.25f,.43f,.38f);
            else if(e.def.id=="ally_highland_guard")coat=new Color(.42f,.35f,.24f);
            else if(e.def.id=="ally_coastal_guard")coat=new Color(.22f,.30f,.42f);
            if((e.def.tags&Tags.Explorer)!=0)coat=new Color(.33f,.39f,.25f);
            Section(root,"Tailored coat",new Vector3(0,1.025f+riderY,0),new Vector2(.40f,.30f),new Vector2(.65f,.42f),.55f,coat);
            Section(root,"Coat skirts",new Vector3(0,.72f+riderY,0),new Vector2(.62f,.40f),new Vector2(.40f,.30f),.32f,coat);
            Section(root,"Waist sash",new Vector3(0,.88f+riderY,0),new Vector2(.61f,.42f),new Vector2(.59f,.42f),.16f,team);
            Section(root,"Head",new Vector3(0,1.52f+riderY,.035f),new Vector2(.29f,.31f),new Vector2(.33f,.33f),.34f,new Color(.77f,.59f,.4f));
            Section(root,"Hair at nape",new Vector3(0,1.58f+riderY,-.13f),new Vector2(.29f,.10f),new Vector2(.31f,.10f),.20f,wood);
            Headwear(root,e,riderY,team);
            Part(root,"Coat front facing",PrimitiveType.Cube,new Vector3(0,1.10f+riderY,.218f),new Vector3(.13f,.38f,.035f),new Color(.83f,.76f,.57f));
            rig.LeftShoulder=Pivot(root,"Left shoulder",new Vector3(-.3f,1.25f+riderY,.05f));rig.RightShoulder=Pivot(root,"Right shoulder",new Vector3(.3f,1.25f+riderY,.05f));
            rig.LeftElbow=Pivot(rig.LeftShoulder,"Left elbow",new Vector3(0,-.36f,0));rig.RightElbow=Pivot(rig.RightShoulder,"Right elbow",new Vector3(0,-.36f,0));
            rig.LeftHand=Pivot(rig.LeftElbow,"Left hand",new Vector3(0,-.34f,0));rig.RightHand=Pivot(rig.RightElbow,"Right hand",new Vector3(0,-.34f,0));
            foreach(float x in new[]{-.17f,.17f})
            {
                var leg=Pivot(root,x<0?"Left leg":"Right leg",new Vector3(horse?Mathf.Sign(x)*.40f:x,.65f+riderY,horse?-.08f:0));if(x<0)rig.LeftLeg=leg;else rig.RightLeg=leg;
                Section(leg,"Breeches",new Vector3(0,-.17f,0),new Vector2(.20f,.22f),new Vector2(.24f,.24f),.34f,new Color(.64f,.61f,.48f));
                var knee=Pivot(leg,"Knee",new Vector3(0,-.34f,0));if(x<0)rig.LeftKnee=knee;else rig.RightKnee=knee;
                Section(knee,"Boot shaft",new Vector3(0,-.14f,0),new Vector2(.19f,.21f),new Vector2(.22f,.22f),.30f,wood);
                Section(knee,"Boot toe",new Vector3(0,-.27f,.07f),new Vector2(.24f,.35f),new Vector2(.21f,.32f),.14f,wood);
                if(horse){leg.localRotation=Quaternion.Euler(-65,0,x<0?-8:8);knee.localRotation=Quaternion.Euler(65,0,0);}
                var shoulder=x<0?rig.LeftShoulder:rig.RightShoulder;
                Section(shoulder,"Coat sleeve",new Vector3(0,-.18f,0),new Vector2(.16f,.18f),new Vector2(.22f,.21f),.36f,coat);
                Section(x<0?rig.LeftElbow:rig.RightElbow,"Forearm",new Vector3(0,-.17f,0),new Vector2(.14f,.16f),new Vector2(.17f,.18f),.34f,coat);
                Section(x<0?rig.LeftHand:rig.RightHand,"Hand",Vector3.zero,new Vector2(.17f,.18f),new Vector2(.16f,.17f),.18f,new Color(.77f,.59f,.4f));
            }
            Equip(e,rig,horse);
            if(e.def.revolutionary)rig.SupplyCollectorWeapon=rig.Weapon;
            ExpansionEquipment(root,e,rig,team,riderY);
            ClassicEquipment(root,e,rig,team,riderY);
            DetailedUnits.Refine(root,rig,e,team);
            rig.CaptureInitialPose();
            return root;
        }
        private static void MedicalFigure076(Transform root,UnitRig rig,EntityState e,Color team)
        {
            // Original support silhouettes: long unarmed robes, an Ottoman
            // wrapped turban or a European square cap, and a joint-bound book.
            bool imam=e.def.id=="imam";rig.Unarmed=true;
            Color linen=new Color(.87f,.83f,.69f),cloth=imam?new Color(.72f,.72f,.58f):new Color(.16f,.18f,.20f);
            Color skin=new Color(.77f,.59f,.40f),leather=new Color(.21f,.12f,.065f);
            rig.HealerBody=Pivot(root,"Medical torso pivot",Vector3.zero);var body=rig.HealerBody;
            Section(body,imam?"Imam long kaftan":"Priest full cassock",new Vector3(0,.79f,0),new Vector2(.74f,.51f),new Vector2(.52f,.37f),1.05f,cloth);
            Section(body,"Medical fitted shoulders",new Vector3(0,1.24f,0),new Vector2(.53f,.38f),new Vector2(.57f,.40f),.23f,cloth);
            Section(body,"Head",new Vector3(0,1.55f,.035f),new Vector2(.29f,.31f),new Vector2(.33f,.33f),.34f,skin);
            Section(body,"Medical collar",new Vector3(0,1.35f,.04f),new Vector2(.31f,.29f),new Vector2(.32f,.29f),.09f,linen);
            foreach(float side in new[]{-1f,1f})
            {
                Beam(body,"Medical robe front trim",new Vector3(side*.12f,.32f,.25f),new Vector3(side*.085f,1.30f,.205f),.045f,.022f,imam?team:linen);
                Section(body,"Robe folded hem",new Vector3(side*.23f,.31f,.03f),new Vector2(.13f,.47f),new Vector2(.11f,.44f),.10f,cloth*.8f);
                Part(body,"Medical visible eye",PrimitiveType.Sphere,new Vector3(side*.072f,1.57f,.199f),new Vector3(.025f,.022f,.018f),leather);
            }
            Part(body,"Medical nose",PrimitiveType.Sphere,new Vector3(0,1.51f,.218f),new Vector3(.065f,.10f,.07f),skin);
            if(imam)
            {
                Section(body,"Imam felt cap",new Vector3(0,1.84f,.015f),new Vector2(.29f,.29f),new Vector2(.25f,.25f),.24f,team);
                for(int band=0;band<3;band++)
                    Section(body,"Imam layered turban wrap",new Vector3(0,1.72f+band*.045f,.015f),new Vector2(.48f-band*.035f,.43f-band*.025f),new Vector2(.47f-band*.035f,.42f-band*.025f),.072f,linen*(1-band*.025f));
                Beam(body,"Imam rear cloth tail",new Vector3(-.13f,1.76f,-.19f),new Vector3(-.15f,1.39f,-.23f),.10f,.027f,linen);
                Section(body,"Imam short beard",new Vector3(0,1.40f,.16f),new Vector2(.10f,.08f),new Vector2(.23f,.10f),.18f,leather);
                Section(body,"Imam woven waist sash",new Vector3(0,.87f,0),new Vector2(.64f,.435f),new Vector2(.62f,.425f),.12f,team);
            }
            else
            {
                Section(body,"Priest square biretta",new Vector3(0,1.77f,.015f),new Vector2(.41f,.39f),new Vector2(.37f,.35f),.17f,cloth*.65f);
                foreach(float side in new[]{-1f,1f})
                    Beam(body,"Priest cap raised ridge",new Vector3(side*.15f,1.86f,-.16f),new Vector3(side*.15f,1.86f,.16f),.055f,.055f,cloth*.7f);
                Part(body,"Priest cap tuft",PrimitiveType.Sphere,new Vector3(0,1.89f,.015f),new Vector3(.09f,.08f,.09f),cloth*.6f);
                foreach(float side in new[]{-1f,1f})Beam(body,"Priest team stole",new Vector3(side*.17f,1.31f,.22f),new Vector3(side*.17f,.49f,.26f),.080f,.026f,team);
                for(int button=0;button<6;button++)Part(body,"Cassock front button",PrimitiveType.Sphere,new Vector3(0,.59f+button*.11f,.257f),new Vector3(.028f,.028f,.025f),linen);
            }
            rig.LeftShoulder=Pivot(body,"Left shoulder",new Vector3(-.28f,1.25f,.05f));rig.RightShoulder=Pivot(body,"Right shoulder",new Vector3(.28f,1.25f,.05f));
            rig.LeftElbow=Pivot(rig.LeftShoulder,"Left elbow",new Vector3(0,-.36f,0));rig.RightElbow=Pivot(rig.RightShoulder,"Right elbow",new Vector3(0,-.36f,0));
            rig.LeftHand=Pivot(rig.LeftElbow,"Left hand",new Vector3(0,-.34f,0));rig.RightHand=Pivot(rig.RightElbow,"Right hand",new Vector3(0,-.34f,0));
            foreach(float side in new[]{-1f,1f})
            {
                var shoulder=side<0?rig.LeftShoulder:rig.RightShoulder;var elbow=side<0?rig.LeftElbow:rig.RightElbow;var hand=side<0?rig.LeftHand:rig.RightHand;
                Section(shoulder,"Medical loose sleeve",new Vector3(0,-.18f,0),new Vector2(.22f,.23f),new Vector2(.24f,.23f),.36f,cloth);
                Section(elbow,"Medical lower sleeve",new Vector3(0,-.15f,0),new Vector2(.17f,.19f),new Vector2(.21f,.22f),.30f,cloth);
                Section(elbow,"Medical linen cuff",new Vector3(0,-.29f,0),new Vector2(.18f,.20f),new Vector2(.18f,.20f),.07f,linen);
                Part(hand,"Medical open hand",PrimitiveType.Sphere,Vector3.zero,new Vector3(.13f,.15f,.13f),skin);
                var leg=Pivot(root,side<0?"Left leg":"Right leg",new Vector3(side*.17f,.63f,0));
                var knee=Pivot(leg,"Knee",new Vector3(0,-.32f,0));
                Section(knee,"Medical shoe",new Vector3(0,-.22f,.07f),new Vector2(.20f,.35f),new Vector2(.20f,.30f),.17f,leather);
                if(side<0){rig.LeftLeg=leg;rig.LeftKnee=knee;}else{rig.RightLeg=leg;rig.RightKnee=knee;}
            }
            rig.HealerBook=Pivot(rig.LeftHand,"Medical book pivot",Vector3.zero);
            Part(rig.HealerBook,"Medical book leather binding",PrimitiveType.Cube,Vector3.zero,new Vector3(.28f,.045f,.35f),leather);
            Part(rig.HealerBook,"Medical book visible pages",PrimitiveType.Cube,new Vector3(0,.033f,0),new Vector3(.25f,.027f,.32f),linen);
            Beam(rig.HealerBook,"Medical book centre seam",new Vector3(0,.052f,-.155f),new Vector3(0,.052f,.155f),.013f,.013f,leather*.8f);
        }
        private static void ScoutingFigure(Transform root,UnitRig rig,EntityState e,Color team)
        {
            // Original role silhouettes use the ordinary limb lengths, so hand IK
            // and animation pooling keep the same contract as the other figures.
            bool envoy=e.def.id=="envoy";rig.Unarmed=envoy;
            Color skin=new Color(.77f,.59f,.40f),leather=new Color(.20f,.13f,.075f),linen=new Color(.87f,.83f,.67f);
            Color cloth=envoy?new Color(.19f,.30f,.33f):new Color(.44f,.48f,.32f);
            rig.ScoutBody=Pivot(root,"Scouting torso pivot",Vector3.zero);var body=rig.ScoutBody;
            Section(body,envoy?"Envoy narrow long coat":"Scout short tunic",new Vector3(0,1.10f,0),new Vector2(.40f,.28f),new Vector2(.54f,.37f),.58f,cloth);
            Section(body,"Head",new Vector3(0,1.55f,.035f),new Vector2(.29f,.31f),new Vector2(.33f,.33f),.34f,skin);
            Section(body,"Hair at nape",new Vector3(0,1.60f,-.13f),new Vector2(.27f,.10f),new Vector2(.30f,.10f),.17f,leather);
            if(envoy)
            {
                Color felt=new Color(.09f,.13f,.14f);
                Section(body,"Envoy narrow hat brim",new Vector3(0,1.765f,0),new Vector2(.62f,.45f),new Vector2(.62f,.45f),.045f,felt);
                Section(body,"Envoy tall hat crown",new Vector3(0,1.945f,0),new Vector2(.32f,.30f),new Vector2(.28f,.26f),.32f,felt);
                Section(body,"Envoy hat ribbon",new Vector3(0,1.815f,0),new Vector2(.335f,.315f),new Vector2(.325f,.305f),.075f,team);
                foreach(float side in new[]{-1f,1f})
                    Section(body,side<0?"Left split coat tail":"Right split coat tail",new Vector3(side*.12f,.64f,-.14f),new Vector2(.20f,.11f),new Vector2(.22f,.20f),.55f,cloth);
                Section(body,"Envoy pale document satchel",new Vector3(.33f,.83f,.01f),new Vector2(.25f,.13f),new Vector2(.28f,.14f),.33f,linen);
                Section(body,"Document satchel flap",new Vector3(.33f,.95f,.09f),new Vector2(.285f,.035f),new Vector2(.27f,.035f),.12f,linen*.83f);
                Beam(body,"Document satchel shoulder strap",new Vector3(-.23f,1.37f,.195f),new Vector3(.33f,.97f,.16f),.045f,.022f,leather);
                Beam(body,"Document satchel rear strap",new Vector3(-.23f,1.37f,-.19f),new Vector3(.33f,.97f,-.075f),.045f,.022f,leather);
                Section(body,"Visible paper folio",new Vector3(.34f,1.055f,.01f),new Vector2(.18f,.065f),new Vector2(.18f,.065f),.16f,linen*1.08f);
            }
            else
            {
                Section(body,"Scout low cloth headwrap",new Vector3(0,1.697f,.015f),new Vector2(.365f,.355f),new Vector2(.35f,.335f),.13f,linen);
                Section(body,"Scout headwrap team seam",new Vector3(0,1.70f,.015f),new Vector2(.37f,.36f),new Vector2(.37f,.36f),.032f,team);
                Beam(body,"Scout headwrap cloth tie",new Vector3(-.12f,1.68f,-.16f),new Vector3(-.18f,1.48f,-.20f),.065f,.022f,linen);
                // The left mantle hangs lower than the right; there is no blanket pack.
                MeshPart(body,"Scout asymmetric short mantle",new[]{new Vector3(-.36f,1.35f,-.20f),new Vector3(.30f,1.33f,-.20f),new Vector3(.27f,1.04f,-.24f),new Vector3(-.31f,.91f,-.24f),new Vector3(-.36f,1.35f,-.235f),new Vector3(.30f,1.33f,-.235f),new Vector3(.27f,1.04f,-.275f),new Vector3(-.31f,.91f,-.275f)},new[]{0,1,2,0,2,3,4,6,5,4,7,6,0,4,5,0,5,1,1,5,6,1,6,2,2,6,7,2,7,3,3,7,4,3,4,0},cloth*.76f);
                Beam(body,"Scout diagonal quiver strap",new Vector3(.25f,1.36f,.20f),new Vector3(-.24f,.91f,.20f),.065f,.028f,leather);
                var quiver=Pivot(body,"Scout rear quiver",new Vector3(.20f,1.16f,-.34f));quiver.localRotation=Quaternion.Euler(0,0,-12);
                Section(quiver,"Scout quiver leather case",Vector3.zero,new Vector2(.17f,.17f),new Vector2(.21f,.20f),.56f,leather);
                Section(quiver,"Scout quiver team binding",new Vector3(0,.15f,0),new Vector2(.215f,.205f),new Vector2(.215f,.205f),.065f,team);
                for(int i=0;i<3;i++)
                {
                    float x=-.055f+i*.055f;
                    Beam(quiver,"Scout carried arrow",new Vector3(x,.20f,0),new Vector3(x,.48f,0),.014f,.014f,new Color(.54f,.37f,.18f));
                    Section(quiver,"Scout arrow fletching",new Vector3(x,.435f,0),new Vector2(.05f,.025f),new Vector2(.035f,.025f),.095f,linen);
                }
            }
            rig.LeftShoulder=Pivot(body,"Left shoulder",new Vector3(-.28f,1.25f,.05f));rig.RightShoulder=Pivot(body,"Right shoulder",new Vector3(.28f,1.25f,.05f));
            rig.LeftElbow=Pivot(rig.LeftShoulder,"Left elbow",new Vector3(0,-.36f,0));rig.RightElbow=Pivot(rig.RightShoulder,"Right elbow",new Vector3(0,-.36f,0));
            rig.LeftHand=Pivot(rig.LeftElbow,"Left hand",new Vector3(0,-.34f,0));rig.RightHand=Pivot(rig.RightElbow,"Right hand",new Vector3(0,-.34f,0));
            foreach(float side in new[]{-1f,1f})
            {
                var shoulder=side<0?rig.LeftShoulder:rig.RightShoulder;var elbow=side<0?rig.LeftElbow:rig.RightElbow;var hand=side<0?rig.LeftHand:rig.RightHand;
                Section(shoulder,"Coat sleeve",new Vector3(0,-.18f,0),new Vector2(.16f,.18f),new Vector2(.21f,.20f),.36f,cloth);
                Section(elbow,"Forearm",new Vector3(0,-.17f,0),new Vector2(.14f,.16f),new Vector2(.17f,.18f),.34f,cloth);
                Section(hand,"Empty hand",Vector3.zero,new Vector2(.17f,.18f),new Vector2(.16f,.17f),.18f,skin);
                var leg=Pivot(root,side<0?"Left leg":"Right leg",new Vector3(side*.17f,.65f,0));if(side<0)rig.LeftLeg=leg;else rig.RightLeg=leg;
                Section(leg,"Breeches",new Vector3(0,-.17f,0),new Vector2(.20f,.22f),new Vector2(.24f,.24f),.34f,envoy?new Color(.57f,.60f,.56f):new Color(.48f,.42f,.28f));
                var knee=Pivot(leg,"Knee",new Vector3(0,-.34f,0));if(side<0)rig.LeftKnee=knee;else rig.RightKnee=knee;
                Section(knee,"Boot shaft",new Vector3(0,-.14f,0),new Vector2(.19f,.21f),new Vector2(.22f,.22f),.30f,leather);
                Section(knee,"Boot toe",new Vector3(0,-.27f,.07f),new Vector2(.24f,.35f),new Vector2(.21f,.32f),.14f,leather);
            }
        }
        private static void Headwear(Transform root,EntityState e,float riderY,Color team)
        {
            if(ClassicHeadwear(root,e,riderY,team))return;
            bool broad=e.Worker||e.def.revolutionary||e.def.id=="explorer"||e.def.id=="skirmisher"||e.def.id=="longbow";
            Color felt=e.Worker?new Color(.65f,.49f,.25f):new Color(.12f,.16f,.17f);
            Section(root,"Hat brim",new Vector3(0,1.73f+riderY,0),new Vector2(broad?.70f:.48f,broad?.58f:.44f),new Vector2(broad?.70f:.48f,broad?.58f:.44f),.055f,felt);
            float height=e.def.id=="hussar"||e.def.id=="carabineer"?.24f:e.Worker?.12f:.15f;
            Section(root,"Hat crown",new Vector3(0,1.76f+height*.5f+riderY,0),new Vector2(.37f,.36f),new Vector2(.29f,.29f),height,felt);
            Section(root,"Hat team ribbon",new Vector3(0,1.80f+riderY,0),new Vector2(.38f,.37f),new Vector2(.36f,.35f),.07f,team);
            if(e.def.id=="hussar")Section(root,"Cap plume",new Vector3(.12f,2.06f+riderY,0),new Vector2(.09f,.08f),new Vector2(.13f,.10f),.20f,new Color(.86f,.78f,.56f));
        }
        private static void Horse(Transform root,UnitRig rig,Color team)
        {
            Color hide=new Color(.40f,.26f,.16f),dark=new Color(.13f,.085f,.055f),hoof=new Color(.15f,.16f,.15f);
            Section(root,"Horse torso",new Vector3(0,.99f,0),new Vector2(.62f,1.18f),new Vector2(.78f,1.25f),.51f,hide);
            Beam(root,"Horse neck",new Vector3(0,1.03f,.42f),new Vector3(0,1.62f,.77f),.36f,.43f,hide);
            Beam(root,"Dark mane",new Vector3(0,1.16f,.32f),new Vector3(0,1.74f,.69f),.13f,.14f,dark);
            rig.HorseHead=Pivot(root,"Horse head pivot",new Vector3(0,1.62f,.79f));
            Section(rig.HorseHead,"Horse skull",Vector3.zero,new Vector2(.31f,.37f),new Vector2(.27f,.32f),.34f,hide);
            Section(rig.HorseHead,"Horse muzzle",new Vector3(0,-.11f,.25f),new Vector2(.27f,.40f),new Vector2(.30f,.35f),.25f,new Color(.31f,.20f,.13f));
            foreach(float x in new[]{-.11f,.11f})Section(rig.HorseHead,"Pointed ear",new Vector3(x,.26f,-.07f),new Vector2(.11f,.13f),new Vector2(.025f,.04f),.26f,hide);
            Part(rig.HorseHead,"Bridle noseband",PrimitiveType.Cube,new Vector3(0,-.08f,.39f),new Vector3(.30f,.085f,.04f),dark);
            var horseLegs=new List<Transform>();
            foreach(float x in new[]{-.28f,.28f})foreach(float z in new[]{-.43f,.43f})
            {
                var leg=Pivot(root,"Horse leg pivot",new Vector3(x,.85f,z));
                Section(leg,"Horse upper leg",new Vector3(0,-.19f,0),new Vector2(.12f,.14f),new Vector2(.20f,.21f),.39f,hide);
                Section(leg,"Horse lower leg",new Vector3(0,-.56f,0),new Vector2(.12f,.14f),new Vector2(.13f,.15f),.37f,hide);
                Section(leg,"Hoof",new Vector3(0,-.78f,.03f),new Vector2(.20f,.24f),new Vector2(.15f,.18f),.15f,hoof);horseLegs.Add(leg);
            }
            rig.RearLeftHorseLeg=horseLegs[0];rig.LeftHorseLeg=horseLegs[1];rig.RearRightHorseLeg=horseLegs[2];rig.RightHorseLeg=horseLegs[3];
            rig.HorseTail=Pivot(root,"Horse tail pivot",new Vector3(0,1.08f,-.59f));
            Beam(rig.HorseTail,"Horse tail",Vector3.zero,new Vector3(0,-.42f,-.32f),.14f,.18f,dark);
            Section(root,"Team saddle blanket",new Vector3(0,1.29f,-.02f),new Vector2(.99f,.73f),new Vector2(.93f,.67f),.16f,team);
            Section(root,"Saddle leather",new Vector3(0,1.42f,-.07f),new Vector2(.52f,.56f),new Vector2(.48f,.48f),.13f,dark);
            foreach(float x in new[]{-.15f,.15f})Beam(root,"Reins",new Vector3(-.20f,rig.RiderSeatHeight+1.04f,.45f),new Vector3(x,1.54f,.91f),.035f,.035f,dark);
        }
        private static void Artillery(Transform root,UnitRig rig,bool mortar,Color team,bool culverin=false)
        {
            Color wood=new Color(.29f,.18f,.10f),iron=new Color(.19f,.22f,.22f),rim=new Color(.40f,.43f,.40f);
            rig.Carriage=Part(root,mortar?"Mortar bed":"Cannon carriage",PrimitiveType.Cube,new Vector3(0,mortar?.30f:.55f,-.20f),new Vector3(mortar?1.05f:1.15f,.28f,mortar?1.15f:1.8f),wood).transform;
            if(!mortar)foreach(float x in new[]{-.34f,.34f})Beam(root,"Carriage cheek",new Vector3(x,.57f,-.68f),new Vector3(x,.96f,.32f),.20f,.27f,wood);
            else Section(root,"Mortar turntable",new Vector3(0,.57f,.10f),new Vector2(.87f,.86f),new Vector2(.70f,.72f),.23f,iron);
            rig.Barrel=Tube(root,"Barrel",new Vector3(0,mortar?.94f:1.02f,mortar?.24f:culverin?.65f:.35f),mortar?.70f:culverin?.29f:.40f,mortar?.82f:culverin?3.3f:2.05f,mortar?.09f:culverin?.055f:.07f,iron);
            rig.Barrel.localRotation=Quaternion.Euler(mortar?35:78,0,0);rig.ArtilleryTravelTilt=mortar?18:12;rig.ArtilleryRecoil=mortar?.14f:.30f;
            Tube(rig.Barrel,"Muzzle ring",new Vector3(0,mortar?.37f:culverin?1.57f:.97f,0),mortar?.78f:culverin?.37f:.49f,.14f,culverin?.055f:.08f,rim);
            Section(rig.Barrel,"Breech",new Vector3(0,mortar?-.40f:culverin?-1.57f:-.97f,0),new Vector2(mortar?.60f:culverin?.37f:.48f,mortar?.60f:culverin?.37f:.48f),new Vector2(mortar?.60f:culverin?.37f:.48f,mortar?.60f:culverin?.37f:.48f),.13f,iron);
            if(culverin){Part(root,"Range sight",PrimitiveType.Cube,new Vector3(.27f,1.3f,-.73f),new Vector3(.06f,.38f,.06f),rim);Beam(root,"Extended split trail",new Vector3(-.32f,.53f,-.55f),new Vector3(-.52f,.25f,-1.8f),.19f,.23f,wood);Beam(root,"Extended split trail",new Vector3(.32f,.53f,-.55f),new Vector3(.52f,.25f,-1.8f),.19f,.23f,wood);}
            var wheels=new List<Transform>();float diameter=mortar?.65f:1.12f;
            foreach(float x in new[]{mortar?-.65f:-.72f,mortar?.65f:.72f})
            {
                var wheel=Pivot(root,"Wheel",new Vector3(x,diameter*.5f,mortar?-.33f:-.08f));wheel.localRotation=Quaternion.Euler(0,0,90);
                Tube(wheel,"Iron wheel rim",Vector3.zero,diameter,.16f,.07f,rim);
                Section(wheel,"Wheel hub",Vector3.zero,new Vector2(.25f,.25f),new Vector2(.25f,.25f),.21f,iron);
                for(int i=0;i<4;i++)Part(wheel,"Wooden spoke",PrimitiveType.Cube,Vector3.zero,new Vector3(diameter*.85f,.12f,.075f),wood).transform.localRotation=Quaternion.Euler(0,i*45,0);
                wheels.Add(wheel);
            }
            rig.LeftWheel=wheels[0];rig.RightWheel=wheels[1];
            Part(root,"Team carriage panel",PrimitiveType.Cube,new Vector3(0,mortar?.46f:.75f,-.67f),new Vector3(.92f,.22f,.14f),team);
        }
        private static void FactoryWagon(Transform root,UnitRig rig,Color team)
        {
            Color wood=new Color(.34f,.23f,.15f),iron=new Color(.20f,.23f,.24f),canvas=new Color(.77f,.72f,.59f);
            rig.Carriage=Part(root,"Factory wagon flatbed",PrimitiveType.Cube,new Vector3(0,.67f,0),new Vector3(1.30f,.19f,1.80f),wood).transform;
            var wheels=new List<Transform>();
            foreach(float x in new[]{-.73f,.73f})foreach(float z in new[]{-.59f,.59f})
            {
                var wheel=Pivot(root,"Factory wagon wheel",new Vector3(x,.40f,z));wheel.localRotation=Quaternion.Euler(0,0,90);
                Tube(wheel,"Wagon wheel rim",Vector3.zero,.77f,.16f,.065f,iron);
                for(int i=0;i<4;i++)Part(wheel,"Wagon spoke",PrimitiveType.Cube,Vector3.zero,new Vector3(.65f,.11f,.05f),wood).transform.localRotation=Quaternion.Euler(0,i*45,0);
                wheels.Add(wheel);
            }
            rig.CartWheels=wheels.ToArray();
            Part(root,"Crated factory machinery",PrimitiveType.Cube,new Vector3(0,1.05f,-.18f),new Vector3(1.1f,.62f,1.08f),canvas);
            foreach(float z in new[]{-.53f,.19f})Part(root,"Machinery shipping strap",PrimitiveType.Cube,new Vector3(0,1.065f,z),new Vector3(1.13f,.65f,.055f),iron);
            Tube(root,"Exposed flywheel cargo",new Vector3(0,1.54f,-.12f),.64f,.14f,.08f,iron).localRotation=Quaternion.Euler(90,0,0);
            Part(root,"Industrial team plate",PrimitiveType.Cube,new Vector3(0,1.10f,-.75f),new Vector3(.63f,.27f,.06f),team);
            foreach(float x in new[]{-.42f,.42f})Beam(root,"Wagon drawbar",new Vector3(x,.66f,.48f),new Vector3(x,.44f,1.28f),.075f,.09f,wood);
        }
        private static void IndustrialArtillery(Transform root,UnitRig rig,string id,Color team)
        {
            bool rocket=id=="rocket",bombard=id=="great_bombard";
            Color wood=new Color(.30f,.20f,.13f),metal=bombard?new Color(.61f,.44f,.23f):new Color(.24f,.27f,.27f);
            float width=bombard?1.46f:1.18f,length=bombard?2.42f:rocket?1.70f:2.10f;
            rig.Carriage=Part(root,rocket?"Rocket launch carriage":bombard?"Bombard reinforced bed":"Heavy cannon carriage",PrimitiveType.Cube,new Vector3(0,.53f,-.24f),new Vector3(width,.25f,length),wood).transform;
            var wheels=new List<Transform>();float diameter=bombard?1.17f:rocket?.98f:1.30f;
            foreach(float x in new[]{-width*.63f,width*.63f})foreach(float z in bombard?new[]{-.70f,.49f}:new[]{-.14f})
            {
                var wheel=Pivot(root,"Industrial artillery wheel",new Vector3(x,diameter*.5f,z));wheel.localRotation=Quaternion.Euler(0,0,90);
                Tube(wheel,"Bound iron wheel rim",Vector3.zero,diameter,.19f,.075f,metal);
                for(int i=0;i<6;i++)Part(wheel,"Industrial wheel spoke",PrimitiveType.Cube,Vector3.zero,new Vector3(diameter*.85f,.11f,.06f),wood).transform.localRotation=Quaternion.Euler(0,i*30,0);
                wheels.Add(wheel);
            }
            rig.CartWheels=wheels.ToArray();rig.ArtilleryTravelTilt=rocket?15:8;rig.ArtilleryRecoil=rocket?.11f:bombard?.43f:.34f;
            if(rocket)
            {
                rig.Barrel=Pivot(root,"Twin rocket rails",new Vector3(0,1.04f,.17f));rig.Barrel.localRotation=Quaternion.Euler(65,0,0);
                foreach(float x in new[]{-.33f,.33f})
                {
                    Part(rig.Barrel,"Steel launch guide",PrimitiveType.Cube,new Vector3(x,0,0),new Vector3(.10f,2.0f,.09f),metal);
                    Tube(rig.Barrel,"Rocket casing",new Vector3(x,.55f,.14f),.17f,.67f,.035f,new Color(.59f,.37f,.20f));
                    Section(rig.Barrel,"Rocket nose",new Vector3(x,.95f,.14f),new Vector2(.17f,.17f),new Vector2(.025f,.025f),.20f,metal);
                    Part(rig.Barrel,"Rocket stabilizer",PrimitiveType.Cube,new Vector3(x,.17f,.14f),new Vector3(.30f,.18f,.045f),metal);
                }
            }
            else
            {
                float tubeLength=bombard?3.20f:2.72f,diameterTube=bombard?.77f:.52f;
                rig.Barrel=Tube(root,bombard?"Great bronze bombard barrel":"Heavy iron cannon barrel",new Vector3(0,bombard?1.14f:1.17f,.45f),diameterTube,tubeLength,.10f,metal);
                rig.Barrel.localRotation=Quaternion.Euler(79,0,0);
                Tube(rig.Barrel,"Reinforced muzzle ring",new Vector3(0,tubeLength*.47f,0),diameterTube+.15f,.20f,.11f,metal);
                Section(rig.Barrel,"Closed artillery breech",new Vector3(0,-tubeLength*.50f,0),new Vector2(diameterTube,diameterTube),new Vector2(diameterTube,diameterTube),.14f,metal);
                foreach(float x in new[]{-width*.32f,width*.32f})Beam(root,"Reinforced carriage cheek",new Vector3(x,.60f,-.88f),new Vector3(x,1.12f,.27f),.17f,.23f,wood);
            }
            Part(root,"Industrial artillery team panel",PrimitiveType.Cube,new Vector3(0,.75f,-length*.54f),new Vector3(width*.73f,.27f,.07f),team);
        }
        private static void Equip(EntityState e,UnitRig rig,bool horse)
        {
            if(e.def.id=="envoy")
            {
                // Only actual construction unpacks a hammer; weak melee uses
                // the empty fist, with no worker gathering equipment or gun.
                rig.WeaponPivot=Pivot(rig.RightHand,"Construction tool grip",Vector3.zero);
                rig.WorkerEquipment=rig.gameObject.AddComponent<WorkerEquipment>();rig.WorkerEquipment.InitializeConstructionOnly(rig);return;
            }
            Color wood=new Color(.24f,.15f,.085f),iron=new Color(.19f,.22f,.22f);
            bool bow=e.def.id=="longbow"||e.def.id=="native_scout";
            rig.WeaponPivot=Pivot(bow?rig.LeftHand:rig.RightHand,"Weapon pivot",Vector3.zero);
            if(e.def.id=="pike"||e.def.id=="escort"||e.def.id=="shield_guard"||e.def.id=="halberdier"||e.def.id=="ally_highland_guard")
            {
                rig.Weapon=Part(rig.WeaponPivot,"Pike shaft",PrimitiveType.Cylinder,new Vector3(0,.9f,0),new Vector3(.055f,1.5f,.055f),wood).transform;
                MeshPart(rig.WeaponPivot,"Pike steel head",new[]{new Vector3(-.08f,2.35f,0),new Vector3(.08f,2.35f,0),new Vector3(0,2.8f,0),new Vector3(0,2.4f,.07f)},new[]{0,2,1,0,3,2,1,2,3,0,1,3},iron);
                if(e.def.id=="halberdier")
                {
                    var edge=new[]{new Vector2(.025f,2.04f),new Vector2(.34f,1.97f),new Vector2(.49f,2.35f),new Vector2(.08f,2.47f)};
                    var vertices=new List<Vector3>();foreach(float z in new[]{-.045f,.045f})foreach(var p in edge)vertices.Add(new Vector3(p.x,p.y,z));
                    MeshPart(rig.WeaponPivot,"Dutch halberd axe blade",vertices.ToArray(),new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7},new Color(.58f,.62f,.59f));
                    Beam(rig.WeaponPivot,"Halberd rear hook",new Vector3(0,2.26f,0),new Vector3(-.29f,2.16f,0),.075f,.065f,iron);
                }
            }
            else if(bow)
            {
                bool compact=e.def.id=="native_scout";float height=compact?.63f:.88f,tipBack=compact?-.19f:-.24f;
                rig.Weapon=Pivot(rig.WeaponPivot,compact?"Scout compact bow":"Longbow",Vector3.zero);
                rig.BowUpperTip=new Vector3(0,height,tipBack);rig.BowLowerTip=new Vector3(0,-height,tipBack);
                var curve=compact?new[]{rig.BowLowerTip,new Vector3(0,-.47f,-.087f),new Vector3(0,-.26f,-.016f),Vector3.zero,new Vector3(0,.26f,-.016f),new Vector3(0,.47f,-.087f),rig.BowUpperTip}:new[]{new Vector3(0,-.88f,-.24f),new Vector3(0,-.65f,-.11f),new Vector3(0,-.36f,-.02f),Vector3.zero,new Vector3(0,.36f,-.02f),new Vector3(0,.65f,-.11f),new Vector3(0,.88f,-.24f)};
                var vertices=new List<Vector3>();var faces=new List<int>();
                for(int i=0;i<curve.Length;i++)
                {
                    var tangent=(curve[Math.Min(i+1,curve.Length-1)]-curve[Math.Max(0,i-1)]).normalized;
                    var side=Vector3.Cross(tangent,Vector3.right).normalized;
                    for(int j=0;j<6;j++){float angle=j*Mathf.PI/3;vertices.Add(curve[i]+(Vector3.right*Mathf.Cos(angle)+side*Mathf.Sin(angle))*.035f);}
                    if(i==0)continue;for(int j=0;j<6;j++){int x=(i-1)*6+j,y=(i-1)*6+(j+1)%6;faces.AddRange(new[]{x,y,x+6,y,y+6,x+6});}
                }
                MeshPart(rig.Weapon,"Curved bow stave",vertices.ToArray(),faces.ToArray(),wood);
                rig.BowStringUpper=Part(rig.Weapon,"Upper bow string",PrimitiveType.Cube,new Vector3(0,height*.5f,tipBack),new Vector3(.014f,height,.014f),new Color(.8f,.72f,.52f)).transform;
                rig.BowStringLower=Part(rig.Weapon,"Lower bow string",PrimitiveType.Cube,new Vector3(0,-height*.5f,tipBack),new Vector3(.014f,height,.014f),new Color(.8f,.72f,.52f)).transform;
            }
            else if(e.def.id=="cuirassier")
            {
                rig.Weapon=Pivot(rig.WeaponPivot,"Cuirassier heavy sword",Vector3.zero);
                Section(rig.Weapon,"Straight steel blade",new Vector3(0,.73f,0),new Vector2(.13f,.055f),new Vector2(.045f,.035f),1.18f,new Color(.68f,.71f,.68f));
                Part(rig.Weapon,"Basket sword guard",PrimitiveType.Cube,new Vector3(0,.045f,0),new Vector3(.31f,.13f,.19f),new Color(.62f,.53f,.31f));
                Part(rig.Weapon,"Leather sword grip",PrimitiveType.Cylinder,new Vector3(0,-.13f,0),new Vector3(.07f,.15f,.07f),wood);
            }
            else if(e.def.id=="hussar"||e.def.id=="outrider"||e.def.id=="spahi")
            {
                rig.Weapon=Pivot(rig.WeaponPivot,"Hussar sabre",Vector3.zero);
                var outline=new[]{new Vector2(-.055f,.08f),new Vector2(.065f,.08f),new Vector2(.07f,.75f),new Vector2(.20f,1.15f),new Vector2(.07f,1.05f),new Vector2(-.055f,.77f)};
                var vertices=new List<Vector3>();var faces=new List<int>();
                foreach(float z in new[]{-.025f,.025f})foreach(var p in outline)vertices.Add(new Vector3(p.x,p.y,z));
                for(int i=1;i<5;i++){faces.AddRange(new[]{0,i+1,i,6,i+6,i+7});}
                for(int i=0;i<6;i++){int j=(i+1)%6;faces.AddRange(new[]{i,j,i+6,j,j+6,i+6});}
                MeshPart(rig.Weapon,"Curved sabre blade",vertices.ToArray(),faces.ToArray(),new Color(.64f,.68f,.65f));
                Part(rig.Weapon,"Sabre guard",PrimitiveType.Cube,new Vector3(0,.02f,0),new Vector3(.3f,.06f,.13f),new Color(.6f,.49f,.25f));
            }
            else if(e.def.id=="abus")
            {
                rig.Weapon=Pivot(rig.WeaponPivot,"Abus handheld cannon",Vector3.zero);
                Part(rig.Weapon,"Hand cannon stock",PrimitiveType.Cube,new Vector3(0,0,-.22f),new Vector3(.21f,.20f,.76f),wood);
                Tube(rig.Weapon,"Heavy hand cannon barrel",new Vector3(0,.105f,.36f),.27f,1.07f,.052f,new Color(.48f,.40f,.25f)).localRotation=Quaternion.Euler(90,0,0);
                Tube(rig.Weapon,"Reinforced hand cannon muzzle",new Vector3(0,.105f,.88f),.33f,.15f,.052f,iron).localRotation=Quaternion.Euler(90,0,0);
                Part(rig.Weapon,"Cannon lock",PrimitiveType.Cube,new Vector3(.16f,.10f,-.02f),new Vector3(.10f,.13f,.19f),iron);
            }
            else if(!e.Worker||e.def.id=="coureur")
            {
                rig.Weapon=Pivot(rig.WeaponPivot,horse?(e.def.id=="carabineer"?"Short carbine":"Dragoon musket"):"Musket",Vector3.zero);
                float length=e.def.id=="carabineer"?.78f:e.def.id=="convoy_gunner"?.62f:e.def.id=="dragoon"?1.12f:.96f;
                Part(rig.Weapon,"Wooden stock",PrimitiveType.Cube,new Vector3(0,0,-.14f),new Vector3(.14f,.17f,.58f),wood);
                Part(rig.Weapon,"Gun barrel",PrimitiveType.Cylinder,new Vector3(0,.065f,length*.5f),new Vector3(.075f,length*.5f,.075f),iron).transform.localRotation=Quaternion.Euler(90,0,0);
                Part(rig.Weapon,"Gun lock",PrimitiveType.Cube,new Vector3(.085f,.06f,.08f),new Vector3(.08f,.10f,.16f),new Color(.62f,.55f,.34f));
                if(e.def.id=="convoy_gunner")Tube(rig.Weapon,"Wide brass muzzle",new Vector3(0,.065f,length),.15f,.19f,.035f,new Color(.62f,.43f,.23f)).localRotation=Quaternion.Euler(90,0,0);
                if(e.def.repairArtillery){rig.WorkerEquipment=rig.gameObject.AddComponent<WorkerEquipment>();rig.WorkerEquipment.Initialize(rig);rig.RepairWeapon=rig.Weapon;}
                if(e.def.id=="coureur"){rig.WorkerEquipment=rig.gameObject.AddComponent<WorkerEquipment>();rig.WorkerEquipment.Initialize(rig);rig.WorkerFirearm=rig.Weapon;}
            }
            else
            {
                rig.WorkerEquipment=rig.gameObject.AddComponent<WorkerEquipment>();
                rig.WorkerEquipment.Initialize(rig);
            }
        }
        private static void ExpansionEquipment(Transform root,EntityState e,UnitRig rig,Color team,float riderY)
        {
            Color copper=new Color(.64f,.43f,.24f),leather=new Color(.28f,.19f,.12f),steel=new Color(.39f,.43f,.41f);
            if(e.def.civilization=="wayfarer")
            {
                Section(root,"Copper shoulder clasp",new Vector3(.24f,1.31f+riderY,.15f),new Vector2(.18f,.17f),new Vector2(.14f,.14f),.10f,copper);
                Part(root,"Indigo travel scarf",PrimitiveType.Cube,new Vector3(-.21f,1.10f+riderY,-.24f),new Vector3(.22f,.60f,.05f),new Color(.19f,.21f,.39f));
                Part(root,"Copper belt buckle",PrimitiveType.Cube,new Vector3(0,.87f+riderY,.238f),new Vector3(.20f,.16f,.04f),copper);
                if(e.def.id=="outrider")
                {foreach(float x in new[]{-.49f,.49f})Part(root,"Road saddlebag",PrimitiveType.Cube,new Vector3(x,1.10f,-.35f),new Vector3(.32f,.39f,.45f),leather);Part(root,"Outrider pennant",PrimitiveType.Cube,new Vector3(-.19f,1.65f,-.31f),new Vector3(.34f,.24f,.045f),team);}
                if(e.def.id=="escort")
                {rig.Shield=Pivot(root,"Copper round escort shield",new Vector3(-.43f,1,.10f));var disc=Tube(rig.Shield,"Shield rim",Vector3.zero,.76f,.12f,.08f,copper);disc.localRotation=Quaternion.Euler(90,0,0);Part(rig.Shield,"Shield face",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.64f,.05f,.64f),team).transform.localRotation=Quaternion.Euler(90,0,0);}
                if(e.def.id=="convoy_gunner")
                {for(int i=0;i<4;i++)Part(root,"Copper powder flask",PrimitiveType.Cylinder,new Vector3(-.20f+i*.13f,1.02f,.25f),new Vector3(.065f,.12f,.065f),copper);}
            }
            if(e.def.id=="field_engineer")
            {
                var rack=Pivot(root,"Engineer tool frame",new Vector3(0,1.02f,-.33f));
                foreach(float x in new[]{-.24f,.24f})Beam(rack,"Backpack frame upright",new Vector3(x,-.33f,0),new Vector3(x,.56f,0),.055f,.07f,leather);
                Part(rack,"Rolled engineering plans",PrimitiveType.Cylinder,new Vector3(0,-.20f,-.12f),new Vector3(.21f,.29f,.21f),new Color(.79f,.72f,.54f)).transform.localRotation=Quaternion.Euler(0,0,90);
                Beam(rack,"Spanner shaft",new Vector3(-.13f,-.12f,0),new Vector3(-.13f,.64f,0),.065f,.07f,steel);
                Part(rack,"Spanner fork left",PrimitiveType.Cube,new Vector3(-.19f,.64f,0),new Vector3(.065f,.17f,.09f),steel);
                Part(rack,"Spanner fork right",PrimitiveType.Cube,new Vector3(-.07f,.64f,0),new Vector3(.065f,.17f,.09f),steel);
                Beam(rack,"Heavy mallet handle",new Vector3(.13f,-.12f,0),new Vector3(.13f,.43f,0),.065f,.07f,leather);
                Part(rack,"Heavy mallet head",PrimitiveType.Cube,new Vector3(.13f,.45f,0),new Vector3(.33f,.17f,.19f),steel);
                Part(root,"Guild leather apron",PrimitiveType.Cube,new Vector3(0,.93f,.25f),new Vector3(.42f,.65f,.06f),leather);
            }
            if(e.def.shieldAbility)
            {
                rig.Shield=Pivot(root,"Folding pavise",new Vector3(-.48f,.90f,.06f));
                Part(rig.Shield,"Square shield board",PrimitiveType.Cube,Vector3.zero,new Vector3(.83f,1.23f,.13f),new Color(.32f,.36f,.31f));
                foreach(float x in new[]{-.40f,.40f})Part(rig.Shield,"Iron edge rail",PrimitiveType.Cube,new Vector3(x,0,-.015f),new Vector3(.065f,1.3f,.17f),steel);
                foreach(float y in new[]{-.59f,.59f})Part(rig.Shield,"Iron cross rail",PrimitiveType.Cube,new Vector3(0,y,-.015f),new Vector3(.85f,.065f,.17f),steel);
                Part(rig.Shield,"Guild shield insignia",PrimitiveType.Cube,new Vector3(0,0,.085f),new Vector3(.18f,.80f,.035f),team);
                Beam(rig.Shield,"Pavise kickstand",new Vector3(0,.45f,-.10f),new Vector3(0,-.55f,-.38f),.07f,.10f,leather);
            }
        }
        private static bool ClassicHeadwear(Transform root,EntityState e,float riderY,Color team)
        {
            Color cream=new Color(.82f,.78f,.64f),iron=new Color(.42f,.46f,.46f),brass=new Color(.64f,.53f,.29f);
            if(e.def.id=="coureur")
            {
                Section(root,"Coureur fur cap",new Vector3(0,1.81f,0),new Vector2(.52f,.48f),new Vector2(.44f,.41f),.25f,new Color(.31f,.26f,.20f));
                Part(root,"Fur cap side fold",PrimitiveType.Cube,new Vector3(-.23f,1.72f,.01f),new Vector3(.10f,.31f,.29f),new Color(.40f,.33f,.24f));return true;
            }
            if(e.def.id=="cuirassier")
            {
                Section(root,"Steel cavalry helmet",new Vector3(0,1.82f+riderY,0),new Vector2(.45f,.42f),new Vector2(.30f,.34f),.28f,iron);
                Part(root,"Helmet peak",PrimitiveType.Cube,new Vector3(0,1.73f+riderY,.22f),new Vector3(.47f,.06f,.23f),iron);
                Section(root,"Brass helmet comb",new Vector3(0,2.06f+riderY,-.04f),new Vector2(.13f,.34f),new Vector2(.09f,.23f),.24f,brass);
                Beam(root,"Black horsehair crest",new Vector3(0,2.11f+riderY,-.10f),new Vector3(0,1.71f+riderY,-.38f),.15f,.20f,new Color(.13f,.14f,.13f));return true;
            }
            if(e.def.id=="janissary")
            {
                Section(root,"Janissary tall felt cap",new Vector3(0,1.96f,0),new Vector2(.39f,.36f),new Vector2(.34f,.28f),.47f,cream);
                Part(root,"Janissary hanging cap cloth",PrimitiveType.Cube,new Vector3(0,1.78f,-.22f),new Vector3(.35f,.66f,.055f),cream);
                Part(root,"Janissary brass cap badge",PrimitiveType.Cube,new Vector3(0,1.95f,.19f),new Vector3(.085f,.34f,.045f),brass);return true;
            }
            if(e.def.id=="abus"||e.def.id=="spahi")
            {
                Section(root,"Ottoman wrapped turban",new Vector3(0,1.79f+riderY,0),new Vector2(.60f,.54f),new Vector2(.47f,.44f),.23f,cream);
                Section(root,"Ottoman cloth crown",new Vector3(0,1.97f+riderY,0),new Vector2(.31f,.29f),new Vector2(.20f,.21f),.23f,team);
                for(int i=0;i<3;i++)Part(root,"Turban cloth fold",PrimitiveType.Cube,new Vector3(0,1.73f+riderY+i*.068f,.245f),new Vector3(.40f,.025f,.035f),new Color(.63f,.59f,.47f));
                if(e.def.id=="spahi")Section(root,"Spahi helmet finial",new Vector3(0,2.17f+riderY,0),new Vector2(.12f,.12f),new Vector2(.015f,.015f),.23f,brass);return true;
            }
            return false;
        }
        private static void ClassicEquipment(Transform root,EntityState e,UnitRig rig,Color team,float riderY)
        {
            Color leather=new Color(.30f,.20f,.12f),steel=new Color(.52f,.56f,.55f),brass=new Color(.65f,.53f,.29f);
            if(e.def.id=="coureur")
            {
                Section(root,"Coureur field pack",new Vector3(0,1.07f,-.30f),new Vector2(.46f,.28f),new Vector2(.42f,.25f),.54f,leather);
                Part(root,"Rolled hunting blanket",PrimitiveType.Cylinder,new Vector3(0,1.37f,-.35f),new Vector3(.27f,.34f,.27f),new Color(.48f,.49f,.35f)).transform.localRotation=Quaternion.Euler(0,0,90);
                foreach(float x in new[]{-.22f,.22f})Beam(root,"Pack shoulder strap",new Vector3(x,1.34f,.20f),new Vector3(x,.84f,.23f),.065f,.045f,leather);
                Part(root,"Woven hunter sash",PrimitiveType.Cube,new Vector3(0,.83f,.26f),new Vector3(.53f,.14f,.045f),team);
            }
            if(e.def.id=="cuirassier")
            {
                Section(root,"French steel breastplate",new Vector3(0,1.055f+riderY,.22f),new Vector2(.39f,.12f),new Vector2(.63f,.19f),.52f,steel);
                Section(root,"French steel backplate",new Vector3(0,1.055f+riderY,-.22f),new Vector2(.39f,.12f),new Vector2(.62f,.15f),.52f,steel);
                foreach(var shoulder in new[]{rig.LeftShoulder,rig.RightShoulder})Section(shoulder,"Brass cavalry epaulette",new Vector3(0,.04f,0),new Vector2(.29f,.25f),new Vector2(.24f,.23f),.12f,brass);
                Part(root,"Cuirassier cartridge case",PrimitiveType.Cube,new Vector3(.42f,.93f+riderY,-.02f),new Vector3(.22f,.27f,.27f),leather);
                foreach(float x in new[]{-.46f,.46f})Part(root,"Heavy cavalry saddle roll",PrimitiveType.Cylinder,new Vector3(x,1.14f,-.39f),new Vector3(.25f,.26f,.25f),new Color(.21f,.28f,.40f)).transform.localRotation=Quaternion.Euler(90,0,0);
            }
            if(e.def.id=="janissary")
            {
                Part(root,"Janissary broad waist belt",PrimitiveType.Cube,new Vector3(0,.87f,.25f),new Vector3(.55f,.17f,.055f),new Color(.52f,.24f,.17f));
                foreach(float x in new[]{-.20f,.20f})Part(root,"Janissary coat braid",PrimitiveType.Cube,new Vector3(x,1.08f,.235f),new Vector3(.055f,.36f,.04f),brass);
                Part(root,"Janissary cartridge pouch",PrimitiveType.Cube,new Vector3(-.39f,.86f,.08f),new Vector3(.24f,.28f,.19f),leather);
                rig.MeleeWeapon=Pivot(rig.WeaponPivot,"Janissary scimitar",Vector3.zero);
                var edge=new[]{new Vector2(-.06f,.10f),new Vector2(.075f,.10f),new Vector2(.10f,.60f),new Vector2(.27f,1.10f),new Vector2(.10f,.94f),new Vector2(-.06f,.63f)};
                var vertices=new List<Vector3>();var faces=new List<int>();foreach(float z in new[]{-.03f,.03f})foreach(var p in edge)vertices.Add(new Vector3(p.x,p.y,z));
                for(int i=1;i<5;i++)faces.AddRange(new[]{0,i+1,i,6,i+6,i+7});for(int i=0;i<6;i++){int j=(i+1)%6;faces.AddRange(new[]{i,j,i+6,j,j+6,i+6});}
                MeshPart(rig.MeleeWeapon,"Curved scimitar blade",vertices.ToArray(),faces.ToArray(),steel);
                Part(rig.MeleeWeapon,"Scimitar brass crossguard",PrimitiveType.Cube,new Vector3(0,.045f,0),new Vector3(.30f,.08f,.13f),brass);rig.MeleeWeapon.gameObject.SetActive(false);
            }
            if(e.def.id=="abus")
            {
                Part(root,"Abus powder satchel",PrimitiveType.Cube,new Vector3(-.37f,.94f,-.08f),new Vector3(.28f,.42f,.28f),leather);
                for(int i=0;i<3;i++)Part(root,"Hand cannon ammunition flask",PrimitiveType.Cylinder,new Vector3(-.18f+i*.18f,1.12f,.25f),new Vector3(.11f,.18f,.11f),brass);
            }
            if(e.def.id=="spahi")
            {
                for(int row=0;row<4;row++)for(int column=0;column<4;column++)Part(root,"Spahi lamellar plate",PrimitiveType.Cube,new Vector3((column-1.5f)*.135f,.91f+riderY+row*.12f,.26f),new Vector3(.12f,.10f,.05f),row%2==0?steel:brass);
                rig.Shield=Pivot(root,"Spahi round shield",new Vector3(-.48f,1.04f+riderY,.05f));Part(rig.Shield,"Spahi shield face",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.73f,.065f,.73f),team).transform.localRotation=Quaternion.Euler(90,0,0);
                Tube(rig.Shield,"Spahi shield brass rim",Vector3.zero,.80f,.14f,.065f,brass).localRotation=Quaternion.Euler(90,0,0);
                foreach(float x in new[]{-.48f,.48f})Part(root,"Spahi embroidered saddle cloth",PrimitiveType.Cube,new Vector3(x,1.08f,-.20f),new Vector3(.09f,.54f,.86f),new Color(.50f,.23f,.20f));
            }
        }
        private static void CargoCaravan(Transform root,UnitRig rig,Color team)
        {
            Color wood=new Color(.31f,.20f,.12f),copper=new Color(.64f,.43f,.24f),indigo=new Color(.21f,.23f,.40f);
            rig.Carriage=Part(root,"Freight chassis",PrimitiveType.Cube,new Vector3(0,.65f,0),new Vector3(1.25f,.22f,1.85f),wood).transform;
            for(int x=-1;x<=1;x+=2)Part(root,"Freight side board",PrimitiveType.Cube,new Vector3(x*.58f,.95f,0),new Vector3(.10f,.50f,1.85f),indigo);
            rig.Cargo=Pivot(root,"Paid cargo crates",Vector3.zero);
            for(int i=0;i<3;i++){var crate=Part(rig.Cargo,"Bound cargo chest",PrimitiveType.Cube,new Vector3(0,.97f,(i-1)*.51f),new Vector3(1.0f,.47f,.45f),wood).transform;Part(crate,"Copper cargo band",PrimitiveType.Cube,new Vector3(0,.025f,-.51f),new Vector3(.14f,.95f,.055f),copper);}
            var wheels=new List<Transform>();foreach(float x in new[]{-.72f,.72f})foreach(float z in new[]{-.60f,.60f})
            {var wheel=Pivot(root,"Freight wheel",new Vector3(x,.40f,z));wheel.localRotation=Quaternion.Euler(0,0,90);Tube(wheel,"Freight iron rim",Vector3.zero,.76f,.16f,.055f,copper);for(int i=0;i<4;i++)Part(wheel,"Freight spoke",PrimitiveType.Cube,Vector3.zero,new Vector3(.67f,.13f,.055f),wood).transform.localRotation=Quaternion.Euler(0,i*45,0);wheels.Add(wheel);}
            rig.CartWheels=wheels.ToArray();
            Part(root,"Merchant flagstaff",PrimitiveType.Cylinder,new Vector3(.51f,1.58f,-.73f),new Vector3(.05f,.72f,.05f),wood);
            Part(root,"Merchant route banner",PrimitiveType.Cube,new Vector3(.72f,2.08f,-.73f),new Vector3(.42f,.32f,.045f),team);
            foreach(float x in new[]{-.41f,.41f})Beam(root,"Freight pulling shaft",new Vector3(x,.64f,.50f),new Vector3(x,.50f,1.43f),.08f,.10f,wood);
        }
        private static void Waystation(Transform root,float w,float d,Color team)
        {
            Color plaster=new Color(.71f,.61f,.44f),indigo=new Color(.21f,.23f,.40f),wood=new Color(.31f,.20f,.12f),copper=new Color(.64f,.43f,.24f);
            var court=Pivot(root,"Open caravan courtyard",Vector3.zero);
            Part(court,"Courtyard paving",PrimitiveType.Cube,new Vector3(0,.38f,0),new Vector3(w*1.8f,.12f,d*1.8f),new Color(.59f,.53f,.40f));
            Hall(root,"Rear lodging range",w*.94f,d*.26f,1.7f,new Vector3(0,0,d*.67f),plaster,indigo,team,RoofForm.Flat);
            Hall(root,"West courtyard lodge",w*.27f,d*.61f,1.4f,new Vector3(-w*.67f,0,-d*.18f),plaster,indigo,team,RoofForm.Flat);
            Awning(root,w*.30f,d*.65f,1.64f,new Vector3(w*.60f,0,-d*.22f),indigo,wood);
            for(int i=0;i<3;i++)Part(root,"Waystation freight chest",PrimitiveType.Cube,new Vector3(w*.60f,.69f,-d*.52f+i*.63f),new Vector3(.75f,.57f,.49f),wood);
            foreach(float x in new[]{-w*.28f,w*.28f})Part(root,"Copper gateway post",PrimitiveType.Cube,new Vector3(x,1.30f,-d*.94f),new Vector3(.14f,1.9f,.16f),copper);
            Part(root,"Route sign lintel",PrimitiveType.Cube,new Vector3(0,2.18f,-d*.94f),new Vector3(w*.77f,.25f,.18f),indigo);Flag(root,w,2.58f,team);
        }
        private static void Workshop(Transform root,float w,float d,Color team)
        {
            Color stone=new Color(.44f,.46f,.42f),wood=new Color(.29f,.20f,.13f),steel=new Color(.26f,.31f,.30f);
            Part(root,"Heavy stone workshop base",PrimitiveType.Cube,new Vector3(0,.50f,0),new Vector3(w*1.96f,.52f,d*1.96f),stone);
            Hall(root,"Guild fabrication hall",w*.68f,d*.80f,1.70f,new Vector3(-w*.27f,.22f,d*.10f),stone,new Color(.26f,.31f,.30f),team,RoofForm.Shed);
            var crane=Pivot(root,"Workshop loading crane",new Vector3(w*.64f,0,-d*.54f));
            Beam(crane,"Crane upright",new Vector3(0,.50f,0),new Vector3(0,3.32f,0),.19f,.22f,wood);
            Beam(crane,"Crane boom",new Vector3(0,3.18f,0),new Vector3(-1.27f,3.18f,-.36f),.17f,.19f,wood);
            Beam(crane,"Diagonal crane brace",new Vector3(0,2.02f,0),new Vector3(-.84f,3.18f,-.24f),.11f,.13f,steel);
            Beam(crane,"Suspended crane cable",new Vector3(-1.1f,3.17f,-.31f),new Vector3(-1.1f,1.47f,-.31f),.035f,.035f,steel);
            Part(crane,"Crane hook",PrimitiveType.Cube,new Vector3(-1.05f,1.44f,-.31f),new Vector3(.18f,.10f,.06f),steel);
            var stock=Pivot(root,"Prefab storage stack",new Vector3(w*.61f,.64f,d*.41f));
            for(int i=0;i<4;i++)Part(stock,"Prepared framing panel",PrimitiveType.Cube,new Vector3(0,.12f+i*.17f,0),new Vector3(.85f,.11f,1.13f),wood);
            Part(root,"Outdoor assembly bench",PrimitiveType.Cube,new Vector3(.12f,1.0f,-d*.83f),new Vector3(1.37f,.18f,.59f),wood);
            Tube(root,"Unfinished counter gun barrel",new Vector3(.12f,1.25f,-d*.83f),.24f,1.47f,.045f,steel).localRotation=Quaternion.Euler(0,0,90);
        }
        private static void Mosque(Transform root,float w,float d,Color team)
        {
            Color plaster=new Color(.74f,.70f,.56f),stone=new Color(.57f,.57f,.49f),roof=new Color(.30f,.40f,.41f),brass=new Color(.69f,.56f,.30f);
            var hall=Pivot(root,"Mosque prayer hall",new Vector3(0,0,d*.10f));
            Part(hall,"Mosque stone plinth",PrimitiveType.Cube,new Vector3(0,.46f,0),new Vector3(w*1.78f,.45f,d*1.72f),stone);
            Part(hall,"Prayer hall walls",PrimitiveType.Cube,new Vector3(0,1.35f,0),new Vector3(w*1.57f,1.47f,d*1.47f),plaster);
            Part(hall,"Dome supporting cornice",PrimitiveType.Cube,new Vector3(0,2.11f,0),new Vector3(w*1.68f,.19f,d*1.59f),stone);
            Dome(hall,"Mosque central dome",Mathf.Min(w,d)*.82f,2.20f,roof);
            Part(hall,"Mosque brass finial",PrimitiveType.Cylinder,new Vector3(0,2.30f+Mathf.Min(w,d)*.72f,0),new Vector3(.07f,.25f,.07f),brass);
            foreach(float x in new[]{-w*.54f,0,w*.54f})
            {
                Part(root,"Entrance arcade column",PrimitiveType.Cylinder,new Vector3(x,1.19f,-d*.84f),new Vector3(.18f,.78f,.18f),plaster);
                Part(root,"Arcade lintel",PrimitiveType.Cube,new Vector3(x,2.02f,-d*.78f),new Vector3(w*.56f,.18f,.45f),stone);
            }
            Part(root,"Recessed mosque door",PrimitiveType.Cube,new Vector3(0,1.04f,-d*.66f),new Vector3(.65f,1.11f,.065f),new Color(.25f,.30f,.26f));
            for(int side=-1;side<=1;side+=2)for(int window=-1;window<=1;window+=2)Part(hall,"Blue glazed window",PrimitiveType.Cube,new Vector3(w*.795f*side,1.43f,d*.40f*window),new Vector3(.04f,.55f,.31f),team);
            var minaret=Pivot(root,"Mosque minaret",new Vector3(w*.83f,0,d*.59f));
            Section(minaret,"Minaret square foot",new Vector3(0,.64f,0),new Vector2(.69f,.69f),new Vector2(.61f,.61f),.78f,stone);
            Section(minaret,"Slender minaret shaft",new Vector3(0,2.29f,0),new Vector2(.43f,.43f),new Vector2(.34f,.34f),2.68f,plaster);
            Tube(minaret,"Minaret gallery rim",new Vector3(0,3.28f,0),.80f,.21f,.10f,stone);
            Section(minaret,"Minaret upper lantern",new Vector3(0,3.67f,0),new Vector2(.34f,.34f),new Vector2(.27f,.27f),.63f,plaster);
            Section(minaret,"Pointed minaret cap",new Vector3(0,4.21f,0),new Vector2(.46f,.46f),new Vector2(.018f,.018f),.53f,roof);
            Flag(root,w,2.59f,team);
        }
        private static void Dome(Transform root,string name,float radius,float bottom,Color color)
        {
            var vertices=new List<Vector3>();var faces=new List<int>();const int sides=12,rings=4;
            for(int ring=0;ring<=rings;ring++)
            {float angle=ring*Mathf.PI*.5f/rings;for(int side=0;side<sides;side++){float around=side*Mathf.PI*2/sides;vertices.Add(new Vector3(Mathf.Cos(around)*Mathf.Cos(angle)*radius,bottom+Mathf.Sin(angle)*radius*.78f,Mathf.Sin(around)*Mathf.Cos(angle)*radius));}}
            for(int ring=0;ring<rings;ring++)for(int side=0;side<sides;side++){int a=ring*sides+side,b=ring*sides+(side+1)%sides;faces.AddRange(new[]{a,a+sides,b,b,a+sides,b+sides});}
            MeshPart(root,name,vertices.ToArray(),faces.ToArray(),color);
        }
        private static Transform Pivot(Transform parent,string name,Vector3 position){var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;}
        private static void Building(Transform root,EntityState e,Color team)
        {
            float w=e.def.width*.44f,d=e.def.depth*.44f;
            Color plaster=new Color(.69f,.64f,.48f),wood=new Color(.31f,.21f,.13f),roof=new Color(.38f,.24f,.16f),stone=new Color(.47f,.48f,.42f);
            Part(root,"Foundation",PrimitiveType.Cube,new Vector3(0,.18f,0),new Vector3(w*2+.25f,.36f,d*2+.25f),new Color(.39f,.38f,.31f));
            if(e.def.id=="waystation"){Waystation(root,w,d,team);return;}
            if(e.def.id=="workshop"){Workshop(root,w,d,team);return;}
            if(e.def.id=="mosque"){Mosque(root,w,d,team);return;}
            if(e.def.id=="farm")
            {
                Part(root,"Tilled field",PrimitiveType.Cube,new Vector3(0,.38f,0),new Vector3(w*1.9f,.10f,d*1.9f),wood);
                Crops(root,w*.85f,d*.85f,7,new Color(.69f,.62f,.25f),Vector3.zero);
                foreach(float x in new[]{-w*.94f,w*.94f})Part(root,"Field boundary",PrimitiveType.Cube,new Vector3(x,.45f,0),new Vector3(.075f,.12f,d*1.90f),stone);return;
            }
            if(e.def.id=="barracks")
            {
                Hall(root,"Training hall",w,d*.60f,1.9f,new Vector3(0,0,d*.35f),plaster,roof,team);
                Part(root,"Training court",PrimitiveType.Cube,new Vector3(0,.40f,-d*.70f),new Vector3(w*1.85f,.08f,d*.56f),new Color(.58f,.52f,.36f));
                foreach(float x in new[]{-w*.68f,w*.68f})
                {
                    for(int i=0;i<3;i++)Beam(root,"Rack pike",new Vector3(x+(i-1)*.14f,.47f,-d*.73f),new Vector3(x+(i-1)*.14f,1.58f,-d*.67f),.055f,.055f,new Color(.46f,.46f,.39f));
                    Part(root,"Weapon rack",PrimitiveType.Cube,new Vector3(x,.95f,-d*.70f),new Vector3(.58f,.12f,.17f),wood);
                }
                Section(root,"Practice target",new Vector3(0,1.0f,-d*.76f),new Vector2(.34f,.20f),new Vector2(.44f,.20f),.55f,new Color(.65f,.49f,.27f));
                Beam(root,"Target post",new Vector3(0,.4f,-d*.76f),new Vector3(0,1.35f,-d*.76f),.10f,.10f,wood);return;
            }
            if(e.def.id=="stable")
            {
                var shed=Pivot(root,"Open stable",new Vector3(0,0,d*.26f));
                Part(shed,"Stable back wall",PrimitiveType.Cube,new Vector3(0,1.14f,d*.63f),new Vector3(w*1.9f,1.8f,.17f),wood);
                foreach(float x in new[]{-w*.86f,w*.86f})
                {Part(shed,"Stable side wall",PrimitiveType.Cube,new Vector3(x,1.14f,0),new Vector3(.18f,1.8f,d*1.3f),plaster);Part(shed,"Open gate post",PrimitiveType.Cube,new Vector3(x,1.2f,-d*.62f),new Vector3(.18f,1.95f,.18f),wood);}
                Roof(shed,w*1.1f,d*.72f,2.12f,roof);
                for(int i=-1;i<=1;i++)Part(shed,"Stall divider",PrimitiveType.Cube,new Vector3(i*w*.57f,.82f,d*.22f),new Vector3(.10f,.90f,d*.7f),wood);
                Section(shed,"Hay bale",new Vector3(-w*.53f,.65f,d*.33f),new Vector2(.47f,.46f),new Vector2(.42f,.43f),.47f,new Color(.72f,.59f,.29f));
                Fence(root,w*.88f,d*.85f,wood);Flag(root,w,2.5f,team);return;
            }
            if(e.def.id=="foundry")
            {
                Hall(root,"Brick forge",w*.95f,d*.90f,1.9f,Vector3.zero,new Color(.48f,.34f,.26f),new Color(.28f,.28f,.26f),team);
                foreach(float x in new[]{-w*.56f,w*.56f})
                {Part(root,"Forge chimney",PrimitiveType.Cube,new Vector3(x,3.20f,d*.36f),new Vector3(.49f,2.30f,.49f),stone);Part(root,"Soot chimney cap",PrimitiveType.Cube,new Vector3(x,4.36f,d*.36f),new Vector3(.61f,.17f,.61f),new Color(.21f,.23f,.22f));}
                Part(root,"Furnace iron frame",PrimitiveType.Cube,new Vector3(0,.91f,-d*.93f),new Vector3(.97f,1.14f,.15f),new Color(.20f,.22f,.21f));
                Part(root,"Furnace opening",PrimitiveType.Cube,new Vector3(0,.86f,-d*.996f),new Vector3(.65f,.80f,.035f),new Color(.11f,.09f,.07f));
                Part(root,"Hot forge bed",PrimitiveType.Cube,new Vector3(0,.53f,-d*1.02f),new Vector3(.58f,.19f,.035f),new Color(.94f,.38f,.09f));return;
            }
            if(e.def.id=="market")
            {
                Hall(root,"Market store",w*.72f,d*.35f,1.45f,new Vector3(0,0,d*.55f),plaster,roof,team);
                Awning(root,w*.90f,d*.68f,1.60f,new Vector3(0,0,-d*.30f),team,wood);
                Part(root,"Market counter",PrimitiveType.Cube,new Vector3(0,.82f,-d*.60f),new Vector3(w*1.6f,.16f,.48f),wood);
                for(int i=-1;i<=1;i++)
                {Section(root,"Produce bin",new Vector3(i*w*.53f,.97f,-d*.58f),new Vector2(.44f,.37f),new Vector2(.49f,.40f),.23f,wood);Section(root,"Produce heap",new Vector3(i*w*.53f,1.12f,-d*.58f),new Vector2(.41f,.32f),new Vector2(.21f,.21f),.15f,i==0?new Color(.70f,.28f,.16f):new Color(.69f,.66f,.25f));}return;
            }
            if(e.def.id=="bank")
            {
                Hall(root,"Stone bank",w*.95f,d*.93f,2.25f,Vector3.zero,new Color(.61f,.61f,.54f),new Color(.28f,.35f,.36f),team,RoofForm.Flat);
                foreach(float x in new[]{-w*.78f,w*.78f})Section(root,"Thick stone pilaster",new Vector3(x,1.36f,-d*.94f),new Vector2(.32f,.27f),new Vector2(.32f,.27f),2.08f,stone);
                Part(root,"Vault door",PrimitiveType.Cube,new Vector3(0,.96f,-d*.965f),new Vector3(.76f,1.32f,.10f),new Color(.20f,.25f,.25f));
                var emblem=Section(root,"Gold vault seal",new Vector3(0,1.92f,-d*1.015f),new Vector2(.61f,.61f),new Vector2(.61f,.61f),.09f,new Color(.80f,.64f,.24f));emblem.localRotation=Quaternion.Euler(90,0,0);
                Part(root,"Vault seal key",PrimitiveType.Cube,new Vector3(0,1.92f,-d*1.065f),new Vector3(.10f,.29f,.035f),wood);return;
            }
            if(e.def.id=="estate")
            {
                Hall(root,"Estate counting house",w*.88f,d*.50f,1.65f,new Vector3(0,0,d*.45f),plaster,new Color(.32f,.39f,.32f),team,RoofForm.Hip);
                Crops(root,w*.76f,d*.29f,4,new Color(.32f,.44f,.19f),new Vector3(0,0,-d*.62f));
                Part(root,"Ledger desk",PrimitiveType.Cube,new Vector3(w*.65f,.87f,-d*.08f),new Vector3(.56f,.14f,.46f),wood);
                Part(root,"Open account book",PrimitiveType.Cube,new Vector3(w*.65f,.965f,-d*.08f),new Vector3(.40f,.055f,.32f),new Color(.87f,.81f,.64f));return;
            }
            if(e.def.id=="trade_post")
            {
                Hall(root,"Freight warehouse",w*.90f,d*.48f,1.85f,new Vector3(0,0,d*.43f),plaster,roof,team,RoofForm.Shed);
                Awning(root,w*.90f,d*.44f,1.62f,new Vector3(0,0,-d*.48f),team,wood);
                for(int i=-1;i<=1;i++)Part(root,"Freight crate",PrimitiveType.Cube,new Vector3(i*w*.52f,.70f,-d*.61f),new Vector3(.65f,.62f,.61f),wood);
                Part(root,"Loading deck",PrimitiveType.Cube,new Vector3(0,.41f,-d*.59f),new Vector3(w*1.8f,.12f,d*.75f),wood);return;
            }
            float h=e.def.id=="town"?2.8f:e.def.id=="tower"?5.2f:1.9f;
            Hall(root,"Main building",w,d,h,Vector3.zero,plaster,roof,team);
            if(e.def.id=="town")
            {Part(root,"Clock tower",PrimitiveType.Cube,new Vector3(0,h+.75f,0),new Vector3(1.4f,1.8f,1.4f),plaster);Roof(root,.9f,.9f,h+1.7f,team);}
            if(e.def.id=="tower")Part(root,"Watch balcony",PrimitiveType.Cube,new Vector3(0,4.5f,0),new Vector3(w*2.1f,.24f,d*2.1f),wood);
        }
        private static void Hall(Transform root,string name,float w,float d,float h,Vector3 position,Color plaster,Color roof,Color team,RoofForm form=RoofForm.Gable)
        {
            var hall=Pivot(root,name,position);Color wood=new Color(.31f,.21f,.13f);
            Part(hall,"Walls",PrimitiveType.Cube,new Vector3(0,h*.5f+.25f,0),new Vector3(w*2,h,d*2),plaster);
            if(form==RoofForm.Flat)
            {
                Part(hall,"Flat vault roof",PrimitiveType.Cube,new Vector3(0,h+.31f,0),new Vector3(w*2.12f,.16f,d*2.12f),roof);
                foreach(float x in new[]{-w,w})Part(hall,"Stone roof side parapet",PrimitiveType.Cube,new Vector3(x,h+.49f,0),new Vector3(.15f,.38f,d*2.1f),plaster);
                foreach(float z in new[]{-d,d})Part(hall,"Stone roof end parapet",PrimitiveType.Cube,new Vector3(0,h+.49f,z),new Vector3(w*2.1f,.38f,.15f),plaster);
            }
            else if(form==RoofForm.Hip)
            {
                float rw=w*1.12f,rd=d*1.12f,y=h+.25f;
                MeshPart(hall,"Four slope counting house roof",new[]{new Vector3(-rw,y,-rd),new Vector3(rw,y,-rd),new Vector3(rw,y,rd),new Vector3(-rw,y,rd),new Vector3(0,y+w*.60f,0)},new[]{0,4,1,1,4,2,2,4,3,3,4,0,0,1,2,0,2,3},roof);
            }
            else if(form==RoofForm.Shed)
            {
                float rw=w*1.12f,rd=d*1.12f,y=h+.25f;
                MeshPart(hall,"Single slope warehouse roof",new[]{new Vector3(-rw,y,-rd),new Vector3(rw,y,-rd),new Vector3(-rw,y,rd),new Vector3(rw,y,rd),new Vector3(-rw,y+.62f,rd),new Vector3(rw,y+.62f,rd)},new[]{0,4,1,1,4,5,0,2,4,1,5,3,2,3,5,2,5,4,0,1,3,0,3,2},roof);
            }
            else Roof(hall,w*1.12f,d*1.12f,h+.25f,roof);
            for(int side=-1;side<=1;side+=2)
            {Part(hall,"Door",PrimitiveType.Cube,new Vector3(0,.8f,d*side+.02f),new Vector3(.72f,1.12f,.075f),wood);for(int i=-1;i<=1;i+=2)Part(hall,"Shutter",PrimitiveType.Cube,new Vector3(w*.62f*i,1.25f,d*side+.03f),new Vector3(.35f,.53f,.075f),team);}
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Part(hall,"Timber",PrimitiveType.Cube,new Vector3(w*x,h*.5f+.2f,d*z),new Vector3(.14f,h+.2f,.14f),wood);
            Flag(hall,w,h+.65f,team);
        }
        private static void Flag(Transform root,float w,float y,Color team)
        {Part(root,"Banner pole",PrimitiveType.Cube,new Vector3(w*.52f,y,0),new Vector3(.06f,.95f,.06f),new Color(.31f,.21f,.13f));Part(root,"Team banner",PrimitiveType.Cube,new Vector3(w*.52f+w*.19f,y+.22f,0),new Vector3(w*.38f,.48f,.055f),team);}
        private static void Fence(Transform root,float w,float d,Color wood)
        {
            foreach(float x in new[]{-w,w})
            {foreach(float z in new[]{-d,-d*.36f})Part(root,"Corral post",PrimitiveType.Cube,new Vector3(x,.85f,z),new Vector3(.10f,.95f,.10f),wood);foreach(float y in new[]{.65f,1.05f})Part(root,"Corral side rail",PrimitiveType.Cube,new Vector3(x,y,-d*.68f),new Vector3(.075f,.10f,d*.7f),wood);}
            foreach(float x in new[]{-w*.65f,w*.65f})foreach(float y in new[]{.65f,1.05f})Part(root,"Corral front rail",PrimitiveType.Cube,new Vector3(x,y,-d),new Vector3(w*.70f,.10f,.075f),wood);
        }
        private static void Awning(Transform root,float w,float d,float y,Vector3 position,Color cloth,Color wood)
        {
            var shed=Pivot(root,"Covered frontage",position);
            Part(shed,"Cloth canopy",PrimitiveType.Cube,new Vector3(0,y,0),new Vector3(w*2,.11f,d*2),cloth).transform.localRotation=Quaternion.Euler(-8,0,0);
            foreach(float x in new[]{-w*.93f,w*.93f})Part(shed,"Awning post",PrimitiveType.Cube,new Vector3(x,y*.5f,-d*.86f),new Vector3(.11f,y,.11f),wood);
            for(int i=-1;i<=1;i++)Part(shed,"Canopy linen stripe",PrimitiveType.Cube,new Vector3(i*w*.56f,y+.067f,0),new Vector3(.14f,.012f,d*1.94f),new Color(.84f,.79f,.59f)).transform.localRotation=Quaternion.Euler(-8,0,0);
        }
        private static void Crops(Transform root,float w,float d,int rows,Color color,Vector3 position)
        {
            var vertices=new List<Vector3>();var faces=new List<int>();
            for(int row=0;row<rows;row++)for(int plant=0;plant<7;plant++)
            {
                float x=Mathf.Lerp(-w,w,(row+.5f)/rows),z=Mathf.Lerp(-d,d,(plant+.5f)/7),height=.26f+(plant%3)*.055f;int n=vertices.Count;
                vertices.AddRange(new[]{new Vector3(x-.095f,.40f,z-.06f)+position,new Vector3(x+.095f,.40f,z-.06f)+position,new Vector3(x+.095f,.40f,z+.06f)+position,new Vector3(x-.095f,.40f,z+.06f)+position,new Vector3(x,.40f+height,z)+position});
                faces.AddRange(new[]{n,n+4,n+1,n+1,n+4,n+2,n+2,n+4,n+3,n+3,n+4,n});
            }
            MeshPart(root,"Crop rows",vertices.ToArray(),faces.ToArray(),color);
        }
        private static void Resource(Transform root,EntityState e)
        {
            if(DetailedEnvironment.TryBuild(root,e))return;
            Color wood=new Color(.3f,.22f,.12f);
            if(e.def.id=="trade_socket"){for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Part(root,"Site marker",PrimitiveType.Cylinder,new Vector3(x*1.8f,.35f,z*1.8f),new Vector3(.18f,.35f,.18f),new Color(.65f,.56f,.28f));return;}
            if(World.Treasure(e)){Part(root,"Treasure chest",PrimitiveType.Cube,new Vector3(0,.5f,0),new Vector3(1.2f,.8f,.8f),wood);Part(root,"Brass lid",PrimitiveType.Cube,new Vector3(0,.96f,0),new Vector3(1.3f,.2f,.9f),new Color(.77f,.61f,.24f));Part(root,"Cache marker",PrimitiveType.Cylinder,new Vector3(0,1.6f,0),new Vector3(.08f,.65f,.08f),wood);return;}
            switch(e.def.id)
            {
                case "tree":
                    Part(root,"Trunk",PrimitiveType.Cylinder,new Vector3(0,1.4f,0),new Vector3(.5f,1.4f,.5f),wood);
                    for(int i=0;i<3;i++)Part(root,"Crown",PrimitiveType.Sphere,new Vector3((i-1)*.6f,3.2f+i*.4f,0),new Vector3(2.5f,2.8f,2.3f),new Color(.17f+i*.035f,.3f+i*.03f,.12f));break;
                case "mine":
                    for(int i=0;i<5;i++)Part(root,"Ore",PrimitiveType.Sphere,new Vector3((i%3-1)*.7f,.45f,(i/3)*.7f),new Vector3(1.7f,.8f+i*.12f,1.5f),new Color(.39f+i*.025f,.39f+i*.023f,.36f));break;
                case "deer":
                    Part(root,"Deer body",PrimitiveType.Cube,new Vector3(0,.8f,0),new Vector3(.4f,.5f,1),new Color(.48f,.32f,.17f));
                    Part(root,"Deer head",PrimitiveType.Cube,new Vector3(0,1.13f,.55f),new Vector3(.28f,.5f,.4f),new Color(.5f,.34f,.19f));
                    for(int i=0;i<4;i++)Part(root,"Deer leg",PrimitiveType.Cube,new Vector3(i%2==0?-.13f:.13f,.35f,i<2?-.3f:.3f),new Vector3(.09f,.7f,.09f),wood);break;
                case "berry":
                    for(int i=0;i<3;i++)Part(root,"Berry bush",PrimitiveType.Sphere,new Vector3((i-1)*.55f,.6f,0),new Vector3(1.2f,1.3f,1.2f),new Color(.3f,.39f,.14f));
                    for(int i=0;i<8;i++)Part(root,"Berries",PrimitiveType.Sphere,new Vector3(Mathf.Sin(i*2)*.8f,1+Mathf.Cos(i)*.25f,Mathf.Cos(i*2)*.6f),Vector3.one*.17f,new Color(.52f,.16f,.16f));break;
                default:
                    SupplyCrate(root,e.def.resource);break;
            }
        }
        private static void SupplyCrate(Transform root,ResourceKind resource)
        {
            Color wood=new Color(.57f,.40f,.21f),iron=new Color(.25f,.27f,.24f);
            Part(root,"Open supply crate base",PrimitiveType.Cube,new Vector3(0,.20f,0),new Vector3(.99f,.16f,.83f),wood);
            foreach(float x in new[]{-.45f,.45f})Part(root,"Crate side plank",PrimitiveType.Cube,new Vector3(x,.40f,0),new Vector3(.09f,.40f,.83f),wood);
            foreach(float z in new[]{-.37f,.37f})Part(root,"Crate end plank",PrimitiveType.Cube,new Vector3(0,.40f,z),new Vector3(.94f,.40f,.09f),wood);
            foreach(float x in new[]{-.28f,.28f})Part(root,"Crate iron strap",PrimitiveType.Cube,new Vector3(x,.40f,-.425f),new Vector3(.055f,.43f,.035f),iron);
            if(resource==ResourceKind.Wood)
            {
                for(int i=0;i<5;i++)
                {float z=(i%3-1)*.20f,y=.50f+i/3*.19f;var log=Section(root,"Visible timber log",new Vector3(0,y,z),new Vector2(.20f,.20f),new Vector2(.20f,.20f),.82f,new Color(.31f,.19f,.10f));log.localRotation=Quaternion.Euler(0,0,90);Section(log,"Pale cut end",new Vector3(0,.42f,0),new Vector2(.17f,.17f),new Vector2(.17f,.17f),.018f,new Color(.77f,.59f,.31f));}
            }
            else if(resource==ResourceKind.Coin)
            {
                for(int i=0;i<6;i++)Section(root,"Visible gold ingot",new Vector3((i%3-1)*.26f,.48f+i/3*.13f,(i/3-.5f)*.26f),new Vector2(.24f,.29f),new Vector2(.17f,.23f),.14f,new Color(.88f,.69f,.20f));
            }
            else
            {
                for(int i=0;i<3;i++)Section(root,"Food grain sack",new Vector3((i-1)*.26f,.58f,.03f),new Vector2(.24f,.31f),new Vector2(.14f,.19f),.46f,new Color(.79f,.68f,.39f));
                foreach(float x in new[]{-.14f,.14f})Section(root,"Food produce",new Vector3(x,.66f,-.20f),new Vector2(.20f,.18f),new Vector2(.15f,.15f),.21f,new Color(.69f,.25f,.13f));
            }
        }
    }
}
