using UnityEngine.EventSystems;
using UnityEngine.UI;

public abstract class BaseState : IState
{
    protected readonly DragFSM _fsm;
    protected GraphicRaycaster _raycaster;
    protected EventSystem _eventSystem;

    public BaseState(DragFSM fsm, GraphicRaycaster raycaster, EventSystem eventSystem)
    {
        _fsm = fsm;
        _raycaster = raycaster;
        _eventSystem = eventSystem;
    }

    public virtual void Enter() { }
    public virtual void Exit() { }
}