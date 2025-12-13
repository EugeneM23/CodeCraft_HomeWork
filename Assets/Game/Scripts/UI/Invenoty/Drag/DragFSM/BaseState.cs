public abstract class BaseState : IState
{
    protected readonly DragFSM _fsm;

    public BaseState(DragFSM fsm) => _fsm = fsm;

    public virtual void Enter() { }
    public virtual void Exit() { }
}