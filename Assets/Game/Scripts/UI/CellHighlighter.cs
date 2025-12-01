using UnityEngine;
using System.Collections.Generic;
using Inventories;

public class CellHighlighter : MonoBehaviour
{
    [SerializeField] private InventoryView _inventoryView;
    [SerializeField] private InventoryPresenter _presenter;

    private readonly List<CellView> _highlightedCells = new();

    public void HighlightCells(Item draggedItem, CellView hoveredCell, Vector2Int dragAnchor)
    {
        ClearHighlights();

        if (draggedItem == null || hoveredCell == null) return;

        Vector2Int[] positions = _presenter._inventory.GetPositions(draggedItem);
        if (positions == null || positions.Length == 0) return;

        (int width, int height) = CalculateItemDimensions(positions);
        Vector2Int topLeft = hoveredCell.GridPosition - dragAnchor;
        bool canPlace = CanPlaceItemAt(draggedItem, topLeft, width, height);

        HighlightArea(topLeft, width, height, canPlace);
    }

    public (int width, int height) GetItemDimensions(Item item)
    {
        Vector2Int[] positions = _presenter._inventory.GetPositions(item);
        if (positions == null || positions.Length == 0) return (0, 0);

        return CalculateItemDimensions(positions);
    }


    public void ClearHighlights()
    {
        foreach (var cell in _highlightedCells)
            cell?.UnHighlight();

        _highlightedCells.Clear();
    }

    public bool CanPlaceItemAt(Item item, Vector2Int topLeft, int width, int height)
    {
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Vector2Int pos = new(topLeft.x + x, topLeft.y + y);
                if (!IsInsideGrid(pos) || !_presenter._inventory.IsFree(pos, item))
                    return false;
            }

        return true;
    }

    private (int width, int height) CalculateItemDimensions(Vector2Int[] positions)
    {
        Vector2Int min = positions[0];
        Vector2Int max = positions[0];

        foreach (var pos in positions)
        {
            min.x = Mathf.Min(min.x, pos.x);
            min.y = Mathf.Min(min.y, pos.y);
            max.x = Mathf.Max(max.x, pos.x);
            max.y = Mathf.Max(max.y, pos.y);
        }

        return (max.x - min.x + 1, max.y - min.y + 1);
    }


    private void HighlightArea(Vector2Int topLeft, int width, int height, bool isValid)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2Int pos = new Vector2Int(topLeft.x + x, topLeft.y + y);
                if (!IsInsideGrid(pos)) continue;

                CellView cell = _inventoryView._cells[pos.x, pos.y];
                
                if (isValid)
                    cell.Highlight();
                else
                    cell.SetErrorHighlight();

                _highlightedCells.Add(cell);
            }
        }
    }

    private bool IsInsideGrid(Vector2Int pos)
    {
        return pos.x >= 0 && pos.y >= 0 &&
               pos.x < _inventoryView._cells.GetLength(0) &&
               pos.y < _inventoryView._cells.GetLength(1);
    }
}