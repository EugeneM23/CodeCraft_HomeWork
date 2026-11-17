// Horde crowd and corpses, drawn with Graphics.RenderMeshIndirect (Game.Horde.HordeRenderer).
// Vertex animation from a baked texture (Tools/Horde/Bake Regax VAT); per-instance data in
// _HordeInstances. Two SubShaders: URP (the game's pipeline) and Built-in (a fallback, unused while URP is on).
// Lighting is deliberately simple: one directional light, ambient,
// a little specular, emission.
Shader "Game/HordeVAT"
{
    Properties
    {
        _Color ("Color", Color) = (0.02, 0.2, 0.02, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        _EmissionMap ("Emission", 2D) = "black" {}
        _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _Gloss ("Gloss", Range(0, 1)) = 0.6
        _Specular ("Specular", Range(0, 1)) = 0.25
        _Ambient ("Ambient (URP pass)", Color) = (0.35, 0.38, 0.42, 1)
        [NoScaleOffset] _HordeVat ("VAT: positions, then normals", 2D) = "black" {}
        _HordeVatFrames ("VAT frames", Float) = 1
        _HordeScale ("Scale", Float) = 1
    }

    // ------------------------------------------------------------------ URP
    // Lit by the URP library: the main light with its shadows, ambient from the light probes (SH), fog.
    // Additional lights are not taken: a crowd of thousands under one sun.
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "HordeVATCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float4 _EmissionColor;
            float4 _Ambient;
            float _Gloss;
            float _Specular;
            float4 _MainTex_ST;
        CBUFFER_END

        struct HordeAttributes
        {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            float2 uv : TEXCOORD0;
            float2 uv1 : TEXCOORD1;
        };
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings Vert(HordeAttributes v, uint instanceId : SV_InstanceID)
            {
                Varyings o;
                float3 wp;
                float3 wn;
                HordeVertex((uint)(v.uv1.x + 0.5), instanceId, v.vertex.xyz, v.normal, wp, wn);
                o.positionCS = TransformWorldToHClip(wp);
                o.uv = v.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                o.normalWS = wn;
                o.positionWS = wp;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                Light main = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb * _Color.rgb;
                float3 lit = main.color * (saturate(dot(n, main.direction)) * main.shadowAttenuation * main.distanceAttenuation);
                float3 h = normalize(main.direction + GetWorldSpaceNormalizeViewDir(i.positionWS));
                float spec = pow(saturate(dot(n, h)), exp2(10.0 * _Gloss + 1.0)) * _Specular * main.shadowAttenuation;
                float3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb * _EmissionColor.rgb;
                float3 color = albedo * (SampleSH(n) + lit) + spec * main.color + emission;
                color = MixFog(color, i.fogFactor);
                return half4(color, 1.0);
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
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            float4 Vert(HordeAttributes v, uint instanceId : SV_InstanceID) : SV_POSITION
            {
                float3 wp;
                float3 wn;
                HordeVertex((uint)(v.uv1.x + 0.5), instanceId, v.vertex.xyz, v.normal, wp, wn);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(wp, wn, _LightDirection));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
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
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            float4 Vert(HordeAttributes v, uint instanceId : SV_InstanceID) : SV_POSITION
            {
                float3 wp;
                float3 wn;
                HordeVertex((uint)(v.uv1.x + 0.5), instanceId, v.vertex.xyz, v.normal, wp, wn);
                return TransformWorldToHClip(wp);
            }

            half4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }

    // ------------------------------------------------------------------ Built-in
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "HordeVATCommon.hlsl"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _EmissionMap;
            fixed4 _Color;
            fixed4 _EmissionColor;
            half _Gloss;
            half _Specular;

            struct Attributes
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half3 ambient : TEXCOORD3;
                UNITY_FOG_COORDS(4)
            };

            Varyings Vert(Attributes v, uint instanceId : SV_InstanceID)
            {
                Varyings o;
                float3 wp;
                float3 wn;
                HordeVertex((uint)(v.uv1.x + 0.5), instanceId, v.vertex.xyz, v.normal, wp, wn);
                o.pos = UnityWorldToClipPos(wp);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normalWS = wn;
                o.positionWS = wp;
                o.ambient = ShadeSH9(float4(wn, 1.0));
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 Frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float3 albedo = tex2D(_MainTex, i.uv).rgb * _Color.rgb;
                float diffuse = saturate(dot(n, l));
                float3 h = normalize(l + normalize(_WorldSpaceCameraPos - i.positionWS));
                float spec = pow(saturate(dot(n, h)), exp2(10.0 * _Gloss + 1.0)) * _Specular;
                float3 emission = tex2D(_EmissionMap, i.uv).rgb * _EmissionColor.rgb;
                fixed4 color = fixed4(albedo * (i.ambient + diffuse * _LightColor0.rgb) + spec * _LightColor0.rgb + emission, 1.0);
                UNITY_APPLY_FOG(i.fogCoord, color);
                return color;
            }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #include "UnityCG.cginc"
            #include "HordeVATCommon.hlsl"

            struct Attributes
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv1 : TEXCOORD1;
            };

            float4 Vert(Attributes v, uint instanceId : SV_InstanceID) : SV_POSITION
            {
                float3 wp;
                float3 wn;
                HordeVertex((uint)(v.uv1.x + 0.5), instanceId, v.vertex.xyz, v.normal, wp, wn);
                // Normal offset as UnityClipSpaceShadowCasterPos does, in world space (no object matrix here).
                if (unity_LightShadowBias.z != 0.0)
                {
                    float3 toLight = normalize(UnityWorldSpaceLightDir(wp));
                    float shadowCos = dot(wn, toLight);
                    float shadowSine = sqrt(1.0 - shadowCos * shadowCos);
                    wp -= wn * unity_LightShadowBias.z * shadowSine;
                }

                return UnityApplyLinearShadowBias(UnityWorldToClipPos(wp));
            }

            float4 Frag() : SV_Target { return 0; }
            ENDCG
        }
    }
}
