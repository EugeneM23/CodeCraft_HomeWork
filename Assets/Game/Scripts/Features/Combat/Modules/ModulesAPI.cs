using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    /// <summary>Entity keys the tank's loadout writes on its weapons (LoadoutApplyUseCase).</summary>
    [EntityExtensionsAPI]
    public static partial class ModulesAPI
    {
        /// <summary>
        /// On the cannon: the blast radius (m) of the shells it fires. The shell spawn copies it onto each shell's
        /// ExplosionRadius (Weapons/Bullets, the targeting area).
        /// </summary>
        public static readonly ValueKey<IEntity, IVariable<float>> ShellRadius = new(nameof(ShellRadius));

        /// <summary>On an automatic weapon (the machine gun): how far (m) it looks for a target.</summary>
        public static readonly ValueKey<IEntity, IVariable<float>> WeaponRange = new(nameof(WeaponRange));
    }
}
