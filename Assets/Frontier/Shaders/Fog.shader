Shader "Frontier/Fog" {
 Properties { _MainTex ("Visibility", 2D) = "black" {} _WorldSpace ("World-space sampling", Float) = 0 }
 SubShader { Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
 Pass { Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off Offset -1, -1
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
 struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 positionWS:TEXCOORD1; };
 float _WorldSpace;
 TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
 Varyings vert(Attributes i){Varyings o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;o.positionWS=TransformObjectToWorld(i.positionOS.xyz);return o;}
 half4 frag(Varyings i):SV_Target{return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,lerp(i.uv,i.positionWS.xz/128,_WorldSpace));}
 ENDHLSL
 } } }
