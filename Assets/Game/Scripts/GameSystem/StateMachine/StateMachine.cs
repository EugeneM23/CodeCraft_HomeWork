using System;
using System.Collections.Generic;

namespace Gameplay
{
    public class StateMachine : IInitializeble
    {
        private SpriteAnimator _animator;

        private readonly Dictionary<Type, IState> _states = new();
        public IState CurrentState { get; private set; }

        [Inject]
        public void Construct(List<IState> states, SpriteAnimator animator)
        {
            _animator = animator;
            
            foreach (IState state in states)
                _states.Add(state.GetType(), state);
        }

        public void Initialize()
        {
            CurrentState = _states[typeof(IdleState)];
            CurrentState.Enter();
        }

        public void SetState<T>() where T : IState
        {
            if (!_animator.CurrentAnimation.CanInterrupt) return;

            if (CurrentState is T) return;

            CurrentState = _states[typeof(T)];
            CurrentState.Enter();
        }
    }
}