using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The tank's numbers shown in the hangar and the shop. Module levels add to the base values.
    /// Placeholder set until the game design document.
    /// </summary>
    [Serializable]
    public struct TankStats
    {
        public const int COUNT = 6;
        public const int RELOAD_INDEX = 4;

        public float health;
        public float armor;
        public float speed;
        public float damage;
        [Tooltip("Seconds between main gun shots; a module lowers it with a negative value")]
        public float reload;
        [Tooltip("Machine gun shots per second")]
        public float fireRate;

        /// <summary>Stat by index 0..COUNT-1, in the order of the fields.</summary>
        public float this[int index] => index switch
        {
            0 => health,
            1 => armor,
            2 => speed,
            3 => damage,
            4 => reload,
            5 => fireRate,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

        /// <summary>For the stat at index, is a larger number better (reload: smaller is better).</summary>
        public static bool HigherIsBetter(int index) => index != RELOAD_INDEX;

        public static TankStats operator +(TankStats a, TankStats b) => new()
        {
            health = a.health + b.health,
            armor = a.armor + b.armor,
            speed = a.speed + b.speed,
            damage = a.damage + b.damage,
            reload = a.reload + b.reload,
            fireRate = a.fireRate + b.fireRate
        };
    }
}
