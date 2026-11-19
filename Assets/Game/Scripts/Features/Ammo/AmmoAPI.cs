using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    /// <summary>Entity keys of the cannon's shell types.</summary>
    [EntityExtensionsAPI]
    public static partial class AmmoAPI
    {
        /// <summary>On the CANNON weapon entity: the shell type loaded (AmmoUseCase adds it on first use).</summary>
        public static readonly ValueKey<IEntity, IReactiveVariable<ShellKind>> CannonAmmo = new(nameof(CannonAmmo));

        /// <summary>On a cannon shell in flight: its type, written on every shot.</summary>
        public static readonly ValueKey<IEntity, IVariable<ShellKind>> ShellAmmoKind = new(nameof(ShellAmmoKind));

        /// <summary>On a cannon shell: its own damage holder (the weapon's damage times the type's factor).</summary>
        public static readonly ValueKey<IEntity, IReactiveVariable<int>> ShellDamage = new(nameof(ShellDamage));
    }
}
