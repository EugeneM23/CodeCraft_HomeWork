using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using Inventories;

public class DragController : MonoBehaviour
{
    [SerializeField] private InventoryView inventoryView;
    [SerializeField] private Canvas canvas;
    [SerializeField] private InventoryPresenter _presenter;

    private GraphicRaycaster raycaster;
    private EventSystem eventSystem;

    private Item draggedItem;
    private Vector2Int dragStartPosition;
    private Vector2Int dragAnchor; // смещение внутри предмета при захвате
    private InventoryItem _inventoryItem;
    private Vector2 dragOffset;
    private Vector2 originalContainerPosition;

    private List<CellView> highlightedCells = new();

    private void Start()
    {
        raycaster = FindObjectOfType<GraphicRaycaster>();
        eventSystem = EventSystem.current;

        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();
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
        if (cell == null || cell.Item == null) return;

        draggedItem = cell.Item;
        dragStartPosition = cell.GridPosition;
        dragAnchor = cell.ItemMatrixPosition;

        _inventoryItem = cell.InventoryItem;

        if (_inventoryItem != null)
        {
            _inventoryItem.transform.SetAsLastSibling();
            originalContainerPosition = _inventoryItem.RectTransform.localPosition;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                Input.mousePosition,
                canvas.worldCamera,
                out Vector2 localPoint
            );

            dragOffset = (Vector2)_inventoryItem.RectTransform.localPosition - localPoint;
        }
    }

    private void HandleDragging()
    {
        if (_inventoryItem == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            Input.mousePosition,
            canvas.worldCamera,
            out Vector2 localPoint
        );

        _inventoryItem.RectTransform.localPosition = localPoint + dragOffset;
        _inventoryItem.EnableBackGround(false);

        CellView cellUnderMouse = GetCellUnderMouse();
        HighlightCells(cellUnderMouse);
    }

    private void HandleMouseUp()
    {
        if (!Input.GetMouseButtonUp(0) || draggedItem == null) return;

        CellView cell = GetCellUnderMouse();

        if (cell != null)
        {
            Vector2Int targetTopLeft = cell.GridPosition - dragAnchor;

            if (CanPlaceItem(draggedItem, targetTopLeft))
            {
                inventoryView.RequestMoveItem(draggedItem, dragStartPosition, cell.GridPosition);
            }
            else
            {
                if (_inventoryItem != null)
                    _inventoryItem.RectTransform.localPosition = originalContainerPosition;
            }
        }
        else
        {
            if (_inventoryItem != null)
                _inventoryItem.RectTransform.localPosition = originalContainerPosition;
        }

        draggedItem = null;
        _inventoryItem = null;

        ClearHighlights();
    }

    private CellView GetCellUnderMouse()
    {
        PointerEventData pointerData = new PointerEventData(eventSystem)
        {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        raycaster.Raycast(pointerData, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject.TryGetComponent(out CellView cellView))
                return cellView;
        }

        return null;
    }

    private void HighlightCells(CellView hoveredCell)
    {
        ClearHighlights();

        if (draggedItem == null || hoveredCell == null) return;

        Vector2Int[] positions = _presenter._inventory.GetPositions(draggedItem);
        if (positions == null || positions.Length == 0) return;

        Vector2Int min = positions[0];
        Vector2Int max = positions[0];
        foreach (var p in positions)
        {
            if (p.x < min.x) min.x = p.x;
            if (p.y < min.y) min.y = p.y;
            if (p.x > max.x) max.x = p.x;
            if (p.y > max.y) max.y = p.y;
        }

        int width = max.x - min.x + 1;
        int height = max.y - min.y + 1;

        Vector2Int topLeft = hoveredCell.GridPosition - dragAnchor;

        // Проверяем, свободны ли все клетки с учётом перетаскиваемого предмета
        bool allFree = true;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2Int pos = new Vector2Int(topLeft.x + x, topLeft.y + y);
                if (!IsInsideGrid(pos) || !_presenter._inventory.IsFree(pos, draggedItem))
                {
                    allFree = false;
                    break;
                }
            }
            if (!allFree) break;
        }

        // Подсвечиваем клетки
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2Int pos = new Vector2Int(topLeft.x + x, topLeft.y + y);
                if (!IsInsideGrid(pos)) continue;

                CellView cell = inventoryView._cells[pos.x, pos.y];
                if (allFree)
                    cell.Highlight();
                else
                    cell.SetErrorHighlight();

                highlightedCells.Add(cell);
            }
        }
    }


    private bool IsInsideGrid(Vector2Int pos)
    {
        return inventoryView != null &&
               inventoryView._cells != null &&
               pos.x >= 0 &&
               pos.y >= 0 &&
               pos.x < inventoryView._cells.GetLength(0) &&
               pos.y < inventoryView._cells.GetLength(1);
    }

    private void ClearHighlights()
    {
        foreach (var c in highlightedCells)
        {
            if (c != null)
                c.UnHighlight();
        }

        highlightedCells.Clear();
    }

    // Простейший метод для DragController, чтобы проверить возможность поставить предмет
    private bool CanPlaceItem(Item item, Vector2Int topLeft)
    {
        Vector2Int[] positions = _presenter._inventory.GetPositions(item);
        if (positions == null || positions.Length == 0) return false;

        Vector2Int min = positions[0];
        Vector2Int max = positions[0];
        foreach (var p in positions)
        {
            if (p.x < min.x) min.x = p.x;
            if (p.y < min.y) min.y = p.y;
            if (p.x > max.x) max.x = p.x;
            if (p.y > max.y) max.y = p.y;
        }

        int width = max.x - min.x + 1;
        int height = max.y - min.y + 1;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2Int pos = new Vector2Int(topLeft.x + x, topLeft.y + y);
                if (!IsInsideGrid(pos) || !_presenter._inventory.IsFree(pos, item))
                    return false;
            }
        }

        return true;
    }

}