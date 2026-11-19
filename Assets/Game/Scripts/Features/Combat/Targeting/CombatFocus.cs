using UnityEngine;

namespace Game
{
    /// <summary>
    /// The player's focus for the auto guns: while the right mouse button is held, the machine gun and the turrets
    /// fire at the cursor point on the ground instead of the nearest enemy. <see cref="ControllableFireController"/>
    /// writes it every frame; <see cref="AutoGunBehaviour"/> and <see cref="TurretsBehaviour"/> read it.
    /// <see cref="Enabled"/> is set by <see cref="TargetingSetup"/> while a tank has auto guns: without it the
    /// right button fires the secondary gun as before.
    /// </summary>
    public static class CombatFocus
    {
        /// <summary>Auto guns are attached and the focus mode is on (WeaponSettings.focusMode).</summary>
        public static bool Enabled { get; set; }

        /// <summary>The right button is held this frame and the cursor hit the ground plane.</summary>
        public static bool Active { get; private set; }

        /// <summary>Where the cursor points on the ground (the tank's height).</summary>
        public static Vector3 Point { get; private set; }

        public static void Set(Vector3 point)
        {
            Point = point;
            Active = true;
        }

        public static void Clear() => Active = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Enabled = false;
            Active = false;
            Point = default;
        }
    }
}
