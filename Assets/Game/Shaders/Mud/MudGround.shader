// Wet mud with puddles and the ruts MudSurface records. The mesh is a dense flat grid on the floor
// collider's top; every vertex is lifted to the mud's height (MudCommon.hlsl), so ruts are real
// dents and berms real ridges. Per-pixel normals come from the same height, which draws the tread
// grooves finer than the grid. Below the water level the mud is a puddle: flat, glossy, rippling,
// darker where it is deeper.
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

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow fullforwardshadows
        #pragma target 3.5

        #include "MudCommon.hlsl"

        float4 _MudDry;
        float4 _MudWet;
        float _Wetness;
        float _DrySmoothness;
        float _WetSmoothness;
        float _ClumpScale;
        float _ClumpHeight;

        float4 _WaterShallow;
        float4 _WaterDeep;
        float _WaterSmoothness;
        float _RippleScale;
        float _RippleSpeed;
        float _RippleStrength;

        struct Input
        {
            float3 worldPos;
        };

        void vert(inout appdata_full v)
        {
            float3 world = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)).xyz;
            world.y += MudDisplayHeight(world.xz);
            v.vertex.xyz = mul(unity_WorldToObject, float4(world, 1)).xyz;

            // Flat grid: tangent space is world X, world Z, world Y (see surf).
            v.normal = float3(0, 1, 0);
            v.tangent = float4(1, 0, 0, -1);
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

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 xz = IN.worldPos.xz;
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
            float3 mudNormal = normalize(float3(-dx, -dz, 1));

            // Water normal: two drifting noise layers.
            const float r = 0.03;
            float rx = Ripples(xz + float2(r, 0)) - Ripples(xz - float2(r, 0));
            float rz = Ripples(xz + float2(0, r)) - Ripples(xz - float2(0, r));
            float3 waterNormal = normalize(float3(-rx * _RippleStrength / r, -rz * _RippleStrength / r, 1));

            // Wetness: patchy everywhere, wetter at puddle rims and in ruts, drier on the berms.
            float patches = MudFbm(xz * 0.6 + _Seed * 3.1 + 11.0);
            float rim = 1.0 - smoothstep(_PuddleLevel, _PuddleLevel + 0.012, relief);
            float wet = saturate(_Wetness + (patches - 0.5) * 0.8 + rim * 0.6 + tracks.x * 0.4 - tracks.y * 0.5);

            float tint = MudValueNoise(xz * 1.3 + 5.0);
            float3 mudColour = lerp(_MudDry.rgb, _MudWet.rgb, wet) * lerp(0.85, 1.1, tint);
            float mudSmooth = lerp(_DrySmoothness, _WetSmoothness, wet * wet);

            float depth = saturate((waterSurface - ground) / 0.06) * puddle + rutWater * 0.4;
            float3 waterColour = lerp(_WaterShallow.rgb, _WaterDeep.rgb, saturate(depth));

            o.Albedo = lerp(mudColour, waterColour, water);
            o.Smoothness = lerp(mudSmooth, _WaterSmoothness, water);
            o.Metallic = 0;
            o.Normal = normalize(lerp(mudNormal, waterNormal, water));
            o.Occlusion = lerp(1.0, 0.45, tracks.x * (1 - water));
        }
        ENDCG
    }

    FallBack "Diffuse"
}
