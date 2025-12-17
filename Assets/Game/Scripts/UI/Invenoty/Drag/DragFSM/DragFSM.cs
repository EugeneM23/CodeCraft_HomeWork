using System;
using System.Collections.Generic;
using Inventories;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragFSM : MonoBehaviour
{
    [SerializeField] private DragItem _dragItemPrefab;
    [SerializeField] private BackPack _backPack;
    [SerializeField] private SceneItemSpawner _spawner;
    [SerializeField] private Vector2Int _cellSize = new(75, 75);

    private Dictionary<Type, IState> _states;
    private IState _currentState;
    private RaycastDetector _raycastDetector;
    
    public DragContext Context { get; private set; }
    public Inventory OriginInventory => _backPack.Inventory;
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

    public Vector2Int CalculateGrabbedCell(DragItem item, Vector2 clickPosition)
    {
        RectTransform rectTransform = item.RectTransform;
        Vector2Int itemSize = item.ItemInstance.itemData.Size;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            clickPosition,
            null,
            out Vector2 localPoint
        );

        Vector2 rectSize = rectTransform.rect.size;
        Vector2 pivot = rectTransform.pivot;

        Vector2 normalizedPoint = new Vector2(
            (localPoint.x / rectSize.x) + pivot.x,
            (localPoint.y / rectSize.y) + pivot.y
        );

        int cellX = Mathf.Clamp(Mathf.FloorToInt(normalizedPoint.x * itemSize.x), 0, itemSize.x - 1);
        int cellY = Mathf.Clamp(Mathf.FloorToInt((1f - normalizedPoint.y) * itemSize.y), 0, itemSize.y - 1);

        return new Vector2Int(cellX, cellY);
    }

    public void HighlightCells(Vector2Int hoveredCell)
    {
        if (!Context.IsDragging) return;

        Vector2Int size = Context.CurrentDragItem.ItemInstance.itemData.Size;
        Vector2Int topLeftCell = hoveredCell - Context.GrabbedCell;

        Vector2Int[] cells = CalculateOccupiedCells(topLeftCell, size);

        Context.CurrentInventory?.Highlight(cells);
    }

    public void UnhighlightCells()
    {
        Context.CurrentInventory?.UnHighlight();
    }

    private Vector2Int[] CalculateOccupiedCells(Vector2Int topLeft, Vector2Int size)
    {
        Vector2Int[] cells = new Vector2Int[size.x * size.y];
        int index = 0;

        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                cells[index++] = new Vector2Int(topLeft.x + x, topLeft.y + y);
            }
        }

        return cells;
    }
}