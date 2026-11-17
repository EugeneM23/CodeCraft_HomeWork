// Standard-lit solid colour that mud can cake onto. TankDirtBehaviour drives it per renderer through
// a property block:
//   _Dirt       0..1, how many and how big the mud spots are (low parts get more);
//   _DirtWet    0..1, fresh and glossy (1) to dried and dull (0);
//   _DirtFrame  object space -> the vehicle's own space, so the dirt pattern sticks to the vehicle
//               and every part of it shares one pattern;
//   _DirtLow/_DirtHigh  the vehicle's bottom and top in that space.
// The mud stands proud of the paint: its thickness is bumped into the normal, so spots catch the
// light on their lips and lumps. With _Dirt at 0 (the default) it is a plain Standard material.
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

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard vertex:vert fullforwardshadows
        #pragma target 3.5

        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness;
        half _Metallic;

        fixed4 _DirtWetColor;
        fixed4 _DirtDryColor;
        float _DirtScale;
        float _Dirt;
        float _DirtWet;
        float _DirtLow;
        float _DirtHigh;
        float4x4 _DirtFrame;

        // How thick (m) the mud stands proud of the paint at its thickest, for the bump.
        #define MUD_RELIEF 0.022
        // Step (m, vehicle space) the bump's slope is measured over.
        #define MUD_STEP 0.008

        struct Input
        {
            float2 uv_MainTex;
            float3 dirtPos;
            // The surface's own frame in vehicle space, to turn the bump into a tangent-space normal.
            float3 dirtTangent;
            float3 dirtBinormal;
            float3 dirtNormal;
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

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.dirtPos = mul(_DirtFrame, float4(v.vertex.xyz, 1.0)).xyz;

            float3 normal = normalize(mul((float3x3)_DirtFrame, v.normal));
            float3 tangent = normalize(mul((float3x3)_DirtFrame, v.tangent.xyz));
            o.dirtNormal = normal;
            o.dirtTangent = tangent;
            o.dirtBinormal = cross(normal, tangent) * v.tangent.w;
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

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 base = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = base.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;

            if (_Dirt <= 0.001)
                return;

            float dirt, n;
            float thick = DirtField(IN.dirtPos, dirt, n);
            if (dirt <= 0.001)
                return;

            fixed3 mud = lerp(_DirtDryColor.rgb, _DirtWetColor.rgb, _DirtWet) * lerp(0.85, 1.1, n);
            // Thin mud at a spot's edge lets the dark through: it reads as a lip, not a decal.
            mud *= lerp(0.8, 1.0, saturate(thick * 2.5));

            o.Albedo = lerp(base.rgb, mud, dirt);
            o.Metallic = lerp(_Metallic, 0, dirt);
            o.Smoothness = lerp(_Glossiness, lerp(0.1, 0.38, _DirtWet), dirt);

            // Bump: the slope of the mud's thickness, measured in vehicle space and kept to the
            // surface, tilts the normal; then into the surface's tangent frame.
            float3 N = normalize(IN.dirtNormal);
            float3 T = normalize(IN.dirtTangent);
            float3 B = normalize(IN.dirtBinormal);
            float3 grad = float3(
                DirtThickness(IN.dirtPos + float3(MUD_STEP, 0, 0)) - DirtThickness(IN.dirtPos - float3(MUD_STEP, 0, 0)),
                DirtThickness(IN.dirtPos + float3(0, MUD_STEP, 0)) - DirtThickness(IN.dirtPos - float3(0, MUD_STEP, 0)),
                DirtThickness(IN.dirtPos + float3(0, 0, MUD_STEP)) - DirtThickness(IN.dirtPos - float3(0, 0, MUD_STEP)))
                * (MUD_RELIEF / (2.0 * MUD_STEP));
            grad -= N * dot(grad, N);
            float3 bumped = normalize(N - grad);
            o.Normal = normalize(float3(dot(bumped, T), dot(bumped, B), dot(bumped, N)));
        }
        ENDCG
    }

    FallBack "Standard"
}
