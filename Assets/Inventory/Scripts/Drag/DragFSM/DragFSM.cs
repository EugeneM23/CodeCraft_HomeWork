using System;
using System.Collections.Generic;
using Inventories;
using UnityEngine;

public class DragFSM
{
    private Vector2Int _cellSize = new(75, 75);

    private readonly SceneItemSpawner _spawner;
    private readonly InventoryView _inventoryView;
    private readonly RaycastDetector _raycastDetector;
    private readonly Inventory _originalInventory;
    private readonly DragItem _dragItemPrefab;

    private Dictionary<Type, IState> _states;
    private IState _currentState;
    public Inventory OriginInventory => _originalInventory;

    public DragFSM(InventoryView inventoryView, RaycastDetector raycastDetector, Inventory originalInventory,
        DragItem dragItemPrefab, SceneItemSpawner spawner)
    {
        _inventoryView = inventoryView;
        _raycastDetector = raycastDetector;
        _originalInventory = originalInventory;
        _dragItemPrefab = dragItemPrefab;
        _spawner = spawner;
    }

    public DragContext Context { get; private set; }
    public SceneItemSpawner ItemSpawner => _spawner;

    public void Initialize()
    {
        Context = new DragContext();

        _states = new Dictionary<Type, IState>
        {
            [typeof(IdleDragState)] = new IdleDragState(this),
            [typeof(StartDragState)] = new StartDragState(this),
            [typeof(UpdateDragState)] = new UpdateDragState(this),
            [typeof(EndDragState)] = new EndDragState(this)
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

    public DragItem CreateDragItem(ItemInstance itemInstance)
    {
        DragItem dragItem = GameObject.Instantiate(_dragItemPrefab, _spawner.transform.parent);
        dragItem.Construct(itemInstance, _cellSize, OriginInventory);
        return dragItem;
    }
}