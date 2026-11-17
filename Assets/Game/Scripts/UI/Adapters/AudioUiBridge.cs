using UnityEngine;

namespace Game.UI.Adapters
{
    /// <summary>
    /// Hands the menus the sound team's volumes and interface sounds. The Game assembly cannot see the audio
    /// adapters (they reference Game), so this small assembly sets them into AppServices before the first scene.
    /// </summary>
    public static class AudioUiBridge
    {
        // After AppServices resets itself (SubsystemRegistration) and before any scene with UI wakes up
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            AppServices.Audio = new AudioUiVolumes();
            AppServices.UiSounds = new AudioUiSounds();
        }
    }
}
