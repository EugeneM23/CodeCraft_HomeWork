using Game.Scripts.UI.Equipment;
using UnityEngine;

public class StartDragState : BaseState
{
    public StartDragState(DragFSM fsm) : base(fsm)
    {
    }

    public override void Enter()
    {
        if (TryStartDragFromCell())
            return;

        if (TryStartDragFromEquipmentSlot())
            return;

        if (TryStartDragFromScene())
            return;

        _fsm.SetState<IdleDragState>();
    }

    private bool TryStartDragFromCell()
    {
        if (!_fsm.TryGetComponentUnderMouse<CellView>(out var cell))
            return false;

        if (cell.InventoryItem == null)
            return false;

        _fsm.CurrenDragItem = cell.InventoryItem;
        _fsm.DragOffset = _fsm.CurrenDragItem.transform.position - Input.mousePosition;
        _fsm.DragItemCell = _fsm.GetDragItemCell(_fsm.CurrenDragItem, Input.mousePosition);
        _fsm.StartDragCell = cell.GridPosition - _fsm.DragItemCell;
        
        _fsm.RemoveItemFromInventory(_fsm.CurrenDragItem);
        _fsm.SetState<UpdateDragState>();
        
        return true;
    }

    private bool TryStartDragFromEquipmentSlot()
    {
        if (!_fsm.TryGetComponentUnderMouse<EquipmentSlot>(out var slot))
            return false;

        _fsm.CurrenDragItem = slot.InventoryItem;
        _fsm.SetState<UpdateDragState>();
        
        return true;
    }

    private bool TryStartDragFromScene()
    {
        if (!_fsm.TryGetComponentUnderMouse<SceneItem>(out var sceneItem))
            return false;

        Debug.Log("Dragging scene");
        
        return true;
    }
}