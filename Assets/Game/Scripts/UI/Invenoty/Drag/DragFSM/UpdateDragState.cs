using UnityEngine;

public class UpdateDragState : BaseState, ITickable
{
    private Vector2Int _lastHoveredCell = new(-1, -1);

    public UpdateDragState(DragFSM fsm) : base(fsm) { }

    public void Tick()
    {
        UpdateDragItemPosition();
        UpdateHighlight();
        UpdateCurrentInventory();
        CheckForDragEnd();
    }

    private void UpdateDragItemPosition()
    {
        if (_fsm.Context.CurrentDragItem != null)
        {
            _fsm.Context.CurrentDragItem.transform.position = Input.mousePosition;
        }
    }

    private void UpdateHighlight()
    {
        if (!_fsm.TryGetComponentUnderMouse(out CellView cell))
            return;

        if (cell.GridPosition != _lastHoveredCell)
        {
            _lastHoveredCell = cell.GridPosition;
            _fsm.HighlightCells(_lastHoveredCell);
        }
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

    public override void Exit()
    {
        _lastHoveredCell = new Vector2Int(-1, -1);
    }
}