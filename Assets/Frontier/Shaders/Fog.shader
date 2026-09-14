Shader "Frontier/Fog" {
 Properties { _MainTex ("Visibility", 2D) = "black" {} }
 SubShader { Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
 Pass { Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
 struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
 TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
 Varyings vert(Attributes i){Varyings o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;return o;}
 half4 frag(Varyings i):SV_Target{return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);}
 ENDHLSL
 } } }
