using Game.Scripts.UI.Equipment.Game.Equipment.View;
using Inventories.EndDrag;
using UnityEngine;

public class EndDragState : BaseState
{
    public EndDragState(DragFSM fsm) : base(fsm)
    {
    }

    public override void Enter()
    {
        if (_fsm.TryGetComponentUnderMouse(out CellView cell))
        {
            _fsm.SetState<EndDragToInventoryState>();
            return;
        }

        if (_fsm.TryGetComponentUnderMouse(out EquipmentSlotView slot))
        {
            _fsm.SetState<EndDragToEquipmentState>();
            return;
        }

        if (_fsm.TryGetComponentUnderMouse(out RectTransform _))
        {
            _fsm.SetState<EndDragReturnToSourceState>();
            return;
        }

        _fsm.SetState<EndDragToSceneState>();
    }
}