using System;
using System.Collections.Generic;
using UnityEngine;
using S=Frontier.ArchitecturalMaterials.Surface;

namespace Frontier
{
    // Original modular architecture, inspired by early-modern craft and the
    // readable tabletop proportions of classic historical strategy games.
    public static class DetailedArchitecture
    {
        public static string CivilizationFor(EntityState e,string requested)
        {
            if(e.def.id=="bank")return "river";if(e.def.id=="mosque")return "ottoman";
            if(e.def.id=="waystation")return "wayfarer";if(e.def.id=="workshop")return "highland";
            if(string.IsNullOrEmpty(requested))requested=string.IsNullOrEmpty(e.def.civilization)?"coast":e.def.civilization;
            return requested;
        }
        public static void Build(Transform root,EntityState entity,Color team,string civilization,int age)
        {new Builder(root,entity,team,CivilizationFor(entity,civilization),Mathf.Clamp(age,1,5)).Build();}
        sealed class Builder
        {
            readonly Transform root;readonly EntityState entity;readonly string civ;readonly int age;readonly float w,d;
            readonly ArchitecturalGeometry ground=new ArchitecturalGeometry(),wall=new ArchitecturalGeometry(),roof=new ArchitecturalGeometry(),detail=new ArchitecturalGeometry();
            readonly Material stone,facade,trim,wood,roofMat,iron,glass,dark,team,canvas,earth,brass,leaf;
            readonly bool dutch,french,ottoman,wayfarer,highland;
            readonly S roofSurface;readonly Color roofColor;
            readonly List<Transform> wallFeatures=new List<Transform>(),roofFeatures=new List<Transform>(),detailFeatures=new List<Transform>(),groundFeatures=new List<Transform>();
            float highest;
            public Builder(Transform root,EntityState e,Color teamColor,string civilization,int age)
            {
                this.root=root;entity=e;civ=civilization;this.age=age;w=e.def.width*.46f;d=e.def.depth*.46f;
                dutch=civ=="river";french=civ=="france";ottoman=civ=="ottoman";wayfarer=civ=="wayfarer";highland=civ=="highland";
                Material M(S s,float r,float g,float b)=>ArchitecturalMaterials.Get(s,new Color(r,g,b));
                stone=M(S.Stone,highland?.43f:.57f,highland?.45f:.54f,highland?.42f:.46f);
                facade=dutch?M(S.Brick,.64f,.29f,.19f):french?M(S.Plaster,.83f,.78f,.65f):ottoman?M(S.Stone,.79f,.69f,.49f):wayfarer?M(S.Plaster,.69f,.52f,.33f):highland?M(S.Stone,.55f,.56f,.49f):M(S.Brick,.64f,.39f,.27f);
                trim=M(S.Stone,french?.92f:.83f,french?.88f:.78f,french?.75f:.64f);wood=M(S.Wood,.30f,.20f,.12f);
                roofSurface=ottoman||dutch||wayfarer?S.Tile:S.Slate;
                roofColor=dutch?new Color(.54f,.25f,.14f):french?new Color(.24f,.34f,.41f):ottoman?new Color(.52f,.32f,.19f):wayfarer?new Color(.64f,.52f,.35f):highland?new Color(.25f,.31f,.31f):new Color(.31f,.34f,.33f);
                roofMat=ArchitecturalMaterials.Get(roofSurface,roofColor);iron=M(S.Metal,.18f,.22f,.23f);brass=M(S.Metal,.67f,.50f,.22f);
                dark=M(S.Wood,.065f,.071f,.064f);glass=M(S.Glass,.19f,.30f,.33f);team=ArchitecturalMaterials.Get(S.Cloth,teamColor);
                canvas=M(S.Cloth,.80f,.73f,.54f);earth=M(S.Earth,.40f,.32f,.20f);leaf=M(S.Cloth,.39f,.47f,.22f);
            }
            public void Build()
            {
                ground.Box(new Vector3(0,.09f,0),new Vector3(w*2,.18f,d*2),stone);
                // The plinth and its separate bevel-like courses ground the model.
                ground.Box(new Vector3(0,.195f,0),new Vector3(w*1.95f,.07f,d*1.95f),trim);
                switch(entity.def.id)
                {
                    case "town": Town();break;case "house": House();break;case "barracks": Barracks();break;
                    case "stable": Stable();break;case "foundry": Foundry();break;case "market": Market();break;
                    case "tower": Tower();break;case "farm": Farm();break;case "bank": Bank();break;
                    case "estate": Estate();break;case "trade_post": TradePost();break;
                    case "waystation": Waystation();break;case "workshop": Workshop();break;case "mosque": Mosque();break;
                    default: House();break;
                }
                var model=root.gameObject.AddComponent<ArchitectureModel>();model.Civilization=civ;model.Age=age;
                ground.Create(root,"Foundation and ground dressing");model.Walls=wall.Create(root,"Masonry and structural joinery");
                model.Roofs=roof.Create(root,"Crafted roof silhouette");model.Details=detail.Create(root,"Doors windows ironwork and livelihood");
                foreach(var feature in groundFeatures)feature.SetParent(root.Find("Foundation and ground dressing"),false);
                foreach(var feature in wallFeatures)feature.SetParent(model.Walls,false);
                foreach(var feature in roofFeatures)feature.SetParent(model.Roofs,false);
                foreach(var feature in detailFeatures)feature.SetParent(model.Details,false);
                // Optional named feature meshes remain real rendered geometry.
                model.Scaffold=Scaffolding();
                bool first=true;Bounds bounds=default;
                foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(f.transform.IsChildOf(model.Scaffold))continue;
                    foreach(var p in f.sharedMesh.vertices){var point=root.InverseTransformPoint(f.transform.TransformPoint(p));if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);}
                }
                model.LocalBounds=bounds;model.SetConstruction(entity.construction);
            }
            void Town()
            {
                if(ottoman)
                {
                    Hall(new Vector3(0,0,d*.20f),w*.67f,d*.58f,2.55f,true,false);
                    Dome(new Vector3(0,2.98f,d*.18f),w*.53f,.85f,"Council lead dome",ArchitecturalMaterials.Get(S.Metal,new Color(.31f,.43f,.44f)));
                    Arcade(new Vector3(0,.22f,-d*.63f),w*1.40f,.62f,1.10f,4);
                    foreach(int s in new[]{-1,1})Hall(new Vector3(s*w*.76f,0,d*.14f),w*.20f,d*.55f,1.85f,false);
                    Flag(new Vector3(w*.68f,2.82f,-d*.43f));return;
                }
                if(wayfarer){Courtyard(false);return;}
                if(highland){HighlandTown();return;}
                float mainHeight=2.62f+age*.06f;
                Hall(new Vector3(0,0,d*.12f),w*.68f,d*.69f,mainHeight,true);
                foreach(int s in new[]{-1,1})Hall(new Vector3(s*w*.78f,0,d*.22f),w*.16f,d*.52f,1.74f,false);
                Porch(new Vector3(0,.22f,-d*.75f),w*.51f,.43f,1.53f);
                if(age>=2)
                {
                    float mainRise=Mathf.Clamp(w*.68f*(dutch?.95f:french?.94f:.78f),.24f,1.55f);
                    var c=new Vector3(0,.23f+mainHeight+.045f+mainRise+.30f,d*.19f);float radius=.32f;
                    wall.Box(c,new Vector3(radius*2,.82f,radius*2),facade);
                    foreach(int s in new[]{-1,1})Window(c+new Vector3(0,.04f,s*(radius+.017f)),.34f,.36f,s==1?180:0,false);
                    var clock=new ArchitecturalGeometry();clock.Cylinder(c+new Vector3(0,.02f,-radius-.047f),.15f,.02f,trim,24,Quaternion.Euler(90,0,0));
                    clock.Beam(c+new Vector3(0,.02f,-radius-.065f),c+new Vector3(0,.135f,-radius-.065f),.018f,.018f,iron);
                    clock.Beam(c+new Vector3(0,.02f,-radius-.065f),c+new Vector3(.09f,-.035f,-radius-.065f),.016f,.016f,iron);Feature(clock,"Town clock dial",3);
                    Roof(c+Vector3.up*.44f,radius+.10f,radius+.10f,.57f,1);
                    Finial(c+Vector3.up*1.09f);
                }
                Flag(new Vector3(w*.75f,3.0f,-d*.30f));
            }
            void House()
            {
                if(highland){HighlandHouse();return;}
                float h=1.83f+(age>=3?.25f:0);
                Hall(new Vector3(0,0,d*.06f),w*.84f,d*.77f,h,age>=3);
                Steps(new Vector3(0,.22f,-d*.81f),.71f,.34f);
                if(!ottoman&&!wayfarer)Chimney(new Vector3(-w*.55f,h+.46f,d*.34f),.27f,.78f);
                if(ottoman)
                {
                    detail.Box(new Vector3(0,1.43f,-d*.74f),new Vector3(1.24f,.85f,.22f),wood);
                    for(int i=-1;i<=1;i++)Window(new Vector3(i*.37f,1.50f,-d*.88f),.27f,.51f,0,true);
                    foreach(int s in new[]{-1,1})detail.Beam(new Vector3(s*.51f,.76f,-d*.70f),new Vector3(s*.51f,1.07f,-d*.85f),.065f,.065f,wood);
                }
                Barrel(new Vector3(w*.70f,.22f,-d*.80f),.15f,.41f);FlowerBox(new Vector3(-w*.65f,.35f,-d*.84f));
                Flag(new Vector3(w*.62f,h+.66f,d*.12f));
            }
            void HighlandTown()
            {
                // An asymmetric stone guildhall and freestanding corner belfry,
                // with a working timber platform instead of the British portico.
                Hall(new Vector3(w*.24f,0,d*.25f),w*.59f,d*.62f,1.88f+age*.035f,true);
                float towerHalf=w*.24f,towerDepth=d*.30f,towerHeight=2.75f+age*.13f;
                var tower=new Vector3(-w*.60f,.23f,-d*.20f);
                wall.Box(tower+Vector3.up*(towerHeight*.5f),new Vector3(towerHalf*2,towerHeight,towerDepth*2),stone);
                for(int course=0;course<4;course++)Cornice(tower+Vector3.up*(.12f+course*towerHeight*.29f),towerHalf+.015f,towerDepth+.015f,.09f,trim);
                Door(tower+new Vector3(0,0,-towerDepth-.015f),.49f,1.24f,0,false);
                for(int level=0;level<2;level++)Window(tower+new Vector3(0,1.60f+level*.68f,-towerDepth-.015f),.18f,.41f,0,false);
                float upper=tower.y+towerHeight;
                wall.Box(new Vector3(tower.x,upper+.06f,tower.z),new Vector3(towerHalf*2.20f,.16f,towerDepth*2.12f),wood);
                foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
                    wall.Beam(new Vector3(tower.x+x*towerHalf*.79f,upper+.10f,tower.z+z*towerDepth*.79f),new Vector3(tower.x+x*towerHalf*.79f,upper+.77f,tower.z+z*towerDepth*.79f),.08f,.08f,wood);
                detail.Lathe(new Vector3(tower.x,upper+.20f,tower.z),new[]{new Vector2(.19f,0),new Vector2(.20f,.045f),new Vector2(.13f,.16f),new Vector2(.045f,.29f),new Vector2(0,.31f)},brass,20);
                detail.Beam(new Vector3(tower.x,upper+.48f,tower.z),new Vector3(tower.x,upper+.77f,tower.z),.032f,.032f,iron);
                Roof(new Vector3(tower.x,upper+.79f,tower.z),towerHalf+.10f,towerDepth+.08f,.57f,1);
                if(age>=4)Finial(new Vector3(tower.x,upper+1.43f,tower.z));
                ground.Box(new Vector3(w*.34f,.30f,-d*.69f),new Vector3(w*.87f,.15f,.70f),wood);
                Steps(new Vector3(w*.34f,.27f,-d*.86f),.82f,.25f);
                foreach(int side in new[]{-1,1})
                {
                    float x=w*.34f+side*w*.40f;
                    wall.Beam(new Vector3(x,.31f,-d*.67f),new Vector3(x,1.65f,-d*.67f),.085f,.085f,wood);
                    wall.Beam(new Vector3(x,.66f,-d*.66f),new Vector3(x,1.66f,-d*.35f),.055f,.065f,wood);
                }
                wall.Beam(new Vector3(-w*.06f,1.69f,-d*.67f),new Vector3(w*.74f,1.69f,-d*.67f),.11f,.13f,wood);
                Crate(new Vector3(w*.65f,.38f,-d*.68f),.38f,.31f,.30f);
                Barrel(new Vector3(-w*.08f,.38f,-d*.70f),.14f,.35f);
                Flag(new Vector3(tower.x,upper+.91f,tower.z));
            }
            void HighlandHouse()
            {
                float h=1.59f+age*.055f;
                Hall(new Vector3(-w*.18f,0,d*.10f),w*.66f,d*.76f,h,age>=3);
                Hall(new Vector3(w*.63f,0,d*.28f),w*.22f,d*.46f,1.07f+age*.025f,false);
                Chimney(new Vector3(-w*.49f,h+.37f,d*.27f),.36f,.86f);
                Steps(new Vector3(-w*.18f,.23f,-d*.78f),.67f,.31f);
                Barrel(new Vector3(w*.62f,.23f,-d*.62f),.16f,.38f);
                for(int i=0;i<4;i++)detail.Box(new Vector3(w*.59f,.27f+i*.085f,-d*.30f),new Vector3(.38f,.065f,.20f),wood);
                Flag(new Vector3(-w*.02f,h+.61f,d*.10f));
            }
            void Barracks()
            {
                Hall(new Vector3(0,0,d*.35f),w*.88f,d*.56f,1.99f,age>=3);
                ground.Box(new Vector3(0,.24f,-d*.56f),new Vector3(w*1.80f,.035f,d*.66f),earth);
                for(int side=-1;side<=1;side+=2)
                {
                    float x=side*w*.68f;
                    foreach(float z in new[]{-d*.54f,-d*.84f})detail.Box(new Vector3(x,.65f,z),new Vector3(.055f,.81f,.055f),wood);
                    detail.Box(new Vector3(x,1.02f,-d*.69f),new Vector3(.59f,.075f,.14f),wood);
                    for(int i=-2;i<=2;i++){float px=x+i*.095f;detail.Beam(new Vector3(px,.25f,-d*.80f),new Vector3(px,1.24f,-d*.60f),.026f,.028f,wood);detail.Beam(new Vector3(px,1.24f,-d*.60f),new Vector3(px,1.40f,-d*.57f),.032f,.04f,iron);}
                }
                detail.Beam(new Vector3(0,.25f,-d*.75f),new Vector3(0,1.15f,-d*.75f),.065f,.065f,wood);
                detail.Cylinder(new Vector3(0,1.06f,-d*.79f),.23f,.055f,canvas,20,Quaternion.Euler(90,0,0));
                detail.Cylinder(new Vector3(0,1.06f,-d*.827f),.12f,.015f,team,20,Quaternion.Euler(90,0,0));
                Flag(new Vector3(w*.77f,2.65f,d*.45f));
            }
            void Stable()
            {
                // Open bays are actual empty geometry, not dark paint on a cube.
                float h=1.95f,z=d*.23f;
                wall.Box(new Vector3(0,1.09f,z+d*.61f),new Vector3(w*1.8f,1.72f,.14f),facade);
                foreach(int s in new[]{-1,1})wall.Box(new Vector3(s*w*.88f,1.09f,z),new Vector3(.15f,1.72f,d*1.27f),facade);
                for(int i=-1;i<=1;i++)
                {
                    float x=i*w*.64f;Column(new Vector3(x,.22f,-d*.45f),h-.22f,.075f,wood);
                    detail.Box(new Vector3(x,.85f,d*.36f),new Vector3(.065f,1.2f,d*.66f),wood);
                    Barrel(new Vector3(x+.15f,.23f,d*.48f),.18f,.32f,canvas);
                }
                wall.Box(new Vector3(0,1.92f,-d*.45f),new Vector3(w*1.86f,.18f,.18f),wood);
                Roof(new Vector3(0,2.06f,z),w*.92f,d*.68f,.75f);
                Fence(-d*.84f,w*.88f);Trough(new Vector3(-w*.59f,.22f,-d*.74f),.70f,.24f);Flag(new Vector3(w*.69f,2.44f,d*.34f));
            }
            void Foundry()
            {
                Hall(new Vector3(0,0,d*.17f),w*.85f,d*.73f,1.9f,false);
                foreach(int s in new[]{-1,1})Chimney(new Vector3(s*w*.60f,2.04f,d*.37f),.37f,1.42f);
                var front=new Vector3(0,.22f,-d*.72f);Door(front,.86f,1.30f,0,true);
                detail.Box(front+new Vector3(0,.22f,-.055f),new Vector3(.57f,.26f,.028f),ArchitecturalMaterials.Get(S.Metal,new Color(.88f,.30f,.06f)));
                // A forged anvil has a modeled heel, waist and tapered horn.
                var p=new Vector3(w*.60f,.22f,-d*.74f);detail.Box(p+Vector3.up*.17f,new Vector3(.40f,.34f,.33f),wood);
                detail.Box(p+Vector3.up*.45f,new Vector3(.39f,.11f,.22f),iron);detail.Box(p+Vector3.up*.34f,new Vector3(.16f,.17f,.18f),iron);
                detail.Lathe(p+new Vector3(-.21f,.45f,0),new[]{new Vector2(.08f,0),new Vector2(.02f,.23f)},iron,12,Quaternion.Euler(0,0,90));
                Wheel(new Vector3(-w*.62f,.52f,-d*.80f),.28f);Barrel(new Vector3(-w*.72f,.22f,-d*.19f),.16f,.42f);
            }
            void Market()
            {
                Hall(new Vector3(0,0,d*.50f),w*.75f,d*.40f,1.72f,false);
                Awning(new Vector3(0,1.62f,-d*.29f),w*.89f,d*.59f);
                detail.Box(new Vector3(0,.75f,-d*.64f),new Vector3(w*1.72f,.14f,.44f),wood);
                for(int i=-2;i<=2;i++)
                {
                    var p=new Vector3(i*w*.33f,.88f,-d*.63f);Crate(p,.37f,.20f,.33f);
                    for(int n=0;n<5;n++)detail.Cylinder(p+new Vector3((n%3-1)*.095f,.12f,n/3*.09f-.05f),.055f,.085f,i%2==0?canvas:leaf,8);
                }
                Barrel(new Vector3(-w*.81f,.23f,d*.15f),.17f,.46f);
            }
            void Tower()
            {
                float radius=w*.61f,h=3.35f+age*.12f;
                wall.Box(new Vector3(0,.22f+h*.5f,0),new Vector3(radius*2,h,d*1.10f),highland||ottoman?stone:facade);
                for(int n=0;n<4;n++)Cornice(new Vector3(0,.34f+n*h*.27f,0),radius+.015f,d*.565f,.055f,stone);
                Door(new Vector3(0,.22f,-d*.56f),.60f,1.34f,0,ottoman);
                foreach(int side in new[]{-1,1})for(int row=0;row<2;row++)Window(new Vector3(side*(radius+.02f),1.74f+row*.9f,0),.18f,.43f,side*90,ottoman);
                Cornice(new Vector3(0,h+.30f,0),w*.84f,d*.80f,.22f,wood);
                for(int i=-2;i<=2;i++)foreach(int s in new[]{-1,1})
                {
                    detail.Box(new Vector3(i*w*.34f,h+.69f,s*d*.78f),new Vector3(.085f,.63f,.085f),wood);
                    detail.Box(new Vector3(s*w*.82f,h+.69f,i*d*.31f),new Vector3(.085f,.63f,.085f),wood);
                }
                Roof(new Vector3(0,h+1.02f,0),w*.88f,d*.84f,.76f,ottoman||wayfarer?1:0);
                Flag(new Vector3(w*.69f,h+1.23f,d*.41f));
            }
            void Farm()
            {
                ground.Box(new Vector3(0,.23f,0),new Vector3(w*1.91f,.035f,d*1.90f),earth);
                for(int row=-4;row<=4;row++)
                {
                    float z=row*d*.19f;ground.Box(new Vector3(0,.265f,z),new Vector3(w*1.84f,.035f,.08f),wood);
                    for(int x=-6;x<=6;x++)
                    {
                        float px=x*w*.14f,height=.34f+(x*x+row*row)%4*.035f;
                        detail.Beam(new Vector3(px,.28f,z),new Vector3(px+.026f,height+.28f,z),.014f,.018f,canvas);
                        detail.Lathe(new Vector3(px+.026f,height+.19f,z),new[]{new Vector2(.025f,0),new Vector2(.041f,.12f),new Vector2(0,.21f)},canvas,5);
                    }
                }
                Fence(d*.95f,w*.93f);detail.Beam(new Vector3(-w*.89f,.24f,-d*.72f),new Vector3(-w*.84f,.91f,-d*.68f),.05f,.05f,wood);
                Barrel(new Vector3(w*.80f,.25f,-d*.82f),.15f,.31f,canvas);
            }
            void Bank()
            {
                Hall(new Vector3(0,0,d*.12f),w*.83f,d*.77f,2.53f,true);
                foreach(int s in new[]{-1,1})Column(new Vector3(s*.48f,.23f,-d*.75f),1.57f,.095f,trim);
                Pediment(new Vector3(0,1.87f,-d*.81f),.69f,.33f,.15f);
                Door(new Vector3(0,.24f,-d*.70f),.65f,1.38f,0,true);
                var seal=new ArchitecturalGeometry();seal.Cylinder(new Vector3(0,2.31f,-d*.68f),.18f,.045f,brass,24,Quaternion.Euler(90,0,0));Feature(seal,"Bank gilded merchant seal",3);
                Steps(new Vector3(0,.23f,-d*.85f),1.34f,.30f);
            }
            void Estate()
            {
                Hall(new Vector3(0,0,d*.42f),w*.82f,d*.47f,2.15f,true);
                for(int r=0;r<3;r++)for(int i=-4;i<=4;i++)
                {
                    var p=new Vector3(i*w*.19f,.23f,-d*.37f-r*.23f);detail.Beam(p,p+new Vector3(0,.43f,0),.022f,.025f,wood);
                    detail.Lathe(p+Vector3.up*.12f,new[]{new Vector2(.07f,0),new Vector2(.11f,.18f),new Vector2(.01f,.33f)},leaf,7);
                }
                Fence(-d*.93f,w*.88f);Barrel(new Vector3(w*.83f,.23f,d*.09f),.15f,.38f);
            }
            void TradePost()
            {
                Hall(new Vector3(0,0,d*.39f),w*.85f,d*.48f,1.98f,false);
                ground.Box(new Vector3(0,.33f,-d*.49f),new Vector3(w*1.82f,.19f,d*.75f),wood);
                Awning(new Vector3(0,1.85f,-d*.39f),w*.86f,d*.46f);
                for(int i=-1;i<=1;i++)Crate(new Vector3(i*w*.52f,.42f,-d*.65f),.52f,.48f,.48f);
                Barrel(new Vector3(w*.72f,.42f,-d*.07f),.19f,.55f);Flag(new Vector3(-w*.75f,2.65f,d*.40f));
            }
            void Waystation(){Courtyard(true);}
            void Courtyard(bool caravan)
            {
                Hall(new Vector3(0,0,d*.63f),w*.89f,d*.25f,1.93f,false,false);
                foreach(int s in new[]{-1,1})Hall(new Vector3(s*w*.73f,0,d*.02f),w*.17f,d*.40f,1.62f,false,false);
                var court=new ArchitecturalGeometry();court.Box(new Vector3(0,.25f,-d*.22f),new Vector3(w*1.07f,.04f,d*1.35f),earth);Feature(court,"Open caravan courtyard",0);
                Arcade(new Vector3(0,.24f,-d*.68f),w*1.64f,.40f,1.20f,3);
                Awning(new Vector3(-w*.52f,1.59f,d*.0f),w*.29f,d*.47f);
                for(int i=0;i<3;i++){Crate(new Vector3(w*.53f,.24f,-d*.23f+i*.42f),.39f,.30f,.33f);Barrel(new Vector3(-w*.52f,.24f,d*.40f-i*.38f),.14f,.42f);}
                Flag(new Vector3(w*.71f,2.35f,d*.52f));
            }
            void Workshop()
            {
                Hall(new Vector3(-w*.24f,0,d*.24f),w*.64f,d*.65f,2.06f,false);
                Chimney(new Vector3(-w*.53f,2.05f,d*.51f),.37f,1.10f);
                var crane=new ArchitecturalGeometry();var p=new Vector3(w*.66f,.24f,-d*.12f);
                crane.Beam(p,p+Vector3.up*2.30f,.18f,.18f,wood);crane.Beam(p+Vector3.up*2.26f,p+new Vector3(-.74f,2.26f,0),.14f,.16f,wood);
                crane.Beam(p+Vector3.up*1.48f,p+new Vector3(-.68f,2.25f,0),.11f,.12f,wood);
                crane.Cylinder(p+new Vector3(-.67f,2.28f,0),.12f,.055f,iron,16,Quaternion.Euler(90,0,0));
                crane.Beam(p+new Vector3(-.68f,2.23f,0),p+new Vector3(-.68f,.86f,0),.022f,.022f,canvas);
                crane.Lathe(p+new Vector3(-.68f,.74f,0),new[]{new Vector2(.09f,0),new Vector2(.09f,.10f)},iron,12);Feature(crane,"Workshop loading crane",1);
                for(int n=0;n<4;n++)detail.Box(new Vector3(w*.56f,.28f+n*.13f,-d*.63f),new Vector3(.78f,.105f,.30f),wood);
                Wheel(new Vector3(-w*.58f,.60f,-d*.62f),.32f);Flag(new Vector3(-w*.74f,2.66f,d*.48f));
            }
            void Mosque()
            {
                Hall(new Vector3(-w*.15f,0,d*.20f),w*.67f,d*.62f,1.94f,false,false);
                Dome(new Vector3(-w*.15f,2.28f,d*.20f),w*.68f,.81f,"Mosque central dome",ArchitecturalMaterials.Get(S.Metal,new Color(.31f,.44f,.45f)));
                Arcade(new Vector3(-w*.12f,.24f,-d*.58f),w*1.35f,.44f,1.07f,3);
                var minaret=new ArchitecturalGeometry();var p=new Vector3(w*.74f,.23f,d*.48f);
                minaret.Box(p+Vector3.up*.36f,new Vector3(.40f,.72f,.40f),stone);
                minaret.Lathe(p,new[]{new Vector2(.17f,.54f),new Vector2(.14f,2.68f),new Vector2(.27f,2.77f),new Vector2(.27f,2.85f),new Vector2(.13f,2.90f),new Vector2(.12f,3.52f)},trim,20);Feature(minaret,"Carved minaret and balcony",1);
                var cap=new ArchitecturalGeometry();cap.Lathe(p,new[]{new Vector2(.16f,3.50f),new Vector2(.13f,3.59f),new Vector2(.0f,4.02f)},roofMat,20);Feature(cap,"Pointed minaret cap",2);
                Finial(p+Vector3.up*4.04f);Finial(new Vector3(-w*.15f,3.16f,d*.20f));
                Flag(new Vector3(-w*.74f,2.55f,d*.58f));
            }

