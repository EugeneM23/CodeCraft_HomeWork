using UnityEngine;

namespace Game
{
    /// <summary>
    /// Writes what a run skill at a level does into the tank's <see cref="CombatModifiers"/>. Level 0 puts the
    /// skill's fields back to neutral. Does not raise Changed: the caller does, once for a batch.
    /// </summary>
    public static class SkillEffectsUseCase
    {
        /// <summary>False for a skill combat does not know (not in milestone 1): nothing changed.</summary>
        public static bool Apply(CombatModifiers modifiers, SkillId skill, int level, SkillEffectTable table)
        {
            if (modifiers == null || table == null)
                return false;

            if (skill == SkillIds.Turret)
            {
                modifiers.TurretCount = Mathf.RoundToInt(table.Value(skill, level, 0f));
                modifiers.TurretDamageFactor = table.Extra(skill, level, 1f);
                return true;
            }

            if (skill == SkillIds.AirStrike)
            {
                modifiers.AirStrikeLevel = Mathf.RoundToInt(table.Value(skill, level, 0f));
                return true;
            }

            if (skill == SkillIds.Ricochet)
            {
                modifiers.RicochetBounces = Mathf.RoundToInt(table.Value(skill, level, 0f));
                return true;
            }

            if (skill == SkillIds.RateOfFire)
            {
                modifiers.CannonReloadFactor = table.Value(skill, level, 1f);
                return true;
            }

            if (skill == SkillIds.Armor)
            {
                modifiers.MaxHealthFactor = table.Value(skill, level, 1f);
                return true;
            }

            if (skill == SkillIds.Magnet)
            {
                modifiers.PickupRadiusFactor = table.Value(skill, level, 1f);
                return true;
            }

            return false;
        }
    }
}
