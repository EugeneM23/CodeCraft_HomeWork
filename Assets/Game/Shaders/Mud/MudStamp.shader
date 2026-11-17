// Draws track stamps into the mud track map (see MudSurface). Each quad is one strip of track:
// R gets the rut, G the berm beside it, B the tread pattern.
//   Pass 0, Dig: added on. The ground the leading end of a track sweeps over gets one pass deeper.
//   Pass 1, Press: kept if deeper. Whatever a track stands on is at least one pass deep, at once.
//   Pass 2, Dent: added on. An oval bowl with a rim round it: craters, footprints.
//   Pass 3, Tread: replaced. Under a track the last pass's grooves overwrite B (0.5 a ridge, 1 a
//            groove; 0 where no track ever was). R only says how deep; the ground shader holds the
//            ridges up from B, so the tread shows in a rut however many passes have deepened it.
// The rut's edge and its berm wander with noise along the ground, so a rut is never ruler-straight.
Shader "Hidden/Game/MudStamp"
{
    CGINCLUDE
    #include "UnityCG.cginc"

    // Distance (m) between grooves across the track.
    #define TREAD_PITCH 0.15

    struct appdata
    {
        float4 vertex : POSITION;   // xy: position in the map's UV
        float2 local : TEXCOORD0;   // along the strip (m), across it (m)
        float4 data : TEXCOORD1;    // strip: half width, berm's outer edge, amount, world along
                                    // dent: half length, half width, amount, -
        float4 shape : TEXCOORD2;   // dent only: bowl exponent, rim share, roughness, seed
    };

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 local : TEXCOORD0;
        float4 data : TEXCOORD1;
        float4 shape : TEXCOORD2;
    };

    float Hash(float2 p)
    {
        p = frac(p * float2(123.34, 456.21));
        p += dot(p, p + 45.32);
        return frac(p.x * p.y);
    }

    float ValueNoise(float2 p)
    {
        float2 i = floor(p);
        float2 f = frac(p);
        float2 u = f * f * (3.0 - 2.0 * f);
        return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x),
                    lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
    }

    v2f vert(appdata v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(float4(v.vertex.xy, 0, 1));
        o.local = v.local;
        o.data = v.data;
        o.shape = v.shape;
        return o;
    }

    // Oval bowl: deepest in the middle, flatter-bottomed the higher the exponent (2 a crater, 4 a
    // footprint), and a rim of pushed-out mud just past its edge. Roughness (0..1) breaks it up,
    // differently for every seed: the edge wanders in and out round the bowl, the bottom is lumpy
    // and the rim is heaped higher in some places than others.
    float4 fragDent(v2f i) : SV_Target
    {
        float2 n = i.local / i.data.xy;
        float d = length(n);
        float rough = i.shape.z;
        float seed = i.shape.w;

        // Edge: a few lobes of different sizes, phase-shifted by the seed.
        float a = atan2(n.y, n.x);
        float wobble = sin(a * 3.0 + seed * 6.1) * 0.5
                     + sin(a * 5.0 + seed * 11.3) * 0.3
                     + sin(a * 9.0 + seed * 2.7) * 0.2;
        d *= 1.0 + rough * 0.35 * wobble;

        float bowl = saturate(1.0 - pow(d, i.shape.x));

        // Lumps in the bottom, on the dent's own scale.
        float lumps = ValueNoise(n * 3.0 + seed * 17.0) * 0.6 + ValueNoise(n * 7.0 - seed * 5.0) * 0.4;
        bowl *= 1.0 - rough * 0.45 * lumps;

        float outer = 1.0 + 2.0 * i.shape.y;
        float t = saturate((d - 0.85) / max(outer - 0.85, 1e-4));
        float rim = i.shape.y > 0 ? sin(t * UNITY_PI) * step(0.85, d) : 0;
        // Sampled round a circle rather than by angle: no seam where the angle wraps.
        float2 round = n / max(d, 1e-4);
        rim *= lerp(1.0, 0.3 + 1.4 * ValueNoise(round * 1.7 + seed * 9.0), rough);

        return float4(float2(bowl, rim * 0.6) * i.data.z, 0, 0);
    }

    // The track's half width here: it wanders in and out along the ground, each side on its own,
    // so the rut's edge is torn rather than ruled. Along runs with the world, not the tank.
    float WanderingHalf(v2f i)
    {
        float side = i.local.y >= 0 ? 5.3 : -2.9;
        float n = ValueNoise(float2(i.data.w * 4.0, side)) * 0.6 + ValueNoise(float2(i.data.w * 11.0, side * 1.7)) * 0.4;
        return i.data.x * (0.82 + 0.32 * n);
    }

    // Rut (x) and berm (y) of a strip at this point, before the amount.
    float2 Profile(v2f i)
    {
        float across = abs(i.local.y);
        float half = WanderingHalf(i);
        float outer = max(i.data.y, half * 1.2);

        // Full depth across the track, rounded off over its ragged edge.
        float rut = 1.0 - smoothstep(half * 0.78, half, across);

        // Berm: rises past the track's edge and falls off to the strip's edge, heaped unevenly.
        float t = saturate((across - half * 0.9) / max(outer - half * 0.9, 1e-4));
        float berm = sin(t * UNITY_PI) * step(half * 0.9, across);
        float side = i.local.y >= 0 ? 3.1 : 8.7;
        berm *= lerp(0.45, 1.45, ValueNoise(float2(i.data.w * 2.5, side)));

        return float2(rut, berm * 0.5);
    }

    float4 fragDig(v2f i) : SV_Target
    {
        return float4(Profile(i) * i.data.z, 0, 0);
    }

    float4 fragPress(v2f i) : SV_Target
    {
        return float4(Profile(i).x * i.data.z, 0, 0, 0);
    }

    // The grooves under the track, replacing whatever pattern was there. Grooves are fixed to the
    // ground: the phase runs with the world, not the tank.
    float4 fragTread(v2f i) : SV_Target
    {
        clip(WanderingHalf(i) * 0.8 - abs(i.local.y));
        float groove = step(frac(i.data.w / TREAD_PITCH), 0.5);
        return float4(0, 0, 0.5 + 0.5 * groove, 0);
    }
    ENDCG

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Dig"
            Blend One One
            ColorMask RG

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDig
            ENDCG
        }

        Pass
        {
            Name "Press"
            BlendOp Max
            Blend One One
            ColorMask RG

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragPress
            ENDCG
        }

        Pass
        {
            Name "Dent"
            Blend One One
            ColorMask RG

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDent
            ENDCG
        }

        Pass
        {
            Name "Tread"
            Blend Off
            ColorMask B

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragTread
            ENDCG
        }
    }
}
