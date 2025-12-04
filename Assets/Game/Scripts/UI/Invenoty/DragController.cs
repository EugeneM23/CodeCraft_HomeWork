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
    private Vector2Int _startPosition;
    private Vector2 _dragOffset;
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
        if (cell == null || cell.Item == null) return;

        _isDragging = true;
        _startPresenter = _currentPresenter = cell.Presenter;

        _itemMatrixPosition = cell.ItemMatrixPosition;
        _draggedInventoryItem = cell.InventoryItem;
        _startPosition = _currentPresenter.GetItemPosition(cell.Item);


        _draggedInventoryItem.EnableBackGround(false);
        _currentPresenter.RemoveItemFromInventory(cell.Item);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _parent, Input.mousePosition, null, out Vector2 localPoint);

        _dragOffset = _draggedInventoryItem.RectTransform.anchoredPosition - localPoint;
    }

    private void UpdateDrag()
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _parent, Input.mousePosition, null, out Vector2 localPoint);

        _draggedInventoryItem.RectTransform.anchoredPosition = localPoint + _dragOffset;

        CellView cell = GetCellUnderMouse();

        if (cell != null)
        {
            _currentPresenter = cell.Presenter;

            _currentPresenter.HighlightCells(_draggedInventoryItem.Item, cell.GridPosition, _itemMatrixPosition, cell);
        }
        else
        {
            _currentPresenter.ClearHighlights();
        }
    }

    private void EndDrag()
    {
        if (!_isDragging) return;

        _isDragging = false;
        _draggedInventoryItem.EnableBackGround(true);

        CellView cell = GetCellUnderMouse();
        Vector2Int targetPosition = cell != null ? cell.GridPosition - _itemMatrixPosition : _startPosition;
        InventoryPresenter targetPresenter = cell != null ? cell.Presenter : _startPresenter;

        bool success =
            targetPresenter.AddItemToInventory(_draggedInventoryItem.Item, _draggedInventoryItem, targetPosition);

        if (!success)
            _startPresenter.AddItemToInventory(_draggedInventoryItem.Item, _draggedInventoryItem, _startPosition);

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