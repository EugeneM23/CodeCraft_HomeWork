using System.Collections.Generic;
using Game.Scripts.UI.Equipment;
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
    private Vector2Int _startPositionOnGrid;
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
        EquipmentSlot slot = GetEquipmentCellUnderMouse();

        if (slot != null && slot.CurrentItem != null)
        {
            _draggedInventoryItem = slot.CurrentItem;
            _draggedInventoryItem.EnableDrag(true);
            _isDragging = true;
        }

        if (cell == null || cell.InventoryItem == null) return;

        InitializeDragState(cell);
        _currentPresenter.RemoveFromModelItem(_draggedInventoryItem.Item);
        _draggedInventoryItem.EnableDrag(true);
    }

    private void UpdateDrag()
    {
        CellView cell = GetCellUnderMouse();

        if (cell != null)
        {
            UpdateCurrentPresenter(cell);
            _currentPresenter.HighlightCells(
                _draggedInventoryItem.Item,
                cell.GridPosition,
                _itemMatrixPosition,
                cell
            );
        }
        else
        {
            _currentPresenter.ClearHighlights();
        }
    }

    private void EndDrag()
    {
        if (!_isDragging) return;

        _draggedInventoryItem.EnableDrag(false);
        _isDragging = false;

        EquipmentSlot slot = GetEquipmentCellUnderMouse();

        if (slot != null && _draggedInventoryItem.Item.ItemTipe == slot.ItemTipe)
        {
            slot.AddItem(_draggedInventoryItem);
            _currentPresenter.RemoveFromViewItem(_draggedInventoryItem);
            return;
        }

        CellView cell = GetCellUnderMouse();
        TryPlaceItem(cell);

        ClearAllHighlights();
        _draggedInventoryItem = null;
    }

    private void InitializeDragState(CellView cell)
    {
        _isDragging = true;
        _itemMatrixPosition = cell.ItemMatrixPosition;
        _startPresenter = cell.Presenter;
        _currentPresenter = cell.Presenter;
        _draggedInventoryItem = cell.InventoryItem;
        _startPositionOnGrid = _currentPresenter.GetItemPosition(_draggedInventoryItem.Item);
    }

    private void UpdateCurrentPresenter(CellView cell)
    {
        if (cell.Presenter != _currentPresenter)
        {
            _currentPresenter = cell.Presenter;
            _draggedInventoryItem.transform.SetParent(_currentPresenter.transform);
        }
    }

    private void TryPlaceItem(CellView targetCell)
    {
        Vector2Int targetPosition;
        InventoryPresenter targetPresenter;

        if (targetCell != null)
        {
            targetPosition = targetCell.GridPosition - _itemMatrixPosition;
            targetPresenter = targetCell.Presenter;
        }
        else
        {
            targetPosition = _startPositionOnGrid;
            targetPresenter = _startPresenter;
        }

        bool placed = targetPresenter.MoveItem(
            _draggedInventoryItem.Item,
            _draggedInventoryItem,
            targetPosition
        );

        if (!placed)
        {
            _startPresenter.MoveItem(
                _draggedInventoryItem.Item,
                _draggedInventoryItem,
                _startPositionOnGrid
            );
        }
        else
        {
            _startPresenter.RemoveFromViewItem(_draggedInventoryItem);
            _currentPresenter.AddViewItemToView(_draggedInventoryItem);
        }
    }

    private void ClearAllHighlights()
    {
        _startPresenter.ClearHighlights();
        _currentPresenter.ClearHighlights();
    }

    private CellView GetCellUnderMouse()
    {
        PointerEventData pointerData = new(_eventSystem) { position = Input.mousePosition };
        List<RaycastResult> results = new();
        _raycaster.Raycast(pointerData, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject.TryGetComponent(out CellView cell))
                return cell;
        }

        return null;
    }

    private EquipmentSlot GetEquipmentCellUnderMouse()
    {
        PointerEventData pointerData = new(_eventSystem) { position = Input.mousePosition };
        List<RaycastResult> results = new();
        _raycaster.Raycast(pointerData, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject.TryGetComponent(out EquipmentSlot slot))
                return slot;
        }

        return null;
    }
}