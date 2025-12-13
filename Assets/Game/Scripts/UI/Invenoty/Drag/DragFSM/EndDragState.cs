using UnityEngine;

public class EndDragState : BaseState
{
    public EndDragState(DragFSM fsm) : base(fsm)
    {
    }

    public override void Enter()
    {
        Debug.Log("EndDrag");
        _fsm.SetState<IdleDragState>();
    }
}