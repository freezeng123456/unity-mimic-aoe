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
            float CurrentStreak(float2 p,float2 flow,float unionFlow)
            {
                if(unionFlow>.5)
                {
                    // Union meshes carry the actual blended channel direction.
                    // Orient ribbons in that local frame rather than world Z.
                    p.x/=2.4;float lengthSquared=dot(flow,flow);
                    float2 downstream=lengthSquared>.0001?flow*rsqrt(max(.0001,lengthSquared)):float2(0,1);
                    p=float2(dot(p,float2(downstream.y,-downstream.x))*2.4,dot(p,downstream));
                }
                float ribbon=sin(p.x*28+sin(p.y*2.8)*1.1);
                float broken=sin(p.y*6+p.x*3);
                return smoothstep(.64,.96,ribbon)*smoothstep(-.2,.7,broken);
            }
            float4 RiverSurface085(float2 p,float2 flow,float unionFlow)
            {
                if(unionFlow>.5)
                {
                    // Use the same channel frame as CurrentStreak. Only pigment
                    // and the shading normal vary; the surface stays on its mesh.
                    p.x/=2.4;float lengthSquared=dot(flow,flow);
                    float2 downstream=lengthSquared>.0001?flow*rsqrt(max(.0001,lengthSquared)):float2(0,1);
                    p=float2(dot(p,float2(downstream.y,-downstream.x))*2.4,dot(p,downstream));
                }
                float lane=sin(p.x*15+sin(p.y*1.7)*.75);
                float ripples=sin(p.y*29+sin(p.x*8.5+p.y*3.1)*1.2);
                float fine=sin(p.y*83-p.x*17+lane*.6);
                float broken=smoothstep(.70,.98,ripples)*smoothstep(-.25,.75,sin(p.y*6-p.x*3.5));
                return float4(lane*.6+ripples*.4,ripples,fine,broken);
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
                // A broad current layer survives the strategy camera while fine
                // ripples keep the close view. It shares the same periodic
                // advection blend; no vertex displacement or extra water mesh.
                float swell=sin(flowing.y*9+sin(flowing.x*5)*.8);
                float crossSwell=cos(flowing.x*11+flowing.y*6);
                float streak=CurrentStreak(flowing,i.flow,i.unionFlow);
                float4 surface=RiverSurface085(flowing,i.flow,i.unionFlow);
                if(i.unionFlow>.5)
                {
                    float2 other=basis-velocity*frac(phase+.5);float blend=abs(phase*2-1);
                    swell=lerp(swell,sin(other.y*9+sin(other.x*5)*.8),blend);
                    crossSwell=lerp(crossSwell,cos(other.x*11+other.y*6),blend);
                    streak=lerp(streak,CurrentStreak(other,i.flow,i.unionFlow),blend);
                    surface=lerp(surface,RiverSurface085(other,i.flow,i.unionFlow),blend);
                }
                half3 n=normalize(half3(wave*.075+swell*.060,1,second*.080+crossSwell*.042));
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half lit=.55+.45*saturate(dot(n,sun.direction))*sun.shadowAttenuation;
                half3 view=normalize(GetWorldSpaceViewDir(i.positionWS));
                half fresnel=pow(1-saturate(dot(n,view)),4);
                half glint=pow(saturate(dot(n,normalize(view+sun.direction))),80)*sun.shadowAttenuation;
                // Green-grey shallows come from per-vertex depth, not a painted
                // rectangular shore. Intersections share one continuous plane.
                half depth=saturate(i.color.r);
                half tropical=saturate(1-abs(_RiverTheme-.45)/.45);
                half estuary=saturate((_RiverTheme-.45)/.55);
                half3 shallow=lerp(half3(.23,.43,.34),half3(.28,.52,.42),tropical);
                shallow=lerp(shallow,half3(.34,.45,.36),estuary);
                half3 deep=lerp(half3(.045,.18,.215),half3(.045,.22,.255),tropical);
                deep=lerp(deep,half3(.045,.17,.25),estuary);
                // Depth stays the original mesh-owned value. The slight
                // sediment variation is confined to real shallows, so tributary
                // joins do not acquire a fictitious internal shoreline.
                half silt=(surface.x*.5+.5)*(1-depth)*(1-depth);
                half3 col=lerp(shallow,deep,smoothstep(.08,.86,depth))*lit;
                col*=1+swell*.080+crossSwell*.035;
                // Two readable scales break up the broad colour bands. The
                // variation stays low contrast, with no foam sheet or white noise.
                col*=1+surface.x*.050+surface.z*.015;
                col=lerp(col,col*half3(1.10,1.055,.94),silt*.32);
                // Center the fine sample instead of washing the entire deep
                // channel with its average brightness. Broken longitudinal
                // ribbons show current at zoom26 without an opaque foam ring.
                col+=(fine-.5)*.055+half3(.15,.23,.25)*fresnel+sun.color*glint*.23;
                col+=half3(.12,.19,.17)*streak*.16;
                // Preserve the original reflection normal and highlight energy;
                // the added current detail remains a low-contrast pigment layer.
                half shoreBreak=saturate(.35+swell*.35+second*.20);
                half foam=saturate((1-depth-.81)*6)*shoreBreak*.035;
                return half4(saturate((col+foam)*_BaseColor.rgb),1);
            }
            ENDHLSL
        }
    }
}
