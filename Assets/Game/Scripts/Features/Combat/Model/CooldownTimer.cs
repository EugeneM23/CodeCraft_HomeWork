using System;
using UnityEngine;

namespace Game
{
    [Serializable]
    public sealed class CooldownTimer
    {
        [SerializeField] private float _current;

        [SerializeField] private float _duration;

        public CooldownTimer(float duration, float current = 0)
        {
            _duration = duration;
            _current = current;
        }

        public float Duration => _duration;

        public bool IsExpired()
        {
            return _current <= 0;
        }

        public float GetProgress()
        {
            return _current / _duration;
        }

        public void Reset()
        {
            _current = _duration;
        }

        public void Tick(float deltaTime)
        {
            _current = Mathf.Max(0, _current - deltaTime);
        }

        public void SetDuration(float duration)
        {
            _duration = duration;
        }
    }
}