using UnityEngine;

namespace Modules.PlayerController
{
    internal class JumpComponent : ITickable
    {
        private const float COYOTE_TIME = 0.15f;
        private readonly CharacterController2D _character;
        private float _lastGroundedTime;
        private int _availableJumps;

        public JumpComponent(CharacterController2D character)
        {
            _character = character;
        }

        public void Tick()
        {
            if (_character.IsGrounded || _character.IsWallSliding)
            {
                _lastGroundedTime = Time.time;
                _availableJumps = _character.Stats.MaxJumps;
            }
        }

        public void Jump()
        {
            if (_character.IsWallSliding && _availableJumps > 0)
            {
                Vector2 wallJump = new Vector2(-_character.WallDirection * _character.Stats.JumpFromWall,
                    _character.Stats.JumpPower);

                _character.AddImpulse(wallJump);
                return;
            }

            bool coyoteTime = Time.time - _lastGroundedTime <= COYOTE_TIME;
            if (_character.IsGrounded || coyoteTime || _availableJumps > 0)
            {
                _character.AddImpulse(Vector2.up * _character.Stats.JumpPower);

                if (!_character.IsGrounded && !coyoteTime)
                    _availableJumps--;
            }
        }
    }
}