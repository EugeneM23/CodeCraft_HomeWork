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

    private GraphicRaycaster _raycaster;
    private EventSystem _eventSystem;

    //Drag
    public Vector3 DragOffset;
    public InventoryItem CurrenDragItem;
    public Vector2Int DragItemCell { get; set; }
    public Vector2Int StartDragCell { get; set; }

    private void Start()
    {
        _raycaster = FindObjectOfType<GraphicRaycaster>();
        _eventSystem = EventSystem.current;

        _states = new()
        {
            [typeof(StartDragState)] = new StartDragState(this, _raycaster, _eventSystem),
            [typeof(UpdateDragState)] = new UpdateDragState(this, _raycaster, _eventSystem),
            [typeof(EndDragState)] = new EndDragState(this, _raycaster, _eventSystem),
            [typeof(IdleDragState)] = new IdleDragState(this, _raycaster, _eventSystem),
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

    public bool TryGetComponentUnderMouse<T>(out T component) where T : Component
    {
        component = null;

        PointerEventData pointerData = new(_eventSystem) { position = Input.mousePosition };
        List<RaycastResult> results = new();
        _raycaster.Raycast(pointerData, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject.TryGetComponent(out component))
                return true;
        }

        if (EventSystem.current.IsPointerOverGameObject()) return false;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit) && hit.collider.gameObject.TryGetComponent(out component))
        {
            return true;
        }

        return false;
    }

    public void RemoveItemFromInventory(InventoryItem inventoryItem)
    {
        _inventoryPresenter.RemoveItem(inventoryItem.Item.uniqueId);
    }

    public bool AddItemToInventory(InventoryItem inventoryItem, Vector2Int position)
    {
        bool success = _inventoryPresenter.AddItem(inventoryItem.Item.itemData, position);
        return success;
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