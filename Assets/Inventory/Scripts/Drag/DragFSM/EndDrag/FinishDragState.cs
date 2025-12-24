namespace Inventories.EndDrag
{
    public class FinishDragState : BaseState
    {
        private readonly InventoryFactory _factory;

        public FinishDragState(DragFSM fsm, InventoryFactory factory) : base(fsm)
        {
            _factory = factory;
        }

        public override void Enter()
        {
            if (_fsm.Context.CurrentDragItem != null) 
                _factory.DeSpawn(_fsm.Context.CurrentDragItem.gameObject);

            _fsm.Context.Clear();
            _fsm.SetState<IdleDragState>();
        }
    }
}
