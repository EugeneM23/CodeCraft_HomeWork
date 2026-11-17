using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Experiment: the cannon's smoke as voxel cubes instead of particles. Tunable in the tank's
    /// inspector (TankInstaller → Muzzle Smoke (voxel)); the particle smoke can be switched back
    /// on to compare.
    /// </summary>
    [Serializable]
    public sealed class VoxelMuzzleSmokeSettings
    {
        [Tooltip("Voxel smoke on a shot.")]
        public bool Voxel = true;
        [Tooltip("The old pooled particle burst (flash, flame, smoke) as well.")]
        public bool ParticleBurst;
        [Tooltip("The old particle wisp still leaking from the barrel after a shot.")]
        public bool ParticleWisp;

        [Header("Blast")]
        [Tooltip("Cubes thrown out on a shot.")]
        [Range(0, 3000)] public int Cubes = 640;
        [Tooltip("Seconds a cube lives; each gets 60...100 % of it.")]
        public float Lifetime = 3.9f;
        [Tooltip("Speed (m/s) along the barrel of the fastest cubes.")]
        public float Speed = 14f;
        [Tooltip("How wide the blast opens: 0 a straight jet, 1 a half sphere.")]
        [Range(0f, 1f)] public float Spread = 0.45f;
        [Tooltip("Share of the cubes thrown sideways round the muzzle: the ring of a muzzle brake.")]
        [Range(0f, 1f)] public float SideShare = 0.3f;
        [Tooltip("How fast the cubes lose speed in the air (1/s).")]
        public float Drag = 3.2f;
        [Tooltip("Upward drift (m/s²) of the smoke; below 0 it slowly settles. Steady speed is Rise / Drag.")]
        public float Rise = -0.25f;
        [Tooltip("Sideways push (m/s²) that keeps a slowed cube creeping away from the muzzle, so the cloud spreads. Steady speed is Creep / Drag.")]
        public float Creep = 0.6f;
        [Tooltip("Edge (m) of a cube at birth and at its fullest.")]
        public Vector2 Size = new(0.1f, 0.38f);
        [Tooltip("Cubes snap to a grid of this step (m); 0 moves them freely.")]
        public float Grid = 0.08f;

        [Header("Colour")]
        public Color HotColor = new(1f, 0.55f, 0.15f, 1f);
        [Tooltip("Seconds the young cubes stay fire-coloured.")]
        public float HotTime = 0.12f;
        public Color SmokeColor = new(0.62f, 0.62f, 0.64f, 1f);
        [Tooltip("Colour the smoke ends at, as it thins.")]
        public Color FadeColor = new(0.82f, 0.82f, 0.84f, 1f);
        [Tooltip("Brightness of the fire-coloured cubes: above 1 they bloom.")]
        public float HotGlow = 4f;
        [Tooltip("How opaque a cube of smoke is at its thickest.")]
        [Range(0f, 1f)] public float Opacity = 0.175f;
        [Tooltip("Share of its life a cube keeps its full opacity before thinning out.")]
        [Range(0f, 1f)] public float FadeStart = 0.3f;

        [Header("Wisp")]
        [Tooltip("Seconds smoke still leaks out of the barrel after a shot; 0 for none.")]
        public float WispTime = 2.5f;
        [Tooltip("Cubes per second at the start of the wisp; thins to none.")]
        public float WispRate = 90f;
        public float WispSize = 0.14f;
    }
}
