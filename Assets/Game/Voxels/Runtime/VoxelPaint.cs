using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// The look laid over a voxel grid: a camouflage (patches over one paint colour, in the model's
    /// own space so they run on across parts) and decals (pixel pictures painted on a part's side).
    /// Shared by <see cref="VoxelizedModel"/> (runs of <see cref="MeshVoxelizer.VARIANTS"/> shades per
    /// colour) and the editor voxelizer (one slot per colour).
    /// </summary>
    public static class VoxelPaint
    {
        /// <summary>Shade spread of a decal colour's run: nearly flat, a painted sign, not weathered steel.</summary>
        private const float DECAL_NOISE = 0.02f;

        /// <summary>
        /// Repaints the voxels in slots [<paramref name="paintFirst"/>, <paramref name="paintFirst"/> + <paramref name="variants"/>)
        /// with the camouflage's patch, dark and light colours, each voxel keeping its shade within the run.
        /// The new colours go into free palette slots: a run of shades each (<see cref="MeshVoxelizer.AddShades"/>)
        /// when <paramref name="variants"/> is <see cref="MeshVoxelizer.VARIANTS"/>, one slot each (reused if the
        /// palette already has that colour) when it is 1. Returns how many voxels changed colour.
        /// </summary>
        /// <param name="partToModel">Grid's space to the model's: the noise runs on across parts in it, in metres.</param>
        public static int Camouflage(NativeArray<byte> voxels, int3 size, float3 origin, float voxelSize, float4x4 partToModel,
            NativeArray<Color32> palette, int paintFirst, int variants, in VoxelizedModel.Camouflage camo, float noise)
        {
            int patch = AddColour(palette, camo.Patch, noise, variants);
            int dark = AddColour(palette, camo.Dark, noise, variants);
            int light = AddColour(palette, camo.Light, noise, variants);
            if (patch < 0 || dark < 0 || light < 0)
                return 0;

            var count = new NativeArray<int>(1, Allocator.TempJob);
            new CamouflageJob
            {
                Voxels = voxels,
                Size = size,
                Origin = origin,
                VoxelSize = voxelSize,
                PartToModel = partToModel,
                Paint = paintFirst,
                Variants = variants,
                Patch = patch,
                Dark = dark,
                Light = light,
                Frequency = Mathf.Max(0.01f, camo.Frequency),
                Threshold = camo.Threshold,
                Count = count
            }.Run();

            int painted = count[0];
            count.Dispose();
            return painted;
        }

        /// <summary>The decals whose <see cref="VoxelizedModel.Decal.Part"/> is <paramref name="partPath"/>, painted on this grid.</summary>
        public static void Decals(NativeArray<byte> voxels, int3 size, float3 origin, float voxelSize, NativeArray<Color32> palette,
            int variants, string partPath, VoxelizedModel.Decal[] decals)
        {
            if (decals == null)
                return;

            foreach (VoxelizedModel.Decal decal in decals)
                if (decal.Part == partPath)
                    Decal(voxels, size, origin, voxelSize, palette, variants, decal);
        }

        /// <summary>One decal on this grid, whatever part it names.</summary>
        public static void Decal(NativeArray<byte> voxels, int3 size, float3 origin, float voxelSize, NativeArray<Color32> palette,
            int variants, in VoxelizedModel.Decal decal)
        {
            if (decal.Rows == null || decal.Rows.Length == 0 || string.IsNullOrEmpty(decal.Keys) || decal.Colors == null)
                return;

            // One run of shades per colour, nearly flat.
            var slots = new int[decal.Keys.Length];
            for (int k = 0; k < slots.Length; k++)
                slots[k] = k < decal.Colors.Length ? AddColour(palette, decal.Colors[k], DECAL_NOISE, variants) : -1;

            int side = decal.Side >= 0 ? 1 : -1;
            int height = decal.Rows.Length;

            for (int row = 0; row < height; row++)
            {
                string line = decal.Rows[row];
                for (int column = 0; column < line.Length; column++)
                {
                    int key = decal.Keys.IndexOf(line[column]);
                    if (key < 0 || slots[key] < 0)
                        continue;

                    // Pixel centres one voxel apart about the centre; left to right as seen from
                    // the side: towards -Z on the -X side, towards +Z on the +X side.
                    float z = decal.Center.x + side * (column - (line.Length - 1) * 0.5f) * voxelSize;
                    float y = decal.Center.y + ((height - 1) * 0.5f - row) * voxelSize;
                    int cz = (int)math.floor((z - origin.z) / voxelSize);
                    int cy = (int)math.floor((y - origin.y) / voxelSize);
                    if (cy < 0 || cz < 0 || cy >= size.y || cz >= size.z)
                        continue;

                    // In from that side to the first voxel: the paint lies on the surface.
                    for (int step = 0; step < size.x; step++)
                    {
                        int cx = side > 0 ? size.x - 1 - step : step;
                        int index = VoxelMath.Index(new int3(cx, cy, cz), size);
                        int voxel = voxels[index];
                        if (voxel == 0)
                            continue;

                        voxels[index] = (byte)(slots[key] + (voxel - 1) % variants);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// A colour's first slot: a run of shades in the first free run when <paramref name="variants"/> is
        /// <see cref="MeshVoxelizer.VARIANTS"/>; otherwise one slot, the one already holding this colour or
        /// the first free one after the last used slot (gaps in the voxelizer's runs are left alone). -1 if full.
        /// </summary>
        private static int AddColour(NativeArray<Color32> palette, Color color, float noise, int variants)
        {
            if (variants == MeshVoxelizer.VARIANTS)
                return MeshVoxelizer.AddShades(palette, color, noise);

            Color32 slot = MeshVoxelizer.WithWetness(MeshVoxelizer.ToVertexColor(color), 0f);
            int last = 0;
            for (int i = 1; i < palette.Length; i++)
            {
                Color32 c = palette[i];
                if (c.a == 0)
                    continue;
                if (c.r == slot.r && c.g == slot.g && c.b == slot.b && c.a == slot.a)
                    return i;
                last = i;
            }

            if (last + 1 >= palette.Length)
                return -1;
            palette[last + 1] = slot;
            return last + 1;
        }

        /// <summary>
        /// Patch, dark heart or sun-bleached sand for each paint voxel, from simplex noise in the
        /// model's space. The axes are sheared so patches run in slanting streaks, as brushed paint
        /// does; each voxel keeps its own shade within the new colour.
        /// </summary>
        [BurstCompile]
        private struct CamouflageJob : IJob
        {
            public NativeArray<byte> Voxels;
            public int3 Size;
            public float3 Origin;
            public float VoxelSize;
            public float4x4 PartToModel;
            public int Paint;
            public int Variants;
            public int Patch;
            public int Dark;
            public int Light;
            public float Frequency;
            public float Threshold;
            public NativeArray<int> Count;

            public void Execute()
            {
                int count = 0;
                for (int i = 0; i < Voxels.Length; i++)
                {
                    int voxel = Voxels[i];
                    if (voxel < Paint || voxel >= Paint + Variants)
                        continue;

                    float3 local = Origin + ((float3)VoxelMath.Coord(i, Size) + 0.5f) * VoxelSize;
                    float3 p = math.transform(PartToModel, local);
                    float3 q = new float3(p.x + p.y * 0.8f, p.y * 1.6f + p.z * 0.5f, p.z * 1.2f + p.y * 1.1f);
                    float streak = noise.snoise(q * Frequency) + 0.3f * noise.snoise(q * Frequency * 2.67f + 11.3f);

                    int first = Paint;
                    if (streak > Threshold)
                        first = streak > Threshold + 0.65f ? Dark : Patch;
                    else if (noise.snoise(p * Frequency * 1.13f + 37.1f) > 0.45f)
                        first = Light;

                    if (first != Paint)
                        count++;
                    Voxels[i] = (byte)(first + (voxel - Paint));
                }

                Count[0] = count;
            }
        }
    }
}
