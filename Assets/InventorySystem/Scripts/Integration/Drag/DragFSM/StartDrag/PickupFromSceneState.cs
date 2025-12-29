public class PickupFromSceneState : BaseState
{
    private readonly InventoryFactory _factory;

    public PickupFromSceneState(DragFSM fsm, InventoryFactory factory) : base(fsm)
        => _factory = factory;

    public override void Enter()
    {
        if (!_fsm.TryGetComponentUnderMouse(out SceneItem sceneItem))
        {
            _fsm.SetState<IdleDragState>();
            return;
        }

        if (_fsm.MainPresenter.AddItem(sceneItem.ItemData, sceneItem.Quantity))
            _factory.DeSpawn(sceneItem.gameObject);

        _fsm.SetState<IdleDragState>();
    }
}