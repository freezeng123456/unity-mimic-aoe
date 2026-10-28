using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier
{
    // Multiple channels are one geographic water region. Triangulating its
    // shared distance field prevents coplanar overlap, fake internal banks and
    // contradictory depth colors where a tributary enters the main river.
    public static class ConfluenceWaterPresentation
    {
        public static Transform Build(Transform parent,MapLayout layout,Material material)
        {
            const int count=257;const float cell=.5f;
            var distance=new float[count*count];
            for(int z=0;z<count;z++)for(int x=0;x<count;x++)distance[x+z*count]=layout.SignedBankDistance(new Vector2(x*cell,z*cell));
            var points=new List<Vector3>();var uv=new List<Vector2>();var flow=new List<Vector2>();var colors=new List<Color>();var triangles=new List<int>();
            var gridVertices=new Dictionary<int,int>();var intersections=new Dictionary<ulong,int>();
            Vector2 GridPoint(int id)=>new Vector2((id%count)*cell,(id/count)*cell);
            int AddPoint(Vector2 p,float shore)
            {
                int index=points.Count;points.Add(new Vector3(p.x,layout.WaterHeight,p.y));
                // World UVs are continuous across the union; its smoothly blended
                // current vector advects the texture along each adjoining river.
                uv.Add(p/12);flow.Add(Current(layout,p));
                colors.Add(new Color(Mathf.Sqrt(Mathf.Clamp01(-shore/3.5f)),0,0,1));return index;
            }
            int Vertex(int id)
            {
                if(!gridVertices.TryGetValue(id,out int index))
                {index=AddPoint(GridPoint(id),distance[id]);gridVertices.Add(id,index);}
                return index;
            }
            int Cut(int a,int b)
            {
                if(Mathf.Abs(distance[a])<.000001f)return Vertex(a);
                if(Mathf.Abs(distance[b])<.000001f)return Vertex(b);
                uint low=(uint)Mathf.Min(a,b),high=(uint)Mathf.Max(a,b);ulong key=((ulong)low<<32)|high;
                if(!intersections.TryGetValue(key,out int index))
                {
                    float t=distance[a]/(distance[a]-distance[b]);
                    index=AddPoint(Vector2.Lerp(GridPoint(a),GridPoint(b),t),0);intersections.Add(key,index);
                }
                return index;
            }
            void Triangle(int a,int b,int c)
            {
                var polygon=new List<int>(4);var ids=new[]{a,b,c};
                for(int side=0;side<3;side++)
                {
                    int start=ids[side],end=ids[(side+1)%3];bool inside=distance[start]<=0,nextInside=distance[end]<=0;
                    if(inside&&nextInside)polygon.Add(Vertex(end));
                    else if(inside)polygon.Add(Cut(start,end));
                    else if(nextInside){polygon.Add(Cut(start,end));polygon.Add(Vertex(end));}
                }
                for(int i=1;i<polygon.Count-1;i++)
                {
                    int x=polygon[0],y=polygon[i],z=polygon[i+1];
                    if(Vector3.Cross(points[y]-points[x],points[z]-points[x]).sqrMagnitude>.0000001f)
                        triangles.AddRange(new[]{x,y,z});
                }
            }
            for(int z=0;z<count-1;z++)for(int x=0;x<count-1;x++)
            {
                int a=x+z*count,b=a+count,c=b+1,d=a+1;
                if(distance[a]>0&&distance[b]>0&&distance[c]>0&&distance[d]>0)continue;
                Triangle(a,b,c);Triangle(a,c,d);
            }
            var mesh=new Mesh{name="Confluence union surface with shared boundary vertices",indexFormat=points.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.SetVertices(points);mesh.SetUVs(0,uv);mesh.SetUVs(1,flow);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            var go=new GameObject("Continuous navigable river");go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            go.AddComponent<ProceduralMeshLifetime>().OwnedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
            return go.transform;
        }

        static Vector2 Current(MapLayout layout,Vector2 point)
        {
            Vector2 result=Vector2.zero;
            foreach(var channel in layout.Channels)
            {
                float closest=float.MaxValue;Vector2 tangent=Vector2.up;
                for(int i=0;i<channel.Centers.Length-1;i++)
                {
                    Vector2 start=channel.Centers[i],delta=channel.Centers[i+1]-start;
                    float t=Mathf.Clamp01(Vector2.Dot(point-start,delta)/Mathf.Max(.000001f,delta.sqrMagnitude));
                    float distance=(point-start-delta*t).sqrMagnitude;
                    if(distance<closest){closest=distance;tangent=delta.normalized;}
                }
                float weight=1/Mathf.Pow(Mathf.Sqrt(closest)+2,4);result+=tangent*weight;
            }
            return result.sqrMagnitude>.000000001f?result.normalized:Vector2.up;
        }
    }
}
