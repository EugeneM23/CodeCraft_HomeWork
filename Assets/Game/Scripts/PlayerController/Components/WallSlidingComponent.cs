using Gameplay;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class WallSlidingComponent : IVelocity
    {
        private readonly PlayerController _player;

        public WallSlidingComponent(PlayerController player) => _player = player;

        public Vector2 GetVelocity()
        {
            if (!_player.IsOnWall || _player.Velocity.y >= 0)
                return Vector2.zero;

            int wallDir = _player.WallDirection;
            bool isPressingIntoWall = Mathf.Sign(_player.MoveDirection.x) == wallDir;

            if (isPressingIntoWall)
            {
                return new Vector2(0, _player.Velocity.y / 1f * -1);
            }

            return Vector2.zero;
        }
    }
}