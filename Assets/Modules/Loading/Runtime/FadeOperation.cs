using System;
using UnityEngine;

namespace Loading
{
    /// <summary>
    /// Moves the CanvasGroup alpha to the target at the pace of the full 0..1 range per duration (a half-way fade
    /// takes half the time). Duration 0 or already at the target — at once. Weightless by default: a fade is not
    /// loading progress. Runs on the dt it is ticked with — give it unscaled time.
    /// </summary>
    public sealed class FadeOperation : LoadingOperation
    {
        private readonly CanvasGroup _group;
        private readonly float _target;
        private readonly float _duration;

        public FadeOperation(CanvasGroup group, float target, float duration, float weight = 0f) : base(weight)
        {
            if (group == null)
                throw new ArgumentNullException(nameof(group));

            _group = group;
            _target = Mathf.Clamp01(target);
            _duration = Mathf.Max(0f, duration);
        }

        protected override void OnBegin()
        {
            if (_duration <= 0f)
                _group.alpha = _target;

            SucceedIfReached();
        }

        protected override void OnTick(float dt)
        {
            if (_group == null)
            {
                Fail($"{nameof(FadeOperation)}: the CanvasGroup was destroyed");
                return;
            }

            _group.alpha = Mathf.MoveTowards(_group.alpha, _target, dt / _duration);
            SucceedIfReached();
        }

        private void SucceedIfReached()
        {
            if (Mathf.Approximately(_group.alpha, _target))
            {
                _group.alpha = _target;
                Succeed();
            }
        }
    }
}
