using System;
using UnityEngine;

namespace Game
{
    /// <summary>How the tank crushes the enemies it runs into, and how the blood flies and sticks.</summary>
    [Serializable]
    public sealed class TankCrushConfig
    {
        [Tooltip("Slowest (m/s) the tank closes on an enemy and still crushes it; slower it only shoves.")]
        public float minSpeed = 1.5f;

        [Tooltip("Drops in each of the three fans thrown from under the tracks: out to the left, out to the right and back along the tracks.")]
        public int dropsPerFan = 120;

        [Tooltip("Speed (m/s) of the fans when the tank crawls over the enemy…")]
        public float slowSpray = 3f;

        [Tooltip("…and when it runs it down at full speed (10 m/s).")]
        public float fastSpray = 9f;

        [Tooltip("How far a drop strays from its fan's direction.")]
        [Range(0f, 1f)] public float spread = 0.35f;

        // `splashes` and `thickBelow` were `stains` (30) and `stainHeight` (0.9 m: nothing higher);
        // `splashReach` was `stainReach` (1.6 m), then `thickWithin` (2.5 m, the whole tank,
        // thinning), then `burstReach` (3 m, falling off gently). Renamed so the old values kept by
        // open scenes give way.
        [Tooltip("Splashes of blood stuck onto the tank's voxel skin per crush, on the side the body burst against: each part in reach takes them by its size.")]
        public int splashes = 120;

        [Tooltip("Radius (m) of one splash.")]
        public float stainRadius = 0.18f;

        [Tooltip("Share of the cells in a splash that take blood.")]
        [Range(0f, 1f)] public float stainFill = 0.55f;

        [Tooltip("How far (m) from where the body burst its blood reaches on the tank: thickest right at it, thinning as (1 − distance / this)² to none here. Only faces turned to the burst take it.")]
        public float splashReach = 2.5f;

        [Tooltip("Height (m above the ground) below which splashes lie thickest; higher they thin as (this / height)².")]
        public float thickBelow = 1.2f;

        public BloodLook blood = new();
    }
}
