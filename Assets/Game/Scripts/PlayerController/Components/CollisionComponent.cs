using Gameplay;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class CollisionComponent
    {
        private readonly CapsuleCollider2D _collider;
        private readonly ScriptableStats _stats;
        private readonly PlayerController _player;

        public CollisionComponent(CapsuleCollider2D collider, ScriptableStats stats, PlayerController player)
        {
            _collider = collider;
            _stats = stats;
            _player = player;

            Physics2D.queriesStartInColliders = false;
        }

        public bool IsGrounded { get; private set; }
        public bool IsCeilingHit { get; private set; }

        public Vector2 SurfaceNormal { get; private set; }

        public void DetectCollisions()
        {
            IsCeilingHit = CheckCeilingCollision();
            IsGrounded = CheckGroundCollision() && SurfaceNormal == Vector2.zero;
        }

        private bool CheckGroundCollision()
        {
            bool groundHit = Physics2D.CapsuleCast(
                _collider.bounds.center,
                _collider.size,
                _collider.direction,
                0,
                Vector2.down,
                _stats.GrounderDistance,
                _stats.PlayerLayer
            );

            return groundHit;
        }

        private bool CheckCeilingCollision()
        {
            bool ceilingHit = Physics2D.CapsuleCast(
                _collider.bounds.center,
                _collider.size,
                _collider.direction,
                0,
                Vector2.up,
                _stats.GrounderDistance,
                _stats.PlayerLayer
            );

            if (ceilingHit && _player._frameVelocity.y > 0)
                return true;

            return false;
        }
    }
}