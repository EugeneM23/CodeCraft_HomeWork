using System.IO;
using Saving;
using UnityEngine;

namespace Game
{
    /// <summary>Where the game keeps its saves on this device. Tests give the services an InMemorySaveStorage instead.</summary>
    public static class SaveStores
    {
        public const string SETTINGS_FOLDER = "Settings";
        public const string PROGRESS_FOLDER = "Saves";

        public static string GameVersion => Application.version;

        public static ISaveStorage Settings() =>
            new FileSaveStorage(Path.Combine(Application.persistentDataPath, SETTINGS_FOLDER));

        public static ISaveStorage Progress() =>
            new FileSaveStorage(Path.Combine(Application.persistentDataPath, PROGRESS_FOLDER));
    }
}
