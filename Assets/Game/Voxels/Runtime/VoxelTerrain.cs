using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>What the ground is made of on top, at a point.</summary>
    public enum TerrainSoil
    {
        Mud,
        Grass,
        DryGrass,
        Dirt,
        Rock,

        /// <summary>A street: cobbled road, kerb or flagged pavement. No prints, no mud.</summary>
        Paved
    }

    /// <summary>The rolling ground's shape: one noise field over the whole map, in world metres.</summary>
    [Serializable]
    public struct TerrainShape
    {
        [Tooltip("World height (m) of the lowest valleys.")]
        public float Base;

        [Tooltip("Height (m) from the lowest valley to the highest hilltop.")]
        public float Amplitude;

        [Tooltip("Hills per metre: 0.03 — a hill every 30 m or so.")]
        public float Frequency;

        [Tooltip("Shifts the noise: another map from the same settings.")]
        public Vector2 Offset;

        public static TerrainShape Default => new() { Base = 0f, Amplitude = 6f, Frequency = 0.018f, Offset = new Vector2(17.3f, -4.1f) };
    }

    /// <summary>
    /// The ground's height anywhere on the map: a few octaves of simplex noise, levelled to flat
    /// pads where buildings and the tank stand. The same code runs in the Burst job that fills the
    /// ground tiles and in the editor that places the town, so everything sits on the surface.
    /// </summary>
    public static class VoxelTerrain
    {
        /// <summary>Metres over which a pad blends back into the hills.</summary>
        public const float PAD_BLEND = 4f;

        /// <summary>The ground without pads.</summary>
        public static float RawHeight(float2 xz, in TerrainShape shape)
        {
            float2 p = xz * shape.Frequency + (float2)(Vector2)shape.Offset;
            // Broad hills with a little roll on them; the finest octave is kept faint, so slopes run smooth.
            float n = noise.snoise(p) * 0.74f + noise.snoise(p * 2.1f + 31.7f) * 0.21f + noise.snoise(p * 4.3f - 12.9f) * 0.05f;
            return shape.Base + shape.Amplitude * math.saturate(0.5f + 0.6f * n);
        }

        /// <summary>The ground with its pads: (x, z, radius, level) each.</summary>
        public static float Height(float2 xz, in TerrainShape shape, NativeArray<float4> pads)
        {
            float h = RawHeight(xz, shape);
            for (int i = 0; i < pads.Length; i++)
                h = Level(h, xz, pads[i]);
            return h;
        }

        /// <summary>The same, from managed code (the editor, the ruts).</summary>
        public static float Height(Vector2 xz, in TerrainShape shape, Vector4[] pads)
        {
            float h = RawHeight(xz, shape);
            if (pads != null)
                foreach (Vector4 pad in pads)
                    h = Level(h, xz, pad);
            return h;
        }

        /// <summary>How steep the ground is (rise over run) at a point, with its pads.</summary>
        public static float Slope(float2 xz, in TerrainShape shape, NativeArray<float4> pads)
        {
            const float step = 0.5f;
            float dx = Height(xz + new float2(step, 0f), shape, pads) - Height(xz - new float2(step, 0f), shape, pads);
            float dz = Height(xz + new float2(0f, step), shape, pads) - Height(xz - new float2(0f, step), shape, pads);
            return math.length(new float2(dx, dz)) / (2f * step);
        }

        /// <summary>The same, from managed code.</summary>
        public static float Slope(Vector2 xz, in TerrainShape shape, Vector4[] pads)
        {
            const float step = 0.5f;
            float dx = Height(xz + new Vector2(step, 0f), shape, pads) - Height(xz - new Vector2(step, 0f), shape, pads);
            float dz = Height(xz + new Vector2(0f, step), shape, pads) - Height(xz - new Vector2(0f, step), shape, pads);
            return math.length(new float2(dx, dz)) / (2f * step);
        }

        /// <summary>What lies on top of the ground at a point (Burst: the generator).</summary>
        public static TerrainSoil Soil(float2 xz, in TerrainShape shape, NativeArray<float4> pads) =>
            SoilFrom(xz, Height(xz, shape, pads), Slope(xz, shape, pads), shape);

        /// <summary>The same, from managed code (the tank's tracks).</summary>
        public static TerrainSoil Soil(Vector2 xz, in TerrainShape shape, Vector4[] pads) =>
            SoilFrom(xz, Height(xz, shape, pads), Slope(xz, shape, pads), shape);

        /// <summary>
        /// The soil rules: rock on the steepest slopes, bare earth on steep ones; mud in wide
        /// fields — the low ground and big patches across the gentle slopes, big enough to drive a
        /// tank through; dry grass on the hilltops; grass elsewhere. Ragged borders, not contours.
        /// </summary>
        public static TerrainSoil SoilFrom(float2 xz, float height, float slope, in TerrainShape shape)
        {
            // Flat ground (no amplitude) counts as middling: grass with fields of mud, no hilltops.
            float level = shape.Amplitude > 0.01f ? (height - shape.Base) / shape.Amplitude : 0.5f;
            float jitter = noise.snoise(xz * 0.25f) * 0.08f;

            if (slope > 0.75f + jitter)
                return TerrainSoil.Rock;
            if (slope > 0.42f + jitter)
                return TerrainSoil.Dirt;

            if (MudAmount(xz, height, slope, shape) >= 0.5f)
                return TerrainSoil.Mud;
            if (level > 0.76f + jitter)
                return TerrainSoil.DryGrass;
            return TerrainSoil.Grass;
        }

        /// <summary>
        /// How much of a mud field a point is in: 0 — none, 1 — well inside, rising through a band
        /// a few metres wide at the edge. A point counts as mud from 0.5; across the band the
        /// ground is a scatter of mud and grass (see the generator), not a hard line.
        /// </summary>
        public static float MudAmount(float2 xz, float height, float slope, in TerrainShape shape)
        {
            if (slope > 0.42f)
                return 0f;

            // Fields: a slow noise (a patch every 40–60 m) with a rough edge.
            float fields = noise.snoise(xz * 0.022f + 7.3f) + 0.25f * noise.snoise(xz * 0.09f - 2.1f);
            float byFields = math.saturate((fields - 0.38f) / 0.2f + 0.5f) * math.saturate((0.3f - slope) / 0.1f + 0.5f);

            // And the low ground, on hills.
            if (shape.Amplitude <= 0.01f)
                return byFields;
            float level = (height - shape.Base) / shape.Amplitude;
            float jitter = noise.snoise(xz * 0.25f) * 0.08f;
            float byLevel = math.saturate((0.24f + jitter - level) / 0.08f + 0.5f);
            return math.max(byFields, byLevel);
        }

        /// <summary>
        /// Grass is not one green: lush, plain and pale in patches of a few metres, with the
        /// borders dithered voxel by voxel. -1, 0 or 1.
        /// </summary>
        public const int STREET_NONE = 0;
        public const int STREET_ROAD = 1;
        public const int STREET_KERB = 2;
        /// <summary>The strip beyond the kerb where the road frays into the ground round it.</summary>
        public const int STREET_VERGE = 3;

        /// <summary>Width (m) of a kerb.</summary>
        public const float KERB = 0.25f;

        /// <summary>
        /// Which part of a street a point is on — the cobbled road along each segment
        /// (x0, z0, x1, z1), the kerb at its edge, the verge beyond — or none. Also gives the
        /// point's place along and across the nearest street and its distance from the centre line
        /// (m), for laying the stones and fraying the edge.
        /// </summary>
        public static int StreetZone(float2 xz, NativeArray<float4> streets, float roadHalf, float verge,
            out float along, out float across, out float distance)
        {
            along = across = 0f;
            float best = float.MaxValue;
            for (int i = 0; i < streets.Length; i++)
                Nearest(xz, streets[i], ref best, ref along, ref across);
            distance = best;
            return Zone(best, roadHalf, verge);
        }

        /// <summary>The same, from managed code.</summary>
        public static int StreetZone(Vector2 xz, Vector4[] streets, float roadHalf, float verge)
        {
            if (streets == null)
                return STREET_NONE;
            float along = 0f, across = 0f, best = float.MaxValue;
            foreach (Vector4 street in streets)
                Nearest(xz, street, ref best, ref along, ref across);
            return Zone(best, roadHalf, verge);
        }

        private static void Nearest(float2 xz, float4 street, ref float best, ref float along, ref float across)
        {
            float2 a = street.xy;
            float2 ab = street.zw - a;
            float length = math.length(ab);
            if (length < 0.01f)
                return;
            float2 dir = ab / length;
            float t = math.clamp(math.dot(xz - a, dir), 0f, length);
            float d = math.distance(xz, a + dir * t);
            if (d >= best)
                return;
            best = d;
            along = math.dot(xz - a, dir);
            across = math.dot(xz - a, new float2(-dir.y, dir.x));
        }

        private static int Zone(float distance, float roadHalf, float verge)
        {
            if (distance <= roadHalf)
                return STREET_ROAD;
            if (distance <= roadHalf + KERB)
                return STREET_KERB;
            if (distance <= roadHalf + KERB + verge)
                return STREET_VERGE;
            return STREET_NONE;
        }

        public static int GrassTone(float2 xz, float dither)
        {
            float tone = noise.snoise(xz * 0.07f + 3.1f) * 0.7f + noise.snoise(xz * 0.23f - 8.4f) * 0.3f + (dither - 0.5f) * 0.35f;
            return tone < -0.28f ? -1 : tone > 0.3f ? 1 : 0;
        }

        private static float Level(float h, float2 xz, float4 pad)
        {
            float d = math.distance(xz, pad.xy);
            float w = 1f - math.smoothstep(pad.z, pad.z + PAD_BLEND, d);
            return math.lerp(h, pad.w, w);
        }
    }
}
