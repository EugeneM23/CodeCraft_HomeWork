using UnityEngine;

public class InventoryHighlight : MonoBehaviour
{
    [SerializeField] private DragFSM _fsm;
    [SerializeField] private InventoryView _inventoryView;

    private void Update()
    {
        UnhighlightAll();

        if (!_fsm.Context.IsDragging || _fsm.Context.CurrentDragItem == null) return;

        Vector2Int startCell = _fsm.Context.SelectedCell;
        Vector2Int itemSize = _fsm.Context.CurrentDragItem.ItemInstance.itemData.Size;

        for (int x = 0; x < itemSize.x; x++)
        {
            for (int y = 0; y < itemSize.y; y++)
            {
                int cellX = startCell.x + x;
                int cellY = startCell.y + y;

                if (cellX >= 0 && cellX < _inventoryView.Cells.GetLength(0) &&
                    cellY >= 0 && cellY < _inventoryView.Cells.GetLength(1))
                {
                    _inventoryView.Cells[cellX, cellY].Highlight(true);
                }
            }
        }
    }

    private void UnhighlightAll()
    {
        if (_inventoryView.Cells == null) return;

        for (int x = 0; x < _inventoryView.Cells.GetLength(0); x++)
        {
            for (int y = 0; y < _inventoryView.Cells.GetLength(1); y++)
            {
                _inventoryView.Cells[x, y].UnHighlight();
            }
        }
    }
}