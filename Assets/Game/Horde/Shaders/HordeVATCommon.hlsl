// Shared by every pass of Game/HordeVAT, Built-in and URP: no pipeline includes here.
// A crowd instance plays a vertex animation texture (VAT); a corpse instance (_HordeCorpse = 1)
// draws the static death-pose mesh from a ring buffer and sinks into the ground with age.
#ifndef HORDE_VAT_COMMON_INCLUDED
#define HORDE_VAT_COMMON_INCLUDED

#define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
#include "UnityIndirect.cginc"

// Matches Game.Horde.HordeInstance (32 bytes).
struct HordeInstance
{
    float4 positionHeading; // feet xyz, yaw (0 faces +Z)
    float4 animation;       // VAT row a, row b, blend; corpse: birth time
};

StructuredBuffer<HordeInstance> _HordeInstances;
Texture2D<float4> _HordeVat; // width = vertices; rows [0, frames) positions, [frames, 2 frames) normals
float _HordeVatFrames;
float _HordeScale;
float _HordeCorpse;
float _HordeRingStart;
float _HordeRingCapacity;
float _HordeTime;
float4 _HordeCorpseTimes; // lie, sink, depth

// World position and normal of a vertex of instance svInstanceId.
void HordeVertex(uint vertexId, uint svInstanceId, float3 meshPosition, float3 meshNormal,
    out float3 worldPosition, out float3 worldNormal)
{
    InitIndirectDrawArgs(0);
    uint id = GetIndirectInstanceID(svInstanceId);
    float3 p;
    float3 n;
    float sink = 0;

    if (_HordeCorpse > 0.5)
    {
        id = (id + (uint)_HordeRingStart) % (uint)max(_HordeRingCapacity, 1.0);
        HordeInstance corpse = _HordeInstances[id];
        p = meshPosition;
        n = meshNormal;
        float age = _HordeTime - corpse.animation.x;
        sink = saturate((age - _HordeCorpseTimes.x) / max(_HordeCorpseTimes.y, 0.001)) * _HordeCorpseTimes.z;
        float s0, c0;
        sincos(corpse.positionHeading.w, s0, c0);
        worldPosition = float3(c0 * p.x + s0 * p.z, p.y, -s0 * p.x + c0 * p.z) * _HordeScale
            + corpse.positionHeading.xyz - float3(0, sink, 0);
        worldNormal = normalize(float3(c0 * n.x + s0 * n.z, n.y, -s0 * n.x + c0 * n.z));
        return;
    }

    HordeInstance inst = _HordeInstances[id];
    int frames = (int)_HordeVatFrames;
    int3 a = int3((int)vertexId, (int)inst.animation.x, 0);
    int3 b = int3((int)vertexId, (int)inst.animation.y, 0);
    float t = inst.animation.z;
    p = lerp(_HordeVat.Load(a).xyz, _HordeVat.Load(b).xyz, t);
    n = lerp(_HordeVat.Load(a + int3(0, frames, 0)).xyz, _HordeVat.Load(b + int3(0, frames, 0)).xyz, t);

    float s, c;
    sincos(inst.positionHeading.w, s, c);
    worldPosition = float3(c * p.x + s * p.z, p.y, -s * p.x + c * p.z) * _HordeScale + inst.positionHeading.xyz;
    worldNormal = normalize(float3(c * n.x + s * n.z, n.y, -s * n.x + c * n.z));
}

// The instance's own tint seed, 0..1 (the user: a crowd of slightly different shades): the fraction of
// animation.w, under the kind in its whole part (HordeRenderJob). Corpses and spit carry none: 0.5, no shift.
float HordeTintSeed(uint svInstanceId)
{
    if (_HordeCorpse > 0.5)
        return 0.5;
    InitIndirectDrawArgs(0);
    uint id = GetIndirectInstanceID(svInstanceId);
    float f = frac(_HordeInstances[id].animation.w);
    return f < 0.005 ? 0.5 : saturate((f - 0.01) / 0.98);
}

float3 HordeRgbToHsv(float3 c)
{
    float4 k = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 p = c.g < c.b ? float4(c.bg, k.wz) : float4(c.gb, k.xy);
    float4 q = c.r < p.x ? float4(p.xyw, c.r) : float4(c.r, p.yzx);
    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

float3 HordeHsvToRgb(float3 c)
{
    float3 p = abs(frac(c.xxx + float3(1.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0);
    return c.z * lerp(float3(1, 1, 1), saturate(p - 1.0), c.y);
}

// The albedo shifted by a seed: hue by up to ±hueRange of the wheel, saturation by up to ±satRange of itself.
float3 HordeTint(float3 albedo, float seed, float hueRange, float satRange)
{
    float hueShift = (seed * 2.0 - 1.0) * hueRange;
    float satScale = 1.0 + (frac(seed * 7.31 + 0.37) * 2.0 - 1.0) * satRange;
    float3 hsv = HordeRgbToHsv(albedo);
    hsv.x = frac(hsv.x + hueShift);
    hsv.y = saturate(hsv.y * satScale);
    return HordeHsvToRgb(hsv);
}

#endif