            Transform Feature(ArchitecturalGeometry geometry,string name,int phase)
            {
                var result=geometry.Create(root,name);
                (phase==0?groundFeatures:phase==1?wallFeatures:phase==2?roofFeatures:detailFeatures).Add(result);return result;
            }
            void Hall(Vector3 p,float halfWidth,float halfDepth,float height,bool grand=false,bool pitched=true)
            {
                float bottom=.23f,top=bottom+height;
                wall.Box(p+new Vector3(0,bottom+height*.5f,0),new Vector3(halfWidth*2,height,halfDepth*2),facade);
                Cornice(p+Vector3.up*(bottom+.12f),halfWidth+.025f,halfDepth+.025f,.18f,stone);
                Cornice(p+Vector3.up*top,halfWidth+.05f,halfDepth+.05f,.10f,trim);
                if(age>=2)Cornice(p+Vector3.up*(top-.23f),halfWidth+.023f,halfDepth+.023f,.065f,trim);
                if(age>=3)
                {
                    int count=Mathf.Max(3,Mathf.FloorToInt(height/.28f));
                    foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})for(int n=0;n<count;n++)
                        detail.Box(p+new Vector3(x*(halfWidth-.035f),bottom+.17f+n*(height-.2f)/count,z*(halfDepth-.035f)),new Vector3(n%2==0?.20f:.15f,.16f,n%2==0?.15f:.20f),trim);
                }
                bool tiny=halfWidth<.48f||halfDepth<.35f;
                if(!tiny)
                {
                    float doorHeight=Mathf.Min(1.35f,height*.72f),doorWidth=Mathf.Min(.61f,halfWidth*.63f);
                    Door(p+new Vector3(0,bottom,-halfDepth-.012f),doorWidth,doorHeight,0,ottoman||wayfarer);
                    float opening=Mathf.Min(.40f,halfWidth*.38f),windowHeight=Mathf.Min(.62f,height*.32f),windowY=bottom+height*.61f;
                    foreach(int side in new[]{-1,1})foreach(int i in new[]{-1,1})
                        Window(p+new Vector3(i*halfWidth*.66f,windowY,side*(halfDepth+.013f)),opening,windowHeight,side==1?180:0,ottoman);
                    if(age>=2)foreach(int side in new[]{-1,1})
                        Window(p+new Vector3(side*(halfWidth+.012f),windowY,0),opening*.85f,windowHeight,side==1?-90:90,ottoman);
                    if(age>=4&&height>2)
                    {
                        foreach(int i in new[]{-1,1})Window(p+new Vector3(i*halfWidth*.65f,bottom+height*.86f,-halfDepth-.015f),opening*.73f,windowHeight*.49f,0,false);
                        detail.Box(p+new Vector3(0,bottom+height*.47f,-halfDepth-.036f),new Vector3(halfWidth*1.80f,.05f,.06f),trim);
                    }
                    if(age==5)
                    {
                        Pediment(p+new Vector3(0,bottom+doorHeight+.10f,-halfDepth-.09f),doorWidth*.72f,.27f,.08f);
                        foreach(int i in new[]{-1,1})detail.Cylinder(p+new Vector3(i*halfWidth*.84f,top+.08f,-halfDepth+.11f),.058f,.24f,brass,10);
                    }
                    if(highland||age==1)
                    {
                        foreach(int side in new[]{-1,1})wall.Box(p+new Vector3(side*(halfWidth-.04f),bottom+height*.5f,-halfDepth-.02f),new Vector3(.095f,height,.08f),wood);
                        if(highland)foreach(int side in new[]{-1,1})detail.Beam(p+new Vector3(side*halfWidth*.35f,bottom+.2f,-halfDepth-.056f),p+new Vector3(side*halfWidth*.82f,top-.15f,-halfDepth-.056f),.065f,.055f,wood);
                    }
                }
                float pitch=highland?.60f:ottoman?.45f:wayfarer?.12f:dutch?.95f:french?.94f:.78f;
                float rise=Mathf.Clamp(halfWidth*pitch,.24f,1.55f);
                Roof(p+Vector3.up*(top+.045f),halfWidth+.065f,halfDepth+.065f,rise,!pitched?2:0);
                if(grand&&age>=3&&!wayfarer&&!ottoman&&!tiny)
                {
                    float dormerWidth=Mathf.Min(.27f,halfWidth*.23f),dormerY=top+rise*.55f;
                    foreach(int side in new[]{-1,1})
                    {
                        var point=p+new Vector3(side*halfWidth*.47f,dormerY,-halfDepth*.46f);
                        roof.Box(point+Vector3.up*.17f,new Vector3(dormerWidth*2,.36f,.39f),facade);
                        GableRoof(point+Vector3.up*.37f,dormerWidth+.045f,.25f,.24f,roofMat);
                        Window(point+new Vector3(0,.17f,-.21f),dormerWidth*1.18f,.23f,0,false);
                    }
                }
                if(age>=2&&!tiny&&!ottoman&&!wayfarer&&entity.def.id!="foundry"&&entity.def.id!="workshop")
                    Chimney(p+new Vector3(halfWidth*.56f,top+rise*.42f,halfDepth*.45f),.20f,age>=4?.88f:.58f);
                if(age>=4&&wayfarer)
                {
                    var beamY=top+.23f;
                    for(int i=-2;i<=2;i++)detail.Box(p+new Vector3(i*halfWidth*.39f,beamY,halfDepth*.80f),new Vector3(.065f,.065f,halfDepth*.25f),wood);
                }
                highest=Mathf.Max(highest,p.y+top+rise+.8f);
            }
            void Cornice(Vector3 p,float halfWidth,float halfDepth,float thickness,Material material)
            {
                wall.Box(p+new Vector3(0,0,-halfDepth),new Vector3(halfWidth*2,thickness,.075f),material);
                wall.Box(p+new Vector3(0,0,halfDepth),new Vector3(halfWidth*2,thickness,.075f),material);
                foreach(int side in new[]{-1,1})wall.Box(p+new Vector3(side*halfWidth,0,0),new Vector3(.075f,thickness,halfDepth*2),material);
            }
            void Roof(Vector3 p,float halfWidth,float halfDepth,float height,int mode=0)
            {
                highest=Mathf.Max(highest,p.y+height+.2f);
                roof.Box(p,new Vector3(halfWidth*2,.09f,halfDepth*2),wood);
                if(mode==2||wayfarer)
                {
                    roof.Box(p+Vector3.up*.065f,new Vector3(halfWidth*1.96f,.13f,halfDepth*1.96f),roofMat);
                    float parapet=.18f+age*.035f;
                    foreach(int side in new[]{-1,1})
                    {
                        roof.Box(p+new Vector3(side*(halfWidth-.05f),parapet*.5f+.12f,0),new Vector3(.10f,parapet,halfDepth*2),facade);
                        roof.Box(p+new Vector3(0,parapet*.5f+.12f,side*(halfDepth-.05f)),new Vector3(halfWidth*2,parapet,.10f),facade);
                    }
                    if(age>=3)for(int i=-1;i<=1;i++)roof.Box(p+new Vector3(i*halfWidth*.62f,parapet+.19f,halfDepth-.07f),new Vector3(.16f,.14f,.14f),trim);
                    return;
                }
                if(mode==1||ottoman)
                {HipRoof(p,halfWidth,halfDepth,height,.18f,roofMat);return;}
                if(french)
                {
                    RoofFrustum(p,halfWidth,halfDepth,halfWidth*.65f,halfDepth*.68f,height*.73f,roofMat);
                    HipRoof(p+Vector3.up*(height*.73f),halfWidth*.65f,halfDepth*.68f,height*.27f,.18f,roofMat);
                    foreach(int s in new[]{-1,1})roof.Box(p+new Vector3(s*halfWidth*.65f,height*.73f,0),new Vector3(.043f,.045f,halfDepth*1.36f),trim);
                }
                else
                {
                    GableRoof(p,halfWidth,halfDepth,height,roofMat);
                    if(dutch)
                    {
                        int steps=age>=4?6:5;float step=height/steps;
                        foreach(int side in new[]{-1,1})for(int n=0;n<steps;n++)
                        {
                            float width=halfWidth*2*(1-n/(float)steps);
                            var point=p+new Vector3(0,(n+.5f)*step,side*(halfDepth-.035f));
                            roof.Box(point,new Vector3(width,step+.025f,.10f),facade);
                            roof.Box(point+Vector3.up*(step*.5f),new Vector3(width+.035f,.045f,.15f),trim);
                        }
                    }
                    else if(highland)
                    {
                        foreach(int side in new[]{-1,1})roof.Box(p+new Vector3(side*(halfWidth-.035f),.07f,0),new Vector3(.11f,.14f,halfDepth*2),stone);
                        roof.Box(p+Vector3.up*(height+.035f),new Vector3(.11f,.10f,halfDepth*2.01f),stone);
                    }
                    else
                    {
                        // The British bargeboards stand proud of the roof edge.
                        foreach(int side in new[]{-1,1})foreach(int x in new[]{-1,1})
                            roof.Beam(p+new Vector3(x*halfWidth,0,side*(halfDepth-.015f)),p+new Vector3(0,height,side*(halfDepth-.015f)),.065f,.065f,trim);
                    }
                }
                if(age==5)Finial(p+Vector3.up*(height+.05f));
            }
            void GableRoof(Vector3 p,float hw,float hd,float h,Material material)
            {
                Vector3 P(float x,float y,float z)=>p+new Vector3(x,y,z);
                roof.Quad(P(-hw,0,-hd),P(-hw,0,hd),P(0,h,hd),P(0,h,-hd),material);
                roof.Quad(P(0,h,-hd),P(0,h,hd),P(hw,0,hd),P(hw,0,-hd),material);
                roof.Triangle(P(-hw,0,-hd),P(0,h,-hd),P(hw,0,-hd),facade);
                roof.Triangle(P(hw,0,hd),P(0,h,hd),P(-hw,0,hd),facade);
                roof.Box(p+Vector3.up*(h+.025f),new Vector3(.075f,.065f,hd*2),material);
            }
            void RoofFrustum(Vector3 p,float hw,float hd,float tw,float td,float h,Material material)
            {
                Vector3 P(float x,float y,float z)=>p+new Vector3(x,y,z);
                roof.Quad(P(-hw,0,-hd),P(-tw,h,-td),P(tw,h,-td),P(hw,0,-hd),material);
                roof.Quad(P(hw,0,hd),P(tw,h,td),P(-tw,h,td),P(-hw,0,hd),material);
                roof.Quad(P(-hw,0,hd),P(-tw,h,td),P(-tw,h,-td),P(-hw,0,-hd),material);
                roof.Quad(P(hw,0,-hd),P(tw,h,-td),P(tw,h,td),P(hw,0,hd),material);
                roof.Quad(P(-tw,h,-td),P(-tw,h,td),P(tw,h,td),P(tw,h,-td),material);
            }
            void HipRoof(Vector3 p,float hw,float hd,float h,float ridge,Material material)
            {RoofFrustum(p,hw,hd,Mathf.Min(hw*.34f,ridge),hd*.46f,h,material);roof.Box(p+Vector3.up*(h+.025f),new Vector3(Mathf.Min(hw*.68f,ridge*2)+.04f,.065f,hd*.94f),material);}
            void Window(Vector3 p,float width,float height,float yaw,bool arched)
            {
                var rotation=Quaternion.Euler(0,yaw,0);Vector3 P(float x,float y,float z)=>p+rotation*new Vector3(x,y,z);
                void B(float x,float y,float z,float sx,float sy,float sz,Material mat)=>detail.Box(P(x,y,z),new Vector3(sx,sy,sz),mat,rotation);
                B(0,0,0,width+.09f,height+.10f,.075f,dark);B(0,0,-.048f,width,height,.025f,glass);
                foreach(int side in new[]{-1,1})B(side*(width*.5f+.025f),0,-.066f,.052f,height+.11f,.055f,trim);
                B(0,-height*.5f-.045f,-.087f,width+.17f,.07f,.13f,trim);B(0,height*.5f+.025f,-.068f,width+.14f,.065f,.06f,trim);
                B(0,0,-.082f,.025f,height,.023f,wood);B(0,0,-.085f,width,.025f,.023f,wood);
                if(arched&&Mathf.Abs(yaw)<.01f)detail.Arch(p+new Vector3(0,height*.5f-.08f,-.06f),width,0,height*.24f,.065f,.08f,trim,10);
                if(age>=2&&!arched)
                {
                    foreach(int side in new[]{-1,1})
                    {
                        B(side*(width*.73f+.07f),0,-.037f,width*.32f,height,.045f,team);
                        for(int n=-1;n<=1;n++)B(side*(width*.73f+.07f),n*height*.23f,-.064f,width*.29f,.022f,.02f,wood);
                    }
                }
            }
            void Door(Vector3 p,float width,float height,float yaw,bool arch)
            {
                var rotation=Quaternion.Euler(0,yaw,0);Vector3 P(float x,float y,float z)=>p+rotation*new Vector3(x,y,z);
                void B(float x,float y,float z,float sx,float sy,float sz,Material mat)=>detail.Box(P(x,y,z),new Vector3(sx,sy,sz),mat,rotation);
                B(0,height*.5f,0,width+.12f,height+.07f,.10f,dark);B(0,height*.5f,-.055f,width,height,.065f,wood);
                int boards=5;for(int i=1;i<boards;i++)B(-width*.5f+i*width/boards,height*.5f,-.095f,.014f,height,.015f,dark);
                foreach(int side in new[]{-1,1})B(side*(width*.5f+.07f),height*.5f,-.025f,.11f,height+.08f,.14f,trim);
                B(0,height+.04f,-.035f,width+.25f,.13f,.16f,trim);B(0,.02f,-.11f,width+.20f,.055f,.26f,stone);
                foreach(float y in new[]{height*.25f,height*.72f})B(0,y,-.105f,width*.83f,.045f,.018f,iron);
                B(width*.27f,height*.48f,-.126f,.038f,.085f,.025f,brass);
                if(arch&&Mathf.Abs(yaw)<.01f)detail.Arch(p+new Vector3(0,height-.08f,-.018f),width+.10f,0,width*.30f,.105f,.18f,trim,12);
                if(age>=4)B(0,height+.18f,-.03f,width*.53f,.10f,.05f,team);
            }
            void Chimney(Vector3 p,float width,float height)
            {
                roof.Box(p+Vector3.up*(height*.5f),new Vector3(width,height,width),facade);
                roof.Box(p+Vector3.up*(height-.02f),new Vector3(width+.09f,.09f,width+.09f),trim);
                roof.Box(p+Vector3.up*(height+.036f),new Vector3(width*.68f,.026f,width*.68f),dark);
                if(age>=4)
                {roof.Cylinder(p+new Vector3(-width*.20f,height+.11f,0),width*.13f,.16f,iron,12);roof.Cylinder(p+new Vector3(width*.20f,height+.11f,0),width*.13f,.16f,iron,12);}
                highest=Mathf.Max(highest,p.y+height+.20f);
            }
            void Steps(Vector3 p,float width,float depth)
            {
                for(int n=0;n<3;n++)ground.Box(p+new Vector3(0,-.13f+n*.042f,-depth*(1-n/3f)*.43f),new Vector3(width*(1-n*.08f),.06f,depth*(1-n/3f)),stone);
            }
            void Column(Vector3 p,float height,float radius,Material material)
            {
                wall.Cylinder(p+Vector3.up*(height*.5f),radius,height,material,12);
                wall.Box(p+Vector3.up*.045f,new Vector3(radius*2.9f,.09f,radius*2.9f),trim);
                wall.Box(p+Vector3.up*(height-.04f),new Vector3(radius*2.7f,.09f,radius*2.7f),trim);
            }
            void Porch(Vector3 p,float halfWidth,float depth,float height)
            {
                ground.Box(p+new Vector3(0,.012f,-depth*.18f),new Vector3(halfWidth*2,.07f,depth),stone);
                foreach(int side in new[]{-1,1})Column(p+new Vector3(side*halfWidth*.78f,.035f,-depth*.28f),height,.07f,trim);
                roof.Box(p+new Vector3(0,height+.10f,-depth*.13f),new Vector3(halfWidth*2.04f,.14f,depth*1.12f),trim);
                if(french||age>=3)Pediment(p+new Vector3(0,height+.16f,-depth*.70f),halfWidth,.38f,.09f);
                else GableRoof(p+new Vector3(0,height+.18f,-depth*.13f),halfWidth*1.03f,depth*.56f,.33f,roofMat);
            }
            void Arcade(Vector3 p,float width,float depth,float height,int bays)
            {
                float span=width/bays;ground.Box(p+new Vector3(0,.013f,0),new Vector3(width+.08f,.06f,depth),stone);
                for(int n=0;n<bays;n++)
                {
                    float x=-width*.5f+span*(n+.5f);
                    wall.Arch(p+new Vector3(x,0,-depth*.40f),span-.11f,height*.60f,height*.32f,.065f,.15f,trim,12);
                }
                roof.Box(p+new Vector3(0,height+.055f,0),new Vector3(width+.15f,.15f,depth*1.09f),facade);
                roof.Box(p+new Vector3(0,height+.15f,0),new Vector3(width+.17f,.055f,depth*1.11f),trim);
                if(age>=4)for(int n=0;n<=bays;n++)roof.Box(p+new Vector3(-width*.5f+n*span,height+.28f,-depth*.37f),new Vector3(.13f,.21f,.13f),trim);
            }
            void Dome(Vector3 p,float radius,float height,string name,Material material)
            {
                var dome=new ArchitecturalGeometry();var profile=new List<Vector2>{new Vector2(radius*.92f,-.10f),new Vector2(radius,-.055f),new Vector2(radius,0)};
                const int rings=10;
                for(int i=1;i<=rings;i++){float angle=i*Mathf.PI*.5f/rings;profile.Add(new Vector2(i==rings?0:Mathf.Cos(angle)*radius,Mathf.Sin(angle)*height));}
                dome.Lathe(p,profile.ToArray(),material,32);Feature(dome,name,2);
                roof.Lathe(p,new[]{new Vector2(radius*.99f,-.13f),new Vector2(radius*1.03f,-.10f),new Vector2(radius*1.03f,-.035f),new Vector2(radius*.99f,0)},trim,32);
                highest=Mathf.Max(highest,p.y+height+.3f);
            }
            void Finial(Vector3 p)
            {
                roof.Lathe(p,new[]{new Vector2(.075f,0),new Vector2(.067f,.075f),new Vector2(.025f,.13f),new Vector2(.052f,.21f),new Vector2(0,.32f)},brass,12);
                highest=Mathf.Max(highest,p.y+.32f);
            }
            void Pediment(Vector3 p,float halfWidth,float height,float depth)
            {
                roof.Triangle(p+new Vector3(-halfWidth,0,-depth*.5f),p+new Vector3(0,height,-depth*.5f),p+new Vector3(halfWidth,0,-depth*.5f),trim);
                roof.Triangle(p+new Vector3(halfWidth,0,depth*.5f),p+new Vector3(0,height,depth*.5f),p+new Vector3(-halfWidth,0,depth*.5f),trim);
                foreach(int side in new[]{-1,1})roof.Beam(p+new Vector3(side*halfWidth,0,0),p+Vector3.up*height,.065f,depth+.04f,trim);
                roof.Box(p,new Vector3(halfWidth*2.10f,.07f,depth+.055f),trim);
            }
            void Flag(Vector3 p)
            {
                float width=Mathf.Min(.45f,entity.def.width*.15f),height=.30f;
                // Place the cloth toward the center when a flag is near the edge.
                float direction=p.x>0?-1:1;detail.Cylinder(p+Vector3.up*.36f,.022f,.86f,wood,10);
                Vector3 a=p+new Vector3(0,.71f,0),b=p+new Vector3(direction*width*.53f,.67f,.055f),c=p+new Vector3(direction*width,.70f,.005f);
                detail.Quad(a,a-Vector3.up*height,b-Vector3.up*height,b,team);detail.Quad(b,b-Vector3.up*height,c-Vector3.up*height,c,team);
                detail.Quad(b,b-Vector3.up*height,a-Vector3.up*height,a,team);detail.Quad(c,c-Vector3.up*height,b-Vector3.up*height,b,team);
                detail.Cylinder(p+Vector3.up*.81f,.047f,.055f,brass,10);highest=Mathf.Max(highest,p.y+.86f);
            }
            void Barrel(Vector3 p,float radius,float height,Material material=null)
            {
                var body=material??wood;
                detail.Lathe(p,new[]{new Vector2(0,0),new Vector2(radius*.87f,0),new Vector2(radius,height*.24f),new Vector2(radius,height*.70f),new Vector2(radius*.85f,height),new Vector2(0,height)},body,16);
                foreach(float y in new[]{height*.16f,height*.78f})detail.Lathe(p,new[]{new Vector2(radius*.985f,y),new Vector2(radius*1.04f,y+.013f),new Vector2(radius*1.04f,y+.045f),new Vector2(radius*.985f,y+.056f)},iron,16);
                detail.Cylinder(p+new Vector3(0,height+.012f,0),radius*.12f,.018f,wood,10);
            }
            void Crate(Vector3 p,float width,float height,float depth)
            {
                detail.Box(p+Vector3.up*(height*.5f),new Vector3(width,height,depth),wood);
                foreach(int side in new[]{-1,1})foreach(float y in new[]{.025f,height-.025f})detail.Box(p+new Vector3(0,y,side*(depth*.5f+.009f)),new Vector3(width,.055f,.025f),trim);
                foreach(int side in new[]{-1,1})detail.Beam(p+new Vector3(-width*.40f,.04f,side*(depth*.5f+.015f)),p+new Vector3(width*.40f,height-.04f,side*(depth*.5f+.015f)),.045f,.025f,wood);
            }
            void FlowerBox(Vector3 p)
            {
                detail.Box(p,new Vector3(.37f,.13f,.19f),wood);detail.Box(p+Vector3.up*.073f,new Vector3(.32f,.022f,.14f),earth);
                for(int i=-1;i<=1;i++){var q=p+new Vector3(i*.10f,.10f,0);detail.Lathe(q,new[]{new Vector2(.025f,0),new Vector2(.055f,.065f),new Vector2(0,.16f)},leaf,6);}
            }
            void Fence(float z,float halfWidth)
            {
                const int posts=6;
                for(int i=0;i<posts;i++)detail.Box(new Vector3(Mathf.Lerp(-halfWidth,halfWidth,i/(float)(posts-1)),.57f,z),new Vector3(.06f,.66f,.06f),wood);
                foreach(float y in new[]{.47f,.74f})detail.Box(new Vector3(0,y,z),new Vector3(halfWidth*2,.055f,.035f),wood);
            }
            void Trough(Vector3 p,float width,float depth)
            {
                detail.Box(p+Vector3.up*.075f,new Vector3(width,.15f,depth),wood);detail.Box(p+Vector3.up*.159f,new Vector3(width-.09f,.022f,depth-.07f),glass);
                foreach(int side in new[]{-1,1})detail.Box(p+new Vector3(0,.20f,side*(depth*.5f-.02f)),new Vector3(width,.10f,.04f),wood);
            }
            void Wheel(Vector3 p,float radius)
            {
                var q=Quaternion.Euler(90,0,0);
                detail.Lathe(p,new[]{new Vector2(radius*.84f,-.045f),new Vector2(radius,-.045f),new Vector2(radius,.045f),new Vector2(radius*.84f,.045f),new Vector2(radius*.84f,-.045f)},iron,24,q);
                detail.Cylinder(p,.06f,.14f,wood,12,q);
                for(int i=0;i<8;i++){float angle=i*Mathf.PI*.25f;detail.Beam(p,p+new Vector3(Mathf.Cos(angle)*radius*.88f,Mathf.Sin(angle)*radius*.88f,0),.035f,.045f,wood);}
            }
            void Awning(Vector3 p,float halfWidth,float halfDepth)
            {
                foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
                {
                    var foot=new Vector3(p.x+x*halfWidth*.92f,.23f,p.z+z*halfDepth*.85f);
                    wall.Beam(foot,new Vector3(foot.x,p.y+(z==1?.09f:-.09f),foot.z),.055f,.055f,wood);
                }
                const int strips=8;
                for(int i=0;i<strips;i++)
                {
                    float a=Mathf.Lerp(-halfWidth,halfWidth,i/(float)strips),b=Mathf.Lerp(-halfWidth,halfWidth,(i+1)/(float)strips);
                    Vector3 aa=p+new Vector3(a,-.09f,-halfDepth),bb=p+new Vector3(a,.09f,halfDepth),cc=p+new Vector3(b,.09f,halfDepth),dd=p+new Vector3(b,-.09f,-halfDepth);
                    var cloth=i%3==0?team:canvas;roof.Quad(aa,bb,cc,dd,cloth);roof.Quad(dd,cc,bb,aa,cloth);
                    roof.Box(p+new Vector3((a+b)*.5f,-.16f,-halfDepth),new Vector3(b-a,.15f,.025f),cloth);
                }
                wall.Beam(p+new Vector3(-halfWidth,-.07f,-halfDepth),p+new Vector3(halfWidth,-.07f,-halfDepth),.045f,.045f,wood);
            }
            Transform Scaffolding()
            {
                var scaffold=new ArchitecturalGeometry();float h=Mathf.Max(1,highest*.64f),sx=w*.94f,sz=d*.94f;
                foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})scaffold.Beam(new Vector3(x*sx,.22f,z*sz),new Vector3(x*sx,h,z*sz),.065f,.065f,wood);
                foreach(int side in new[]{-1,1})
                {
                    foreach(float y in new[]{h*.45f,h*.78f})
                    {scaffold.Beam(new Vector3(-sx,y,side*sz),new Vector3(sx,y,side*sz),.07f,.08f,wood);scaffold.Beam(new Vector3(side*sx,y,-sz),new Vector3(side*sx,y,sz),.07f,.08f,wood);}
                    scaffold.Beam(new Vector3(-sx,.28f,side*sz),new Vector3(sx,h*.78f,side*sz),.045f,.055f,wood);
                }
                // A narrow real ladder, kept inside the foundation envelope.
                float lx=sx*.65f,lz=-sz*.94f;
                foreach(int side in new[]{-1,1})scaffold.Beam(new Vector3(lx+side*.13f,.22f,lz),new Vector3(lx+side*.13f,h*.80f,lz+.16f),.04f,.04f,wood);
                for(int i=0;i<8;i++){float t=i/7f;scaffold.Beam(new Vector3(lx-.13f,Mathf.Lerp(.30f,h*.76f,t),lz+t*.16f),new Vector3(lx+.13f,Mathf.Lerp(.30f,h*.76f,t),lz+t*.16f),.035f,.04f,wood);}
                return scaffold.Create(root,"Construction scaffold and access ladder");
            }
        }
    }
}
