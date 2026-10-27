Shader "Frontier/Fog" {
 Properties { _MainTex ("Visibility", 2D) = "black" {} _WorldSpace ("World-space sampling", Float) = 0 _SoftEdge ("Conservative fog edge", Range(0,1)) = 1 }
 SubShader { Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
 Pass { Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off Offset -1, -1
 HLSLPROGRAM
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
 // Soften inward only: a hidden fragment can never become less obscured.
 fog.a=max(fog.a,lerp(fog.a,edge,saturate(_SoftEdge)));return fog;
 }
 ENDHLSL
 } } }
