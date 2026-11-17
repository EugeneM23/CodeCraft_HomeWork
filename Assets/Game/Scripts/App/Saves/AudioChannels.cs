using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Names of the fixed channels of <see cref="AudioSettingsData"/>, for code that picks a channel by string (sound
    /// banks, mixer groups, a menu slider list).
    /// </summary>
    public static class AudioChannels
    {
        public const string MASTER = "master";
        public const string MUSIC = "music";
        public const string EFFECTS = "effects";
        public const string UI = "ui";
        public const string AMBIENT = "ambient";

        public static readonly IReadOnlyList<string> All = new[] { MASTER, MUSIC, EFFECTS, UI, AMBIENT };

        public static float Get(AudioSettingsData audio, string channel)
        {
            if (audio == null)
                throw new ArgumentNullException(nameof(audio));

            return channel switch
            {
                MASTER => audio.master,
                MUSIC => audio.music,
                EFFECTS => audio.effects,
                UI => audio.ui,
                AMBIENT => audio.ambient,
                _ => throw new ArgumentException($"Unknown audio channel '{channel}'.", nameof(channel))
            };
        }

        /// <summary>Sets the channel's volume, clamped to 0..1.</summary>
        public static void Set(AudioSettingsData audio, string channel, float volume)
        {
            if (audio == null)
                throw new ArgumentNullException(nameof(audio));

            volume = Mathf.Clamp01(volume);
            switch (channel)
            {
                case MASTER: audio.master = volume; break;
                case MUSIC: audio.music = volume; break;
                case EFFECTS: audio.effects = volume; break;
                case UI: audio.ui = volume; break;
                case AMBIENT: audio.ambient = volume; break;
                default: throw new ArgumentException($"Unknown audio channel '{channel}'.", nameof(channel));
            }
        }
    }
}
