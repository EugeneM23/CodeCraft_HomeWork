using System;
using System.Collections.Generic;
using Modules.PlayerController;
using UnityEngine;
using Color = System.Drawing.Color;

namespace Gameplay
{
    public class StateMachine : IInitializeble, ITickable
    {
        private readonly Dictionary<Type, BaseState> _states = new();

        private Character _player;
        private CharacterController2D _controller;
        private BaseState _currentState;

        [Inject]
        public void Construct(List<BaseState> states, CharacterController2D character, Character player)
        {
            _player = player;
            _controller = character;
            foreach (BaseState state in states)
                _states.Add(state.GetType(), state);
        }

        public void Initialize()
        {
            SetState<IdleState>();

            _player.OnDash += TransitionToDash;
            _player.OnJump += TransitionToJump;
            _player.OnSmash += TransitionToSmash;
        }

        public void SetState<T>() where T : BaseState
        {
            if (_currentState is T) return;

            _currentState?.Exit();
            _currentState = _states[typeof(T)];
            _currentState.Enter();
        }

        public void Tick()
        {
            _currentState?.Tick();

            if (_controller.IsWallSliding)
            {
                SetState<WallSlideState>();
                return;
            }

            if (!_controller.IsGrounded)
            {
                SetState<FallMidState>();
                return;
            }
        }

        private void TransitionToSmash()
        {
            SetState<SmashState>();
        }

        private void TransitionToJump()
        {
            SetState<JumpStartState>();
        }

        private void TransitionToDash()
        {
            if (_controller.IsGrounded)
                SetState<RollState>();
            else
                SetState<DashState>();
        }
    }
}