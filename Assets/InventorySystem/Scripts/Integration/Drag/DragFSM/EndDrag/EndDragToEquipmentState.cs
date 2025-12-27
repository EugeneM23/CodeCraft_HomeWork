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

        ItemInstance itemInstance = _fsm.Context.CurrentDragItem.ItemInstance;

        if (slot.ItemType == itemInstance.itemData.ItemType)
        {
            slot.DropItem(itemInstance);
            _fsm.SetState<FinishDragState>();
        }
        else
        {
            _fsm.SetState<EndDragReturnToSourceState>();
        }
    }
}