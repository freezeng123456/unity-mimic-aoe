using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public enum WorkerTool { None,Axe,Pick,Hammer,Basket }
    // Original low-poly work silhouettes. Created once per pooled worker rig.
    public sealed class WorkerEquipment:MonoBehaviour
    {
        readonly Dictionary<WorkerTool,GameObject> models=new Dictionary<WorkerTool,GameObject>();
        Transform basket;
        public WorkerTool CurrentTool { get; private set; }=(WorkerTool)(-1);
        static readonly Color Wood=new Color(.32f,.19f,.09f),Steel=new Color(.43f,.48f,.49f),Wicker=new Color(.65f,.44f,.21f);
        public void Initialize(UnitRig rig)
        {
            var axe=Root(rig.WeaponPivot,WorkerTool.Axe);Handle(axe,.8f);
            Extrude(axe,"Axe blade",new[]{new Vector2(-.06f,.46f),new Vector2(-.06f,.65f),new Vector2(.18f,.64f),new Vector2(.47f,.73f),new Vector2(.43f,.37f),new Vector2(.15f,.45f)},.055f,Steel,new[]{0,1,2,0,2,5,2,3,4,2,4,5});
            var pick=Root(rig.WeaponPivot,WorkerTool.Pick);Handle(pick,.9f);
            Extrude(pick,"Curved pick head",new[]{new Vector2(-.56f,.35f),new Vector2(-.35f,.61f),new Vector2(0,.68f),new Vector2(.34f,.6f),new Vector2(.55f,.34f),new Vector2(.25f,.5f),new Vector2(0,.54f),new Vector2(-.25f,.5f)},.05f,Steel,new[]{0,1,7,1,2,6,1,6,7,2,3,5,2,5,6,3,4,5});
            var hammer=Root(rig.WeaponPivot,WorkerTool.Hammer);Handle(hammer,.52f);
            ProceduralArt.Part(hammer,"Hammer head",PrimitiveType.Cube,new Vector3(0,.31f,0),new Vector3(.38f,.19f,.17f),Steel);
            basket=Root(rig.LeftHand,WorkerTool.Basket);
            // Open octagonal body: four rings form outer and inner walls and rim.
            var vertices=new List<Vector3>();var triangles=new List<int>();
            float[] radius={.18f,.27f,.225f,.145f};float[] height={-.44f,-.19f,-.19f,-.4f};
            for(int ring=0;ring<4;ring++)for(int j=0;j<8;j++){float a=j*Mathf.PI/4;vertices.Add(new Vector3(Mathf.Cos(a)*radius[ring],height[ring],Mathf.Sin(a)*radius[ring]));}
            for(int ring=0;ring<3;ring++)for(int j=0;j<8;j++){int a=ring*8+j,b=ring*8+(j+1)%8;triangles.AddRange(new[]{a,b,a+8,b,b+8,a+8});}
            for(int j=1;j<7;j++)triangles.AddRange(new[]{24,24+j,24+j+1});
            for(int i=0;i<triangles.Count;i+=3){int swap=triangles[i];triangles[i]=triangles[i+1];triangles[i+1]=swap;}
            ProceduralArt.MeshPart(basket,"Basket body",vertices.ToArray(),triangles.ToArray(),Wicker);
            // Angular handle stays joined to the hand at local origin.
            Segment(basket,new Vector3(-.24f,-.2f,0),new Vector3(-.14f,-.02f,0));
            Segment(basket,new Vector3(-.14f,-.02f,0),new Vector3(.14f,-.02f,0));
            Segment(basket,new Vector3(.14f,-.02f,0),new Vector3(.24f,-.2f,0));
            Show(WorkerTool.None);
        }
        Transform Root(Transform parent,WorkerTool tool){var go=new GameObject(tool.ToString());go.transform.SetParent(parent,false);models.Add(tool,go);return go.transform;}
        static void Handle(Transform parent,float length){ProceduralArt.Part(parent,"Wood handle",PrimitiveType.Cube,new Vector3(0,length*.25f,0),new Vector3(.075f,length,.085f),Wood);}
        static void Segment(Transform parent,Vector3 a,Vector3 b)
        {var go=ProceduralArt.Part(parent,"Wicker handle",PrimitiveType.Cube,(a+b)*.5f,new Vector3(.04f,(b-a).magnitude,.04f),Wicker);go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
        void Extrude(Transform parent,string name,Vector2[] polygon,float halfDepth,Color color,int[] capTriangles)
        {
            int n=polygon.Length;var vertices=new Vector3[n*2];var faces=new List<int>();
            for(int i=0;i<n;i++){vertices[i]=new Vector3(polygon[i].x,polygon[i].y,-halfDepth);vertices[i+n]=new Vector3(polygon[i].x,polygon[i].y,halfDepth);}
            // Authored triangulation follows each outline, including the pick's
            // concave underside. No center fan across empty space.
            for(int i=0;i<capTriangles.Length;i+=3){int a=capTriangles[i],b=capTriangles[i+1],c=capTriangles[i+2];faces.AddRange(new[]{a,b,c,c+n,b+n,a+n});}
            for(int i=0;i<n;i++){int j=(i+1)%n;faces.AddRange(new[]{i,i+n,j,j,i+n,j+n});}
            ProceduralArt.MeshPart(parent,name,vertices,faces.ToArray(),color);
        }

        public void Show(WorkerTool tool)
        {
            if(CurrentTool==tool)return;
            foreach(var pair in models)pair.Value.SetActive(pair.Key==tool);CurrentTool=tool;
        }
        public void KeepBasketUpright(Quaternion rotation){if(basket!=null)basket.rotation=rotation;}
    }
}
