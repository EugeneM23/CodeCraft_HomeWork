using Game.Scripts.UI.Equipment;
using Inventories;
using UnityEngine;

public class StartDragState : BaseState
{
    public StartDragState(DragFSM fsm) : base(fsm)
    {
    }

    public override void Enter()
    {
        if (TryStartDragFromInventory()) return;
        if (TryStartDragFromEquipment()) return;
        if (TryPickupFromScene()) return;

        _fsm.SetState<IdleDragState>();
    }

    private bool TryStartDragFromInventory()
    {
        if (!_fsm.TryGetComponentUnderMouse(out CellView cell)) return false;
        if (cell.InventoryItem == null) return false;

        ItemInstance itemInstance = cell.InventoryItem.ItemInstance;
        Vector3 originalPosition = cell.InventoryItem.transform.position;
        Vector2Int[] itemPositions = cell.Inventory.GetItemGridPositions(itemInstance);
        Vector2Int itemTopLeftCell = itemPositions[0];

        _fsm.Context.StartDragCell = itemTopLeftCell;

        cell.Inventory.RemoveItem(itemInstance.ID);

        CreateDragItem(itemInstance, originalPosition, cell.Inventory);
        CalculateGrabbedOffset(cell.GridPosition, itemTopLeftCell);

        _fsm.SetState<UpdateDragState>();
        return true;
    }

    private bool TryStartDragFromEquipment()
    {
        if (!_fsm.TryGetComponentUnderMouse(out EquipmentSlot slot)) return false;
        if (slot.ItemInstance == null) return false;

        slot.RemoveItem();

        CreateDragItem(slot.ItemInstance, Input.mousePosition, _fsm.OriginInventory);
        _fsm.Context.EquipmentSlot = slot;
        _fsm.Context.SelectedCell = Vector2Int.zero;
        _fsm.Context.DragOffset = Vector2.zero;

        _fsm.SetState<UpdateDragState>();
        return true;
    }

    private bool TryPickupFromScene()
    {
        if (!_fsm.TryGetComponentUnderMouse(out SceneItem sceneItem)) return false;

        if (_fsm.OriginInventory.AddItem(sceneItem.ItemData, sceneItem.Quantity))
            GameObject.Destroy(sceneItem.gameObject);

        _fsm.SetState<IdleDragState>();
        return true;
    }

    private void CreateDragItem(ItemInstance itemInstance, Vector3 position, Inventory sourceInventory)
    {
        _fsm.Context.CurrentDragItem = _fsm.CreateDragItem(itemInstance);
        _fsm.Context.CurrentDragItem.transform.parent = _fsm.Context.CurrentDragItem.transform.root;
        _fsm.Context.CurrentDragItem.transform.position = position;
        _fsm.Context.SourceInventory = sourceInventory;
        _fsm.Context.DragOffset = position - Input.mousePosition;
    }

    private void CalculateGrabbedOffset(Vector2Int clickedCell, Vector2Int itemTopLeftCell)
    {
        _fsm.Context.GridOffset = new Vector2Int(
            clickedCell.x - itemTopLeftCell.x,
            clickedCell.y - itemTopLeftCell.y
        );
    }
}