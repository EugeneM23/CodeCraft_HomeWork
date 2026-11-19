using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    /// <summary>Entity keys of the combat team (tank, weapons, enemies).</summary>
    [EntityExtensionsAPI]
    public static partial class CombatAPI
    {
        /// <summary>Run skill modifiers on the player's tank.</summary>
        public static readonly ValueKey<IEntity, CombatModifiers> CombatModifiers = new(nameof(CombatModifiers));

        /// <summary>The special module on key E.</summary>
        public static readonly ValueKey<IEntity, ISpecialModule> SpecialModule = new(nameof(SpecialModule));

        /// <summary>Stun state of a tank (the elite's leap). Use <see cref="TankStunUseCase"/>.</summary>
        public static readonly ValueKey<IEntity, TankStun> TankStun = new(nameof(TankStun));

        /// <summary>
        /// On an enemy: it can be crushed only at or below this share of its health (the elite: 0.25).
        /// Set by the AI team.
        /// </summary>
        public static readonly ValueKey<IEntity, IValue<float>> CrushBelowHealth = new(nameof(CrushBelowHealth));
    }
}
