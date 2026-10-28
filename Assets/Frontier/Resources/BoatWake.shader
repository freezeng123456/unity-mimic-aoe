Shader "Frontier/Boat Wake"
{
    Properties {[MainColor] _BaseColor("Foam",Color)=(.56,.73,.66,.44) _Phase("Simulation phase",Float)=0}
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off Offset -1,-1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;float _Phase;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            Varyings vert(Attributes v){Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;return o;}
            half4 frag(Varyings i):SV_Target
            {
                float feather=saturate(1-abs(i.uv.x*2-1));
                float pulse=.6+.4*sin(i.uv.y*18-_Phase*4);
                return half4(_BaseColor.rgb,_BaseColor.a*feather*pulse*saturate(1-i.uv.y*.43));
            }
            ENDHLSL
        }
    }
}
