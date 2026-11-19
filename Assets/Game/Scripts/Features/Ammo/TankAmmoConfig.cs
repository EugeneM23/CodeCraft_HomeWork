using UnityEngine;

namespace Game
{
    /// <summary>
    /// The cannon's shell types: high-explosive and armour-piercing. Loaded from Resources (TankAmmoConfig); without
    /// the asset the code defaults hold.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Combat/Tank Ammo Config", fileName = "TankAmmoConfig")]
    public sealed class TankAmmoConfig : ScriptableObject
    {
        public const string RESOURCE_PATH = "TankAmmoConfig";

        [SerializeField] private ShellAmmo _highExplosive = ShellAmmo.DefaultHighExplosive;

        [SerializeField] private ShellAmmo _armourPiercing = ShellAmmo.DefaultArmourPiercing;

        [Tooltip("The shell loaded when the tank appears.")]
        [SerializeField] private ShellKind _start = ShellKind.HighExplosive;

        private static TankAmmoConfig s_loaded;

        public ShellKind Start => _start;

        public ShellAmmo Get(ShellKind kind) => kind == ShellKind.ArmourPiercing ? _armourPiercing : _highExplosive;

        public void Set(ShellKind kind, ShellAmmo ammo)
        {
            if (kind == ShellKind.ArmourPiercing)
                _armourPiercing = ammo;
            else
                _highExplosive = ammo;
        }

        /// <summary>The asset from Resources, or one with the code defaults (kept once loaded).</summary>
        public static TankAmmoConfig Load()
        {
            if (s_loaded != null)
                return s_loaded;
            s_loaded = Resources.Load<TankAmmoConfig>(RESOURCE_PATH);
            if (s_loaded == null)
                s_loaded = CreateInstance<TankAmmoConfig>();
            return s_loaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => s_loaded = null;
    }
}
