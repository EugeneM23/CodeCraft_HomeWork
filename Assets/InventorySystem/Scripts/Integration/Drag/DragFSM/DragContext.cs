using Game.Scripts.UI.Equipment;
using Game.Scripts.UI.Equipment.Game.Equipment.View;
using Inventories;
using UnityEngine;

public class DragContext
{
    public DragItem CurrentDragItem { get; set; }
    public InventoryPresenter SourceInventory { get; set; }
    public InventoryPresenter CurrentInventoryPresenter { get; set; }
    public Vector2Int SelectedCell { get; set; }
    public Vector2Int StartDragCell { get; set; }
    public Vector2Int GridOffset { get; set; }
    public Vector3 DragOffset { get; set; }

    public bool IsDragging => CurrentDragItem != null;
    public EquipmentSlotView EquipmentSlotOld { get; set; }
    public InventoryItem CurrentItemUnderMouse { get; set; }

    public void Clear()
    {
        GridOffset = Vector2Int.zero;
        DragOffset = Vector3.zero;
        StartDragCell = Vector2Int.zero;
        EquipmentSlotOld = null;
        CurrentDragItem = null;
        SourceInventory = null;
        CurrentInventoryPresenter = null;
        SelectedCell = Vector2Int.zero;
        CurrentItemUnderMouse = null;
    }
}