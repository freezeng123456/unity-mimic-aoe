using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // Original repeatable material studies. Surface scale is in model metres;
    // shared albedo/normal maps are independent of team and civilization tint.
    public static class ArchitecturalMaterials
    {
        public enum Surface { Plaster,Brick,Stone,Slate,Tile,Wood,Metal,Glass,Cloth,Earth }
        static readonly Dictionary<Surface,Texture2D[]> Textures=new Dictionary<Surface,Texture2D[]>();
        static readonly Dictionary<string,Material> Materials=new Dictionary<string,Material>();
        public static Material Get(Surface surface,Color tint)
        {
            string key=surface+"_"+ColorUtility.ToHtmlStringRGBA(tint);
            if(Materials.TryGetValue(key,out var material)&&material!=null)return material;
            if(!Textures.TryGetValue(surface,out var maps))Textures[surface]=maps=MakeMaps(surface);
            material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Frontier / "+surface+" / "+key,color=tint,enableInstancing=true};
            material.SetTexture("_BaseMap",maps[0]);material.SetTexture("_BumpMap",maps[1]);material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_BumpScale",surface==Surface.Glass?.15f:.55f);
            material.SetFloat("_Smoothness",surface==Surface.Metal?.56f:surface==Surface.Glass?.68f:surface==Surface.Wood?.18f:.10f);
            material.SetFloat("_Metallic",surface==Surface.Metal?.72f:0);
            Materials[key]=material;return material;
        }
        static Texture2D[] MakeMaps(Surface surface)
        {
            const int size=256;var pixels=new Color[size*size];var heights=new float[size*size];
            float Fract(float v)=>v-Mathf.Floor(v);
            float Hash(int x,int y)=>Fract(Mathf.Sin(x*127.1f+y*311.7f+(int)surface*53.17f)*43758.5453f);
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=x/(float)size,v=y/(float)size;
                // Periodic sine fields keep every surface seamless at tile edges.
                float grain=Mathf.Sin(u*150.79645f+Mathf.Sin(v*31.41593f)*2)*Mathf.Sin(v*100.53097f+u*6.283185f);
                float fine=Hash(x,y)-.5f,value=.91f+grain*.026f+fine*.038f,height=.5f+grain*.08f;
                if(surface==Surface.Brick||surface==Surface.Stone||surface==Surface.Slate||surface==Surface.Tile)
                {
                    float rows=surface==Surface.Brick?8:surface==Surface.Stone?4:10;
                    float columns=surface==Surface.Brick?3:surface==Surface.Stone?2:5;
                    int row=Mathf.FloorToInt(v*rows);float bx=u*columns+(row%2)*.5f;
                    float jointX=Mathf.Min(Fract(bx),1-Fract(bx)),jointY=Mathf.Min(Fract(v*rows),1-Fract(v*rows));
                    float joint=Mathf.Min(jointX*(surface==Surface.Stone?1.8f:1),jointY*.62f);
                    float mortar=1-Mathf.SmoothStep(.018f,.054f,joint);
                    float block=Hash(Mathf.FloorToInt(bx),row);
                    value=(.83f+block*.16f)*(1-mortar*.26f);height=.65f-mortar*.48f+grain*.018f;
                    if(surface==Surface.Tile){float wave=Mathf.Cos(Fract(bx)*Mathf.PI*2);value+=wave*.055f;height+=wave*.08f;}
                    if(surface==Surface.Slate)value+=(Fract(v*rows)-.5f)*.06f;
                }
                else if(surface==Surface.Wood)
                {
                    float fibre=Mathf.Sin(u*251.3274f+Mathf.Sin(v*12.56637f)*.9f+Mathf.Sin(u*50.26548f)*1.3f);
                    float seam=1-Mathf.SmoothStep(.012f,.035f,Mathf.Min(Fract(u*4),1-Fract(u*4)));
                    value=.83f+fibre*.09f+grain*.03f-seam*.22f;height=.5f+fibre*.12f-seam*.25f;
                }
                else if(surface==Surface.Cloth)
                {float weave=Mathf.Sin(x*Mathf.PI*.5f)*Mathf.Sin(y*Mathf.PI*.5f);value=.94f+weave*.035f;height=.5f+weave*.12f;}
                else if(surface==Surface.Earth){value=.82f+grain*.09f+fine*.10f;height=.5f+grain*.12f+fine*.13f;}
                else if(surface==Surface.Glass){value=.82f+v*.12f+Mathf.Sin((u+v)*25.13274f)*.025f;height=.5f;}
                pixels[x+y*size]=new Color(value,value,value,1);heights[x+y*size]=height;
            }
            var normals=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float dx=heights[(x+1)%size+y*size]-heights[(x+size-1)%size+y*size];
                float dy=heights[x+(y+1)%size*size]-heights[x+(y+size-1)%size*size];
                Vector3 n=new Vector3(-dx*1.2f,-dy*1.2f,1).normalized;
                normals[x+y*size]=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,1);
            }
            var albedo=new Texture2D(size,size,TextureFormat.RGBA32,true,false){name="Authored "+surface+" albedo",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};
            var normal=new Texture2D(size,size,TextureFormat.RGBA32,true,true){name="Authored "+surface+" normal",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};
            albedo.SetPixels(pixels);albedo.Apply(true,true);normal.SetPixels(normals);normal.Apply(true,true);return new[]{albedo,normal};
        }
    }
}
