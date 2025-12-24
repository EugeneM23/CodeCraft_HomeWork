using Inventories;
using Inventories.EndDrag;
using UnityEngine;

public class EndDragToInventoryState : BaseState
{
    public EndDragToInventoryState(DragFSM fsm) : base(fsm)
    {
    }

    public override void Enter()
    {
        if (!_fsm.TryGetComponentUnderMouse(out CellView cell))
        {
            _fsm.SetState<EndDragReturnToSourceState>();
            return;
        }

        Vector2Int targetPosition = _fsm.Context.SelectedCell;
        ItemInstance draggedItem = _fsm.Context.CurrentDragItem.ItemInstance;

        if (cell.Inventory.AddItem(draggedItem.itemData, targetPosition, draggedItem.StackQuantity))
            _fsm.SetState<FinishDragState>();
        else
            _fsm.SetState<EndDragReturnToSourceState>();
    }
}