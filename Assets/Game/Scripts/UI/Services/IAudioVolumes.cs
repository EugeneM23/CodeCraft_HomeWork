namespace Game
{
    /// <summary>Volume of a sound channel, 0..1. Stub: Master drives AudioListener.volume; the sound team maps the rest to its mixer.</summary>
    public interface IAudioVolumes
    {
        float GetVolume(AudioChannel channel);
        void SetVolume(AudioChannel channel, float volume);
    }
}
