using System;

namespace Game
{
    /// <summary>Channel volumes 0..1. Channels: master, music, effects, ui, ambient (AudioChannels).</summary>
    [Serializable]
    public sealed class AudioSettingsData
    {
        public float master = 1f;
        public float music = 1f;
        public float effects = 1f;
        public float ui = 1f;
        public float ambient = 1f;
    }
}
