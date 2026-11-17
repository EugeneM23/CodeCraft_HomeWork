// Voxels: chunk meshes carry their colour in the vertices, debris cubes in an instanced
// property. _Color tints both (the tank wreck darkens its skin through it).
// Alpha of the voxel colour is its dryness: 1 is dry, lower is wet (mud) — darker and glossy.
// URP: lit like URP's Lit (GameLit.hlsl). Material properties sit in UnityPerMaterial for the SRP
// Batcher; _InstColor moves to the instancing buffer in the instanced variant (debris and smoke
// cubes drawn with DrawMeshInstanced).
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

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        half4 _Color;
        #if !defined(UNITY_INSTANCING_ENABLED)
        float4 _InstColor;
        #endif
        half _Glossiness;
        half _Metallic;
        half _WetSmoothness;
        half _WetDarken;
        half _WetPatches;
        float _WetPatchScale;
        half _VoxelFlashReceive;
    CBUFFER_END

    #if defined(UNITY_INSTANCING_ENABLED)
    UNITY_INSTANCING_BUFFER_START(VoxelProps)
        UNITY_DEFINE_INSTANCED_PROP(float4, _InstColor)
    UNITY_INSTANCING_BUFFER_END(VoxelProps)
    #define VOXEL_INSTANCE_COLOR UNITY_ACCESS_INSTANCED_PROP(VoxelProps, _InstColor)
    #else
    #define VOXEL_INSTANCE_COLOR _InstColor
    #endif

    // A blast's flash, set globally (VoxelFlash): xyz position, w range; rgb linear colour ×
    // intensity, a > 0 while it burns. Lit here, in the base pass, as a point light without
    // shadows would light it: a real light would draw every chunk in its reach once more.
    float4 _VoxelFlashPos;
    float4 _VoxelFlashColor;

    struct Attributes
    {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
        half4 color : COLOR;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 200

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
            #pragma multi_compile_instancing
            // Debris and smoke cubes are scaled evenly: half the per-instance data, no inverse matrices.
            #pragma instancing_options assumeuniformscaling

            #include "Assets/Game/Shaders/Common/GameLit.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

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

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = position.positionCS;
                o.positionWS = position.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.color = input.color;
                o.fogFactor = ComputeFogFactor(position.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float4 instance = VOXEL_INSTANCE_COLOR;
                half3 normalWS = normalize(input.normalWS);
                GameSurface s = GameSurfaceDefault(input.positionWS, normalWS);

                float3 albedo = input.color.rgb * _Color.rgb * instance.rgb;
                float wet = saturate(1.0 - input.color.a * instance.a);
                s.smoothness = _Glossiness;
                if (wet > 0.0)
                {
                    // Water pools on top faces; sides drain and stay a little duller.
                    float up = saturate(normalWS.y);
                    float patches = Noise(input.positionWS * _WetPatchScale) * 0.65 + Noise(input.positionWS * _WetPatchScale * 3.1) * 0.35;
                    float sheen = wet * lerp(0.75, 1.0, up) * lerp(1.0, saturate(patches * 1.6 - 0.1), _WetPatches);
                    albedo *= 1.0 - _WetDarken * wet;
                    s.smoothness = lerp(_Glossiness, _WetSmoothness, saturate(sheen));
                }

                s.albedo = albedo;
                s.metallic = _Metallic;

                if (_VoxelFlashColor.a * _VoxelFlashReceive > 0.0)
                {
                    float3 toLight = _VoxelFlashPos.xyz - input.positionWS;
                    float d2 = dot(toLight, toLight);
                    float r2 = _VoxelFlashPos.w * _VoxelFlashPos.w;
                    if (d2 < r2)
                    {
                        // Built-in's point-light falloff, 1 / (1 + 25 d²/r²) faded out over the last
                        // fifth of r², which the flash was tuned against.
                        float x = d2 / r2;
                        float atten = saturate(5.0 * (1.0 - x)) / (1.0 + 25.0 * x);
                        float ndl = saturate(dot(normalWS, toLight * rsqrt(max(d2, 1e-4))));
                        s.emission = albedo * (1.0 - _Metallic) * _VoxelFlashColor.rgb * (atten * ndl);
                    }
                }

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
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Assets/Game/Shaders/Common/GameLit.hlsl"

            float4 Vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return GameShadowCasterPositionCS(TransformObjectToWorld(input.positionOS.xyz), TransformObjectToWorldNormal(input.normalOS));
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
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            float4 Vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
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
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            #include "Assets/Game/Shaders/Common/GameLit.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target { return GameDepthNormalsOutput(input.normalWS); }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
