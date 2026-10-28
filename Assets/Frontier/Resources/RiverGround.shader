Shader "Frontier/River Ground"
{
    Properties
    {
        _GrassMap("Meadow texture",2D)="white"{}
        _GrassNormals("Meadow normals",2D)="bump"{}
        _MeadowDetail("Original painted meadow",2D)="white"{}
        _PaintedStrength("Painted surface blend",Range(0,1))=0
        _MeadowOnly("Use legacy meadow surface",Float)=0
        _SoilMap("Soil and sand texture",2D)="white"{}
        _SoilNormals("Soil normals",2D)="bump"{}
        _TurfTint("Meadow tint",Color)=(1,1,1,1)
        _MudColor("Wet soil tint",Color)=(.39,.335,.225,1)
        _SandColor("Dry sand tint",Color)=(.62,.555,.37,1)
        _DetailOrigin("Deterministic landscape variation",Vector)=(0,0,0,0)
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
            TEXTURE2D(_MeadowDetail); SAMPLER(sampler_MeadowDetail);
            TEXTURE2D(_SoilMap); SAMPLER(sampler_SoilMap);
            TEXTURE2D(_SoilNormals); SAMPLER(sampler_SoilNormals);
            CBUFFER_START(UnityPerMaterial)
            half4 _TurfTint,_MudColor,_SandColor;
            float4 _DetailOrigin;
            half _PaintedStrength,_MeadowOnly;
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
            float DetailHash(float2 p)
            {
                float3 q=frac(float3(p.x,p.y,p.x)*.1031);
                q+=dot(q,q.yzx+33.33);return frac((q.x+q.y)*q.z);
            }
            float DetailNoise(float2 p)
            {
                float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(DetailHash(cell),DetailHash(cell+float2(1,0)),f.x),
                    lerp(DetailHash(cell+float2(0,1)),DetailHash(cell+1),f.x),f.y);
            }
            half4 frag(Varyings i):SV_Target
            {
                half3 weights=max(0,i.layers);weights/=max(.0001,weights.r+weights.g+weights.b);
                // Break up the sand/meadow transition in world space, while
                // keeping all terrain vertices and physical shore queries exact.
                // This only changes a surface's pigment, never the water edge.
                float2 detail=i.positionWS.xz+_DetailOrigin.xy;
                half broad=DetailNoise(detail*.095),patch=DetailNoise(detail*.38);
                // The authored weights still delimit the bank. Restrict the
                // darkest wet pigment to its water-side part, with broader
                // sand/meadow fingers within that existing transition zone.
                half turf=saturate(weights.b+(patch-.48)*1.45*weights.g);
                half wet=min(1-turf,smoothstep(.70,.98,weights.r));
                weights=half3(wet,max(0,1-wet-turf),turf);
                weights=lerp(weights,half3(0,0,1),saturate(_MeadowOnly));
                float2 meadowUV=i.positionWS.xz/128,soilUV=i.positionWS.xz*.5;
                half3 grass=SAMPLE_TEXTURE2D(_GrassMap,sampler_GrassMap,meadowUV).rgb*_TurfTint.rgb;
                // Independent scales soften obvious tiling; broad pigment and
                // authored shoreline weights remain shared with the map.
                float2 paintedUV=(i.positionWS.xz+_DetailOrigin.xy)/14;
                half3 painted=SAMPLE_TEXTURE2D(_MeadowDetail,sampler_MeadowDetail,paintedUV).rgb;
                half3 turned=SAMPLE_TEXTURE2D(_MeadowDetail,sampler_MeadowDetail,float2(-paintedUV.y,paintedUV.x)*.63+float2(.31,.57)).rgb;
                painted=lerp(painted,turned,.28);
                half paintedValue=dot(painted,half3(.2126,.7152,.0722));
                painted=lerp(paintedValue.xxx,painted,.65)*.92;
                grass=lerp(grass,painted*_TurfTint.rgb,saturate(_PaintedStrength));
                half3 soil=SAMPLE_TEXTURE2D(_SoilMap,sampler_SoilMap,soilUV).rgb;
                // Low-frequency cool growth patches and warm dry flecks remain
                // readable at the ordinary strategy camera, with subdued grit.
                half growth=smoothstep(.24,.76,broad);
                grass*=lerp(half3(.64,.84,.69),half3(1.08,1.06,.92),growth);
                // Patchy damp sediment breaks up the dry shoulder without
                // placing a second painted waterline on navigable terrain.
                half damp=smoothstep(.38,.80,1-patch)*.28;
                half3 sand=lerp(_SandColor.rgb,_MudColor.rgb,damp)*lerp(.88,1.08,patch);
                half3 mud=_MudColor.rgb*lerp(.77,1.02,broad*.4+patch*.6);
                half3 albedo=soil*mud*weights.r+soil*sand*weights.g+grass*weights.b;
                half3 meadowN=DecodeAuthoredNormal(SAMPLE_TEXTURE2D(_GrassNormals,sampler_GrassNormals,meadowUV));
                half3 soilN=DecodeAuthoredNormal(SAMPLE_TEXTURE2D(_SoilNormals,sampler_SoilNormals,soilUV));
                half3 tangentN=normalize(lerp(soilN,meadowN,weights.b));
                tangentN.xy*=lerp(.30,.60,weights.b);tangentN=normalize(tangentN);
                half3 n=normalize(i.tangentWS*tangentN.x+i.bitangentWS*tangentN.y+i.normalWS*tangentN.z);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 illumination=SampleSH(n)+sun.color*saturate(dot(n,sun.direction))*sun.shadowAttenuation;
                return half4(albedo*illumination,1);
            }
            ENDHLSL
        }
    }
}
