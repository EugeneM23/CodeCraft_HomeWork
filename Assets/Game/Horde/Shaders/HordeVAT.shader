// Horde crowd and corpses, drawn with Graphics.RenderMeshIndirect (Game.Horde.HordeRenderer).
// Vertex animation from a baked texture (Tools/Horde/Bake Regax VAT); per-instance data in
// _HordeInstances. Two SubShaders: URP (the game's pipeline) and Built-in (a fallback, unused while URP is on).
// URP: lit as URP Lit (UniversalFragmentPBR), so the crowd matches the skinned zombies (MT_Zombie).
// Built-in: one directional light, ambient, a little specular, emission.
Shader "Game/HordeVAT"
{
    Properties
    {
        _Color ("Color", Color) = (0.02, 0.2, 0.02, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        _EmissionMap ("Emission", 2D) = "black" {}
        _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _Gloss ("Gloss (Built-in pass)", Range(0, 1)) = 0.6
        _Specular ("Specular (Built-in pass)", Range(0, 1)) = 0.25
        _Smoothness ("Smoothness (URP Lit, as MT_Zombie)", Range(0, 1)) = 0.15
        _Ambient ("Ambient (URP pass)", Color) = (0.35, 0.38, 0.42, 1)
        [NoScaleOffset] _HordeVat ("VAT: positions, then normals", 2D) = "black" {}
        _HordeVatFrames ("VAT frames", Float) = 1
        _HordeScale ("Scale", Float) = 1
        _HueRange ("Hue range (+- share of the wheel, per zombie)", Range(0, 0.5)) = 0
        _SatRange ("Saturation range (+- share, per zombie)", Range(0, 1)) = 0.1
    }

    // ------------------------------------------------------------------ URP
    // Lit by the URP library as URP Lit: the main light with its shadows, additional lights, probes (SH), SSAO, fog.
    // A DepthNormals pass feeds screen-space AO.
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
            float _Smoothness;
            float4 _MainTex_ST;
            float _HueRange;
            float _SatRange;
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
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
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
                float tint : TEXCOORD4;
            };

            Varyings Vert(HordeAttributes v, uint instanceId : SV_InstanceID)
            {
                Varyings o;
                o.tint = HordeTintSeed(instanceId);
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

            // URP's own Lit model (UniversalFragmentPBR), as the skinned zombie's MT_Zombie (URP Lit, metallic 0,
            // smoothness _Smoothness): the main light and its shadows, the additional lights (Forward+), the light
            // probes, the reflection, screen-space AO, fog. Only the normal map and the lightmaps are not there.
            half4 Frag(Varyings i) : SV_Target
            {
                InputData input = (InputData)0;
                input.positionWS = i.positionWS;
                input.normalWS = NormalizeNormalPerPixel(i.normalWS);
                input.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                input.fogCoord = i.fogFactor;
                input.vertexLighting = half3(0, 0, 0);
                input.bakedGI = SampleSH(input.normalWS);
                input.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = HordeTint(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb * _Color.rgb, i.tint, _HueRange, _SatRange);
                surface.metallic = 0;
                surface.specular = half3(0, 0, 0);
                surface.smoothness = _Smoothness;
                surface.occlusion = 1;
                surface.emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb * _EmissionColor.rgb;
                surface.alpha = 1;
                surface.normalTS = half3(0, 0, 1);

                half4 color = UniversalFragmentPBR(input, surface);
                color.rgb = MixFog(color.rgb, input.fogCoord);
                color.a = 1;
                return color;
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

        // The normals screen-space AO reads (its source is DepthNormals): without it the crowd's pixels had the
        // ground's normals behind them.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct NormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            NormalsVaryings Vert(HordeAttributes v, uint instanceId : SV_InstanceID)
            {
                NormalsVaryings o;
                float3 wp;
                float3 wn;
                HordeVertex((uint)(v.uv1.x + 0.5), instanceId, v.vertex.xyz, v.normal, wp, wn);
                o.positionCS = TransformWorldToHClip(wp);
                o.normalWS = wn;
                return o;
            }

            half4 Frag(NormalsVaryings i) : SV_Target
            {
                float3 n = NormalizeNormalPerPixel(i.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 octahedral = PackNormalOctQuadEncode(n);
                return half4(PackFloat2To888(saturate(octahedral * 0.5 + 0.5)), 0.0);
            #else
                return half4(n, 0.0);
            #endif
            }
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
