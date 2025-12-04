using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragController : MonoBehaviour
{
    [SerializeField] private RectTransform _parent;

    private InventoryPresenter _startPresenter;
    private InventoryPresenter _currentPresenter;
    private EventSystem _eventSystem;
    private GraphicRaycaster _raycaster;

    private InventoryItem _draggedInventoryItem;
    private Vector2Int _itemMatrixPosition;
    private Vector2Int _startPositioOnGrid;
    private bool _isDragging;

    private void Start()
    {
        _raycaster = FindObjectOfType<GraphicRaycaster>();
        _eventSystem = EventSystem.current;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0)) StartDrag();
        if (Input.GetMouseButton(0) && _isDragging) UpdateDrag();
        if (Input.GetMouseButtonUp(0)) EndDrag();
    }

    private void StartDrag()
    {
        CellView cell = GetCellUnderMouse();
        if (cell == null || cell.InventoryItem == null) return;

        SetStartState(cell);

        _currentPresenter.RemoveItemFromInventory(_draggedInventoryItem.Item);
        _draggedInventoryItem.EnableDrag(true);
    }

    private void SetStartState(CellView cell)
    {
        _isDragging = true;
        _itemMatrixPosition = cell.ItemMatrixPosition;
        _startPresenter = cell.Presenter;
        _currentPresenter = cell.Presenter;
        _draggedInventoryItem = cell.InventoryItem;
        _startPositioOnGrid = _currentPresenter.GetItemPosition(_draggedInventoryItem.Item);
    }

    private void UpdateDrag()
    {
        CellView cell = GetCellUnderMouse();

        if (cell != null)
        {
            if (cell.Presenter != _currentPresenter)
            {
                _currentPresenter = cell.Presenter;
                _draggedInventoryItem.transform.parent = _currentPresenter.transform;
            }

            _currentPresenter.HighlightCells(_draggedInventoryItem.Item, cell.GridPosition, _itemMatrixPosition, cell);
        }
        else
            _currentPresenter.ClearHighlights();
    }

    private void EndDrag()
    {
        if (!_isDragging) return;

        _draggedInventoryItem.EnableDrag(_isDragging = false);

        CellView cell = GetCellUnderMouse();
        Vector2Int targetPosition = cell != null ? cell.GridPosition - _itemMatrixPosition : _startPositioOnGrid;
        InventoryPresenter targetPresenter = cell != null ? cell.Presenter : _startPresenter;

        bool success =
            targetPresenter.AddItemToInventory(_draggedInventoryItem.Item, _draggedInventoryItem, targetPosition);

        if (!success)
            _startPresenter.AddItemToInventory(_draggedInventoryItem.Item, _draggedInventoryItem, _startPositioOnGrid);

        _startPresenter.ClearHighlights();
        _currentPresenter.ClearHighlights();
        _draggedInventoryItem = null;
    }

    private CellView GetCellUnderMouse()
    {
        PointerEventData pointerData = new(_eventSystem) { position = Input.mousePosition };
        List<RaycastResult> results = new();
        _raycaster.Raycast(pointerData, results);

        foreach (var result in results)
            if (result.gameObject.TryGetComponent(out CellView cell))
                return cell;

        return null;
    }
}