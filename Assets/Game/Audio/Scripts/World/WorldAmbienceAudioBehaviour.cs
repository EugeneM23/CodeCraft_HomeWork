using Atomic.Entities;
using Game.Voxels;
using UnityEngine;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// The level's ambience, on the GameContext: wind all through the battle, rain while VoxelRain is on (its
    /// volume follows world.rain, eased 0↔1), thunder every 15–40 s while it rains.
    ///
    /// VoxelRain is looked up once, and again every couple of seconds only during the first seconds of the level
    /// or after the one found is destroyed, not every frame.
    /// </summary>
    public sealed class WorldAmbienceAudioBehaviour : IEntityInit, IEntityTick, IEntityDispose
    {
        /// <summary>Seconds world.rain takes from 0 to 1 and back.</summary>
        private const float RAIN_FADE = 3f;

        private const float THUNDER_MIN = 15f;
        private const float THUNDER_MAX = 40f;

        /// <summary>Rain level from which it counts as raining for thunder.</summary>
        private const float THUNDER_RAIN = 0.5f;

        private const float SEARCH_INTERVAL = 2f;

        /// <summary>Searches for a VoxelRain before giving up, while none has been found.</summary>
        private const int SEARCH_ATTEMPTS = 5;

        private readonly AudioLoop _wind = new(AudioKeys.World.WIND_LOOP, 1f);
        private readonly AudioLoop _rainLoop = new(AudioKeys.World.RAIN_LOOP, 0.5f);

        private VoxelRain _rain;
        private bool _hadRain;
        private int _searchesLeft = SEARCH_ATTEMPTS;
        private float _searchTimer;

        private float _rainLevel;
        private float _thunderTimer;

        public void Init(IEntity entity)
        {
            FindRain();
            _thunderTimer = Random.Range(THUNDER_MIN, THUNDER_MAX);
            GameAudio.SetParameter(AudioKeys.World.RAIN, 0f);
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            // Retried each frame: a pause or the first frames before the banks load may refuse the start.
            _wind.Start(null);

            if (_rain == null)
                SearchRain(deltaTime);

            bool raining = _rain != null && _rain.isActiveAndEnabled;
            _rainLevel = Mathf.MoveTowards(_rainLevel, raining ? 1f : 0f, deltaTime / RAIN_FADE);
            GameAudio.SetParameter(AudioKeys.World.RAIN, _rainLevel);
            _rainLoop.Set(_rainLevel > 0f, null);

            if (!raining || _rainLevel < THUNDER_RAIN)
            {
                _thunderTimer = Random.Range(THUNDER_MIN, THUNDER_MAX);
                return;
            }

            _thunderTimer -= deltaTime;
            if (_thunderTimer > 0f)
                return;

            _thunderTimer = Random.Range(THUNDER_MIN, THUNDER_MAX);
            GameAudio.Play(AudioKeys.World.THUNDER);
        }

        public void Dispose(IEntity entity)
        {
            _wind.StopNow();
            _rainLoop.StopNow();
        }

        private void SearchRain(float deltaTime)
        {
            // The one found was destroyed: worth looking again, a new one may have come with a scene.
            if (_hadRain)
            {
                _hadRain = false;
                _searchesLeft = SEARCH_ATTEMPTS;
            }

            if (_searchesLeft <= 0)
                return;

            _searchTimer -= deltaTime;
            if (_searchTimer > 0f)
                return;

            FindRain();
        }

        private void FindRain()
        {
            _searchTimer = SEARCH_INTERVAL;
            _searchesLeft--;
            _rain = Object.FindAnyObjectByType<VoxelRain>(FindObjectsInactive.Include);
            _hadRain = _rain != null;
        }
    }
}
