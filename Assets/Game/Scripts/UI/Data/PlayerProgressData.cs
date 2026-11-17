using System;
using System.Collections.Generic;

namespace Game
{
    /// <summary>The saved progress: plain data for JSON. Changed through ShopUseCase and saved by IPlayerProgress.</summary>
    [Serializable]
    public sealed class PlayerProgressData
    {
        public int version = 1;
        public bool initialized;
        public int credits;
        public List<OwnedModule> modules = new();
        public List<InstalledModule> installed = new();
        public List<string> completedLevels = new();

        /// <summary>Level of an owned module, 0 when it is not owned.</summary>
        public int ModuleLevel(string id)
        {
            for (int i = 0; i < modules.Count; i++)
                if (modules[i].id == id)
                    return modules[i].level;
            return 0;
        }

        public void SetModuleLevel(string id, int level)
        {
            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i].id == id)
                {
                    modules[i].level = level;
                    return;
                }
            }
            modules.Add(new OwnedModule { id = id, level = level });
        }

        /// <summary>Id of the module in the slot, or null.</summary>
        public string Installed(TankSlot slot)
        {
            for (int i = 0; i < installed.Count; i++)
                if (installed[i].slot == slot)
                    return installed[i].id;
            return null;
        }

        public void Install(TankSlot slot, string id)
        {
            for (int i = 0; i < installed.Count; i++)
            {
                if (installed[i].slot == slot)
                {
                    installed[i].id = id;
                    return;
                }
            }
            installed.Add(new InstalledModule { slot = slot, id = id });
        }

        [Serializable]
        public sealed class OwnedModule
        {
            public string id;
            public int level;
        }

        [Serializable]
        public sealed class InstalledModule
        {
            public TankSlot slot;
            public string id;
        }
    }
}
