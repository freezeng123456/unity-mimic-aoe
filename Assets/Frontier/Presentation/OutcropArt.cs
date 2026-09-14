using System;
using UnityEngine;

namespace Frontier
{
    // Deterministic authored geometry for terrain outcrops. The caller owns
    // material, renderer, collider and GameObject lifetime.
    public static class OutcropArt
    {
        private const int Sides=12;
        public static Mesh CreateMesh(float halfExtent,int variation)
        {
            halfExtent=Mathf.Max(.1f,halfExtent);int seed=variation*92821+17;
            float Phase(int i,float scale)=>Mathf.Sin(seed*.013f+i*scale)*.5f+.5f;
            var vertices=new Vector3[Sides*4+2];var triangles=new int[Sides*24];
            float[] levels={0,.16f,1.25f,2.3f};float[] widths={1f,.96f,.9f,.68f};
            // Twelve perimeter samples: three on each edge, with all four
            // corners retained exactly so the base follows the square grid
            // obstruction footprint.
            float[] px={-1f,-.333333f,.333333f,1f,1f,1f,1f,.333333f,-.333333f,-1f,-1f,-1f};
            float[] pz={-1f,-1f,-1f,-1f,-.333333f,.333333f,1f,1f,1f,1f,.333333f,-.333333f};
            for(int ring=0;ring<4;ring++)for(int i=0;i<Sides;i++)
            {
                // A low foundation retains the occupied square. Above it,
                // clipped corners and a sloping fractured crown read as stone.
                float wobble=ring==0?1f:.86f+.14f*Phase(i+ring*3,2.17f);
                bool corner=i%3==0;float bevel=ring>0&&corner?.78f:1f;
                float scale=halfExtent*widths[ring]*wobble*bevel;
                float h=levels[ring];
                if(ring==1)h+=.06f*Phase(i,1.7f);
                if(ring==2)h+=.18f*(Phase(i,1.31f)-.5f);
                if(ring==3)h+=.22f*px[i]-.17f*pz[i]+.12f*(Phase(i,1.31f)-.5f);
                var offset=ring>=2?new Vector3((Phase(1,1.8f)-.5f)*halfExtent*.15f,0,(Phase(2,1.8f)-.5f)*halfExtent*.15f):Vector3.zero;
                vertices[ring*Sides+i]=new Vector3(px[i]*scale,h,pz[i]*scale)+offset;
            }
            vertices[Sides*4+1]=new Vector3(halfExtent*.06f,2.45f+.06f*Phase(3,1.3f),-halfExtent*.04f);
            int t=0;
            for(int i=0;i<Sides;i++){int n=(i+1)%Sides;triangles[t++]=i;triangles[t++]=Sides+i;triangles[t++]=n;triangles[t++]=Sides+i;triangles[t++]=Sides+n;triangles[t++]=n;}
            for(int ring=1;ring<3;ring++)for(int i=0;i<Sides;i++){int n=(i+1)%Sides;int a=ring*Sides+i,b=ring*Sides+n,c=(ring+1)*Sides+i,d=(ring+1)*Sides+n;triangles[t++]=a;triangles[t++]=c;triangles[t++]=b;triangles[t++]=c;triangles[t++]=d;triangles[t++]=b;}
            int bottom=Sides*4,apex=Sides*4+1;vertices[bottom]=Vector3.zero;
            for(int i=0;i<Sides;i++){int n=(i+1)%Sides;triangles[t++]=bottom;triangles[t++]=i;triangles[t++]=n;}
            for(int i=0;i<Sides;i++){int n=(i+1)%Sides;triangles[t++]=3*Sides+i;triangles[t++]=apex;triangles[t++]=3*Sides+n;}
            // Expand each face so adjacent faces retain hard, authored edges.
            // The construction mesh remains 50 logical vertices; the output
            // has one independent vertex and one explicit normal per corner.
            var flatVertices=new Vector3[triangles.Length];var flatNormals=new Vector3[triangles.Length];
            for(int i=0;i<triangles.Length;i+=3)
            {
                Vector3 a=vertices[triangles[i]],b=vertices[triangles[i+1]],c=vertices[triangles[i+2]];
                Vector3 normal=Vector3.Cross(b-a,c-a).normalized;
                flatVertices[i]=a;flatVertices[i+1]=b;flatVertices[i+2]=c;
                flatNormals[i]=normal;flatNormals[i+1]=normal;flatNormals[i+2]=normal;
            }
            var flatTriangles=new int[triangles.Length];for(int i=0;i<flatTriangles.Length;i++)flatTriangles[i]=i;
            var mesh=new Mesh{name="Frontier Outcrop "+variation};mesh.vertices=flatVertices;mesh.normals=flatNormals;mesh.triangles=flatTriangles;mesh.RecalculateBounds();return mesh;
        }
    }
}
