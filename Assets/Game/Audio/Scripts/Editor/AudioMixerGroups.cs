using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.AudioEngine.Adapters.EditorTools
{
    /// <summary>The mixer of the game and its channel groups (<see cref="AudioChannel"/> name = group name).</summary>
    internal static class AudioMixerGroups
    {
        public const string MASTER = "Master";

        public static AudioMixer LoadMixer() => AssetDatabase.LoadAssetAtPath<AudioMixer>(AudioAssetPaths.MIXER);

        public static AudioMixerGroup Find(AudioMixer mixer, AudioChannel channel)
        {
            if (mixer == null)
                return null;

            string name = channel.ToString();
            foreach (AudioMixerGroup group in mixer.FindMatchingGroups(MASTER))
                if (group.name == name)
                    return group;

            Debug.LogWarning($"Audio: mixer group {name} not found in {AudioAssetPaths.MIXER}");
            return null;
        }

        public static string VolumeParameter(AudioChannel channel) => channel + "Volume";
    }
}
