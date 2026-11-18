using System;

namespace Game
{
    /// <summary>The hangar's slot ids: the core's ModuleSlot names, in ModuleSlot order.</summary>
    public static class HangarSlots
    {
        public static readonly string[] Ids = Enum.GetNames(typeof(ModuleSlot));

        public static string Id(ModuleSlot slot) => Ids[(int)slot];

        /// <summary>Position of the slot id in Ids; -1 when unknown.</summary>
        public static int IndexOf(string slotId)
        {
            for (int i = 0; i < Ids.Length; i++)
                if (Ids[i] == slotId)
                    return i;
            return -1;
        }
    }
}
