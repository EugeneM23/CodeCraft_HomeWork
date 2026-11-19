using System;

namespace Game
{
    /// <summary>
    /// Slot names as strings (the save's loadout keys, the old menu's IPlayerProgress slots) to <see cref="ModuleSlot"/>.
    /// Lenient: case, '_', '-' and spaces are ignored. Accepted: every ModuleSlot name (Gun, MachineGun, Armor, Engine,
    /// Tracks, Special) and the aliases "cannon" (Gun), "mg" (MachineGun), "armour" (Armor), "track" (Tracks), "e"
    /// (Special). The old menu's TankSlot names are the same except "Hull", which has no ModuleSlot: it does not parse
    /// (its "Armor" is the Armor slot, so mapping Hull there too would make two menu slots fight over one).
    /// </summary>
    public static class ModuleSlotNames
    {
        /// <summary>The name the save writes for the slot: the enum name.</summary>
        public static string Key(ModuleSlot slot) => slot.ToString();

        public static bool TryParse(string name, out ModuleSlot slot)
        {
            slot = default;
            if (string.IsNullOrEmpty(name))
                return false;

            string key = name.Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty)
                .ToLowerInvariant();
            switch (key)
            {
                case "gun":
                case "cannon":
                    slot = ModuleSlot.Gun;
                    return true;
                case "machinegun":
                case "mg":
                    slot = ModuleSlot.MachineGun;
                    return true;
                case "armor":
                case "armour":
                    slot = ModuleSlot.Armor;
                    return true;
                case "engine":
                    slot = ModuleSlot.Engine;
                    return true;
                case "tracks":
                case "track":
                    slot = ModuleSlot.Tracks;
                    return true;
                case "special":
                case "e":
                    slot = ModuleSlot.Special;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>All six slots in ModuleSlot order.</summary>
        public static readonly ModuleSlot[] All = (ModuleSlot[])Enum.GetValues(typeof(ModuleSlot));
    }
}
