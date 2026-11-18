using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Experience orbs: how many there can be, what each enemy drops (GDD §5), the pickup and the look.
    /// The level curve is the run's (IRunService), not here.
    /// </summary>
    [Serializable]
    public sealed class XpSettings
    {
        [Header("Field")]
        [Tooltip("Most orbs at once; past it a new drop merges into the nearest orb on the ground")]
        public int capacity = 512;

        [Tooltip("Orbs closer than this (m) to the tank fly to it; times the magnet's PickupRadiusFactor")]
        public float pickupRadius = 4f;

        [Tooltip("An orb this close (m) to the tank is collected")]
        public float collectDistance = 0.6f;

        [Tooltip("Speed (m/s) an orb starts flying to the tank with")]
        public float flySpeedMin = 6f;

        [Tooltip("Top speed (m/s) of an orb flying to the tank")]
        public float flySpeedMax = 25f;

        [Tooltip("How fast (m/s²) a flying orb gains speed")]
        public float flyAcceleration = 40f;

        [Tooltip("Pull (m/s²) on an orb hopping out of a dead enemy")]
        public float gravity = 20f;

        [Tooltip("Layers an orb lands on (found once, by a ray down, when it drops)")]
        public LayerMask groundMask = (1 << 0) | (1 << 3) | (1 << 10) | (1 << 14) | (1 << 16);

        [Header("Look")]
        [Tooltip("Edge of the orb's cube, m")]
        public float orbSize = 0.22f;

        public Color color = new(1f, 0.55f, 0.1f, 1f);

        [Tooltip("Emission = colour × this")]
        public float emissionIntensity = 4f;

        [Tooltip("How high (m) a lying orb bobs up and down")]
        public float bobHeight = 0.06f;

        [Tooltip("Bobs per second × 2π")]
        public float bobSpeed = 3f;

        [Tooltip("Spin, degrees per second")]
        public float spinSpeed = 120f;

        [Header("Experience per enemy (GDD §5)")]
        public int crawlerXp = 1;
        public int spitterXp = 3;
        public int exploderXp = 3;
        public int eliteXp = 50;
        public int bossXp = 300;

        [Tooltip("An elite's experience is split into this many orbs")]
        public int orbsPerElite = 10;

        [Tooltip("A boss's experience is split into this many orbs")]
        public int orbsPerBoss = 20;

        /// <summary>Experience an enemy of this kind brings.</summary>
        public int XpFor(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Crawler: return crawlerXp;
                case EnemyKind.Spitter: return spitterXp;
                case EnemyKind.Exploder: return exploderXp;
                case EnemyKind.EliteRegax: return eliteXp;
                case EnemyKind.Boss: return bossXp;
                default: return 0;
            }
        }

        /// <summary>How many orbs its experience drops as.</summary>
        public int OrbsFor(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.EliteRegax: return Mathf.Max(1, orbsPerElite);
                case EnemyKind.Boss: return Mathf.Max(1, orbsPerBoss);
                default: return 1;
            }
        }
    }
}
