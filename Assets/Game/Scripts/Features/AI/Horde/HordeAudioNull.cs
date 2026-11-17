namespace Game.AIKit.Adapters
{
    /// <summary>Silent <see cref="IHordeAudio"/> for tests; the game uses <see cref="GameHordeAudio"/>.</summary>
    public sealed class HordeAudioNull : IHordeAudio
    {
        public static readonly HordeAudioNull Instance = new();

        public void SetDensity(float density)
        {
        }

        public void WaveStarted()
        {
        }
    }
}
