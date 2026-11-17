// Lit solid colour that mud can cake onto. TankDirtBehaviour drives it per renderer through
// a property block:
//   _Dirt       0..1, how many and how big the mud spots are (low parts get more);
//   _DirtWet    0..1, fresh and glossy (1) to dried and dull (0);
//   _DirtFrame  object space -> the vehicle's own space, so the dirt pattern sticks to the vehicle
//               and every part of it shares one pattern;
//   _DirtLow/_DirtHigh  the vehicle's bottom and top in that space.
// The mud stands proud of the paint: its thickness is bumped into the normal, so spots catch the
// light on their lips and lumps. With _Dirt at 0 (the default) it is a plain lit material.
// URP: lit like URP's Lit (GameLit.hlsl); properties in UnityPerMaterial for the SRP Batcher.
Shader "Game/TankDirt"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0, 1)) = 0.3
        _Metallic ("Metallic", Range(0, 1)) = 0.25

        [Header(Dirt)]
        _DirtWetColor ("Wet Mud", Color) = (0.17, 0.11, 0.06, 1)
        _DirtDryColor ("Dry Mud", Color) = (0.40, 0.31, 0.21, 1)
        _DirtScale ("Spot Scale (1/m)", Float) = 3.5
        _Dirt ("Dirt", Range(0, 1)) = 0
        _DirtWet ("Wetness", Range(0, 1)) = 0
        _DirtLow ("Bottom (vehicle space)", Float) = 0
        _DirtHigh ("Top (vehicle space)", Float) = 2
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);

    CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_ST;
        half4 _Color;
        half _Glossiness;
        half _Metallic;
        half4 _DirtWetColor;
        half4 _DirtDryColor;
        float _DirtScale;
        float _Dirt;
        float _DirtWet;
        float _DirtLow;
        float _DirtHigh;
    CBUFFER_END

    // Set only through property blocks (TankDirtBehaviour); not a material property.
    float4x4 _DirtFrame;

    struct Attributes
    {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
        float4 tangentOS : TANGENT;
        float2 uv : TEXCOORD0;
    };
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
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

            #include "Assets/Game/Shaders/Common/GameLit.hlsl"

            // How thick (m) the mud stands proud of the paint at its thickest, for the bump.
            #define MUD_RELIEF 0.022
            // Step (m, vehicle space) the bump's slope is measured over.
            #define MUD_STEP 0.008

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                // World tangent frame of the mesh: the bump goes through it, as a tangent-space
                // normal did in the surface shader this replaced.
                half3 normalWS : TEXCOORD3;
                half4 tangentWS : TEXCOORD4;
                float3 dirtPos : TEXCOORD5;
                // The surface's own frame in vehicle space, to turn the bump into a tangent-space normal.
                float3 dirtTangent : TEXCOORD6;
                float3 dirtBinormal : TEXCOORD7;
                float3 dirtNormal : TEXCOORD8;
            };

            float DirtHash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float DirtNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(DirtHash(i), DirtHash(i + float3(1, 0, 0)), u.x),
                         lerp(DirtHash(i + float3(0, 1, 0)), DirtHash(i + float3(1, 1, 0)), u.x), u.y),
                    lerp(lerp(DirtHash(i + float3(0, 0, 1)), DirtHash(i + float3(1, 0, 1)), u.x),
                         lerp(DirtHash(i + float3(0, 1, 1)), DirtHash(i + float3(1, 1, 1)), u.x), u.y),
                    u.z);
            }

            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                o.positionCS = position.positionCS;
                o.positionWS = position.positionWS;
                o.uv = TRANSFORM_TEX(input.uv, _MainTex);
                o.fogFactor = ComputeFogFactor(position.positionCS.z);
                o.normalWS = normals.normalWS;
                o.tangentWS = half4(normals.tangentWS, input.tangentOS.w * GetOddNegativeScale());

                o.dirtPos = mul(_DirtFrame, float4(input.positionOS.xyz, 1.0)).xyz;
                float3 normal = normalize(mul((float3x3)_DirtFrame, input.normalOS));
                float3 tangent = normalize(mul((float3x3)_DirtFrame, input.tangentOS.xyz));
                o.dirtNormal = normal;
                o.dirtTangent = tangent;
                o.dirtBinormal = cross(normal, tangent) * input.tangentOS.w;
                return o;
            }

            // The mud at a point in vehicle space: how much of it covers the paint (0..1), the large
            // noise that shaped it (for its tint), and how thick it stands (0..1).
            //
            // Spots of mud, never a coat: patches where the noise peaks, more and bigger the dirtier
            // the tank and the lower the part. Even fully dirty, the top keeps only a few. A spot is
            // thickest inside, thinning to its edge, and lumpy all over.
            float DirtField(float3 dirtPos, out float cover, out float shape)
            {
                float3 p = dirtPos * _DirtScale;
                shape = DirtNoise(p) * 0.55 + DirtNoise(p * 2.3 + 11.3) * 0.3 + DirtNoise(p * 6.1 + 5.1) * 0.15;
                float height = saturate((dirtPos.y - _DirtLow) / max(_DirtHigh - _DirtLow, 0.001));
                float low = pow(1.0 - height, 1.5);

                float threshold = 0.72 - _Dirt * lerp(0.16, 0.5, low);
                float spots = smoothstep(threshold, threshold + 0.03, shape);
                // Thicker the further inside the spot: a lip at its edge, a heap in its middle.
                float body = smoothstep(threshold, threshold + 0.12, shape);

                // Small flecks thrown about between the patches: thin.
                float fleck = smoothstep(0.84, 0.86, DirtNoise(p * 9.0 + 2.9) + _Dirt * 0.16 * low)
                            * saturate(_Dirt * 3.0);

                cover = max(spots, fleck);
                float lumps = DirtNoise(p * 14.0 + 7.7);
                return max(spots * (0.3 + 0.7 * body), fleck * 0.5) * (0.4 + 0.6 * lumps);
            }

            float DirtThickness(float3 dirtPos)
            {
                float cover, shape;
                return DirtField(dirtPos, cover, shape);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 base = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                half3 normalWS = normalize(input.normalWS);
                GameSurface s = GameSurfaceDefault(input.positionWS, normalWS);
                s.albedo = base.rgb;
                s.metallic = _Metallic;
                s.smoothness = _Glossiness;

                if (_Dirt > 0.001)
                {
                    float dirt, n;
                    float thick = DirtField(input.dirtPos, dirt, n);
                    if (dirt > 0.001)
                    {
                        half3 mud = lerp(_DirtDryColor.rgb, _DirtWetColor.rgb, _DirtWet) * lerp(0.85, 1.1, n);
                        // Thin mud at a spot's edge lets the dark through: it reads as a lip, not a decal.
                        mud *= lerp(0.8, 1.0, saturate(thick * 2.5));

                        s.albedo = lerp(base.rgb, mud, dirt);
                        s.metallic = lerp(_Metallic, 0, dirt);
                        s.smoothness = lerp(_Glossiness, lerp(0.1, 0.38, _DirtWet), dirt);

                        // Bump: the slope of the mud's thickness, measured in vehicle space and kept to
                        // the surface, tilts the normal; then into the surface's tangent frame, and from
                        // there into world space through the mesh's own tangent frame.
                        float3 N = normalize(input.dirtNormal);
                        float3 T = normalize(input.dirtTangent);
                        float3 B = normalize(input.dirtBinormal);
                        float3 grad = float3(
                            DirtThickness(input.dirtPos + float3(MUD_STEP, 0, 0)) - DirtThickness(input.dirtPos - float3(MUD_STEP, 0, 0)),
                            DirtThickness(input.dirtPos + float3(0, MUD_STEP, 0)) - DirtThickness(input.dirtPos - float3(0, MUD_STEP, 0)),
                            DirtThickness(input.dirtPos + float3(0, 0, MUD_STEP)) - DirtThickness(input.dirtPos - float3(0, 0, MUD_STEP)))
                            * (MUD_RELIEF / (2.0 * MUD_STEP));
                        grad -= N * dot(grad, N);
                        float3 bumped = normalize(N - grad);
                        float3 normalTS = normalize(float3(dot(bumped, T), dot(bumped, B), dot(bumped, N)));

                        float3 bitangentWS = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                        s.normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS));
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
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Assets/Game/Shaders/Common/GameLit.hlsl"

            float4 Vert(Attributes input) : SV_POSITION
            {
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

            float4 Vert(Attributes input) : SV_POSITION
            {
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

            #include "Assets/Game/Shaders/Common/GameLit.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
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
