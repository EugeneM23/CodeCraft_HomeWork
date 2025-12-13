using UnityEngine;

public class UpdateDragState : BaseState, ITickable
{
    public UpdateDragState(DragFSM fsm) : base(fsm)
    {
    }

    public void Tick()
    {
        _fsm.CurrenDragItem.transform.position = Input.mousePosition + _fsm.DragOffset;

        if (_fsm.TryGetComponentUnderMouse(out CellView cell))
            _fsm.Highlight(cell.GridPosition);


        if (Input.GetMouseButtonUp(0))
            _fsm.SetState<EndDragState>();
    }
}