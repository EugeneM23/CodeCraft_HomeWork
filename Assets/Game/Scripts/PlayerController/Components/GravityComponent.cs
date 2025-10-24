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
            // На наклонной поверхности
            if (_collision.IsGrounded && _collision.SurfaceNormal != Vector2.zero &&
                _collision.SurfaceNormal != Vector2.up)
                return 0;

            // Удар головой о потолок
            if (_collision.IsCeilingHit)
                return Mathf.Min(0, _player.Velocity.y);

            // На земле и падаем/стоим
            if (_collision.IsGrounded && _player.Velocity.y <= 0f)
                return 0;

            // В воздухе - применяем гравитацию
            float gravity = _stats.FallAcceleration * (1 + _fallMultiplier);
            _fallMultiplier += _stats.FallMultiplier * Time.fixedDeltaTime;

            float moveTowards = Mathf.MoveTowards(_player.Velocity.y, -_stats.MaxFallSpeed,
                gravity * Time.fixedDeltaTime);

            /*
            if (moveTowards == 0f)
                return _player.Velocity.y;
                */

            return moveTowards;
        }

        public void Reset() => _fallMultiplier = 0;
    }
}