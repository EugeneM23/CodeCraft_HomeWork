using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The tank's numbers the hangar shows: health, speed, cannon damage and reload. The same names as in the
    /// combat team's TankStats, so the adapter over it maps field to field. Also used as a set of bonuses or
    /// percents per stat (module placeholders of StubHangarSource).
    /// </summary>
    [Serializable]
    public struct HangarTankStats
    {
        public const int COUNT = 4;
        public const int HEALTH = 0;
        public const int SPEED = 1;
        public const int CANNON_DAMAGE = 2;
        public const int CANNON_RELOAD = 3;

        public float health;
        [Tooltip("m/s")]
        public float speed;
        [Tooltip("Main gun damage per shell")]
        public float cannonDamage;
        [Tooltip("Seconds between main gun shots; lower is better")]
        public float cannonReload;

        public HangarTankStats(float health, float speed, float cannonDamage, float cannonReload)
        {
            this.health = health;
            this.speed = speed;
            this.cannonDamage = cannonDamage;
            this.cannonReload = cannonReload;
        }

        /// <summary>Stat by index 0..COUNT-1, in the order of the fields.</summary>
        public float this[int index] => index switch
        {
            HEALTH => health,
            SPEED => speed,
            CANNON_DAMAGE => cannonDamage,
            CANNON_RELOAD => cannonReload,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

        /// <summary>For the stat at index, is a larger number better (reload: smaller is better).</summary>
        public static bool HigherIsBetter(int index) => index != CANNON_RELOAD;

        public static HangarTankStats operator +(HangarTankStats a, HangarTankStats b) =>
            new(a.health + b.health, a.speed + b.speed, a.cannonDamage + b.cannonDamage, a.cannonReload + b.cannonReload);

        /// <summary>Each stat of value times (1 + percent / 100) of the same stat.</summary>
        public static HangarTankStats Scale(HangarTankStats value, HangarTankStats percent) =>
            new(value.health * (1f + percent.health / 100f), value.speed * (1f + percent.speed / 100f),
                value.cannonDamage * (1f + percent.cannonDamage / 100f),
                value.cannonReload * (1f + percent.cannonReload / 100f));
    }
}
