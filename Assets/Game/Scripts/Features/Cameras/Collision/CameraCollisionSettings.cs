using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// How the camera keeps a house from hiding what it follows: when a building below the camera
    /// stands between them, CameraFollowController raises the camera straight up until it sees over
    /// the roof, tilting it so the target stays where it was on screen. Only what is below the camera
    /// moves it, and only up: it never comes down or closer to the target, not even with the target
    /// inside a house.
    /// </summary>
    [Serializable]
    public class CameraCollisionSettings
    {
        [field: Tooltip("Off: the camera follows through houses, as it did before.")]
        [field: SerializeField]
        public bool Enabled { get; private set; } = true;

        [field: Tooltip("How wide a clear view of the target the camera keeps, in metres round the line " +
                        "to it: wide enough that the whole tank shows over a roof, not only its centre.")]
        [field: SerializeField, Min(0.05f)]
        public float ViewRadius { get; private set; } = 0.8f;

        [field: Tooltip("Height above the camera point the camera keeps in view, in metres: the middle " +
                        "of the tank rather than the ground under it.")]
        [field: SerializeField, Min(0f)]
        public float TargetHeight { get; private set; } = 1f;

        [field: Tooltip("Highest the camera may rise over a house, in metres.")]
        [field: SerializeField, Min(0f)]
        public float MaxRise { get; private set; } = 30f;

        [field: Tooltip("Step of the search for the least rise that sees over, in metres.")]
        [field: SerializeField, Range(0.1f, 2f)]
        public float SearchStep { get; private set; } = 0.5f;

        [field: Tooltip("How quickly the camera rises when a house gets in the way.")]
        [field: SerializeField, Min(0.1f)]
        public float RiseSpeed { get; private set; } = 10f;

        [field: Tooltip("How quickly the camera settles back down once the house is out of the way.")]
        [field: SerializeField, Min(0.1f)]
        public float ReturnSpeed { get; private set; } = 3f;

        [field: Tooltip("Layers the buildings are on (voxel houses are on Default). Only buildings move " +
                        "the camera; the ground, trees, walls and anything moving never do.")]
        [field: SerializeField]
        public LayerMask Obstacles { get; private set; } = DefaultObstacles;

        public const int DefaultObstacles = 1 << 0;

        public CameraCollisionSettings()
        {
        }
    }
}
