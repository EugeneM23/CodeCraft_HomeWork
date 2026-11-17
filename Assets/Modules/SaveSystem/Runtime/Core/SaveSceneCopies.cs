using System;
using System.Collections.Generic;

namespace Saving
{
    /// <summary>
    /// The editor only: scenes that had unsaved changes when Play started. A load that reloads such a scene would take
    /// it from disk — without the objects added since the last save of the scene, with other ids — so the editor
    /// keeps a copy of the scene as it was played, and loads go to the copy. The copy stands for the original
    /// everywhere a save names a scene: its sections and the saved scene keep the original's path. Empty in a build.
    /// </summary>
    public static class SaveSceneCopies
    {
        private static readonly Dictionary<string, string> CopyOfOriginal = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> OriginalOfCopy = new(StringComparer.Ordinal);

        /// <summary>The scene at <paramref name="original"/> is played from the copy at <paramref name="copy"/>.</summary>
        public static void Set(string original, string copy)
        {
            if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(copy))
                return;

            CopyOfOriginal[original] = copy;
            OriginalOfCopy[copy] = original;
        }

        public static void Clear()
        {
            CopyOfOriginal.Clear();
            OriginalOfCopy.Clear();
        }

        /// <summary>The path to load for a scene: its play copy if it has one.</summary>
        public static string ToLoad(string path) =>
            path != null && CopyOfOriginal.TryGetValue(path, out string copy) ? copy : path;

        /// <summary>The path a save knows a scene by: the original of a play copy.</summary>
        public static string ToSave(string path) =>
            path != null && OriginalOfCopy.TryGetValue(path, out string original) ? original : path;

        public static IReadOnlyDictionary<string, string> Copies => CopyOfOriginal;
    }
}
