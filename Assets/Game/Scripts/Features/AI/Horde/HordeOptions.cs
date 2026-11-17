using System;
using Game.Horde;
using UnityEngine;

namespace Game.AI
{
    /// <summary>Level-side settings of the horde (the installer's inspector).</summary>
    [Serializable]
    public sealed class HordeOptions
    {
        [Tooltip("Monster slots: the most that can live at once.")]
        public int capacity = 8192;

        [Tooltip("Monsters kept alive from the start; the dead are replaced far from the tank. 0: no respawn.")]
        public int population;

        [Tooltip("New monsters appear at least this far from the tank, m.")]
        public float spawnDistance = 15f;

        [Tooltip("Spawns per frame at most (keeps a big wave from stalling one frame).")]
        public int spawnsPerFrame = 512;

        [Tooltip("XZ min and max corner of the level, m: the draw bounds, and the shared navigation grid if the level has no AI entity yet.")]
        public Vector2 mapMin = new(-40f, -40f);

        public Vector2 mapMax = new(40f, 40f);

        [Tooltip("Navigation rays start this high and look this far down, m (for a grid the horde makes).")]
        public float rayTop = 20f;

        public float rayLength = 30f;

        [Tooltip("Walking speed, m/s (each monster ± 20 %).")]
        public float speed = 4.5f;

        [Tooltip("Damage per second of one monster clawing the hull; 0 makes the tank immune.")]
        public float attackDps = 2f;

        [Tooltip("Tank speed above which it crushes the monsters under the hull, m/s.")]
        public float crushSpeed = 1.5f;

        public float health = 100f;

        [Tooltip("Damage of a blast at its centre (half at its edge).")]
        public float explosionDamage = 200f;

        [Tooltip("Crowd casts shadows. Off by default: see the horde measurement.")]
        public bool castShadows;

        public int corpseCapacity = 512;

        [Tooltip("Corpses lie this long, then sink, s.")]
        public float corpseLie = 6f;

        public float corpseSink = 3f;

        [Tooltip("Deaths per frame that get a blood spray; the rest of the frame's deaths go without.")]
        public int bloodPerFrame = 3;

        public int bloodDrops = 10;

        /// <summary>Simulation tuning from these options.</summary>
        public HordeSettings ToSettings()
        {
            HordeSettings s = HordeSettings.Default;
            s.Speed = speed;
            s.AttackDps = attackDps;
            s.CrushSpeed = crushSpeed;
            s.Health = health;
            return s;
        }
    }
}
