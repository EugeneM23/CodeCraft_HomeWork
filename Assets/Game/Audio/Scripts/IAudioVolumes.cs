namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// Volume of each settings channel, 0..1. The one entry point for whoever owns the settings: the menu now,
    /// AppContext settings later (keys master/music/effects/ui/ambient match <see cref="AudioChannel"/>).
    /// </summary>
    public interface IAudioVolumes
    {
        float GetVolume(AudioChannel channel);

        /// <param name="volume">0..1, clamped.</param>
        void SetVolume(AudioChannel channel, float volume);
    }
}
