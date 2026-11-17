// Experiment: see-through smoke cubes (the cannon's voxel smoke). One colour per instance
// (_InstColor, alpha included); faces lit softly by the main light and the ambient, so the
// cubes keep their shape. Colours above 1 bloom (the hot cubes of the blast).
// Drawn only with DrawMeshInstanced (TankVoxelMuzzleSmokeBehaviour).
Shader "Game/VoxelPuff"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _InstColor ("Instance Colour", Color) = (1, 1, 1, 1)
        _LightShare ("Main Light Share", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "Puff"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                #if !defined(UNITY_INSTANCING_ENABLED)
                float4 _InstColor;
                #endif
                half _LightShare;
            CBUFFER_END

            #if defined(UNITY_INSTANCING_ENABLED)
            UNITY_INSTANCING_BUFFER_START(PuffProps)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstColor)
            UNITY_INSTANCING_BUFFER_END(PuffProps)
            #define PUFF_INSTANCE_COLOR UNITY_ACCESS_INSTANCED_PROP(PuffProps, _InstColor)
            #else
            #define PUFF_INSTANCE_COLOR _InstColor
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 c = PUFF_INSTANCE_COLOR * _Color;
                half3 n = normalize(input.normalWS);
                Light sun = GetMainLight();
                // Wrapped lighting: smoke scatters light, its shadowed side is not black.
                half wrap = saturate(dot(n, sun.direction) * 0.5 + 0.5);
                half3 ambient = SampleSH(n);
                half3 light = ambient + sun.color * wrap * _LightShare;
                c.rgb *= light;
                c.rgb = MixFog(c.rgb, input.fogFactor);
                return c;
            }
            ENDHLSL
        }
    }
}
