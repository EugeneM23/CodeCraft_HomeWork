using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels.EditorTools
{
    /// <summary>A part's triangles in its own space, with what colours them.</summary>
    public sealed class VoxelizerTriangles : IDisposable
    {
        public NativeArray<float3> Corners;   // 3 per triangle
        public NativeArray<float2> Uvs;       // 3 per triangle
        public NativeArray<int> Material;     // 1 per triangle
        public NativeArray<Color32> Tints;    // 1 per material, sRGB as authored
        public NativeArray<Color32> Pixels;   // every material's texture, one after another
        public NativeArray<int4> Textures;    // per material: offset into Pixels, width, height, 1 if textured
        public float3 Min;
        public float3 Max;

        public int Count => Corners.IsCreated ? Corners.Length / 3 : 0;

        public void Dispose()
        {
            if (Corners.IsCreated) Corners.Dispose();
            if (Uvs.IsCreated) Uvs.Dispose();
            if (Material.IsCreated) Material.Dispose();
            if (Tints.IsCreated) Tints.Dispose();
            if (Pixels.IsCreated) Pixels.Dispose();
            if (Textures.IsCreated) Textures.Dispose();
        }
    }

    /// <summary>
    /// One part on the voxel grid before the palette: a = 0 empty, 255 surface, 128 inside.
    /// Colours are sRGB as authored; the palette turns them into vertex colours.
    /// </summary>
    public sealed class VoxelizerPartGrid : IDisposable
    {
        public string Path = "";
        public Transform Root;
        public int3 Size;
        public float3 Origin;
        public float VoxelSize; // model units: the class voxel / Scale
        public NativeArray<Color32> Cells;

        public const byte SURFACE = 255;
        public const byte INSIDE = 128;

        public void Dispose()
        {
            if (Cells.IsCreated) Cells.Dispose();
        }
    }
}
