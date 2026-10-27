Shader "Frontier/Fog" {
 Properties { _MainTex ("Visibility", 2D) = "black" {} _WorldSpace ("World-space sampling", Float) = 0 _SoftEdge ("Conservative fog edge", Range(0,1)) = 1 }
 SubShader { Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
 Pass { Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off Offset -1, -1
 HLSLPROGRAM
 #pragma target 3.5
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
 struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 positionWS:TEXCOORD1; };
 float _WorldSpace, _SoftEdge;
 float4 _MainTex_TexelSize;
 TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
 Varyings vert(Attributes i){Varyings o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;o.positionWS=TransformObjectToWorld(i.positionOS.xyz);return o;}
 half4 frag(Varyings i):SV_Target
 {
 float2 uv=lerp(i.uv,i.positionWS.xz/128,_WorldSpace),dx=float2(_MainTex_TexelSize.x,0),dy=float2(0,_MainTex_TexelSize.y);
 half4 fog=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv);
 half edge=fog.a*.5h+(SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv+dx).a+SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv-dx).a+
                     SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv+dy).a+SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv-dy).a)*.125h;
 // Bilinear filtering averages visibility cells. For a world point in an
 // unexplored cell, preserve complete opacity even near its edge; visible
 // and explored cells retain their existing conservative softening.
 int2 cell=clamp((int2)floor(uv*_MainTex_TexelSize.zw+.5),int2(0,0),(int2)_MainTex_TexelSize.zw-1);
 half cellAlpha=LOAD_TEXTURE2D(_MainTex,cell).a;
 fog.a=max(fog.a,lerp(fog.a,edge,saturate(_SoftEdge)));
 if(cellAlpha>.995h){fog.a=1;return fog;}
 // Only obscure known cells, never weaken an unexplored cell. Distance to
 // neighbouring unexplored cell squares gives a continuous one-sided veil,
 // softening the hard grid silhouette without seeing through hidden terrain.
 if(_SoftEdge>0)
 {
     float2 grid=uv*_MainTex_TexelSize.zw;
     float nearest=1e3;
     [unroll] for(int y=-1;y<=1;y++)[unroll] for(int x=-1;x<=1;x++)
     {
         if(x==0&&y==0)continue;
         int2 n=clamp(cell+int2(x,y),int2(0,0),(int2)_MainTex_TexelSize.zw-1);
         if(LOAD_TEXTURE2D(_MainTex,n).a>.995h)
         {
             float2 delta=max(abs(grid-(float2)n)-.5,0);
             nearest=min(nearest,length(delta));
         }
     }
     half veil=(1-smoothstep(0,.85,nearest))*saturate(_SoftEdge);
     fog.a=max(fog.a,veil);
 }
 return fog;
 }
 ENDHLSL
 } } }
