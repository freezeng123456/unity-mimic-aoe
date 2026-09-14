// Frozen original project 0.2.2 fixture for visual comparison; not a runtime asset.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier.Tests
{
    // Original development models, assembled from authored silhouettes; no third-party game assets.
    public static class BaselineArt022
    {
        private static readonly Dictionary<Color,Material> Materials=new Dictionary<Color,Material>();
        public static readonly Color Blue=new Color(.13f,.42f,.64f), Red=new Color(.68f,.22f,.15f);
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
            var go=new GameObject(name);go.transform.SetParent(parent,false);var m=new Mesh{name=name};m.vertices=vertices;m.triangles=triangles;m.RecalculateNormals();go.AddComponent<MeshFilter>().sharedMesh=m;go.AddComponent<MeshRenderer>().sharedMaterial=Mat(color);return go;
        }
        public static void Roof(Transform t,float w,float d,float y,Color c)
        {
            MeshPart(t,"Gabled roof",new[]{new Vector3(-w,y,-d),new Vector3(w,y,-d),new Vector3(0,y+w*.7f,-d),new Vector3(-w,y,d),new Vector3(w,y,d),new Vector3(0,y+w*.7f,d)},new[]{0,2,1,3,4,5,0,3,5,0,5,2,1,2,5,1,5,4,0,1,4,0,4,3},c);
        }
        public static Transform BuildEntity(EntityState e,Transform parent)
        {
            var root=new GameObject(e.def.name).transform;root.SetParent(parent,false);
            Color team=e.owner==0?Blue:e.owner==1?Red:new Color(.4f,.43f,.24f);
            if(e.def.kind==EntityKind.Resource){Resource(root,e);return root;}
            if(e.def.kind==EntityKind.Building){Building(root,e,team);return root;}
            bool horse=(e.def.tags&Tags.Cavalry)!=0;
            bool gun=(e.def.tags&Tags.Artillery)!=0;
            Color wood=new Color(.24f,.15f,.085f), iron=new Color(.19f,.22f,.22f);
            if(gun)
            {
                Part(root,"Carriage",PrimitiveType.Cube,new Vector3(0,.5f,0),new Vector3(1.5f,.35f,2),wood);
                var barrel=Part(root,"Barrel",PrimitiveType.Cylinder,new Vector3(0,1.1f,.4f),new Vector3(.46f,1.1f,.46f),iron);barrel.transform.localRotation=Quaternion.Euler(78,0,0);
                foreach(float x in new[]{-.85f,.85f}){var wh=Part(root,"Wheel",PrimitiveType.Cylinder,new Vector3(x,.6f,0),new Vector3(1.1f,.12f,1.1f),wood);wh.transform.localRotation=Quaternion.Euler(0,0,90);}
                Part(root,"Team band",PrimitiveType.Cube,new Vector3(0,.72f,-.5f),new Vector3(1.4f,.13f,.35f),team);
                return root;
            }
            float riderY=horse?1.15f:0;
            if(horse)
            {
                Part(root,"Horse body",PrimitiveType.Capsule,new Vector3(0,.9f,0),new Vector3(.7f,.7f,1.4f),new Color(.37f,.23f,.13f)).transform.localRotation=Quaternion.Euler(90,0,0);
                Part(root,"Horse head",PrimitiveType.Cube,new Vector3(0,1.25f,.8f),new Vector3(.35f,.65f,.55f),wood);
                foreach(float x in new[]{-.28f,.28f})foreach(float z in new[]{-.45f,.45f})Part(root,"Horse leg",PrimitiveType.Cube,new Vector3(x,.35f,z),new Vector3(.15f,.7f,.15f),wood);
                Part(root,"Saddle",PrimitiveType.Cube,new Vector3(0,1.25f,0),new Vector3(.8f,.15f,.5f),team);
            }
            Color coat=e.Worker?new Color(.69f,.57f,.36f):team;
            if((e.def.tags&Tags.Explorer)!=0)coat=new Color(.33f,.39f,.25f);
            Part(root,"Coat",PrimitiveType.Cube,new Vector3(0,.95f+riderY,0),new Vector3(.55f,.7f,.38f),coat);
            Part(root,"Head",PrimitiveType.Sphere,new Vector3(0,1.53f+riderY,0),new Vector3(.35f,.4f,.35f),new Color(.77f,.59f,.4f));
            Part(root,"Hat",PrimitiveType.Cylinder,new Vector3(0,1.78f+riderY,0),new Vector3(.65f,.065f,.5f),e.Worker?new Color(.65f,.49f,.25f):iron);
            Part(root,"Hat crown",PrimitiveType.Cylinder,new Vector3(0,1.88f+riderY,0),new Vector3(.35f,.095f,.35f),e.Worker?new Color(.65f,.49f,.25f):iron);
            Part(root,"Sash",PrimitiveType.Cube,new Vector3(0,.93f+riderY,-.21f),new Vector3(.57f,.12f,.055f),team);
            foreach(float x in new[]{-.17f,.17f})
            {
                var leg=new GameObject(x<0?"Left leg":"Right leg").transform;leg.SetParent(root,false);leg.localPosition=new Vector3(x,.65f+riderY,0);
                Part(leg,"Boot",PrimitiveType.Cube,new Vector3(0,-.35f,0),new Vector3(.17f,.7f,.23f),wood);
                Part(root,"Arm",PrimitiveType.Cube,new Vector3(x*2,.97f+riderY,.1f),new Vector3(.16f,.6f,.18f),coat).transform.localRotation=Quaternion.Euler(-20,0,x<0?10:-10);
            }
            if(e.def.id=="pike")Part(root,"Pike",PrimitiveType.Cylinder,new Vector3(.42f,1.35f+riderY,.55f),new Vector3(.055f,1.5f,.055f),wood).transform.localRotation=Quaternion.Euler(20,0,0);
            else if(!e.Worker)Part(root,"Musket",PrimitiveType.Cube,new Vector3(.38f,1.08f+riderY,.55f),new Vector3(.12f,.12f,1.3f),iron);
            else Part(root,"Tool",PrimitiveType.Cube,new Vector3(.42f,.9f,.4f),new Vector3(.08f,.8f,.1f),wood).transform.localRotation=Quaternion.Euler(35,0,0);
            return root;
        }
        private static void Building(Transform root,EntityState e,Color team)
        {
            float w=e.def.width*.44f,d=e.def.depth*.44f;
            Color plaster=new Color(.69f,.64f,.48f),wood=new Color(.31f,.21f,.13f), roof=new Color(.38f,.24f,.16f);
            Part(root,"Foundation",PrimitiveType.Cube,new Vector3(0,.18f,0),new Vector3(w*2+.25f,.36f,d*2+.25f),new Color(.39f,.38f,.31f));
            if(e.def.id=="farm")
            {
                for(int i=0;i<7;i++)Part(root,"Crops",PrimitiveType.Cube,new Vector3(-w+i*w/3,.4f,0),new Vector3(.15f,.35f,d*1.8f),new Color(.6f,.55f,.22f));return;
            }
            float h=e.def.id=="town"?2.8f:e.def.id=="tower"?5.2f:1.9f;
            Part(root,"Walls",PrimitiveType.Cube,new Vector3(0,h*.5f+.25f,0),new Vector3(w*2,h,d*2),plaster);
            Roof(root,w*1.12f,d*1.12f,h+.25f,roof);
            for(int side=-1;side<=1;side+=2)
            {
                Part(root,"Door",PrimitiveType.Cube,new Vector3(0,.8f,d*side+.02f),new Vector3(.85f,1.2f,.09f),wood);
                for(int i=-1;i<=1;i+=2)Part(root,"Shutter",PrimitiveType.Cube,new Vector3(w*.62f*i,1.25f,d*side+.03f),new Vector3(.45f,.6f,.1f),team);
            }
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Part(root,"Timber",PrimitiveType.Cube,new Vector3(w*x,h*.5f+.2f,d*z),new Vector3(.18f,h+.2f,.18f),wood);
            Part(root,"Banner pole",PrimitiveType.Cylinder,new Vector3(w*.6f,h+1.8f,0),new Vector3(.075f,1.6f,.075f),wood);
            Part(root,"Banner",PrimitiveType.Cube,new Vector3(w*.6f+.55f,h+2.7f,0),new Vector3(1.1f,.65f,.055f),team);
            if(e.def.id=="town")
            {Part(root,"Clock tower",PrimitiveType.Cube,new Vector3(0,h+.75f,0),new Vector3(1.4f,1.8f,1.4f),plaster);Roof(root,.9f,.9f,h+1.7f,team);}
            if(e.def.id=="barracks")for(int i=0;i<3;i++)Part(root,"Pike rack",PrimitiveType.Cylinder,new Vector3(-w-.4f,1.2f,-.5f+i*.5f),new Vector3(.08f,1.2f,.08f),wood);
            if(e.def.id=="foundry")Part(root,"Chimney",PrimitiveType.Cube,new Vector3(-w*.55f,h+1f,-d*.5f),new Vector3(.9f,2,.9f),new Color(.34f,.3f,.25f));
            if(e.def.id=="market")Part(root,"Market canopy",PrimitiveType.Cube,new Vector3(0,1.6f,-d-1),new Vector3(w*1.5f,.13f,1.5f),team);
            if(e.def.id=="trade_post"){Part(root,"Freight awning",PrimitiveType.Cube,new Vector3(0,1.5f,-d-.8f),new Vector3(w*1.8f,.15f,1.4f),team);for(int i=0;i<3;i++)Part(root,"Freight crate",PrimitiveType.Cube,new Vector3(-1+i,.5f,-d-.6f),new Vector3(.7f,.8f,.7f),wood);}
            if(e.def.id=="stable")Part(root,"Corral rail",PrimitiveType.Cube,new Vector3(-w-.6f,.7f,0),new Vector3(.1f,.2f,d*2),wood);
        }
        private static void Resource(Transform root,EntityState e)
        {
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
                    Part(root,"Crate",PrimitiveType.Cube,new Vector3(0,.4f,0),new Vector3(.8f,.8f,.8f),new Color(.57f,.4f,.21f));
                    Part(root,"Crate strap",PrimitiveType.Cube,new Vector3(0,.41f,0),new Vector3(.87f,.86f,.15f),new Color(.22f,.25f,.23f));break;
            }
        }
    }
}
