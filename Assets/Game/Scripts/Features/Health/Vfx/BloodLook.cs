using System;
using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>What a body's blood looks like as voxel drops. The defaults are a saturated red.</summary>
    [Serializable]
    public sealed class BloodLook
    {
        // Was `colors` (Regax's green). Renamed without FormerlySerializedAs on purpose: the green
        // saved in prefabs and kept by open scenes must give way to the red defaults.
        [Tooltip("Blood colours (sRGB): drops, splats, veins in the meat, stains on the tank.")]
        public Color[] palette = { new(0.80f, 0.03f, 0.02f), new(0.50f, 0.02f, 0.01f) };

        [Tooltip("0 dry, 1 soaking: glossier and darker in the Game/Voxel shader.")]
        [Range(0f, 1f)] public float wetness = 0.9f;

        [Tooltip("Edge of one drop, m: three quarters of a 5 cm voxel.")]
        public float dropSize = 0.0375f;

        public Color32[] ToVoxels()
        {
            var voxels = new Color32[palette.Length];
            for (int i = 0; i < palette.Length; i++)
                voxels[i] = MeshVoxelizer.WithWetness(MeshVoxelizer.ToVertexColor(palette[i]), wetness);
            return voxels;
        }
    }
}
