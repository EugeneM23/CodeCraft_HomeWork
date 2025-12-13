using Game.Scripts.UI.Equipment;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EndDragState : BaseState
{
    public EndDragState(DragFSM fsm, GraphicRaycaster raycaster, EventSystem eventSystem) : base(fsm, raycaster,
        eventSystem)
    {
    }

    public override void Enter()
    {
        if (_fsm.TryGetComponentUnderMouse(out CellView cellView))
        {
            if (!_fsm.AddItemToInventory(_fsm.CurrenDragItem, cellView.GridPosition - _fsm.DragItemCell))
                _fsm.AddItemToInventory(_fsm.CurrenDragItem, _fsm.StartDragCell);

            GameObject.Destroy(_fsm.CurrenDragItem.gameObject);
        }

        if (_fsm.TryGetComponentUnderMouse(out EquipmentSlot slot))
        {
            slot.AddItem(_fsm.CurrenDragItem.Item.itemData);
            GameObject.Destroy(_fsm.CurrenDragItem.gameObject);
        }

        GameObject.Destroy(_fsm.CurrenDragItem.gameObject);
        _fsm.SetState<IdleDragState>();
    }
}