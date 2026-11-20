using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// How strong an HE shell's hit is: the «Мощный фугас» skill's <see cref="CombatModifiers.HeFactor"/> of the tank that
    /// fired it (the shell's BulletWeapon → the weapon's WeaponOwner, as ShellBounceUseCase reads it). It scales the
    /// gore's and the debris' throw speeds and the bite in houses. 1 — not an HE shell, no shooter known, or no skill:
    /// exactly the behaviour without it. Read once per hit; nothing allocated.
    /// </summary>
    public static class ShellPowerUseCase
    {
        public static float Of(IEntity projectile)
        {
            if (projectile == null || !ShellHitSparksUseCase.IsHighExplosive(projectile))
                return 1f;

            if (!projectile.TryGetBulletWeapon(out IVariable<IEntity> holder) || holder.Value == null)
                return 1f;

            if (!holder.Value.TryGetWeaponOwner(out IEntity owner) || owner == null ||
                !owner.TryGetCombatModifiers(out CombatModifiers modifiers) || modifiers == null)
                return 1f;

            return Mathf.Max(0.01f, modifiers.HeFactor);
        }

        /// <summary>
        /// The bite's radius factor for a power: the hole widens evenly with the skill's level, <see cref="TOP_HOLE"/>
        /// times as wide at level 5 (power 2.5); 1 without the skill.
        /// </summary>
        public static float BiteRadius(float power) =>
            power <= 1f ? 1f : 1f + (power - 1f) * (TOP_HOLE - 1f) / (TOP_POWER - 1f);

        /// <summary>How much wider the HE hole is at the skill's top level (the user's call: 2.3).</summary>
        public const float TOP_HOLE = 2.3f;

        /// <summary>The power at the skill's top level (1 + 0.3 × 5).</summary>
        private const float TOP_POWER = 2.5f;
    }
}
