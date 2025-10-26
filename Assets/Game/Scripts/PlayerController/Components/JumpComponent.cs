using Game.Scripts.PlayerController.Game.Scripts.PlayerController;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class JumpComponent
    {
        private readonly ScriptableStats _stats;
        private readonly InputHandler _inputHandler;
        private readonly PlayerController _player;
        private readonly WallSlidingComponent _wallSliding;
        private LedgeGrabComponent ledgeGrab;

        public JumpComponent(ScriptableStats stats, InputHandler inputHandler, PlayerController player,
            WallSlidingComponent wallSliding, LedgeGrabComponent ledgeGrab)
        {
            _stats = stats;
            _inputHandler = inputHandler;
            _player = player;
            _wallSliding = wallSliding;
            this.ledgeGrab = ledgeGrab;
        }

        public Vector2 GetJumpVector()
        {
            if (_inputHandler.JumpToConsume)
            {
                _inputHandler.ConsumeJump();
                _player.IsOnStairs = false;

                ledgeGrab.ReleaseGrab();

                if (_wallSliding.IsOnWall)
                    return (_wallSliding.WallNormal + Vector2.up) * _stats.JumpPower;

                if (_player.IsGrounded || _player.IsOnStairs)
                {
                    return (_wallSliding.WallNormal + Vector2.up) * _stats.JumpPower;
                }

                if (!_player.IsGrounded)
                    return Vector2.up * _stats.JumpPower;
            }

            return Vector2.zero;
        }
    }
}