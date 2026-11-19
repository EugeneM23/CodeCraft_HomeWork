using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    /// <summary>Entity keys of the weapons and targeting area (cannon shells, auto guns, skills).</summary>
    [EntityExtensionsAPI]
    public static partial class TargetingAPI
    {
        /// <summary>
        /// On the CANNON weapon entity: enemies a shell passes through before its blast (the armour-piercing gun).
        /// The modules area sets it (<see cref="TargetingSetup.GetOrAddShellPierce"/> adds it when missing);
        /// every shot copies it into the shell's <see cref="BulletPierce"/>. Missing or 0: the shell bursts on the first.
        /// </summary>
        public static readonly ValueKey<IEntity, IVariable<int>> ShellPierce = new(nameof(ShellPierce));

        /// <summary>On a bullet or shell in flight: enemies it may still pass through.</summary>
        public static readonly ValueKey<IEntity, IVariable<int>> BulletPierce = new(nameof(BulletPierce));

        /// <summary>On a bullet or shell in flight: the weapon entity that fired it (ricochet skill reads its owner).</summary>
        public static readonly ValueKey<IEntity, IVariable<IEntity>> BulletWeapon = new(nameof(BulletWeapon));

        /// <summary>On a cannon shell in flight: skill ricochets it has already made (0 for a fresh shot).</summary>
        public static readonly ValueKey<IEntity, IVariable<int>> ShellBounces = new(nameof(ShellBounces));

        /// <summary>On a weapon entity mounted on a vehicle: the vehicle (the player's tank). Set at attach.</summary>
        public static readonly ValueKey<IEntity, IEntity> WeaponOwner = new(nameof(WeaponOwner));
    }
}
