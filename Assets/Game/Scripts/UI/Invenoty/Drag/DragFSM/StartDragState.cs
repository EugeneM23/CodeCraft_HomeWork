using Game.Scripts.UI.Equipment;
using Inventories;
using UnityEngine;

public class StartDragState : BaseState
{
    public StartDragState(DragFSM fsm) : base(fsm)
    {
    }

    public override void Enter()
    {
        if (TryStartDragFromInventory())
            return;

        if (TryStartDragFromEquipment())
            return;

        if (TryPickupFromScene())
            return;

        _fsm.SetState<IdleDragState>();
    }

    private bool TryStartDragFromInventory()
    {
        if (!_fsm.TryGetComponentUnderMouse(out CellView cell))
            return false;

        if (cell.InventoryItem == null)
            return false;

        ItemInstance itemInstance = cell.InventoryItem.ItemInstance;

        _fsm.Context.CurrentDragItem = _fsm.CreateDragItem(itemInstance);
        _fsm.Context.CurrentDragItem.transform.parent = _fsm.Context.CurrentDragItem.transform.root;
        _fsm.Context.SourceInventory = cell.Inventory;
        _fsm.Context.GrabbedCell = _fsm.GetItemPostion(_fsm.Context.CurrentDragItem);

        cell.Inventory.RemoveItem(itemInstance.ID);

        _fsm.SetState<UpdateDragState>();
        return true;
    }

    private bool TryStartDragFromEquipment()
    {
        if (!_fsm.TryGetComponentUnderMouse(out EquipmentSlot slot))
            return false;

        if (slot.ItemInstance == null)
            return false;

        _fsm.Context.CurrentDragItem = _fsm.CreateDragItem(slot.ItemInstance);
        _fsm.Context.CurrentDragItem.transform.parent = _fsm.Context.CurrentDragItem.transform.root;
        _fsm.Context.SourceInventory = _fsm.OriginInventory;
        _fsm.Context.EquipmentSlot = slot;
        //_fsm.Context.GrabbedCell = _fsm.CalculateGrabbedCell(_fsm.Context.CurrentDragItem, Input.mousePosition);

        slot.RemoveItem();

        _fsm.SetState<UpdateDragState>();
        return true;
    }

    private bool TryPickupFromScene()
    {
        if (!_fsm.TryGetComponentUnderMouse(out SceneItem sceneItem))
            return false;

        if (_fsm.OriginInventory.AddItem(sceneItem.ItemData, sceneItem.Quantity))
            GameObject.Destroy(sceneItem.gameObject);

        _fsm.SetState<IdleDragState>();
        return true;
    }
}