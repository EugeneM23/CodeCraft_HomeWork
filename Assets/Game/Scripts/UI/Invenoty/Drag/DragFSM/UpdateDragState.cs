using UnityEngine;

public class UpdateDragState : BaseState, ITickable
{
    public UpdateDragState(DragFSM fsm) : base(fsm)
    {
    }

    public void Tick()
    {
        UpdateDragItemPosition();
        UpdateCurrentDragCell();
        UpdateCurrentInventory();
        CheckForDragEnd();
    }

    private void UpdateDragItemPosition()
    {
        if (_fsm.Context.CurrentDragItem != null)
            _fsm.Context.CurrentDragItem.transform.position = Input.mousePosition + _fsm.Context.DragOffset;
    }

    private void UpdateCurrentDragCell()
    {
        if (!_fsm.TryGetComponentUnderMouse(out CellView cell))
            return;

        _fsm.Context.CurrentDragCell = new Vector2Int(
            cell.GridPosition.x - _fsm.Context.GrabbedCell.x,
            cell.GridPosition.y - _fsm.Context.GrabbedCell.y
        );
    }

    private void UpdateCurrentInventory()
    {
        if (!_fsm.TryGetComponentUnderMouse(out BackPack backPack))
            return;

        if (_fsm.Context.CurrentInventory == backPack.Inventory)
            return;

        _fsm.Context.CurrentInventory?.UnHighlight();
        _fsm.Context.CurrentInventory = backPack.Inventory;
    }

    private void CheckForDragEnd()
    {
        if (Input.GetMouseButtonUp(0))
        {
            _fsm.SetState<EndDragState>();
        }
    }
}