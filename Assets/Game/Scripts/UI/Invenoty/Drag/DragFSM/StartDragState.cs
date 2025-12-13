using UnityEngine;

public class StartDragState : BaseState
{
    public StartDragState(DragFSM fsm) : base(fsm)
    {
    }

    public override void Enter()
    {
        Debug.Log("StartDrag");
        _fsm.SetState<UpdateDragState>();
    }
}