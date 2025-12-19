using System;
using System.Collections.Generic;
using Inventories;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragFSM : MonoBehaviour
{
    [SerializeField] private InventoryFactory _factory;
    [SerializeField] private GraphicRaycaster _raycaster;

    private Dictionary<Type, IState> _states;
    private IState _currentState;
    private RaycastDetector _raycastDetector;

    public Inventory MainInventory { get; private set; }

    public DragContext Context { get; private set; }

    private void OnEnable()
    {
        _raycastDetector = new RaycastDetector(_raycaster, EventSystem.current);
    }

    public void SetMainInventory(Inventory mainInventory) => MainInventory = mainInventory;

    private void Start() => Initialize();

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

    public void Update()
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