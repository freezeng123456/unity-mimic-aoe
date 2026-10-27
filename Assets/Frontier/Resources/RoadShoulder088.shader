Shader "Frontier/Road Shoulder"
{
    Properties { _BaseMap("Earth",2D)="white"{} _BaseColor("Earth tint",Color)=(.58,.48,.32,1) }
    SubShader
    {
        // Draw before the world fog overlay (Transparent = 3000), so softened
        // edges do not reveal terrain hidden by the player's vision.
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-20" }
        Pass
        {
            Name "Soft earth shoulder"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;half4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half opacity:TEXCOORD2;};
            Varyings vert(Attributes v)
            {
                Varyings o;o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.opacity=v.color.a;return o;
            }
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p)
            {
                float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);
            }
            half4 frag(Varyings i):SV_Target
            {
                half a=saturate(i.opacity);
                // Disturb only the blended shoulder, never the continuous centre.
                a=saturate(a+(noise(i.positionWS.xz*3.1)-.5)*.38*a*(1-a));
                half3 earth=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.positionWS.xz).rgb*_BaseColor.rgb;
                half3 n=normalize(i.normalWS);Light sun=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 illumination=SampleSH(n)+sun.color*saturate(dot(n,sun.direction))*sun.shadowAttenuation;
                return half4(earth*illumination,a);
            }
            ENDHLSL
        }
    }
}
