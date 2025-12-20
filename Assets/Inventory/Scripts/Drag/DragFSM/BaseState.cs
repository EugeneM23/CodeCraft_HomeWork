public class BaseState : IState
{
    protected readonly DragFSM _fsm;

    protected BaseState(DragFSM fsm) => _fsm = fsm;

    public virtual void Enter()
    {
    }

    public virtual void Exit()
    {
    }
}