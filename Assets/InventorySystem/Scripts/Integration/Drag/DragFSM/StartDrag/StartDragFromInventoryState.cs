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

        ItemInstance itemInstance = cell.InventoryItem.ItemInstance;
        Vector3 itemPosition = cell.InventoryItem.transform.position;
        Vector2Int itemStartCell = cell.Inventory.GetItemGridPositions(itemInstance)[0];

        RectTransform cellRect = cell.InventoryItem.GetComponent<RectTransform>();
        Vector2Int clickedCell = _fsm.CalculateClickedCellInSlot(cellRect, itemInstance.itemData.Size);
        
        Vector2Int adjustedClickedCell =
            new Vector2Int(itemStartCell.x + clickedCell.x, itemStartCell.y + clickedCell.y);

        cell.Inventory.RemoveItem(itemInstance.ID);

        _fsm.SetupDragContext(itemInstance, itemPosition, cell.Inventory, adjustedClickedCell, itemStartCell);

        _fsm.SetState<UpdateDragState>();
    }
}