using Game.Scripts.UI.Equipment;
using UnityEngine;

public class EndDragState : BaseState
{
    public EndDragState(DragFSM fsm) : base(fsm)
    {
    }

    public override void Enter()
    {
        if (TryPlaceInCell())
            return;

        if (TryPlaceInEquipmentSlot())
            return;

        if (IsOverUI())
        {
            ReturnItemToInventory();
            return;
        }

        DropItemToScene();
    }

    public override void Exit()
    {
        _fsm.UnHighlight();
    }

    private bool TryPlaceInCell()
    {
        if (!_fsm.TryGetComponentUnderMouse(out CellView cellView))
            return false;

        Vector2Int targetPosition = cellView.GridPosition - _fsm.DragItemCell;

        if (!cellView.Inventory.AddItem(_fsm.CurrentDragItem.Item.itemData, targetPosition))
            ReturnItemToInventory();

        DestroyItemAndReturnToIdle();
        return true;
    }

    private bool TryPlaceInEquipmentSlot()
    {
        if (!_fsm.TryGetComponentUnderMouse(out EquipmentSlot slot))
            return false;

        if (!slot.AddItem(_fsm.CurrentDragItem))
            ReturnItemToInventory();

        _fsm.CurrentDragItem = null;
        _fsm.SetState<IdleDragState>();

        return true;
    }

    private bool IsOverUI()
    {
        return _fsm.TryGetComponentUnderMouse(out RectTransform _);
    }

    private void ReturnItemToInventory()
    {
        _fsm.StartDragInventory.AddItem(_fsm.CurrentDragItem.Item.itemData);
        DestroyItemAndReturnToIdle();
    }

    private void DropItemToScene()
    {
        DestroyItemAndReturnToIdle();
    }

    private void DestroyItemAndReturnToIdle()
    {
        GameObject.Destroy(_fsm.CurrentDragItem.gameObject);
        _fsm.SetState<IdleDragState>();
    }
}