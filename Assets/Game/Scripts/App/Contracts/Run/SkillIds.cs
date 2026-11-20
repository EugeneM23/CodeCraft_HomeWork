namespace Game
{
    /// <summary>The 20 run skills of GDD §3. Milestone 1 has six: Turret, AirStrike, Ricochet, RateOfFire, Armor, Magnet.</summary>
    public static class SkillIds
    {
        // Weapons (up to 4 in a run)
        public static readonly SkillId Turret = new("turret");
        public static readonly SkillId SideFlamers = new("side_flamers");
        public static readonly SkillId Mortar = new("mortar");
        public static readonly SkillId Rockets = new("rockets");
        public static readonly SkillId RearMines = new("rear_mines");
        public static readonly SkillId AirStrike = new("air_strike");

        // Gun (upgrades, up to 6 together with the tank's)
        public static readonly SkillId Ricochet = new("ricochet");
        public static readonly SkillId DoubleShot = new("double_shot");
        public static readonly SkillId Piercing = new("piercing");
        public static readonly SkillId BigCaliber = new("big_caliber");
        public static readonly SkillId RateOfFire = new("rate_of_fire");
        public static readonly SkillId Shrapnel = new("shrapnel");
        public static readonly SkillId HighExplosive = new("high_explosive");

        // Tank (upgrades)
        public static readonly SkillId TrackSpikes = new("track_spikes");
        public static readonly SkillId Armor = new("armor");
        public static readonly SkillId Regeneration = new("regeneration");
        public static readonly SkillId Magnet = new("magnet");
        public static readonly SkillId Boost = new("boost");
        public static readonly SkillId Roller = new("roller");
        public static readonly SkillId Shake = new("shake");
        public static readonly SkillId Luck = new("luck");
    }
}
