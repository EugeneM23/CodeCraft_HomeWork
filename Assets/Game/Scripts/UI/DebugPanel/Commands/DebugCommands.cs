namespace Game
{
    /// <summary>
    /// The debug panel's own entries, registered once per game when the panel is made. Features in other assemblies
    /// add theirs from their own <c>[RuntimeInitializeOnLoadMethod]</c> (the horde: HordeDebugCommands).
    /// </summary>
    public static class DebugCommands
    {
        private static bool s_registered;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_registered = false;

        public static void RegisterOnce()
        {
            if (s_registered)
                return;

            s_registered = true;
            SkillDebugCommands.Register();
            TankDebugCommands.Register();
            WorldDebugCommands.Register();
            RunDebugCommands.Register();
        }

        /// <summary>Entries that need the app's catalog: added on the panel's first open with the app running.</summary>
        public static void EnsureCatalogEntries() => SkillDebugCommands.EnsureSkillRows();
    }
}
