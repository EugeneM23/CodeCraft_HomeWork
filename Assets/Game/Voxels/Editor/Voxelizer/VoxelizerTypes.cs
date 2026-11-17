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
        public float VoxelSize; // part space: the class voxel / Scale / the part's world scale
        public NativeArray<Color32> Cells;

        public const byte SURFACE = 255;
        public const byte INSIDE = 128;

        public void Dispose()
        {
            if (Cells.IsCreated) Cells.Dispose();
        }
    }

    public sealed class VoxelizerResultPart
    {
        public string Path;
        public Transform Root;
        public int3 Size;
        /// <summary>Part space (the prefab root's Scale brings it to real size).</summary>
        public float3 Origin;
        /// <summary>The voxel edge in the part's own space: the class voxel / Scale / the part's world scale.</summary>
        public float VoxelSize;
        public NativeArray<byte> Voxels;
    }

    public sealed class VoxelizerResult : IDisposable
    {
        public readonly System.Collections.Generic.List<VoxelizerResultPart> Parts = new();
        /// <summary>Rule roots that gave no part (Skip, or no triangles): their renderers stay as they are.</summary>
        public readonly System.Collections.Generic.List<Transform> KeptRoots = new();
        public Color32[] Palette;
        public float VoxelSize; // world units of the instance as placed: the class voxel / Scale
        /// <summary>The class voxel in real metres (3 cm, 6 cm, 10 cm).</summary>
        public float ClassVoxel;
        /// <summary>World to real metres: the config's length over the model's longest side (1 without a length).</summary>
        public float Scale = 1f;
        /// <summary>The model root's uniform world scale (R).</summary>
        public float RootScale = 1f;

        public void Dispose()
        {
            foreach (VoxelizerResultPart part in Parts)
                if (part.Voxels.IsCreated) part.Voxels.Dispose();
            Parts.Clear();
        }
    }
}
