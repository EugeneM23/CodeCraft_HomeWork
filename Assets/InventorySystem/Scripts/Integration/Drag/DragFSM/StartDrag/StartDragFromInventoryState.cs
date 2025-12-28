using Inventories;
using UnityEngine;

public class StartDragFromInventoryState : BaseState
{
    public StartDragFromInventoryState(DragFSM fsm) : base(fsm)
    {
    }

    public override void Enter()
    {
        if (!_fsm.TryGetComponentUnderMouse(out CellView cell) || cell.InventoryItem == null)
        {
            _fsm.SetState<IdleDragState>();
            return;
        }

        Item item = cell.InventoryItem.Item;
        Vector3 itemPosition = cell.InventoryItem.transform.position;
        Vector2Int itemStartCell = cell.Inventory.GetItemGridPositions(item)[0];

        RectTransform cellRect = cell.InventoryItem.GetComponent<RectTransform>();
        Vector2Int clickedCell = _fsm.CalculateClickedCellInSlot(cellRect, item.itemData.Size);
        
        Vector2Int adjustedClickedCell =
            new Vector2Int(itemStartCell.x + clickedCell.x, itemStartCell.y + clickedCell.y);

        cell.Inventory.RemoveItem(item.ID);

        _fsm.SetupDragContext(item, itemPosition, cell.Inventory, adjustedClickedCell, itemStartCell);

        _fsm.SetState<UpdateDragState>();
    }
}