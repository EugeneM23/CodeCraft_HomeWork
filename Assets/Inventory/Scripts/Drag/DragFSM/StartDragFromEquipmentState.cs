using Game.Scripts.UI.Equipment;
using Inventories;
using UnityEngine;

public class StartDragFromEquipmentState : BaseState
{
    public StartDragFromEquipmentState(DragFSM fsm) : base(fsm)
    {
    }

    public override void Enter()
    {
        if (!_fsm.TryGetComponentUnderMouse(out EquipmentSlot slot) || slot.ItemInstance == null)
        {
            _fsm.SetState<IdleDragState>();
            return;
        }

        RectTransform slotRect = slot.GetComponent<RectTransform>();
        Vector2Int clickedCell = _fsm.CalculateClickedCellInSlot(slotRect, slot.ItemInstance.itemData.Size);

        _fsm.SetupDragContext(slot.ItemInstance, slot.transform.position, _fsm.MainInventory, clickedCell,
            Vector2Int.zero, slot);

        slot.Remove();

        _fsm.SetState<UpdateDragState>();
    }
}