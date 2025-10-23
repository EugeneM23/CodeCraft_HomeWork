using UnityEngine;

namespace Game.Scripts.PlayerController
{
    internal class GravityComponent
    {
        private readonly ScriptableStats _stats;
        private float _fallMultiplier;

        public GravityComponent(ScriptableStats stats)
        {
            _stats = stats;
        }

        public float GetGravity(bool isGrounded, float currentYVelocity, bool IsCeilingHit)
        {
            if (IsCeilingHit)
                return Mathf.Min(0, currentYVelocity);

            if (isGrounded && currentYVelocity <= 0f)
            {
                _fallMultiplier = _stats.FallMultiplier;
                return _stats.GroundingForce;
            }

            float inAirGravity = _stats.FallAcceleration * (1 + _fallMultiplier);

            float newYVelocity = Mathf.MoveTowards(currentYVelocity, -_stats.MaxFallSpeed,
                inAirGravity * Time.fixedDeltaTime);

            _fallMultiplier += _stats.FallMultiplier;

            return newYVelocity;
        }

        public void Reset()
        {
            _fallMultiplier = 0;
        }
    }
}