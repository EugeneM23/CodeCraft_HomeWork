namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// Volume channels of the settings menu. Each is a group of Assets/Game/Audio/AudioMixer.mixer with an
    /// exposed volume parameter named &lt;Channel&gt;Volume (MasterVolume, MusicVolume...).
    /// </summary>
    public enum AudioChannel
    {
        Master = 0,
        Music = 1,
        Effects = 2,
        UI = 3,
        Ambient = 4
    }
}
