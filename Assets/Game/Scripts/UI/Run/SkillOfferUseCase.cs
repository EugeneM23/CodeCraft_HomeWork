using System;
using System.Collections.Generic;

namespace Game
{
    /// <summary>
    /// Draws the cards of a skill choice (GDD §3): up to `count` different skills of the pool, each a new skill
    /// (from level 0) or the next level of a taken one. A skill at the top level is not offered; a new weapon
    /// is not offered once the weapon slots are full, a new upgrade once the upgrade slots are. Called on a
    /// level up, not per frame.
    /// </summary>
    public static class SkillOfferUseCase
    {
        public const int MAX_LEVEL = 5;
        public const int MAX_WEAPONS = 4;
        public const int MAX_UPGRADES = 6;
        public const int OFFER_COUNT = 3;

        /// <summary>
        /// Fills result (cleared first) with up to count offers drawn without repeats. poolIds[i] is a weapon when
        /// poolIsWeapon[i]. candidates is a scratch list (cleared and reused).
        /// </summary>
        public static void Draw(IReadOnlyList<RunSkill> taken, IReadOnlyList<string> poolIds, IReadOnlyList<bool> poolIsWeapon,
            int count, Random random, List<SkillOffer> result, List<int> candidates)
        {
            result.Clear();
            candidates.Clear();

            int weapons = 0;
            int upgrades = 0;
            for (int i = 0; i < taken.Count; i++)
            {
                if (taken[i].IsWeapon)
                    weapons++;
                else
                    upgrades++;
            }

            for (int i = 0; i < poolIds.Count; i++)
            {
                int level = LevelOf(taken, poolIds[i]);
                if (level >= MAX_LEVEL)
                    continue;
                if (level == 0 && (poolIsWeapon[i] ? weapons >= MAX_WEAPONS : upgrades >= MAX_UPGRADES))
                    continue;
                candidates.Add(i);
            }

            // Partial Fisher–Yates: the first `count` places get distinct random candidates.
            int take = Math.Min(count, candidates.Count);
            for (int i = 0; i < take; i++)
            {
                int j = random.Next(i, candidates.Count);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);

                int index = candidates[i];
                int level = LevelOf(taken, poolIds[index]);
                result.Add(new SkillOffer(poolIds[index], level, level + 1, poolIsWeapon[index]));
            }
        }

        /// <summary>The level of a taken skill, 0 when it is not taken.</summary>
        public static int LevelOf(IReadOnlyList<RunSkill> taken, string id)
        {
            for (int i = 0; i < taken.Count; i++)
            {
                if (taken[i].Id == id)
                    return taken[i].Level;
            }
            return 0;
        }

        /// <summary>
        /// Applies a chosen offer to the taken list: raises the skill's level, or adds it (weapons before upgrades).
        /// </summary>
        public static void Apply(List<RunSkill> taken, SkillOffer offer)
        {
            for (int i = 0; i < taken.Count; i++)
            {
                if (taken[i].Id != offer.Id)
                    continue;
                taken[i] = new RunSkill(offer.Id, offer.ToLevel, taken[i].IsWeapon);
                return;
            }

            var skill = new RunSkill(offer.Id, offer.ToLevel, offer.IsWeapon);
            if (!offer.IsWeapon)
            {
                taken.Add(skill);
                return;
            }

            int at = 0;
            while (at < taken.Count && taken[at].IsWeapon)
                at++;
            taken.Insert(at, skill);
        }
    }
}
