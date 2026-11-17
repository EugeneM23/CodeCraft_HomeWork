// Horde crowd and corpses, drawn with Graphics.RenderMeshIndirect (Game.Horde.HordeRenderer).
// Vertex animation from a baked texture (Tools/Horde/Bake Regax VAT); per-instance data in
// _HordeInstances. Two SubShaders: URP first (the render team moves the game to it), Built-in second
// (what the game runs on today). Lighting is deliberately simple: one directional light, ambient,
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
    // TODO(URP): URP is not in the project yet, so this SubShader declares the few URP globals it
    // reads (unity_MatrixVP, _MainLightPosition, _MainLightColor, _LightDirection, _ShadowBias)
    // instead of including the URP ShaderLibrary: the file must compile without the package. Once
    // URP is merged, switch to Core.hlsl / Lighting.hlsl (GetMainLight, SH, shadow receiving) and
    // check the passes in the URP frame debugger.
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "HordeVATCommon.hlsl"

        float4x4 unity_MatrixVP;
        float3 _WorldSpaceCameraPos;

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

            float4 _MainLightPosition;
            float4 _MainLightColor;
            float4 _Color;
            float4 _EmissionColor;
            float4 _Ambient;
            float _Gloss;
            float _Specular;
            float4 _MainTex_ST;
            Texture2D _MainTex;
            SamplerState sampler_MainTex;
            Texture2D _EmissionMap;
            SamplerState sampler_EmissionMap;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings Vert(HordeAttributes v, uint instanceId : SV_InstanceID)
            {
                Varyings o;
                float3 wp;
                float3 wn;
                HordeVertex((uint)(v.uv1.x + 0.5), instanceId, v.vertex.xyz, v.normal, wp, wn);
                o.positionCS = mul(unity_MatrixVP, float4(wp, 1.0));
                o.uv = v.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                o.normalWS = wn;
                o.positionWS = wp;
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 l = normalize(_MainLightPosition.xyz);
                float3 albedo = _MainTex.Sample(sampler_MainTex, i.uv).rgb * _Color.rgb;
                float diffuse = saturate(dot(n, l));
                float3 h = normalize(l + normalize(_WorldSpaceCameraPos - i.positionWS));
                float spec = pow(saturate(dot(n, h)), exp2(10.0 * _Gloss + 1.0)) * _Specular;
                float3 emission = _EmissionMap.Sample(sampler_EmissionMap, i.uv).rgb * _EmissionColor.rgb;
                float3 color = albedo * (_Ambient.rgb + diffuse * _MainLightColor.rgb) + spec * _MainLightColor.rgb + emission;
                return float4(color, 1.0);
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

            float3 _LightDirection;
            float4 _ShadowBias;

            float4 Vert(HordeAttributes v, uint instanceId : SV_InstanceID) : SV_POSITION
            {
                float3 wp;
                float3 wn;
                HordeVertex((uint)(v.uv1.x + 0.5), instanceId, v.vertex.xyz, v.normal, wp, wn);
                wp += _LightDirection * _ShadowBias.x;
                wp += wn * (1.0 - saturate(dot(_LightDirection, wn))) * _ShadowBias.y;
                return mul(unity_MatrixVP, float4(wp, 1.0));
            }

            float4 Frag() : SV_Target { return 0; }
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
                return mul(unity_MatrixVP, float4(wp, 1.0));
            }

            float4 Frag() : SV_Target { return 0; }
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
