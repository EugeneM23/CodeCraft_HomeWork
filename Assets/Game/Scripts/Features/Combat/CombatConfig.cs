using UnityEngine;

namespace Game
{
    /// <summary>
    /// All combat numbers of milestone 1: the tank's base, its modules, the run skills, experience, auto weapons.
    /// One asset at <c>Assets/Game/Configs/Combat/Resources/CombatConfig.asset</c>; without it the code defaults
    /// (GDD §3, §4, §6) apply.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatConfig", menuName = "Game/Combat/Combat Config")]
    public sealed class CombatConfig : ScriptableObject
    {
        public const string RESOURCE_PATH = "CombatConfig";

        [Tooltip("Base numbers of the Tiger (GDD §4) until the core's tank catalog provides them.")]
        public TankBaseSettings Tiger = new();

        [Tooltip("Module effects by ModuleId and level (GDD §6).")]
        public ModuleEffectConfig[] Modules = new ModuleEffectConfig[0];

        [Tooltip("Run skills by SkillId and level (GDD §3).")]
        public SkillEffectConfig[] Skills = new SkillEffectConfig[0];

        [Tooltip("Experience orbs, pickup, level curve.")]
        public XpSettings Xp = new();

        [Tooltip("Auto machine gun, turrets, ricochet, air strike.")]
        public WeaponSettings Weapons = new();

        private static CombatConfig s_default;

        /// <summary>The asset from Resources, or one made with the code defaults.</summary>
        public static CombatConfig Load()
        {
            CombatConfig config = Resources.Load<CombatConfig>(RESOURCE_PATH);
            if (config != null)
                return config;

            if (s_default == null)
            {
                s_default = CreateInstance<CombatConfig>();
                s_default.name = "CombatConfig (defaults)";
                s_default.hideFlags = HideFlags.DontSave;
            }

            return s_default;
        }
    }
}
