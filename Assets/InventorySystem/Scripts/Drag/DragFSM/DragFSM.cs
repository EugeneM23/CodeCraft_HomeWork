using System;
using System.Collections.Generic;
using Game.Scripts.UI.Equipment.Game.Equipment.View;
using Inventories;
using Inventories.EndDrag;
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

    public InventoryPresenter MainPresenter { get; private set; }
    public DragContext Context { get; private set; }

    private void OnEnable()
    {
        _raycastDetector = new RaycastDetector(_raycaster, EventSystem.current);
    }

    public void SetMainInventory(InventoryPresenter presenter) => MainPresenter = presenter;

    private void Start() => Initialize();

    private void Initialize()
    {
        Context = new DragContext();

        _states = new Dictionary<Type, IState>
        {
            [typeof(IdleDragState)] = new IdleDragState(this),
            [typeof(StartDragFromInventoryState)] = new StartDragFromInventoryState(this),
            [typeof(StartDragFromEquipmentState)] = new StartDragFromEquipmentState(this),
            [typeof(PickupFromSceneState)] = new PickupFromSceneState(this, _factory),
            [typeof(UpdateDragState)] = new UpdateDragState(this),
            [typeof(EndDragState)] = new EndDragState(this),
            [typeof(EndDragToInventoryState)] = new EndDragToInventoryState(this),
            [typeof(EndDragToEquipmentState)] = new EndDragToEquipmentState(this),
            [typeof(EndDragToSceneState)] = new EndDragToSceneState(this, _factory),
            [typeof(EndDragReturnToSourceState)] = new EndDragReturnToSourceState(this),
            [typeof(FinishDragState)] = new FinishDragState(this, _factory),
        };

        SetState<IdleDragState>();
    }

    public void Update()
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

    public bool TryGetComponentUnderMouse<T>(out T component) where T : Component
    {
        return _raycastDetector.TryGetComponent(out component);
    }

    public bool TryGetSceneRaycastHit(out RaycastHit raycastHit)
    {
        return _raycastDetector.TryGetSceneRaycastHit(out raycastHit);
    }

    public void SetupDragContext(Item item, Vector3 position, InventoryPresenter sourcePresenter,
        Vector2Int clickedCell, Vector2Int itemStartCell, EquipmentSlotView slotView = null)
    {
        Vector2 cellSize = new Vector2(50, 50);
        Context.CurrentDragItem = _factory.SpawnDragItem(item, cellSize, sourcePresenter);
        Context.CurrentDragItem.transform.parent = Context.CurrentDragItem.transform.root;
        Context.CurrentDragItem.transform.position = position;
        Context.SourceInventory = sourcePresenter;
        Context.StartDragCell = itemStartCell;
        Context.DragOffset = position - Input.mousePosition;
        Context.GridOffset = new Vector2Int(clickedCell.x - itemStartCell.x, clickedCell.y - itemStartCell.y);
        Context.EquipmentSlotOld = slotView;
    }

    public Vector2Int CalculateClickedCellInSlot(RectTransform slotRect, Vector2Int itemSize)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            slotRect,
            Input.mousePosition,
            null,
            out Vector2 localPoint
        );

        Vector2 rectSize = slotRect.rect.size;
        Vector2 pivot = slotRect.pivot;

        Vector2 normalizedPoint = new Vector2(
            (localPoint.x / rectSize.x) + pivot.x,
            (localPoint.y / rectSize.y) + pivot.y
        );

        int cellX = Mathf.Clamp(Mathf.FloorToInt(normalizedPoint.x * itemSize.x), 0, itemSize.x - 1);
        int cellY = Mathf.Clamp(Mathf.FloorToInt((1f - normalizedPoint.y) * itemSize.y), 0, itemSize.y - 1);

        return new Vector2Int(cellX, cellY);
    }
}