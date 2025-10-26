using Gameplay;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    internal class GravityComponent
    {
        private readonly PlayerController _player;
        private readonly ScriptableStats _stats;
        private readonly CollisionComponent _collision;

        public GravityComponent(ScriptableStats stats, CollisionComponent collision, PlayerController player)
        {
            _player = player;
            _stats = stats;
            _collision = collision;
        }

        public Vector2 GetGravityVector()
        {
            if (_player.IsOnStairs || _player.IsGrabbingLedge.Log())
                return Vector2.zero;
            
            if (_collision.IsGrounded && _collision.SurfaceNormal != Vector2.up)
                return Vector2.zero;

            if (_collision.IsCeilingHit)
                return new Vector2(0, -5);

            float gravity = _stats.FallAcceleration;
            
            float currentYVelocity = _player.Velocity.y;

            float newYVelocity = Mathf.MoveTowards(currentYVelocity, -_stats.MaxFallSpeed,
                gravity * Time.fixedDeltaTime);

            return new Vector2(0, newYVelocity);
        }
    }
}