using System;
using System.Collections.Generic;

namespace Gameplay
{
    public class AnimationFSM : IInitializeble, ITickable
    {
        private readonly Dictionary<Type, BaseState> _states = new();

        private Character _player;
        private BaseState _currentState;

        [Inject]
        public void Construct(List<BaseState> states, Character player)
        {
            _player = player;
            foreach (BaseState state in states)
                _states.Add(state.GetType(), state);
        }

        public void Initialize()
        {
            SetState<IdleState>();

            _player.OnDash += TransitionTo;
            _player.OnJump += TransitionTo;
            _player.OnSmash += TransitionTo;
            _player.OnItemThrow += TransitionTo;
        }

        private void TransitionTo(StateType stateType)
        {
            switch (stateType)
            {
                case StateType.ThrowItem:
                    SetState<ThrowItemState>();
                    break;
                case StateType.Smash:
                    SetState<SmashState>();
                    break;
                case StateType.Jump:
                    SetState<JumpStartState>();
                    break;
                case StateType.Dash:
                    if (_player.IsGrounded)
                        SetState<RollState>();
                    else
                        SetState<DashState>();
                    break;
            }
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

            if (_player.IsWallSliding)
            {
                SetState<WallSlideState>();
                return;
            }

            if (!_player.IsGrounded)
            {
                SetState<FallMidState>();
                return;
            }
        }
    }
}