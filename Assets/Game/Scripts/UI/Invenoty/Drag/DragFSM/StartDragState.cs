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

    public override void Exit()
    {
        if (_fsm.CurrentDragItem != null)
            _fsm.CurrentDragItem.DisableBackGround();
    }

    private bool TryStartDragFromCell()
    {
        if (!_fsm.TryGetComponentUnderMouse(out CellView cell))
            return false;

        if (cell.InventoryItem == null)
            return false;

        _fsm.CurrentDragItem = cell.InventoryItem;
        _fsm.DragOffset = _fsm.CurrentDragItem.transform.position - Input.mousePosition;
        _fsm.DragItemCell = _fsm.GetDragItemCell(_fsm.CurrentDragItem, Input.mousePosition);
        _fsm.StartDragInventory = cell.Inventory;
        _fsm.CurrentDragItem.transform.parent = _fsm.CurrentDragItem.transform.root;

        cell.Inventory.RemoveItem(_fsm.CurrentDragItem.Item.uniqueId);
        _fsm.SetState<UpdateDragState>();

        return true;
    }

    private bool TryStartDragFromEquipmentSlot()
    {
        if (!_fsm.TryGetComponentUnderMouse(out EquipmentSlot slot))
            return false;

        _fsm.CurrentDragItem = slot.InventoryItem;
        _fsm.CurrentDragItem.transform.parent = _fsm.CurrentDragItem.transform.root;
        _fsm.DragOffset = _fsm.CurrentDragItem.transform.position - Input.mousePosition;
        _fsm.DragItemCell = _fsm.GetDragItemCell(_fsm.CurrentDragItem, Input.mousePosition);

        slot.RemoveItem();
        _fsm.SetState<UpdateDragState>();

        return true;
    }

    private bool TryStartDragFromScene()
    {
        if (_fsm.TryGetComponentUnderMouse(out SceneItem sceneItem))
            if (_fsm.OriginInventory.AddItem(sceneItem.ItemData))
                GameObject.Destroy(sceneItem.gameObject);

        _fsm.SetState<IdleDragState>();
        return true;
    }
}