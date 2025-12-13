using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragFSM : MonoBehaviour
{
    [SerializeField] private InventoryPresenter _inventoryPresenter;
    private Dictionary<Type, IState> _states;
    private IState _currentState;
    private RaycastDetector _raycastDetector;

    public Vector3 DragOffset;
    public InventoryItem CurrenDragItem;
    public Vector2Int DragItemCell { get; set; }
    public Vector2Int StartDragCell { get; set; }

    private void Start()
    {
        GraphicRaycaster raycaster = FindObjectOfType<GraphicRaycaster>();
        EventSystem eventSystem = EventSystem.current;

        _raycastDetector = new RaycastDetector(raycaster, eventSystem);

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

        _raycastDetector.SetIgnoredItem(CurrenDragItem);
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

    public void RemoveItemFromInventory(InventoryItem inventoryItem)
    {
        _inventoryPresenter.RemoveItem(inventoryItem.Item.uniqueId);
    }

    public bool AddItemToInventory(InventoryItem inventoryItem, Vector2Int position)
    {
        return _inventoryPresenter.AddItem(inventoryItem.Item.itemData, position);
    }

    public Vector2Int GetDragItemCell(InventoryItem item, Vector2 clickPosition)
    {
        RectTransform rectTransform = item.RectTransform;
        Vector2Int itemSize = item.Item.itemData.Size;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, clickPosition, null,
            out Vector2 localPoint);

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
}