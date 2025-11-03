using System;
using System.Collections.Generic;
using Modules.PlayerController;
using UnityEngine;
using Color = System.Drawing.Color;

namespace Gameplay
{
    public class StateMachine : IInitializeble, ITickable
    {
        private CharacterController2D _сharacter;
        private readonly Dictionary<Type, BaseState> _states = new();
        private BaseState _currentState;

        [Inject]
        public void Construct(List<BaseState> states, CharacterController2D character)
        {
            _сharacter = character;
            foreach (BaseState state in states)
                _states.Add(state.GetType(), state);
        }

        public void Initialize()
        {
            SetState<IdleState>();

            _сharacter.OnDash += TransitionToDash;
            _сharacter.OnJump += TransitionToJump;
            _сharacter.OnSmash += TransitionToSmash;
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
            
            if (_сharacter.IsWallSliding)
            {
                SetState<WallSlideState>();
                return;
            }

            if (!_сharacter.IsGrounded)
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
            if (_сharacter.IsGrounded)
                SetState<RollState>();
            else
                SetState<DashState>();
        }
    }
}