using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using Inventories;

public class DragController : MonoBehaviour
{
    [SerializeField] private Canvas _canvas;
    [SerializeField] private InventoryPresenter _presenter;
    [SerializeField] private CellHighlighter _cellHighlighter;

    private GraphicRaycaster _raycaster;
    private EventSystem _eventSystem;

    private Item _draggedItem;
    private Vector2Int _dragAnchor;
    private InventoryItem _inventoryItem;
    private Vector2 _dragOffset;
    private Vector2 _originalPosition;

    private void Start()
    {
        _raycaster = FindObjectOfType<GraphicRaycaster>();
        _eventSystem = EventSystem.current;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            StartDrag();

        if (_draggedItem != null && Input.GetMouseButton(0))
            UpdateDrag();

        if (Input.GetMouseButtonUp(0) && _draggedItem != null)
            EndDrag();
    }

    private void StartDrag()
    {
        if (_draggedItem != null) return;

        CellView cell = GetCellUnderMouse();
        if (cell?.Item == null || cell.InventoryItem == null) return;

        _draggedItem = cell.Item;
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

    private void UpdateDrag()
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

    private void EndDrag()
    {
        CellView cell = GetCellUnderMouse();
        bool moved = false;

        if (cell != null)
        {
            Vector2Int targetPosition = cell.GridPosition - _dragAnchor;
            moved = _presenter._inventory.MoveItem(_draggedItem, targetPosition);
        }

        if (moved)
        {
            _draggedItem = null;
            _inventoryItem = null;
        }
        else if (_inventoryItem != null)
        {
            _inventoryItem.RectTransform.localPosition = _originalPosition;
            _inventoryItem.EnableBackGround(true);
            _draggedItem = null;
            _inventoryItem = null;
        }

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