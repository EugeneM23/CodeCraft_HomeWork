using UnityEngine;
using System.Collections.Generic;
using Inventories;

public partial class InventoryPresenter
{
    private readonly List<CellView> _highlightedCells = new();

    public void ClearHighlights()
    {
        foreach (var cell in _highlightedCells)
            cell?.UnHighlight();

        _highlightedCells.Clear();
    }

    public void HighlightCells(Item item, Vector2Int currentPosition, Vector2Int matrixPosition,
        CellView currentCell)
    {
        ClearHighlights();
        Vector2Int startPos = currentPosition - matrixPosition;
        int cols = _view.Cells.GetLength(0);
        int rows = _view.Cells.GetLength(1);

        bool IsCorrect = true;
        for (int y = 0; y < item.Size.y; y++)
        for (int x = 0; x < item.Size.x; x++)
        {
            Vector2Int p = new(startPos.x + x, startPos.y + y);
            if (p.x >= 0 && p.y >= 0 && p.x < cols && p.y < rows)
            {
                var cell = _view.Cells[p.x, p.y];

                if (cell.Item != null && cell.Item != item)
                    IsCorrect = false;

                _highlightedCells.Add(cell);
            }
            else
            {
                IsCorrect = false;
            }
        }

        if (currentCell == null)
            IsCorrect = false;


        foreach (CellView cell in _highlightedCells)
            cell.Highlight(IsCorrect);
    }

    private bool IsInsideGrid(Vector2Int cellPos)
    {
        return cellPos.x >= 0 &&
               cellPos.y >= 0 &&
               cellPos.x < _inventory.Count &&
               cellPos.y < _inventory.Height;
    }
}