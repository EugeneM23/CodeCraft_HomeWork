using System;
using System.Collections.Generic;
using Inventories;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class DragFSM : MonoBehaviour
{
    [SerializeField] private DragItem _dragItemPrefab;
    [SerializeField] private InventoryInstaller inventoryInstaller;
    [SerializeField] private SceneItemSpawner _spawner;
    [SerializeField] private Vector2Int _cellSize = new(75, 75);

    [SerializeField] private InventoryView _inventoryView;

    private Dictionary<Type, IState> _states;
    private IState _currentState;
    private RaycastDetector _raycastDetector;

    public DragContext Context { get; private set; }
    public Inventory OriginInventory => inventoryInstaller.Inventory;
    public SceneItemSpawner ItemSpawner => _spawner;

    private void Start()
    {
        GraphicRaycaster raycaster = FindObjectOfType<GraphicRaycaster>();
        EventSystem eventSystem = EventSystem.current;

        _raycastDetector = new RaycastDetector(raycaster, eventSystem);
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

    private void Update()
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
        DragItem dragItem = Instantiate(_dragItemPrefab, transform.parent);
        dragItem.Construct(itemInstance, _cellSize, OriginInventory);
        return dragItem;
    }
}