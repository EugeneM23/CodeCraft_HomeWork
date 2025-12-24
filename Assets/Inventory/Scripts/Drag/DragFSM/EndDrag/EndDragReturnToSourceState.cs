namespace Inventories.EndDrag
{
    public class EndDragReturnToSourceState : BaseState
    {
        public EndDragReturnToSourceState(DragFSM fsm) : base(fsm)
        {
        }

        public override void Enter()
        {
            if (_fsm.Context.EquipmentSlotOld != null)
            {
                _fsm.Context.EquipmentSlotOld.EquipItem(_fsm.Context.CurrentDragItem.ItemInstance);
            }
            else
            {
                ItemInstance draggedItem = _fsm.Context.CurrentDragItem.ItemInstance;
                _fsm.Context.SourceInventory.AddItem(
                    draggedItem.itemData, 
                    _fsm.Context.StartDragCell,
                    draggedItem.StackQuantity
                );
            }

            _fsm.SetState<FinishDragState>();
        }
    }
}