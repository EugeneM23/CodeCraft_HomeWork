using Game.AudioEngine.Adapters;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// The horde's sound through the audio team's <see cref="GameAudio.Horde"/>: crowd density and wave starts; the
    /// burrow, bite, spit, acid and alive-count sounds wait for their methods there.
    /// Silent while the level has no GameAudio (tests, scenes without sound).
    /// </summary>
    public sealed class GameHordeAudio : IHordeAudio
    {
        public static readonly GameHordeAudio Instance = new();

        public void SetDensity(float density) => GameAudio.Horde?.SetDensity(density);

        public void WaveStarted() => GameAudio.Horde?.WaveStarted();

        public void BurrowOpened(Vector3 at)
        {
            // TODO(audio): GameAudio.Horde.BurrowOpened(at)
        }

        public void CrawlerBite(Vector3 at)
        {
            // TODO(audio): GameAudio.Horde.CrawlerBite(at)
        }

        public void Spit(Vector3 from)
        {
            // TODO(audio): GameAudio.Horde.Spit(from)
        }

        public void AcidHit(Vector3 at)
        {
            // TODO(audio): GameAudio.Horde.AcidHit(at)
        }

        public void SetAlive(int alive)
        {
            // TODO(audio): GameAudio.Horde.SetAlive(alive)
        }
    }
}
