using UnityEngine;

public class UpdateDragState : BaseState, ITickable
{
    public UpdateDragState(DragFSM fsm) : base(fsm) { }

    public void Tick()
    {
        Debug.Log("UpdateDragState");
        if (Input.GetMouseButtonUp(0))
            _fsm.SetState<EndDragState>();
    }
}