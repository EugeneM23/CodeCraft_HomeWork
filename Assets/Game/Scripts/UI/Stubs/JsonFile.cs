using System;
using System.IO;
using UnityEngine;

namespace Game
{
    /// <summary>STUB storage until the core team's SaveSystem: one JsonUtility file in persistentDataPath.</summary>
    public static class JsonFile
    {
        public static string PathOf(string fileName) => Path.Combine(Application.persistentDataPath, fileName);

        /// <summary>The object read from the file; null when there is no file or it does not parse.</summary>
        public static T Read<T>(string fileName) where T : class
        {
            string path = PathOf(fileName);
            if (!File.Exists(path))
                return null;

            try
            {
                return JsonUtility.FromJson<T>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not read {path}, using defaults: {exception.Message}");
                return null;
            }
        }

        public static void Write<T>(string fileName, T value)
        {
            string path = PathOf(fileName);
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(value, true));
            if (File.Exists(path))
                File.Delete(path);
            File.Move(temp, path);
        }
    }
}
