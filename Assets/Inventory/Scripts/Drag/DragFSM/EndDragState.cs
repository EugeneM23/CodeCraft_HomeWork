using Game.Scripts.UI.Equipment;
using Inventories;
using UnityEngine;

public class EndDragState : BaseState
{
    private readonly InventoryFactory _factory;

    public EndDragState(DragFSM fsm, InventoryFactory factory) : base(fsm)
    {
        _factory = factory;
    }

    public override void Enter()
    {
        if (TryPlaceInInventory())
            return;

        if (TryPlaceInEquipmentSlot())
            return;

        if (IsOverUI())
        {
            ReturnToSource();
            FinishDrag();
            return;
        }

        DropToScene();
    }

    private bool TryPlaceInInventory()
    {
        if (!_fsm.TryGetComponentUnderMouse(out CellView cell))
            return false;

        Vector2Int targetPosition = _fsm.Context.SelectedCell;
        ItemInstance draggedItem = _fsm.Context.CurrentDragItem.ItemInstance;

        bool success = cell.Inventory.AddItem(draggedItem.itemData, targetPosition, draggedItem.StackQuantity);

        if (!success)
            ReturnToSource();

        FinishDrag();
        return true;
    }

    private bool TryPlaceInEquipmentSlot()
    {
        if (!_fsm.TryGetComponentUnderMouse(out EquipmentSlot slot))
            return false;
        if (slot.ItemType != _fsm.Context.CurrentDragItem.ItemInstance.itemData.ItemType)
            return false;

        ItemInstance itemInstance = _fsm.Context.CurrentDragItem.ItemInstance;

        if (!slot.AddItem(itemInstance))
        {
            ReturnToSource();
        }

        FinishDrag();
        return true;
    }

    private bool IsOverUI()
    {
        return _fsm.TryGetComponentUnderMouse(out RectTransform _);
    }

    private void ReturnToSource()
    {
        if (_fsm.Context.EquipmentSlot != null)
        {
            _fsm.Context.EquipmentSlot.AddItem(_fsm.Context.CurrentDragItem.ItemInstance);
            return;
        }

        ItemInstance draggedItem = _fsm.Context.CurrentDragItem.ItemInstance;
        _fsm.Context.SourceInventory.AddItem(draggedItem.itemData, _fsm.Context.StartDragCell,
            draggedItem.StackQuantity);
    }

    private void DropToScene()
    {
        if (_fsm.TryGetSceneRaycastHit(out RaycastHit hit))
        {
            ItemInstance draggedItem = _fsm.Context.CurrentDragItem.ItemInstance;
            _factory.SpawnSceneItem(draggedItem.itemData, draggedItem.StackQuantity, hit.point);
            FinishDrag();
        }
        else
        {
            ReturnToSource();
            FinishDrag();
        }
    }

    private void FinishDrag()
    {
        _factory.DeSpawn(_fsm.Context.CurrentDragItem.gameObject);
        _fsm.Context.Clear();
        _fsm.SetState<IdleDragState>();
    }
}