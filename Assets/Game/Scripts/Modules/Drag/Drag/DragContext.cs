using Equipment;
using Inventories;
using UnityEngine;
using UnityEngine.UI;

public class DragContext
{
    public Item Item { get; private set; }
    public Vector2Int StartPosition { get; private set; }
    public Vector2Int ClickOffset { get; private set; }
    public Vector2 DragOffset { get; private set; }
    public bool IsDragging { get; private set; }
    public InventoryCell CurrentInventoryCell { get; set; }
    public InventoryCell StartCell { get; set; }
    public Image EquipmentSlotImage { get; set; }

    public void BeginDrag(Item item, Vector2Int startPosition, Vector2Int clickOffset, Vector2 dragOffset,
        InventoryCell cell = null, Image equipmentSlotImage = null)
    {
        Item = item;
        StartPosition = startPosition;
        ClickOffset = clickOffset;
        DragOffset = dragOffset;
        IsDragging = true;
        StartCell = cell;
        EquipmentSlotImage = equipmentSlotImage;
    }

    public void UpdateCurrentCell(InventoryCell inventoryCell)
    {
        CurrentInventoryCell = inventoryCell;
    }

    public void Reset()
    {
        Item = null;
        StartPosition = default;
        ClickOffset = default;
        DragOffset = default;
        IsDragging = false;
        CurrentInventoryCell = null;
        StartCell = null;
        EquipmentSlotImage = null;
    }
}