Shader "Frontier/River Ground"
{
    Properties
    {
        _GrassMap("Meadow texture",2D)="white"{}
        _GrassNormals("Meadow normals",2D)="bump"{}
        _SoilMap("Soil and sand texture",2D)="white"{}
        _SoilNormals("Soil normals",2D)="bump"{}
        _TurfTint("Meadow tint",Color)=(1,1,1,1)
        _MudColor("Wet soil tint",Color)=(.39,.335,.225,1)
        _SandColor("Dry sand tint",Color)=(.62,.555,.37,1)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Name "Continuous riverbank layers"
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_GrassMap); SAMPLER(sampler_GrassMap);
            TEXTURE2D(_GrassNormals); SAMPLER(sampler_GrassNormals);
            TEXTURE2D(_SoilMap); SAMPLER(sampler_SoilMap);
            TEXTURE2D(_SoilNormals); SAMPLER(sampler_SoilNormals);
            CBUFFER_START(UnityPerMaterial)
            half4 _TurfTint,_MudColor,_SandColor;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half3 tangentWS:TEXCOORD2;half3 bitangentWS:TEXCOORD3;half3 layers:TEXCOORD4;};
            Varyings vert(Attributes v)
            {
                Varyings o;VertexNormalInputs basis=GetVertexNormalInputs(v.normalOS,v.tangentOS);
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=basis.normalWS;o.tangentWS=basis.tangentWS;o.bitangentWS=basis.bitangentWS;
                // Smooth weights interpolate across triangles. There are no
                // per-cell material cuts or thresholded terrain-texture masks.
                o.layers=v.color.rgb;return o;
            }
            half3 DecodeAuthoredNormal(half4 sampled)
            {return normalize(sampled.rgb*2-1);}
            half4 frag(Varyings i):SV_Target
            {
                half3 weights=max(0,i.layers);weights/=max(.0001,weights.r+weights.g+weights.b);
                float2 meadowUV=i.positionWS.xz/128,soilUV=i.positionWS.xz*.5;
                half3 grass=SAMPLE_TEXTURE2D(_GrassMap,sampler_GrassMap,meadowUV).rgb*_TurfTint.rgb;
                half3 soil=SAMPLE_TEXTURE2D(_SoilMap,sampler_SoilMap,soilUV).rgb;
                half3 albedo=soil*_MudColor.rgb*weights.r+soil*_SandColor.rgb*weights.g+grass*weights.b;
                half3 meadowN=DecodeAuthoredNormal(SAMPLE_TEXTURE2D(_GrassNormals,sampler_GrassNormals,meadowUV));
                half3 soilN=DecodeAuthoredNormal(SAMPLE_TEXTURE2D(_SoilNormals,sampler_SoilNormals,soilUV));
                half3 tangentN=normalize(lerp(soilN,meadowN,weights.b));
                tangentN.xy*=.48;tangentN=normalize(tangentN);
                half3 n=normalize(i.tangentWS*tangentN.x+i.bitangentWS*tangentN.y+i.normalWS*tangentN.z);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 illumination=SampleSH(n)+sun.color*saturate(dot(n,sun.direction))*sun.shadowAttenuation;
                return half4(albedo*illumination,1);
            }
            ENDHLSL
        }
    }
}
