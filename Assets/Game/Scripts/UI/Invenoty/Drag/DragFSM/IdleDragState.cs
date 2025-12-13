using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class IdleDragState : BaseState, ITickable
{
    public IdleDragState(DragFSM fsm) : base(fsm)
    {
    }

    public void Tick()
    {
        Debug.Log("IdleDrag");
        if (Input.GetMouseButtonDown(0))
        {
            _fsm.SetState<StartDragState>();
        }
    }
}