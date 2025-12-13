using Game.Scripts.UI.Equipment;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

    private bool TryPlaceInCell()
    {
        if (!_fsm.TryGetComponentUnderMouse(out CellView cellView))
            return false;

        Vector2Int targetPosition = cellView.GridPosition - _fsm.DragItemCell;

        if (!_fsm.AddItemToInventory(_fsm.CurrenDragItem, targetPosition))
            _fsm.AddItemToInventory(_fsm.CurrenDragItem, _fsm.StartDragCell);

        DestroyItemAndReturnToIdle();
        return true;
    }

    private bool TryPlaceInEquipmentSlot()
    {
        if (!_fsm.TryGetComponentUnderMouse(out EquipmentSlot slot))
            return false;

        slot.AddItem(_fsm.CurrenDragItem);
        _fsm.CurrenDragItem = null;
        _fsm.SetState<IdleDragState>();
        return true;
    }

    private bool IsOverUI()
    {
        return _fsm.TryGetComponentUnderMouse(out RectTransform _);
    }

    private void ReturnItemToInventory()
    {
        _fsm.AddItemToInventory(_fsm.CurrenDragItem, _fsm.StartDragCell);
        DestroyItemAndReturnToIdle();
    }

    private void DropItemToScene()
    {
        DestroyItemAndReturnToIdle();
    }

    private void DestroyItemAndReturnToIdle()
    {
        GameObject.Destroy(_fsm.CurrenDragItem.gameObject);
        _fsm.SetState<IdleDragState>();
    }
}