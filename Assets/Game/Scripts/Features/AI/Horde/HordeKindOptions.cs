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

        [Tooltip("Melee: damage of one blow at the hull and seconds between blows (0: no blows).")]
        public float hitDamage;

        public float hitInterval;

        [Tooltip("Ranged: damage of one acid shot, seconds between shots, range and the shot's horizontal speed.")]
        public float shotDamage;

        public float shotInterval;
        public float shotRange;
        public float shotSpeed;

        [Tooltip("Ranged: it stops this close to the tank when the hull is in sight, m, and backs off under the second distance.")]
        public float stopDistance;

        public float backOff;
        public bool crushable = true;

        [Tooltip("Crushed only at or below this share of its full health (0: always).")]
        public float crushBelowHealth;

        public static HordeKindOptions From(in HordeKind kind) => new()
        {
            speed = kind.Speed,
            radius = kind.Radius,
            health = kind.Health,
            renderScale = kind.RenderScale,
            tactic = kind.Tactic,
            biteDps = kind.BiteDps,
            hitDamage = kind.HitDamage,
            hitInterval = kind.HitInterval,
            shotDamage = kind.ShotDamage,
            shotInterval = kind.ShotInterval,
            shotRange = kind.ShotRange,
            shotSpeed = kind.ShotSpeed,
            stopDistance = kind.StopDistance,
            backOff = kind.BackOff,
            crushable = kind.Crushable,
            crushBelowHealth = kind.CrushBelowHealth
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
            HitDamage = hitDamage,
            HitInterval = hitInterval,
            ShotDamage = shotDamage,
            ShotInterval = shotInterval,
            ShotRange = shotRange,
            ShotSpeed = shotSpeed,
            StopDistance = stopDistance,
            BackOff = backOff,
            Crushable = crushable,
            CrushBelowHealth = crushBelowHealth
        };
    }
}
