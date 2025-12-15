using System;
using System.Collections.Generic;
using Inventories;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragFSM : MonoBehaviour
{
    [SerializeField] private BackPack _backPack;
    [SerializeField] private SceneItemSpawner _spawner;

    private Dictionary<Type, IState> _states;
    private IState _currentState;
    private RaycastDetector _raycastDetector;

    public Vector3 DragOffset { get; set; }
    public InventoryItem CurrentDragItem { get; set; }
    public Vector2Int DragItemCell { get; set; }
    public Inventory StartDragInventory { get; set; }
    public Inventory CurrentInventory { get; set; }
    public Inventory OriginInventory => _backPack.Inventory;

    private void Start()
    {
        GraphicRaycaster raycaster = FindObjectOfType<GraphicRaycaster>();
        EventSystem eventSystem = EventSystem.current;

        _raycastDetector = new RaycastDetector(raycaster, eventSystem);

        _states = new Dictionary<Type, IState>
        {
            [typeof(IdleDragState)] = new IdleDragState(this),
            [typeof(StartDragState)] = new StartDragState(this),
            [typeof(UpdateDragState)] = new UpdateDragState(this),
            [typeof(EndDragState)] = new EndDragState(this, _spawner)
        };

        SetState<IdleDragState>();
    }

    private void Update()
    {
        if (_currentState is ITickable tickable)
            tickable.Tick();

        _raycastDetector.SetIgnoredItem(CurrentDragItem);
    }

    public void SetState<T>() where T : IState
    {
        if (_currentState != null)
            _currentState.Exit();

        _currentState = _states[typeof(T)];

        if (_currentState != null)
            _currentState.Enter();
    }

    public bool TryGetComponentUnderMouse<T>(out T component) where T : Component
    {
        return _raycastDetector.TryGetComponent(out component);
    }

    public Vector2Int GetDragItemCell(InventoryItem item, Vector2 clickPosition)
    {
        RectTransform rectTransform = item.RectTransform;
        Vector2Int itemSize = item.Item.itemData.Size;

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

    public void Highlight(Vector2Int cellIndex)
    {
        Vector2Int size = CurrentDragItem.Item.itemData.Size;
        Vector2Int startPosition = new Vector2Int(
            cellIndex.x - DragItemCell.x,
            cellIndex.y - DragItemCell.y
        );

        Vector2Int[] cells = new Vector2Int[size.x * size.y];
        int index = 0;

        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                cells[index] = new Vector2Int(startPosition.x + x, startPosition.y + y);
                index++;
            }
        }

        if (CurrentInventory != null)
            CurrentInventory.Highlight(cells);
    }

    public void UnHighlight()
    {
        if (CurrentInventory != null)
            CurrentInventory.UnHighlight();
    }

    public bool TryGetSceneRaycastHit(out RaycastHit raycastHit)
    {
        return _raycastDetector.TryGetSceneRaycastHit(out raycastHit);
    }
}