using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UpdateDragState : BaseState, ITickable
{
    public UpdateDragState(DragFSM fsm, GraphicRaycaster raycaster, EventSystem eventSystem) : base(fsm, raycaster,
        eventSystem)
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