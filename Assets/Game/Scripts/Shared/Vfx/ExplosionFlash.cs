using UnityEngine;

namespace Game
{
    /// <summary>
    /// A short, shadowless flash for a pooled explosion effect: full intensity when the effect is
    /// spawned, gone a fraction of a second later. The light is switched off once faded, so an
    /// effect waiting in its pool slot costs no lighting pass.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class ExplosionFlash : MonoBehaviour
    {
        [SerializeField] private float _duration = 0.12f;
        [Tooltip("As tuned under the Built-in pipeline; converted for URP (LightIntensity).")]
        [SerializeField] private float _intensity = 6f;

        private Light _light;
        private float _time;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _light.shadows = LightShadows.None;
        }

        // Pooled: the effect is re-activated on every explosion, which restarts the flash.
        private void OnEnable()
        {
            _time = 0f;
            _light.intensity = LightIntensity.FromBuiltIn(_intensity);
            _light.enabled = true;
        }

        private void Update()
        {
            if (!_light.enabled)
                return;

            _time += Time.deltaTime;
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
