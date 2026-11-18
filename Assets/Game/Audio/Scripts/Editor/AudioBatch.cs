using UnityEditor;
using UnityEngine;

namespace Game.AudioEngine.Adapters.EditorTools
{
    /// <summary>
    /// Entry points for a headless editor (-batchmode -executeMethod), so the audio setup can be rebuilt without an
    /// editor window: Game.AudioEngine.Adapters.EditorTools.AudioBatch.SyncAndBuild.
    /// </summary>
    public static class AudioBatch
    {
        /// <summary>Sync Banks (banks, events, catalogs), Build Prefabs (wires them in), Sync again (preload list).</summary>
        public static void SyncAndBuild()
        {
            Debug.Log("[AudioBatch] " + AudioBankSync.Sync());
            AudioPrefabBuilder.Build();
            Debug.Log("[AudioBatch] " + AudioBankSync.Sync());
            AssetDatabase.SaveAssets();
        }
    }
}
