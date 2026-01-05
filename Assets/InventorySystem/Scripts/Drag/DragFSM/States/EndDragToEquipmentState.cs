using Game.Scripts.UI.Equipment.Game.Equipment.View;
using Inventories;
using Inventories.EndDrag;
using UnityEngine;

public class EndDragToEquipmentState : BaseState
{
    public EndDragToEquipmentState(DragFSM fsm) : base(fsm)
    {
    }

    public override void Enter()
    {
        if (!_fsm.TryGetComponentUnderMouse(out EquipmentSlotView slot))
        {
            _fsm.SetState<EndDragReturnToSourceState>();
            return;
        }

        Item item = _fsm.Context.CurrentDragItem.Item;

        if (slot.ItemType == item.itemData.ItemType)
        {
            slot.DropItem(item);
            _fsm.SetState<FinishDragState>();
        }
        else
        {
            _fsm.SetState<EndDragReturnToSourceState>();
        }
    }
}