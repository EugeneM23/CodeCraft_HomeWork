using System;
using UnityEngine;

namespace ScreenNavigation
{
    /// <summary>
    /// Fades the CanvasGroup alpha in or out over Duration seconds of unscaled time. A screen switched off becomes
    /// transparent, so every show fades in from zero. Duration 0 — at once.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class FadeTransition : ScreenTransition
    {
        [Tooltip("Seconds of unscaled time; 0 — at once")]
        [SerializeField] private float _duration = 0.15f;

        private CanvasGroup _canvasGroup;
        private float _target;
        private Action _done;
        private bool _playing;

        public float Duration => _duration;
        public bool IsPlaying => _playing;

        private CanvasGroup Group => _canvasGroup != null ? _canvasGroup : _canvasGroup = GetComponent<CanvasGroup>();

        private void Awake() => Group.alpha = 0f;

        private void OnDisable() => Group.alpha = 0f;

        private void OnValidate()
        {
            if (_duration < 0f)
                _duration = 0f;
        }

        private void Update() => Tick(Time.unscaledDeltaTime);

        /// <summary>Setup for objects built in code (tests); prefabs set Duration in the Inspector.</summary>
        public void Configure(float duration) => _duration = Mathf.Max(0f, duration);

        public override void Play(bool show, Action done)
        {
            _target = show ? 1f : 0f;
            _done = done;
            _playing = true;
            Tick(0f);
        }

        /// <summary>Advances the fade by dt seconds: Update passes unscaled time, tests — their own step.</summary>
        public void Tick(float dt)
        {
            if (!_playing)
                return;

            float step = _duration > 0f ? dt / _duration : 1f;
            Group.alpha = Mathf.MoveTowards(Group.alpha, _target, step);
            if (Group.alpha != _target)
                return;

            _playing = false;
            Action done = _done;
            _done = null;
            done?.Invoke();
        }
    }
}
