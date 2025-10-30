using UnityEngine;

namespace Game.Scripts.Modules.PlayerController.Components
{
    internal class JumpComponent : ITickable
    {
        private const float COYOTE_TIME = 0.15f;
        private readonly PlayerController _player;
        private float _lastGroundedTime;
        private int _availableJumps;

        public JumpComponent(PlayerController player)
        {
            _player = player;
        }

        public void Tick()
        {
            if (_player.IsGrounded || _player.IsOnWallSliding)
            {
                _lastGroundedTime = Time.time;
                _availableJumps = _player.Stats.MaxJumps;
            }
        }

        public void Jump()
        {
            if (_player.IsOnWallSliding)
            {
                Vector2 wallJump = new Vector2(-_player.WallDirection * _player.Stats.JumpFromWall,
                    _player.Stats.JumpPower);

                _player.AddImpulse(wallJump);
                return;
            }

            bool coyoteTime = Time.time - _lastGroundedTime <= COYOTE_TIME;
            if (_player.IsGrounded || coyoteTime || _availableJumps > 0)
            {
                _player.AddImpulse(Vector2.up * _player.Stats.JumpPower);

                if (!_player.IsGrounded && !coyoteTime)
                    _availableJumps--;
            }
        }
    }
}