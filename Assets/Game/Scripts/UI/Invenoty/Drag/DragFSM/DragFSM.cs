using System;
using System.Collections.Generic;
using UnityEngine;

public class DragFSM : MonoBehaviour
{
    private Dictionary<Type, IState> _states;
    private IState _currentState;

    private void Start()
    {
        _states = new()
        {
            [typeof(StartDragState)] = new StartDragState(this),
            [typeof(UpdateDragState)] = new UpdateDragState(this),
            [typeof(EndDragState)] = new EndDragState(this),
            [typeof(IdleDragState)] = new IdleDragState(this),
        };

        SetState<IdleDragState>();
    }

    private void Update()
    {
        if (_currentState is ITickable tickable)
            tickable.Tick();
    }

    public void SetState<T>() where T : IState
    {
        _currentState?.Exit();
        _currentState = _states[typeof(T)];
        _currentState?.Enter();
    }
}