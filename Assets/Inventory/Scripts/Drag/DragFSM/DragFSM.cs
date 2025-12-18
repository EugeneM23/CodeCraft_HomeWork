using System;
using System.Collections.Generic;
using Inventories;
using UnityEngine;

public class DragFSM
{
    private readonly InventoryFactory _factory;
    private readonly RaycastDetector _raycastDetector;
    private readonly Inventory _originalInventory;

    private Dictionary<Type, IState> _states;
    private IState _currentState;
    public Inventory OriginInventory => _originalInventory;

    public DragFSM(RaycastDetector raycastDetector, Inventory originalInventory, InventoryFactory factory)
    {
        _raycastDetector = raycastDetector;
        _originalInventory = originalInventory;
        _factory = factory;
    }

    public DragContext Context { get; private set; }
    public InventoryFactory ItemFactory => _factory;

    public void Initialize()
    {
        Context = new DragContext();

        _states = new Dictionary<Type, IState>
        {
            [typeof(IdleDragState)] = new IdleDragState(this),
            [typeof(StartDragState)] = new StartDragState(this, _factory),
            [typeof(UpdateDragState)] = new UpdateDragState(this),
            [typeof(EndDragState)] = new EndDragState(this, _factory),
        };

        SetState<IdleDragState>();
    }

    public void Tick()
    {
        if (_currentState is ITickable tickable)
            tickable.Tick();

        _raycastDetector.SetIgnoredItem(Context.CurrentDragItem);
    }

    public void SetState<T>() where T : IState
    {
        _currentState?.Exit();
        _currentState = _states[typeof(T)];
        _currentState?.Enter();
    }

    public bool TryGetComponentUnderMouse<T>(out T component) where T : Component
    {
        return _raycastDetector.TryGetComponent(out component);
    }

    public bool TryGetSceneRaycastHit(out RaycastHit raycastHit)
    {
        return _raycastDetector.TryGetSceneRaycastHit(out raycastHit);
    }
}