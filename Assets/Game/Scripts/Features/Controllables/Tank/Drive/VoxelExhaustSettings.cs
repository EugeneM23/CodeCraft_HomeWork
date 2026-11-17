using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Experiment: the exhaust's black smoke as see-through voxel cubes instead of particles. The
    /// load, the cranking and Shift drive it as they drive the particles (TankExhaustBehaviour);
    /// these only say how a cube looks and moves. Tunable in TankInstaller → Voxel Exhaust.
    /// </summary>
    [Serializable]
    public sealed class VoxelExhaustSettings
    {
        [Tooltip("On: voxel cubes. Off: the old particle smoke.")]
        public bool Voxel = true;

        [Tooltip("Cubes per particle the old smoke would emit: cubes are bigger, so fewer.")]
        [Range(0.05f, 2f)] public float RateScale = 0.3f;
        [Tooltip("Cube opacity against the particle's alpha (a soft particle shows less than its alpha).")]
        [Range(0f, 2f)] public float OpacityScale = 0.75f;
        [Tooltip("A cube's edge at the pipe against the particle's size.")]
        public float SizeScale = 1.4f;
        [Tooltip("How many times a cube swells over its life (the particles: 6).")]
        public float Grow = 4.5f;
        [Tooltip("Seconds a cube lives, min and max (the particles: 1.6...2.6).")]
        public Vector2 Lifetime = new(1.6f, 2.6f);
        [Tooltip("Half-angle (deg) of the cone the cubes leave the pipe in.")]
        public float Cone = 16f;
        [Tooltip("How fast the cubes lose speed in the air (1/s).")]
        public float Drag = 2.5f;
        [Tooltip("Upward drift (m/s²).")]
        public float Rise = 0.08f;
        [Tooltip("Turbulence that tears the plume up (m/s²).")]
        public float Noise = 1.2f;
        [Tooltip("Cubes snap to a grid of this step (m); 0 moves them freely.")]
        public float Grid = 0.05f;
        [Tooltip("Share of its life a cube keeps its full opacity before thinning out.")]
        [Range(0f, 1f)] public float FadeStart = 0.15f;

        [Tooltip("Walls, ground and rubble stop the smoke; it spreads over them.")]
        public bool ReactToWorld = true;
        [Tooltip("Tank hulls push the smoke out of themselves: driving through it leaves a wake. Shots and blasts part it.")]
        public bool ReactToTanks = true;

        public Color DarkColor = new(0.03f, 0.03f, 0.03f, 1f);
        public Color LightColor = new(0.065f, 0.062f, 0.06f, 1f);
    }
}
