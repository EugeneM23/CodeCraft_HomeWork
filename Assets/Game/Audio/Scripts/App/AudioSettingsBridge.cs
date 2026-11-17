using System;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// Feeds the saved volumes (AppContext settings, GameSettings.audio: master/music/effects/ui/ambient) into the
    /// mixer at start and on every settings change. The menu writes the settings; the sound only reads them.
    /// </summary>
    public sealed class AudioSettingsBridge : IDisposable
    {
        private static readonly AudioChannel[] Channels =
            { AudioChannel.Master, AudioChannel.Music, AudioChannel.Effects, AudioChannel.UI, AudioChannel.Ambient };

        private readonly SettingsStore _store;
        private readonly IAudioVolumes _volumes;

        public AudioSettingsBridge(SettingsStore store, IAudioVolumes volumes)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _volumes = volumes ?? throw new ArgumentNullException(nameof(volumes));
            _store.Changed += Apply;
            Apply(_store.Current);
        }

        public void Dispose() => _store.Changed -= Apply;

        /// <summary>Settings key of a channel: the same names as <see cref="AudioChannels"/>.</summary>
        public static string KeyOf(AudioChannel channel) => channel switch
        {
            AudioChannel.Master => AudioChannels.MASTER,
            AudioChannel.Music => AudioChannels.MUSIC,
            AudioChannel.Effects => AudioChannels.EFFECTS,
            AudioChannel.UI => AudioChannels.UI,
            AudioChannel.Ambient => AudioChannels.AMBIENT,
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, null)
        };

        private void Apply(GameSettings settings)
        {
            if (settings?.audio == null)
                return;

            foreach (AudioChannel channel in Channels)
                _volumes.SetVolume(channel, AudioChannels.Get(settings.audio, KeyOf(channel)));
        }
    }
}
