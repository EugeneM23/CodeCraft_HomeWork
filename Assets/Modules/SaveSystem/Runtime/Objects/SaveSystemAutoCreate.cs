using System;
using UnityEngine;

namespace Saving.Objects
{
    /// <summary>
    /// A save system for scenes that have none: the first Saveable that joins the registry while no SaveLoadSystem is in
    /// play makes "SaveSystem (auto)" — DontDestroyOnLoad, the project's SaveSystemSettings found in a Resources folder,
    /// SaveLoadSystem's own defaults (slot 0, files in persistentDataPath/Saves, autosave every 30 s of game time, saves
    /// on pause, focus loss and quit, no Load On Start) — and loads its slot in place, without switching scenes. A
    /// SaveSystem in the scene or one made by code earlier wins: it wakes before the Saveables. No settings asset there,
    /// Create System When Missing off or a setup that throws — nothing is made, and that answer holds until Play ends or
    /// the lookup is replaced.
    /// </summary>
    internal static class SaveSystemAutoCreate
    {
        public const string SYSTEM_NAME = "SaveSystem (auto)";

        private static Func<SaveSystemSettings> _findSettings = FromResources;
        private static bool _declined;

        /// <summary>
        /// Where the settings come from: Resources; tests put their own. A new lookup forgets an earlier "no".
        /// </summary>
        internal static Func<SaveSystemSettings> FindSettings
        {
            get => _findSettings;
            set
            {
                _findSettings = value ?? FromResources;
                _declined = false;
            }
        }

        /// <summary>Runs on the new system before it starts: tests give it a storage in memory.</summary>
        internal static Action<SaveLoadSystem> BeforeStart;

        /// <summary>The start of Play: settings from Resources, no setup hook, nothing declined yet. Tests call it too.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        internal static void Reset()
        {
            FindSettings = FromResources; // the setter also forgets an earlier "no"
            BeforeStart = null;
            // A system in a scene with an empty settings field (a demo of the module installed in another project) takes
            // the project's settings from Resources: not the hook above, which tests switch off for the auto-system only
            SaveLoadSystem.FallbackSettings = FromResources;
            // After SaveRegistry clears its events in SubsystemRegistration
            SaveRegistry.Registered -= OnRegistered;
            SaveRegistry.Registered += OnRegistered;
        }

        private static void OnRegistered(Saveable saveable)
        {
            if (_declined || SaveLoadSystem.Current != null || !Application.isPlaying)
                return;

            SaveLoadSystem system = null;
            try
            {
                SaveSystemSettings settings = _findSettings();
                if (settings == null || !settings.CreateSystemWhenMissing)
                {
                    // Remembered: every spawn of a Saveable would look for the asset again
                    _declined = true;
                    return;
                }

                // Its Awake makes it Current and keeps it across scenes, so the Saveables that follow find it
                system = new GameObject(SYSTEM_NAME).AddComponent<SaveLoadSystem>();
                system.UseSettings(settings);
                BeforeStart?.Invoke(system);
                system.LoadInPlace();
            }
            catch (Exception exception)
            {
                // Raised from the registry, inside the scan of a loading scene: a broken setup must not stop the scan,
                // and a half-built system must not run — it never loaded its slot, so its saves would overwrite it
                _declined = true;
                Debug.LogException(exception, saveable);
                if (system != null)
                {
                    system.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(system.gameObject);
                }
            }
        }

        private static SaveSystemSettings FromResources() =>
            Resources.Load<SaveSystemSettings>(SaveSystemSettings.RESOURCES_NAME);
    }
}
