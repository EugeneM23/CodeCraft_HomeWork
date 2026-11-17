namespace Game.AI
{
    /// <summary>Silent <see cref="IHordeAudio"/>: the default until the audio team's one is merged.</summary>
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
