using UnityEngine;

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

        public void BurrowOpened(Vector3 at)
        {
        }

        public void CrawlerBite(Vector3 at)
        {
        }

        public void Spit(Vector3 from)
        {
        }

        public void AcidHit(Vector3 at)
        {
        }

        public void SetAlive(int alive)
        {
        }
    }
}
