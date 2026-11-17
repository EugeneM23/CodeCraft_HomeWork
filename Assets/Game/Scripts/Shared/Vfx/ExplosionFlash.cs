using Ticking;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A short, shadowless flash for a pooled explosion effect: full intensity when the effect is
    /// spawned, gone a fraction of a second later. The light is switched off once faded, so an
    /// effect waiting in its pool slot costs no lighting pass.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class ExplosionFlash : MonoBehaviour, IUpdateListener
    {
        [SerializeField] private float _duration = 0.12f;
        [Tooltip("As tuned under the Built-in pipeline; converted for URP (LightIntensity).")]
        [SerializeField] private float _intensity = 6f;

        private Light _light;
        private float _time;

        private bool _looped;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _light.shadows = LightShadows.None;
        }

        // Pooled: the effect is re-activated on every explosion, which restarts the flash.
        private void OnEnable()
        {
            _looped = GameTicks.TryAdd(this);
            _time = 0f;
            _light.intensity = LightIntensity.FromBuiltIn(_intensity);
            _light.enabled = true;
        }

        private void OnDisable()
        {
            if (_looped)
                GameTicks.Remove(this);
            _looped = false;
        }

        private void Update() // perf-ok: ticks itself without the game loop (tests); one flag check with it
        {
            if (!_looped)
                OnUpdate(Time.deltaTime);
        }

        // On the game loop with the app: the flash stands on pause
        public void OnUpdate(float deltaTime)
        {
            if (!_light.enabled)
                return;

            _time += deltaTime;
            float t = _time / _duration;

            if (t >= 1f)
            {
                _light.enabled = false;
                return;
            }

            float fade = 1f - t;
            _light.intensity = LightIntensity.FromBuiltIn(_intensity * fade * fade);
        }
    }
}
