using UnityEngine;

namespace Modules.PlayerController
{
    internal class GravityComponent : IVelocity
    {
        private readonly PlayerController _player;

        public GravityComponent(PlayerController player)
        {
            _player = player;
        }

        public Vector2 GetVelocity()
        {
            if (_player.IsOnStairs  || _player.IsWallSliding)
                return Vector2.zero;

            if (_player.IsCeilingHit)
                return new Vector2(0, 0);

            float gravity = _player.Stats.FallAcceleration;
            float currentYVelocity = _player.Velocity.y;

            float newYVelocity =
                Mathf.MoveTowards(currentYVelocity, -_player.Stats.MaxFallSpeed, gravity * Time.fixedDeltaTime);

            return new Vector2(0, newYVelocity);
        }
    }
}