using Game.Scripts.UI.Equipment;
using Inventories;
using UnityEngine;

public class EndDragState : BaseState
{
    public EndDragState(DragFSM fsm) : base(fsm)
    {
    }

    public override void Enter()
    {
        if (TryPlaceInInventory())
            return;

        if (TryPlaceInEquipmentSlot())
            return;

        if (IsOverUI())
        {
            ReturnToSource();
            FinishDrag();
            return;
        }

        DropToScene();
    }

    public override void Exit()
    {
        _fsm.UnhighlightCells();
    }

    private bool TryPlaceInInventory()
    {
        if (!_fsm.TryGetComponentUnderMouse(out CellView cell))
            return false;

        Vector2Int targetPosition = cell.GridPosition - _fsm.Context.GrabbedCell;
        ItemInstance draggedItem = _fsm.Context.CurrentDragItem.ItemInstance;

        bool success = cell.Inventory.AddItem(draggedItem.itemData, targetPosition, draggedItem.StackQuantity);

        if (!success)
            ReturnToSource();

        FinishDrag();
        return true;
    }

    private bool TryPlaceInEquipmentSlot()
    {
        if (!_fsm.TryGetComponentUnderMouse(out EquipmentSlot slot))
            return false;

        ItemInstance draggedItem = _fsm.Context.CurrentDragItem.ItemInstance;

        if (!slot.AddItem(draggedItem))
        {
            ReturnToSource();
        }

        FinishDrag();
        return true;
    }

    private bool IsOverUI()
    {
        return _fsm.TryGetComponentUnderMouse(out RectTransform _);
    }

    private void ReturnToSource()
    {
        if (_fsm.Context.EquipmentSlot != null)
        {
            _fsm.Context.EquipmentSlot.AddItem(_fsm.Context.CurrentDragItem.ItemInstance);
            return;
        }

        ItemInstance draggedItem = _fsm.Context.CurrentDragItem.ItemInstance;
        _fsm.Context.SourceInventory.AddItem(draggedItem.itemData, draggedItem.StackQuantity);
    }

    private void DropToScene()
    {
        if (_fsm.TryGetSceneRaycastHit(out RaycastHit hit))
        {
            ItemInstance draggedItem = _fsm.Context.CurrentDragItem.ItemInstance;
            _fsm.ItemSpawner.SpawnItem(draggedItem.itemData, draggedItem.StackQuantity, hit.point);
            FinishDrag();
        }
        else
        {
            ReturnToSource();
            FinishDrag();
        }
    }

    private void FinishDrag()
    {
        GameObject.Destroy(_fsm.Context.CurrentDragItem.gameObject);

        _fsm.Context.Clear();
        _fsm.SetState<IdleDragState>();
    }
}