using System;
using Game.Horde;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>Level-side settings of the horde (the installer's inspector).</summary>
    [Serializable]
    public sealed class HordeOptions
    {
        [Tooltip("Monster slots: the most that can live at once.")]
        public int capacity = 8192;

        [Tooltip("Crawlers kept alive from the start; the dead are replaced far from the tank. 0: no respawn (an arena's director spawns).")]
        public int population;

        [Tooltip("New monsters of the population appear at least this far from the tank, m.")]
        public float spawnDistance = 15f;

        [Tooltip("Spawns per frame at most (keeps a big wave from stalling one frame).")]
        public int spawnsPerFrame = 512;

        [Tooltip("XZ min and max corner of the level, m: the draw bounds, and the shared navigation grid if the level has no AI entity yet.")]
        public Vector2 mapMin = new(-40f, -40f);

        public Vector2 mapMax = new(40f, 40f);

        [Tooltip("Navigation rays start this high and look this far down, m (for a grid the horde makes).")]
        public float rayTop = 20f;

        public float rayLength = 30f;

        [Tooltip("Kind row 0: the Crawler (GDD §5).")]
        public HordeKindOptions crawler = HordeKindOptions.From(HordeKind.Crawler);

        [Tooltip("Kind row 1: the Spitter (GDD §5).")]
        public HordeKindOptions spitter = HordeKindOptions.From(HordeKind.Spitter);

        [Tooltip("Kind row 2: the big Regax of arena 5 (EliteRegax).")]
        public HordeKindOptions brute = HordeKindOptions.From(HordeKind.Brute);

        [Tooltip("Neighbours closer than this push each other apart, m: small keeps the crowd a dense mass round the hull.")]
        public float separation = 0.9f;

        [Tooltip("Scale of every monster's damage to the tank: bites and acid (0 makes the tank immune: benchmarks).")]
        public float tankDamageScale = 1f;

        [Tooltip("Seconds a monster takes to climb out of its burrow.")]
        public float emergeTime = 1f;

        [Tooltip("Knockback of a blast (ExplosionEvents.Exploded) at its centre, m. Blast damage comes from the weapons (the target source).")]
        public float explosionPush = 1.5f;

        [Tooltip("Crowd casts shadows. Off by default: see the horde measurement.")]
        public bool castShadows;

        [Tooltip("Corpses per kind lying at once (a ring: the oldest goes first).")]
        public int corpseCapacity = 512;

        [Tooltip("Corpses lie this long, then sink, s.")]
        public float corpseLie = 6f;

        public float corpseSink = 3f;

        [Tooltip("Deaths per frame that get a blood spray; the rest of the frame's deaths go without.")]
        public int bloodPerFrame = 3;

        public int bloodDrops = 10;

        [Tooltip("Acid hits on the ground per frame that get a small splash.")]
        public int acidSplashPerFrame = 2;

        public int acidSplashDrops = 6;

        /// <summary>
        /// Simulation tuning from these options (the kinds are set apart, <see cref="Kind"/>). No crush threshold: the
        /// horde takes the player's vehicle's every frame (<see cref="HordeBehaviour.CrushSpeedOf"/>).
        /// </summary>
        public HordeSettings ToSettings()
        {
            HordeSettings s = HordeSettings.Default;
            HordeKind crawlerKind = crawler.ToKind();
            s.Speed = crawlerKind.Speed;
            s.Health = crawlerKind.Health;
            s.Radius = crawlerKind.Radius;
            s.AttackDps = crawlerKind.BiteDps;
            s.CrushSpeed = HordeBehaviour.NO_CRUSH;
            s.EmergeTime = emergeTime;
            s.SeparationRadius = separation;
            return s;
        }

        /// <summary>The kind table's row <paramref name="row"/> (<see cref="HordeKinds"/>).</summary>
        public HordeKind Kind(int row) => (row == HordeKinds.SPITTER ? spitter : row == HordeKinds.BRUTE ? brute : crawler).ToKind();
    }
}
