using System;
using Game.Scripts.PlayerController.Game.Scripts.PlayerController;
using Gameplay;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class JumpComponent : IVelocity
    {
        private readonly InputHandler _inputHandler;
        private readonly PlayerController _player;

        public JumpComponent(InputHandler inputHandler, PlayerController player)
        {
            _inputHandler = inputHandler;
            _player = player;
        }

        public Vector2 GetVelocity()
        {
            if (_inputHandler.JumpToConsume)
            {
                _inputHandler.ConsumeJump();
                _player.IsOnStairs = false;

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

            return Vector2.zero;
        }
    }
}