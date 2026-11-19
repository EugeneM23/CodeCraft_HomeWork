using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// Horde sound as events, not a thousand sources. The audio team implements it over
    /// <c>GameAudio.Horde</c>; until then <see cref="HordeAudioNull"/>. Every call is throttled by the horde
    /// (at most 4 a second where noted) and allocates nothing.
    /// </summary>
    public interface IHordeAudio
    {
        /// <summary>Share of live monsters near the tank, 0..1. Called at most 4 times a second.</summary>
        void SetDensity(float density);

        /// <summary>A wave of monsters was released.</summary>
        void WaveStarted();

        /// <summary>A monster starts climbing out of a newly opened burrow at <paramref name="at"/>.</summary>
        void BurrowOpened(Vector3 at);

        /// <summary>A crawler bites the hull at <paramref name="at"/> (at most 4 a second for the whole horde).</summary>
        void CrawlerBite(Vector3 at);

        /// <summary>A spitter fired acid from <paramref name="from"/>.</summary>
        void Spit(Vector3 from);

        /// <summary>Acid hit the tank or the ground at <paramref name="at"/> (at most 4 a second).</summary>
        void AcidHit(Vector3 at);

        /// <summary>Live monsters now (on a change, at most 4 a second).</summary>
        void SetAlive(int alive);
    }
}
