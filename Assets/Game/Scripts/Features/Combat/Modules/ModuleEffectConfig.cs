using System;
using System.Collections.Generic;

namespace Game
{
    /// <summary>
    /// One tank module's effect (GDD §6): its id, slot and the numbers it changes per level. Kept in
    /// <see cref="CombatConfig.Modules"/>; a module missing there takes its code default
    /// (<see cref="ModuleEffectDefaults"/>).
    /// </summary>
    [Serializable]
    public sealed class ModuleEffectConfig
    {
        public ModuleId Id;
        public ModuleSlot Slot;
        public ModuleStatEffect[] Effects = new ModuleStatEffect[0];

        public ModuleEffectConfig()
        {
        }

        public ModuleEffectConfig(ModuleId id, ModuleSlot slot, params ModuleStatEffect[] effects)
        {
            Id = id;
            Slot = slot;
            Effects = effects ?? new ModuleStatEffect[0];
        }

        /// <summary>The module's effect: from the configured list if it is there, else the code default; null if neither.</summary>
        public static ModuleEffectConfig Find(IReadOnlyList<ModuleEffectConfig> configured, ModuleId id)
        {
            if (id.IsNone)
                return null;

            if (configured != null)
            {
                for (int i = 0; i < configured.Count; i++)
                {
                    ModuleEffectConfig config = configured[i];
                    if (config != null && config.Id == id)
                        return config;
                }
            }

            return ModuleEffectDefaults.Find(id);
        }
    }
}
