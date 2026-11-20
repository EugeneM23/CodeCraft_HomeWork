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
        /// The bite's radius factor for a power: the bitten volume grows with the power (+30 % volume a level), so the
        /// radius grows by its cube root.
        /// </summary>
        public static float BiteRadius(float power) => power == 1f ? 1f : Mathf.Pow(power, 1f / 3f);
    }
}
