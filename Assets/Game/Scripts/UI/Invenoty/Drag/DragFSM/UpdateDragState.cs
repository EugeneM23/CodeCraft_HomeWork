using UnityEngine;

public class UpdateDragState : BaseState, ITickable
{
    public UpdateDragState(DragFSM fsm) : base(fsm)
    {
    }

    public void Tick()
    {
        _fsm.CurrenDragItem.transform.position = Input.mousePosition + _fsm.DragOffset;

        Debug.Log(_fsm.GetDragItemCell(_fsm.CurrenDragItem, Input.mousePosition));
        if (Input.GetMouseButtonUp(0))
            _fsm.SetState<EndDragState>();
    }
}