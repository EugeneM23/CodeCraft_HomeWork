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
        private BaseState _currentState;

        [Inject]
        public void Construct(List<BaseState> states)
        {
            foreach (BaseState state in states)
                _states.Add(state.GetType(), state);
        }

        public void Initialize()
        {
            SetState<IdleState>();
            Debug.Log("StateMachine initialized".Log(Color.Coral));
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
            Debug.Log("Tick");
            _currentState?.Tick();
        }
    }
}