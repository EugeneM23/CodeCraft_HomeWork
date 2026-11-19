using Game.AudioEngine.Adapters;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// The horde's sound through the audio team's <see cref="GameAudio.Horde"/>: crowd density, wave starts, burrows,
    /// bites, spits, acid hits and the count of the living (the pack loop and the battle music). The horde calls the
    /// loops and the bites at most 4 times a second; the one-shots keep their own repeat limit in the audio layout.
    /// Silent while the level has no GameAudio (tests, scenes without sound): every GameAudio call is a no-op then.
    /// </summary>
    public sealed class GameHordeAudio : IHordeAudio
    {
        public static readonly GameHordeAudio Instance = new();

        public void SetDensity(float density) => GameAudio.Horde?.SetDensity(density);

        public void WaveStarted() => GameAudio.Horde?.WaveStarted();

        public void BurrowOpened(Vector3 at) => GameAudio.Horde?.BurrowOpened(at);

        public void CrawlerBite(Vector3 at) => GameAudio.Horde?.CrawlerBite(at);

        public void Spit(Vector3 from) => GameAudio.Horde?.Spit(from);

        public void AcidHit(Vector3 at) => GameAudio.Horde?.AcidHit(at);

        public void SetAlive(int alive) => GameAudio.Horde?.SetAlive(alive);
    }
}
