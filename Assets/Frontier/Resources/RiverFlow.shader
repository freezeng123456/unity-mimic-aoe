Shader "Frontier/River Flow"
{
    Properties
    {
        _BaseMap("Fine ripples",2D)="white"{}
        _BumpMap("Ripple normals",2D)="bump"{}
        [MainColor] _BaseColor("Water tint",Color)=(1,1,1,1)
        _BumpScale("Ripple amplitude",Float)=0.42
        _Smoothness("Surface gloss",Float)=0.72
        _Metallic("Glints",Float)=0.12
        _FlowTime("Simulation flow time",Float)=0
        _RiverTheme("Estuary and tropical tint",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10"}
        Pass
        {
            Name "River current"
            Tags {"LightMode"="UniversalForward"}
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _BumpScale, _Smoothness, _Metallic, _FlowTime, _RiverTheme;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float2 flow:TEXCOORD1;float4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float2 uv:TEXCOORD1;float4 color:TEXCOORD2;float2 flow:TEXCOORD3;float unionFlow:TEXCOORD4;};
            Varyings vert(Attributes v)
            {
                Varyings o;o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);o.uv=v.uv;o.color=v.color;
                o.unionFlow=dot(v.flow,v.flow)>.01?1:0;
                o.flow=o.unionFlow>.5?v.flow:float2(0,1);return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                // U crosses a channel; V is accumulated river arc length. The
                // moving ripples therefore follow bends instead of world Z.
                float time=_FlowTime*.045,phase=i.unionFlow>.5?frac(time):time;
                float2 basis=float2(i.uv.x*2.4,i.uv.y),velocity=float2(i.flow.x*2.4,i.flow.y);
                float2 flowing=basis-velocity*phase;
                half3 fine=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,flowing).rgb;
                float wave=sin(flowing.y*44+sin(flowing.x*19)*.65);
                float second=cos(flowing.y*71-flowing.x*13);
                if(i.unionFlow>.5)
                {
                    // Two periodic advection phases crossfade. A long game does
                    // not accumulate stretched UV gradients at the confluence.
                    float2 other=basis-velocity*frac(phase+.5);float blend=abs(phase*2-1);
                    fine=lerp(fine,SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,other).rgb,blend);
                    wave=lerp(wave,sin(other.y*44+sin(other.x*19)*.65),blend);
                    second=lerp(second,cos(other.y*71-other.x*13),blend);
                }
                half3 n=normalize(half3(wave*.045,1,second*.065));
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half lit=.55+.45*saturate(dot(n,sun.direction))*sun.shadowAttenuation;
                half3 view=normalize(GetWorldSpaceViewDir(i.positionWS));
                half fresnel=pow(1-saturate(dot(n,view)),4);
                half glint=pow(saturate(dot(n,normalize(view+sun.direction))),80)*sun.shadowAttenuation;
                // Green-grey shallows come from per-vertex depth, not a painted
                // rectangular shore. Intersections share one continuous plane.
                half depth=i.color.r;
                half3 shallow=lerp(half3(.255,.415,.33),half3(.32,.49,.435),_RiverTheme);
                half3 deep=lerp(half3(.075,.235,.255),half3(.085,.26,.33),_RiverTheme);
                half3 col=lerp(shallow,deep,depth)*lit;
                col+=fine*.15+half3(.24,.34,.36)*fresnel+sun.color*glint*.3;
                half foam=saturate((1-depth-.81)*6)*(.5+.5*sin(flowing.y*63))* .11;
                return half4((col+foam)*_BaseColor.rgb,1);
            }
            ENDHLSL
        }
    }
}
