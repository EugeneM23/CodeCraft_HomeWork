// Wet mud with puddles and the ruts MudSurface records. The mesh is a dense flat grid on the floor
// collider's top; every vertex is lifted to the mud's height (MudCommon.hlsl), so ruts are real
// dents and berms real ridges. Per-pixel normals come from the same height, which draws the tread
// grooves finer than the grid. Below the water level the mud is a puddle: flat, glossy, rippling,
// darker where it is deeper.
// URP: lit like URP's Lit (GameLit.hlsl); the shadow, depth and depth-normals passes lift the
// vertices the same way, so ruts cast and receive shadows and SSAO sees them.
Shader "Game/MudGround"
{
    Properties
    {
        [Header(Shape)]
        _MudThickness ("Mud Thickness (m)", Float) = 0.32
        _ReliefDepth ("Relief Depth (m)", Float) = 0.05
        _ReliefScale ("Relief Scale (1/m)", Float) = 0.18
        _ReliefContrast ("Relief Contrast", Float) = 1.3
        _BermHeight ("Berm Height (m)", Float) = 0.12
        _PuddleLevel ("Puddle Level (m)", Float) = -0.03
        _RutWater ("Rut Holds Water Past", Range(0, 1)) = 0.85
        _Seed ("Seed", Float) = 0

        [Header(Mud)]
        _MudDry ("Mud Dry", Color) = (0.36, 0.25, 0.15, 1)
        _MudWet ("Mud Wet", Color) = (0.17, 0.11, 0.06, 1)
        _Wetness ("Wetness", Range(0, 1)) = 0.55
        _DrySmoothness ("Dry Smoothness", Range(0, 1)) = 0.12
        _WetSmoothness ("Wet Smoothness", Range(0, 1)) = 0.6
        _ClumpScale ("Clump Scale (1/m)", Float) = 3.5
        _ClumpHeight ("Clump Height (m)", Float) = 0.004

        [Header(Water)]
        _WaterShallow ("Water Shallow", Color) = (0.16, 0.12, 0.08, 1)
        _WaterDeep ("Water Deep", Color) = (0.05, 0.04, 0.03, 1)
        _WaterSmoothness ("Water Smoothness", Range(0, 1)) = 0.95
        _RippleScale ("Ripple Scale (1/m)", Float) = 4
        _RippleSpeed ("Ripple Speed", Float) = 0.35
        _RippleStrength ("Ripple Strength", Float) = 0.012

        [HideInInspector] _MudTracks ("Tracks", 2D) = "black" {}
    }

    HLSLINCLUDE
    #define MUD_EXTRA_MATERIAL_PROPERTIES \
        float4 _MudDry; \
        float4 _MudWet; \
        float _Wetness; \
        float _DrySmoothness; \
        float _WetSmoothness; \
        float _ClumpScale; \
        float _ClumpHeight; \
        float4 _WaterShallow; \
        float4 _WaterDeep; \
        float _WaterSmoothness; \
        float _RippleScale; \
        float _RippleSpeed; \
        float _RippleStrength;

    #include "MudCommon.hlsl"

    struct Attributes
    {
        float4 positionOS : POSITION;
    };

    // Lifts a grid vertex to the drawn mud height.
    float3 MudVertexWS(float3 positionOS)
    {
        float3 world = TransformObjectToWorld(positionOS);
        world.y += MudDisplayHeight(world.xz);
        return world;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Assets/Game/Shaders/Common/GameLit.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionWS = MudVertexWS(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float Clumps(float2 xz)
            {
                return (MudValueNoise(xz * _ClumpScale + 3.7) - 0.5) * _ClumpHeight;
            }

            float SurfaceHeight(float2 xz)
            {
                return MudDisplayHeight(xz) + Clumps(xz);
            }

            float Ripples(float2 xz)
            {
                float t = _Time.y * _RippleSpeed;
                return MudValueNoise(xz * _RippleScale + float2(t, t * 0.6))
                     + MudValueNoise(xz * _RippleScale * 1.7 + float2(-t * 0.8, t * 1.1));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 xz = input.positionWS.xz;
                float2 e = max(_MudTexel.xy, 0.01);

                float relief = MudRelief(xz);
                float2 tracks = MudTracks(xz);
                float ground = MudGroundHeight(relief, tracks);
                float waterSurface = MudWaterSurface();

                // Water: a puddle where the mud is under its surface, or a rut dug deep enough.
                float puddle = MudPuddle(relief) * saturate((waterSurface - ground) / 0.004);
                float rutWater = smoothstep(_RutWater - 0.05, _RutWater + 0.05, tracks.x);
                float water = saturate(max(puddle, rutWater));

                // Mud normal from the drawn height, including clumps finer than the grid.
                float dx = (SurfaceHeight(xz + float2(e.x, 0)) - SurfaceHeight(xz - float2(e.x, 0))) / (2 * e.x);
                float dz = (SurfaceHeight(xz + float2(0, e.y)) - SurfaceHeight(xz - float2(0, e.y))) / (2 * e.y);
                float3 mudNormal = normalize(float3(-dx, 1, -dz));

                // Water normal: two drifting noise layers.
                const float r = 0.03;
                float rx = Ripples(xz + float2(r, 0)) - Ripples(xz - float2(r, 0));
                float rz = Ripples(xz + float2(0, r)) - Ripples(xz - float2(0, r));
                float3 waterNormal = normalize(float3(-rx * _RippleStrength / r, 1, -rz * _RippleStrength / r));

                // Wetness: patchy everywhere, wetter at puddle rims and in ruts, drier on the berms.
                float patches = MudFbm(xz * 0.6 + _Seed * 3.1 + 11.0);
                float rim = 1.0 - smoothstep(_PuddleLevel, _PuddleLevel + 0.012, relief);
                float wet = saturate(_Wetness + (patches - 0.5) * 0.8 + rim * 0.6 + tracks.x * 0.4 - tracks.y * 0.5);

                float tint = MudValueNoise(xz * 1.3 + 5.0);
                float3 mudColour = lerp(_MudDry.rgb, _MudWet.rgb, wet) * lerp(0.85, 1.1, tint);
                float mudSmooth = lerp(_DrySmoothness, _WetSmoothness, wet * wet);

                float depth = saturate((waterSurface - ground) / 0.06) * puddle + rutWater * 0.4;
                float3 waterColour = lerp(_WaterShallow.rgb, _WaterDeep.rgb, saturate(depth));

                GameSurface s = GameSurfaceDefault(input.positionWS, normalize(lerp(mudNormal, waterNormal, water)));
                s.albedo = lerp(mudColour, waterColour, water);
                s.smoothness = lerp(mudSmooth, _WaterSmoothness, water);
                s.metallic = 0;
                s.occlusion = lerp(1.0, 0.45, tracks.x * (1 - water));
                return GameLitFragment(s, input.positionCS, input.fogFactor);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Assets/Game/Shaders/Common/GameLit.hlsl"

            float4 Vert(Attributes input) : SV_POSITION
            {
                return GameShadowCasterPositionCS(MudVertexWS(input.positionOS.xyz), float3(0, 1, 0));
            }

            half4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            float4 Vert(Attributes input) : SV_POSITION
            {
                return TransformWorldToHClip(MudVertexWS(input.positionOS.xyz));
            }

            half Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Assets/Game/Shaders/Common/GameLit.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionWS = MudVertexWS(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Coarse normal of the drawn height: enough for SSAO.
                float2 xz = input.positionWS.xz;
                float2 e = max(_MudTexel.xy, 0.01);
                float dx = (MudDisplayHeight(xz + float2(e.x, 0)) - MudDisplayHeight(xz - float2(e.x, 0))) / (2 * e.x);
                float dz = (MudDisplayHeight(xz + float2(0, e.y)) - MudDisplayHeight(xz - float2(0, e.y))) / (2 * e.y);
                return GameDepthNormalsOutput(normalize(float3(-dx, 1, -dz)));
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
