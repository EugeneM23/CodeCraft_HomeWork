using Game.Scripts.UI.Equipment;
using Inventories;
using UnityEngine;

public class DragContext
{
    public DragItem CurrentDragItem { get; set; }
    public Inventory SourceInventory { get; set; }
    public Inventory CurrentInventory { get; set; }
    public Vector2Int SelectedCell { get; set; }
    public Vector2Int StartDragCell { get; set; }
    public Vector2Int GridOffset { get; set; }
    public Vector3 DragOffset { get; set; }

    public bool IsDragging => CurrentDragItem != null;
    public EquipmentSlot EquipmentSlot { get; set; }

    public void Clear()
    {
        GridOffset = Vector2Int.zero;
        DragOffset = Vector3.zero;
        StartDragCell = Vector2Int.zero;
        EquipmentSlot = null;
        CurrentDragItem = null;
        SourceInventory = null;
        CurrentInventory = null;
        SelectedCell = Vector2Int.zero;
    }
}