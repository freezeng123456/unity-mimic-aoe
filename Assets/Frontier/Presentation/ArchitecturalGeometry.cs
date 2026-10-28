using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier
{
    // A building section becomes one renderer with shared material submeshes.
    // No per-brick/per-window GameObjects or primitive colliders are created.
    public sealed class ArchitecturalGeometry
    {
        readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
        readonly List<Vector2> uv=new List<Vector2>();
        readonly List<Material> materials=new List<Material>();readonly List<List<int>> indices=new List<List<int>>();
        public int VertexCount=>vertices.Count;
        List<int> Triangles(Material material)
        {int i=materials.IndexOf(material);if(i<0){i=materials.Count;materials.Add(material);indices.Add(new List<int>());}return indices[i];}
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Material material,float uvScale=1)
        {
            int n=vertices.Count;var normal=Vector3.Cross(b-a,c-a);
            if(normal.sqrMagnitude<.00000001f)normal=Vector3.Cross(c-a,d-a);
            normal=normal.normalized;vertices.AddRange(new[]{a,b,c,d});normals.AddRange(new[]{normal,normal,normal,normal});
            float w=Vector3.Distance(a,d)*uvScale,h=Vector3.Distance(a,b)*uvScale;
            uv.AddRange(new[]{Vector2.zero,new Vector2(0,h),new Vector2(w,h),new Vector2(w,0)});
            Triangles(material).AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
        }
        public void Triangle(Vector3 a,Vector3 b,Vector3 c,Material material)
        {
            int n=vertices.Count;var normal=Vector3.Cross(b-a,c-a).normalized;vertices.AddRange(new[]{a,b,c});normals.AddRange(new[]{normal,normal,normal});
            uv.AddRange(new[]{new Vector2(a.x,a.y),new Vector2(b.x,b.y),new Vector2(c.x,c.y)});Triangles(material).AddRange(new[]{n,n+1,n+2});
        }
        public void Box(Vector3 center,Vector3 size,Material material,Quaternion? rotation=null)
        {
            var q=rotation??Quaternion.identity;var r=size*.5f;Vector3 P(float x,float y,float z)=>center+q*new Vector3(x*r.x,y*r.y,z*r.z);
            Quad(P(-1,-1,-1),P(-1,1,-1),P(1,1,-1),P(1,-1,-1),material);
            Quad(P(1,-1,1),P(1,1,1),P(-1,1,1),P(-1,-1,1),material);
            Quad(P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1),material);
            Quad(P(1,-1,-1),P(1,1,-1),P(1,1,1),P(1,-1,1),material);
            Quad(P(-1,1,-1),P(-1,1,1),P(1,1,1),P(1,1,-1),material);
            Quad(P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1),P(1,-1,1),material);
        }
        public void Beam(Vector3 a,Vector3 b,float width,float depth,Material material)
        {Box((a+b)*.5f,new Vector3(width,(b-a).magnitude,depth),material,Quaternion.FromToRotation(Vector3.up,b-a));}
        public void Lathe(Vector3 center,Vector2[] profile,Material material,int sides=20,Quaternion? rotation=null)
        {
            var q=rotation??Quaternion.identity;
            Vector3 P(int i,int ring){float a=i*Mathf.PI*2/sides;return center+q*new Vector3(Mathf.Cos(a)*profile[ring].x,profile[ring].y,Mathf.Sin(a)*profile[ring].x);}
            for(int ring=0;ring<profile.Length-1;ring++)for(int i=0;i<sides;i++)Quad(P(i,ring),P(i,ring+1),P(i+1,ring+1),P(i+1,ring),material);
        }
        public void Cylinder(Vector3 center,float radius,float height,Material material,int sides=16,Quaternion? rotation=null)
        {Lathe(center,new[]{new Vector2(0,-height*.5f),new Vector2(radius,-height*.5f),new Vector2(radius,height*.5f),new Vector2(0,height*.5f)},material,sides,rotation);}
        public void Arch(Vector3 origin,float width,float spring,float rise,float thickness,float depth,Material material,int segments=12)
        {
            float radius=width*.5f;
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI/segments,b=(i+1)*Mathf.PI/segments;
                Vector3 P(float angle,float offset,float z)=>origin+new Vector3(Mathf.Cos(angle)*(radius+offset),spring+Mathf.Sin(angle)*(rise+offset),z);
                var fa=P(a,0,-depth*.5f);var fb=P(b,0,-depth*.5f);var oa=P(a,thickness,-depth*.5f);var ob=P(b,thickness,-depth*.5f);
                Quad(fa,oa,ob,fb,material);Quad(P(b,0,depth*.5f),P(b,thickness,depth*.5f),P(a,thickness,depth*.5f),P(a,0,depth*.5f),material);
                Quad(oa,P(a,thickness,depth*.5f),P(b,thickness,depth*.5f),ob,material);
                Quad(fb,P(b,0,depth*.5f),P(a,0,depth*.5f),fa,material);
            }
            foreach(int side in new[]{-1,1})Box(origin+new Vector3(side*(radius+thickness*.5f),spring*.5f,0),new Vector3(thickness,spring,depth),material);
        }
        public Transform Create(Transform parent,string name)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);if(vertices.Count==0)return go.transform;
            var mesh=new Mesh{name=name,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.subMeshCount=materials.Count;
            for(int i=0;i<materials.Count;i++)mesh.SetTriangles(indices[i],i);mesh.RecalculateBounds();mesh.RecalculateTangents();
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=materials.ToArray();
            renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;go.AddComponent<ProceduralMeshLifetime>().OwnedMesh=mesh;return go.transform;
        }
    }
}
