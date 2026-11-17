using System;
using UnityEngine;

namespace Game.Voxels.EditorTools
{
    /// <summary>How one model is voxelized: kept as an asset next to the baked prefab, so a rebake gives the same result.</summary>
    [CreateAssetMenu(menuName = "Game/Voxels/Voxelizer Config", fileName = "ModelVoxelizerConfig")]
    public sealed class VoxelizerConfig : ScriptableObject
    {
        public enum ModelKind { Vehicle, Building }

        public enum PartMark { Auto, Box, Cylinder, AsIs, Skip }

        [Serializable]
        public sealed class PartRule
        {
            [Tooltip("Path from the model root; '*' and '?' match any name in that step.")]
            public string path = "";
            public PartMark mark;
            [Tooltip("Cylinder axis in the part's space: 0 X, 1 Y, 2 Z.")]
            [Range(0, 2)] public int axis;
        }

        public const float VEHICLE_VOXEL = 0.03f;
        public const float BUILDING_VOXEL = 0.1f;

        public GameObject model;
        public ModelKind kind;
        [Tooltip("Real length of the model's longest side, m; 0 - as the model is.")]
        public float length;
        [Range(4, 32)] public int colours = 12;
        public PartRule[] parts = Array.Empty<PartRule>();

        public float VoxelSize => kind == ModelKind.Vehicle ? VEHICLE_VOXEL : BUILDING_VOXEL;
    }
}
