using Game.AudioEngine.Adapters;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// The horde's sound through the audio team's <see cref="GameAudio.Horde"/>: crowd density and wave starts.
    /// Silent while the level has no GameAudio (tests, scenes without sound).
    /// </summary>
    public sealed class GameHordeAudio : IHordeAudio
    {
        public static readonly GameHordeAudio Instance = new();

        public void SetDensity(float density) => GameAudio.Horde?.SetDensity(density);

        public void WaveStarted() => GameAudio.Horde?.WaveStarted();
    }
}
