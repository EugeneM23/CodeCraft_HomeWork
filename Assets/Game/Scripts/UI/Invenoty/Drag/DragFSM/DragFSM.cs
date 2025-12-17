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

    [SerializeField] private InventoryView _inventoryView;

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

    public Vector2Int GetItemPostion(DragItem item)
    {
        return Context.SourceInventory.GetItemGridPositions(item.ItemInstance)[0];
    }

    public void HighlightCells(Vector2Int hoveredCell)
    {
        if (Context.CurrentInventory == null) return;

        int count = Context.CurrentInventory.Height + Context.CurrentInventory.Width;
        Vector2Int[] cells = new Vector2Int[count];
        for (int i = 0; i < Context.CurrentInventory.Width; i++)
        {
            for (int j = 0; j < Context.CurrentInventory.Height; j++)
            {
                cells[i + j] = new Vector2Int(i, j);
            }
        }

        Debug.Log(cells.Length);
        Context.CurrentInventory.Highlight(cells);
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