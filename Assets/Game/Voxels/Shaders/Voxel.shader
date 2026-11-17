// Voxels: chunk meshes carry their colour in the vertices, debris cubes in an instanced
// property. _Color tints both (the tank wreck darkens its skin through it).
// Alpha of the voxel colour is its dryness: 1 is dry, lower is wet (mud) — darker and glossy.
Shader "Game/Voxel"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _InstColor ("Instance Colour", Color) = (1, 1, 1, 1)
        _Glossiness ("Smoothness", Range(0, 1)) = 0.12
        _Metallic ("Metallic", Range(0, 1)) = 0
        _WetSmoothness ("Wet Smoothness", Range(0, 1)) = 0.65
        _WetDarken ("Wet Darkening", Range(0, 1)) = 0.4
        _WetPatches ("Wet Patch Contrast", Range(0, 1)) = 0.35
        _WetPatchScale ("Wet Patch Scale", Float) = 0.6
        // 0 on renderers a real flash Light already reaches (VoxelFlash.LitByLight).
        [HideInInspector] _VoxelFlashReceive ("Blast Flash", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma multi_compile_instancing
        // Debris and smoke cubes are scaled evenly: half the per-instance data, no inverse matrices.
        #pragma instancing_options assumeuniformscaling
        #pragma target 3.5

        struct Input
        {
            float4 color : COLOR;
            float3 worldPos;
            float3 worldNormal;
        };

        fixed4 _Color;

        // A blast's flash, set globally (VoxelFlash): xyz position, w range; rgb linear colour ×
        // intensity, a > 0 while it burns. Lit here, in the base pass, as a point light without
        // shadows would light it: a real light would draw every chunk in its reach once more.
        float4 _VoxelFlashPos;
        float4 _VoxelFlashColor;
        half _VoxelFlashReceive;

        half _Glossiness;
        half _Metallic;
        half _WetSmoothness;
        half _WetDarken;
        half _WetPatches;
        float _WetPatchScale;

        float Hash(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        // Smooth value noise: puddles and drier crusts across the mud, not one even sheen.
        float Noise(float3 x)
        {
            float3 i = floor(x);
            float3 f = frac(x);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x),
                             lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                        lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                             lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
        }

        UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(float4, _InstColor)
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float4 instance = UNITY_ACCESS_INSTANCED_PROP(Props, _InstColor);
            float3 albedo = IN.color.rgb * _Color.rgb * instance.rgb;

            float wet = saturate(1.0 - IN.color.a * instance.a);
            if (wet > 0.0)
            {
                // Water pools on top faces; sides drain and stay a little duller.
                float up = saturate(normalize(IN.worldNormal).y);
                float patches = Noise(IN.worldPos * _WetPatchScale) * 0.65 + Noise(IN.worldPos * _WetPatchScale * 3.1) * 0.35;
                float sheen = wet * lerp(0.75, 1.0, up) * lerp(1.0, saturate(patches * 1.6 - 0.1), _WetPatches);
                albedo *= 1.0 - _WetDarken * wet;
                o.Smoothness = lerp(_Glossiness, _WetSmoothness, saturate(sheen));
            }
            else
            {
                o.Smoothness = _Glossiness;
            }

            o.Albedo = albedo;
            o.Metallic = _Metallic;
            o.Alpha = 1;

            if (_VoxelFlashColor.a * _VoxelFlashReceive > 0.0)
            {
                float3 toLight = _VoxelFlashPos.xyz - IN.worldPos;
                float d2 = dot(toLight, toLight);
                float r2 = _VoxelFlashPos.w * _VoxelFlashPos.w;
                if (d2 < r2)
                {
                    // Unity's point-light falloff: 1 / (1 + 25 d²/r²), faded out over the last fifth of r².
                    float x = d2 / r2;
                    float atten = saturate(5.0 * (1.0 - x)) / (1.0 + 25.0 * x);
                    float ndl = saturate(dot(normalize(IN.worldNormal), toLight * rsqrt(max(d2, 1e-4))));
                    o.Emission = albedo * (1.0 - _Metallic) * _VoxelFlashColor.rgb * (atten * ndl);
                }
            }
        }
        ENDCG
    }

    FallBack "Diffuse"
}
