using System;
using System.Collections.Generic;
using AudioEngine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.AudioEngine.Adapters.EditorTools
{
    /// <summary>
    /// Tools/Audio/Validate: the mixer has Master with the channel groups under it and an exposed
    /// &lt;Channel&gt;Volume per channel; every layout key has its event and bank entry; the GameAudio and AppAudio prefabs are wired.
    /// </summary>
    public static class AudioSetupValidator
    {
        private const string MENU = "Tools/Audio/Validate";

        [MenuItem(MENU, priority = 2)]
        public static void ValidateFromMenu() => Validate();

        /// <summary>Returns the problems found (empty when all is well) and logs them.</summary>
        public static List<string> Validate()
        {
            List<string> problems = new List<string>();
            ValidateMixer(problems);
            ValidateBanks(problems);
            problems.AddRange(AudioPrefabCheck.FindProblems(AudioAssetPaths.GAME_AUDIO_PREFAB));
            problems.AddRange(AudioPrefabCheck.FindProblems(AudioAssetPaths.APP_AUDIO_PREFAB));

            if (problems.Count == 0)
                Debug.Log("Audio validate: mixer, banks and events OK");
            else
                Debug.LogError($"Audio validate: {problems.Count} problems\n{string.Join("\n", problems)}");

            return problems;
        }

        private static void ValidateMixer(List<string> problems)
        {
            AudioMixer mixer = AudioMixerGroups.LoadMixer();
            if (mixer == null)
            {
                problems.Add($"mixer {AudioAssetPaths.MIXER} not found");
                return;
            }

            AudioMixerGroup[] groups = mixer.FindMatchingGroups(AudioMixerGroups.MASTER);
            foreach (AudioChannel channel in (AudioChannel[])Enum.GetValues(typeof(AudioChannel)))
            {
                string path = channel == AudioChannel.Master ? AudioMixerGroups.MASTER : $"{AudioMixerGroups.MASTER}/{channel}";
                if (Array.Find(groups, g => g.name == channel.ToString()) == null)
                    problems.Add($"mixer group {path} missing");

                string parameter = AudioMixerGroups.VolumeParameter(channel);
                if (!mixer.GetFloat(parameter, out _))
                    problems.Add($"exposed mixer parameter {parameter} missing");
            }
        }

        private static void ValidateBanks(List<string> problems)
        {
            foreach (string bank in AudioLayout.Banks)
            {
                AudioBank asset = AssetDatabase.LoadAssetAtPath<AudioBank>(AudioAssetPaths.BankAsset(bank));
                if (asset == null)
                {
                    problems.Add($"bank {AudioAssetPaths.BankAsset(bank)} missing (run Tools/Audio/Sync Banks)");
                    continue;
                }

                HashSet<string> ids = new HashSet<string>(asset.GetEventIds());
                foreach (AudioLayout.Sound sound in AudioLayout.Sounds)
                {
                    if (sound.Bank != bank)
                        continue;
                    if (!ids.Contains(sound.Key))
                        problems.Add($"bank {bank} has no event {sound.Key}");
                    if (AudioBankSync.LoadEvent(sound) == null)
                        problems.Add($"event {AudioAssetPaths.EventAsset(sound.Bank, sound.Key)} missing");
                }
            }
        }
    }
}
