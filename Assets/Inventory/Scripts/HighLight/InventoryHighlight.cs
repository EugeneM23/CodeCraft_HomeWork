using System;
using UnityEngine;

namespace Inventories
{
    public class InventoryHighlight
    {
        private readonly DragFSM _fsm;
        private readonly InventoryView _inventoryView;

        private Vector2Int _currentSelectedCell;
        private Vector2Int[] _highlightedCells = Array.Empty<Vector2Int>();
        private readonly Inventory _inventory;

        public InventoryHighlight(DragFSM fsm, InventoryView inventoryView, Inventory inventory)
        {
            _fsm = fsm;
            _inventory = inventory;
            _inventoryView = inventoryView;
        }

        public void Tick()
        {
            if (!_fsm.Context.IsDragging || _fsm.Context.CurrentDragItem == null || _fsm.Context.CurrentInventory != _inventory)
            {
                ClearHighlight();
                return;
            }

            if (_currentSelectedCell == _fsm.Context.SelectedCell) return;

            _currentSelectedCell = _fsm.Context.SelectedCell;

            Vector2Int startCell = _fsm.Context.SelectedCell;
            Vector2Int itemSize = _fsm.Context.CurrentDragItem.ItemInstance.itemData.Size;

            Vector2Int[] newCells = CalculateCells(startCell, itemSize);

            if (!AreAllCellsValid(newCells))
            {
                ClearHighlight();
                return;
            }

            ClearHighlight();
            _highlightedCells = newCells;
            HighlightCells(_highlightedCells, true);
        }

        private Vector2Int[] CalculateCells(Vector2Int startCell, Vector2Int itemSize)
        {
            Vector2Int[] cells = new Vector2Int[itemSize.x * itemSize.y];
            int index = 0;

            for (int x = 0; x < itemSize.x; x++)
            {
                for (int y = 0; y < itemSize.y; y++)
                {
                    cells[index++] = new Vector2Int(startCell.x + x, startCell.y + y);
                }
            }

            return cells;
        }

        private bool AreAllCellsValid(Vector2Int[] cells)
        {
            foreach (Vector2Int cell in cells)
            {
                if (cell.x < 0 || cell.x >= _inventoryView.Cells.GetLength(0) ||
                    cell.y < 0 || cell.y >= _inventoryView.Cells.GetLength(1))
                {
                    return false;
                }

                if (_inventoryView.Cells[cell.x, cell.y].InventoryItem != null)
                {
                    return false;
                }
            }

            return true;
        }

        private void HighlightCells(Vector2Int[] cells, bool isCorrect)
        {
            foreach (Vector2Int cell in cells)
            {
                _inventoryView.Cells[cell.x, cell.y].Highlight(isCorrect);
            }
        }

        private void ClearHighlight()
        {
            foreach (Vector2Int cell in _highlightedCells)
            {
                if (cell.x >= 0 && cell.x < _inventoryView.Cells.GetLength(0) &&
                    cell.y >= 0 && cell.y < _inventoryView.Cells.GetLength(1))
                {
                    _inventoryView.Cells[cell.x, cell.y].UnHighlight();
                }
            }

            _highlightedCells = new Vector2Int[0];
        }
    }
}