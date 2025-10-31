using UnityEngine;

namespace Modules.PlayerController
{
    internal class WallSlidingComponent : ITickable, IVelocity
    {
        private readonly PlayerController _player;

        public bool IsWallSliding { get; private set; }

        public WallSlidingComponent(PlayerController player)
        {
            _player = player;
        }

        public void Tick()
        {
            // Определяем, скользим ли по стене
            if (_player.IsOnWall && Mathf.Abs(_player.MoveDirection.x) > 0.1f)
            {
                IsWallSliding = _player.MoveDirection.x * _player.WallDirection > 0;
            }
            else
            {
                IsWallSliding = false;
            }
        }

        public Vector2 GetVelocity()
        {
            if (!IsWallSliding || _player.Velocity.y > 0.5f)
                return Vector2.zero;

            return new Vector2(0, -_player.Stats.WallSlideSpeed);
        }
    }
}