using System;
using Game.Scripts.PlayerController.Game.Scripts.PlayerController;
using Gameplay;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class JumpComponent
    {
        private readonly ScriptableStats _stats;
        private readonly InputHandler _inputHandler;
        private readonly PlayerController _player;
        private readonly WallSlidingComponent _wallSliding;
        private readonly LedgeGrabComponent _ledgeGrab;
        private readonly GravityComponent _gravityComponent;

        public JumpComponent(ScriptableStats stats, InputHandler inputHandler, PlayerController player,
            WallSlidingComponent wallSliding, LedgeGrabComponent ledgeGrab, GravityComponent gravityComponent)
        {
            _stats = stats;
            _inputHandler = inputHandler;
            _player = player;
            _wallSliding = wallSliding;
            _ledgeGrab = ledgeGrab;
            _gravityComponent = gravityComponent;
        }

        public Vector2 GetJumpVector()
        {
            if (_inputHandler.JumpToConsume)
            {
                _inputHandler.ConsumeJump();
                _player.IsOnStairs = false;

                _ledgeGrab.ReleaseGrab();

                if (_wallSliding.IsOnWall)
                {
                    Vector2 jumpDirection = (_player.SurfaceNormal + Vector2.up).normalized * _stats.JumpPower;
                    _gravityComponent.AddImpulse(jumpDirection);
                }
                else if (_player.IsOnSlope || _player.IsOnStairs)
                {
                    Vector2 jumpDirection = (_player.SurfaceNormal + Vector2.up).normalized * _stats.JumpPower;
                    _gravityComponent.AddImpulse(jumpDirection);
                }
                else if (_player.IsGrounded)
                {
                    _gravityComponent.AddImpulse(new Vector2(0, _stats.JumpPower));
                }
            }

            return Vector2.zero;
        }
    }
}