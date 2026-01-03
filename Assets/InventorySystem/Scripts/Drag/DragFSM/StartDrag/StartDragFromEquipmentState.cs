using Game.Scripts.UI.Equipment;
using Game.Scripts.UI.Equipment.Game.Equipment.View;
using Inventories;
using UnityEngine;

public class StartDragFromEquipmentState : BaseState
{
    public StartDragFromEquipmentState(DragFSM fsm) : base(fsm)
    {
    }

    public override void Enter()
    {
        if (!_fsm.TryGetComponentUnderMouse(out EquipmentSlotView slot) || slot.CurrentItem == null)
        {
            _fsm.SetState<IdleDragState>();
            return;
        }

        RectTransform slotRect = slot.GetComponent<RectTransform>();
        Vector2Int clickedCell = _fsm.CalculateClickedCellInSlot(slotRect, slot.CurrentItem.itemData.Size);

        _fsm.SetupDragContext(slot.CurrentItem, slot.transform.position, _fsm.MainPresenter, clickedCell,
            Vector2Int.zero, slot);

        slot.UnEquip();

        _fsm.SetState<UpdateDragState>();
    }
}