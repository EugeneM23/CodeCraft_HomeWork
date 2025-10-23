using UnityEngine;

namespace Game.Scripts.PlayerController
{
    internal class GravityComponent
    {
        private readonly SurfaceTangentDebugger _debugger;
        private readonly ScriptableStats _stats;
        private float _fallMultiplier;

        public GravityComponent(ScriptableStats stats, SurfaceTangentDebugger debugger)
        {
            _stats = stats;
            _debugger = debugger;
        }

        public float GetGravity(bool isGrounded, float currentYVelocity, bool IsCeilingHit)
        {
            // Если на поверхности с нормалью - не применяем стандартную гравитацию
            if (isGrounded && _debugger.SurfaceNormal != Vector2.zero && _debugger.SurfaceNormal != Vector2.up)
            {
                _fallMultiplier = 0;
                return 0; // Гравитация будет через прижатие к поверхности в MoveComponentDva
            }
            
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