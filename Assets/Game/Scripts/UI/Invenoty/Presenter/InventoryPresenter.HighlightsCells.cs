using Inventories;
using UnityEngine;

public partial class InventoryPresenter
{
    public void ClearHighlights()
    {
        foreach (var cell in _highlightedCells)
            cell?.UnHighlight();

        _highlightedCells.Clear();
    }

    public void HighlightCells(Item item, Vector2Int currentPosition, Vector2Int matrixPosition, CellView currentCell)
    {
        ClearHighlights();

        Vector2Int startPos = currentPosition - matrixPosition;
        bool isCorrect = !(currentCell == null || !CanAddItem(item, startPos));
        Debug.Log(currentCell == null);
        for (int y = 0; y < item.Size.y; y++)
        {
            for (int x = 0; x < item.Size.x; x++)
            {
                Vector2Int pos = new(startPos.x + x, startPos.y + y);

                if (pos.x >= 0 && pos.y >= 0 && pos.x < _view.Cells.GetLength(0) && pos.y < _view.Cells.GetLength(1))
                {
                    var cell = _view.Cells[pos.x, pos.y];
                    _highlightedCells.Add(cell);
                }
                else
                {
                    isCorrect = false;
                }
            }
        }

        foreach (CellView cell in _highlightedCells)
            cell.Highlight(isCorrect);
    }
}