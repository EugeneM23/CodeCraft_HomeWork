using UnityEngine;

namespace Game.Scripts.PlayerController
{
    internal class GravityComponent
    {
        private readonly PlayerController _player;
        private readonly ScriptableStats _stats;
        private readonly CollisionComponent _collision;

        private float _fallMultiplier;

        public GravityComponent(ScriptableStats stats, CollisionComponent collision, PlayerController player)
        {
            _player = player;
            _stats = stats;
            _collision = collision;
        }

        public float GetYVelocity()
        {
            // На наклонной поверхности - не применяем гравитацию
            if (_collision.IsGrounded && _collision.SurfaceNormal != Vector2.up)
                return _player._frameVelocity.y;

            if (_collision.IsGrounded)
                return 0;

            if (_collision.IsCeilingHit)
                return _player._frameVelocity.y = -_player._frameVelocity.y / 2;

            // В воздухе - применяем гравитацию
            float gravity = _stats.FallAcceleration * (1 + _fallMultiplier);
            _fallMultiplier += _stats.FallMultiplier * Time.fixedDeltaTime;

            float moveTowards = Mathf.MoveTowards(_player.Velocity.y, -_stats.MaxFallSpeed,
                gravity * Time.fixedDeltaTime);


            return moveTowards;
        }

        public void Reset() => _fallMultiplier = 0;
    }
}