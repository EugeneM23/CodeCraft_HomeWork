using System;
using System.Collections.Generic;
using Modules.PlayerController;

namespace Gameplay
{
    public class StateMachine : IInitializeble, ITickable, IDisposable
    {
        private SpriteAnimator _animator;
        private PlayerController _player;

        private readonly Dictionary<Type, BaseState> _states = new();
        private BaseState _currentState;

        [Inject]
        public void Construct(List<BaseState> states, SpriteAnimator animator, PlayerController player)
        {
            _player = player;
            _animator = animator;

            foreach (BaseState state in states)
                _states.Add(state.GetType(), state);
        }

        public void Initialize()
        {
            SetState<IdleState>();
            _player.OnJump += TransitToJump;
            _player.OnDash += TransitToDash;
        }

        public void Dispose()
        {
            _player.OnDash -= TransitToDash;
            _player.OnJump -= TransitToJump;
        }

        private void TransitToDash() => SetState<DashState>();

        private void TransitToJump() => SetState<RiseState>();

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
        }
    }
}