using System;
using System.Collections.Generic;
using Gameplay;

namespace Game.Scripts.GameSystem.Controllers.Player.StateMachine
{
    public class StateMachine : IInitializeble, ITickable
    {
        private Dictionary<Type, IState> _states = new();
        public IState CurrentState { get; private set; }
        public bool CanSetState;

        [Inject]
        public void Construct(List<IState> states)
        {
            foreach (IState state in states)
                _states.Add(state.GetType(), state);
        }

        public void Initialize()
        {
            CanSetState = true;
            CurrentState = _states[typeof(IdleState)];
            CurrentState.Enter();
        }

        public void SetState<T>() where T : IState
        {
            if (!CanSetState || CurrentState is T) return;

            CurrentState.Exit();
            CurrentState = _states[typeof(T)];
            CurrentState.Enter();
        }

        public void Tick()
        {
            CurrentState.Tick();
        }
    }
}