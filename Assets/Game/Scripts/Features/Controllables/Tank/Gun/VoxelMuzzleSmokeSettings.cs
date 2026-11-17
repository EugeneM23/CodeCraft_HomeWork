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

        [Header("Jet")]
        [Tooltip("Cubes thrown out on a shot.")]
        [Range(0, 3000)] public int Cubes = 675;
        [Tooltip("Seconds a cube lives; each gets 60...100 % of it.")]
        public float Lifetime = 5.1f;
        [Tooltip("How far (m) along the barrel the jet reaches; its fattest blob sits at the end.")]
        public float JetLength = 5.4f;
        [Tooltip("Radius (m) of the jet at the muzzle.")]
        public float RadiusMuzzle = 0.15f;
        [Tooltip("Radius (m) of the blob at the far end.")]
        public float RadiusEnd = 0.84f;
        [Tooltip("How late along the jet it fattens: 1 a straight cone, higher stays thin longer and swells at the end.")]
        public float Taper = 2f;
        [Tooltip("How the cubes spread along the jet: 0 even, higher packs more of them towards the far end.")]
        public float DensityToEnd = 1.2f;
        [Tooltip("Share of the cubes in the round blob at the end of the jet.")]
        [Range(0f, 1f)] public float TipShare = 0.4f;
        [Tooltip("Edge (m) of a cube at the muzzle and in the blob at the end.")]
        public float SizeMuzzle = 0.12f;
        public float SizeEnd = 0.375f;
        [Tooltip("Seconds the jet takes to stretch out to its full length.")]
        public float ExtendTime = 0.3f;
        [Tooltip("How sharply the jet is spat out: 1 even speed all the way, higher shoots out fast and brakes hard at the end.")]
        public float EjectSharpness = 3f;
        [Tooltip("Speed (m/s) a cube keeps once the jet has stretched out; the slow drift starts from it.")]
        public float ResidualSpeed = 0.5f;

        [Header("Mushroom cap")]
        [Tooltip("How flat the blob at the end is along the barrel: 1 a ball, smaller a flatter cap.")]
        [Range(0.1f, 1f)] public float CapFlatten = 0.45f;
        [Tooltip("How much the cap's burst rolls back towards the muzzle, like a mushroom's brim: 0 straight out sideways.")]
        [Range(0f, 1f)] public float CapCurl = 0.45f;

        [Header("Burst")]
        [Tooltip("How hard (m/s) the smoke at the far end bursts out sideways and up once the jet has stretched out.")]
        public float Burst = 6.2f;
        [Tooltip("How much stronger the burst gets towards the far end: 0 the same all along, 1 growing evenly, higher only right at the end.")]
        public float BurstGrowth = 3f;
        [Tooltip("Share of the burst at the muzzle (the far end has all of it).")]
        [Range(0f, 1f)] public float BurstAtMuzzle = 0.1f;
        [Tooltip("Share of the burst that goes up.")]
        [Range(0f, 1f)] public float BurstUp = 0.15f;
        [Tooltip("How many times more a bursting cube swells over its life, at full burst.")]
        public float BurstSwell = 2f;
        [Tooltip("How many times faster bursting smoke creeps apart afterwards, at full burst.")]
        public float BurstCreep = 2f;

        [Header("Drift")]
        [Tooltip("How fast the cubes lose speed in the air (1/s).")]
        public float Drag = 3.2f;
        [Tooltip("Upward drift (m/s²) of the smoke; below 0 it slowly settles. Steady speed is Rise / Drag.")]
        public float Rise = -0.25f;
        [Tooltip("Sideways push (m/s²) that keeps a slowed cube creeping away from the muzzle, so the cloud spreads. Steady speed is Creep / Drag.")]
        public float Creep = 0.6f;
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
