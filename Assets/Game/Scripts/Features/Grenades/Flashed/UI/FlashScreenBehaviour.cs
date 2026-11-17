using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// Whites the screen out when a flash goes off in sight, then lets it come back slowly: full
    /// white held for a moment, then a fade that starts quick and lingers at the end. The closer the
    /// flash (the stronger), the whiter, the longer the hold and the slower the fade. A stronger
    /// flash during a fade starts it over; a weaker one is lost in the glare.
    /// </summary>
    public sealed class FlashScreenBehaviour : IEntityInit, IEntityDispose, IEntityTick
    {
        private const float MIN_HOLD = 0.3f;
        private const float EXTRA_HOLD = 0.7f;
        private const float MIN_FADE = 1f;
        private const float EXTRA_FADE = 2.5f;

        private readonly Image _image;
        private IEvent<float> _flashed;

        private float _peak;
        private float _hold;
        private float _fade;
        private float _time;

        public FlashScreenBehaviour(Image image) => _image = image;

        public void Init(IEntity entity)
        {
            _flashed = entity.GetFlashedEvent();
            _flashed.OnEvent += OnFlashed;
            SetAlpha(0f);
        }

        public void Dispose(IEntity entity)
        {
            _flashed.OnEvent -= OnFlashed;

            // Nothing ticks the fade any more: do not leave the screen half white.
            _peak = 0f;
            SetAlpha(0f);
        }

        private void OnFlashed(float strength)
        {
            strength = Mathf.Clamp01(strength);

            if (strength <= CurrentAlpha())
                return;

            _peak = strength;
            _hold = MIN_HOLD + EXTRA_HOLD * strength;
            _fade = MIN_FADE + EXTRA_FADE * strength;
            _time = 0f;
            SetAlpha(_peak);
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            if (_peak <= 0f)
                return;

            _time += deltaTime;
            float alpha = CurrentAlpha();
            SetAlpha(alpha);

            if (alpha <= 0f)
                _peak = 0f;
        }

        private float CurrentAlpha()
        {
            if (_peak <= 0f)
                return 0f;

            if (_time <= _hold)
                return _peak;

            float t = Mathf.Clamp01((_time - _hold) / _fade);
            float left = 1f - t;
            return _peak * left * left;
        }

        private void SetAlpha(float alpha)
        {
            Color colour = _image.color;
            colour.a = alpha;
            _image.color = colour;
            _image.enabled = alpha > 0f;
        }
    }
}
