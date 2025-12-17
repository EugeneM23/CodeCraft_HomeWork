using UnityEngine;

public class UpdateDragState : BaseState, ITickable
{
    public UpdateDragState(DragFSM fsm) : base(fsm)
    {
    }

    public void Tick()
    {
        UpdateDragItemPosition();
        UpdateCurrentCell();
        UpdateCurrentInventory();
        
        if (Input.GetMouseButtonUp(0))
            _fsm.SetState<EndDragState>();
    }

    private void UpdateDragItemPosition()
    {
        _fsm.Context.CurrentDragItem.transform.position = Input.mousePosition + _fsm.Context.DragOffset;
    }

    private void UpdateCurrentCell()
    {
        if (!_fsm.TryGetComponentUnderMouse(out CellView hoveredCell)) return;

        _fsm.Context.CurrentDragCell = new Vector2Int(
            hoveredCell.GridPosition.x - _fsm.Context.GrabbedCell.x,
            hoveredCell.GridPosition.y - _fsm.Context.GrabbedCell.y
        );
    }

    private void UpdateCurrentInventory()
    {
        if (!_fsm.TryGetComponentUnderMouse(out BackPack backPack)) return;
        if (_fsm.Context.CurrentInventory == backPack.Inventory) return;

        _fsm.Context.CurrentInventory?.UnHighlight();
        _fsm.Context.CurrentInventory = backPack.Inventory;
    }
}