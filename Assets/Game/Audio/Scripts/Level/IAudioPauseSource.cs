namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// Says whether the battle is paused. Polled by <see cref="GameAudio"/> every frame. Once the core brings
    /// GameLoop events, only the implementation changes.
    /// </summary>
    public interface IAudioPauseSource
    {
        bool IsPaused { get; }
    }
}
