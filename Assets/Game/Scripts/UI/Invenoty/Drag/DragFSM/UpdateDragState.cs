using UnityEngine;

public class UpdateDragState : BaseState, ITickable
{
    private Vector2Int _currentCell;

    public UpdateDragState(DragFSM fsm) : base(fsm)
    {
    }

    public void Tick()
    {
        _fsm.CurrenDragItem.transform.position = Input.mousePosition + _fsm.DragOffset;

        if (_fsm.TryGetComponentUnderMouse(out CellView cell) && cell.GridPosition != _currentCell)
        {
            _currentCell = cell.GridPosition;
            _fsm.Highlight(_currentCell);
        }

        if (Input.GetMouseButtonUp(0))
            _fsm.SetState<EndDragState>();
    }
}