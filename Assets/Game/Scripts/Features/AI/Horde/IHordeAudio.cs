namespace Game.AIKit.Adapters
{
    /// <summary>
    /// Horde sound as events, not a thousand sources. The audio team implements it over
    /// <c>GameAudio.Horde</c>; until then <see cref="HordeAudioNull"/>.
    /// </summary>
    public interface IHordeAudio
    {
        /// <summary>Share of live monsters near the tank, 0..1. Called at most 4 times a second.</summary>
        void SetDensity(float density);

        /// <summary>A wave of monsters was released.</summary>
        void WaveStarted();
    }
}
