using System.Collections.Generic;
using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class JumpComponent : ITickable
    {
        [Inject] private readonly CharacterController2D _controller;
        [Inject] private readonly Character _character;
        [Inject] private readonly List<IAction> _actions;

        private const float COYOTE_TIME = 0.15f;
        private float _lastGroundedTime;
        private int _availableJumps;

        public interface IAction
        {
            void Invoke();
        }

        private void DoActions()
        {
            foreach (var action in _actions)
                action.Invoke();
        }

        public void Tick()
        {
            if (_controller.IsGrounded || _controller.IsWallSliding)
            {
                _lastGroundedTime = Time.time;
                _availableJumps = _controller.Stats.MaxJumps;
            }
        }

        public void Jump()
        {
            if (_controller.IsWallSliding && _availableJumps > 0)
            {
                Vector2 wallJump = new Vector2(-_controller.WallDirection * _controller.Stats.JumpFromWall,
                    _controller.Stats.JumpPower);

                DoActions();
                _character.Jump(wallJump);
                return;
            }

            bool coyoteTime = Time.time - _lastGroundedTime <= COYOTE_TIME;
            if (_controller.IsGrounded || coyoteTime || _availableJumps > 0)
            {
                DoActions();
                _character.Jump(Vector2.up * _controller.Stats.JumpPower);
                if (!_controller.IsGrounded && !coyoteTime)
                    _availableJumps--;
            }
        }
    }
}