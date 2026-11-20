using System;

namespace Game
{
    /// <summary>
    /// What the run skills change on the tank, on top of its loadout stats. Skills write it and call
    /// <see cref="RaiseChanged"/>; the weapons, the orb pickup and the stat applier read it. Tank key: CombatModifiers.
    /// </summary>
    public sealed class CombatModifiers
    {
        /// <summary>Raised after skills changed something.</summary>
        public event Action Changed;

        /// <summary>Cannon reload multiplier (rate of fire: 0.88 at level 1).</summary>
        public float CannonReloadFactor = 1f;

        /// <summary>Max health multiplier (armor: 1.15 at level 1).</summary>
        public float MaxHealthFactor = 1f;

        /// <summary>Xp pickup radius multiplier (magnet: 1.5 at level 1).</summary>
        public float PickupRadiusFactor = 1f;

        /// <summary>Times a cannon shell bounces on to another enemy after its blast.</summary>
        public int RicochetBounces;

        /// <summary>Machine-gun turrets on the hull.</summary>
        public int TurretCount;

        /// <summary>Turret damage multiplier.</summary>
        public float TurretDamageFactor = 1f;

        /// <summary>Air strike skill level, 0: none.</summary>
        public int AirStrikeLevel;

        /// <summary>
        /// The «Мощный фугас» skill: how much harder the tank's HE shells throw gore and debris and bite houses (1: as
        /// without the skill, +0.3 a level). Read per hit by ShellPowerUseCase.
        /// </summary>
        public float HeFactor = 1f;

        public void RaiseChanged() => Changed?.Invoke();

        public void Reset()
        {
            CannonReloadFactor = 1f;
            MaxHealthFactor = 1f;
            PickupRadiusFactor = 1f;
            RicochetBounces = 0;
            TurretCount = 0;
            TurretDamageFactor = 1f;
            AirStrikeLevel = 0;
            HeFactor = 1f;
            RaiseChanged();
        }
    }
}
