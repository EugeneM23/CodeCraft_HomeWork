using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class JumpComponent
    {
        private readonly PlayerController _player;

        public JumpComponent(PlayerController player) => _player = player;

        public void Jump()
        {
            if (_player.IsOnWall && !_player.IsGrounded && _player.MoveDirection != Vector2.zero)
            {
                float jumpX = (-_player.WallDirection * _player.Stats.JumpFromWall) * 0.6f; // Горизонтальная сила
                float jumpY = _player.Stats.JumpFromWall; // Вертикальная сила (такая же как обычный прыжок)

                Vector2 jumpDirection = new Vector2(jumpX, jumpY);
                _player.AddImpulse(jumpDirection);
            }
            else if (_player.IsOnSlope || _player.IsOnStairs)
            {
                Vector2 jumpDirection = (_player.SurfaceNormal + Vector2.up).normalized * _player.Stats.JumpPower;
                _player.AddImpulse(jumpDirection);
            }
            else if (_player.IsGrounded)
            {
                _player.AddImpulse(new Vector2(0, _player.Stats.JumpPower));
            }
        }
    }
}