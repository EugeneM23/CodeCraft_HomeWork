// Shared by everything that needs to know the shape of the mud: Game/MudGround draws it, later
// effects (splashes) can ask where the puddles are. All heights are metres above the floor
// collider's top; the deepest rut reaches 0, where the tank's tracks stand.
// URP: the mud's material properties live in UnityPerMaterial (SRP Batcher). A shader that adds
// properties of its own lists them in MUD_EXTRA_MATERIAL_PROPERTIES before including this file.
#ifndef GAME_MUD_COMMON_INCLUDED
#define GAME_MUD_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

#ifndef MUD_EXTRA_MATERIAL_PROPERTIES
#define MUD_EXTRA_MATERIAL_PROPERTIES
#endif

// R: rut, G: berm, B: tread (0 none, 0.5 ridge, 1 groove; MudSurface). Black when no tank has been
// here. Set per renderer with the rect and texel below (MudSurface's property block).
TEXTURE2D(_MudTracks);
SAMPLER(sampler_MudTracks);
float4 _MudTracksRect;     // xMin, zMin, 1/sizeX, 1/sizeZ
float4 _MudTexel;          // world size of one texel (x, z)

// How deep a tread's ridge sits, as a share of the rut it is in: the grooves go the full depth.
#define MUD_TREAD_RIDGE 0.7

CBUFFER_START(UnityPerMaterial)
    float _MudThickness;       // mud surface above the collider, rut-free and on flat relief
    float _ReliefDepth;        // relief swings this much up and down
    float _ReliefScale;        // relief noise frequency (1/m)
    float _ReliefContrast;     // >1 widens the relief so more of it reaches the puddle level
    float _BermHeight;
    float _PuddleLevel;        // relief below this is a puddle (m, negative)
    float _RutWater;           // rut depth (0..1) past which a rut holds water
    float _Seed;
    MUD_EXTRA_MATERIAL_PROPERTIES
CBUFFER_END

float MudHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float MudValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);

    float a = MudHash(i);
    float b = MudHash(i + float2(1, 0));
    float c = MudHash(i + float2(0, 1));
    float d = MudHash(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float MudFbm(float2 p)
{
    float sum = 0.0;
    float amplitude = 0.5;
    for (int octave = 0; octave < 3; octave++)
    {
        sum += MudValueNoise(p) * amplitude;
        p = p * 2.03 + 17.1;
        amplitude *= 0.5;
    }
    return sum / 0.875;
}

// Ground relief, -_ReliefDepth .. +_ReliefDepth.
float MudRelief(float2 xz)
{
    float n = MudFbm(xz * _ReliefScale + _Seed * 7.31);
    return clamp((n - 0.5) * 2.0 * _ReliefContrast, -1.0, 1.0) * _ReliefDepth;
}

// Rut and berm, 0..1 each. Where a track left its tread the ridges stand up out of the rut,
// however deep passes have dug it, so the pattern is always there.
float2 MudTracks(float2 xz)
{
    float2 uv = (xz - _MudTracksRect.xy) * _MudTracksRect.zw;
    float3 map = SAMPLE_TEXTURE2D_LOD(_MudTracks, sampler_MudTracks, uv, 0).rgb;
    float rut = saturate(map.r);
    float tread = saturate(map.b * 4.0 - 1.0);              // 0 off a track, 1 on its tread
    float groove = saturate((map.b - 0.5) * 2.0);           // 0 ridge, 1 groove
    rut *= lerp(1.0, lerp(MUD_TREAD_RIDGE, 1.0, groove), tread);
    return float2(rut, saturate(map.g));
}

// 1 inside a puddle, 0 outside, soft over a few millimetres of relief.
float MudPuddle(float relief)
{
    return 1.0 - smoothstep(_PuddleLevel - 0.003, _PuddleLevel + 0.003, relief);
}

float MudWaterSurface()
{
    return _MudThickness + _PuddleLevel;
}

// How much deeper the mud's bottom lies under a puddle (m): a full mud thickness more, so the mud
// there is twice as deep and a track sinks twice as far. Eased in over the puddle's first 6 mm.
// MudSurface.SinkAt works out the same on the CPU for the tank's wheels; keep the two alike.
float MudPuddleSink(float relief)
{
    return saturate((_PuddleLevel - relief) / 0.006) * _MudThickness;
}

// The mud itself. A rut takes the relief down with it, so a full rut is at the bottom: 0 on firm
// ground, below the collider under a puddle.
float MudGroundHeight(float relief, float2 tracks)
{
    float keep = 1.0 - tracks.x;
    return (_MudThickness + relief) * keep + _BermHeight * tracks.y * keep - MudPuddleSink(relief) * tracks.x;
}

// What is drawn: the mud, or a puddle's flat surface over it.
float MudDisplayHeight(float2 xz)
{
    float relief = MudRelief(xz);
    float ground = MudGroundHeight(relief, MudTracks(xz));
    float puddle = MudPuddle(relief);
    return lerp(ground, max(ground, MudWaterSurface()), puddle);
}

#endif
