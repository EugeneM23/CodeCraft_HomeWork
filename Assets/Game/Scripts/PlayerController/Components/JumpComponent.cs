using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class JumpComponent
    {
        private readonly PlayerController _player;

        public JumpComponent(PlayerController player) => _player = player;

        public void Jump()
        {
            if (_player.IsOnWall)
            {
                Vector2 jumpDirection = (_player.SurfaceNormal + Vector2.up).normalized * _player.Stats.JumpPower;
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