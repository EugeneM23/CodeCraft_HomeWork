namespace Game
{
    /// <summary>The 15 modules of GDD §6: a standard one per slot and the bought ones.</summary>
    public static class ModuleIds
    {
        // Gun
        public static readonly ModuleId Gun88He = new("gun_88_he");
        public static readonly ModuleId GunArmorPiercing = new("gun_ap");
        public static readonly ModuleId GunHowitzer = new("gun_howitzer");

        // Machine gun
        public static readonly ModuleId MachineGun = new("mg_standard");
        public static readonly ModuleId MachineGunTwin = new("mg_twin");

        // Armor
        public static readonly ModuleId ArmorStandard = new("armor_standard");
        public static readonly ModuleId ArmorSkirts = new("armor_skirts");
        public static readonly ModuleId ArmorDozer = new("armor_dozer");

        // Engine
        public static readonly ModuleId EngineStandard = new("engine_standard");
        public static readonly ModuleId EngineBoosted = new("engine_boosted");

        // Tracks
        public static readonly ModuleId TracksStandard = new("tracks_standard");
        public static readonly ModuleId TracksWide = new("tracks_wide");

        // Special (E)
        public static readonly ModuleId SpecialSmoke = new("special_smoke");
        public static readonly ModuleId SpecialRepair = new("special_repair");
        public static readonly ModuleId SpecialShockwave = new("special_shockwave");
    }
}
