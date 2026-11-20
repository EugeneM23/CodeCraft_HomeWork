using System.Collections.Generic;

namespace Game
{
    /// <summary>
    /// The numbers of the milestone's six run skills per level: the code defaults (GDD §3), overridden per skill by
    /// <see cref="CombatConfig.Skills"/> where an asset gives a full row of five levels. Built once per tank attach;
    /// read only when a skill changes.
    /// </summary>
    public sealed class SkillEffectTable
    {
        public const int LEVELS = SkillConfig.MAX_LEVEL;

        /// <summary>The skills of milestone 1, the ones combat knows how to apply.</summary>
        public static readonly SkillId[] MilestoneSkills =
        {
            SkillIds.Turret, SkillIds.AirStrike, SkillIds.Ricochet, SkillIds.RateOfFire, SkillIds.Armor, SkillIds.Magnet,
            SkillIds.HighExplosive
        };

        private readonly Dictionary<SkillId, float[]> _values = new();
        private readonly Dictionary<SkillId, float[]> _extra = new();

        public SkillEffectTable(SkillEffectConfig[] configs = null)
        {
            _values[SkillIds.Turret] = new[] { 1f, 1f, 2f, 2f, 3f };
            _extra[SkillIds.Turret] = new[] { 1f, 1.2f, 1.2f, 1.44f, 1.44f };
            _values[SkillIds.AirStrike] = new[] { 1f, 2f, 3f, 4f, 5f };
            _values[SkillIds.Ricochet] = new[] { 1f, 2f, 3f, 4f, 5f };
            _values[SkillIds.RateOfFire] = new[] { 0.88f, 0.80f, 0.72f, 0.64f, 0.56f };
            _values[SkillIds.Armor] = new[] { 1.15f, 1.30f, 1.45f, 1.60f, 1.75f };
            _values[SkillIds.Magnet] = new[] { 1.5f, 1.8f, 2.1f, 2.4f, 2.7f };
            // «Мощный фугас»: +30 % a level to the HE shell's gore and debris throw and its bite in houses
            _values[SkillIds.HighExplosive] = new[] { 1.3f, 1.6f, 1.9f, 2.2f, 2.5f };

            if (configs == null)
                return;

            for (int i = 0; i < configs.Length; i++)
            {
                SkillEffectConfig config = configs[i];
                if (config == null || config.Id.IsNone)
                    continue;

                if (config.Values != null && config.Values.Length >= LEVELS)
                    _values[config.Id] = config.Values;
                if (config.Extra != null && config.Extra.Length >= LEVELS)
                    _extra[config.Id] = config.Extra;
            }
        }

        /// <summary>The skill's main number at a level 1..5; <paramref name="fallback"/> for an unknown skill or level 0.</summary>
        public float Value(SkillId skill, int level, float fallback) => Read(_values, skill, level, fallback);

        /// <summary>The skill's second number (turret: damage factor) at a level 1..5.</summary>
        public float Extra(SkillId skill, int level, float fallback) => Read(_extra, skill, level, fallback);

        private static float Read(Dictionary<SkillId, float[]> table, SkillId skill, int level, float fallback)
        {
            if (level <= 0 || !table.TryGetValue(skill, out float[] row) || row == null || row.Length == 0)
                return fallback;

            int index = level > row.Length ? row.Length - 1 : level - 1;
            return row[index];
        }
    }
}
