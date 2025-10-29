using System;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class JumpComponent : ITickable
    {
        private readonly PlayerController _player;

        // Coyote time параметры
        private float _coyoteTime = 0.2f; // время в секундах
        private float _lastGroundedTime;

        public JumpComponent(PlayerController player)
        {
            _player = player;
        }

        public void Jump()
        {
            // Wall jump: только если на стене и не на земле
            if (_player.IsOnWall && !_player.IsGrounded && _player.MoveDirection != Vector2.zero)
            {
                float jumpX = (-_player.WallDirection * _player.Stats.JumpFromWall) * 0.6f;
                float jumpY = _player.Stats.JumpFromWall;

                Vector2 jumpDirection = new Vector2(jumpX, jumpY);
                _player.AddImpulse(jumpDirection);
                return; // выход — остальные условия не проверяем
            }

            // Slope / stairs jump: проверка без coyote time
            if (_player.IsOnSlope || _player.IsOnStairs)
            {
                Vector2 jumpDirection = (_player.SurfaceNormal + Vector2.up).normalized * _player.Stats.JumpPower;
                _player.AddImpulse(jumpDirection);
                return;
            }

            // Ground jump (или coyote time)
            if (_player.IsGrounded || (Time.time - _lastGroundedTime <= _coyoteTime))
            {
                _player.AddImpulse(new Vector2(0, _player.Stats.JumpPower));
            }
        }

        public void Tick()
        {
            // Обновляем время последнего касания земли
            if (_player.IsGrounded || _player.IsOnWallSliding)
            {
                _lastGroundedTime = Time.time;
            }
        }

        private bool CanJump() => _player.IsGrounded || (Time.time - _lastGroundedTime <= _coyoteTime);
    }
}