// Rain drops and their splashes: unlit, see-through, one colour per instance (_InstColor,
// alpha included) times _Color. A touch of the scene's ambient light keeps them from glowing
// in the dark. Drawn only with DrawMeshInstanced (VoxelRain).
Shader "Game/VoxelRain"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _InstColor ("Instance Colour", Color) = (1, 1, 1, 1)
        _Ambient ("Ambient Share", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "Unlit"
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
                half _Ambient;
            CBUFFER_END

            #if defined(UNITY_INSTANCING_ENABLED)
            UNITY_INSTANCING_BUFFER_START(RainProps)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstColor)
            UNITY_INSTANCING_BUFFER_END(RainProps)
            #define RAIN_INSTANCE_COLOR UNITY_ACCESS_INSTANCED_PROP(RainProps, _InstColor)
            #else
            #define RAIN_INSTANCE_COLOR _InstColor
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half fogFactor : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 c = RAIN_INSTANCE_COLOR * _Color;
                half3 light = lerp(1.0, saturate(SampleSH(half3(0, 1, 0)) * 1.6), _Ambient);
                c.rgb *= light;
                c.rgb = MixFog(c.rgb, input.fogFactor);
                return c;
            }
            ENDHLSL
        }
    }
}
