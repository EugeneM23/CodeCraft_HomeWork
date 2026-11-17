using UnityEditor;
using UnityEngine;

namespace Game.AudioEngine.Adapters.EditorTools
{
    /// <summary>
    /// On entering Play mode: one warning per key whose event has no clip, once per editor session.
    /// A key without a clip is silent by design, this only reminds where to drop the file.
    /// </summary>
    [InitializeOnLoad]
    internal static class MissingClipCheck
    {
        private const string SESSION_KEY = "Game.Audio.MissingClipCheck.Done";

        static MissingClipCheck()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode || SessionState.GetBool(SESSION_KEY, false))
                return;

            SessionState.SetBool(SESSION_KEY, true);
            foreach (string id in AudioBankSync.FindSoundsWithoutClips())
            {
                int dot = id.IndexOf('.');
                string folder = AudioAssetPaths.KeyFolder(id.Substring(0, dot), id.Substring(dot + 1));
                Debug.LogWarning($"Audio: {id} has no clip — drop a file into {folder}/");
            }
        }
    }
}
