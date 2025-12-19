using Game.Scripts.UI.Equipment;
using Inventories;
using UnityEngine;

public class StartDragState : BaseState
{
    private readonly InventoryFactory _factory;

    public StartDragState(DragFSM fsm, InventoryFactory factory) : base(fsm)
    {
        _factory = factory;
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
        Vector3 itemPosition = cell.InventoryItem.transform.position;
        Vector2Int itemStartCell = cell.Inventory.GetItemGridPositions(itemInstance)[0];

        cell.Inventory.RemoveItem(itemInstance.ID);

        SetupDragContext(itemInstance, itemPosition, cell.Inventory, cell.GridPosition, itemStartCell);

        _fsm.SetState<UpdateDragState>();
        return true;
    }

    private bool TryStartDragFromEquipment()
    {
        if (!_fsm.TryGetComponentUnderMouse(out EquipmentSlot slot)) return false;
        if (slot.ItemInstance == null) return false;

        Vector2Int cellInSlot =
            CalculateClickedCellInSlot(slot, slot.ItemInstance.itemData.Size);

        SetupDragContext(slot.ItemInstance, slot.transform.position, _fsm.OriginInventory, cellInSlot, Vector2Int.zero,
            slot);

        slot.RemoveItem();

        _fsm.SetState<UpdateDragState>();
        return true;
    }

    private bool TryPickupFromScene()
    {
        if (!_fsm.TryGetComponentUnderMouse(out SceneItem sceneItem)) return false;

        if (_fsm.OriginInventory.AddItem(sceneItem.ItemData, sceneItem.Quantity))
            _factory.DeSpawn(sceneItem.gameObject);

        _fsm.SetState<IdleDragState>();
        return true;
    }

    private void SetupDragContext(ItemInstance itemInstance, Vector3 position, Inventory sourceInventory,
        Vector2Int clickedCell, Vector2Int itemStartCell, EquipmentSlot equipmentSlot = null)
    {
        _fsm.Context.CurrentDragItem = _factory.SpawnDragItem(itemInstance);
        _fsm.Context.CurrentDragItem.transform.parent = _fsm.Context.CurrentDragItem.transform.root;
        _fsm.Context.CurrentDragItem.transform.position = position;
        _fsm.Context.SourceInventory = sourceInventory;
        _fsm.Context.StartDragCell = itemStartCell;
        _fsm.Context.DragOffset = position - Input.mousePosition;
        _fsm.Context.GridOffset = new Vector2Int(clickedCell.x - itemStartCell.x, clickedCell.y - itemStartCell.y);
        _fsm.Context.EquipmentSlot = equipmentSlot;
    }

    private Vector2Int CalculateClickedCellInSlot(EquipmentSlot slot, Vector2Int itemSize)
    {
        RectTransform slotRect = slot.GetComponent<RectTransform>();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            slotRect,
            Input.mousePosition,
            null,
            out Vector2 localPoint
        );

        Vector2 rectSize = slotRect.rect.size;
        Vector2 pivot = slotRect.pivot;

        Vector2 normalizedPoint = new Vector2(
            (localPoint.x / rectSize.x) + pivot.x,
            (localPoint.y / rectSize.y) + pivot.y
        );

        int cellX = Mathf.Clamp(Mathf.FloorToInt(normalizedPoint.x * itemSize.x), 0, itemSize.x - 1);
        int cellY = Mathf.Clamp(Mathf.FloorToInt((1f - normalizedPoint.y) * itemSize.y), 0, itemSize.y - 1);


        return new Vector2Int(cellX, cellY);
    }
}