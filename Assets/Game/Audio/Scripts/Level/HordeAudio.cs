using AudioEngine;
using UnityEngine;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// Horde sounds, called by the AI team through <see cref="GameAudio.Horde"/>. Events, not a source per monster:
    ///   the distant crowd hum follows <see cref="SetDensity"/>;
    ///   the Crawler pack is one loop whose volume follows <see cref="SetAlive"/>, which also drives the battle music;
    ///   burrows, bites, spits, acid and deaths play once at their point, limited by their repeat rate.
    /// </summary>
    public sealed class HordeAudio
    {
        /// <summary>Density at and below which a loop is stopped.</summary>
        private const float SILENT = 0.01f;

        private const float FADE_OUT = 1f;

        private AudioEventHandle _crowd;
        private AudioEventHandle _pack;
        private float _density;
        private float _crawlers;
        private float _alive;

        public float Density => _density;

        /// <summary>The pack loop's level, 0..1, as last set.</summary>
        public float Crawlers => _crawlers;

        /// <summary>All live horde monsters against the arena's limit, 0..1, as last set.</summary>
        public float Alive => _alive;

        /// <summary>How thick the horde is near the tank, 0..1: sets horde.density and starts or stops the crowd loop.</summary>
        public void SetDensity(float density)
        {
            _density = Mathf.Clamp01(density);
            GameAudio.SetParameter(AudioKeys.Horde.DENSITY, _density);
            Toggle(ref _crowd, AudioKeys.Horde.CROWD_LOOP, _density);
        }

        /// <summary>
        /// Live horde monsters at which the pack loop and the battle music are at full: the arena's limit of the
        /// milestone (60 alive). Tunable for later chapters.
        /// </summary>
        public int FullHorde { get; set; } = 60;

        /// <summary>
        /// How many horde monsters are alive: the pack loop (crawler.alive) and the battle music's layers follow
        /// it against <see cref="FullHorde"/>. AI calls it at most 4 times a second.
        /// </summary>
        public void SetAlive(int alive)
        {
            _alive = FullHorde > 0 ? Mathf.Clamp01((float)alive / FullHorde) : 0f;
            _crawlers = _alive;
            GameAudio.SetParameter(AudioKeys.Crawler.ALIVE, _crawlers);
            Toggle(ref _pack, AudioKeys.Crawler.PACK_LOOP, _crawlers);

            AppAudio app = AppAudio.Instance;
            if (app != null)
                app.SetBattleIntensity(_alive);
        }

        /// <summary>A wave was released: its sound and a surge of the battle music.</summary>
        public void WaveStarted()
        {
            GameAudio.Play(AudioKeys.Horde.WAVE_START);

            AppAudio app = AppAudio.Instance;
            if (app != null)
                app.SurgeBattleIntensity();
        }

        /// <summary>A burrow opens at the arena's edge: the ground tears, monsters climb out.</summary>
        public void BurrowOpened(Vector3 point) => GameAudio.Play(AudioKeys.Monsters.BURROW_OPEN, point);

        public void CrawlerBite(Vector3 point) => GameAudio.Play(AudioKeys.Crawler.BITE, point);

        public void CrawlerDied(Vector3 point) => GameAudio.Play(AudioKeys.Crawler.DEATH, point);

        public void Spit(Vector3 from) => GameAudio.Play(AudioKeys.Spitter.SPIT, from);

        /// <summary>Acid landed and hisses: on the tank, the ground, a wall.</summary>
        public void AcidHit(Vector3 point) => GameAudio.Play(AudioKeys.Spitter.ACID_HIT, point);

        public void SpitterDied(Vector3 point) => GameAudio.Play(AudioKeys.Spitter.DEATH, point);

        /// <summary>The level is gone: forget the loops, its AudioSystem took them along.</summary>
        internal void Reset()
        {
            _crowd = default;
            _pack = default;
            _density = 0f;
            _crawlers = 0f;
            _alive = 0f;
        }

        private static void Toggle(ref AudioEventHandle loop, string id, float level)
        {
            if (level > SILENT)
            {
                if (!loop.IsValid)
                    GameAudio.StartLoop(id, out loop);
            }
            else if (loop.IsValid)
            {
                GameAudio.StopLoop(ref loop, FADE_OUT);
            }
        }
    }
}
