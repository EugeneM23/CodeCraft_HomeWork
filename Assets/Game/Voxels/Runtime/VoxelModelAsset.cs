using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// A model voxelized in the editor (the Voxelizer window): one grid per moving part, one palette
    /// for the whole model, and how it breaks. VoxelModel hands it to VoxelBody on start, so nothing
    /// is voxelized in the game.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Voxels/Voxel Model", fileName = "VX_Model_Data")]
    public sealed class VoxelModelAsset : ScriptableObject
    {
        [Serializable]
        public sealed class Part
        {
            [Tooltip("The part's transform as a path from the model root; empty — the root itself.")]
            public string path = "";
            public Vector3Int size;
            [Tooltip("Part space position of the min corner of voxel (0, 0, 0), m.")]
            public Vector3 origin;
            [Tooltip("Voxel edge in the part's own space, m; the part's world scale in the prefab makes it the class voxel.")]
            public float voxelSize;
            [Tooltip("The grid, x fastest, as (count, value) byte pairs.")]
            public byte[] runs = Array.Empty<byte>();
        }

        public Part[] parts = Array.Empty<Part>();
        [Tooltip("256 colours; index 0 is empty. Alpha is dryness (255 — dry).")]
        public Color32[] palette = new Color32[256];
        [Tooltip("The class voxel in real metres, for display; each part's grid uses its own Part.voxelSize.")]
        public float voxelSize = 0.03f;
        [Range(0.1f, 1f)] public float toughness = 1f;
        public float density = 1800f;
        public VoxelAnchor anchor = VoxelAnchor.Largest;
        public bool colliders;
        [Tooltip("Edge of a collision cell in voxels; 0 or 1 — every voxel.")]
        public int colliderCell;

        /// <summary>A part's body settings: its own voxel size and origin (part space), the model's class settings.</summary>
        public VoxelBodySettings Settings(Part part, int layer)
        {
            VoxelBodySettings settings = VoxelBodySettings.Default(part.voxelSize);
            settings.Origin = (float3)part.origin;
            settings.Anchor = anchor;
            settings.Colliders = colliders;
            settings.ColliderCell = colliderCell;
            settings.Layer = layer;
            settings.Toughness = toughness;
            settings.Density = density;
            return settings;
        }

        /// <summary>Runs of equal bytes as (count 1..255, value) pairs.</summary>
        public static byte[] Encode(NativeArray<byte> voxels)
        {
            var runs = new List<byte>(1024);
            int i = 0;
            while (i < voxels.Length)
            {
                byte value = voxels[i];
                int count = 1;
                while (i + count < voxels.Length && count < 255 && voxels[i + count] == value)
                    count++;
                runs.Add((byte)count);
                runs.Add(value);
                i += count;
            }

            return runs.ToArray();
        }

        public static NativeArray<byte> Decode(byte[] runs, int length, Allocator allocator)
        {
            var voxels = new NativeArray<byte>(length, allocator);
            int at = 0;
            for (int r = 0; r + 1 < runs.Length && at < length; r += 2)
            {
                int count = math.min(runs[r], length - at);
                byte value = runs[r + 1];
                for (int k = 0; k < count; k++)
                    voxels[at + k] = value;
                at += count;
            }

            return voxels;
        }
    }
}
