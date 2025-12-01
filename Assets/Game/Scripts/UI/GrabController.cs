using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using Inventories;

public class DragController : MonoBehaviour
{
    [SerializeField] private InventoryView _inventoryView;
    [SerializeField] private Canvas _canvas;
    [SerializeField] private InventoryPresenter _presenter;
    [SerializeField] private CellHighlighter _cellHighlighter;

    private GraphicRaycaster _raycaster;
    private EventSystem _eventSystem;

    private Item _draggedItem;
    private Vector2Int _dragStartPosition;
    private Vector2Int _dragAnchor;
    private InventoryItem _inventoryItem;
    private Vector2 _dragOffset;
    private Vector2 _originalPosition;

    private void Start()
    {
        _raycaster = FindObjectOfType<GraphicRaycaster>();
        _eventSystem = EventSystem.current;
        _canvas ??= FindObjectOfType<Canvas>();
    }

    private void Update()
    {
        HandleMouseDown();
        HandleDragging();
        HandleMouseUp();
    }

    private void HandleMouseDown()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        CellView cell = GetCellUnderMouse();
        if (cell?.Item == null) return;

        _draggedItem = cell.Item;
        _dragStartPosition = cell.GridPosition;
        _dragAnchor = cell.ItemMatrixPosition;
        _inventoryItem = cell.InventoryItem;

        _inventoryItem.transform.SetAsLastSibling();
        _originalPosition = _inventoryItem.RectTransform.localPosition;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.transform as RectTransform,
            Input.mousePosition,
            _canvas.worldCamera,
            out Vector2 localPoint
        );

        _dragOffset = (Vector2)_inventoryItem.RectTransform.localPosition - localPoint;
    }

    private void HandleDragging()
    {
        if (_inventoryItem == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.transform as RectTransform,
            Input.mousePosition,
            _canvas.worldCamera,
            out Vector2 localPoint
        );

        _inventoryItem.RectTransform.localPosition = localPoint + _dragOffset;
        _inventoryItem.EnableBackGround(false);

        _cellHighlighter.HighlightCells(_draggedItem, GetCellUnderMouse(), _dragAnchor);
    }

    private void HandleMouseUp()
    {
        if (!Input.GetMouseButtonUp(0) || _draggedItem == null) return;

        CellView cell = GetCellUnderMouse();

        if (cell != null)
        {
            Vector2Int targetTopLeft = cell.GridPosition - _dragAnchor;
            (int width, int height) = _cellHighlighter.GetItemDimensions(_draggedItem);

            if (_cellHighlighter.CanPlaceItemAt(_draggedItem, targetTopLeft, width, height))
                _inventoryView.RequestMoveItem(_draggedItem, _dragStartPosition, cell.GridPosition);
            else
                _inventoryItem.RectTransform.localPosition = _originalPosition;
        }
        else
        {
            _inventoryItem.RectTransform.localPosition = _originalPosition;
        }

        _draggedItem = null;
        _inventoryItem = null;
        _cellHighlighter.ClearHighlights();
    }

    private CellView GetCellUnderMouse()
    {
        PointerEventData pointerData = new(_eventSystem) { position = Input.mousePosition };
        List<RaycastResult> results = new();
        _raycaster.Raycast(pointerData, results);

        foreach (var result in results)
            if (result.gameObject.TryGetComponent(out CellView cellView))
                return cellView;

        return null;
    }
}