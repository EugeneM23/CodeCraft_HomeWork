using UnityEngine;

namespace Gameplay
{
    public class CooldownComponent : ITickable
    {
        private float _cooldownDuration;
        private float _lastActionTime;
        private bool _isReady = true;

        public CooldownComponent(float cooldownDuration)
        {
            _cooldownDuration = cooldownDuration;
        }

        public bool TryUse()
        {
            if (!_isReady)
                return false;

            _isReady = false;
            _lastActionTime = Time.time;
            return true;
        }

        public void Tick()
        {
            if (!_isReady && Time.time >= _lastActionTime + _cooldownDuration)
                _isReady = true;
        }

        public bool IsNotRedy() => !_isReady;
    }
}