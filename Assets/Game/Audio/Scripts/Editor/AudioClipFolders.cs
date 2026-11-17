using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.AudioEngine.Adapters.EditorTools
{
    /// <summary>
    /// The clip folders of the keys: creating them (with a README so git keeps them), finding the clips the
    /// user dropped in, loading the default clips of a layout row.
    /// </summary>
    internal static class AudioClipFolders
    {
        /// <summary>
        /// Creates the folder on disk with a README.txt in it. Returns true when something was written; the
        /// caller refreshes the AssetDatabase once after a batch of these.
        /// </summary>
        public static bool EnsureOnDisk(string assetFolder, string readmeText)
        {
            string fullPath = ToFullPath(assetFolder);
            bool written = false;
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                written = true;
            }

            if (readmeText == null)
                return written;

            string readme = Path.Combine(fullPath, AudioAssetPaths.README_NAME);
            if (!File.Exists(readme))
            {
                File.WriteAllText(readme, readmeText + "\n");
                written = true;
            }

            return written;
        }

        /// <summary>Every AudioClip inside the folder (and its subfolders), sorted by path.</summary>
        public static List<AudioClip> FindClips(string assetFolder)
        {
            List<AudioClip> clips = new List<AudioClip>();
            if (!AssetDatabase.IsValidFolder(assetFolder))
                return clips;

            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { assetFolder });
            List<string> paths = new List<string>(guids.Length);
            foreach (string guid in guids)
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(StringComparer.Ordinal);

            foreach (string path in paths)
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null && !clips.Contains(clip))
                    clips.Add(clip);
            }

            return clips;
        }

        /// <summary>Clips of a ';'-separated list of paths relative to Assets/Game/Audio. Missing ones are logged.</summary>
        public static List<AudioClip> LoadDefaultClips(string id, string defaultClips)
        {
            List<AudioClip> clips = new List<AudioClip>();
            if (string.IsNullOrEmpty(defaultClips))
                return clips;

            foreach (string part in defaultClips.Split(';'))
            {
                string relative = part.Trim();
                if (relative.Length == 0)
                    continue;

                string path = $"{AudioAssetPaths.ROOT}/{relative}";
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null)
                    Debug.LogWarning($"Audio: default clip of {id} not found: {path}");
                else if (!clips.Contains(clip))
                    clips.Add(clip);
            }

            return clips;
        }

        public static void Ping(string assetFolder)
        {
            UnityEngine.Object folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetFolder);
            if (folder == null)
            {
                Debug.LogWarning($"Audio: {assetFolder} does not exist yet, run Tools/Audio/Sync Banks");
                return;
            }

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);
        }

        private static string ToFullPath(string assetPath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot ?? string.Empty, assetPath);
        }
    }
}
