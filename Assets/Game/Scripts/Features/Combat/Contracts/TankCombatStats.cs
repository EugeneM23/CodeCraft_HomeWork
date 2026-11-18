namespace Game
{
    /// <summary>
    /// A tank's combat numbers with a loadout, before run skills. <see cref="TankStatsUseCase"/> computes them;
    /// the hangar compares two of them ("before → after").
    /// </summary>
    public struct TankCombatStats
    {
        public float Health;
        public float Speed;
        public float BoostSpeed;
        public float TurretSpeed;

        public float CannonDamage;
        public float CannonRadius;
        public float CannonReload;

        /// <summary>Enemies a shell passes through before its blast (armour-piercing gun); 0 — bursts on the first.</summary>
        public int CannonPierce;

        public float MgDamage;
        public float MgInterval;
        public float MgSpread;
        public float MgRange;

        public float CrushSpeed;
        public float CrushWidthFactor;

        public float SpecialDuration;
        public float SpecialCooldown;
    }
}
