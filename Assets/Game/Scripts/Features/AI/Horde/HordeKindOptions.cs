using System;
using Game.Horde;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>One horde kind in the inspector (chapter-1 values; the director scales health and damage).</summary>
    [Serializable]
    public sealed class HordeKindOptions
    {
        public float speed;
        public float radius;
        public float health;

        [Tooltip("Scale of the Regax model (and of its corpse).")]
        public float renderScale;

        public HordeTactic tactic;

        [Tooltip("Melee: damage per second at the hull.")]
        public float biteDps;

        [Tooltip("Ranged: damage of one acid shot, seconds between shots, range and the shot's horizontal speed.")]
        public float shotDamage;

        public float shotInterval;
        public float shotRange;
        public float shotSpeed;

        [Tooltip("Ranged: the band it keeps from the tank, m, and the distance under which it backs off.")]
        public float keepMin;

        public float keepMax;
        public float backOff;
        public bool crushable = true;

        public static HordeKindOptions From(in HordeKind kind) => new()
        {
            speed = kind.Speed,
            radius = kind.Radius,
            health = kind.Health,
            renderScale = kind.RenderScale,
            tactic = kind.Tactic,
            biteDps = kind.BiteDps,
            shotDamage = kind.ShotDamage,
            shotInterval = kind.ShotInterval,
            shotRange = kind.ShotRange,
            shotSpeed = kind.ShotSpeed,
            keepMin = kind.KeepMin,
            keepMax = kind.KeepMax,
            backOff = kind.BackOff,
            crushable = kind.Crushable
        };

        public HordeKind ToKind() => new()
        {
            Speed = speed,
            Radius = radius,
            Health = health,
            RenderScale = renderScale,
            EmergeDepth = 2.7f * renderScale,
            Tactic = tactic,
            BiteDps = biteDps,
            ShotDamage = shotDamage,
            ShotInterval = shotInterval,
            ShotRange = shotRange,
            ShotSpeed = shotSpeed,
            KeepMin = keepMin,
            KeepMax = keepMax,
            BackOff = backOff,
            Crushable = crushable
        };
    }
}
