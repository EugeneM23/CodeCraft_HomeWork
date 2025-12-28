using Inventories;
using Inventories.EndDrag;
using UnityEngine;

public class EndDragToSceneState : BaseState
{
    private readonly InventoryFactory _factory;

    public EndDragToSceneState(DragFSM fsm, InventoryFactory factory) : base(fsm)
    {
        _factory = factory;
    }

    public override void Enter()
    {
        if (!_fsm.TryGetSceneRaycastHit(out RaycastHit hit))
        {
            _fsm.SetState<EndDragReturnToSourceState>();
            return;
        }

        Item draggedItem = _fsm.Context.CurrentDragItem.Item;
        _factory.SpawnSceneItem(draggedItem.itemData, draggedItem.StackQuantity, hit.point);
        
        _fsm.SetState<FinishDragState>();
    }
}